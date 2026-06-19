namespace ProxyBase
{
    partial class ClientTab
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ClientTab));
            this.textConsoleOutput = new System.Windows.Forms.RichTextBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.textConsoleInput = new System.Windows.Forms.RichTextBox();
            this.buttonSend = new System.Windows.Forms.Button();
            this.buttonRecv = new System.Windows.Forms.Button();
            this.toolStrip2 = new System.Windows.Forms.ToolStrip();
            this.checkRecv = new System.Windows.Forms.ToolStripButton();
            this.checkSend = new System.Windows.Forms.ToolStripButton();
            this.panel1.SuspendLayout();
            this.toolStrip2.SuspendLayout();
            this.SuspendLayout();
            // 
            // textConsoleOutput
            // 
            this.textConsoleOutput.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textConsoleOutput.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textConsoleOutput.Location = new System.Drawing.Point(0, 25);
            this.textConsoleOutput.Name = "textConsoleOutput";
            this.textConsoleOutput.Size = new System.Drawing.Size(632, 328);
            this.textConsoleOutput.TabIndex = 3;
            this.textConsoleOutput.Text = "";
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.textConsoleInput);
            this.panel1.Controls.Add(this.buttonSend);
            this.panel1.Controls.Add(this.buttonRecv);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel1.Location = new System.Drawing.Point(0, 353);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(632, 75);
            this.panel1.TabIndex = 4;
            // 
            // textConsoleInput
            // 
            this.textConsoleInput.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textConsoleInput.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textConsoleInput.Location = new System.Drawing.Point(0, 0);
            this.textConsoleInput.Name = "textConsoleInput";
            this.textConsoleInput.Size = new System.Drawing.Size(482, 75);
            this.textConsoleInput.TabIndex = 1;
            this.textConsoleInput.Text = "";
            // 
            // buttonSend
            // 
            this.buttonSend.Dock = System.Windows.Forms.DockStyle.Right;
            this.buttonSend.Location = new System.Drawing.Point(482, 0);
            this.buttonSend.Name = "buttonSend";
            this.buttonSend.Size = new System.Drawing.Size(75, 75);
            this.buttonSend.TabIndex = 2;
            this.buttonSend.Text = "Send";
            this.buttonSend.UseVisualStyleBackColor = true;
            this.buttonSend.Click += new System.EventHandler(this.buttonSend_Click);
            // 
            // buttonRecv
            // 
            this.buttonRecv.Dock = System.Windows.Forms.DockStyle.Right;
            this.buttonRecv.Location = new System.Drawing.Point(557, 0);
            this.buttonRecv.Name = "buttonRecv";
            this.buttonRecv.Size = new System.Drawing.Size(75, 75);
            this.buttonRecv.TabIndex = 3;
            this.buttonRecv.Text = "Recv";
            this.buttonRecv.UseVisualStyleBackColor = true;
            this.buttonRecv.Click += new System.EventHandler(this.buttonRecv_Click);
            // 
            // toolStrip2
            // 
            this.toolStrip2.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.checkRecv,
            this.checkSend});
            this.toolStrip2.Location = new System.Drawing.Point(0, 0);
            this.toolStrip2.Name = "toolStrip2";
            this.toolStrip2.Size = new System.Drawing.Size(632, 25);
            this.toolStrip2.TabIndex = 2;
            this.toolStrip2.Text = "toolStrip2";
            // 
            // checkRecv
            // 
            this.checkRecv.CheckOnClick = true;
            this.checkRecv.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.checkRecv.Image = ((System.Drawing.Image)(resources.GetObject("checkRecv.Image")));
            this.checkRecv.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.checkRecv.Name = "checkRecv";
            this.checkRecv.Size = new System.Drawing.Size(105, 22);
            this.checkRecv.Text = "Incoming Packets";
            // 
            // checkSend
            // 
            this.checkSend.CheckOnClick = true;
            this.checkSend.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.checkSend.Image = ((System.Drawing.Image)(resources.GetObject("checkSend.Image")));
            this.checkSend.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.checkSend.Name = "checkSend";
            this.checkSend.Size = new System.Drawing.Size(105, 22);
            this.checkSend.Text = "Outgoing Packets";
            // 
            // ClientTab
            // 
            this.ClientSize = new System.Drawing.Size(632, 428);
            this.Controls.Add(this.textConsoleOutput);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.toolStrip2);
            this.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Name = "ClientTab";
            this.Text = "ClientTab";
            this.panel1.ResumeLayout(false);
            this.toolStrip2.ResumeLayout(false);
            this.toolStrip2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.RichTextBox textConsoleOutput;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.RichTextBox textConsoleInput;
        private System.Windows.Forms.Button buttonSend;
        private System.Windows.Forms.Button buttonRecv;
        private System.Windows.Forms.ToolStrip toolStrip2;
        private System.Windows.Forms.ToolStripButton checkRecv;
        private System.Windows.Forms.ToolStripButton checkSend;

    }
}