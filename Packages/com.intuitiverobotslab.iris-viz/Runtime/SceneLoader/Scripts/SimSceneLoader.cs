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

        static void ApplyTransform(Transform uTransform, IRISTransform simTrans)
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
                CreateSimObject(null, simObject);
            });
            return IRISMSG.SUCCESS;
        }

        public void CreateSimObject(string parentName, SimObject simObject)
        {
            if (_simObjectDict.ContainsKey(simObject.name))
            {
                Debug.LogWarning($"SimObject with name {simObject.name} already exists, skipping creation.");
                return;
            }
            GameObject newSimGameObject = new GameObject(simObject.name);
            if (!_simObjTransDict.ContainsKey(parentName))
            {
                // Debug.Log($"Parent found for {simObject.name}: {parentName}");
                newSimGameObject.transform.SetParent(transform, false);
            }
            else
            {
                newSimGameObject.transform.SetParent(_simObjTransDict[parentName], false);
            }
            RegisterGameObject(simObject, newSimGameObject);
            ApplyTransform(newSimGameObject.transform, simObject.trans);
            if (simObject.visuals != null)
            {
                foreach (var visual in simObject.visuals)
                {
                    // Skip creating visuals with mesh or material
                    if (visual.mesh != null) continue;
                    if (visual.material != null && visual.material.texture != null) continue;
                    CreateSimVisual(newSimGameObject.name, visual, null, null);
                }
            }
            if (simObject.children != null)
            {
                foreach (var child in simObject.children)
                {
                    CreateSimObject(newSimGameObject.name, child);
                }
            }
            Debug.Log($"Created SimObject: {simObject.name}");
        }

        private string CreateSimVisualCb(string objName, SimVisual simVisual, byte[] meshBytes, byte[] textureBytes)
        {
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                CreateSimVisual(objName, simVisual, meshBytes, textureBytes);
            });
            return IRISMSG.SUCCESS;
        }


        public void CreateSimVisual(string objName, SimVisual simVisual, byte[] meshBytes, byte[] textureBytes)
        {
            if (!_simObjTransDict.ContainsKey(objName))
            {
                Debug.LogWarning($"SimObject with name {objName} not found, creating a new one.");
                return;
            }
            GameObject visualObj;
            Debug.Log($"Creating visual for {objName}");
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
                        Debug.LogWarning($"SimVisual {objName} has no mesh data, creating an empty GameObject.");
                        return;
                    }
                    BuildMesh(simVisual.mesh, visualObj, meshBytes);
                    break;
                default:
                    Debug.LogWarning($"Unknown SimVisual type {simVisual.type}, creating an empty GameObject.");
                    return;
            }
            Renderer visualRenderer = visualObj.GetComponent<Renderer>();
            if (visualRenderer != null)
            {
                materialResolver.ApplyMaterial(simVisual, visualRenderer);
            }
            if (simVisual.material.texture != null)
            {
                BuildTexture(simVisual.material.texture, visualRenderer.material, textureBytes);
            }
            visualObj.transform.SetParent(_simObjTransDict[objName], false);
            ApplyTransform(visualObj.transform, simVisual.trans);
        }


        public void BuildMesh(SimMesh simMesh, GameObject visualObj, byte[] meshBytes)
        {
            _ = meshBytes;
            MeshFilter meshFilter = visualObj.GetComponent<MeshFilter>();
            meshFilter.mesh = new Mesh
            {
                vertices = DecodeArray<Vector3>(simMesh.vertices, 0, simMesh.vertices.Length),
                normals = DecodeArray<Vector3>(simMesh.normals, 0, simMesh.normals.Length),
                triangles = DecodeArray<int>(simMesh.indices, 0, simMesh.indices.Length),
                uv = DecodeArray<Vector2>(simMesh.uv, 0, simMesh.uv.Length)
            };
        }

        public void BuildTexture(SimTexture simTex, Material mat, byte[] textureBytes)
        {
            Texture2D tex = new Texture2D(simTex.width, simTex.height, TextureFormat.RGB24, false);
            tex.LoadRawTextureData(textureBytes);
            tex.Apply();
            mat.mainTexture = tex;
            mat.mainTextureScale = new Vector2(simTex.textureScale[0], simTex.textureScale[1]);
        }



        private string SubscribeRigidObjectsControllerCb(string url)
        {
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                RigidObjectsController rigidObjectsController = GetComponent<RigidObjectsController>();
                rigidObjectsController.StartSubscription(url);
            });
            return IRISMSG.SUCCESS;
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
            IRISXRNode.Instance.ServiceManager.UnregisterServiceCallback($"{gameObject.name}/CreateSimObject");
            IRISXRNode.Instance.ServiceManager.UnregisterServiceCallback($"{gameObject.name}/SubscribeRigidObjectsController");
            _simObjectDict.Clear();
            _simObjTransDict.Clear();
            materialResolver?.Cleanup();
        }

    }
}
