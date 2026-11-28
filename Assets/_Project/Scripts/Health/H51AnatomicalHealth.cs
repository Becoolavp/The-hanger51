using System;
using System.Collections.Generic;
using Hanger51.NPCs;
using Rng.Blood;
using UnityEngine;
using UnityEngine.AI;

namespace Hanger51.Health
{
    public enum H51DamageType
    {
        Blunt,
        Penetrating,
        Ballistic,
        Burn,
        Explosion,
        Crush,
        Fall
    }

    public enum H51BodyRegion
    {
        Head,
        Neck,
        Chest,
        Abdomen,
        Pelvis,
        LeftArm,
        RightArm,
        LeftLeg,
        RightLeg
    }

    public enum H51OrganType
    {
        Brain,
        Heart,
        LeftLung,
        RightLung,
        Liver,
        Stomach,
        Intestines,
        LeftKidney,
        RightKidney,
        Spine
    }

    [Serializable]
    public sealed class H51BodyRegionState
    {
        public H51BodyRegion region;
        public string displayName;
        [Range(0f,100f)] public float condition = 100f;
        [Min(0f)] public float externalBleedMlPerMinute;
        public bool arterialBleed;
        public bool fractured;
        public bool disabled;
    }

    [Serializable]
    public sealed class H51OrganState
    {
        public H51OrganType organ;
        public string displayName;
        [Range(0f,100f)] public float condition = 100f;
        [Min(0f)] public float internalBleedMlPerMinute;
        public bool critical;
    }

    [DisallowMultipleComponent]
    public sealed class H51AnatomicalHealth : MonoBehaviour, IBloodReservoir
    {
        [Header("Circulation")]
        [SerializeField, Min(.1f)] float normalBloodVolumeLiters = 5.2f;
        [SerializeField, Min(0f)] float bloodVolumeLiters = 5.2f;
        [SerializeField, Range(0f,1f)] float oxygenSaturation = .985f;
        [SerializeField, Range(0f,220f)] float pulseBpm = 72f;
        [SerializeField, Range(0f,180f)] float systolicPressure = 120f;

        [Header("Whole-body response")]
        [SerializeField, Range(0f,100f)] float pain;
        [SerializeField, Range(0f,100f)] float shock;
        [SerializeField, Range(0f,100f)] float adrenaline;
        [SerializeField, Range(0f,100f)] float consciousness = 100f;
        [SerializeField] bool dead;
        [SerializeField] string causeOfDeath;

        [Header("Anatomy")]
        [SerializeField] List<H51BodyRegionState> regions =
            new List<H51BodyRegionState>();
        [SerializeField] List<H51OrganState> organs =
            new List<H51OrganState>();

        float lowOxygenSeconds;
        float breathingSupportSeconds;
        float shockSupportSeconds;
        bool emergencyReported;

        H51CitizenEmergencyState citizenEmergency;
        H51CitizenBrain citizenBrain;
        NavMeshAgent navAgent;
        Animator animator;

        public event Action<H51AnatomicalHealth> OnChanged;
        public event Action<H51AnatomicalHealth> OnDeath;

        public float NormalBloodVolumeLiters => normalBloodVolumeLiters;
        public float BloodVolumeLiters => bloodVolumeLiters;
        public float BloodFraction => normalBloodVolumeLiters <= .01f
            ? 0f
            : Mathf.Clamp01(bloodVolumeLiters / normalBloodVolumeLiters);
        public float BloodLeft => BloodFraction;
        public float OxygenSaturation => oxygenSaturation;
        public float PulseBpm => pulseBpm;
        public float SystolicPressure => systolicPressure;
        public float Pain => pain;
        public float Shock => shock;
        public float Adrenaline => adrenaline;
        public float Consciousness => consciousness;
        public bool IsDead => dead;
        public string CauseOfDeath => causeOfDeath;
        public IReadOnlyList<H51BodyRegionState> Regions
        {
            get
            {
                EnsureAnatomy();
                return regions;
            }
        }

        public IReadOnlyList<H51OrganState> Organs
        {
            get
            {
                EnsureAnatomy();
                return organs;
            }
        }

        public float ExternalBleedMlPerMinute
        {
            get
            {
                float total = 0f;
                for (int i=0;i<regions.Count;i++)
                    total += Mathf.Max(0f,regions[i].externalBleedMlPerMinute);
                return total;
            }
        }

        public float InternalBleedMlPerMinute
        {
            get
            {
                float total = 0f;
                for (int i=0;i<organs.Count;i++)
                    total += Mathf.Max(0f,organs[i].internalBleedMlPerMinute);
                return total;
            }
        }

        public float TotalBleedMlPerMinute =>
            ExternalBleedMlPerMinute +
            InternalBleedMlPerMinute;

        public string OverallStatus
        {
            get
            {
                if (dead)
                    return "DECEASED";
                if (consciousness < 15f)
                    return "UNCONSCIOUS / CRITICAL";
                if (shock >= 70f || BloodFraction < .55f)
                    return "CRITICAL";
                if (shock >= 40f || TotalBleedMlPerMinute >= 300f)
                    return "SERIOUS";
                if (pain >= 30f || TotalBleedMlPerMinute > 1f)
                    return "INJURED";
                return "FULLY FUNCTIONAL";
            }
        }

