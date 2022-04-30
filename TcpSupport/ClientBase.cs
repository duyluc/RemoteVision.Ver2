using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace TcpSupport
{
    public class ClientBase
    {
        public enum DeleveryStatus
        {
            Free,
            Sending,
            Sended,
            Receiving,
            Received,
        }
        public enum DeleveryResult
        {
            Free,
            Success,
            Fault
        }

        public DeleveryStatus SendStatus { get; set; } = DeleveryStatus.Free;
        public DeleveryStatus ReceiveStatus { get; set; } = DeleveryStatus.Free;
        public DeleveryResult SendResult { get; set; } = DeleveryResult.Free;
        public DeleveryResult ReceiveResult { get; set; } = DeleveryResult.Free;

        public event EventHandler Sending;
        public event EventHandler Sended;
        public event EventHandler Receiving;
        public event EventHandler Received;

        public Socket TcpSocket { get; set; }

        public int SendTimeout { get; set; } = 3000; //3000 ms
        public int ReceivetimeOut { get; set; } = 3000; //3000 ms
        private int WaitingGetSampleTime = 10; //10 ms
        protected Exception ConnectInterruptEx { get; set; } = new Exception("0x00-->Connect is Interrupted!");

        public ClientBase()
        {
        }

        public ClientBase(Socket _tcpSocket)
        {
            this.TcpSocket = _tcpSocket;
        }

        public byte[] Recieve(Socket _tcpSocket)
        {
            byte[] _recieveData = null;
            try
            {
                // Temporary variation for handler Receive status
                bool _receivecomplite = false;
                //bool _istimeout = false;
                int _timeout = this.ReceivetimeOut / this.WaitingGetSampleTime;
                int _waitingcounter = 0;
                byte[] byte_receivedatalength = new byte[4];
                int _receivedatalength = 0;
                int offset = 0;
                if (!_tcpSocket.Connected) throw this.ConnectInterruptEx;

                Thread _t = new Thread(() =>
                {
                    while (_tcpSocket.Available == 0)
                    {
                        Thread.Sleep(this.WaitingGetSampleTime);
                    }
                    int read = _tcpSocket.Receive(byte_receivedatalength, 0, 4, SocketFlags.None);
                    if (read < 4)
                    {
                        throw new Exception("0x02-->Memo Data length is invalid!");
                    }
                    _receivedatalength = BitConverter.ToInt32(byte_receivedatalength, 0);
                    _recieveData = new byte[_receivedatalength];
                    while (true)
                    {
                        read = _tcpSocket.Receive(_recieveData, offset, _receivedatalength - offset, SocketFlags.None);
                        offset += read;
                        if (offset == _receivedatalength) break;
                    }
                    _receivecomplite = true;
                });
                _t.Start();
                while (!_receivecomplite && _waitingcounter < _timeout)
                {
                    Thread.Sleep(this.WaitingGetSampleTime);
                    _waitingcounter++;
                }
                if (!_receivecomplite)
                {
                    if (_t.IsAlive) _t.Abort();
                    throw new Exception("0x01-->Receive Timeout!");
                }
            }
            catch (Exception t)
            {
                _recieveData = null;
            }
            finally
            {

            }
            OnReceived(_recieveData);
            return _recieveData;
        }

        public bool Send(Socket _tcpSocket, byte[] _sendData)
        {
            bool _sendcomplite = false;
            int offset = 0;
            try
            {
                int _datalength = _sendData.Length;
                //bool _istimeout = false;
                int _timeout = this.ReceivetimeOut / this.WaitingGetSampleTime;
                int _waitingcounter = 0;
                byte[] byte_receivedatalength = BitConverter.GetBytes(_datalength);
                if (!_tcpSocket.Connected) throw this.ConnectInterruptEx;

                Thread _t = new Thread(() =>
                {
                    while (true)
                    {
                        int write = _tcpSocket.Send(byte_receivedatalength, offset, 4 - offset, SocketFlags.None);
                        offset += write;
                        if (offset == 4) break;
                    }
                    Thread.Sleep(20);
                    offset = 0;
                    while (true)
                    {
                        int write = _tcpSocket.Send(_sendData, offset, _datalength - offset, SocketFlags.None);
                        offset += write;
                        if (offset == _datalength) break;
                    }
                    _sendcomplite = true;
                });

                _t.Start();
                while (!_sendcomplite && _waitingcounter < _timeout)
                {
                    Thread.Sleep(this.WaitingGetSampleTime);
                    _waitingcounter++;
                }
                if (!_sendcomplite)
                {
                    if (_t.IsAlive) _t.Abort();
                    throw new Exception("0x03-->Send Timeout!");
                }

            }
            catch (Exception t)
            {
                _sendcomplite = false;
            }
            finally
            {

            }
            OnSended(offset);
            return _sendcomplite;
        }

        public void OnSending()
        {
            Sending?.Invoke(this, EventArgs.Empty);
            SendStatus = DeleveryStatus.Sending;
        }

        public void OnSended(int sended)
        {
            Sended?.Invoke(this, new ClientEventArgs(sended));
            SendStatus = DeleveryStatus.Sended;
        }

        public void OnReceiving()
        {
            Receiving?.Invoke(this, EventArgs.Empty);
            ReceiveStatus = DeleveryStatus.Receiving;
        }

        public void OnReceived(byte[] recievedata)
        {
            ClientEventArgs args = new ClientEventArgs(recievedata);
            Received?.Invoke(this, args);
            ReceiveStatus = DeleveryStatus.Received;
        }
    }
}
