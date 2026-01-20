using System.Diagnostics;
using UnityEngine;

namespace IRIS.Utilities
{

	public static class IRISLogger
	{
		[Conditional("UNITY_EDITOR")]
		[Conditional("DEVELOPMENT_BUILD")]
		public static void Log(object message)
		{
			UnityEngine.Debug.Log(message);
		}

		[Conditional("UNITY_EDITOR")]
		[Conditional("DEVELOPMENT_BUILD")]
		public static void LogWarning(object message)
		{
			UnityEngine.Debug.LogWarning(message);
		}

		public static void LogError(object message)
		{
			UnityEngine.Debug.LogError(message);
		}

		[Conditional("UNITY_EDITOR")]
		[Conditional("DEVELOPMENT_BUILD")]
		public static void LogFormat(string format, params object[] args)
		{
			UnityEngine.Debug.LogFormat(format, args);
		}
	}
}
