using Characters;
using UnityEngine;

namespace Characters
{
    class Lanterntoggle : SimpleUseable
    {
        public Lanterntoggle(BaseCharacter owner, string name, float cooldown) : base(owner)
        {
            Name = name;
            Cooldown = cooldown;
        }

        protected override void Activate()
        {
            var human = _owner as Human;
            if (human == null || !human.photonView.IsMine)
                return;

            LanternStateSync sync = human.GetComponent<LanternStateSync>();
            if (sync != null)
            {
                sync.Toggle();
            }
            else
            {
                Debug.LogWarning("[Lanterntoggle] LanternStateSync not found on player.");
            }
        }
    }
}
