using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class TextDisplay : MonoBehaviourPun
{
    [Header("Text Settings")]
    public float characterSize = 0.15f;

    [Header("Billboard Settings")]
    public Camera referenceCamera;

    private TextMesh textMesh;

    private void Awake()
    {
        // Get the TextMesh component attached to this object
        textMesh = GetComponent<TextMesh>();

        if (textMesh == null)
        {
            Debug.LogError("ProximityChatText requires a TextMesh component on the same GameObject!");
            return;
        }

        // Initialize with empty text and set character size
        textMesh.text = "";
        textMesh.characterSize = characterSize;

        // Get camera reference
        if (referenceCamera == null)
            referenceCamera = Camera.main;
    }

    // Public function to set the text - call this from other scripts
    public void SetText(string newText)
    {
        if (textMesh == null || string.IsNullOrEmpty(newText)) return;

        // Update locally
        textMesh.text = newText;

        // Sync with other players
        if (photonView.IsMine)
        {
            photonView.RPC("RPC_SetText", RpcTarget.Others, newText);
        }
    }

    // Public function to clear the text
    public void ClearText()
    {
        SetText("");
    }

    [PunRPC]
    private void RPC_SetText(string text)
    {
        if (textMesh != null)
        {
            textMesh.text = text;
        }
    }

    private void Update()
    {
        // Billboard effect - always face the camera
        if (referenceCamera == null)
            referenceCamera = Camera.main;

        if (referenceCamera != null && textMesh != null && !string.IsNullOrEmpty(textMesh.text))
        {
            // Make the text face the camera while maintaining up direction
            transform.rotation = Quaternion.LookRotation(transform.position - referenceCamera.transform.position);
        }
    }
}