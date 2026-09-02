using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class P51CoolantJug : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float capacityLiters = 10f;
        [SerializeField, Min(0f)] private float litersRemaining = 10f;

        public float CapacityLiters => capacityLiters;
        public float LitersRemaining => litersRemaining;
        public bool HasCoolant => litersRemaining > 0.001f;

        public void Configure(float configuredCapacityLiters, float configuredLiters)
        {
            capacityLiters = Mathf.Max(0.1f, configuredCapacityLiters);
            litersRemaining = Mathf.Clamp(configuredLiters, 0f, capacityLiters);
        }

        public float DrawCoolant(float requestedLiters)
        {
            float amount = Mathf.Clamp(requestedLiters, 0f, litersRemaining);
            litersRemaining -= amount;
            return amount;
        }

        public void Refill()
        {
            litersRemaining = capacityLiters;
        }

        private void OnValidate()
        {
            capacityLiters = Mathf.Max(0.1f, capacityLiters);
            litersRemaining = Mathf.Clamp(litersRemaining, 0f, capacityLiters);
        }
    }
}
