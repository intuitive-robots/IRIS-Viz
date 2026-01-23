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

    public static class ResponseStatus
    {
        public const string SUCCESS = "SUCCESS";
        public const string NOSERVICE = "NOSERVICE";
        public const string INVALID_RESPONSE = "INVALID_RESPONSE";
        public const string SERVICE_FAIL = "SERVICE_FAIL";
        public const string SERVICE_TIMEOUT = "SERVICE_TIMEOUT";
        public const string INVALID_REQUEST = "INVALID_REQUEST";
        public const string UNKNOWN_ERROR = "UNKNOWN_ERROR";
    }

	public static class MsgUtils
	{
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