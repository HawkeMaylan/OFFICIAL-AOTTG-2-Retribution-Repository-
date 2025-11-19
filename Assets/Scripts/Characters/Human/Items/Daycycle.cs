using Characters;
using UnityEngine;
using Photon.Pun;

namespace Characters
{
    class Daycycle : SimpleUseable
    {
        public Daycycle(BaseCharacter owner, string name, float cooldown) : base(owner)
        {
            Name = name;
            Cooldown = cooldown;
        }

        protected override void Activate()
        {
            var human = _owner as Human;
            if (human == null || !_owner.photonView.IsMine || !PhotonNetwork.InRoom || !PhotonNetwork.IsConnectedAndReady)
                return;



            try
            {
                Vector3 pos = human.Cache.Transform.position + Vector3.up * 1.5f;
                GameObject WagonObj = PhotonNetwork.Instantiate("Buildables/HawkDayNightManager", pos, Quaternion.identity);


            }
            catch { /* Silently ignore errors if any occur */ }
        }
    }
}
