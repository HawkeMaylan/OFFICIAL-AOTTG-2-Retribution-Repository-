using Characters;
using UnityEngine;
using Photon.Pun;

namespace Characters
{
    class Wagon1Spawn : SimpleUseable
    {
        public Wagon1Spawn(BaseCharacter owner, string name, float cooldown) : base(owner)
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
            if (inventory == null)
            {
                Debug.LogError("HumanInventory component not found!");
                return;
            }

            // Check item count and show popup if needed
            if (inventory.GetItemCount("Wagon1") <= 0)
            {
                Debug.Log("Not enough Wagon1 count to spawn.");

                // Show the "Not Enough" popup through the inventory system
                inventory.SetItemCount("Wagon1", -1); // This will trigger the popup
                return;
            }

            try
            {
                Vector3 pos = human.Cache.Transform.position + Vector3.up * 1.5f;
                GameObject wagonObj = PhotonNetwork.Instantiate("Buildables/Wagon1aEdit", pos, Quaternion.identity);

                // Use the proper inventory method to remove the item
                inventory.RemoveItem("Wagon1"); // This will handle the RPC and popup automatically
            }
            catch
            {
                Debug.LogWarning("Wagon1 spawn failed.");
            }
        }
    }
}