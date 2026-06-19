using System;
using System.IO;
using System.Windows.Forms;

namespace ProxyBase
{
    public partial class OptionsForm : Form
    {
        /// <summary>The Dark Ages client path shown in / edited by the dialog.</summary>
        public string DarkAgesPath
        {
            get { return textDarkAgesPath.Text.Trim(); }
            set { textDarkAgesPath.Text = value; }
        }

        public OptionsForm()
        {
            InitializeComponent();
        }

        private void buttonBrowse_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Select your Dark Ages client (Darkages.exe)";
                dialog.Filter = "Dark Ages client (Darkages.exe)|Darkages.exe|Executable (*.exe)|*.exe|All files (*.*)|*.*";

                var current = textDarkAgesPath.Text.Trim();
                if (!string.IsNullOrEmpty(current))
                {
                    var dir = Path.GetDirectoryName(current);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        dialog.InitialDirectory = dir;
                    dialog.FileName = Path.GetFileName(current);
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                    textDarkAgesPath.Text = dialog.FileName;
            }
        }
    }
}
