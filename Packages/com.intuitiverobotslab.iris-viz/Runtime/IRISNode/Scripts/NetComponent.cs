using NetMQ;
using NetMQ.Sockets;
using System;
using Newtonsoft.Json;
using UnityEngine;
using IRIS.Utilities;


namespace IRIS.Node
{

	public interface INetComponent
	{
		string Name { get; set; }
		// bool IsActive { get; }

	}

	public class Publisher<MsgType>
	{
		protected PublisherSocket _pubSocket;
		protected string _topic;

		public Publisher(string topic, bool globalNameSpace = false)
		{
			IRISXRNode _XRNode = IRISXRNode.Instance;
			if (globalNameSpace)
			{
				_topic = topic;
			}
			else
			{
				_topic = $"{_XRNode.localInfo.name}/{topic}";
			}
			_pubSocket = _XRNode._pubSocket;
			if (!_XRNode.localInfo.topicList.Contains(_topic))
			{
				_XRNode.localInfo.topicList.Add(_topic);
			}
			Debug.Log($"Publisher for topic {_topic} is created");
		}

		public void Publish(string data)
		{
			// Combine topic and message
			string msg = MsgUtils.CombineHeaderWithMessage(_topic, data);
			// Send the message
			TryPublish(MsgUtils.String2Bytes(msg));
		}

		public void Publish(byte[] data)
		{
			TryPublish(MsgUtils.CombineHeaderWithMessage(_topic, data));
		}

		public void Publish(MsgType data)
		{
			string msg = MsgUtils.CombineHeaderWithMessage(_topic, JsonConvert.SerializeObject(data));
			TryPublish(MsgUtils.String2Bytes(msg));
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

	}

	public class Subscriber<MsgType>
	{
		protected string _topic;
		protected SubscriberSocket _subSocket;
		private Action<MsgType> _receiveAction;
		private Func<byte[], MsgType> onProcessMsg;

		public Subscriber(string topic, Action<MsgType> receiveAction)
		{
			_topic = topic;
			_receiveAction = receiveAction;
			_subSocket = new SubscriberSocket();
		}

		public void StartSubscription(string url, string topicName = null)
		{
			_subSocket.Connect(url);
			if (topicName == null)
			{
				topicName = _topic;
			}
			_subSocket.Subscribe("");
			IRISXRNode _XRNode = IRISXRNode.Instance;
			onProcessMsg = MsgUtils.CreateDecoderProcessor<MsgType>();
			_XRNode.SubscriptionSpin += SubscriptionSpin;
			Debug.Log($"Subscribed to topic {_topic} at {url}");
		}

		public void OnReceive(byte[] byteMessage)
		{
			try
			{
				MsgType msg = onProcessMsg(byteMessage);
				_receiveAction(msg);
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"Error processing message for topic {_topic}: {ex.Message}");
			}
		}

		public void Unsubscribe()
		{
			IRISXRNode _XRNode = IRISXRNode.Instance;
			_XRNode.SubscriptionSpin -= SubscriptionSpin;
		}

		public void SubscriptionSpin()
		{
			byte[] latestMessage = null;
			
			// Keep reading until no more messages are available
			while (_subSocket.TryReceiveFrameBytes(TimeSpan.Zero, out byte[] message))
			{
				latestMessage = message;
			}
			
			// Process only the latest message if any were received
			if (latestMessage != null)
			{
				OnReceive(latestMessage);
			}
		}
	}

	// Service class: Since it is running in the main thread, 
	// so we don't need to destroy it

	public abstract class IRISService : INetComponent
	{
		public string Name { get; set; }

		public IRISService(string serviceName, bool globalNameSpace = false)
		{
			IRISXRNode netManager = IRISXRNode.Instance;
			string hostName = netManager.localInfo.name;
			Name = globalNameSpace ? serviceName : $"{hostName}/{serviceName}";
			if (netManager.localInfo.serviceList.Contains(Name))
			{
				throw new ArgumentException($"Service {Name} is already registered");
			}
			netManager.localInfo.serviceList.Add(Name);
			netManager.serviceCallbacks[Name] = BytesCallback;
			Debug.Log($"Service {Name} is registered");
		}

		public virtual byte[][] BytesCallback(byte[][] bytes)
		{
			throw new NotImplementedException("BytesCallback must be implemented in derived class");
		}


		public void Unregister()
		{
			IRISXRNode netManager = IRISXRNode.Instance;

			if (netManager.localInfo.serviceList.Contains(Name))
			{
				netManager.localInfo.serviceList.Remove(Name);
				netManager.serviceCallbacks.Remove(Name);
				Debug.Log($"Service {Name} is unregistered");
			}
			else
			{
				Debug.LogWarning($"Service {Name} is not registered");
			}
		}



		public byte[] HandleErrorResponse(Exception ex)
		{
			string errorMessage = $"Error: {ex.Message}";
			return MsgUtils.ObjectSerialize2Bytes((string)(object)errorMessage);
		}

	}


