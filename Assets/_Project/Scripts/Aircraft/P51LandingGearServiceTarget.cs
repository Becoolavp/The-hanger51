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
        [SerializeField] private P51LandingGearMaintenanceController controller;
        [SerializeField] private P51LandingGearServiceKind serviceKind;
        [SerializeField, Range(0, 2)] private int wheelIndex;
        [SerializeField, Min(0.2f)] private float holdDuration = 1.15f;

        [Header("Mount Bolt Animation")]
        [SerializeField] private Transform animatedBolt;
        [SerializeField, Min(0f)] private float boltExtractionDistance = 0.20f;
        [SerializeField, Min(0f)] private float boltRotationTurns = 2.5f;

        private P51LandingGearInventoryBridge inventoryBridge;
        private float holdProgress;
        private bool removing;
        private Vector3 boltInstalledLocalPosition;
        private Quaternion boltInstalledLocalRotation = Quaternion.identity;
        private bool boltPoseCaptured;

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

                if (controller.IsTireInstalled(wheelIndex))
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
            ResolveBoltVisual();
            CaptureBoltPose();
            ResetBoltPose();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ResolveBoltVisual();
            CaptureBoltPose();
            ResetBoltPose();
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
            ResolveBoltVisual();
            CaptureBoltPose();
            ResetBoltPose();
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
                valid = controller.IsGearInstalled(wheelIndex)
                    && (shouldRemove || shouldInstall);
            }

            if (!valid || !controller.CanService(out resultMessage))
            {
                CancelHold();
                return false;
            }

            if (holdProgress > 0f && removing != shouldRemove)
            {
                ResetBoltPose();
                holdProgress = 0f;
            }

            removing = shouldRemove;
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
            else if (removing && controller.IsTireInstalled(wheelIndex))
            {
                completed = inventoryBridge.TryRemoveTire(
                    wheelIndex,
                    inventory,
                    out resultMessage);
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
            else if (!removing && !controller.IsTireInstalled(wheelIndex))
            {
                completed = inventoryBridge.TryInstallTire(
                    wheelIndex,
                    inventory,
                    out resultMessage);
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
            string rimText = inventoryBridge != null
                ? inventoryBridge.GetRimInspectionText(wheelIndex)
                : "Rim inventory state unavailable";
            return $"{baseText} | {rimText}";
        }

        public void CancelHold()
        {
            holdProgress = 0f;
            removing = false;
            ResetBoltPose();
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

            animatedBolt.gameObject.SetActive(true);
            Vector3 extractionDirection =
                (boltInstalledLocalRotation * Vector3.up).normalized;
            Vector3 extractedPosition = boltInstalledLocalPosition
                + extractionDirection * boltExtractionDistance;
            animatedBolt.localPosition = Vector3.Lerp(
                isRemoving ? boltInstalledLocalPosition : extractedPosition,
                isRemoving ? extractedPosition : boltInstalledLocalPosition,
                Mathf.Clamp01(normalizedProgress));

            float spinDirection = isRemoving ? -1f : 1f;
            Quaternion spin = Quaternion.AngleAxis(
                360f * boltRotationTurns * Mathf.Clamp01(normalizedProgress) * spinDirection,
                Vector3.up);
            animatedBolt.localRotation = boltInstalledLocalRotation * spin;
        }

        private void ResetBoltPose()
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

            animatedBolt.localPosition = boltInstalledLocalPosition;
            animatedBolt.localRotation = boltInstalledLocalRotation;
            if (controller != null)
            {
                animatedBolt.gameObject.SetActive(controller.IsGearInstalled(wheelIndex));
            }
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

            boltInstalledLocalPosition = animatedBolt.localPosition;
            boltInstalledLocalRotation = animatedBolt.localRotation;
            boltPoseCaptured = true;
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
            ResetBoltPose();
        }

        private void OnValidate()
        {
            wheelIndex = Mathf.Clamp(wheelIndex, 0, 2);
            holdDuration = Mathf.Max(0.2f, holdDuration);
            boltExtractionDistance = Mathf.Max(0f, boltExtractionDistance);
            boltRotationTurns = Mathf.Max(0f, boltRotationTurns);
            ResolveReferences();
            ResolveBoltVisual();
            CaptureBoltPose();
        }
    }
}
