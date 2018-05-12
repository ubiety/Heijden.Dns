using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Heijden.DNS;

/*
 * Network Working Group                                     P. Mockapetris
 * Request for Comments: 1035                                           ISI
 *                                                            November 1987
 *
 *           DOMAIN NAMES - IMPLEMENTATION AND SPECIFICATION
 *
 */

namespace Ubiety.Dns.Core
{
    /// <summary>
    ///     DNS resolver runs querys against a server
    /// </summary>
    public class Resolver
    {
        /// <summary>
        ///     Gets the current version of the library
        /// </summary>
        public string Version
        {
            get
            {
                return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
        }

        /// <summary>
        ///     Default DNS port
        /// </summary>
        public const int DefaultPort = 53;

        /// <summary>
        ///     Default OpenDNS server addresses
        /// </summary>
        public static readonly IPEndPoint[] DefaultDnsServers = 
            { 
                new IPEndPoint(IPAddress.Parse("208.67.222.222"), DefaultPort), 
                new IPEndPoint(IPAddress.Parse("208.67.220.220"), DefaultPort) 
            };

        private ushort m_Unique;
        private bool m_UseCache;
        private bool m_Recursion;
        private int m_Retries;
        private int m_Timeout;
        private TransportType m_TransportType;

        private List<IPEndPoint> m_DnsServers;

        private Dictionary<string,Response> m_ResponseCache;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Resolver" /> class
        /// </summary>
        /// <param name="DnsServers">Set of DNS servers</param>
        public Resolver(IPEndPoint[] DnsServers)
        {
            this.m_ResponseCache = new Dictionary<string, Response>();
            this.m_DnsServers = new List<IPEndPoint>();
            this.m_DnsServers.AddRange(DnsServers);

            this.m_Unique = (ushort)(new Random()).Next();
            this.m_Retries = 3;
            this.m_Timeout = 1;
            this.m_Recursion = true;
            this.m_UseCache = true;
            this.m_TransportType = TransportType.Udp;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="Resolver" /> class
        /// </summary>
        /// <param name="DnsServer">DNS server to use</param>
        public Resolver(IPEndPoint DnsServer)
            : this(new IPEndPoint[] { DnsServer })
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="Resolver" /> class
        /// </summary>
        /// <param name="ServerIpAddress">DNS server to use</param>
        /// <param name="ServerPortNumber">DNS port to use</param>
        public Resolver(IPAddress ServerIpAddress, int ServerPortNumber)
            : this(new IPEndPoint(ServerIpAddress,ServerPortNumber))
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="Resolver" /> class
        /// </summary>
        /// <param name="ServerIpAddress">DNS server address to use</param>
        /// <param name="ServerPortNumber">DNS port to use</param>
        public Resolver(string ServerIpAddress, int ServerPortNumber)
            : this(IPAddress.Parse(ServerIpAddress), ServerPortNumber)
        {
        }
        
        /// <summary>
        ///     Initializes a new instance of the <see cref="Resolver" /> class
        /// </summary>
        /// <param name="ServerIpAddress">DNS server address to use</param>
        public Resolver(string ServerIpAddress)
            : this(IPAddress.Parse(ServerIpAddress), DefaultPort)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="Resolver" /> class
        /// </summary>
        public Resolver()
            : this(GetDnsServers())
        {
        }

        /// <summary>
        ///     Event args for verbose output
        /// </summary>
        public class VerboseOutputEventArgs : EventArgs
        {
            /// <summary>
            ///     Gets the string message
            /// </summary>
            public string Message;

            /// <summary>
            ///     Initializes a new instance of the <see cref="VerboseOutputEventArgs" /> class
            /// </summary>
            /// <param name="Message">Message to output</param>
            public VerboseOutputEventArgs(string Message)
            {
                this.Message = Message;
            }
        }

        private void Verbose(string format, params object[] args)
        {
            if (OnVerbose != null)
                OnVerbose(this, new VerboseEventArgs(string.Format(format, args)));
        }

        /// <summary>
        /// Verbose messages from internal operations
        /// </summary>
        public event VerboseEventHandler OnVerbose;

        /// <summary>
        ///     Verbose event handler
        /// </summary>
        public delegate void VerboseEventHandler(object sender, VerboseEventArgs e);

        /// <summary>
        ///     Verbose event args
        /// </summary>
        public class VerboseEventArgs : EventArgs
        {
            /// <summary>
            ///     Gets the message to output
            /// </summary>
            public string Message;

            /// <summary>
            ///     Initializes a new instance of the <see cref="VerboseEventArgs" /> class
            /// </summary>
            public VerboseEventArgs(string Message)
            {
                this.Message = Message;
            }
        }


        /// <summary>
        ///     Gets or sets timeout in milliseconds
        /// </summary>
        public int TimeOut
        {
            get
            {
                return this.m_Timeout;
            }
            set
            {
                this.m_Timeout = value;
            }
        }

        /// <summary>
        ///     Gets or sets the number of retries before giving up
        /// </summary>
        public int Retries
        {
            get
            {
                return this.m_Retries;
            }
            set
            {
                if(value>=1)
                    this.m_Retries = value;
            }
        }

        /// <summary>
        ///     Gets or set recursion for doing queries
        /// </summary>
        public bool Recursion
        {
            get
            {
                return this.m_Recursion;
            }
            set
            {
                this.m_Recursion = value;
            }
        }

        /// <summary>
        ///     Gets or sets protocol to use
        /// </summary>
        public TransportType TransportType
        {
            get
            {
                return this.m_TransportType;
            }
            set
            {
                this.m_TransportType = value;
            }
        }

        /// <summary>
        ///     Gets or sets list of DNS servers to use
        /// </summary>
        public IPEndPoint[] DnsServers
        {
            get
            {
                return this.m_DnsServers.ToArray();
            }
            set
            {
                this.m_DnsServers.Clear();
                this.m_DnsServers.AddRange(value);
            }
        }

        /// <summary>
        ///     Gets first DNS server address or sets single DNS server to use
        /// </summary>
        public string DnsServer
        {
            get
            {
                return this.m_DnsServers[0].Address.ToString();
            }
            set
            {
                IPAddress ip;
                if (IPAddress.TryParse(value, out ip))
                {
                    this.m_DnsServers.Clear();
                    this.m_DnsServers.Add(new IPEndPoint(ip, DefaultPort));
                    return;
                }
                Response response = Query(value, QType.A);
                if (response.RecordsA.Length > 0)
                {
                    this.m_DnsServers.Clear();
                    this.m_DnsServers.Add(new IPEndPoint(response.RecordsA[0].Address, DefaultPort));
                }
            }
        }

        /// <summary>
        ///     Gets or sets whether to use the cache
        /// </summary>
        public bool UseCache
        {
            get
            {
                return this.m_UseCache;
            }
            set
            {
                this.m_UseCache = value;
                if (!this.m_UseCache)
                    this.m_ResponseCache.Clear();
            }
        }

        /// <summary>
        ///     Clear the resolver cache
        /// </summary>
        public void ClearCache()
        {
            this.m_ResponseCache.Clear();
        }

        private Response SearchInCache(Question question)
        {
            if (!this.m_UseCache)
                return null;

            string strKey = question.QClass + "-" + question.QType + "-" + question.QName;

            Response response = null;

            lock (this.m_ResponseCache)
            {
                if (!this.m_ResponseCache.ContainsKey(strKey))
                    return null;

                response = this.m_ResponseCache[strKey];
            }

            int TimeLived = (int)((DateTime.Now.Ticks - response.TimeStamp.Ticks) / TimeSpan.TicksPerSecond);
            foreach (RR rr in response.RecordsRR)
            {
                rr.TimeLived = TimeLived;
                // The TTL property calculates its actual time to live
                if (rr.TTL == 0)
                    return null; // out of date
            }
            return response;
        }

        private void AddToCache(Response response)
        {
            if (!this.m_UseCache)
                return;

            // No question, no caching
            if (response.Questions.Count == 0)
                return;

            // Only cached non-error responses
            if (response.header.RCODE != RCode.NoError)
                return;

            Question question = response.Questions[0];

            string strKey = question.QClass + "-" + question.QType + "-" + question.QName;

            lock (this.m_ResponseCache)
            {
                if (this.m_ResponseCache.ContainsKey(strKey))
                    this.m_ResponseCache.Remove(strKey);

                this.m_ResponseCache.Add(strKey, response);
            }
        }

        private Response UdpRequest(Request request)
        {
            // RFC1035 max. size of a UDP datagram is 512 bytes
            byte[] responseMessage = new byte[512];

            for (int intAttempts = 0; intAttempts < this.m_Retries; intAttempts++)
            {
                for (int intDnsServer = 0; intDnsServer < this.m_DnsServers.Count; intDnsServer++)
                {
                    Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, this.m_Timeout * 1000);

                    try
                    {
                        socket.SendTo(request.Data, this.m_DnsServers[intDnsServer]);
                        int intReceived = socket.Receive(responseMessage);
                        byte[] data = new byte[intReceived];
                        Array.Copy(responseMessage, data, intReceived);
                        Response response = new Response(this.m_DnsServers[intDnsServer], data);
                        AddToCache(response);
                        return response;
                    }
                    catch (SocketException)
                    {
                        Verbose(string.Format(";; Connection to nameserver {0} failed", (intDnsServer + 1)));
                        continue; // next try
                    }
                    finally
                    {
                        this.m_Unique++;

                        // close the socket
                        socket.Close();
                    }
                }
            }
            Response responseTimeout = new Response();
            responseTimeout.Error = "Timeout Error";
            return responseTimeout;
        }

        private Response TcpRequest(Request request)
        {
            //System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            //sw.Start();

            byte[] responseMessage = new byte[512];

            for (int intAttempts = 0; intAttempts < this.m_Retries; intAttempts++)
            {
                for (int intDnsServer = 0; intDnsServer < this.m_DnsServers.Count; intDnsServer++)
                {
                    TcpClient tcpClient = new TcpClient();
                    tcpClient.ReceiveTimeout = this.m_Timeout * 1000;

                    try
                    {
                        IAsyncResult result = tcpClient.BeginConnect(this.m_DnsServers[intDnsServer].Address, this.m_DnsServers[intDnsServer].Port, null, null);

                        bool success = result.AsyncWaitHandle.WaitOne(m_Timeout*1000, true);

                        if (!success || !tcpClient.Connected)
                        {
                            tcpClient.Close();
                            this.Verbose(string.Format(";; Connection to nameserver {0} failed", (intDnsServer + 1)));
                            continue;
                        }

                        BufferedStream bs = new BufferedStream(tcpClient.GetStream());

                        byte[] data = request.Data;
                        bs.WriteByte((byte)((data.Length >> 8) & 0xff));
                        bs.WriteByte((byte)(data.Length & 0xff));
                        bs.Write(data, 0, data.Length);
                        bs.Flush();

                        Response TransferResponse = new Response();
                        int intSoa = 0;
                        int intMessageSize = 0;

                        //Debug.WriteLine("Sending "+ (request.Length+2) + " bytes in "+ sw.ElapsedMilliseconds+" mS");

                        while (true)
                        {
                            int intLength = bs.ReadByte() << 8 | bs.ReadByte();
                            if (intLength <= 0)
                            {
                                tcpClient.Close();
                                this.Verbose(string.Format(";; Connection to nameserver {0} failed", (intDnsServer + 1)));
                                throw new SocketException(); // next try
                            }

                            intMessageSize += intLength;

                            data = new byte[intLength];
                            bs.Read(data, 0, intLength);
                            Response response = new Response(this.m_DnsServers[intDnsServer], data);

                            //Debug.WriteLine("Received "+ (intLength+2)+" bytes in "+sw.ElapsedMilliseconds +" mS");

                            if (response.header.RCODE != RCode.NoError)
                                return response;

                            if (response.Questions[0].QType != QType.AXFR)
                            {
                                this.AddToCache(response);
                                return response;
                            }

                            // Zone transfer!!

                            if(TransferResponse.Questions.Count==0)
                                TransferResponse.Questions.AddRange(response.Questions);
                            TransferResponse.Answers.AddRange(response.Answers);
                            TransferResponse.Authorities.AddRange(response.Authorities);
                            TransferResponse.Additionals.AddRange(response.Additionals);

                            if (response.Answers[0].Type == RecordType.SOA)
                                    intSoa++;

                            if (intSoa == 2)
                            {
                                TransferResponse.header.QuestionCount = (ushort)TransferResponse.Questions.Count;
                                TransferResponse.header.AnswerCount = (ushort)TransferResponse.Answers.Count;
                                TransferResponse.header.NameserverCount = (ushort)TransferResponse.Authorities.Count;
                                TransferResponse.header.AdditionalRecordsCount = (ushort)TransferResponse.Additionals.Count;
                                TransferResponse.MessageSize = intMessageSize;
                                return TransferResponse;
                            }
                        }
                    } // try
                    catch (SocketException)
                    {
                        continue; // next try
                    }
                    finally
                    {
                        this.m_Unique++;

                        // close the socket
                        tcpClient.Close();
                    }
                }
            }
            Response responseTimeout = new Response();
            responseTimeout.Error = "Timeout Error";
            return responseTimeout;
        }

        /// <summary>
        /// Do Query on specified DNS servers
        /// </summary>
        /// <param name="name">Name to query</param>
        /// <param name="qtype">Question type</param>
        /// <param name="qclass">Class type</param>
        /// <returns>Response of the query</returns>
        public Response Query(string name, QType qtype, QClass qclass)
        {
            Question question = new Question(name, qtype, qclass);
            Response response = SearchInCache(question);
            if (response != null)
                return response;

            Request request = new Request();
            request.AddQuestion(question);
            return GetResponse(request);
        }

        /// <summary>
        /// Do an QClass=IN Query on specified DNS servers
        /// </summary>
        /// <param name="name">Name to query</param>
        /// <param name="qtype">Question type</param>
        /// <returns>Response of the query</returns>
        public Response Query(string name, QType qtype)
        {
            Question question = new Question(name, qtype, QClass.IN);
            Response response = SearchInCache(question);
            if (response != null)
                return response;

            Request request = new Request();
            request.AddQuestion(question);
            return GetResponse(request);
        }

        private Response GetResponse(Request request)
        {
            request.header.Id = this.m_Unique;
            request.header.RD = this.m_Recursion;

            if (this.m_TransportType == TransportType.Udp)
                return UdpRequest(request);

            if (this.m_TransportType == TransportType.Tcp)
                return TcpRequest(request);

            Response response = new Response();
            response.Error = "Unknown TransportType";
            return response;
        }

        /// <summary>
        /// Gets a list of default DNS servers used on the Windows machine.
        /// </summary>
        /// <returns></returns>
        public static IPEndPoint[] GetDnsServers()
        {
            List<IPEndPoint> list = new List<IPEndPoint>();

            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface n in adapters)
            {
                if (n.OperationalStatus == OperationalStatus.Up)
                {
                    IPInterfaceProperties ipProps = n.GetIPProperties();
                    // thanks to Jon Webster on May 20, 2008
                    foreach (IPAddress ipAddr in ipProps.DnsAddresses)
                    {
                        IPEndPoint entry = new IPEndPoint(ipAddr, DefaultPort);
                        if (!list.Contains(entry))
                            list.Add(entry);
                    }

                }
            }
            return list.ToArray();
        } 

