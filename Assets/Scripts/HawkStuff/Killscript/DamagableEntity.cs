using UnityEngine;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CustomLogic;
using GameManagers;
using Settings;
using Characters;
using System;

namespace Entities
{
    public enum EntityForm { Human, Titan }
    public enum DamageType { Generic, Blade, AHSS, APG, Titan, Grab, Punch, Kick }

    [RequireComponent(typeof(Collider))]
    public class DamageableEntity : MonoBehaviourPunCallbacks, IPunInstantiateMagicCallback, IPunObservable
    {
        [Header("Entity Setup")]
        public string entityName = "DamageableEntity";
        public int maxHP = 100;
        public int currentHP = -1;
        public string team = "Neutral";

        [Header("GeneralKill Compatibility")]
        public EntityForm entityForm = EntityForm.Human;

        [Header("Options")]
        public bool showKillFeed = true;
        public bool useTextDisplay = true;
        public bool use3DHealthBar = true;
        public bool destroyOnDeath = true;
        public bool onlyShowUIWhenDamaged = false;
        public float hideUIDistance = 50f;
        public Camera referenceCamera;

        [Header("UI Fade Settings")]
        public bool fadeUIAfterDelay = false;
        public float uiFadeDelay = 5f;

        [Header("UI Offsets and Scale")]
        public Vector3 healthBarOffset = new Vector3(0f, 2.5f, 0f);
        public Vector3 textOffset = new Vector3(0f, 2f, 0f);
        public float textScale = 1f;
        public Vector3 healthBarScale = new Vector3(1f, 0.2f, 1f);

        [Header("Hit Cooldown")]
        public float hitCooldown = 0.2f;

        [Header("Damage Settings")]
        [Tooltip("Base damage when hit by environment/unknown sources")]
        public int flatDamageFromUnknown = 100;

        [Header("NPC Damage Settings")]
        [Tooltip("Base damage NPCs deal to this object")]
        public int npcBaseDamage = 100;
        [Tooltip("Damage multiplier for titan attacks")]
        public float titanDamageMultiplier = 1f;

        [Serializable]
        public class DamageMultiplier
        {
            public DamageType type;
            public float multiplier = 1f;
        }

        [Header("Damage Multipliers")]
        public List<DamageMultiplier> damageMultipliers = new List<DamageMultiplier>()
        {
            new DamageMultiplier { type = DamageType.Titan, multiplier = 1f },
            new DamageMultiplier { type = DamageType.Blade, multiplier = 1.5f }
        };

        [Header("Effect Prefabs (Resources/HParticles)")]
        public GameObject hitEffectPrefab;
        public Vector3 hitEffectOffset;
        public Vector3 hitEffectRotation;

        public GameObject deathEffectPrefab;
        public Vector3 deathEffectOffset;
        public Vector3 deathEffectRotation;

        [Header("Custom Collider")]
        public Collider customCollider;

        private bool isDead;
        private float lastHitTime = -999f;
        private bool wasDamaged = false;

        private GameObject hpBillboard;
        private TextMesh hpText;

        private GameObject healthBarRoot;
        private Transform foregroundBar;

        private void Awake()
        {
            if (currentHP < 0 || currentHP > maxHP)
                currentHP = maxHP;
        }

        private void Start()
        {
            if (referenceCamera == null)
                referenceCamera = Camera.main;

            if (useTextDisplay && hpBillboard == null)
                CreateBillboard();
            if (use3DHealthBar && healthBarRoot == null)
                CreateHealthBar();

            UpdateBillboard();
            UpdateHealthBar();

            if (customCollider != null && customCollider != GetComponent<Collider>())
            {
                ColliderEventForwarder forwarder = customCollider.gameObject.AddComponent<ColliderEventForwarder>();
                forwarder.Setup(this);
            }
        }

        public void OnPhotonInstantiate(PhotonMessageInfo info)
        {
            photonView.RPC("UpdateHealthRPC", RpcTarget.AllBuffered, currentHP);
            UpdateBillboard();
            UpdateHealthBar();
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
                stream.SendNext(currentHP);
            else
            {
                currentHP = (int)stream.ReceiveNext();
                UpdateBillboard();
                UpdateHealthBar();
            }
        }

        [PunRPC]
        private void UpdateHealthRPC(int hp)
        {
            currentHP = hp;
            UpdateBillboard();
            UpdateHealthBar();
        }

