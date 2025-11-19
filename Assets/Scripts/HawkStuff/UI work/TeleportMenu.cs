using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using Characters;
using UI;
using GameManagers;
using Photon.Pun;
using System.Collections;
using System.IO;
using Settings;
using ApplicationManagers;
using UI;
using GameManagers;


public class TeleportMenu : MonoBehaviourPunCallbacks
{
    private bool menuOpen = false;
    private Vector2 scrollPosition;
    private Player selectedPlayer;
    private string inputX = "", inputY = "", inputZ = "", searchFilter = "";

    private bool confirmKick = false;
    private bool confirmBan = false;

    private List<Player> _cachedPlayers = new List<Player>();
    private Dictionary<int, string> _playerLabels = new Dictionary<int, string>();
    private Dictionary<int, string> _cachedNames = new Dictionary<int, string>();
    private Dictionary<int, bool> _cachedDeathStatus = new Dictionary<int, bool>();

    private bool showInventoryPanel = false;
    private string newCannonCount = "0";
    private string newWagon1Count = "0";
    private string newWagon2Count = "0";

    private bool showStatsPanel = false;
    private string newHP = "100";
    private string newMaxGas = "1000";
    private string newMaxBlades = "5";
    private string newAcceleration = "20";
    private string newSpeed = "14";
    private string newHorseSpeed = "20";

    private List<SavedLocation> savedLocations = new List<SavedLocation>();
    private string newTitle = "";
    private string searchQuery = "";
    private string deleteConfirmTitle = null;
    private Vector2 savedScroll;

    private bool showSavedLocationPanel = false;

    private bool showCustomSpawnPanel = false;
    private string customPrefabName = "";
    private string customBundleName = "";
    private string customPosX = "0", customPosY = "0", customPosZ = "0";
    private string customRotX = "0", customRotY = "0", customRotZ = "0";


    private List<GameObject> spawnedCustomAssets = new List<GameObject>();



    private string customLayer = "23";

    private bool showAssetManagerPanel = false;
    private GameObject selectedMoveTarget = null;
    private string moveInputX = "0", moveInputY = "0", moveInputZ = "0";







    [System.Serializable]
    public class SavedLocation
    {
        public string Title;
        public float X, Y, Z;
    }

    [System.Serializable]
    public class SavedLocationList
    {
        public List<SavedLocation> Locations = new List<SavedLocation>();
    }




    private void Start()
    {
        LoadLocations();
    }
    private void Update()
    {
        if (SettingsManager.InputSettings.General.MCMenu.GetKeyDown() && PhotonNetwork.IsMasterClient)

        {
            menuOpen = !menuOpen;
            ToggleCursor(menuOpen);

            if (menuOpen)
                RefreshPlayerList();
        }
    }

    private void ToggleCursor(bool enable)
    {
        Cursor.lockState = enable ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = enable;
    }

    private void RefreshPlayerList()
    {
        _cachedPlayers = new List<Player>(PhotonNetwork.PlayerList);
        _playerLabels.Clear();
        _cachedNames.Clear();
        _cachedDeathStatus.Clear();

        foreach (var human in FindObjectsOfType<Human>())
        {
            if (human.photonView?.Owner != null)
            {
                int actorId = human.photonView.Owner.ActorNumber;
                _cachedNames[actorId] = human.Name;
                _cachedDeathStatus[actorId] = human.Dead;
            }
        }

        foreach (var player in _cachedPlayers)
        {
            _playerLabels[player.ActorNumber] = GeneratePlayerLabel(player);
        }
    }

    private void OnGUI()
    {
        if (!menuOpen)
            return;

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            alignment = TextAnchor.UpperCenter,
            normal = { textColor = Color.white }
        };

        GUI.Label(new Rect(Screen.width / 2 - 200, 20, 400, 40), "MC Menu", titleStyle);

        if (GUI.Button(new Rect(Screen.width - 120, 20, 100, 30), "Close"))
        {
            menuOpen = false;
            ToggleCursor(false);
            return;
        }

        // Show/Hide Saved Locations Button
        if (GUI.Button(new Rect(Screen.width - 240, 60, 180, 30), showSavedLocationPanel ? "Hide Saved Locations" : "Show Saved Locations"))
        {
            showSavedLocationPanel = !showSavedLocationPanel;
        }

