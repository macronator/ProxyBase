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

        public bool Connected { get; private set; }
        public Server Server { get; private set; }

        public ClientTab Tab { get; private set; }

        public Thread ClientLoopThread { get; private set; }

        public Socket ClientSocket { get; private set; }
        public Socket ServerSocket { get; private set; }

        private bool clientReceiving = false;
        private bool serverReceiving = false;

        private byte[] clientBuffer = new byte[65535];
        private byte[] serverBuffer = new byte[65535];

        private List<byte> fullClientBuffer = new List<byte>();
        private List<byte> fullServerBuffer = new List<byte>();

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

            this.Key = Encoding.ASCII.GetBytes("UrkcnItnI");
            this.KeyTable = new byte[1024];

            this.ClientLoopThread = new Thread(new ThreadStart(ClientLoop));
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

                for (int i = 0; i < count; i++)
                    client.fullClientBuffer.Add(client.clientBuffer[i]);

                if (count == 0 || client.fullClientBuffer[0] != 0xAA)
                {
                    client.Connected = false;
                    return;
                }

                while (client.fullClientBuffer.Count > 3)
                {
                    var length = ((client.fullClientBuffer[1] << 8) + client.fullClientBuffer[2] + 3);

                    if (length > client.fullClientBuffer.Count)
                        break;

                    var data = client.fullClientBuffer.GetRange(0, length);
                    client.fullClientBuffer.RemoveRange(0, length);

                    var msg = new ClientPacket(data.ToArray());

                    if (msg.ShouldEncrypt)
                    {
                        msg.Decrypt(client);
                    }

                    if (msg.Opcode == 0x39 || msg.Opcode == 0x3A)
                    {
                        msg.DecryptDialog();
                    }

                    lock (Program.SyncObj)
                    {
                        client.clientProcessQueue.Enqueue(msg);
                    }
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

                for (int i = 0; i < count; i++)
                    client.fullServerBuffer.Add(client.serverBuffer[i]);

                if (count == 0 || client.fullServerBuffer[0] != 0xAA)
                {
                    client.Connected = false;
                    return;
                }

                while (client.fullServerBuffer.Count > 3)
                {
                    var length = ((client.fullServerBuffer[1] << 8) + client.fullServerBuffer[2] + 3);

                    if (length > client.fullServerBuffer.Count)
                        break;

                    var data = client.fullServerBuffer.GetRange(0, length);
                    client.fullServerBuffer.RemoveRange(0, length);

                    var msg = new ServerPacket(data.ToArray());

                    if (msg.ShouldEncrypt)
                    {
                        msg.Decrypt(client);
                    }

                    lock (Program.SyncObj)
                    {
                        client.serverProcessQueue.Enqueue(msg);
                    }
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
    }
}