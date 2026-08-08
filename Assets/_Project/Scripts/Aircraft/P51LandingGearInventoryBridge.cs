using System;
using System.Reflection;
using Hanger51.Inventory;
using UnityEngine;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(60)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(P51LandingGearMaintenanceController))]
    [RequireComponent(typeof(P51RaycastLandingGear))]
    public sealed class P51LandingGearInventoryBridge : MonoBehaviour
    {
        public const string MainTireItemId = "p51-main-landing-tire";
        public const string TailTireItemId = "p51-tailwheel-tire";
        public const string MainRimItemId = "p51-main-wheel-rim";
        public const string TailRimItemId = "p51-tailwheel-rim";

        private const int WheelCount = 3;
        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;

        [Header("Inventory Parts")]
        [SerializeField] private InventoryItemDefinition mainTireItem;
        [SerializeField] private InventoryItemDefinition tailTireItem;
        [SerializeField] private InventoryItemDefinition mainRimItem;
        [SerializeField] private InventoryItemDefinition tailRimItem;

        [Header("Rim State")]
        [SerializeField] private bool[] rimInstalled = { true, true, true };
        [SerializeField] private float[] rimHealth = { 100f, 100f, 100f };

        private P51LandingGearMaintenanceController maintenance;
        private P51RaycastLandingGear physicsGear;
        private FieldInfo tireHealthField;
        private FieldInfo tirePressureField;
        private FieldInfo tireInstalledField;
        private FieldInfo tireBurstField;
        private FieldInfo rimVisualRootsField;
        private MethodInfo ensureArraysMethod;
        private MethodInfo applyVisualStateMethod;
        private MethodInfo pushPhysicsStateMethod;

        public bool IsReady { get; private set; }

        private void Awake()
        {
            ResolveBindings();
            EnsureRimArrays();
            ApplyRimVisualState();
        }

        private void OnEnable()
        {
            ResolveBindings();
            EnsureRimArrays();
            ApplyRimVisualState();
        }

        private void Update()
        {
            ResolveBindings();
            EnsureRimArrays();
            ApplyRimVisualState();
            OverridePhysicsForMissingRims();
        }

        private void FixedUpdate()
        {
            OverridePhysicsForMissingRims();
        }

        public void Configure(
            InventoryItemDefinition configuredMainTire,
            InventoryItemDefinition configuredTailTire,
            InventoryItemDefinition configuredMainRim,
            InventoryItemDefinition configuredTailRim)
        {
            mainTireItem = configuredMainTire;
            tailTireItem = configuredTailTire;
            mainRimItem = configuredMainRim;
            tailRimItem = configuredTailRim;
            ResolveBindings();
            EnsureRimArrays();
            ApplyRimVisualState();
            OverridePhysicsForMissingRims();
        }

        public bool IsRimInstalled(int wheelIndex)
        {
            EnsureRimArrays();
            return IsValidWheel(wheelIndex) && rimInstalled[wheelIndex];
        }

        public float GetRimHealth(int wheelIndex)
        {
            EnsureRimArrays();
            return IsValidWheel(wheelIndex) ? rimHealth[wheelIndex] : 0f;
        }

        public bool HasCorrectEquippedTire(int wheelIndex, PlayerInventory inventory)
        {
            InventoryItemDefinition required = GetTireItem(wheelIndex);
            return required != null
                && inventory != null
                && inventory.EquippedItem == required;
        }

        public bool HasCorrectEquippedRim(int wheelIndex, PlayerInventory inventory)
        {
            InventoryItemDefinition required = GetRimItem(wheelIndex);
            return required != null
                && inventory != null
                && inventory.EquippedItem == required;
        }

        public string GetRimInspectionText(int wheelIndex)
        {
            if (!IsValidWheel(wheelIndex))
            {
                return string.Empty;
            }

            return IsRimInstalled(wheelIndex)
                ? $"Rim: installed, {GetRimHealth(wheelIndex):F1}% health"
                : $"Rim: removed, last condition {GetRimHealth(wheelIndex):F1}%";
        }

        public bool TryRemoveTire(
            int wheelIndex,
            PlayerInventory inventory,
            out string resultMessage)
        {
            _ = inventory;
            resultMessage = string.Empty;
            if (!PrepareService(wheelIndex, out resultMessage))
            {
                return false;
            }
            if (!maintenance.IsGearInstalled(wheelIndex))
            {
                resultMessage = "Reinstall the landing gear before removing its tire.";
                return false;
            }
            if (!IsRimInstalled(wheelIndex))
            {
                resultMessage = "The rim is already removed.";
                return false;
            }
            if (!maintenance.IsTireInstalled(wheelIndex))
            {
                resultMessage = $"The {maintenance.GetWheelName(wheelIndex)} tire is already removed.";
                return false;
            }

            InventoryItemDefinition item = GetTireItem(wheelIndex);
            if (item == null)
            {
                resultMessage = "The tire inventory item is not configured. Run P-51 Step 30.";
                return false;
            }

            GetTireArrays(
                out float[] health,
                out float[] pressure,
                out bool[] installed,
                out bool[] burst);
            EnginePartConditionData condition = EnginePartConditionData.Create(
                EnginePartConditionKind.Tire,
                health[wheelIndex],
                0f,
                0f,
                pressure[wheelIndex],
                maintenance.GetProperPressure(wheelIndex),
                burst[wheelIndex]);

            if (!SpawnLoosePart(wheelIndex, item, condition, true))
            {
                resultMessage = "The tire could not be placed beside the wheel.";
                return false;
            }

            installed[wheelIndex] = false;
            RefreshMaintenanceVisualsAndPhysics();
            resultMessage = $"Pulled the {maintenance.GetWheelName(wheelIndex)} tire off the rim. That exact {condition.Health:F0}% / {condition.TirePressurePsi:F1} PSI tire is now loose beside the wheel; press E on it to put it in inventory.";
            return true;
        }

        public bool TryInstallTire(
            int wheelIndex,
            PlayerInventory inventory,
            out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!PrepareService(wheelIndex, out resultMessage))
            {
                return false;
            }
            if (!maintenance.IsGearInstalled(wheelIndex))
            {
                resultMessage = "Reinstall the landing gear before fitting a tire.";
                return false;
            }
            if (!IsRimInstalled(wheelIndex))
            {
                resultMessage = "Install the correct rim before fitting a tire.";
                return false;
            }
            if (maintenance.IsTireInstalled(wheelIndex))
            {
                resultMessage = "A tire is already installed on that rim.";
                return false;
            }

            InventoryItemDefinition required = GetTireItem(wheelIndex);
            if (required == null)
            {
                resultMessage = "The tire inventory item is not configured. Run P-51 Step 30.";
                return false;
            }
            if (inventory == null || inventory.EquippedItem != required)
            {
                resultMessage = $"Equip a {required.DisplayName} from inventory before fitting it to this rim.";
                return false;
            }
            if (!inventory.TryRemoveFirstItem(required, out EnginePartConditionData condition))
            {
                resultMessage = "The equipped tire could not be removed from inventory.";
                return false;
            }

            condition ??= EnginePartConditionData.CreateDefaultForItem(required);
            GetTireArrays(
                out float[] health,
                out float[] pressure,
                out bool[] installed,
                out bool[] burst);
            health[wheelIndex] = condition != null ? condition.Health : 100f;
            pressure[wheelIndex] = condition != null
                ? condition.TirePressurePsi
                : wheelIndex == 2 ? 6f : 8f;
            burst[wheelIndex] = condition != null && condition.TireFailed;
            installed[wheelIndex] = true;
            RefreshMaintenanceVisualsAndPhysics();

            resultMessage = $"Installed that exact {required.DisplayName}: {health[wheelIndex]:F0}% health, {pressure[wheelIndex]:F1} PSI. Correct pressure is {maintenance.GetProperPressure(wheelIndex):F0} PSI.";
            return true;
        }

        public bool TryRemoveRim(
            int wheelIndex,
            PlayerInventory inventory,
            out string resultMessage)
        {
            _ = inventory;
            resultMessage = string.Empty;
            if (!PrepareService(wheelIndex, out resultMessage))
            {
                return false;
            }
            if (!maintenance.IsGearInstalled(wheelIndex))
            {
                resultMessage = "Reinstall the landing gear before removing its rim.";
                return false;
            }
            if (maintenance.IsTireInstalled(wheelIndex))
            {
                resultMessage = "Remove the tire from the rim first.";
                return false;
            }
            if (!IsRimInstalled(wheelIndex))
            {
                resultMessage = "That rim is already removed.";
                return false;
            }

            InventoryItemDefinition item = GetRimItem(wheelIndex);
            if (item == null)
            {
                resultMessage = "The rim inventory item is not configured. Run P-51 Step 30.";
                return false;
            }

            EnginePartConditionData condition = EnginePartConditionData.Create(
                EnginePartConditionKind.Rim,
                rimHealth[wheelIndex]);
            if (!SpawnLoosePart(wheelIndex, item, condition, false))
            {
                resultMessage = "The rim could not be placed beside the wheel.";
                return false;
            }

            rimInstalled[wheelIndex] = false;
            ApplyRimVisualState();
            OverridePhysicsForMissingRims();
            resultMessage = $"Pulled the {maintenance.GetWheelName(wheelIndex)} rim off the gear. That exact {condition.Health:F0}% rim is now loose beside the wheel; press E on it to put it in inventory.";
            return true;
        }

        public bool TryInstallRim(
            int wheelIndex,
            PlayerInventory inventory,
            out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!PrepareService(wheelIndex, out resultMessage))
            {
                return false;
            }
            if (!maintenance.IsGearInstalled(wheelIndex))
            {
                resultMessage = "Reinstall the landing gear before fitting its rim.";
                return false;
            }
            if (maintenance.IsTireInstalled(wheelIndex))
            {
                resultMessage = "A tire is still installed. Remove it before changing the rim.";
                return false;
            }
            if (IsRimInstalled(wheelIndex))
            {
                resultMessage = "A rim is already installed on that gear.";
                return false;
            }

            InventoryItemDefinition required = GetRimItem(wheelIndex);
            if (required == null)
            {
                resultMessage = "The rim inventory item is not configured. Run P-51 Step 30.";
                return false;
            }
            if (inventory == null || inventory.EquippedItem != required)
            {
                resultMessage = $"Equip a {required.DisplayName} from inventory before installing it.";
                return false;
            }
            if (!inventory.TryRemoveFirstItem(required, out EnginePartConditionData condition))
            {
                resultMessage = "The equipped rim could not be removed from inventory.";
                return false;
            }

            condition ??= EnginePartConditionData.CreateDefaultForItem(required);
            rimHealth[wheelIndex] = condition != null ? condition.Health : 100f;
            rimInstalled[wheelIndex] = true;
            ApplyRimVisualState();
            OverridePhysicsForMissingRims();
            resultMessage = $"Installed the {required.DisplayName} at {rimHealth[wheelIndex]:F0}% condition. Fit the correct tire next.";
            return true;
        }

        private bool PrepareService(int wheelIndex, out string resultMessage)
        {
            resultMessage = string.Empty;
            ResolveBindings();
            EnsureRimArrays();
            if (!IsReady || maintenance == null)
            {
                resultMessage = "The wheel inventory bridge is not configured. Run P-51 Step 30.";
                return false;
            }
            if (!IsValidWheel(wheelIndex))
            {
                resultMessage = "That wheel station is invalid.";
                return false;
            }
            return maintenance.CanService(out resultMessage);
        }

        private void GetTireArrays(
            out float[] health,
            out float[] pressure,
            out bool[] installed,
            out bool[] burst)
        {
            ensureArraysMethod.Invoke(maintenance, null);
            health = tireHealthField.GetValue(maintenance) as float[];
            pressure = tirePressureField.GetValue(maintenance) as float[];
            installed = tireInstalledField.GetValue(maintenance) as bool[];
            burst = tireBurstField.GetValue(maintenance) as bool[];
        }

        private bool SpawnLoosePart(
            int wheelIndex,
            InventoryItemDefinition item,
            EnginePartConditionData condition,
            bool isTire)
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
                pickupObject.transform.localScale = wheelIndex == 2
                    ? new Vector3(0.20f, 0.08f, 0.20f)
                    : new Vector3(0.42f, 0.12f, 0.42f);
            }

            pickupObject.name = $"Removed {item.DisplayName}";
            Transform wheelReference = maintenance != null
                ? maintenance.GetValveTarget(wheelIndex)
                : null;
            Vector3 basePosition = wheelReference != null
                ? wheelReference.position
                : transform.position;
            Vector3 outward = wheelIndex == 0
                ? -transform.right
                : wheelIndex == 1
                    ? transform.right
                    : transform.right;
            float outwardDistance = wheelIndex == 2 ? 0.48f : 0.72f;
            pickupObject.transform.position = basePosition
                + outward * outwardDistance
                + Vector3.up * (isTire ? 0.18f : 0.12f)
                + transform.forward * (wheelIndex == 2 ? -0.10f : 0.06f);
            pickupObject.transform.rotation = Quaternion.Euler(
                90f,
                transform.eulerAngles.y,
                0f);

            Collider collider = pickupObject.GetComponent<Collider>();
            if (collider == null)
            {
                collider = pickupObject.AddComponent<BoxCollider>();
            }
            collider.isTrigger = true;

            InventoryPickup pickup = pickupObject.GetComponent<InventoryPickup>();
            if (pickup == null)
            {
                pickup = pickupObject.AddComponent<InventoryPickup>();
            }
            pickup.Configure(item, condition);
            return true;
        }

        private void RefreshMaintenanceVisualsAndPhysics()
        {
            applyVisualStateMethod.Invoke(maintenance, new object[] { true });
            pushPhysicsStateMethod.Invoke(maintenance, null);
            ApplyRimVisualState();
            OverridePhysicsForMissingRims();
        }

        private void ApplyRimVisualState()
        {
            ResolveBindings();
            EnsureRimArrays();
            if (!IsReady)
            {
                return;
            }

            Transform[] rims = rimVisualRootsField.GetValue(maintenance) as Transform[];
            if (rims == null)
            {
                return;
            }

            for (int index = 0; index < WheelCount; index++)
            {
                if (index < rims.Length && rims[index] != null)
                {
                    rims[index].gameObject.SetActive(
                        maintenance.IsGearInstalled(index) && rimInstalled[index]);
                }
            }
        }

        private void OverridePhysicsForMissingRims()
        {
            if (physicsGear == null || maintenance == null)
            {
                return;
            }

            EnsureRimArrays();
            for (int index = 0; index < WheelCount; index++)
            {
                physicsGear.ApplyMaintenanceState(
                    index,
                    maintenance.IsGearInstalled(index) && rimInstalled[index],
                    maintenance.IsTireInstalled(index),
                    maintenance.IsTireFailed(index),
                    maintenance.GetTirePressure(index),
                    maintenance.GetProperPressure(index),
                    maintenance.DeploymentFraction);
            }
        }

        private InventoryItemDefinition GetTireItem(int wheelIndex)
        {
            return wheelIndex == 2 ? tailTireItem : mainTireItem;
        }

        private InventoryItemDefinition GetRimItem(int wheelIndex)
        {
            return wheelIndex == 2 ? tailRimItem : mainRimItem;
        }

        private void ResolveBindings()
        {
            if (maintenance == null)
            {
                maintenance = GetComponent<P51LandingGearMaintenanceController>();
            }
            if (physicsGear == null)
            {
                physicsGear = GetComponent<P51RaycastLandingGear>();
            }

            Type type = typeof(P51LandingGearMaintenanceController);
            tireHealthField = type.GetField("tireHealth", PrivateInstance);
            tirePressureField = type.GetField("tirePressurePsi", PrivateInstance);
            tireInstalledField = type.GetField("tireInstalled", PrivateInstance);
            tireBurstField = type.GetField("tireBurst", PrivateInstance);
            rimVisualRootsField = type.GetField("rimVisualRoots", PrivateInstance);
            ensureArraysMethod = type.GetMethod("EnsureArrays", PrivateInstance);
            applyVisualStateMethod = type.GetMethod("ApplyVisualState", PrivateInstance);
            pushPhysicsStateMethod = type.GetMethod("PushPhysicsState", PrivateInstance);

            IsReady = maintenance != null
                && physicsGear != null
                && tireHealthField != null
                && tirePressureField != null
                && tireInstalledField != null
                && tireBurstField != null
                && rimVisualRootsField != null
                && ensureArraysMethod != null
                && applyVisualStateMethod != null
                && pushPhysicsStateMethod != null
                && mainTireItem != null
                && tailTireItem != null
                && mainRimItem != null
                && tailRimItem != null;
        }

        private void EnsureRimArrays()
        {
            if (rimInstalled == null || rimInstalled.Length != WheelCount)
            {
                bool[] resized = { true, true, true };
                if (rimInstalled != null)
                {
                    Array.Copy(
                        rimInstalled,
                        resized,
                        Mathf.Min(rimInstalled.Length, resized.Length));
                }
                rimInstalled = resized;
            }

            if (rimHealth == null || rimHealth.Length != WheelCount)
            {
                float[] resized = { 100f, 100f, 100f };
                if (rimHealth != null)
                {
                    Array.Copy(
                        rimHealth,
                        resized,
                        Mathf.Min(rimHealth.Length, resized.Length));
                }
                rimHealth = resized;
            }

            for (int index = 0; index < WheelCount; index++)
            {
                rimHealth[index] = Mathf.Clamp(rimHealth[index], 0f, 100f);
            }
        }

        private static bool IsValidWheel(int wheelIndex)
        {
            return wheelIndex >= 0 && wheelIndex < WheelCount;
        }

        private void OnValidate()
        {
            ResolveBindings();
            EnsureRimArrays();
        }
    }
}
