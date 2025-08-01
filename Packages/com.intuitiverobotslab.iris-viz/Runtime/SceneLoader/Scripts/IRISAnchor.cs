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
        protected string _anchorName;
        protected IRISAnchorData _data;
        protected bool isTrackingQR = false;
        private IRISService<IRISAnchorData, string> applyOffsetService;

        public void Initialize(string anchorName)
        {
            _anchorName = anchorName;
            applyOffsetService = new IRISService<IRISAnchorData, string>($"{_anchorName}/ApplyOffset", (data) =>
            {
                ApplyOffset(data);
                return IRISMSG.SUCCESS;
            });
        }

        protected void ApplyOffset(IRISAnchorData data)
        {
            _data = data;
            foreach (Transform child in transform)
            {
                child.SetLocalPositionAndRotation(_data.GetPos(), _data.GetRot());
            }
            Debug.Log($"Applying offset for {name}");
        }

        public virtual void StartTracking() { }

        public virtual void StopTracking() { }

    }

}