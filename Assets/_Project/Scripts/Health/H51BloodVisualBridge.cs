using System.Collections.Generic;
using Hanger51.NPCs;
using Rng.Blood;
using UnityEngine;

namespace Hanger51.Health
{
    [DisallowMultipleComponent]
    public sealed class H51BloodVisualBridge : MonoBehaviour
    {
        sealed class ActiveWound
        {
            public H51BodyRegion region;
            public Transform anchor;
            public float nextDripTime;
            public float nextArterialPulseTime;
        }

        readonly List<ActiveWound> activeWounds =
            new List<ActiveWound>();

        H51AnatomicalHealth health;
        H51BoZoCitizenVisual citizenVisual;
        BloodWounds bloodWounds;
        BloodSupply bloodSupply;

        Vector3 lastImpactDirection = Vector3.forward;
        BloodHitKind lastHitKind = BloodHitKind.Bullet;
        bool deathVisualSpawned;

        void Awake()
        {
            health = GetComponent<H51AnatomicalHealth>();
            citizenVisual = GetComponent<H51BoZoCitizenVisual>();

            bloodWounds = GetComponent<BloodWounds>();
            if (!bloodWounds)
                bloodWounds = gameObject.AddComponent<BloodWounds>();

            bloodSupply = GetComponent<BloodSupply>();
            if (!bloodSupply)
                bloodSupply = gameObject.AddComponent<BloodSupply>();

            if (health)
            {
                bloodSupply.Capacity = health.NormalBloodVolumeLiters;
                health.OnDeath += HandleDeath;
            }
        }

        void OnDestroy()
        {
            if (health)
                health.OnDeath -= HandleDeath;
        }

        void Update()
        {
            if (!health)
                return;

            if (!health.IsDead)
                deathVisualSpawned = false;

            float now = Time.time;

            for (int i=activeWounds.Count-1;i>=0;i--)
            {
                ActiveWound wound = activeWounds[i];

                if (wound == null || !wound.anchor)
                {
                    activeWounds.RemoveAt(i);
                    continue;
                }

                H51BodyRegionState state =
                    health.GetRegion(wound.region);

                if (state == null ||
                    state.externalBleedMlPerMinute <= .8f)
                {
                    Destroy(wound.anchor.gameObject);
                    activeWounds.RemoveAt(i);
                    continue;
                }

                float rate =
                    state.externalBleedMlPerMinute;

                if (now >= wound.nextDripTime)
                {
                    Blood.Drip(wound.anchor.position);

                    float normalized =
                        Mathf.Clamp01(rate/700f);

                    wound.nextDripTime =
                        now +
                        Mathf.Lerp(.90f,.18f,normalized);
                }

                if (state.arterialBleed &&
                    rate >= 220f &&
                    now >= wound.nextArterialPulseTime)
                {
                    Blood.Fountain(
                        wound.anchor,
                        Vector3.forward,
                        true,
                        Mathf.Lerp(.55f,1.35f,Mathf.Clamp01(rate/1200f)),
                        .14f,
                        Blood.Settings.Gout);

                    wound.nextArterialPulseTime =
                        now +
                        Mathf.Lerp(
                            2.6f,
                            1.35f,
                            Mathf.Clamp01(rate/1100f));
                }
            }
        }

