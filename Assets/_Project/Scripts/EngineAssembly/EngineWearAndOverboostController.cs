using System;
using System.Reflection;
using UnityEngine;

namespace Hanger51.EngineAssembly
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EngineConditionController))]
    public sealed class EngineWearAndOverboostController : MonoBehaviour
    {
        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly FieldInfo ThrottleField =
            typeof(EngineConditionController).GetField("throttle", PrivateInstance);
        private static readonly FieldInfo SparkPlugHealthField =
            typeof(EngineConditionController).GetField("sparkPlugHealth", PrivateInstance);
        private static readonly FieldInfo CoverHealthField =
            typeof(EngineConditionController).GetField("coverHealth", PrivateInstance);
        private static readonly MethodInfo IsSparkPlugInstalledMethod =
            typeof(EngineConditionController).GetMethod("IsSparkPlugInstalled", PrivateInstance);
        private static readonly MethodInfo IsCoverInstalledMethod =
            typeof(EngineConditionController).GetMethod("IsCoverInstalled", PrivateInstance);
        private static readonly MethodInfo RecalculateConditionMethod =
            typeof(EngineConditionController).GetMethod("RecalculateCondition", PrivateInstance);
        private static readonly MethodInfo RefreshConditionVisualsMethod =
            typeof(EngineConditionController).GetMethod("RefreshConditionVisuals", PrivateInstance);

        [Header("Condition Reference")]
        [SerializeField] private EngineConditionController condition;

        [Header("Very Slow Spark-Plug Wear")]
        [SerializeField, Min(0f)] private float sparkPlugWearPerRunningHour = 0.20f;
        [SerializeField, Min(0.0001f)] private float minimumAppliedWearStep = 0.001f;

        [Header("Sustained High-Power Cover Damage")]
        [SerializeField, Range(0.5f, 1f)] private float overboostThrottleThreshold = 0.95f;
        [SerializeField, Min(1f)] private float overboostGraceSeconds = 60f;
        [SerializeField, Min(0f)] private float primaryCoverDamagePerMinute = 55f;
        [SerializeField, Min(0f)] private float secondaryCoverDelaySeconds = 45f;
        [SerializeField, Min(0f)] private float secondaryCoverDamagePerMinute = 24f;
        [SerializeField, Min(0f)] private float exposureCooldownPerSecond = 0.75f;

        [Header("Runtime Exposure")]
        [SerializeField, Min(0f)] private float overboostExposureSeconds;
        [SerializeField] private int primaryCoverIndex = -1;

        private readonly double[] plugWearRemainders = new double[24];
        private bool reflectionReady;

        public float SparkPlugWearPerRunningHour => sparkPlugWearPerRunningHour;
        public float OverboostThrottleThreshold => overboostThrottleThreshold;
        public float OverboostGraceSeconds => overboostGraceSeconds;
        public float OverboostExposureSeconds => overboostExposureSeconds;
        public float SecondsUntilCoverDamage => Mathf.Max(
            0f,
            overboostGraceSeconds - overboostExposureSeconds);
        public bool IsOverboosting => condition != null
            && condition.EngineRunning
            && ReadThrottle() >= overboostThrottleThreshold;

        private void Awake()
        {
            ResolveReferences();
            ValidateReflectionBindings();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ValidateReflectionBindings();
        }

        public void Configure(
            float configuredPlugWearPerHour,
            float configuredThrottleThreshold,
            float configuredGraceSeconds,
            float configuredPrimaryDamagePerMinute,
            float configuredSecondaryDelaySeconds,
            float configuredSecondaryDamagePerMinute,
            float configuredCooldownPerSecond)
        {
            sparkPlugWearPerRunningHour = Mathf.Max(0f, configuredPlugWearPerHour);
            overboostThrottleThreshold = Mathf.Clamp(
                configuredThrottleThreshold,
                0.5f,
                1f);
            overboostGraceSeconds = Mathf.Max(1f, configuredGraceSeconds);
            primaryCoverDamagePerMinute = Mathf.Max(
                0f,
                configuredPrimaryDamagePerMinute);
            secondaryCoverDelaySeconds = Mathf.Max(
                0f,
                configuredSecondaryDelaySeconds);
            secondaryCoverDamagePerMinute = Mathf.Max(
                0f,
                configuredSecondaryDamagePerMinute);
            exposureCooldownPerSecond = Mathf.Max(
                0f,
                configuredCooldownPerSecond);
            ResolveReferences();
            ValidateReflectionBindings();
        }

        public void PrimeForOverboostTest()
        {
            ResolveReferences();
            ValidateReflectionBindings();
            if (condition == null || !reflectionReady)
            {
                return;
            }

            EnsurePrimaryCover();
            overboostExposureSeconds = overboostGraceSeconds + 1f;

            float[] coverHealth = GetCoverHealthArray();
            if (coverHealth != null && coverHealth.Length >= 2)
            {
                coverHealth[primaryCoverIndex] = Mathf.Min(
                    coverHealth[primaryCoverIndex],
                    44f);
                int secondary = 1 - primaryCoverIndex;
                coverHealth[secondary] = Mathf.Min(coverHealth[secondary], 78f);
                RefreshConditionAfterWear();
            }
        }

        public void ResetExposure()
        {
            overboostExposureSeconds = 0f;
            primaryCoverIndex = -1;
            Array.Clear(plugWearRemainders, 0, plugWearRemainders.Length);
        }

        private void FixedUpdate()
        {
            ResolveReferences();
            if (condition == null)
            {
                return;
            }

            if (!reflectionReady)
            {
                ValidateReflectionBindings();
            }
            if (!reflectionReady)
            {
                return;
            }

            float deltaTime = Time.fixedDeltaTime;
            if (!condition.EngineRunning)
            {
                CoolExposure(deltaTime, 2f);
                return;
            }

            float throttle = ReadThrottle();
            ProcessPreciseSparkPlugWear(deltaTime, throttle);
            ProcessSustainedOverboost(deltaTime, throttle);
        }

        private void ProcessPreciseSparkPlugWear(float deltaTime, float throttle)
        {
            float[] health = GetSparkPlugHealthArray();
            if (health == null || health.Length == 0)
            {
                return;
            }

            double runningHours = Math.Max(0.0, deltaTime) / 3600.0;
            double throttleStress = Mathf.Lerp(0.40f, 1.65f, throttle);
            double roughMultiplier = Mathf.Lerp(
                1f,
                3.5f,
                condition.RoughRunningSeverity);
            double wearThisFrame = sparkPlugWearPerRunningHour
                * runningHours
                * throttleStress
                * roughMultiplier;

            bool changed = false;
            int count = Mathf.Min(health.Length, plugWearRemainders.Length);
            for (int index = 0; index < count; index++)
            {
                if (!IsSparkPlugInstalled(index) || health[index] <= 0f)
                {
                    plugWearRemainders[index] = 0.0;
                    continue;
                }

                plugWearRemainders[index] += wearThisFrame;
                double stepSize = Math.Max(0.0001, minimumAppliedWearStep);
                double applied = Math.Floor(
                    plugWearRemainders[index] / stepSize) * stepSize;
                if (applied < stepSize)
                {
                    continue;
                }

                float previous = health[index];
                health[index] = Mathf.Max(0f, previous - (float)applied);
                double actualApplied = previous - health[index];
                plugWearRemainders[index] = Math.Max(
                    0.0,
                    plugWearRemainders[index] - actualApplied);
                changed |= health[index] < previous;
            }

            if (changed)
            {
                RefreshConditionAfterWear();
            }
        }

        private void ProcessSustainedOverboost(float deltaTime, float throttle)
        {
            if (throttle >= overboostThrottleThreshold)
            {
                EnsurePrimaryCover();
                float stress = Mathf.Lerp(
                    0.75f,
                    1.25f,
                    Mathf.InverseLerp(
                        overboostThrottleThreshold,
                        1f,
                        throttle));
                overboostExposureSeconds += deltaTime * stress;

                if (overboostExposureSeconds <= overboostGraceSeconds)
                {
                    return;
                }

                float ramp = Mathf.Lerp(
                    0.45f,
                    1f,
                    Mathf.InverseLerp(
                        overboostGraceSeconds,
                        overboostGraceSeconds + 60f,
                        overboostExposureSeconds));
                bool changed = ApplyCoverDamage(
                    primaryCoverIndex,
                    primaryCoverDamagePerMinute
                        * (deltaTime / 60f)
                        * ramp);

                if (overboostExposureSeconds
                    >= overboostGraceSeconds + secondaryCoverDelaySeconds)
                {
                    changed |= ApplyCoverDamage(
                        1 - primaryCoverIndex,
                        secondaryCoverDamagePerMinute
                            * (deltaTime / 60f)
                            * ramp);
                }

                if (changed)
                {
                    RefreshConditionAfterWear();
                }
                return;
            }

            if (throttle <= overboostThrottleThreshold - 0.03f)
            {
                CoolExposure(deltaTime, 1f);
            }
        }

        private void CoolExposure(float deltaTime, float multiplier)
        {
            if (overboostExposureSeconds <= 0f)
            {
                overboostExposureSeconds = 0f;
                primaryCoverIndex = -1;
                return;
            }

            overboostExposureSeconds = Mathf.Max(
                0f,
                overboostExposureSeconds
                    - exposureCooldownPerSecond
                    * Mathf.Max(0f, multiplier)
                    * deltaTime);
            if (overboostExposureSeconds <= 0.001f)
            {
                overboostExposureSeconds = 0f;
                primaryCoverIndex = -1;
            }
        }

        private bool ApplyCoverDamage(int coverIndex, float amount)
        {
            if (amount <= 0f || !IsCoverInstalled(coverIndex))
            {
                return false;
            }

            float[] health = GetCoverHealthArray();
            if (health == null || coverIndex < 0 || coverIndex >= health.Length)
            {
                return false;
            }

            float previous = health[coverIndex];
            health[coverIndex] = Mathf.Max(0f, previous - amount);
            return health[coverIndex] < previous;
        }

        private void EnsurePrimaryCover()
        {
            if (primaryCoverIndex == 0 || primaryCoverIndex == 1)
            {
                return;
            }

            primaryCoverIndex = (condition.GetInstanceID() & 1) == 0 ? 0 : 1;
        }

        private float ReadThrottle()
        {
            if (condition == null || ThrottleField == null)
            {
                return 0f;
            }

            object value = ThrottleField.GetValue(condition);
            return value is float throttleValue
                ? Mathf.Clamp01(throttleValue)
                : 0f;
        }

        private float[] GetSparkPlugHealthArray()
        {
            return SparkPlugHealthField?.GetValue(condition) as float[];
        }

        private float[] GetCoverHealthArray()
        {
            return CoverHealthField?.GetValue(condition) as float[];
        }

        private bool IsSparkPlugInstalled(int index)
        {
            return InvokeInstalledCheck(IsSparkPlugInstalledMethod, index);
        }

        private bool IsCoverInstalled(int index)
        {
            return InvokeInstalledCheck(IsCoverInstalledMethod, index);
        }

        private bool InvokeInstalledCheck(MethodInfo method, int index)
        {
            if (condition == null || method == null)
            {
                return false;
            }

            object result = method.Invoke(condition, new object[] { index });
            return result is bool installed && installed;
        }

        private void RefreshConditionAfterWear()
        {
            if (condition == null)
            {
                return;
            }

            RecalculateConditionMethod?.Invoke(condition, null);
            RefreshConditionVisualsMethod?.Invoke(
                condition,
                new object[] { false });
        }

        private void ResolveReferences()
        {
            if (condition == null)
            {
                condition = GetComponent<EngineConditionController>();
            }
        }

        private void ValidateReflectionBindings()
        {
            reflectionReady = ThrottleField != null
                && SparkPlugHealthField != null
                && CoverHealthField != null
                && IsSparkPlugInstalledMethod != null
                && IsCoverInstalledMethod != null
                && RecalculateConditionMethod != null
                && RefreshConditionVisualsMethod != null;

            if (!reflectionReady)
            {
                Debug.LogError(
                    "EngineWearAndOverboostController could not bind to the current EngineConditionController fields. Re-run the latest setup after compiling.",
                    this);
            }
        }

        private void OnDisable()
        {
            CoolExposure(Time.fixedDeltaTime, 2f);
        }

        private void OnValidate()
        {
            sparkPlugWearPerRunningHour = Mathf.Max(
                0f,
                sparkPlugWearPerRunningHour);
            minimumAppliedWearStep = Mathf.Max(
                0.0001f,
                minimumAppliedWearStep);
            overboostThrottleThreshold = Mathf.Clamp(
                overboostThrottleThreshold,
                0.5f,
                1f);
            overboostGraceSeconds = Mathf.Max(1f, overboostGraceSeconds);
            primaryCoverDamagePerMinute = Mathf.Max(
                0f,
                primaryCoverDamagePerMinute);
            secondaryCoverDelaySeconds = Mathf.Max(
                0f,
                secondaryCoverDelaySeconds);
            secondaryCoverDamagePerMinute = Mathf.Max(
                0f,
                secondaryCoverDamagePerMinute);
            exposureCooldownPerSecond = Mathf.Max(
                0f,
                exposureCooldownPerSecond);
            overboostExposureSeconds = Mathf.Max(0f, overboostExposureSeconds);
            ResolveReferences();
        }
    }
}
