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

        Quaternion deathTargetRotation;
        bool deathPoseRequested;

        void Awake()
        {
            health = GetComponent<H51AnatomicalHealth>();
            agent = GetComponent<NavMeshAgent>();
            citizenVisual = GetComponent<H51BoZoCitizenVisual>();
            animator = citizenVisual
                ? citizenVisual.VisualAnimator
                : GetComponentInChildren<Animator>(true);
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
                    projectileMomentumKgMps * .11f * regionMotion,
                    .025f,
                    .62f);

            reactionVelocity +=
                planar * velocityChange +
                Vector3.up *
                Mathf.Clamp(
                    trauma/650f,
                    0f,
                    .09f);

            reactionYaw +=
                Random.Range(-1f,1f) *
                Mathf.Clamp(
                    trauma*.16f,
                    1.5f,
                    12f);

            flinchRegion = region;
            flinch =
                Mathf.Max(
                    flinch,
                    Mathf.Clamp01(trauma/42f));

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

            if (health && health.IsDead)
                RequestDeathPose(planar);
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
                planar * Mathf.Clamp(severity*.012f,.08f,1.6f) +
                Vector3.up * Mathf.Clamp(severity*.006f,.02f,.55f);

            reactionYaw += Random.Range(-18f,18f);
            flinchRegion = H51BodyRegion.Chest;
            flinch = Mathf.Max(flinch,Mathf.Clamp01(severity/60f));
            staggerSeconds = Mathf.Max(staggerSeconds,.55f);

            if (health && health.IsDead)
                RequestDeathPose(planar);
        }

        void Update()
        {
            if (!health)
                health = GetComponent<H51AnatomicalHealth>();
            if (!agent)
                agent = GetComponent<NavMeshAgent>();
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

            if (health && health.IsDead)
            {
                if (!deathPoseRequested)
                    RequestDeathPose(transform.forward);

                transform.rotation =
                    Quaternion.Slerp(
                        transform.rotation,
                        deathTargetRotation,
                        1f-Mathf.Exp(-dt*3.6f));
            }
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
                            -18f*flinch,
                            7f*flinch,
                            -32f*flinch));
                    ApplyBone(
                        HumanBodyBones.LeftLowerArm,
                        new Vector3(
                            0f,
                            0f,
                            -28f*flinch));
                    ApplyBone(
                        HumanBodyBones.RightUpperArm,
                        new Vector3(
                            -8f*flinch,
                            0f,
                            10f*flinch));
                    break;

                case H51BodyRegion.RightArm:
                    ApplyBone(
                        HumanBodyBones.RightUpperArm,
                        new Vector3(
                            -18f*flinch,
                            -7f*flinch,
                            32f*flinch));
                    ApplyBone(
                        HumanBodyBones.RightLowerArm,
                        new Vector3(
                            0f,
                            0f,
                            28f*flinch));
                    ApplyBone(
                        HumanBodyBones.LeftUpperArm,
                        new Vector3(
                            -8f*flinch,
                            0f,
                            -10f*flinch));
                    break;

                case H51BodyRegion.LeftLeg:
                    ApplyBone(
                        HumanBodyBones.LeftUpperLeg,
                        new Vector3(18f*flinch,0f,-8f*flinch));
                    ApplyBone(
                        HumanBodyBones.Spine,
                        new Vector3(0f,0f,14f*flinch));
                    break;

                case H51BodyRegion.RightLeg:
                    ApplyBone(
                        HumanBodyBones.RightUpperLeg,
                        new Vector3(18f*flinch,0f,8f*flinch));
                    ApplyBone(
                        HumanBodyBones.Spine,
                        new Vector3(0f,0f,-14f*flinch));
                    break;

                case H51BodyRegion.Head:
                case H51BodyRegion.Neck:
                    ApplyBone(
                        HumanBodyBones.Neck,
                        new Vector3(
                            -12f*flinch,
                            side*12f*flinch,
                            side*16f*flinch));
                    break;

                default:
                    ApplyBone(
                        HumanBodyBones.Spine,
                        new Vector3(
                            -10f*flinch,
                            side*5f*flinch,
                            side*14f*flinch));
                    ApplyBone(
                        HumanBodyBones.Chest,
                        new Vector3(
                            -8f*flinch,
                            side*4f*flinch,
                            side*10f*flinch));
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

        void RequestDeathPose(
            Vector3 impactDirection)
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
