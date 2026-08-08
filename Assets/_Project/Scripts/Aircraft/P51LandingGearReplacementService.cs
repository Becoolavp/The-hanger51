using System;
using System.Reflection;
using Hanger51.Inventory;
using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(P51LandingGearMaintenanceController))]
    public sealed class P51LandingGearReplacementService : MonoBehaviour
    {
        public const string MainTireItemId = "p51-main-landing-tire";
        public const string TailTireItemId = "p51-tailwheel-tire";

        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;

        private P51LandingGearMaintenanceController maintenance;
        private FieldInfo tireHealthField;
        private FieldInfo tirePressureField;
        private FieldInfo tireInstalledField;
        private FieldInfo tireBurstField;
        private FieldInfo looseTireObjectsField;
        private MethodInfo ensureArraysMethod;
        private MethodInfo applyVisualStateMethod;
        private MethodInfo pushPhysicsStateMethod;

        public bool IsReady { get; private set; }

        private void Awake()
        {
            ResolveBindings();
        }

        private void OnEnable()
        {
            ResolveBindings();
        }

        public bool CanUseEquippedReplacement(int wheelIndex, PlayerInventory inventory)
        {
            if (inventory == null || inventory.EquippedItem == null)
            {
                return false;
            }

            string expected = wheelIndex == 2 ? TailTireItemId : MainTireItemId;
            return inventory.EquippedItem.ItemId == expected;
        }

        public bool TryInstallReplacementTire(
            int wheelIndex,
            PlayerInventory inventory,
            out string resultMessage)
        {
            resultMessage = string.Empty;
            ResolveBindings();
            if (!IsReady || maintenance == null)
            {
                resultMessage = "The landing-gear replacement system is not ready.";
                return false;
            }
            if (wheelIndex < 0 || wheelIndex > 2)
            {
                resultMessage = "That wheel station is invalid.";
                return false;
            }
            if (!maintenance.CanService(out resultMessage))
            {
                return false;
            }
            if (!maintenance.IsGearInstalled(wheelIndex))
            {
                resultMessage = "Reinstall the landing-gear assembly before fitting a replacement tire.";
                return false;
            }
            if (maintenance.IsTireInstalled(wheelIndex))
            {
                resultMessage = "Remove the currently installed tire from the rim first.";
                return false;
            }
            if (!CanUseEquippedReplacement(wheelIndex, inventory))
            {
                string needed = wheelIndex == 2
                    ? "P-51 Tailwheel Tire"
                    : "P-51 Main Landing Tire";
                resultMessage = $"Equip a {needed} from inventory before fitting a new tire.";
                return false;
            }

            InventoryItemDefinition replacement = inventory.EquippedItem;
            if (!inventory.TryRemoveFirstItem(replacement, out _))
            {
                resultMessage = "The equipped replacement tire could not be consumed from inventory.";
                return false;
            }

            try
            {
                ensureArraysMethod.Invoke(maintenance, null);
                float[] health = tireHealthField.GetValue(maintenance) as float[];
                float[] pressure = tirePressureField.GetValue(maintenance) as float[];
                bool[] installed = tireInstalledField.GetValue(maintenance) as bool[];
                bool[] burst = tireBurstField.GetValue(maintenance) as bool[];
                GameObject[] looseTires = looseTireObjectsField.GetValue(maintenance) as GameObject[];
                if (health == null || pressure == null || installed == null || burst == null
                    || wheelIndex >= health.Length
                    || wheelIndex >= pressure.Length
                    || wheelIndex >= installed.Length
                    || wheelIndex >= burst.Length)
                {
                    inventory.AddItem(replacement, 1);
                    resultMessage = "The replacement tire state could not be applied; the item was returned to inventory.";
                    return false;
                }

                // The removed old tire remains physically on the floor as its own
                // damaged part. Clear only the controller's reference so future
                // removals do not destroy that historical tire object.
                if (looseTires != null && wheelIndex < looseTires.Length)
                {
                    looseTires[wheelIndex] = null;
                }

                health[wheelIndex] = 100f;
                pressure[wheelIndex] = wheelIndex == 2 ? 6f : 8f;
                burst[wheelIndex] = false;
                installed[wheelIndex] = true;
                applyVisualStateMethod.Invoke(maintenance, new object[] { true });
                pushPhysicsStateMethod.Invoke(maintenance, null);

                float correct = maintenance.GetProperPressure(wheelIndex);
                resultMessage = $"Fitted a new {maintenance.GetWheelName(wheelIndex)} tire to the rim. It is new but only partially inflated; use the nitrogen cart to set it to {correct:F0} PSI before flight.";
                return true;
            }
            catch (Exception exception)
            {
                inventory.AddItem(replacement, 1);
                resultMessage = $"Replacement tire installation failed and the item was returned: {exception.Message}";
                return false;
            }
        }

        private void ResolveBindings()
        {
            if (maintenance == null)
            {
                maintenance = GetComponent<P51LandingGearMaintenanceController>();
            }

            Type type = typeof(P51LandingGearMaintenanceController);
            tireHealthField = type.GetField("tireHealth", PrivateInstance);
            tirePressureField = type.GetField("tirePressurePsi", PrivateInstance);
            tireInstalledField = type.GetField("tireInstalled", PrivateInstance);
            tireBurstField = type.GetField("tireBurst", PrivateInstance);
            looseTireObjectsField = type.GetField("looseTireObjects", PrivateInstance);
            ensureArraysMethod = type.GetMethod("EnsureArrays", PrivateInstance);
            applyVisualStateMethod = type.GetMethod("ApplyVisualState", PrivateInstance);
            pushPhysicsStateMethod = type.GetMethod("PushPhysicsState", PrivateInstance);

            IsReady = maintenance != null
                && tireHealthField != null
                && tirePressureField != null
                && tireInstalledField != null
                && tireBurstField != null
                && looseTireObjectsField != null
                && ensureArraysMethod != null
                && applyVisualStateMethod != null
                && pushPhysicsStateMethod != null;
        }

        private void OnValidate()
        {
            ResolveBindings();
        }
    }
}
