using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace getGcisServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Server server;
            while(true)
            {
                string command = Console.ReadLine();
                switch(command)
                {
                    case "exit":
                        break;
                    case "start":
                        server = Server.getInstance();
                        server.Start();
                        break;
                }
            }
        }
    }
}
