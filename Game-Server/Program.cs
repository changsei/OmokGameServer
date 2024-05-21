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
using Server_Repository;
using Message = Net_Framework_4._7._2_Model.Message;
using System.Runtime.Remoting.Messaging;
using Server_Socket_Handler;
using System.Diagnostics.Eventing.Reader;
using Server_Omok_Game;

namespace Game_Server
{
    public class RepositoryManager
    {
        private ServerSokcetReopsitory _serverSocketReopsitory;
        private UserRepository _userRepository;
        private GameRoomRepository _gameRoomRepository;
        private Queue<Message> _messages;
        private Logger _logger;
        private object _messageLock;
        private object _handlersLock;

        public RepositoryManager(Queue<Message> messages, object messageLock)
        {
            _userRepository = new UserRepository();
            _gameRoomRepository = new GameRoomRepository();
            _serverSocketReopsitory = new ServerSokcetReopsitory();
            _handlersLock = _serverSocketReopsitory.GetLock();
            _messageLock = messageLock;
            _messages = messages;
            _logger = Logger.Instance;
        }

        public void InitializeSocketSettings(Socket serverSocket)
        {
            ServerSocketHandler _serverSocketHandler = new ServerSocketHandler();
            _serverSocketHandler.SetLooger(_logger);
            _logger.Log(Logger.LogLevel.Info, $"[클라이언트의 연결 요청 대기]");
            _serverSocketHandler.Socket = serverSocket.Accept();
            _logger.Log(Logger.LogLevel.Info, $"[클라이언트 연결 승인]");
            _serverSocketHandler.OpenStream();
            _serverSocketHandler.SetMessageQueue(_messages);
            _serverSocketHandler.SetLockObject(_messageLock);
            _serverSocketHandler.SetKeepAlive();
            _serverSocketHandler.StartToConnectionTimer();
            _serverSocketHandler.StartToReceive();

            lock (_handlersLock)
            {
                _serverSocketReopsitory.AddServerSocketHandler(_serverSocketHandler);
                _serverSocketHandler.UserCount = _serverSocketReopsitory.GetUserCount();
                _logger.Log(Logger.LogLevel.Info, $"[유저 카운터]: {_serverSocketHandler.UserCount}");
            }
        }

        public void RemoveServerSocketHandler(string handlerNameToRemove)
        {
            ServerSocketHandler handlerToRemove = _serverSocketReopsitory.RemoveServerSocketHandler(handlerNameToRemove);
            int userCount = _serverSocketReopsitory.GetUserCount();
            _logger.Log(Logger.LogLevel.Info, $"[핸들러 제거]: {handlerNameToRemove} - [유저 카운터]: {userCount}");
        }

        public ServerSocketHandler SearchServerSocketHandler(string handlerNameToSearch)
        {
            return _serverSocketReopsitory.GetServerSocketHandler(handlerNameToSearch);
        }

        public List<ServerSocketHandler> GetServerSocketHanlders() {
            return _serverSocketReopsitory.GetServerSocketHandlers();
        }

        public int GetServerSocketHandlerCount()
        {
            return _serverSocketReopsitory.GetUserCount();
        }

        public void BroadCastToServerSocketHandlers(Message message)
        {
            List<ServerSocketHandler> serverSocketHandlers = _serverSocketReopsitory.GetServerSocketHandlers();

            foreach (ServerSocketHandler handler in serverSocketHandlers) {
                handler.Send(handler.CreateTheData(message));
            }
        }

        public GameRoomRepository GetGameRoomRepository()
        {
            return this._gameRoomRepository;
        }

        public UserRepository GetUserRepository()
        {
            return this._userRepository;
        }
    }
    
    public interface Handler
    {
        void HandleMessage(Message message, ServerSocketHandler serverSocketHandler);
    }

    public class ConnectionHandler : Handler
    {
        private RepositoryManager _repositoryManager;
        private UserRepository _userRepository;
        private GameRoomRepository _gameRoomRepository;