        void Reset()
        {
            EnsureAnatomy();
            ResetHealthy();
        }

        void Awake()
        {
            EnsureAnatomy();

            if (bloodVolumeLiters <= .01f && !dead)
                bloodVolumeLiters = normalBloodVolumeLiters;

            citizenEmergency =
                GetComponent<H51CitizenEmergencyState>();
            citizenBrain =
                GetComponent<H51CitizenBrain>();
            navAgent =
                GetComponent<NavMeshAgent>();
            animator =
                GetComponentInChildren<Animator>(true);
        }

        void OnValidate()
        {
            normalBloodVolumeLiters =
                Mathf.Max(.1f,normalBloodVolumeLiters);
            bloodVolumeLiters =
                Mathf.Clamp(
                    bloodVolumeLiters,
                    0f,
                    normalBloodVolumeLiters);

            EnsureAnatomy();
        }

        void Update()
        {
            if (dead)
                return;

            float dt =
                Time.deltaTime;
            if (dt <= 0f)
                return;

            SimulateBleeding(dt);
            SimulateVitals(dt);
            EvaluateCriticalState();
            ReportNpcEmergencyIfNeeded();
        }

        public H51BodyRegionState GetRegion(
            H51BodyRegion region)
        {
            EnsureAnatomy();
            for (int i=0;i<regions.Count;i++)
                if (regions[i].region == region)
                    return regions[i];
            return null;
        }

        public H51OrganState GetOrgan(
            H51OrganType organ)
        {
            EnsureAnatomy();
            for (int i=0;i<organs.Count;i++)
                if (organs[i].organ == organ)
                    return organs[i];
            return null;
        }

        public void ApplyDamage(
            Vector3 worldPoint,
            float amount,
            H51DamageType type,
            Vector3 direction,
            float penetration = 0f)
        {
            if (dead || amount <= 0f)
                return;

            Vector3 local =
                transform.InverseTransformPoint(worldPoint);

            H51BodyRegion region =
                EstimateRegion(local);

            ApplyDamageInternal(
                region,
                local,
                amount,
                type,
                Mathf.Clamp01(penetration),
                direction);
        }

        public void ApplyDamage(
            H51BodyRegion region,
            Vector3 worldPoint,
            float amount,
            H51DamageType type,
            Vector3 direction,
            float penetration = 0f)
        {
            if (dead || amount <= 0f)
                return;

            Vector3 local =
                transform.InverseTransformPoint(worldPoint);

            ApplyDamageInternal(
                region,
                local,
                amount,
                type,
                Mathf.Clamp01(penetration),
                direction);
        }

        public void ApplyDamage(
            H51BodyRegion region,
            H51OrganType organ,
            Vector3 worldPoint,
            float amount,
            H51DamageType type,
            Vector3 direction,
            float penetration = 0f)
        {
            if (dead || amount <= 0f)
                return;

            Vector3 local =
                transform.InverseTransformPoint(worldPoint);

            ApplyDamageInternal(
                region,
                local,
                amount,
                type,
                Mathf.Clamp01(penetration),
                direction,
                organ);
        }

        public void ApplyOrganImpact(
            H51OrganType organ,
            float amount,
            H51DamageType type,
            float penetration = 0f)
        {
            if (dead || amount <= 0f)
                return;

            ApplyDirectOrganDamage(
                organ,
                amount,
                type,
                Mathf.Clamp01(penetration));

            adrenaline =
                Mathf.Max(
                    adrenaline,
                    Mathf.Clamp(
                        amount * 1.15f,
                        0f,
                        100f));

            OnChanged?.Invoke(this);
            EvaluateCriticalState();
            ReportNpcEmergencyIfNeeded();
        }

        public void ApplyDamage(
            H51BodyRegion region,
            float amount,
            H51DamageType type,
            float penetration = 0f)
        {
            if (dead || amount <= 0f)
                return;

            Vector3 local =
                RepresentativeLocalPoint(region);

            ApplyDamageInternal(
                region,
                local,
                amount,
                type,
                Mathf.Clamp01(penetration),
                Vector3.forward);
        }

        public void StabilizeRegion(
            H51BodyRegion region,
            float effectiveness = .85f)
        {
            H51BodyRegionState state =
                GetRegion(region);
            if (state == null)
                return;

            state.externalBleedMlPerMinute *=
                1f - Mathf.Clamp01(effectiveness);

            if (state.externalBleedMlPerMinute < 35f)
                state.arterialBleed = false;

            OnChanged?.Invoke(this);
        }

        public void StabilizeAllBleeding(
            float effectiveness = .85f)
        {
            float remain =
                1f - Mathf.Clamp01(effectiveness);

            for (int i=0;i<regions.Count;i++)
            {
                regions[i].externalBleedMlPerMinute *= remain;
                if (regions[i].externalBleedMlPerMinute < 35f)
                    regions[i].arterialBleed = false;
            }

            for (int i=0;i<organs.Count;i++)
                organs[i].internalBleedMlPerMinute *=
                    Mathf.Lerp(1f,remain,.38f);

            OnChanged?.Invoke(this);
        }

