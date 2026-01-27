using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using MessagePack;
using NetMQ;
using UnityEngine;

namespace IRIS.Utilities
{

	// public static class UnityPortSet
	// {
	// 	public static readonly int DISCOVERY = 7720;
	// 	public static readonly int HEARTBEAT = 7721;
	// 	public static readonly int SERVICE = 7730;
	// 	public static readonly int TOPIC = 7731;
	// }

	// public class NodeAddress
	// {
	// 	public string ip;
	// 	public int port;
	// 	public NodeAddress(string ip, int port)
	// 	{
	// 		this.ip = ip;
	// 		this.port = port;
	// 	}
	// }

	/// <summary>
	/// C# equivalent of the Python SocketInfo TypedDict.
	/// Represents individual socket details for topics or services.
	/// </summary>
	[MessagePackObject]
	public class SocketInfo
	{
		[Key("name")]
		public string Name { get; set; }

		[Key("ip")]
		public string Ip { get; set; }

		[Key("port")]
		public int Port { get; set; }
	}

	/// <summary>
	/// C# equivalent of the Python NodeInfo TypedDict.
	/// Contains global node identification and lists of available sockets.
	/// </summary>
	[MessagePackObject]
	public class NodeInfo
	{
		[Key("nodeID")]
		public string NodeID { get; set; }

		[Key("infoID")]
		public int InfoID { get; set; }

		[Key("name")]
		public string Name { get; set; }

		[Key("ip")]
		public string Ip { get; set; }

		[Key("topics")]
		public List<SocketInfo> Topics { get; set; }

		[Key("services")]
		public List<SocketInfo> Services { get; set; }
	}

	/// <summary>
	/// Lightweight heartbeat message compatible with pyzlc protocol.
	/// Structure (56+ bytes):
	///   - zlc_version: 3 x int32 (12 bytes) - major, minor, patch
	///   - node_id: 36 bytes string (UUID)
	///   - info_id: int32 (4 bytes)
	///   - service_port: int32 (4 bytes)
	///   - group_name: remaining bytes (variable)
	/// </summary>
	public class HeartbeatMessage
	{
		public int VersionMajor { get; set; }
		public int VersionMinor { get; set; }
		public int VersionPatch { get; set; }
		public string NodeId { get; set; }      // 36-char UUID
		public int InfoId { get; set; }
		public int ServicePort { get; set; }
		public string GroupName { get; set; }

		public HeartbeatMessage(string nodeId, int infoId, int servicePort, string groupName,
			int versionMajor = 2, int versionMinor = 0, int versionPatch = 1)
		{
			VersionMajor = versionMajor;
			VersionMinor = versionMinor;
			VersionPatch = versionPatch;
			NodeId = nodeId;
			InfoId = infoId;
			ServicePort = servicePort;
			GroupName = groupName;
		}

		/// <summary>
		/// Serialize to bytes using Big-Endian encoding to match pyzlc's struct.pack("!3i36s2i", ...)
		/// </summary>
		public byte[] ToBytes()
		{
			using (MemoryStream ms = new MemoryStream())
			{
				// Version: 3 x int32 Big-Endian (12 bytes)
				ms.Write(ToBigEndian(BitConverter.GetBytes(VersionMajor)), 0, 4);
				ms.Write(ToBigEndian(BitConverter.GetBytes(VersionMinor)), 0, 4);
				ms.Write(ToBigEndian(BitConverter.GetBytes(VersionPatch)), 0, 4);

				// NodeID: 36 bytes fixed string (pad or truncate to exactly 36 bytes)
				byte[] nodeIdBytes = new byte[36];
				byte[] nodeIdUtf8 = Encoding.UTF8.GetBytes(NodeId ?? "");
				Array.Copy(nodeIdUtf8, nodeIdBytes, Math.Min(nodeIdUtf8.Length, 36));
				ms.Write(nodeIdBytes, 0, 36);

				// InfoID: int32 Big-Endian (4 bytes)
				ms.Write(ToBigEndian(BitConverter.GetBytes(InfoId)), 0, 4);

				// ServicePort: int32 Big-Endian (4 bytes)
				ms.Write(ToBigEndian(BitConverter.GetBytes(ServicePort)), 0, 4);

				// GroupName: variable length UTF-8
				byte[] groupNameBytes = Encoding.UTF8.GetBytes(GroupName ?? "");
				ms.Write(groupNameBytes, 0, groupNameBytes.Length);

				return ms.ToArray();
			}
		}

		private static byte[] ToBigEndian(byte[] bytes)
		{
			if (BitConverter.IsLittleEndian)
			{
				Array.Reverse(bytes);
			}
			return bytes;
		}
	}


	public class LocalInfo
	{
		public NodeInfo nodeInfo;
		public string servicePort;

		public LocalInfo(string nodeName)
		{
			nodeInfo = new NodeInfo();
			nodeInfo.Topics = new List<SocketInfo>();
			nodeInfo.Services = new List<SocketInfo>();
			nodeInfo.NodeID = Guid.NewGuid().ToString();
			nodeInfo.InfoID = 0;
			nodeInfo.Name = nodeName;
			nodeInfo.Ip = NetworkUtils.GetLocalIPsInSameSubnet("127.0.0.1");
		}

		public void AddService(string serviceName, int port)
		{
			foreach (var service in nodeInfo.Services)
			{
				if (service.Name == serviceName)
				{
					throw new Exception($"Service {serviceName} already exists.");
				}
			}
			nodeInfo.Services.Add(new SocketInfo { Name = serviceName, Ip = nodeInfo.Ip, Port = port });
			GenerateNewNodeInfoID();
		}

