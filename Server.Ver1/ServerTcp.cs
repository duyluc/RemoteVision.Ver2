using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Server.Ver1
{
    public delegate void TcpServerConnectionChanged(TcpServerConnection connection);
    public delegate void TcpServerError(TcpServer server, Exception e);

    public class TcpServer : Component
    {

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetForegroundWindow(IntPtr hWnd);
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// <summary>
        /// TCPSERVER
        /// </summary>
        private List<TcpServerConnection> _connections;
        private TcpListener _listener;
        private Thread _listenThread;
        private Thread _sendThread;
        private bool _mIsOpen;
        private int _mPort;
        private int _mMaxSendAttempts;
        private int _mIdleTime;
        private int _mMaxCallbackThreads;
        private int _mVerifyConnectionInterval;
        private Encoding _mEncoding;
        private SemaphoreSlim _sem;
        private bool _waiting;
        private int _activeThreads;
        private readonly object _activeThreadsLock = new object();
        public event TcpServerConnectionChanged OnConnect;
        public event TcpServerConnectionChanged OnDataAvailable;

        public event TcpServerError OnError;

        public ListBox _listbox;
        public TcpServer()
        {
            Initialise();
        }

        public TcpServer(IContainer container)
        {
            container.Add(this);
            Initialise();
        }

        private void Initialise()
        {
            try
            {
                _connections = new List<TcpServerConnection>();
                _listener = null;

                _listenThread = null;
                _sendThread = null;

                _mPort = -1;
                _mMaxSendAttempts = 3;
                _mIsOpen = false;
                _mIdleTime = 50;
                _mMaxCallbackThreads = 100;
                _mVerifyConnectionInterval = 100;
                _mEncoding = Encoding.ASCII;

                _sem = new SemaphoreSlim(0);
                _waiting = false;

                _activeThreads = 0;
                //this.Listbox = new ListBox();
            }
            catch (Exception ex)
            {

            }
        }

        public int Port
        {
            get
            {
                return _mPort;
            }
            set
            {
                try
                {
                    if (value < 0)
                    {
                        return;
                    }

                    if (_mPort == value)
                    {
                        return;
                    }

                    if (_mIsOpen)
                    {
                        throw new Exception("Thay đổi cổng kết nối không hợp lệ.\nNgắt kết nối trước khi thay đổi.");
                    }

                    _mPort = value;
                    if (_listener == null)
                    {
                        //this should only be called the first time.
                        _listener = new TcpListener(IPAddress.Any, _mPort);
                    }
                    else
                    {
                        _listener.Server.Bind(new IPEndPoint(IPAddress.Any, _mPort));
                    }
                }
                catch (Exception ex)
                {

                }
            }
        }

        public int MaxSendAttempts
        {
            get
            {
                return _mMaxSendAttempts;
            }
            set
            {
                _mMaxSendAttempts = value;
            }
        }

        [Browsable(false)]
        public bool IsOpen
        {
            get
            {
                return _mIsOpen;
            }
            set
            {
                try
                {
                    if (_mIsOpen == value)
                    {
                        return;
                    }

                    if (value)
                    {
                        Open();
                    }
                    else
                    {
                        Close();
                    }
                }
                catch (Exception ex)
                {

                }
            }
        }

        public List<TcpServerConnection> Connections
        {
            get
            {
                List<TcpServerConnection> rv = new List<TcpServerConnection>();
                rv.AddRange(_connections);
                return rv;
            }
        }

        public int IdleTime
        {
            get
            {
                return _mIdleTime;
            }
            set
            {
                _mIdleTime = value;
            }
        }

        public int MaxCallbackThreads
        {
            get
            {
                return _mMaxCallbackThreads;
            }
            set
            {
                _mMaxCallbackThreads = value;
            }
        }

        public int VerifyConnectionInterval
        {
            get
            {
                return _mVerifyConnectionInterval;
            }
            set
            {
                _mVerifyConnectionInterval = value;
            }
        }

        public Encoding Encoding
        {
            get
            {
                return _mEncoding;
            }
            set
            {
                try
                {
                    Encoding oldEncoding = _mEncoding;
                    _mEncoding = value;
                    lock (_connections)
                    {
                        foreach (TcpServerConnection client in _connections)
                        {
                            if (client.Encoding.Equals(oldEncoding))
                            {
                                client.Encoding = _mEncoding;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {

                }
            }
        }

        public ListBox Listbox { get => _listbox; set => _listbox = value; }

        public void SetEncoding(Encoding encoding, bool changeAllClients)
        {
            try
            {
                //Encoding oldEncoding = _mEncoding;
                _mEncoding = encoding;
                if (changeAllClients)
                {
                    lock (_connections)
                    {
                        foreach (TcpServerConnection client in _connections)
                        {
                            client.Encoding = _mEncoding;
                        }
                    }
                }
            }
            catch (Exception ex)
            {

            }
        }

        private void RunListener()
        {
            while (_mIsOpen && _mPort >= 0)
            {
                try
                {
                    if (_listener.Pending())
                    {
                        TcpClient socket = _listener.AcceptTcpClient();
                        TcpServerConnection conn = new TcpServerConnection(socket, _mEncoding);

                        if (OnConnect != null)
                        {
                            lock (_activeThreadsLock)
                            {
                                _activeThreads++;
                            }
                            conn.CallbackThread = new Thread(() =>
                            {
                                OnConnect(conn);
                                conn.SendData("CONNECT OK");
                            });
                            conn.CallbackThread.Start();
                        }

                        lock (_connections)
                        {
                            _connections.Add(conn);
                        }
                    }
                    else
                    {
                        Thread.Sleep(_mIdleTime);
                    }
                }
                catch (ThreadInterruptedException ex)//thread is interrupted when we quit
                {

                }
                catch (Exception ex)
                {

                    if (_mIsOpen)
                    {
                        if (OnError != null)
                        {
                            OnError.Invoke(this, ex);
                        }
                    }
                }
            }
        }

        private void RunSender()
        {
            while (_mIsOpen && _mPort >= 0)
            {
                try
                {
                    bool moreWork = false;
                    for (int i = 0; i < _connections.Count; i++)
                    {
                        if (_connections[i].CallbackThread != null)
                        {
                            try
                            {
                                _connections[i].CallbackThread = null;
                                lock (_activeThreadsLock)
                                {
                                    _activeThreads--;
                                }
                            }
                            catch (Exception ex)
                            {
                            }
                        }

                        if (_connections[i].CallbackThread != null)
                        {
                        }
                        else if (_connections[i].Connected() &&
                                 (_connections[i].LastVerifyTime.AddMilliseconds(_mVerifyConnectionInterval) >
                                  DateTime.UtcNow ||
                                  _connections[i].VerifyConnected()))
                        {
                            moreWork = moreWork || ProcessConnection(_connections[i]);
                        }
                        else
                        {
                            lock (_connections)
                            {
                                _connections.RemoveAt(i);
                                i--;
                            }
                        }
                    }

                    if (!moreWork)
                    {
                        Thread.Yield();
                        lock (_sem)
                        {
                            foreach (TcpServerConnection conn in _connections)
                            {
                                if (conn.HasMoreWork())
                                {
                                    moreWork = true;
                                    break;
                                }
                            }
                        }
                        if (!moreWork)
                        {
                            _waiting = true;
                            _sem.Wait(_mIdleTime);
                            _waiting = false;
                        }
                    }
                }
                catch (ThreadInterruptedException ex) //thread is interrupted when we quit
                {

                }
                catch (Exception ex)
                {

                    if (_mIsOpen)
                    {
                        if (OnError != null)
                        {
                            OnError.Invoke(this, ex);
                        }
                    }
                }
            }
        }

        private bool IsConnected(TcpClient tcpClient)
        {
            //try
            //{
            //    return !(tcpClient.Client.Poll(1, SelectMode.SelectRead) && tcpClient.Available == 0);
            //}
            //catch (SocketException)
            //{
            //    return false;
            //}

            // Detect if client disconnected 
            //try
            //{
            //    if (tcpClient.Client.Poll(0, SelectMode.SelectRead))
            //    {
            //        byte[] buff = new byte[1];
            //        if (tcpClient.Client.Receive(buff, SocketFlags.Peek) == 0)
            //        {
            //            return false;
            //        }
            //    }
            //}
            //catch (SocketException)
            //{
            //    return false;
            //}
            //return true;

            //// <summary> 
            //// Checks the connection state 
            //// </summary> 
            //// <returns>True on connected. False on disconnected.</returns> 
            //if (tcpClient.Client.Connected)
            //{
            if ((tcpClient.Client.Poll(0, SelectMode.SelectWrite)) && (!tcpClient.Client.Poll(0, SelectMode.SelectError)))
            {
                byte[] buffer = new byte[1];
                if (tcpClient.Client.Receive(buffer, SocketFlags.Peek) == 0)
                {
                    return false;
                }
                return true;
            }
            return false;
            //}
            //return false;
        }

        public int ConnectedCount()
        {
            int count = _connections.Count;
            try
            {
                lock (_connections)
                {
                    foreach (TcpServerConnection conn in _connections)
                    {
                        //if (!IsConnected(conn.Socket))
                        //{
                        //    count = count - 1;
                        //}
                        if (conn.Socket.Client.Poll(0, SelectMode.SelectRead) && conn.Socket.Available == 0)
                        {
                            byte[] buff = new byte[1];
                            if (conn.Socket.Client.Receive(buff, SocketFlags.Peek) == 0)
                            {
                                count = count - 1;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {

            }
            return count;
        }

        private bool ProcessConnection(TcpServerConnection conn)
        {
            bool moreWork = false;
            try
            {
                moreWork = conn.ProcessOutgoing(_mMaxSendAttempts);
                if (OnDataAvailable != null && _activeThreads < _mMaxCallbackThreads && conn.Socket.Available > 0)
                {
                    lock (_activeThreadsLock)
                    {
                        _activeThreads++;
                    }
                    conn.CallbackThread = new Thread(() =>
                    {
                        OnDataAvailable(conn);
                    });
                    conn.CallbackThread.Start();
                    Thread.Yield();
                }
            }
            catch (Exception ex)
            {

            }

            return moreWork;
        }

        public void Open()
        {
            try
            {
                lock (this)
                {
                    if (_mIsOpen)
                    {
                        //already open, no work to do
                        return;
                    }
                    if (_mPort < 0)
                    {
                        throw new Exception("Cổng kết nối không hợp lệ");
                    }

                    try
                    {
                        _listener.Start(5);
                    }
                    catch (Exception ex)
                    {

                        _listener.Stop();
                        _listener = new TcpListener(IPAddress.Any, _mPort);
                        _listener.Start(5);
                    }

                    _mIsOpen = true;

                    _listenThread = new Thread(RunListener);
                    _listenThread.Start();

                    _sendThread = new Thread(RunSender);
                    _sendThread.Start();
                }
            }
            catch (Exception ex)
            {

            }
        }

        public void Open(string ipAddress)
        {
            try
            {
                lock (this)
                {
                    if (_mIsOpen)
                    {
                        //already open, no work to do
                        return;
                    }
                    if (_mPort < 0)
                    {
                        throw new Exception("Ethernet Port is not accepted!");
                    }

                    try
                    {

                        _listener = new TcpListener(IPAddress.Parse(ipAddress), _mPort);
                        _listener.Start(30);
                        //Listbox.Items.Add(DateTime.Now.ToShortTimeString() + ": Created server " + ipAddress + ":" + _mPort.ToString());
                    }
                    catch (Exception ex)
                    {

                        _listener.Stop();
                        //string IP = GetLocalIPv4(NetworkInterfaceType.Wireless80211);
                        _listener = new TcpListener(IPAddress.Parse(ipAddress), _mPort);
                        _listener.Start(30);
                        //Listbox.Items.Add(DateTime.Now.ToShortTimeString() + ": Created server " + ipAddress + ":" + _mPort.ToString());
                    }

                    _mIsOpen = true;

                    _listenThread = new Thread(RunListener);
                    _listenThread.Start();

                    _sendThread = new Thread(RunSender);
                    _sendThread.Start();
                }
            }
            catch (Exception ex)
            {

            }
        }

        public void Close()
        {
            try
            {
                if (!_mIsOpen)
                {
                    return;
                }

                lock (this)
                {
                    _mIsOpen = false;
                    foreach (TcpServerConnection conn in _connections)
                    {
                        conn.ForceDisconnect();
                    }
                    try
                    {
                        if (_listenThread.IsAlive)
                        {
                            _listenThread.Interrupt();

                            Thread.Yield();
                            if (_listenThread.IsAlive)
                            {
                                _listenThread.Abort();
                            }
                        }
                    }
                    catch (System.Security.SecurityException ex)
                    {

                    }
                    try
                    {
                        if (_sendThread.IsAlive)
                        {
                            _sendThread.Interrupt();

                            Thread.Yield();
                            if (_sendThread.IsAlive)
                            {
                                _sendThread.Abort();
                            }
                        }
                    }
                    catch (System.Security.SecurityException ex)
                    {

                    }
                }
                _listener.Stop();

                lock (_connections)
                {
                    _connections.Clear();
                }

                _listenThread = null;
                _sendThread = null;
                GC.Collect();
                //Listbox.Items.Add(DateTime.Now.ToShortTimeString() + ": Closed server!");
            }
            catch (Exception ex)
            {

            }
        }

        public void Send(string data)
        {
            try
            {
                lock (_sem)
                {
                    foreach (TcpServerConnection conn in _connections)
                    {
                        //if (conn.Connected())
                        //{
                        conn.SendData(data);
                        //}
                    }
                    Thread.Yield();
                    if (_waiting)
                    {
                        _sem.Release();
                        _waiting = false;
                    }
                }
            }
            catch (Exception ex)
            {

            }
        }

        public void Send(string data, string ip)
        {
            try
            {
                lock (_sem)
                {
                    foreach (TcpServerConnection conn in _connections)
                    {
                        if (conn.RemoteEndPoint.Address.ToString() == ip)
                        {
                            conn.SendData(data);
                            break;
                        }
                    }
                    Thread.Yield();
                    if (_waiting)
                    {
                        _sem.Release();
                        _waiting = false;
                    }
                }
            }
            catch (Exception ex)
            {

            }
        }
        public string GetLocalIPv4(NetworkInterfaceType _type)
        {
            string output = "";
            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (item.NetworkInterfaceType == _type && item.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (UnicastIPAddressInformation ip in item.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            output = ip.Address.ToString();
                        }
                    }
                }
            }
            return output;
        }
    }
}
