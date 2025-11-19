using UnityEngine;
using Photon.Pun;
using System.Collections;

public class SelfDestroy : MonoBehaviourPun
{
    public float lifetime = 3f;
    public float ownershipTimeout = 2f;

    private void Start()
    {
        Invoke(nameof(DestroyObjectSafely), lifetime);
    }

    private void DestroyObjectSafely()
    {
        if (!PhotonNetwork.IsConnected || photonView == null || photonView.ViewID == 0)
        {
            Destroy(gameObject);
            return;
        }

        if (photonView.IsMine)
        {
            PhotonNetwork.Destroy(gameObject);
        }
        else if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.Destroy(gameObject);
        }
        else if (photonView.OwnershipTransfer == OwnershipOption.Takeover || photonView.OwnershipTransfer == OwnershipOption.Request)
        {
            photonView.RequestOwnership();
            StartCoroutine(WaitAndDestroy());
        }
        else
        {
            Debug.LogWarning("[SelfDestroy] Cannot destroy — not owner, not MC, and ownership not transferable.");
        }
    }

    private IEnumerator WaitAndDestroy()
    {
        float elapsed = 0f;
        while (!photonView.IsMine && elapsed < ownershipTimeout)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }

        if (photonView.IsMine)
        {
            PhotonNetwork.Destroy(gameObject);
        }
        else
        {
            Debug.LogWarning("[SelfDestroy] Timed out waiting for ownership. Destroy skipped.");
        }
    }
}
