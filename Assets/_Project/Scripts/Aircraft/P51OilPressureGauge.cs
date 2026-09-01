using Hanger51.EngineAssembly;
using UnityEngine;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(46)]
    [DisallowMultipleComponent]
    public sealed class P51OilPressureGauge : MonoBehaviour
    {
        [SerializeField] private P51FlightController flightController;
        [SerializeField] private P51RadiatorCoolingSystem coolingSystem;
        [SerializeField] private Transform needlePivot;
        [SerializeField] private TextMesh pressureReadout;
        [SerializeField] private TextMesh statusReadout;
        [SerializeField] private float zeroNeedleDegrees = 110f;
        [SerializeField] private float maximumNeedleDegrees = -110f;
        [SerializeField] private float maximumDisplayedPsi = 100f;

        public P51FlightController FlightController => flightController;
        public P51RadiatorCoolingSystem CoolingSystem => coolingSystem;
        public EngineConditionController InstalledEngineCondition => ResolveInstalledEngineCondition();
        public float DisplayedPressurePsi => CalculateOilPressurePsi();
        public bool IsConfigured => flightController != null && coolingSystem != null && needlePivot != null && pressureReadout != null;

        private void Awake()
        {
            ResolveReferences();
            RefreshGauge();
        }

        private void OnEnable()
        {
            ResolveReferences();
            RefreshGauge();
        }

        private void Update()
        {
            ResolveReferences();
            RefreshGauge();
        }

        public void Configure(
            P51FlightController configuredFlightController,
            P51RadiatorCoolingSystem configuredCoolingSystem,
            Transform configuredNeedlePivot,
            TextMesh configuredPressureReadout,
            TextMesh configuredStatusReadout)
        {
            flightController = configuredFlightController;
            coolingSystem = configuredCoolingSystem;
            needlePivot = configuredNeedlePivot;
            pressureReadout = configuredPressureReadout;
            statusReadout = configuredStatusReadout;
            RefreshGauge();
        }

        public void RefreshGauge()
        {
            float pressure = DisplayedPressurePsi;
            float normalized = Mathf.Clamp01(pressure / Mathf.Max(1f, maximumDisplayedPsi));

            if (needlePivot != null)
            {
                float angle = Mathf.Lerp(zeroNeedleDegrees, maximumNeedleDegrees, normalized);
                needlePivot.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            if (pressureReadout != null)
            {
                pressureReadout.text = $"{pressure:0} PSI";
            }

            if (statusReadout == null)
            {
                return;
            }

            EngineConditionController condition = InstalledEngineCondition;
            if (flightController == null || !flightController.EngineRunning)
            {
                statusReadout.text = "ENGINE OFF";
                statusReadout.color = Color.white;
            }
            else if (condition == null)
            {
                statusReadout.text = "NO ENGINE";
                statusReadout.color = Color.red;
            }
            else if (pressure < 25f)
            {
                statusReadout.text = "LOW PRESS";
                statusReadout.color = Color.red;
            }
            else if (pressure < 40f || condition.OilQuantityLiters < condition.SafeMinimumOilLiters)
            {
                statusReadout.text = "CAUTION";
                statusReadout.color = new Color(1f, 0.55f, 0.05f);
            }
            else
            {
                statusReadout.text = "NORMAL";
                statusReadout.color = Color.green;
            }
        }

        private float CalculateOilPressurePsi()
        {
            EngineConditionController condition = InstalledEngineCondition;
            if (flightController == null || !flightController.EngineRunning || condition == null)
            {
                return 0f;
            }

            float basePressure = Mathf.Lerp(48f, 78f, Mathf.Clamp01(flightController.Throttle));
            float safeMinimum = Mathf.Max(0.1f, condition.SafeMinimumOilLiters);
            float oilToSafeRatio = Mathf.Clamp01(condition.OilQuantityLiters / safeMinimum);
            float oilFactor = condition.OilQuantityLiters >= safeMinimum
                ? Mathf.Lerp(0.96f, 1.04f, Mathf.InverseLerp(safeMinimum, condition.OilCapacityLiters, condition.OilQuantityLiters))
                : Mathf.Pow(oilToSafeRatio, 1.55f);

            return Mathf.Clamp(basePressure * oilFactor, 0f, maximumDisplayedPsi);
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
                flightController = GetComponentInParent<P51FlightController>();
            }
            if (coolingSystem == null)
            {
                coolingSystem = GetComponentInParent<P51RadiatorCoolingSystem>();
            }
        }
    }
}
