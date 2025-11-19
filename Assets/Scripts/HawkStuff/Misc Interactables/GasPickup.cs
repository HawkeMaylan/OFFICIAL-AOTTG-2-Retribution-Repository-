using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Characters;
using Settings;
using GameManagers;
using ApplicationManagers;
using System.Collections;
using System.Collections.Generic;

public class GasPickup : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Gas Settings")]
    public Collider triggerZone;
    public float gasPickup = 100f; // Amount of gas to add to player
    public float cooldownDuration = 10f;
    public int maxGrants = 3;

    [Header("Object Cleanup")]
    public bool destroyWhenEmpty = false;
    public float shrinkAndDestroyTime = 1.5f;

    [Header("UI Prompt")]
    public float promptDuration = 3f;

    private Human localHuman;
    private Coroutine promptCoroutine;
    private static string currentPrompt = "";
    private static string extraPrompt = "";

    [SerializeField]
    private float lastGrantTime = -999f;
    private int grantsUsed = 0;
    private bool isInside = false;
    private bool isShrinking = false;

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(grantsUsed);
            stream.SendNext(lastGrantTime);
            stream.SendNext(isShrinking);
        }
        else
        {
            grantsUsed = (int)stream.ReceiveNext();
            lastGrantTime = (float)stream.ReceiveNext();
            isShrinking = (bool)stream.ReceiveNext();
        }
    }

    private void Update()
    {
        if (ChatManager.IsChatActive() || isShrinking)
            return;

        // Only check if we lost track of the human
        if (isInside && (localHuman == null || !IsHumanValid(localHuman)))
        {
            ClearPrompt();
            isInside = false;
            localHuman = null;
            return;
        }

        if (isInside && localHuman != null)
        {
            UpdatePromptAndInput();
        }
    }

    private void UpdatePromptAndInput()
    {
        int remaining = maxGrants - grantsUsed;

        if (remaining <= 0)
        {
            currentPrompt = "No Gas remaining";
            extraPrompt = "";
            return;
        }

        extraPrompt = $"Gas pickups left: {remaining}";

        float timeSinceLast = Time.time - lastGrantTime;
        if (timeSinceLast < cooldownDuration)
        {
            float timeLeft = Mathf.Ceil(cooldownDuration - timeSinceLast);
            currentPrompt = $"Pickup on cooldown ({timeLeft}s)";
        }
        else
        {
            // Check if player is already at max gas
            if (localHuman.Stats != null && localHuman.Stats.CurrentGas >= localHuman.Stats.MaxGas)
            {
                currentPrompt = "Gas is already full!";
            }
            else
            {
                currentPrompt = $"Press {SettingsManager.InputSettings.Interaction.Interact2} to Collect Gas: +{gasPickup}";

                if (SettingsManager.InputSettings.Interaction.Interact2.GetKeyDown())
                {
                    TryGrantGas();
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isShrinking) return;

        Human human = other.GetComponentInParent<Human>();
        if (human != null && human.photonView.IsMine)
        {
            localHuman = human;
            isInside = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Human human = other.GetComponentInParent<Human>();
        if (human != null && human == localHuman)
        {
            ClearPrompt();
            isInside = false;
            localHuman = null;
        }
    }

    private void TryGrantGas()
    {
        if (isShrinking) return;

        // Check local conditions first
        if (grantsUsed >= maxGrants || (Time.time - lastGrantTime) < cooldownDuration)
            return;

        if (localHuman == null || localHuman.Stats == null)
            return;

        // Check if player already has max gas
        if (localHuman.Stats.CurrentGas >= localHuman.Stats.MaxGas)
            return;

        // Update the shared state via RPC
        photonView.RPC("RPC_UpdateGrants", RpcTarget.All, grantsUsed + 1, Time.time);

        // Apply gas locally to the interacting player
        float gasToAdd = gasPickup;
        float newGas = localHuman.Stats.CurrentGas + gasToAdd;
        if (newGas > localHuman.Stats.MaxGas)
        {
            gasToAdd = localHuman.Stats.MaxGas - localHuman.Stats.CurrentGas;
        }
        localHuman.Stats.CurrentGas += gasToAdd;

        // Handle destruction if empty
        if ((grantsUsed + 1) >= maxGrants && destroyWhenEmpty)
        {
            if (photonView.IsMine) // Only the owner handles destruction
            {
                photonView.RPC("RPC_StartShrinkAndDestroy", RpcTarget.All);
            }
        }
    }

    [PunRPC]
    private void RPC_UpdateGrants(int newGrantsUsed, float newLastGrantTime)
    {
        grantsUsed = newGrantsUsed;
        lastGrantTime = newLastGrantTime;
    }

    [PunRPC]
    private void RPC_StartShrinkAndDestroy()
    {
        if (!isShrinking)
            StartCoroutine(ShrinkAndDestroy());
    }

    private IEnumerator ShrinkAndDestroy()
    {
        isShrinking = true;

        if (localHuman != null && localHuman.photonView.IsMine)
        {
            ClearPrompt();
            isInside = false;
            localHuman = null;
        }

        Vector3 originalScale = transform.localScale;
        float timer = 0f;

        while (timer < shrinkAndDestroyTime)
        {
            float t = timer / shrinkAndDestroyTime;
            transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, t);
            timer += Time.deltaTime;
            yield return null;
        }

        transform.localScale = Vector3.zero;

        if (photonView != null && photonView.IsMine)
            PhotonNetwork.Destroy(gameObject);
        else
            Destroy(gameObject);
    }

    private bool IsHumanValid(Human human)
    {
        return human != null && human.gameObject != null && human.photonView != null && human.Stats != null;
    }

    private void ClearPrompt()
    {
        currentPrompt = "";
        extraPrompt = "";
        if (promptCoroutine != null)
        {
            StopCoroutine(promptCoroutine);
            promptCoroutine = null;
        }
    }

    private void OnGUI()
    {
        if (!string.IsNullOrEmpty(currentPrompt))
        {
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                alignment = TextAnchor.UpperCenter,
                wordWrap = false,
                normal = { textColor = currentPrompt.Contains("cooldown") ? Color.red :
                          currentPrompt.Contains("full") ? Color.yellow : Color.white }
            };

            float labelWidth = 600f;
            float labelHeight = 30f;
            float labelX = Screen.width / 2 - labelWidth / 2;

            GUI.Label(new Rect(labelX, 50, labelWidth, labelHeight), currentPrompt, style);

            if (!string.IsNullOrEmpty(extraPrompt))
                GUI.Label(new Rect(labelX, 85, labelWidth, labelHeight), extraPrompt, style);
        }
    }
}