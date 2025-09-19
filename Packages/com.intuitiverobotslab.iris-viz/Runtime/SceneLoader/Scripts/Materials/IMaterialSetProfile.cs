using UnityEngine;

namespace IRIS.SceneLoader
{
    public interface IMaterialSetProfile
    {
        string DisplayName { get; }

        bool Supports(SimVisual simVisual);

        Material CreateMaterialInstance(SimVisual simVisual);
    }
}
