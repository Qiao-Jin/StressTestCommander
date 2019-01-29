using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Runtime.InteropServices;

namespace StressTestCommander
{
    class Commander
    {
        public static readonly string configFileURL = @"config.ini";
        public static readonly string recorderFile = @"result.csv";
        public static readonly string countTransactionsCommand = @"countTransactions";
        public static readonly string getBlockCountCommand = @"getblockcount";
        public static int port = 0;
        public static int port2 = 0;
        public static List<string> testee = new List<string> ();
        public static StreamWriter recorder = null;
        public static string testeeURL = null;
        public static int plannedTestee = 0;
        public static int plannedRounds = 0;
        public static int successfulTestee = 0;
        public static int successfulCount = 0;
        public static int startHeight = 0;
        public static int currentHeight = 0;
        public static int plannedTx = 0;
        public static int successfulTx = 0;
        public static DateTime startTime;
        public static DateTime endTime;
        public static TesteeResult[] testeeResults = null;
        public static bool isOver = false;

        [DllImport("user32.dll")]
        public static extern int MessageBeep(uint uType);
        public static uint beepI = 0x00000030;

        public static bool readConfig()
        {
            if (!File.Exists(configFileURL)) return false;
            StreamReader reader = new StreamReader(configFileURL);
            testeeURL = reader.ReadLine();
            if (testeeURL == null || testeeURL == "" || reader.EndOfStream) return false;
            port = int.Parse(reader.ReadLine());
            if (port <= 0 || reader.EndOfStream) return false;
            port2 = int.Parse(reader.ReadLine());
            if (port2 <= 0 || reader.EndOfStream) return false;
            plannedTestee = int.Parse(reader.ReadLine());
            if (plannedTestee <= 0 || reader.EndOfStream) return false;
            plannedRounds = int.Parse(reader.ReadLine());
            if (plannedRounds <= 0) return false;
            testeeResults = new TesteeResult[plannedTestee];
            return true;
        }

        /// <summary>
        /// 接收消息
        /// </summary>
        /// <param name="o"></param>
        static void Receive(object o)
        {
            var send = o as Socket;
            while (true)
            {
                //获取发送过来的消息
                byte[] buffer = new byte[1024 * 1024 * 2];
                var effective = send.Receive(buffer);
                if (effective == 0)
                {
                    break;
                }
                var str = Encoding.UTF8.GetString(buffer, 0, effective);
                string[] result = str.Split(new char[] { ','});
                Console.WriteLine("Received: " + successfulCount + 1);
                plannedTx += int.Parse(result[0]);
                Console.WriteLine("Current planned Tx: " + plannedTx);
                successfulTx += int.Parse(result[1]);
                Console.WriteLine("Current successful Tx: " + successfulTx);
                DateTime receivedTime = DateTime.FromBinary(long.Parse(result[2]));
                receivedTime = DateTime.FromBinary(long.Parse(result[3]));
                endTime = endTime > receivedTime ? endTime : receivedTime;
                Console.WriteLine("Current end time: " + endTime);
                int receivedHeight = int.Parse(result[4]);
                currentHeight = Math.Max(currentHeight, receivedHeight);
                Console.WriteLine("Current height: " + currentHeight);
                testeeResults[successfulCount] = new TesteeResult();
                testeeResults[successfulCount].successfulTx = int.Parse(result[1]);
                testeeResults[successfulCount].start = DateTime.FromBinary(long.Parse(result[2]));
                testeeResults[successfulCount].end = DateTime.FromBinary(long.Parse(result[3]));
                testeeResults[successfulCount].height = int.Parse(result[4]);
                successfulCount++;

                if (successfulCount == testee.Count())
                {
                    isOver = true;
                    Console.WriteLine("All testees have finished test.");
                    Console.WriteLine("Start waiting for transaction onto block chain...");
                    /*while (GetBlockHeight() <= currentHeight)
                    {
                        Thread.Sleep(5000);
                    }

                    int finalResult = CountTransactions(startHeight);
                    double totalSeconds = (endTime - startTime).TotalSeconds;
                    writeLineCSVFile(new List<string> { plannedTx.ToString(), successfulTx.ToString(), finalResult.ToString(), totalSeconds.ToString(), (successfulTx / totalSeconds).ToString(), (finalResult / totalSeconds).ToString() }, ref recorder);*/

                    double successfulTx2 = 0;
                    int onChainTx2 = 0;
                    DateTime start = testeeResults[0].start;
                    DateTime end = testeeResults[0].end;
                    foreach (TesteeResult testee in testeeResults)
                    {
                        if (testee.start > start) start = testee.start;
                        if (testee.end < end) end = testee.end;
                    }
                    start = start > startTime ? start : startTime;
                    foreach (TesteeResult testee in testeeResults)
                    {
                        successfulTx2 += testee.successfulTx * (end - start).TotalMilliseconds / (testee.end - testee.start).TotalMilliseconds;
                    }
                    onChainTx2 = CountTransactions(startHeight, testeeResults[0].height);
                    writeLineCSVFile(new List<string> { successfulTx2.ToString(), onChainTx2.ToString(), (end - start).TotalSeconds.ToString(), (successfulTx / (end - start).TotalSeconds).ToString(), (onChainTx2 / (end - start).TotalSeconds).ToString() }, ref recorder);
                    recorder.Flush();
                    recorder.Close();
                    Console.WriteLine("Test has finished.");
                    for (int i = 0; i < 10; i++)
                    {
                        MessageBeep(beepI);
                        Thread.Sleep(1000);
                    }


                    /*int a = -1, b = -2, c = -3, round = 1;
                    while (a != b || b != c || round < 15)
                    {
                        Thread.Sleep(20000);
                        Console.WriteLine("Round " + round + " ...");
                        c = b;
                        b = a;
                        a = CountTransactions(startHeight);
                        Console.WriteLine("Current OnChain Transactions: " + a);
                        if (a >= successfulTx) break;
                        round++;
                    }
                    double totalSeconds = (endTime - startTime).TotalSeconds;
                    writeLineCSVFile(new List<string>{plannedTx.ToString(), successfulTx.ToString(), totalSeconds.ToString(), (successfulTx/totalSeconds).ToString(), a.ToString()}, ref recorder);
                    recorder.Flush();
                    recorder.Close();
                    Console.WriteLine("Test has finished.");*/
                }
            }
        }

