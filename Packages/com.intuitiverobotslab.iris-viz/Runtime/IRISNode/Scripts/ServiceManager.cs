using NetMQ;
using NetMQ.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using IRIS.Utilities;


namespace IRIS.Node
{

	using RawFrameHandler = Func<byte[][], byte[][]>;
	public interface IService
	{
		byte[][] BytesCallback(byte[][] bytes);
		void Close();
	}



	public class Service<RequestType, ResponseType> : IService
	{
		public string Name { get; set; }
		public readonly Func<RequestType, ResponseType> _onRequest;

		public Service(string serviceName, Func<RequestType, ResponseType> onRequest)
		{
			Name = serviceName;
			_onRequest = onRequest ?? throw new ArgumentNullException(nameof(onRequest));
		}

		public byte[][] BytesCallback(byte[][] bytes)
		{
			try
			{
				RequestType request = MsgUtils.Deserialize2Object<RequestType>(bytes[0]);
				ResponseType response = _onRequest(request);
				return new byte[][] { MsgUtils.Serialize2Bytes<ResponseType>(response) };
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"Error processing request for service {Name}: {ex.Message}\n{ex.StackTrace}");
				return new byte[][] { HandleErrorResponse(ex) };
			}
		}

		public void Close()
		{
			// No unmanaged resources to clean up in this implementation
		}

		private byte[] HandleErrorResponse(Exception ex)
		{
			string errorMessage = $"Error: {ex.Message}";
			return MsgUtils.Serialize2Bytes((string)(object)errorMessage);
		}
	}



	public class ServiceManager
	{
		private readonly Dictionary<string, IService> _serviceDict = new();
		private readonly Dictionary<string, RawFrameHandler> _serviceCallbacks = new();
		private readonly ResponseSocket _responseSocket;
		private int _port;
		private Task serviceTask;
		public ServiceManager(CancellationToken cancellationToken)
		{
			_responseSocket = new ResponseSocket();
			_responseSocket.Bind("tcp://0.0.0.0:0");
			_port = NetworkUtils.GetNetZMQSocketPort(_responseSocket);
			serviceTask = Task.Run(() => StartServiceTask(cancellationToken));
			Debug.Log($"Service initialized at port {_port}");
		}

		public void RegisterServiceCallback<RequestType, ResponseType>(string serviceName, Func<RequestType, ResponseType> callback)
		{
			if (string.IsNullOrEmpty(serviceName)) throw new ArgumentNullException(nameof(serviceName));
			if (callback == null) throw new ArgumentNullException(nameof(callback));
			_serviceDict[serviceName] = new Service<RequestType, ResponseType>(serviceName, callback);
			_serviceCallbacks[serviceName] = _serviceDict[serviceName].BytesCallback;
			IRISXRNode.Instance.localInfo.AddService(serviceName, _port);
			Debug.Log($"Service {serviceName} is registered");
		}

		public void UnregisterServiceCallback(string serviceName)
		{
			if (string.IsNullOrEmpty(serviceName)) return;
			if (!_serviceCallbacks.ContainsKey(serviceName)) return;
			IRISXRNode.Instance.localInfo.RemoveService(serviceName);
			_serviceCallbacks.Remove(serviceName);
			_serviceDict.Remove(serviceName);
			Debug.Log($"Service {serviceName} is unregistered");
		}

		public List<string> GetServiceList()
		{
			return _serviceCallbacks.Keys.ToList();
		}

		public void UnregisterAll()
		{
			foreach (var serviceName in _serviceCallbacks.Keys.ToList())
			{
				UnregisterServiceCallback(serviceName);
			}
		}	


		public void Close()
		{
			UnregisterAll();
            // Wait for service task completion
            try
            {
                serviceTask?.Wait();
                Debug.Log("Service task completed successfully.");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error waiting for service task completion: {e.Message}\n{e.StackTrace}");
            }
			_responseSocket.Close();
			Debug.Log("ServiceManager is closed");
		}

		public void StartServiceTask(CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				if (!_responseSocket.HasIn) continue;
				try
				{
					List<byte[]> messageReceived = _responseSocket.ReceiveMultipartBytes();
					if (messageReceived.Count > 1)
					{
						string serviceName = MsgUtils.Bytes2String(messageReceived[0]);
						if (_serviceCallbacks.ContainsKey(serviceName))
						{
							byte[][] response = _serviceCallbacks[serviceName](messageReceived.Skip(1).ToArray());
							_responseSocket.SendMultipartBytes(response);
						}
						else
						{
							Debug.LogWarning($"Service {serviceName} not found");
							_responseSocket.SendFrame(IRISMSG.NOTFOUND);
						}
					}
					else
					{
						Debug.LogWarning("Received message does not contain service name or request data");
						_responseSocket.SendFrame(IRISMSG.ERROR);
					}
				}
				catch (TimeoutException)
				{
					continue;
				}
				catch (OperationCanceledException)
				{
					Debug.Log("Service task was cancelled");
					return;
				}
				catch (Exception ex)
				{
					Debug.LogError($"Error receiving service request: {ex.Message}\n{ex.StackTrace}");
					_responseSocket.SendFrame(IRISMSG.ERROR);
				}
			}
		}
	}


}
