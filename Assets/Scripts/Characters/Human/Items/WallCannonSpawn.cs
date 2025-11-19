using Characters;
using UnityEngine;
using Photon.Pun;

namespace Characters
{
    class WallCannonSpawn : SimpleUseable
    {
        public WallCannonSpawn(BaseCharacter owner, string name, float cooldown) : base(owner)
        {
            Name = name;
            Cooldown = cooldown;
        }

        protected override void Activate()
        {
            var human = _owner as Human;
            if (human == null || !_owner.photonView.IsMine || !PhotonNetwork.InRoom || !PhotonNetwork.IsConnectedAndReady || human.Horse == null)
                return;

            var inventory = human.GetComponent<HumanInventory>();
            if (inventory == null || inventory.GetItemCount("WallCannon") <= 0)
            {
                Debug.Log("Not enough WallCannon count to spawn.");
                inventory.SetItemCount("WallCannon", -1);
                return;
            }

            try
            {
                Vector3 pos = human.Cache.Transform.position + human.Cache.Transform.forward * 3f;
                GameObject wagonObj = PhotonNetwork.Instantiate("Buildables/WallCannon", pos, Quaternion.identity);

                // Deduct Cannon using modular inventory system
                int newWallCannonCount = Mathf.Max(0, inventory.GetItemCount("WallCannon") - 1);
                inventory.photonView?.RPC("RPC_SetItemCount", RpcTarget.AllBufferedViaServer, "WallCannon", newWallCannonCount);
            }
            catch
            {
                Debug.LogWarning("WallCannon spawn failed.");
            }
        }
    }
}
