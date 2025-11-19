using UnityEngine;
using ApplicationManagers;
using GameManagers;
using Utility;
using Settings;
using Photon.Pun;
using Photon.Realtime;
using UI;

namespace Characters
{
    class Horse : BaseCharacter
    {
        Human _owner;
        HorseComponentCache HorseCache;
        public HorseState State;
        private float WalkSpeed = 15f;
        private float RunCloseSpeed = 20f;
        private float TeleportTime = 10f;
        protected override Vector3 Gravity => Vector3.down * 30f;
        private float JumpForce = 15f;
        private float _idleTimeLeft;
        private float _teleportTimeLeft;
        private float _jumpCooldownLeft;

        public float SprintSpeedMultiplier = 1.3f;
        public float StaminaDrainRate = 15f; // per second while sprinting
        public float StaminaRegenRate = 2f; // per second normally
        public float StaminaIdleRegenBonus = 2f; // extra regen when idle
        public float MaxStamina = 100f;
        public float CurrentStamina = 100f;
        public bool IsSprinting = false;
        private GameObject _staminaBar;
        private UnityEngine.UI.Image _staminaBarFill;

        private bool _isWhistleActive = false;
        private float _whistleTimer = 0f;
        private const float WhistleDuration = 8f;

        public int MountedStatus = 0; // 0 = No rider, 1 = Rider mounted


        public void Init(Human human)
        {
            if (human == null) return;

            _owner = human;
            base.Init(true, human.Team);  // Sets team and health etc

            CurrentStamina = MaxStamina;
            CreateStaminaBar();

            if (photonView.IsMine && Cache.Rigidbody != null)
            {
                Cache.Rigidbody.isKinematic = false;
            }

            Debug.Log($"[Horse] Init completed for {human.name} on actor {PhotonNetwork.LocalPlayer.ActorNumber}");
        }



        protected override void CreateCache(BaseComponentCache cache)
        {
            HorseCache = new HorseComponentCache(gameObject);
            base.CreateCache(HorseCache);
        }

        public void Jump()
        {
            if (_jumpCooldownLeft > 0f || !Grounded || CurrentStamina <= 15)
                return;

            Cache.Rigidbody.AddForce(Vector3.up * JumpForce, ForceMode.VelocityChange);
            Cache.Rigidbody.AddForce(Cache.Transform.forward * JumpForce / 2, ForceMode.VelocityChange);
            _jumpCooldownLeft = 0f;
            CurrentStamina -= StaminaDrainRate;
        }

        public void HorseWhistle()
        {
            if (_owner == null || _owner.Dead)
                return;

            float flatDistance = Util.DistanceIgnoreY(_owner.Cache.Transform.position, Cache.Transform.position);
            if (flatDistance > 800f)
                return;

            _isWhistleActive = true;
            _whistleTimer = WhistleDuration;
            State = HorseState.RunToPoint;
        }

        protected override void Awake()
        {
            base.Awake();
            if (IsMine())
                CrossFade(HorseAnimations.Idle0, 0.1f);
            HorseCache.Dust.Play();
            ToggleDust(false);
        }

        private void ToggleDust(bool toggle)
        {
            var emission = HorseCache.Dust.emission;
            emission.enabled = toggle;
        }

        public void TeleportToHuman()
        {
            Vector3 position = _owner.Cache.Transform.position + Vector3.right * UnityEngine.Random.Range(-2f, 2f) + Vector3.forward * UnityEngine.Random.Range(-2f, 2f);
            position.y = GetHeight(position) + 1f;
            Cache.Transform.position = position;
            _teleportTimeLeft = TeleportTime;
        }

        private float GetHeight(Vector3 pt)
        {
            RaycastHit hit;
            if (Physics.Raycast(pt + Vector3.up * 1f, -Vector3.up, out hit, 1000f, GroundMask))
                return hit.point.y;
            return 0f;
        }

        private void UpdateIdle()
        {
            _idleTimeLeft -= Time.deltaTime;
            if (_idleTimeLeft > 0f)
                return;

            if (!Animation.IsPlaying(HorseAnimations.Idle0))
            {
                CrossFade(HorseAnimations.Idle0, 0.1f);
                _idleTimeLeft = UnityEngine.Random.Range(6f, 9f);
                return;
            }

            float choose = UnityEngine.Random.Range(0f, 1f);
            if (choose < 0.25f)
                IdleOneShot(HorseAnimations.Idle1);
            else if (choose < 0.5f)
            {
                IdleOneShot(HorseAnimations.Idle2);
                if (SettingsManager.SoundSettings.HorseSoundEffect.Value)
                    PlaySound(HorseSounds.Idle1);
            }
            else if (choose < 0.75f)
            {
                IdleOneShot(HorseAnimations.Idle3);
                if (SettingsManager.SoundSettings.HorseSoundEffect.Value)
                    PlaySound(HorseSounds.Idle2);
            }
            else
            {
                IdleOneShot(HorseAnimations.Crazy);
                if (SettingsManager.SoundSettings.HorseSoundEffect.Value)
                    PlaySound(HorseSounds.Idle3);
            }
        }