        GUI.Label(new Rect(30, 50, 60, 20), "Search:");
        string newSearch = GUI.TextField(new Rect(90, 50, 200, 20), searchFilter);
        if (newSearch != searchFilter)
        {
            searchFilter = newSearch;
            RefreshPlayerList();
        }

        GUILayout.BeginArea(new Rect(30, 80, 300, Screen.height - 150));
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        foreach (var player in _cachedPlayers)
        {
            if (!_playerLabels.TryGetValue(player.ActorNumber, out string label))
                label = GeneratePlayerLabel(player);

            if (!string.IsNullOrEmpty(searchFilter) && !label.ToLower().Contains(searchFilter.ToLower()))
                continue;

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 12 };

            if (GUILayout.Button(label, buttonStyle, GUILayout.Height(22)))
            {
                selectedPlayer = player;
                confirmKick = false;
                confirmBan = false;
            }
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();

        if (GUI.Button(new Rect(30, Screen.height - 60, 100, 30), "Refresh"))
        {
            RefreshPlayerList();
        }


        


        DrawPlayerPanel();

        // Draw saved location panel if toggled
        if (showSavedLocationPanel)
        {
            DrawSavedLocationPanel();
        }

        if (showCustomSpawnPanel)
        {
            GUI.Box(new Rect(50, Screen.height - 400, 300, 250), "Custom Asset Spawner");

            GUI.Label(new Rect(60, Screen.height - 380, 100, 20), "Bundle:");
            customBundleName = GUI.TextField(new Rect(140, Screen.height - 380, 180, 20), customBundleName);

            GUI.Label(new Rect(60, Screen.height - 350, 100, 20), "Prefab:");
            customPrefabName = GUI.TextField(new Rect(140, Screen.height - 350, 180, 20), customPrefabName);

            GUI.Label(new Rect(60, Screen.height - 320, 100, 20), "Position:");
            customPosX = GUI.TextField(new Rect(140, Screen.height - 320, 50, 20), customPosX);
            customPosY = GUI.TextField(new Rect(195, Screen.height - 320, 50, 20), customPosY);
            customPosZ = GUI.TextField(new Rect(250, Screen.height - 320, 50, 20), customPosZ);

            GUI.Label(new Rect(60, Screen.height - 290, 100, 20), "Rotation:");
            customRotX = GUI.TextField(new Rect(140, Screen.height - 290, 50, 20), customRotX);
            customRotY = GUI.TextField(new Rect(195, Screen.height - 290, 50, 20), customRotY);
            customRotZ = GUI.TextField(new Rect(250, Screen.height - 290, 50, 20), customRotZ);

            GUI.Label(new Rect(60, Screen.height - 260, 100, 20), "Layer:");
            customLayer = GUI.TextField(new Rect(140, Screen.height - 260, 180, 20), customLayer);


            if (GUI.Button(new Rect(100, Screen.height - 220, 160, 30), "Spawn"))
            {
                StartCoroutine(SpawnCustomAssetCoroutine());

            }
        }

       
        

