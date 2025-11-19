using UnityEngine;
using Photon.Pun;
using System.Linq;

public class LanternStateSync : MonoBehaviourPun, IPunObservable
{
    private bool _lampIsOn = false;

    public void Toggle()
    {
        _lampIsOn = !_lampIsOn;
        UpdateLamp();
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(_lampIsOn);
        }
        else
        {
            _lampIsOn = (bool)stream.ReceiveNext();
            UpdateLamp();
        }
    }

    private void UpdateLamp()
    {
        Transform lamp0 = transform.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.name == "HumanLamp0");

        Transform lamp1 = transform.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.name == "HumanLamp1");

        if (lamp0 != null)
        {
            var mesh = lamp0.GetComponent<MeshRenderer>();
            if (mesh != null)
                mesh.enabled = !_lampIsOn;
        }

        if (lamp1 != null)
        {
            var mesh = lamp1.GetComponent<MeshRenderer>();
            var light = lamp1.GetComponent<Light>();

            if (mesh != null)
                mesh.enabled = _lampIsOn;
            if (light != null)
                light.enabled = _lampIsOn;
        }


    }
}
