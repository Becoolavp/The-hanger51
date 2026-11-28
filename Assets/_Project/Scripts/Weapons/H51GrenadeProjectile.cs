using System.Collections.Generic;
using Hanger51.Health;
using Hanger51.NPCs;
using UnityEngine;

namespace Hanger51.Weapons
{
    [DisallowMultipleComponent]
    public sealed class H51GrenadeProjectile : MonoBehaviour
    {
        H51CombatItemDefinition definition;
        GameObject owner;
        float detonateAt;
        bool detonated;

        public void Initialize(
            H51CombatItemDefinition configuredDefinition,
            GameObject configuredOwner)
        {
            definition = configuredDefinition;
            owner = configuredOwner;
            detonateAt =
                Time.time +
                (definition ? definition.GrenadeFuseSeconds : 3.5f);
        }

        void Update()
        {
            if (!detonated && Time.time >= detonateAt)
                Detonate();
        }

        void Detonate()
        {
            if (detonated)
                return;

            detonated = true;

            float radius =
                definition
                    ? definition.GrenadeBlastRadius
                    : 6f;
            float baseTrauma =
                definition
                    ? definition.GrenadeTrauma
                    : 70f;

            Vector3 center =
                transform.position;

            Collider[] hits =
                Physics.OverlapSphere(
                    center,
                    radius,
                    ~0,
                    QueryTriggerInteraction.Collide);

            HashSet<H51AnatomicalHealth> affected =
                new HashSet<H51AnatomicalHealth>();

            for (int i=0;i<hits.Length;i++)
            {
                Collider collider = hits[i];
                if (!collider)
                    continue;

                Rigidbody rb = collider.attachedRigidbody;
                if (rb && !rb.isKinematic)
                {
                    rb.AddExplosionForce(
                        320f,
                        center,
                        radius,
                        .25f,
                        ForceMode.Impulse);
                }

                H51AnatomicalHealth health =
                    collider.GetComponentInParent<H51AnatomicalHealth>();

                if (!health || affected.Contains(health))
                    continue;

                affected.Add(health);

                float distance =
                    Vector3.Distance(
                        center,
                        health.transform.position + Vector3.up);
                float falloff =
                    Mathf.Clamp01(1f - distance / radius);
                if (falloff <= .01f)
                    continue;

                Vector3 direction =
                    health.transform.position - center;
                direction.y += .45f;
                direction.Normalize();

                float trauma =
                    baseTrauma *
                    Mathf.Lerp(.18f,1f,falloff);

                health.ApplyDamage(
                    H51BodyRegion.Chest,
                    trauma,
                    H51DamageType.Explosion,
                    .35f);

                H51BloodVisualBridge blood =
                    health.GetComponent<H51BloodVisualBridge>();
                if (!blood)
                    blood =
                        health.gameObject.AddComponent<H51BloodVisualBridge>();

                blood.RegisterExplosion(
                    direction,
                    trauma);

                H51BallisticImpactReaction reaction =
                    health.GetComponent<H51BallisticImpactReaction>();
                if (!reaction)
                    reaction =
                        health.gameObject.AddComponent<H51BallisticImpactReaction>();

                reaction.ApplyExplosionImpact(
                    direction,
                    trauma);

                H51CitizenEmergencyState emergency =
                    health.GetComponent<H51CitizenEmergencyState>();
                if (emergency)
                {
                    emergency.TriggerMedicalEmergency(
                        Mathf.Clamp(trauma,35f,100f),
                        "blast and fragmentation injuries",
                        true);
                }
            }

            Destroy(gameObject);
        }
    }
}
