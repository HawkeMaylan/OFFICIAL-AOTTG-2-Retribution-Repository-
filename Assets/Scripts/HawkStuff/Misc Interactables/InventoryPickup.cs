using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Characters;
using Settings;
using GameManagers;
using ApplicationManagers;
using System.Collections;
using System.Collections.Generic;

public class ItemGrantZone : MonoBehaviourPunCallbacks, IPunObservable
{
    [System.Serializable]
    public struct GrantItem
    {
        public string itemType;
        public int amount;
    }

    [Header("Grant Settings")]
    public Collider triggerZone;
    public List<GrantItem> itemsToGrant = new List<GrantItem>();
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

    private Dictionary<string, string> friendlyNames = new Dictionary<string, string>
    {
        { "Wagon1", "Support Wagon" },
        { "Wagon2", "Resupply Wagon" },
        { "Cannon", "Cannon" },
        { "WallCannon", "Wall Cannon" },
        { "CannonGround", "Field Cannon" },
    };

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(grantsUsed);
            stream.SendNext(lastGrantTime);
        }
        else
        {
            grantsUsed = (int)stream.ReceiveNext();
            lastGrantTime = (float)stream.ReceiveNext();
        }
    }

    private void Update()
    {
        if (ChatManager.IsChatActive() || isShrinking) return;

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
            int remaining = maxGrants - grantsUsed;

            if (remaining <= 0)
            {
                currentPrompt = "No Items remaining";
                extraPrompt = "";
                return;
            }

            extraPrompt = $"Items left: {remaining}";

            float timeSinceLast = Time.time - lastGrantTime;
            if (timeSinceLast < cooldownDuration)
            {
                float timeLeft = Mathf.Ceil(cooldownDuration - timeSinceLast);
                currentPrompt = $"Pickup on cooldown ({timeLeft}s)";
            }
            else
            {
                string itemList = string.Join(", ", itemsToGrant.ConvertAll(entry =>
                {
                    string name = friendlyNames.TryGetValue(entry.itemType, out var display) ? display : entry.itemType;
                    return $"{name} x{entry.amount}";
                }));

                currentPrompt = $"Press {SettingsManager.InputSettings.Interaction.Interact2} to Pick Up: {itemList}";

                if (SettingsManager.InputSettings.Interaction.Interact2.GetKeyDown())
                {
                    photonView.RPC("RPC_TryGrant", RpcTarget.MasterClient, PhotonNetwork.LocalPlayer.ActorNumber);
                }
            }
        }
    }

    [PunRPC]
    private void RPC_TryGrant(int actorId, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || isShrinking) return;

        if (grantsUsed >= maxGrants || (Time.time - lastGrantTime) < cooldownDuration)
            return;

        grantsUsed++;
        lastGrantTime = Time.time;

        photonView.RPC("RPC_SyncGrant", RpcTarget.All, grantsUsed, lastGrantTime);

        foreach (var human in FindObjectsOfType<Human>())
        {
            if (human.photonView != null && human.photonView.OwnerActorNr == actorId)
            {
                var inventory = human.GetComponent<HumanInventory>();
                if (inventory != null)
                {
                    foreach (var entry in itemsToGrant)
                    {
                        for (int i = 0; i < entry.amount; i++)
                        {
                            inventory.AddItem(entry.itemType);
                        }
                    }
                }
                break;
            }
        }

        if (grantsUsed >= maxGrants && destroyWhenEmpty)
            StartCoroutine(ShrinkAndDestroy());
    }

    [PunRPC]
    private void RPC_SyncGrant(int used, float lastTime)
    {
        grantsUsed = used;
        lastGrantTime = lastTime;
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
                normal = { textColor = currentPrompt.Contains("cooldown") ? Color.red : Color.white }
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
