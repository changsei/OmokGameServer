using DB_Handler;
using Socket_Handler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using static System.Net.Mime.MediaTypeNames;
using System.Xml.Linq;
using Net_Framework_4._7._2_Model;

namespace Game_Server
{
    public class ServerSocketHandler : SocketHandler
    {
        private System.Timers.Timer _timer;
        private int _time = 1000;
        public int UserCount { get; set; }
        public override void Parse(string jsonString)
        {
            Message message = ConvertToMessage(jsonString);

            // 연결 상태 처리 
            if (message.RequestType.Equals("CONNECT")) return;

            string log = this.Name + jsonString.ToString();
            _logger.Log(Logger.LogLevel.Info, log);

            // 계정 관련 요청 전 처리
            if (message.RequestType.Equals("LOGIN"))
            {
                this.Name = message.Name;
                EnqueueMessage(message);
                return;
            }

            if (message.RequestType.Equals("REGIST"))
            {
                this.Name = message.Name + UserCount;
                this.Send(CreateTheData(new Message
                {
                    Name = this.Name,
                    RequestType = "REGIST_RESPONSE",
                    Text = "CONNECTED"
                })); 
                return;
            }

            if (message.RequestType.Equals("UNREGIST"))
            {
                this.Name = message.Name + UserCount;
                this.Send(CreateTheData(new Message
                {
                    Name = this.Name,
                    RequestType = "UNREGIST_RESPONSE"
                }));
                return;
            }

            if (message.RequestType.Equals("SEARCH"))
            {
                this.Name = message.Name + UserCount;
                this.Send(CreateTheData(new Message
                {
                    Name = this.Name,
                    RequestType = "SEARCH_RESPONSE"
                }));
                EnqueueMessage(message);
                return;
            }

            if (message.RequestType.Equals("DELETE"))
            {
                this.Send(CreateTheData(new Message
                {
                    Name = this.Name,
                    RequestType = "DELETE_RESPONSE"
                }));
            }

            // DB 접속, 게임 방 접속 등 나머지 데이터 처리 
            EnqueueMessage(message);        
        }

