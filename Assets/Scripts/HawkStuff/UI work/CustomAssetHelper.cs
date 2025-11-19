using UnityEngine;
using Photon.Pun;

public class CustomAssetHelper : MonoBehaviourPun
{
    public void Move(Vector3 newPosition, Vector3 newRotation)
    {
        if (photonView.IsMine || !PhotonNetwork.IsConnected)
        {
            transform.position = newPosition;
            transform.rotation = Quaternion.Euler(newRotation);
        }
        else
        {
            photonView.RPC("RPC_Move", RpcTarget.MasterClient, newPosition, newRotation);
        }
    }

    public void Delete()
    {
        if (photonView.IsMine || !PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Destroy(gameObject);
        }
        else
        {
            photonView.RPC("RPC_Delete", RpcTarget.MasterClient);
        }
    }

    [PunRPC]
    private void RPC_Move(Vector3 newPosition, Vector3 newRotation)
    {
        transform.position = newPosition;
        transform.rotation = Quaternion.Euler(newRotation);
    }

    [PunRPC]
    private void RPC_Delete()
    {
        PhotonNetwork.Destroy(gameObject);
    }
}





