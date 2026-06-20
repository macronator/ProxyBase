using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ProxyBase
{
    public delegate bool ClientMessageHandler(Client client, ClientPacket msg);
    public delegate bool ServerMessageHandler(Client client, ServerPacket msg);

    public class Server
    {
        public bool Running { get; private set; }
        public MainForm Form { get; private set; }
        public Socket Socket { get; private set; }
        public TcpListener Listener { get; private set; }
        public ClientMessageHandler[] ClientMessageHandlers { get; private set; }
        public ServerMessageHandler[] ServerMessageHandlers { get; private set; }
        public EndPoint RemoteEndPoint { get; private set; }
        public List<Client> Clients { get; set; }
        public Thread ServerLoopThread { get; private set; }

        public Server(MainForm mainForm)
        {
            this.Form = mainForm;

            this.Listener = new TcpListener(IPAddress.Loopback, Config.LocalListenPort);
            this.Listener.Start(10);

            this.Clients = new List<Client>();
            this.ClientMessageHandlers = new ClientMessageHandler[256];
            this.ServerMessageHandlers = new ServerMessageHandler[256];

            for (int i = 0; i < ClientMessageHandlers.Length; i++)
            {
                ClientMessageHandlers[i] = (client, msg) => { return true; };
            }

            for (int i = 0; i < ServerMessageHandlers.Length; i++)
            {
                ServerMessageHandlers[i] = (client, msg) => { return true; };
            }

            ClientMessageHandlers[(byte)ClientOpcode.ClientJoin] = new ClientMessageHandler(ClientMessage_0x10_ClientJoin);
            ServerMessageHandlers[(byte)ServerOpcode.Redirect] = new ServerMessageHandler(ServerMessage_0x03_Redirect);
            ServerMessageHandlers[(byte)ServerOpcode.PlayerId] = new ServerMessageHandler(ServerMessage_0x05_PlayerID);

            // Example handlers (safe to delete) -- parse a packet per its reverse-engineered
            // wire layout, log the decoded fields, then forward it unchanged. They show the
            // pattern for writing your own. 0x0A is static-key (always decodes); 0x0C is
            // dynamic-key (decodes once the in-game key is set after the 0x10 handshake).
            ServerMessageHandlers[(byte)ServerOpcode.SystemMessage] = new ServerMessageHandler(ServerMessage_0x0A_SystemMessage);
            ServerMessageHandlers[(byte)ServerOpcode.CreatureWalk] = new ServerMessageHandler(ServerMessage_0x0C_CreatureWalk);

            this.RemoteEndPoint = new IPEndPoint(IPAddress.Parse(Config.RemoteServerIp), Config.RemoteServerPort);

            this.ServerLoopThread = new Thread(new ThreadStart(ServerLoop));
            this.ServerLoopThread.IsBackground = true;
            this.ServerLoopThread.Start();
        }

        public void ServerLoop()
        {
            Running = true;

            while (Running)
            {
                if (Listener.Pending())
                {
                    var socket = Listener.AcceptSocket();
                    var client = new Client(this, socket, RemoteEndPoint);

                    Clients.Add(client);

                    RemoteEndPoint = new IPEndPoint(IPAddress.Parse(Config.RemoteServerIp), Config.RemoteServerPort);
                }

                Thread.Sleep(1);
            }
        }

        /// <summary>Stops accepting connections and releases the listen port.</summary>
        public void Stop()
        {
            Running = false;
            try { Listener.Stop(); }
            catch (SocketException) { }
        }

        public bool ClientMessage_0x10_ClientJoin(Client client, ClientPacket msg)
        {
            var seed = msg.ReadByte();
            var key = msg.Read(msg.ReadByte());
            var name = msg.ReadString8();
            var id = msg.ReadUInt32();

            client.Tab.Text = name;

            string table;

            table = Program.GetHashString(name);
            table = Program.GetHashString(table);
            for (var i = 0; i < 31; i++)
            {
                table += Program.GetHashString(table);
            }

            client.Seed = seed;
            client.Key = key;
            client.KeyTable = Encoding.ASCII.GetBytes(table);

            return true;
        }
        public bool ServerMessage_0x03_Redirect(Client client, ServerPacket msg)
        {
            var address = msg.Read(4);
            var port = msg.ReadUInt16();
            var length = msg.ReadByte();
            var seed = msg.ReadByte();
            var key = msg.Read(msg.ReadByte());
            var name = msg.ReadString8();
            var id = msg.ReadUInt32();

            Array.Reverse(address);
            RemoteEndPoint = new IPEndPoint(new IPAddress(address), port);

            msg.BodyData[0] = 0x01;
            msg.BodyData[1] = 0x00;
            msg.BodyData[2] = 0x00;
            msg.BodyData[3] = 0x7F;

            msg.BodyData[4] = (byte)(Config.LocalListenPort / 256);
            msg.BodyData[5] = (byte)(Config.LocalListenPort % 256);

            return true;
        }
        public bool ServerMessage_0x05_PlayerID(Client client, ServerPacket msg)
        {
            Form.AddTab(client.Tab);
            return true;
        }

        /// <summary>
        /// Example handler (S2C <see cref="ServerOpcode.SystemMessage"/>, 0x0A): a server
        /// or system message. Wire layout from the protocol RE:
        /// <c>type:u8, length:u16(BE), text[length]</c>. This is a static-key packet, so it
        /// always decodes. Read-only -- it logs the decoded text and forwards the packet
        /// unchanged. Wrapped defensively so a malformed packet can never drop the relay.
        /// </summary>
        public bool ServerMessage_0x0A_SystemMessage(Client client, ServerPacket msg)
        {
            try
            {
                var type = msg.ReadByte();
                var length = msg.ReadUInt16();
                var text = msg.ReadString(length);
                client.Tab.LogIncomingPacket("  [SystemMessage type={0}] {1}", type, text);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
            return true;
        }

        /// <summary>
        /// Example handler (S2C <see cref="ServerOpcode.CreatureWalk"/>, 0x0C): another
        /// creature or player taking a step. Wire layout from the protocol RE:
        /// <c>id:u32, x:u16, y:u16, direction:u8</c>. This is a dynamic-key packet, so it
        /// only decodes once the in-game key has been established (after the 0x10
        /// handshake). Read-only; forwards the packet unchanged.
        /// </summary>
        public bool ServerMessage_0x0C_CreatureWalk(Client client, ServerPacket msg)
        {
            try
            {
                var id = msg.ReadUInt32();
                var x = msg.ReadUInt16();
                var y = msg.ReadUInt16();
                var direction = msg.ReadByte();
                client.Tab.LogIncomingPacket("  [CreatureWalk] id={0} ({1},{2}) dir={3}", id, x, y, direction);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
            return true;
        }
    }
}