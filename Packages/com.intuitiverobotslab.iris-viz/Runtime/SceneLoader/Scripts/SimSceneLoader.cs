using System;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using System.Threading.Tasks;
using IRIS.Node;
using IRIS.Utilities;

namespace IRIS.SceneLoader
{

    public class SimSceneLoader : MonoBehaviour
    {
        [SerializeField] private Material defaultMaterial;
        [SerializeField] private GameObject sceneAxis;
        private object updateActionLock = new();
        private Action updateAction;
        public Action OnSceneLoaded;
        public Action OnSceneCleared;
        private IRISXRNode _XRNode;
        private GameObject _simSceneObj;
        private SimScene _simScene;
        private Dictionary<string, GameObject> _simObjectDict = new();
        private Dictionary<string, Transform> _simObjTransDict = new();

        void Start()
        {
            _XRNode = IRISXRNode.Instance;
            updateAction = () => { };
            sceneAxis.SetActive(false);
            sceneAxis.name = $"{name}_SceneAxis_IRIS";
            // OnSceneLoaded += () => Debug.Log("Scene Loaded");
        }

        void RunOnMainThread(Action action)
        {
            lock (updateActionLock)
            {
                updateAction += action;
            }
        }

        void Update()
        {
            lock (updateActionLock)
            {
                updateAction.Invoke();
                updateAction = () => { };
            }
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


        public void CreateSimObject(SimObject simObject)
        {
            if (_simObjectDict.ContainsKey(simObject.name))
            {
                Debug.LogWarning($"SimObject with name {simObject.name} already exists, skipping creation.");
                return;
            }
            GameObject newSimGameObject = new GameObject(simObject.name);
            if (!_simObjTransDict.ContainsKey(simObject.parentName))
            {
                Debug.Log($"Parent found for {simObject.name}: {simObject.parentName}");
                newSimGameObject.transform.SetParent(transform, false);
            }
            else
            {
                newSimGameObject.transform.SetParent(_simObjTransDict[simObject.parentName], false);
            }
            RegisterGameObject(simObject, newSimGameObject);
            ApplyTransform(newSimGameObject.transform, simObject.trans);
            Debug.Log($"Created SimObject: {simObject.name}");
        }

        public void CreateSimVisual(SimVisual simVisual, byte[] meshBytes, byte[] textureBytes)
        {
            if (!_simObjTransDict.ContainsKey(simVisual.objName))
            {
                Debug.LogWarning($"SimObject with name {simVisual.objName} not found, creating a new one.");
                return;
            }
            GameObject visualObj;
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
                        Debug.LogWarning($"SimVisual {simVisual.objName} has no mesh data, creating an empty GameObject.");
                        return;
                    }
                    BuildMesh(simVisual.mesh, visualObj, meshBytes);
                    // visualObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    break;
                default:
                    Debug.LogWarning($"Unknown SimVisual type {simVisual.type}, creating an empty GameObject.");
                    return;
            }
            BuildMaterial(simVisual.material, visualObj, textureBytes);
            // Renderer renderer = visualObj.GetComponent<Renderer>();
            // Debug.LogWarning($"Parent not found for {simVisual.objName}, setting to root.");
            visualObj.transform.SetParent(_simObjTransDict[simVisual.objName], false);
            ApplyTransform(visualObj.transform, simVisual.trans);
        }

        public void BuildMesh(SimMesh simMesh, GameObject visualObj, byte[] meshBytes)
        {
            MeshFilter meshFilter = visualObj.GetComponent<MeshFilter>();
            meshFilter.mesh = new Mesh
            {
                vertices = DecodeArray<Vector3>(meshBytes, simMesh.verticesLayout[0], simMesh.verticesLayout[1]),
                normals = DecodeArray<Vector3>(meshBytes, simMesh.normalsLayout[0], simMesh.normalsLayout[1]),
                triangles = DecodeArray<int>(meshBytes, simMesh.indicesLayout[0], simMesh.indicesLayout[1]),
                uv = DecodeArray<Vector2>(meshBytes, simMesh.uvLayout[0], simMesh.uvLayout[1])
            };
        }

        public void BuildMaterial(SimMaterial simMat, GameObject visualObj, byte[] textureBytes)
        {
            Material mat = new Material(defaultMaterial);
            if (simMat.color.Count == 3)
            {
                simMat.color.Add(1.0f);
            }
            else if (simMat.color.Count != 4)
            {
                Debug.LogWarning($"Invalid color for {visualObj.name}, using default color.");
                simMat.emissionColor = new List<float> { 1.0f, 1.0f, 1.0f, 1.0f };
            }
            // Transparency
            if (simMat.color[3] < 1)
            {
                mat.SetFloat("_Mode", 2);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }
            // In URP, the set color function is using "_BaseColor" instead of "_Color"
            mat.SetColor("_BaseColor", new Color(simMat.color[0], simMat.color[1], simMat.color[2], simMat.color[3]));
            if (simMat.emissionColor != null)
            {
                mat.SetColor("_emissionColor", new Color(simMat.emissionColor[0], simMat.emissionColor[1], simMat.emissionColor[2], simMat.emissionColor[3]));
            }
            mat.SetFloat("_specularHighlights", simMat.specular);
            mat.SetFloat("_Smoothness", simMat.shininess);
            mat.SetFloat("_GlossyReflections", simMat.reflectance);
            visualObj.GetComponent<Renderer>().material = mat;
            if (simMat.texture != null)
            {
                BuildTexture(simMat.texture, mat, textureBytes);
            }
            else
            {
                Debug.LogWarning($"No texture found for {visualObj.name}, using default material.");
            }
            // return mat;
        }

        public void BuildTexture(SimTexture simTex, Material mat, byte[] textureBytes)
        {
            // if (textureBytes == null || textureBytes.Length == 0)
            // {
            //     Debug.LogWarning($"No texture data found for {simTex.name}, using default material.");
            //     return;
            // }
            // Texture2D texture = new Texture2D(2, 2);
            // texture.LoadImage(textureBytes);
            // mat.mainTexture = texture;
            Texture2D tex = new Texture2D(simTex.width, simTex.height, TextureFormat.RGB24, false);
            tex.LoadRawTextureData(textureBytes);
            tex.Apply();
            mat.mainTexture = tex;
            mat.mainTextureScale = new Vector2(simTex.textureScale[0], simTex.textureScale[1]);
        }


        public static T[] DecodeArray<T>(byte[] data, int start, int length) where T : struct
        {
            return MemoryMarshal.Cast<byte, T>(new ReadOnlySpan<byte>(data, start, length)).ToArray();
        }

        void ClearScene()
        {
            OnSceneCleared?.Invoke();
            if (_simSceneObj != null) Destroy(_simSceneObj);
            _simObjectDict.Clear();
            _simObjTransDict.Clear();
            Debug.Log("Scene Cleared");
        }


        public Dictionary<string, Transform> GetObjectsTrans()
        {
            return _simObjTransDict;
        }

        public GameObject GetSimObject()
        {
            return _simSceneObj;
        }

    }
}