using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TcpSupport;

namespace Server.Ver1
{
    public partial class FrmServer : Form
    {
        public ServerTcp ServerTcp { get; set; }
        public FrmServer()
        {
            InitializeComponent();
            string ip = this.tbServerAddress.Text.Split(':')[0];
            int port = int.Parse(this.tbServerAddress.Text.Split(':')[1]);
            this.ServerTcp = new ServerTcp(ip, port);
            this.lbListenStatus.Text = "Status: Listening!";
            this.ServerTcp.ConnectAccepted += ServerTcp_ConnectAccepted;
        }

        private void ServerTcp_ConnectAccepted(object sender, string ip)
        {
            this.ServerTcp.ConnectedClients[ip].Received += ServerMain_Received;
        }

        private void ServerMain_Received(object sender, EventArgs e)
        {
            this.MessageBox.Invoke(new Action(() =>
            {
                this.MessageBox.Text = ((ClientEventArgs)e).RecieveData.Length.ToString();
            }));
        }
    }
}
