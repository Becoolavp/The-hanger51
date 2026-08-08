using System.Reflection;
using Hanger51.Inventory;
using UnityEngine;

namespace Hanger51.Aircraft
{
    public enum P51LandingGearServiceKind
    {
        MountBolt,
        TireAndValve
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class P51LandingGearServiceTarget : MonoBehaviour
    {
        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;

        [SerializeField] private P51LandingGearMaintenanceController controller;
        [SerializeField] private P51LandingGearServiceKind serviceKind;
        [SerializeField, Range(0, 2)] private int wheelIndex;
        [SerializeField, Min(0.2f)] private float holdDuration = 1.15f;

        [Header("Mount Bolt Animation")]
        [SerializeField] private Transform animatedBolt;
        [SerializeField, Min(0f)] private float boltExtractionDistance = 0.24f;
        [SerializeField, Min(0f)] private float boltRotationTurns = 3f;

        private P51LandingGearInventoryBridge inventoryBridge;
        private float holdProgress;
        private bool removing;
        private bool isHolding;

        private Vector3 boltInstalledWorldPosition;
        private Quaternion boltInstalledWorldRotation = Quaternion.identity;
        private bool boltPoseCaptured;

        private bool tireRemovalLatched;
        private Transform installedTireVisual;
        private FieldInfo tireInstalledField;
        private MethodInfo applyVisualStateMethod;
        private MethodInfo pushPhysicsStateMethod;

        public P51LandingGearMaintenanceController Controller
        {
            get
            {
                ResolveReferences();
                return controller;
            }
        }

        public P51LandingGearServiceKind ServiceKind => serviceKind;
        public int WheelIndex => wheelIndex;
        public Transform ServicePoint => transform;

        public string InteractionText
        {
            get
            {
                ResolveReferences();
                if (controller == null)
                {
                    return string.Empty;
                }

                string name = controller.GetWheelName(wheelIndex);
                if (!controller.CanService(out string reason))
                {
                    return $"{name} gear — {reason} | X inspect";
                }

                int percent = Mathf.RoundToInt(holdProgress * 100f);
                string progress = holdProgress > 0f ? $" ({percent}%)" : string.Empty;
                if (serviceKind == P51LandingGearServiceKind.MountBolt)
                {
                    return controller.IsGearInstalled(wheelIndex)
                        ? $"Hold R: unscrew large {name} gear mounting bolt and remove gear{progress} | X inspect"
                        : $"Hold E: reinstall {name} gear and screw in large mounting bolt{progress} | X inspect";
                }

                if (!controller.IsGearInstalled(wheelIndex))
                {
                    return $"{name} wheel station — reinstall the gear first | X inspect";
                }

                if (inventoryBridge == null || !inventoryBridge.IsReady)
                {
                    return $"{name} wheel station — inventory wheel service needs P-51 Step 30 | X inspect";
                }

                if (IsTirePresentForService())
                {
                    return $"Hold R: pull {name} tire off rim onto the floor{progress} | N: connect nitrogen cart | X inspect";
                }

                if (inventoryBridge.IsRimInstalled(wheelIndex))
                {
                    return $"Hold R: pull {name} rim off gear onto the floor | Equip correct tire + Hold E: install tire{progress} | X inspect";
                }

                return $"Equip correct {name} rim + Hold E: install rim{progress} | X inspect";
            }
        }

        private void Awake()
        {
            ResolveReferences();
            ResolveControllerStateBindings();
            ResolveBoltVisual();
            CaptureBoltPose();
            ResolveInstalledTireVisual();
            tireRemovalLatched = serviceKind == P51LandingGearServiceKind.TireAndValve
                && controller != null
                && !controller.IsTireInstalled(wheelIndex);
            ApplyStableVisualState();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ResolveControllerStateBindings();
            ResolveBoltVisual();
            CaptureBoltPose();
            ResolveInstalledTireVisual();
            ApplyStableVisualState();
        }

        private void LateUpdate()
        {
            ResolveReferences();
            if (serviceKind == P51LandingGearServiceKind.MountBolt)
            {
                if (!isHolding)
                {
                    ApplyStableBoltPose();
                }
                return;
            }

            if (tireRemovalLatched)
            {
                if (controller != null && controller.IsTireInstalled(wheelIndex))
                {
                    ForceControllerTireInstalled(false);
                }
                ForceInstalledTireVisible(false);
            }
        }

        public void Configure(
            P51LandingGearMaintenanceController configuredController,
            P51LandingGearServiceKind configuredKind,
            int configuredWheelIndex,
            float configuredHoldDuration)
        {
            controller = configuredController;
            serviceKind = configuredKind;
            wheelIndex = Mathf.Clamp(configuredWheelIndex, 0, 2);
            holdDuration = Mathf.Max(0.2f, configuredHoldDuration);
            ResolveReferences();
            ResolveControllerStateBindings();
            ResolveBoltVisual();
            CaptureBoltPose();
            ResolveInstalledTireVisual();
            ApplyStableVisualState();
        }

        public bool ProcessInteraction(
            PlayerInventory inventory,
            bool holdInstall,
            bool holdRemove,
            float deltaTime,
            out string resultMessage)
        {
            resultMessage = string.Empty;
            ResolveReferences();
            if (controller == null)
            {
                CancelHold();
                return false;
            }

            bool shouldRemove = holdRemove && !holdInstall;
            bool shouldInstall = holdInstall && !holdRemove;
            bool valid;
            if (serviceKind == P51LandingGearServiceKind.MountBolt)
            {
                valid = shouldRemove
                    ? controller.IsGearInstalled(wheelIndex)
                    : shouldInstall && !controller.IsGearInstalled(wheelIndex);
            }
            else
            {
                bool tirePresent = IsTirePresentForService();
                bool rimPresent = inventoryBridge != null
                    && inventoryBridge.IsReady
                    && inventoryBridge.IsRimInstalled(wheelIndex);
                valid = controller.IsGearInstalled(wheelIndex)
                    && (shouldRemove
                        ? tirePresent || (!tirePresent && rimPresent)
                        : shouldInstall && (!rimPresent || !tirePresent));
            }

            if (!valid || !controller.CanService(out resultMessage))
            {
                CancelHold();
                return false;
            }

            if (holdProgress > 0f && removing != shouldRemove)
            {
                holdProgress = 0f;
                isHolding = false;
                ApplyStableVisualState();
            }

            removing = shouldRemove;
            isHolding = true;
            holdProgress = Mathf.Clamp01(
                holdProgress + Mathf.Max(0f, deltaTime) / holdDuration);

            if (serviceKind == P51LandingGearServiceKind.MountBolt)
            {
                ApplyBoltAnimatedPose(holdProgress, removing);
            }

            if (holdProgress < 1f)
            {
                return false;
            }

            bool completed;
            if (serviceKind == P51LandingGearServiceKind.MountBolt)
            {
                completed = removing
                    ? controller.TryRemoveGear(wheelIndex, out resultMessage)
                    : controller.TryInstallGear(wheelIndex, out resultMessage);
            }
            else if (inventoryBridge == null || !inventoryBridge.IsReady)
            {
                completed = false;
                resultMessage = "Wheel inventory service is not configured. Run P-51 Step 30.";
            }
            else if (removing && IsTirePresentForService())
            {
                completed = inventoryBridge.TryRemoveTire(
                    wheelIndex,
                    inventory,
                    out resultMessage);
                if (completed)
                {
                    tireRemovalLatched = true;
                    ForceControllerTireInstalled(false);
                    ForceInstalledTireVisible(false);
                }
            }
            else if (removing && inventoryBridge.IsRimInstalled(wheelIndex))
            {
                completed = inventoryBridge.TryRemoveRim(
                    wheelIndex,
                    inventory,
                    out resultMessage);
            }
            else if (!removing && !inventoryBridge.IsRimInstalled(wheelIndex))
            {
                completed = inventoryBridge.TryInstallRim(
                    wheelIndex,
                    inventory,
                    out resultMessage);
            }
            else if (!removing && !IsTirePresentForService())
            {
                completed = inventoryBridge.TryInstallTire(
                    wheelIndex,
                    inventory,
                    out resultMessage);
                if (completed)
                {
                    tireRemovalLatched = false;
                    ForceControllerTireInstalled(true);
                    ForceInstalledTireVisible(true);
                }
            }
            else
            {
                completed = false;
                resultMessage = "That wheel service action is not available in the current configuration.";
            }

            CancelHold();
            return completed;
        }

        public string Inspect()
        {
            ResolveReferences();
            if (controller == null)
            {
                return "Landing gear condition controller is missing.";
            }

            string baseText = controller.GetInspectionText(wheelIndex);
            if (serviceKind == P51LandingGearServiceKind.TireAndValve
                && tireRemovalLatched)
            {
                baseText = $"{controller.GetWheelName(wheelIndex)} gear: gear installed | Tire: TIRE REMOVED | Correct pressure: {controller.GetProperPressure(wheelIndex):F0} PSI";
            }

            string rimText = inventoryBridge != null
                ? inventoryBridge.GetRimInspectionText(wheelIndex)
                : "Rim inventory state unavailable";
            return $"{baseText} | {rimText}";
        }

        public void CancelHold()
        {
            holdProgress = 0f;
            removing = false;
            isHolding = false;
            ApplyStableVisualState();
        }

        private bool IsTirePresentForService()
        {
            return serviceKind == P51LandingGearServiceKind.TireAndValve
                && !tireRemovalLatched
                && controller != null
                && controller.IsTireInstalled(wheelIndex);
        }

        private void ApplyStableVisualState()
        {
            if (serviceKind == P51LandingGearServiceKind.MountBolt)
            {
                ApplyStableBoltPose();
            }
            else if (tireRemovalLatched)
            {
                ForceControllerTireInstalled(false);
                ForceInstalledTireVisible(false);
            }
        }

        private void ApplyBoltAnimatedPose(float normalizedProgress, bool isRemoving)
        {
            if (serviceKind != P51LandingGearServiceKind.MountBolt)
            {
                return;
            }

            ResolveBoltVisual();
            CaptureBoltPose();
            if (animatedBolt == null || !boltPoseCaptured)
            {
                return;
            }

            float t = Mathf.Clamp01(normalizedProgress);
            Vector3 extractedPosition = GetBoltExtractedWorldPosition();
            Vector3 startPosition = isRemoving
                ? boltInstalledWorldPosition
                : extractedPosition;
            Vector3 endPosition = isRemoving
                ? extractedPosition
                : boltInstalledWorldPosition;

            animatedBolt.gameObject.SetActive(true);
            animatedBolt.position = Vector3.Lerp(startPosition, endPosition, t);

            float spinDirection = isRemoving ? -1f : 1f;
            Quaternion spin = Quaternion.AngleAxis(
                360f * boltRotationTurns * t * spinDirection,
                transform.up);
            animatedBolt.rotation = spin * boltInstalledWorldRotation;
        }

        private void ApplyStableBoltPose()
        {
            if (serviceKind != P51LandingGearServiceKind.MountBolt)
            {
                return;
            }

            ResolveBoltVisual();
            CaptureBoltPose();
            if (animatedBolt == null || !boltPoseCaptured)
            {
                return;
            }

            bool installed = controller == null || controller.IsGearInstalled(wheelIndex);
            animatedBolt.gameObject.SetActive(true);
            animatedBolt.SetPositionAndRotation(
                installed ? boltInstalledWorldPosition : GetBoltExtractedWorldPosition(),
                boltInstalledWorldRotation);
        }

        private Vector3 GetBoltExtractedWorldPosition()
        {
            return boltInstalledWorldPosition
                + transform.up * Mathf.Max(0f, boltExtractionDistance);
        }

        private void ResolveBoltVisual()
        {
            if (serviceKind != P51LandingGearServiceKind.MountBolt || animatedBolt != null)
            {
                return;
            }

            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < all.Length; index++)
            {
                Transform candidate = all[index];
                if (candidate != null
                    && candidate != transform
                    && candidate.name.Contains("Large Mount Bolt"))
                {
                    animatedBolt = candidate;
                    break;
                }
            }
        }

