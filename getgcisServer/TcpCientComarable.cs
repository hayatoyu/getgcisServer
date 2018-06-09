using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;

namespace getGcisServer
{
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
