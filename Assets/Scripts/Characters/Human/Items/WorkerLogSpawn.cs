using Characters;
using UnityEngine;
using Photon.Pun;

namespace Characters
{
    class WorkerLogSpawn : SimpleUseable
    {
        public WorkerLogSpawn(BaseCharacter owner, string name, float cooldown) : base(owner)
        {
            Name = name;
            Cooldown = cooldown;
        }

        protected override void Activate()
        {
            var human = _owner as Human;
            if (human == null || !_owner.photonView.IsMine || !PhotonNetwork.InRoom || !PhotonNetwork.IsConnectedAndReady || human.Horse == null)
                return;

            try
            {
                Vector3 pos = Vector3.zero; // Always spawn at (0, 0, 0)
                GameObject ShigGateN = PhotonNetwork.Instantiate("Buildables/WorkerLog", pos, Quaternion.identity);

                /// GameObject WaterBase = PhotonNetwork.Instantiate("Buildables/WaterEmpty", pos, Quaternion.identity);
            }
            catch { /* Silently ignore errors if any occur */ }
        }
    }
}
