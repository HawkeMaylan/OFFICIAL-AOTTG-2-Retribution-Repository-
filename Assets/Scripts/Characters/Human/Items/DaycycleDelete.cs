using Characters;
using UnityEngine;
using Photon.Pun;

namespace Characters
{
    class DaycycleDelete : SimpleUseable
    {
        public DaycycleDelete(BaseCharacter owner, string name, float cooldown) : base(owner)
        {
            Name = name;
            Cooldown = cooldown;
        }

        protected override void Activate()
        {
            var human = _owner as Human;
            if (human == null || !_owner.photonView.IsMine || !PhotonNetwork.InRoom || !PhotonNetwork.IsConnectedAndReady)
                return;

            try
            {
                GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();

                foreach (var obj in allObjects)
                {
                    if (obj.name.Contains("HawkDayNightManager") && obj.GetComponent<PhotonView>() != null)
                    {
                        if (obj.GetComponent<PhotonView>().IsMine)
                        {
                            PhotonNetwork.Destroy(obj);
                        }
                    }
                }
            }
            catch { /* Silently ignore errors if any occur */ }
        }
    }
}