        private IPHostEntry MakeEntry(string HostName)
        {
            IPHostEntry entry = new IPHostEntry();

            entry.HostName = HostName;

            Response response = Query(HostName, QType.A, QClass.IN);

            // fill AddressList and aliases
            List<IPAddress> AddressList = new List<IPAddress>();
            List<string> Aliases = new List<string>();
            foreach (AnswerRR answerRR in response.Answers)
            {
                if (answerRR.Type == RecordType.A)
                {
                    // answerRR.RECORD.ToString() == (answerRR.RECORD as RecordA).Address
                    AddressList.Add(IPAddress.Parse((answerRR.RECORD.ToString())));
                    entry.HostName = answerRR.NAME;
                }
                else
                {
                    if (answerRR.Type == RecordType.CNAME)
                        Aliases.Add(answerRR.NAME);
                }
            }
            entry.AddressList = AddressList.ToArray();
            entry.Aliases = Aliases.ToArray();

            return entry;
        }

        /// <summary>
        /// Translates the IPV4 or IPV6 address into an arpa address
        /// </summary>
        /// <param name="ip">IP address to get the arpa address form</param>
        /// <returns>The 'mirrored' IPV4 or IPV6 arpa address</returns>
        public static string GetArpaFromIp(IPAddress ip)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("in-addr.arpa.");
                foreach (byte b in ip.GetAddressBytes())
                {
                    sb.Insert(0, string.Format("{0}.", b));
                }
                return sb.ToString();
            }
            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("ip6.arpa.");
                foreach (byte b in ip.GetAddressBytes())
                {
                    sb.Insert(0, string.Format("{0:x}.", (b >> 4) & 0xf));
                    sb.Insert(0, string.Format("{0:x}.", (b >> 0) & 0xf));
                }
                return sb.ToString();
            }
            return "?";
        }

        /// <summary>
        /// </summary>
        public static string GetArpaFromEnum(string strEnum)
        {
            StringBuilder sb = new StringBuilder();
            string Number = System.Text.RegularExpressions.Regex.Replace(strEnum, "[^0-9]", "");
            sb.Append("e164.arpa.");
            foreach (char c in Number)
            {
                sb.Insert(0, string.Format("{0}.", c));
            }
            return sb.ToString();
        }

        private enum RRRecordStatus
        {
            UNKNOWN,
            NAME,
            TTL,
            CLASS,
            TYPE,
            VALUE
        }

        /// <summary>
        /// </summary>
        public void LoadRootFile(string strPath)
        {
            StreamReader sr = new StreamReader(strPath);
            while (!sr.EndOfStream)
            {
                string strLine = sr.ReadLine();
                if (strLine == null)
                    break;
                int intI = strLine.IndexOf(';');
                if (intI >= 0)
                    strLine = strLine.Substring(0, intI);
                strLine = strLine.Trim();
                if (strLine.Length == 0)
                    continue;
                RRRecordStatus status = RRRecordStatus.NAME;
                string Name="";
                string Ttl="";
                string Class="";
                string Type="";
                string Value="";
                string strW = "";
                for (intI = 0; intI < strLine.Length; intI++)
                {
                    char C = strLine[intI];

                    if (C <= ' ' && strW!="")
                    {
                        switch (status)
                        {
                            case RRRecordStatus.NAME:
                                Name = strW;
                                status = RRRecordStatus.TTL;
                                break;
                            case RRRecordStatus.TTL:
                                Ttl = strW;
                                status = RRRecordStatus.CLASS;
                                break;
                            case RRRecordStatus.CLASS:
                                Class = strW;
                                status = RRRecordStatus.TYPE;
                                break;
                            case RRRecordStatus.TYPE:
                                Type = strW;
                                status = RRRecordStatus.VALUE;
                                break;
                            case RRRecordStatus.VALUE:
                                Value = strW;
                                status = RRRecordStatus.UNKNOWN;
                                break;
                            default:
                                break;
                        }
                        strW = "";
                    }
                    if (C > ' ')
                        strW += C;
                }

            }
            sr.Close();
        }
    } // class
}