        private void IdleOneShot(string animation)
        {
            CrossFade(animation, 0.1f);
            _idleTimeLeft = Animation.GetLength(animation);
        }

        private float _initTimeout = 5f;

        private void Update()
        {
            if (_owner == null)
            {
                // Wait patiently for Init() without destroying
                return;
            }

            if (_owner.Dead)
            {
                if (photonView.IsMine || PhotonNetwork.IsMasterClient)
                {
                    PhotonNetwork.Destroy(gameObject);
                }
                return;
            }


            CheckSprintInput();
            UpdateStamina();
            UpdateStaminaBar();




            //  Update MountedStatus automatically
            MountedStatus = (_owner.MountState == HumanMountState.Horse) ? 1 : 0;

            if (_owner.MountState == HumanMountState.Horse)
            {
                if (_owner.HasDirection)
                {
                    State = _owner.IsWalk ? HorseState.ControlledWalk : HorseState.ControlledRun;
                    // No rotation here — handled physically in FixedUpdate
                }

                else
                {
                    State = HorseState.ControlledIdle;
                }
            }
            else
            {
                _teleportTimeLeft -= Time.deltaTime;

                float flatDistance = Util.DistanceIgnoreY(_owner.Cache.Transform.position, Cache.Transform.position);

                if (_isWhistleActive)
                {
                    _whistleTimer -= Time.deltaTime;

                    if (_whistleTimer <= 0f || flatDistance < 3f)
                    {
                        _isWhistleActive = false;
                        State = HorseState.Idle;
                    }
                    else
                    {
                        State = HorseState.RunToPoint;
                    }
                }
                else
                {
                    if (flatDistance < 5f)
                        State = HorseState.Idle;
                    else if (flatDistance < 25f)
                        State = HorseState.WalkToPoint;
                    else
                        State = HorseState.Idle;
                }

                // Only apply rotation if not mounted and actively walking/running toward player
                if (_owner.MountState != HumanMountState.Horse &&
                    (State == HorseState.WalkToPoint || State == HorseState.RunToPoint))
                {
                    Vector3 direction = (_owner.Cache.Transform.position - Cache.Transform.position);
                    direction.y = 0f;

                    if (direction != Vector3.zero)
                        Cache.Transform.rotation = Quaternion.Lerp(Cache.Transform.rotation, Quaternion.LookRotation(direction.normalized), 10f * Time.deltaTime);
                }

            }


        }

        [PunRPC]
        public void RPC_SetHorseOwner(int actorNumber)
        {
            foreach (var human in FindObjectsOfType<Human>())
            {
                if (human.photonView != null && human.photonView.Owner != null &&
                    human.photonView.Owner.ActorNumber == actorNumber)
                {
                    Init(human);          // Set _owner = human on ALL clients
                    human.Horse = this;   // Link horse to human on ALL clients
                    break;
                }
            }
        }



        private void UpdateStamina()
        {
            if (!IsMine()) return;

            if (IsSprinting)
            {
                // Drain stamina while sprinting
                CurrentStamina -= StaminaDrainRate * Time.deltaTime;
                if (CurrentStamina <= 0)
                {
                    CurrentStamina = 0;
                    ToggleSprint(false);
                }
            }
            else if (CurrentStamina < MaxStamina)
            {
                // Regen stamina when not sprinting
                float regenRate = StaminaRegenRate;

                // Bonus regen when idle
                if (State == HorseState.Idle || State == HorseState.ControlledIdle)
                {
                    regenRate += StaminaIdleRegenBonus;
                }

                CurrentStamina += regenRate * Time.deltaTime;
                CurrentStamina = Mathf.Min(CurrentStamina, MaxStamina);
            }
        }



        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            if (!IsMine() || _owner == null || _owner.Dead)
                return;

            CheckGround();

            // Check if horse is attached to a wagon
            AttachToHorseTrigger wagon = GetComponentInChildren<AttachToHorseTrigger>();
            bool isAttachedToWagon = wagon != null && wagon.IsAttachedToThisHorse(this);