        if (selectedMoveTarget != null)
        {
            GUI.Box(new Rect(850, Screen.height - 200, 200, 130), "Move Object");

            GUI.Label(new Rect(860, Screen.height - 180, 30, 20), "X:");
            moveInputX = GUI.TextField(new Rect(890, Screen.height - 180, 80, 20), moveInputX);

            GUI.Label(new Rect(860, Screen.height - 155, 30, 20), "Y:");
            moveInputY = GUI.TextField(new Rect(890, Screen.height - 155, 80, 20), moveInputY);

            GUI.Label(new Rect(860, Screen.height - 130, 30, 20), "Z:");
            moveInputZ = GUI.TextField(new Rect(890, Screen.height - 130, 80, 20), moveInputZ);

            if (GUI.Button(new Rect(860, Screen.height - 100, 100, 25), "Apply"))
            {
                if (float.TryParse(moveInputX, out float x) &&
                    float.TryParse(moveInputY, out float y) &&
                    float.TryParse(moveInputZ, out float z))
                {
                    selectedMoveTarget.transform.position = new Vector3(x, y, z);
                    selectedMoveTarget = null;
                }
            }

            if (GUI.Button(new Rect(965, Screen.height - 100, 70, 25), "Cancel"))
            {
                selectedMoveTarget = null;
            }
        }


    }



    private Dictionary<string, string> inventoryInputs = new Dictionary<string, string>();


    private void DrawPlayerPanel()
    {
        if (selectedPlayer == null) return;

        GUI.Box(new Rect(Screen.width - 550, 100, 500, 700), "Teleport " + GeneratePlayerLabel(selectedPlayer));

        GUI.Label(new Rect(Screen.width - 320, 140, 50, 25), "X:");
        inputX = GUI.TextField(new Rect(Screen.width - 270, 140, 140, 25), inputX);

        GUI.Label(new Rect(Screen.width - 320, 170, 50, 25), "Y:");
        inputY = GUI.TextField(new Rect(Screen.width - 270, 170, 140, 25), inputY);

        GUI.Label(new Rect(Screen.width - 320, 200, 50, 25), "Z:");
        inputZ = GUI.TextField(new Rect(Screen.width - 270, 200, 140, 25), inputZ);

        if (GUI.Button(new Rect(Screen.width - 300, 240, 200, 30), "Teleport Player")) TryTeleportSelectedPlayer();
        if (GUI.Button(new Rect(Screen.width - 300, 280, 200, 30), "Teleport Player's Horse")) TryTeleportHorseToPlayer();
        if (GUI.Button(new Rect(Screen.width - 300, 320, 200, 30), "Kill Player's Horse")) StartCoroutine(TryKillHorse(selectedPlayer));
        if (GUI.Button(new Rect(Screen.width - 300, 360, 200, 30), "Respawn Player's Horse")) TryRespawnHorse();
        if (GUI.Button(new Rect(Screen.width - 300, 400, 200, 30), "Bring Selected Player to Me")) BringPlayerToMC();
        if (GUI.Button(new Rect(Screen.width - 300, 480, 200, 30), "Get Player Coordinates"))
        {
            Human human = FindHumanByPlayer(selectedPlayer);
            if (human != null)
            {
                Vector3 pos = human.Cache.Transform.position;
                inputX = pos.x.ToString("F2");
                inputY = pos.y.ToString("F2");
                inputZ = pos.z.ToString("F2");
            }
        }

        if (GUI.Button(new Rect(Screen.width - 300, 440, 200, 30), "Bring Me to Selected Player")) BringMCToPlayer();
        if (GUI.Button(new Rect(Screen.width - 500, 240, 200, 30), "Revive Player")) TryReviveSelectedPlayer();
        if (GUI.Button(new Rect(Screen.width - 500, 280, 200, 30), "Kill Player")) TryKillSelectedPlayer();
        if (GUI.Button(new Rect(Screen.width - 500, 320, 200, 30), "Full Relocate + Respawn")) TryFullRelocateAndRespawn();

        GUI.Box(new Rect(Screen.width - 260, 70, 250, 25), "Selected: " + GeneratePlayerLabel(selectedPlayer));

        if (GUI.Button(new Rect(Screen.width - 300, 520, 200, 30), confirmKick ? "Are you sure? (Kick)" : "Kick Player"))
        {
            if (confirmKick)
            {
                ChatManager.KickPlayer(selectedPlayer);
                confirmKick = false;
            }
            else confirmKick = true;
        }

        if (GUI.Button(new Rect(Screen.width - 300, 560, 200, 30), confirmBan ? "Are you sure? (Ban)" : "Ban Player"))
        {
            if (confirmBan)
            {
                ChatManager.KickPlayer(selectedPlayer, ban: true);
                confirmBan = false;
            }
            else confirmBan = true;
        }

        if (GUI.Button(new Rect(Screen.width - 300, 640, 200, 30), "Manage Inventory"))
            showInventoryPanel = !showInventoryPanel;

        if (GUI.Button(new Rect(Screen.width - 300, 680, 200, 30), "Manage Stats"))
            showStatsPanel = !showStatsPanel;

        float leftX = Screen.width - 540 - 240;
        float inventoryY = 280;
        float statsY = 80;

        // Inventory Panel
        if (showInventoryPanel)
        {
            Human selectedHuman = FindHumanByPlayer(selectedPlayer);
            if (selectedHuman != null)
            {
                var inv = selectedHuman.GetComponent<HumanInventory>();
                if (inv != null)
                {
                    var itemTypes = inv.GetItemTypes();
                    int boxHeight = 40 + itemTypes.Count * 30;
                    GUI.Box(new Rect(leftX, inventoryY, 230, boxHeight), "Inventory");

                    for (int i = 0; i < itemTypes.Count; i++)
                    {
                        string type = itemTypes[i];
                        int current = inv.GetItemCount(type);
                        float y = inventoryY + 25 + i * 25;

                        GUI.Label(new Rect(leftX + 10, y, 90, 20), $"{type}: {current}");

                        if (!inventoryInputs.ContainsKey(type))
                            inventoryInputs[type] = current.ToString();

                        inventoryInputs[type] = GUI.TextField(new Rect(leftX + 100, y, 50, 20), inventoryInputs[type]);

                        if (GUI.Button(new Rect(leftX + 160, y, 60, 20), "Set"))
                        {
                            int newVal = ParseSafe(inventoryInputs[type]);
                            inv.photonView?.RPC("RPC_SetItemCount", RpcTarget.AllBufferedViaServer, type, newVal);
                        }
                    }
                }
                else
                {
                    GUI.Label(new Rect(leftX, inventoryY + 105, 200, 20), "Inventory not found.");
                }
            }
        }

        // Stats Panel
        if (showStatsPanel)
        {
            Human selectedHuman = FindHumanByPlayer(selectedPlayer);
            if (selectedHuman != null)
            {
                var stats = selectedHuman.Stats;
                GUI.Box(new Rect(leftX, statsY, 230, 200), "Stats");

                GUI.Label(new Rect(leftX + 10, statsY + 25, 100, 20), $"Speed: {stats.Speed}");
                newSpeed = GUI.TextField(new Rect(leftX + 110, statsY + 25, 50, 20), newSpeed);
                if (GUI.Button(new Rect(leftX + 165, statsY + 25, 50, 20), "Set"))
                    selectedHuman.photonView?.RPC("RPC_SetStats", RpcTarget.AllBufferedViaServer,
                        int.Parse(newSpeed), stats.Gas, stats.Ammunition, stats.Acceleration, stats.HorseSpeed);

                GUI.Label(new Rect(leftX + 10, statsY + 50, 100, 20), $"Gas: {stats.Gas}");
                newMaxGas = GUI.TextField(new Rect(leftX + 110, statsY + 50, 50, 20), newMaxGas);
                if (GUI.Button(new Rect(leftX + 165, statsY + 50, 50, 20), "Set"))
                    selectedHuman.photonView?.RPC("RPC_SetStats", RpcTarget.AllBufferedViaServer,
                        stats.Speed, int.Parse(newMaxGas), stats.Ammunition, stats.Acceleration, stats.HorseSpeed);

                GUI.Label(new Rect(leftX + 10, statsY + 75, 100, 20), $"Ammo: {stats.Ammunition}");
                newMaxBlades = GUI.TextField(new Rect(leftX + 110, statsY + 75, 50, 20), newMaxBlades);
                if (GUI.Button(new Rect(leftX + 165, statsY + 75, 50, 20), "Set"))
                    selectedHuman.photonView?.RPC("RPC_SetStats", RpcTarget.AllBufferedViaServer,
                        stats.Speed, stats.Gas, int.Parse(newMaxBlades), stats.Acceleration, stats.HorseSpeed);

                GUI.Label(new Rect(leftX + 10, statsY + 100, 100, 20), $"Accel: {stats.Acceleration}");
                newAcceleration = GUI.TextField(new Rect(leftX + 110, statsY + 100, 50, 20), newAcceleration);
                if (GUI.Button(new Rect(leftX + 165, statsY + 100, 50, 20), "Set"))
                    selectedHuman.photonView?.RPC("RPC_SetStats", RpcTarget.AllBufferedViaServer,
                        stats.Speed, stats.Gas, stats.Ammunition, int.Parse(newAcceleration), stats.HorseSpeed);

                GUI.Label(new Rect(leftX + 10, statsY + 125, 100, 20), $"HSpeed: {stats.HorseSpeed}");
                newHorseSpeed = GUI.TextField(new Rect(leftX + 110, statsY + 125, 50, 20), newHorseSpeed);
                if (GUI.Button(new Rect(leftX + 165, statsY + 125, 50, 20), "Set"))
                    selectedHuman.photonView?.RPC("RPC_SetStats", RpcTarget.AllBufferedViaServer,
                        stats.Speed, stats.Gas, stats.Ammunition, stats.Acceleration, float.Parse(newHorseSpeed));
            }

            

            else
            {
                GUI.Label(new Rect(leftX, statsY, 200, 20), "Stats not found.");
            }
        }
    }












    private IEnumerator TryKillHorse(Player targetPlayer)
    {
        Horse targetHorse = null;

        foreach (var horse in FindObjectsOfType<Horse>())
        {
            if (horse.photonView != null && horse.photonView.Owner != null &&
                horse.photonView.Owner.ActorNumber == targetPlayer.ActorNumber)
            {
                targetHorse = horse;
                break;
            }
        }

        if (targetHorse == null)
        {
            Debug.LogWarning($"[HorseKill] No horse found for player {targetPlayer.ActorNumber}");
            yield break;
        }

        PhotonView horseView = targetHorse.photonView;

        // Request ownership if not already owned by the MC
        if (!horseView.IsMine)
        {
            horseView.TransferOwnership(PhotonNetwork.LocalPlayer);
        }

        // Wait for ownership confirmation (up to 2s)
        float elapsed = 0f;
        while (!horseView.IsMine && elapsed < 2f)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        if (horseView.IsMine)
        {
            PhotonNetwork.Destroy(horseView.gameObject);
            Debug.Log($"[HorseKill] Successfully destroyed horse owned by player {targetPlayer.ActorNumber}");
        }
        else
        {
            Debug.LogWarning($"[HorseKill] Could not gain ownership of horse for player {targetPlayer.ActorNumber}");
        }
    }



    private void TryRespawnHorse()
    {
        if (selectedPlayer == null || !PhotonNetwork.IsMasterClient)
            return;

        StartCoroutine(RespawnHorseCoroutine(selectedPlayer));
    }

    private IEnumerator RespawnHorseCoroutine(Player targetPlayer)
    {
        Human humanOwner = FindHumanByPlayer(targetPlayer);
        if (humanOwner == null)
            yield break;

        Vector3 spawnPos = humanOwner.Cache.Transform.position + Vector3.right * 2f;

        StartCoroutine(TryKillHorse(selectedPlayer));
        // Clean up old horse

        GameObject horseObj = PhotonNetwork.Instantiate("Characters/Horse/Prefabs/Horse", spawnPos, Quaternion.identity);
        yield return new WaitUntil(() => horseObj.GetComponent<PhotonView>().ViewID != 0);
        yield return new WaitForSeconds(0.25f); // Allow network propagation

        PhotonView horseView = horseObj.GetComponent<PhotonView>();
        horseView.TransferOwnership(targetPlayer);

        StartCoroutine(WaitForOwnershipAndInit(horseView, targetPlayer));
    }


    private IEnumerator WaitForOwnershipAndInit(PhotonView horseView, Player targetPlayer)
    {
        float timeout = 2f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (horseView != null)
            {
                if (horseView.Owner != null && horseView.Owner.ActorNumber == targetPlayer.ActorNumber)
                {
                    // Ownership confirmed
                    horseView.RPC("RPC_SetHorseOwner", RpcTarget.All, targetPlayer.ActorNumber);



                    // Now init horse
                    Horse horse = horseView.GetComponent<Horse>();
                    Human human = FindHumanByPlayer(targetPlayer);
                    if (horse != null && human != null)
                    {



                    }

                    yield break;
                }
                else
                {
                    horseView.TransferOwnership(targetPlayer);
                }
            }

            yield return new WaitForSeconds(0.2f);
            elapsed += 0.2f;
        }

        Debug.LogWarning($"[HorseRespawn] Failed to confirm ownership for player {targetPlayer.ActorNumber}");
    }




    private string GeneratePlayerLabel(Player player)
    {
        string name = _cachedNames.TryGetValue(player.ActorNumber, out var val) ? val : player.NickName;
        bool isDead = _cachedDeathStatus.TryGetValue(player.ActorNumber, out var dead) && dead;

        string label = "";
        if (isDead)
            label += "{X} ";

        label += name;

        if (player.IsMasterClient)
            label += " (MC)";

        label += $" {{{player.ActorNumber}}}";
        return label;
    }


    private void TryTeleportSelectedPlayer()
    {
        if (float.TryParse(inputX, out float x) &&
            float.TryParse(inputY, out float y) &&
            float.TryParse(inputZ, out float z))
        {
            StartCoroutine(RepeatTeleportOverTime(new Vector3(x, y, z), selectedPlayer.ActorNumber));
        }
    }

    private IEnumerator RepeatTeleportOverTime(Vector3 targetPosition, int targetActor)
    {
        float duration = 3f;
        float interval = 0.1f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            foreach (var human in FindObjectsOfType<Human>())
            {
                if (human.photonView != null && human.photonView.Owner != null &&
                    human.photonView.Owner.ActorNumber == targetActor)
                {
                    if (human.MountState == HumanMountState.Horse)
                        human.Unmount(true);

                    human.photonView.RPC("RPC_Teleport", human.photonView.Owner, targetPosition);
                    break; // only teleport one matching human
                }
            }

            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }
    }


    private void TryTeleportHorseToPlayer()
    {
        StartCoroutine(RepeatHorseTeleportOverTime(selectedPlayer.ActorNumber));
    }

    private IEnumerator RepeatHorseTeleportOverTime(int targetActor)
    {
        float duration = 3f;
        float interval = 0.1f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            foreach (var horse in FindObjectsOfType<Horse>())
            {
                if (horse.photonView != null && horse.photonView.Owner != null &&
                    horse.photonView.Owner.ActorNumber == targetActor)
                {
                    horse.photonView.RPC("RPC_TeleportToHuman", horse.photonView.Owner);
                    break; // Only teleport one matching horse
                }
            }

            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }
    }


    private void BringPlayerToMC()
    {
        Human mc = FindLocalHuman();
        if (mc == null) return;

        foreach (var human in FindObjectsOfType<Human>())
        {
            if (human.photonView != null && human.photonView.Owner != null &&
                human.photonView.Owner.ActorNumber == selectedPlayer.ActorNumber)
            {
                if (human.MountState == HumanMountState.Horse)
                    human.Unmount(true);

                human.photonView.RPC("RPC_Teleport", human.photonView.Owner, mc.Cache.Transform.position);
                break;
            }
        }
    }

    private void BringMCToPlayer()
    {
        Human mc = FindLocalHuman();
        if (mc == null) return;

        foreach (var human in FindObjectsOfType<Human>())
        {
            if (human.photonView != null && human.photonView.Owner != null &&
                human.photonView.Owner.ActorNumber == selectedPlayer.ActorNumber)
            {
                mc.photonView.RPC("RPC_Teleport", mc.photonView.Owner, human.Cache.Transform.position);
                break;
            }
        }
    }

    private void TryReviveSelectedPlayer()
    {
        if (selectedPlayer != null)
        {
            RPCManager.PhotonView.RPC("SpawnPlayerRPC", selectedPlayer, new object[] { false });
            ChatManager.SendChat("You have been revived by master client.", selectedPlayer, ChatTextColor.System);
        }
    }

    private void TryKillSelectedPlayer()
    {
        foreach (var human in FindObjectsOfType<Human>())
        {
            if (human.photonView != null && human.photonView.Owner != null &&
                human.photonView.Owner.ActorNumber == selectedPlayer.ActorNumber)
            {
                if (!human.Dead)
                    human.GetHit("Smited", 400, "Thunderspear", "");
                break;
            }
        }
    }
    private void TryFullRelocateAndRespawn()
    {
        if (selectedPlayer == null)
            return;

        if (!float.TryParse(inputX, out float x) ||
            !float.TryParse(inputY, out float y) ||
            !float.TryParse(inputZ, out float z))
            return;

        Vector3 targetPos = new Vector3(x, y, z);

        // 1. Kill human if alive
        StartCoroutine(TryKillHorse(selectedPlayer));

        foreach (var human in FindObjectsOfType<Human>())
        {
            if (human.photonView != null && human.photonView.Owner == selectedPlayer)
            {
                if (!human.Dead)
                    human.GetHit("MCReset", 9999, "Thunderspear", "");
                break;
            }
        }

        // 2. Kill horse
        StartCoroutine(TryKillHorse(selectedPlayer));


        // 3. Revive human after a short delay
        StartCoroutine(DelayedRelocateRevive(selectedPlayer, targetPos));
        StartCoroutine(TryKillHorse(selectedPlayer));

    }

    private IEnumerator DelayedRelocateRevive(Player targetPlayer, Vector3 targetPos)
    {
        yield return new WaitForSeconds(0.2f); // Let death register

        // Revive player
        RPCManager.PhotonView.RPC("SpawnPlayerRPC", targetPlayer, new object[] { false });
        ChatManager.SendChat("You have been fully relocated by master client.", targetPlayer, ChatTextColor.System);

        // Wait a bit for the player to respawn
        yield return new WaitForSeconds(0.5f);

        // Teleport player multiple times for sync
        StartCoroutine(RepeatTeleportOverTime(targetPos, targetPlayer.ActorNumber));

        // === Match RespawnHorse ===
        Human humanOwner = FindHumanByPlayer(targetPlayer);
        if (humanOwner == null)
            yield break;

        Vector3 spawnPosition = targetPos + Vector3.right * 2f;

        StartCoroutine(TryKillHorse(selectedPlayer));
        // Clean up any lingering horse

        GameObject horseObj = PhotonNetwork.Instantiate("Characters/Horse/Prefabs/Horse", spawnPosition, Quaternion.identity);
        PhotonView horseView = horseObj.GetComponent<PhotonView>();

        horseView.TransferOwnership(targetPlayer);
        StartCoroutine(EnsureHorseOwnershipAndLink(horseView, targetPlayer.ActorNumber));
    }



    private Human FindLocalHuman()
    {
        foreach (var human in FindObjectsOfType<Human>())
        {
            if (human != null && human.IsMine())
                return human;
        }
        return null;
    }
    private System.Collections.IEnumerator InitHorseAfterDelay(Horse horse, Human owner)
    {
        yield return new WaitForSeconds(0.1f); // Wait for Awake
        if (horse != null && owner != null)
            horse.Init(owner); // This links the horse to the human
    }

    private int ParseSafe(string input)
    {
        return int.TryParse(input, out int val) ? Mathf.Max(0, val) : 0;
    }

    private Human FindHumanByPlayer(Player player)
    {
        foreach (var h in FindObjectsOfType<Human>())
        {
            if (h.photonView != null && h.photonView.Owner == player)
                return h;
        }
        return null;
    }
    private IEnumerator EnsureHorseOwnershipAndLink(PhotonView horseView, int targetActor)
    {
        float timeout = 2f;
        float elapsed = 0f;
        bool linked = false;

        while (elapsed < timeout)
        {
            if (horseView != null)
            {
                if (horseView.Owner != null && horseView.Owner.ActorNumber == targetActor)
                {
                    horseView.RPC("RPC_SetHorseOwner", horseView.Owner, targetActor);
                    linked = true;
                    break;
                }
                else
                {
                    horseView.TransferOwnership(targetActor);
                }
            }

            elapsed += 0.2f;
            yield return new WaitForSeconds(0.2f);
        }

        if (!linked)
        {
            Debug.LogWarning($"[HorseRevive] Failed to link horse to player {targetActor} after timeout.");
        }
        else
        {
            Debug.Log($"[HorseRevive] Successfully linked horse to player {targetActor}.");
        }
    }

    private string SaveFilePath => Path.Combine(Application.persistentDataPath, "saved_locations.json");

    private void SaveLocations()
    {
        var container = new SavedLocationList { Locations = savedLocations };
        string json = JsonUtility.ToJson(container, true);
        File.WriteAllText(SaveFilePath, json);
    }

    private void LoadLocations()
    {
        if (File.Exists(SaveFilePath))
        {
            string json = File.ReadAllText(SaveFilePath);
            var container = JsonUtility.FromJson<SavedLocationList>(json);
            savedLocations = container?.Locations ?? new List<SavedLocation>();
        }
    }

    private void DrawSavedLocationPanel()
    {
        GUI.Box(new Rect(350, 80, 320, 520), "Saved Locations");

        GUI.Label(new Rect(360, 110, 50, 20), "Title:");
        newTitle = GUI.TextField(new Rect(410, 110, 180, 20), newTitle);

        if (GUI.Button(new Rect(600, 110, 20, 20), "+"))
        {
            if (float.TryParse(inputX, out float x) &&
                float.TryParse(inputY, out float y) &&
                float.TryParse(inputZ, out float z) &&
                !string.IsNullOrWhiteSpace(newTitle))
            {
                savedLocations.Add(new SavedLocation { Title = newTitle, X = x, Y = y, Z = z });
                SaveLocations();
                newTitle = "";
            }
        }

        GUI.Label(new Rect(360, 140, 60, 20), "Search:");
        searchQuery = GUI.TextField(new Rect(420, 140, 180, 20), searchQuery);

        savedScroll = GUI.BeginScrollView(new Rect(360, 170, 270, 400), savedScroll, new Rect(0, 0, 250, savedLocations.Count * 30));

        int yOffset = 0;
        foreach (var loc in savedLocations)
        {
            if (!string.IsNullOrEmpty(searchQuery) && !loc.Title.ToLower().Contains(searchQuery.ToLower()))
                continue;

            if (GUI.Button(new Rect(0, yOffset, 180, 25), loc.Title))
            {
                inputX = loc.X.ToString();
                inputY = loc.Y.ToString();
                inputZ = loc.Z.ToString();
            }

            if (deleteConfirmTitle == loc.Title)
            {
                if (GUI.Button(new Rect(185, yOffset, 60, 25), "Sure?"))
                {
                    savedLocations.Remove(loc);
                    SaveLocations();
                    deleteConfirmTitle = null;
                    break;
                }
            }
            else
            {
                if (GUI.Button(new Rect(185, yOffset, 60, 25), "X"))
                {
                    deleteConfirmTitle = loc.Title;
                }
            }

            yOffset += 30;
        }

        GUI.EndScrollView();
    }



    private IEnumerator SpawnCustomAssetCoroutine()
    {
        if (!float.TryParse(customPosX, out float x) ||
            !float.TryParse(customPosY, out float y) ||
            !float.TryParse(customPosZ, out float z) ||
            !float.TryParse(customRotX, out float rx) ||
            !float.TryParse(customRotY, out float ry) ||
            !float.TryParse(customRotZ, out float rz))
        {
            ChatManager.AddLine("Invalid position or rotation input.", ChatTextColor.System);
            yield break;
        }

        if (!AssetBundleManager.LoadedBundle(customBundleName))
        {
            yield return StartCoroutine(AssetBundleManager.LoadBundle(customBundleName, "", true));

            if (!AssetBundleManager.LoadedBundle(customBundleName))
            {
                ChatManager.AddLine("Failed to load bundle: " + customBundleName, ChatTextColor.System);
                yield break;
            }
        }

        GameObject prefab = AssetBundleManager.LoadAsset(customBundleName, customPrefabName) as GameObject;
        if (prefab == null)
        {
            ChatManager.AddLine("Prefab not found: " + customPrefabName, ChatTextColor.System);
            yield break;
        }

        Vector3 pos = new Vector3(x, y, z);
        Quaternion rot = Quaternion.Euler(rx, ry, rz);
        GameObject go = Instantiate(prefab, pos, rot);
        spawnedCustomAssets.Add(go);


        int parsedLayer = 23;
        int.TryParse(customLayer, out parsedLayer); // fallback to 23 if invalid
        SetLayerRecursively(go, parsedLayer);


        int viewID = -1;
        PhotonView view = go.GetComponent<PhotonView>();
        if (view != null)
        {
            viewID = PhotonNetwork.AllocateViewID(true);
            view.ViewID = viewID;
        }

        photonView.RPC("RPC_SpawnAssetLocally", RpcTarget.Others, customBundleName, customPrefabName, pos, new Vector3(rx, ry, rz), viewID);
        ChatManager.AddLine($"[MC] Spawned: {customPrefabName} from {customBundleName}", ChatTextColor.System);


    }

    [PunRPC]
    private void RPC_SpawnAssetLocally(string bundleName, string prefabName, Vector3 pos, Vector3 rotEuler, int viewID)
    {
        StartCoroutine(SpawnAfterLoad(bundleName, prefabName, pos, Quaternion.Euler(rotEuler), viewID));
    }

    private IEnumerator SpawnAfterLoad(string bundleName, string prefabName, Vector3 pos, Quaternion rot, int viewID)
    {
        if (!AssetBundleManager.LoadedBundle(bundleName))
            yield return AssetBundleManager.LoadBundle(bundleName, "", true);

        GameObject prefab = AssetBundleManager.LoadAsset(bundleName, prefabName) as GameObject;
        if (prefab == null)
        {
            Debug.LogWarning($"[SpawnAfterLoad] Prefab not found: {prefabName} in bundle: {bundleName}");
            yield break;
        }

        GameObject go = Instantiate(prefab, pos, rot);
        spawnedCustomAssets.Add(go);


        int parsedLayer = 23;
        int.TryParse(customLayer, out parsedLayer); // Use same fallback
        SetLayerRecursively(go, parsedLayer);


        if (viewID > 0)
        {
            PhotonView view = go.GetComponent<PhotonView>();
            if (view != null)
                view.ViewID = viewID;
        }
        
        


    }


    private void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

}





