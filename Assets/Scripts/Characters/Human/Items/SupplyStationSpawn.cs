using Characters;
using UnityEngine;
using Photon.Pun;

namespace Characters
{
    class SupplyStationSpawn : SimpleUseable
    {
        public SupplyStationSpawn(BaseCharacter owner, string name, float cooldown) : base(owner)
        {
            Name = name;
            Cooldown = cooldown;
        }

        protected override void Activate()
        {
            var human = _owner as Human;
            if (human == null || !_owner.photonView.IsMine || !PhotonNetwork.InRoom || !PhotonNetwork.IsConnectedAndReady)
                return;

            var inventory = human.GetComponent<HumanInventory>();
            if (inventory == null)
            {
                Debug.LogError("HumanInventory component not found!");
                return;
            }

            // Check item count and show popup if needed
            if (inventory.GetItemCount("Supply Station") <= 0)
            {
                Debug.Log("Not enough Cannon count to spawn.");

                // Show the "Not Enough" popup through the inventory system
                inventory.SetItemCount("Supply Station", -1); // This will trigger the popup
                return;
            }

            try
            {
                Vector3 pos = human.Cache.Transform.position + human.Cache.Transform.forward * 3f;
                GameObject cannonObj = PhotonNetwork.Instantiate("Buildables/SupplyStation", pos, Quaternion.identity);

                // Use the proper inventory method to remove the item
                inventory.RemoveItem("Supply Station"); // This will handle the RPC and popup automatically
            }
            catch
            {
                Debug.LogWarning("Cannon spawn failed.");
            }
        }
    }
}