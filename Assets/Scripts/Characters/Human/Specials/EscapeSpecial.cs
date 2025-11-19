using Effects;
using System.Collections;
using UnityEngine;
using Characters;
using UI;

namespace Characters
{
    class EscapeSpecial : ExtendedUseable
    {
        protected override float ActiveTime => 0.64f;
        private float _oldGas;
        private float _currentGas;
        private Human _human;

        public EscapeSpecial(BaseCharacter owner) : base(owner)
        {
            UsesLeft = MaxUses = 1;
            _human = (Human)owner;
        }

        public override bool CanUse()
        {
            return base.CanUse() && ((Human)_owner).State == HumanState.Grab;
        }

        protected override void Activate()
        {
            ((Human)_owner).CrossFade(HumanAnimations.SpecialJean, 0.1f);
            _oldGas = _human.Stats.CurrentGas;
            _currentGas = _oldGas * 0.60f;
            _human.Stats.CurrentGas = _currentGas;

            if (_human.Weapon is AmmoWeapon ammo)
            {
                ammo.AmmoLeft = Mathf.Max(0, ammo.AmmoLeft / 2);
                ammo.RoundLeft = Mathf.Max(0, ammo.RoundLeft / 2);
            }
            if (_human.Weapon is BladeWeapon bladeWeapon)
            {
                bladeWeapon.BladesLeft = Mathf.Max(0, bladeWeapon.BladesLeft / 2);
                bladeWeapon.CurrentDurability = Mathf.Max(0f, bladeWeapon.CurrentDurability / 2f);
            }
        }

        protected override void Deactivate()
        {
            var human = (Human)_owner;
            if (!human.Dead && human.Grabber != null && human.State == HumanState.Grab)
            {
                human.Ungrab(true, false);
                EffectSpawner.Spawn(EffectPrefabs.Blood1, human.HumanCache.BladeHitLeft.transform.position, Quaternion.Euler(270f, 0f, 0f));
                human.PlaySound(HumanSounds.BladeHit);
                human.SpecialActionState(0.5f);
                human.Cache.Rigidbody.velocity = Vector3.up * 30f;

            }
        }
    }
}
