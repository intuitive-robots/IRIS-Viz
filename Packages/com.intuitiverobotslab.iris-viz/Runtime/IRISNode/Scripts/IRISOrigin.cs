using System.Collections.Generic;
using UnityEngine;
using IRIS.Node;
using MessagePack;

namespace IRIS.SceneLoader
{

    [MessagePackObject(keyAsPropertyName: true)]
    public class SceneOffset
    {
        public float x, y, z;
        public float rotX, rotY, rotZ;
        public Vector3 GetPos()
        {
            return new Vector3(-y, z, x);
        }

        public Quaternion GetRot()
        {
            return Quaternion.Euler(rotY, -rotZ, -rotX);
        }
    }

    public class IRISOrigin : MonoBehaviour
    {
        [SerializeField]
        private GameObject _originVisualObject;
        [SerializeField]
        private Transform _sceneTransform;
        protected SceneOffset _offsetData;

        public void Start()
        {
            ServiceManager _serviceManager = IRISXRNode.Instance.ServiceManager;
            _serviceManager.RegisterServiceCallback<SceneOffset, string>("ApplyAlignmentOffset", ApplyAlignmentOffsetCallback, true);
            _serviceManager.RegisterServiceCallback<string, string>("ToggleOrigin", ToggleOriginCallback, true);
        }

        public void OnDestroy()
        {
            ServiceManager _serviceManager = IRISXRNode.Instance.ServiceManager;
            _serviceManager.UnregisterServiceCallback("ApplyAlignmentOffset");
            _serviceManager.UnregisterServiceCallback("ToggleOrigin");
        }

        private string ApplyAlignmentOffsetCallback(SceneOffset data)
        {
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                ApplyAlignmentOffset(data);
            });
            return "Offset Command Received";
        }

        public void ApplyAlignmentOffset(SceneOffset data)
        {
            _offsetData = data;
            _sceneTransform.localPosition = _offsetData.GetPos();
            _sceneTransform.localRotation = _offsetData.GetRot();
            Debug.Log($"Applying offset for {name}");
        }

        private string ToggleOriginCallback(string msg)
        {
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                ToggleOrigin();
            });
            return "Toggle Command Received";
        }

        public string ToggleOrigin()
        {
            if (_originVisualObject == null)
            {
                Debug.LogWarning("Origin visual object is not assigned.");
                return "Origin Visual Not Assigned";
            }
            if (_originVisualObject.activeInHierarchy)
            {
                Debug.Log("Toggling origin off.");
                _originVisualObject.SetActive(false);
            }
            else
            {
                Debug.Log("Toggling origin on.");
                _originVisualObject.SetActive(true);
            }
            return "Origin Toggled";
        }
    }
}