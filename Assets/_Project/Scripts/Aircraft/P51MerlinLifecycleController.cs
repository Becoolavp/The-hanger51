using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(220)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(P51FlightController))]
    public sealed class P51MerlinLifecycleController : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float startupSeconds = 3.2f;
        [SerializeField, Min(0.5f)] private float shutdownSeconds = 2.2f;

        private P51FlightController flightController;
        private FieldInfo engineRunningField;
        private FieldInfo throttleField;
        private int phase; // 0 stopped, 1 starting, 2 running, 3 stopping
        private float phaseTime;
        private float shutdownThrottle;
        private float lastRunningThrottle;
        private float transitionPropellerAngle;
        private bool reflectionReady;
        private bool reflectionErrorLogged;

        public bool IsStopped => phase == 0;
        public bool IsStarting => phase == 1;
        public bool IsRunning => phase == 2;
        public bool IsStopping => phase == 3;
        public float StartupDuration => Mathf.Max(0.5f, startupSeconds);
        public float ShutdownDuration => Mathf.Max(0.5f, shutdownSeconds);
        public float TransitionNormalized => phase == 1
            ? Mathf.Clamp01(phaseTime / StartupDuration)
            : phase == 3
                ? Mathf.Clamp01(phaseTime / ShutdownDuration)
                : phase == 2 ? 1f : 0f;

        public void Configure(float configuredStartupSeconds, float configuredShutdownSeconds)
        {
            startupSeconds = Mathf.Max(0.5f, configuredStartupSeconds);
            shutdownSeconds = Mathf.Max(0.5f, configuredShutdownSeconds);
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
            phase = flightController != null && flightController.EngineRunning ? 2 : 0;
            lastRunningThrottle = flightController != null ? flightController.Throttle : 0f;
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (flightController != null && flightController.EngineRunning && phase == 0)
            {
                phase = 2;
            }
        }

        private void Update()
        {
            ResolveReferences();
            if (flightController == null || !reflectionReady)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            bool startStopPressed = keyboard != null && keyboard.tKey.wasPressedThisFrame;

            if (!flightController.EngineInstalled)
            {
                if (phase != 0)
                {
                    ForceEngineState(false, 0f);
                    phase = 0;
                    phaseTime = 0f;
                }
                return;
            }

            if (phase == 0)
            {
                // P51FlightController processes T earlier in the frame. Intercept its instant
                // start and replace it with a real cranking/ignition sequence.
                if (startStopPressed && flightController.EngineRunning)
                {
                    BeginStartup();
                }
                else if (flightController.EngineRunning)
                {
                    phase = 2;
                }
            }
            else if (phase == 1)
            {
                ForceEngineState(false, 0f);
                phaseTime += Time.deltaTime;
                if (phaseTime >= StartupDuration)
                {
                    phase = 2;
                    phaseTime = StartupDuration;
                    ForceEngineState(true, 0f);
                    flightController.ShowCockpitMessage(
                        "Merlin caught and settled at idle. Q increases throttle; Z decreases throttle.",
                        3.5f);
                }
            }
            else if (phase == 2)
            {
                if (startStopPressed && !flightController.EngineRunning)
                {
                    BeginShutdown();
                }
                else if (!flightController.EngineRunning)
                {
                    phase = 0;
                    phaseTime = 0f;
                }
                else
                {
                    lastRunningThrottle = flightController.Throttle;
                }
            }
            else
            {
                phaseTime += Time.deltaTime;
                float t = Mathf.Clamp01(phaseTime / ShutdownDuration);
                float decayingThrottle = shutdownThrottle * (1f - t);

                if (t < 1f)
                {
                    ForceEngineState(true, decayingThrottle);
                }
                else
                {
                    ForceEngineState(false, 0f);
                    phase = 0;
                    phaseTime = 0f;
                    flightController.ShowCockpitMessage("Merlin stopped.", 2.5f);
                }
            }
        }

        private void LateUpdate()
        {
            if (flightController == null || flightController.PropellerRoot == null)
            {
                return;
            }

            float rpm = 0f;
            if (phase == 1)
            {
                float p = TransitionNormalized;
                rpm = p < 0.72f
                    ? Mathf.Lerp(95f, 430f, p / 0.72f)
                    : Mathf.Lerp(430f, 700f, (p - 0.72f) / 0.28f);
            }
            else if (phase == 3)
            {
                float initialRpm = Mathf.Lerp(700f, 2500f, shutdownThrottle);
                rpm = Mathf.Lerp(initialRpm, 0f, TransitionNormalized);
            }
            else
            {
                return;
            }

            transitionPropellerAngle = Mathf.Repeat(
                transitionPropellerAngle + rpm * 6f * Time.deltaTime,
                360f);
            flightController.PropellerRoot.localRotation = Quaternion.Euler(
                0f,
                0f,
                transitionPropellerAngle);
        }

        private void BeginStartup()
        {
            phase = 1;
            phaseTime = 0f;
            shutdownThrottle = 0f;
            ForceEngineState(false, 0f);
            flightController.ShowCockpitMessage(
                "Starter engaged — Merlin cranking...",
                StartupDuration + 0.5f);
        }

        private void BeginShutdown()
        {
            phase = 3;
            phaseTime = 0f;
            shutdownThrottle = Mathf.Clamp01(lastRunningThrottle);
            ForceEngineState(true, shutdownThrottle);
            flightController.ShowCockpitMessage(
                "Mixture cut — Merlin winding down...",
                ShutdownDuration + 0.5f);
        }

        private void ForceEngineState(bool running, float throttle)
        {
            if (!reflectionReady || flightController == null)
            {
                return;
            }

            engineRunningField.SetValue(flightController, running);
            throttleField.SetValue(flightController, Mathf.Clamp01(throttle));
        }

        private void ResolveReferences()
        {
            if (flightController == null)
            {
                flightController = GetComponent<P51FlightController>();
            }

            if (reflectionReady || flightController == null)
            {
                return;
            }

            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            System.Type type = typeof(P51FlightController);
            engineRunningField = type.GetField("engineRunning", flags);
            throttleField = type.GetField("throttle", flags);
            reflectionReady = engineRunningField != null && throttleField != null;

            if (!reflectionReady && !reflectionErrorLogged)
            {
                reflectionErrorLogged = true;
                Debug.LogError(
                    "P-51 Merlin lifecycle could not bind the flight-controller engine fields. Startup/shutdown timing is disabled.",
                    this);
            }
        }

        private void OnValidate()
        {
            startupSeconds = Mathf.Max(0.5f, startupSeconds);
            shutdownSeconds = Mathf.Max(0.5f, shutdownSeconds);
        }
    }
}
