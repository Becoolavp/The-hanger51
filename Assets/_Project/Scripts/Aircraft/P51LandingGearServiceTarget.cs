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

        [Header("Gear Mount Bolt Animation")]
        [SerializeField] private Transform animatedBolt;
        [SerializeField, Min(0f)] private float boltExtractionDistance = 0.24f;
        [SerializeField, Min(0f)] private float boltRotationTurns = 3f;

        [Header("Wheel Retaining Bolt and Install Highlight")]
        [SerializeField] private Transform wheelRetainingBolt;
        [SerializeField] private GameObject installHighlightRoot;
        [SerializeField, Min(0f)] private float wheelBoltExtractionDistance = 0.24f;
        [SerializeField, Min(0f)] private float wheelBoltRotationTurns = 3f;

        private P51LandingGearInventoryBridge inventoryBridge;
        private PlayerInventory playerInventory;
        private float holdProgress;
        private bool removing;
        private bool isHolding;

        private Vector3 boltInstalledWorldPosition;
        private Quaternion boltInstalledWorldRotation = Quaternion.identity;
        private bool boltPoseCaptured;

        private Vector3 wheelBoltInstalledLocalPosition;
        private Quaternion wheelBoltInstalledLocalRotation = Quaternion.identity;
        private bool wheelBoltPoseCaptured;

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

                bool rimPresent = inventoryBridge.IsRimInstalled(wheelIndex);
                bool tirePresent = IsTirePresentForService();
                if (rimPresent && tirePresent)
                {
                    return $"Hold R: unscrew visible wheel retaining bolt and pull complete {name} tire + rim off axle{progress} | N nitrogen | X inspect";
                }

                if (!rimPresent)
                {
                    return inventoryBridge.HasCorrectEquippedRim(wheelIndex, playerInventory)
                        ? $"Hold E: install equipped {name} rim at highlighted axle{progress} | X inspect"
                        : $"Equip the correct {name} rim to highlight its axle install point | X inspect";
                }

                if (!tirePresent)
                {
                    return inventoryBridge.HasCorrectEquippedTire(wheelIndex, playerInventory)
                        ? $"Hold E: fit equipped {name} tire and screw wheel retaining bolt in{progress} | Hold R: remove bare rim | X inspect"
                        : $"Equip the correct {name} tire to highlight this rim | Hold R: remove bare rim | X inspect";
                }

                return $"{name} wheel station | X inspect";
            }
        }

        private void Awake()
        {
            ResolveReferences();
            ResolveControllerStateBindings();
            ResolveBoltVisual();
            CaptureBoltPose();
            ResolveInstalledTireVisual();
            EnsureWheelHardware();
            tireRemovalLatched = serviceKind == P51LandingGearServiceKind.TireAndValve
                && controller != null
                && !controller.IsTireInstalled(wheelIndex);
            ApplyStableVisualState();
            UpdateInstallHighlight();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ResolveControllerStateBindings();
            ResolveBoltVisual();
            CaptureBoltPose();
            ResolveInstalledTireVisual();
            EnsureWheelHardware();
            ApplyStableVisualState();
            UpdateInstallHighlight();
        }

        private void LateUpdate()
        {
            ResolveReferences();
            EnsureWheelHardware();

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

            if (!isHolding)
            {
                ApplyStableWheelBoltPose();
            }
            UpdateInstallHighlight();
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
            EnsureWheelHardware();
            ApplyStableVisualState();
            UpdateInstallHighlight();
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
            if (inventory != null)
            {
                playerInventory = inventory;
            }
            if (controller == null)
            {
                CancelHold();
                return false;
            }

            bool shouldRemove = holdRemove && !holdInstall;
            bool shouldInstall = holdInstall && !holdRemove;
            bool rimPresent = inventoryBridge != null
                && inventoryBridge.IsReady
                && inventoryBridge.IsRimInstalled(wheelIndex);
            bool tirePresent = IsTirePresentForService();

            bool valid;
            if (serviceKind == P51LandingGearServiceKind.MountBolt)
            {
                valid = shouldRemove
                    ? controller.IsGearInstalled(wheelIndex)
                    : shouldInstall && !controller.IsGearInstalled(wheelIndex);
            }
            else
            {
                bool validRemove = shouldRemove
                    && controller.IsGearInstalled(wheelIndex)
                    && ((rimPresent && tirePresent) || (rimPresent && !tirePresent));
                bool validInstall = shouldInstall
                    && controller.IsGearInstalled(wheelIndex)
                    && ((!rimPresent
                            && inventoryBridge != null
                            && inventoryBridge.HasCorrectEquippedRim(wheelIndex, playerInventory))
                        || (rimPresent
                            && !tirePresent
                            && inventoryBridge != null
                            && inventoryBridge.HasCorrectEquippedTire(wheelIndex, playerInventory)));
                valid = validRemove || validInstall;
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
            else if (rimPresent && tirePresent && shouldRemove)
            {
                ApplyWheelBoltAnimatedPose(holdProgress, true);
            }
            else if (rimPresent && !tirePresent && shouldInstall)
            {
                ApplyWheelBoltAnimatedPose(holdProgress, false);
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
            else if (removing && rimPresent && tirePresent)
            {
                completed = inventoryBridge.TryRemoveWheelAssembly(
                    wheelIndex,
                    out resultMessage);
                if (completed)
                {
                    tireRemovalLatched = true;
                    ForceControllerTireInstalled(false);
                    ForceInstalledTireVisible(false);
                }
            }
            else if (removing && rimPresent && !tirePresent)
            {
                completed = inventoryBridge.TryRemoveRim(
                    wheelIndex,
                    inventory,
                    out resultMessage);
            }
            else if (!removing && !rimPresent)
            {
                completed = inventoryBridge.TryInstallRim(
                    wheelIndex,
                    inventory,
                    out resultMessage);
            }
            else if (!removing && rimPresent && !tirePresent)
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
            UpdateInstallHighlight();
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
                return;
            }

            if (tireRemovalLatched)
            {
                ForceControllerTireInstalled(false);
                ForceInstalledTireVisible(false);
            }
            ApplyStableWheelBoltPose();
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

        private void ApplyWheelBoltAnimatedPose(float normalizedProgress, bool isRemoving)
        {
            EnsureWheelHardware();
            if (wheelRetainingBolt == null || !wheelBoltPoseCaptured)
            {
                return;
            }

            float t = Mathf.Clamp01(normalizedProgress);
            Vector3 extracted = GetWheelBoltExtractedLocalPosition();
            Vector3 start = isRemoving
                ? wheelBoltInstalledLocalPosition
                : extracted;
            Vector3 end = isRemoving
                ? extracted
                : wheelBoltInstalledLocalPosition;
            wheelRetainingBolt.gameObject.SetActive(true);
            wheelRetainingBolt.localPosition = Vector3.Lerp(start, end, t);

            float spinDirection = isRemoving ? -1f : 1f;
            Quaternion spin = Quaternion.AngleAxis(
                360f * wheelBoltRotationTurns * t * spinDirection,
                Vector3.right);
            wheelRetainingBolt.localRotation = wheelBoltInstalledLocalRotation * spin;
        }

        private void ApplyStableWheelBoltPose()
        {
            if (serviceKind != P51LandingGearServiceKind.TireAndValve)
            {
                return;
            }

            EnsureWheelHardware();
            if (wheelRetainingBolt == null || !wheelBoltPoseCaptured)
            {
                return;
            }

            bool completeWheel = controller != null
                && controller.IsGearInstalled(wheelIndex)
                && inventoryBridge != null
                && inventoryBridge.IsReady
                && inventoryBridge.IsRimInstalled(wheelIndex)
                && IsTirePresentForService();
            wheelRetainingBolt.gameObject.SetActive(
                controller == null || controller.IsGearInstalled(wheelIndex));
            wheelRetainingBolt.localPosition = completeWheel
                ? wheelBoltInstalledLocalPosition
                : GetWheelBoltExtractedLocalPosition();
            wheelRetainingBolt.localRotation = wheelBoltInstalledLocalRotation;
        }

        private Vector3 GetWheelBoltExtractedLocalPosition()
        {
            Vector3 outward = wheelIndex == 0 ? Vector3.left : Vector3.right;
            return wheelBoltInstalledLocalPosition
                + outward * Mathf.Max(0f, wheelBoltExtractionDistance);
        }

        private void EnsureWheelHardware()
        {
            if (serviceKind != P51LandingGearServiceKind.TireAndValve)
            {
                return;
            }

            if (wheelRetainingBolt == null)
            {
                Transform existing = FindDirectOrNested("Wheel Retaining Bolt");
                if (existing != null)
                {
                    wheelRetainingBolt = existing;
                }
                else
                {
                    GameObject bolt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    bolt.name = "Wheel Retaining Bolt";
                    bolt.transform.SetParent(transform, false);
                    bool tail = wheelIndex == 2;
                    float side = wheelIndex == 0 ? -1f : 1f;
                    bolt.transform.localPosition = new Vector3(
                        side * (tail ? 0.11f : 0.19f),
                        0f,
                        0f);
                    bolt.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    bolt.transform.localScale = tail
                        ? new Vector3(0.055f, 0.075f, 0.055f)
                        : new Vector3(0.080f, 0.105f, 0.080f);

                    Renderer renderer = bolt.GetComponent<Renderer>();
                    Material sourceMaterial = FindServiceHardwareMaterial();
                    if (renderer != null && sourceMaterial != null)
                    {
                        renderer.sharedMaterial = sourceMaterial;
                    }

                    Collider primitiveCollider = bolt.GetComponent<Collider>();
                    if (primitiveCollider != null)
                    {
                        Destroy(primitiveCollider);
                    }
                    wheelRetainingBolt = bolt.transform;
                }
            }

            if (wheelRetainingBolt != null && !wheelBoltPoseCaptured)
            {
                wheelBoltInstalledLocalPosition = wheelRetainingBolt.localPosition;
                wheelBoltInstalledLocalRotation = wheelRetainingBolt.localRotation;
                wheelBoltPoseCaptured = true;
            }

            EnsureInstallHighlight();
        }

        private void EnsureInstallHighlight()
        {
            if (installHighlightRoot != null)
            {
                return;
            }

            Transform existing = FindDirectOrNested("Wheel Install Highlight");
            if (existing != null)
            {
                installHighlightRoot = existing.gameObject;
                return;
            }

            GameObject root = new GameObject("Wheel Install Highlight");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;

            bool tail = wheelIndex == 2;
            float radius = tail ? 0.27f : 0.52f;
            float markerLength = tail ? 0.15f : 0.24f;
            float markerThickness = tail ? 0.025f : 0.035f;
            Material material = FindHighlightMaterial();

            CreateHighlightMarker(root.transform, new Vector3(0f, radius, 0f),
                new Vector3(markerThickness, markerLength, markerThickness), material);
            CreateHighlightMarker(root.transform, new Vector3(0f, -radius, 0f),
                new Vector3(markerThickness, markerLength, markerThickness), material);
            CreateHighlightMarker(root.transform, new Vector3(0f, 0f, radius),
                new Vector3(markerThickness, markerThickness, markerLength), material);
            CreateHighlightMarker(root.transform, new Vector3(0f, 0f, -radius),
                new Vector3(markerThickness, markerThickness, markerLength), material);
            CreateHighlightMarker(root.transform, new Vector3(0f, radius * 0.70f, radius * 0.70f),
                Vector3.one * markerThickness * 1.5f, material);
            CreateHighlightMarker(root.transform, new Vector3(0f, radius * 0.70f, -radius * 0.70f),
                Vector3.one * markerThickness * 1.5f, material);
            CreateHighlightMarker(root.transform, new Vector3(0f, -radius * 0.70f, radius * 0.70f),
                Vector3.one * markerThickness * 1.5f, material);
            CreateHighlightMarker(root.transform, new Vector3(0f, -radius * 0.70f, -radius * 0.70f),
                Vector3.one * markerThickness * 1.5f, material);

            installHighlightRoot = root;
            installHighlightRoot.SetActive(false);
        }

        private void UpdateInstallHighlight()
        {
            if (serviceKind != P51LandingGearServiceKind.TireAndValve)
            {
                return;
            }

            EnsureWheelHardware();
            ResolveReferences();
            if (installHighlightRoot == null)
            {
                return;
            }

            bool show = false;
            if (controller != null
                && controller.IsGearInstalled(wheelIndex)
                && inventoryBridge != null
                && inventoryBridge.IsReady
                && playerInventory != null)
            {
                bool rimPresent = inventoryBridge.IsRimInstalled(wheelIndex);
                bool tirePresent = IsTirePresentForService();
                show = !rimPresent
                    ? inventoryBridge.HasCorrectEquippedRim(wheelIndex, playerInventory)
                    : !tirePresent
                        && inventoryBridge.HasCorrectEquippedTire(wheelIndex, playerInventory);
            }

            installHighlightRoot.SetActive(show);
            if (show)
            {
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.045f;
                installHighlightRoot.transform.localScale = Vector3.one * pulse;
            }
            else
            {
                installHighlightRoot.transform.localScale = Vector3.one;
            }
        }

        private static void CreateHighlightMarker(
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Install Highlight Marker";
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = localPosition;
            marker.transform.localRotation = Quaternion.identity;
            marker.transform.localScale = localScale;
            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }
            Collider collider = marker.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
        }

        private Material FindServiceHardwareMaterial()
        {
            P51LandingGearServiceTarget[] targets =
                controller != null
                    ? controller.GetComponentsInChildren<P51LandingGearServiceTarget>(true)
                    : new P51LandingGearServiceTarget[0];
            for (int index = 0; index < targets.Length; index++)
            {
                if (targets[index] == null
                    || targets[index].serviceKind != P51LandingGearServiceKind.MountBolt)
                {
                    continue;
                }
                Renderer renderer = targets[index].GetComponentInChildren<Renderer>(true);
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    return renderer.sharedMaterial;
                }
            }
            return FindHighlightMaterial();
        }

        private Material FindHighlightMaterial()
        {
            Renderer[] localRenderers = GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < localRenderers.Length; index++)
            {
                Renderer renderer = localRenderers[index];
                if (renderer != null
                    && renderer.sharedMaterial != null
                    && renderer.name.ToLowerInvariant().Contains("valve"))
                {
                    return renderer.sharedMaterial;
                }
            }

            if (controller != null)
            {
                Renderer[] all = controller.GetComponentsInChildren<Renderer>(true);
                for (int index = 0; index < all.Length; index++)
                {
                    Renderer renderer = all[index];
                    if (renderer != null
                        && renderer.sharedMaterial != null
                        && renderer.name.ToLowerInvariant().Contains("mount bolt"))
                    {
                        return renderer.sharedMaterial;
                    }
                }
            }
            return null;
        }

        private Transform FindDirectOrNested(string objectName)
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < all.Length; index++)
            {
                if (all[index] != null && all[index].name == objectName)
                {
                    return all[index];
                }
            }
            return null;
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
                Renderer renderer = valveRenderers[index];
                if (renderer != null
                    && renderer.transform != wheelRetainingBolt
                    && (installHighlightRoot == null
                        || !renderer.transform.IsChildOf(installHighlightRoot.transform)))
                {
                    renderer.enabled = visible;
                }
            }
        }

        private void ResolveReferences()
        {
            if (controller == null)
            {
                controller = GetComponentInParent<P51LandingGearMaintenanceController>();
            }
            if (inventoryBridge == null && controller != null)
            {
                inventoryBridge = controller.GetComponent<P51LandingGearInventoryBridge>();
            }
            if (playerInventory == null)
            {
                playerInventory = FindFirstObjectByType<PlayerInventory>();
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
            applyVisualStateMethod?.Invoke(controller, new object[] { true });
            pushPhysicsStateMethod?.Invoke(controller, null);
        }

        private void OnDisable()
        {
            holdProgress = 0f;
            removing = false;
            isHolding = false;
            if (installHighlightRoot != null)
            {
                installHighlightRoot.SetActive(false);
            }
        }

        private void OnValidate()
        {
            wheelIndex = Mathf.Clamp(wheelIndex, 0, 2);
            holdDuration = Mathf.Max(0.2f, holdDuration);
            boltExtractionDistance = Mathf.Max(0f, boltExtractionDistance);
            boltRotationTurns = Mathf.Max(0f, boltRotationTurns);
            wheelBoltExtractionDistance = Mathf.Max(0f, wheelBoltExtractionDistance);
            wheelBoltRotationTurns = Mathf.Max(0f, wheelBoltRotationTurns);
            ResolveReferences();
            ResolveBoltVisual();
            CaptureBoltPose();
        }
    }
}
