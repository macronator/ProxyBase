using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ProxyBase
{
    public partial class MainForm : Form
    {
        public Server Server { get; private set; }

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            this.Server = new Server(this);
        }
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            System.Diagnostics.Process.GetCurrentProcess().Kill();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.GetCurrentProcess().Kill();
        }
        private void launchDarkAgesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var path = Config.ClientPath;

            ProcessInformation information;
            StartupInfo startupInfo = new StartupInfo();
            startupInfo.Size = Marshal.SizeOf(startupInfo);
            Kernel32.CreateProcess(path, null, IntPtr.Zero, IntPtr.Zero, false, ProcessCreationFlags.Suspended,
                IntPtr.Zero, null, ref startupInfo, out information);

            using (ProcessMemoryStream stream = new ProcessMemoryStream(information.ProcessId,
                ProcessAccess.VmWrite | ProcessAccess.VmRead | ProcessAccess.VmOperation))
            {
                stream.Position = Config.PatchForceJump;
                stream.WriteByte(0xEB);

                // overwrite the connect() IP args with 127.0.0.1 (push 0x7F,0x00,0x00,0x01)
                stream.Position = Config.PatchConnectIp;
                stream.WriteByte(0x6A);
                stream.WriteByte(0x01);
                stream.WriteByte(0x6A);
                stream.WriteByte(0x00);
                stream.WriteByte(0x6A);
                stream.WriteByte(0x00);
                stream.WriteByte(0x6A);
                stream.WriteByte(0x7F);

                stream.Position = Config.PatchConnectPort;
                stream.WriteByte((byte)(Config.LocalListenPort % 256));
                stream.WriteByte((byte)(Config.LocalListenPort / 256));

                stream.Position = Config.PatchSecondJump;
                stream.WriteByte(0xEB);

                Kernel32.ResumeThread(information.ThreadHandle);
            }
        }

        public void AddTab(ClientTab clientTab)
        {
            if (InvokeRequired)
            {
                Invoke((MethodInvoker)delegate()
                {
                    tabControl1.TabPages.Add(clientTab);
                });
            }
            else
            {
                tabControl1.TabPages.Add(clientTab);
            }
        }
        public void RemoveTab(ClientTab clientTab)
        {
            if (InvokeRequired)
            {
                Invoke((MethodInvoker)delegate()
                {
                    clientTab.Dispose();
                });
            }
            else
            {
                clientTab.Dispose();
            }
        }
    }
}