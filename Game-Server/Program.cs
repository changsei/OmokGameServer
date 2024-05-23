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
using Model;
using Repository;
using Message = Model.Message;
using System.Runtime.Remoting.Messaging;
using System.Diagnostics.Eventing.Reader;
using Server_Omok_Game;

namespace Game_Server
{
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
