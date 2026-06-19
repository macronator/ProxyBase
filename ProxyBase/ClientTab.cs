using System;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;

namespace ProxyBase
{
    public partial class ClientTab : TabPage
    {
        public Client Client { get; set; }

        public ClientTab(Client client)
        {
            InitializeComponent();
            this.Client = client;
        }

        private void buttonSend_Click(object sender, EventArgs e)
        {
            var lines = textConsoleInput.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                ClientPacket msg = null;
                var words = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in words)
                {
                    byte value;
                    if (byte.TryParse(word, NumberStyles.HexNumber, null, out value))
                    {
                        if (msg == null)
                            msg = new ClientPacket(value);
                        else
                            msg.WriteByte(value);
                    }
                }

                if (msg.Opcode == 0x39 || msg.Opcode == 0x3A)
                    msg.GenerateDialogHeader();

                if (msg.UseDefaultKey)
                {
                    msg.WriteByte(0x00);
                }
                else
                {
                    msg.WriteByte(0x00);
                    msg.WriteByte(msg.Opcode);
                }

                msg.Write(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });

                new Thread(() => { Client.Enqueue(msg); }).Start();
            }
        }
        private void buttonRecv_Click(object sender, EventArgs e)
        {
            var lines = textConsoleInput.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                ServerPacket msg = null;
                var words = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in words)
                {
                    byte value;
                    if (byte.TryParse(word, NumberStyles.HexNumber, null, out value))
                    {
                        if (msg == null)
                            msg = new ServerPacket(value);
                        else
                            msg.WriteByte(value);
                    }
                }

                msg.Write(new byte[] { 0x00, 0x00, 0x00 });

                new Thread(() => { Client.Enqueue(msg); }).Start();
            }
        }

        public void LogIncomingPacket(string format, params object[] args)
        {
            if (InvokeRequired)
            {
                Invoke((MethodInvoker)delegate() { LogIncomingPacket(format, args); });
            }
            else
            {
                if (checkRecv.Checked)
                {
                    textConsoleOutput.AppendText(string.Format(format, args));
                    textConsoleOutput.AppendText(Environment.NewLine);
                }
            }
        }
        public void LogOutgoingPacket(string format, params object[] args)
        {
            if (InvokeRequired)
            {
                Invoke((MethodInvoker)delegate() { LogOutgoingPacket(format, args); });
            }
            else
            {
                if (checkSend.Checked)
                {
                    textConsoleOutput.AppendText(string.Format(format, args));
                    textConsoleOutput.AppendText(Environment.NewLine);
                }
            }
        }
    }
}