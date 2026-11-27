using System;
using Hanger51.Health;
using Hanger51.NPCs;
using UnityEngine;

namespace Hanger51.Weapons
{
    [DisallowMultipleComponent]
    public sealed class H51BallisticProjectile : MonoBehaviour
    {
        H51CombatItemDefinition definition;
        GameObject owner;
        Vector3 velocity;
        float age;
        float projectileMassKg;
        const float Radius = .006f;
        const float MaximumLifetime = 7f;

        public void Initialize(
            H51CombatItemDefinition configuredDefinition,
            GameObject configuredOwner,
            Vector3 position,
            Vector3 direction,
            float velocityMultiplier)
        {
            definition = configuredDefinition;
            owner = configuredOwner;
            transform.position = position;

            projectileMassKg =
                definition
                    ? definition.ProjectileMassGrams / 1000f
                    : .004f;

            float muzzleVelocity =
                definition
                    ? definition.MuzzleVelocityMps
                    : 800f;

            velocity =
                direction.normalized *
                muzzleVelocity *
                Mathf.Clamp(velocityMultiplier,.5f,1.1f);
        }

        void FixedUpdate()
        {
            if (!definition)
            {
                Destroy(gameObject);
                return;
            }

            float dt = Time.fixedDeltaTime;
            age += dt;
            if (age >= MaximumLifetime)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 start = transform.position;
            float speed = velocity.magnitude;
            if (speed < 1f)
            {
                Destroy(gameObject);
                return;
            }

            // Lightweight external-ballistics approximation. Gravity is real
            // Unity gravity; drag is intentionally modest and caliber data-driven.
            Vector3 drag =
                -velocity.normalized *
                definition.BallisticDrag *
                speed * speed *
                .000035f;

            velocity +=
                (Physics.gravity + drag) * dt;

            Vector3 travel = velocity * dt;
            float distance = travel.magnitude;
            if (distance <= .0001f)
                return;

            RaycastHit[] hits =
                Physics.SphereCastAll(
                    start,
                    Radius,
                    travel.normalized,
                    distance,
                    ~0,
                    QueryTriggerInteraction.Collide);

            if (hits != null && hits.Length > 0)
            {
                Array.Sort(
                    hits,
                    (a,b) => a.distance.CompareTo(b.distance));

                for (int i=0;i<hits.Length;i++)
                {
                    RaycastHit hit = hits[i];
                    if (!hit.collider)
                        continue;

                    if (owner &&
                        hit.collider.transform.IsChildOf(owner.transform))
                        continue;

                    Impact(hit);
                    return;
                }
            }

            transform.position = start + travel;
        }

        void Impact(RaycastHit hit)
        {
            float impactSpeed = velocity.magnitude;
            float energyJoules =
                .5f *
                projectileMassKg *
                impactSpeed *
                impactSpeed;
            float momentum =
                projectileMassKg *
                impactSpeed;

            Rigidbody rigidbody =
                hit.rigidbody;
            if (rigidbody && !rigidbody.isKinematic)
            {
                rigidbody.AddForceAtPosition(
                    velocity.normalized * momentum,
                    hit.point,
                    ForceMode.Impulse);
            }

            H51AnatomicalHealth health =
                hit.collider.GetComponentInParent<H51AnatomicalHealth>();

            if (health)
            {
                float trauma =
                    Mathf.Clamp(
                        energyJoules / 55f *
                        definition.DamageScale,
                        2f,
                        95f);

                float penetration =
                    Mathf.Clamp01(
                        (energyJoules - 220f) / 2500f);

                health.ApplyDamage(
                    hit.point,
                    trauma,
                    H51DamageType.Ballistic,
                    velocity.normalized,
                    penetration);

                H51BallisticImpactReaction reaction =
                    health.GetComponent<H51BallisticImpactReaction>();
                if (!reaction)
                    reaction =
                        health.gameObject.AddComponent<H51BallisticImpactReaction>();

                reaction.ApplyProjectileImpact(
                    velocity.normalized,
                    momentum,
                    trauma);

                H51CitizenEmergencyState emergency =
                    health.GetComponent<H51CitizenEmergencyState>();
                if (emergency)
                {
                    emergency.TriggerMedicalEmergency(
                        Mathf.Clamp(
                            trauma + health.Shock * .35f,
                            28f,
                            100f),
                        "a gunshot wound",
                        true);
                }
            }

            Destroy(gameObject);
        }
    }
}
