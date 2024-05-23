using Model;
using Socket_Handler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Game_Server
{
    public class MainServer
    {
        // 모든 핸들러에 RepositoryManager 의존성 주입, string으로 매핑 
        private RepositoryHandler _repositoryManager;
        private Dictionary<string, Handler> _processhandlers;
        private Queue<Message> _messages;
        private Socket _serverSocket;
        private Thread _thread;
        private Logger _logger;
        private int _port;
        private object _messagesLock;

        public MainServer()
        {
            _logger = Logger.Instance;
            _messages = new Queue<Message>();
            _port = 8080;
            _messagesLock = new object();
            _repositoryManager = new RepositoryHandler(_messages, _messagesLock);
            _processhandlers = initializeProcessHandlers();
        }

        public Dictionary<string, Handler> initializeProcessHandlers()
        {
            Dictionary<string, Handler> processhandlers = new Dictionary<string, Handler>
            {
                // 리퀘스트 타입으로 구분 -> 추후에 목적지로 수정  
                { "DISCONNECT", new ConnectionHandler(_repositoryManager) },
                // 목적지로 구분 
                { "GAME_ROOM", new GameRoomHandler(_repositoryManager) },
                { "DATABASE", new LoginHandler(_repositoryManager) },
                { "MAIN_ROBBY", new MainRobbyHandler(_repositoryManager) }
            };

            return processhandlers;
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
                    _repositoryManager.InitializeSocketSettings(_serverSocket);
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
                _logger.Log(Logger.LogLevel.Info, $"[메시지] : {message.RequestType}");
                if (_repositoryManager.GetServerSocketHandlerCount() < 0) continue;

                ServerSocketHandler handlerToParse = _repositoryManager.SearchServerSocketHandler(message.Name);
                if (handlerToParse == null) continue;

                if (message.RequestType.Equals("DISCONNECT"))
                {
                    // 모든 if문 제거 후 하나의 로직으로 통합 가능 
                    Handler handler = _processhandlers[message.RequestType];
                    handler.HandleMessage(message, handlerToParse);
                    continue;
                }

                if (message.Destination.Equals("DATABASE"))
                {
                    Handler handler = _processhandlers[message.Destination];
                    handler.HandleMessage(message, handlerToParse);
                    continue;
                }

                if (message.Destination.Equals("GAME_ROOM"))
                {
                    Handler handler = _processhandlers[message.Destination];
                    handler.HandleMessage(message, handlerToParse);
                    continue;
                }


                if (message.Destination.Equals("MAIN_ROBBY"))
                {
                    Handler handler = _processhandlers[message.Destination];
                    handler.HandleMessage(message, handlerToParse);
                    continue;
                }
            }
        }

        public void StartToParseMessages()
        {
            _thread = new Thread(new ThreadStart(Parse));
            _thread.Start();
        }
    }
}