        public H51BodyRegion FindWorstBleedingLimb()
        {
            H51BodyRegion[] limbs =
            {
                H51BodyRegion.LeftArm,
                H51BodyRegion.RightArm,
                H51BodyRegion.LeftLeg,
                H51BodyRegion.RightLeg
            };

            H51BodyRegion best = H51BodyRegion.LeftArm;
            float highest = 0f;

            for (int i=0;i<limbs.Length;i++)
            {
                H51BodyRegionState state = GetRegion(limbs[i]);
                if (state != null &&
                    state.externalBleedMlPerMinute > highest)
                {
                    highest = state.externalBleedMlPerMinute;
                    best = limbs[i];
                }
            }

            return best;
        }

        public float WorstLimbBleedMlPerMinute
        {
            get
            {
                H51BodyRegionState state =
                    GetRegion(FindWorstBleedingLimb());
                return state != null
                    ? state.externalBleedMlPerMinute
                    : 0f;
            }
        }

        public void ApplyTraumaDressing()
        {
            for (int i=0;i<regions.Count;i++)
            {
                H51BodyRegionState state = regions[i];
                if (state == null)
                    continue;

                state.externalBleedMlPerMinute *= .24f;
                if (state.externalBleedMlPerMinute < 35f)
                    state.arterialBleed = false;
            }

            pain = Mathf.Max(0f,pain-4f);
            OnChanged?.Invoke(this);
        }

        public void ApplyTourniquetToWorstLimb()
        {
            H51BodyRegion region = FindWorstBleedingLimb();
            H51BodyRegionState state = GetRegion(region);
            if (state == null ||
                state.externalBleedMlPerMinute <= .01f)
                return;

            state.externalBleedMlPerMinute *= .025f;
            state.arterialBleed = false;
            pain = Mathf.Clamp(pain+6f,0f,100f);
            OnChanged?.Invoke(this);
        }

        public void ApplyChestSealAndBreathingSupport()
        {
            H51BodyRegionState chest =
                GetRegion(H51BodyRegion.Chest);
            if (chest != null)
                chest.externalBleedMlPerMinute *= .12f;

            breathingSupportSeconds =
                Mathf.Max(breathingSupportSeconds,120f);
            lowOxygenSeconds =
                Mathf.Max(0f,lowOxygenSeconds-8f);
            OnChanged?.Invoke(this);
        }

        public void ProvideAirwaySupport()
        {
            breathingSupportSeconds =
                Mathf.Max(breathingSupportSeconds,90f);
            oxygenSaturation =
                Mathf.Clamp01(oxygenSaturation+.04f);
            lowOxygenSeconds =
                Mathf.Max(0f,lowOxygenSeconds-10f);
            OnChanged?.Invoke(this);
        }

        public void ProvideShockSupport()
        {
            shockSupportSeconds =
                Mathf.Max(shockSupportSeconds,120f);
            shock =
                Mathf.Max(0f,shock-8f);
            pain =
                Mathf.Max(0f,pain-3f);
            OnChanged?.Invoke(this);
        }

        public void ResetHealthy()
        {
            EnsureAnatomy();

            bloodVolumeLiters =
                normalBloodVolumeLiters;
            oxygenSaturation =
                .985f;
            pulseBpm =
                72f;
            systolicPressure =
                120f;
            pain =
                0f;
            shock =
                0f;
            adrenaline =
                0f;
            consciousness =
                100f;
            dead =
                false;
            causeOfDeath =
                string.Empty;
            lowOxygenSeconds =
                0f;
            breathingSupportSeconds =
                0f;
            shockSupportSeconds =
                0f;
            emergencyReported =
                false;

            for (int i=0;i<regions.Count;i++)
            {
                regions[i].condition = 100f;
                regions[i].externalBleedMlPerMinute = 0f;
                regions[i].arterialBleed = false;
                regions[i].fractured = false;
                regions[i].disabled = false;
            }

            for (int i=0;i<organs.Count;i++)
            {
                organs[i].condition = 100f;
                organs[i].internalBleedMlPerMinute = 0f;
                organs[i].critical = false;
            }

            if (animator)
                animator.speed = 1f;

            if (citizenBrain && !citizenBrain.enabled)
                citizenBrain.enabled = true;

            OnChanged?.Invoke(this);
        }

