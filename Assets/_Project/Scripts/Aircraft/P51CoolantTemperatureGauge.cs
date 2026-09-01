using UnityEngine;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(45)]
    [DisallowMultipleComponent]
    public sealed class P51CoolantTemperatureGauge : MonoBehaviour
    {
        [SerializeField] private P51RadiatorCoolingSystem coolingSystem;
        [SerializeField] private Transform needlePivot;
        [SerializeField] private TextMesh temperatureReadout;
        [SerializeField] private TextMesh statusReadout;
        [SerializeField] private float minimumDisplayC = 40f;
        [SerializeField] private float maximumDisplayC = 140f;
        [SerializeField] private float coldNeedleDegrees = 110f;
        [SerializeField] private float hotNeedleDegrees = -110f;

        public P51RadiatorCoolingSystem CoolingSystem => coolingSystem;
        public float DisplayedTemperatureC => coolingSystem != null ? coolingSystem.CoolantTemperatureC : 0f;
        public bool IsConfigured => coolingSystem != null && needlePivot != null && temperatureReadout != null;

        private void Awake()
        {
            ResolveCoolingSystem();
            RefreshGauge();
        }

        private void OnEnable()
        {
            ResolveCoolingSystem();
            RefreshGauge();
        }

        private void Update()
        {
            ResolveCoolingSystem();
            RefreshGauge();
        }

        public void Configure(
            P51RadiatorCoolingSystem configuredCoolingSystem,
            Transform configuredNeedlePivot,
            TextMesh configuredTemperatureReadout,
            TextMesh configuredStatusReadout)
        {
            coolingSystem = configuredCoolingSystem;
            needlePivot = configuredNeedlePivot;
            temperatureReadout = configuredTemperatureReadout;
            statusReadout = configuredStatusReadout;
            RefreshGauge();
        }

        public void RefreshGauge()
        {
            float temperature = DisplayedTemperatureC;
            float normalized = Mathf.InverseLerp(minimumDisplayC, maximumDisplayC, temperature);

            if (needlePivot != null)
            {
                float angle = Mathf.Lerp(coldNeedleDegrees, hotNeedleDegrees, normalized);
                needlePivot.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            if (temperatureReadout != null)
            {
                temperatureReadout.text = coolingSystem != null ? $"{temperature:0} C" : "-- C";
            }

            if (statusReadout == null)
            {
                return;
            }

            if (coolingSystem == null)
            {
                statusReadout.text = "--";
                statusReadout.color = Color.white;
            }
            else if (temperature >= 135f)
            {
                statusReadout.text = "CRITICAL";
                statusReadout.color = Color.red;
            }
            else if (temperature >= 115f)
            {
                statusReadout.text = "OVERHEAT";
                statusReadout.color = Color.red;
            }
            else if (temperature >= 108f || coolingSystem.CoolantIsHot)
            {
                statusReadout.text = "HOT";
                statusReadout.color = new Color(1f, 0.55f, 0.05f);
            }
            else if (temperature < 65f)
            {
                statusReadout.text = "COLD";
                statusReadout.color = Color.white;
            }
            else
            {
                statusReadout.text = "NORMAL";
                statusReadout.color = Color.green;
            }
        }

        private void ResolveCoolingSystem()
        {
            if (coolingSystem == null)
            {
                coolingSystem = GetComponentInParent<P51RadiatorCoolingSystem>();
            }
        }
    }
}
