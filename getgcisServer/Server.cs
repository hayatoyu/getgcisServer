using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.ComponentModel;
using System.Net;
using System.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Threading;
using System.IO;


namespace getGcisServer
{
    class Server
    {
        public int portNum { private set; get; }
        public int clientNumInService { private set; get; }
        public bool AutoListen { private set; get; }
        const int MaxClientNum = 3;
        //private PriorityQueue<TcpCientComarable> WaitLine;
        private Queue<TcpClient> WaitLine;
        private TcpListener listener;
        private BackgroundWorker bgwServer;
        private TcpClient socketClient;
        private static object o = new object();

        public static Server server = new Server();     // 確保只有一個實例

        /*
         * 這是一個簡單的單體模式
         * 將建構子設為 private，讓外部程式無法呼叫
         * 這樣便無法從外部 new 出新實例(以免無法控管連線數)
         */
        private Server()
        {
            try
            {
                portNum = int.Parse(ConfigurationManager.AppSettings["port"]);
            }
            catch (Exception e)
            {
                portNum = 1357;
                Console.WriteLine("讀取 Port 發生錯誤：{0}\n使用預設 Port ： 1357", e.Message);
            }
            clientNumInService = 0;
            WaitLine = new Queue<TcpClient>();
            AutoListen = true;
            bgwServer = new BackgroundWorker();
            bgwServer.DoWork += new DoWorkEventHandler(addToWaitLine);
        }

        /*
           用來取得實例，在其中呼叫建構子。
           如果建構子是 null ， new 出一個實例並回傳之
           由於為了避免併行狀況，加入了 lock。
           事實上因為 server 是靜態變數，而且在一開始就已經給他一個實例了，應該是不需要再 new 才對。
        */
        public static Server getInstance()
        {
            lock (o)
            {
                if (server == null)
                    server = new Server();
            }
            return server;
        }

        /*
         * 接收 Client 端連線的工作，我讓 BackGroundWorker 來執行。
         * 這比較輕量，但我想也夠了。
         */
        public void Start()
        {
            if (!bgwServer.IsBusy)
                bgwServer.RunWorkerAsync();
        }

        public void Stop()
        {
            string CloseMsg = " Server 端關閉連線...";
            listener.Stop();
            bgwServer.Dispose();

            while (WaitLine.Count > 0)
            {
                var c = WaitLine.Dequeue();
                using (NetworkStream ns = c.GetStream())
                {
                    SendToClient(ns, CloseMsg);
                }
                c.Close();
            }
        }

