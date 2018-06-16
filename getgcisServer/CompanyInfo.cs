using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace getGcisServer
{
    /*
     * 用來序列和反序列化公司資訊的類
     * 用於接收到商工公示資料時的解碼
     */
    public class CompanyInfo
    {
        [JsonProperty("Business_Accounting_NO")]
        public string Business_Accounting_NO { get; set; }

        [JsonProperty("Company_Status_Desc")]
        public string Company_Status_Desc { get; set; }

        [JsonProperty("Company_Name")]
        public string Company_Name { get; set; }

        [JsonProperty("Capital_Stock_Amount")]
        public long? Capital_Stock_Amount { get; set; }

        [JsonProperty("Paid_In_Capital_Amount")]
        public long? Paid_In_Capital_Amount { get; set; }

        [JsonProperty("Responsible_Name")]
        public string Responsible_Name { get; set; }

        [JsonProperty("Company_Location")]
        public string Company_Location { get; set; }

        [JsonProperty("Register_Organization_Desc")]
        public string Register_Organization_Desc { get; set; }

        [JsonProperty("Company_Setup_Date")]
        public string Company_Setup_Date { get; set; }

        [JsonProperty("Change_Of_Approval_Data")]
        public string Change_Of_Approval_Data { get; set; }

    }

    /*
     * 這個類除了公司基本資料外，另外加了3個布林值的屬性，分別代表：
     *      Duplicate ： 此公司名稱有不只一個查詢結果
     *      ErrNotice ： 在查詢此公司時發生連線異常，結果可能不正確
     *      NoData    ： 查無資料
     */
    public class CompanyInfoResult : CompanyInfo
    {
        public bool Duplicate { get; set; }
        public bool ErrNotice { get; set; }
        public bool NoData { get; set; }
    }
}
