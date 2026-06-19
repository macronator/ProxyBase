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

            ClientMessageHandlers[0x10] = new ClientMessageHandler(ClientMessage_0x10_ClientJoin);
            ServerMessageHandlers[0x03] = new ServerMessageHandler(ServerMessage_0x03_Redirect);
            ServerMessageHandlers[0x05] = new ServerMessageHandler(ServerMessage_0x05_PlayerID);

            this.RemoteEndPoint = new IPEndPoint(IPAddress.Parse(Config.RemoteServerIp), Config.RemoteServerPort);

            this.ServerLoopThread = new Thread(new ThreadStart(ServerLoop));
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
    }
}