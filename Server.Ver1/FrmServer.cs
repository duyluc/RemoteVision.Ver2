using Cognex.VisionPro;
using PyVisionSupport;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TcpSupport;

namespace Server.Ver1
{
    public partial class FrmServer : Form
    {
        public FrmServer()
        {
            InitializeComponent();
            string ip = this.tbServerAddress.Text.Split(':')[0];
            int port = int.Parse(this.tbServerAddress.Text.Split(':')[1]);
            this.lbListenStatus.Text = "Status: Listening!";
        }

        private void ServerTcp_OnDataAvailable(TcpServerConnection connection)
        {
            byte[] _receiveData = ReadStream(connection.Socket);
            Dictionary<string, Terminal> Input = Serialize.ByteArrayToObject(_receiveData);
            Bitmap bitimg = (Input["OutputImage"].Value as LImage).BitmapImage;
            CogImage8Grey img = new CogImage8Grey(bitimg);
            this.Display.Invoke(new Action(() => { this.Display.Image = img; }));
            lbxMain.Invoke(new Action(()=> 
            {
                lbxMain.Items.Insert(0, DateTime.Now.ToShortTimeString() + ": " + $"Received: {_receiveData.Length} byte"); 
            }));
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            try
            {
                try
                {
                    OpenTcpServer();
                    lbxMain.Items.Insert(0, DateTime.Now.ToShortTimeString() + ": Server Opened!");
                }
                catch (FormatException)
                {
                    lbxMain.Items.Insert(0, DateTime.Now.ToShortTimeString() + ": Ethernet Port format wrong!");
                }
                catch (OverflowException)
                {
                    lbxMain.Items.Insert(0, DateTime.Now.ToShortTimeString() + ": Ethernet Port format over!");
                }
            }
            catch (Exception ex)
            {
                lbxMain.Items.Insert(0, DateTime.Now.ToShortTimeString() + ": " + ex.Message);
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            try
            {
                btnClose.Visible = false;
                btnOpen.Visible = true;
                //timerUpDate.Enabled = false;
                ServerTcp.Close();
            }
            catch (Exception ex)
            {
                lbxMain.Items.Add(DateTime.Now.ToShortTimeString() + ": " + ex.Message);
            }
        }

        public byte[] ReadStream(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                if (stream.DataAvailable)
                {
                    byte[] _datalengthbyte = new byte[4];
                    int read = stream.Read(_datalengthbyte, 0, 4);
                    if (read != 4) throw new Exception("Error Data length");
                    int _receivedatalength = BitConverter.ToInt32(_datalengthbyte,0);
                    byte[] data = new byte[_receivedatalength];
                    int offset = 0;
                    try
                    {
                        while (true)
                        {
                            read = stream.Read(data, offset, data.Length - offset);
                            offset += read;
                            if (offset == data.Length) break;
                        }
                    }
                    catch (IOException ex)
                    {
                        lbxMain.Invoke((MethodInvoker)delegate
                        {
                            lbxMain.Items.Insert(0, DateTime.Now.ToShortTimeString() + ": " + ex.Message);
                        });
                    }
                    return data;
                }
            }
            catch (Exception ex)
            {
                lbxMain.Invoke((MethodInvoker)delegate
                {
                    lbxMain.Items.Insert(0, DateTime.Now.ToShortTimeString() + ": " + ex.Message);
                });
            }
            return null;
        }
        private void OpenTcpServer()
        {
            try
            {
                string IPAddress = this.tbServerAddress.Text.Split(':')[0];
                int Port = int.Parse(this.tbServerAddress.Text.Split(':')[1]);
                ServerTcp.Close();
                ServerTcp.Port = Convert.ToInt32(Port);
                ServerTcp.IdleTime = 50;
                ServerTcp.Open(IPAddress);
                btnOpen.Visible = false;
                btnClose.Visible = true;
            }
            catch (Exception ex)
            {
                lbxMain.Items.Insert(0, DateTime.Now.ToShortTimeString() + ": " + ex.Message);
                btnClose.Visible = false;
                btnOpen.Visible = true;
            }
        }
    }
}
