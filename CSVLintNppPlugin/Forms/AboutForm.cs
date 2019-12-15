﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kbg.NppPluginNET
{
    public partial class AboutForm : Form
    {
        public AboutForm()
        {
            InitializeComponent();

            // version for example "1.3.0.0"
            String ver = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            // if 4 version digits, remove last two ".0" if any, example  "1.3.0.0" ->  "1.3" or  "2.0.0.0" ->  "2.0"
            while ( (ver.Length > 4) && (ver.Substring(ver.Length - 2, 2) == ".0"))
            {
                ver = ver.Substring(0, ver.Length - 2);
            }
            lblTitle.Text = lblTitle.Text + ver;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            Close();
        }
        private void onLinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            LinkLabel lbl = (sender as LinkLabel);
            string url = lbl.Text;
            if (lbl.Tag == "1")
            {
                url = string.Format("mailto:{0}?subject=NPP+CSVLint", url);
            }

            // Change the color of the link text by setting LinkVisited to true.
            lbl.LinkVisited = true;

            // Call the Process.Start method to open the default browser with a URL:
            System.Diagnostics.Process.Start(url);
        }
    }
}