        public static string CreateCommand(string command, List<string> parameters)
        {
            string result = testeeURL + @"/?jsonrpc=2.0&method=" + command + @"&params=[";
            if (parameters.Count != 0)
            {
                foreach (string parameter in parameters)
                {
                    result += "\"" + parameter + "\",";
                }
                result = result.Substring(0, result.Length - 1);
            }
            result += @"]&id=1";
            return result;
        }

        public static int CountTransactions(int start, int end = -1)
        {
            if (start < 0) return -1;
            if (start == end) return 0;
            HttpWebRequest webReq = null;
            webReq = (HttpWebRequest)WebRequest.Create(new Uri(CreateCommand(countTransactionsCommand, new List<string> { (start + 1).ToString(), end.ToString()})));
            webReq.Method = "GET";
            webReq.ContentType = "application/x-www-form-urlencoded";
            webReq.Timeout = 6000000;
            webReq.ContentLength = 0;

            HttpWebResponse response = (HttpWebResponse)webReq.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
            string ret = sr.ReadToEnd();
            sr.Close();
            response.Close();
            ReturnedResult deserializedResult = JsonConvert.DeserializeObject<ReturnedResult>(ret);
            if (deserializedResult.result != null)
                Console.WriteLine(deserializedResult.result);
            return int.Parse(deserializedResult.result);
        }

        public static int GetBlockHeight()
        {
            HttpWebRequest webReq = (HttpWebRequest)WebRequest.Create(new Uri(CreateCommand(getBlockCountCommand, new List<string> { })));
            webReq.Method = "GET";
            webReq.ContentType = "application/x-www-form-urlencoded";
            webReq.Timeout = 6000000;
            webReq.ContentLength = 0;

            HttpWebResponse response = (HttpWebResponse)webReq.GetResponse();
            StreamReader sr = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
            string ret = sr.ReadToEnd();
            sr.Close();
            response.Close();
            ReturnedResult deserializedResult = JsonConvert.DeserializeObject<ReturnedResult>(ret);
            int result = int.Parse(deserializedResult.result);
            return result <= 0 ? -1 : result - 1;
        }

