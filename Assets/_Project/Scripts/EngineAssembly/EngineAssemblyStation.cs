using System.Collections.Generic;
using Hanger51.Inventory;
using UnityEngine;

namespace Hanger51.EngineAssembly
{
    [RequireComponent(typeof(Collider))]
    public sealed class EngineAssemblyStation : MonoBehaviour
    {
        [Header("Required Inventory Items")]
        [SerializeField] private InventoryItemDefinition engineBlockItem;
        [SerializeField] private InventoryItemDefinition cylinderCoverItem;
        [SerializeField] private InventoryItemDefinition sparkPlugItem;

        [Header("Installed Visuals")]
        [SerializeField] private GameObject engineCoreVisual;
        [SerializeField] private List<GameObject> cylinderCoverVisuals = new List<GameObject>();
        [SerializeField] private List<GameObject> sparkPlugVisuals = new List<GameObject>();

        [Header("Assembly State")]
        [SerializeField] private bool engineBlockInstalled;
        [SerializeField, Min(0)] private int cylinderCoversInstalled;
        [SerializeField, Min(0)] private int sparkPlugsInstalled;

        public bool EngineBlockInstalled => engineBlockInstalled;
        public int CylinderCoversInstalled => cylinderCoversInstalled;
        public int SparkPlugsInstalled => sparkPlugsInstalled;
        public int RequiredCylinderCovers => cylinderCoverVisuals.Count;
        public int RequiredSparkPlugs => sparkPlugVisuals.Count;
        public bool IsComplete => engineBlockInstalled
            && cylinderCoversInstalled >= RequiredCylinderCovers
            && sparkPlugsInstalled >= RequiredSparkPlugs;

        public string InteractionText
        {
            get
            {
                if (!engineBlockInstalled)
                {
                    return "Press I to place the V-1650 engine block on the stand";
                }

                if (cylinderCoversInstalled < RequiredCylinderCovers)
                {
                    return $"Press I to install cylinder covers ({cylinderCoversInstalled}/{RequiredCylinderCovers})";
                }

                if (sparkPlugsInstalled < RequiredSparkPlugs)
                {
                    return $"Press I to install spark plugs ({sparkPlugsInstalled}/{RequiredSparkPlugs})";
                }

                return "V-1650 engine assembly complete";
            }
        }

        public string ProgressText
        {
            get
            {
                if (!engineBlockInstalled)
                {
                    return "Install target: Engine stand — engine block required";
                }

                if (cylinderCoversInstalled < RequiredCylinderCovers)
                {
                    return $"V-1650 assembly: covers {cylinderCoversInstalled}/{RequiredCylinderCovers}";
                }

                if (sparkPlugsInstalled < RequiredSparkPlugs)
                {
                    return $"V-1650 assembly: spark plugs {sparkPlugsInstalled}/{RequiredSparkPlugs}";
                }

                return "V-1650 assembly: complete";
            }
        }

        private void Awake()
        {
            ClampState();
            RefreshVisuals();
        }

        public void Configure(
            InventoryItemDefinition configuredEngineBlockItem,
            InventoryItemDefinition configuredCylinderCoverItem,
            InventoryItemDefinition configuredSparkPlugItem,
            GameObject configuredEngineCoreVisual,
            List<GameObject> configuredCylinderCoverVisuals,
            List<GameObject> configuredSparkPlugVisuals)
        {
            engineBlockItem = configuredEngineBlockItem;
            cylinderCoverItem = configuredCylinderCoverItem;
            sparkPlugItem = configuredSparkPlugItem;
            engineCoreVisual = configuredEngineCoreVisual;
            cylinderCoverVisuals = configuredCylinderCoverVisuals ?? new List<GameObject>();
            sparkPlugVisuals = configuredSparkPlugVisuals ?? new List<GameObject>();

            ClampState();
            RefreshVisuals();
        }

