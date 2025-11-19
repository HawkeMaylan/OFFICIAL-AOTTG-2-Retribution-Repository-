using UnityEngine;
using Photon.Pun;

public class BuildableTracker : MonoBehaviourPunCallbacks
{
    private string prefabName;
    private int viewId;

    public void Initialize(string prefabName, int viewId)
    {
        this.prefabName = prefabName;
        this.viewId = viewId;
    }

    private void OnDestroy()
    {
        // Notify BuildSystem to remove this from tracking
        BuildSystem buildSystem = FindObjectOfType<BuildSystem>();
        if (buildSystem != null)
        {
            buildSystem.RemoveBuildable(viewId);
        }
    }

    public string GetPrefabName() => prefabName;
    public int GetViewId() => viewId;
}