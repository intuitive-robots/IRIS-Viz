using System;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using IRIS.Node;
using IRIS.Utilities;

namespace IRIS.SceneLoader
{

    public class SimSceneLoader : MonoBehaviour
    {
        private SimMaterialResolver materialResolver;
        private Dictionary<string, GameObject> _simObjectDict = new();
        private Dictionary<string, Transform> _simObjTransDict = new();

        void Awake()
        {
            materialResolver = SimSceneSpawner.Instance.GetMaterialResolver();
        }

        public void InitializeServices(string sceneName)
        {
            gameObject.name = sceneName;
            IRISXRNode.Instance.ServiceManager.RegisterServiceCallback<SimObject, string>($"{gameObject.name}/CreateSimObject", CreateSimObjectCb);
            IRISXRNode.Instance.ServiceManager.RegisterServiceCallback<string, string>($"{gameObject.name}/SubscribeRigidObjectsController", SubscribeRigidObjectsControllerCb);
        }

        static void ApplyTransform(Transform uTransform, SimTransform simTrans)
        {
            uTransform.localPosition = simTrans.GetPos();
            uTransform.localRotation = simTrans.GetRot();
            uTransform.localScale = simTrans.GetScale();
        }

        void RegisterGameObject(SimObject simObject, GameObject simGameObject)
        {
            if (_simObjectDict.ContainsKey(simObject.name))
            {
                Debug.LogWarning($"SimObject with name {simObject.name} already exists, skipping registration.");
                return;
            }
            if (_simObjTransDict.ContainsKey(simObject.name))
            {
                Debug.LogWarning($"SimObject with id {simObject.name} already exists, skipping registration.");
                return;
            }
            _simObjectDict.Add(simObject.name, simGameObject);
            _simObjTransDict.Add(simObject.name, simGameObject.transform);
        }

        private string CreateSimObjectCb(SimObject simObject)
        {
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                CreateSimObject(simObject);
            });
            return ResponseStatus.SUCCESS;
        }

        public void CreateSimObject(SimObject simObject)
        {
            if (_simObjectDict.ContainsKey(simObject.name))
            {
                Debug.LogWarning($"SimObject with name {simObject.name} already exists, skipping creation.");
                return;
            }
            GameObject newSimGameObject = new GameObject(simObject.name);
            if (!_simObjTransDict.ContainsKey(simObject.parent))
            {
                // Debug.Log($"Parent found for {simObject.name}: {simObject.parent}");
                newSimGameObject.transform.SetParent(transform, false);
            }
            else
            {
                newSimGameObject.transform.SetParent(_simObjTransDict[simObject.parent], false);
            }
            RegisterGameObject(simObject, newSimGameObject);
            ApplyTransform(newSimGameObject.transform, simObject.trans);
            if (simObject.visuals != null)
            {
                foreach (var visual in simObject.visuals)
                {
                    // Debug.Log($"Creating visual for {simObject.name} without mesh or texture");
                    GameObject visualObj = CreateSimVisual(visual);
                    if (visualObj != null)
                    {
                        visualObj.transform.SetParent(newSimGameObject.transform, false);
                    }
                }
            }
        }

        public GameObject CreateSimVisual(SimVisual simVisual)
        {
            if (_simObjTransDict.ContainsKey(simVisual.name))
            {
                Debug.LogWarning($"SimVisualObject with name {simVisual.name} already exists, skipping creation.");
                return null;
            }
            GameObject visualObj;
            // Debug.Log($"Creating visual for {simVisual.name} with type {simVisual.type}");
            switch (simVisual.type)
            {
                case "CUBE":
                    visualObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    break;
                case "PLANE":
                    visualObj = GameObject.CreatePrimitive(PrimitiveType.Plane);
                    break;
                case "CYLINDER":
                    visualObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    break;
                case "CAPSULE":
                    visualObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    break;
                case "SPHERE":
                    visualObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    break;
                case "MESH":
                    visualObj = new GameObject(simVisual.name, typeof(MeshFilter), typeof(MeshRenderer));
                    if (simVisual.mesh == null)
                    {
                        Debug.LogWarning($"SimVisual {simVisual.name} has no mesh data, creating an empty GameObject.");
                        return null;
                    }
                    BuildMesh(simVisual.mesh, visualObj);
                    break;
                default:
                    Debug.LogWarning($"Unknown SimVisual type {simVisual.type}, creating an empty GameObject.");
                    return null;
            }
            Renderer visualRenderer = visualObj.GetComponent<Renderer>();
            if (visualRenderer != null)
            {
                materialResolver.ApplyMaterial(simVisual, visualRenderer);
            }
            if (simVisual.material.texture != null)
            {
                BuildTexture(simVisual.material.texture, visualRenderer.material);
            }
            ApplyTransform(visualObj.transform, simVisual.trans);
            return visualObj;
        }


        public void BuildMesh(SimMesh simMesh, GameObject visualObj)
        {
            Debug.Log($"Building mesh for {visualObj.name}");
            MeshFilter meshFilter = visualObj.GetComponent<MeshFilter>();
            meshFilter.mesh = new Mesh();
            meshFilter.mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            meshFilter.mesh.name = $"{visualObj.name}_Mesh";
            meshFilter.mesh.vertices = DecodeArray<Vector3>(simMesh.vertices, 0, simMesh.vertices.Length);
            meshFilter.mesh.normals = DecodeArray<Vector3>(simMesh.normals, 0, simMesh.normals.Length);
            meshFilter.mesh.triangles = DecodeArray<int>(simMesh.indices, 0, simMesh.indices.Length);
            if (simMesh.uv != null)
            {
                meshFilter.mesh.uv = DecodeArray<Vector2>(simMesh.uv, 0, simMesh.uv.Length);
            }
        }

        public void BuildTexture(SimTexture simTex, Material mat)
        {
            Texture2D tex = new Texture2D(2, 2); 
            // LoadImage automatically decodes the JPEG header and data
            if (tex.LoadImage(simTex.textureData)) 
            {
                tex.Apply();
                mat.mainTexture = tex;
                mat.mainTextureScale = new Vector2(simTex.textureScale[0], simTex.textureScale[1]);
            }
            else 
            {
                Debug.LogError("Failed to decode the JPEG/PNG data.");
            }
        }

        private string SubscribeRigidObjectsControllerCb(string url)
        {
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                RigidObjectsController rigidObjectsController = GetComponent<RigidObjectsController>();
                rigidObjectsController.StartSubscription(url);
            });
            return ResponseStatus.SUCCESS;
        }


        public static T[] DecodeArray<T>(byte[] data, int start, int length) where T : struct
        {
            return MemoryMarshal.Cast<byte, T>(new ReadOnlySpan<byte>(data, start, length)).ToArray();
        }

        public Dictionary<string, Transform> GetObjectsTrans()
        {
            return _simObjTransDict;
        }

        private void OnDestroy()
        {
            Debug.LogWarning($"Destroying SimSceneLoader for scene {gameObject.name}");
            IRISXRNode.Instance.ServiceManager.UnregisterServiceCallback($"{gameObject.name}/CreateSimObject");
            IRISXRNode.Instance.ServiceManager.UnregisterServiceCallback($"{gameObject.name}/SubscribeRigidObjectsController");
            _simObjectDict.Clear();
            _simObjTransDict.Clear();
            materialResolver?.Cleanup();
            Debug.LogWarning($"SimSceneLoader for scene {gameObject.name} destroyed");
        }

    }
}
