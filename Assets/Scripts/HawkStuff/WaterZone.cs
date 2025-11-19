using UnityEngine;
using Characters; // Assuming Human is in Characters namespace

public class WaterZone : MonoBehaviour
{
    public float floatStrength = 3f;
    public float floatStrength2 = 3f;
    private void OnTriggerEnter(Collider other)
    {
        Human human = other.GetComponentInParent<Human>();
        if (human != null && human.IsMine())
        {
            DrainGas(human);
            EnterWater(human);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        Human human = other.GetComponentInParent<Human>();
        if (human != null && human.IsMine())
        {
            ApplyFloat(human);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Human human = other.GetComponentInParent<Human>();
        if (human != null && human.IsMine())
        {
            ExitWater(human);
        }
    }

    private void DrainGas(Human human)
    {
        human.Stats.CurrentGas = 0f;
    }

    private void EnterWater(Human human)
    {
        Rigidbody rb = human.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.drag = 5f;        // Higher drag = move slower in water
            rb.angularDrag = 5f;
        }
    }

    private void ApplyFloat(Human human)
    {
        Rigidbody rb = human.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Float upward (global Y-axis)
            rb.AddForce(Vector3.up * floatStrength, ForceMode.Acceleration);

            // Push forward (relative to player's facing direction)
            Vector3 playerForward = human.transform.forward;
            rb.AddForce(playerForward * floatStrength2, ForceMode.Acceleration);
        }
    }

    private void ExitWater(Human human)
    {
        Rigidbody rb = human.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.drag = 0f;         // Reset normal drag
            rb.angularDrag = 0.05f;
        }
    }
}
