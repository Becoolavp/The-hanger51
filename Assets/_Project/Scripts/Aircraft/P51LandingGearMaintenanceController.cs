using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Aircraft
{
    public enum P51LandingGearStation
    {
        LeftMain = 0,
        RightMain = 1,
        Tail = 2
    }

    [DefaultExecutionOrder(40)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(P51FlightController))]
    [RequireComponent(typeof(P51RaycastLandingGear))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class P51LandingGearMaintenanceController : MonoBehaviour
    {
        private const int WheelCount = 3;

        [Header("References")]
        [SerializeField] private P51FlightController flightController;
        [SerializeField] private P51RaycastLandingGear physicsGear;
        [SerializeField] private Rigidbody aircraftBody;
        [SerializeField] private Transform[] gearVisualRoots = new Transform[WheelCount];
        [SerializeField] private Transform[] tireVisualRoots = new Transform[WheelCount];
        [SerializeField] private Transform[] rimVisualRoots = new Transform[WheelCount];
        [SerializeField] private Transform[] mountBoltVisuals = new Transform[WheelCount];
        [SerializeField] private Transform[] serviceValveTargets = new Transform[WheelCount];

        [Header("Retraction")]
        [SerializeField, Min(0.5f)] private float gearCycleSeconds = 2.4f;
        [SerializeField] private bool gearCommandDown = true;
        [SerializeField, Range(0f, 1f)] private float deploymentFraction = 1f;
        [SerializeField] private Vector3[] deployedLocalPositions = new Vector3[WheelCount];
        [SerializeField] private Vector3[] deployedLocalEulers = new Vector3[WheelCount];
        [SerializeField] private Vector3[] retractedLocalPositions = new Vector3[WheelCount];
        [SerializeField] private Vector3[] retractedLocalEulers = new Vector3[WheelCount];

        [Header("Tire State")]
        [SerializeField] private float[] tireHealth = { 100f, 100f, 100f };
        [SerializeField] private float[] tirePressurePsi = { 30f, 30f, 24f };
        [SerializeField] private float[] properPressurePsi = { 30f, 30f, 24f };
        [SerializeField] private float[] burstPressurePsi = { 43f, 43f, 35f };
        [SerializeField] private bool[] gearInstalled = { true, true, true };
        [SerializeField] private bool[] tireInstalled = { true, true, true };
        [SerializeField] private bool[] tireBurst = { false, false, false };

        [Header("Landing Damage")]
        [SerializeField, Min(0f)] private float damageBeginsDownwardSpeed = 2.4f;
        [SerializeField, Min(0f)] private float damagePerMeterPerSecond = 8f;
        [SerializeField, Min(0f)] private float severeLandingSpeed = 6.5f;
        [SerializeField, Min(1f)] private float lowPressureDamageMultiplier = 2.6f;

        private readonly bool[] previousLoaded = new bool[WheelCount];
        private readonly Vector3[] originalTireScale = new Vector3[WheelCount];
        private GameObject[] looseGearObjects = new GameObject[WheelCount];
        private GameObject[] looseTireObjects = new GameObject[WheelCount];
        private float previousDownwardSpeed;
        private bool scalesCaptured;

        public bool GearCommandDown => gearCommandDown;
        public float DeploymentFraction => deploymentFraction;
        public string GearStatusText => deploymentFraction >= 0.98f
            ? "DOWN"
            : deploymentFraction <= 0.02f
                ? "UP"
                : gearCommandDown ? "EXTENDING" : "RETRACTING";

        private void Awake()
        {
            ResolveReferences();
            EnsureArrays();
            CaptureTireScales();
            ApplyVisualState(true);
            PushPhysicsState();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureArrays();
            CaptureTireScales();
            ApplyVisualState(true);
            PushPhysicsState();
        }

        public void Configure(
            P51FlightController configuredFlight,
            P51RaycastLandingGear configuredPhysics,
            Rigidbody configuredBody,
            Transform[] configuredGearRoots,
            Transform[] configuredTireRoots,
            Transform[] configuredRimRoots,
            Transform[] configuredBoltVisuals,
            Transform[] configuredValveTargets,
            Vector3[] configuredDeployedPositions,
            Vector3[] configuredDeployedEulers,
            Vector3[] configuredRetractedPositions,
            Vector3[] configuredRetractedEulers)
        {
            flightController = configuredFlight;
            physicsGear = configuredPhysics;
            aircraftBody = configuredBody;
            gearVisualRoots = CopyTransformArray(configuredGearRoots);
            tireVisualRoots = CopyTransformArray(configuredTireRoots);
            rimVisualRoots = CopyTransformArray(configuredRimRoots);
            mountBoltVisuals = CopyTransformArray(configuredBoltVisuals);
            serviceValveTargets = CopyTransformArray(configuredValveTargets);
            deployedLocalPositions = CopyVectorArray(configuredDeployedPositions);
            deployedLocalEulers = CopyVectorArray(configuredDeployedEulers);
            retractedLocalPositions = CopyVectorArray(configuredRetractedPositions);
            retractedLocalEulers = CopyVectorArray(configuredRetractedEulers);
            EnsureArrays();
            scalesCaptured = false;
            CaptureTireScales();
            gearCommandDown = true;
            deploymentFraction = 1f;
            ApplyVisualState(true);
            PushPhysicsState();
        }

        private void Update()
        {
            ResolveReferences();
            EnsureArrays();

            Keyboard keyboard = Keyboard.current;
            if (flightController != null
                && flightController.PilotPresent
                && keyboard != null
                && keyboard.gKey.wasPressedThisFrame)
            {
                gearCommandDown = !gearCommandDown;
                flightController.ShowCockpitMessage(
                    gearCommandDown ? "Landing gear EXTENDING." : "Landing gear RETRACTING.",
                    2.2f);
            }

            float target = gearCommandDown ? 1f : 0f;
            deploymentFraction = Mathf.MoveTowards(
                deploymentFraction,
                target,
                Time.deltaTime / Mathf.Max(0.5f, gearCycleSeconds));
            ApplyVisualState(false);
            PushPhysicsState();
        }

        private void FixedUpdate()
        {
            ResolveReferences();
            EnsureArrays();
            if (aircraftBody == null || physicsGear == null)
            {
                return;
            }

            bool[] loaded =
            {
                physicsGear.LeftMainLoaded,
                physicsGear.RightMainLoaded,
                physicsGear.TailwheelLoaded
            };

            for (int index = 0; index < WheelCount; index++)
            {
                if (loaded[index] && !previousLoaded[index])
                {
                    ApplyTouchdownDamage(index, previousDownwardSpeed);
                }
                previousLoaded[index] = loaded[index];
            }

            previousDownwardSpeed = Mathf.Max(0f, -aircraftBody.linearVelocity.y);
            PushPhysicsState();
        }

        public bool IsGearInstalled(int wheelIndex)
        {
            EnsureArrays();
            return IsValidWheel(wheelIndex) && gearInstalled[wheelIndex];
        }

        public bool IsTireInstalled(int wheelIndex)
        {
            EnsureArrays();
            return IsValidWheel(wheelIndex) && tireInstalled[wheelIndex];
        }

        public bool IsTireFailed(int wheelIndex)
        {
            EnsureArrays();
            return IsValidWheel(wheelIndex)
                && (tireBurst[wheelIndex] || tireHealth[wheelIndex] <= 0.01f);
        }

        public float GetTireHealth(int wheelIndex)
        {
            EnsureArrays();
            return IsValidWheel(wheelIndex) ? tireHealth[wheelIndex] : 0f;
        }

        public float GetTirePressure(int wheelIndex)
        {
            EnsureArrays();
            return IsValidWheel(wheelIndex) ? tirePressurePsi[wheelIndex] : 0f;
        }

        public float GetProperPressure(int wheelIndex)
        {
            EnsureArrays();
            return IsValidWheel(wheelIndex) ? properPressurePsi[wheelIndex] : 0f;
        }

        public Transform GetValveTarget(int wheelIndex)
        {
            EnsureArrays();
            return IsValidWheel(wheelIndex) ? serviceValveTargets[wheelIndex] : null;
        }

        public string GetWheelName(int wheelIndex)
        {
            switch (wheelIndex)
            {
                case 0: return "left main";
                case 1: return "right main";
                default: return "tail";
            }
        }

        public string GetInspectionText(int wheelIndex)
        {
            if (!IsValidWheel(wheelIndex))
            {
                return "Landing gear station is invalid.";
            }

            string tireState = !tireInstalled[wheelIndex]
                ? "TIRE REMOVED"
                : IsTireFailed(wheelIndex)
                    ? "TIRE DESTROYED"
                    : tireHealth[wheelIndex] <= 35f
                        ? "BADLY WORN"
                        : tireHealth[wheelIndex] <= 70f
                            ? "WORN"
                            : "SERVICEABLE";
            string gearState = gearInstalled[wheelIndex]
                ? "gear installed"
                : "gear removed";
            return $"{GetWheelName(wheelIndex)} gear: {gearState} | Tire: {tireState}, {tireHealth[wheelIndex]:F0}% health, {tirePressurePsi[wheelIndex]:F1} PSI | Correct pressure: {properPressurePsi[wheelIndex]:F0} PSI";
        }

        public bool CanService(out string reason)
        {
            reason = string.Empty;
            if (flightController != null && flightController.PilotPresent)
            {
                reason = "Exit the cockpit before servicing landing gear.";
                return false;
            }
            if (deploymentFraction < 0.96f)
            {
                reason = "Extend the landing gear fully before servicing it.";
                return false;
            }
            if (aircraftBody != null
                && Vector3.ProjectOnPlane(aircraftBody.linearVelocity, Vector3.up).magnitude > 1.2f)
            {
                reason = "Stop the aircraft before servicing landing gear.";
                return false;
            }
            return true;
        }

        public bool TryRemoveGear(int wheelIndex, out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!IsValidWheel(wheelIndex) || !CanService(out resultMessage))
            {
                return false;
            }
            if (!gearInstalled[wheelIndex])
            {
                resultMessage = $"The {GetWheelName(wheelIndex)} gear assembly is already removed.";
                return false;
            }

            CreateLooseGearObject(wheelIndex);
            gearInstalled[wheelIndex] = false;
            ApplyVisualState(true);
            PushPhysicsState();
            resultMessage = $"Removed the large mounting bolt and pulled the {GetWheelName(wheelIndex)} landing-gear assembly from the aircraft. Its tire condition and pressure were preserved.";
            return true;
        }

        public bool TryInstallGear(int wheelIndex, out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!IsValidWheel(wheelIndex) || !CanService(out resultMessage))
            {
                return false;
            }
            if (gearInstalled[wheelIndex])
            {
                resultMessage = $"The {GetWheelName(wheelIndex)} gear assembly is already installed.";
                return false;
            }
            if (looseGearObjects[wheelIndex] == null)
            {
                resultMessage = $"The removed {GetWheelName(wheelIndex)} gear assembly is not nearby.";
                return false;
            }

            Destroy(looseGearObjects[wheelIndex]);
            looseGearObjects[wheelIndex] = null;
            gearInstalled[wheelIndex] = true;
            ApplyVisualState(true);
            PushPhysicsState();
            resultMessage = $"Reinstalled the {GetWheelName(wheelIndex)} landing-gear assembly and secured its large mounting bolt.";
            return true;
        }

        public bool TryRemoveTire(int wheelIndex, out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!IsValidWheel(wheelIndex) || !CanService(out resultMessage))
            {
                return false;
            }
            if (!gearInstalled[wheelIndex])
            {
                resultMessage = "Reinstall the landing-gear assembly before removing its tire from the rim.";
                return false;
            }
            if (!tireInstalled[wheelIndex])
            {
                resultMessage = $"The {GetWheelName(wheelIndex)} tire is already off the rim.";
                return false;
            }

            CreateLooseTireObject(wheelIndex);
            tireInstalled[wheelIndex] = false;
            ApplyVisualState(true);
            PushPhysicsState();
            resultMessage = $"Removed the {GetWheelName(wheelIndex)} tire from its rim. Tire health and pressure remain with that exact tire.";
            return true;
        }

        public bool TryInstallTire(int wheelIndex, out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!IsValidWheel(wheelIndex) || !CanService(out resultMessage))
            {
                return false;
            }
            if (!gearInstalled[wheelIndex])
            {
                resultMessage = "Reinstall the landing-gear assembly before fitting its tire.";
                return false;
            }
            if (tireInstalled[wheelIndex])
            {
                resultMessage = $"The {GetWheelName(wheelIndex)} tire is already installed.";
                return false;
            }
            if (looseTireObjects[wheelIndex] == null)
            {
                resultMessage = $"The removed {GetWheelName(wheelIndex)} tire is not nearby.";
                return false;
            }

            Destroy(looseTireObjects[wheelIndex]);
            looseTireObjects[wheelIndex] = null;
            tireInstalled[wheelIndex] = true;
            ApplyVisualState(true);
            PushPhysicsState();
            resultMessage = $"Mounted the same {GetWheelName(wheelIndex)} tire back onto its rim at {tirePressurePsi[wheelIndex]:F1} PSI and {tireHealth[wheelIndex]:F0}% health.";
            return true;
        }

        public bool ServicePressureToward(
            int wheelIndex,
            float regulatorPsi,
            float deltaTime,
            out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!IsValidWheel(wheelIndex) || !CanService(out resultMessage))
            {
                return false;
            }
            if (!gearInstalled[wheelIndex] || !tireInstalled[wheelIndex])
            {
                resultMessage = "Install the gear and tire before servicing pressure.";
                return false;
            }
            if (IsTireFailed(wheelIndex))
            {
                resultMessage = "The tire is destroyed and must be replaced; nitrogen cannot repair it.";
                return false;
            }

            float target = Mathf.Clamp(regulatorPsi, 0f, 80f);
            tirePressurePsi[wheelIndex] = Mathf.MoveTowards(
                tirePressurePsi[wheelIndex],
                target,
                Mathf.Max(0f, deltaTime) * 12f);

            if (tirePressurePsi[wheelIndex] >= burstPressurePsi[wheelIndex])
            {
                BurstTire(wheelIndex, "overpressure");
                resultMessage = $"BANG — the {GetWheelName(wheelIndex)} tire burst from overpressure at {tirePressurePsi[wheelIndex]:F1} PSI.";
                return true;
            }

            ApplyVisualState(false);
            PushPhysicsState();
            resultMessage = $"{GetWheelName(wheelIndex)} tire: {tirePressurePsi[wheelIndex]:F1} PSI | Setpoint {target:F1} PSI | Correct {properPressurePsi[wheelIndex]:F0} PSI";
            return true;
        }

        private void ApplyTouchdownDamage(int wheelIndex, float downwardSpeed)
        {
            if (!IsValidWheel(wheelIndex)
                || !gearInstalled[wheelIndex]
                || !tireInstalled[wheelIndex]
                || IsTireFailed(wheelIndex)
                || downwardSpeed <= damageBeginsDownwardSpeed)
            {
                return;
            }

            float damage = (downwardSpeed - damageBeginsDownwardSpeed)
                * damagePerMeterPerSecond;
            float pressureRatio = properPressurePsi[wheelIndex] > 0.1f
                ? tirePressurePsi[wheelIndex] / properPressurePsi[wheelIndex]
                : 1f;
            if (pressureRatio < 0.82f)
            {
                float lowSeverity = 1f - Mathf.InverseLerp(0.20f, 0.82f, pressureRatio);
                damage *= Mathf.Lerp(1.25f, lowPressureDamageMultiplier, lowSeverity);
            }
            else if (pressureRatio > 1.12f)
            {
                damage *= Mathf.Lerp(1.15f, 1.8f, Mathf.InverseLerp(1.12f, 1.40f, pressureRatio));
            }
            if (downwardSpeed >= severeLandingSpeed)
            {
                damage *= 1.45f;
            }

            tireHealth[wheelIndex] = Mathf.Max(0f, tireHealth[wheelIndex] - damage);
            if (tireHealth[wheelIndex] <= 0.01f)
            {
                BurstTire(wheelIndex, "impact damage");
            }
            ApplyVisualState(false);
            PushPhysicsState();
        }

        private void BurstTire(int wheelIndex, string reason)
        {
            tireBurst[wheelIndex] = true;
            tireHealth[wheelIndex] = 0f;
            if (flightController != null && flightController.PilotPresent)
            {
                flightController.ShowCockpitMessage(
                    $"{GetWheelName(wheelIndex).ToUpperInvariant()} TIRE FAILED — {reason}. Strong drag will remain on that wheel until repaired.",
                    5f);
            }
        }

        private void PushPhysicsState()
        {
            if (physicsGear == null)
            {
                return;
            }

            for (int index = 0; index < WheelCount; index++)
            {
                physicsGear.ApplyMaintenanceState(
                    index,
                    gearInstalled[index],
                    tireInstalled[index],
                    IsTireFailed(index),
                    tirePressurePsi[index],
                    properPressurePsi[index],
                    deploymentFraction);
            }
        }

        private void ApplyVisualState(bool immediate)
        {
            EnsureArrays();
            CaptureTireScales();
            float smooth = deploymentFraction * deploymentFraction
                * (3f - 2f * deploymentFraction);

            for (int index = 0; index < WheelCount; index++)
            {
                Transform root = gearVisualRoots[index];
                if (root != null)
                {
                    root.gameObject.SetActive(gearInstalled[index]);
                    root.localPosition = Vector3.Lerp(
                        retractedLocalPositions[index],
                        deployedLocalPositions[index],
                        smooth);
                    Quaternion retracted = Quaternion.Euler(retractedLocalEulers[index]);
                    Quaternion deployed = Quaternion.Euler(deployedLocalEulers[index]);
                    root.localRotation = Quaternion.Slerp(retracted, deployed, smooth);
                }

                if (rimVisualRoots[index] != null)
                {
                    rimVisualRoots[index].gameObject.SetActive(gearInstalled[index]);
                }
                if (mountBoltVisuals[index] != null)
                {
                    mountBoltVisuals[index].gameObject.SetActive(gearInstalled[index]);
                }
                if (tireVisualRoots[index] != null)
                {
                    tireVisualRoots[index].gameObject.SetActive(
                        gearInstalled[index] && tireInstalled[index]);
                    float pressureRatio = properPressurePsi[index] > 0.1f
                        ? tirePressurePsi[index] / properPressurePsi[index]
                        : 1f;
                    Vector3 scale = originalTireScale[index];
                    if (IsTireFailed(index))
                    {
                        scale = Vector3.Scale(scale, new Vector3(1.10f, 0.30f, 1.08f));
                    }
                    else if (pressureRatio < 0.82f)
                    {
                        float flatten = Mathf.Lerp(0.48f, 1f, Mathf.Clamp01(pressureRatio / 0.82f));
                        scale = Vector3.Scale(scale, new Vector3(1.03f, flatten, 1.03f));
                    }
                    tireVisualRoots[index].localScale = scale;
                }
            }
        }

        private void CreateLooseGearObject(int wheelIndex)
        {
            if (looseGearObjects[wheelIndex] != null)
            {
                Destroy(looseGearObjects[wheelIndex]);
            }
            Transform source = gearVisualRoots[wheelIndex];
            if (source == null)
            {
                return;
            }

            GameObject clone = Instantiate(source.gameObject);
            clone.name = $"Removed {GetWheelName(wheelIndex)} Landing Gear";
            clone.transform.SetParent(null, true);
            clone.transform.position = source.position
                + transform.right * (wheelIndex == 0 ? -1.0f : wheelIndex == 1 ? 1.0f : 0.5f)
                + transform.forward * 0.35f;
            clone.transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 78f);
            RemoveServiceScriptsAndColliders(clone);
            looseGearObjects[wheelIndex] = clone;
        }

        private void CreateLooseTireObject(int wheelIndex)
        {
            if (looseTireObjects[wheelIndex] != null)
            {
                Destroy(looseTireObjects[wheelIndex]);
            }
            Transform source = tireVisualRoots[wheelIndex];
            if (source == null)
            {
                return;
            }

            GameObject clone = Instantiate(source.gameObject);
            clone.name = $"Removed {GetWheelName(wheelIndex)} Tire — {tireHealth[wheelIndex]:F0}% — {tirePressurePsi[wheelIndex]:F1} PSI";
            clone.transform.SetParent(null, true);
            clone.transform.position = source.position
                + transform.right * (wheelIndex == 0 ? -0.75f : wheelIndex == 1 ? 0.75f : 0.45f)
                + Vector3.up * 0.12f;
            clone.transform.rotation = Quaternion.Euler(90f, transform.eulerAngles.y, 0f);
            RemoveServiceScriptsAndColliders(clone);
            looseTireObjects[wheelIndex] = clone;
        }

        private static void RemoveServiceScriptsAndColliders(GameObject root)
        {
            if (root == null)
            {
                return;
            }
            P51LandingGearServiceTarget[] targets =
                root.GetComponentsInChildren<P51LandingGearServiceTarget>(true);
            for (int index = 0; index < targets.Length; index++)
            {
                if (targets[index] != null) Destroy(targets[index]);
            }
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null) Destroy(colliders[index]);
            }
        }

        private void CaptureTireScales()
        {
            if (scalesCaptured)
            {
                return;
            }
            EnsureArrays();
            for (int index = 0; index < WheelCount; index++)
            {
                originalTireScale[index] = tireVisualRoots[index] != null
                    ? tireVisualRoots[index].localScale
                    : Vector3.one;
            }
            scalesCaptured = true;
        }

        private void ResolveReferences()
        {
            if (flightController == null) flightController = GetComponent<P51FlightController>();
            if (physicsGear == null) physicsGear = GetComponent<P51RaycastLandingGear>();
            if (aircraftBody == null) aircraftBody = GetComponent<Rigidbody>();
        }

        private void EnsureArrays()
        {
            gearVisualRoots = Resize(gearVisualRoots);
            tireVisualRoots = Resize(tireVisualRoots);
            rimVisualRoots = Resize(rimVisualRoots);
            mountBoltVisuals = Resize(mountBoltVisuals);
            serviceValveTargets = Resize(serviceValveTargets);
            deployedLocalPositions = Resize(deployedLocalPositions);
            deployedLocalEulers = Resize(deployedLocalEulers);
            retractedLocalPositions = Resize(retractedLocalPositions);
            retractedLocalEulers = Resize(retractedLocalEulers);
            tireHealth = Resize(tireHealth, 100f);
            tirePressurePsi = Resize(tirePressurePsi, 30f);
            properPressurePsi = Resize(properPressurePsi, 30f);
            burstPressurePsi = Resize(burstPressurePsi, 43f);
            gearInstalled = Resize(gearInstalled, true);
            tireInstalled = Resize(tireInstalled, true);
            tireBurst = Resize(tireBurst, false);

            properPressurePsi[0] = Mathf.Max(1f, properPressurePsi[0]);
            properPressurePsi[1] = Mathf.Max(1f, properPressurePsi[1]);
            properPressurePsi[2] = Mathf.Max(1f, properPressurePsi[2]);
            for (int index = 0; index < WheelCount; index++)
            {
                tireHealth[index] = Mathf.Clamp(tireHealth[index], 0f, 100f);
                tirePressurePsi[index] = Mathf.Clamp(tirePressurePsi[index], 0f, 80f);
                burstPressurePsi[index] = Mathf.Max(properPressurePsi[index] + 3f, burstPressurePsi[index]);
            }
        }

        private static bool IsValidWheel(int index) => index >= 0 && index < WheelCount;

        private static Transform[] CopyTransformArray(Transform[] source)
        {
            Transform[] result = new Transform[WheelCount];
            if (source != null) Array.Copy(source, result, Mathf.Min(source.Length, result.Length));
            return result;
        }

        private static Vector3[] CopyVectorArray(Vector3[] source)
        {
            Vector3[] result = new Vector3[WheelCount];
            if (source != null) Array.Copy(source, result, Mathf.Min(source.Length, result.Length));
            return result;
        }

        private static Transform[] Resize(Transform[] source)
        {
            return CopyTransformArray(source);
        }

        private static Vector3[] Resize(Vector3[] source)
        {
            return CopyVectorArray(source);
        }

        private static float[] Resize(float[] source, float defaultValue)
        {
            float[] result = new float[WheelCount];
            for (int index = 0; index < WheelCount; index++)
            {
                result[index] = source != null && index < source.Length
                    ? source[index]
                    : defaultValue;
            }
            return result;
        }

        private static bool[] Resize(bool[] source, bool defaultValue)
        {
            bool[] result = new bool[WheelCount];
            for (int index = 0; index < WheelCount; index++)
            {
                result[index] = source != null && index < source.Length
                    ? source[index]
                    : defaultValue;
            }
            return result;
        }

        private void OnValidate()
        {
            ResolveReferences();
            EnsureArrays();
            gearCycleSeconds = Mathf.Max(0.5f, gearCycleSeconds);
            damageBeginsDownwardSpeed = Mathf.Max(0f, damageBeginsDownwardSpeed);
            damagePerMeterPerSecond = Mathf.Max(0f, damagePerMeterPerSecond);
            severeLandingSpeed = Mathf.Max(damageBeginsDownwardSpeed, severeLandingSpeed);
            lowPressureDamageMultiplier = Mathf.Max(1f, lowPressureDamageMultiplier);
        }
    }
}
