using UnityEngine;
using Characters;
using Photon.Pun;
using UI;
using Settings;
using GameManagers;
using ApplicationManagers;
using System.Collections;

public class IntegratedMountSystem : MonoBehaviourPunCallbacks
{
    [Header("Mount Settings")]
    public Transform mountPoint;
    public Vector3 positionOffset = Vector3.zero;
    public Vector3 rotationOffset = Vector3.zero;

    [Header("UI Settings")]
    public float promptDuration = 5f;
    public string mountText = "Press {0} to Mount";
    public string unmountText = "Press {0} to Unmount";

    [Header("Animation Settings")]
    public bool useHorseAnimations = true;
    public bool enableRunAnimation = true;
    public float runSpeedThreshold = 4f;

    [Header("Physics Settings")]
    public bool disableGravityOnMount = true;
    public bool adjustMassOnMount = true;
    public float mountedMass = 0.1f;

    private Human _humanInRange;
    private bool _isMounted = false;
    private float _promptTimer = 0f;
    private string _currentPrompt = "";
    private string _lastKey = "";
    private Vector3 _lastPosition;
    private bool _isRunning = false;
    private Collider _triggerCollider;
    private Rigidbody _humanRigidbody;
    private float _originalMass;
    private bool _originalUseGravity;

    private void Start()
    {
        _triggerCollider = GetComponent<Collider>();
        UpdateKeybindText();
    }

    private void Update()
    {
        UpdatePromptVisibility();
        CheckHumanInRange();
        HandleMountInput();
        UpdateRunAnimation();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isMounted) return;

        Human human = other.GetComponentInParent<Human>();
        if (human != null && human.IsMine())
        {
            _humanInRange = human;
            _humanRigidbody = human.GetComponent<Rigidbody>();
            UpdateKeybindText();
            ShowPrompt(mountText);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Human human = other.GetComponentInParent<Human>();
        if (human != null && human == _humanInRange && !_isMounted)
        {
            _humanInRange = null;
            _humanRigidbody = null;
            HidePrompt();
        }
    }

    private void CheckHumanInRange()
    {
        if (_humanInRange == null || _triggerCollider == null) return;

        Vector3 closestPoint = _triggerCollider.ClosestPoint(_humanInRange.transform.position);
        if (Vector3.Distance(_humanInRange.transform.position, closestPoint) > 1f)
        {
            _humanInRange = null;
            _humanRigidbody = null;
            HidePrompt();
        }
    }

    private void HandleMountInput()
    {
        if (_humanInRange == null || InGameMenu.InMenu() || ChatManager.IsChatActive())
            return;

        if (SettingsManager.InputSettings.Interaction.Interact.GetKeyDown())
        {
            if (!_isMounted)
                MountHuman();
            else
                UnmountHuman();
        }
    }

    private void MountHuman()
    {
        if (_humanInRange == null || mountPoint == null) return;

        // Store original physics properties
        if (_humanRigidbody != null)
        {
            _originalMass = _humanRigidbody.mass;
            _originalUseGravity = _humanRigidbody.useGravity;
        }

        // Alternative mount implementation that doesn't require MapObject/MapLoader
        _humanInRange.MountState = HumanMountState.MapObject;
        _humanInRange.MountedTransform = mountPoint;
        _humanInRange.MountedPositionOffset = positionOffset;
        _humanInRange.MountedRotationOffset = rotationOffset;
        _humanInRange.SetInterpolation(false);

        // Apply physics adjustments
        if (_humanRigidbody != null)
        {
            if (disableGravityOnMount)
                _humanRigidbody.useGravity = false;
            if (adjustMassOnMount)
                _humanRigidbody.mass = mountedMass;
        }

        _isMounted = true;
        _lastPosition = _humanInRange.transform.position;
        ShowPrompt(unmountText);

        // Play appropriate idle animation
        _humanInRange.CrossFadeIfNotPlaying(useHorseAnimations ? HumanAnimations.HorseIdle : HumanAnimations.IdleM, 0.2f);
    }

    private void UnmountHuman()
    {
        if (_humanInRange == null) return;

        // Unmount using the existing system
        _humanInRange.Unmount(true);

        // Restore physics properties
        if (_humanRigidbody != null)
        {
            _humanRigidbody.useGravity = _originalUseGravity;
            _humanRigidbody.mass = _originalMass;
        }

        _isMounted = false;
        _isRunning = false;

        if (_humanInRange != null && _triggerCollider.bounds.Contains(_humanInRange.transform.position))
            ShowPrompt(mountText);
        else
            HidePrompt();
    }

    private void UpdateRunAnimation()
    {
        if (!_isMounted || !enableRunAnimation || _humanInRange == null || _humanInRange.MountedTransform == null)
            return;

        Vector3 currentPosition = _humanInRange.MountedTransform.TransformPoint(_humanInRange.MountedPositionOffset);
        float speed = (currentPosition - _lastPosition).magnitude / Time.deltaTime;
        _lastPosition = currentPosition;

        if (speed > runSpeedThreshold)
        {
            if (!_isRunning)
            {
                _humanInRange.CrossFadeIfNotPlaying(HumanAnimations.HorseRun, 0.23f);
                _isRunning = true;
            }
        }
        else if (_isRunning)
        {
            _humanInRange.CrossFadeIfNotPlaying(useHorseAnimations ? HumanAnimations.HorseIdle : HumanAnimations.IdleM, 0.1f);
            _isRunning = false;
        }
    }

    private void UpdateKeybindText()
    {
        string key = SettingsManager.InputSettings.Interaction.Interact.ToString().Replace("Alpha", "");
        mountText = string.Format("Press {0} to Mount", key);
        unmountText = string.Format("Press {0} to Unmount", key);
        _lastKey = key;
    }

    private void UpdatePromptVisibility()
    {
        if (!string.IsNullOrEmpty(_currentPrompt))
        {
            _promptTimer -= Time.deltaTime;
            if (_promptTimer <= 0f)
                HidePrompt();
        }

        // Update if keybind changed
        string currentKey = SettingsManager.InputSettings.Interaction.Interact.ToString();
        if (_lastKey != currentKey)
        {
            UpdateKeybindText();
            if (!string.IsNullOrEmpty(_currentPrompt))
            {
                ShowPrompt(_isMounted ? unmountText : mountText);
            }
        }
    }

    private void ShowPrompt(string text)
    {
        _currentPrompt = text;
        _promptTimer = promptDuration;
    }

    private void HidePrompt()
    {
        _currentPrompt = "";
    }

    private void OnGUI()
    {
        if (!string.IsNullOrEmpty(_currentPrompt))
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 24;
            style.alignment = TextAnchor.UpperCenter;
            style.normal.textColor = Color.white;

            GUI.Label(new Rect(Screen.width / 2 - 200, 10, 400, 50), _currentPrompt, style);
        }
    }
}