using System;
using System.Collections.Generic;
using UnityEngine;

using NetMQ;
using NetMQ.Sockets;
using IRIS.Utilities;

namespace IRIS.Node
{

	public interface ISubscriber
	{
		void Close();
	}


	class Subscriber<MsgType> : ISubscriber
	{
		public string _topic;
		public string _url;
		public SubscriberSocket _subSocket;
		public Action<MsgType> _receiveAction;

		public Subscriber(string topic, Action<MsgType> receiveAction, string url)
		{
			_topic = topic;
			_url = url;
			_receiveAction = receiveAction;
			_subSocket = new SubscriberSocket();
			_subSocket.Subscribe("");
			_subSocket.Connect(_url);
			IRISXRNode.Instance.SubscriptionSpin += SubscriptionSpinTask;
			IRISXRNode.Instance.sockets[_topic] = _subSocket;
			Debug.Log($"Subscriber for topic {_topic} is created and connected to {_url}");
		}

		public void OnReceive(byte[] byteMessage)
		{
			try
			{
				MsgType msg = MsgUtils.Deserialize2Object<MsgType>(byteMessage);
				_receiveAction(msg);
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"Error processing message for topic {_topic}: {ex.Message}");
			}
		}

		public void SubscriptionSpinTask()
		{
			if (_subSocket.TryReceiveFrameBytes(out byte[] latestMessage))
			{
				OnReceive(latestMessage);
			}
		}


		public void Close()
		{
			_subSocket.Close();
			IRISXRNode.Instance.SubscriptionSpin -= SubscriptionSpinTask;
			IRISXRNode.Instance.sockets.Remove(_topic);
		}

	}


	public class SubscriberManager
	{

		private Dictionary<string, ISubscriber> _subscribers = new Dictionary<string, ISubscriber>();


		public void RegisterSubscriptionCallback<MsgType>(string topic, Action<MsgType> receiveAction, string url)
		{
			_subscribers.Add(topic, new Subscriber<MsgType>(topic, receiveAction, url));
		}

		public void Unsubscribe(string topic)
		{
			if (_subscribers.ContainsKey(topic))
			{
				_subscribers[topic].Close();
				_subscribers.Remove(topic);
				Debug.LogWarning($"Unsubscribed from topic {topic}");
			}
		}

		public void Close()
		{
			foreach (var subscriber in _subscribers.Values)
			{
				subscriber.Close();
			}
			_subscribers.Clear();
		}

	}
}
