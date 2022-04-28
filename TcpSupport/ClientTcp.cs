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
            try
            {
                Task _ = new Task(() =>
                {
                    if (!this.TryConnect()) throw ConnectInterruptEx;
                    Send(this.TcpSocket, sendData);
                });
                _.Start();
                await _;
            }
            catch (Exception t)
            {
                throw t;
            }
            finally
            {
                if (this.TcpSocket.Connected) this.TcpSocket.Close();
            }
        }
    }
}
