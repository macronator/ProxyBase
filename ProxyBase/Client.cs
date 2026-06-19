using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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

        public Socket ClientSocket { get; private set; }
        public Socket ServerSocket { get; private set; }

        private readonly NetworkStream clientStream;
        private readonly NetworkStream serverStream;

        private byte clientOrdinal = 0x00;
        private byte serverOrdinal = 0x00;

        // Outbound queues. Kept so manual injection from ClientTab (Enqueue) still
        // works, and so each socket has a single writer (no concurrent-send races).
        private readonly Queue<ServerPacket> clientSendQueue = new Queue<ServerPacket>(); // -> game client
        private readonly Queue<ClientPacket> serverSendQueue = new Queue<ClientPacket>();  // -> real server
        private readonly SemaphoreSlim clientSendSignal = new SemaphoreSlim(0);
        private readonly SemaphoreSlim serverSendSignal = new SemaphoreSlim(0);

        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private bool disconnected;

        public Client(Server server, Socket socket, EndPoint endPoint)
        {
            this.Server = server;
            this.ClientSocket = socket;
            this.ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.ServerSocket.Connect(endPoint);
            this.Tab = new ClientTab(this);

            this.Key = Encoding.ASCII.GetBytes("UrkcnItnI");
            this.KeyTable = new byte[1024];

            this.clientStream = new NetworkStream(ClientSocket);
            this.serverStream = new NetworkStream(ServerSocket);

            Connected = true;

            // Two receive pumps and two send pumps, all async (no busy-wait, no per-client thread).
            Task.Run(() => ReceiveLoopAsync(true, cts.Token));   // from game client -> server
            Task.Run(() => ReceiveLoopAsync(false, cts.Token));  // from real server -> client
            Task.Run(() => SendToServerLoopAsync(cts.Token));
            Task.Run(() => SendToClientLoopAsync(cts.Token));
        }

        #region Enqueue (manual injection + forwarding)
        public void Enqueue(ClientPacket msg) // destined for the real server
        {
            lock (Program.SyncObj)
            {
                serverSendQueue.Enqueue(msg);
            }
            serverSendSignal.Release();
        }
        public void Enqueue(ServerPacket msg) // destined for the game client
        {
            lock (Program.SyncObj)
            {
                clientSendQueue.Enqueue(msg);
            }
            clientSendSignal.Release();
        }
        #endregion

        #region Receive pumps
        private async Task ReceiveLoopAsync(bool fromClient, CancellationToken token)
        {
            var stream = fromClient ? clientStream : serverStream;
            var buffer = new byte[65535];
            var accumulated = new List<byte>();

            try
            {
                while (Connected && !token.IsCancellationRequested)
                {
                    int count = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (count == 0)
                        break; // peer closed the connection

                    for (int i = 0; i < count; i++)
                        accumulated.Add(buffer[i]);

                    if (accumulated[0] != 0xAA)
                        break; // not a valid Dark Ages stream

                    while (accumulated.Count > 3)
                    {
                        var length = (accumulated[1] << 8) + accumulated[2] + 3;
                        if (length > accumulated.Count)
                            break; // wait for the rest of this packet

                        var data = accumulated.GetRange(0, length).ToArray();
                        accumulated.RemoveRange(0, length);

                        ProcessReceived(data, fromClient);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                Disconnect();
            }
        }

        // Decrypt -> handler -> enqueue for the opposite side. The global lock preserves
        // the original serialization of all crypto state (key/seed/ordinals/RNG).
        private void ProcessReceived(byte[] data, bool fromClient)
        {
            lock (Program.SyncObj)
            {
                if (fromClient)
                {
                    var msg = new ClientPacket(data);

                    if (msg.ShouldEncrypt)
                        msg.Decrypt(this);

                    if (msg.Opcode == 0x39 || msg.Opcode == 0x3A)
                        msg.DecryptDialog();

                    if (Server.ClientMessageHandlers[msg.Opcode].Invoke(this, msg))
                    {
                        serverSendQueue.Enqueue(msg);
                        serverSendSignal.Release();
                        Tab.LogOutgoingPacket("Send> {0}", msg);
                    }
                }
                else
                {
                    var msg = new ServerPacket(data);

                    if (msg.ShouldEncrypt)
                        msg.Decrypt(this);

                    if (Server.ServerMessageHandlers[msg.Opcode].Invoke(this, msg))
                    {
                        clientSendQueue.Enqueue(msg);
                        clientSendSignal.Release();
                        Tab.LogIncomingPacket("Recv> {0}", msg);
                    }
                }
            }
        }
        #endregion

        #region Send pumps
        private async Task SendToServerLoopAsync(CancellationToken token)
        {
            try
            {
                while (Connected && !token.IsCancellationRequested)
                {
                    await serverSendSignal.WaitAsync(token);

                    byte[] buffer;
                    lock (Program.SyncObj)
                    {
                        if (serverSendQueue.Count == 0)
                            continue;

                        var msg = serverSendQueue.Dequeue();

                        if (msg.Opcode == 0x39 || msg.Opcode == 0x3A)
                            msg.EncryptDialog();

                        if (msg.ShouldEncrypt)
                        {
                            msg.Ordinal = serverOrdinal++;
                            msg.Encrypt(this);
                        }

                        buffer = BuildBuffer(msg);
                    }

                    await serverStream.WriteAsync(buffer, 0, buffer.Length, token);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                Disconnect();
            }
        }

        private async Task SendToClientLoopAsync(CancellationToken token)
        {
            try
            {
                while (Connected && !token.IsCancellationRequested)
                {
                    await clientSendSignal.WaitAsync(token);

                    byte[] buffer;
                    lock (Program.SyncObj)
                    {
                        if (clientSendQueue.Count == 0)
                            continue;

                        var msg = clientSendQueue.Dequeue();

                        if (msg.ShouldEncrypt)
                        {
                            msg.Ordinal = clientOrdinal++;
                            msg.Encrypt(this);
                        }

                        buffer = BuildBuffer(msg);
                    }

                    await clientStream.WriteAsync(buffer, 0, buffer.Length, token);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                Disconnect();
            }
        }

        private static byte[] BuildBuffer(Packet msg)
        {
            msg.Length = (ushort)(msg.BodyData.Length + (msg.Header.Length - 3));
            var buffer = new byte[msg.Header.Length + msg.BodyData.Length];
            Array.Copy(msg.Header, 0, buffer, 0, msg.Header.Length);
            Array.Copy(msg.BodyData, 0, buffer, msg.Header.Length, msg.BodyData.Length);
            return buffer;
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

        private void Disconnect()
        {
            lock (Program.SyncObj)
            {
                if (disconnected)
                    return;
                disconnected = true;
                Connected = false;
            }

            try { cts.Cancel(); } catch { }
            try { if (ClientSocket.Connected) ClientSocket.Close(); } catch { }
            try { if (ServerSocket.Connected) ServerSocket.Close(); } catch { }

            lock (Program.SyncObj)
            {
                Server.Clients.Remove(this);
            }

            try { Server.Form.RemoveTab(Tab); } catch { }
        }
    }
}
