using UnityEngine;
using Characters;

public class WagonAttachment : MonoBehaviour
{
    public string attachTriggerName = "AttachCollider";
    private bool isAttached = false;

    private void OnTriggerEnter(Collider other)
    {
        if (isAttached)
            return;

        // Make sure this is the specific attach collider
        if (other.gameObject.name != attachTriggerName)
            return;

        // Check if the thing that entered is a horse
        Horse horse = other.GetComponentInParent<Horse>();
        if (horse != null)
        {
            AttachToHorse(horse);
        }
    }

    private void AttachToHorse(Horse horse)
    {
        // Parent the wagon to the horse
        transform.SetParent(horse.transform);

        // Optional: reset local position/rotation if you want the wagon to snap nicely
        transform.localPosition = new Vector3(0, 0, -2); // adjust as needed
        transform.localRotation = Quaternion.identity;

        // Optional: Disable physics if needed
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        isAttached = true;
        Debug.Log("Wagon attached to horse!");
    }
}