            if (isAttachedToWagon)
            {
                float speedTarget = (State == HorseState.ControlledRun || State == HorseState.RunToPoint) ? _owner.Stats.HorseSpeed :
                                  (State == HorseState.ControlledWalk || State == HorseState.WalkToPoint) ? WalkSpeed : 0f;

                // Apply sprint multiplier if sprinting
                if (IsSprinting && speedTarget > 0 && CurrentStamina > 0)
                {
                    speedTarget *= SprintSpeedMultiplier;
                }

                if (speedTarget > 0f)
                {
                    if (_owner.HasDirection && _owner.MountState == HumanMountState.Horse)
                    {
                        Quaternion targetRot = _owner.GetTargetRotation();
                        Cache.Transform.rotation = Quaternion.Slerp(Cache.Transform.rotation, targetRot, 1f * Time.fixedDeltaTime);
                    }

                    Cache.Rigidbody.AddForce(Cache.Transform.forward * speedTarget, ForceMode.Force);
                }
            }
            else
            {
                // Restore manual turning when mounted
                if (_owner.MountState == HumanMountState.Horse && _owner.HasDirection)
                {
                    Quaternion targetRot = _owner.GetTargetRotation();
                    Quaternion newRot = Quaternion.Slerp(Cache.Transform.rotation, targetRot, 5f * Time.fixedDeltaTime);
                    Cache.Transform.rotation = newRot;
                }

                // Movement and following logic
                if (State == HorseState.ControlledIdle || State == HorseState.Idle)
                {
                    if (Grounded)
                    {
                        if (Cache.Rigidbody.velocity.magnitude < 1f)
                            Cache.Rigidbody.velocity = Vector3.up * Cache.Rigidbody.velocity.y;
                        else
                            Cache.Rigidbody.AddForce(
                                -Cache.Rigidbody.velocity.normalized * Mathf.Min(_owner.Stats.HorseSpeed, Cache.Rigidbody.velocity.magnitude * 0.5f),
                                ForceMode.Acceleration);
                    }
                }
                else if (State == HorseState.WalkToPoint || State == HorseState.RunToPoint ||
                        State == HorseState.ControlledWalk || State == HorseState.ControlledRun)
                {
                    float speed = _owner.Stats.HorseSpeed;
                    if (State == HorseState.ControlledWalk)
                        speed = WalkSpeed;
                    else if (State == HorseState.WalkToPoint)
                        speed = RunCloseSpeed;

                    // Apply sprint multiplier if sprinting
                    if (IsSprinting && speed > 0 && CurrentStamina > 0)
                    {
                        speed *= SprintSpeedMultiplier;
                    }

                    // If following player and not mounted, rotate toward them
                    if (_owner.MountState != HumanMountState.Horse &&
                        (State == HorseState.WalkToPoint || State == HorseState.RunToPoint))
                    {
                        Vector3 moveDir = _owner.Cache.Transform.position - Cache.Transform.position;
                        moveDir.y = 0f;
                        if (moveDir != Vector3.zero)
                        {
                            Quaternion targetRot = Quaternion.LookRotation(moveDir.normalized);
                            Cache.Transform.rotation = Quaternion.Slerp(Cache.Transform.rotation, targetRot, 5f * Time.fixedDeltaTime);
                        }
                    }

                    Cache.Rigidbody.AddForce(Cache.Transform.forward * speed, ForceMode.Acceleration);

                    if (Cache.Rigidbody.velocity.magnitude >= speed)
                    {
                        Cache.Rigidbody.AddForce((Mathf.Max(speed - Cache.Rigidbody.velocity.magnitude, -1f)) * Cache.Rigidbody.velocity.normalized,
                            ForceMode.VelocityChange);
                    }
                }
            }

            // Always apply gravity
            Cache.Rigidbody.AddForce(Gravity, ForceMode.Acceleration);
        }


        private void CreateStaminaBar()
        {
            if (!IsMine()) return;

            var menu = GameObject.Find("DefaultMenu(Clone)");
            if (menu == null) return;

            // Create stamina bar parent object
            _staminaBar = new GameObject("StaminaBar");
            _staminaBar.transform.SetParent(menu.transform);

            // Set up the background
            var bg = _staminaBar.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0.2f, 0.2f, 0.2f); // Dark gray background
            bg.type = UnityEngine.UI.Image.Type.Sliced;

            // Create fill object
            var fillObject = new GameObject("Fill");
            fillObject.transform.SetParent(_staminaBar.transform);
            _staminaBarFill = fillObject.AddComponent<UnityEngine.UI.Image>();
            _staminaBarFill.color = Color.white;
            _staminaBarFill.type = UnityEngine.UI.Image.Type.Filled;
            _staminaBarFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            _staminaBarFill.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Right;



            var fillRect = _staminaBarFill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;

