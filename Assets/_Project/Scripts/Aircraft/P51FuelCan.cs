using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class P51FuelCan : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float capacityGallons = 5f;
        [SerializeField, Min(0f)] private float gallonsRemaining = 5f;

        public float CapacityGallons => capacityGallons;
        public float GallonsRemaining => gallonsRemaining;
        public bool HasFuel => gallonsRemaining > 0.001f;

        public void Configure(float configuredCapacityGallons, float configuredGallons)
        {
            capacityGallons = Mathf.Max(0.1f, configuredCapacityGallons);
            gallonsRemaining = Mathf.Clamp(configuredGallons, 0f, capacityGallons);
        }

        public float DrawFuel(float requestedGallons)
        {
            float amount = Mathf.Clamp(requestedGallons, 0f, gallonsRemaining);
            gallonsRemaining -= amount;
            return amount;
        }

        public void Refill()
        {
            gallonsRemaining = capacityGallons;
        }

        private void OnValidate()
        {
            capacityGallons = Mathf.Max(0.1f, capacityGallons);
            gallonsRemaining = Mathf.Clamp(gallonsRemaining, 0f, capacityGallons);
        }
    }
}
