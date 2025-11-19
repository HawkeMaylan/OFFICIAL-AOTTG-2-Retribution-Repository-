using System.Collections;
using UnityEngine;

namespace Characters
{
    class PotatoSpecial : BaseEmoteSpecial
    {
        protected override float ActiveTime => 15f;
        protected override bool GroundedOnly => false;
        private float _oldSpeed;
        private float _currentSpeed;

        public PotatoSpecial(BaseCharacter owner): base(owner)
        {
            Cooldown = 120f;
        }

        protected override void Activate()
        {
            _oldSpeed = _human.Stats.RunSpeed;
            _currentSpeed = _oldSpeed * 1.4f;
            _human.RunAnimation = HumanAnimations.RunBuffed;
            _human.EmoteAnimation(HumanAnimations.SpecialSasha);
        }

        protected override void ActiveFixedUpdate()
        {
            _human.Stats.RunSpeed = _currentSpeed;
        }

        protected override void Deactivate()
        {
            _human.Stats.RunSpeed = _oldSpeed;
            _human.RunAnimation = _human.Weapon is ThunderspearWeapon? HumanAnimations.RunTS : HumanAnimations.Run;
            if (_human.Animation.IsPlaying(HumanAnimations.RunBuffed))
                _human.CrossFade(_human.RunAnimation, 0.1f);
        }
    }
}
