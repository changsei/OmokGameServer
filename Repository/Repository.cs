using Model;
using Newtonsoft.Json;
using Server_Omok_Game;
using Socket_Handler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repository
{
    public class ServerSokcetReopsitory
    {
        private List<ServerSocketHandler> _serverSocketHandlers;
        private readonly object _lock;
        private int _userCount;

        public ServerSokcetReopsitory()
        {
            _serverSocketHandlers = new List<ServerSocketHandler>();
            _lock = new object();
            _userCount = 0;
        }

        public void AddServerSocketHandler(ServerSocketHandler serverSocketHandler)
        {
            lock (_lock)
            {
                _serverSocketHandlers.Add(serverSocketHandler);
                _userCount++;
            }
        }

        public ServerSocketHandler GetServerSocketHandler(string handlerNameToSearch)
        {
            foreach (ServerSocketHandler handlerToSearch in _serverSocketHandlers)
            {
                if (handlerToSearch.Name.Equals(handlerNameToSearch))
                {
                    return handlerToSearch;
                }
            }

            return null;
        }

        public List<ServerSocketHandler> GetServerSocketHandlers()
        {
            return this._serverSocketHandlers;
        }

        public ServerSocketHandler RemoveServerSocketHandler(string handlerNameToRemove)
        {
            lock (_lock)
            {
                foreach (ServerSocketHandler handlerToRemove in _serverSocketHandlers)
                {
                    if (handlerToRemove.Name.Equals(handlerNameToRemove))
                    {
                        _serverSocketHandlers.Remove(handlerToRemove);
                        _userCount--;
                        return handlerToRemove;
                    }
                }

                return null;
            }
        }

        public object GetLock()
        {
            return this._lock;
        }

        public int GetUserCount()
        {
            return this._userCount;
        }
    }

    public class UserRepository
    {
        private List<User> _users;
        private Dictionary<string, User> _userBuffers;
        private readonly object _userBufferLock;
        private readonly object _lock;

        public UserRepository()
        {
            try
            {
                _userBuffers = new Dictionary<string, User>();
                _users = new List<User>();
                _lock = new object();
                _userBufferLock = new object();
            }
            catch (Exception e) { }
        }

        public void RemoveUser(string userID)
        {
            lock (_lock)
            {
                User userToRemove = GetUser(userID);

                if (userToRemove != null)
                {
                    _users.Remove(userToRemove);
                    Logger.Instance.Log(Logger.LogLevel.Error, $"[유저 목록 제거]: {userID}");
                }
                else
                {
                    Logger.Instance.Log(Logger.LogLevel.Error, "[유저 목록 제거]: 게스트 유저");
                }
            }
        }

        public void AddUserBuffer(string userID, User user)
        {
            lock (_userBufferLock)
            {
                _userBuffers.Add(userID, user);
            }
        }

        public void RemoveUserBuffer(string userID)
        {
            lock (_userBufferLock)
            {
                _userBuffers.Remove(userID);
            }
        }

        public User GetUserBuffer(string userID)
        {
            lock (_userBufferLock)
            {
                return _userBuffers[userID];
            }
        }

        public void AddUser(User user)
        {
            lock (_lock)
            {
                _users.Add(user);
            }
        }

        public User GetUser(string userID)
        {
            foreach (User user in _users)
            {
                if (!user.ID.Equals(userID)) continue;
                return user;
            }

            return null;
        }

        public string ConvertUserListToJsonString()
        {
            return JsonConvert.SerializeObject(_users);
        }

        public List<User> ConvertToUserList(string jsonString)
        {
            return JsonConvert.DeserializeObject<List<User>>(jsonString);
        }
        public string ConvertUserToJson(User user)
        {
            return JsonConvert.SerializeObject(user);
        }

        public User ConvertJsonToUser(string jsonString)
        {
            return JsonConvert.DeserializeObject<User>(jsonString);
        }
    }

    public class GameRoomRepository
    {
        private List<GameRoom> _rooms;
        private Dictionary<string, OmokServerGameHandler> _omokGameHandlers = new Dictionary<string, OmokServerGameHandler>();
        private object _lock = new object();
        private readonly int _defaultRoomSize = 3;
        private int _roomCount = 0;

        public GameRoomRepository()
        {
            _rooms = new List<GameRoom>();
            _omokGameHandlers = new Dictionary<string, OmokServerGameHandler>();

            for (int i = 0; i < _defaultRoomSize; i++)
            {
                string roomName = $"ROOM{++_roomCount}";
                this.AddRoom(new GameRoom
                {
                    Name = roomName,
                    MainUser = "NULL",
                    SubUser = "NULL",
                    State = true
                });

                _omokGameHandlers.Add(roomName, new OmokServerGameHandler());
            }
        }

        public OmokServerGameHandler GetGameHandlerByRoomName(string roomName)
        {
            if (_omokGameHandlers.ContainsKey(roomName))
            {
                return _omokGameHandlers[roomName];
            }
            return null;
        }

        public void AddRoom(GameRoom room)
        {
            lock (_lock)
            {
                _rooms.Add(room);
            }
        }

        public List<GameRoom> GetRooms()
        {
            return _rooms;
        }

        public GameRoom GetRoom(string roomName)
        {
            foreach (GameRoom room in _rooms)
            {
                if (room.Name == roomName) return room;
            }

            return null;
        }

        public GameRoom InitializeGameRoom(string roomName, string userName)
        {
            lock (_lock)
            {
                GameRoom room = GetRoom(roomName);

                if (room.MainUser == "NULL")
                {
                    room.MainUser = userName;
                    room.SubUser = "NULL";
                }
                else if (room.SubUser == "NULL")
                {
                    room.SubUser = userName;
                }
                room.State = (room.SubUser == "NULL");
                return room;
            }
        }

        public GameRoom RenewGameRoom(GameRoom gameRoom)
        {
            lock (_lock)
            {
                GameRoom roomToRenew = GetRoom(gameRoom.Name);
                roomToRenew.MainUser = gameRoom.MainUser;
                roomToRenew.SubUser = gameRoom.SubUser;

                // 레디 상태 갱신 
                roomToRenew.MainUserReady = gameRoom.MainUserReady && roomToRenew.MainUser != "NULL";
                roomToRenew.SubUserReady = gameRoom.SubUserReady && roomToRenew.SubUser != "NULL";

                // 방 입장 상태 갱신
                roomToRenew.State = roomToRenew.SubUser == "NULL";

                // 유저가 떠날 경우 레디 상태 초기화 
                if (gameRoom.MainUser.Equals("NULL") || gameRoom.SubUser.Equals("NULL"))
                {
                    roomToRenew.MainUserReady = false;
                    roomToRenew.SubUserReady = false;
                }

                return roomToRenew;
            }
        }

        public GameRoom UpdateDisconnect(string userName)
        {
            lock (_lock)
            {
                foreach (GameRoom room in _rooms)
                {
                    bool updated = false;
                    if (room.MainUser == userName)
                    {
                        room.MainUser = "NULL";
                        updated = true;
                    }
                    if (room.SubUser == userName)
                    {
                        room.SubUser = "NULL";
                        updated = true;
                    }

                    if (updated)
                    {
                        if (room.MainUser == "NULL" && room.SubUser != "NULL")
                        {
                            room.MainUser = room.SubUser;
                            room.SubUser = "NULL";
                        }

                        room.State = (room.SubUser == "NULL");
                        room.MainUserReady = false;
                        room.SubUserReady = false;
                    }
                    return room;
                }

                return null;
            }
        }

        public string ConvertGameRoomListToJsonString()
        {
            return JsonConvert.SerializeObject(_rooms);
        }

        public List<GameRoom> ConvertToRoomList(string jsonString)
        {
            return JsonConvert.DeserializeObject<List<GameRoom>>(jsonString);
        }
        public string ConvertGameRoomToJson(GameRoom gameRoom)
        {
            return JsonConvert.SerializeObject(gameRoom);
        }

        public GameRoom ConvertJsonToGameRoom(string jsonString)
        {
            return JsonConvert.DeserializeObject<GameRoom>(jsonString);
        }

        public string ConvertGameMoveToJson(GameMove gameMove)
        {
            return JsonConvert.SerializeObject(gameMove);
        }

        public GameMove ConvertJsonToGameMove(string jsonString)
        {
            return JsonConvert.DeserializeObject<GameMove>(jsonString);
        }
    }
}