        public bool CanInstall(InventoryItemDefinition item, out string reason)
        {
            reason = string.Empty;

            if (item == null)
            {
                reason = "Select an inventory item first.";
                return false;
            }

            if (item == engineBlockItem)
            {
                if (engineBlockInstalled)
                {
                    reason = "The engine block is already on the stand.";
                    return false;
                }

                return true;
            }

            if (item == cylinderCoverItem)
            {
                if (!engineBlockInstalled)
                {
                    reason = "Place the engine block on the stand first.";
                    return false;
                }

                if (cylinderCoversInstalled >= RequiredCylinderCovers)
                {
                    reason = "Both cylinder covers are already installed.";
                    return false;
                }

                return true;
            }

            if (item == sparkPlugItem)
            {
                if (!engineBlockInstalled)
                {
                    reason = "Place the engine block on the stand first.";
                    return false;
                }

                if (cylinderCoversInstalled < RequiredCylinderCovers)
                {
                    reason = "Install both cylinder covers before the spark plugs.";
                    return false;
                }

                if (sparkPlugsInstalled >= RequiredSparkPlugs)
                {
                    reason = "All spark plugs are already installed.";
                    return false;
                }

                return true;
            }

            reason = $"{item.DisplayName} does not install on this engine stand.";
            return false;
        }

        public string GetInstallButtonLabel(InventoryItemDefinition item)
        {
            if (item == engineBlockItem)
            {
                return "Place Engine Block";
            }

            if (item == cylinderCoverItem)
            {
                return "Install Cover";
            }

            if (item == sparkPlugItem)
            {
                return "Install Spark Plug";
            }

            return "Install";
        }

        public bool TryInstall(
            PlayerInventory inventory,
            int slotIndex,
            out string resultMessage)
        {
            resultMessage = "Unable to install the selected item.";

            if (inventory == null)
            {
                resultMessage = "Player inventory is missing.";
                return false;
            }

            InventorySlotData slot = inventory.GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty)
            {
                resultMessage = "Select an occupied inventory slot.";
                return false;
            }

            InventoryItemDefinition selectedItem = slot.Item;
            if (!CanInstall(selectedItem, out resultMessage))
            {
                return false;
            }

            if (!inventory.TryRemoveFromSlot(
                    slotIndex,
                    1,
                    out InventoryItemDefinition removedItem,
                    out int removedQuantity)
                || removedItem != selectedItem
                || removedQuantity != 1)
            {
                resultMessage = "The selected item could not be removed from inventory.";
                return false;
            }

            if (selectedItem == engineBlockItem)
            {
                engineBlockInstalled = true;
                resultMessage = "Placed the Merlin V-1650 engine block on the stand.";
            }
            else if (selectedItem == cylinderCoverItem)
            {
                cylinderCoversInstalled++;
                resultMessage = $"Installed cylinder cover {cylinderCoversInstalled}/{RequiredCylinderCovers}.";
            }
            else if (selectedItem == sparkPlugItem)
            {
                sparkPlugsInstalled++;
                resultMessage = $"Installed spark plug {sparkPlugsInstalled}/{RequiredSparkPlugs}.";
            }

            ClampState();
            RefreshVisuals();
            return true;
        }

        public void ResetAssembly()
        {
            engineBlockInstalled = false;
            cylinderCoversInstalled = 0;
            sparkPlugsInstalled = 0;
            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            if (engineCoreVisual != null)
            {
                engineCoreVisual.SetActive(engineBlockInstalled);
            }

            for (int index = 0; index < cylinderCoverVisuals.Count; index++)
            {
                if (cylinderCoverVisuals[index] != null)
                {
                    cylinderCoverVisuals[index].SetActive(
                        engineBlockInstalled && index < cylinderCoversInstalled);
                }
            }

            for (int index = 0; index < sparkPlugVisuals.Count; index++)
            {
                if (sparkPlugVisuals[index] != null)
                {
                    sparkPlugVisuals[index].SetActive(
                        engineBlockInstalled && index < sparkPlugsInstalled);
                }
            }
        }

        private void ClampState()
        {
            cylinderCoversInstalled = Mathf.Clamp(
                cylinderCoversInstalled,
                0,
                RequiredCylinderCovers);

            sparkPlugsInstalled = Mathf.Clamp(
                sparkPlugsInstalled,
                0,
                RequiredSparkPlugs);

            if (!engineBlockInstalled)
            {
                cylinderCoversInstalled = 0;
                sparkPlugsInstalled = 0;
            }
        }

        private void OnValidate()
        {
            ClampState();
            RefreshVisuals();
        }
    }
}
