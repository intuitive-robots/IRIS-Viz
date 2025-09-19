using System.Collections.Generic;
using UnityEngine;

namespace IRIS.SceneLoader
{
    [CreateAssetMenu(menuName = "IRIS/Material Profile/RGBD", fileName = "IRISRGBDMaterialProfile")]
    public class RGBDMaterialSetProfile : ScriptableObject, IMaterialSetProfile
    {
        [SerializeField] private string displayName;
        [SerializeField] private Material opaqueMaterial;
        [SerializeField] private Material transparentMaterial;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? "RGBD" : displayName;
        public bool Supports(SimVisual simVisual)
        {
            // RGBD profile supports all visuals but switches material based on alpha.
            return true;
        }

        public Material CreateMaterialInstance(SimVisual simVisual)
        {
            SimMaterial simMat = simVisual?.material;
            Material source = IsTransparent(simMat) ? transparentMaterial : opaqueMaterial;
            if (source == null)
            {
                return new Material(Shader.Find("Standard"));
            }
            Material mat = new Material(source);
            if (simMat.color.Count == 3)
            {
                simMat.color.Add(1.0f);
            }
            else if (simMat.color.Count != 4)
            {
                Debug.LogWarning($"Invalid color for {simVisual.name}, using default color.");
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
                mat.renderQueue = -1;
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
            return mat;
        }


        private static bool IsTransparent(SimMaterial simMaterial)
        {
            if (simMaterial == null || simMaterial.color == null)
            {
                return false;
            }

            if (simMaterial.color.Count >= 4)
            {
                return simMaterial.color[3] < 1f;
            }

            return false;
        }

    }
}
