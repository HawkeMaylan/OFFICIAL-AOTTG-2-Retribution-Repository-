using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;
using Characters;
using System.Collections;
using GameManagers;
using UI;
using Photon.Realtime;
using System.IO;
using System.Linq;
using ApplicationManagers;
using Cameras;
using Controllers;
using CustomLogic;
using CustomSkins;
using Effects;
using GameManagers;
using GameProgress;
using Map;
using Photon.Pun;
using Photon.Realtime;
using Settings;
using SimpleJSONFixed;
using System;
using System.Collections;

using UnityEngine;
using Utility;
using Weather;
using UnityEngine.UI;
using Entities;

public class BuildSystem : MonoBehaviourPunCallbacks
{
    [Header("References")]
    public Transform cam;
    public LayerMask buildLayer;
    public Material buildableMaterial;
    public Material notBuildableMaterial;

    [Header("Controls")]
    public KeyCode toggleKey = KeyCode.BackQuote;
    public KeyCode buildKey = KeyCode.K;
    public KeyCode placeKey = KeyCode.UpArrow;
    public KeyCode rotateLeftKey = KeyCode.LeftArrow;
    public KeyCode rotateRightKey = KeyCode.RightArrow;
    public KeyCode resetRotationKey = KeyCode.Space;
    public KeyCode snapToSurfaceKey = KeyCode.T;

    // Building state
    private bool isBuilding = false;
    private bool scriptActive = false;
    private GameObject currentPreview;
    private Vector3 currentPos;
    private Quaternion currentRotation;
    private List<GameObject> buildablePrefabs = new List<GameObject>();
    private int currentBuildableIndex = 0;
    private HumanInventory _playerInventory;
    private bool _inventorySearchPerformed = false;

    // Cached references for performance
    private BuildableObjectHelper _currentHelper;
    private RadialMenuController _cachedRadialMenu;
    private Coroutine _parentParticlesCoroutine;

    // Rotation state
    private Quaternion surfaceAlignmentRotation = Quaternion.identity;

    // Cache for performance (non-allocating)
    private Collider[] _colliderCache = new Collider[32];
    private List<Renderer> _rendererCache = new List<Renderer>(16);

    // Build tracking system
    [System.Serializable]
    public class BuildableObjectData
    {
        public string prefabName;
        public Vector3 position;
        public Quaternion rotation;
        public int viewId;
        public string ownerId;
        public double timestamp;
    }

    [System.Serializable]
    public class BuildableObjectsCollection
    {
        public List<BuildableObjectData> objects = new List<BuildableObjectData>();
    }

    private static List<BuildableObjectData> _buildableObjects = new List<BuildableObjectData>();
    private static Dictionary<int, BuildableObjectData> _buildableObjectsByViewId = new Dictionary<int, BuildableObjectData>();

    private IEnumerator Start()
    {
        LoadBuildablePrefabs();
        InitializeRadialMenu();
        PhotonNetwork.AddCallbackTarget(this);

        // Master Client scans for all existing buildables on start
        if (PhotonNetwork.IsMasterClient)
        {
            yield return new WaitForSeconds(2f);
            ScanAndTrackAllBuildables();

            // Set up periodic scanning to catch any missed objects
            StartCoroutine(PeriodicBuildableScan());
        }

        Debug.Log("BuildSystem: Initialized");
        yield break;
    }

    private IEnumerator PeriodicBuildableScan()
    {
        while (PhotonNetwork.IsMasterClient)
        {
            yield return new WaitForSeconds(5f); // Scan every 5 seconds
            ScanAndTrackAllBuildables();
        }
    }

    private void ScanAndTrackAllBuildables()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        BuildableTracker[] allTrackers = FindObjectsOfType<BuildableTracker>();
        Debug.Log($"SCAN: Found {allTrackers.Length} buildables in scene");

        _buildableObjects.Clear();