        public void StopToConnectionTimer()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Elapsed -= OnConnectiontimerElapsed; 
                _timer.Dispose();
                _timer = null;
            }
        }

        private void OnConnectiontimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (Socket.Connected)
            {
                Message message = new Message { RequestType = "CONNECT" };
                Send(CreateTheData(message));
            } 
            else
            {
                StopToConnectionTimer();
                _logger.Log(Logger.LogLevel.Info, this.Name + "의 Connection Timer 종료");
            }
        }

        public void StartToConnectionTimer()
        {
            _timer = new System.Timers.Timer(_time);
            _timer.Elapsed += OnConnectiontimerElapsed;
            _timer.Start();
            _logger.Log(Logger.LogLevel.Info, this.Name + "의 Connection Timer 시작");
        }
    }

    public class MainServer
    {
        private List<ServerSocketHandler> _serverSocketHandlers;
        private List<User> _users;
        private Queue<Message> _messages;
        private ServerSocketHandler _serverSocketHandler;
        private DBHandler _dbHandler;
        private Socket _serverSocket;
        private Thread _thread;
        private Logger _logger;
        private int _port;
        private int _userCount;
        private object _messagesLock;
        private object _handlersLock;

        public MainServer()
        {
            _logger = Logger.Instance;
            _dbHandler = DBHandler.Instance;
            _dbHandler.ConnectToDB();
            _users = _dbHandler.SearchAll();

            foreach (var user in _users)
            {
                _logger.Log(Logger.LogLevel.Info, user.Password);
            }

            _serverSocketHandlers = new List<ServerSocketHandler>();
            _messages = new Queue<Message>();
            _port = 8080;
            _messagesLock = new object();
            _handlersLock = new object();
        }

        public void AcceptToConnection()
        {
            try
            {
                _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPEndPoint serverEP = new IPEndPoint(IPAddress.Any, _port);
                _serverSocket.Bind(serverEP);
                _serverSocket.Listen(10);

                while (true)
                {
                    _serverSocketHandler = new ServerSocketHandler();
                    _serverSocketHandler.SetLooger(_logger);
                    _logger.Log(Logger.LogLevel.Info, "클라이언트의 연결 요청을 대기합니다.");
                    _serverSocketHandler.Socket = _serverSocket.Accept();
                    _logger.Log(Logger.LogLevel.Info, "클라이언트가 연결 되었습니다.");
                    _serverSocketHandler.OpenStream();
                    _serverSocketHandler.SetMessageQueue(_messages);
                    _serverSocketHandler.SetLockObject(_messagesLock);
                    _serverSocketHandler.SetKeepAlive();
                    _serverSocketHandler.StartToConnectionTimer();
                    _serverSocketHandler.StartToReceive();

                    lock (_handlersLock)
                    {
                        _serverSocketHandlers.Add(_serverSocketHandler);
                        _serverSocketHandler.UserCount = ++_userCount;
                        _logger.Log(Logger.LogLevel.Info, "유저 카운터: " + _serverSocketHandler.UserCount);
                    }               
                }
            }
            catch (IOException e)
            {
                _logger.Log(Logger.LogLevel.Error, e.ToString());
            }
        }

        public Message DequeueFromMessages()
        {
            lock (_messagesLock)
            {
                return _messages.Dequeue();
            }
        }
        private void Parse() 
        {
            while (true)
            {
                if (_messages.Count <= 0)
                {
                    Thread.Sleep(100);
                    continue;
                }

                Message message = DequeueFromMessages();

                _logger.Log(Logger.LogLevel.Info, message.RequestType);

                if (_serverSocketHandlers.Count < 0) continue;

                var handlerToParse = _serverSocketHandlers.FirstOrDefault(handler => handler.Name.Equals(message.Name));

                if (handlerToParse == null) continue;

                if (message.RequestType.Equals("DISCONNECT"))
                {
                    lock (_handlersLock)
                    {
                        _serverSocketHandlers.Remove(handlerToParse);
                        --_userCount;
                        string log = "핸들러 제거됨: " + handlerToParse.Name + ", 유저 카운터: " + (_userCount);
                        _logger.Log(Logger.LogLevel.Info, log);
                        continue;
                    }
                } 

                if (message.RequestType.Equals("LOGIN"))
                {
                    bool searchResult = false;

                    foreach (var user in _users)
                    {
                        if (!handlerToParse.Name.Equals(user.ID)) continue;

                        if (message.Text == user.Password)
                        {
                            searchResult = true;

                            string log = "유저 로그인: " + handlerToParse.Name + ", 유저 카운터: " + (_userCount);
                            _logger.Log(Logger.LogLevel.Info, log);

                            handlerToParse.Send(handlerToParse.CreateTheData(new Message
                            {
                                RequestType = "LOGIN_RESPONSE",
                                Text = "ACCEPT_NORMAL_USER"
                            }));

                            break;
                        }
                    }

                    if (!searchResult)
                    {
                        handlerToParse.Send(handlerToParse.CreateTheData(new Message
                        {
                            // 수정 필요
                            RequestType = "LOGIN_RESPONSE",
                            Text = "NOT_FOUNT_USER"
                        }));

                        continue;
                    }            
                }

                if (message.RequestType.Equals("REGIST_USER_ID/PW"))
                {
                    bool registResult = false;

                    foreach (var user in _users)
                    {
                        if (handlerToParse.Name.Equals(user.ID)) continue;
                        string[] textArr = SplitTextInMessage(message.Text);

                        _dbHandler.Insert(new User
                        {
                            ID = textArr[0],
                            Name = textArr[1],
                            Password = textArr[2],
                            PhoneNumber = textArr[3]
                        });

                        _logger.Log(Logger.LogLevel.Info, message.Name + " 회원 정보가 DB에 등록 되었습니다.");
                        registResult = true;

                        break;
                    }

                    _logger.Log(Logger.LogLevel.Info, "결과: " + registResult);
                    if (!registResult)
                    {
                        handlerToParse.Send(handlerToParse.CreateTheData(new Message
                        {
                            RequestType = "REGIST_RESPONSE",
                            Text = "REFUSED"
                        }));

                        continue;
                    }

                    if (registResult)
                    {
                        handlerToParse.Send(handlerToParse.CreateTheData(new Message
                        {
                            RequestType = "REGIST_RESPONSE",
                            Text = "COMPLETED"
                        }));
                        _users = _dbHandler.SearchAll();
                    }
                }
            }
        }

        public void StartToParseMessages()
        {
            _thread = new Thread(new ThreadStart(Parse));
            _thread.Start();
        }

        public void SendToClient(Func<Message> delegateMessage)
        {
            Message message = delegateMessage();
            string data = _serverSocketHandler.CreateTheData(message);
            _serverSocketHandler.Send(data);
        }

        public string[] SplitTextInMessage(string textInMessage)
        {
            string[] textArr = textInMessage.ToString().Split(',');
            return textArr;
        }
    }


    internal class Program
    {
        static void Main(string[] args)
        {
            MainServer mainServer = new MainServer();
            mainServer.StartToParseMessages();
            mainServer.AcceptToConnection();
        }
    }
}