        public void GetHit(string source, int damage, string type = "Generic", string hitbox = "")
        {
            if (!PhotonNetwork.IsMasterClient || isDead)
                return;

            if (Time.time - lastHitTime < hitCooldown)
                return;

            lastHitTime = Time.time;
            wasDamaged = true;
            currentHP -= damage;

            Debug.Log($"{entityName} hit by {source} for {damage} damage (type: {type})");

            photonView.RPC("UpdateHealthRPC", RpcTarget.All, currentHP);

            if (hitEffectPrefab != null)
                photonView.RPC("SpawnEffectRPC", RpcTarget.All, hitEffectPrefab.name, transform.position + hitEffectOffset, Quaternion.Euler(hitEffectRotation));

            if (currentHP <= 0)
                Die(source, type);
        }

        [PunRPC]
        private void RequestHitRPC(string source, int damage, string type, string hitbox, int viewID, PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient || isDead || photonView.ViewID != viewID)
                return;

            // Apply damage multipliers
            if (System.Enum.TryParse(type, out DamageType damageType))
            {
                var multiplier = damageMultipliers.FirstOrDefault(x => x.type == damageType);
                if (multiplier != null)
                    damage = Mathf.RoundToInt(damage * multiplier.multiplier);
            }

            GetHit(source, damage, type, hitbox);
        }

        private void Die(string killerName, string type)
        {
            isDead = true;

            if (deathEffectPrefab != null)
                photonView.RPC("SpawnEffectRPC", RpcTarget.All, deathEffectPrefab.name, transform.position + deathEffectOffset, Quaternion.Euler(deathEffectRotation));

            if (showKillFeed && CustomLogicManager.Evaluator != null)
            {
                int damage = Mathf.Clamp(maxHP, 0, maxHP);
                RPCManager.PhotonView?.RPC("ShowKillFeedRPC", RpcTarget.All, new object[] { killerName, entityName, damage, type });
            }

            if (destroyOnDeath)
                photonView.RPC("RequestDestroyRPC", RpcTarget.MasterClient);
        }

        [PunRPC]
        private void RequestDestroyRPC()
        {
            if (PhotonNetwork.IsMasterClient && photonView.IsMine)
                PhotonNetwork.Destroy(gameObject);
        }

        [PunRPC]
        private void SpawnEffectRPC(string resourceName, Vector3 position, Quaternion rotation)
        {
            GameObject prefab = Resources.Load<GameObject>($"HParticles/{resourceName}");
            if (prefab != null)
                Instantiate(prefab, position, rotation);
        }

        public void TryHitFromCollider(Collider collider)
        {
            var hitbox = collider.GetComponent<BaseHitbox>();
            if (hitbox == null || !hitbox.IsActive())
                return;

            var attacker = hitbox.Owner;
            int damage = flatDamageFromUnknown;
            string sourceName = "Environment";
            string type = "Collision";

            // Player attack handling
            if (attacker is Human human)
            {
                sourceName = human.Name;

                // Calculate base damage from velocity
                damage = (human.CarryState == HumanCarryState.Carry && human.Carrier != null)
                    ? Mathf.Max((int)(human.Carrier.CarryVelocity.magnitude * 10f), 10)
                    : Mathf.Max((int)(human.Cache.Rigidbody.velocity.magnitude * 10f), 10);

                // Determine weapon type and apply multipliers
                if (human.Weapon is BladeWeapon blade)
                {
                    type = "Blade";
                    damage = (int)(damage * CharacterData.HumanWeaponInfo["Blade"]["DamageMultiplier"].AsFloat);

                    // Handle blade durability
                    blade.UseDurability(human.Stats.Perks["AdvancedAlloy"].CurrPoints == 1 && damage < 500 ?
                        blade.CurrentDurability : 2f);

                    if (blade.CurrentDurability == 0f)
                    {
                        human.ToggleBlades(false);
                        human.PlaySound(HumanSounds.BladeBreak);
                    }
                }
                else if (human.Weapon is AHSSWeapon ahss)
                {
                    type = "AHSS";
                    damage = (int)(damage * CharacterData.HumanWeaponInfo["AHSS"]["DamageMultiplier"].AsFloat);
                }
                else if (human.Weapon is APGWeapon apg)
                {
                    type = "APG";
                    damage = (int)(damage * CharacterData.HumanWeaponInfo["APG"]["DamageMultiplier"].AsFloat);
                }
            }
            // NPC attack handling
            else if (attacker is BaseTitan titan)
            {
                sourceName = titan.Name;
                type = "Titan";
                damage = titan.CustomDamageEnabled ? titan.CustomDamage : npcBaseDamage;
                damage = Mathf.RoundToInt(damage * titanDamageMultiplier);

                // Debug output to verify values
                Debug.Log($"Titan attack - Base: {npcBaseDamage}, Multiplier: {titanDamageMultiplier}, Final: {damage}");
            }

            // Apply final multipliers
            if (System.Enum.TryParse(type, out DamageType damageType))
            {
                var multiplier = damageMultipliers.FirstOrDefault(x => x.type == damageType);
                if (multiplier != null)
                    damage = Mathf.RoundToInt(damage * multiplier.multiplier);
            }

            photonView.RPC("RequestHitRPC", RpcTarget.MasterClient,
                sourceName,
                damage,
                type,
                collider.name,
                photonView.ViewID);
        }

