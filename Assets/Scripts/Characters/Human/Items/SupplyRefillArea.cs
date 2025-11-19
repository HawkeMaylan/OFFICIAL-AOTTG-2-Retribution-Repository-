using UnityEngine;
using Characters;

namespace Spawnables
{
    public class SupplyRefillArea : MonoBehaviour
    {
        [SerializeField] private float gasFillRate = 20f;    // Units of gas per second
        [SerializeField] private float fullRefillTime = 3f;  // Seconds needed for full refill

        private void OnTriggerStay(Collider other)
        {
            var human = other.transform.root.GetComponent<Human>();
            if (human != null && human.IsMine())
            {
                if (!human.TryGetComponent<RefillScript>(out var refillScript))
                {
                    refillScript = human.gameObject.AddComponent<RefillScript>();
                    refillScript.Initialize(this);
                }

                refillScript.UpdateRefill(Time.deltaTime);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var human = other.transform.root.GetComponent<Human>();
            if (human != null)
            {
                if (human.TryGetComponent<RefillScript>(out var refillScript))
                {
                    Destroy(refillScript);
                }
            }
        }

        public float GetGasFillRate() => gasFillRate;
        public float GetFullRefillTime() => fullRefillTime;
    }

    public class RefillScript : MonoBehaviour
    {
        private SupplyRefillArea _supply;
        private Human _human;
        private float _timeInside = 0f;

        public void Initialize(SupplyRefillArea supply)
        {
            _supply = supply;
            _human = GetComponent<Human>();
            _timeInside = 0f;
        }

        public void UpdateRefill(float deltaTime)
        {
            if (_human == null || _human.Dead)
                return;

            _timeInside += deltaTime;

            // Gradually refill gas
            if (_human.Stats.CurrentGas < _human.Stats.MaxGas)
            {
                _human.Stats.CurrentGas += _supply.GetGasFillRate() * deltaTime;
                _human.Stats.CurrentGas = Mathf.Min(_human.Stats.CurrentGas, _human.Stats.MaxGas);
            }

            // Fully refill if stayed long enough
            if (_timeInside >= _supply.GetFullRefillTime())
            {
                _human.FinishRefill();
                _timeInside = 0f; // Reset timer so can refill again if staying inside
            }
        }
    }
}