        void ApplyDamageInternal(
            H51BodyRegion region,
            Vector3 localPoint,
            float amount,
            H51DamageType type,
            float penetration,
            Vector3 direction,
            H51OrganType? forcedOrgan = null)
        {
            H51BodyRegionState body =
                GetRegion(region);
            if (body == null)
                return;

            float tissueMultiplier =
                DamageMultiplier(type);

            float tissueDamage =
                amount *
                tissueMultiplier;

            body.condition =
                Mathf.Clamp(
                    body.condition - tissueDamage,
                    0f,
                    100f);

            float woundFactor =
                WoundFactor(type);

            float vascularity =
                RegionVascularity(region);

            float newExternalBleed =
                amount *
                woundFactor *
                vascularity *
                Mathf.Lerp(.55f,1.45f,penetration);

            bool arterialHit =
                IsArterialHit(
                    region,
                    localPoint,
                    amount,
                    type,
                    penetration);

            if (arterialHit)
            {
                body.arterialBleed = true;
                newExternalBleed *=
                    region == H51BodyRegion.Neck ||
                    region == H51BodyRegion.Pelvis
                        ? 2.8f
                        : 3.6f;
            }

            body.externalBleedMlPerMinute +=
                newExternalBleed;

            if (type == H51DamageType.Blunt ||
                type == H51DamageType.Crush ||
                type == H51DamageType.Fall ||
                type == H51DamageType.Explosion)
            {
                float fractureThreshold =
                    IsLimb(region) ? 32f : 48f;

                if (amount >= fractureThreshold)
                    body.fractured = true;
            }

            if (body.condition <= 18f &&
                IsLimb(region))
                body.disabled = true;

            pain =
                Mathf.Clamp(
                    pain +
                    amount *
                    PainMultiplier(type) *
                    RegionPainMultiplier(region),
                    0f,
                    100f);

            ApplyOrganDamage(
                region,
                localPoint,
                amount,
                type,
                penetration,
                forcedOrgan);

            adrenaline =
                Mathf.Max(
                    adrenaline,
                    Mathf.Clamp(
                        amount * 1.35f,
                        0f,
                        100f));

            OnChanged?.Invoke(this);
            EvaluateCriticalState();
            ReportNpcEmergencyIfNeeded();
        }

        void ApplyOrganDamage(
            H51BodyRegion region,
            Vector3 local,
            float amount,
            H51DamageType type,
            float penetration,
            H51OrganType? forcedOrgan)
        {
            if (forcedOrgan.HasValue)
            {
                ApplyDirectOrganDamage(
                    forcedOrgan.Value,
                    amount,
                    type,
                    penetration);
                return;
            }

            // Ballistic and penetrating projectiles now need to actually
            // intersect one of the invisible organ volumes. A broad body-region
            // hit by itself represents soft tissue, bone and surface vessels;
            // it no longer means the bullet automatically hit the brain/heart/lung.
            if (type == H51DamageType.Ballistic ||
                type == H51DamageType.Penetrating)
                return;

            float organScale =
                Mathf.Lerp(
                    .28f,
                    1.05f,
                    Mathf.Max(
                        penetration,
                        type == H51DamageType.Ballistic ? .85f : 0f));

            if (type == H51DamageType.Blunt ||
                type == H51DamageType.Crush ||
                type == H51DamageType.Fall ||
                type == H51DamageType.Explosion)
                organScale =
                    Mathf.Max(
                        organScale,
                        .32f);

            if (region == H51BodyRegion.Head)
            {
                DamageOrgan(
                    H51OrganType.Brain,
                    amount * organScale,
                    InternalBleedFor(type,amount,.50f));
                return;
            }

            if (region == H51BodyRegion.Neck)
            {
                DamageOrgan(
                    H51OrganType.Spine,
                    amount * organScale * .58f,
                    InternalBleedFor(type,amount,.18f));
                return;
            }

            if (region == H51BodyRegion.Chest)
            {
                float x =
                    local.x;

                if (Mathf.Abs(x) < .16f)
                {
                    DamageOrgan(
                        H51OrganType.Heart,
                        amount * organScale * .85f,
                        InternalBleedFor(type,amount,1.2f));
                }

                H51OrganType lung =
                    x < 0f
                        ? H51OrganType.LeftLung
                        : H51OrganType.RightLung;

                DamageOrgan(
                    lung,
                    amount * organScale,
                    InternalBleedFor(type,amount,.48f));

                if (type == H51DamageType.Explosion ||
                    penetration > .72f)
                {
                    H51OrganType other =
                        lung == H51OrganType.LeftLung
                            ? H51OrganType.RightLung
                            : H51OrganType.LeftLung;

                    DamageOrgan(
                        other,
                        amount * organScale * .42f,
                        InternalBleedFor(type,amount,.20f));
                }

                DamageOrgan(
                    H51OrganType.Spine,
                    amount * organScale * .16f,
                    InternalBleedFor(type,amount,.05f));
                return;
            }

            if (region == H51BodyRegion.Abdomen ||
                region == H51BodyRegion.Pelvis)
            {
                float x =
                    local.x;
                float z =
                    local.z;

                if (region == H51BodyRegion.Pelvis)
                {
                    H51OrganType kidney =
                        x < 0f
                            ? H51OrganType.LeftKidney
                            : H51OrganType.RightKidney;

                    DamageOrgan(
                        kidney,
                        amount * organScale * .62f,
                        InternalBleedFor(type,amount,.42f));
                }
                else if (x > .10f)
                {
                    DamageOrgan(
                        H51OrganType.Liver,
                        amount * organScale,
                        InternalBleedFor(type,amount,.95f));
                }
                else if (z > .04f)
                {
                    DamageOrgan(
                        H51OrganType.Stomach,
                        amount * organScale * .78f,
                        InternalBleedFor(type,amount,.30f));
                }
                else
                {
                    DamageOrgan(
                        H51OrganType.Intestines,
                        amount * organScale * .82f,
                        InternalBleedFor(type,amount,.38f));
                }

                DamageOrgan(
                    H51OrganType.Spine,
                    amount * organScale * .12f,
                    InternalBleedFor(type,amount,.04f));
            }
        }

