using UnityEngine;
using MessagePack;
using IRIS.Node;
using IRIS.Utilities;



namespace IRIS.SceneLoader
{

    [MessagePackObject(keyAsPropertyName: true)]
    public class VideoStreamConfig
    {
        public string name { get; set; }
        public string url { get; set; }
        public int width { get; set; }
        public int height { get; set; }
    }

    public class VideoStreamSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject videoReceiverPrefab;
        void Start()
        {
            if (videoReceiverPrefab == null)
            {
                Debug.LogError("VideoReceiverPrefab is not assigned in VideoStreamSpawner.");
                return;
            }
            IRISXRNode.Instance.ServiceManager.RegisterServiceCallback<VideoStreamConfig, string>("SpawnVideoReceiver", SpawnVideoReceiver);
            IRISXRNode.Instance.ServiceManager.RegisterServiceCallback<string, string>("DeleteVideoReceiver", DeleteVideoReceiver);
        }

        private string SpawnVideoReceiver(VideoStreamConfig config)
        {
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                GameObject videoStreamObj = Instantiate(videoReceiverPrefab, gameObject.transform);
                videoStreamObj.name = config.name;
                VideoStreamReceiver receiver = videoStreamObj.GetComponent<VideoStreamReceiver>();
                if (receiver != null)                
                {
                    receiver.StartSubscription(config);
                }
            });
            return ResponseStatus.SUCCESS;
        }

        private string DeleteVideoReceiver(string videoStreamId)
        {
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                Transform videoStreamTrans = gameObject.transform.Find(videoStreamId);
                if (videoStreamTrans != null)
                {
                    Destroy(videoStreamTrans.gameObject);
                }
            });
            return ResponseStatus.SUCCESS;
        }
    }
}