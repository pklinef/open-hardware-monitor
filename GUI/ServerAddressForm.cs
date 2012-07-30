using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using OpenHardwareMonitor.DAL;

namespace OpenHardwareMonitor.GUI
{
    public partial class ServerAddressForm : Form
    {
        public ServerAddressForm()
        {
            InitializeComponent();
        }

        private void ServerAddressForm_Load(object sender, EventArgs e)
        {
            if (HttpClient.ServerURL == null || HttpClient.ServerURL == "")
                return;
            var IP = HttpClient.ServerURL.Replace("http://", "").Replace("/aggregator", "").Replace("/", "");
            var port = IP.Split(':')[1];
            var subIPs = IP.Split(':')[0].Split('.');

            this.numericUpDown1.Value = Convert.ToByte(subIPs[0]);
            this.numericUpDown2.Value = Convert.ToByte(subIPs[1]);
            this.numericUpDown3.Value = Convert.ToByte(subIPs[2]);
            this.numericUpDown4.Value = Convert.ToByte(subIPs[3]);
            this.serverPortNumericUpDn.Value = Convert.ToUInt16(port);

        }

        private void textBox4_TextChanged(object sender, EventArgs e)
        {

        }

        private void serverAddressCancelButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void serverAddressOKButton_Click(object sender, EventArgs e)
        {
            HttpClient.ServerURL = "http://" + this.numericUpDown1.Value + "." + this.numericUpDown2.Value + "." + this.numericUpDown3.Value + "." + this.numericUpDown4.Value + ":" + (int)this.serverPortNumericUpDn.Value + "/";
            this.Close();
        }
    }
}
