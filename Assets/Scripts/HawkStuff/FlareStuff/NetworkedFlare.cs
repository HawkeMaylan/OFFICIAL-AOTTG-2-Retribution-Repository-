using UnityEngine;
using Photon.Pun;

public class NetworkedFlare : MonoBehaviourPun, IPunInstantiateMagicCallback
{
    private Renderer _renderer;
    private bool _colorApplied = false;

    void Start()
    {
        _renderer = GetComponent<Renderer>();

        // Apply color immediately on all clients
        ApplyColorFromInstantiationData();

        // Apply force immediately on all clients
        ApplyForceFromInstantiationData();

        if (photonView.IsMine)
        {
            photonView.RPC("RPC_StartRigidbodyRemovalTimer", RpcTarget.AllBuffered);
        }
    }

    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        // This is called on all clients when the object is instantiated
        // Color and force are applied in Start() to ensure they happen on all clients
    }

    private void ApplyColorFromInstantiationData()
    {
        object[] data = photonView.InstantiationData;
        if (data != null && data.Length >= 4 && _renderer != null)
        {
            Color flareColor = new Color((float)data[0], (float)data[1], (float)data[2], (float)data[3]);
            ApplyColorToFlareBandMaterial(flareColor);
        }
    }

    private void ApplyForceFromInstantiationData()
    {
        object[] data = photonView.InstantiationData;
        if (data != null && data.Length >= 7)
        {
            Vector3 forceDirection = new Vector3((float)data[4], (float)data[5], (float)data[6]);
            ApplyForce(forceDirection);
        }
    }

    private void ApplyForce(Vector3 forceDirection)
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(forceDirection * 10f, ForceMode.Impulse);
        }
    }

    [PunRPC]
    private void RPC_StartRigidbodyRemovalTimer()
    {
        StartCoroutine(RemoveRigidbodyAfterDelay());
    }

    private System.Collections.IEnumerator RemoveRigidbodyAfterDelay()
    {
        yield return new WaitForSeconds(30f);

        if (photonView.IsMine)
        {
            Vector3 finalPosition = transform.position;
            Quaternion finalRotation = transform.rotation;
            photonView.RPC("RPC_RemoveRigidbody", RpcTarget.AllBuffered, finalPosition, finalRotation);
        }
    }

    [PunRPC]
    private void RPC_RemoveRigidbody(Vector3 finalPosition, Quaternion finalRotation)
    {
        transform.position = finalPosition;
        transform.rotation = finalRotation;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            Destroy(rb);
        }
    }

    private void ApplyColorToFlareBandMaterial(Color color)
    {
        Material[] materials = _renderer.materials;

        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i].name.StartsWith("FlareBandMaterial") ||
                materials[i].name.Contains("FlareBandMaterial") ||
                materials[i].name.Replace(" (Instance)", "") == "FlareBandMaterial")
            {
                materials[i].color = color;
                materials[i].SetColor("_Color", color);
                break;
            }
        }

        _renderer.materials = materials;
        _colorApplied = true;
    }
}