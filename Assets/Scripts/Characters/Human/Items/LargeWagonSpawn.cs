using Characters;
using UnityEngine;
using Photon.Pun;

namespace Characters
{
    class LargeWagonSpawn : SimpleUseable
    {
        public LargeWagonSpawn(BaseCharacter owner, string name, float cooldown) : base(owner)
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
            if (inventory == null || inventory.GetItemCount("Wagon2") <= 0)
            {
                Debug.Log("Not enough Wagon2 count to spawn.");
                inventory.SetItemCount("Wagon2", -1); 
                return;
            }

            try
            {
                Vector3 pos = human.Cache.Transform.position + Vector3.up * 1.5f;
                GameObject WagonObj = PhotonNetwork.Instantiate("Buildables/LargeWagon", pos, Quaternion.identity);

                // Deduct from inventory using modular system
                int newWagon2Count = Mathf.Max(0, inventory.GetItemCount("Wagon2") - 1);
                inventory.photonView?.RPC("RPC_SetItemCount", RpcTarget.AllBufferedViaServer, "Wagon2", newWagon2Count);
            }
            catch
            {
                Debug.LogWarning("Large wagon spawn failed.");
            }
        }
    }
}