        void ApplyDirectOrganDamage(
            H51OrganType organType,
            float amount,
            H51DamageType type,
            float penetration)
        {
            float damage =
                Mathf.Max(
                    amount,
                    0f);

            float bleedScale = 1f;

            switch (organType)
            {
                case H51OrganType.Brain:
                    if (type == H51DamageType.Ballistic ||
                        type == H51DamageType.Penetrating)
                    {
                        damage =
                            Mathf.Max(
                                amount*7.5f,
                                Mathf.Lerp(
                                    82f,
                                    100f,
                                    Mathf.Clamp01(
                                        penetration+.15f)));

                        bleedScale = 1.35f;
                    }
                    else
                    {
                        damage *= 1.8f;
                        bleedScale = .75f;
                    }
                    break;

                case H51OrganType.Heart:
                    damage *=
                        type == H51DamageType.Ballistic ||
                        type == H51DamageType.Penetrating
                            ? 5.2f
                            : 1.6f;
                    bleedScale = 2.4f;
                    break;

                case H51OrganType.LeftLung:
                case H51OrganType.RightLung:
                    damage *=
                        type == H51DamageType.Ballistic ||
                        type == H51DamageType.Penetrating
                            ? 3.2f
                            : 1.35f;
                    bleedScale = 1.35f;
                    break;

                case H51OrganType.Liver:
                    damage *=
                        type == H51DamageType.Ballistic ||
                        type == H51DamageType.Penetrating
                            ? 2.8f
                            : 1.35f;
                    bleedScale = 2.0f;
                    break;

                case H51OrganType.LeftKidney:
                case H51OrganType.RightKidney:
                    damage *=
                        type == H51DamageType.Ballistic ||
                        type == H51DamageType.Penetrating
                            ? 2.5f
                            : 1.25f;
                    bleedScale = 1.45f;
                    break;

                case H51OrganType.Spine:
                    damage *=
                        type == H51DamageType.Ballistic ||
                        type == H51DamageType.Penetrating
                            ? 4.0f
                            : 1.8f;
                    bleedScale = .65f;
                    break;

                case H51OrganType.Stomach:
                case H51OrganType.Intestines:
                    damage *=
                        type == H51DamageType.Ballistic ||
                        type == H51DamageType.Penetrating
                            ? 2.3f
                            : 1.15f;
                    bleedScale = .85f;
                    break;
            }

            DamageOrgan(
                organType,
                damage,
                InternalBleedFor(
                    type,
                    amount,
                    bleedScale));

            H51OrganState organ =
                GetOrgan(organType);

            if (organType == H51OrganType.Brain &&
                (type == H51DamageType.Ballistic ||
                 type == H51DamageType.Penetrating) &&
                organ != null &&
                organ.condition <= 20f)
            {
                Die("catastrophic brain injury");
                return;
            }

            if (organType == H51OrganType.Heart &&
                (type == H51DamageType.Ballistic ||
                 type == H51DamageType.Penetrating) &&
                organ != null &&
                organ.condition <= 3f)
            {
                Die("catastrophic cardiac injury");
            }
        }

        void DamageOrgan(
            H51OrganType type,
            float amount,
            float internalBleed)
        {
            if (amount <= .01f)
                return;

            H51OrganState organ =
                GetOrgan(type);
            if (organ == null)
                return;

            organ.condition =
                Mathf.Clamp(
                    organ.condition - amount,
                    0f,
                    100f);

            organ.internalBleedMlPerMinute +=
                Mathf.Max(
                    0f,
                    internalBleed);

            organ.critical =
                organ.condition <= 25f;
        }

        void SimulateBleeding(
            float dt)
        {
            float mlPerMinute =
                TotalBleedMlPerMinute;

            if (mlPerMinute <= .01f)
                return;

            bloodVolumeLiters =
                Mathf.Max(
                    0f,
                    bloodVolumeLiters -
                    mlPerMinute /
                    1000f /
                    60f *
                    dt);
        }