        public ConnectionHandler(RepositoryManager repositoryManager)
        {
            this._repositoryManager = repositoryManager;
            _userRepository = _repositoryManager.GetUserRepository();
            _gameRoomRepository = _repositoryManager.GetGameRoomRepository();
        }

        public void HandleMessage(Message message, ServerSocketHandler serverSocketHandler)
        {
            ProcessDisConnectResponse(message, serverSocketHandler);
        }

        public void ProcessDisConnectResponse(Message message, ServerSocketHandler serverSocketHandler)
        {
            _repositoryManager.RemoveServerSocketHandler(serverSocketHandler.Name);
            _userRepository.RemoveUser(message.Name);
            _gameRoomRepository.UpdateDisconnect(message.Name);

            try
            {
                // 유저가 비밀번호 변경을 하지 않았을 경우 버퍼에서 제거
                _userRepository.RemoveUserBuffer(serverSocketHandler.Name);       
                Logger.Instance.Log(Logger.LogLevel.Info, $"[{serverSocketHandler.Name}] 의 임시 정보가 제거 되었습니다.");
            } 
            catch (Exception e) {
                Logger.Instance.Log(Logger.LogLevel.Info, $"[{e.ToString()}]");
            }
        }
    }

    public class GameRoomHandler : Handler
    {
        private RepositoryManager _repositoryManager;
        private GameRoomRepository _gameRoomRepository;

        public GameRoomHandler(RepositoryManager repositoryManager)
        {
            this._repositoryManager = repositoryManager;
            _gameRoomRepository = _repositoryManager.GetGameRoomRepository();
        }

        public void HandleMessage(Message message, ServerSocketHandler serverSocketHandler)
        {

            // 방 접속, 방 탈출, 방 갱신, 방 채팅, 플레이어 공격, 플레이어 기권
            if (message.RequestType.Equals("ENTERANCE_GAME_ROOM"))
            {
                GameRoom gameRoomToRenew = _gameRoomRepository.InitializeGameRoom(message.Text, serverSocketHandler.Name);
                ProcessExitAndEnterance(serverSocketHandler, gameRoomToRenew, "ENTERANCE_RESPONSE", "GAMEROOM_RENEW_RESPONSE");
                serverSocketHandler.SetCurrentRoom(gameRoomToRenew.Name);
                return;
            }

            if (message.RequestType.Equals("EXIT_GAME_ROOM"))
            {
                GameRoom gameRoomToRenew = _gameRoomRepository.UpdateDisconnect(serverSocketHandler.Name);
                ProcessExitAndEnterance(serverSocketHandler, gameRoomToRenew, "EXIT_RESPONSE", "GAMEROOM_RENEW_RESPONSE");
                serverSocketHandler.SetCurrentRoom("MAIN_ROBBY");
                return;
            }

            if (message.RequestType.Equals("CHAT"))
            {
                ProcessChatResponse(message, serverSocketHandler, "CHAT_RESPONSE");
                return;
            }

            if (message.RequestType.Equals("READY_TO_START"))
            {
                ProcessReadyToStartResponse(message, serverSocketHandler);
                return;
            }

            if (message.RequestType.Equals("SURRENDER"))
            {
                ProcessSurrenderResponse(message, serverSocketHandler, "SURRENDER_RESPONSE");
                return;
            }

            if (message.RequestType.Equals("MOVE_STONE"))
            {
                ProcessMoveStoneResponse(message, serverSocketHandler, "MOVE_STONE_RESPONSE");
                Logger.Instance.Log(Logger.LogLevel.Info, $"[유저 메시지]: {message.Text}");
                return;
            }
        }
        // 방 접속, 탈출, 갱신, 채팅, 공격, 기권 로직

