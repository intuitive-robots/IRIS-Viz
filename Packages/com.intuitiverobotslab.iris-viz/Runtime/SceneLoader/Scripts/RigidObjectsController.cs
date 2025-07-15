using System.Collections.Generic;
using UnityEngine;
using IRIS.Node;
using System;

namespace IRIS.SceneLoader
{

    [Serializable]
    public class StreamMessage : Dictionary<string, List<float>> { }

    [RequireComponent(typeof(SimSceneLoader))]
    public class RigidObjectsController : MonoBehaviour
    {
        public Dictionary<string, Transform> _objectsTrans;
        private Transform _trans;
        private Subscriber<StreamMessage> _subscriber;

        void Start()
        {
            _subscriber = new Subscriber<StreamMessage>("RigidObjectUpdate", SubscribeCallback);
        }

        public void StartSubscription(string url)
        {
            _trans = gameObject.transform;
            _objectsTrans = gameObject.GetComponent<SimSceneLoader>().GetObjectsTrans();
            // timeOffset = IRISXRNode.Instance.TimeOffset;
            _subscriber.StartSubscription(url);
        }

        public void StopSubscription()
        {
            _subscriber.Unsubscribe();
        }

        public void SubscribeCallback(StreamMessage streamMsg)
        {
            foreach (var (name, value) in streamMsg)
            {
                if (!_objectsTrans.ContainsKey(name))
                {
                    continue;
                }
                _objectsTrans[name].position = transform.TransformPoint(new Vector3(value[0], value[1], value[2]));
                _objectsTrans[name].rotation = _trans.rotation * new Quaternion(value[3], value[4], value[5], value[6]);
            }
        }

    }
}