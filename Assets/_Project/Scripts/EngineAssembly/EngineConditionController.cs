using System;
using UnityEngine;

namespace Hanger51.EngineAssembly
{
    [DefaultExecutionOrder(20)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EngineAssemblyStation))]
    [RequireComponent(typeof(EngineAssemblyTransportController))]
    public sealed class EngineConditionController : MonoBehaviour
    {
        [Header("Assembly References")]
        [SerializeField] private EngineAssemblyStation station;
        [SerializeField] private EngineAssemblyTransportController transport;
        [SerializeField] private EngineAssemblyInteractionTarget[] coverTargets =
            new EngineAssemblyInteractionTarget[2];
        [SerializeField] private EngineAssemblyInteractionTarget[] sparkPlugTargets =
            new EngineAssemblyInteractionTarget[24];

        [Header("Condition Visuals")]
        [SerializeField] private Renderer[] sparkPlugRenderers = new Renderer[24];
        [SerializeField] private GameObject[] blockDamageStages = new GameObject[3];
        [SerializeField] private GameObject[] coverCrackRoots = new GameObject[2];
        [SerializeField] private ParticleSystem[] coverFireEffects = new ParticleSystem[2];
        [SerializeField] private ParticleSystem[] oilLeakEffects = new ParticleSystem[2];

        [Header("Oil System")]
        [SerializeField, Min(1f)] private float oilCapacityLiters = 20f;
        [SerializeField, Min(0f)] private float oilQuantityLiters = 20f;
        [SerializeField, Min(0f)] private float safeMinimumOilLiters = 15f;
        [SerializeField, Min(0f)] private float normalOilConsumptionLitersPerHour = 0.02f;
        [SerializeField, Min(0f)] private float crackedCoverLeakLitersPerSecond = 0.055f;

        [Header("Health")]
        [SerializeField, Range(0f, 100f)] private float engineBlockHealth = 100f;
        [SerializeField] private float[] coverHealth = { 100f, 100f };
        [SerializeField] private float[] sparkPlugHealth = new float[24];
        [SerializeField, Range(1f, 99f)] private float crackedCoverThreshold = 35f;

        [Header("Wear Rates")]
        [SerializeField, Min(0f)] private float sparkPlugWearPerRunningHour = 0.35f;
        [SerializeField, Min(0f)] private float zeroOilBlockDamagePerMinute = 14f;
        [SerializeField, Min(0f)] private float roughRunningCoverDamagePerMinute = 2.4f;

        [Header("Runtime State")]
        [SerializeField] private bool engineRunning;
        [SerializeField, Range(0f, 1f)] private float throttle;
        [SerializeField, Range(0f, 1f)] private float roughRunningSeverity;
        [SerializeField, Range(0f, 1f)] private float powerMultiplier = 1f;
        [SerializeField] private bool[] previousPlugInstalled = new bool[24];
        [SerializeField] private bool[] previousCoverInstalled = new bool[2];

        private readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        private float nextVisualRefreshTime;

        public float OilCapacityLiters => oilCapacityLiters;
        public float OilQuantityLiters => oilQuantityLiters;
        public float OilFraction => oilCapacityLiters > 0f
            ? Mathf.Clamp01(oilQuantityLiters / oilCapacityLiters)
            : 0f;
        public float SafeMinimumOilLiters => safeMinimumOilLiters;
        public float EngineBlockHealth => engineBlockHealth;
        public float AverageSparkPlugHealth => CalculateAverageInstalledPlugHealth();
        public float RoughRunningSeverity => roughRunningSeverity;
        public float PowerMultiplier => powerMultiplier;
        public bool EngineRunning => engineRunning;
        public bool CanService => !engineRunning
            && transport != null
            && !transport.IsSuspended
            && station != null
            && station.EngineBlockInstalled;

        private void Awake()
        {
            ResolveReferences();
            EnsureStateArrays();
            SynchronizeInstallationState(false);
            RecalculateCondition();
            RefreshConditionVisuals(true);
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureStateArrays();
            SynchronizeInstallationState(false);
            RecalculateCondition();
            RefreshConditionVisuals(true);
        }

        private void FixedUpdate()
        {
            ResolveReferences();
            EnsureStateArrays();
            SynchronizeInstallationState(true);

            float deltaTime = Time.fixedDeltaTime;
            ProcessOilLeak(deltaTime);
            if (engineRunning && station != null && station.EngineBlockInstalled)
            {
                ProcessRunningWear(deltaTime);
            }

            RecalculateCondition();
            if (Time.time >= nextVisualRefreshTime)
            {
                nextVisualRefreshTime = Time.time + 0.18f;
                RefreshConditionVisuals(false);
            }
        }

        public void Configure(
            EngineAssemblyStation configuredStation,
            EngineAssemblyTransportController configuredTransport,
            EngineAssemblyInteractionTarget[] configuredCoverTargets,
            EngineAssemblyInteractionTarget[] configuredSparkPlugTargets,
            Renderer[] configuredSparkPlugRenderers,
            GameObject[] configuredBlockDamageStages,
            GameObject[] configuredCoverCrackRoots,
            ParticleSystem[] configuredCoverFireEffects,
            ParticleSystem[] configuredOilLeakEffects,
            float configuredOilCapacityLiters)
        {
            station = configuredStation;
            transport = configuredTransport;
            coverTargets = configuredCoverTargets ?? new EngineAssemblyInteractionTarget[2];
            sparkPlugTargets = configuredSparkPlugTargets
                ?? new EngineAssemblyInteractionTarget[24];
            sparkPlugRenderers = configuredSparkPlugRenderers ?? new Renderer[24];
            blockDamageStages = configuredBlockDamageStages ?? new GameObject[3];
            coverCrackRoots = configuredCoverCrackRoots ?? new GameObject[2];
            coverFireEffects = configuredCoverFireEffects ?? new ParticleSystem[2];
            oilLeakEffects = configuredOilLeakEffects ?? new ParticleSystem[2];
            oilCapacityLiters = Mathf.Max(1f, configuredOilCapacityLiters);
            safeMinimumOilLiters = Mathf.Clamp(
                safeMinimumOilLiters,
                0f,
                oilCapacityLiters);
            EnsureStateArrays();
            SynchronizeInstallationState(false);
            RecalculateCondition();
            RefreshConditionVisuals(true);
        }

        public void InitializeNewEngineCondition()
        {
            EnsureStateArrays();
            oilQuantityLiters = oilCapacityLiters;
            engineBlockHealth = 100f;
            for (int index = 0; index < coverHealth.Length; index++)
            {
                coverHealth[index] = 100f;
                previousCoverInstalled[index] = IsCoverInstalled(index);
            }
            for (int index = 0; index < sparkPlugHealth.Length; index++)
            {
                sparkPlugHealth[index] = 100f;
                previousPlugInstalled[index] = IsSparkPlugInstalled(index);
            }
            engineRunning = false;
            throttle = 0f;
            roughRunningSeverity = 0f;
            RecalculateCondition();
            RefreshConditionVisuals(true);
        }

        public void SetOperatingState(bool running, float throttleCommand)
        {
            engineRunning = running
                && station != null
                && station.EngineBlockInstalled;
            throttle = engineRunning ? Mathf.Clamp01(throttleCommand) : 0f;
        }

        public float AddOil(float requestedLiters)
        {
            if (!CanService || requestedLiters <= 0f)
            {
                return 0f;
            }

            float accepted = Mathf.Min(
                requestedLiters,
                oilCapacityLiters - oilQuantityLiters);
            oilQuantityLiters += Mathf.Max(0f, accepted);
            RecalculateCondition();
            RefreshConditionVisuals(true);
            return Mathf.Max(0f, accepted);
        }

        public float GetSparkPlugHealth(int plugIndex)
        {
            EnsureStateArrays();
            return plugIndex >= 0 && plugIndex < sparkPlugHealth.Length
                ? sparkPlugHealth[plugIndex]
                : 0f;
        }

        public float GetCoverHealth(int coverIndex)
        {
            EnsureStateArrays();
            return coverIndex >= 0 && coverIndex < coverHealth.Length
                ? coverHealth[coverIndex]
                : 0f;
        }

        public bool IsCoverCracked(int coverIndex)
        {
            return IsCoverInstalled(coverIndex)
                && GetCoverHealth(coverIndex) <= crackedCoverThreshold;
        }

        public string GetOilReadingText()
        {
            string state;
            if (OilFraction >= 0.82f)
            {
                state = "FULL";
            }
            else if (oilQuantityLiters >= safeMinimumOilLiters)
            {
                state = "SAFE";
            }
            else if (OilFraction >= 0.35f)
            {
                state = "LOW";
            }
            else
            {
                state = "CRITICAL";
            }

            return $"Oil: {state} — {oilQuantityLiters:F1}/{oilCapacityLiters:F1} L";
        }

        public string GetBlockInspectionText()
        {
            return $"Engine block condition: {engineBlockHealth:F0}% | "
                + $"Available power: {powerMultiplier * 100f:F0}% | "
                + GetOilReadingText();
        }

        public string GetCoverInspectionText(int coverIndex)
        {
            string side = coverIndex == 0 ? "Left" : "Right";
            string crackState = IsCoverCracked(coverIndex) ? "CRACKED" : "intact";
            return $"{side} cylinder cover: {GetCoverHealth(coverIndex):F0}% — {crackState}.";
        }

        public string GetSparkPlugInspectionText(int plugIndex)
        {
            string installed = IsSparkPlugInstalled(plugIndex)
                ? "installed"
                : "not installed";
            int cylinder = plugIndex / 2 + 1;
            string position = plugIndex % 2 == 0 ? "A" : "B";
            return $"Cylinder {cylinder} plug {position}: "
                + $"{GetSparkPlugHealth(plugIndex):F0}% — {installed}.";
        }

        public void ApplyDebugWear(
            float blockHealth,
            float plugHealth,
            float leftCoverHealth,
            float rightCoverHealth,
            float oilLiters)
        {
            engineBlockHealth = Mathf.Clamp(blockHealth, 0f, 100f);
            EnsureStateArrays();
            for (int index = 0; index < sparkPlugHealth.Length; index++)
            {
                sparkPlugHealth[index] = Mathf.Clamp(plugHealth, 0f, 100f);
            }
            coverHealth[0] = Mathf.Clamp(leftCoverHealth, 0f, 100f);
            coverHealth[1] = Mathf.Clamp(rightCoverHealth, 0f, 100f);
            oilQuantityLiters = Mathf.Clamp(oilLiters, 0f, oilCapacityLiters);
            RecalculateCondition();
            RefreshConditionVisuals(true);
        }

        private void ProcessRunningWear(float deltaTime)
        {
            float hours = deltaTime / 3600f;
            float minutes = deltaTime / 60f;
            float throttleStress = Mathf.Lerp(0.35f, 1.65f, throttle);

            for (int index = 0; index < sparkPlugHealth.Length; index++)
            {
                if (!IsSparkPlugInstalled(index))
                {
                    continue;
                }

                float roughMultiplier = Mathf.Lerp(1f, 4f, roughRunningSeverity);
                sparkPlugHealth[index] = Mathf.Max(
                    0f,
                    sparkPlugHealth[index]
                    - sparkPlugWearPerRunningHour
                    * hours
                    * throttleStress
                    * roughMultiplier);
            }

            oilQuantityLiters = Mathf.Max(
                0f,
                oilQuantityLiters
                - normalOilConsumptionLitersPerHour
                * hours
                * Mathf.Lerp(0.45f, 1.4f, throttle));

            float lowOilSeverity = 1f - Mathf.InverseLerp(
                0.12f,
                0.62f,
                OilFraction);
            if (lowOilSeverity > 0f)
            {
                engineBlockHealth = Mathf.Max(
                    0f,
                    engineBlockHealth
                    - zeroOilBlockDamagePerMinute
                    * minutes
                    * lowOilSeverity
                    * Mathf.Lerp(0.25f, 1.25f, throttle));
            }

            RecalculateCondition();
            if (roughRunningSeverity > 0.25f && throttle > 0.45f)
            {
                float coverDamage = roughRunningCoverDamagePerMinute
                    * minutes
                    * Mathf.InverseLerp(0.25f, 1f, roughRunningSeverity)
                    * Mathf.InverseLerp(0.45f, 1f, throttle);

                int firstBank = Mathf.Sin(Time.time * 0.37f) >= 0f ? 0 : 1;
                DamageCover(firstBank, coverDamage);
                if (roughRunningSeverity > 0.72f)
                {
                    DamageCover(1 - firstBank, coverDamage * 0.60f);
                }
            }
        }

        private void DamageCover(int coverIndex, float amount)
        {
            if (!IsCoverInstalled(coverIndex)
                || coverIndex < 0
                || coverIndex >= coverHealth.Length)
            {
                return;
            }

            coverHealth[coverIndex] = Mathf.Max(
                0f,
                coverHealth[coverIndex] - Mathf.Max(0f, amount));
        }

        private void ProcessOilLeak(float deltaTime)
        {
            int crackedCount = 0;
            for (int index = 0; index < coverHealth.Length; index++)
            {
                if (IsCoverCracked(index))
                {
                    crackedCount++;
                }
            }

            if (crackedCount <= 0)
            {
                return;
            }

            oilQuantityLiters = Mathf.Max(
                0f,
                oilQuantityLiters
                - crackedCoverLeakLitersPerSecond
                * crackedCount
                * deltaTime);
        }

        private void SynchronizeInstallationState(bool resetNewParts)
        {
            for (int index = 0; index < previousPlugInstalled.Length; index++)
            {
                bool installed = IsSparkPlugInstalled(index);
                if (resetNewParts && installed && !previousPlugInstalled[index])
                {
                    sparkPlugHealth[index] = 100f;
                }
                previousPlugInstalled[index] = installed;
            }

            for (int index = 0; index < previousCoverInstalled.Length; index++)
            {
                bool installed = IsCoverInstalled(index);
                if (resetNewParts && installed && !previousCoverInstalled[index])
                {
                    coverHealth[index] = 100f;
                }
                previousCoverInstalled[index] = installed;
            }
        }

        private bool IsSparkPlugInstalled(int index)
        {
            if (station == null || index < 0 || index >= sparkPlugTargets.Length)
            {
                return false;
            }

            EngineAssemblyInteractionTarget target = sparkPlugTargets[index];
            return target != null
                && station.IsTargetComplete(
                    EngineAssemblyInteractionKind.SparkPlug,
                    target.GroupIndex,
                    target.TargetIndex);
        }

        private bool IsCoverInstalled(int index)
        {
            if (station == null || index < 0 || index >= coverTargets.Length)
            {
                return false;
            }

            EngineAssemblyInteractionTarget target = coverTargets[index];
            return target != null
                && station.IsTargetComplete(
                    EngineAssemblyInteractionKind.CoverPlacement,
                    target.GroupIndex,
                    target.TargetIndex);
        }

        private void RecalculateCondition()
        {
            float oilFactor = Mathf.Lerp(
                0.16f,
                1f,
                Mathf.InverseLerp(0.05f, 0.75f, OilFraction));
            float blockFactor = Mathf.Lerp(
                0.28f,
                1f,
                engineBlockHealth / 100f);
            float plugFactor = CalculateIgnitionEffectiveness();
            float coverFactor = 1f;
            for (int index = 0; index < coverHealth.Length; index++)
            {
                if (!IsCoverInstalled(index))
                {
                    coverFactor *= 0.35f;
                }
                else if (IsCoverCracked(index))
                {
                    coverFactor *= 0.68f;
                }
                else
                {
                    coverFactor *= Mathf.Lerp(
                        0.88f,
                        1f,
                        coverHealth[index] / 100f);
                }
            }

            powerMultiplier = Mathf.Clamp(
                oilFactor * blockFactor * plugFactor * coverFactor,
                0.05f,
                1f);

            float lowOilSeverity = 1f - Mathf.InverseLerp(
                0.18f,
                0.62f,
                OilFraction);
            float ignitionSeverity = 1f - plugFactor;
            float blockSeverity = 1f - engineBlockHealth / 100f;
            float crackSeverity = 0f;
            if (IsCoverCracked(0)) crackSeverity += 0.55f;
            if (IsCoverCracked(1)) crackSeverity += 0.45f;
            roughRunningSeverity = Mathf.Clamp01(
                Mathf.Max(lowOilSeverity, ignitionSeverity, blockSeverity * 0.75f)
                + crackSeverity);
        }

        private float CalculateIgnitionEffectiveness()
        {
            int cylinderCount = Mathf.Max(1, sparkPlugHealth.Length / 2);
            float total = 0f;
            for (int cylinder = 0; cylinder < cylinderCount; cylinder++)
            {
                int first = cylinder * 2;
                int second = first + 1;
                float firstEffect = IsSparkPlugInstalled(first)
                    ? sparkPlugHealth[first] / 100f
                    : 0f;
                float secondEffect = IsSparkPlugInstalled(second)
                    ? sparkPlugHealth[second] / 100f
                    : 0f;
                float stronger = Mathf.Max(firstEffect, secondEffect);
                float weaker = Mathf.Min(firstEffect, secondEffect);
                total += Mathf.Clamp01((stronger + weaker * 0.25f) / 1.25f);
            }

            return Mathf.Clamp(total / cylinderCount, 0.05f, 1f);
        }

        private float CalculateAverageInstalledPlugHealth()
        {
            float total = 0f;
            int count = 0;
            for (int index = 0; index < sparkPlugHealth.Length; index++)
            {
                if (!IsSparkPlugInstalled(index))
                {
                    continue;
                }
                total += sparkPlugHealth[index];
                count++;
            }
            return count > 0 ? total / count : 0f;
        }

        private void RefreshConditionVisuals(bool forceParticleRefresh)
        {
            EnsureStateArrays();

            for (int index = 0; index < blockDamageStages.Length; index++)
            {
                if (blockDamageStages[index] == null)
                {
                    continue;
                }

                bool active = index == 0
                    ? engineBlockHealth <= 75f
                    : index == 1
                        ? engineBlockHealth <= 45f
                        : engineBlockHealth <= 18f;
                blockDamageStages[index].SetActive(active);
            }

            for (int index = 0; index < coverCrackRoots.Length; index++)
            {
                bool cracked = IsCoverCracked(index);
                if (coverCrackRoots[index] != null)
                {
                    coverCrackRoots[index].SetActive(cracked);
                }
                SetParticleState(
                    GetParticle(coverFireEffects, index),
                    cracked && engineRunning && throttle > 0.18f,
                    forceParticleRefresh);
                SetParticleState(
                    GetParticle(oilLeakEffects, index),
                    cracked && oilQuantityLiters > 0f,
                    forceParticleRefresh);
            }

            for (int index = 0; index < sparkPlugRenderers.Length; index++)
            {
                Renderer renderer = sparkPlugRenderers[index];
                if (renderer == null)
                {
                    continue;
                }

                float health = GetSparkPlugHealth(index) / 100f;
                Color color = Color.Lerp(
                    new Color(0.10f, 0.07f, 0.04f, 1f),
                    new Color(0.92f, 0.90f, 0.78f, 1f),
                    health);
                renderer.GetPropertyBlock(propertyBlock);
                if (renderer.sharedMaterial != null
                    && renderer.sharedMaterial.HasProperty("_BaseColor"))
                {
                    propertyBlock.SetColor("_BaseColor", color);
                }
                if (renderer.sharedMaterial != null
                    && renderer.sharedMaterial.HasProperty("_Color"))
                {
                    propertyBlock.SetColor("_Color", color);
                }
                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private static ParticleSystem GetParticle(ParticleSystem[] particles, int index)
        {
            return particles != null && index >= 0 && index < particles.Length
                ? particles[index]
                : null;
        }

        private static void SetParticleState(
            ParticleSystem particle,
            bool shouldPlay,
            bool force)
        {
            if (particle == null)
            {
                return;
            }

            if (shouldPlay)
            {
                if (force || !particle.isPlaying)
                {
                    particle.Play(true);
                }
            }
            else if (force || particle.isPlaying)
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void ResolveReferences()
        {
            if (station == null)
            {
                station = GetComponent<EngineAssemblyStation>();
            }
            if (transport == null)
            {
                transport = GetComponent<EngineAssemblyTransportController>();
            }
        }

        private void EnsureStateArrays()
        {
            coverHealth = ResizeFloatArray(coverHealth, 2, 100f);
            sparkPlugHealth = ResizeFloatArray(sparkPlugHealth, 24, 100f);
            previousCoverInstalled = ResizeBoolArray(previousCoverInstalled, 2);
            previousPlugInstalled = ResizeBoolArray(previousPlugInstalled, 24);
            coverTargets = ResizeObjectArray(coverTargets, 2);
            sparkPlugTargets = ResizeObjectArray(sparkPlugTargets, 24);
            sparkPlugRenderers = ResizeRendererArray(sparkPlugRenderers, 24);
            blockDamageStages = ResizeGameObjectArray(blockDamageStages, 3);
            coverCrackRoots = ResizeGameObjectArray(coverCrackRoots, 2);
            coverFireEffects = ResizeParticleArray(coverFireEffects, 2);
            oilLeakEffects = ResizeParticleArray(oilLeakEffects, 2);
            oilCapacityLiters = Mathf.Max(1f, oilCapacityLiters);
            oilQuantityLiters = Mathf.Clamp(oilQuantityLiters, 0f, oilCapacityLiters);
            safeMinimumOilLiters = Mathf.Clamp(
                safeMinimumOilLiters,
                0f,
                oilCapacityLiters);
            engineBlockHealth = Mathf.Clamp(engineBlockHealth, 0f, 100f);
            for (int index = 0; index < coverHealth.Length; index++)
            {
                coverHealth[index] = Mathf.Clamp(coverHealth[index], 0f, 100f);
            }
            for (int index = 0; index < sparkPlugHealth.Length; index++)
            {
                if (sparkPlugHealth[index] <= 0f && !Application.isPlaying)
                {
                    sparkPlugHealth[index] = 100f;
                }
                sparkPlugHealth[index] = Mathf.Clamp(sparkPlugHealth[index], 0f, 100f);
            }
        }

        private static float[] ResizeFloatArray(float[] source, int size, float defaultValue)
        {
            float[] result = new float[size];
            for (int index = 0; index < size; index++)
            {
                result[index] = source != null && index < source.Length
                    ? source[index]
                    : defaultValue;
            }
            return result;
        }

        private static bool[] ResizeBoolArray(bool[] source, int size)
        {
            bool[] result = new bool[size];
            if (source != null)
            {
                Array.Copy(source, result, Mathf.Min(source.Length, result.Length));
            }
            return result;
        }

        private static T[] ResizeObjectArray<T>(T[] source, int size)
            where T : UnityEngine.Object
        {
            T[] result = new T[size];
            if (source != null)
            {
                Array.Copy(source, result, Mathf.Min(source.Length, result.Length));
            }
            return result;
        }

        private static Renderer[] ResizeRendererArray(Renderer[] source, int size)
        {
            return ResizeObjectArray(source, size);
        }

        private static GameObject[] ResizeGameObjectArray(GameObject[] source, int size)
        {
            return ResizeObjectArray(source, size);
        }

        private static ParticleSystem[] ResizeParticleArray(ParticleSystem[] source, int size)
        {
            return ResizeObjectArray(source, size);
        }

        private void OnDisable()
        {
            engineRunning = false;
            throttle = 0f;
            RefreshConditionVisuals(true);
        }

        private void OnValidate()
        {
            ResolveReferences();
            EnsureStateArrays();
            crackedCoverThreshold = Mathf.Clamp(crackedCoverThreshold, 1f, 99f);
            sparkPlugWearPerRunningHour = Mathf.Max(0f, sparkPlugWearPerRunningHour);
            zeroOilBlockDamagePerMinute = Mathf.Max(0f, zeroOilBlockDamagePerMinute);
            roughRunningCoverDamagePerMinute = Mathf.Max(
                0f,
                roughRunningCoverDamagePerMinute);
            RecalculateCondition();
        }
    }
}
