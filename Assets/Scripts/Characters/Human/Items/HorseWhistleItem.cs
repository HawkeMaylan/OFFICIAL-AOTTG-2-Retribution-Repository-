using Characters;
using UnityEngine;
using Photon.Pun;

namespace Characters
{
    class HorseWhistleItem : SimpleUseable
    {
        public HorseWhistleItem(BaseCharacter owner, string name, float cooldown) : base(owner)
        {
            Name = name;
            Cooldown = cooldown;
        }

        protected override void Activate()
        {
            var human = _owner as Human;
            if (human == null || !_owner.photonView.IsMine || !PhotonNetwork.InRoom || !PhotonNetwork.IsConnectedAndReady || human.Horse == null)
                return;

            human.Horse.HorseWhistle();

            try
            {
                Vector3 pos = human.Cache.Transform.position + Vector3.up * 1.5f;
                GameObject whistleObj = PhotonNetwork.Instantiate("Buildables/Whistle", pos, Quaternion.identity);

                if (whistleObj != null)
                    whistleObj.transform.SetParent(human.transform, true);
            }
            catch { /* Silently ignore errors if any occur */ }
        }
    }
}
