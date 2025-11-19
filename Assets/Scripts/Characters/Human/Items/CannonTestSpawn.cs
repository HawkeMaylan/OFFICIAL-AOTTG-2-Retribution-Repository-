using Characters;
using UnityEngine;
using Photon.Pun;

namespace Characters
{
    class CannonTestSpawn : SimpleUseable
    {
        public CannonTestSpawn(BaseCharacter owner, string name, float cooldown) : base(owner)
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
            if (inventory.GetItemCount("Cannon") <= 0)
            {
                Debug.Log("Not enough Cannon count to spawn.");
                inventory.SetItemCount("Cannon", -1); // Triggers "Not Enough Cannon" popup
                return;
            }

            try
            {
                Vector3 pos = human.Cache.Transform.position + human.Cache.Transform.forward * 3f;
                GameObject cannonObj = PhotonNetwork.Instantiate("Buildables/CannonTest", pos, Quaternion.identity);

                // Use inventory system's proper removal method
                inventory.RemoveItem("Cannon"); // Handles RPC and shows "-1" popup
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Cannon spawn failed: {e.Message}");
            }
        }
    }
}