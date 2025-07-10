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
                return IRISSignal.SUCCESS;
            }
            GameObject simSceneObj = Instantiate(simScenePrefab, gameObject.transform);
            simSceneObj.name = simScene.name;
            _simSceneDict.Add(simScene.name, simSceneObj);
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

    }
}