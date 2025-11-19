using Characters;
using UnityEngine;
using Photon.Pun;

namespace Characters
{
    class Act2MissionDespawn : SimpleUseable
    {
        public Act2MissionDespawn(BaseCharacter owner, string name, float cooldown) : base(owner)
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
                if (PhotonNetwork.IsMasterClient)
                {
                    // Find and destroy all stranded soldiers
                    GameObject[] soldiers = {
                        GameObject.Find("StrandedSoldier1(Clone)"),
                        GameObject.Find("StrandedSoldier2(Clone)"),
                        GameObject.Find("StrandedSoldier3(Clone)")
                    };

                    foreach (var soldier in soldiers)
                    {
                        if (soldier != null)
                        {
                            PhotonNetwork.Destroy(soldier);
                        }
                    }
                }
            }
            catch { /* Silently ignore errors if any occur */ }
        }
    }
}