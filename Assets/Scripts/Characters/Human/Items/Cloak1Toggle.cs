using Characters;
using UnityEngine;

namespace Characters
{
    class Cloak1Toggle : SimpleUseable
    {
        public Cloak1Toggle(BaseCharacter owner, string name, float cooldown) : base(owner)
        {
            Name = name;
            Cooldown = cooldown;
        }

        protected override void Activate()
        {
            var human = _owner as Human;
            if (human == null || !human.photonView.IsMine)
                return;

            Cloak1StateSync sync = human.GetComponent<Cloak1StateSync>();
            if (sync != null)
            {
                sync.Toggle();
            }

        }
    }
}
