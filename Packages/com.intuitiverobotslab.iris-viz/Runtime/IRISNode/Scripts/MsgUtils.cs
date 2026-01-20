using System;
using System.IO;
using System.Text;
using MessagePack;

namespace IRIS.Utilities
{

	public static class EchoHeader
	{
		public static readonly byte[] PING = new byte[] { 0x00 };
		public static readonly byte[] HEARTBEAT = new byte[] { 0x01 };
		public static readonly byte[] NODES = new byte[] { 0x02 };
	}

	public static class IRISMSG
	{
		public static readonly string EMPTY = "EMPTY";
		public static readonly string SUCCESS = "SUCCESS";
		public static readonly string ERROR = "ERROR";
		public static readonly string TIMEOUT = "TIMEOUT";
		public static readonly string NOTFOUND = "NOTFOUND";
		public static readonly string START = "START";
		public static readonly string STOP = "STOP";
	}

	public static class MsgUtils
	{

		public const string SEPARATOR = "|";

		public static byte[][] SplitByte(byte[] bytesMsg)
		{
			int separatorIndex = Array.IndexOf(bytesMsg, Encoding.UTF8.GetBytes(SEPARATOR)[0]);
			if (separatorIndex == -1)
			{
				return new byte[][] { bytesMsg, null };
			}
			byte[] part1 = bytesMsg[..separatorIndex];
			byte[] part2 = bytesMsg[(separatorIndex + 1)..];
			return new byte[][] { part1, part2 };
		}

		public static string[] SplitByteToStr(byte[] bytesMsg)
		{
			byte[][] parts = SplitByte(bytesMsg);
			return new string[] { Encoding.UTF8.GetString(parts[0]), Encoding.UTF8.GetString(parts[1]) };
		}

		public static byte[] ConcatenateByteArrays(params byte[][] arrays)
		{
			using (MemoryStream ms = new MemoryStream())
			{
				foreach (byte[] array in arrays)
				{
					if (array != null)
					{
						ms.Write(array, 0, array.Length);
					}
				}
				return ms.ToArray();
			}
		}


		// public static byte[] GenerateHeartbeat(NodeInfo nodeInfo)
		// {
		// 	byte[] serializedLocalInfo = Serialize2Bytes(nodeInfo);
		// 	return ConcatenateByteArrays(EchoHeader.HEARTBEAT, serializedLocalInfo);
		// }

		
		public static string Bytes2String(byte[] byteMessage)
		{
			return Encoding.UTF8.GetString(byteMessage);
		}

		public static byte[] String2Bytes(string message)
		{
			return Encoding.UTF8.GetBytes(message);
		}


		public static byte[] Serialize2Bytes<ObjectType>(ObjectType message)
		{
			return MessagePackSerializer.Serialize<ObjectType>(message);
		}

		public static ObjectType Deserialize2Object<ObjectType>(byte[] byteMessage)
		{	
			return MessagePackSerializer.Deserialize<ObjectType>(byteMessage);
		}

	}
}