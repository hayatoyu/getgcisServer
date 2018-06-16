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
            Console.WriteLine(" ========== 歡迎使用商工行政資料批次抓取系統 ==========");
            Server server = Server.getInstance();
            server.Start();
            while (true)
            {
                string command = Console.ReadLine();
                switch (command)
                {
                    case "exit":
                        Console.WriteLine(" 即將離開系統，歡迎再次使用 ");
                        System.Environment.Exit(0);
                        break;
                }
            }
        }
    }
}