        private void ProcessExitAndEnterance(ServerSocketHandler serverSocketHandler, GameRoom gameRoom, string personalResponseType, string broadcastResponseType)
        {
            string gameRoomJson = _gameRoomRepository.ConvertGameRoomToJson(gameRoom);

            if (serverSocketHandler != null)
            {
                // 개별 유저에게 정보 전송
                serverSocketHandler.Send(serverSocketHandler.CreateTheData(new Message
                {
                    Name = serverSocketHandler.Name,
                    Destination = "MAIN_ROBBY",
                    RequestType = personalResponseType,
                    Text = gameRoomJson
                }));

                // 모든 유저에게 게임 룸 저장소 동기화 메시지 전송
                _repositoryManager.BroadCastToServerSocketHandlers(new Message
                {
                    Destination = "MAIN_ROBBY",
                    RequestType = broadcastResponseType,
                    Text = gameRoomJson
                });
            }
        }

        private void ProcessChatResponse(Message message, ServerSocketHandler serverSocketHandler, string personalResponseType)
        {
            string textToSend = $"[{serverSocketHandler.Name}]: {message.Text}";

            foreach (ServerSocketHandler handlerToChat in _repositoryManager.GetServerSocketHanlders())
            {
                if (serverSocketHandler.Name.Equals(handlerToChat.Name)) continue;
                if (!handlerToChat.GetCurrentRoom().Equals(serverSocketHandler.GetCurrentRoom())) continue;

                handlerToChat.Send(handlerToChat.CreateTheData(new Message
                {
                    Name = handlerToChat.Name,
                    Destination = "GAME_ROOM",
                    RequestType = personalResponseType,
                    Text = textToSend
                }));

                break;
            }
        }

        private void ProcessReadyToStartResponse(Message message, ServerSocketHandler serverSocketHandler)
        {
            GameRoom gameRoomToRenew = _gameRoomRepository.GetRoom(message.Text);
            if (serverSocketHandler.Name.Equals(gameRoomToRenew.MainUser))
            {
                gameRoomToRenew.MainUserReady = true;
            }

            if (serverSocketHandler.Name.Equals(gameRoomToRenew.SubUser))
            {
                gameRoomToRenew.SubUserReady = true;
            }

            string textToSend = _gameRoomRepository.ConvertGameRoomToJson(gameRoomToRenew);

            if (gameRoomToRenew.CheckReadyToStart())
            {
                _gameRoomRepository.GetGameHandlerByRoomName(gameRoomToRenew.Name).Initialize();
                BroadcastToGameRoom(serverSocketHandler, "START_TO_GAME_RESPONSE", textToSend);
            } else
            {
                BroadcastToGameRoom(serverSocketHandler, "READY_TO_GAME_RESPONSE", textToSend);
            }
        }

        private void ProcessSurrenderResponse(Message message, ServerSocketHandler serverSocketHandler, string personalRequestType)
        {
            GameRoom gameRoomToRenew = _gameRoomRepository.GetRoom(message.Text);
            gameRoomToRenew.SubUserReady = false;
            gameRoomToRenew.MainUserReady = false;
            string textToSend = _gameRoomRepository.ConvertGameRoomToJson(gameRoomToRenew);
            this.BroadcastToGameRoom(serverSocketHandler, personalRequestType, textToSend);

            if (serverSocketHandler.Name.Equals(gameRoomToRenew.MainUser))
            {
                BroadcastToGameRoom(serverSocketHandler, "GMAE_ROOM_WINNER_RESPONSE", gameRoomToRenew.SubUser);
            }
            else
            {
                BroadcastToGameRoom(serverSocketHandler, "GMAE_ROOM_WINNER_RESPONSE", gameRoomToRenew.MainUser);
            }
        }

        private void ProcessMoveStoneResponse(Message message, ServerSocketHandler serverSocketHandler, string personalRequestType)
        {
            GameMove gameMove = _gameRoomRepository.ConvertJsonToGameMove(message.Text);
            OmokServerGameHandler omokHandler = _gameRoomRepository.GetGameHandlerByRoomName(gameMove.Text);
            omokHandler.PlaceStone(gameMove.X, gameMove.Y);
            string textToSend = _gameRoomRepository.ConvertGameMoveToJson(new GameMove {
                Text = omokHandler.CurrentTurn == 1 ? "MAIN_USER" : "SUB_USER",
                X = gameMove.X,
                Y = gameMove.Y
            });

            BroadcastToGameRoom(serverSocketHandler, personalRequestType, textToSend);
        }

