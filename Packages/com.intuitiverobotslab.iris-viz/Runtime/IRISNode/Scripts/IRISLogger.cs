using UnityEngine;

namespace IRIS.Utilities
{

	public static class IRISLogger
	{
		public static void Log(object message)
		{
			Debug.Log(message);
		}

		public static void LogWarning(object message)
		{
			Debug.LogWarning(message);
		}

		public static void LogError(object message)
		{
			Debug.LogError(message);
		}

		public static void LogFormat(string format, params object[] args)
		{
			Debug.LogFormat(format, args);
		}
	}
}
