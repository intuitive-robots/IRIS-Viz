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

	using RawFrameHandler = Func<byte[], byte[]>;
	public interface IService
	{
		byte[] BytesCallback(byte[] bytes);
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

		public byte[] BytesCallback(byte[] bytes)
		{
			try
			{
				RequestType request = MsgUtils.Deserialize2Object<RequestType>(bytes);
				ResponseType response = _onRequest(request);
				return MsgUtils.Serialize2Bytes<ResponseType>(response);
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"Error processing request for service {Name}: {ex.Message}\n{ex.StackTrace}");
				return MsgUtils.String2Bytes(ResponseStatus.SERVICE_FAIL);
			}
		}

		public void Close()
		{
			// No unmanaged resources to clean up in this implementation
		}

		private byte[] HandleErrorResponse(Exception ex)
		{
			string errorMessage = $"Error: {ex.Message}";
			return MsgUtils.String2Bytes(errorMessage);
		}
	}



	public class ServiceManager
	{
		private readonly Dictionary<string, IService> _serviceDict = new();
		private readonly Dictionary<string, RawFrameHandler> _serviceCallbacks = new();
		private readonly ResponseSocket _responseSocket;
		public int port { get; private set; }
		private Task serviceTask;
		public ServiceManager(CancellationToken cancellationToken)
		{
			_responseSocket = new ResponseSocket();
			_responseSocket.Bind("tcp://0.0.0.0:0");
			port = NetworkUtils.GetNetZMQSocketPort(_responseSocket);
			serviceTask = Task.Run(() => StartServiceTask(cancellationToken));
			Debug.Log($"Service initialized at port {port}");
		}

		public void RegisterServiceCallback<RequestType, ResponseType>(string serviceName, Func<RequestType, ResponseType> callback, bool useNameSpace = true)
		{
			if (useNameSpace)
			{
				serviceName = $"{IRISXRNode.Instance.localInfo.nodeInfo.Name}/{serviceName}";
			}
			_serviceDict[serviceName] = new Service<RequestType, ResponseType>(serviceName, callback);
			_serviceCallbacks[serviceName] = _serviceDict[serviceName].BytesCallback;
			IRISXRNode.Instance.localInfo.AddService(serviceName, port);
			Debug.Log($"Service {serviceName} is registered");
		}

		public void UnregisterServiceCallback(string serviceName)
		{
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
				byte[][] response = new byte[2][];
				try
				{
					List<byte[]> messageReceived = _responseSocket.ReceiveMultipartBytes();
					if (messageReceived.Count == 2)
					{
						string serviceName = MsgUtils.Bytes2String(messageReceived[0]);
						if (_serviceCallbacks.ContainsKey(serviceName))
						{
							response[0] = MsgUtils.String2Bytes(ResponseStatus.SUCCESS);
							response[1] = _serviceCallbacks[serviceName](messageReceived[1]);
						}
						else
						{
							Debug.LogWarning($"Service {serviceName} not found");
							response[0] = MsgUtils.String2Bytes(ResponseStatus.NOSERVICE);
							response[1] = MsgUtils.String2Bytes($"Service {serviceName} not found");
						}
						
					}
					else
					{
						Debug.LogWarning("Received message does not contain service name or request data");
						response[0] = MsgUtils.String2Bytes(ResponseStatus.INVALID_REQUEST);
						response[1] = MsgUtils.String2Bytes("Invalid request format");
					}
					_responseSocket.SendMultipartBytes(response);
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
					response[0] = MsgUtils.String2Bytes(ResponseStatus.UNKNOWN_ERROR);
					response[1] = MsgUtils.String2Bytes("Unknown error occurred");
				}
				finally
				{
					// Ensure the response is sent even in case of exceptions
					_responseSocket.SendMultipartBytes(response);
				}
			}
		}
	}


}