        public void RegisterBallisticWound(
            H51BodyRegion region,
            Vector3 point,
            Vector3 normal,
            Vector3 direction,
            float trauma,
            BloodHitKind kind)
        {
            if (!health)
                return;

            lastImpactDirection =
                direction.sqrMagnitude > .001f
                    ? direction.normalized
                    : transform.forward;
            lastHitKind = kind;

            Transform bone =
                ResolveVisualBone(region);

            float intensity =
                Mathf.Clamp(
                    trauma/28f,
                    .45f,
                    1.65f);

            Blood.Spawn(
                point,
                normal,
                lastImpactDirection,
                intensity,
                kind,
                bone);

            GameObject anchorObject =
                new GameObject(
                    "H51 Blood Wound - " + region);

            Transform anchor =
                anchorObject.transform;
            anchor.SetParent(
                bone ? bone : transform,
                true);
            anchor.position = point;

            Vector3 outward =
                -lastImpactDirection;

            if (outward.sqrMagnitude < .001f)
                outward = normal.sqrMagnitude > .001f
                    ? normal
                    : transform.forward;

            anchor.rotation =
                Quaternion.LookRotation(
                    outward.normalized,
                    Vector3.up);

            ActiveWound wound =
                new ActiveWound
                {
                    region = region,
                    anchor = anchor,
                    nextDripTime = Time.time+.16f,
                    nextArterialPulseTime = Time.time+.08f
                };

            activeWounds.Add(wound);

            while (activeWounds.Count > 16)
            {
                ActiveWound oldest = activeWounds[0];
                if (oldest != null && oldest.anchor)
                    Destroy(oldest.anchor.gameObject);
                activeWounds.RemoveAt(0);
            }

            H51BodyRegionState state =
                health.GetRegion(region);

            if (state != null &&
                state.arterialBleed &&
                state.externalBleedMlPerMinute >= 220f)
            {
                Blood.Fountain(
                    anchor,
                    Vector3.forward,
                    true,
                    1.6f,
                    .12f,
                    Blood.Settings.Gout);

                wound.nextArterialPulseTime =
                    Time.time+1.4f;
            }
        }

        public void RegisterExplosion(
            Vector3 direction,
            float severity)
        {
            if (!health)
                return;

            lastImpactDirection =
                direction.sqrMagnitude > .001f
                    ? direction.normalized
                    : transform.forward;
            lastHitKind =
                BloodHitKind.Explosion;

            Blood.Spawn(
                transform.position,
                Vector3.up,
                lastImpactDirection,
                Mathf.Clamp(severity/55f,.65f,1.8f),
                BloodHitKind.Explosion,
                ResolveVisualBone(H51BodyRegion.Chest));
        }

        void HandleDeath(
            H51AnatomicalHealth _)
        {
            if (deathVisualSpawned ||
                !health)
                return;

            deathVisualSpawned = true;

            // BloodSupply reads the same H51AnatomicalHealth reservoir,
            // so let RNG Blood scale the death pool from the real remaining
            // blood once rather than multiplying the fraction a second time.
            Blood.Death(
                transform.position,
                lastImpactDirection,
                transform,
                lastHitKind,
                1f);
        }

        Transform ResolveVisualBone(
            H51BodyRegion region)
        {
            if (!citizenVisual)
                citizenVisual =
                    GetComponent<H51BoZoCitizenVisual>();

            Animator animator =
                citizenVisual
                    ? citizenVisual.VisualAnimator
                    : GetComponentInChildren<Animator>(true);

            if (!animator ||
                !animator.isHuman)
                return transform;

            HumanBodyBones primary;

            switch (region)
            {
                case H51BodyRegion.Head:
                    primary = HumanBodyBones.Head;
                    break;
                case H51BodyRegion.Neck:
                    primary = HumanBodyBones.Neck;
                    break;
                case H51BodyRegion.Chest:
                    primary = HumanBodyBones.Chest;
                    break;
                case H51BodyRegion.Abdomen:
                    primary = HumanBodyBones.Spine;
                    break;
                case H51BodyRegion.Pelvis:
                    primary = HumanBodyBones.Hips;
                    break;
                case H51BodyRegion.LeftArm:
                    primary = HumanBodyBones.LeftUpperArm;
                    break;
                case H51BodyRegion.RightArm:
                    primary = HumanBodyBones.RightUpperArm;
                    break;
                case H51BodyRegion.LeftLeg:
                    primary = HumanBodyBones.LeftUpperLeg;
                    break;
                case H51BodyRegion.RightLeg:
                    primary = HumanBodyBones.RightUpperLeg;
                    break;
                default:
                    primary = HumanBodyBones.Hips;
                    break;
            }

            Transform bone =
                animator.GetBoneTransform(primary);

            if (!bone &&
                region == H51BodyRegion.Chest)
                bone = animator.GetBoneTransform(HumanBodyBones.Spine);

            return bone ? bone : transform;
        }
    }
}
