using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TcpSupport
{
    public class ClientEventArgs:EventArgs
    {
        public byte[] RecieveData { get; set; }

        public ClientEventArgs(byte[] receivedata)
        {
            this.RecieveData = receivedata;
        }
    }
}
