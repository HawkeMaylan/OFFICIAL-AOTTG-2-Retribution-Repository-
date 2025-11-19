using UnityEngine;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using ApplicationManagers;
using UI;
using GameManagers;
using Settings;
using System.IO;
using System.Linq;



public class CustomAssetMenu : MonoBehaviourPun
{
    private bool menuOpen = false;

    private string bundleName = "";
    private string prefabName = "";
    private string posX = "0", posY = "0", posZ = "0";
    private string rotX = "0", rotY = "0", rotZ = "0";
    private string layer = "23";

    private List<GameObject> spawnedAssets = new List<GameObject>();
    private GameObject selectedObject = null;
    private string moveX = "0", moveY = "0", moveZ = "0";
    private string moveRotX = "0", moveRotY = "0", moveRotZ = "0";

    private List<string> buildablePrefabNames = new List<string>();
    private int selectedBuildableIndex = 0;
    private Vector2 buildableScroll = Vector2.zero;
    private bool buildablesLoaded = false;

    // Build system integration
    private BuildSystem buildSystem;
    private string saveFileName = "buildables_save.json";
    private bool showBuildSystemPanel = true;

    // JSON file browser
    private List<string> savedJsonFiles = new List<string>();
    private int selectedJsonIndex = -1;
    private Vector2 jsonFileScroll = Vector2.zero;
    private bool jsonFilesLoaded = false;

    // Day Night Cycle Control
    private GameObject dayNightManager;
    private object dayNightCycle; 
    private string timeOfDayInput = "0.5";

    private void Start()
    {
        buildSystem = FindObjectOfType<BuildSystem>();
        FindDayNightManager();
    }

    private void FindDayNightManager()
    {
        dayNightManager = GameObject.Find("HawkDayNightManager(Clone)");
        if (dayNightManager != null)
        {
            // Get all components and find the one with the right name
            Component[] components = dayNightManager.GetComponents<Component>();
            foreach (Component comp in components)
            {
                if (comp.GetType().Name == "SimpleDayNightCycle")
                {
                    dayNightCycle = comp;
                    break;
                }
            }
        }
    }

    private void Update()
    {
        if (SettingsManager.InputSettings.General.CustomAssetMenu.GetKeyDown() && PhotonNetwork.IsMasterClient)
        {
            menuOpen = !menuOpen;
            Cursor.visible = menuOpen;
            Cursor.lockState = menuOpen ? CursorLockMode.None : CursorLockMode.Locked;

            // Refresh JSON file list when opening menu
            if (menuOpen)
            {
                RefreshJsonFileList();
                // Refresh day night cycle reference
                if (dayNightManager == null)
                    FindDayNightManager();
                else if (dayNightCycle != null)
                {
                    // Use reflection to get the timeOfDay value
                    System.Type type = dayNightCycle.GetType();
                    System.Reflection.PropertyInfo timeProp = type.GetProperty("timeOfDay");
                    if (timeProp != null)
                    {
                        float currentTime = (float)timeProp.GetValue(dayNightCycle);
                        timeOfDayInput = currentTime.ToString("F3");
                    }
                }
            }
        }
    }

