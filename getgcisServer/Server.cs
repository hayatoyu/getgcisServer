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
        private PriorityQueue<TcpCientComarable> WaitLine;
        private TcpListener listener;
        private BackgroundWorker bgwServer;
        private TcpClient socketClient;
        private static object o = new object();

        public static Server server = new Server();


        private Server()
        {
            try
            {
                portNum = int.Parse(ConfigurationManager.AppSettings["port"]);
            }
            catch (Exception e)
            {
                portNum = 1357;
            }
            clientNumInService = 0;
            WaitLine = new PriorityQueue<TcpCientComarable>();
            AutoListen = true;
            bgwServer = new BackgroundWorker();
            bgwServer.DoWork += new DoWorkEventHandler(addToWaitLine);
        }

        public static Server getInstance()
        {
            lock (o)
            {
                if (server == null)
                    server = new Server();
            }
            return server;
        }

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

            while (WaitLine.Peep())
            {
                var c = WaitLine.Pop();
                using (NetworkStream ns = c.tcpClient.GetStream())
                {
                    SendToClient(ns, CloseMsg);
                }
                c.tcpClient.Close();
            }
        }

        private void addToWaitLine(object sender, DoWorkEventArgs e)
        {
            try
            {
                if (listener != null)
                    listener.Stop();
                listener = new TcpListener(IPAddress.Any, portNum);
                listener.Start();
                Console.WriteLine("Server start listening...");
                while (AutoListen)
                {
                    socketClient = listener.AcceptTcpClient();
                    string serverMsg = string.Format("Connected to {0},add to WaitLine...", ((IPEndPoint)socketClient.Client.RemoteEndPoint).Address.ToString());
                    Console.WriteLine(serverMsg);
                    lock (WaitLine)
                    {
                        WaitLine.push(new TcpCientComarable(socketClient, DateTime.Now));
                        Thread.Sleep(2000);
                    }

                    string serverResponse = "added";
                    NetworkStream netStream = socketClient.GetStream();
                    SendToClient(netStream, serverResponse);

                    Thread th = new Thread(() => addToService());
                    th.Start();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void addToService()
        {
            TcpCientComarable customer = null;
            Thread th = null;
            while (true)
            {
                lock (WaitLine)
                {
                    if (WaitLine.Peep() && clientNumInService < 3)
                    {
                        customer = WaitLine.Pop();
                        th = new Thread(() => Query(customer.tcpClient));
                        clientNumInService++;
                        th.Start();
                    }
                }

            }
        }

        private void Query(TcpClient client)
        {
            NetworkStream netStream = client.GetStream();
            String serverResponse;
            byte[] buffer = new byte[4096];
            StringBuilder stbr = new StringBuilder();
            int reqlength = 0;
            int requestErr = 0;
            string IPAddr = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();

            // 告訴 client 可以開始送資料了
            serverResponse = "ready";
            SendToClient(netStream, serverResponse);

            while (client.Connected)
            {
                try
                {
                    do
                    {
                        reqlength = netStream.Read(buffer, 0, buffer.Length);
                        stbr.Append(Encoding.UTF8.GetString(buffer, 0, reqlength));
                        Thread.Sleep(1500);
                    }
                    while (netStream.DataAvailable);
                }
                catch(IOException e)
                {
                    // 連線中斷
                    Console.WriteLine(e.Message);
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
                        serverResponse = string.Format("收到 {0} 欲查詢之資料...",IPAddr);
                        SendToClient(netStream, serverResponse);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        serverResponse = string.Format("無法解析來自 {0} 之 Json 字串 : {1}",IPAddr, clientRequest);
                        Console.WriteLine(serverResponse);
                        SendToClient(netStream, serverResponse);
                        requestErr++;
                        if (requestErr > 2)
                        {
                            serverResponse = string.Format("無法解析來自 {0} 之 Json 字串已達 {1} 次，請檢查是否有誤，即將中斷連線",IPAddr, requestErr);
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

                            comName = comRequest.comList[index];
                            stbr.Clear();
                            stbr.Append("http://").Append("data.gcis.nat.gov.tw")
                                .Append("/od/data/api/6BBA2268-1367-4B42-9CCA-BC17499EBE8C")
                                .Append("?$format=json&$filter=")
                                .Append(param.Replace("comName", comName));
                            serverResponse = string.Format("{0} 開始查詢第 {1} / {2} 條資料 : {3}", IPAddr,index + 1,comRequest.comList.Length,comName);
                            SendToClient(netStream, serverResponse);
                            Console.WriteLine(serverResponse);

                            WebRequest request = WebRequest.Create(stbr.ToString());
                            HttpWebResponse response = request.GetResponse() as HttpWebResponse;

                            if (response.StatusCode == HttpStatusCode.OK)
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

                                                result = new CompanyInfoResult
                                                {
                                                    Business_Accounting_NO = comInfos[0].Business_Accounting_NO,
                                                    Company_Status_Desc = comInfos[0].Company_Status_Desc,
                                                    Company_Name = comName,
                                                    Capital_Stock_Amount = comInfos[0].Capital_Stock_Amount,
                                                    Paid_In_Capital_Amount = comInfos[0].Paid_In_Capital_Amount,
                                                    Responsible_Name = comInfos[0].Responsible_Name,
                                                    Company_Location = comInfos[0].Company_Location,
                                                    Register_Organization_Desc = comInfos[0].Register_Organization_Desc,
                                                    Company_Setup_Date = comInfos[0].Company_Setup_Date,
                                                    Change_Of_Approval_Data = comInfos[0].Change_Of_Approval_Data,
                                                    Duplicate = comInfos.Length > 1 ? true : false,
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
                                    serverResponse = string.Format("{0} 查詢 {1} 完成!\n",IPAddr, comName);
                                    SendToClient(netStream, serverResponse);
                                    Console.WriteLine(serverResponse);
                                    index++;
                                }
                                catch (IOException e)
                                {
                                    Console.WriteLine(e.Message);                                    
                                    errCount++;
                                    serverResponse = string.Format("{0} 查詢 {1} 時出現連線錯誤，錯誤代碼 {2}，將等候 10 秒重試...",IPAddr, comName, response.StatusCode.ToString());
                                    SendToClient(netStream, serverResponse);
                                    Console.WriteLine(serverResponse);
                                    Thread.Sleep(10000);
                                    continue;
                                }
                                catch(JsonSerializationException e)
                                {
                                    Console.WriteLine(e.Message);
                                    serverResponse = string.Format("{0} 查詢 {1} 時回應資料無法解析，將等候 5 秒重試...",IPAddr, comName);
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
                                        serverResponse = string.Format("{0} 查詢 {1} 時發生錯誤已達3次，錯誤代碼 {2} ，將暫時跳過",IPAddr, comName, response.StatusCode.ToString());
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
                                    serverResponse = string.Format("{0} 查詢 {1} 時發生錯誤已達3次，錯誤代碼 {2} ，將暫時跳過",IPAddr, comName, response.StatusCode.ToString());
                                    SendToClient(netStream, serverResponse);
                                    Console.WriteLine(serverResponse);
                                    continue;
                                }
                                errCount++;
                                serverResponse = string.Format("{0} 查詢 {1} 時出現連線錯誤，錯誤代碼 {2}，將等候 10 秒重試...",IPAddr, comName, response.StatusCode.ToString());
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
                        netStream.Close();
                    }
                }


            }

            clientNumInService--;
        }

        private void SendToClient(NetworkStream ns, string content)
        {
            byte[] sendByte = Encoding.UTF8.GetBytes(content);
            ns.Write(sendByte, 0, sendByte.Length);
            ns.Flush();
        }
    }
}
