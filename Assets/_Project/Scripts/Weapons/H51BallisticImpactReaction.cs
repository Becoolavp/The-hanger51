using Hanger51.Health;
using UnityEngine;
using UnityEngine.AI;

namespace Hanger51.Weapons
{
    [DisallowMultipleComponent]
    public sealed class H51BallisticImpactReaction : MonoBehaviour
    {
        Vector3 reactionVelocity;
        float reactionYaw;
        H51AnatomicalHealth health;
        NavMeshAgent agent;
        Quaternion deathTargetRotation;
        bool deathPoseRequested;

        void Awake()
        {
            health = GetComponent<H51AnatomicalHealth>();
            agent = GetComponent<NavMeshAgent>();
        }

        public void ApplyProjectileImpact(
            Vector3 direction,
            float projectileMomentumKgMps,
            float trauma)
        {
            Vector3 planar = direction;
            planar.y = 0f;
            if (planar.sqrMagnitude < .001f)
                planar = transform.forward;
            planar.Normalize();

            // Real bullets carry surprisingly little whole-body momentum.
            // Keep the displacement restrained and let the anatomical injury
            // carry the severity instead of launching characters unrealistically.
            float velocityChange =
                Mathf.Clamp(projectileMomentumKgMps * .11f,.04f,.75f);

            reactionVelocity +=
                planar * velocityChange +
                Vector3.up * Mathf.Clamp(trauma / 500f,0f,.12f);

            reactionYaw +=
                Random.Range(-1f,1f) *
                Mathf.Clamp(trauma * .20f,2f,16f);

            if (health && health.IsDead)
                RequestDeathPose(planar);
        }

        public void ApplyExplosionImpact(
            Vector3 direction,
            float severity)
        {
            Vector3 planar = direction;
            planar.y = 0f;
            if (planar.sqrMagnitude < .001f)
                planar = transform.forward;
            planar.Normalize();

            reactionVelocity +=
                planar * Mathf.Clamp(severity * .012f,.08f,1.6f) +
                Vector3.up * Mathf.Clamp(severity * .006f,.02f,.55f);

            reactionYaw += Random.Range(-18f,18f);

            if (health && health.IsDead)
                RequestDeathPose(planar);
        }

        void Update()
        {
            if (!health)
                health = GetComponent<H51AnatomicalHealth>();
            if (!agent)
                agent = GetComponent<NavMeshAgent>();

            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            if (reactionVelocity.sqrMagnitude > .0001f)
            {
                Vector3 delta = reactionVelocity * dt;

                if (agent && agent.enabled && agent.isOnNavMesh && (!health || !health.IsDead))
                    agent.Move(delta);
                else
                    transform.position += delta;

                reactionVelocity =
                    Vector3.MoveTowards(
                        reactionVelocity,
                        Vector3.zero,
                        dt * 3.6f);
            }

            if (Mathf.Abs(reactionYaw) > .01f &&
                (!health || !health.IsDead))
            {
                transform.Rotate(
                    0f,
                    reactionYaw * dt * 5f,
                    0f,
                    Space.World);
                reactionYaw =
                    Mathf.MoveTowards(
                        reactionYaw,
                        0f,
                        dt * 18f);
            }

            if (health && health.IsDead)
            {
                if (!deathPoseRequested)
                    RequestDeathPose(transform.forward);

                transform.rotation =
                    Quaternion.Slerp(
                        transform.rotation,
                        deathTargetRotation,
                        1f - Mathf.Exp(-dt * 3.6f));
            }
        }

        void RequestDeathPose(Vector3 impactDirection)
        {
            deathPoseRequested = true;

            Vector3 side =
                Vector3.Cross(
                    Vector3.up,
                    impactDirection.normalized);

            float roll =
                side.x >= 0f ? 72f : -72f;

            deathTargetRotation =
                Quaternion.Euler(
                    transform.eulerAngles.x,
                    transform.eulerAngles.y,
                    roll);
        }
    }
}