        private void BroadcastToGameRoom(ServerSocketHandler serverSocketHandler, string requestType, string textToSend)
        {
            foreach (ServerSocketHandler handler in _repositoryManager.GetServerSocketHanlders())
            {
                if (!handler.GetCurrentRoom().Equals(serverSocketHandler.GetCurrentRoom())) continue;
                handler.Send(handler.CreateTheData(new Message
                {
                    Name = handler.Name,
                    Destination = "GAME_ROOM",
                    RequestType = requestType,
                    Text = textToSend
                }));
            }
        }
    }

    public class MainRobbyHandler : Handler
    {
        private RepositoryManager _repositoryManager;
        private GameRoomRepository _gameRoomRepository;
        private UserRepository _userRepository;

        public MainRobbyHandler(RepositoryManager repositoryManager)
        {
            this._repositoryManager = repositoryManager;
            _gameRoomRepository = _repositoryManager.GetGameRoomRepository();
            _userRepository = _repositoryManager.GetUserRepository();
        }

        public void HandleMessage(Message message, ServerSocketHandler serverSocketHandler)
        {
            // 전체 채팅, 메인 로비 퇴장, 특정 회원 정보 조회
            if (message.RequestType.Equals("CHAT"))
            {
                ProcessChatResponse(message, serverSocketHandler, "CHAT_RESPONSE");
                return;
            }
        }
        private void ProcessChatResponse(Message message, ServerSocketHandler serverSocketHandler, string personalResponseType)
        {
            string textToSend = $"[{serverSocketHandler.Name}]: {message.Text}";

            foreach (ServerSocketHandler handlerToChat in _repositoryManager.GetServerSocketHanlders())
            {
                if (serverSocketHandler.Name.Equals(handlerToChat.Name)) continue;
                if (handlerToChat.GetCurrentRoom().Equals("MAIN_ROBBY"))
                {
                    handlerToChat.Send(handlerToChat.CreateTheData(new Message
                    {
                        Name = handlerToChat.Name,
                        Destination = "MAIN_ROBBY",
                        RequestType = personalResponseType,
                        Text = textToSend
                    }));
                }
            }
        }
    }

    public class LoginHandler : Handler
    {
        private RepositoryManager _repositoryManager;
        private DBHandler _dbHandler;
        private GameRoomRepository _gameRoomRepository;
        private UserRepository _userRepository;

        public LoginHandler(RepositoryManager repositoryManager)
        {
            this._repositoryManager = repositoryManager;
            _gameRoomRepository = _repositoryManager.GetGameRoomRepository();
            _userRepository = _repositoryManager.GetUserRepository();
            _dbHandler = DBHandler.Instance;
            _dbHandler.ConnectToDB();
        }

        public void HandleMessage(Message message, ServerSocketHandler serverSocketHandler)
        {
            if (message.RequestType.Equals("LOGIN"))
            {
                ProcessLogin(message, serverSocketHandler, "LOGIN_RESPONSE");
                return;
            }
            
            if (message.RequestType.Equals("SEARCH_USER_ID"))
            {
                ProcessSearchUserId(message, serverSocketHandler, "SEARCH_RESPONSE");
                return;
            }

            if (message.RequestType.Equals("SEARCH_USER_PW"))
            {
                ProcessSearchUserPassword(message, serverSocketHandler, "SEARCH_RESPONSE");
                return;
            }

            if (message.RequestType.Equals("REGIST_USER_ID/PW"))
            {
                ProcessRegist(message, serverSocketHandler, "REGIST_RESPONSE");
                return;
            }

            if (message.RequestType.Equals("UNREGIST_USER_ID/PW"))
            {
                ProcessUnRegist(message, serverSocketHandler, "UNREGIST_RESPONSE");
                return;
            }

            if (message.RequestType.Equals("RENEW_USER_PASSWORD"))
            {
                ProcessRenewUserPassword(message, serverSocketHandler, "RENEW_RESPONSE");
            }

        }

