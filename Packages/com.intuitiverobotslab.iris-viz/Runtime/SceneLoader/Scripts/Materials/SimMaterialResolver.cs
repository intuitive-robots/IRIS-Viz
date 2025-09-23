using System;
using System.Collections.Generic;
using UnityEngine;

namespace IRIS.SceneLoader
{
    [CreateAssetMenu(menuName = "IRIS/Material Resolver", fileName = "SimMaterialResolver")]
    public class SimMaterialResolver : ScriptableObject
    {
        [SerializeField] private ScriptableObject defaultProfileAsset;
        [SerializeField] private List<ScriptableObject> materialSetProfileAssets = new();

        private readonly List<IMaterialSetProfile> _profiles = new();
        private IMaterialSetProfile _defaultProfile;
        private bool _initialized;

        public void Initialize()
        {
            BuildProfiles();
            _initialized = true;
        }

        public void Cleanup()
        {
            _profiles.Clear();
            _defaultProfile = null;
            _initialized = false;
        }

        public void ApplyMaterial(SimVisual simVisual, Renderer renderer)
        {
            if (renderer == null)
            {
                Debug.LogWarning("Renderer is null, cannot apply material.");
                return;
            }

            EnsureInitialized();

            IMaterialSetProfile profile = FindSupportingProfile(simVisual) ?? _defaultProfile;
            if (profile == null)
            {
                Debug.LogWarning("No material profile available; renderer keeps its current material.");
                return;
            }

            Material material = profile.CreateMaterialInstance(simVisual);
            if (material == null)
            {
                Debug.LogWarning($"Profile '{profile.DisplayName}' returned a null material instance.");
                return;
            }

            material.hideFlags = HideFlags.DontSave;
            renderer.sharedMaterial = material;
        }

        public bool ApplyProfile(string profileName, Renderer renderer)
        {
            if (renderer == null)
            {
                Debug.LogWarning("Renderer is null, cannot apply profile.");
                return false;
            }

            EnsureInitialized();

            IMaterialSetProfile profile = FindProfileByName(profileName);
            if (profile == null)
            {
                Debug.LogWarning($"Profile '{profileName}' not found. Check the resolver profile list.");
                return false;
            }

            Material material = profile.CreateMaterialInstance(null);
            if (material == null)
            {
                Debug.LogWarning($"Profile '{profile.DisplayName}' returned a null material instance.");
                return false;
            }

            material.hideFlags = HideFlags.DontSave;
            renderer.sharedMaterial = material;
            return true;
        }

        public void ApplyProfileToHierarchy(GameObject root, string profileName, bool includeInactive = true)
        {
            if (root == null)
            {
                Debug.LogWarning("ApplyProfileToHierarchy called with null root GameObject.");
                return;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive);
            foreach (Renderer renderer in renderers)
            {
                ApplyProfile(profileName, renderer);
            }
        }

        private void BuildProfiles()
        {
            _profiles.Clear();
            _defaultProfile = CastProfile(defaultProfileAsset, "Default Profile");

            foreach (ScriptableObject asset in materialSetProfileAssets)
            {
                IMaterialSetProfile profile = CastProfile(asset, asset != null ? asset.name : "Profile");
                if (profile != null)
                {
                    _profiles.Add(profile);
                }
            }
        }

        private static IMaterialSetProfile CastProfile(ScriptableObject asset, string context)
        {
            if (asset == null)
            {
                return null;
            }

            if (asset is IMaterialSetProfile profile)
            {
                return profile;
            }

            Debug.LogWarning($"Material profile asset '{context}' does not implement IMaterialSetProfile.");
            return null;
        }

        private IMaterialSetProfile FindSupportingProfile(SimVisual simVisual)
        {
            foreach (IMaterialSetProfile profile in _profiles)
            {
                if (profile != null && profile.Supports(simVisual))
                {
                    return profile;
                }
            }

            return null;
        }

        private IMaterialSetProfile FindProfileByName(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName))
            {
                return null;
            }

            foreach (IMaterialSetProfile profile in _profiles)
            {
                if (profile != null && string.Equals(profile.DisplayName, profileName, StringComparison.OrdinalIgnoreCase))
                {
                    return profile;
                }
            }

            if (_defaultProfile != null && string.Equals(_defaultProfile.DisplayName, profileName, StringComparison.OrdinalIgnoreCase))
            {
                return _defaultProfile;
            }

            return null;
        }

        private void EnsureInitialized()
        {
            if (!_initialized)
            {
                Initialize();
            }
        }
    }
}
