namespace OpenHardwareMonitor.GUI
{
    partial class ServerAddressForm
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
            this.serverPortNumericUpDn = new System.Windows.Forms.NumericUpDown();
            this.serverAddressCancelButton = new System.Windows.Forms.Button();
            this.serverAddressOKButton = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.numericUpDown1 = new System.Windows.Forms.NumericUpDown();
            this.numericUpDown2 = new System.Windows.Forms.NumericUpDown();
            this.numericUpDown3 = new System.Windows.Forms.NumericUpDown();
            this.numericUpDown4 = new System.Windows.Forms.NumericUpDown();
            this.label2 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.serverPortNumericUpDn)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown2)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown3)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown4)).BeginInit();
            this.SuspendLayout();
            // 
            // serverPortNumericUpDn
            // 
            this.serverPortNumericUpDn.Location = new System.Drawing.Point(110, 51);
            this.serverPortNumericUpDn.Maximum = new decimal(new int[] {
            20000,
            0,
            0,
            0});
            this.serverPortNumericUpDn.Minimum = new decimal(new int[] {
            8085,
            0,
            0,
            0});
            this.serverPortNumericUpDn.Name = "serverPortNumericUpDn";
            this.serverPortNumericUpDn.Size = new System.Drawing.Size(75, 20);
            this.serverPortNumericUpDn.TabIndex = 9;
            this.serverPortNumericUpDn.Value = new decimal(new int[] {
            8085,
            0,
            0,
            0});
            // 
            // serverAddressCancelButton
            // 
            this.serverAddressCancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.serverAddressCancelButton.Location = new System.Drawing.Point(138, 79);
            this.serverAddressCancelButton.Name = "serverAddressCancelButton";
            this.serverAddressCancelButton.Size = new System.Drawing.Size(75, 23);
            this.serverAddressCancelButton.TabIndex = 12;
            this.serverAddressCancelButton.Text = "Cancel";
            this.serverAddressCancelButton.UseVisualStyleBackColor = true;
            this.serverAddressCancelButton.Click += new System.EventHandler(this.serverAddressCancelButton_Click);
            // 
            // serverAddressOKButton
            // 
            this.serverAddressOKButton.Location = new System.Drawing.Point(220, 79);
            this.serverAddressOKButton.Name = "serverAddressOKButton";
            this.serverAddressOKButton.Size = new System.Drawing.Size(75, 23);
            this.serverAddressOKButton.TabIndex = 11;
            this.serverAddressOKButton.Text = "OK";
            this.serverAddressOKButton.UseVisualStyleBackColor = true;
            this.serverAddressOKButton.Click += new System.EventHandler(this.serverAddressOKButton_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(-1, 27);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(105, 13);
            this.label1.TabIndex = 13;
            this.label1.Text = "Data Server Address";
            // 
            // numericUpDown1
            // 
            this.numericUpDown1.Location = new System.Drawing.Point(110, 25);
            this.numericUpDown1.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.numericUpDown1.Name = "numericUpDown1";
            this.numericUpDown1.Size = new System.Drawing.Size(50, 20);
            this.numericUpDown1.TabIndex = 14;
            this.numericUpDown1.Value = new decimal(new int[] {
            255,
            0,
            0,
            0});
            // 
            // numericUpDown2
            // 
            this.numericUpDown2.Location = new System.Drawing.Point(166, 25);
            this.numericUpDown2.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.numericUpDown2.Name = "numericUpDown2";
            this.numericUpDown2.Size = new System.Drawing.Size(50, 20);
            this.numericUpDown2.TabIndex = 15;
            this.numericUpDown2.Value = new decimal(new int[] {
            255,
            0,
            0,
            0});
            // 
            // numericUpDown3
            // 
            this.numericUpDown3.Location = new System.Drawing.Point(222, 25);
            this.numericUpDown3.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.numericUpDown3.Name = "numericUpDown3";
            this.numericUpDown3.Size = new System.Drawing.Size(50, 20);
            this.numericUpDown3.TabIndex = 16;
            this.numericUpDown3.Value = new decimal(new int[] {
            255,
            0,
            0,
            0});
            // 
            // numericUpDown4
            // 
            this.numericUpDown4.Location = new System.Drawing.Point(275, 25);
            this.numericUpDown4.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.numericUpDown4.Name = "numericUpDown4";
            this.numericUpDown4.Size = new System.Drawing.Size(50, 20);
            this.numericUpDown4.TabIndex = 17;
            this.numericUpDown4.Value = new decimal(new int[] {
            255,
            0,
            0,
            0});
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(78, 53);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(26, 13);
            this.label2.TabIndex = 18;
            this.label2.Text = "Port";
            // 
            // ServerAddressForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(442, 130);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.numericUpDown4);
            this.Controls.Add(this.numericUpDown3);
            this.Controls.Add(this.numericUpDown2);
            this.Controls.Add(this.numericUpDown1);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.serverAddressCancelButton);
            this.Controls.Add(this.serverAddressOKButton);
            this.Controls.Add(this.serverPortNumericUpDn);
            this.Name = "ServerAddressForm";
            this.Text = "Data Server Address";
            this.Load += new System.EventHandler(this.ServerAddressForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.serverPortNumericUpDn)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown2)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown3)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown4)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.NumericUpDown serverPortNumericUpDn;
        private System.Windows.Forms.Button serverAddressCancelButton;
        private System.Windows.Forms.Button serverAddressOKButton;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.NumericUpDown numericUpDown1;
        private System.Windows.Forms.NumericUpDown numericUpDown2;
        private System.Windows.Forms.NumericUpDown numericUpDown3;
        private System.Windows.Forms.NumericUpDown numericUpDown4;
        private System.Windows.Forms.Label label2;
    }
}