using System;
using System.Collections.Generic;
using UnityEngine;
using IRIS.Node;
using IRIS.Utilities;

namespace IRIS.SceneLoader
{

    public class SimSceneSpawner : Singleton<SimSceneSpawner>
    {
        public Action OnSceneLoaded;
        public Action OnSceneCleared;
        // Services
        private List<INetComponent> _serviceList = new();
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
            _serviceList.Add(new IRISService<SimScene, string>("SpawnSimScene", SpawnSimScene));
            _serviceList.Add(new IRISService<string, string>("DeleteSimScene", DeleteSimScene));
        }

        private string SpawnSimScene(SimScene simScene)
        {
            // make sure that the scene is loaded after this method is called
            UnityMainThreadDispatcher.Instance.EnqueueAndWait(() =>
            {
                if (_simSceneDict.ContainsKey(simScene.name))
                {
                    Debug.LogWarning($"SimScene with id {simScene.name} already exists, remove the existing scene.");
                    Destroy(_simSceneDict[simScene.name]);
                    _simSceneDict.Remove(simScene.name);
                }
                GameObject simSceneObj = Instantiate(simScenePrefab, gameObject.transform);
                simSceneObj.name = simScene.name;
                _simSceneDict.Add(simScene.name, simSceneObj);
            });
            return IRISMSG.SUCCESS;
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
                return IRISMSG.SUCCESS;
            }
            else
            {
                Debug.LogWarning($"SimScene with id {simSceneId} does not exist.");
                return IRISMSG.ERROR;
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