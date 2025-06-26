using System;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using System.Threading.Tasks;
using IRIS.Node;
using IRIS.Utilities;
using System.Data.SqlTypes;

namespace IRIS.SceneLoader
{

    public class SimSceneSpawner : MonoBehaviour
    {
        public Action OnSceneLoaded;
        public Action OnSceneCleared;
        private object updateActionLock = new();
        private Action updateAction;
        private IRISXRNode _XRNode;
        // Services
        private Dictionary<string, INetComponent> serviceDict = new();
        private Dictionary<string, GameObject> _simSceneDict = new();
        [SerializeField] private GameObject simScenePrefab;

        void Start()
        {
            _XRNode = IRISXRNode.Instance;
            updateAction = () => { };
            serviceDict["SpawnSimScene"] = new IRISService<SimScene, string>("SpawnSimScene", SpawnSimScene, true);
            serviceDict["CreateSimObject"] = new IRISService<SimObject, string>("CreateSimObject", CreateSimObject, true);
            serviceDict["CreateVisual"] = new IRISService<SimVisual, byte[], byte[], string>("CreateVisual", CreateSimVisual, true);
            serviceDict["DeleteSimScene"] = new IRISService<string, string>("DeleteSimScene", DeleteSimScene, true);
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


        private string SpawnSimScene(SimScene simScene)
        {
            if (_simSceneDict.ContainsKey(simScene.name))
            {
                Debug.LogWarning($"SimScene with id {simScene.name} already exists, reusing the existing scene.");
                return IRISSignal.SUCCESS;
            }
            GameObject simSceneObj = Instantiate(simScenePrefab, gameObject.transform);
            simSceneObj.name = simScene.name;
            _simSceneDict.Add(simScene.name, simSceneObj);
            return IRISSignal.SUCCESS;
        }

        private string CreateSimObject(SimObject simObject)
        {
            SimSceneLoader simSceneLoader = _simSceneDict[simObject.sceneName].GetComponent<SimSceneLoader>();
            if (simSceneLoader == null)
            {
                Debug.LogError($"SimSceneLoader component not found in SimScene with id {simObject.sceneName}");
                return IRISSignal.ERROR;
            }
            RunOnMainThread(
                () =>
                {
                    simSceneLoader.CreateSimObject(simObject);
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

        private string CreateSimVisual(SimVisual simVisual, byte[] meshBytes, byte[] materialBytes)
        {
            SimSceneLoader simSceneLoader = _simSceneDict[simVisual.sceneName].GetComponent<SimSceneLoader>();
            if (simSceneLoader == null)
            {
                Debug.LogError($"SimSceneLoader component not found in SimScene with id {simVisual.sceneName}");
                return IRISSignal.ERROR;
            }
            RunOnMainThread(
                () =>
                {
                    simSceneLoader.CreateSimVisual(simVisual, meshBytes, materialBytes);
                }
            );
            return IRISSignal.SUCCESS;
        }

    }
}