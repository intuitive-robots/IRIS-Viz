using UnityEngine;
using TMPro;

public class PointCloudHUD : MonoBehaviour
{
    [Header("Assign in Inspector")]
    public TextMeshProUGUI textTarget;                // drag your TextMeshProUGUI here
    public MultiCamPointCloudRenderer rendererRef;    // drag your MultiCamPointCloudRenderer here

    void Update()
    {
        if (textTarget == null || rendererRef == null)
            return;

        int pts = rendererRef.RenderedPointCountDebug;
        textTarget.text = $"Density: {pts}";
    }
}
