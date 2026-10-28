using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(P51FlightController))]
    public sealed class P51AirspeedWarningDisplay : MonoBehaviour
    {
        [Header("Wings-Level Thresholds")]
        [SerializeField, Min(1f)] private float redThresholdKnots = 58f;
        [SerializeField, Min(1f)] private float orangeThresholdKnots = 74f;
        [SerializeField, Min(1f)] private float yellowThresholdKnots = 92f;

        [Header("Bank Compensation")]
        [SerializeField, Range(0f, 40f)] private float maximumBankThresholdIncreaseKnots = 20f;
        [SerializeField, Range(0f, 70f)] private float bankIncreaseStartsDegrees = 30f;
        [SerializeField, Range(35f, 89f)] private float fullBankIncreaseDegrees = 70f;

        private P51FlightController flightController;
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle numberStyle;
        private GUIStyle stateStyle;

        public float RedThresholdKnots => redThresholdKnots;
        public float OrangeThresholdKnots => orangeThresholdKnots;
        public float YellowThresholdKnots => yellowThresholdKnots;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        public void Configure(
            P51FlightController configuredController,
            float configuredRedThreshold,
            float configuredOrangeThreshold,
            float configuredYellowThreshold,
            float configuredMaximumBankIncrease)
        {
            flightController = configuredController;
            redThresholdKnots = Mathf.Max(1f, configuredRedThreshold);
            orangeThresholdKnots = Mathf.Max(
                redThresholdKnots + 1f,
                configuredOrangeThreshold);
            yellowThresholdKnots = Mathf.Max(
                orangeThresholdKnots + 1f,
                configuredYellowThreshold);
            maximumBankThresholdIncreaseKnots = Mathf.Clamp(
                configuredMaximumBankIncrease,
                0f,
                40f);
            ResolveReferences();
        }

        private void OnGUI()
        {
            ResolveReferences();
            if (flightController == null || !flightController.PilotPresent)
            {
                return;
            }

            EnsureStyles();

            float bankDegrees = CalculateBankDegrees();
            float bankFactor = Mathf.InverseLerp(
                bankIncreaseStartsDegrees,
                fullBankIncreaseDegrees,
                bankDegrees);
            float bankIncrease = maximumBankThresholdIncreaseKnots * bankFactor;

            float adjustedRed = redThresholdKnots + bankIncrease * 0.55f;
            float adjustedOrange = orangeThresholdKnots + bankIncrease * 0.78f;
            float adjustedYellow = yellowThresholdKnots + bankIncrease;
            float airspeed = flightController.AirspeedKnots;

            Color speedColor;
            string state;
            if (flightController.IsGrounded)
            {
                speedColor = new Color(0.72f, 0.76f, 0.82f, 1f);
                state = "GROUND";
            }
            else if (airspeed <= adjustedRed)
            {
                speedColor = new Color(1f, 0.20f, 0.16f, 1f);
                state = "STALL RISK";
            }
            else if (airspeed <= adjustedOrange)
            {
                speedColor = new Color(1f, 0.48f, 0.08f, 1f);
                state = "LOW SPEED";
            }
            else if (airspeed <= adjustedYellow)
            {
                speedColor = new Color(1f, 0.88f, 0.18f, 1f);
                state = "CAUTION";
            }
            else
            {
                speedColor = new Color(0.30f, 1f, 0.42f, 1f);
                state = "SAFE";
            }

            numberStyle.normal.textColor = speedColor;
            stateStyle.normal.textColor = speedColor;

            Rect panelRect = new Rect(383f, 18f, 190f, 112f);
            GUI.Box(panelRect, GUIContent.none, panelStyle);
            GUI.Label(new Rect(395f, 25f, 166f, 22f), "AIRSPEED", titleStyle);
            GUI.Label(
                new Rect(395f, 43f, 166f, 40f),
                $"{airspeed:F0} KT",
                numberStyle);
            GUI.Label(
                new Rect(395f, 81f, 166f, 22f),
                state,
                stateStyle);

            if (!flightController.IsGrounded && bankFactor > 0.05f)
            {
                GUI.Label(
                    new Rect(395f, 99f, 166f, 18f),
                    $"BANK {bankDegrees:F0}°",
                    titleStyle);
            }
        }

        private float CalculateBankDegrees()
        {
            float rightUpDot = Mathf.Clamp(
                Vector3.Dot(transform.right, Vector3.up),
                -1f,
                1f);
            return Mathf.Abs(Mathf.Asin(rightUpDot) * Mathf.Rad2Deg);
        }

        private void EnsureStyles()
        {
            if (panelStyle == null)
            {
                panelStyle = new GUIStyle(GUI.skin.box);
            }

            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 13,
                    fontStyle = FontStyle.Bold
                };
                titleStyle.normal.textColor = Color.white;
            }

            if (numberStyle == null)
            {
                numberStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 27,
                    fontStyle = FontStyle.Bold
                };
            }

            if (stateStyle == null)
            {
                stateStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14,
                    fontStyle = FontStyle.Bold
                };
            }
        }

        private void ResolveReferences()
        {
            if (flightController == null)
            {
                flightController = GetComponent<P51FlightController>();
            }
        }

        private void OnValidate()
        {
            redThresholdKnots = Mathf.Max(1f, redThresholdKnots);
            orangeThresholdKnots = Mathf.Max(
                redThresholdKnots + 1f,
                orangeThresholdKnots);
            yellowThresholdKnots = Mathf.Max(
                orangeThresholdKnots + 1f,
                yellowThresholdKnots);
            maximumBankThresholdIncreaseKnots = Mathf.Clamp(
                maximumBankThresholdIncreaseKnots,
                0f,
                40f);
            bankIncreaseStartsDegrees = Mathf.Clamp(
                bankIncreaseStartsDegrees,
                0f,
                70f);
            fullBankIncreaseDegrees = Mathf.Clamp(
                fullBankIncreaseDegrees,
                bankIncreaseStartsDegrees + 1f,
                89f);
        }
    }
}
