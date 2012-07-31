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
    public partial class EmailAlertsForm : Form
    {
        public EmailAlertsForm()
        {
            InitializeComponent();
        }

        private void portCancelButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void portOKButton_Click(object sender, EventArgs e)
        {
            DataManager.SetEmailSettings(textBoxSMTPServer.Text, textBoxEmailAddress.Text);
            this.Close();
        }

        private void EmailAlertsForm_Load(object sender, EventArgs e)
        {
            string email, smtp;
            DataManager.GetEmailSettings(out smtp, out email);
            if (smtp != null)
                this.textBoxSMTPServer.Text = smtp;
            if (email != null)
                this.textBoxEmailAddress.Text = email;
        }
    }
}
