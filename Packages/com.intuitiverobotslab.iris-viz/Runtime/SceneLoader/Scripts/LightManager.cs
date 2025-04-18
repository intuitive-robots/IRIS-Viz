using UnityEngine;

namespace IRIS.SceneLoader
{
    public class LightManager : MonoBehaviour
    {
        [Header("Light Settings")]
        public float lightIntensity = 0.5f; // Intensity of each light
        public float lightHeight = 10f; // Angle of the lights
        public float lightOffset = 10f; // Range of the lights
        public Color lightColor = Color.white; // Color of the lights

        private void Start()
        {
            CreateLights();
        }

        private void CreateLights()
        {
            Vector3[] lightPositions = new Vector3[]
                    {
                        new Vector3(-lightOffset, lightHeight, -lightOffset),
                        new Vector3(lightOffset, lightHeight, -lightOffset),
                        new Vector3(-lightOffset, lightHeight, lightOffset),
                        new Vector3(lightOffset, lightHeight, lightOffset)
                    };
            for (int i = 0; i < lightPositions.Length; i++)
            {
                // Create a new GameObject for the light
                GameObject lightObject = new GameObject($"DirectionalLight_{i + 1}");

                // Add a Light component
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = lightIntensity;
                light.color = lightColor;
                light.shadows = LightShadows.None; // Disable shadows

                // Set position of the light
                lightObject.transform.position = lightPositions[i];

                // Make the light aim at the origin
                lightObject.transform.LookAt(Vector3.zero);

                // Parent the light object under this GameObject
                lightObject.transform.parent = this.transform;
            }
            Debug.Log("Directional lights created by LightManager.");
        }
    }
}
