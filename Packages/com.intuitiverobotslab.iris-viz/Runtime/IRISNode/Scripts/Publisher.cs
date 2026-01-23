using NetMQ;
using NetMQ.Sockets;
using System;
using UnityEngine;
using IRIS.Utilities;

namespace IRIS.Node
{

	public class Publisher<MsgType>
	{
		protected PublisherSocket _pubSocket;
		protected string _topic;
		public int Port { get; private set; }

		public Publisher(string topic, int port = 0)
		{
			_topic = topic;
			_pubSocket = new PublisherSocket();
			_pubSocket.Bind($"tcp://0.0.0.0:{port}");
			Port = NetworkUtils.GetNetZMQSocketPort(_pubSocket);
			IRISXRNode.Instance.localInfo.AddTopic(_topic, Port);
			IRISXRNode.Instance.sockets[_topic] = _pubSocket;
			Debug.Log($"Publisher for topic {_topic} is created");
		}

		public void Publish(MsgType data)
		{
			TryPublish(MsgUtils.Serialize2Bytes<MsgType>(data));
		}

		private void TryPublish(byte[] msg)
		{
			try
			{
				_pubSocket.SendFrame(msg);
			}
			catch (TerminatingException ex)
			{
				Debug.LogWarning($"Publish failed: NetMQ context terminated. Error: {ex.Message}");
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"Publish failed: Unexpected error occurred. Error: {ex.Message}");
			}
		}

		public void Close()
		{
			IRISXRNode.Instance.localInfo.RemoveTopic(_topic);
			IRISXRNode.Instance.sockets.Remove(_topic);
			_pubSocket.Close();
			Debug.Log($"Publisher for topic {_topic} is closed");
		}


	}
}
