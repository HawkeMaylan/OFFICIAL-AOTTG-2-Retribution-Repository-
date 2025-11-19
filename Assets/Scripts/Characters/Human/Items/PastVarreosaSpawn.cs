using Characters;
using UnityEngine;
using Photon.Pun;

namespace Characters
{
    class PastVarreosaSpawn : SimpleUseable
    {
        public PastVarreosaSpawn(BaseCharacter owner, string name, float cooldown) : base(owner)
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
                ///GameObject ShigGateN = PhotonNetwork.Instantiate("Buildables/ShigGateNorth", pos, Quaternion.identity);
                //GameObject ShigGateS = PhotonNetwork.Instantiate("Buildables/ShigGateSouth", pos, Quaternion.identity);
                //GameObject Varreosa = PhotonNetwork.Instantiate("Buildables/PastVarreosa", pos, Quaternion.identity);
                //GameObject Varreosa5 = PhotonNetwork.Instantiate("Buildables/Riot", pos, Quaternion.identity);
                //GameObject Varreosa1 = PhotonNetwork.Instantiate("Buildables/NPCS/Preset/Act06_1", pos, Quaternion.identity);
                //GameObject Varreosa2 = PhotonNetwork.Instantiate("Buildables/NPCS/Preset/Act06_2", pos, Quaternion.identity);
                //GameObject Varreosa3 = PhotonNetwork.Instantiate("Buildables/NPCS/Preset/Act06_3", pos, Quaternion.identity);
                //GameObject Varreosa4 = PhotonNetwork.Instantiate("Buildables/NPCS/Preset/Fay", pos, Quaternion.identity);
                /// GameObject WaterBase = PhotonNetwork.Instantiate("Buildables/WaterEmpty", pos, Quaternion.identity);
                /// 
                GameObject WaterBase = PhotonNetwork.Instantiate("Buildables/BeyondShiganshinaParent", pos, Quaternion.identity);
                
            }


            catch { /* Silently ignore errors if any occur */ }
        }
    }
}
