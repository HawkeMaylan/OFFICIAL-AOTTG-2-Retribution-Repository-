using UnityEngine;
using Photon.Pun;

public class NetworkedRigidbodyStatFixer : MonoBehaviourPun
{
    [Header("Target")]
    public Rigidbody targetRigidbody;

    [Header("Check Settings")]
    public float checkInterval = 1f;

    [Header("Expected Rigidbody Stats")]
    public float expectedMass = 1f;
    public bool expectedUseGravity = true;
    public float expectedDrag = 0f;
    public float expectedAngularDrag = 0.05f;

    [Header("Mount State Check")]
    public bool isMounted = false; // Set this externally by your mount system

    private float timer = 0f;

    private void Reset()
    {
        targetRigidbody = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        if (targetRigidbody == null)
            targetRigidbody = GetComponent<Rigidbody>();

        if (targetRigidbody != null)
        {
            // Initialize expected values from actual Rigidbody
            expectedMass = targetRigidbody.mass;
            expectedUseGravity = targetRigidbody.useGravity;
            expectedDrag = targetRigidbody.drag;
            expectedAngularDrag = targetRigidbody.angularDrag;
        }
    }

    private void Update()
    {
        if (!photonView.IsMine || isMounted || targetRigidbody == null)
            return;

        timer += Time.deltaTime;
        if (timer >= checkInterval)
        {
            timer = 0f;
            CheckAndFixStatsIfNeeded();
        }
    }

    private void CheckAndFixStatsIfNeeded()
    {
        bool needsFix =
            targetRigidbody.mass != expectedMass ||
            targetRigidbody.useGravity != expectedUseGravity ||
            targetRigidbody.drag != expectedDrag ||
            targetRigidbody.angularDrag != expectedAngularDrag;

        if (needsFix)
        {
            photonView.RPC("RPC_ApplyStats", RpcTarget.AllBuffered,
                expectedMass, expectedUseGravity, expectedDrag, expectedAngularDrag);
        }
    }

    [PunRPC]
    public void RPC_ApplyStats(float mass, bool useGravity, float drag, float angularDrag)
    {
        if (targetRigidbody == null) return;

        targetRigidbody.mass = mass;
        targetRigidbody.useGravity = useGravity;
        targetRigidbody.drag = drag;
        targetRigidbody.angularDrag = angularDrag;
    }
}
