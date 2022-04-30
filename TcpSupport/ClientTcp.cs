using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TcpSupport
{
    public class ClientTcp:ClientBase
    {
        public IPEndPoint ServerEP { get; set; }
        public ClientTcp(string _iPAddress, int _port) : base()
        {
            this.ServerEP = new IPEndPoint(IPAddress.Parse(_iPAddress), _port);
        }

        private bool TryConnect()
        {
            int _countTryConnect = 0;
            while (true)
            {
                try
                {
                    this.TcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    this.TcpSocket.Connect(this.ServerEP);
                    break;
                }
                catch
                {
                    _countTryConnect++;
                    if (_countTryConnect == 5) break;
                }
            }
            if (this.TcpSocket.Connected) return true;
            return false;
        }

        public async Task Command(byte[] sendData)
        {
            Task _ = new Task(() =>
            {
                try
                {
                    if (!this.TryConnect()) throw ConnectInterruptEx;
                    if (Send(this.TcpSocket, sendData))
                    {
                        this.SendResult = DeleveryResult.Success;
                    }
                    else
                    {
                        this.SendResult = DeleveryResult.Fault;
                    }
                    this.ReceiveResult = DeleveryResult.Fault;
                    byte[] receivedata = Recieve(this.TcpSocket);
                    this.ReceiveResult = DeleveryResult.Success;
                }
                catch (Exception t)
                {

                }
                finally
                {
                    if (this.TcpSocket.Connected) this.TcpSocket.Close();
                }
            });
            _.Start();
            await _;
        }
    }
}
