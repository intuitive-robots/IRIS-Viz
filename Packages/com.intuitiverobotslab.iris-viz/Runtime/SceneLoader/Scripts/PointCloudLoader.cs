// using UnityEngine;
// using NetMQ;
// using NetMQ.Sockets;
// using System;
// using System.Runtime.InteropServices;
// using IRIS.Node;


// public class PointCloudLoader : MonoBehaviour
// {
//     private ParticleSystem _particleSystem = null;
//     private ParticleSystem.Particle[] voxels;
//     private Subscriber<byte[]> _pointCloudSubscriber;

//     private void Start()
//     {
//         _particleSystem = GetComponent<ParticleSystem>();
//         _pointCloudSubscriber = new Subscriber<byte[]>("PointCloud", UpdatePointCloud);
//         IRISXRNode.Instance.OnConnectionStart += _pointCloudSubscriber.StartSubscription;

//         Debug.Log("Connected to the server");
//     }

//     public static float[] ByteArrayToFloatArray(byte[] byteArray)
//     {
//         if (byteArray == null || byteArray.Length % 7 != 0)
//             throw new ArgumentException("Invalid byte array length. Must be a multiple of 7.");

//         // Cast the byte array to a ReadOnlySpan<float>
//         ReadOnlySpan<byte> byteSpan = byteArray;
//         ReadOnlySpan<float> floatSpan = MemoryMarshal.Cast<byte, float>(byteSpan);

//         // Convert the ReadOnlySpan<float> to a float[]
//         return floatSpan.ToArray();
//     }

//     private void UpdatePointCloud(byte[] pointCloudMsg)
//     {
//         // Convert the byte array to a string
//         float[] pointCloud = ByteArrayToFloatArray(pointCloudMsg);
//         if (pointCloud.Length % 7 != 0)
//         {
//             Debug.LogError("Invalid point cloud data");
//             return;
//         }
//         int pointNum = pointCloud.Length / 7;
//         // Convert the data to the format that Unity's Particle System can use
//         if (voxels == null || voxels.Length != pointNum)
//         {
//             voxels = new ParticleSystem.Particle[pointNum];
            
//         }
//         for (int i = 0; i < pointNum; i++)
//         {
//             voxels[i].position = new Vector3(pointCloud[i * 7], pointCloud[i * 7 + 1], pointCloud[i * 7 + 2]);
//             voxels[i].startColor = new Color(pointCloud[i * 7 + 3], pointCloud[i * 7 + 4], pointCloud[i * 7 + 5]);
//             voxels[i].startSize = pointCloud[i * 7 + 6];
//         }
//         _particleSystem.SetParticles(voxels);
//     }

// }