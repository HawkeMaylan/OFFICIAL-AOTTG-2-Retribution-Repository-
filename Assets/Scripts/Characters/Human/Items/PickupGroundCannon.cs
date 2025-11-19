using Characters;
using UnityEngine;
using Photon.Pun;

namespace Characters
{
    class PickupGroundCannon : SimpleUseable
    {
        public PickupGroundCannon(BaseCharacter owner, string name, float cooldown) : base(owner)
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
                Vector3 pos = human.Cache.Transform.position + human.Cache.Transform.forward * 10f;
                GameObject WagonObj = PhotonNetwork.Instantiate("Buildables/InventoryItems/GroundCannonPickup", pos, Quaternion.identity);


            }
            catch { /* Silently ignore errors if any occur */ }
        }
    }
}
