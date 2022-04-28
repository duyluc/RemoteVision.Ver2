using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TcpSupport
{
    public class ServerTcp
    {
        public enum Status
        {
            Listening,
            DisListen,
            Free,
            Running,
            Stoped,
        }

        public Status ListenrStatus { get; set; } = Status.Free;
        public Status ServerStatus { get; set; } = Status.Stoped;

        public event EventHandler Listening;
        public event EventHandler DisListen;
        public delegate void ReceiveDelegate(object sender, byte[] receivedata);
        public event ReceiveDelegate Received;

        public delegate void ConnectAcceptedDelegate(object sender, string ip);
        public event ConnectAcceptedDelegate ConnectAccepted;

        public IPEndPoint TcpIPEndpoint { get; set; }
        public Socket Listener { get; set; }
        public Dictionary<string, ClientBase> ConnectedClients;
        public Dictionary<string, Task> ClientServiceTasks;
        public Dictionary<string, Terminal> Input;

        public bool EnableServer { get; set; } = false;

        public ServerTcp(string _iPAddress, int _port)
        {
            ConnectedClients = new Dictionary<string, ClientBase>();
            ClientServiceTasks = new Dictionary<string, Task>();
            Input = new Dictionary<string, Terminal>();
            if (InitServer())
            {
                Task _ = this.RunServer();
            }
        }

        public bool InitServer()
        {
            bool _result = false;
            try
            {
                this.Listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                Thread _t = new Thread(() =>
                {
                    int _tryconnnectcounter = 0;
                    while (true)
                    {
                        try
                        {
                            this.Listener.Bind(this.TcpIPEndpoint);
                            OnListening();
                            _result = true;
                            break;
                        }
                        catch
                        {
                            _tryconnnectcounter++;
                            if (_tryconnnectcounter == 5)
                            {
                                break;
                            }
                        }
                        Thread.Sleep(20);
                    }
                });
            }
            catch (Exception t)
            {
                EndServer();
                throw t;
            }
            finally
            {

            }
            return _result;
        }
        public bool EndServer()
        {
            try
            {
                if (this.Listener.IsBound) this.Listener.Close();
                if (this.EnableServer) this.EnableServer = false;
                return true;
            }
            catch (Exception t)
            {
                return false;
            }
            finally
            {

            }
        }
        public async Task RunServer()
        {
            Listener.Listen(10);
            Task _task = new Task(() =>
            {
                Thread _t = new Thread(() =>
                {
                    while (EnableServer)
                    {
                        Socket _clientsocket = Listener.Accept();
                        ClientBase _client = new ClientBase(_clientsocket);
                        string _ipaddress = ((IPEndPoint)_clientsocket.RemoteEndPoint).Address.ToString();
                        if (this.ConnectedClients.ContainsKey(_ipaddress))
                        {
                            this.ConnectedClients[_ipaddress] = _client;
                        }
                        else
                        {
                            this.ConnectedClients.Add(_ipaddress, _client);
                        }
                        OnConnectAccepted(_ipaddress);
                        Task _servertask = ClientServiceTask(_ipaddress);
                        this.ClientServiceTasks.Add(_ipaddress, _servertask);
                    }
                });
                _t.Start();
                while (EnableServer)
                {
                    Thread.Sleep(10);
                }
                if (_t.IsAlive) _t.Abort();
            });
            _task.Start();
            await _task;
        }

        public async Task ClientServiceTask(string ip)
        {
            try
            {
                var client = this.ConnectedClients[ip];
                byte[] recieveData;
                client.Received += Client_Received;
                Task _task = new Task(() =>
                {
                    recieveData = client.Recieve(client.TcpSocket);
                });

                _task.Start();
                await _task;
            }
            catch (Exception t)
            {

            }
            finally
            {

            }
        }

        private void Client_Received(object sender, EventArgs e)
        {

        }

        public void OnReceived(byte[] _receivedata)
        {
            this.Received?.Invoke(this, _receivedata);
        }

        public void OnConnectAccepted(string ip)
        {
            this.ConnectAccepted?.Invoke(this, ip);
        }

        public void OnListening()
        {
            Listening?.Invoke(this, EventArgs.Empty);
            ListenrStatus = Status.Listening;
        }
        public void OnDisListen()
        {
            DisListen?.Invoke(this, EventArgs.Empty);
            ListenrStatus = Status.DisListen;
        }
    }
}
