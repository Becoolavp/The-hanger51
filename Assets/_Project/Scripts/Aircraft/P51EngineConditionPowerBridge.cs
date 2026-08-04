using Hanger51.EngineAssembly;
using UnityEngine;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(80)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(P51FlightController))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class P51EngineConditionPowerBridge : MonoBehaviour
    {
        [SerializeField] private P51FlightController flightController;
        [SerializeField] private Rigidbody aircraftBody;
        [SerializeField, Min(1000f)] private float configuredMaximumThrustNewtons = 24000f;
        [SerializeField, Min(0.1f)] private float conditionRefreshInterval = 0.15f;

        private EngineConditionController activeCondition;
        private float nextConditionRefreshTime;
        private bool severeWarningShown;

        public EngineConditionController ActiveCondition => activeCondition;
        public float AvailablePowerMultiplier => activeCondition != null
            ? activeCondition.PowerMultiplier
            : 1f;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        public void Configure(float maximumThrustNewtons)
        {
            configuredMaximumThrustNewtons = Mathf.Max(1000f, maximumThrustNewtons);
            ResolveReferences();
        }

        private void FixedUpdate()
        {
            ResolveReferences();
            RefreshConditionReference();

            if (activeCondition != null)
            {
                activeCondition.SetOperatingState(
                    flightController != null && flightController.EngineRunning,
                    flightController != null ? flightController.Throttle : 0f);
            }

            if (flightController == null
                || aircraftBody == null
                || aircraftBody.isKinematic
                || !flightController.EngineRunning
                || activeCondition == null)
            {
                severeWarningShown = false;
                return;
            }

            float multiplier = Mathf.Clamp01(activeCondition.PowerMultiplier);
            if (flightController.Throttle > 0f && multiplier < 0.999f)
            {
                float speedFactor = Mathf.Lerp(
                    1f,
                    0.72f,
                    Mathf.InverseLerp(
                        0f,
                        160f,
                        flightController.AirspeedMetersPerSecond));
                float unavailableThrust = configuredMaximumThrustNewtons
                    * flightController.Throttle
                    * speedFactor
                    * (1f - multiplier);
                aircraftBody.AddForce(
                    -transform.forward * unavailableThrust,
                    ForceMode.Force);
            }

            if (!severeWarningShown
                && activeCondition.RoughRunningSeverity > 0.68f)
            {
                severeWarningShown = true;
                flightController.ShowCockpitMessage(
                    "ENGINE ROUGH — reduce power and inspect oil, plugs, block, and covers.",
                    5f);
            }
            else if (activeCondition.RoughRunningSeverity < 0.45f)
            {
                severeWarningShown = false;
            }
        }

        private void RefreshConditionReference()
        {
            if (flightController == null || Time.time < nextConditionRefreshTime)
            {
                return;
            }

            nextConditionRefreshTime = Time.time
                + Mathf.Max(0.05f, conditionRefreshInterval);
            EngineConditionController resolved = null;
            if (flightController.EngineReceiver != null
                && flightController.EngineReceiver.InstalledTransport != null)
            {
                resolved = flightController.EngineReceiver.InstalledTransport
                    .GetComponent<EngineConditionController>();
            }

            if (resolved == activeCondition)
            {
                return;
            }

            if (activeCondition != null)
            {
                activeCondition.SetOperatingState(false, 0f);
            }
            activeCondition = resolved;
            severeWarningShown = false;
        }

        private void ResolveReferences()
        {
            if (flightController == null)
            {
                flightController = GetComponent<P51FlightController>();
            }
            if (aircraftBody == null)
            {
                aircraftBody = GetComponent<Rigidbody>();
            }
        }

        private void OnDisable()
        {
            if (activeCondition != null)
            {
                activeCondition.SetOperatingState(false, 0f);
            }
            activeCondition = null;
        }

        private void OnValidate()
        {
            configuredMaximumThrustNewtons = Mathf.Max(
                1000f,
                configuredMaximumThrustNewtons);
            conditionRefreshInterval = Mathf.Max(0.05f, conditionRefreshInterval);
            ResolveReferences();
        }
    }
}
