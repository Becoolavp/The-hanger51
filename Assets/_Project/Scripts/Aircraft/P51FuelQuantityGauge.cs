using UnityEngine;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(40)]
    [DisallowMultipleComponent]
    public sealed class P51FuelQuantityGauge : MonoBehaviour
    {
        [Header("Fuel Source")]
        [SerializeField] private P51FuelSystem fuelSystem;

        [Header("Gauge Visuals")]
        [SerializeField] private Transform needlePivot;
        [SerializeField] private TextMesh gallonReadout;
        [SerializeField] private TextMesh percentReadout;
        [SerializeField] private float emptyNeedleDegrees = 110f;
        [SerializeField] private float fullNeedleDegrees = -110f;

        public P51FuelSystem FuelSystem => fuelSystem;
        public float DisplayedGallons => fuelSystem != null ? fuelSystem.TotalFuelGallons : 0f;
        public float DisplayedCapacityGallons => fuelSystem != null ? fuelSystem.TotalCapacityGallons : 0f;
        public float DisplayedFraction => DisplayedCapacityGallons > 0.001f
            ? Mathf.Clamp01(DisplayedGallons / DisplayedCapacityGallons)
            : 0f;
        public bool IsConfigured => fuelSystem != null
            && needlePivot != null
            && gallonReadout != null;

        private void Awake()
        {
            ResolveFuelSystem();
            RefreshGauge();
        }

        private void OnEnable()
        {
            ResolveFuelSystem();
            RefreshGauge();
        }

        private void Update()
        {
            ResolveFuelSystem();
            RefreshGauge();
        }

        public void Configure(
            P51FuelSystem configuredFuelSystem,
            Transform configuredNeedlePivot,
            TextMesh configuredGallonReadout,
            TextMesh configuredPercentReadout)
        {
            fuelSystem = configuredFuelSystem;
            needlePivot = configuredNeedlePivot;
            gallonReadout = configuredGallonReadout;
            percentReadout = configuredPercentReadout;
            RefreshGauge();
        }

        public void RefreshGauge()
        {
            float gallons = DisplayedGallons;
            float capacity = DisplayedCapacityGallons;
            float fraction = DisplayedFraction;

            if (needlePivot != null)
            {
                float needleAngle = Mathf.Lerp(emptyNeedleDegrees, fullNeedleDegrees, fraction);
                needlePivot.localRotation = Quaternion.Euler(0f, 0f, needleAngle);
            }

            if (gallonReadout != null)
            {
                gallonReadout.text = capacity > 0.001f
                    ? $"{gallons:0.0} / {capacity:0} GAL"
                    : "-- GAL";
            }

            if (percentReadout != null)
            {
                percentReadout.text = capacity > 0.001f
                    ? $"{fraction * 100f:0}%"
                    : "--%";
            }
        }

        private void ResolveFuelSystem()
        {
            if (fuelSystem == null)
            {
                fuelSystem = GetComponentInParent<P51FuelSystem>();
            }
        }

        private void OnValidate()
        {
            if (Mathf.Abs(fullNeedleDegrees - emptyNeedleDegrees) < 10f)
            {
                fullNeedleDegrees = emptyNeedleDegrees - 220f;
            }
        }
    }
}
