using UnityEngine;
using Photon.Pun;
using System.Linq;

public class CloakStateSync : MonoBehaviourPun, IPunObservable
{
    private bool _cloakIsOn = false;

    public void Toggle()
    {
        _cloakIsOn = !_cloakIsOn;
        UpdateCloak();
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(_cloakIsOn);
        }
        else
        {
            _cloakIsOn = (bool)stream.ReceiveNext();
            UpdateCloak();
        }
    }

    private void UpdateCloak()
    {
        Transform cloak2 = transform.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.name == "cloak2model");

        if (cloak2 != null)
        {
            var renderer = cloak2.GetComponent<SkinnedMeshRenderer>();
            if (renderer != null)
                renderer.enabled = _cloakIsOn;

            var cloth = cloak2.GetComponent<Cloth>();
            if (cloth != null)
                cloth.enabled = _cloakIsOn;
        }

        // If cloak2 is being turned off, also toggle off cloak1 via its own Toggle method
        if (!_cloakIsOn)
        {
            Transform cloak1 = transform.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "cloak1model");

            if (cloak1 != null)
            {
                var cloak1Renderer = cloak1.GetComponent<MeshRenderer>();
                if (cloak1Renderer != null && cloak1Renderer.enabled)
                {
                    var sync = GetComponent<Cloak1StateSync>();
                    if (sync != null)
                    {
                        sync.Toggle(); // Cleanly toggles cloak1 off

                    }

                }
            }
        }


    }
}
