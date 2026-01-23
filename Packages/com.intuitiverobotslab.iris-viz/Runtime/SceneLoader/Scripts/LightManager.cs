using UnityEngine;
using IRIS.Utilities;
using IRIS.Node;

namespace IRIS.SceneLoader
{
    public class LightManager : MonoBehaviour
    {
        private void Start()
        {
            IRISXRNode.Instance.ServiceManager.RegisterServiceCallback<LightConfig, string>($"{gameObject.name}/CreateLight", CreateLightsCb);
        }

        void OnDestroy()
        {
            IRISXRNode.Instance.ServiceManager.UnregisterServiceCallback($"{gameObject.name}/CreateLight");
        }


        private string CreateLightsCb(LightConfig sceneConfig)
        {
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                CreateLights(sceneConfig);
            });
            return ResponseStatus.SUCCESS;
        }

        private void CreateLights(LightConfig lightConfig)
        {
            GameObject lightGameObject = new GameObject(lightConfig.name);
            Light lightComp = lightGameObject.AddComponent<Light>();
            // Configure light based on provided config
            lightComp.type = lightConfig.lightType.ToLower() switch
            {
                "directional" => LightType.Directional,
                "point" => LightType.Point,
                "spot" => LightType.Spot,
                _ => LightType.Point
            };

            lightComp.color = new Color(
                lightConfig.color[0],
                lightConfig.color[1],
                lightConfig.color[2]
            );
            lightComp.intensity = lightConfig.intensity;

            lightGameObject.transform.position = new Vector3(
                lightConfig.position[0],
                lightConfig.position[1],
                lightConfig.position[2]
            );
            lightGameObject.transform.LookAt(new Vector3(
                lightConfig.direction[0],
                lightConfig.direction[1],
                lightConfig.direction[2]
            ));
            if (lightComp.type == LightType.Spot)
            {
                lightComp.spotAngle = lightConfig.spotAngle;
            }
            lightGameObject.transform.parent = gameObject.transform;
        }
    }
}
