using UnityEngine;
using System.Collections;

public class HawkMountableObject : MonoBehaviour
{
    [Header("Mount Settings")]
    public Transform mountPoint; // Optional specific mount point
    public bool IsOccupied = false;

    [Header("Visual Settings")]
    public bool showMountPrompt = true;

    private Vector3 _lastPosition;
    private Vector3 _currentVelocity;

    private void Start()
    {
        // If no specific mount point, use the object's transform
        if (mountPoint == null)
            mountPoint = transform;

        _lastPosition = mountPoint.position;
    }

    private void Update()
    {
        // Calculate velocity based on position change
        Vector3 newPosition = mountPoint.position;
        _currentVelocity = (newPosition - _lastPosition) / Time.deltaTime;
        _lastPosition = newPosition;
    }

    public Vector3 GetVelocity()
    {
        return _currentVelocity;
    }

    public void OnMounted()
    {
        IsOccupied = true;
        // Reset velocity tracking when mounted
        _lastPosition = mountPoint.position;
        _currentVelocity = Vector3.zero;
    }

    public void OnUnmounted()
    {
        IsOccupied = false;
    }

    // Optional: Visual feedback when mountable
    private void OnDrawGizmos()
    {
        if (showMountPrompt)
        {
            Gizmos.color = IsOccupied ? Color.red : Color.green;
            Gizmos.DrawWireSphere(transform.position, 15f); // Show mount range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(mountPoint.position, 0.5f); // Show mount point

            // Show velocity direction
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(mountPoint.position, _currentVelocity.normalized * 2f);
        }
    }
}