    private void OnGUI()
    {
        if (!menuOpen || !PhotonNetwork.IsMasterClient) return;

        // Main menu area
        GUI.Box(new Rect(20, 20, 320, 340), "Custom Asset Spawner");

        // Existing asset spawning UI...
        GUI.Label(new Rect(30, 50, 60, 20), "Bundle:");
        bundleName = GUI.TextField(new Rect(90, 50, 230, 20), bundleName);

        GUI.Label(new Rect(30, 75, 60, 20), "Prefab:");
        prefabName = GUI.TextField(new Rect(90, 75, 230, 20), prefabName);

        GUI.Label(new Rect(30, 100, 60, 20), "Position:");
        posX = GUI.TextField(new Rect(90, 100, 60, 20), posX);
        posY = GUI.TextField(new Rect(155, 100, 60, 20), posY);
        posZ = GUI.TextField(new Rect(220, 100, 60, 20), posZ);

        GUI.Label(new Rect(30, 125, 60, 20), "Rotation:");
        rotX = GUI.TextField(new Rect(90, 125, 60, 20), rotX);
        rotY = GUI.TextField(new Rect(155, 125, 60, 20), rotY);
        rotZ = GUI.TextField(new Rect(220, 125, 60, 20), rotZ);

        GUI.Label(new Rect(30, 150, 60, 20), "Layer:");
        layer = GUI.TextField(new Rect(90, 150, 230, 20), layer);

        if (GUI.Button(new Rect(90, 180, 140, 30), "Spawn Asset"))
        {
            if (float.TryParse(posX, out float x) &&
                float.TryParse(posY, out float y) &&
                float.TryParse(posZ, out float z) &&
                float.TryParse(rotX, out float rx) &&
                float.TryParse(rotY, out float ry) &&
                float.TryParse(rotZ, out float rz) &&
                int.TryParse(layer, out int parsedLayer))
            {
                StartCoroutine(SpawnAsset(bundleName, prefabName, new Vector3(x, y, z), new Vector3(rx, ry, rz), parsedLayer));
            }
        }

        // Day Night Cycle Panel
        GUI.Box(new Rect(20, 520, 320, 80), "Day Night Cycle Control"); // Increased height

        if (dayNightCycle != null)
        {
            GUI.Label(new Rect(30, 550, 80, 20), "Time (0-1):");
            timeOfDayInput = GUI.TextField(new Rect(110, 550, 80, 20), timeOfDayInput);

            if (GUI.Button(new Rect(200, 550, 120, 20), "Set Time"))
            {
                if (float.TryParse(timeOfDayInput, out float time))
                {
                    time = Mathf.Clamp01(time);
                    // Use reflection to call SetTimeOfDay method with 2 parameters
                    System.Type type = dayNightCycle.GetType();
                    System.Reflection.MethodInfo method = type.GetMethod("SetTimeOfDay");
                    if (method != null)
                    {
                        // Pass both parameters: time and sync (true)
                        method.Invoke(dayNightCycle, new object[] { time, true });
                        AddChatMessage($"[MC] Set time to: {time:F3}");
                    }
                    else
                    {
                        Debug.LogError("SetTimeOfDay method not found!");
                    }
                }
            }

            // Refresh button
            if (GUI.Button(new Rect(30, 580, 120, 20), "Refresh"))
            {
                FindDayNightManager();
                AddChatMessage("[MC] Refreshed DayNightManager reference");
            }
        }
        else
        {
            GUI.Label(new Rect(30, 550, 200, 20), "HawkDayNightManager not found!");
            if (GUI.Button(new Rect(30, 580, 120, 20), "Find Manager"))
            {
                FindDayNightManager();
                if (dayNightCycle != null)
                    AddChatMessage("[MC] Found DayNightManager!");
                else
                    AddChatMessage("[MC] DayNightManager still not found");
            }
        }

        // Build System Panel
        GUI.Box(new Rect(360, 20, 300, 500), "Build System Manager");

        if (buildSystem != null)
        {
            // Buildables count
            GUI.Label(new Rect(370, 50, 200, 20), $"Tracked Buildables: {buildSystem.GetBuildableCount()}");

            // Save/Load controls
            GUI.Label(new Rect(370, 80, 60, 20), "Save File:");
            saveFileName = GUI.TextField(new Rect(440, 80, 200, 20), saveFileName);

            if (GUI.Button(new Rect(370, 110, 130, 30), "Save Buildables"))
            {
                buildSystem.SaveBuildablesToJson(saveFileName);
                RefreshJsonFileList(); // Refresh list after saving
            }

            if (GUI.Button(new Rect(510, 110, 130, 30), "Load Buildables"))
            {
                buildSystem.LoadBuildablesFromJson(saveFileName);
            }

            if (GUI.Button(new Rect(370, 150, 270, 30), "Clear All Buildables"))
            {
                buildSystem.ClearAllBuildablesMaster();
            }

            // JSON File Browser
            GUI.Label(new Rect(370, 190, 200, 20), "Saved Buildable Files:");

            // Refresh button for JSON files
            if (GUI.Button(new Rect(570, 190, 80, 20), "Refresh"))
            {
                RefreshJsonFileList();
            }

            // JSON file list
            jsonFileScroll = GUI.BeginScrollView(
                new Rect(370, 215, 280, 120),
                jsonFileScroll,
                new Rect(0, 0, 260, savedJsonFiles.Count * 25)
            );

            for (int i = 0; i < savedJsonFiles.Count; i++)
            {
                bool isSelected = i == selectedJsonIndex;
                string displayName = Path.GetFileName(savedJsonFiles[i]);

                // Highlight selected file
                if (isSelected)
                {
                    GUI.backgroundColor = Color.blue;
                }

                if (GUI.Button(new Rect(0, i * 25, 260, 25), displayName))
                {
                    selectedJsonIndex = i;
                    saveFileName = Path.GetFileName(savedJsonFiles[i]);
                }

                if (isSelected)
                {
                    GUI.backgroundColor = Color.white;
                }
            }

            GUI.EndScrollView();

            // Load selected file button
            if (selectedJsonIndex >= 0 && selectedJsonIndex < savedJsonFiles.Count)
            {
                if (GUI.Button(new Rect(370, 345, 280, 25), "Load Selected File"))
                {
                    string selectedFile = Path.GetFileName(savedJsonFiles[selectedJsonIndex]);
                    buildSystem.LoadBuildablesFromJson(selectedFile);
                }
            }

            // Buildable spawning section
            if (!buildablesLoaded)
                LoadBuildablePrefabs();

            GUI.Label(new Rect(370, 380, 200, 20), "Spawn Buildable:");

            buildableScroll = GUI.BeginScrollView(
                new Rect(370, 405, 280, 120),
                buildableScroll,
                new Rect(0, 0, 260, buildablePrefabNames.Count * 25)
            );

            for (int i = 0; i < buildablePrefabNames.Count; i++)
            {
                if (GUI.Button(new Rect(0, i * 25, 260, 25), buildablePrefabNames[i]))
                {
                    selectedBuildableIndex = i;
                }
            }

            GUI.EndScrollView();

            GUI.Label(new Rect(370, 530, 200, 20), "Selected: " + buildablePrefabNames[selectedBuildableIndex]);

            if (GUI.Button(new Rect(370, 555, 270, 30), "Spawn Buildable at Position"))
            {
                if (float.TryParse(posX, out float x) &&
                    float.TryParse(posY, out float y) &&
                    float.TryParse(posZ, out float z) &&
                    float.TryParse(rotX, out float rx) &&
                    float.TryParse(rotY, out float ry) &&
                    float.TryParse(rotZ, out float rz) &&
                    int.TryParse(layer, out int parsedLayer))
                {
                    string buildableName = buildablePrefabNames[selectedBuildableIndex];
                    SpawnBuildable(buildableName, new Vector3(x, y, z), new Vector3(rx, ry, rz), parsedLayer);
                }
            }

            // Quick spawn at camera position
            if (GUI.Button(new Rect(370, 590, 270, 30), "Spawn Buildable at Camera"))
            {
                Transform cameraTransform = Camera.main.transform;
                Vector3 spawnPos = cameraTransform.position + cameraTransform.forward * 3f;
                string buildableName = buildablePrefabNames[selectedBuildableIndex];
                SpawnBuildable(buildableName, spawnPos, Quaternion.identity.eulerAngles, int.Parse(layer));
            }
        }
        else
        {
            GUI.Label(new Rect(370, 50, 280, 40), "BuildSystem not found in scene!");
        }

        // JSON Files Panel
        GUI.Box(new Rect(680, 20, 300, 400), "Saved Buildable Files");

        if (savedJsonFiles.Count == 0)
        {
            GUI.Label(new Rect(690, 50, 280, 30), "No saved buildable files found.");
            if (GUI.Button(new Rect(690, 80, 280, 25), "Refresh Files"))
            {
                RefreshJsonFileList();
            }
        }
        else
        {
            // File list with more details
            Vector2 detailedScroll = GUI.BeginScrollView(
                new Rect(690, 50, 280, 300),
                Vector2.zero,
                new Rect(0, 0, 260, savedJsonFiles.Count * 60)
            );

            for (int i = 0; i < savedJsonFiles.Count; i++)
            {
                string filePath = savedJsonFiles[i];
                string fileName = Path.GetFileName(filePath);
                FileInfo fileInfo = new FileInfo(filePath);
                string fileSize = FormatFileSize(fileInfo.Length);
                string modified = fileInfo.LastWriteTime.ToString("MM/dd/yy HH:mm");

                // File entry background
                GUI.Box(new Rect(0, i * 60, 260, 58), "");

                // File name (clickable)
                if (GUI.Button(new Rect(5, i * 60 + 5, 250, 20), fileName))
                {
                    selectedJsonIndex = i;
                    saveFileName = fileName;
                }

                // File details
                GUI.Label(new Rect(5, i * 60 + 28, 250, 15), $"Size: {fileSize} | Modified: {modified}");

                // Quick load button
                if (GUI.Button(new Rect(5, i * 60 + 45, 120, 12), "Quick Load"))
                {
                    buildSystem.LoadBuildablesFromJson(fileName);
                }

                // Delete button
                if (GUI.Button(new Rect(130, i * 60 + 45, 120, 12), "Delete File"))
                {
                    DeleteJsonFile(fileName);
                }
            }

            GUI.EndScrollView();

            // Selected file info
            if (selectedJsonIndex >= 0 && selectedJsonIndex < savedJsonFiles.Count)
            {
                string selectedFilePath = savedJsonFiles[selectedJsonIndex];
                FileInfo selectedFileInfo = new FileInfo(selectedFilePath);

                GUI.Label(new Rect(690, 360, 280, 20), $"Selected: {Path.GetFileName(selectedFilePath)}");
                GUI.Label(new Rect(690, 380, 280, 20), $"Size: {FormatFileSize(selectedFileInfo.Length)}");

                if (GUI.Button(new Rect(690, 405, 280, 25), "Load Selected Buildables"))
                {
                    buildSystem.LoadBuildablesFromJson(Path.GetFileName(selectedFilePath));
                }
            }
        }

        // Existing spawned assets management UI...
        GUI.Box(new Rect(680, 430, 300, 200), "Spawned Assets");

        for (int i = 0; i < spawnedAssets.Count; i++)
        {
            var obj = spawnedAssets[i];
            if (obj == null) continue;

            GUI.Label(new Rect(690, 460 + 25 * i, 150, 20), obj.name);

            if (GUI.Button(new Rect(840, 460 + 25 * i, 60, 20), "Move"))
            {
                selectedObject = obj;
                var pos = obj.transform.position;
                moveX = pos.x.ToString();
                moveY = pos.y.ToString();
                moveZ = pos.z.ToString();
                var rot = obj.transform.rotation.eulerAngles;
                moveRotX = rot.x.ToString();
                moveRotY = rot.y.ToString();
                moveRotZ = rot.z.ToString();
            }

            if (GUI.Button(new Rect(905, 460 + 25 * i, 60, 20), "Delete"))
            {
                obj.GetComponent<CustomAssetHelper>()?.Delete();
            }
        }

        // Move asset panel (existing code)...
        if (selectedObject != null)
        {
            GUI.Box(new Rect(20, 380, 320, 130), "Move Asset");

            GUI.Label(new Rect(30, 410, 30, 20), "X:");
            moveX = GUI.TextField(new Rect(60, 410, 60, 20), moveX);
            GUI.Label(new Rect(130, 410, 30, 20), "Y:");
            moveY = GUI.TextField(new Rect(160, 410, 60, 20), moveY);
            GUI.Label(new Rect(230, 410, 30, 20), "Z:");
            moveZ = GUI.TextField(new Rect(260, 410, 60, 20), moveZ);
            GUI.Label(new Rect(30, 470, 30, 20), "Rot X:");
            moveRotX = GUI.TextField(new Rect(60, 470, 60, 20), moveRotX);
            GUI.Label(new Rect(130, 470, 30, 20), "Y:");
            moveRotY = GUI.TextField(new Rect(160, 470, 60, 20), moveRotY);
            GUI.Label(new Rect(230, 470, 30, 20), "Z:");
            moveRotZ = GUI.TextField(new Rect(260, 470, 60, 20), moveRotZ);

            if (GUI.Button(new Rect(60, 440, 100, 25), "Apply"))
            {
                if (float.TryParse(moveX, out float mx) &&
                    float.TryParse(moveY, out float my) &&
                    float.TryParse(moveZ, out float mz) &&
                    float.TryParse(moveRotX, out float rx) &&
                    float.TryParse(moveRotY, out float ry) &&
                    float.TryParse(moveRotZ, out float rz))
                {
                    var helper = selectedObject.GetComponent<CustomAssetHelper>();
                    if (helper != null)
                    {
                        helper.Move(new Vector3(mx, my, mz), new Vector3(rx, ry, rz));
                    }
                    selectedObject = null;
                }
            }

            if (GUI.Button(new Rect(170, 440, 100, 25), "Cancel"))
            {
                selectedObject = null;
            }
        }
    }