		public void RemoveService(string serviceName)
		{
			if (nodeInfo.Services.Any(s => s.Name == serviceName))
			{
				nodeInfo.Services.RemoveAll(s => s.Name == serviceName);
			}
			GenerateNewNodeInfoID();
			Debug.LogWarning($"Service {serviceName} removed from LocalInfo.");
		}


		public void AddTopic(string topicName, int port)
		{
			foreach (var topic in nodeInfo.Topics)
			{
				if (topic.Name == topicName)
				{
					throw new Exception($"Topic {topicName} already exists.");
				}
			}
			nodeInfo.Topics.Add(new SocketInfo { Name = topicName, Ip = nodeInfo.Ip, Port = port });
			GenerateNewNodeInfoID();
		}

		public void RemoveTopic(string topicName)
		{
			if (nodeInfo.Topics.Any(t => t.Name == topicName))
			{
				nodeInfo.Topics.RemoveAll(t => t.Name == topicName);
			}
			GenerateNewNodeInfoID();
		}

		private void GenerateNewNodeInfoID()
		{
			nodeInfo.InfoID += 1;
		}

		public void Rename(string newName)
		{
			nodeInfo.Name = newName;
			GenerateNewNodeInfoID();
		}

	}

	public static class NodeInfoSerializer
	{
		// Big-Endian Helper: C# is Little-Endian by default
		private static byte[] ToBigEndian(byte[] data)
		{
			if (BitConverter.IsLittleEndian) Array.Reverse(data);
			return data;
		}

		public static byte[] EncodeNodeInfo(NodeInfo info)
		{
			using (MemoryStream ms = new MemoryStream())
			{
				// Note: We don't use BinaryWriter directly for ints to ensure Big-Endian

				// 1. NodeID (Fixed 36 bytes)
				byte[] nodeIdBytes = Encoding.UTF8.GetBytes(info.NodeID);
				ms.Write(nodeIdBytes, 0, 36);

				// 2. InfoID (u32, Big-Endian)
				ms.Write(ToBigEndian(BitConverter.GetBytes((uint)info.InfoID)), 0, 4);

				// 3. Name (String: u16 len + data)
				WriteString(ms, info.Name);

				// 4. IP (String: u16 len + data)
				WriteString(ms, info.Ip);

				// 5. Topics
				ms.Write(ToBigEndian(BitConverter.GetBytes((ushort)info.Topics.Count)), 0, 2);
				foreach (var topic in info.Topics)
				{
					WriteString(ms, topic.Name);
					ms.Write(ToBigEndian(BitConverter.GetBytes((ushort)topic.Port)), 0, 2);
				}

				// 6. Services
				ms.Write(ToBigEndian(BitConverter.GetBytes((ushort)info.Services.Count)), 0, 2);
				foreach (var service in info.Services)
				{
					WriteString(ms, service.Name);
					ms.Write(ToBigEndian(BitConverter.GetBytes((ushort)service.Port)), 0, 2);
				}

				return ms.ToArray();
			}
		}

		public static NodeInfo DecodeNodeInfo(byte[] data)
		{
			NodeInfo info = new NodeInfo();
			int pos = 0;

			// 1. NodeID (36 chars)
			info.NodeID = Encoding.UTF8.GetString(data, pos, 36);
			pos += 36;

			// 2. InfoID (u32)
			info.InfoID = (int)ReadU32(data, ref pos);

			// 3. Name
			info.Name = ReadString(data, ref pos);

			// 4. IP
			info.Ip = ReadString(data, ref pos);

			// 5. Topics
			ushort topicCount = ReadU16(data, ref pos);
			for (int i = 0; i < topicCount; i++)
			{
				string sName = ReadString(data, ref pos);
				ushort sPort = ReadU16(data, ref pos);
				info.Topics.Add(new SocketInfo { Name = sName, Ip = info.Ip, Port = sPort });
			}

			// 6. Services
			ushort serviceCount = ReadU16(data, ref pos);
			for (int i = 0; i < serviceCount; i++)
			{
				string sName = ReadString(data, ref pos);
				ushort sPort = ReadU16(data, ref pos);
				info.Services.Add(new SocketInfo { Name = sName, Ip = info.Ip, Port = sPort });
			}

			return info;
		}

		// --- Private Helpers to match Python BinWriter/BinReader ---

		private static void WriteString(Stream stream, string s)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(s);
			stream.Write(ToBigEndian(BitConverter.GetBytes((ushort)bytes.Length)), 0, 2);
			stream.Write(bytes, 0, bytes.Length);
		}

		private static string ReadString(byte[] data, ref int pos)
		{
			ushort len = ReadU16(data, ref pos);
			string s = Encoding.UTF8.GetString(data, pos, len);
			pos += len;
			return s;
		}

		private static ushort ReadU16(byte[] data, ref int pos)
		{
			byte[] buffer = { data[pos], data[pos + 1] };
			if (BitConverter.IsLittleEndian) Array.Reverse(buffer);
			pos += 2;
			return BitConverter.ToUInt16(buffer, 0);
		}

		private static uint ReadU32(byte[] data, ref int pos)
		{
			byte[] buffer = { data[pos], data[pos + 1], data[pos + 2], data[pos + 3] };
			if (BitConverter.IsLittleEndian) Array.Reverse(buffer);
			pos += 4;
			return BitConverter.ToUInt32(buffer, 0);
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