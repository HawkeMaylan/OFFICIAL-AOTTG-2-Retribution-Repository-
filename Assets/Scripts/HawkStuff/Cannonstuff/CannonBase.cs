using UnityEngine;
using Characters;
using Photon.Pun;
using UI;
using Settings;
using GameManagers;
using ApplicationManagers;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Collections;

[System.Serializable]
public class CannonProjectileOption
{
    public string name;
    public GameObject prefab;
    public float launchForce = 500f;
    public float upwardForce = 100f;
    public Sprite sprite;

    public float fireCooldown = 1f;
    public int projectileCount = 1;
    public float spreadAngle = 0f;

    public bool BarrelRecoil = false;
    public float RecoilDistance = 0.5f;
    public float barrelRecoilAngle = 8f;
    public float RecoilSpeed = 4f;

    public bool Knockback = false;
    public float knockbackForce = 100f;

    public int startingAmmo = 5; // Add this

}


public class CannonBase : MonoBehaviourPunCallbacks, IPunObservable
{

    private List<int> sharedAmmoCounts = new List<int>();

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(isMounted);
            stream.SendNext(selectedProjectileIndex);
            for (int i = 0; i < sharedAmmoCounts.Count; i++)
                stream.SendNext(sharedAmmoCounts[i]);
        }
        else
        {
            isMounted = (bool)stream.ReceiveNext();
            selectedProjectileIndex = (int)stream.ReceiveNext();
            for (int i = 0; i < sharedAmmoCounts.Count; i++)
                sharedAmmoCounts[i] = (int)stream.ReceiveNext();
        }
    }


    void Awake()
    {
        if (photonView != null)
            photonView.OwnershipTransfer = OwnershipOption.Takeover;


    }
   
    private string MountPromptText;
    private string UnmountPromptText;
    private string _lastCachedKey = "";


    [Header("Interaction Settings")]
    public Collider interactionZone;

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

    [Header("Rotation Settings")]
    public Transform CannonBarrel;
    public float rotationSpeed = 5f;
    public float maxHorizontalAngle = 90f;
    public float maxVerticalAngle = 45f;

    [Header("Movement Settings")]
    public Transform MoveTarget;
    public float moveSpeed = 5f;
    public float turnSpeed = 90f;

    [Header("Firing Settings")]
    public Transform firePoint;
    public List<CannonProjectileOption> projectileOptions = new List<CannonProjectileOption>();
    private float nextFireTime = 0f;

    [Header("Firing FX")]
    public GameObject muzzleFlashPrefab; // Must be in Resources folder


    [Header("Projectile UI")]
    public GameObject projectileUIPrefab;

    private int selectedProjectileIndex = 0;
    private Rigidbody moveRigidbody;
    private Human humanInTrigger;
    private Rigidbody humanRigidbody;
    private bool isMounted = false;
    private bool hasExitedAfterUnmount = false;

    private float originalMass;
    private bool originalUseGravity;

    private static string currentPrompt = "";
    private float unmountPromptTimer = 0f;
    private Vector3 lastMountedWorldPos = Vector3.zero;
    private bool isCurrentlyRunning = false;
    private GameObject currentUIImage;

    private Image currentUIImageRenderer;
    private bool hasFlashedReady = false;
    private Coroutine flashGreenRoutine;
    private bool isFlashingGreen = false;
    private bool isFlashingRed = false;

    private GameObject nextUIImage;
    private Image nextUIImageRenderer;
    private GameObject prevUIImage;
    private RectTransform prevRT, currRT, nextRT;
    private float uiLerpProgress = 1f;
    private int targetIndex = -1;
    private bool isSwapping = false;
    private float mountPromptExpireTime = -1f;

    [Header("Flip Settings")]
    public float flipHoldTime = 3f;
    public float flipSpeed = 1f;

    private float gHoldTimer = 0f;
    private bool isFlipping = false;
    private Coroutine flipRoutine;

    [Header("Audio Clips")]
    public AudioClip cooldownSound;
    public AudioClip movementSound;

    private AudioSource audioSource;

    private AudioSource cooldownAudioSource;
    private AudioSource movementLoopAudioSource;






    private void Start()
    {
        UpdatePromptTexts();
        cooldownAudioSource = gameObject.AddComponent<AudioSource>();
        cooldownAudioSource.loop = true;
        cooldownAudioSource.playOnAwake = false;
        cooldownAudioSource.spatialBlend = 1f;
        cooldownAudioSource.rolloffMode = AudioRolloffMode.Linear;
        cooldownAudioSource.minDistance = 5f;
        cooldownAudioSource.maxDistance = 40f;


        movementLoopAudioSource = gameObject.AddComponent<AudioSource>();
        movementLoopAudioSource.loop = true;
        movementLoopAudioSource.playOnAwake = false;
        movementLoopAudioSource.spatialBlend = 1f;
        movementLoopAudioSource.rolloffMode = AudioRolloffMode.Linear;
        movementLoopAudioSource.minDistance = 5f;
        movementLoopAudioSource.maxDistance = 40f;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();


        if (MoveTarget != null)
            moveRigidbody = MoveTarget.GetComponent<Rigidbody>();

        // Initialize shared ammo counts from scratch
        sharedAmmoCounts.Clear();
        foreach (var option in projectileOptions)
            sharedAmmoCounts.Add(option.startingAmmo);


        ClearPrompt();
    }


    private void Update()
    {
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
        if (isMounted && humanInTrigger != null && IsHumanGrabbed())
        {
            Debug.Log("CannonBase: Detaching due to player being grabbed.");
            DetachHuman();
            return;
        }
        HandleMountInput();
        HandleUnmountPromptTimer();
        HandleRunAnimation();
        CheckDistanceOrAliveStatus();

        if (!photonView.IsMine || !ValidateHumanInTrigger()) return;


        // Detect nearby human using interactionZone
        bool playerFound = false;

        if (!isMounted && humanInTrigger == null && interactionZone != null)
        {
            Collider[] hits = Physics.OverlapBox(
                interactionZone.bounds.center,
                interactionZone.bounds.extents,
                interactionZone.transform.rotation
            );

            foreach (var hit in hits)
            {
                Human h = hit.GetComponentInParent<Human>();
                if (h != null && h.IsMine())
                {
                    humanInTrigger = h;
                    humanRigidbody = h.GetComponent<Rigidbody>();
                    SetPrompt(MountPromptText);
                    mountPromptExpireTime = Time.time + 10f;
                    playerFound = true;
                    break;
                }
            }
        }

        // Clear if player leaves zone
        if (!playerFound && humanInTrigger != null)
        {
            float dist = Vector3.Distance(humanInTrigger.transform.position, transform.position);
            if (dist > 40f || !interactionZone.bounds.Contains(humanInTrigger.transform.position))
            {
                humanInTrigger = null;
                humanRigidbody = null;
                ClearPrompt();
                mountPromptExpireTime = -1f;
            }
        }

        // Handle flipping by holding G in range for 3 seconds
        if (!isMounted && humanInTrigger != null && humanInTrigger.IsMine())
        {
            if (SettingsManager.InputSettings.Interaction.Interact.GetKeyDown() && !InGameMenu.InMenu() && !ChatManager.IsChatActive())
            {
                if (interactionZone.bounds.Contains(humanInTrigger.transform.position))
                {
                    gHoldTimer += Time.deltaTime;

                    if (gHoldTimer >= flipHoldTime && !isFlipping)
                    {
                        if (!photonView.IsMine)
                            photonView.RequestOwnership();

                        photonView.RPC("RPC_FlipCannonUpright", RpcTarget.All);
                        gHoldTimer = 0f;
                    }
                }
                else
                {
                    CancelFlipHold();
                }
            }
            else
            {
                CancelFlipHold();
            }
        }

        // Auto-dismount if cannon tips over while mounted
        if (isMounted && humanInTrigger != null)
        {
            Vector3 euler = transform.rotation.eulerAngles;
            float x = NormalizeAngle(euler.x);
            float z = NormalizeAngle(euler.z);

            if (Mathf.Abs(x) > 90f || Mathf.Abs(z) > 90f)
            {
                DetachHuman();
                ClearPrompt();
            }
        }

        // Fire cannon
        if (isMounted && humanInTrigger != null && humanInTrigger.IsMine() && (SettingsManager.InputSettings.Human.FireCannon.GetKeyDown()) && !InGameMenu.InMenu() && !ChatManager.IsChatActive())
        {
            if (Time.time >= nextFireTime && projectileOptions.Count > 0)
            {
                nextFireTime = Time.time + projectileOptions[selectedProjectileIndex].fireCooldown;
                RPC_FireProjectile(selectedProjectileIndex);
                photonView.RPC("RPC_PlayFiringEffects", RpcTarget.Others, selectedProjectileIndex);

            }
            else if (cooldownSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(cooldownSound);
            }
        }


        // Switch projectile selection
        if (isMounted)
        {
            if (SettingsManager.InputSettings.Human.ChangeAmmoL.GetKeyDown() && !InGameMenu.InMenu() && !ChatManager.IsChatActive())
                SelectProjectile((selectedProjectileIndex - 1 + projectileOptions.Count) % projectileOptions.Count);
            else if (SettingsManager.InputSettings.Human.ChangeAmmoR.GetKeyDown() && !InGameMenu.InMenu() && !ChatManager.IsChatActive())
                SelectProjectile((selectedProjectileIndex + 1) % projectileOptions.Count);
        }

        HandleCooldownUI();
        HandleProjectileUISwap();
        RotateTowardsCamera();


        if (isMounted && (!ValidateHumanInTrigger() || humanInTrigger.MountedTransform != mountPoint))
        {
            Debug.LogWarning("CannonBase: Mounted human invalid or removed. Detaching.");
            DetachHuman();
        }


    }





    private void FixedUpdate()
    {
        if (!ValidateHumanInTrigger()) return;
        HandleMovementInput();
    }

    public void SelectProjectile(int index)
    {
        if (isSwapping || index == selectedProjectileIndex)
            return;

        int count = projectileOptions.Count;
        selectedProjectileIndex = (index + count) % count;
        UpdateProjectileUI();
        uiLerpProgress = 0f;
        isSwapping = true;
        nextFireTime = Time.time + projectileOptions[selectedProjectileIndex].fireCooldown;
    }
    private void HandleCooldownUI()
    {
        if (currentUIImageRenderer == null || isFlashingRed) return;

        float cooldown = projectileOptions[selectedProjectileIndex].fireCooldown;
        float timeSinceFire = Time.time - (nextFireTime - cooldown);
        float progress = Mathf.Clamp01(timeSinceFire / cooldown);

        if (!isFlashingGreen)
            currentUIImageRenderer.color = Color.Lerp(Color.gray, Color.white, progress);

        if (progress >= 1f && !hasFlashedReady)
        {
            if (flashGreenRoutine != null)
                StopCoroutine(flashGreenRoutine);

            flashGreenRoutine = StartCoroutine(FlashGreen());
            hasFlashedReady = true;

            if (cooldownAudioSource.isPlaying)
                cooldownAudioSource.Stop();
        }
        else if (progress < 1f)
        {
            hasFlashedReady = false;

            if (!cooldownAudioSource.isPlaying && cooldownSound != null)
            {
                cooldownAudioSource.clip = cooldownSound;
                cooldownAudioSource.Play();
            }
        }
    }



    private void HandleProjectileUISwap()
    {
        if (isSwapping && uiLerpProgress < 1f)
        {
            uiLerpProgress += Time.deltaTime * 4f;
            float t = Mathf.SmoothStep(0, 1, uiLerpProgress);

            Vector2 center = new Vector2(-180f, 100f);
            Vector2 offset = new Vector2(90f, 0f);

            if (prevRT) prevRT.anchoredPosition = Vector2.Lerp(center - offset * 2, center - offset, t);
            if (currRT) currRT.anchoredPosition = Vector2.Lerp(center, center + (targetIndex > selectedProjectileIndex ? -offset : offset), t);
            if (nextRT) nextRT.anchoredPosition = Vector2.Lerp(center + offset, center + offset * 2, t);

            if (uiLerpProgress >= 1f)
            {
                isSwapping = false;
                UpdateProjectileUI();
            }
        }
    }

    
    public void RPC_FireProjectile(int index)

    {
        if (!ValidateHumanInTrigger()) return;

        var selected = projectileOptions[index];
    if (sharedAmmoCounts[index] <= 0)
    {
        if (photonView.IsMine)
            StartCoroutine(FlashRed());
        return;
    }


    // Spawn projectile (only owner)
    if (photonView.IsMine)
        {
            if (muzzleFlashPrefab != null)
            {
                string flashPath = $"Buildables/Projectiles/{muzzleFlashPrefab.name}";
                PhotonNetwork.Instantiate(flashPath, firePoint.position, firePoint.rotation);
            }

            string projectilePath = $"Buildables/Projectiles/{selected.prefab.name}";
            int count = Mathf.Max(1, selected.projectileCount);
            float spread = selected.spreadAngle;

            for (int i = 0; i < count; i++)
            {
                Vector3 direction = firePoint.forward;
                direction = Quaternion.AngleAxis(Random.Range(-spread, spread), firePoint.up) * direction;
                direction = Quaternion.AngleAxis(Random.Range(-spread, spread), firePoint.right) * direction;

                GameObject projectile = PhotonNetwork.Instantiate(projectilePath, firePoint.position, Quaternion.LookRotation(direction));
                if (projectile.TryGetComponent<Rigidbody>(out var rb))
                {
                    rb.AddForce(direction * selected.launchForce + firePoint.up * selected.upwardForce);
                }
            }

        sharedAmmoCounts[index]--;
        UpdateProjectileUI();

    }


    if (selected.BarrelRecoil && CannonBarrel != null)
            StartCoroutine(BarrelRecoil(CannonBarrel, selected.RecoilDistance, selected.barrelRecoilAngle, selected.RecoilSpeed));


        if (selected.Knockback && moveRigidbody != null)
        {
            Vector3 backwardForce = -firePoint.forward * selected.knockbackForce;
            moveRigidbody.AddForce(backwardForce, ForceMode.Impulse);
        }
    }



    [PunRPC]
    public void RPC_PlayFiringEffects(int index)
    {
        var selected = projectileOptions[index];

        if (selected.BarrelRecoil && CannonBarrel != null)
            StartCoroutine(BarrelRecoil(CannonBarrel, selected.RecoilDistance, selected.barrelRecoilAngle, selected.RecoilSpeed));

        if (selected.Knockback && moveRigidbody != null)
        {
            Vector3 backwardForce = -firePoint.forward * selected.knockbackForce;
            moveRigidbody.AddForce(backwardForce, ForceMode.Impulse);
        }

        
    }






    private void RotateTowardsCamera()
    {
        if (!photonView.IsMine || !ValidateHumanInTrigger() || !isMounted || CannonBarrel == null || Camera.main == null)
            return;
       
        Vector3 localForward = transform.InverseTransformDirection(Camera.main.transform.forward);
        float yaw = Mathf.Atan2(localForward.x, localForward.z) * Mathf.Rad2Deg;
        float pitch = -Mathf.Asin(localForward.y) * Mathf.Rad2Deg;

        yaw = Mathf.Clamp(yaw, -maxHorizontalAngle, maxHorizontalAngle);
        pitch = Mathf.Clamp(pitch, -maxVerticalAngle, maxVerticalAngle);

        Quaternion targetRotation = Quaternion.Euler(pitch, yaw, 0f);
        CannonBarrel.localRotation = Quaternion.Slerp(CannonBarrel.localRotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void HandleMovementInput()
    {
        if (moveRigidbody == null) return;

        // Block movement unless local player owns it
        if (!photonView.IsMine || !isMounted) return;

        float move = 0f;
        float rotate = 0f;

        if (SettingsManager.InputSettings.General.Forward.GetKey() && !InGameMenu.InMenu() && !ChatManager.IsChatActive()) move += 1f;
        if (SettingsManager.InputSettings.General.Back.GetKey() && !InGameMenu.InMenu() && !ChatManager.IsChatActive()) move -= 1f;
        if (SettingsManager.InputSettings.General.Right.GetKey() && !InGameMenu.InMenu() && !ChatManager.IsChatActive()) rotate += 1f;
        if (SettingsManager.InputSettings.General.Left.GetKey() && !InGameMenu.InMenu() && !ChatManager.IsChatActive()) rotate -= 1f;


        Vector3 forwardMovement = Vector3.ProjectOnPlane(MoveTarget.forward, Vector3.up).normalized * move * moveSpeed * Time.fixedDeltaTime;
        moveRigidbody.MovePosition(moveRigidbody.position + forwardMovement);

        Quaternion deltaRotation = Quaternion.Euler(0f, rotate * turnSpeed * Time.fixedDeltaTime, 0f);
        moveRigidbody.MoveRotation(moveRigidbody.rotation * deltaRotation);

        // Movement sound
        bool isMoving = Mathf.Abs(move) > 0f;
        if (isMoving)
        {
            if (!movementLoopAudioSource.isPlaying && movementSound != null)
            {
                movementLoopAudioSource.clip = movementSound;
                movementLoopAudioSource.Play();
            }
        }
        else if (movementLoopAudioSource.isPlaying)
        {
            movementLoopAudioSource.Stop();
        }
    }





    private void CheckDistanceOrAliveStatus()
    {
        if (!isMounted || humanInTrigger == null) return;

        bool isTooFar = Vector3.Distance(transform.position, humanInTrigger.transform.position) > 40f;
        bool isDead = humanInTrigger.Dead;
        bool isGrabbed = IsHumanGrabbed(); // Check if player is grabbed

        if (isTooFar || isDead || isGrabbed) // Added isGrabbed condition
        {
            Debug.LogWarning("CannonBase: Detaching due to distance, death, or grab.");
            DetachHuman(); // Force detach on death/grab
            ClearPrompt();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isMounted) return;

        Human human = other.GetComponentInParent<Human>();
        if (human != null && human.IsMine())
        {
            humanInTrigger = human;
            humanRigidbody = human.GetComponent<Rigidbody>();
            hasExitedAfterUnmount = false;

            SetPrompt(MountPromptText);
            mountPromptExpireTime = Time.time + 10f;
        }
    }


    private void OnTriggerExit(Collider other)
    {
        Human human = other.GetComponentInParent<Human>();
        if (human != null && human == humanInTrigger && !isMounted)
        {
            hasExitedAfterUnmount = true;
            humanInTrigger = null;
            humanRigidbody = null;
            ClearPrompt();
            mountPromptExpireTime = -1f;
        }
    }

    private void HandleMountInput()
    {
        // Auto-clear prompt if it times out
        if (mountPromptExpireTime > 0f && Time.time > mountPromptExpireTime)
        {
            ClearPrompt();
            mountPromptExpireTime = -1f;
            return;
        }

        if (humanInTrigger == null) return;

        // Prevent mount/unmount if player is no longer actually at this mount point
        if (isMounted && humanInTrigger.MountedTransform != mountPoint)
            return;

        if (!InGameMenu.InMenu() && !ChatManager.IsChatActive())
        {
            if (SettingsManager.InputSettings.Interaction.Interact.GetKeyDown())
            {
                // Normalize X/Z angles and check if tipped
                Vector3 euler = transform.rotation.eulerAngles;
                float x = NormalizeAngle(euler.x);
                float z = NormalizeAngle(euler.z);

                bool isTipped = Mathf.Abs(x) > 90f || Mathf.Abs(z) > 90f;

                if (isTipped)
                {
                    // Flip instead of mount
                    if (!isFlipping)
                    {
                        if (!photonView.IsMine)
                            photonView.RequestOwnership();

                        photonView.RPC("RPC_FlipCannonUpright", RpcTarget.All);
                        isFlipping = true;
                    }
                    return; // Skip mounting if flipping
                }

                // Mount or unmount
                if (!isMounted && !hasExitedAfterUnmount)
                {
                    AttachHuman();
                }
                else if (isMounted && humanInTrigger.MountedTransform == mountPoint)
                {
                    DetachHuman();
                    mountPromptExpireTime = -1f;
                }
            }
        }
    }





    private void HandleUnmountPromptTimer()
                    {
                        if (isMounted && unmountPromptTimer > 0f)
                        {
                            unmountPromptTimer -= Time.deltaTime;
                            if (unmountPromptTimer <= 0f)
                                ClearPrompt();
                        }
                    }

                    private float NormalizeAngle(float angle)
    {
        angle = angle % 360f;
        if (angle > 180f) angle -= 360f;
        return angle;
    }


    private void HandleRunAnimation()
    {
        if (!isMounted || humanInTrigger == null || !enableRunAnimation) return;
        if (humanInTrigger.MountedTransform == null) return;

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
        else if (isCurrentlyRunning)
        {
            humanInTrigger.CrossFadeIfNotPlaying(GetIdleAnimation(), 0.1f);
            isCurrentlyRunning = false;
        }
    }

    private void AttachHuman()

    {
        if (!ValidateHumanInTrigger()) return;

        if (humanInTrigger == null || mountPoint == null || isMounted)
        {
            Debug.LogWarning("CannonBase: Invalid mount attempt – missing human or already mounted.");
            return;
        }

        if (!photonView.IsMine)
        {
            Debug.Log("Requesting ownership before mounting.");
            photonView.RequestOwnership();
        }

        // Confirm human isn't already mounted to something else
        if (humanInTrigger.MountState != HumanMountState.None && humanInTrigger.MountedTransform != mountPoint)
        {
            Debug.LogWarning("CannonBase: Human is already mounted elsewhere.");
            return;
        }

        // Double-check MoveTarget is valid
        if (MoveTarget == null || moveRigidbody == null)
        {
            Debug.LogError("CannonBase: MoveTarget or Rigidbody is null – cannot mount.");
            return;
        }

        // Sync mount
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

            if (disableGravityOnMount) humanRigidbody.useGravity = false;
            if (disableMassOnMount) humanRigidbody.mass = mountedMass;
        }

        isMounted = true;
        hasExitedAfterUnmount = false;

        Debug.Log("CannonBase: Human mounted successfully.");

        ClearPrompt();
        SetPrompt(UnmountPromptText);
        mountPromptExpireTime = Time.time + unmountPromptDuration;
        unmountPromptTimer = unmountPromptDuration;
        lastMountedWorldPos = humanInTrigger.MountedTransform.TransformPoint(humanInTrigger.MountedPositionOffset);

        humanInTrigger.CrossFadeIfNotPlaying(GetIdleAnimation(), 0.2f);
        UpdateProjectileUI();
    }




    private void DetachHuman()
    {
        if (humanInTrigger == null) return;

        // Always destroy UI 
        if (currentUIImage != null) Destroy(currentUIImage);
        if (nextUIImage != null) Destroy(nextUIImage);
        if (prevUIImage != null) Destroy(prevUIImage);

        nextUIImageRenderer = null;
        currentUIImageRenderer = null;
        prevRT = currRT = nextRT = null;

        // Stop movement and cooldown sounds
        if (movementLoopAudioSource.isPlaying)
            movementLoopAudioSource.Stop();

        if (cooldownAudioSource.isPlaying)
            cooldownAudioSource.Stop();

        if (!ValidateHumanInTrigger())
        {
            humanInTrigger = null;
            humanRigidbody = null;
            isMounted = false;
            return;
        }

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
            unmountPromptTimer = 0f;
        }
        else
        {
            ClearPrompt();
        }

        humanInTrigger = null;
        humanRigidbody = null;
    }


    private void UpdateProjectileUI()
    {
        if (humanInTrigger == null || !humanInTrigger.IsMine()) return;

        GameObject menu = GameObject.Find("DefaultMenu(Clone)");
        if (menu == null) return;

        if (prevUIImage) Destroy(prevUIImage);
        if (currentUIImage) Destroy(currentUIImage);
        if (nextUIImage) Destroy(nextUIImage);

        int count = projectileOptions.Count;
        int prevIndex = (selectedProjectileIndex - 1 + count) % count;
        int nextIndex = (selectedProjectileIndex + 1) % count;

        Vector2 center = new Vector2(-180f, 100f);
        Vector2 offset = new Vector2(90f, 0f);

        prevUIImage = Instantiate(projectileUIPrefab, menu.transform);
        prevRT = prevUIImage.GetComponent<RectTransform>();
        prevRT.anchoredPosition = center - offset;
        prevRT.sizeDelta = new Vector2(100f, 100f);
        prevRT.localScale = Vector3.one * 0.8f;
        prevRT.anchorMin = prevRT.anchorMax = new Vector2(1f, 0f);
        prevRT.pivot = new Vector2(0.5f, 0.5f);
        prevUIImage.GetComponent<Image>().sprite = projectileOptions[prevIndex].sprite;
        prevUIImage.GetComponent<Image>().color = Color.gray;

        currentUIImage = Instantiate(projectileUIPrefab, menu.transform);
        currRT = currentUIImage.GetComponent<RectTransform>();
        currRT.anchoredPosition = center;
        currRT.sizeDelta = new Vector2(130f, 130f);
        currRT.localScale = Vector3.one;
        currRT.anchorMin = currRT.anchorMax = new Vector2(1f, 0f);
        currRT.pivot = new Vector2(0.5f, 0.5f);
        currentUIImageRenderer = currentUIImage.GetComponent<Image>();
        currentUIImageRenderer.sprite = projectileOptions[selectedProjectileIndex].sprite;

        var ammoTextObj = currentUIImage.transform.Find("AmmoText");
        if (ammoTextObj)
        {
            var ammoText = ammoTextObj.GetComponent<Text>();
            ammoText.text = $"x{sharedAmmoCounts[selectedProjectileIndex]}";

         }

    nextUIImage = Instantiate(projectileUIPrefab, menu.transform);
        nextRT = nextUIImage.GetComponent<RectTransform>();
        nextRT.anchoredPosition = center + offset;
        nextRT.sizeDelta = new Vector2(100f, 100f);
        nextRT.localScale = Vector3.one * 0.8f;
        nextRT.anchorMin = nextRT.anchorMax = new Vector2(1f, 0f);
        nextRT.pivot = new Vector2(0.5f, 0.5f);
        nextUIImage.GetComponent<Image>().sprite = projectileOptions[nextIndex].sprite;
        nextUIImage.GetComponent<Image>().color = Color.gray;
    }

    private string GetIdleAnimation()
    {
        return useHorseIdle ? HumanAnimations.HorseIdle : HumanAnimations.IdleM;
    }

    private void OnGUI()
    {
        if (humanInTrigger == null || !humanInTrigger.IsMine()) return;

        if (!string.IsNullOrEmpty(currentPrompt))
        {
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(Screen.width / 2 - 150, 10, 300, 50), currentPrompt, style);
        }
    }

    private void SetPrompt(string text)
    {
        if (humanInTrigger == null || !humanInTrigger.IsMine()) return;
        currentPrompt = text;
    }

    private void ClearPrompt()
    {
        if (humanInTrigger == null || !humanInTrigger.IsMine()) return;
        currentPrompt = "";
    }

    private IEnumerator FlashRed()
    {
        if (!photonView.IsMine) yield break;
        if (currentUIImageRenderer == null) yield break;

        isFlashingRed = true;

        Color original = currentUIImageRenderer.color;
        currentUIImageRenderer.color = Color.red;

        yield return new WaitForSeconds(0.2f);

        currentUIImageRenderer.color = original;
        isFlashingRed = false;
    }


    private IEnumerator FlashGreen()
    {
        if (!photonView.IsMine) yield break;

        if (currentUIImageRenderer == null) yield break;

        isFlashingGreen = true;
        currentUIImageRenderer.color = Color.green;
        yield return new WaitForSeconds(0.3f);
        currentUIImageRenderer.color = Color.white;
        isFlashingGreen = false;
    }

    private IEnumerator BarrelRecoil(Transform barrel, float distance, float angle, float speed)
    {
        if (!photonView.IsMine) yield break;

        Vector3 originalPos = barrel.localPosition;
        Quaternion originalRot = barrel.localRotation;

        Vector3 recoilPos = originalPos - Vector3.forward * distance;
        Quaternion recoilRot = originalRot * Quaternion.Euler(-angle, 0f, 0f);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * speed;
            barrel.localPosition = Vector3.Lerp(originalPos, recoilPos, t);
            barrel.localRotation = Quaternion.Slerp(originalRot, recoilRot, t);
            yield return null;
        }

        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * speed;
            barrel.localPosition = Vector3.Lerp(recoilPos, originalPos, t);
            barrel.localRotation = Quaternion.Slerp(recoilRot, originalRot, t);
            yield return null;
        }
    }
    [PunRPC]
    public void RPC_FlipCannonUpright()
    {
        if (flipRoutine != null)
            StopCoroutine(flipRoutine);

        flipRoutine = StartCoroutine(FlipCannonOverTime(3f));
    }

    private IEnumerator FlipCannonOverTime(float duration)
    {
        isFlipping = true;
        Quaternion startRot = transform.rotation;
        Quaternion endRot = Quaternion.Euler(0f, transform.rotation.eulerAngles.y, 0f);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            transform.rotation = Quaternion.Slerp(startRot, endRot, t / duration);
            yield return null;
        }

        transform.rotation = endRot;
        isFlipping = false;
    }


    private void CancelFlipHold()
    {
        gHoldTimer = 0f;
    }

    private bool ValidateHumanInTrigger()
    {
        if (humanInTrigger == null)
            return false;

        bool isDead = humanInTrigger.Dead || !humanInTrigger.gameObject.activeInHierarchy;
        bool isNotMine = !humanInTrigger.IsMine();
        bool isGrabbed = IsHumanGrabbed(); // Check if grabbed

        return !(isDead || isNotMine || isGrabbed); // Added isGrabbed
    }


    private bool IsHumanGrabbed()
    {
        return humanInTrigger != null && humanInTrigger.State == HumanState.Grab;
    }

    private void UpdatePromptTexts()
    {
        string key = SettingsManager.InputSettings.Interaction.Interact.ToString().Replace("Alpha", "");
        MountPromptText = $"Press {key} to Mount";
        UnmountPromptText = $"Press {key} to Unmount";
    }


}