	public class IRISService<RequestType, ResponseType> : IRISService
	{
		public readonly Func<RequestType, ResponseType> _onRequest;
		public Func<byte[], RequestType> ProcessRequestFunc;
		public Func<ResponseType, byte[]> ProcessResponseFunc;

		public IRISService(string serviceName, Func<RequestType, ResponseType> onRequest, bool globalNameSpace = false)
			: base(serviceName, globalNameSpace) // Call base constructor with parameters
		{
			_onRequest = onRequest ?? throw new ArgumentNullException(nameof(onRequest));

			// Use helper methods
			ProcessRequestFunc = MsgUtils.CreateDecoderProcessor<RequestType>();
			ProcessResponseFunc = MsgUtils.CreateEncoderProcessor<ResponseType>();
		}

		public override byte[][] BytesCallback(byte[][] bytes)
		{
			try
			{
				RequestType request = ProcessRequestFunc(bytes[0]);
				ResponseType response = _onRequest(request);
				return new byte[][] { ProcessResponseFunc(response) };
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"Error processing request for service {Name}: {ex.Message}");
				return new byte[][] { HandleErrorResponse(ex) };
			}
		}

	}


	public class IRISService<RequestType1, RequestType2, ResponseType> : IRISService
	{
		public readonly Func<RequestType1, RequestType2, ResponseType> _onRequest;
		public Func<byte[], RequestType1> ProcessRequest1Func;
		public Func<byte[], RequestType2> ProcessRequest2Func;
		public Func<ResponseType, byte[]> ProcessResponseFunc;

		public IRISService(string serviceName, Func<RequestType1, RequestType2, ResponseType> onRequest, bool globalNameSpace = false)
			: base(serviceName, globalNameSpace)
		{
			_onRequest = onRequest ?? throw new ArgumentNullException(nameof(onRequest));

			// Use helper methods
			ProcessRequest1Func = MsgUtils.CreateDecoderProcessor<RequestType1>();
			ProcessRequest2Func = MsgUtils.CreateDecoderProcessor<RequestType2>();
			ProcessResponseFunc = MsgUtils.CreateEncoderProcessor<ResponseType>();
		}

		public override byte[][] BytesCallback(byte[][] bytes)
		{
			try
			{
				// Expecting exactly 2 byte arrays for the two request types
				if (bytes.Length != 2)
				{
					throw new ArgumentException("Expected 2 byte arrays for RequestType1 and RequestType2");
				}

				RequestType1 request1 = ProcessRequest1Func(bytes[0]);
				RequestType2 request2 = ProcessRequest2Func(bytes[1]);
				ResponseType response = _onRequest(request1, request2);
				return new byte[][] { ProcessResponseFunc(response) };
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"Error processing request for service {Name}: {ex.Message}");
				return new byte[][] { HandleErrorResponse(ex) };
			}
		}
	}


	public class IRISService<RequestType1, RequestType2, RequestType3, ResponseType> : IRISService
	{
		public readonly Func<RequestType1, RequestType2, RequestType3, ResponseType> _onRequest;
		public Func<byte[], RequestType1> ProcessRequest1Func;
		public Func<byte[], RequestType2> ProcessRequest2Func;
		public Func<byte[], RequestType3> ProcessRequest3Func;
		public Func<ResponseType, byte[]> ProcessResponseFunc;

		public IRISService(string serviceName, Func<RequestType1, RequestType2, RequestType3, ResponseType> onRequest, bool globalNameSpace = false)
			: base(serviceName, globalNameSpace)
		{
			_onRequest = onRequest ?? throw new ArgumentNullException(nameof(onRequest));

			// Use helper methods
			ProcessRequest1Func = MsgUtils.CreateDecoderProcessor<RequestType1>();
			ProcessRequest2Func = MsgUtils.CreateDecoderProcessor<RequestType2>();
			ProcessRequest3Func = MsgUtils.CreateDecoderProcessor<RequestType3>();
			ProcessResponseFunc = MsgUtils.CreateEncoderProcessor<ResponseType>();
		}

		public override byte[][] BytesCallback(byte[][] bytes)
		{
			try
			{
				// Expecting at least 3 byte arrays for the three request types
				if (bytes.Length != 3)
				{
					throw new ArgumentException("Expected 3 byte arrays for RequestType1, RequestType2, and RequestType3");
				}
				RequestType1 request1 = ProcessRequest1Func(bytes[0]);
				RequestType2 request2 = ProcessRequest2Func(bytes[1]);
				RequestType3 request3 = ProcessRequest3Func(bytes[2]);
				ResponseType response = _onRequest(request1, request2, request3);
				return new byte[][] { ProcessResponseFunc(response) };
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"Error processing request for service {Name}: {ex.Message}");
				return new byte[][] { HandleErrorResponse(ex) };
			}
		}

	}

	public class IRISService<RequestType1, RequestType2, RequestType3, RequestType4, ResponseType> : IRISService
	{
		public readonly Func<RequestType1, RequestType2, RequestType3, RequestType4, ResponseType> _onRequest;
		public Func<byte[], RequestType1> ProcessRequest1Func;
		public Func<byte[], RequestType2> ProcessRequest2Func;
		public Func<byte[], RequestType3> ProcessRequest3Func;
		public Func<byte[], RequestType4> ProcessRequest4Func;
		public Func<ResponseType, byte[]> ProcessResponseFunc;

		public IRISService(string serviceName, Func<RequestType1, RequestType2, RequestType3, RequestType4, ResponseType> onRequest, bool globalNameSpace = false)
			: base(serviceName, globalNameSpace)
		{
			_onRequest = onRequest ?? throw new ArgumentNullException(nameof(onRequest));

			// Use helper methods
			ProcessRequest1Func = MsgUtils.CreateDecoderProcessor<RequestType1>();
			ProcessRequest2Func = MsgUtils.CreateDecoderProcessor<RequestType2>();
			ProcessRequest3Func = MsgUtils.CreateDecoderProcessor<RequestType3>();
			ProcessRequest4Func = MsgUtils.CreateDecoderProcessor<RequestType4>();
			ProcessResponseFunc = MsgUtils.CreateEncoderProcessor<ResponseType>();
		}

		public override byte[][] BytesCallback(byte[][] bytes)
		{
			try
			{
				// Expecting exactly 4 byte arrays for the four request types
				if (bytes.Length != 4)
				{
					throw new ArgumentException("Expected 4 byte arrays for RequestType1, RequestType2, RequestType3, and RequestType4");
				}

				RequestType1 request1 = ProcessRequest1Func(bytes[0]);
				RequestType2 request2 = ProcessRequest2Func(bytes[1]);
				RequestType3 request3 = ProcessRequest3Func(bytes[2]);
				RequestType4 request4 = ProcessRequest4Func(bytes[3]);
				ResponseType response = _onRequest(request1, request2, request3, request4);
				return new byte[][] { ProcessResponseFunc(response) };
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"Error processing request for service {Name}: {ex.Message}");
				return new byte[][] { HandleErrorResponse(ex) };
			}
		}
	}

	public class IRISService<RequestType1, RequestType2, RequestType3, RequestType4, RequestType5, ResponseType> : IRISService
	{
		public readonly Func<RequestType1, RequestType2, RequestType3, RequestType4, RequestType5, ResponseType> _onRequest;
		public Func<byte[], RequestType1> ProcessRequest1Func;
		public Func<byte[], RequestType2> ProcessRequest2Func;
		public Func<byte[], RequestType3> ProcessRequest3Func;
		public Func<byte[], RequestType4> ProcessRequest4Func;
		public Func<byte[], RequestType5> ProcessRequest5Func;
		public Func<ResponseType, byte[]> ProcessResponseFunc;

		public IRISService(string serviceName, Func<RequestType1, RequestType2, RequestType3, RequestType4, RequestType5, ResponseType> onRequest, bool globalNameSpace = false)
			: base(serviceName, globalNameSpace)
		{
			_onRequest = onRequest ?? throw new ArgumentNullException(nameof(onRequest));
			
			// Use helper methods
			ProcessRequest1Func = MsgUtils.CreateDecoderProcessor<RequestType1>();
			ProcessRequest2Func = MsgUtils.CreateDecoderProcessor<RequestType2>();
			ProcessRequest3Func = MsgUtils.CreateDecoderProcessor<RequestType3>();
			ProcessRequest4Func = MsgUtils.CreateDecoderProcessor<RequestType4>();
			ProcessRequest5Func = MsgUtils.CreateDecoderProcessor<RequestType5>();
			ProcessResponseFunc = MsgUtils.CreateEncoderProcessor<ResponseType>();
		}

		public override byte[][] BytesCallback(byte[][] bytes)
		{
			try
			{
				// Expecting exactly 5 byte arrays for the five request types
				if (bytes.Length != 5)
				{
					throw new ArgumentException("Expected 5 byte arrays for RequestType1, RequestType2, RequestType3, RequestType4, and RequestType5");
				}
				
				RequestType1 request1 = ProcessRequest1Func(bytes[0]);
				RequestType2 request2 = ProcessRequest2Func(bytes[1]);
				RequestType3 request3 = ProcessRequest3Func(bytes[2]);
				RequestType4 request4 = ProcessRequest4Func(bytes[3]);
				RequestType5 request5 = ProcessRequest5Func(bytes[4]);
				ResponseType response = _onRequest(request1, request2, request3, request4, request5);
				return new byte[][] { ProcessResponseFunc(response) };
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"Error processing request for service {Name}: {ex.Message}");
				return new byte[][] { HandleErrorResponse(ex) };
			}
		}
	}

}