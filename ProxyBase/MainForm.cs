using System;
using System.IO;
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
            try
            {
                this.Server = new Server(this);
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show(
                    "Could not listen on 127.0.0.1:" + Config.LocalListenPort +
                    ".\r\n\r\nAnother program (another ProxyBase instance, or a bot such as " +
                    "Ascend) is probably already using that port. Close it and start ProxyBase again.",
                    "ProxyBase - port in use",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(1);
            }
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
            var path = ResolveDarkAgesPath();
            if (string.IsNullOrEmpty(path))
                return; // no client selected

            ProcessInformation information;
            StartupInfo startupInfo = new StartupInfo();
            startupInfo.Size = Marshal.SizeOf(startupInfo);
            Kernel32.CreateProcess(path, null, IntPtr.Zero, IntPtr.Zero, false, ProcessCreationFlags.Suspended,
                IntPtr.Zero, Path.GetDirectoryName(path), ref startupInfo, out information);

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

        private void chooseDaPathToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowDaPathDialog();
        }

        /// <summary>
        /// Returns the Dark Ages client path to launch: the saved setting if present,
        /// otherwise the Config.cs default, otherwise opens the Options dialog so the
        /// user can set it.
        /// </summary>
        private string ResolveDarkAgesPath()
        {
            var path = Properties.Settings.Default.DarkAgesPath;
            if (string.IsNullOrEmpty(path))
                path = Config.ClientPath;

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                path = ShowDaPathDialog();

            return path;
        }

        /// <summary>
        /// Shows the Options dialog (editable path + Browse), saves the chosen path,
        /// and returns it — or null if the user cancelled.
        /// </summary>
        private string ShowDaPathDialog()
        {
            using (var options = new OptionsForm())
            {
                var current = Properties.Settings.Default.DarkAgesPath;
                if (string.IsNullOrEmpty(current))
                    current = Config.ClientPath;
                options.DarkAgesPath = current;

                if (options.ShowDialog(this) == DialogResult.OK)
                {
                    Properties.Settings.Default.DarkAgesPath = options.DarkAgesPath;
                    Properties.Settings.Default.Save();
                    return options.DarkAgesPath;
                }
            }
            return null;
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