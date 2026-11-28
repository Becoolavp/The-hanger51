using System.Reflection;
using Hanger51.EngineAssembly;
using UnityEngine;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(115)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(P51FlightController))]
    public sealed class P51RadiatorCoolingSystem : MonoBehaviour
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo BlockHealthField =
            typeof(EngineConditionController).GetField("engineBlockHealth", PrivateInstance);
        private static readonly FieldInfo CoverHealthField =
            typeof(EngineConditionController).GetField("coverHealth", PrivateInstance);
        private static readonly MethodInfo RecalculateConditionMethod =
            typeof(EngineConditionController).GetMethod("RecalculateCondition", PrivateInstance);
        private static readonly MethodInfo RefreshConditionVisualsMethod =
            typeof(EngineConditionController).GetMethod("RefreshConditionVisuals", PrivateInstance);

        [Header("Aircraft / Visual References")]
        [SerializeField] private P51FlightController flightController;
        [SerializeField] private Transform radiatorExitDoorPivot;
        [SerializeField] private Vector3 doorClosedLocalEuler = Vector3.zero;
        [SerializeField] private Vector3 doorOpenLocalEuler = new Vector3(-38f, 0f, 0f);
        [SerializeField] private ParticleSystem coolantLeakEffect;

        [Header("Coolant Circuit")]
        [SerializeField, Min(1f)] private float coolantCapacityLiters = 98f;
        [SerializeField, Min(0f)] private float coolantLiters = 98f;
        [SerializeField] private bool resetCoolantOnAwake = true;

        [Header("Radiator Condition")]
        [SerializeField, Range(0f, 100f)] private float radiatorHealth = 100f;
        [SerializeField, Range(0f, 100f)] private float leakStartsBelowHealth = 95f;
        [SerializeField, Min(0f)] private float maximumLeakLitersPerSecond = 0.35f;

        [Header("Engine Temperature")]
        [SerializeField] private float ambientTemperatureC = 22f;
        [SerializeField] private float coolantTemperatureC = 22f;
        [SerializeField] private float doorStartsOpeningC = 80f;
        [SerializeField] private float doorFullyOpenC = 118f;
        [SerializeField] private float overheatWarningC = 108f;
        [SerializeField] private float engineDamageStartsC = 115f;
        [SerializeField] private float severeOverheatC = 135f;
        [SerializeField, Min(0.01f)] private float doorTravelPerSecond = 0.55f;

        [Header("Thermal Rates")]
        [SerializeField, Min(0f)] private float idleHeatCPerSecond = 0.08f;
        [SerializeField, Min(0f)] private float fullPowerHeatCPerSecond = 0.42f;
        [SerializeField, Min(0f)] private float baseCoolingCPerSecond = 0.05f;
        [SerializeField, Min(0f)] private float doorCoolingCPerSecond = 0.28f;
        [SerializeField, Min(0f)] private float ramAirCoolingCPerSecond = 0.55f;
        [SerializeField, Min(1f)] private float fullRamAirSpeedMetersPerSecond = 70f;

        [Header("Overheat Damage")]
        [SerializeField, Min(0f)] private float mildBlockDamagePerMinute = 2f;
        [SerializeField, Min(0f)] private float severeBlockDamagePerMinute = 22f;
        [SerializeField, Min(0f)] private float severeCoverDamagePerMinute = 14f;

        [Header("Runtime")]
        [SerializeField, Range(0f, 1f)] private float doorOpenFraction;
        [SerializeField] private bool overheating;
        [SerializeField] private bool warningShown;

        public float CoolantCapacityLiters => coolantCapacityLiters;
        public float CoolantLiters => coolantLiters;
        public float CoolantFraction => coolantCapacityLiters > 0f
            ? Mathf.Clamp01(coolantLiters / coolantCapacityLiters)
            : 0f;
        public float CoolantTemperatureC => coolantTemperatureC;
        public float RadiatorHealth => radiatorHealth;
        public float DoorOpenFraction => doorOpenFraction;
        public bool IsLeaking => radiatorHealth < leakStartsBelowHealth && coolantLiters > 0.001f;
        public bool IsOverheating => overheating;
        public bool EngineRunning => flightController != null && flightController.EngineRunning;
        public bool CanService => !EngineRunning && coolantTemperatureC <= 80f;

        private void Awake()
        {
            ResolveReferences();
            if (resetCoolantOnAwake)
            {
                coolantLiters = coolantCapacityLiters;
                radiatorHealth = 100f;
                coolantTemperatureC = ambientTemperatureC;
            }
            ClampState();
            ApplyDoorVisual(true);
            UpdateLeakEffect();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ClampState();
            ApplyDoorVisual(true);
            UpdateLeakEffect();
        }

        private void FixedUpdate()
        {
            ResolveReferences();
            float dt = Time.fixedDeltaTime;
            ProcessCoolantLeak(dt);
            ProcessThermalState(dt);
            ProcessOverheatDamage(dt);
            UpdateDoor(dt);
            UpdateLeakEffect();
        }

        public void Configure(
            P51FlightController configuredFlightController,
            Transform configuredDoorPivot,
            Vector3 configuredClosedEuler,
            Vector3 configuredOpenEuler,
            ParticleSystem configuredLeakEffect,
            float configuredCoolantCapacityLiters)
        {
            flightController = configuredFlightController;
            radiatorExitDoorPivot = configuredDoorPivot;
            doorClosedLocalEuler = configuredClosedEuler;
            doorOpenLocalEuler = configuredOpenEuler;
            coolantLeakEffect = configuredLeakEffect;
            coolantCapacityLiters = Mathf.Max(1f, configuredCoolantCapacityLiters);
            coolantLiters = coolantCapacityLiters;
            radiatorHealth = 100f;
            coolantTemperatureC = ambientTemperatureC;
            resetCoolantOnAwake = true;
            doorOpenFraction = 0.05f;
            warningShown = false;
            ApplyDoorVisual(true);
            UpdateLeakEffect();
        }

        public float AddCoolant(float requestedLiters)
        {
            if (!CanService || requestedLiters <= 0f)
            {
                return 0f;
            }

            float accepted = Mathf.Min(
                requestedLiters,
                Mathf.Max(0f, coolantCapacityLiters - coolantLiters));
            coolantLiters += Mathf.Max(0f, accepted);
            ClampState();
            return Mathf.Max(0f, accepted);
        }

        public void ApplyRadiatorDamage(float amount, Vector3 worldHitPoint)
        {
            if (amount <= 0f)
            {
                return;
            }

            radiatorHealth = Mathf.Max(0f, radiatorHealth - amount);
            if (flightController != null && radiatorHealth < leakStartsBelowHealth)
            {
                flightController.ShowCockpitMessage(
                    $"Radiator damaged — coolant leak. Radiator condition {radiatorHealth:F0}%.",
                    4f);
            }
            UpdateLeakEffect();
        }

        public void RestoreRadiatorForNewAircraft()
        {
            radiatorHealth = 100f;
            coolantLiters = coolantCapacityLiters;
            coolantTemperatureC = ambientTemperatureC;
            doorOpenFraction = 0.05f;
            warningShown = false;
            ApplyDoorVisual(true);
            UpdateLeakEffect();
        }

        public string GetServiceReading()
        {
            string leak = IsLeaking ? "LEAKING" : "sealed";
            return $"Coolant {coolantLiters:F1}/{coolantCapacityLiters:F0} L | "
                + $"Temp {coolantTemperatureC:F0} C | Radiator {radiatorHealth:F0}% ({leak})";
        }

        private void ProcessCoolantLeak(float dt)
        {
            if (!IsLeaking)
            {
                return;
            }

            float leakSeverity = 1f - Mathf.InverseLerp(0f, leakStartsBelowHealth, radiatorHealth);
            float leakRate = maximumLeakLitersPerSecond
                * Mathf.Lerp(0.08f, 1f, leakSeverity * leakSeverity);
            coolantLiters = Mathf.Max(0f, coolantLiters - leakRate * dt);
        }

        private void ProcessThermalState(float dt)
        {
            bool running = EngineRunning;
            float throttle = running && flightController != null
                ? Mathf.Clamp01(flightController.Throttle)
                : 0f;
            float airspeed = flightController != null
                ? flightController.AirspeedMetersPerSecond
                : 0f;

            if (!running)
            {
                float passiveRate = Mathf.Lerp(0.015f, 0.10f, doorOpenFraction);
                coolantTemperatureC = Mathf.MoveTowards(
                    coolantTemperatureC,
                    ambientTemperatureC,
                    passiveRate * dt);
                overheating = coolantTemperatureC >= overheatWarningC;
                return;
            }

            float heat = Mathf.Lerp(idleHeatCPerSecond, fullPowerHeatCPerSecond, throttle);
            float coolantEfficiency = Mathf.Lerp(0.08f, 1f, Mathf.InverseLerp(0.03f, 0.45f, CoolantFraction));
            float radiatorEfficiency = Mathf.Lerp(0.12f, 1f, radiatorHealth / 100f);
            float ramFraction = Mathf.Clamp01(airspeed / fullRamAirSpeedMetersPerSecond);
            float temperatureHead = Mathf.Clamp01((coolantTemperatureC - ambientTemperatureC) / 80f);
            float cooling = (baseCoolingCPerSecond
                    + doorOpenFraction * doorCoolingCPerSecond
                    + doorOpenFraction * ramFraction * ramAirCoolingCPerSecond)
                * coolantEfficiency
                * radiatorEfficiency
                * Mathf.Lerp(0.25f, 1.25f, temperatureHead);

            coolantTemperatureC += (heat - cooling) * dt;
            coolantTemperatureC = Mathf.Clamp(coolantTemperatureC, ambientTemperatureC, 180f);
            overheating = coolantTemperatureC >= overheatWarningC;

            if (overheating && !warningShown && flightController != null)
            {
                warningShown = true;
                flightController.ShowCockpitMessage(
                    $"COOLANT HOT — {coolantTemperatureC:F0} C. Radiator door opening.",
                    4f);
            }
            else if (warningShown && coolantTemperatureC <= overheatWarningC - 8f)
            {
                warningShown = false;
            }
        }

        private void ProcessOverheatDamage(float dt)
        {
            if (!EngineRunning || coolantTemperatureC < engineDamageStartsC)
            {
                return;
            }

            EngineConditionController condition = ResolveInstalledEngineCondition();
            if (condition == null
                || BlockHealthField == null
                || CoverHealthField == null
                || RecalculateConditionMethod == null
                || RefreshConditionVisualsMethod == null)
            {
                return;
            }

            float severity = Mathf.InverseLerp(engineDamageStartsC, 155f, coolantTemperatureC);
            float blockRate = Mathf.Lerp(mildBlockDamagePerMinute, severeBlockDamagePerMinute, severity);
            float blockHealth = (float)BlockHealthField.GetValue(condition);
            blockHealth = Mathf.Max(0f, blockHealth - blockRate * (dt / 60f));
            BlockHealthField.SetValue(condition, blockHealth);

            float[] coverHealth = CoverHealthField.GetValue(condition) as float[];
            if (coverHealth != null && coolantTemperatureC >= severeOverheatC)
            {
                float coverSeverity = Mathf.InverseLerp(severeOverheatC, 160f, coolantTemperatureC);
                float coverDamage = severeCoverDamagePerMinute * coverSeverity * (dt / 60f);
                for (int index = 0; index < coverHealth.Length; index++)
                {
                    coverHealth[index] = Mathf.Max(0f, coverHealth[index] - coverDamage);
                }
            }

            RecalculateConditionMethod.Invoke(condition, null);
            RefreshConditionVisualsMethod.Invoke(condition, new object[] { true });

            if (blockHealth <= 0.1f && flightController != null)
            {
                flightController.ShowCockpitMessage(
                    "Merlin engine block failed from severe overheating.",
                    5f);
            }
        }

        private void UpdateDoor(float dt)
        {
            float target;
            if (!EngineRunning)
            {
                target = coolantTemperatureC > 65f ? 0.15f : 0.05f;
            }
            else
            {
                target = Mathf.Lerp(
                    0.05f,
                    1f,
                    Mathf.InverseLerp(doorStartsOpeningC, doorFullyOpenC, coolantTemperatureC));
            }

            doorOpenFraction = Mathf.MoveTowards(
                doorOpenFraction,
                target,
                doorTravelPerSecond * dt);
            ApplyDoorVisual(false);
        }

        private void ApplyDoorVisual(bool immediate)
        {
            if (radiatorExitDoorPivot == null)
            {
                return;
            }

            Vector3 euler = Vector3.Lerp(doorClosedLocalEuler, doorOpenLocalEuler, doorOpenFraction);
            radiatorExitDoorPivot.localRotation = Quaternion.Euler(euler);
        }

        private void UpdateLeakEffect()
        {
            if (coolantLeakEffect == null)
            {
                return;
            }

            bool shouldPlay = IsLeaking;
            var emission = coolantLeakEffect.emission;
            float severity = 1f - Mathf.InverseLerp(0f, leakStartsBelowHealth, radiatorHealth);
            emission.rateOverTime = shouldPlay ? Mathf.Lerp(6f, 38f, severity) : 0f;
            if (shouldPlay && !coolantLeakEffect.isPlaying)
            {
                coolantLeakEffect.Play();
            }
            else if (!shouldPlay && coolantLeakEffect.isPlaying)
            {
                coolantLeakEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private EngineConditionController ResolveInstalledEngineCondition()
        {
            if (flightController == null
                || flightController.EngineReceiver == null
                || flightController.EngineReceiver.InstalledTransport == null)
            {
                return null;
            }

            return flightController.EngineReceiver.InstalledTransport
                .GetComponent<EngineConditionController>();
        }

        private void ResolveReferences()
        {
            if (flightController == null)
            {
                flightController = GetComponent<P51FlightController>();
            }
        }

        private void ClampState()
        {
            coolantCapacityLiters = Mathf.Max(1f, coolantCapacityLiters);
            coolantLiters = Mathf.Clamp(coolantLiters, 0f, coolantCapacityLiters);
            radiatorHealth = Mathf.Clamp(radiatorHealth, 0f, 100f);
            coolantTemperatureC = Mathf.Max(ambientTemperatureC, coolantTemperatureC);
            doorOpenFraction = Mathf.Clamp01(doorOpenFraction);
        }

        private void OnValidate()
        {
            leakStartsBelowHealth = Mathf.Clamp(leakStartsBelowHealth, 0f, 100f);
            maximumLeakLitersPerSecond = Mathf.Max(0f, maximumLeakLitersPerSecond);
            doorFullyOpenC = Mathf.Max(doorStartsOpeningC + 1f, doorFullyOpenC);
            overheatWarningC = Mathf.Max(doorStartsOpeningC, overheatWarningC);
            engineDamageStartsC = Mathf.Max(overheatWarningC + 1f, engineDamageStartsC);
            severeOverheatC = Mathf.Max(engineDamageStartsC + 1f, severeOverheatC);
            fullRamAirSpeedMetersPerSecond = Mathf.Max(1f, fullRamAirSpeedMetersPerSecond);
            ClampState();
        }
    }
}
