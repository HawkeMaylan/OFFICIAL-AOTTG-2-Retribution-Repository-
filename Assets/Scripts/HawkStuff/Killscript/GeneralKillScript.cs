using UnityEngine;
using Photon.Pun;
using Characters;
using Effects;
using System.Collections.Generic;
using System.Collections;
using Entities;

[RequireComponent(typeof(Collider))]
public class GeneralKillScript : MonoBehaviourPunCallbacks
{
    [Header("General Settings")]
    public float destroyAfterSeconds = 5f;
    public string killSourceName = "Blade";

    [Header("Collision Detection")]
    public bool useCustomCollider = false;
    public Collider customDamageCollider;

    [Header("Optional Collision Animation")]
    public bool playAnimationOnCollision = false;
    public AnimationClip collisionAnimation;
    public float animationDelayTime = 0f;
    public bool makeKinematicOnCollision = false;
    public LayerMask animationCollisionLayers = ~0;

    [Header("Optional Particle Effect")]
    public bool spawnParticleOnCollision = false;
    public GameObject collisionParticlePrefab;

    private Animation legacyAnim;
    private bool animationPlayed = false;
    private bool particleSpawned = false;
    private float spawnTime;
    private Rigidbody rb;
    private Collider selfCollider;

    [Header("Human Settings")]
    public bool damageHumans = true;
    public int humanDamage = 100;

    [Header("Titan Settings")]
    public bool damageNape = true;
    public int titanNapeDamage = 1000;
    public bool disableArms = true;
    public bool crippleLegs = true;
    public bool blindEyes = true;
    public bool directionalStun = true;
    public float knockbackForce = 30f;
    public int maxKnockbacksPerTitan = 1;

    private Dictionary<BaseTitan, int> titanKnockbackCounts = new Dictionary<BaseTitan, int>();
    private HashSet<GameObject> recentlyHit = new HashSet<GameObject>();
    public float hitCooldown = 0.002f;

    private void Start()
    {
        spawnTime = Time.time;

        if (destroyAfterSeconds > 0f)
            Invoke(nameof(SelfDestruct), destroyAfterSeconds);

        if (playAnimationOnCollision)
        {
            if (collisionAnimation != null)
            {
                legacyAnim = GetComponent<Animation>();
                if (legacyAnim == null)
                    legacyAnim = gameObject.AddComponent<Animation>();

                legacyAnim.playAutomatically = false;

                if (!legacyAnim.GetClip(collisionAnimation.name))
                    legacyAnim.AddClip(collisionAnimation, collisionAnimation.name);
            }
            else
            {
                Debug.LogWarning($"[GeneralKillScript] playAnimationOnCollision is enabled, but no animation clip is assigned on {gameObject.name}");
            }
        }

        rb = GetComponent<Rigidbody>();
        selfCollider = GetComponent<Collider>();

        if (useCustomCollider && customDamageCollider != null)
        {
            customDamageCollider.isTrigger = true;
            customDamageCollider.enabled = true;
        }
    }

    private void Update()
    {
        if (playAnimationOnCollision && Time.time - spawnTime < animationDelayTime)
            return;

        if (!useCustomCollider)
        {
            CheckOverlapBoxDamage();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!useCustomCollider) return;
        if (other == selfCollider || recentlyHit.Contains(other.gameObject)) return;

        ProcessDamage(other.gameObject, other);
    }

    private void CheckOverlapBoxDamage()
    {
        Collider[] hits = Physics.OverlapBox(transform.position, transform.localScale / 2f, transform.rotation);
        foreach (var other in hits)
        {
            if (other == selfCollider || recentlyHit.Contains(other.gameObject))
                continue;

            ProcessDamage(other.gameObject, other);
        }
    }