    private void RefreshJsonFileList()
    {
        savedJsonFiles.Clear();
        string persistentDataPath = Application.persistentDataPath;

        try
        {
            // Get all JSON files in persistent data path
            string[] allJsonFiles = Directory.GetFiles(persistentDataPath, "*.json");

            foreach (string filePath in allJsonFiles)
            {
                // Optional: Filter only buildable files if they have a specific pattern
                // For now, include all JSON files
                savedJsonFiles.Add(filePath);
            }

            // Sort by modification date (newest first)
            savedJsonFiles = savedJsonFiles
                .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                .ToList();

            jsonFilesLoaded = true;

            // Auto-select the first file if none selected
            if (savedJsonFiles.Count > 0 && selectedJsonIndex == -1)
            {
                selectedJsonIndex = 0;
                saveFileName = Path.GetFileName(savedJsonFiles[0]);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error refreshing JSON file list: {e.Message}");
        }
    }

    private void DeleteJsonFile(string fileName)
    {
        try
        {
            string filePath = Path.Combine(Application.persistentDataPath, fileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                RefreshJsonFileList(); // Refresh the list

                // Add chat message
                AddChatMessage($"[System] Deleted buildable file: {fileName}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error deleting JSON file: {e.Message}");
            AddChatMessage($"[System] Error deleting file: {fileName}");
        }
    }

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double len = bytes;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private void AddChatMessage(string message)
    {
        try
        {
            ChatManager.AddLine(message, ChatTextColor.System);
        }
        catch
        {
            Debug.Log($"CHAT: {message}");
        }
    }

    private void LoadBuildablePrefabs()
    {
        buildablePrefabNames.Clear();
        GameObject[] allPrefabs = Resources.LoadAll<GameObject>("Buildables");
        foreach (GameObject prefab in allPrefabs)
        {
            string fullPath = "Buildables/" + prefab.name;
            GameObject check = Resources.Load<GameObject>(fullPath);
            if (check != null)
            {
                buildablePrefabNames.Add(prefab.name);
            }
        }
        buildablesLoaded = true;
    }

    private IEnumerator SpawnAsset(string bundle, string prefab, Vector3 position, Vector3 rotation, int layer)
    {
        if (!AssetBundleManager.LoadedBundle(bundle))
            yield return AssetBundleManager.LoadBundle(bundle, "", true);

        GameObject prefabObj = AssetBundleManager.LoadAsset(bundle, prefab) as GameObject;
        if (prefabObj == null)
        {
            AddChatMessage($"[MC] Prefab not found: {prefab}");
            yield break;
        }

        GameObject go = Instantiate(prefabObj, position, Quaternion.Euler(rotation));
        PhotonView view = go.AddComponent<PhotonView>();
        view.ViewID = PhotonNetwork.AllocateViewID(true);
        SetLayerRecursively(go, layer);
        go.AddComponent<CustomAssetHelper>();
        spawnedAssets.Add(go);

        PhotonView rpcView = RPCManager.PhotonView;
        rpcView.RPC("RPC_SpawnRemote", RpcTarget.Others, bundle, prefab, position, rotation, layer, view.ViewID);
    }

    [PunRPC]
    private void RPC_SpawnRemote(string bundle, string prefab, Vector3 position, Vector3 rotation, int layer, int viewID)
    {
        StartCoroutine(RemoteSpawn(bundle, prefab, position, rotation, layer, viewID));
    }

    private IEnumerator RemoteSpawn(string bundle, string prefab, Vector3 position, Vector3 rotation, int layer, int viewID)
    {
        if (!AssetBundleManager.LoadedBundle(bundle))
            yield return AssetBundleManager.LoadBundle(bundle, "", true);

        GameObject prefabObj = AssetBundleManager.LoadAsset(bundle, prefab) as GameObject;
        if (prefabObj == null) yield break;

        GameObject go = Instantiate(prefabObj, position, Quaternion.Euler(rotation));
        PhotonView view = go.AddComponent<PhotonView>();
        view.ViewID = viewID;
        SetLayerRecursively(go, layer);
        go.AddComponent<CustomAssetHelper>();
    }

    private void SpawnBuildable(string prefabName, Vector3 position, Vector3 rotation, int layer)
    {
        GameObject go = PhotonNetwork.InstantiateRoomObject("Buildables/" + prefabName, position, Quaternion.Euler(rotation));
        SetLayerRecursively(go, layer);
        go.AddComponent<CustomAssetHelper>();
        spawnedAssets.Add(go);

        AddChatMessage($"[MC] Spawned buildable: {prefabName}");
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}