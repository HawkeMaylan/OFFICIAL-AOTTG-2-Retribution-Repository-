using Projectiles;
using System.Collections;
using UnityEngine;
using Characters;

namespace Characters
{
    class FlareItem : SimpleUseable
    {
        Color _color;
        float Speed = 450f;
        Vector3 Gravity = Vector3.down * 20f;

        public FlareItem(BaseCharacter owner, string name, Color color, float cooldown) : base(owner)
        {
            Name = name;
            _color = color;
            Cooldown = cooldown;
        }

        public override bool CanUse()
        {
            // First check the base conditions (like cooldown)
            if (!base.CanUse())
                return false;

            var human = _owner as Human;
            if (human == null)
                return false;

            // Check if human is already firing flare
            if (human.State == HumanState.FiringFlare)
                return false;

            return true;
        }

        protected override void Activate()
        {
            var human = (Human)_owner;

            // Activate the flare firing state and animation
            human.FireFlare(_color);

            // Start coroutine to delay projectile spawn
            human.StartCoroutine(SpawnFlareWithDelay(human));
        }

        private IEnumerator SpawnFlareWithDelay(Human human)
        {
            // Wait for 2 seconds to sync with animation
            yield return new WaitForSeconds(2f);

            Vector3 target = human.GetAimPoint();
            Vector3 start = human.Cache.Transform.position + human.Cache.Transform.up * 5f;
            Vector3 direction = (target - start).normalized;
            ProjectileSpawner.Spawn(ProjectilePrefabs.Flare, start, Quaternion.identity, direction * Speed, Gravity, 6.5f, human.Cache.PhotonView.ViewID,
                "", new object[] { _color });
            human.PlaySound(HumanSounds.FlareLaunch);
        }
    }
}