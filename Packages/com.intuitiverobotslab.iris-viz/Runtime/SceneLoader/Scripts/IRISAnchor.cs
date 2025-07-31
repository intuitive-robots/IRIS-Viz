using System.Collections.Generic;
using UnityEngine;
using IRIS.Node;

namespace IRIS.Utilities
{

    public class IRISAnchorData
    {
        public List<float> posOffset;
        public List<float> eulerOffset;
        public bool fixZAxis;
        public Vector3 GetPos()
        {
            return new Vector3(-posOffset[1], posOffset[2], posOffset[0]);
        }

        public Quaternion GetRot()
        {
            if (fixZAxis)
            {
                return Quaternion.Euler(0, eulerOffset[2], 0);
            }
            else
            {
                return Quaternion.Euler(eulerOffset[1], -eulerOffset[2], -eulerOffset[0]);
            }
        }
    }

    public class IRISAnchor : MonoBehaviour
    {


        [SerializeField] protected GameObject indicator;
        protected IRISAnchorData _data;
        protected bool isTrackingQR = false;
        private IRISService<IRISAnchorData, string> startAlignmentService;
        private IRISService<string, string> stopAlignmentService;

        private void Start()
        {
            startAlignmentService = new("ApplyOffset", StartAlignment);
            stopAlignmentService = new("ToggleAnchorTracking", StopAlignment);
        }

        protected void ApplyOffset()
        {
            foreach (Transform child in transform)
            {
                if (child.gameObject == indicator)
                    continue;
                child.SetLocalPositionAndRotation(_data.GetPos(), _data.GetRot());
            }
        }

        public string StartAlignment(IRISAnchorData data)
        {
            _data = data;
            isTrackingQR = true;
            indicator.SetActive(true);
            Debug.Log("Start Scene Tracking");
            StartSceneTracking(_data);
            return IRISMSG.SUCCESS;
        }

        public string StopAlignment(string signal)
        {
            isTrackingQR = false;
            indicator.SetActive(false);
            Debug.Log("Stop Scene Tracking");
            StopSceneTracking();
            return IRISMSG.SUCCESS;
        }

        public virtual void StartSceneTracking(IRISAnchorData data)
        {
            ApplyOffset();  // Only for testing
        }

        public virtual void StopSceneTracking()
        {
        }

        protected void SetSceneOrigin(Pose origin)
        {
            transform.position = origin.position;
            transform.rotation = origin.rotation;
            ApplyOffset();
        }
    }

}