        private void CreateBillboard()
        {
            hpBillboard = new GameObject("HPBillboard");
            hpBillboard.transform.SetParent(transform);
            hpBillboard.transform.localPosition = textOffset;
            hpBillboard.transform.localRotation = Quaternion.identity;
            hpBillboard.transform.localScale = Vector3.one * textScale;

            TextMesh textMesh = hpBillboard.AddComponent<TextMesh>();
            textMesh.fontSize = 32;
            textMesh.characterSize = 0.1f;
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.color = Color.white;
            hpText = textMesh;
        }

        private void CreateHealthBar()
        {
            healthBarRoot = new GameObject("HealthBarRoot");
            healthBarRoot.transform.SetParent(transform);
            healthBarRoot.transform.localPosition = healthBarOffset;
            healthBarRoot.transform.localScale = healthBarScale;

            GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "BarBackground";
            bg.transform.SetParent(healthBarRoot.transform);
            bg.transform.localPosition = Vector3.zero;
            bg.transform.localScale = Vector3.one;
            bg.GetComponent<Renderer>().material.color = Color.black;

            GameObject fg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fg.name = "BarForeground";
            fg.transform.SetParent(healthBarRoot.transform);
            fg.transform.localPosition = new Vector3(-0.5f, 0f, -0.01f);
            fg.transform.localScale = Vector3.one;
            fg.GetComponent<Renderer>().material.color = Color.green;
            foregroundBar = fg.transform;
        }

        private void UpdateBillboard()
        {
            if (hpText != null)
                hpText.text = currentHP.ToString();
        }

        private void UpdateHealthBar()
        {
            if (foregroundBar != null && maxHP > 0)
            {
                float ratio = Mathf.Clamp01((float)currentHP / maxHP);
                foregroundBar.localScale = new Vector3(ratio, 1f, 1f);
                foregroundBar.localPosition = new Vector3((ratio - 1f) * 0.5f, 0f, -0.01f);

                var color = Color.green;
                if (ratio <= 0.25f)
                    color = Color.red;
                else if (ratio <= 0.5f)
                    color = new Color(1f, 0.65f, 0f);

                foregroundBar.GetComponent<Renderer>().material.color = color;
            }
        }

        private void FixedUpdate()
        {
            if (referenceCamera == null)
                referenceCamera = Camera.main;
            if (referenceCamera == null)
                return;

            float dist = Vector3.Distance(referenceCamera.transform.position, transform.position);
            bool recentlyHit = Time.time - lastHitTime < uiFadeDelay;
            bool showUI = (!onlyShowUIWhenDamaged || (wasDamaged && (!fadeUIAfterDelay || recentlyHit))) && dist < hideUIDistance;

            if (useTextDisplay && hpBillboard != null)
            {
                hpBillboard.transform.rotation = Quaternion.LookRotation(hpBillboard.transform.position - referenceCamera.transform.position);
                hpBillboard.SetActive(showUI);
            }

            if (use3DHealthBar && healthBarRoot != null)
            {
                healthBarRoot.transform.rotation = Quaternion.LookRotation(healthBarRoot.transform.position - referenceCamera.transform.position);
                healthBarRoot.SetActive(showUI);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (customCollider == null)
                TryHitFromCollider(other);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (customCollider == null)
                TryHitFromCollider(collision.collider);
        }
    }

    public class ColliderEventForwarder : MonoBehaviour
    {
        private DamageableEntity damageable;

        public void Setup(DamageableEntity entity)
        {
            damageable = entity;
        }

        private void OnTriggerEnter(Collider other)
        {
            damageable?.TryHitFromCollider(other);
        }

        private void OnCollisionEnter(Collision collision)
        {
            damageable?.TryHitFromCollider(collision.collider);
        }
    }
}