        /*
         * 將 Client 端來的連線都加入等待佇列之中。
         * BackGroundWorker會在背後執行這個函式
         * 只要在 AutoListen 屬性為開啟的狀態下，就會不斷的等待連線。
         * 所以的客戶都會先進到等待佇列中，再由 addToService() 判斷是否要開始查詢。
         * 當客戶端收到 "added" 訊息時，她會知道他已經被加入佇列，但還需要等 Server 叫他。
         */
        private void addToWaitLine(object sender, DoWorkEventArgs e)
        {

            if (listener != null)
                listener.Stop();
            listener = new TcpListener(IPAddress.Any, portNum);
            listener.Start();
            Console.WriteLine("Server start listening...");
            while (AutoListen)
            {
                try
                {
                    socketClient = listener.AcceptTcpClient();
                    string serverMsg = string.Format("Connected to {0},add to WaitLine...", ((IPEndPoint)socketClient.Client.RemoteEndPoint).Address.ToString());
                    Console.WriteLine(serverMsg);
                    lock (WaitLine)
                    {
                        //WaitLine.push(new TcpCientComarable(socketClient, DateTime.Now));
                        WaitLine.Enqueue(socketClient);
                        Thread.Sleep(2000);
                    }

                    string serverResponse = "added";
                    NetworkStream netStream = socketClient.GetStream();
                    SendToClient(netStream, serverResponse);

                    Thread th = new Thread(() => addToService());
                    th.Start();
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }


        }

        /*
         * 迴圈會一直持續到開始查詢
         * 每次查看佇列，如果佇列中有 Client 在等，且目前連線數小於3
         * 則將排第一的 Client Pop() 出來。Pop()後該客戶會從等待佇列中移除。
         * 更改 StartService 的值，當程式出迴圈後這個 Thread 就會結束。
         * 不過在結束前會另起一個 Query 的 Thread 進行查詢作業。
         */
        private void addToService()
        {
            TcpClient customer = null;
            Thread th = null;
            
            lock(WaitLine)
            {
                if(WaitLine.Count > 0 && clientNumInService < 3)
                {
                    customer = WaitLine.Dequeue();
                    th = new Thread(() => Query(customer));
                    clientNumInService++;
                    th.Start();                    
                }
            }
        }

        /*
         * 這時才開始查詢
         * 先傳送 "ready" 給客戶端，告訴客戶端可以開始傳送查詢列表了
         * 查詢過程中每查一筆就會回傳客戶端一筆資料
         * 最後傳送 "finish" 告訴客戶端已經全部查完
         * 但中斷連線則是由客戶端去中斷
         * 在查詢過程中發生錯誤會嘗試重試3次，都不行則跳過。
         * 錯誤處理的部分應該能重構，但我懶得做。
         */
        private void Query(TcpClient client)
        {
            NetworkStream netStream = client.GetStream();
            String serverResponse;
            byte[] buffer = new byte[4096];
            StringBuilder stbr = new StringBuilder();
            int reqlength = 0;
            int requestErr = 0;
            string IPAddr = string.Empty;
            try
            {
                IPAddr = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
            }
            catch(Exception e)
            {
                PrintErrMsgToConsole(e);
                client.Close();
                return;
            }
            

            // 告訴 client 可以開始送資料了
            serverResponse = "ready";
            SendToClient(netStream, serverResponse);
            try
            {
                netStream.ReadTimeout = int.Parse(ConfigurationManager.AppSettings["TimeOut"].ToString());
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
                netStream.ReadTimeout = 10000;
            }

            while (client.Connected)
            {
                try
                {
                    /*
                     * 由於客戶列表相當長，buffer沒辦法一次讀完
                     * 請用迴圈依次將 netStream 中的資料加入 StringBuilder
                     * 實測過程中如果不加入延遲，有機會資料傳到一半 DataAvailable 變為 false 跳出迴圈而讀取不完全
                     * 猜測應該是迴圈跑太快，下一次讀取時剛好資料還沒進來導致的。
                     */
                    do
                    {
                        reqlength = netStream.Read(buffer, 0, buffer.Length);
                        stbr.Append(Encoding.UTF8.GetString(buffer, 0, reqlength));
                        Thread.Sleep(1500);
                    }
                    while (netStream.DataAvailable);
                }
                catch (IOException e)
                {
                    // 連線中斷
                    Console.WriteLine(e.Message);
                    serverResponse = e.Message;
                    SendToClient(netStream, serverResponse);
                    serverResponse = "finish";
                    SendToClient(netStream, serverResponse);
                    Thread.Sleep(2000);
                    client.Close();
                    break;
                }


                string clientRequest = stbr.ToString();
                stbr.Clear();
                Console.WriteLine("從 {0} 接收到請求列表：\n{1}", IPAddr, clientRequest);

                if (!string.IsNullOrEmpty(clientRequest))
                {
                    ComRequest comRequest = null;
                    try
                    {
                        comRequest = JsonConvert.DeserializeObject<ComRequest>(clientRequest);
                        serverResponse = string.Format("收到 {0} 欲查詢之資料...", IPAddr);
                        SendToClient(netStream, serverResponse);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        serverResponse = string.Format("無法解析來自 {0} 之 Json 字串 : {1}", IPAddr, clientRequest);
                        Console.WriteLine(serverResponse);
                        SendToClient(netStream, serverResponse);
                        requestErr++;
                        if (requestErr > 2)
                        {
                            serverResponse = string.Format("無法解析來自 {0} 之 Json 字串已達 {1} 次，請檢查是否有誤，即將中斷連線", IPAddr, requestErr);
                            Console.WriteLine(serverResponse);
                            SendToClient(netStream, serverResponse);
                            client.Close();
                        }
                        else
                        {
                            serverResponse = "ready";
                            SendToClient(netStream, serverResponse);
                            Console.WriteLine("{0} 準備就緒...", IPAddr);
                            continue;
                        }
                    }
                    if (comRequest != null)
                    {
                        // 要開始查API了
                        stbr = new StringBuilder();
                        string param = "Company_Name like comName and Company_Status eq 01";
                        int errCount = 0, index = 0;
                        string comName;
                        serverResponse = string.Format("{0} 共有 {1} 條資料待查詢...", IPAddr, comRequest.comList.Length);
                        Console.WriteLine(serverResponse);
                        SendToClient(netStream, serverResponse);
                        while (index < comRequest.comList.Length)
                        {
                            if (index % 100 == 0 && index > 0)
                            {
                                serverResponse = string.Format("{0} 已連續查詢 100 條，將等待 10 秒繼續...", IPAddr);
                                SendToClient(netStream, serverResponse);
                                Console.WriteLine(serverResponse);
                                Thread.Sleep(10000);
                            }

                            comName = comRequest.comList[index].Trim();
                            stbr.Clear();
                            stbr.Append("http://").Append("data.gcis.nat.gov.tw")
                                .Append("/od/data/api/6BBA2268-1367-4B42-9CCA-BC17499EBE8C")
                                .Append("?$format=json&$filter=")
                                .Append(param.Replace("comName", comName));
                            serverResponse = string.Format("{0} 開始查詢第 {1} / {2} 條資料 : {3}", IPAddr, index + 1, comRequest.comList.Length, comName);
                            SendToClient(netStream, serverResponse);
                            Console.WriteLine(serverResponse);
                            HttpWebResponse response = null;
                            
                            try
                            {
                                WebRequest request = WebRequest.Create(stbr.ToString());
                                /*
                                 * 如果 IE 中有設定 Proxy，WebRequest預設將採用IE的Proxy設定。
                                 * 可在 App.config 檔裡設定
                                 */
                                setWebProxy(request);
                                response = request.GetResponse() as HttpWebResponse;
                            }
                            catch(WebException e)
                            {
                                Console.WriteLine(e.Message);
                            }
                            

                            if (response != null && response.StatusCode == HttpStatusCode.OK)
                            {
                                try
                                {
                                    // 要解Json了
                                    using (Stream stream = response.GetResponseStream())
                                    {
                                        using (StreamReader reader = new StreamReader(stream))
                                        {
                                            string resFromAPI = reader.ReadToEnd();
                                            CompanyInfoResult result = null;

                                            if (!string.IsNullOrEmpty(resFromAPI))
                                            {
                                                var comInfos = JsonConvert.DeserializeObject<CompanyInfo[]>(resFromAPI);
                                                CompanyInfo cInfo = null;
                                                bool NameMatch = false;
                                                /*
                                                 * 這裡有可能找到多家公司，目前處理方法如下：
                                                 *  1. 如果有多家公司，比對公司名稱完全吻合
                                                 *  2. 如果沒有公司名稱完全吻合，取第一個
                                                 *  3. 如果只有一家公司，取第一個
                                                 * 
                                                 */
                                                if(comInfos.Length > 1)
                                                {
                                                    cInfo = comInfos.Where(c => c.Company_Name.Equals(comName)).FirstOrDefault();
                                                    NameMatch = cInfo != default(CompanyInfo) ? true : false;
                                                }
                                                if (cInfo == null || cInfo == default(CompanyInfo))
                                                    cInfo = comInfos[0];
                                                result = new CompanyInfoResult
                                                {
                                                    Business_Accounting_NO = cInfo.Business_Accounting_NO,
                                                    Company_Status_Desc = cInfo.Company_Status_Desc,
                                                    Company_Name = cInfo.Company_Name,
                                                    Capital_Stock_Amount = cInfo.Capital_Stock_Amount,
                                                    Paid_In_Capital_Amount = cInfo.Paid_In_Capital_Amount,
                                                    Responsible_Name = cInfo.Responsible_Name,
                                                    Company_Location = cInfo.Company_Location,
                                                    Register_Organization_Desc = cInfo.Register_Organization_Desc,
                                                    Company_Setup_Date = cInfo.Company_Setup_Date,
                                                    Change_Of_Approval_Data = cInfo.Change_Of_Approval_Data,
                                                    Duplicate = comInfos.Length > 1 ? true : false,
                                                    NameMatch = NameMatch,
                                                    ErrNotice = false
                                                };
                                            }
                                            else
                                            {
                                                result = new CompanyInfoResult
                                                {
                                                    Company_Name = comName,
                                                    NoData = true
                                                };
                                            }

                                            serverResponse = "result:" + JsonConvert.SerializeObject(result);
                                            SendToClient(netStream, serverResponse);
                                            Thread.Sleep(2000);
                                        }
                                    }
                                    serverResponse = string.Format("{0} 查詢 {1} 完成!\n", IPAddr, comName);
                                    SendToClient(netStream, serverResponse);
                                    Console.WriteLine(serverResponse);
                                    index++;
                                }
                                catch (IOException e)
                                {
                                    Console.WriteLine(e.Message);
                                    errCount++;
                                    serverResponse = string.Format("{0} 查詢 {1} 時出現連線錯誤，將等候 10 秒重試...", IPAddr, comName);
                                    SendToClient(netStream, serverResponse);
                                    Console.WriteLine(serverResponse);
                                    Thread.Sleep(10000);
                                    continue;
                                }
                                catch (JsonSerializationException e)
                                {
                                    Console.WriteLine(e.Message);
                                    serverResponse = string.Format("{0} 查詢 {1} 時回應資料無法解析，將等候 5 秒重試...", IPAddr, comName);
                                    errCount++;
                                    SendToClient(netStream, serverResponse);
                                    Console.WriteLine(serverResponse);
                                    Thread.Sleep(5000);
                                    continue;
                                }
                                finally
                                {
                                    if (errCount >= 3)
                                    {
                                        index++;
                                        errCount = 0;

                                        CompanyInfoResult err = new CompanyInfoResult
                                        {
                                            Company_Name = comName,
                                            ErrNotice = true
                                        };
                                        serverResponse = "result:" + JsonConvert.SerializeObject(err);
                                        SendToClient(netStream, serverResponse);
                                        serverResponse = string.Format("{0} 查詢 {1} 時發生錯誤已達3次，錯誤代碼 {2} ，將暫時跳過", IPAddr, comName, response.StatusCode.ToString());
                                        Console.WriteLine(serverResponse);
                                        SendToClient(netStream, serverResponse);
                                    }
                                }
                            }
                            else
                            {
                                if (errCount >= 3)
                                {
                                    index++;
                                    errCount = 0;

                                    CompanyInfoResult err = new CompanyInfoResult
                                    {
                                        Company_Name = comName,
                                        ErrNotice = true
                                    };
                                    serverResponse = "result:" + JsonConvert.SerializeObject(err);
                                    SendToClient(netStream, serverResponse);
                                    serverResponse = string.Format("{0} 查詢 {1} 時發生錯誤已達3次，將暫時跳過", IPAddr, comName);
                                    SendToClient(netStream, serverResponse);
                                    Console.WriteLine(serverResponse);
                                    continue;
                                }
                                errCount++;
                                serverResponse = string.Format("{0} 查詢 {1} 時出現連線錯誤，將等候 10 秒重試...", IPAddr, comName);
                                SendToClient(netStream, serverResponse);
                                Console.WriteLine(serverResponse);
                                Thread.Sleep(10000);
                                continue;
                            }
                        }


                        Thread.Sleep(3000);
                        serverResponse = "finish";
                        SendToClient(netStream, serverResponse);
                        serverResponse = string.Format("{0} 批次查詢作業完畢", IPAddr);
                        Console.WriteLine(serverResponse);
                        Thread.Sleep(3000);
                        netStream.Close();
                    }
                }


            }

            clientNumInService--;
            Thread th = new Thread(addToService);
            th.Start();
        }

        private void SendToClient(NetworkStream ns, string content)
        {
            byte[] sendByte = Encoding.UTF8.GetBytes(content);
            try
            {
                ns.Write(sendByte, 0, sendByte.Length);
                ns.Flush();
            }
            catch(Exception e)
            {
                PrintErrMsgToConsole(e);
            }
        }

        private void PrintErrMsgToConsole(Exception e)
        {
            Console.WriteLine("錯誤類型：{0}\n錯誤訊息：{1}\n堆疊：{2}：", e.GetType(), e.Message, e.StackTrace);
        }

        private void setWebProxy(WebRequest request)
        {
            if(ConfigurationManager.AppSettings["useProxy"].ToString().Equals(bool.FalseString))
            {
                request.Proxy = null;
            }            
        }
    }
}
