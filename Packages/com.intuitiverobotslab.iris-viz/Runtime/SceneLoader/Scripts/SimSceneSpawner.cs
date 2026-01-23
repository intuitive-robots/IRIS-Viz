using System;
using System.Collections.Generic;
using IRIS.Node;
using IRIS.Utilities;
using UnityEngine;

namespace IRIS.SceneLoader
{

    public class SimSceneSpawner : Singleton<SimSceneSpawner>
    {
        public Action OnSceneLoaded;
        public Action OnSceneCleared;
        // Services
        // private List<INetComponent> _serviceList = new();
        private Dictionary<string, GameObject> _simSceneDict = new();
        [SerializeField] private GameObject simScenePrefab;
        [SerializeField] private SimMaterialResolver materialResolver;

        void Start()
        {
            if (simScenePrefab == null)
            {
                Debug.LogError("SimScenePrefab is not assigned in SimSceneSpawner.");
                return;
            }
            if (materialResolver == null)
            {
                Debug.LogError("MaterialResolver is not assigned in SimSceneSpawner.");
            }
            else
            {
                materialResolver.Initialize();
            }
            IRISXRNode.Instance.ServiceManager.RegisterServiceCallback<string, string>("DeleteSimScene", DeleteSimScene);
            IRISXRNode.Instance.ServiceManager.RegisterServiceCallback<SimSceneConfig, string>("SpawnSimScene", SpawnSimScene);
        }

        private string SpawnSimScene(SimSceneConfig setting)
        {
            // make sure that the scene is loaded after this method is called
            UnityMainThreadDispatcher.Instance.EnqueueAndWait(() =>
            {
                if (_simSceneDict.ContainsKey(setting.name))
                {
                    Debug.LogWarning($"SimScene with id {setting.name} already exists, remove the existing scene.");
                    Destroy(_simSceneDict[setting.name]);
                    _simSceneDict.Remove(setting.name);
                }
                GameObject simSceneObj = Instantiate(simScenePrefab, gameObject.transform);
                simSceneObj.name = setting.name;
                simSceneObj.GetComponent<SimSceneLoader>().InitializeServices(setting.name);
                _simSceneDict.Add(setting.name, simSceneObj);
            });
            return ResponseStatus.SUCCESS;
        }

        private string DeleteSimScene(string simSceneId)
        {
            if (_simSceneDict.ContainsKey(simSceneId))
            {
                UnityMainThreadDispatcher.Instance.EnqueueAndWait(() =>
                {
                    if (_simSceneDict[simSceneId] != null)
                    {
                        Destroy(_simSceneDict[simSceneId]);
                    }
                    _simSceneDict.Remove(simSceneId);
                });
                return ResponseStatus.SUCCESS;
            }
            else
            {
                Debug.Log($"SimScene with id {simSceneId} does not exist, ignore it");
                return "No Scene Found";
            }
        }

        public GameObject GetSceneObject(string sceneName)
        {
            if (_simSceneDict.ContainsKey(sceneName))
            {
                return _simSceneDict[sceneName];
            }
            else
            {
                return null;
            }
        }

        public Transform GetSceneTransform(string sceneName)
        {
            if (_simSceneDict.ContainsKey(sceneName))
            {
                return _simSceneDict[sceneName].transform;
            }
            else
            {
                return null;
            }
        }

        public SimMaterialResolver GetMaterialResolver()
        {
            return materialResolver;
        }


    }
}