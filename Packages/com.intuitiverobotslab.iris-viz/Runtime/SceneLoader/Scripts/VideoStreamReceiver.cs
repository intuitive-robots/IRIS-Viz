

using UnityEngine;
using UnityEngine.UI;
using IRIS.Node;
using MessagePack;

namespace IRIS.SceneLoader
{

    [MessagePackObject(keyAsPropertyName: true)]
    public class VideoFrame
    {
        public int width { get; set; }
        public int height { get; set; }
        public byte[] image { get; set; }
        public double timestamp { get; set; }
    }


    public class VideoStreamReceiver : MonoBehaviour
    {
        public RawImage rawImage;
        private Texture2D texture;
        private byte[] latestImageBytes;
        private readonly object frameLock = new object();

        private void Start() {
            texture = new Texture2D(2, 2);
            rawImage.texture = texture;
        }
        public void StartSubscription(VideoStreamConfig config)
        {
            IRISXRNode.Instance.SubscriberManager.RegisterSubscriptionCallback<VideoFrame>(config.name, OnFrameReceived, config.url);
            rawImage.rectTransform.sizeDelta = new Vector2(config.width, config.height);
        }

        private void Update()
        {
            byte[] imageBytes = null;
            lock (frameLock)
            {
                if (latestImageBytes != null)
                {
                    imageBytes = latestImageBytes;
                    latestImageBytes = null;
                }
            }
            if (imageBytes == null) return;
            if (texture.LoadImage(imageBytes, false))
            {
                rawImage.texture = texture;
            }
        }

        private void OnFrameReceived(VideoFrame videoFrame)
        {
            lock (frameLock)
            {
                latestImageBytes = videoFrame.image;
            }
        }
    }
}