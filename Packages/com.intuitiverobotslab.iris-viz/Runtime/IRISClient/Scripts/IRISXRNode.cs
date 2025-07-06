using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using NetMQ;
using NetMQ.Sockets;
using IRIS.Utilities;
using System.Linq;

namespace IRIS.Node
{
    [DefaultExecutionOrder(-100)]
    public class IRISXRNode : Singleton<IRISXRNode>
    {

        public NodeInfo localInfo { get; set; }
        private string localID { get; set; }

        [Header("Multicast Settings")]
        public string multicastAddress = "239.255.10.10";
        public int port = 7720;
        public float messageSendInterval = 1.0f; // Send a message every second

        private UdpClient udpClient;
        private IPEndPoint remoteEndPoint;
        private CancellationTokenSource cancellationTokenSource;
        private Task multicastTask;
        private Task serviceTask;
        private string multicastMessage;
        public Action SubscriptionSpin;
        private NetMQRuntime runtime;
        private ResponseSocket _resSocket;
        public Dictionary<string, Func<byte[][], byte[][]>> serviceCallbacks;
        public PublisherSocket _pubSocket;
        public List<string> serviceList;
        private List<NetMQSocket> _sockets;
        private Dictionary<string, INetComponent> serviceDict;

        void Start()
        {
            // NOTE: This is not necessary use DontDestroyOnLoad
            // DontDestroyOnLoad(gameObject);
            // Force to use .NET implementation of NetMQ
            AsyncIO.ForceDotNet.Force();
            // runtime = new NetMQRuntime();
            // runtime.Run();
            // Initialize local node info
            localInfo = new NodeInfo
            {
                name = "UnityNode",
                nodeID = Guid.NewGuid().ToString(),
                type = "UnityNode",
                servicePort = 0,
                topicPort = 0,
                serviceList = new List<string>(),
                topicList = new List<string>()
            };
            serviceDict = new Dictionary<string, INetComponent>();
            // Default host name
            if (PlayerPrefs.HasKey("HostName"))
            {
                // The key exists, proceed to get the value
                string nodeName = PlayerPrefs.GetString("HostName");
                Debug.Log($"Find Host Name: {nodeName}");
            }
            else
            {
                string nodeName = $"Unity-{Environment.MachineName}";
                Debug.Log($"Host Name not found, using default name {nodeName}");
            }
            // NOTE: Since the NetZMQ setting is initialized in "AsyncIO.ForceDotNet.Force();"
            // NOTE: we should initialize the sockets after that
            _pubSocket = new PublisherSocket();
            _resSocket = new ResponseSocket();

            _sockets = new List<NetMQSocket>() { _pubSocket, _resSocket };
            serviceCallbacks = new();
            Debug.Log("IRISXRNode initialized");
            // cancellationTokenSource = new CancellationTokenSource();

            Debug.Log("IRISXRNode started");
            InitializeService();

            // Initialize cancellation token
            cancellationTokenSource = new CancellationTokenSource();
            // Start the sending task
            InitializeUdpClient();
            multicastTask = StartMulticastTask(cancellationTokenSource.Token);
            serviceTask = StartServiceTask(cancellationTokenSource.Token);
        }

        void Update()
        {
            SubscriptionSpin?.Invoke();
        }

        private void InitializeUdpClient()
        {
            try
            {
                // Clean up existing client if it exists
                if (udpClient != null)
                {
                    udpClient.Close();
                    udpClient = null;
                }

                // Create UDP client using default interface
                udpClient = new UdpClient();

                // Configure for multicast
                remoteEndPoint = new IPEndPoint(IPAddress.Parse(multicastAddress), port);

                // Set TTL for multicast
                udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);

                Debug.Log("UDP client initialized successfully using default interface");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error initializing UDP client: {e.Message}");
            }
        }

