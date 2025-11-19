using UnityEngine;
using Characters;
using Photon.Pun;
using UI;
using Settings;
using GameManagers;
using ApplicationManagers;
using System.Collections;

public class DirectMountBundled : MonoBehaviourPunCallbacks
{
    [Header("Mount Target")]
    public Transform mountPoint;
    public Vector3 positionOffset;
    public Vector3 rotationOffset;

    [Header("Unmount Prompt Settings")]
    public float unmountPromptDuration = 5f;

    [Header("Animation Settings")]
    public bool useHorseIdle = true;
    public bool enableRunAnimation = true;
    public float runSpeedThreshold = 4f;

    [Header("Rigidbody Settings")]
    public bool disableGravityOnMount = true;
    public bool disableMassOnMount = true;
    public float mountedMass = 0.1f;

    private Human humanInTrigger;
    private Rigidbody humanRigidbody;
    private bool isMounted = false;
    private bool hasExitedAfterUnmount = false;

    private float originalMass;
    private bool originalUseGravity;

    private string currentPrompt = ""; // REMOVED STATIC
    private float unmountPromptTimer = 0f;
    private Vector3 lastMountedWorldPos = Vector3.zero;
    private bool isCurrentlyRunning = false;

    private string MountPromptText;
    private string UnmountPromptText;
    private string _lastCachedKey = "";

    private Collider _triggerCollider;
    private const float MaxDistanceBuffer = 40.5f;

    [Header("Trigger Validation")]
    public float maxTriggerDistance = 40.5f;

    private int isOccupied = 0;

    // FIX: Cached GUI elements to prevent memory leaks
    private GUIStyle _cachedPromptStyle;
    private bool _isStyleInitialized = false;

    private void Start()
    {
        _triggerCollider = GetComponent<Collider>();
        UpdatePromptTexts();
        ClearPrompt();
        InitializeGUIStyle(); // Initialize style once
    }

    // FIX: Initialize GUI style once to prevent memory leaks
    private void InitializeGUIStyle()
    {
        if (!_isStyleInitialized)
        {
            _cachedPromptStyle = new GUIStyle(GUI.skin.label);
            _cachedPromptStyle.fontSize = 24;
            _cachedPromptStyle.alignment = TextAnchor.UpperCenter;
            _cachedPromptStyle.normal.textColor = Color.white;
            _isStyleInitialized = true;
        }
    }

    private void UpdatePromptTexts()
    {
        string key = SettingsManager.InputSettings.Interaction.Interact.ToString().Replace("Alpha", "");
        MountPromptText = $"Press {key} to Mount";
        UnmountPromptText = $"Press {key} to Unmount";
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isOccupied == 1) return;

        Human human = other.GetComponentInParent<Human>();
        if (human != null && human.IsMine())
        {
            humanInTrigger = human;
            humanRigidbody = human.GetComponent<Rigidbody>();
            hasExitedAfterUnmount = false;

            UpdatePromptTexts();
            SetPrompt(MountPromptText);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Human human = other.GetComponentInParent<Human>();
        if (human != null && human == humanInTrigger)
        {
            if (!isMounted)
            {
                hasExitedAfterUnmount = true;
                humanInTrigger = null;
                humanRigidbody = null;
                ClearPrompt();
            }
        }
    }

    private void Update()
    {
        if (humanInTrigger != null)
        {
            CheckIfStillInsideCollider();

            string currentKey = SettingsManager.InputSettings.Interaction.Interact.ToString();
            if (_lastCachedKey != currentKey)
            {
                _lastCachedKey = currentKey;
                UpdatePromptTexts();

                if (!isMounted)
                    SetPrompt(MountPromptText);
                else
                    SetPrompt(UnmountPromptText);
            }
        }

        HandleMountInput();
        HandleUnmountPromptTimer();
        HandleRunAnimation();
    }

    private void CheckIfStillInsideCollider()
    {
        if (humanInTrigger == null || _triggerCollider == null)
            return;

        Vector3 closest = _triggerCollider.ClosestPoint(humanInTrigger.transform.position);
        float dist = Vector3.Distance(humanInTrigger.transform.position, closest);

        if (dist > MaxDistanceBuffer)
        {
            humanInTrigger = null;
            humanRigidbody = null;
            ClearPrompt();
        }
    }

