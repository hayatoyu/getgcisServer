using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;

namespace getGcisServer
{
    /*
     * 這是一個 TcpClient 的包裝類，用來將 TcpClient 加入可以比較的屬性，就是接收到的時間
     * 方便將 TcpClient 加入到 Priority Queue 中。
     */
    class TcpCientComarable : IComparer<TcpCientComarable>
    {
        public TcpClient tcpClient { get; set; }
        public DateTime recTime { get; set; }

        public TcpCientComarable(TcpClient client, DateTime recTime)
        {
            this.tcpClient = client;
            this.recTime = recTime;
        }

        public int Compare(TcpCientComarable a, TcpCientComarable b)
        {
            if (a == null & b == null)
                return 0;
            else if (a == null)
                return -1;
            else if (b == null)
                return 1;
            else if (a.recTime < b.recTime)
                return 1;
            else
                return -1;
        }
    }
}