        private void InitializeService()
        {
            _resSocket.Bind("tcp://0.0.0.0:0");
            string endpoint = _resSocket.Options.LastEndpoint;
            Debug.Log($"Service initialized at port {endpoint}");
            string portString = endpoint.Split(':')[2];
            localInfo.servicePort = portString != null ? int.Parse(portString) : 0;

            serviceDict["Rename"] = new IRISService<string, string>("Rename", Rename, true);
            serviceDict["GetNodeInfo"] = new IRISService<string, NodeInfo>("GetNodeInfo", (req) => localInfo, true);
        }


        private async Task StartMulticastTask(CancellationToken cancellationToken)
        {
            Debug.Log("Multicast sender initialized with threading");
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    SendMulticastMessage($"{localInfo.nodeID}{localInfo.servicePort}");

                    // Wait for the specified interval or until cancellation is requested
                    await Task.Delay(TimeSpan.FromSeconds(messageSendInterval), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("Multicast sending task was cancelled");
            }
            catch (Exception e)
            {
                Debug.LogError("Error in sending task: " + e.Message);
            }
        }

        private async Task StartServiceTask(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Use timeout to allow cancellation checks
                    List<byte[]> messageReceived = await Task.Run(() => _resSocket.ReceiveMultipartBytes(), cancellationToken);
                    if (messageReceived.Count > 1)
                    {
                        // Extract service name from first frame
                        string serviceName = MsgUtils.Bytes2String(messageReceived[0]);
                        Debug.Log($"Received service {serviceName}");
                        if (serviceCallbacks.ContainsKey(serviceName))
                        {
                            // Run callback on background thread if it's CPU-intensive
                            byte[][] response = serviceCallbacks[serviceName](messageReceived.Skip(1).ToArray());
                            _resSocket.SendMultipartBytes(response);
                        }
                        else
                        {
                            Debug.LogWarning($"Service {serviceName} not found");
                            _resSocket.SendFrame(IRISSignal.NOSERVICE);
                        }
                    }
                    else
                    {
                        Debug.LogWarning("Received message does not contain service name or request data");
                        _resSocket.SendFrame(IRISSignal.ERROR);
                    }
                }
                catch (TimeoutException)
                {
                    // Timeout is expected - allows cancellation token to be checked
                    continue;
                }
                catch (OperationCanceledException)
                {
                    Debug.Log("Service task was cancelled");
                    return; // Exit the loop if cancellation is requested
                }
                catch (Exception e)
                {
                    Debug.LogError("Error receiving service request: " + e.Message);
                    _resSocket.SendFrame(IRISSignal.ERROR);
                }
            }
        }


        void SendMulticastMessage(string message)
        {
            try
            {
                // Check if UDP client and endpoint are initialized
                if (udpClient == null || remoteEndPoint == null)
                {
                    Debug.LogWarning("UDP client or remote endpoint is null. Reinitializing...");
                    InitializeUdpClient();
                    return;
                }

                byte[] data = Encoding.UTF8.GetBytes(message);
                udpClient.Send(data, data.Length, remoteEndPoint);
                // Debug.Log("Sent: " + message);
            }
            catch (Exception e)
            {
                Debug.LogError("Error sending multicast message: " + e.Message);
            }
        }



        void OnDestroy()
        {
            // Cancel the task
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
            }

            // Wait for task completion (with timeout)
            if (multicastTask != null)
            {
                try
                {
                    multicastTask.Wait(TimeSpan.FromSeconds(1));
                }
                catch (AggregateException)
                {
                    // Task was cancelled, which is expected
                }
            }

            // Clean up resources
            if (udpClient != null)
            {
                udpClient.Close();
            }

            cancellationTokenSource?.Dispose();
        }



        public string Rename(string newName)
        {
            localInfo.name = newName;
            PlayerPrefs.SetString("HostName", localInfo.name);
            Debug.Log($"Change Host Name to {localInfo.name}");
            PlayerPrefs.Save();
            return IRISSignal.SUCCESS;
        }

    }
}