using Characters;
using UnityEngine;

namespace Characters
{
    class CloakToggle : SimpleUseable
    {
        public CloakToggle(BaseCharacter owner, string name, float cooldown) : base(owner)
        {
            Name = name;
            Cooldown = cooldown;
        }

        protected override void Activate()
        {
            var human = _owner as Human;
            if (human == null || !human.photonView.IsMine)
                return;

            CloakStateSync sync = human.GetComponent<CloakStateSync>();
            if (sync != null)
            {
                sync.Toggle();
            }

        }
    }
}
