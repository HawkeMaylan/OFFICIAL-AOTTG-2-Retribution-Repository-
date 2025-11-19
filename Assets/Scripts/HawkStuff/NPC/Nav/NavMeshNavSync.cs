using UnityEngine;
using UnityEngine.AI;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class NavMeshNavSync : MonoBehaviourPun, IPunObservable
{
    public Transform target;                 // Anyone can assign this
    public NavMeshAgent agent;               // Drag manually in Inspector

    private Vector3 networkedPosition;
    private Quaternion networkedRotation;

    private void Start()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            agent.Warp(hit.position); // Fully initializes NavMeshAgent
        }
        else
        {
            Debug.LogError("Could not snap agent to NavMesh at start.");
        }
    }

    private void Update()
    {
        if (agent != null && agent.isOnNavMesh && target != null)
        {
            agent.isStopped = false;
            agent.SetDestination(target.position);
            Debug.DrawLine(transform.position, target.position, Color.green);
        }

        // Sync visuals for clients (smoothing)
        if (!photonView.IsMine)
        {
            transform.position = Vector3.Lerp(transform.position, networkedPosition, Time.deltaTime * 10f);
            transform.rotation = Quaternion.Lerp(transform.rotation, networkedRotation, Time.deltaTime * 10f);
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
        }
        else
        {
            networkedPosition = (Vector3)stream.ReceiveNext();
            networkedRotation = (Quaternion)stream.ReceiveNext();
        }
    }
}