        private void ProcessLogin(Message message, ServerSocketHandler serverSocketHandler, string personalRequestType)
        {
            User user = _userRepository.ConvertJsonToUser(message.Text);
            bool resultQuery = Login(user.ID, user.Password);
            bool resultSearch = _userRepository.GetUser(user.ID) == null;

            if (resultQuery && resultSearch)
            {
                serverSocketHandler.Send(serverSocketHandler.CreateTheData(new Message
                {
                    Name = user.ID,
                    RequestType = personalRequestType,
                    Text = "ACCEPT_NORMAL_USER"
                }));

                serverSocketHandler.Send(serverSocketHandler.CreateTheData(new Message
                {
                    Name = user.ID,
                    Destination = "MAIN_ROBBY",
                    RequestType = personalRequestType,
                    Text = _gameRoomRepository.ConvertGameRoomListToJsonString()
                }));

                // 현재 유저 저장소에 유저 정보 등록
                serverSocketHandler.Name = user.ID;
                serverSocketHandler.SetCurrentRoom("MAIN_ROBBY");
                _userRepository.AddUser(new User { ID = user.ID });
            } 
            else
            {
                if (!resultSearch)
                {
                    ProcessDuplicateAccess(user.ID, serverSocketHandler, personalRequestType);
                }

                serverSocketHandler.Send(serverSocketHandler.CreateTheData(new Message
                {
                    RequestType = personalRequestType,
                    Text = "NOT_FOUND_USER"
                }));
            }
        }

        private void ProcessDuplicateAccess(string userID, ServerSocketHandler serverSocketHandler, string personalRequestType)
        {
            // 메시지 수정 필요
            serverSocketHandler.Send(serverSocketHandler.CreateTheData(new Message
            {
                RequestType = personalRequestType,
                Text = "NOT_FOUND_USER"
            }));
        }
       

        private void ProcessSearchUserId(Message message, ServerSocketHandler serverSocketHandler, string personalRequestType)
        {
            string phoneNumber = message.Text;
            User userToFind = FindUserByPhoneNumber(phoneNumber);
            bool resultQuery = userToFind != null;

            if (resultQuery)
            {
                serverSocketHandler.Send(serverSocketHandler.CreateTheData(new Message
                {
                    Name = serverSocketHandler.Name,
                    Destination = "ID_FORM",
                    RequestType = personalRequestType,
                    Text = userToFind.ID
                }));
            }
            else
            {
                serverSocketHandler.Send(serverSocketHandler.CreateTheData(new Message
                {
                    Name = serverSocketHandler.Name,
                    RequestType = personalRequestType,
                    Text = "REFUSED"
                }));
            }
        }


        private void ProcessSearchUserPassword(Message message, ServerSocketHandler serverSocketHandler, string personalRequestType)
        {
            string userId = message.Text;
            User userToSearch = _dbHandler.SearchByIdForPassword(userId) ;
            bool resultQuery = userToSearch != null;

            if (resultQuery)
            {
                serverSocketHandler.Send(serverSocketHandler.CreateTheData(new Message
                {
                    Name = serverSocketHandler.Name,
                    Destination = "PASSWORD_FORM",
                    RequestType = personalRequestType,
                    Text = "CMPLETED"
                }));

                _userRepository.AddUserBuffer(serverSocketHandler.Name, userToSearch);
            }
            else
            {
                serverSocketHandler.Send(serverSocketHandler.CreateTheData(new Message
                {
                    Name = serverSocketHandler.Name,
                    RequestType = personalRequestType,
                    Text = "REFUSED"
                }));
            }
        }

