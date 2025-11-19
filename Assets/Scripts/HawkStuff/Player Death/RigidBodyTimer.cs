using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RigidbodyTimer : MonoBehaviour
{
    private float delayTime;
    private Coroutine timerCoroutine;
    private List<string> bodyPartsToRemove;

    public void Initialize(float delay, List<string> bodyParts)
    {
        delayTime = delay;
        bodyPartsToRemove = bodyParts;

        // Start the timer
        timerCoroutine = StartCoroutine(ExecuteAfterDelay());
    }

    private IEnumerator ExecuteAfterDelay()
    {
        yield return new WaitForSeconds(delayTime);

        // Remove rigidbodies from specified body parts
        RemoveRigidbodiesFromBodyParts();

        // Self-destruct after completing the task
        Destroy(this);
    }

    private void RemoveRigidbodiesFromBodyParts()
    {
        if (bodyPartsToRemove == null || bodyPartsToRemove.Count == 0)
        {
            Debug.LogWarning("No body parts specified for rigidbody removal");
            return;
        }

        foreach (string bodyPartName in bodyPartsToRemove)
        {
            RemoveRigidbodyFromBodyPart(bodyPartName);
        }

        Debug.Log($"Removed rigidbodies from {bodyPartsToRemove.Count} body parts after {delayTime} seconds");
    }

    private void RemoveRigidbodyFromBodyPart(string bodyPartName)
    {
        Transform bodyPartTransform = FindDeepChild(transform, bodyPartName);
        if (bodyPartTransform != null)
        {
            GameObject bodyPart = bodyPartTransform.gameObject;
            Rigidbody rb = bodyPart.GetComponent<Rigidbody>();

            if (rb != null)
            {
                Destroy(rb);
                Debug.Log($"Removed rigidbody from: {bodyPartName}");
            }
            else
            {
                Debug.LogWarning($"No rigidbody found on body part: {bodyPartName}");
            }
        }
        else
        {
            Debug.LogWarning($"Body part '{bodyPartName}' not found in hierarchy");
        }
    }

    // Recursive method to find deep child by name
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

    private void OnDestroy()
    {
        // Stop coroutine if object is destroyed prematurely
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
        }
    }
}