        foreach (BuildableTracker tracker in allTrackers)
        {
            if (tracker != null && tracker.gameObject != null)
            {
                // GET PREFAB NAME FROM GAME OBJECT NAME AS FALLBACK
                string prefabName = tracker.GetPrefabName();

                // If prefabName is empty, use the game object name
                if (string.IsNullOrEmpty(prefabName))
                {
                    prefabName = tracker.gameObject.name.Replace("(Clone)", "").Trim();
                    Debug.Log($"Using fallback name: {prefabName} for object {tracker.gameObject.name}");
                }

                // Get view ID from PhotonView
                int viewId = 0;
                PhotonView pv = tracker.GetComponent<PhotonView>();
                if (pv != null)
                {
                    viewId = pv.ViewID;
                }

                BuildableObjectData buildData = new BuildableObjectData
                {
                    prefabName = prefabName, // THIS WAS EMPTY BEFORE
                    position = tracker.transform.position,
                    rotation = tracker.transform.rotation,
                    viewId = viewId,
                    ownerId = "MC",
                    timestamp = PhotonNetwork.Time
                };

                _buildableObjects.Add(buildData);
                Debug.Log($"Tracked: {prefabName} at {tracker.transform.position}");
            }
        }
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        Debug.Log($"Master client switched to: {newMasterClient.ActorNumber}");

