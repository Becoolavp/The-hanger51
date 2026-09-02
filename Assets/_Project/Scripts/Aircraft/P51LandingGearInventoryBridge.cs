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
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

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
            return required != null && inventory != null && inventory.EquippedItem == required;
        }

        public bool HasCorrectEquippedRim(int wheelIndex, PlayerInventory inventory)
        {
            InventoryItemDefinition required = GetRimItem(wheelIndex);
            return required != null && inventory != null && inventory.EquippedItem == required;
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

        public bool TryRemoveWheelAssembly(int wheelIndex, out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!PrepareService(wheelIndex, out resultMessage))
            {
                return false;
            }
            if (!maintenance.IsGearInstalled(wheelIndex))
            {
                resultMessage = "Reinstall the landing gear before removing its wheel assembly.";
                return false;
            }
            if (!IsRimInstalled(wheelIndex) || !maintenance.IsTireInstalled(wheelIndex))
            {
                resultMessage = "The complete tire-and-rim wheel assembly is not installed on this gear.";
                return false;
            }

            InventoryItemDefinition tireItem = GetTireItem(wheelIndex);
            InventoryItemDefinition rimItem = GetRimItem(wheelIndex);
            if (tireItem == null || rimItem == null)
            {
                resultMessage = "The wheel inventory items are not configured. Run P-51 Step 30.";
                return false;
            }

            GetTireArrays(
                out float[] health,
                out float[] pressure,
                out bool[] installed,
                out bool[] burst);

            EnginePartConditionData tireCondition = EnginePartConditionData.Create(
                EnginePartConditionKind.Tire,
                health[wheelIndex],
                0f,
                0f,
                pressure[wheelIndex],
                maintenance.GetProperPressure(wheelIndex),
                burst[wheelIndex]);
            EnginePartConditionData rimCondition = EnginePartConditionData.Create(
                EnginePartConditionKind.Rim,
                rimHealth[wheelIndex]);

            Transform wheelReference = maintenance.GetValveTarget(wheelIndex);
            Vector3 basePosition = wheelReference != null ? wheelReference.position : transform.position;
            Vector3 outward = wheelIndex == 0 ? -transform.right : transform.right;
            float outwardDistance = wheelIndex == 2 ? 0.58f : 0.82f;
            Vector3 spawnPosition = basePosition
                + outward * outwardDistance
                + Vector3.up * (wheelIndex == 2 ? 0.13f : 0.24f)
                + transform.forward * (wheelIndex == 2 ? -0.12f : 0.08f);
            Quaternion spawnRotation = Quaternion.Euler(
                0f,
                transform.eulerAngles.y,
                wheelIndex == 2 ? 82f : 90f);

            P51LooseWheelAssembly looseWheel = P51LooseWheelAssembly.Create(
                maintenance.GetWheelName(wheelIndex),
                wheelIndex,
                spawnPosition,
                spawnRotation,
                tireItem,
                tireCondition,
                rimItem,
                rimCondition);
            if (looseWheel == null)
            {
                resultMessage = "The complete wheel assembly could not be placed beside the aircraft.";
                return false;
            }

            installed[wheelIndex] = false;
            rimInstalled[wheelIndex] = false;
            RefreshMaintenanceVisualsAndPhysics();

            resultMessage = $"Removed the {maintenance.GetWheelName(wheelIndex)} wheel as one complete tire-and-rim assembly. Press E on the loose wheel to carry it, or hold R on it to separate the tire from the rim.";
            return true;
        }

        public bool TryInstallWheelAssembly(
            int wheelIndex,
            P51LooseWheelAssembly wheelAssembly,
            out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!PrepareService(wheelIndex, out resultMessage))
            {
                return false;
            }
            if (!maintenance.IsGearInstalled(wheelIndex))
            {
                resultMessage = "Reinstall the landing-gear strut before fitting its wheel assembly.";
                return false;
            }
            if (rimInstalled[wheelIndex] || maintenance.IsTireInstalled(wheelIndex))
            {
                resultMessage = "A wheel assembly is already installed on that strut.";
                return false;
            }
            if (wheelAssembly == null || !wheelAssembly.IsCarried || !wheelAssembly.IsComplete)
            {
                resultMessage = "Carry a complete tire-and-rim wheel assembly to this strut before installing it.";
                return false;
            }
            if (!wheelAssembly.CanInstallOn(wheelIndex))
            {
                resultMessage = $"That wheel assembly belongs to the {GetWheelName(wheelAssembly.OriginWheelIndex)} station, not this strut.";
                return false;
            }

            EnginePartConditionData tireCondition = wheelAssembly.CaptureTireCondition();
            EnginePartConditionData rimCondition = wheelAssembly.CaptureRimCondition();
            if (tireCondition == null || rimCondition == null)
            {
                resultMessage = "The carried wheel is missing its saved tire or rim condition.";
                return false;
            }

            GetTireArrays(
                out float[] health,
                out float[] pressure,
                out bool[] installed,
                out bool[] burst);

            health[wheelIndex] = tireCondition.Health;
            pressure[wheelIndex] = tireCondition.TirePressurePsi;
            burst[wheelIndex] = tireCondition.TireFailed;
            installed[wheelIndex] = true;
            rimHealth[wheelIndex] = rimCondition.Health;
            rimInstalled[wheelIndex] = true;
            RefreshMaintenanceVisualsAndPhysics();

            wheelAssembly.CompleteAircraftInstallation();
            resultMessage = $"Installed the complete {maintenance.GetWheelName(wheelIndex)} wheel assembly at {health[wheelIndex]:F0}% tire health and {pressure[wheelIndex]:F1} PSI, with the wheel retaining bolt secured.";
            return true;
        }

        public bool TryRemoveTire(int wheelIndex, PlayerInventory inventory, out string resultMessage)
        {
            _ = wheelIndex;
            _ = inventory;
            resultMessage = "Remove the complete wheel from the aircraft first. Tire/rim separation is performed on the loose wheel assembly.";
            return false;
        }

        public bool TryInstallTire(int wheelIndex, PlayerInventory inventory, out string resultMessage)
        {
            _ = wheelIndex;
            _ = inventory;
            resultMessage = "Rebuild the loose wheel assembly off the aircraft, then carry the completed wheel back to its original strut.";
            return false;
        }

        public bool TryRemoveRim(int wheelIndex, PlayerInventory inventory, out string resultMessage)
        {
            _ = wheelIndex;
            _ = inventory;
            resultMessage = "Remove the complete wheel from the aircraft first. Rim service is performed on the loose wheel assembly.";
            return false;
        }

        public bool TryInstallRim(int wheelIndex, PlayerInventory inventory, out string resultMessage)
        {
            _ = wheelIndex;
            _ = inventory;
            resultMessage = "Rebuild the loose wheel assembly off the aircraft, then carry the completed wheel back to its original strut.";
            return false;
        }

        private string GetWheelName(int wheelIndex)
        {
            return wheelIndex == 0 ? "left main" : wheelIndex == 1 ? "right main" : "tail";
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
                    Array.Copy(rimInstalled, resized, Mathf.Min(rimInstalled.Length, resized.Length));
                }
                rimInstalled = resized;
            }

            if (rimHealth == null || rimHealth.Length != WheelCount)
            {
                float[] resized = { 100f, 100f, 100f };
                if (rimHealth != null)
                {
                    Array.Copy(rimHealth, resized, Mathf.Min(rimHealth.Length, resized.Length));
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
