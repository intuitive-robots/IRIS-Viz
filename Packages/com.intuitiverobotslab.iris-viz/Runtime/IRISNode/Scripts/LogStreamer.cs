using UnityEngine;
using IRIS.Utilities;

namespace IRIS.Node
{

    public class LogStreamer : MonoBehaviour
    {
        private Publisher<string> _logPublisher;
        private IRISService<string, string> toggleConsoleLoggerService;
        // private Publisher<string> _fpsPublisher;
        // private IRISService<string, string> toggleFPSLoggerService;
        private bool isLogStreamerEnabled = true;

        void Start()
        {
            _logPublisher = new Publisher<string>("ConsoleLogger");
            Application.logMessageReceived += HandleLog;
            toggleConsoleLoggerService = new IRISService<string, string>("ToggleConsoleLogger", ToggleLogStreamerService);
            // _fpsPublisher = new Publisher<string>("FPS");
            // timer = Time.realtimeSinceStartup;
        }

        // private int frameCounter = 0;
        // private float timer = 0;

        void HandleLog(string logString, string stackTrace, LogType type)
        {
            _logPublisher.Publish(logString);
        }

        private void OnApplicationQuit()
        {
            Application.logMessageReceived -= HandleLog;
        }

        public string ToggleLogStreamerService(string req)
        {
            if (isLogStreamerEnabled)
            {
                Application.logMessageReceived -= HandleLog;
            }
            else
            {
                Application.logMessageReceived += HandleLog;
            }
            return isLogStreamerEnabled ? IRISMSG.STOP : IRISMSG.START;
        }


        void Update()
        {
            // TODO: finish the fps logger
            // frameCounter += 1;
            // float totalTime = Time.realtimeSinceStartup - timer;
            // if (totalTime > 5.0f)
            // {
            //     float fps = frameCounter / totalTime;
            //     HandleLog("Average FPS in the last 5s: " + fps, null, LogType.Log);
            //     timer = Time.realtimeSinceStartup;
            //     frameCounter = 0;
            // }
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= HandleLog;
            toggleConsoleLoggerService?.Unregister();
        }


    }
}