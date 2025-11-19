using Characters;
using UnityEngine;
using Photon.Pun;

namespace Characters
{
    class GasBombSpawn : SimpleUseable
    {
        public GasBombSpawn(BaseCharacter owner, string name, float cooldown) : base(owner)
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
            if (inventory.GetItemCount("Gas Bomb") <= 0)
            {
                Debug.Log("Not enough Gas Bombs.");
                inventory.SetItemCount("Gas Bomb", -1); // Triggers "Not Enough GasBomb" popup
                return;
            }

            try
            {
                // Get aim target and direction
                Vector3 target = human.GetAimPoint();
                Vector3 spawnPos = human.Cache.Transform.position + Vector3.up * 1.5f;
                Vector3 direction = (target - spawnPos).normalized;
                float baseThrowSpeed = 20f;

                // Include player's forward momentum
                Vector3 playerVelocity = human.Cache.Rigidbody.velocity;
                float addedSpeed = Mathf.Max(Vector3.Dot(playerVelocity, direction), 0f);
                float finalSpeed = baseThrowSpeed + addedSpeed;
                Vector3 finalVelocity = direction * finalSpeed;

                // Spawn molotov and apply force
                GameObject molotov = PhotonNetwork.Instantiate("Buildables/GasBomb", spawnPos, Quaternion.LookRotation(direction));

                Rigidbody rb = molotov.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.velocity = Vector3.zero;
                    rb.AddForce(finalVelocity, ForceMode.VelocityChange);
                }

                // Use inventory system's proper removal method
                inventory.RemoveItem("Gas Bomb"); // Handles RPC and shows "-1" popup
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"GasBomb spawn failed: {e.Message}");
            }
        }
    }
}