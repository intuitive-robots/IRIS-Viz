using System;
using System.Collections.Generic;
using UnityEngine;
using IRIS.Node;
using IRIS.Utilities;

namespace IRIS.SceneLoader
{

    public class SimSceneSpawner : MonoBehaviour
    {
        public Action OnSceneLoaded;
        public Action OnSceneCleared;
        // Services
        private List<INetComponent> _serviceList = new();
        private Dictionary<string, GameObject> _simSceneDict = new();
        [SerializeField] private GameObject simScenePrefab;

        void Start()
        {
            _serviceList.Add(new IRISService<SimScene, string>("SpawnSimScene", SpawnSimScene));
            _serviceList.Add(new IRISService<string, string>("DeleteSimScene", DeleteSimScene));
        }

        private string SpawnSimScene(SimScene simScene)
        {
            if (_simSceneDict.ContainsKey(simScene.name))
            {
                Debug.LogWarning($"SimScene with id {simScene.name} already exists, reusing the existing scene.");
                return IRISMSG.SUCCESS;
            }
            // make sure that the scene is loaded after this method is called
            UnityMainThreadDispatcher.Instance.EnqueueAndWait(() =>
            {
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
                UnityMainThreadDispatcher.Instance.Enqueue(() =>
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

    }
}