using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ProxyBase
{
    public class Client
    {
        public byte Seed { get; set; }
        public byte[] Key { get; set; }
        public byte[] KeyTable { get; set; }

        private volatile bool connected;
        public bool Connected { get { return connected; } private set { connected = value; } }
        public Server Server { get; private set; }

        public ClientTab Tab { get; private set; }

        public Thread ClientLoopThread { get; private set; }

        public Socket ClientSocket { get; private set; }
        public Socket ServerSocket { get; private set; }

        private volatile bool clientReceiving = false;
        private volatile bool serverReceiving = false;

        private byte[] clientBuffer = new byte[65535];
        private byte[] serverBuffer = new byte[65535];

        // Receive reassembly buffers. Offset/length framing (compacted once per receive)
        // instead of List<byte> + RemoveRange, so draining N packets is O(n), not O(n^2).
        private byte[] clientAccum = new byte[65535];
        private int clientAccumLength = 0;
        private byte[] serverAccum = new byte[65535];
        private int serverAccumLength = 0;

        // Safety cap on the reassembly buffer. The largest legal frame is 65538 bytes
        // (0xAA + u16 length + body), so a leftover beyond this means the stream is
        // garbage or hostile -- drop the connection rather than buffer forever.
        private const int MaxReassemblyBuffer = 1 << 17; // 128 KB

        private byte clientOrdinal = 0x00;
        private byte serverOrdinal = 0x00;

        private Queue<ServerPacket> clientSendQueue = new Queue<ServerPacket>();
        private Queue<ClientPacket> serverSendQueue = new Queue<ClientPacket>();

        private Queue<ClientPacket> clientProcessQueue = new Queue<ClientPacket>();
        private Queue<ServerPacket> serverProcessQueue = new Queue<ServerPacket>();

        public Client(Server server, Socket socket, EndPoint endPoint)
        {
            this.Server = server;
            this.ClientSocket = socket;
            this.ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.ServerSocket.Connect(endPoint);
            this.Tab = new ClientTab(this);

            // Roomier kernel receive buffers so a burst (busy area, GM event) doesn't
            // outrun the reassembly path.
            this.ClientSocket.ReceiveBufferSize = 1 << 19;
            this.ServerSocket.ReceiveBufferSize = 1 << 19;

            this.Key = Encoding.ASCII.GetBytes("UrkcnItnI");
            this.KeyTable = new byte[1024];

            this.ClientLoopThread = new Thread(new ThreadStart(ClientLoop));
            this.ClientLoopThread.IsBackground = true;
            this.ClientLoopThread.Start();
        }

        public void ClientLoop()
        {
            Connected = true;

            while (Connected)
            {
                lock (Program.SyncObj)
                {
                    try
                    {
                        ClientReceive();
                        ClientProcess();
                        ClientDequeue();

                        ServerReceive();
                        ServerProcess();
                        ServerDequeue();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex);
                        Connected = false;
                    }
                }

                Thread.Sleep(1);
            }

            lock (Program.SyncObj)
            {
                if (ClientSocket.Connected)
                    ClientSocket.Close();

                if (ServerSocket.Connected)
                    ServerSocket.Close();

                Server.Clients.Remove(this);
                Server.Form.RemoveTab(Tab);
            }
        }

        #region Networking
        public void ClientReceive()
        {
            if (Connected && !clientReceiving)
            {
                clientReceiving = true;
                ClientSocket.BeginReceive(clientBuffer, 0, clientBuffer.Length, SocketFlags.None, ClientEndReceive, this);
            }
        }
        public void ServerReceive()
        {
            if (Connected && !serverReceiving)
            {
                serverReceiving = true;
                ServerSocket.BeginReceive(serverBuffer, 0, serverBuffer.Length, SocketFlags.None, ServerEndReceive, this);
            }
        }

        public void ClientProcess()
        {
            lock (Program.SyncObj)
            {
                while (clientProcessQueue.Count > 0)
                {
                    var msg = clientProcessQueue.Dequeue();
                    if (Server.ClientMessageHandlers[msg.Opcode].Invoke(this, msg))
                    {
                        Enqueue(msg);
                        Tab.LogOutgoingPacket("Send> {0}", msg);
                    }
                }
            }
        }
        public void ServerProcess()
        {
            lock (Program.SyncObj)
            {
                while (serverProcessQueue.Count > 0)
                {
                    var msg = serverProcessQueue.Dequeue();
                    if (Server.ServerMessageHandlers[msg.Opcode].Invoke(this, msg))
                    {
                        Enqueue(msg);
                        Tab.LogIncomingPacket("Recv> {0}", msg);
                    }
                }
            }
        }

        public void ClientDequeue()
        {
            lock (Program.SyncObj)
            {
                while (clientSendQueue.Count > 0)
                {
                    var msg = clientSendQueue.Dequeue();

                    if (msg.ShouldEncrypt)
                    {
                        msg.Ordinal = clientOrdinal++;
                        msg.Encrypt(this);
                    }

                    msg.Length = (ushort)(msg.BodyData.Length + (msg.Header.Length - 3));
                    var buffer = new byte[msg.Header.Length + msg.BodyData.Length];
                    Array.Copy(msg.Header, 0, buffer, 0, msg.Header.Length);
                    Array.Copy(msg.BodyData, 0, buffer, msg.Header.Length, msg.BodyData.Length);

                    ClientSocket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, ClientEndSend, this);
                }
            }
        }
        public void ServerDequeue()
        {
            lock (Program.SyncObj)
            {
                while (serverSendQueue.Count > 0)
                {
                    var msg = serverSendQueue.Dequeue();

                    if (msg.Opcode == 0x39 || msg.Opcode == 0x3A)
                    {
                        msg.EncryptDialog();
                    }

                    if (msg.ShouldEncrypt)
                    {
                        msg.Ordinal = serverOrdinal++;
                        msg.Encrypt(this);
                    }

                    msg.Length = (ushort)(msg.BodyData.Length + (msg.Header.Length - 3));
                    var buffer = new byte[msg.Header.Length + msg.BodyData.Length];
                    Array.Copy(msg.Header, 0, buffer, 0, msg.Header.Length);
                    Array.Copy(msg.BodyData, 0, buffer, msg.Header.Length, msg.BodyData.Length);

                    ServerSocket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, ServerEndSend, this);
                }
            }
        }

        public void Enqueue(ClientPacket msg)
        {
            lock (Program.SyncObj)
            {
                serverSendQueue.Enqueue(msg);
            }
        }
        public void Enqueue(ServerPacket msg)
        {
            lock (Program.SyncObj)
            {
                clientSendQueue.Enqueue(msg);
            }
        }

        static void ClientEndSend(IAsyncResult ar)
        {
            var client = (Client)ar.AsyncState;

            try
            {
                client.ClientSocket.EndSend(ar);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                client.Connected = false;
            }
        }
        static void ServerEndSend(IAsyncResult ar)
        {
            var client = (Client)ar.AsyncState;

            try
            {
                client.ServerSocket.EndSend(ar);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                client.Connected = false;
            }
        }

        static void ClientEndReceive(IAsyncResult ar)
        {
            var client = (Client)ar.AsyncState;

            try
            {
                var count = client.ClientSocket.EndReceive(ar);
                if (count == 0)
                {
                    client.Connected = false; // peer closed the connection
                    return;
                }

                AppendBytes(ref client.clientAccum, ref client.clientAccumLength, client.clientBuffer, count);

                int start = 0;
                int length = client.clientAccumLength;
                while (length - start >= 3)
                {
                    if (client.clientAccum[start] != 0xAA)
                    {
                        // Desync: skip to the next signature byte instead of dropping the
                        // whole connection -- one stray byte shouldn't end the relay.
                        System.Diagnostics.Debug.WriteLine("[client] resync: skipping unframed byte(s)");
                        int sig = IndexOfSignature(client.clientAccum, start + 1, length);
                        if (sig < 0) { start = length; break; }
                        start = sig;
                        continue;
                    }

                    int frameLength = ((client.clientAccum[start + 1] << 8) | client.clientAccum[start + 2]) + 3;
                    if (length - start < frameLength)
                        break; // partial frame; wait for the rest

                    var data = new byte[frameLength];
                    Buffer.BlockCopy(client.clientAccum, start, data, 0, frameLength);
                    start += frameLength;

                    try
                    {
                        var msg = new ClientPacket(data);

                        if (msg.ShouldEncrypt)
                            msg.Decrypt(client);

                        if (msg.Opcode == 0x39 || msg.Opcode == 0x3A)
                            msg.DecryptDialog();

                        lock (Program.SyncObj)
                            client.clientProcessQueue.Enqueue(msg);
                    }
                    catch (Exception ex)
                    {
                        // A single malformed packet is logged and skipped, not fatal.
                        System.Diagnostics.Debug.WriteLine(ex);
                    }
                }

                // Keep any leftover (partial frame) bytes for the next receive.
                client.clientAccumLength = length - start;
                if (start > 0 && client.clientAccumLength > 0)
                    Array.Copy(client.clientAccum, start, client.clientAccum, 0, client.clientAccumLength);

                if (client.clientAccumLength > MaxReassemblyBuffer)
                {
                    System.Diagnostics.Debug.WriteLine("[client] reassembly buffer exceeded cap; disconnecting");
                    client.Connected = false;
                    return;
                }

                client.clientReceiving = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                client.Connected = false;
            }
        }
        static void ServerEndReceive(IAsyncResult ar)
        {
            var client = (Client)ar.AsyncState;

            try
            {
                var count = client.ServerSocket.EndReceive(ar);
                if (count == 0)
                {
                    client.Connected = false; // peer closed the connection
                    return;
                }

                AppendBytes(ref client.serverAccum, ref client.serverAccumLength, client.serverBuffer, count);

                int start = 0;
                int length = client.serverAccumLength;
                while (length - start >= 3)
                {
                    if (client.serverAccum[start] != 0xAA)
                    {
                        System.Diagnostics.Debug.WriteLine("[server] resync: skipping unframed byte(s)");
                        int sig = IndexOfSignature(client.serverAccum, start + 1, length);
                        if (sig < 0) { start = length; break; }
                        start = sig;
                        continue;
                    }

                    int frameLength = ((client.serverAccum[start + 1] << 8) | client.serverAccum[start + 2]) + 3;
                    if (length - start < frameLength)
                        break; // partial frame; wait for the rest

                    var data = new byte[frameLength];
                    Buffer.BlockCopy(client.serverAccum, start, data, 0, frameLength);
                    start += frameLength;

                    try
                    {
                        var msg = new ServerPacket(data);

                        if (msg.ShouldEncrypt)
                            msg.Decrypt(client);

                        lock (Program.SyncObj)
                            client.serverProcessQueue.Enqueue(msg);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex);
                    }
                }

                client.serverAccumLength = length - start;
                if (start > 0 && client.serverAccumLength > 0)
                    Array.Copy(client.serverAccum, start, client.serverAccum, 0, client.serverAccumLength);

                if (client.serverAccumLength > MaxReassemblyBuffer)
                {
                    System.Diagnostics.Debug.WriteLine("[server] reassembly buffer exceeded cap; disconnecting");
                    client.Connected = false;
                    return;
                }

                client.serverReceiving = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                client.Connected = false;
            }
        }
        #endregion

        public byte[] GenerateKey(ushort bRand, byte sRand)
        {
            var key = new byte[9];

            for (var i = 0; i < 9; ++i)
            {
                key[i] = KeyTable[(i * (9 * i + sRand * sRand) + bRand) % 1024];
            }

            return key;
        }

        // Appends `count` bytes from `source` onto the reassembly buffer, growing it if
        // needed. Buffer and its length are passed by ref so the field is updated in place.
        private static void AppendBytes(ref byte[] buffer, ref int length, byte[] source, int count)
        {
            if (length + count > buffer.Length)
                Array.Resize(ref buffer, Math.Max(buffer.Length * 2, length + count));
            Buffer.BlockCopy(source, 0, buffer, length, count);
            length += count;
        }

        // Index of the next 0xAA frame-signature byte at or after `start`, or -1.
        private static int IndexOfSignature(byte[] buffer, int start, int length)
        {
            for (int i = start; i < length; i++)
                if (buffer[i] == 0xAA)
                    return i;
            return -1;
        }
    }
}