using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace getGcisServer
{
    /*
     * 用來接收客戶端傳來的查詢列表
     */
    class ComRequest
    {
        public CompanyInfo[] comList { get; set; }
    }
}