    private void HandleMountInput()
    {
        if (humanInTrigger == null)
            return;

        if (!InGameMenu.InMenu() && !ChatManager.IsChatActive())
        {
            if (SettingsManager.InputSettings.Interaction.Interact.GetKeyDown())
            {
                if (!isMounted && !hasExitedAfterUnmount)
                    AttachHuman();
                else if (isMounted)
                    DetachHuman();
            }
        }
    }

    private void HandleUnmountPromptTimer()
    {
        if (!string.IsNullOrEmpty(currentPrompt))
        {
            unmountPromptTimer -= Time.deltaTime;
            if (unmountPromptTimer <= 0f)
                ClearPrompt();
        }
    }

    private void HandleRunAnimation()
    {
        if (!isMounted || humanInTrigger == null || !enableRunAnimation)
            return;

        if (humanInTrigger.MountedTransform == null)
            return;

        Vector3 currentWorldPos = humanInTrigger.MountedTransform.TransformPoint(humanInTrigger.MountedPositionOffset);
        float speed = (currentWorldPos - lastMountedWorldPos).magnitude / Time.deltaTime;
        lastMountedWorldPos = currentWorldPos;

        if (speed > runSpeedThreshold)
        {
            if (!isCurrentlyRunning)
            {
                humanInTrigger.CrossFadeIfNotPlaying(HumanAnimations.HorseRun, 0.23f);
                isCurrentlyRunning = true;
            }
        }
        else
        {
            if (isCurrentlyRunning)
            {
                humanInTrigger.CrossFadeIfNotPlaying(GetIdleAnimation(), 0.1f);
                isCurrentlyRunning = false;
            }
        }
    }

    private void AttachHuman()
    {
        if (humanInTrigger == null || mountPoint == null)
            return;

        isOccupied = 1;

        humanInTrigger.MountedTransform = mountPoint;
        humanInTrigger.MountedMapObject = null;
        humanInTrigger.MountedPositionOffset = positionOffset;
        humanInTrigger.MountedRotationOffset = rotationOffset;
        humanInTrigger.MountState = HumanMountState.MapObject;
        humanInTrigger.SetInterpolation(false);

        if (humanRigidbody != null)
        {
            originalMass = humanRigidbody.mass;
            originalUseGravity = humanRigidbody.useGravity;

            if (disableGravityOnMount)
                humanRigidbody.useGravity = false;
            if (disableMassOnMount)
                humanRigidbody.mass = mountedMass;
        }

        isMounted = true;
        hasExitedAfterUnmount = false;

        SetPrompt(UnmountPromptText);
        lastMountedWorldPos = humanInTrigger.MountedTransform.TransformPoint(humanInTrigger.MountedPositionOffset);
        humanInTrigger.CrossFadeIfNotPlaying(GetIdleAnimation(), 0.2f);
    }

    private void DetachHuman()
    {
        if (humanInTrigger == null)
            return;

        isOccupied = 0;

        humanInTrigger.Unmount(true);

        if (humanRigidbody != null)
        {
            humanRigidbody.useGravity = originalUseGravity;
            humanRigidbody.mass = originalMass;
        }

        isMounted = false;

        if (humanInTrigger != null && !hasExitedAfterUnmount)
        {
            SetPrompt(MountPromptText);
        }
        else
        {
            ClearPrompt();
        }
    }

    private string GetIdleAnimation()
    {
        return useHorseIdle ? HumanAnimations.HorseIdle : HumanAnimations.IdleM;
    }

    // FIX: Using cached GUIStyle to prevent memory leaks
    private void OnGUI()
    {
        if (!string.IsNullOrEmpty(currentPrompt))
        {
            // Use the pre-initialized style instead of creating new one each frame
            GUI.Label(new Rect(Screen.width / 2 - 200, 10, 400, 50), currentPrompt, _cachedPromptStyle);
        }
    }

    private void SetPrompt(string text)
    {
        currentPrompt = text;
        unmountPromptTimer = unmountPromptDuration;
    }

    private void ClearPrompt()
    {
        currentPrompt = "";
    }

    // FIX: Clean up when object is destroyed
    private void OnDestroy()
    {
        // Clear references to help garbage collection
        humanInTrigger = null;
        humanRigidbody = null;
        currentPrompt = "";

        // If style was created, we let it be garbage collected naturally
        // since GUIStyle doesn't implement IDisposable
    }

    // FIX: Additional cleanup when disabled
    private void OnDisable()
    {
        ClearPrompt();
    }
}