        void SimulateVitals(
            float dt)
        {
            float blood =
                BloodFraction;

            float heart =
                OrganFraction(
                    H51OrganType.Heart);

            float leftLung =
                OrganFraction(
                    H51OrganType.LeftLung);
            float rightLung =
                OrganFraction(
                    H51OrganType.RightLung);
            float lungs =
                (leftLung + rightLung) * .5f;

            float brain =
                OrganFraction(
                    H51OrganType.Brain);

            float targetOxygen =
                Mathf.Clamp01(
                    .40f +
                    lungs * .48f +
                    heart * .08f);

            if (breathingSupportSeconds > 0f)
            {
                breathingSupportSeconds =
                    Mathf.Max(0f,breathingSupportSeconds-dt);
                targetOxygen =
                    Mathf.Max(
                        targetOxygen,
                        Mathf.Lerp(.78f,.96f,lungs));
            }

            targetOxygen *=
                Mathf.Lerp(.58f,1f,blood);

            oxygenSaturation =
                Mathf.MoveTowards(
                    oxygenSaturation,
                    targetOxygen,
                    dt *
                    (targetOxygen < oxygenSaturation ? .09f : .025f));

            float hemorrhageShock =
                Mathf.InverseLerp(
                    1f,
                    .42f,
                    blood);

            float organShock =
                1f -
                Mathf.Min(
                    heart,
                    Mathf.Min(
                        lungs,
                        brain));

            float painShock =
                pain / 100f *
                .42f;

            float targetShock =
                Mathf.Clamp01(
                    Mathf.Max(
                        hemorrhageShock,
                        organShock * .88f) +
                    painShock) *
                100f;

            if (shockSupportSeconds > 0f)
            {
                shockSupportSeconds =
                    Mathf.Max(0f,shockSupportSeconds-dt);
                targetShock *= .82f;
            }

            shock =
                Mathf.MoveTowards(
                    shock,
                    targetShock,
                    dt *
                    (targetShock > shock ? 18f : 4f));

            float targetAdrenaline =
                Mathf.Clamp(
                    shock * .88f +
                    pain * .35f,
                    0f,
                    100f);

            adrenaline =
                Mathf.MoveTowards(
                    adrenaline,
                    targetAdrenaline,
                    dt * 7f);

            float targetPulse =
                Mathf.Clamp(
                    68f +
                    shock * .82f +
                    adrenaline * .26f,
                    35f,
                    190f);

            if (heart < .35f ||
                blood < .35f)
                targetPulse *=
                    Mathf.Lerp(
                        .40f,
                        1f,
                        Mathf.Min(
                            heart / .35f,
                            blood / .35f));

            pulseBpm =
                Mathf.MoveTowards(
                    pulseBpm,
                    targetPulse,
                    dt * 24f);

            systolicPressure =
                Mathf.Clamp(
                    122f *
                    Mathf.Lerp(.26f,1f,blood) *
                    Mathf.Lerp(.30f,1f,heart) +
                    adrenaline * .12f,
                    0f,
                    180f);

            float targetConsciousness =
                100f;

            targetConsciousness *=
                Mathf.Lerp(.05f,1f,brain);
            targetConsciousness *=
                Mathf.InverseLerp(.40f,.92f,oxygenSaturation);
            targetConsciousness *=
                Mathf.Lerp(.08f,1f,blood);
            targetConsciousness *=
                Mathf.Lerp(.28f,1f,1f-shock/100f);

            consciousness =
                Mathf.MoveTowards(
                    consciousness,
                    Mathf.Clamp(targetConsciousness,0f,100f),
                    dt *
                    (targetConsciousness < consciousness ? 20f : 6f));

            pain =
                Mathf.MoveTowards(
                    pain,
                    Mathf.Max(
                        0f,
                        InjuryPainFloor()),
                    dt * .45f);

            if (oxygenSaturation < .55f)
                lowOxygenSeconds += dt;
            else
                lowOxygenSeconds =
                    Mathf.Max(
                        0f,
                        lowOxygenSeconds - dt * 2f);

            OnChanged?.Invoke(this);
        }

        float InjuryPainFloor()
        {
            float worst =
                100f;

            for (int i=0;i<regions.Count;i++)
                worst =
                    Mathf.Min(
                        worst,
                        regions[i].condition);

            return
                Mathf.Clamp(
                    (100f-worst) * .45f,
                    0f,
                    75f);
        }

        void EvaluateCriticalState()
        {
            if (dead)
                return;

            H51OrganState brain =
                GetOrgan(H51OrganType.Brain);
            H51OrganState heart =
                GetOrgan(H51OrganType.Heart);

            if (brain != null &&
                brain.condition <= 2f)
            {
                Die("catastrophic brain injury");
                return;
            }

            if (heart != null &&
                heart.condition <= 2f)
            {
                Die("catastrophic cardiac injury");
                return;
            }

            if (bloodVolumeLiters <= 1.55f)
            {
                Die("exsanguination");
                return;
            }

            if (lowOxygenSeconds >= 30f)
            {
                Die("prolonged hypoxia");
            }
        }

