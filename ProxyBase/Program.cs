using System;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace ProxyBase
{
    public static class Program
    {
        public static object SyncObj = new object();
        public static string StartupPath { get; private set; }

        [STAThread]
        static void Main()
        {
            StartupPath = Application.StartupPath;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        public static string GetHashString(string value)
        {
            var md5 = MD5.Create();
            var buffer = Encoding.ASCII.GetBytes(value);
            var hash = md5.ComputeHash(buffer);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();
        }
    }
}