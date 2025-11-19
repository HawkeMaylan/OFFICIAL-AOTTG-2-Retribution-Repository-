using Characters;
using UnityEngine;
using Photon.Pun;

namespace Characters
{
    class Act0MissionSpawn : SimpleUseable
    {
        public Act0MissionSpawn(BaseCharacter owner, string name, float cooldown) : base(owner)
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
                GameObject fay = GameObject.Find("Fay(Clone)");
                if (fay != null && PhotonNetwork.IsMasterClient)
                {
                    PhotonNetwork.Destroy(fay);
                }

                // Load prefabs to get their default positions
                GameObject soldier1Prefab = Resources.Load<GameObject>("Buildables/NPCS/Preset/StrandedSoldier1");
                GameObject soldier2Prefab = Resources.Load<GameObject>("Buildables/NPCS/Preset/StrandedSoldier2");
                GameObject soldier3Prefab = Resources.Load<GameObject>("Buildables/NPCS/Preset/StrandedSoldier3");

                // Spawn soldiers at their prefab positions (fallback to Vector3.zero if prefab missing)
                Vector3 pos1 = soldier1Prefab != null ? soldier1Prefab.transform.position : Vector3.zero;
                Vector3 pos2 = soldier2Prefab != null ? soldier2Prefab.transform.position : Vector3.zero;
                Vector3 pos3 = soldier3Prefab != null ? soldier3Prefab.transform.position : Vector3.zero;

                GameObject Mission = PhotonNetwork.Instantiate("Buildables/NPCS/Preset/StrandedSoldier1", pos1, Quaternion.identity);
                GameObject Mission2 = PhotonNetwork.Instantiate("Buildables/NPCS/Preset/StrandedSoldier2", pos2, Quaternion.identity);
                GameObject Mission3 = PhotonNetwork.Instantiate("Buildables/NPCS/Preset/StrandedSoldier3", pos3, Quaternion.identity);
            }
            catch { /* Silently ignore errors if any occur */ }
        }
    }
}