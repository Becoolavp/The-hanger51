using Hanger51.Health;
using Hanger51.NPCs;
using UnityEngine;
using UnityEngine.AI;

namespace Hanger51.Weapons
{
    [DisallowMultipleComponent]
    public sealed class H51BallisticImpactReaction : MonoBehaviour
    {
        Vector3 reactionVelocity;
        float reactionYaw;
        float flinch;
        float staggerSeconds;
        H51BodyRegion flinchRegion;
        Vector3 localImpactDirection;

        H51AnatomicalHealth health;
        NavMeshAgent agent;
        H51BoZoCitizenVisual citizenVisual;
        Animator animator;
        H51CitizenRagdoll ragdoll;

        void Awake()
        {
            health = GetComponent<H51AnatomicalHealth>();
            agent = GetComponent<NavMeshAgent>();
            citizenVisual = GetComponent<H51BoZoCitizenVisual>();
            animator = citizenVisual
                ? citizenVisual.VisualAnimator
                : GetComponentInChildren<Animator>(true);
            ragdoll = GetComponent<H51CitizenRagdoll>();
        }

        public void ApplyProjectileImpact(
            H51BodyRegion region,
            Vector3 direction,
            float projectileMomentumKgMps,
            float trauma)
        {
            Vector3 planar = direction;
            planar.y = 0f;
            if (planar.sqrMagnitude < .001f)
                planar = transform.forward;
            planar.Normalize();

            float regionMotion =
                region == H51BodyRegion.LeftArm ||
                region == H51BodyRegion.RightArm
                    ? .55f
                    : region == H51BodyRegion.LeftLeg ||
                      region == H51BodyRegion.RightLeg
                        ? .72f
                        : region == H51BodyRegion.Head ||
                          region == H51BodyRegion.Neck
                            ? .82f
                            : .68f;

            float velocityChange =
                Mathf.Clamp(
                    projectileMomentumKgMps * .19f * regionMotion,
                    .045f,
                    .88f);

            reactionVelocity +=
                planar * velocityChange +
                Vector3.up *
                Mathf.Clamp(
                    trauma/430f,
                    0f,
                    .15f);

            reactionYaw +=
                Random.Range(-1f,1f) *
                Mathf.Clamp(
                    trauma*.28f,
                    2.5f,
                    20f);

            if (!ragdoll)
                ragdoll = GetComponent<H51CitizenRagdoll>();
            if (!ragdoll)
                ragdoll = gameObject.AddComponent<H51CitizenRagdoll>();

            ragdoll.RegisterImpact(
                region,
                direction,
                projectileMomentumKgMps,
                trauma);

            flinchRegion = region;
            flinch =
                Mathf.Max(
                    flinch,
                    Mathf.Clamp01(trauma/26f));

            localImpactDirection =
                transform.InverseTransformDirection(direction.normalized);

            staggerSeconds =
                Mathf.Max(
                    staggerSeconds,
                    region == H51BodyRegion.LeftLeg ||
                    region == H51BodyRegion.RightLeg
                        ? Mathf.Lerp(.30f,1.05f,flinch)
                        : region == H51BodyRegion.Head ||
                          region == H51BodyRegion.Neck
                            ? Mathf.Lerp(.20f,.72f,flinch)
                            : Mathf.Lerp(.08f,.35f,flinch));

        }

        public void ApplyProjectileImpact(
            Vector3 direction,
            float projectileMomentumKgMps,
            float trauma)
        {
            ApplyProjectileImpact(
                H51BodyRegion.Chest,
                direction,
                projectileMomentumKgMps,
                trauma);
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
                planar * Mathf.Clamp(severity*.018f,.12f,2.0f) +
                Vector3.up * Mathf.Clamp(severity*.008f,.03f,.68f);

            reactionYaw += Random.Range(-18f,18f);
            flinchRegion = H51BodyRegion.Chest;
            flinch = Mathf.Max(flinch,Mathf.Clamp01(severity/42f));
            staggerSeconds = Mathf.Max(staggerSeconds,.55f);

        }

