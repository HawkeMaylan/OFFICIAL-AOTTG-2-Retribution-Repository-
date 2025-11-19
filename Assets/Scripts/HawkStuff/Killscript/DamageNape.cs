using Settings;
using UnityEngine;
using Photon;
using Characters;
using System.Collections.Generic;
using System.Collections;
using Effects;
using ApplicationManagers;
using GameManagers;
using UI;
using Utility;
using CustomLogic;
using Cameras;
using System;
using Effects;


public class SimpleNapeDamager : MonoBehaviour
{
    [Header("Damage Settings")]
    public int Damage = 100;

    [Header("Effects")]
    public AudioClip HitSound;
    public GameObject HitEffect;

    private void OnCollisionEnter(Collision collision)
    {
        TryDamage(collision.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryDamage(other);
    }

    void TryDamage(Collider hitCollider)
    {
        // Only process hurtboxes or human layers
        if (hitCollider.gameObject.layer != PhysicsLayer.Hurtbox &&
            hitCollider.gameObject.layer != PhysicsLayer.Human)
            return;

        BaseCharacter character = hitCollider.transform.root.GetComponent<BaseCharacter>();

        // Only damage living AI Titans
        if (character is BaseTitan titan && !character.Dead && character.AI)
        {
            titan.GetHit("NapeDamage", Damage, "nape", hitCollider.name);
            PlayEffects();
        }
    }

    void PlayEffects()
    {
        if (HitSound != null)
            AudioSource.PlayClipAtPoint(HitSound, transform.position);

        if (HitEffect != null)
            Instantiate(HitEffect, transform.position, Quaternion.identity);
    }
}