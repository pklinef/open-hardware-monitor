namespace OpenHardwareMonitor.GUI
{
    partial class EmailAlertsForm
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
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.portCancelButton = new System.Windows.Forms.Button();
            this.portOKButton = new System.Windows.Forms.Button();
            this.textBoxEmailAddress = new System.Windows.Forms.TextBox();
            this.textBoxSMTPServer = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(73, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Email Address";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 39);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(71, 13);
            this.label2.TabIndex = 1;
            this.label2.Text = "SMTP Server";
            // 
            // portCancelButton
            // 
            this.portCancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.portCancelButton.Location = new System.Drawing.Point(77, 74);
            this.portCancelButton.Name = "portCancelButton";
            this.portCancelButton.Size = new System.Drawing.Size(75, 23);
            this.portCancelButton.TabIndex = 3;
            this.portCancelButton.Text = "Cancel";
            this.portCancelButton.UseVisualStyleBackColor = true;
            this.portCancelButton.Click += new System.EventHandler(this.portCancelButton_Click);
            // 
            // portOKButton
            // 
            this.portOKButton.Location = new System.Drawing.Point(159, 74);
            this.portOKButton.Name = "portOKButton";
            this.portOKButton.Size = new System.Drawing.Size(75, 23);
            this.portOKButton.TabIndex = 2;
            this.portOKButton.Text = "OK";
            this.portOKButton.UseVisualStyleBackColor = true;
            this.portOKButton.Click += new System.EventHandler(this.portOKButton_Click);
            // 
            // textBoxEmailAddress
            // 
            this.textBoxEmailAddress.Location = new System.Drawing.Point(92, 10);
            this.textBoxEmailAddress.Name = "textBoxEmailAddress";
            this.textBoxEmailAddress.Size = new System.Drawing.Size(222, 20);
            this.textBoxEmailAddress.TabIndex = 4;
            // 
            // textBoxSMTPServer
            // 
            this.textBoxSMTPServer.Location = new System.Drawing.Point(92, 32);
            this.textBoxSMTPServer.Name = "textBoxSMTPServer";
            this.textBoxSMTPServer.Size = new System.Drawing.Size(222, 20);
            this.textBoxSMTPServer.TabIndex = 5;
            // 
            // EmailAlertsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(326, 109);
            this.Controls.Add(this.textBoxSMTPServer);
            this.Controls.Add(this.textBoxEmailAddress);
            this.Controls.Add(this.portCancelButton);
            this.Controls.Add(this.portOKButton);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Name = "EmailAlertsForm";
            this.Text = "EmailAlertsForm";
            this.Load += new System.EventHandler(this.EmailAlertsForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button portCancelButton;
        private System.Windows.Forms.Button portOKButton;
        private System.Windows.Forms.TextBox textBoxEmailAddress;
        private System.Windows.Forms.TextBox textBoxSMTPServer;
    }
}