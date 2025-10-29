using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

public class SpatialAnchorManager : MonoBehaviour
{
    public OVRSpatialAnchor anchorPrefab;
    public const string NumUuidsplayerPref = "NumUuids";

    private Canvas canvas;
    private TextMeshProUGUI uuidText;
    private TextMeshProUGUI savedStatusText;

    private readonly List<OVRSpatialAnchor> anchors = new();
    private OVRSpatialAnchor lastCreatedAnchor;
    private AnchorLoader anchorLoader;

    private bool _replaceInProgress = false; // debounce for creation
    private bool _wipeInProgress = false;    // debounce for X-button wipe

    private void Awake()
    {
        anchorLoader = GetComponent<AnchorLoader>();
        if (anchorLoader == null)
            Debug.LogWarning("AnchorLoader not found on the same GameObject. Loading will be unavailable.");
    }

    private void Update()
    {
        // Right Trigger -> create (only if no saved UUIDs exist)
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
            CreateSpatialAnchor();

        // A (right) -> save last created
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
            SaveLastCreatedAnchor();

        // B (right) -> unsave last created
        if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
            UnsaveLastCreatedAnchor();

        // Grip (right) -> unsave all anchors in our list (scene only list)
        if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch))
            UnsaveAllAnchors();

        // Right Thumbstick press -> load saved anchors
        if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.RTouch))
            LoadSavedAnchors();

        // X (left) -> FULL wipe: erase from device, destroy in scene, clear PlayerPrefs
        if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.LTouch))
        {
            _ = EraseDestroyAndClearAllAsync();
        }
    }

    // Gate creation on presence of saved UUIDs
    private bool HasAnySavedUuids()
    {
        return PlayerPrefs.HasKey(NumUuidsplayerPref) && PlayerPrefs.GetInt(NumUuidsplayerPref) > 0;
    }

    public async void CreateSpatialAnchor()
    {
        // Block creation if a saved anchor exists (must be unsaved/erased first)
        if (HasAnySavedUuids())
        {
            Debug.Log("A saved anchor exists. Unsave/erase it before creating a new one.");
            if (savedStatusText != null)
                savedStatusText.text = "Saved — unsave to replace";
            return;
        }

        if (_replaceInProgress) return;
        _replaceInProgress = true;
        try
        {
            // If a previous (unsaved) anchor exists, erase (harmless if not saved) and replace it
            if (lastCreatedAnchor != null)
            {
                try
                {
                    // Erase if it was saved; returns false if not saved
                    await lastCreatedAnchor.EraseAnchorAsync();
                    RemoveUuidFromPlayerPrefs(lastCreatedAnchor.Uuid);
                }
                catch (Exception e)
                {
                    Debug.Log($"Optional erase failed/ignored: {e.Message}");
                }

                anchors.Remove(lastCreatedAnchor);
                Destroy(lastCreatedAnchor.gameObject);
                lastCreatedAnchor = null;

                if (uuidText != null) uuidText.text = "UUID: (cleared)";
                if (savedStatusText != null) savedStatusText.text = "Not Saved";
            }

            // Create the new anchor at the right controller pose
            var pos = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
            var rot = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch);
            var workingAnchor = Instantiate(anchorPrefab, pos, rot);

            // Hook up small UI on the prefab (robust lookup)
            canvas = workingAnchor.gameObject.GetComponentInChildren<Canvas>(true);
            uuidText = null;
            savedStatusText = null;
            if (canvas != null)
            {
                var texts = canvas.GetComponentsInChildren<TextMeshProUGUI>(true);
                if (texts.Length > 1) uuidText = texts[0];
                if (texts.Length > 2) savedStatusText = texts[1];
            }

            // Enforce only one entry in our runtime list
            anchors.Clear();

            // Finish setup when anchor is actually created
            StartCoroutine(AnchorCreated(workingAnchor));
        }
        finally
        {
            _replaceInProgress = false;
        }
    }

    private IEnumerator AnchorCreated(OVRSpatialAnchor workingAnchor)
    {
        while (!workingAnchor.Created)
            yield return null;

        Guid anchorGuid = workingAnchor.Uuid;
        anchors.Add(workingAnchor);
        lastCreatedAnchor = workingAnchor;

        if (uuidText != null) uuidText.text = "UUID: " + anchorGuid.ToString();
        if (savedStatusText != null) savedStatusText.text = "Not Saved";
    }

    private async void SaveLastCreatedAnchor()
    {
        if (lastCreatedAnchor == null)
        {
            Debug.LogWarning("No anchor to save. Create one first.");
            return;
        }

        bool success = await lastCreatedAnchor.SaveAnchorAsync();
        if (savedStatusText != null)
            savedStatusText.text = success ? "Saved" : "Save Failed";

        if (success)
        {
            // Enforce single saved UUID for strict "one anchor" rule
            SaveSingleUuidToPlayerPrefs(lastCreatedAnchor.Uuid);
        }
    }

    // Overwrite to a single saved UUID (strict mode)
    private void SaveSingleUuidToPlayerPrefs(Guid uuid)
    {
        int prev = PlayerPrefs.GetInt(NumUuidsplayerPref, 0); // read old count first
        PlayerPrefs.SetInt(NumUuidsplayerPref, 1);
        PlayerPrefs.SetString("Uuid0", uuid.ToString());

        // delete any leftover Uuid1..Uuid{prev-1}
        for (int i = 1; i < prev; i++)
            PlayerPrefs.DeleteKey("Uuid" + i);

        PlayerPrefs.Save();
    }

    private async void UnsaveLastCreatedAnchor()
    {
        if (lastCreatedAnchor == null)
        {
            Debug.LogWarning("No anchor to erase. Create one first.");
            return;
        }

        bool success = await lastCreatedAnchor.EraseAnchorAsync();
        if (success)
        {
            if (savedStatusText != null) savedStatusText.text = "Not Saved";
            RemoveUuidFromPlayerPrefs(lastCreatedAnchor.Uuid);
        }
        else
        {
            Debug.LogWarning("Erase failed for lastCreatedAnchor.");
        }
    }

    private async void UnsaveAllAnchors()
    {
        foreach (var anchor in anchors)
            await UnsaveAnchorAsync(anchor);
    }

    private async Task UnsaveAnchorAsync(OVRSpatialAnchor anchor)
    {
        bool success = await anchor.EraseAnchorAsync();
        if (!success) return;

        RemoveUuidFromPlayerPrefs(anchor.Uuid);

        var texts = anchor.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (texts != null && texts.Length > 1)
            texts[1].text = "Not Saved";
    }

    private void RemoveUuidFromPlayerPrefs(Guid uuid)
    {
        if (!PlayerPrefs.HasKey(NumUuidsplayerPref)) return;

        int n = PlayerPrefs.GetInt(NumUuidsplayerPref);
        var kept = new List<string>(n);
        for (int i = 0; i < n; i++)
        {
            string key = "Uuid" + i;
            string val = PlayerPrefs.GetString(key, null);
            if (!string.IsNullOrEmpty(val) && !string.Equals(val, uuid.ToString(), StringComparison.OrdinalIgnoreCase))
                kept.Add(val);
        }

        // rewrite compacted list
        for (int i = 0; i < kept.Count; i++)
            PlayerPrefs.SetString("Uuid" + i, kept[i]);

        // delete leftover slots
        for (int i = kept.Count; i < n; i++)
            PlayerPrefs.DeleteKey("Uuid" + i);

        PlayerPrefs.SetInt(NumUuidsplayerPref, kept.Count);
        PlayerPrefs.Save();
    }

    private void ClearAllUuidsFromPlayerPrefs()
    {
        if (!PlayerPrefs.HasKey(NumUuidsplayerPref)) return;

        int n = PlayerPrefs.GetInt(NumUuidsplayerPref);
        for (int i = 0; i < n; i++)
            PlayerPrefs.DeleteKey("Uuid" + i);

        PlayerPrefs.DeleteKey(NumUuidsplayerPref);
        PlayerPrefs.Save();
    }

    public void LoadSavedAnchors()
    {
        if (anchorLoader == null)
        {
            Debug.LogWarning("AnchorLoader missing. Add it to the same GameObject.");
            return;
        }

        // Optional UX hint
        if (savedStatusText != null && HasAnySavedUuids())
            savedStatusText.text = "Saved — unsave to replace";

        anchorLoader.LoadAnchorsByUuid();
    }

    // X-button full wipe: erase from device, destroy in scene, clear prefs
    private async Task EraseDestroyAndClearAllAsync()
    {
        if (_wipeInProgress) return;
        _wipeInProgress = true;
        try
        {
            // Include inactive anchors; no sorting for performance
            var allAnchors = FindObjectsByType<OVRSpatialAnchor>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            // Erase from device/cloud
            foreach (var a in allAnchors)
            {
                try { await a.EraseAnchorAsync(); } catch { /* ignore failure, continue */ }
            }

            // Destroy GameObjects
            foreach (var a in allAnchors)
            {
                if (a != null) Destroy(a.gameObject);
            }

            // Clear saved UUIDs and reset state/UI
            ClearAllUuidsFromPlayerPrefs();
            anchors.Clear();
            lastCreatedAnchor = null;

            if (uuidText != null) uuidText.text = "UUID: (cleared)";
            if (savedStatusText != null) savedStatusText.text = "Not Saved";

            Debug.Log("All anchors erased, destroyed, and UUIDs cleared.");
        }
        finally
        {
            _wipeInProgress = false;
        }
    }
}