    private void ProcessDamage(GameObject otherGameObject, Collider otherCollider)
    {
        recentlyHit.Add(otherGameObject);
        StartCoroutine(RemoveHitCooldown(otherGameObject, hitCooldown));

        if (((1 << otherGameObject.layer) & animationCollisionLayers) != 0 && !animationPlayed)
        {
            photonView.RPC("RPC_PlayCollisionAnimation", RpcTarget.All);
            animationPlayed = true;
        }

        if (spawnParticleOnCollision && !particleSpawned && collisionParticlePrefab != null)
        {
            photonView.RPC("RPC_SpawnParticle", RpcTarget.All, transform.position);
            particleSpawned = true;
        }

        Human human = otherCollider.GetComponentInParent<Human>();
        if (damageHumans && human != null && human.IsMine())
        {
            human.GetHit(killSourceName, humanDamage, "Collision", otherCollider.name);
            return;
        }

        BaseTitan baseTitan = otherCollider.GetComponentInParent<BaseTitan>();
        if (baseTitan != null && !baseTitan.Dead && baseTitan.AI)
        {
            BasicTitan titan = baseTitan as BasicTitan;
            if (titan == null) return;

            string hitboxName = otherCollider.name;
            var cache = titan.BaseTitanCache;

            if (blindEyes && hitboxName == cache.EyesHurtbox?.name)
            {
                EffectSpawner.Spawn(EffectPrefabs.CriticalHit, transform.position, Quaternion.Euler(270f, 0f, 0f));
                titan.GetHit("SmokeBomb", 0, "SmokeBomb", hitboxName);
            }

            if (damageNape && hitboxName == cache.NapeHurtbox?.name)
            {
                titan.GetHit(killSourceName, titanNapeDamage, "BladeThrow", hitboxName);
            }

            if (disableArms && (hitboxName == titan.BasicCache.ForearmLHurtbox?.name || hitboxName == titan.BasicCache.ForearmRHurtbox?.name))
            {
                titan.GetHit(killSourceName, 0, "BladeThrow", hitboxName);
            }

            if (crippleLegs && (hitboxName == cache.LegLHurtbox?.name || hitboxName == cache.LegRHurtbox?.name))
            {
                titan.GetHit(killSourceName, 0, "BladeThrow", hitboxName);
            }

            if (directionalStun)
            {
                if (!titanKnockbackCounts.ContainsKey(baseTitan))
                    titanKnockbackCounts[baseTitan] = 0;

                if (titanKnockbackCounts[baseTitan] < maxKnockbacksPerTitan)
                {
                    Vector3 dir = (titan.Cache.Transform.position - transform.position).normalized;
                    dir.y = 0f;

                    titan.GetHit(killSourceName, 0, "TitanStun", hitboxName);
                    titan.Cache.Rigidbody.isKinematic = false;
                    titan.Cache.Rigidbody.AddForce(dir * knockbackForce, ForceMode.Impulse);
                    titanKnockbackCounts[baseTitan]++;
                }
            }

            return;
        }

        DamageableEntity damageable = otherCollider.GetComponentInParent<DamageableEntity>();
        if (damageable != null)
        {
            if (damageHumans && damageable.entityForm == EntityForm.Human)
            {
                damageable.GetHit(killSourceName, humanDamage, "Collision", otherCollider.name);
                return;
            }

            if (damageNape && damageable.entityForm == EntityForm.Titan)
            {
                damageable.GetHit(killSourceName, titanNapeDamage, "BladeThrow", otherCollider.name);
                return;
            }
        }
    }

    private IEnumerator RemoveHitCooldown(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        recentlyHit.Remove(obj);
    }

    [PunRPC]
    private void RPC_PlayCollisionAnimation()
    {
        if (legacyAnim != null && collisionAnimation != null)
        {
            legacyAnim.Play(collisionAnimation.name);
            if (makeKinematicOnCollision && rb != null)
                rb.isKinematic = true;
        }
    }

    [PunRPC]
    private void RPC_SpawnParticle(Vector3 position)
    {
        if (collisionParticlePrefab != null)
            Instantiate(collisionParticlePrefab, position, Quaternion.identity);
    }

    private void SelfDestruct()
    {
        if (!PhotonNetwork.IsConnected)
        {
            Destroy(gameObject);
            return;
        }

        if (photonView != null && photonView.ViewID != 0)
        {
            if (photonView.IsMine || PhotonNetwork.IsMasterClient)
            {
                PhotonNetwork.Destroy(photonView.gameObject);
            }
            else
            {
                Debug.LogWarning("[SelfDestruct] Cannot destroy object. Not owner or MC.");
            }
        }
    }
}