using Hanger51.Inventory;
using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class P51LooseWheelAssembly : MonoBehaviour
    {
        private enum ServiceAction
        {
            None,
            RemoveTire,
            InstallRim,
            InstallTire
        }

        [SerializeField] private InventoryItemDefinition tireItem;
        [SerializeField] private InventoryItemDefinition rimItem;
        [SerializeField] private EnginePartConditionData tireCondition;
        [SerializeField] private EnginePartConditionData rimCondition;
        [SerializeField] private string wheelLabel = "P-51 wheel";
        [SerializeField, Range(0, 2)] private int originWheelIndex;
        [SerializeField] private bool tireInstalled = true;
        [SerializeField] private bool rimInstalled = true;
        [SerializeField, Min(0.2f)] private float serviceHoldSeconds = 1.15f;

        private Collider interactionCollider;
        private Transform tireVisual;
        private Transform rimVisual;
        private Transform rebuildMarker;
        private Transform serviceValveTarget;
        private float serviceProgress;
        private ServiceAction activeAction;

        public static P51LooseWheelAssembly CurrentCarried { get; private set; }

        public bool IsComplete => tireInstalled && rimInstalled;
        public bool IsCarried => CurrentCarried == this;
        public bool HasTire => tireInstalled;
        public bool HasRim => rimInstalled;
        public bool IsBareRim => rimInstalled && !tireInstalled;
        public bool IsTireFailed => tireCondition != null && tireCondition.TireFailed;
        public int OriginWheelIndex => originWheelIndex;
        public string WheelLabel => wheelLabel;
        public float TirePressurePsi => tireCondition != null ? tireCondition.TirePressurePsi : 0f;
        public float ProperPressurePsi => originWheelIndex == 2 ? 24f : 30f;
        public Transform ServiceValveTarget => IsComplete ? serviceValveTarget : null;

        public static P51LooseWheelAssembly Create(
            string label,
            int configuredOriginWheelIndex,
            Vector3 worldPosition,
            Quaternion worldRotation,
            InventoryItemDefinition configuredTireItem,
            EnginePartConditionData configuredTireCondition,
            InventoryItemDefinition configuredRimItem,
            EnginePartConditionData configuredRimCondition)
        {
            if (configuredTireItem == null || configuredRimItem == null)
            {
                return null;
            }

            GameObject root = new GameObject($"Removed {label} Wheel Assembly");
            root.transform.SetPositionAndRotation(worldPosition, worldRotation);

            bool tail = configuredOriginWheelIndex == 2;
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.center = Vector3.up * (tail ? 0.16f : 0.32f);
            collider.size = tail
                ? new Vector3(0.55f, 0.55f, 0.34f)
                : new Vector3(1.05f, 0.92f, 0.54f);

            P51LooseWheelAssembly loose = root.AddComponent<P51LooseWheelAssembly>();
            loose.tireItem = configuredTireItem;
            loose.rimItem = configuredRimItem;
            loose.tireCondition = configuredTireCondition != null
                ? configuredTireCondition.Clone()
                : EnginePartConditionData.CreateDefaultForItem(configuredTireItem);
            loose.rimCondition = configuredRimCondition != null
                ? configuredRimCondition.Clone()
                : EnginePartConditionData.CreateDefaultForItem(configuredRimItem);
            loose.wheelLabel = string.IsNullOrWhiteSpace(label) ? "P-51 wheel" : label;
            loose.originWheelIndex = Mathf.Clamp(configuredOriginWheelIndex, 0, 2);
            loose.tireInstalled = true;
            loose.rimInstalled = true;
            loose.interactionCollider = collider;
            loose.BuildOrRefreshVisuals();
            return loose;
        }

        private void Awake()
        {
            ResolveCollider();
            ResolveExistingVisuals();
            EnsureServiceValveTarget();
            RefreshVisuals();
        }

        public bool HasCorrectEquippedTire(PlayerInventory inventory)
        {
            return inventory != null && tireItem != null && inventory.EquippedItem == tireItem;
        }

        public bool HasCorrectEquippedRim(PlayerInventory inventory)
        {
            return inventory != null && rimItem != null && inventory.EquippedItem == rimItem;
        }

        public string GetInteractionText(PlayerInventory inventory)
        {
            int percent = Mathf.RoundToInt(serviceProgress * 100f);
            string progress = serviceProgress > 0f ? $" ({percent}%)" : string.Empty;

            if (IsCarried)
            {
                return $"Carrying {wheelLabel} wheel assembly";
            }

            if (IsComplete)
            {
                return $"E: carry complete {wheelLabel} wheel | Hold R: remove tire from rim{progress} | N nitrogen | X inspect";
            }

            if (IsBareRim)
            {
                return HasCorrectEquippedTire(inventory)
                    ? $"Hold E: mount equipped {tireItem.DisplayName} onto this rim{progress} | X inspect"
                    : $"E: put bare {rimItem.DisplayName} in inventory | Equip {tireItem.DisplayName} to mount a tire | X inspect";
            }

            if (!rimInstalled)
            {
                return HasCorrectEquippedRim(inventory)
                    ? $"Hold E: place equipped {rimItem.DisplayName} at this wheel rebuild position{progress} | X inspect"
                    : $"Equip {rimItem.DisplayName} to restore the rim at this wheel rebuild position | X inspect";
            }

            return $"Loose {wheelLabel} wheel service position | X inspect";
        }

        public bool ProcessService(
            PlayerInventory inventory,
            bool installHeld,
            bool removeHeld,
            float deltaTime,
            out string resultMessage)
        {
            resultMessage = string.Empty;
            if (IsCarried)
            {
                CancelHold();
                return false;
            }

            ServiceAction desired = ResolveDesiredAction(inventory, installHeld, removeHeld);
            if (desired == ServiceAction.None)
            {
                CancelHold();
                return false;
            }

            if (activeAction != desired)
            {
                activeAction = desired;
                serviceProgress = 0f;
            }

            serviceProgress = Mathf.Clamp01(
                serviceProgress + Mathf.Max(0f, deltaTime) / Mathf.Max(0.2f, serviceHoldSeconds));
            if (serviceProgress < 1f)
            {
                return false;
            }

            bool completed = CompleteServiceAction(desired, inventory, out resultMessage);
            serviceProgress = 0f;
            activeAction = ServiceAction.None;
            return completed;
        }

        public bool TryPickupBareRim(PlayerInventory inventory, out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!IsBareRim)
            {
                resultMessage = "Remove the tire before putting the rim in inventory.";
                return false;
            }
            if (inventory == null || rimItem == null)
            {
                resultMessage = "The rim inventory item is not available.";
                return false;
            }

            int remaining = inventory.AddConditionedItem(rimItem, rimCondition);
            if (remaining > 0)
            {
                resultMessage = "Inventory is full; the bare rim remains on the floor.";
                return false;
            }

            rimInstalled = false;
            RefreshVisuals();
            resultMessage = $"Put the bare {rimItem.DisplayName} in inventory. The wheel rebuild position remains here so the same rim or a replacement rim can be installed later.";
            return true;
        }

        public bool TryBeginCarry(Transform carryAnchor, out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!IsComplete)
            {
                resultMessage = "Mount a tire on the rim before carrying the complete wheel back to the aircraft.";
                return false;
            }
            if (carryAnchor == null)
            {
                resultMessage = "The Player wheel-carry anchor is missing.";
                return false;
            }
            if (CurrentCarried != null && CurrentCarried != this)
            {
                resultMessage = "You are already carrying another wheel assembly.";
                return false;
            }

            CurrentCarried = this;
            CancelHold();
            ResolveCollider();
            if (interactionCollider != null)
            {
                interactionCollider.enabled = false;
            }

            transform.SetParent(carryAnchor, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            resultMessage = $"Picked up the complete {wheelLabel} wheel assembly. Carry it to its highlighted landing-gear axle and hold E to reinstall, or press E away from the axle to set it down.";
            return true;
        }

        public bool TryPlace(Vector3 worldPosition, Quaternion worldRotation, out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!IsCarried)
            {
                return false;
            }

            CurrentCarried = null;
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(worldPosition, worldRotation);
            ResolveCollider();
            if (interactionCollider != null)
            {
                interactionCollider.enabled = true;
            }
            resultMessage = $"Set down the {wheelLabel} wheel assembly.";
            return true;
        }

        public bool CanInstallOn(int wheelIndex)
        {
            return IsComplete && wheelIndex == originWheelIndex;
        }

        public EnginePartConditionData CaptureTireCondition()
        {
            return tireCondition != null ? tireCondition.Clone() : null;
        }

        public EnginePartConditionData CaptureRimCondition()
        {
            return rimCondition != null ? rimCondition.Clone() : null;
        }

        public bool ServicePressureToward(
            float regulatorPsi,
            float deltaTime,
            out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!IsComplete)
            {
                resultMessage = "The tire must be mounted on its rim before nitrogen service.";
                return false;
            }
            if (tireCondition == null || tireCondition.Kind != EnginePartConditionKind.Tire)
            {
                resultMessage = "The loose tire condition record is missing.";
                return false;
            }
            if (tireCondition.TireFailed)
            {
                resultMessage = "That tire is destroyed and must be replaced; nitrogen cannot repair it.";
                return false;
            }

            float target = Mathf.Clamp(regulatorPsi, 0f, 80f);
            float nextPressure = Mathf.MoveTowards(
                tireCondition.TirePressurePsi,
                target,
                Mathf.Max(0f, deltaTime) * 12f);
            tireCondition.SetTirePressure(nextPressure);

            float burstPressure = originWheelIndex == 2 ? 35f : 43f;
            if (nextPressure >= burstPressure)
            {
                tireCondition.FailTire(nextPressure);
                RefreshVisuals();
                resultMessage = $"BANG — the loose {wheelLabel} tire burst from overpressure at {nextPressure:F1} PSI.";
                return true;
            }

            RefreshVisuals();
            resultMessage = $"Loose {wheelLabel} tire: {nextPressure:F1} PSI | Setpoint {target:F1} PSI | Correct {ProperPressurePsi:F0} PSI";
            return true;
        }

        public void CompleteAircraftInstallation()
        {
            if (CurrentCarried == this)
            {
                CurrentCarried = null;
            }
            Destroy(gameObject);
        }

        public string Inspect()
        {
            string tireSummary = tireInstalled
                ? (tireCondition != null ? tireCondition.GetConditionSummary() : "condition unavailable")
                : "removed from rim";
            string rimSummary = rimInstalled
                ? (rimCondition != null ? rimCondition.GetConditionSummary() : "condition unavailable")
                : "stored/removed";
            string state = IsComplete
                ? "complete tire + rim wheel"
                : IsBareRim
                    ? "bare rim; tire removed"
                    : "rim removed from rebuild position";
            return $"Loose {wheelLabel} wheel | {state} | Tire: {tireSummary} | Rim: {rimSummary} | Origin station: {GetOriginName()}";
        }

        public void CancelHold()
        {
            serviceProgress = 0f;
            activeAction = ServiceAction.None;
        }

        private ServiceAction ResolveDesiredAction(
            PlayerInventory inventory,
            bool installHeld,
            bool removeHeld)
        {
            if (removeHeld && !installHeld && IsComplete)
            {
                return ServiceAction.RemoveTire;
            }

            if (installHeld && !removeHeld && inventory != null)
            {
                if (!rimInstalled && HasCorrectEquippedRim(inventory))
                {
                    return ServiceAction.InstallRim;
                }
                if (IsBareRim && HasCorrectEquippedTire(inventory))
                {
                    return ServiceAction.InstallTire;
                }
            }

            return ServiceAction.None;
        }

        private bool CompleteServiceAction(
            ServiceAction action,
            PlayerInventory inventory,
            out string resultMessage)
        {
            resultMessage = string.Empty;
            Vector3 groundSide = GetGroundSideDirection();

            switch (action)
            {
                case ServiceAction.RemoveTire:
                    if (!SpawnPickup(
                            tireItem,
                            tireCondition,
                            transform.position + groundSide * 0.72f + Vector3.up * 0.14f,
                            transform.rotation))
                    {
                        resultMessage = "The tire could not be placed beside the loose rim.";
                        return false;
                    }
                    tireInstalled = false;
                    RefreshVisuals();
                    resultMessage = $"Removed the tire from the {wheelLabel} rim. The rim stays intact, and the exact tire is now a physical pickup beside it.";
                    return true;

                case ServiceAction.InstallRim:
                    if (inventory == null
                        || rimItem == null
                        || !inventory.TryRemoveFirstItem(rimItem, out EnginePartConditionData installedRim))
                    {
                        resultMessage = "The equipped rim could not be removed from inventory.";
                        return false;
                    }
                    rimCondition = installedRim ?? EnginePartConditionData.CreateDefaultForItem(rimItem);
                    rimInstalled = true;
                    RefreshVisuals();
                    resultMessage = $"Placed that exact {rimItem.DisplayName} at the {wheelLabel} rebuild position. Mount the matching tire onto it next.";
                    return true;

                case ServiceAction.InstallTire:
                    if (inventory == null
                        || tireItem == null
                        || !inventory.TryRemoveFirstItem(tireItem, out EnginePartConditionData installedTire))
                    {
                        resultMessage = "The equipped tire could not be removed from inventory.";
                        return false;
                    }
                    tireCondition = installedTire ?? EnginePartConditionData.CreateDefaultForItem(tireItem);
                    tireInstalled = true;
                    RefreshVisuals();
                    resultMessage = $"Mounted that exact {tireItem.DisplayName} back onto the {wheelLabel} rim. The complete wheel can now be carried and reinstalled on its strut.";
                    return true;

                default:
                    return false;
            }
        }

        private Vector3 GetGroundSideDirection()
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            return side.sqrMagnitude > 0.001f ? side.normalized : Vector3.right;
        }

        private void BuildOrRefreshVisuals()
        {
            ResolveExistingVisuals();
            if (tireVisual == null)
            {
                tireVisual = CreateVisualChild(transform, tireItem, "Loose Tire");
            }
            if (rimVisual == null)
            {
                rimVisual = CreateVisualChild(transform, rimItem, "Loose Rim");
            }
            EnsureRebuildMarker();
            EnsureServiceValveTarget();
            RefreshVisuals();
        }

        private void ResolveExistingVisuals()
        {
            if (tireVisual == null)
            {
                tireVisual = transform.Find("Loose Tire");
            }
            if (rimVisual == null)
            {
                rimVisual = transform.Find("Loose Rim");
            }
            if (rebuildMarker == null)
            {
                rebuildMarker = transform.Find("Loose Wheel Rebuild Marker");
            }
            if (serviceValveTarget == null)
            {
                serviceValveTarget = transform.Find("Loose Tire Valve Target");
            }
        }

        private void EnsureServiceValveTarget()
        {
            if (serviceValveTarget != null)
            {
                return;
            }

            GameObject valve = new GameObject("Loose Tire Valve Target");
            valve.transform.SetParent(transform, false);
            bool tail = originWheelIndex == 2;
            valve.transform.localPosition = tail
                ? new Vector3(0.08f, 0.07f, 0f)
                : new Vector3(0.16f, 0.14f, 0f);
            serviceValveTarget = valve.transform;
        }

        private void EnsureRebuildMarker()
        {
            if (rebuildMarker != null)
            {
                return;
            }

            GameObject markerRoot = new GameObject("Loose Wheel Rebuild Marker");
            markerRoot.transform.SetParent(transform, false);
            markerRoot.transform.localPosition = Vector3.zero;
            markerRoot.transform.localRotation = Quaternion.identity;

            bool tail = originWheelIndex == 2;
            float radius = tail ? 0.20f : 0.42f;
            float length = tail ? 0.16f : 0.28f;
            float thickness = tail ? 0.025f : 0.04f;

            CreateMarkerPart(markerRoot.transform,
                new Vector3(0f, radius, 0f),
                new Vector3(thickness, length, thickness));
            CreateMarkerPart(markerRoot.transform,
                new Vector3(0f, -radius, 0f),
                new Vector3(thickness, length, thickness));
            CreateMarkerPart(markerRoot.transform,
                new Vector3(0f, 0f, radius),
                new Vector3(thickness, thickness, length));
            CreateMarkerPart(markerRoot.transform,
                new Vector3(0f, 0f, -radius),
                new Vector3(thickness, thickness, length));

            rebuildMarker = markerRoot.transform;
            rebuildMarker.gameObject.SetActive(false);
        }

        private static void CreateMarkerPart(
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Rebuild Marker Segment";
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = localPosition;
            marker.transform.localRotation = Quaternion.identity;
            marker.transform.localScale = localScale;
            Collider collider = marker.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }
        }

        private void RefreshVisuals()
        {
            if (tireVisual != null)
            {
                tireVisual.gameObject.SetActive(tireInstalled);
                ConfigureConditionVisual(tireVisual, tireCondition);
            }
            if (rimVisual != null)
            {
                rimVisual.gameObject.SetActive(rimInstalled);
                ConfigureConditionVisual(rimVisual, rimCondition);
            }
            if (rebuildMarker != null)
            {
                rebuildMarker.gameObject.SetActive(!rimInstalled && !IsCarried);
            }
            if (serviceValveTarget != null)
            {
                serviceValveTarget.gameObject.SetActive(IsComplete && !IsCarried);
            }
        }

        private static void ConfigureConditionVisual(
            Transform visualRoot,
            EnginePartConditionData condition)
        {
            if (visualRoot == null)
            {
                return;
            }

            EnginePartConditionVisual visual = visualRoot.GetComponent<EnginePartConditionVisual>();
            if (visual == null)
            {
                visual = visualRoot.gameObject.AddComponent<EnginePartConditionVisual>();
            }
            visual.Configure(condition);
        }

        private static Transform CreateVisualChild(
            Transform parent,
            InventoryItemDefinition item,
            string childName)
        {
            if (parent == null || item == null)
            {
                return null;
            }

            GameObject child;
            if (item.WorldPrefab != null)
            {
                child = Instantiate(item.WorldPrefab, parent);
                child.transform.localScale = item.WorldScale;
            }
            else
            {
                child = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                child.transform.SetParent(parent, false);
            }

            child.name = childName;
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            DisablePickupAndColliders(child);
            return child.transform;
        }

        private static void DisablePickupAndColliders(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            InventoryPickup[] pickups = root.GetComponentsInChildren<InventoryPickup>(true);
            for (int index = 0; index < pickups.Length; index++)
            {
                if (pickups[index] != null)
                {
                    pickups[index].enabled = false;
                }
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null)
                {
                    colliders[index].enabled = false;
                }
            }
        }

        private static bool SpawnPickup(
            InventoryItemDefinition item,
            EnginePartConditionData condition,
            Vector3 worldPosition,
            Quaternion worldRotation)
        {
            if (item == null)
            {
                return false;
            }

            GameObject pickupObject;
            if (item.WorldPrefab != null)
            {
                pickupObject = Instantiate(item.WorldPrefab);
                pickupObject.transform.localScale = item.WorldScale;
            }
            else
            {
                pickupObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            }

            pickupObject.SetActive(true);
            pickupObject.name = $"Separated {item.DisplayName}";
            pickupObject.transform.SetPositionAndRotation(worldPosition, worldRotation);

            Collider collider = pickupObject.GetComponent<Collider>();
            if (collider == null)
            {
                collider = pickupObject.AddComponent<BoxCollider>();
            }
            collider.enabled = true;
            collider.isTrigger = true;

            InventoryPickup pickup = pickupObject.GetComponent<InventoryPickup>();
            if (pickup == null)
            {
                pickup = pickupObject.AddComponent<InventoryPickup>();
            }
            pickup.enabled = true;
            pickup.Configure(item, condition);

            EnginePartConditionVisual conditionVisual = pickupObject.GetComponent<EnginePartConditionVisual>();
            if (conditionVisual == null)
            {
                conditionVisual = pickupObject.AddComponent<EnginePartConditionVisual>();
            }
            conditionVisual.Configure(condition);
            return true;
        }

        private void ResolveCollider()
        {
            if (interactionCollider == null)
            {
                interactionCollider = GetComponent<Collider>();
            }
        }

        private string GetOriginName()
        {
            return originWheelIndex == 0
                ? "left main"
                : originWheelIndex == 1
                    ? "right main"
                    : "tail";
        }

        private void OnDestroy()
        {
            if (CurrentCarried == this)
            {
                CurrentCarried = null;
            }
        }

        private void OnDisable()
        {
            CancelHold();
        }
    }
}