        private void ProcessRegist(Message message, ServerSocketHandler serverSocketHandler, string personalRequestType)
        {
            User userToRegist = _userRepository.ConvertJsonToUser(message.Text);
            bool resultQuery = Register(userToRegist);
            SendResponse(serverSocketHandler, personalRequestType, resultQuery, serverSocketHandler.Name);
            if (resultQuery) { Logger.Instance.Log(Logger.LogLevel.Info, $"[{userToRegist.ID}] 회원 정보가 DB에 등록 되었습니다."); }
        }

        private void ProcessUnRegist(Message message, ServerSocketHandler serverSocketHandler, string personalRequestType)
        {
            User userToUnRegist = _userRepository.ConvertJsonToUser(message.Text);
            bool resultQuery = Unregister(userToUnRegist.ID, userToUnRegist.Password);
            SendResponse(serverSocketHandler, personalRequestType, resultQuery, serverSocketHandler.Name);
            if (resultQuery) { Logger.Instance.Log(Logger.LogLevel.Info, $"[{userToUnRegist.ID}] 회원 정보가 DB에서 제거 되었습니다."); }
        }

        private void ProcessRenewUserPassword(Message message, ServerSocketHandler serverSocketHandler, string personalRequestType)
        {
            User userToReceived = _userRepository.ConvertJsonToUser(message.Text);
            User userToRenewPassword = _userRepository.GetUserBuffer(userToReceived.ID);
            bool resultQuery = ResetPassword(userToRenewPassword.ID, userToReceived.Password);
            SendResponse(serverSocketHandler, personalRequestType, resultQuery, serverSocketHandler.Name);
            if (resultQuery) { _userRepository.RemoveUserBuffer(userToRenewPassword.ID); }
        }

        private void SendResponse(ServerSocketHandler serverSocketHandler, string personalRequestType, bool queryResult, string userName)
        {
            string responseText = queryResult ? "COMPLETED" : "REFUSED";
            serverSocketHandler.Send(serverSocketHandler.CreateTheData(new Message
            {
                Name = userName,
                RequestType = personalRequestType,
                Text = responseText
            }));

            if (queryResult)
            {
                Logger.Instance.Log(Logger.LogLevel.Info, $"[{userName}] [{(responseText == "COMPLETED" ? " 작업이 완료 되었습니다." : " 작업이 거부되었습니다.")}]");
            }
        }

        private bool Login(string userID, string password)
        {
            User user = _dbHandler.SearchByIdForPassword(userID);
            if (user == null)
            {
                return false; 
            }
            return user.Password == password; 
        }

        private bool Register(User newUser)
        {
            User existingUser = _dbHandler.SearchByIdForPassword(newUser.ID);
            if (existingUser != null)
            {
                return false; 
            }

            return _dbHandler.Insert(newUser); 
        }

        private bool Unregister(string userID, string password)
        {
            User user = _dbHandler.SearchByIdForPassword(userID);
            if (user == null || user.Password != password)
            {
                return false; 
            }

            return _dbHandler.Delete(user); 
        }

        private User FindUserByPhoneNumber(string phoneNumber)
        {
            return _dbHandler.SearchByPhoneNumber(phoneNumber); 
        }

        public bool ResetPassword(string userID, string newPassword)
        {
            User user = _dbHandler.SearchByIdForPassword(userID);
            if (user == null)
            {
                return false;
            }

            user.Password = newPassword;
            return _dbHandler.Update(user);
        }
    }


    public class MainServer
    {
        // 모든 핸들러에 RepositoryManager 의존성 주입, string으로 매핑 
        private RepositoryManager _repositoryManager;
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
            _repositoryManager = new RepositoryManager(_messages, _messagesLock);
            _processhandlers = initializeProcessHandlers();
        }

        public Dictionary<string, Handler> initializeProcessHandlers()
        {
            Dictionary<string, Handler>  processhandlers = new Dictionary<string, Handler>
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
                _logger.Log(Logger.LogLevel.Info, $"[메시지 타입] : {message.RequestType}"); 
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