        public static void writeLineCSVFile(List<string> inputs, ref StreamWriter writer)
        {
            if (inputs.Count == 0) return;
            string result = "";
            foreach (string input in inputs)
            {
                result += input + ",";
            }
            writer.WriteLine(result);
        }

        public static void startTestee(string ipInput)
        {
            Socket socketClient = new Socket(SocketType.Stream, ProtocolType.Tcp);
            IPAddress ip = IPAddress.Parse(ipInput);
            IPEndPoint point = new IPEndPoint(ip, port);
            //进行连接
            socketClient.Connect(point);
            Console.WriteLine(ipInput + " connected.");

            //不停的接收服务器端发送的消息
            Thread thread = new Thread(Receive);
            thread.IsBackground = true;
            thread.Start(socketClient);

            var buffer = Encoding.UTF8.GetBytes(plannedRounds.ToString());
            var temp = socketClient.Send(buffer);
        }

        static void Listen(object o)
        {
            var serverSocket = o as Socket;
            while (true)
            {
                //等待连接并且创建一个负责通讯的socket
                var send = serverSocket.Accept();
                //获取链接的IP地址
                var sendIpoint = send.RemoteEndPoint.ToString();
                Console.WriteLine($"{sendIpoint}Connection");
                //开启一个新线程不停接收消息
                Thread thread = new Thread(ReceiveReady);
                thread.IsBackground = true;
                thread.Start(send);
            }
        }

        static void ReceiveReady(object o)
        {
            var send = o as Socket;
            while (true)
            {
                //获取发送过来的消息容器
                byte[] buffer = new byte[1024 * 1024 * 2];
                var effective = send.Receive(buffer);
                //有效字节为0则跳过
                if (effective == 0)
                {
                    break;
                }
                var str = Encoding.UTF8.GetString(buffer, 0, effective);
                Console.WriteLine(str + " is ready.");
                successfulTestee++;
                testee.Add(str);
                if (successfulTestee == plannedTestee)
                {
                    startHeight = GetBlockHeight();
                    Console.WriteLine("Start height: " + startHeight);
                    startTime = DateTime.Now.AddYears(100);
                    Console.WriteLine("All testee are ready. Test starts.");
                    Thread thread = new Thread(ResetStartTimeAndHeight);
                    thread.Start();
                    foreach (string testIP in testee)
                    {
                        startTestee(testIP);
                    }
                }
            }
        }

        static void ResetStartTimeAndHeight()
        {
            Thread.Sleep(60000);
            if(!isOver)
            {
                startHeight = GetBlockHeight();
                startTime = DateTime.Now;
                Console.WriteLine("Start time reset!");
            }
        }

        static void Main(string[] args)
        {
            if (!readConfig()) return;

            if (!File.Exists(recorderFile))
            {
                recorder = new StreamWriter(recorderFile, true);
                writeLineCSVFile(new List<string> {"Successful Transactions", "OnChain Transactions", "Seconds Consumed", "Sent TPS", "OnChain TPS"}, ref recorder);
                recorder.Flush();
                recorder.Close();
            }
            recorder = new StreamWriter(recorderFile, true);

            Socket serverSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            IPAddress ip = IPAddress.Any;
            IPEndPoint point = new IPEndPoint(ip, port2);
            //socket绑定监听地址
            serverSocket.Bind(point);
            Console.WriteLine("Listen Success");
            //设置同时连接个数
            serverSocket.Listen(30);

            //利用线程后台执行监听,否则程序会假死
            Thread thread = new Thread(Listen);
            thread.IsBackground = true;
            thread.Start(serverSocket);

            Console.WriteLine("Input \"exit\" to finish test.");
            while (!Console.ReadLine().ToLower().Equals("exit"))
            {
            }

            Console.ReadLine();
        }
    }

    class ReturnedResult
    {
        public string jsonrpc = null;
        public string id = null;
        public string result = null;
    }

    class TesteeResult
    {
        public DateTime start;
        public DateTime end;
        public int successfulTx = 0;
        public int height = 0;
    }
}
