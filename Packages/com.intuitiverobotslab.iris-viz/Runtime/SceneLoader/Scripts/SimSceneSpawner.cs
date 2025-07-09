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
        private Dictionary<string, INetComponent> serviceDict = new();
        private Dictionary<string, GameObject> _simSceneDict = new();
        [SerializeField] private GameObject simScenePrefab;

        void Start()
        {
            serviceDict["SpawnSimScene"] = new IRISService<SimScene, string>("SpawnSimScene", SpawnSimScene, true);
            serviceDict["CreateSimObject"] = new IRISService<string, string, SimObject, string>("CreateSimObject", CreateSimObject, true);
            serviceDict["CreateVisual"] = new IRISService<string, string, SimVisual, byte[], byte[], string>("CreateVisual", CreateSimVisual, true);
            serviceDict["DeleteSimScene"] = new IRISService<string, string>("DeleteSimScene", DeleteSimScene, true);
            serviceDict["SubscribeRigidObjectsController"] = new IRISService<string, string, string, string>("SubscribeRigidObjectsController", SubscribeRigidObjectsController, true);
        }

        private string SpawnSimScene(SimScene simScene)
        {
            if (_simSceneDict.ContainsKey(simScene.name))
            {
                Debug.LogWarning($"SimScene with id {simScene.name} already exists, reusing the existing scene.");
                return IRISSignal.SUCCESS;
            }
            UnityMainThreadDispatcher.Instance.Enqueue(
                () =>
                {
                    GameObject simSceneObj = Instantiate(simScenePrefab, gameObject.transform);
                    simSceneObj.name = simScene.name;
                    _simSceneDict.Add(simScene.name, simSceneObj);
                }
            );
            return IRISSignal.SUCCESS;
        }

        private string CreateSimObject(string sceneName, string parentName, SimObject simObject)
        {
            SimSceneLoader simSceneLoader = _simSceneDict[sceneName].GetComponent<SimSceneLoader>();
            if (simSceneLoader == null)
            {
                Debug.LogError($"SimSceneLoader component not found in SimScene with id {sceneName}");
                return IRISSignal.ERROR;
            }
            UnityMainThreadDispatcher.Instance.Enqueue(
                () =>
                {
                    simSceneLoader.CreateSimObject(parentName, simObject);
                }
            );
            return IRISSignal.SUCCESS;
        }


        private string DeleteSimScene(string simSceneId)
        {
            if (_simSceneDict.ContainsKey(simSceneId))
            {
                Destroy(_simSceneDict[simSceneId]);
                _simSceneDict.Remove(simSceneId);
                return IRISSignal.SUCCESS;
            }
            else
            {
                Debug.LogWarning($"SimScene with id {simSceneId} does not exist.");
                return IRISSignal.ERROR;
            }
        }

        private string CreateSimVisual(string sceneName, string objName, SimVisual simVisual, byte[] meshBytes, byte[] textureBytes)
        {
            if (!_simSceneDict.ContainsKey(sceneName))
            {
                return IRISSignal.SUCCESS;
            }
            SimSceneLoader simSceneLoader = _simSceneDict[sceneName].GetComponent<SimSceneLoader>();
            UnityMainThreadDispatcher.Instance.Enqueue(
                () =>
                {
                    simSceneLoader.CreateSimVisual(objName, simVisual, meshBytes, textureBytes);
                }
            );
            return IRISSignal.SUCCESS;
        }

        private string SubscribeRigidObjectsController(string sceneName, string url, string topicName)
        {
            RigidObjectsController rigidObjectsController = _simSceneDict[sceneName].GetComponent<RigidObjectsController>();
            if (rigidObjectsController == null)
            {
                Debug.LogError("RigidObjectsController component not found on the GameObject.");
                return IRISSignal.ERROR;
            }
            rigidObjectsController.StartSubscription(url);
            return IRISSignal.SUCCESS;
        }

    }
}