        private void CaptureBoltPose()
        {
            if (boltPoseCaptured
                || serviceKind != P51LandingGearServiceKind.MountBolt
                || animatedBolt == null)
            {
                return;
            }

            boltInstalledWorldPosition = animatedBolt.position;
            boltInstalledWorldRotation = animatedBolt.rotation;
            boltPoseCaptured = true;
        }

        private void ResolveInstalledTireVisual()
        {
            if (serviceKind != P51LandingGearServiceKind.TireAndValve
                || installedTireVisual != null
                || controller == null)
            {
                return;
            }

            string expectedName = wheelIndex == 0
                ? "Left Main Tire Visual"
                : wheelIndex == 1
                    ? "Right Main Tire Visual"
                    : "Tailwheel Tire Visual";
            Transform[] all = controller.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < all.Length; index++)
            {
                if (all[index] != null && all[index].name == expectedName)
                {
                    installedTireVisual = all[index];
                    break;
                }
            }
        }

        private void ForceInstalledTireVisible(bool visible)
        {
            ResolveInstalledTireVisual();
            if (installedTireVisual != null)
            {
                installedTireVisual.gameObject.SetActive(visible);
            }

            Renderer[] valveRenderers = GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < valveRenderers.Length; index++)
            {
                if (valveRenderers[index] != null)
                {
                    valveRenderers[index].enabled = visible;
                }
            }
        }

        private void ResolveControllerStateBindings()
        {
            if (controller == null)
            {
                return;
            }

            System.Type type = typeof(P51LandingGearMaintenanceController);
            tireInstalledField = type.GetField("tireInstalled", PrivateInstance);
            applyVisualStateMethod = type.GetMethod("ApplyVisualState", PrivateInstance);
            pushPhysicsStateMethod = type.GetMethod("PushPhysicsState", PrivateInstance);
        }

        private void ForceControllerTireInstalled(bool installed)
        {
            ResolveReferences();
            ResolveControllerStateBindings();
            if (controller == null || tireInstalledField == null)
            {
                return;
            }

            bool[] states = tireInstalledField.GetValue(controller) as bool[];
            if (states == null || wheelIndex < 0 || wheelIndex >= states.Length)
            {
                return;
            }

            states[wheelIndex] = installed;
            tireInstalledField.SetValue(controller, states);
            applyVisualStateMethod?.Invoke(controller, new object[] { true });
            pushPhysicsStateMethod?.Invoke(controller, null);
        }

        private void ResolveReferences()
        {
            if (controller == null)
            {
                controller = GetComponentInParent<P51LandingGearMaintenanceController>();
            }
            if (inventoryBridge == null)
            {
                inventoryBridge = GetComponentInParent<P51LandingGearInventoryBridge>();
            }
        }

        private void OnDisable()
        {
            holdProgress = 0f;
            removing = false;
            isHolding = false;
        }

        private void OnValidate()
        {
            wheelIndex = Mathf.Clamp(wheelIndex, 0, 2);
            holdDuration = Mathf.Max(0.2f, holdDuration);
            boltExtractionDistance = Mathf.Max(0f, boltExtractionDistance);
            boltRotationTurns = Mathf.Max(0f, boltRotationTurns);
            ResolveReferences();
            ResolveControllerStateBindings();
            ResolveBoltVisual();
            CaptureBoltPose();
        }
    }
}
