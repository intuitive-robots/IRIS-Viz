using UnityEngine;
using System.Collections.Generic;

namespace IRIS.Utilities
{
    public static class TransformUtils
    {
        public static List<float> Unity2ROS(Vector3 position)
        {
            return new List<float> { position.z, - position.x, position.y };
        }

        public static List<float> Unity2ROS(Quaternion rotation)
        {
            return new List<float> { - rotation.z, rotation.x, - rotation.y, rotation.w };
        }
    }
}