            _staminaBar.SetActive(false);
        }

        private void UpdateStaminaBar()
        {
            if (_staminaBarFill != null)
            {
                _staminaBarFill.fillAmount = CurrentStamina / MaxStamina;
                _staminaBar.SetActive(IsSprinting || CurrentStamina < MaxStamina);
                // Set up RectTransforms
                var rect = _staminaBar.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0);
                rect.anchorMax = new Vector2(0.5f, 0);
                rect.pivot = new Vector2(0.5f, 0);
                rect.anchoredPosition = new Vector2(0, 100);
                rect.sizeDelta = new Vector2(2 * CurrentStamina, 20);
                bool shouldShow = (_owner.MountState == HumanMountState.Horse) &&
                         (IsSprinting || CurrentStamina < MaxStamina);
                _staminaBar.SetActive(shouldShow);
            }
        }

        public void ToggleSprint(bool sprint)
        {
            if (!IsMine()) return;

            // Only allow toggling sprint when mounted and moving
            bool canSprint = _owner.MountState == HumanMountState.Horse &&
                            (State == HorseState.ControlledRun || State == HorseState.RunToPoint);

            if (!canSprint)
            {
                sprint = false;
            }

            IsSprinting = sprint && CurrentStamina > 0;

            // Replace the array-style access with proper animation speed setting
            Animation.SetSpeed(HorseAnimations.Run, IsSprinting ? SprintSpeedMultiplier : 1f);
            if (_owner.MountState == HumanMountState.Horse)
            {
                _owner.Animation.SetSpeed(HumanAnimations.HorseRun, IsSprinting ? SprintSpeedMultiplier : 1f);
            }

            UpdateStaminaBar();
        }




        protected override void LateUpdate()
        {
            base.LateUpdate();

            if (!IsMine() || _owner == null || _owner.Dead)
                return;

            if (Cache.Rigidbody.velocity.magnitude > 8f)
            {
                CrossFadeIfNotPlaying(HorseAnimations.Run, 0.1f);
                if (_owner.MountState == HumanMountState.Horse)
                    _owner.CrossFadeIfNotPlaying(HumanAnimations.HorseRun, 0.1f);
                _idleTimeLeft = 0f;
            }
            else if (Cache.Rigidbody.velocity.magnitude > 1f)
            {
                CrossFadeIfNotPlaying(HorseAnimations.Walk, 0.1f);
                if (_owner.MountState == HumanMountState.Horse)
                    _owner.CrossFadeIfNotPlaying(HumanAnimations.HorseIdle, 0.1f);
                _idleTimeLeft = 0f;
            }
            else
            {
                UpdateIdle();
                if (_owner.MountState == HumanMountState.Horse)
                    _owner.CrossFadeIfNotPlaying(HumanAnimations.HorseIdle, 0.1f);
            }

            if (Animation.IsPlaying(HorseAnimations.Run) && Grounded)
            {
                ToggleDust(true);
                if (SettingsManager.SoundSettings.HorseSoundEffect.Value)
                    ToggleSoundLocal(HorseSounds.Run, true);
            }
            else
            {
                ToggleDust(false);
                if (SettingsManager.SoundSettings.HorseSoundEffect.Value)
                    ToggleSoundLocal(HorseSounds.Run, false);
            }
        }

        protected override void CheckGround()
        {
            RaycastHit hit;
            JustGrounded = false;
            if (Physics.SphereCast(Cache.Transform.position + Vector3.up * 0.8f, 0.6f, Vector3.down, out hit, 0.8f, GroundMask.value))
            {
                if (!Grounded)
                    Grounded = JustGrounded = true;
            }
            else
            {
                Grounded = false;
            }
        }

        [PunRPC]
        public void RPC_TeleportToHuman()
        {
            if (_owner == null) return;

            Vector3 position = _owner.Cache.Transform.position + Vector3.right * UnityEngine.Random.Range(-2f, 2f) + Vector3.forward * UnityEngine.Random.Range(-2f, 2f);
            position.y = GetHeight(position) + 1f;
            Cache.Transform.position = position;
            _teleportTimeLeft = TeleportTime;
        }


        private void CheckSprintInput()
        {
            if (!IsMine() || _owner == null || _owner.Dead)
                return;

            // Only allow sprinting when mounted and moving
            bool canSprint = _owner.MountState == HumanMountState.Horse &&
                            (State == HorseState.ControlledRun || State == HorseState.RunToPoint);

            if (canSprint && SettingsManager.InputSettings.Human.HorseSprint.GetKey())
            {
                
                if (!IsSprinting && CurrentStamina > 0)
                {
                    ToggleSprint(true);
                }
            }
            else if (IsSprinting)
            {
                ToggleSprint(false);
            }
        }
        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_staminaBar != null)
            {
                Destroy(_staminaBar);
            }
        }

    }




    enum HorseState
    {
        Idle,
        WalkToPoint,
        RunToPoint,
        ControlledIdle,
        ControlledRun,
        ControlledWalk
    }
}
