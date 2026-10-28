using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class P51RadiatorDamageReceiver : MonoBehaviour
    {
        [SerializeField] private P51RadiatorCoolingSystem coolingSystem;
        [SerializeField, Min(0.01f)] private float projectileDamageMultiplier = 0.45f;
        [SerializeField, Min(0f)] private float collisionImpulseThreshold = 2500f;
        [SerializeField, Min(0f)] private float collisionDamagePer1000Impulse = 1.5f;

        public P51RadiatorCoolingSystem CoolingSystem => coolingSystem;

        public void Configure(P51RadiatorCoolingSystem configuredCoolingSystem)
        {
            coolingSystem = configuredCoolingSystem;
        }

        public void RegisterProjectileHit(Vector3 hitPoint, float incomingDamage)
        {
            ResolveReferences();
            if (coolingSystem == null || incomingDamage <= 0f)
            {
                return;
            }

            coolingSystem.ApplyRadiatorDamage(
                incomingDamage * projectileDamageMultiplier,
                hitPoint);
        }

        private void OnCollisionEnter(Collision collision)
        {
            ResolveReferences();
            if (coolingSystem == null || collision == null)
            {
                return;
            }

            float impulse = collision.impulse.magnitude;
            if (impulse <= collisionImpulseThreshold)
            {
                return;
            }

            float damage = (impulse - collisionImpulseThreshold)
                / 1000f
                * collisionDamagePer1000Impulse;
            Vector3 hitPoint = collision.contactCount > 0
                ? collision.GetContact(0).point
                : transform.position;
            coolingSystem.ApplyRadiatorDamage(damage, hitPoint);
        }

        private void ResolveReferences()
        {
            if (coolingSystem == null)
            {
                coolingSystem = GetComponentInParent<P51RadiatorCoolingSystem>();
            }
        }

        private void OnValidate()
        {
            projectileDamageMultiplier = Mathf.Max(0.01f, projectileDamageMultiplier);
            collisionImpulseThreshold = Mathf.Max(0f, collisionImpulseThreshold);
            collisionDamagePer1000Impulse = Mathf.Max(0f, collisionDamagePer1000Impulse);
        }
    }
}
