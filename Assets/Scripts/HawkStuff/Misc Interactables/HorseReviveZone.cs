using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Characters;
using Settings;
using GameManagers;
using ApplicationManagers;
using System.Collections;

public class HorseReviveZone : MonoBehaviourPunCallbacks
{
    public Collider triggerZone;
    public Vector3 spawnOffset = new Vector3(2f, 0f, 0f);
    public float promptDuration = 3f;
    public float cooldownDuration = 10f;
    public int maxRespawns = 3;

    private Human localHuman;
    private Coroutine promptCoroutine;
    private string currentPrompt = "";
    private string extraPrompt = "";
    private bool isInside = false;

    private double lastRespawnTime = -999f;
    private int respawnsUsed = 0;

    private void Update()
    {
        if (ChatManager.IsChatActive()) return;

        if (!isInside)
        {
            Human checkHuman = FindLocalHumanInZone();
            if (checkHuman != null)
            {
                localHuman = checkHuman;
                isInside = true;
            }
        }
        else if (localHuman == null || !IsStillInZone(localHuman))
        {
            ClearPrompt();
            isInside = false;
            localHuman = null;
        }

        if (isInside && localHuman != null)
        {
            double timeSinceLast = PhotonNetwork.Time - lastRespawnTime;
            int remaining = maxRespawns - respawnsUsed;

            if (remaining <= 0)
            {
                currentPrompt = "No Horses Remaining";
                extraPrompt = "";
                return;
            }

            extraPrompt = $"Horses Left: {remaining}";

            if (timeSinceLast < cooldownDuration)
            {
                float timeLeft = Mathf.Ceil((float)(cooldownDuration - timeSinceLast));
                currentPrompt = $"New Horse Being Prepared ({timeLeft}s)";
            }
            else
            {
                currentPrompt = $"Press {SettingsManager.InputSettings.Interaction.Interact2} To Get A New Horse";

                if (SettingsManager.InputSettings.Interaction.Interact2.GetKeyDown())
                {
                    photonView.RPC(nameof(RPC_RequestHorseRespawn), RpcTarget.MasterClient, localHuman.photonView.OwnerActorNr, localHuman.Cache.Transform.position);
                    ClearPrompt();
                    isInside = false;
                }
            }
        }
    }

    private Human FindLocalHumanInZone()
    {
        foreach (Human h in FindObjectsOfType<Human>())
        {
            if (h.photonView.IsMine)
            {
                Transform trigger = h.transform.Find("HumanTrigger");
                if (trigger != null && triggerZone.bounds.Contains(trigger.position))
                    return h;
            }
        }
        return null;
    }

    private bool IsStillInZone(Human h)
    {
        Transform trigger = h.transform.Find("HumanTrigger");
        return trigger != null && triggerZone.bounds.Contains(trigger.position);
    }

    [PunRPC]
    private void RPC_RequestHorseRespawn(int actorNumber, Vector3 position)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (respawnsUsed >= maxRespawns) return;

        double timeSinceLast = PhotonNetwork.Time - lastRespawnTime;
        if (timeSinceLast < cooldownDuration) return;

        // Try to find the Human for this actorNumber
        foreach (var human in FindObjectsOfType<Human>())
        {
            if (human.photonView.Owner != null && human.photonView.Owner.ActorNumber == actorNumber)
            {
                // Destroy current horse if it exists
                if (human.Horse != null && human.Horse.photonView != null)
                {
                    PhotonNetwork.Destroy(human.Horse.gameObject);
                }
                break;
            }
        }

        Player target = PhotonNetwork.CurrentRoom.GetPlayer(actorNumber);
        if (target != null)
        {
            respawnsUsed++;
            lastRespawnTime = PhotonNetwork.Time;

            photonView.RPC(nameof(RPC_UpdateReviveState), RpcTarget.All, respawnsUsed, lastRespawnTime);

            Vector3 spawnPosition = position + spawnOffset;
            GameObject horseObj = PhotonNetwork.Instantiate("Characters/Horse/Prefabs/Horse", spawnPosition, Quaternion.identity);
            PhotonView horseView = horseObj.GetComponent<PhotonView>();
            horseView.TransferOwnership(target);
            photonView.RPC(nameof(RPC_ConfirmHorseRespawn), target, horseView.ViewID);
            StartCoroutine(EnsureHorseOwnershipAndLink(horseView, actorNumber));
        }
    }

    [PunRPC]
    private void RPC_ConfirmHorseRespawn(int viewID)
    {
        PhotonView horseView = PhotonView.Find(viewID);
        if (horseView != null && horseView.IsMine)
        {
            Horse horse = horseView.GetComponent<Horse>();
            if (horse != null)
            {
                Debug.Log("[HorseReviveZone] Horse confirmed and owned.");
            }
        }
    }

    [PunRPC]
    private void RPC_UpdateReviveState(int used, double respawnTime)
    {
        respawnsUsed = used;
        lastRespawnTime = respawnTime;
    }

    private IEnumerator EnsureHorseOwnershipAndLink(PhotonView horseView, int actorNumber)
    {
        float timeout = 2f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (horseView != null)
            {
                if (horseView.Owner != null && horseView.Owner.ActorNumber == actorNumber)
                {
                    horseView.RPC("RPC_SetHorseOwner", horseView.Owner, actorNumber);
                    yield break;
                }
                else
                {
                    horseView.TransferOwnership(actorNumber);
                }
            }

            yield return new WaitForSeconds(0.2f);
            elapsed += 0.2f;
        }

        Debug.LogWarning($"[HorseReviveZone] Failed to assign horse to player {actorNumber}");
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
        if (!isInside || localHuman == null || !localHuman.photonView.IsMine)
            return;

        if (!string.IsNullOrEmpty(currentPrompt))
        {
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                alignment = TextAnchor.UpperCenter,
                wordWrap = false,
                normal = { textColor = Color.white }
            };

            float labelWidth = 800f;
            float labelHeight = 50f;
            float labelX = Screen.width / 2 - labelWidth / 2;

            GUI.Label(new Rect(labelX, 50, labelWidth, labelHeight), currentPrompt, style);

            if (!string.IsNullOrEmpty(extraPrompt))
                GUI.Label(new Rect(labelX, 85, labelWidth, labelHeight), extraPrompt, style);
        }
    }
}
