using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class HumanCorpseRagdoller : MonoBehaviour
{
    [Header("Body Parts to Process")]
    [SerializeField]
    private List<string> bodyPartsToProcess = new List<string>()
    {
        "hip", "chest", "spine", "shoulder_L", "shoulder_R",
        "neck", "head", "forearm_L", "forearm_R", "hand_L", "hand_R"
    };

    [Header("Rigidbody Settings")]
    [SerializeField] private float mass = 0f;
    [SerializeField] private float drag = 0f;
    [SerializeField] private float angularDrag = 1f;
    [SerializeField] private float rigorDelay = 1f;

    [Header("Torque Settings")]
    [SerializeField] private float baseTorqueForce = 10f;

    [Header("Collider Settings")]
    [SerializeField] private float capsuleRadius = 0.5f;
    [SerializeField] private float capsuleHeight = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    void Awake()
    {
        // Equivalent to RPC_ProcessAllSubObjects()
        ProcessAllSubObjects();

        // Equivalent to RPC_ConfigureAllColliders()
        ConfigureAllColliders();

        StartCoroutine(RemoveRigidbodiesAfterDelay(rigorDelay));

        if (debugLogs)
        {
            Debug.Log($"[ChildPhysicsProcessor] Completed physics processing on {gameObject.name}");
        }
    }

    /// <summary>
    /// Equivalent to RPC_ProcessAllSubObjects - processes all body parts with rigidbodies
    /// </summary>
    private void ProcessAllSubObjects()
    {
        foreach (string bodyPartName in bodyPartsToProcess)
        {
            ProcessSubObjectRecursive(bodyPartName);
        }
    }

    /// <summary>
    /// Equivalent to the original ProcessSubObjectRecursive method
    /// </summary>
    private void ProcessSubObjectRecursive(string objectName)
    {
        Transform subObjectTransform = FindDeepChild(transform, objectName);
        if (subObjectTransform != null)
        {
            GameObject subObject = subObjectTransform.gameObject;

            // Add Rigidbody component if it doesn't exist
            Rigidbody subRb = subObject.GetComponent<Rigidbody>();
            if (subRb == null)
            {
                subRb = subObject.AddComponent<Rigidbody>();
            }

            // Configure the Rigidbody - FREE rotation but FROZEN position (exactly like original)
            subRb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            subRb.constraints = RigidbodyConstraints.FreezePosition; // Freeze all position movement
            subRb.detectCollisions = true;
            subRb.includeLayers = ~0; // All layers

            // Set mass, drag, and angular drag to 0 (exactly like original)
            subRb.mass = mass;
            subRb.drag = drag;
            subRb.angularDrag = angularDrag;

            ApplyRandomRotationForce(subRb, objectName);

            if (debugLogs)
            {
                Debug.Log($"Processed sub object: {subObject.name} at path: {GetGameObjectPath(subObjectTransform)}");
            }
        }
        else
        {
            Debug.LogWarning($"Sub object '{objectName}' not found in hierarchy on {gameObject.name}");
        }
    }

    /// <summary>
    /// Equivalent to RPC_ConfigureAllColliders
    /// </summary>
    private void ConfigureAllColliders()
    {
        // Configure capsule colliders (exactly like original)
        CapsuleCollider[] capsuleColliders = GetComponents<CapsuleCollider>();
        foreach (CapsuleCollider collider in capsuleColliders)
        {
            collider.enabled = true;
            collider.isTrigger = false;
            collider.includeLayers = ~0;
            collider.excludeLayers = 0;
            collider.radius = capsuleRadius;
            collider.height = capsuleHeight;
        }

        // Also configure any other colliders to include everything (exactly like original)
        Collider[] allColliders = GetComponents<Collider>();
        foreach (Collider collider in allColliders)
        {
            collider.enabled = true;
            collider.isTrigger = false;
            collider.includeLayers = ~0;
            collider.excludeLayers = 0;
        }

        if (debugLogs)
        {
            Debug.Log($"Configured {capsuleColliders.Length} capsule colliders and {allColliders.Length} total colliders on {gameObject.name}");
        }
    }

    /// <summary>
    /// Equivalent to the original ApplyRandomRotationForce method
    /// </summary>
    private void ApplyRandomRotationForce(Rigidbody rb, string bodyPartName)
    {
        float torqueMultiplier = 1f;

        // Adjust torque based on body part size/type (exactly like original)
        switch (bodyPartName.ToLower())
        {
            case "hip":
            case "chest":
                torqueMultiplier = 0.5f; // Less rotation for core parts
                break;
            case "head":
                torqueMultiplier = 0.3f; // Even less for head
                break;
        }

        Vector3 randomTorque = new Vector3(
            Random.Range(-baseTorqueForce, baseTorqueForce) * torqueMultiplier,
            Random.Range(-baseTorqueForce, baseTorqueForce) * torqueMultiplier,
            Random.Range(-baseTorqueForce, baseTorqueForce) * torqueMultiplier
        );

        rb.AddTorque(randomTorque, ForceMode.Impulse);
    }

    // Recursive method to find deep child by name (exactly like original)
    private Transform FindDeepChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform result = FindDeepChild(child, childName);
            if (result != null)
                return result;
        }
        return null;
    }

    // Helper method to get full path for debugging (exactly like original)
    private string GetGameObjectPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }

    // Public methods for manual control if needed
    public void ReprocessAllBodyParts()
    {
        ProcessAllSubObjects();
    }

    public void ReconfigureColliders()
    {
        ConfigureAllColliders();
    }


    private IEnumerator RemoveRigidbodiesAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        foreach (string bodyPartName in bodyPartsToProcess)
        {
            Transform bodyPart = FindDeepChild(transform, bodyPartName);
            if (bodyPart != null)
            {
                Rigidbody rb = bodyPart.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Destroy(rb);
                }

                // Also remove colliders if needed
                Collider collider = bodyPart.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }
            }
        }

        if (debugLogs)
        {
            Debug.Log($"[HumanCorpseRagdoller] Removed Rigidbodies after {delay} seconds");
        }
    }


    // Editor helpers
#if UNITY_EDITOR
    [ContextMenu("Debug Find Body Parts")]
    private void DebugFindBodyParts()
    {
        int foundCount = 0;
        foreach (string bodyPartName in bodyPartsToProcess)
        {
            Transform found = FindDeepChild(transform, bodyPartName);
            if (found != null)
            {
                Debug.Log($"Found: {bodyPartName} at {GetGameObjectPath(found)}", found);
                foundCount++;
            }
            else
            {
                Debug.LogWarning($"Missing: {bodyPartName}");
            }
        }
        Debug.Log($"Found {foundCount}/{bodyPartsToProcess.Count} body parts on {gameObject.name}");
    }

    [ContextMenu("Manual Process Now")]
    private void ManualProcessNow()
    {
        ProcessAllSubObjects();
        ConfigureAllColliders();
        Debug.Log("Manually processed physics");
    }
#endif
}