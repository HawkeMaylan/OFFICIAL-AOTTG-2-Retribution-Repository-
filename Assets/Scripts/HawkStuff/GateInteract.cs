using UnityEngine;
using Characters;
using Photon.Pun;
using UI;
using Settings;
using GameManagers;
using ApplicationManagers;
using System.Collections;

public class GateInteract : MonoBehaviourPunCallbacks
{
    [Header("References")]
    public SphereCollider interactionTrigger; // Assign manually
    public Transform movingPart; // Gate piece to move

    [Header("Gate Settings")]
    public Vector3 closedLocalPosition;
    public Vector3 openedLocalPosition;
    public float moveSpeed = 2f;
    public string interactionText = "Press G to Open/Close";

    private bool isOpen = false;
    private bool isMoving = false;
    private Human humanInTrigger = null;
    private Vector3 targetPosition;

    private static string currentPrompt = "";

    private void Start()
    {
        if (movingPart != null)
        {
            movingPart.localPosition = closedLocalPosition;
        }

        if (interactionTrigger != null)
        {
            interactionTrigger.isTrigger = true;
        }
    }

    private void Update()
    {
        if (humanInTrigger != null && humanInTrigger.IsMine())
        {
            if (!InGameMenu.InMenu() && !ChatManager.IsChatActive())
            {
                if (Input.GetKeyDown(KeyCode.G) && !isMoving)
                {
                    photonView.RPC(nameof(RPC_ToggleGate), RpcTarget.All);
                }
            }
        }

        if (isMoving && movingPart != null)
        {
            movingPart.localPosition = Vector3.MoveTowards(movingPart.localPosition, targetPosition, moveSpeed * Time.deltaTime);

            if (Vector3.Distance(movingPart.localPosition, targetPosition) < 0.01f)
            {
                movingPart.localPosition = targetPosition;
                isMoving = false;
            }
        }
    }

    [PunRPC]
    private void RPC_ToggleGate()
    {
        if (isMoving) return; // Prevent toggling while still moving

        isOpen = !isOpen;
        targetPosition = isOpen ? openedLocalPosition : closedLocalPosition;
        isMoving = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        Human human = other.GetComponentInParent<Human>();
        if (human != null && human.IsMine())
        {
            humanInTrigger = human;
            SetPrompt(interactionText);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Human human = other.GetComponentInParent<Human>();
        if (human != null && human == humanInTrigger)
        {
            humanInTrigger = null;
            ClearPrompt();
        }
    }

    private void OnGUI()
    {
        if (!string.IsNullOrEmpty(currentPrompt))
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 24;
            style.alignment = TextAnchor.UpperCenter;
            style.normal.textColor = Color.white;

            GUI.Label(new Rect(Screen.width / 2 - 150, 10, 300, 50), currentPrompt, style);
        }
    }

    private void SetPrompt(string text)
    {
        currentPrompt = text;
    }

    private void ClearPrompt()
    {
        currentPrompt = "";
    }
}