        // If we become the new master client, we need to restore all buildables
        if (PhotonNetwork.IsMasterClient)
        {
            StartCoroutine(RestoreBuildablesForNewMaster());
        }
    }

    private IEnumerator RestoreBuildablesForNewMaster()
    {
        yield return new WaitForSeconds(1f); // Wait for scene to stabilize

        Debug.Log("New master client restoring buildables...");

        // Clear existing buildables (in case any were orphaned)
        ClearAllBuildables();

        // Restore from our saved data
        foreach (var buildData in _buildableObjects)
        {
            yield return StartCoroutine(InstantiateBuildableForAll(buildData));
        }

        Debug.Log($"Restored {_buildableObjects.Count} buildables as new master");
    }

    private bool FindLocalPlayerInventory()
    {
        _playerInventory = null;

        // Method 1: Find by PhotonView ownership (removed IsMine check)
        var humans = FindObjectsOfType<Human>();
        foreach (var human in humans)
        {
            if (human != null && human.photonView != null)
            {
                _playerInventory = human.GetComponent<HumanInventory>();
                if (_playerInventory != null)
                {
                    Debug.Log("BuildSystem: Found player inventory via PhotonView");
                    _inventorySearchPerformed = true;
                    return true;
                }
            }
        }

        // Method 2: Fallback to tag search
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _playerInventory = player.GetComponentInChildren<HumanInventory>(true);
            if (_playerInventory != null)
            {
                Debug.Log("BuildSystem: Found player inventory via tag search");
                _inventorySearchPerformed = true;
                return true;
            }
        }

        // Method 3: Final fallback (removed IsMine check)
        HumanInventory[] allInventories = FindObjectsOfType<HumanInventory>();
        foreach (HumanInventory inventory in allInventories)
        {
            if (inventory != null && inventory.photonView != null)
            {
                _playerInventory = inventory;
                Debug.Log("BuildSystem: Found player inventory via scene search");
                _inventorySearchPerformed = true;
                return true;
            }
        }

        Debug.LogWarning("BuildSystem: Could not find player inventory");
        return false;
    }

    void Update()
    {
        HandleSystemToggle();
        if (!scriptActive) return;

        HandleBuildingToggle();
        if (isBuilding)
        {
            UpdatePreview();
            HandleBuildingInput();
        }
    }

    void LoadBuildablePrefabs()
    {
        buildablePrefabs.Clear();
        GameObject[] prefabs = Resources.LoadAll<GameObject>("Buildables");
        foreach (GameObject prefab in prefabs)
        {
            if (prefab.GetComponent<BuildableObjectHelper>() != null)
            {
                buildablePrefabs.Add(prefab);
                Debug.Log($"BuildSystem: Loaded buildable prefab {prefab.name}");
            }
        }

        if (buildablePrefabs.Count == 0)
        {
            Debug.LogWarning("BuildSystem: No buildable prefabs found in Resources/Buildables");
        }
    }

    void InitializeRadialMenu()
    {
        if (_cachedRadialMenu == null)
        {
            _cachedRadialMenu = FindObjectOfType<RadialMenuController>();
        }

        if (_cachedRadialMenu != null)
        {
            _cachedRadialMenu.InitializeWithBuildables(buildablePrefabs);
            Debug.Log("BuildSystem: Radial menu initialized");
        }
    }

    void HandleSystemToggle()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            scriptActive = !scriptActive;
            isBuilding = false;
            ToggleCursor(!scriptActive);

            if (!scriptActive)
            {
                CleanupPreview();
            }
        }
    }

    void HandleBuildingToggle()
    {
        if (Input.GetKeyDown(buildKey) && !InGameMenu.InMenu() && !ChatManager.IsChatActive())
        {
            // Only search for inventory when they first try to build
            if (_playerInventory == null && !_inventorySearchPerformed)
            {
                if (!FindLocalPlayerInventory())
                {
                    Debug.LogError("BuildSystem: Cannot start building - no player inventory found");
                    return;
                }
            }

            isBuilding = !isBuilding;

            if (!isBuilding)
            {
                CleanupPreview();
            }
            else if (currentPreview == null)
            {
                CreatePreview();
            }
        }
    }

    void CreatePreview()
    {
        if (currentBuildableIndex < 0 || currentBuildableIndex >= buildablePrefabs.Count)
            return;

        GameObject prefab = buildablePrefabs[currentBuildableIndex];
        _currentHelper = prefab.GetComponent<BuildableObjectHelper>();

        if (_currentHelper == null || _currentHelper.preview == null)
        {
            Debug.LogError("BuildSystem: Missing BuildableObjectHelper or preview");
            return;
        }

        // Clean up existing preview
        CleanupPreview();

        // Reset all rotation states
        currentRotation = Quaternion.identity;
        surfaceAlignmentRotation = Quaternion.identity;

        // Initialize with forced alignment if enabled
        Quaternion spawnRotation = Quaternion.identity;
        if (_currentHelper.forceUpAlignment)
        {
            spawnRotation = _currentHelper.GetForcedRotation();
            Debug.Log($"Applying forced alignment - Up: {_currentHelper.forcedUpAxis}, Forward: {_currentHelper.forwardAxis}");
        }

        // Create new preview with proper rotation
        currentPreview = Instantiate(_currentHelper.preview, currentPos, spawnRotation);
        SetLayerRecursively(currentPreview, LayerMask.NameToLayer("Preview"));

        Debug.Log($"Created preview for {prefab.name} " +
                 $"(Force Up: {_currentHelper.forceUpAlignment}, " +
                 $"Rotation: {spawnRotation.eulerAngles})");
    }

    void UpdatePreview()
    {
        if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, 40, buildLayer))
        {
            if (_currentHelper == null) return;

            // Calculate grid-aligned position
            float gridSize = _currentHelper.gridSize;
            currentPos = hit.point + hit.normal * _currentHelper.offset;
            currentPos = new Vector3(
                Mathf.Round(currentPos.x / gridSize) * gridSize,
                Mathf.Round(currentPos.y / gridSize) * gridSize,
                Mathf.Round(currentPos.z / gridSize) * gridSize
            );

            // Calculate surface alignment
            surfaceAlignmentRotation = _currentHelper.snapToSurface ?
                Quaternion.FromToRotation(Vector3.up, hit.normal) :
                Quaternion.identity;

            // Update preview position
            currentPreview.transform.position = currentPos;

            // Apply rotation based on helper settings
            if (_currentHelper.forceUpAlignment)
            {
                // Get the forced rotation from helper
                Quaternion forcedRotation = _currentHelper.GetForcedRotation();

                // Combine rotations:
                currentPreview.transform.rotation = surfaceAlignmentRotation *
                                      forcedRotation *
                                      currentRotation;
            }
            else
            {
                // Standard rotation behavior
                currentPreview.transform.rotation = surfaceAlignmentRotation * currentRotation;
            }

            // Update preview materials based on validity
            UpdatePreviewMaterials();
        }
    }

    void UpdatePreviewMaterials()
    {
        // Only check position validity for preview, not costs
        bool isValid = IsPreviewValid();

        // Use the cached list to avoid GC allocations
        _rendererCache.Clear();
        currentPreview.GetComponentsInChildren<Renderer>(true, _rendererCache);

        foreach (Renderer renderer in _rendererCache)
        {
            renderer.material = isValid ? buildableMaterial : notBuildableMaterial;
        }
    }

    bool IsPreviewValid()
    {
        if (currentPreview == null) return false;
        if (_currentHelper == null || _currentHelper.collisionCheckObject == null) return false;

        Vector3 checkPos = currentPreview.transform.position + _currentHelper.collisionCheckObject.transform.localPosition;

        // Use non-allocating version if possible, fallback to regular version
        Collider checkCollider = _currentHelper.collisionCheckObject.GetComponent<Collider>();
        if (checkCollider == null) return false;

        int numColliders = Physics.OverlapBoxNonAlloc(
            checkPos,
            checkCollider.bounds.extents,
            _colliderCache,
            currentPreview.transform.rotation,
            buildLayer | (1 << LayerMask.NameToLayer("Player"))
        );

        for (int i = 0; i < numColliders; i++)
        {
            Collider col = _colliderCache[i];
            if (col != null && col.gameObject != currentPreview)
            {
                return false;
            }
        }
        return true;
    }

    void HandleBuildingInput()
    {
        // Rotation application
        if (Input.GetKeyDown(rotateLeftKey))
            RotatePreview(-1f); // Counter-clockwise
        if (Input.GetKeyDown(rotateRightKey))
            RotatePreview(1f); // Clockwise

        // Rotation reset
        if (Input.GetKeyDown(resetRotationKey))
        {
            if (_currentHelper != null)
            {
                currentRotation = _currentHelper.forceUpAlignment ? _currentHelper.GetForcedRotation() : Quaternion.identity;

                if (currentPreview != null)
                {
                    currentPreview.transform.rotation = surfaceAlignmentRotation * currentRotation;
                }
            }
        }

        // Snap to surface normal
        if (Input.GetKeyDown(snapToSurfaceKey) && !InGameMenu.InMenu() && !ChatManager.IsChatActive())
        {
            if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, 40, buildLayer))
            {
                surfaceAlignmentRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                if (currentPreview != null)
                {
                    currentPreview.transform.rotation = surfaceAlignmentRotation * currentRotation;
                }
            }
        }

        if (Input.GetKeyDown(placeKey))
        {
            Build();
        }
    }

    void RotatePreview(float direction)
    {
        if (_currentHelper == null) return;

        // Get the axis to rotate around from the prefab settings
        Vector3 axis = Vector3.up;
        switch (_currentHelper.rotationAxis)
        {
            case BuildableObjectHelper.RotationAxis.X: axis = Vector3.right; break;
            case BuildableObjectHelper.RotationAxis.Y: axis = Vector3.up; break;
            case BuildableObjectHelper.RotationAxis.Z: axis = Vector3.forward; break;
        }

        // Apply rotation using the prefab's increment
        currentRotation *= Quaternion.AngleAxis(direction * _currentHelper.rotationIncrement, axis);

        if (currentPreview != null)
        {
            if (_currentHelper.forceUpAlignment)
            {
                Quaternion forcedRotation = _currentHelper.GetForcedRotation();
                currentPreview.transform.rotation = surfaceAlignmentRotation * forcedRotation * currentRotation;
            }
            else
            {
                currentPreview.transform.rotation = surfaceAlignmentRotation * currentRotation;
            }
        }
    }

    void Build()
    {
        // 1. Validate build position
        if (currentPreview == null || !IsPreviewValid()) return;

        // 2. Search for inventory if we don't have it yet
        if (_playerInventory == null && !_inventorySearchPerformed)
        {
            if (!FindLocalPlayerInventory())
            {
                Debug.LogError("BuildSystem: Cannot build - no player inventory found");
                return;
            }
        }

        // 3. Check & deduct resources from current player
        if (_currentHelper == null) return;

        foreach (InventoryCost cost in _currentHelper.buildCosts)
        {
            if (_playerInventory.GetItemCount(cost.itemName) < cost.amount)
            {
                _playerInventory.ShowNotEnoughMessage(cost.itemName);
                return;
            }
            _playerInventory.SetItemCount(cost.itemName, _playerInventory.GetItemCount(cost.itemName) - cost.amount);
        }

        // 4. Create build data
        BuildableObjectData buildData = new BuildableObjectData
        {
            prefabName = buildablePrefabs[currentBuildableIndex].name,
            position = currentPos,
            rotation = currentPreview.transform.rotation,
            ownerId = PhotonNetwork.LocalPlayer.UserId,
            timestamp = PhotonNetwork.Time
        };

        // 5. Spawn the object for all players
        StartCoroutine(InstantiateBuildableForAll(buildData));

        // 6. Local effects
        if (_currentHelper.buildParticleEffectPrefab != null)
            SpawnBuildParticles(_currentHelper);

        CleanupPreview();
        CreatePreview();
    }

    [PunRPC]
    private void RequestBuildObject(BuildableObjectData buildData, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // Verify the request came from a valid player
        if (info.Sender == null) return;

        Debug.Log($"Master client received build request from {info.Sender.ActorNumber}");
        StartCoroutine(InstantiateBuildableForAll(buildData));
    }

    private IEnumerator InstantiateBuildableForAll(BuildableObjectData buildData)
    {
        string prefabPath = "Buildables/" + buildData.prefabName;
        GameObject builtObject = PhotonNetwork.Instantiate(prefabPath, buildData.position, buildData.rotation);

        if (builtObject != null)
        {
            PhotonView photonView = builtObject.GetComponent<PhotonView>();
            if (photonView != null)
            {
                buildData.viewId = photonView.ViewID;

                // Add tracker component - THIS IS THE KEY
                BuildableTracker tracker = builtObject.GetComponent<BuildableTracker>();
                if (tracker == null)
                {
                    tracker = builtObject.AddComponent<BuildableTracker>();
                }
                tracker.Initialize(buildData.prefabName, buildData.viewId);

                // Add to local tracking
                if (!_buildableObjects.Exists(b => b.viewId == buildData.viewId))
                {
                    _buildableObjects.Add(buildData);
                    _buildableObjectsByViewId[buildData.viewId] = buildData;
                }

                // Optional: Still sync for other players
                photonView.RPC("SyncBuildableData", RpcTarget.OthersBuffered, buildData);
            }
        }

        yield return null;
    }
    public void ForceRefreshBuildableTracking()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            ScanAndTrackAllBuildables();
            Debug.Log($"MC Force Refresh: Now tracking {_buildableObjects.Count} buildables");

            // Show in chat
            AddChatMessage($"[System] Refreshed buildable tracking: {_buildableObjects.Count} objects");
        }
    }


    [PunRPC]
    private void SyncBuildableData(BuildableObjectData buildData, PhotonMessageInfo info)
    {
        // Just add to local tracking - MC will pick it up via scanning anyway
        if (!_buildableObjects.Exists(b => b.viewId == buildData.viewId))
        {
            _buildableObjects.Add(buildData);
            _buildableObjectsByViewId[buildData.viewId] = buildData;
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log($"New player joined, syncing {_buildableObjects.Count} buildables");

            // Sync all existing buildables to the new player
            foreach (var buildData in _buildableObjects)
            {
                // Find the existing object in scene and sync its data
                PhotonView existingView = PhotonView.Find(buildData.viewId);
                if (existingView != null)
                {
                    existingView.RPC("SyncBuildableData", newPlayer, buildData);
                }
            }
        }
    }


    private void FindAndTrackExistingBuildables()
    {
        // Find all buildable trackers in scene
        BuildableTracker[] allBuildables = FindObjectsOfType<BuildableTracker>();
        Debug.Log($"MC found {allBuildables.Length} buildables in scene to track");

        foreach (BuildableTracker tracker in allBuildables)
        {
            if (tracker.photonView != null)
            {
                int viewId = tracker.photonView.ViewID;

                // Check if we're already tracking this
                if (!_buildableObjects.Exists(b => b.viewId == viewId))
                {
                    // Use game object name as fallback for prefab name
                    string prefabName = tracker.gameObject.name.Replace("(Clone)", "").Trim();

                    // Create new build data for this existing object
                    BuildableObjectData buildData = new BuildableObjectData
                    {
                        prefabName = prefabName,
                        position = tracker.transform.position,
                        rotation = tracker.transform.rotation,
                        viewId = viewId,
                        ownerId = "Unknown", // We don't know the original owner
                        timestamp = PhotonNetwork.Time
                    };

                    _buildableObjects.Add(buildData);
                    _buildableObjectsByViewId[viewId] = buildData;
                    Debug.Log($"MC added existing buildable to tracking: {prefabName} (ViewID: {viewId})");
                }
            }
        }
    }


    // JSON Saving/Loading Methods
    public void SaveBuildablesToJson(string filePath = "buildables_save.json")
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("Only master client can save buildables");
            return;
        }

        // Force scan to get ALL objects
        ScanAndTrackAllBuildables();

        BuildableObjectsCollection collection = new BuildableObjectsCollection();
        collection.objects = new List<BuildableObjectData>(_buildableObjects);

        string json = JsonUtility.ToJson(collection, true);
        string fullPath = Path.Combine(Application.persistentDataPath, filePath);
        File.WriteAllText(fullPath, json);

        Debug.Log($"SAVED: {_buildableObjects.Count} objects to {fullPath}");

        // Debug what we saved
        foreach (var buildData in _buildableObjects)
        {
            Debug.Log($"- {buildData.prefabName} at {buildData.position}");
        }

        AddChatMessage($"[System] Saved {_buildableObjects.Count} buildables");
    }

    public void LoadBuildablesFromJson(string filePath = "buildables_save.json")
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("Only master client can load buildables");
            return;
        }

        string fullPath = Path.Combine(Application.persistentDataPath, filePath);
        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"No save file found at {fullPath}");
            AddChatMessage($"[System] No save file found at {filePath}");
            return;
        }

        string json = File.ReadAllText(fullPath);
        BuildableObjectsCollection collection = JsonUtility.FromJson<BuildableObjectsCollection>(json);

        Debug.Log($"LOADING: Found {collection.objects.Count} buildables in save file");

        // JUST SPAWN THE DAMN OBJECTS
        foreach (var buildData in collection.objects)
        {
            Debug.Log($"Spawning: {buildData.prefabName} at {buildData.position} with rot {buildData.rotation}");
            SpawnBuildableSimple(buildData);
        }

        AddChatMessage($"[System] Loaded {collection.objects.Count} buildables");
    }

    private void SpawnBuildableSimple(BuildableObjectData buildData)
    {
        string prefabPath = "Buildables/" + buildData.prefabName;

        // JUST INSTANTIATE THE PREFAB AT POSITION WITH ROTATION
        GameObject builtObject = PhotonNetwork.Instantiate(prefabPath, buildData.position, buildData.rotation);

        if (builtObject != null)
        {
            Debug.Log($"SUCCESS: Spawned {buildData.prefabName}");
            // Optionally add tracker if needed
            BuildableTracker tracker = builtObject.GetComponent<BuildableTracker>();
            if (tracker == null)
                tracker = builtObject.AddComponent<BuildableTracker>();
            tracker.Initialize(buildData.prefabName, builtObject.GetComponent<PhotonView>().ViewID);
        }
        else
        {
            Debug.LogError($"FAILED to spawn: {buildData.prefabName}");
        }
    }
    private IEnumerator SpawnBuildableForAllPlayers(BuildableObjectData buildData)
    {
        if (!PhotonNetwork.IsMasterClient) yield break;

        string prefabPath = "Buildables/" + buildData.prefabName;

        // MASTER CLIENT spawns the object using PhotonNetwork.Instantiate
        GameObject builtObject = PhotonNetwork.Instantiate(prefabPath, buildData.position, buildData.rotation);

        if (builtObject != null)
        {
            PhotonView photonView = builtObject.GetComponent<PhotonView>();
            if (photonView != null)
            {
                // Update the view ID in build data
                buildData.viewId = photonView.ViewID;

                // Add tracker component
                BuildableTracker tracker = builtObject.GetComponent<BuildableTracker>();
                if (tracker == null)
                {
                    tracker = builtObject.AddComponent<BuildableTracker>();
                }
                tracker.Initialize(buildData.prefabName, buildData.viewId);

                // Add to tracking
                if (!_buildableObjects.Exists(b => b.viewId == buildData.viewId))
                {
                    _buildableObjects.Add(buildData);
                    _buildableObjectsByViewId[buildData.viewId] = buildData;
                }

                Debug.Log($"MC spawned buildable: {buildData.prefabName} at {buildData.position}");
            }
        }

        // Small delay to prevent overwhelming the network
        yield return new WaitForSeconds(0.1f);
    }

    [PunRPC]
    private void ClearAllBuildablesRPC(PhotonMessageInfo info)
    {
        if (info.Sender != null && !info.Sender.IsMasterClient)
        {
            Debug.LogWarning("ClearAllBuildablesRPC called by non-master client");
            return;
        }

        ClearAllBuildables();
    }

    public void ClearAllBuildablesMaster()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("Only master client can clear buildables");
            return;
        }

        if (photonView == null)
        {
            Debug.LogError("BuildSystem: PhotonView is null, cannot send RPC");
            // Fallback: clear locally
            ClearAllBuildables();
            AddChatMessage($"[System] Cleared all buildables (local fallback)");
            return;
        }

        try
        {
            photonView.RPC("ClearAllBuildablesRPC", RpcTarget.AllBuffered);
            AddChatMessage($"[System] Cleared all buildables");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"BuildSystem: Error clearing buildables: {e.Message}");
            // Fallback to local clearing
            ClearAllBuildables();
            AddChatMessage($"[System] Cleared all buildables (fallback)");
        }
    }

    private void ClearAllBuildables()
    {
        try
        {
            // Find and destroy all buildable objects in scene
            var buildables = FindObjectsOfType<BuildableTracker>();
            Debug.Log($"Clearing {buildables.Length} buildable objects");

            foreach (var buildable in buildables)
            {
                if (buildable != null && buildable.gameObject != null)
                {
                    if (buildable.photonView != null && buildable.photonView.IsMine)
                    {
                        PhotonNetwork.Destroy(buildable.gameObject);
                    }
                    else if (!PhotonNetwork.IsConnected)
                    {
                        Destroy(buildable.gameObject);
                    }
                }
            }

            // Also clear any spawned assets from CustomAssetMenu
            var customAssetMenu = FindObjectOfType<CustomAssetMenu>();
            if (customAssetMenu != null)
            {
                // Use reflection to clear spawnedAssets if needed, or add a public method
            }

            _buildableObjects.Clear();
            _buildableObjectsByViewId.Clear();

            Debug.Log("Cleared all buildables successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error during buildable clearing: {e.Message}");
        }
    }

    // Safe method to add chat messages
    private void AddChatMessage(string message)
    {
        // Try multiple ways to send chat messages
        try
        {
            // Method 1: Use the static method that exists in your project
            ChatManager.AddLine(message, ChatTextColor.System);
            return;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Failed to send chat message via ChatManager.AddLine: {e.Message}");
        }

        try
        {
            // Method 2: Find ChatManager in scene and use reflection
            ChatManager chatManager = FindObjectOfType<ChatManager>();
            if (chatManager != null)
            {
                var addLineMethod = chatManager.GetType().GetMethod("AddLine",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);
                if (addLineMethod != null)
                {
                    addLineMethod.Invoke(null, new object[] { message, ChatTextColor.System });
                    return;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Failed to send chat message via reflection: {e.Message}");
        }

        // Method 3: Just use debug log as fallback
        Debug.Log($"CHAT: {message}");
    }

    // Helper method to get current buildables count
    public int GetBuildableCount()
    {
        return _buildableObjects.Count;
    }

    // Get all buildable objects data
    public List<BuildableObjectData> GetAllBuildableData()
    {
        return new List<BuildableObjectData>(_buildableObjects);
    }

    // Remove a specific buildable by view ID
    public void RemoveBuildable(int viewId)
    {
        BuildableObjectData data = _buildableObjects.Find(b => b.viewId == viewId);
        if (data != null)
        {
            _buildableObjects.Remove(data);
            _buildableObjectsByViewId.Remove(viewId);
            Debug.Log($"Removed buildable from tracking: {data.prefabName} (ViewID: {viewId})");
        }
    }

    // Modified to always allow building for any player
    private bool IsLocalPlayer => true;

    private void SpawnBuildParticles(BuildableObjectHelper helper)
    {
        if (helper.buildParticleEffectPrefab == null) return;

        string particlePrefabName = "HParticles/" + helper.buildParticleEffectPrefab.name;
        Vector3 spawnPos = currentPos + currentPreview.transform.TransformDirection(helper.particleEffectOffset);
        Quaternion spawnRot = helper.particleUsePreviewRotation ? currentPreview.transform.rotation : Quaternion.identity;

        GameObject spawnedParticles = null;
        if (PhotonNetwork.IsConnectedAndReady)
        {
            spawnedParticles = PhotonNetwork.Instantiate(particlePrefabName, spawnPos, spawnRot);
        }
        else
        {
            Debug.LogWarning("Photon not ready, spawning locally");
            spawnedParticles = Instantiate(helper.buildParticleEffectPrefab, spawnPos, spawnRot);
        }

        if (spawnedParticles == null) return;

        // Parenting logic
        if (helper.particleParentToBuilding)
        {
            // Stop any existing coroutine
            if (_parentParticlesCoroutine != null)
            {
                StopCoroutine(_parentParticlesCoroutine);
            }
            _parentParticlesCoroutine = StartCoroutine(ParentParticlesAfterBuild(spawnedParticles, currentPos));
        }
    }

    private IEnumerator ParentParticlesAfterBuild(GameObject particles, Vector3 buildPosition)
    {
        // Wait one frame to allow building to spawn
        yield return null;

        // Check if objects are still valid
        if (this == null || particles == null || !gameObject.activeInHierarchy)
            yield break;

        // Find the nearest building object at our build position
        int numColliders = Physics.OverlapSphereNonAlloc(buildPosition, 0.5f, _colliderCache);
        for (int i = 0; i < numColliders; i++)
        {
            Collider col = _colliderCache[i];
            if (col != null && col.gameObject != currentPreview && col.CompareTag("Buildable"))
            {
                particles.transform.SetParent(col.transform);
                break;
            }
        }

        _parentParticlesCoroutine = null;
    }

    public void HandleBuildableSelection(GameObject prefab)
    {
        int index = buildablePrefabs.IndexOf(prefab);
        if (index == -1)
        {
            Debug.LogError($"BuildSystem: Prefab {prefab.name} not in buildable list");
            return;
        }

        currentBuildableIndex = index;
        _currentHelper = prefab.GetComponent<BuildableObjectHelper>();

        if (isBuilding)
        {
            CleanupPreview();
            CreatePreview();
        }
        else
        {
            isBuilding = true;
            scriptActive = true;
            ToggleCursor(false);
            CreatePreview();
        }
    }

    void ToggleCursor(bool enable)
    {
        Cursor.visible = enable;
        Cursor.lockState = enable ? CursorLockMode.None : CursorLockMode.Locked;
    }

    void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    public void SetPlayerInventory(HumanInventory inventory)
    {
        _playerInventory = inventory;
        _inventorySearchPerformed = true; // Mark as found
        Debug.Log($"BuildSystem: Player inventory set externally to {inventory?.gameObject?.name}");
    }

    private void CleanupPreview()
    {
        if (currentPreview != null)
        {
            Destroy(currentPreview);
            currentPreview = null;
        }
    }

    private void OnDisable()
    {
        // Clean up all coroutines
        if (_parentParticlesCoroutine != null)
        {
            StopCoroutine(_parentParticlesCoroutine);
            _parentParticlesCoroutine = null;
        }

        StopAllCoroutines();
        CleanupPreview();

        // Clear cached references
        _currentHelper = null;
        _cachedRadialMenu = null;
    }

    private void OnDestroy()
    {
        // Additional cleanup
        CleanupPreview();
        buildablePrefabs.Clear();

        // Ensure all coroutines are stopped
        if (_parentParticlesCoroutine != null)
        {
            StopCoroutine(_parentParticlesCoroutine);
            _parentParticlesCoroutine = null;
        }

        // Unregister from Photon callbacks
        PhotonNetwork.RemoveCallbackTarget(this);
    }
}
