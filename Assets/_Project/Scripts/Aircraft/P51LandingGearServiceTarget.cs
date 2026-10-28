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
        private float holdProgress;
        private bool removing;
        private bool isHolding;

        private Vector3 boltInstalledLocalPosition;
        private Quaternion boltInstalledLocalRotation = Quaternion.identity;
        private bool boltPoseCaptured;

        private Vector3 wheelBoltInstalledLocalPosition;
        private Quaternion wheelBoltInstalledLocalRotation = Quaternion.identity;
        private bool wheelBoltPoseCaptured;

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
                    return $"{name} wheel station — reinstall the gear strut first | X inspect";
                }

                if (inventoryBridge == null || !inventoryBridge.IsReady)
                {
                    return $"{name} wheel station — wheel service needs P-51 Step 30 | X inspect";
                }

                bool rimPresent = inventoryBridge.IsRimInstalled(wheelIndex);
                bool tirePresent = controller.IsTireInstalled(wheelIndex);
                if (rimPresent && tirePresent)
                {
                    return $"Hold R: unscrew wheel retaining bolt and pull complete {name} tire + rim off axle{progress} | N nitrogen | X inspect";
                }

                if (!rimPresent && !tirePresent)
                {
                    P51LooseWheelAssembly carried = P51LooseWheelAssembly.CurrentCarried;
                    if (carried != null && carried.CanInstallOn(wheelIndex))
                    {
                        return $"Hold E: install carried complete {name} wheel on highlighted axle and tighten retaining bolt{progress} | X inspect";
                    }
                    if (carried != null)
                    {
                        return $"This axle needs its own {name} wheel; the carried {carried.WheelLabel} wheel belongs to another station | X inspect";
                    }
                    return $"{name} wheel removed — rebuild/pick up its complete tire + rim assembly and carry it here | X inspect";
                }

                return $"{name} wheel state is incomplete — service the complete wheel off the aircraft | X inspect";
            }
        }

        private void Awake()
        {
            ResolveReferences();
            ResolveBoltVisual();
            CaptureBoltPose();
            EnsureWheelHardware();
            ApplyStableVisualState();
            UpdateInstallHighlight();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ResolveBoltVisual();
            CaptureBoltPose();
            EnsureWheelHardware();
            ApplyStableVisualState();
            UpdateInstallHighlight();
        }

        private void LateUpdate()
        {
            ResolveReferences();
            EnsureWheelHardware();

            if (!isHolding)
            {
                ApplyStableVisualState();
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
            ResolveBoltVisual();
            CaptureBoltPose();
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
            _ = inventory;
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
                bool rimPresent = inventoryBridge != null
                    && inventoryBridge.IsReady
                    && inventoryBridge.IsRimInstalled(wheelIndex);
                bool tirePresent = controller.IsTireInstalled(wheelIndex);
                P51LooseWheelAssembly carried = P51LooseWheelAssembly.CurrentCarried;

                bool validRemove = shouldRemove
                    && controller.IsGearInstalled(wheelIndex)
                    && rimPresent
                    && tirePresent;
                bool validInstall = shouldInstall
                    && controller.IsGearInstalled(wheelIndex)
                    && !rimPresent
                    && !tirePresent
                    && carried != null
                    && carried.CanInstallOn(wheelIndex);
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
            else
            {
                ApplyWheelBoltAnimatedPose(holdProgress, removing);
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
                resultMessage = "Wheel service is not configured. Run P-51 Step 30.";
            }
            else if (removing)
            {
                completed = inventoryBridge.TryRemoveWheelAssembly(wheelIndex, out resultMessage);
            }
            else
            {
                completed = inventoryBridge.TryInstallWheelAssembly(
                    wheelIndex,
                    P51LooseWheelAssembly.CurrentCarried,
                    out resultMessage);
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

        private void ApplyStableVisualState()
        {
            if (serviceKind == P51LandingGearServiceKind.MountBolt)
            {
                ApplyStableBoltPose();
            }
            else
            {
                ApplyStableWheelBoltPose();
            }
        }

        private void ApplyBoltAnimatedPose(float normalizedProgress, bool isRemoving)
        {
            ResolveBoltVisual();
            CaptureBoltPose();
            if (animatedBolt == null || !boltPoseCaptured)
            {
                return;
            }

            float t = Mathf.Clamp01(normalizedProgress);
            Vector3 extractedPosition = GetBoltExtractedLocalPosition();
            Vector3 startPosition = isRemoving ? boltInstalledLocalPosition : extractedPosition;
            Vector3 endPosition = isRemoving ? extractedPosition : boltInstalledLocalPosition;

            animatedBolt.gameObject.SetActive(true);
            animatedBolt.localPosition = Vector3.Lerp(startPosition, endPosition, t);

            float spinDirection = isRemoving ? 1f : -1f;
            Quaternion spin = Quaternion.AngleAxis(
                360f * boltRotationTurns * t * spinDirection,
                Vector3.up);
            animatedBolt.localRotation = boltInstalledLocalRotation * spin;
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
            animatedBolt.localPosition = installed
                ? boltInstalledLocalPosition
                : GetBoltExtractedLocalPosition();
            animatedBolt.localRotation = boltInstalledLocalRotation;
        }

        private Vector3 GetBoltExtractedLocalPosition()
        {
            Vector3 shaftDirection = boltInstalledLocalRotation * Vector3.up;
            if (wheelIndex == 1)
            {
                shaftDirection = -shaftDirection;
            }
            if (shaftDirection.sqrMagnitude < 0.001f)
            {
                shaftDirection = Vector3.up;
            }
            return boltInstalledLocalPosition
                + shaftDirection.normalized * Mathf.Max(0f, boltExtractionDistance);
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
            Vector3 start = isRemoving ? wheelBoltInstalledLocalPosition : extracted;
            Vector3 end = isRemoving ? extracted : wheelBoltInstalledLocalPosition;

            wheelRetainingBolt.gameObject.SetActive(true);
            wheelRetainingBolt.localPosition = Vector3.Lerp(start, end, t);

            float spinDirection = isRemoving ? 1f : -1f;
            Quaternion spin = Quaternion.AngleAxis(
                360f * wheelBoltRotationTurns * t * spinDirection,
                Vector3.up);
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
                && controller.IsTireInstalled(wheelIndex)
                && inventoryBridge != null
                && inventoryBridge.IsReady
                && inventoryBridge.IsRimInstalled(wheelIndex);

            wheelRetainingBolt.gameObject.SetActive(
                controller == null || controller.IsGearInstalled(wheelIndex));
            wheelRetainingBolt.localPosition = completeWheel
                ? wheelBoltInstalledLocalPosition
                : GetWheelBoltExtractedLocalPosition();
            wheelRetainingBolt.localRotation = wheelBoltInstalledLocalRotation;
        }

        private Vector3 GetWheelBoltExtractedLocalPosition()
        {
            Vector3 shaftDirection = wheelBoltInstalledLocalRotation * Vector3.up;
            if (shaftDirection.sqrMagnitude < 0.001f)
            {
                shaftDirection = wheelIndex == 0 ? Vector3.left : Vector3.right;
            }
            return wheelBoltInstalledLocalPosition
                + shaftDirection.normalized * Mathf.Max(0f, wheelBoltExtractionDistance);
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
                else if (Application.isPlaying)
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
                    bolt.transform.localRotation = Quaternion.Euler(
                        0f,
                        0f,
                        wheelIndex == 0 ? 90f : -90f);
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

            if (!Application.isPlaying)
            {
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

            bool wheelMissing = controller != null
                && controller.IsGearInstalled(wheelIndex)
                && !controller.IsTireInstalled(wheelIndex)
                && inventoryBridge != null
                && inventoryBridge.IsReady
                && !inventoryBridge.IsRimInstalled(wheelIndex);
            P51LooseWheelAssembly carried = P51LooseWheelAssembly.CurrentCarried;
            bool show = wheelMissing
                && carried != null
                && carried.IsCarried
                && carried.CanInstallOn(wheelIndex);

            installHighlightRoot.SetActive(show);
            installHighlightRoot.transform.localScale = show
                ? Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.045f)
                : Vector3.one;
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
            P51LandingGearServiceTarget[] targets = controller != null
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
            if (inventoryBridge == null && controller != null)
            {
                inventoryBridge = controller.GetComponent<P51LandingGearInventoryBridge>();
            }
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
