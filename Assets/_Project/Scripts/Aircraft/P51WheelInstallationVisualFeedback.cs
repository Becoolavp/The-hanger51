using System;
using System.Collections.Generic;
using Hanger51.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Aircraft
{
    /// <summary>
    /// Adds clear visual feedback to the two off-aircraft wheel installation holds without owning
    /// any maintenance state. The existing service scripts remain authoritative for inventory,
    /// condition, installation and removal. This component only mirrors their 1.15 second hold so
    /// the player can see the tire move onto a bare rim and the carried wheel move onto the axle.
    ///
    /// It also creates its own world-space axle highlights outside of the tire/valve service-target
    /// hierarchy. That is intentional: the tire-wear controller hides renderers underneath the
    /// valve target when a tire is removed, which can also hide highlight children placed there.
    /// </summary>
    [DefaultExecutionOrder(325)]
    [DisallowMultipleComponent]
    public sealed class P51WheelInstallationVisualFeedback : MonoBehaviour
    {
        private const float HoldSeconds = 1.15f;
        private const float InteractionDistance = 6f;
        private const string HighlightRootName = "Runtime Wheel Install Highlight";

        private Camera playerCamera;
        private PlayerInventory inventory;

        private P51LandingGearServiceTarget[] aircraftTargets =
            Array.Empty<P51LandingGearServiceTarget>();
        private float nextTargetRefreshTime;

        private readonly Dictionary<int, GameObject> installHighlights =
            new Dictionary<int, GameObject>();
        private Material highlightMaterial;

        private P51BareRimServiceTarget activeBareRim;
        private GameObject tirePreview;
        private Vector3 tirePreviewStartPosition;
        private Quaternion tirePreviewTargetRotation = Quaternion.identity;
        private float tireMountProgress;

        private P51LooseWheelAssembly previewWheel;
        private P51LandingGearServiceTarget previewAxle;
        private Transform previewWheelCarryParent;
        private Vector3 previewWheelCarryLocalPosition;
        private Quaternion previewWheelCarryLocalRotation = Quaternion.identity;
        private Vector3 previewWheelStartPosition;
        private Quaternion previewWheelStartRotation = Quaternion.identity;
        private float aircraftInstallProgress;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallRuntimeFeedback()
        {
            PlayerInventory player = FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            if (player != null
                && player.GetComponent<P51WheelInstallationVisualFeedback>() == null)
            {
                player.gameObject.AddComponent<P51WheelInstallationVisualFeedback>();
            }
        }

        private void Awake()
        {
            ResolveReferences();
            RefreshAircraftTargets();
        }

        private void OnEnable()
        {
            ResolveReferences();
            RefreshAircraftTargets();
        }

        private void Update()
        {
            ResolveReferences();
            if (playerCamera == null || inventory == null)
            {
                HideAllHighlights();
                CancelTirePreview();
                CancelAircraftPreview(true);
                return;
            }

            if (Time.unscaledTime >= nextTargetRefreshTime)
            {
                RefreshAircraftTargets();
            }

            P51LooseWheelAssembly carriedWheel = P51LooseWheelAssembly.CurrentCarried;
            UpdateInstallHighlights(carriedWheel);

            Keyboard keyboard = Keyboard.current;
            bool holdingE = keyboard != null && keyboard.eKey.isPressed;

            P51LandingGearServiceTarget aimedAxle = FindAimedAircraftWheelTarget();
            P51BareRimServiceTarget aimedBareRim = FindAimedBareRim();

            UpdateAircraftInstallPreview(carriedWheel, aimedAxle, holdingE);
            UpdateTireToRimPreview(aimedBareRim, holdingE);
        }

        private void ResolveReferences()
        {
            if (inventory == null)
            {
                inventory = GetComponent<PlayerInventory>();
                if (inventory == null)
                {
                    inventory = FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
                }
            }

            if (playerCamera == null && inventory != null)
            {
                playerCamera = inventory.GetComponentInChildren<Camera>(true);
            }
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }
        }

        private void RefreshAircraftTargets()
        {
            aircraftTargets = FindObjectsByType<P51LandingGearServiceTarget>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            nextTargetRefreshTime = Time.unscaledTime + 1f;
        }

        private void UpdateInstallHighlights(P51LooseWheelAssembly carriedWheel)
        {
            HashSet<int> validTargetIds = new HashSet<int>();

            for (int index = 0; index < aircraftTargets.Length; index++)
            {
                P51LandingGearServiceTarget target = aircraftTargets[index];
                if (target == null
                    || target.ServiceKind != P51LandingGearServiceKind.TireAndValve)
                {
                    continue;
                }

                bool show = IsCompatibleEmptyAxle(target, carriedWheel);
                int id = target.GetInstanceID();
                validTargetIds.Add(id);

                GameObject highlight = GetOrCreateHighlight(target);
                if (highlight == null)
                {
                    continue;
                }

                highlight.SetActive(show);
                if (!show)
                {
                    continue;
                }

                Transform controllerTransform = target.Controller != null
                    ? target.Controller.transform
                    : target.transform;
                highlight.transform.SetPositionAndRotation(
                    target.ServicePoint.position,
                    controllerTransform.rotation);

                float pulse = 1f + Mathf.Sin(Time.unscaledTime * 6.5f) * 0.085f;
                highlight.transform.localScale = Vector3.one * pulse;

                Renderer[] renderers = highlight.GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    if (renderers[rendererIndex] != null)
                    {
                        renderers[rendererIndex].enabled = true;
                    }
                }
            }

            List<int> stale = null;
            foreach (KeyValuePair<int, GameObject> pair in installHighlights)
            {
                if (validTargetIds.Contains(pair.Key))
                {
                    continue;
                }

                if (pair.Value != null)
                {
                    Destroy(pair.Value);
                }
                stale ??= new List<int>();
                stale.Add(pair.Key);
            }

            if (stale != null)
            {
                for (int index = 0; index < stale.Count; index++)
                {
                    installHighlights.Remove(stale[index]);
                }
            }
        }

        private GameObject GetOrCreateHighlight(P51LandingGearServiceTarget target)
        {
            if (target == null)
            {
                return null;
            }

            int id = target.GetInstanceID();
            if (installHighlights.TryGetValue(id, out GameObject existing)
                && existing != null)
            {
                return existing;
            }

            GameObject root = new GameObject($"{HighlightRootName} {target.WheelIndex}");
            root.SetActive(false);

            bool tail = target.WheelIndex == 2;
            float radius = tail ? 0.27f : 0.52f;
            float markerLength = tail ? 0.16f : 0.27f;
            float markerThickness = tail ? 0.035f : 0.055f;
            float diagonal = radius * 0.72f;

            CreateMarker(root.transform,
                new Vector3(0f, radius, 0f),
                new Vector3(markerThickness, markerLength, markerThickness));
            CreateMarker(root.transform,
                new Vector3(0f, -radius, 0f),
                new Vector3(markerThickness, markerLength, markerThickness));
            CreateMarker(root.transform,
                new Vector3(0f, 0f, radius),
                new Vector3(markerThickness, markerThickness, markerLength));
            CreateMarker(root.transform,
                new Vector3(0f, 0f, -radius),
                new Vector3(markerThickness, markerThickness, markerLength));

            CreateMarker(root.transform,
                new Vector3(0f, diagonal, diagonal),
                Vector3.one * markerThickness * 1.35f);
            CreateMarker(root.transform,
                new Vector3(0f, diagonal, -diagonal),
                Vector3.one * markerThickness * 1.35f);
            CreateMarker(root.transform,
                new Vector3(0f, -diagonal, diagonal),
                Vector3.one * markerThickness * 1.35f);
            CreateMarker(root.transform,
                new Vector3(0f, -diagonal, -diagonal),
                Vector3.one * markerThickness * 1.35f);

            installHighlights[id] = root;
            return root;
        }

        private void CreateMarker(Transform parent, Vector3 localPosition, Vector3 localScale)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Wheel Install Marker";
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = localPosition;
            marker.transform.localRotation = Quaternion.identity;
            marker.transform.localScale = localScale;

            Collider collider = marker.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = GetHighlightMaterial();
            }
        }

        private Material GetHighlightMaterial()
        {
            if (highlightMaterial != null)
            {
                return highlightMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            if (shader == null)
            {
                return null;
            }

            Color highlightColor = new Color(0.12f, 1f, 0.28f, 1f);
            highlightMaterial = new Material(shader)
            {
                name = "Runtime P-51 Wheel Install Highlight",
                color = highlightColor
            };

            if (highlightMaterial.HasProperty("_BaseColor"))
            {
                highlightMaterial.SetColor("_BaseColor", highlightColor);
            }
            if (highlightMaterial.HasProperty("_Color"))
            {
                highlightMaterial.SetColor("_Color", highlightColor);
            }
            if (highlightMaterial.HasProperty("_EmissionColor"))
            {
                highlightMaterial.SetColor("_EmissionColor", highlightColor * 4.5f);
                highlightMaterial.EnableKeyword("_EMISSION");
            }
            return highlightMaterial;
        }

        private void UpdateAircraftInstallPreview(
            P51LooseWheelAssembly carriedWheel,
            P51LandingGearServiceTarget aimedTarget,
            bool holdingE)
        {
            bool valid = holdingE
                && IsCompatibleEmptyAxle(aimedTarget, carriedWheel);

            if (!valid)
            {
                CancelAircraftPreview(true);
                return;
            }

            if (previewWheel != carriedWheel || previewAxle != aimedTarget)
            {
                CancelAircraftPreview(true);
                BeginAircraftPreview(carriedWheel, aimedTarget);
            }

            if (previewWheel == null || previewAxle == null)
            {
                return;
            }

            aircraftInstallProgress = Mathf.Clamp01(
                aircraftInstallProgress
                + Time.deltaTime / HoldSeconds);
            float t = aircraftInstallProgress;
            float smooth = t * t * (3f - 2f * t);

            Vector3 targetPosition = previewAxle.ServicePoint.position;
            Quaternion targetRotation = previewAxle.Controller != null
                ? previewAxle.Controller.transform.rotation
                : previewAxle.transform.rotation;

            previewWheel.transform.position = Vector3.Lerp(
                previewWheelStartPosition,
                targetPosition,
                smooth);
            previewWheel.transform.rotation = Quaternion.Slerp(
                previewWheelStartRotation,
                targetRotation,
                smooth);
        }

        private void BeginAircraftPreview(
            P51LooseWheelAssembly carriedWheel,
            P51LandingGearServiceTarget target)
        {
            if (carriedWheel == null || target == null)
            {
                return;
            }

            previewWheel = carriedWheel;
            previewAxle = target;
            previewWheelCarryParent = carriedWheel.transform.parent;
            previewWheelCarryLocalPosition = carriedWheel.transform.localPosition;
            previewWheelCarryLocalRotation = carriedWheel.transform.localRotation;
            previewWheelStartPosition = carriedWheel.transform.position;
            previewWheelStartRotation = carriedWheel.transform.rotation;
            aircraftInstallProgress = 0f;
        }

        private void CancelAircraftPreview(bool restoreCarryPose)
        {
            if (restoreCarryPose
                && previewWheel != null
                && previewWheel.IsCarried
                && previewWheel.transform != null
                && previewWheelCarryParent != null)
            {
                previewWheel.transform.SetParent(previewWheelCarryParent, false);
                previewWheel.transform.localPosition = previewWheelCarryLocalPosition;
                previewWheel.transform.localRotation = previewWheelCarryLocalRotation;
            }

            previewWheel = null;
            previewAxle = null;
            previewWheelCarryParent = null;
            aircraftInstallProgress = 0f;
        }

        private void UpdateTireToRimPreview(
            P51BareRimServiceTarget aimedRim,
            bool holdingE)
        {
            bool valid = holdingE && IsCorrectTireEquippedForRim(aimedRim);
            if (!valid)
            {
                CancelTirePreview();
                return;
            }

            if (activeBareRim != aimedRim || tirePreview == null)
            {
                CancelTirePreview();
                BeginTirePreview(aimedRim);
            }

            if (activeBareRim == null || tirePreview == null)
            {
                return;
            }

            tireMountProgress = Mathf.Clamp01(
                tireMountProgress
                + Time.deltaTime / HoldSeconds);
            float t = tireMountProgress;
            float smooth = t * t * (3f - 2f * t);

            Vector3 targetPosition = activeBareRim.transform.position;
            tirePreview.transform.position = Vector3.Lerp(
                tirePreviewStartPosition,
                targetPosition,
                smooth);
            tirePreview.transform.rotation = Quaternion.Slerp(
                tirePreview.transform.rotation,
                tirePreviewTargetRotation,
                Mathf.Clamp01(Time.deltaTime * 12f));

            float squeeze = Mathf.Sin(smooth * Mathf.PI) * 0.045f;
            tirePreview.transform.localScale = Vector3.one * (1f - squeeze);
        }

        private void BeginTirePreview(P51BareRimServiceTarget rim)
        {
            if (rim == null
                || inventory == null
                || inventory.EquippedItem == null
                || inventory.EquippedItem.WorldPrefab == null)
            {
                return;
            }

            activeBareRim = rim;
            InventoryItemDefinition tireItem = inventory.EquippedItem;
            tirePreview = Instantiate(tireItem.WorldPrefab);
            tirePreview.name = $"Mounting Preview - {tireItem.DisplayName}";
            tirePreview.SetActive(true);
            tirePreview.transform.localScale = tireItem.WorldScale;

            DisablePreviewInteractions(tirePreview);

            bool tail = tireItem.ItemId == P51LandingGearInventoryBridge.TailTireItemId;
            float slideDistance = tail ? 0.34f : 0.72f;
            Vector3 slideDirection = rim.transform.right.sqrMagnitude > 0.001f
                ? rim.transform.right.normalized
                : Vector3.right;

            tirePreviewStartPosition = rim.transform.position + slideDirection * slideDistance;
            tirePreviewTargetRotation = rim.transform.rotation;
            tirePreview.transform.SetPositionAndRotation(
                tirePreviewStartPosition,
                tirePreviewTargetRotation);
            tireMountProgress = 0f;
        }

        private static void DisablePreviewInteractions(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null)
                {
                    colliders[index].enabled = false;
                }
            }

            InventoryPickup[] pickups = root.GetComponentsInChildren<InventoryPickup>(true);
            for (int index = 0; index < pickups.Length; index++)
            {
                if (pickups[index] != null)
                {
                    pickups[index].enabled = false;
                }
            }

            P51BareRimServiceTarget[] rimTargets =
                root.GetComponentsInChildren<P51BareRimServiceTarget>(true);
            for (int index = 0; index < rimTargets.Length; index++)
            {
                if (rimTargets[index] != null)
                {
                    rimTargets[index].enabled = false;
                }
            }
        }

        private void CancelTirePreview()
        {
            if (tirePreview != null)
            {
                Destroy(tirePreview);
            }
            tirePreview = null;
            activeBareRim = null;
            tireMountProgress = 0f;
        }

        private P51LandingGearServiceTarget FindAimedAircraftWheelTarget()
        {
            if (playerCamera == null)
            {
                return null;
            }

            RaycastHit[] hits = Physics.RaycastAll(
                playerCamera.transform.position,
                playerCamera.transform.forward,
                InteractionDistance,
                ~0,
                QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int index = 0; index < hits.Length; index++)
            {
                Collider collider = hits[index].collider;
                if (collider == null)
                {
                    continue;
                }

                P51LandingGearServiceTarget target =
                    collider.GetComponentInParent<P51LandingGearServiceTarget>();
                if (target != null
                    && target.ServiceKind == P51LandingGearServiceKind.TireAndValve)
                {
                    return target;
                }
            }
            return null;
        }

        private P51BareRimServiceTarget FindAimedBareRim()
        {
            if (playerCamera == null)
            {
                return null;
            }

            RaycastHit[] hits = Physics.RaycastAll(
                playerCamera.transform.position,
                playerCamera.transform.forward,
                InteractionDistance,
                ~0,
                QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int index = 0; index < hits.Length; index++)
            {
                Collider collider = hits[index].collider;
                if (collider == null)
                {
                    continue;
                }

                if (collider.GetComponentInParent<P51LooseWheelAssembly>() != null)
                {
                    continue;
                }

                P51BareRimServiceTarget rim =
                    collider.GetComponentInParent<P51BareRimServiceTarget>();
                if (rim != null && rim.IsReady)
                {
                    return rim;
                }
            }
            return null;
        }

        private bool IsCompatibleEmptyAxle(
            P51LandingGearServiceTarget target,
            P51LooseWheelAssembly carriedWheel)
        {
            if (target == null
                || carriedWheel == null
                || !carriedWheel.IsCarried
                || !carriedWheel.IsComplete
                || target.ServiceKind != P51LandingGearServiceKind.TireAndValve
                || target.Controller == null
                || !target.Controller.IsGearInstalled(target.WheelIndex)
                || target.Controller.IsTireInstalled(target.WheelIndex)
                || !carriedWheel.CanInstallOn(target.WheelIndex))
            {
                return false;
            }

            P51LandingGearInventoryBridge bridge =
                target.Controller.GetComponent<P51LandingGearInventoryBridge>();
            return bridge != null
                && bridge.IsReady
                && !bridge.IsRimInstalled(target.WheelIndex);
        }

        private bool IsCorrectTireEquippedForRim(P51BareRimServiceTarget rim)
        {
            if (rim == null
                || !rim.IsReady
                || rim.Pickup == null
                || rim.Pickup.Item == null
                || inventory == null
                || inventory.EquippedItem == null)
            {
                return false;
            }

            bool tailRim = rim.Pickup.Item.ItemId
                == P51LandingGearInventoryBridge.TailRimItemId;
            string expectedTireId = tailRim
                ? P51LandingGearInventoryBridge.TailTireItemId
                : P51LandingGearInventoryBridge.MainTireItemId;
            return inventory.EquippedItem.ItemId == expectedTireId;
        }

        private void HideAllHighlights()
        {
            foreach (KeyValuePair<int, GameObject> pair in installHighlights)
            {
                if (pair.Value != null)
                {
                    pair.Value.SetActive(false);
                }
            }
        }

        private void OnDisable()
        {
            HideAllHighlights();
            CancelTirePreview();
            CancelAircraftPreview(true);
        }

        private void OnDestroy()
        {
            CancelTirePreview();
            CancelAircraftPreview(false);

            foreach (KeyValuePair<int, GameObject> pair in installHighlights)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value);
                }
            }
            installHighlights.Clear();

            if (highlightMaterial != null)
            {
                Destroy(highlightMaterial);
            }
        }
    }
}