        void ReportNpcEmergencyIfNeeded()
        {
            if (!citizenEmergency ||
                dead)
                return;

            float severity =
                Mathf.Max(
                    100f-BloodFraction*100f,
                    Mathf.Max(
                        shock,
                        100f-consciousness));

            severity =
                Mathf.Max(
                    severity,
                    WorstAnatomyDamage());

            bool needsHelp =
                severity >= 25f ||
                TotalBleedMlPerMinute >= 120f;

            if (needsHelp &&
                !emergencyReported)
            {
                emergencyReported = true;
                citizenEmergency.TriggerMedicalEmergency(
                    Mathf.Clamp(severity,25f,100f),
                    "traumatic injury",
                    false);
            }
            else if (!needsHelp &&
                     emergencyReported &&
                     severity < 12f)
            {
                emergencyReported = false;
            }
        }

        float WorstAnatomyDamage()
        {
            float worst =
                0f;

            for (int i=0;i<regions.Count;i++)
                worst =
                    Mathf.Max(
                        worst,
                        100f-regions[i].condition);

            for (int i=0;i<organs.Count;i++)
                worst =
                    Mathf.Max(
                        worst,
                        100f-organs[i].condition);

            return worst;
        }

        void Die(
            string reason)
        {
            if (dead)
                return;

            dead =
                true;
            causeOfDeath =
                reason;
            consciousness =
                0f;
            pulseBpm =
                0f;
            systolicPressure =
                0f;

            if (citizenBrain)
                citizenBrain.enabled = false;

            if (navAgent &&
                navAgent.enabled &&
                navAgent.isOnNavMesh)
            {
                navAgent.isStopped = true;
                if (navAgent.hasPath)
                    navAgent.ResetPath();
            }

            if (animator)
                animator.speed = 0f;

            OnChanged?.Invoke(this);
            OnDeath?.Invoke(this);
        }

        H51BodyRegion EstimateRegion(
            Vector3 local)
        {
            CharacterController controller =
                GetComponent<CharacterController>();

            float height =
                controller
                    ? Mathf.Max(1f,controller.height)
                    : 1.8f;

            float y =
                controller
                    ? local.y -
                      controller.center.y +
                      height * .5f
                    : local.y;

            float normalized =
                Mathf.Clamp01(
                    y / height);

            float x =
                local.x;

            if (normalized >= .88f)
                return H51BodyRegion.Head;
            if (normalized >= .80f)
                return H51BodyRegion.Neck;

            if (Mathf.Abs(x) > .32f &&
                normalized >= .45f &&
                normalized < .80f)
                return x < 0f
                    ? H51BodyRegion.LeftArm
                    : H51BodyRegion.RightArm;

            if (normalized >= .62f)
                return H51BodyRegion.Chest;
            if (normalized >= .46f)
                return H51BodyRegion.Abdomen;
            if (normalized >= .34f)
                return H51BodyRegion.Pelvis;

            return x < 0f
                ? H51BodyRegion.LeftLeg
                : H51BodyRegion.RightLeg;
        }

        static Vector3 RepresentativeLocalPoint(
            H51BodyRegion region)
        {
            switch (region)
            {
                case H51BodyRegion.Head:
                    return new Vector3(0f,1.68f,.02f);
                case H51BodyRegion.Neck:
                    return new Vector3(0f,1.48f,.02f);
                case H51BodyRegion.Chest:
                    return new Vector3(.08f,1.15f,.05f);
                case H51BodyRegion.Abdomen:
                    return new Vector3(.12f,.82f,.05f);
                case H51BodyRegion.Pelvis:
                    return new Vector3(-.10f,.62f,0f);
                case H51BodyRegion.LeftArm:
                    return new Vector3(-.48f,1.05f,0f);
                case H51BodyRegion.RightArm:
                    return new Vector3(.48f,1.05f,0f);
                case H51BodyRegion.LeftLeg:
                    return new Vector3(-.18f,.28f,0f);
                default:
                    return new Vector3(.18f,.28f,0f);
            }
        }

        static float DamageMultiplier(
            H51DamageType type)
        {
            switch (type)
            {
                case H51DamageType.Ballistic:
                    return 1.25f;
                case H51DamageType.Penetrating:
                    return 1.08f;
                case H51DamageType.Explosion:
                    return 1.15f;
                case H51DamageType.Crush:
                    return 1.12f;
                case H51DamageType.Burn:
                    return .72f;
                case H51DamageType.Fall:
                    return .88f;
                default:
                    return .82f;
            }
        }

        static float PainMultiplier(
            H51DamageType type)
        {
            switch (type)
            {
                case H51DamageType.Burn:
                    return 1.65f;
                case H51DamageType.Penetrating:
                    return 1.25f;
                case H51DamageType.Ballistic:
                    return 1.40f;
                case H51DamageType.Crush:
                    return 1.22f;
                default:
                    return 1f;
            }
        }

        static float WoundFactor(
            H51DamageType type)
        {
            switch (type)
            {
                case H51DamageType.Ballistic:
                    return 4.6f;
                case H51DamageType.Penetrating:
                    return 3.8f;
                case H51DamageType.Explosion:
                    return 2.2f;
                case H51DamageType.Crush:
                    return .65f;
                case H51DamageType.Blunt:
                    return .24f;
                case H51DamageType.Fall:
                    return .18f;
                case H51DamageType.Burn:
                    return .08f;
                default:
                    return .2f;
            }
        }