        void Update()
        {
            if (!health)
                health = GetComponent<H51AnatomicalHealth>();
            if (!agent)
                agent = GetComponent<NavMeshAgent>();
            if (!ragdoll)
                ragdoll = GetComponent<H51CitizenRagdoll>();
            if (!animator)
            {
                if (!citizenVisual)
                    citizenVisual = GetComponent<H51BoZoCitizenVisual>();
                animator = citizenVisual
                    ? citizenVisual.VisualAnimator
                    : GetComponentInChildren<Animator>(true);
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            if (ragdoll && ragdoll.IsRagdolled)
            {
                reactionVelocity = Vector3.zero;
                reactionYaw = 0f;
                flinch = 0f;
                staggerSeconds = 0f;
                return;
            }

            staggerSeconds = Mathf.Max(0f,staggerSeconds-dt);

            if (reactionVelocity.sqrMagnitude > .0001f)
            {
                Vector3 delta = reactionVelocity*dt;

                if (agent &&
                    agent.enabled &&
                    agent.isOnNavMesh &&
                    (!health || !health.IsDead))
                    agent.Move(delta);
                else
                    transform.position += delta;

                reactionVelocity =
                    Vector3.MoveTowards(
                        reactionVelocity,
                        Vector3.zero,
                        dt*3.8f);
            }

            if (Mathf.Abs(reactionYaw) > .01f &&
                (!health || !health.IsDead))
            {
                transform.Rotate(
                    0f,
                    reactionYaw*dt*5f,
                    0f,
                    Space.World);

                reactionYaw =
                    Mathf.MoveTowards(
                        reactionYaw,
                        0f,
                        dt*20f);
            }

            flinch =
                Mathf.MoveTowards(
                    flinch,
                    0f,
                    dt*1.55f);

        }

        void LateUpdate()
        {
            if (!animator ||
                flinch <= .001f ||
                (health && health.IsDead) ||
                !animator.isHuman)
                return;

            float side =
                Mathf.Clamp(
                    localImpactDirection.x,
                    -1f,
                    1f);

            switch (flinchRegion)
            {
                case H51BodyRegion.LeftArm:
                    ApplyBone(
                        HumanBodyBones.LeftUpperArm,
                        new Vector3(
                            -30f*flinch,
                            12f*flinch,
                            -48f*flinch));
                    ApplyBone(
                        HumanBodyBones.LeftLowerArm,
                        new Vector3(
                            0f,
                            0f,
                            -42f*flinch));
                    ApplyBone(
                        HumanBodyBones.RightUpperArm,
                        new Vector3(
                            -14f*flinch,
                            0f,
                            18f*flinch));
                    break;

                case H51BodyRegion.RightArm:
                    ApplyBone(
                        HumanBodyBones.RightUpperArm,
                        new Vector3(
                            -30f*flinch,
                            -12f*flinch,
                            48f*flinch));
                    ApplyBone(
                        HumanBodyBones.RightLowerArm,
                        new Vector3(
                            0f,
                            0f,
                            42f*flinch));
                    ApplyBone(
                        HumanBodyBones.LeftUpperArm,
                        new Vector3(
                            -14f*flinch,
                            0f,
                            -18f*flinch));
                    break;

                case H51BodyRegion.LeftLeg:
                    ApplyBone(
                        HumanBodyBones.LeftUpperLeg,
                        new Vector3(30f*flinch,0f,-14f*flinch));
                    ApplyBone(
                        HumanBodyBones.Spine,
                        new Vector3(0f,0f,24f*flinch));
                    break;

                case H51BodyRegion.RightLeg:
                    ApplyBone(
                        HumanBodyBones.RightUpperLeg,
                        new Vector3(30f*flinch,0f,14f*flinch));
                    ApplyBone(
                        HumanBodyBones.Spine,
                        new Vector3(0f,0f,-24f*flinch));
                    break;

                case H51BodyRegion.Head:
                case H51BodyRegion.Neck:
                    ApplyBone(
                        HumanBodyBones.Neck,
                        new Vector3(
                            -20f*flinch,
                            side*20f*flinch,
                            side*28f*flinch));
                    break;

                default:
                    ApplyBone(
                        HumanBodyBones.Spine,
                        new Vector3(
                            -16f*flinch,
                            side*9f*flinch,
                            side*24f*flinch));
                    ApplyBone(
                        HumanBodyBones.Chest,
                        new Vector3(
                            -14f*flinch,
                            side*8f*flinch,
                            side*18f*flinch));
                    break;
            }
        }

        void ApplyBone(
            HumanBodyBones bone,
            Vector3 euler)
        {
            Transform target =
                animator.GetBoneTransform(bone);

            if (!target)
                return;

            target.localRotation =
                target.localRotation *
                Quaternion.Euler(euler);
        }

    }
}
