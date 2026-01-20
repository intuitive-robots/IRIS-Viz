using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using NetMQ;
using NetMQ.Sockets;
using IRIS.Utilities;

namespace IRIS.Node
{
    [DefaultExecutionOrder(-100)]
    public class IRISXRNode : Singleton<IRISXRNode>
    {

        public LocalInfo localInfo { get; set; }

        [Header("Multicast Settings")]
        public string multicastAddress = "239.255.10.10";
        public int port = 7720;
        public float messageSendInterval = 1.0f; // Send a message every second
        private CancellationTokenSource cancellationTokenSource;
        private List<Task> multicastTasksList = new();
        public Action SubscriptionSpin;
        public Dictionary<string, NetMQSocket> sockets = new();
        public ServiceManager ServiceManager { get; private set; }
        public SubscriberManager SubscriberManager { get; private set; }

        void Start()
        {
            // NOTE: This is not necessary use DontDestroyOnLoad
            // DontDestroyOnLoad(gameObject);
            // Force to use .NET implementation of NetMQ
            // It may not be necessary on Linux, but Windows requires it
            // NOTE: Since the NetZMQ setting is initialized in "AsyncIO.ForceDotNet.Force();"
            // NOTE: we should initialize the sockets after that
            AsyncIO.ForceDotNet.Force();
            NetMQConfig.Linger = TimeSpan.Zero;
            // Initialize local node info
            // Default host name
            string nodeName = $"Unity-{Environment.MachineName}";
            if (PlayerPrefs.HasKey("HostName"))
            {
                // The key exists, proceed to get the value
                nodeName = PlayerPrefs.GetString("HostName");
                Debug.Log($"Find Host Name: {nodeName}");
            }
            else
            {
                Debug.Log($"Host Name not found, using default name {nodeName}");
            }
            // TODO: check the management of sockets
            // _sockets = new List<NetMQSocket>() { ServiceManager.ResponseSocket };
            localInfo = new LocalInfo(nodeName, "UnityNode", 0);
            Debug.Log("IRISXRNode started");
            // Initialize cancellation token
            cancellationTokenSource = new CancellationTokenSource();
            ServiceManager = new ServiceManager(cancellationTokenSource.Token);
            SubscriberManager = new SubscriberManager();
            InitializeDefaultServices();
            
            foreach (IPAddress ipAddress in NetworkUtils.GetNetworkInterfaces(true, true))
            {
                // Start the multicast sending task for each interface
                multicastTasksList.Add(StartMulticastAsync(ipAddress, cancellationTokenSource.Token));
            }
        }

        void Update()
        {
            // ServiceRespondSpin();
            SubscriptionSpin?.Invoke();
        }

        private void InitializeDefaultServices()
        {
            ServiceManager.RegisterServiceCallback<string, NodeInfo>("GetNodeInfo", (req) => localInfo.nodeInfo);
            ServiceManager.RegisterServiceCallback<string, string>("Rename", (req) => Rename(req));
        }

        private async Task StartMulticastAsync(IPAddress ipAddress, CancellationToken cancellationToken)
        {
            UdpClient client = null;
            try
            {
                // Create UDP client bound to specific interface
                client = new UdpClient(new IPEndPoint(ipAddress, 0));
                // Configure multicast options
                client.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 32);
                client.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, ipAddress.GetAddressBytes());
                // Configure for multicast
                IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Parse(multicastAddress), port);
                Debug.Log($"UDP client initialized successfully for interface {ipAddress}");
                while (!cancellationToken.IsCancellationRequested)
                {
                    byte[] msgBytes = NodeInfoSerializer.EncodeNodeInfo(localInfo.nodeInfo);
                    client.Send(msgBytes, msgBytes.Length, remoteEndPoint);
                    // Wait for the specified interval or until cancellation is requested
                    await Task.Delay(TimeSpan.FromSeconds(messageSendInterval), cancellationToken);
                    Debug.Log($"Multicast message sent from interface {ipAddress}");
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"Multicast sending task was cancelled for {ipAddress}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in sending task for {ipAddress}: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                // Clean up the client
                try
                {
                    client?.Close();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error closing UDP client for {ipAddress}: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }

        void OnDestroy()
        {
            ServiceManager?.UnregisterAll();
            // Cancel the task
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
            }

            // Wait for task completion (with timeout)
            Task.WhenAll(multicastTasksList).ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    Debug.LogError($"Error in multicast tasks: {t.Exception?.Message}\n{t.Exception?.StackTrace}");
                }
            });

            // Clean up sockets
            foreach (var socket in sockets.Values)
            {
                try
                {
                    socket?.Close();
                    Debug.Log($"Socket {socket.GetType().Name} closed successfully.");
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error closing socket {socket.GetType().Name}: {e.Message}");
                }
            }
            ServiceManager?.Close();
            SubscriberManager?.Close();
            // Clean up service callbacks.
            // Windows require this, otherwise it will block when quitting
            NetMQConfig.Cleanup();
            Debug.Log("IRISXRNode destroyed and resources cleaned up.");
        }


        public string Rename(string newName)
        {
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                localInfo.Rename(newName);
                PlayerPrefs.SetString("HostName", localInfo.nodeInfo.Name);
                Debug.Log($"Change Host Name to {localInfo.nodeInfo.Name}");
                PlayerPrefs.Save();                
            });
            return IRISMSG.SUCCESS;
        }

        public List<string> GetServiceList(string req)
        {
            return ServiceManager.GetServiceList();
        }

    }
}