        static bool IsArterialHit(
            H51BodyRegion region,
            Vector3 local,
            float amount,
            H51DamageType type,
            float penetration)
        {
            bool penetrating =
                type == H51DamageType.Ballistic ||
                type == H51DamageType.Penetrating;

            if (!penetrating ||
                penetration < .32f ||
                amount < 8f)
                return false;

            switch (region)
            {
                case H51BodyRegion.Neck:
                    return true;

                case H51BodyRegion.Pelvis:
                    return penetration >= .48f && amount >= 14f;

                case H51BodyRegion.LeftArm:
                    // Brachial artery: upper/medial arm, toward the torso.
                    return local.y > .90f &&
                           local.x > -.47f;

                case H51BodyRegion.RightArm:
                    return local.y > .90f &&
                           local.x < .47f;

                case H51BodyRegion.LeftLeg:
                    // Femoral region: upper and medial thigh.
                    return local.y > .30f &&
                           local.x > -.22f;

                case H51BodyRegion.RightLeg:
                    return local.y > .30f &&
                           local.x < .22f;

                default:
                    return false;
            }
        }

        public void DrainBlood(float litres)
        {
            if (litres <= 0f || dead)
                return;

            bloodVolumeLiters =
                Mathf.Max(
                    0f,
                    bloodVolumeLiters-litres);

            OnChanged?.Invoke(this);
            EvaluateCriticalState();
            ReportNpcEmergencyIfNeeded();
        }

        static float RegionVascularity(
            H51BodyRegion region)
        {
            switch (region)
            {
                case H51BodyRegion.Neck:
                    return 2.5f;
                case H51BodyRegion.Head:
                    return 1.45f;
                case H51BodyRegion.Chest:
                    return 1.35f;
                case H51BodyRegion.Abdomen:
                    return 1.30f;
                case H51BodyRegion.Pelvis:
                    return 1.65f;
                case H51BodyRegion.LeftArm:
                case H51BodyRegion.RightArm:
                case H51BodyRegion.LeftLeg:
                case H51BodyRegion.RightLeg:
                    return 1.0f;
                default:
                    return 1f;
            }
        }

        static float RegionPainMultiplier(
            H51BodyRegion region)
        {
            if (region == H51BodyRegion.Head ||
                region == H51BodyRegion.Neck)
                return 1.15f;

            return 1f;
        }

        static float InternalBleedFor(
            H51DamageType type,
            float amount,
            float vascularity)
        {
            float factor =
                type == H51DamageType.Ballistic
                    ? 5.0f
                    : type == H51DamageType.Penetrating
                        ? 4.0f
                        : type == H51DamageType.Explosion
                            ? 2.2f
                            : type == H51DamageType.Crush
                                ? 1.2f
                                : .45f;

            return
                amount *
                factor *
                vascularity;
        }

        static bool IsLimb(
            H51BodyRegion region)
        {
            return
                region == H51BodyRegion.LeftArm ||
                region == H51BodyRegion.RightArm ||
                region == H51BodyRegion.LeftLeg ||
                region == H51BodyRegion.RightLeg;
        }

        float OrganFraction(
            H51OrganType type)
        {
            H51OrganState organ =
                GetOrgan(type);

            return
                organ == null
                    ? 1f
                    : Mathf.Clamp01(
                        organ.condition / 100f);
        }

        void EnsureAnatomy()
        {
            EnsureRegions();
            EnsureOrgans();
        }

        void EnsureRegions()
        {
            foreach (H51BodyRegion region in
                     Enum.GetValues(typeof(H51BodyRegion)))
            {
                bool found = false;
                for (int i=0;i<regions.Count;i++)
                {
                    if (regions[i] != null &&
                        regions[i].region == region)
                    {
                        if (string.IsNullOrWhiteSpace(regions[i].displayName))
                            regions[i].displayName =
                                Friendly(region.ToString());
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    regions.Add(
                        new H51BodyRegionState
                        {
                            region = region,
                            displayName = Friendly(region.ToString()),
                            condition = 100f
                        });
                }
            }
        }

        void EnsureOrgans()
        {
            foreach (H51OrganType organ in
                     Enum.GetValues(typeof(H51OrganType)))
            {
                bool found = false;
                for (int i=0;i<organs.Count;i++)
                {
                    if (organs[i] != null &&
                        organs[i].organ == organ)
                    {
                        if (string.IsNullOrWhiteSpace(organs[i].displayName))
                            organs[i].displayName =
                                Friendly(organ.ToString());
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    organs.Add(
                        new H51OrganState
                        {
                            organ = organ,
                            displayName = Friendly(organ.ToString()),
                            condition = 100f
                        });
                }
            }
        }

        static string Friendly(
            string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            System.Text.StringBuilder builder =
                new System.Text.StringBuilder();

            for (int i=0;i<value.Length;i++)
            {
                char c = value[i];
                if (i > 0 &&
                    char.IsUpper(c))
                    builder.Append(' ');
                builder.Append(c);
            }

            return builder.ToString();
        }
    }
}
