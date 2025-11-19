using UnityEngine;
using Characters;
using Photon.Pun;
using UI;
using Settings;
using GameManagers;
using ApplicationManagers;
using System.Collections;

public class CannonMount : MonoBehaviourPunCallbacks, IPunObservable
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

    private string currentPrompt = "";
    private float unmountPromptTimer = 0f;
    private Vector3 lastMountedWorldPos = Vector3.zero;
    private bool isCurrentlyRunning = false;

    private string MountPromptText;
    private string UnmountPromptText;
    private string _lastCachedKey = "";

    private Collider _triggerCollider;
    private const float MaxDistanceBuffer = 40.5f;

    private PhotonView _photonView;
    private int _mountedPlayerId = -1;

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(isMounted);
            stream.SendNext(_mountedPlayerId);
        }
        else
        {
            isMounted = (bool)stream.ReceiveNext();
            _mountedPlayerId = (int)stream.ReceiveNext();
        }
    }

    private void Start()
    {
        _triggerCollider = GetComponent<Collider>();
        _photonView = GetComponent<PhotonView>();
        UpdatePromptTexts();
        ClearPrompt();
    }

    private void UpdatePromptTexts()
    {
        string key = SettingsManager.InputSettings.Interaction.Interact.ToString().Replace("Alpha", "");
        MountPromptText = $"Press {key} to Mount";
        UnmountPromptText = $"Press {key} to Unmount";
    }

    private void OnTriggerEnter(Collider other)
    {
        // Don't allow mounting if already occupied by another player
        if (isMounted && _mountedPlayerId != -1 && _mountedPlayerId != PhotonNetwork.LocalPlayer.ActorNumber)
            return;

        Human human = other.GetComponentInParent<Human>();
        if (human != null && human.IsMine())
        {
            humanInTrigger = human;
            humanRigidbody = human.GetComponent<Rigidbody>();
            hasExitedAfterUnmount = false;

            UpdatePromptTexts();

            // Only show mount prompt if not already mounted or if this player is the one mounted
            if (!isMounted || _mountedPlayerId == PhotonNetwork.LocalPlayer.ActorNumber)
                SetPrompt(MountPromptText);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Human human = other.GetComponentInParent<Human>();
        if (human != null && human == humanInTrigger)
        {
            if (!isMounted || _mountedPlayerId != PhotonNetwork.LocalPlayer.ActorNumber)
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

                if (!isMounted || _mountedPlayerId != PhotonNetwork.LocalPlayer.ActorNumber)
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

        // Only allow interaction if this player owns the mount or it's not mounted
        if ((!isMounted || _mountedPlayerId == PhotonNetwork.LocalPlayer.ActorNumber) &&
            !InGameMenu.InMenu() && !ChatManager.IsChatActive())
        {
            if (SettingsManager.InputSettings.Interaction.Interact.GetKeyDown())
            {
                if (!isMounted && !hasExitedAfterUnmount)
                    AttachHuman();
                else if (isMounted && _mountedPlayerId == PhotonNetwork.LocalPlayer.ActorNumber)
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

        // Request ownership before mounting
        if (!_photonView.IsMine)
            _photonView.RequestOwnership();

        // Use RPC to synchronize mounting across network
        _photonView.RPC("RPC_AttachHuman", RpcTarget.All, PhotonNetwork.LocalPlayer.ActorNumber);
    }

    [PunRPC]
    private void RPC_AttachHuman(int playerId)
    {
        // Set mounted state and track which player is mounted
        isMounted = true;
        _mountedPlayerId = playerId;

        // Only execute mount logic for the actual mounted player
        if (playerId == PhotonNetwork.LocalPlayer.ActorNumber && humanInTrigger != null)
        {
            humanInTrigger.MountState = HumanMountState.MapObject;
            humanInTrigger.MountedTransform = mountPoint;
            humanInTrigger.MountedPositionOffset = positionOffset;
            humanInTrigger.MountedRotationOffset = rotationOffset;

            // KEY FIX: Keep interpolation enabled to prevent physics conflicts with joints
            humanInTrigger.SetInterpolation(true);

            if (humanRigidbody != null)
            {
                originalMass = humanRigidbody.mass;
                originalUseGravity = humanRigidbody.useGravity;

                if (disableGravityOnMount)
                    humanRigidbody.useGravity = false;
                if (disableMassOnMount)
                    humanRigidbody.mass = mountedMass;
            }

            hasExitedAfterUnmount = false;
            SetPrompt(UnmountPromptText);
            lastMountedWorldPos = humanInTrigger.MountedTransform.TransformPoint(humanInTrigger.MountedPositionOffset);
            humanInTrigger.CrossFadeIfNotPlaying(GetIdleAnimation(), 0.2f);
        }
    }

    private void DetachHuman()
    {
        // Use RPC to synchronize unmounting across network
        _photonView.RPC("RPC_DetachHuman", RpcTarget.All);
    }

    [PunRPC]
    private void RPC_DetachHuman()
    {
        // Only execute unmount logic for the mounted player
        if (_mountedPlayerId == PhotonNetwork.LocalPlayer.ActorNumber && humanInTrigger != null)
        {
            humanInTrigger.Unmount(true);

            if (humanRigidbody != null)
            {
                humanRigidbody.useGravity = originalUseGravity;
                humanRigidbody.mass = originalMass;
            }

            if (humanInTrigger != null && !hasExitedAfterUnmount)
            {
                SetPrompt(MountPromptText);
            }
            else
            {
                ClearPrompt();
            }
        }

        // Reset mounted state for all clients
        isMounted = false;
        _mountedPlayerId = -1;
    }

    private string GetIdleAnimation()
    {
        return useHorseIdle ? HumanAnimations.HorseIdle : HumanAnimations.IdleM;
    }

    private void OnGUI()
    {
        // Only show prompt if this is the local player's cannon interaction
        if (!string.IsNullOrEmpty(currentPrompt) && humanInTrigger != null && humanInTrigger.IsMine())
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 24;
            style.alignment = TextAnchor.UpperCenter;
            style.normal.textColor = Color.white;

            GUI.Label(new Rect(Screen.width / 2 - 200, 10, 400, 50), currentPrompt, style);
        }
    }

    private void SetPrompt(string text)
    {
        // Only set prompt for local player
        if (humanInTrigger != null && humanInTrigger.IsMine())
        {
            currentPrompt = text;
            unmountPromptTimer = unmountPromptDuration;
        }
    }

    private void ClearPrompt()
    {
        // Only clear prompt for local player
        if (humanInTrigger != null && humanInTrigger.IsMine())
        {
            currentPrompt = "";
        }
    }
}