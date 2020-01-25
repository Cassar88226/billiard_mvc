using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BilliardWindowsApplication
{
    public partial class frmclose : Form
    {
        public frmclose()
        {
            InitializeComponent();
        }
        biliardService.BilliardScoreboard API = new biliardService.BilliardScoreboard();
        private void pictureBox1_Click(object sender, EventArgs e)
        {
            API.deletelastgamecostiffree(BLL_BilliardWindowsApplication.gamecostdetailsStatic);
            //wanttoclose = true;
            Application.Restart();
        }

        private void pictureBox3_Click(object sender, EventArgs e)
        {
			API.deletelastgamecostiffree(BLL_BilliardWindowsApplication.gamecostdetailsStatic);
			BallTrackAPI.BTAPI_Free();
			// wanttoclose = true;
			if (BallTrackAPI.m_nInputMethod == 0)
				Process.Start("shutdown", "/s /t 0");
            Application.Exit();
        }

        private void pictureBox2_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
