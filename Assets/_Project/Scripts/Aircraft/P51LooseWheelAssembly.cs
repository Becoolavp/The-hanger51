using Hanger51.Inventory;
using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class P51LooseWheelAssembly : MonoBehaviour
    {
        [SerializeField] private InventoryItemDefinition tireItem;
        [SerializeField] private InventoryItemDefinition rimItem;
        [SerializeField] private EnginePartConditionData tireCondition;
        [SerializeField] private EnginePartConditionData rimCondition;
        [SerializeField] private string wheelLabel = "P-51 wheel";
        [SerializeField, Range(-1, 2)] private int originWheelIndex = -1;
        [SerializeField, Min(0.2f)] private float serviceHoldSeconds = 1.15f;

        private Collider interactionCollider;
        private Transform tireVisual;
        private Transform rimVisual;
        private Transform serviceValveTarget;
        private float serviceProgress;

        public static P51LooseWheelAssembly CurrentCarried { get; private set; }

        public bool IsComplete => tireItem != null && rimItem != null;
        public bool IsCarried => CurrentCarried == this;
        public bool IsTireFailed => tireCondition != null && tireCondition.TireFailed;
        public int OriginWheelIndex => originWheelIndex;
        public string WheelLabel => wheelLabel;
        public float TirePressurePsi => tireCondition != null ? tireCondition.TirePressurePsi : 0f;
        public float ProperPressurePsi => IsTailWheel ? 24f : 30f;
        public Transform ServiceValveTarget => serviceValveTarget;

        private bool IsTailWheel =>
            (rimItem != null && rimItem.ItemId == P51LandingGearInventoryBridge.TailRimItemId)
            || (tireItem != null && tireItem.ItemId == P51LandingGearInventoryBridge.TailTireItemId)
            || originWheelIndex == 2;

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

            bool tail = configuredOriginWheelIndex == 2
                || configuredRimItem.ItemId == P51LandingGearInventoryBridge.TailRimItemId;
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
            loose.originWheelIndex = Mathf.Clamp(configuredOriginWheelIndex, -1, 2);
            if (loose.rimCondition != null && loose.originWheelIndex >= 0)
            {
                loose.rimCondition.SetWheelStationIndex(loose.originWheelIndex);
            }
            loose.interactionCollider = collider;
            loose.BuildVisuals();
            return loose;
        }

        private void Awake()
        {
            ResolveCollider();
            ResolveExistingVisuals();
            EnsureServiceValveTarget();
        }

        public string GetInteractionText(PlayerInventory inventory)
        {
            _ = inventory;
            int percent = Mathf.RoundToInt(serviceProgress * 100f);
            string progress = serviceProgress > 0f ? $" ({percent}%)" : string.Empty;
            return IsCarried
                ? $"Carrying {wheelLabel} wheel assembly"
                : $"E: carry complete {wheelLabel} wheel | Hold R: remove tire from rim{progress} | N nitrogen | X inspect";
        }

        public bool ProcessService(
            PlayerInventory inventory,
            bool installHeld,
            bool removeHeld,
            float deltaTime,
            out string resultMessage)
        {
            _ = inventory;
            _ = installHeld;
            resultMessage = string.Empty;

            if (IsCarried || !removeHeld)
            {
                CancelHold();
                return false;
            }

            serviceProgress = Mathf.Clamp01(
                serviceProgress + Mathf.Max(0f, deltaTime) / Mathf.Max(0.2f, serviceHoldSeconds));
            if (serviceProgress < 1f)
            {
                return false;
            }

            serviceProgress = 0f;
            return RemoveTireAndLeaveStandaloneRim(out resultMessage);
        }

        public bool TryBeginCarry(Transform carryAnchor, out string resultMessage)
        {
            resultMessage = string.Empty;
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
            resultMessage = $"Picked up the complete {wheelLabel} wheel assembly. Carry it to a compatible highlighted landing-gear axle and hold E to reinstall, or press E away from the axle to set it down.";
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
            if (!IsComplete || wheelIndex < 0 || wheelIndex > 2)
            {
                return false;
            }

            if (originWheelIndex >= 0)
            {
                return wheelIndex == originWheelIndex;
            }

            return IsTailWheel ? wheelIndex == 2 : wheelIndex == 0 || wheelIndex == 1;
        }

        public EnginePartConditionData CaptureTireCondition()
        {
            return tireCondition != null ? tireCondition.Clone() : null;
        }

        public EnginePartConditionData CaptureRimCondition()
        {
            EnginePartConditionData clone = rimCondition != null ? rimCondition.Clone() : null;
            if (clone != null && originWheelIndex >= 0)
            {
                clone.SetWheelStationIndex(originWheelIndex);
            }
            return clone;
        }

        public bool ServicePressureToward(
            float regulatorPsi,
            float deltaTime,
            out string resultMessage)
        {
            resultMessage = string.Empty;
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

            float burstPressure = IsTailWheel ? 35f : 43f;
            if (nextPressure >= burstPressure)
            {
                tireCondition.FailTire(nextPressure);
                ConfigureConditionVisual(tireVisual, tireCondition);
                resultMessage = $"BANG — the loose {wheelLabel} tire burst from overpressure at {nextPressure:F1} PSI.";
                return true;
            }

            ConfigureConditionVisual(tireVisual, tireCondition);
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
            string tireSummary = tireCondition != null
                ? tireCondition.GetConditionSummary()
                : "condition unavailable";
            string rimSummary = rimCondition != null
                ? rimCondition.GetConditionSummary()
                : "condition unavailable";
            return $"Loose {wheelLabel} complete wheel | Tire: {tireSummary} | Rim: {rimSummary} | Origin: {GetOriginName()}";
        }

        public void CancelHold()
        {
            serviceProgress = 0f;
        }

        private bool RemoveTireAndLeaveStandaloneRim(out string resultMessage)
        {
            resultMessage = string.Empty;
            Vector3 groundSide = GetGroundSideDirection();
            if (!SpawnPickup(
                    tireItem,
                    tireCondition,
                    transform.position + groundSide * 0.72f + Vector3.up * 0.14f,
                    transform.rotation))
            {
                resultMessage = "The tire could not be placed beside the rim.";
                return false;
            }

            if (rimCondition == null)
            {
                rimCondition = EnginePartConditionData.CreateDefaultForItem(rimItem);
            }
            if (rimCondition != null && originWheelIndex >= 0)
            {
                rimCondition.SetWheelStationIndex(originWheelIndex);
            }

            if (tireVisual != null)
            {
                Destroy(tireVisual.gameObject);
                tireVisual = null;
            }
            if (serviceValveTarget != null)
            {
                Destroy(serviceValveTarget.gameObject);
                serviceValveTarget = null;
            }

            InventoryPickup rimPickup = GetComponent<InventoryPickup>();
            if (rimPickup == null)
            {
                rimPickup = gameObject.AddComponent<InventoryPickup>();
            }
            rimPickup.Configure(rimItem, rimCondition);
            P51BareRimServiceTarget.EnsureForPickup(rimPickup, originWheelIndex);

            resultMessage = $"Removed the tire from the {wheelLabel} rim. The destroyed/removed tire is a separate physical pickup, and the bare rim is now a normal pickup that can also accept a matching replacement tire directly.";
            Destroy(this);
            return true;
        }

        private void BuildVisuals()
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
            EnsureServiceValveTarget();
            ConfigureConditionVisual(tireVisual, tireCondition);
            ConfigureConditionVisual(rimVisual, rimCondition);
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
            valve.transform.localPosition = IsTailWheel
                ? new Vector3(0.08f, 0.07f, 0f)
                : new Vector3(0.16f, 0.14f, 0f);
            serviceValveTarget = valve.transform;
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

            InventoryPickup[] pickups = child.GetComponentsInChildren<InventoryPickup>(true);
            for (int index = 0; index < pickups.Length; index++)
            {
                pickups[index].enabled = false;
            }
            Collider[] colliders = child.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                colliders[index].enabled = false;
            }
            P51BareRimServiceTarget[] rimTargets =
                child.GetComponentsInChildren<P51BareRimServiceTarget>(true);
            for (int index = 0; index < rimTargets.Length; index++)
            {
                rimTargets[index].enabled = false;
            }
            return child.transform;
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

            GameObject pickupObject = item.WorldPrefab != null
                ? Instantiate(item.WorldPrefab)
                : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            if (item.WorldPrefab != null)
            {
                pickupObject.transform.localScale = item.WorldScale;
            }
            pickupObject.SetActive(true);
            pickupObject.transform.SetPositionAndRotation(worldPosition, worldRotation);

            InventoryPickup pickup = pickupObject.GetComponent<InventoryPickup>();
            if (pickup == null)
            {
                pickup = pickupObject.AddComponent<InventoryPickup>();
            }
            pickup.Configure(item, condition);

            BoxCollider rootCollider = pickupObject.GetComponent<BoxCollider>();
            if (rootCollider == null)
            {
                rootCollider = pickupObject.AddComponent<BoxCollider>();
            }
            rootCollider.enabled = true;
            rootCollider.isTrigger = true;

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
            if (originWheelIndex == 0) return "left main";
            if (originWheelIndex == 1) return "right main";
            if (originWheelIndex == 2) return "tail";
            return IsTailWheel ? "tail-compatible" : "either main";
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

        private void OnValidate()
        {
            originWheelIndex = Mathf.Clamp(originWheelIndex, -1, 2);
            serviceHoldSeconds = Mathf.Max(0.2f, serviceHoldSeconds);
        }
    }
}
