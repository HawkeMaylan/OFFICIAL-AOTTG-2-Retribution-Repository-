using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class NPCDialogueAuto : MonoBehaviourPun
{
    [Header("Dialogue Settings")]
    public List<string> dialogueLines = new List<string>();
    public float dialogueInterval = 10f;
    public float fadeTime = 2f;

    [Header("Display Settings")]
    public Vector3 textOffset = new Vector3(0f, 2f, 0f);
    public float textScale = 1f;
    public Camera referenceCamera;

    private GameObject dialogueTextObject;
    private TextMesh dialogueTextMesh;
    private float fadeTimer = -1f;
    private Color baseColor;

    private void Awake()
    {
        CreateDialogueText();
    }

    private void Start()
    {
        if (referenceCamera == null)
            referenceCamera = Camera.main;

        if (PhotonNetwork.IsMasterClient)
            StartCoroutine(DialogueLoop());
    }

    IEnumerator DialogueLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(dialogueInterval);
            if (dialogueLines.Count == 0) continue;

            int index = Random.Range(0, dialogueLines.Count);
            photonView.RPC("RPC_ShowDialogue", RpcTarget.All, dialogueLines[index]);
        }
    }

    [PunRPC]
    private void RPC_ShowDialogue(string line)
    {
        if (dialogueTextMesh == null)
            return;

        dialogueTextMesh.text = line;
        dialogueTextObject.SetActive(true);
        baseColor = dialogueTextMesh.color = Color.white;
        fadeTimer = fadeTime;
    }

    private void CreateDialogueText()
    {
        dialogueTextObject = new GameObject("NPCDialogueText");
        dialogueTextObject.transform.SetParent(transform);
        dialogueTextObject.transform.localPosition = textOffset;
        dialogueTextObject.transform.localRotation = Quaternion.identity;
        dialogueTextObject.transform.localScale = Vector3.one * textScale;

        dialogueTextMesh = dialogueTextObject.AddComponent<TextMesh>();
        dialogueTextMesh.fontSize = 32;
        dialogueTextMesh.characterSize = 0.1f;
        dialogueTextMesh.alignment = TextAlignment.Center;
        dialogueTextMesh.anchor = TextAnchor.MiddleCenter;
        dialogueTextMesh.color = Color.white;
        dialogueTextMesh.text = "";
        dialogueTextObject.SetActive(false);
    }

    private void FixedUpdate()
    {
        if (dialogueTextObject == null || dialogueTextMesh == null)
            return;

        if (referenceCamera == null)
            referenceCamera = Camera.main;
        if (referenceCamera == null)
            return;

        dialogueTextObject.transform.rotation =
            Quaternion.LookRotation(dialogueTextObject.transform.position - referenceCamera.transform.position);

        if (fadeTimer > 0f)
        {
            fadeTimer -= Time.fixedDeltaTime;
            float alpha = Mathf.Clamp01(fadeTimer / fadeTime);
            dialogueTextMesh.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

            if (fadeTimer <= 0f)
                dialogueTextObject.SetActive(false);
        }
    }
}
