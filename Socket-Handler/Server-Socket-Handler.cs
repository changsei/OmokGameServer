using Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace Socket_Handler
{
    public class ServerSocketHandler : SocketHandler
    {
        private System.Timers.Timer _timer;
        private string _currentRoom;
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
                this.Name = message.Name + UserCount;
                // 로그인 중복 처리를 위함
                message.Name = this.Name;
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
                    RequestType = "UNREGIST_RESPONSE",
                    Text = "CONNECTED"
                }));
                return;
            }

            if (message.RequestType.Equals("SEARCH"))
            {
                this.Name = message.Name + UserCount;
                this.Send(CreateTheData(new Message
                {
                    Name = this.Name,
                    RequestType = "SEARCH_RESPONSE",
                    Text = "CONNECTED"
                }));
                EnqueueMessage(message);
                return;
            }

            if (message.RequestType.Equals("DELETE"))
            {
                this.Name = message.Name + UserCount;
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
                _logger.Log(Logger.LogLevel.Info, $"[연결 타이머 종료]: {this.Name}");
            }
        }

        public void StartToConnectionTimer()
        {
            _timer = new System.Timers.Timer(_time);
            _timer.Elapsed += OnConnectiontimerElapsed;
            _timer.Start();
            _logger.Log(Logger.LogLevel.Info, $"[연결 타이머 시작]: {this.Name}");
        }

        public string GetCurrentRoom()
        {
            return _currentRoom;
        }

        public void SetCurrentRoom(string room)
        {
            _currentRoom = room;
        }
    }
}
