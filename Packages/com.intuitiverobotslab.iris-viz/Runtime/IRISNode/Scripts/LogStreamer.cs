using UnityEngine;
using System.Collections.Generic;
using IRIS.Utilities;

namespace IRIS.Node
{
    /// <summary>
    /// LogStreamer: Captures Unity logs and system metrics (FPS) 
    /// and publishes them via IRIS (ZMQ) infrastructure.
    /// </summary>
    public class LogStreamer : MonoBehaviour
    {
        private Publisher<string> _logPublisher;
        private IRISService<string, string> _toggleService;

        [Header("Configuration")]
        [SerializeField] private float testLogInterval = 1.0f;
        [SerializeField] private float fpsUpdateInterval = 5.0f;

        private bool _isLogEnabled = true;
        private float _nextTestLogTime;
        private float _nextFpsLogTime;
        
        // FPS Tracking Variables
        private int _frameCount = 0;
        private float _fpsTimer = 0f;

        void Start()
        {
            // Initialize Publisher with specific topic
            _logPublisher = new Publisher<string>("ConsoleLogger");

            // Subscribe to Unity's log event
            Application.logMessageReceived += HandleLog;

            // Register service to allow remote control of logging
            _toggleService = new IRISService<string, string>("ToggleConsoleLogger", OnToggleServiceRequest);

            // Initialize timers using real-time to be independent of Time.timeScale
            _nextTestLogTime = Time.realtimeSinceStartup + testLogInterval;
            _nextFpsLogTime = Time.realtimeSinceStartup + fpsUpdateInterval;
        }

        void Update()
        {
            TrackMetrics();
            HandleScheduledLogs();
        }

        /// <summary>
        /// High-performance metric tracking. 
        /// Running in Update ensures we count every rendered frame.
        /// </summary>
        private void TrackMetrics()
        {
            _frameCount++;
            _fpsTimer += Time.unscaledDeltaTime;
        }

        /// <summary>
        /// Replaces InvokeRepeating with a manual timer in Update.
        /// This avoids Reflection overhead and provides better control.
        /// </summary>
        private void HandleScheduledLogs()
        {
            float currentTime = Time.realtimeSinceStartup;

            // 1. Handle Test/Heartbeat Logs
            if (currentTime >= _nextTestLogTime)
            {
                _logPublisher.Publish($"[Heartbeat] Total frames since start: {_frameCount}");
                _nextTestLogTime = currentTime + testLogInterval;
            }

            // 2. Handle FPS Reporting
            if (currentTime >= _nextFpsLogTime)
            {
                float fps = _frameCount / _fpsTimer;
                // Using string interpolation (optimized in modern C#)
                string statsMsg = $"[Metrics] Average FPS: {fps:F2} over last {_fpsTimer:F1}s";
                _logPublisher.Publish(statsMsg);

                // Reset metrics for next interval
                _frameCount = 0;
                _fpsTimer = 0f;
                _nextFpsLogTime = currentTime + fpsUpdateInterval;
            }
        }

        /// <summary>
        /// Callback for Unity's logMessageReceived event.
        /// Note: This can be called from background threads if using Threaded Logs.
        /// </summary>
        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            if (!_isLogEnabled) return;

            // Format: [Type] Message
            // In a production environment, consider filtering LogType.Log to avoid flooding
            _logPublisher.Publish($"[{type}] {logString}");
        }

        /// <summary>
        /// IRIS Service handler to enable/disable logging at runtime.
        /// </summary>
        public string OnToggleServiceRequest(string req)
        {
            _isLogEnabled = !_isLogEnabled;
            
            // Clean up or re-subscribe based on state
            if (_isLogEnabled)
                Application.logMessageReceived += HandleLog;
            else
                Application.logMessageReceived -= HandleLog;

            Debug.Log($"LogStreamer state changed: {_isLogEnabled}");
            return _isLogEnabled ? "LOGGING_STARTED" : "LOGGING_STOPPED";
        }

        private void OnDestroy()
        {
            // Crucial: Unsubscribe to prevent memory leaks and NullReferenceExceptions
            Application.logMessageReceived -= HandleLog;
            _toggleService?.Unregister();
            
            // Note: Publisher cleanup should be handled within its own Dispose logic
        }

        private void OnApplicationQuit()
        {
            _isLogEnabled = false;
        }
    }
}