using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class SelfDestroyOnImpact : MonoBehaviourPun
{
    [Header("Destruction Settings")]
    public float safetyTime = 1f;

    [Tooltip("Drag the prefab here (must be in Resources/Buildables)")]
    public GameObject prefabToSpawn;

    [Tooltip("Match the prefab path under Resources/ (e.g. 'Buildables/MyPrefab')")]
    public string resourcePath = "Buildables/IncendiaryZoneCannon";

    private bool canTrigger = false;
    private PhotonView view;

    private void Start()
    {
        view = GetComponent<PhotonView>();
        Invoke(nameof(EnableTrigger), safetyTime);
    }

    private void EnableTrigger()
    {
        canTrigger = true;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!canTrigger) return;

        Vector3 hitPosition = collision.contacts[0].point;

        if (prefabToSpawn == null)
        {
            Debug.LogError("Prefab to spawn is not assigned!");
            return;
        }

        if (string.IsNullOrEmpty(resourcePath))
        {
            Debug.LogError("resourcePath is empty — must be something like 'Buildables/MyPrefab'");
            return;
        }

        // Force Quaternion.identity (0,0,0 rotation)
        if (PhotonNetwork.InRoom)
            PhotonNetwork.Instantiate(resourcePath, hitPosition, Quaternion.identity);
        else
            Instantiate(Resources.Load(resourcePath), hitPosition, Quaternion.identity);

        TryDestroy();
    }


    private void TryDestroy()
    {
        if (!PhotonNetwork.IsConnected || PhotonNetwork.OfflineMode)
        {
            Destroy(gameObject);
            return;
        }

        if (photonView.IsMine || PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.Destroy(gameObject);
            return;
        }

        // Try to take ownership and destroy
        photonView.TransferOwnership(PhotonNetwork.MasterClient);

        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.Destroy(gameObject);
        }
        else
        {
            Debug.LogWarning("[SelfDestroyOnImpact] Could not destroy via Photon. Falling back to local destroy.");
            Destroy(gameObject);
        }
    }
}
