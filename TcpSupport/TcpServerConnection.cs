using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;

namespace TcpSupport
{
    public class TcpServerConnection
    {
        private TcpClient _mSocket;
        private readonly List<byte[]> _messagesToSend;
        private int _attemptCount;

        private Thread _mThread;

        private DateTime _mLastVerifyTime;

        private Encoding _mEncoding;

        public TcpServerConnection(TcpClient sock, Encoding encoding)
        {
            try
            {
                _mSocket = sock;
                _messagesToSend = new List<byte[]>();
                _attemptCount = 0;

                _mLastVerifyTime = DateTime.UtcNow;
                _mEncoding = encoding;
            }
            catch (Exception)
            {

            }
        }

        public bool Connected()
        {
            try
            {
                return _mSocket.Connected;
            }
            catch (Exception )
            {

                return false;
            }
        }

        public bool VerifyConnected()
        {
            //note: `Available` is checked before because it's faster,
            //`Available` is also checked after to prevent a race condition.
            bool connected = _mSocket.Client.Available != 0 || !_mSocket.Client.Poll(1, SelectMode.SelectRead) || _mSocket.Client.Available != 0;
            _mLastVerifyTime = DateTime.UtcNow;
            return connected;
        }

        public bool ProcessOutgoing(int maxSendAttempts)
        {
            try
            {
                lock (_mSocket)
                {
                    if (!_mSocket.Connected)
                    {
                        _messagesToSend.Clear();
                        return false;
                    }

                    if (_messagesToSend.Count == 0)
                    {
                        return false;
                    }

                    NetworkStream stream = _mSocket.GetStream();
                    try
                    {
                        stream.Write(_messagesToSend[0], 0, _messagesToSend[0].Length);

                        lock (_messagesToSend)
                        {
                            _messagesToSend.RemoveAt(0);
                        }
                        _attemptCount = 0;
                    }
                    catch (System.IO.IOException)
                    {
                        //occurs when there's an error writing to network
                        _attemptCount++;
                        if (_attemptCount >= maxSendAttempts)
                        {
                            lock (_messagesToSend)
                            {
                                _messagesToSend.RemoveAt(0);
                            }
                            _attemptCount = 0;
                        }
                    }
                    catch (ObjectDisposedException )
                    {
                        //occurs when stream is closed
                        _mSocket.Close();
                        return false;
                    }
                }
                return _messagesToSend.Count != 0;
            }
            catch (Exception )
            {

            }
            return false;
        }

        public void SendData(string data)
        {
            try
            {
                byte[] array = _mEncoding.GetBytes(data);
                lock (_messagesToSend)
                {
                    _messagesToSend.Add(array);
                }
            }
            catch (Exception )
            {

            }
        }

        public void ForceDisconnect()
        {
            try
            {
                lock (_mSocket)
                {
                    _mSocket.Close();
                }
            }
            catch (Exception )
            {

            }
        }

        public bool HasMoreWork()
        {
            try
            {
                return _messagesToSend.Count > 0 || (Socket.Available > 0 && CanStartNewThread());
            }
            catch (Exception )
            {

            }
            return false;
        }

        private bool CanStartNewThread()
        {
            if (_mThread == null)
            {
                return true;
            }
            return (_mThread.ThreadState & (ThreadState.Aborted | ThreadState.Stopped)) != 0 &&
                   (_mThread.ThreadState & ThreadState.Unstarted) == 0;
        }

        public TcpClient Socket
        {
            get
            {
                return _mSocket;
            }
            set
            {
                _mSocket = value;
            }
        }

        public Thread CallbackThread
        {
            get
            {
                return _mThread;
            }
            set
            {
                if (!CanStartNewThread())
                {
                    throw new Exception("Thread is running. Can create new thread!");
                }
                _mThread = value;
            }
        }

        public DateTime LastVerifyTime
        {
            get { return _mLastVerifyTime; }
        }
        //public DateTime LastVerifyTime => _mLastVerifyTime;

        public Encoding Encoding
        {
            get
            {
                return _mEncoding;
            }
            set
            {
                _mEncoding = value;
            }
        }

        public IPEndPoint RemoteEndPoint
        {
            get { return _mSocket.Client.RemoteEndPoint as IPEndPoint; }
        }
        //public IPEndPoint RemoteEndPoint => _mSocket.Client.RemoteEndPoint as IPEndPoint;
    }
}