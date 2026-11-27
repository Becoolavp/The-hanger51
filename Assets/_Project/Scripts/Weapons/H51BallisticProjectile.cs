using System;
using Hanger51.Health;
using Hanger51.NPCs;
using Rng.Blood;
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
        int shotSequence;
        static readonly System.Collections.Generic.Dictionary<long,float> shotgunBloodedVictims =
            new System.Collections.Generic.Dictionary<long,float>();
        const float Radius = .006f;
        const float MaximumLifetime = 7f;

        public void Initialize(
            H51CombatItemDefinition configuredDefinition,
            GameObject configuredOwner,
            Vector3 position,
            Vector3 direction,
            float velocityMultiplier,
            int configuredShotSequence = 0)
        {
            definition = configuredDefinition;
            owner = configuredOwner;
            shotSequence = configuredShotSequence;
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

            H51AnatomicalHitbox anatomicalHitbox =
                hit.collider.GetComponent<H51AnatomicalHitbox>();

            H51AnatomicalHealth health =
                anatomicalHitbox && anatomicalHitbox.Health
                    ? anatomicalHitbox.Health
                    : hit.collider.GetComponentInParent<H51AnatomicalHealth>();

            if (health)
            {
                float trauma =
                    Mathf.Clamp(
                        energyJoules / 55f *
                        definition.DamageScale,
                        2f,
                        95f);

                if (definition.WeaponClass == H51WeaponClass.Shotgun)
                    trauma *= .70f;

                float penetration =
                    Mathf.Clamp01(
                        (energyJoules - 220f) / 2500f);

                if (definition.WeaponClass == H51WeaponClass.Shotgun)
                    penetration =
                        Mathf.Max(
                            penetration,
                            .55f);

                H51RagdollBone ragdollBone =
                    hit.collider.GetComponentInParent<H51RagdollBone>();

                H51BodyRegion region =
                    anatomicalHitbox &&
                    anatomicalHitbox.Health == health
                        ? anatomicalHitbox.Region
                        : ragdollBone
                            ? ragdollBone.Region
                            : H51BodyRegion.Chest;

                if (anatomicalHitbox && anatomicalHitbox.Health == health)
                {
                    health.ApplyDamage(
                        region,
                        hit.point,
                        trauma,
                        H51DamageType.Ballistic,
                        velocity.normalized,
                        penetration);
                }
                else
                {
                    health.ApplyDamage(
                        hit.point,
                        trauma,
                        H51DamageType.Ballistic,
                        velocity.normalized,
                        penetration);
                }

                H51BallisticImpactReaction reaction =
                    health.GetComponent<H51BallisticImpactReaction>();
                if (!reaction)
                    reaction =
                        health.gameObject.AddComponent<H51BallisticImpactReaction>();

                reaction.ApplyProjectileImpact(
                    region,
                    velocity.normalized,
                    momentum,
                    trauma);

                H51BloodVisualBridge blood =
                    health.GetComponent<H51BloodVisualBridge>();
                if (!blood)
                    blood =
                        health.gameObject.AddComponent<H51BloodVisualBridge>();

                if (ShouldSpawnBloodForVictim(health))
                {
                    BloodHitKind bloodKind =
                        region == H51BodyRegion.Head
                            ? BloodHitKind.Headshot
                            : definition.WeaponClass == H51WeaponClass.Shotgun
                                ? BloodHitKind.Shotgun
                                : owner &&
                                  Vector3.Distance(
                                      owner.transform.position,
                                      hit.point) < 3f
                                    ? BloodHitKind.BulletClose
                                    : BloodHitKind.Bullet;

                    blood.RegisterBallisticWound(
                        region,
                        hit.point,
                        hit.normal,
                        velocity.normalized,
                        trauma,
                        bloodKind);
                }

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

        bool ShouldSpawnBloodForVictim(
            H51AnatomicalHealth health)
        {
            if (!health ||
                definition.WeaponClass != H51WeaponClass.Shotgun ||
                shotSequence == 0)
                return true;

            long key =
                ((long)shotSequence << 32) ^
                (uint)health.GetInstanceID();

            if (shotgunBloodedVictims.ContainsKey(key))
                return false;

            shotgunBloodedVictims[key] =
                Time.time;

            if (shotgunBloodedVictims.Count > 512)
            {
                float cutoff = Time.time-10f;
                var remove =
                    new System.Collections.Generic.List<long>();

                foreach (var pair in shotgunBloodedVictims)
                    if (pair.Value < cutoff)
                        remove.Add(pair.Key);

                for (int i=0;i<remove.Count;i++)
                    shotgunBloodedVictims.Remove(remove[i]);
            }

            return true;
        }
    }
}
