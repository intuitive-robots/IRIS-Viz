using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using UnityEngine;
using NetMQ;

namespace IRIS.Utilities
{

	public static class UnityPortSet
	{
		public static readonly int DISCOVERY = 7720;
		public static readonly int HEARTBEAT = 7721;
		public static readonly int SERVICE = 7730;
		public static readonly int TOPIC = 7731;
	}

	public class NodeAddress
	{
		public string ip;
		public int port;
		public NodeAddress(string ip, int port)
		{
			this.ip = ip;
			this.port = port;
		}
	}

	public class NodeInfo
	{
		public string name;
		public string nodeID;
		public string nodeInfoID;
		public string type;
		public int port;
		public List<string> serviceList = new();
		public Dictionary<string, int> topicDict = new();

		public NodeInfo(string nodeName, string nodeType, int servicePort)
		{
			name = nodeName;
			nodeID = Guid.NewGuid().ToString();
			nodeInfoID = Guid.NewGuid().ToString();
			type = nodeType;
			port = servicePort;
		}

		public void AddService(string serviceName)
		{
			if (!serviceList.Contains(serviceName))
			{
				serviceList.Add(serviceName);
			}
			GenerateNewNodeInfoID();
		}

		public void RemoveService(string serviceName)
		{
			if (serviceList.Contains(serviceName))
			{
				serviceList.Remove(serviceName);
			}
			GenerateNewNodeInfoID();
		}


		public void AddTopic(string topicName, int port)
		{
			if (!topicDict.ContainsKey(topicName))
			{
				topicDict[topicName] = port;
			}
			GenerateNewNodeInfoID();
		}

		public void RemoveTopic(string topicName)
		{
			if (topicDict.ContainsKey(topicName))
			{
				topicDict.Remove(topicName);
			}
			GenerateNewNodeInfoID();
		}

		private void GenerateNewNodeInfoID()
		{
			nodeInfoID = Guid.NewGuid().ToString();
		}

		public void Rename(string newName)
		{
			name = newName;
			GenerateNewNodeInfoID();
		}


	}

	public static class NetworkUtils
	{

		public static UdpClient CreateUDPClient(IPEndPoint endPoint)
		{
			UdpClient udpClient = new UdpClient();
			udpClient.Client.Bind(endPoint);
			return udpClient;
		}

		public static UdpClient CreateUDPClient(int port)
		{
			return CreateUDPClient(new IPEndPoint(IPAddress.Any, port));
		}

		public static UdpClient CreateUDPClient(string localIP, int port)
		{
			IPAddress ipAddress = IPAddress.Parse(localIP);
			return CreateUDPClient(new IPEndPoint(ipAddress, port));
		}

		public static string GetLocalIPsInSameSubnet(string inputIPAddress)
		{
			if (inputIPAddress == "127.0.0.1")
			{
				return "127.0.0.1";
			}
			IPAddress inputIP;
			if (!IPAddress.TryParse(inputIPAddress, out inputIP))
			{
				throw new ArgumentException("Invalid IP address format.", nameof(inputIPAddress));
			}
			IPAddress subnetMask = IPAddress.Parse("255.255.255.0");
			// Get all network interfaces
			NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
			foreach (NetworkInterface ni in networkInterfaces)
			{
				// Get IP properties of the network interface
				IPInterfaceProperties ipProperties = ni.GetIPProperties();
				UnicastIPAddressInformationCollection unicastIPAddresses = ipProperties.UnicastAddresses;
				foreach (UnicastIPAddressInformation ipInfo in unicastIPAddresses)
				{
					if (ipInfo.Address.AddressFamily == AddressFamily.InterNetwork)
					{
						IPAddress localIP = ipInfo.Address;
						// Check if the IP is in the same subnet
						if (IsInSameSubnet(inputIP, localIP, subnetMask))
						{
							return localIP.ToString();
						}
					}
				}
			}
			return null;
		}

		public struct InterfaceInfo
		{
			public string Name;
			public IPAddress IPAddress;
			public string Description;
			public NetworkInterfaceType InterfaceType;
			public OperationalStatus Status;

			public override string ToString()
			{
				return $"{Name} ({IPAddress}) - {InterfaceType} [{Status}]";
			}
		}

		public static List<IPAddress> GetNetworkInterfaces(bool includeLoopback = false, bool includeInactive = false)
		{
			List<IPAddress> interfaceList = new List<IPAddress>();

			try
			{
				NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();

				foreach (NetworkInterface networkInterface in interfaces)
				{
					// Skip based on operational status
					if (!includeInactive && networkInterface.OperationalStatus != OperationalStatus.Up)
					{
						continue;
					}

					// Skip based on interface type
					if (!includeLoopback && networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
					{
						continue;
					}

					// Skip tunnel interfaces
					if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
					{
						continue;
					}

					// Get IP properties
					IPInterfaceProperties ipProperties = networkInterface.GetIPProperties();

					foreach (UnicastIPAddressInformation unicastInfo in ipProperties.UnicastAddresses)
					{
						// Only include IPv4 addresses and skip link-local addresses
						if (unicastInfo.Address.AddressFamily == AddressFamily.InterNetwork &&
							!unicastInfo.Address.ToString().StartsWith("169.254") &&
							!unicastInfo.Address.ToString().StartsWith("127.0.0.1")) // Skip APIPA addresses
						{
							interfaceList.Add(unicastInfo.Address);
						}
					}
				}
			}
			catch (Exception e)
			{
				Debug.LogError($"Error getting network interfaces: {e.Message}");
			}

			return interfaceList;
		}


		private static bool IsInSameSubnet(IPAddress ip1, IPAddress ip2, IPAddress subnetMask)
		{
			byte[] ip1Bytes = ip1.GetAddressBytes();
			byte[] ip2Bytes = ip2.GetAddressBytes();
			byte[] maskBytes = subnetMask.GetAddressBytes();

			for (int i = 0; i < ip1Bytes.Length; i++)
			{
				if ((ip1Bytes[i] & maskBytes[i]) != (ip2Bytes[i] & maskBytes[i]))
				{
					return false;
				}
			}
			return true;
		}

		public static int GetNetZMQSocketPort(NetMQSocket _socket)
		{
			string pubEndpoint = _socket.Options.LastEndpoint;
			Debug.Log($"Publisher initialized at port {pubEndpoint}");
			string pubPortString = pubEndpoint.Split(':')[2];
			return pubPortString != null ? int.Parse(pubPortString) : 0;
		}

	}
}