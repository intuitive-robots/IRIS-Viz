using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

public class AnchorLoader : MonoBehaviour
{
    private OVRSpatialAnchor anchorPrefab;
    private SpatialAnchorManager spatialAnchorManager;

    private void Awake()
    {
        spatialAnchorManager = GetComponent<SpatialAnchorManager>();
        if (spatialAnchorManager == null)
        {
            Debug.LogError("SpatialAnchorManager component not found on this GameObject.");
            return;
        }

        anchorPrefab = spatialAnchorManager.anchorPrefab;
        if (anchorPrefab == null)
        {
            Debug.LogError("Anchor prefab is not assigned in SpatialAnchorManager.");
        }
    }

    public void LoadAnchorsByUuid()
    {
        if (!PlayerPrefs.HasKey(SpatialAnchorManager.NumUuidsplayerPref))
        {
            PlayerPrefs.SetInt(SpatialAnchorManager.NumUuidsplayerPref, 0);
        }

        int count = PlayerPrefs.GetInt(SpatialAnchorManager.NumUuidsplayerPref);
        if (count <= 0)
        {
            Debug.Log("No saved anchors to load.");
            return;
        }

        var uuids = new Guid[count];
        for (int i = 0; i < count; i++)
        {
            string key = "Uuid" + i;
            string val = PlayerPrefs.GetString(key, string.Empty);
            if (!Guid.TryParse(val, out uuids[i]))
            {
                Debug.LogWarning($"Invalid UUID at '{key}': '{val}'");
            }
        }

        LoadByGuids(uuids);
    }

    private async void LoadByGuids(Guid[] uuids)
    {
        try
        {
            OVRSpatialAnchor.UnboundAnchor[] anchors;
#pragma warning disable CS0618 // LoadOptions is still needed in some SDKs
            var options = new OVRSpatialAnchor.LoadOptions { Uuids = uuids };
            anchors = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(options);
#pragma warning restore CS0618

            if (anchors == null || anchors.Length == 0)
            {
                Debug.Log("Failed to load anchors or none found.");
                return;
            }

            foreach (var unbound in anchors)
                await HandleUnboundAnchorAsync(unbound);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error loading unbound anchors: {ex.Message}");
        }
    }

    private async Task HandleUnboundAnchorAsync(OVRSpatialAnchor.UnboundAnchor unboundAnchor)
    {
        bool localized = unboundAnchor.Localized || await unboundAnchor.LocalizeAsync();
        if (!localized)
        {
            Debug.LogWarning("Unbound anchor failed to localize.");
            return;
        }

        if (!unboundAnchor.TryGetPose(out var pose))
        {
            Debug.LogWarning("Localized anchor has no valid pose.");
            return;
        }

        if (anchorPrefab == null)
        {
            Debug.LogError("Anchor prefab is not assigned.");
            return;
        }

        var spatialAnchor = Instantiate(anchorPrefab, pose.position, pose.rotation);
        unboundAnchor.BindTo(spatialAnchor);

        var texts = spatialAnchor.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (texts != null && texts.Length >= 2)
        {
            texts[0].text = "UUID: " + spatialAnchor.Uuid.ToString();
            texts[1].text = "Loaded from device";
        }
    }
}
