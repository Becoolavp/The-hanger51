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

        [Header("Physical Interaction Targets")]
        [SerializeField] private List<EngineAssemblyInteractionTarget> coverPlacementTargets =
            new List<EngineAssemblyInteractionTarget>();
        [SerializeField] private List<EngineAssemblyInteractionTarget> coverBoltTargets =
            new List<EngineAssemblyInteractionTarget>();
        [SerializeField] private List<EngineAssemblyInteractionTarget> sparkPlugTargets =
            new List<EngineAssemblyInteractionTarget>();

        [Header("Assembly State")]
        [SerializeField] private bool engineBlockInstalled;
        [SerializeField] private List<bool> coverPlaced = new List<bool>();
        [SerializeField] private List<bool> coverBoltsTightened = new List<bool>();
        [SerializeField] private List<bool> sparkPlugInstalled = new List<bool>();

        private PlayerInventory trackedInventory;

        public bool EngineBlockInstalled => engineBlockInstalled;
        public int CylinderCoversInstalled => CountCompleted(coverPlaced);
        public int CylinderCoversSecured => CountSecuredCovers();
        public int CoverBoltsTightened => CountCompleted(coverBoltsTightened);
        public int SparkPlugsInstalled => CountCompleted(sparkPlugInstalled);
        public int RequiredCylinderCovers => cylinderCoverVisuals.Count;
        public int RequiredCoverBolts => coverBoltTargets.Count;
        public int RequiredSparkPlugs => sparkPlugVisuals.Count;
        public bool IsComplete => engineBlockInstalled
            && CylinderCoversSecured >= RequiredCylinderCovers
            && SparkPlugsInstalled >= RequiredSparkPlugs;

        public string InteractionText
        {
            get
            {
                if (!engineBlockInstalled)
                {
                    return "Press I to place the V-1650 engine block on the stand";
                }

                if (CylinderCoversInstalled < RequiredCylinderCovers)
                {
                    if (trackedInventory != null
                        && trackedInventory.EquippedItem == cylinderCoverItem)
                    {
                        return "A cover mounting area is highlighted — aim at it and hold E";
                    }

                    return $"Equip a cylinder cover to reveal its mount ({CylinderCoversInstalled}/{RequiredCylinderCovers})";
                }

                if (CoverBoltsTightened < RequiredCoverBolts)
                {
                    return $"Tighten the highlighted cover bolts ({CoverBoltsTightened}/{RequiredCoverBolts})";
                }

                if (SparkPlugsInstalled < RequiredSparkPlugs)
                {
                    if (trackedInventory != null
                        && trackedInventory.EquippedItem == sparkPlugItem)
                    {
                        return "Spark-plug wells are highlighted — aim at one and hold E";
                    }

                    return $"Equip a spark plug to reveal the open wells ({SparkPlugsInstalled}/{RequiredSparkPlugs})";
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

                if (CylinderCoversInstalled < RequiredCylinderCovers)
                {
                    return $"V-1650 assembly: covers placed {CylinderCoversInstalled}/{RequiredCylinderCovers}";
                }

                if (CoverBoltsTightened < RequiredCoverBolts)
                {
                    return $"V-1650 assembly: cover bolts tightened {CoverBoltsTightened}/{RequiredCoverBolts}";
                }

                if (SparkPlugsInstalled < RequiredSparkPlugs)
                {
                    return $"V-1650 assembly: spark plugs seated {SparkPlugsInstalled}/{RequiredSparkPlugs}";
                }

                return "V-1650 assembly: complete";
            }
        }

        private void Awake()
        {
            EnsureStateLists();
            FindTrackedInventory();
            RefreshVisuals();
        }

        private void OnEnable()
        {
            FindTrackedInventory();
            SubscribeToInventory();
            RefreshVisuals();
        }

        private void OnDisable()
        {
            UnsubscribeFromInventory();
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

            EnsureStateLists();
            ClampState();
            RefreshVisuals();
        }

        public void ConfigureFastenerSystem(
            List<EngineAssemblyInteractionTarget> configuredCoverPlacementTargets,
            List<EngineAssemblyInteractionTarget> configuredCoverBoltTargets,
            List<EngineAssemblyInteractionTarget> configuredSparkPlugTargets)
        {
            coverPlacementTargets = configuredCoverPlacementTargets
                ?? new List<EngineAssemblyInteractionTarget>();
            coverBoltTargets = configuredCoverBoltTargets
                ?? new List<EngineAssemblyInteractionTarget>();
            sparkPlugTargets = configuredSparkPlugTargets
                ?? new List<EngineAssemblyInteractionTarget>();

            EnsureStateLists();
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
                reason = "Equip the cover, close inventory, and hold E on the highlighted bank.";
                return false;
            }

            if (item == sparkPlugItem)
            {
                reason = "Equip a spark plug, close inventory, and hold E on a highlighted plug well.";
                return false;
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
                return "Equip, Then Use Highlight";
            }

            if (item == sparkPlugItem)
            {
                return "Equip, Then Screw In";
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

            if (selectedItem != engineBlockItem)
            {
                resultMessage = "Only the engine block is placed from the inventory button.";
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
                resultMessage = "The engine block could not be removed from inventory.";
                return false;
            }

            engineBlockInstalled = true;
            resultMessage = "Placed the Merlin V-1650 engine block on the stand. Equip a cover next.";
            RefreshVisuals();
            return true;
        }

        public bool IsTargetAvailable(
            EngineAssemblyInteractionKind kind,
            int groupIndex,
            int targetIndex)
        {
            EnsureStateLists();

            switch (kind)
            {
                case EngineAssemblyInteractionKind.CoverPlacement:
                    return engineBlockInstalled
                        && IsValidIndex(coverPlaced, groupIndex)
                        && !coverPlaced[groupIndex]
                        && trackedInventory != null
                        && trackedInventory.EquippedItem == cylinderCoverItem;

                case EngineAssemblyInteractionKind.CoverBolt:
                    return engineBlockInstalled
                        && IsValidIndex(coverPlaced, groupIndex)
                        && coverPlaced[groupIndex]
                        && IsValidIndex(coverBoltsTightened, targetIndex)
                        && !coverBoltsTightened[targetIndex];

                case EngineAssemblyInteractionKind.SparkPlug:
                    return engineBlockInstalled
                        && AreAllCoversSecured()
                        && IsValidIndex(sparkPlugInstalled, targetIndex)
                        && !sparkPlugInstalled[targetIndex]
                        && trackedInventory != null
                        && trackedInventory.EquippedItem == sparkPlugItem;

                default:
                    return false;
            }
        }

        public bool ShouldHighlightTarget(
            EngineAssemblyInteractionKind kind,
            int groupIndex,
            int targetIndex)
        {
            return IsTargetAvailable(kind, groupIndex, targetIndex);
        }

        public bool IsTargetComplete(
            EngineAssemblyInteractionKind kind,
            int groupIndex,
            int targetIndex)
        {
            EnsureStateLists();

            switch (kind)
            {
                case EngineAssemblyInteractionKind.CoverPlacement:
                    return IsValidIndex(coverPlaced, groupIndex) && coverPlaced[groupIndex];

                case EngineAssemblyInteractionKind.CoverBolt:
                    return IsValidIndex(coverBoltsTightened, targetIndex)
                        && coverBoltsTightened[targetIndex];

                case EngineAssemblyInteractionKind.SparkPlug:
                    return IsValidIndex(sparkPlugInstalled, targetIndex)
                        && sparkPlugInstalled[targetIndex];

                default:
                    return false;
            }
        }

        public string GetTargetInteractionText(
            EngineAssemblyInteractionKind kind,
            int groupIndex,
            int targetIndex,
            float holdProgress)
        {
            int percent = Mathf.RoundToInt(Mathf.Clamp01(holdProgress) * 100f);
            string progress = holdProgress > 0f ? $" ({percent}%)" : string.Empty;
            string bankName = groupIndex == 0 ? "left" : "right";

            switch (kind)
            {
                case EngineAssemblyInteractionKind.CoverPlacement:
                    return $"Hold E to lower the {bankName} cylinder cover into place{progress}";

                case EngineAssemblyInteractionKind.CoverBolt:
                    return $"Hold E to tighten highlighted cover bolt {targetIndex + 1}/{RequiredCoverBolts}{progress}";

                case EngineAssemblyInteractionKind.SparkPlug:
                    return $"Hold E to screw in spark plug {targetIndex + 1}/{RequiredSparkPlugs}{progress}";

                default:
                    return string.Empty;
            }
        }

        public bool TryCompleteTarget(
            EngineAssemblyInteractionKind kind,
            int groupIndex,
            int targetIndex,
            PlayerInventory inventory,
            out string resultMessage)
        {
            resultMessage = "The highlighted engine part could not be completed.";

            if (!IsTargetAvailable(kind, groupIndex, targetIndex))
            {
                resultMessage = "That engine target is not ready yet.";
                return false;
            }

            switch (kind)
            {
                case EngineAssemblyInteractionKind.CoverPlacement:
                    if (!TryConsumeEquippedItem(inventory, cylinderCoverItem))
                    {
                        resultMessage = "The equipped cylinder cover is no longer in inventory.";
                        return false;
                    }

                    coverPlaced[groupIndex] = true;
                    resultMessage = $"Placed the {(groupIndex == 0 ? "left" : "right")} cover. Tighten its highlighted bolts.";
                    break;

                case EngineAssemblyInteractionKind.CoverBolt:
                    coverBoltsTightened[targetIndex] = true;
                    resultMessage = $"Tightened cover bolt {CoverBoltsTightened}/{RequiredCoverBolts}.";
                    break;

                case EngineAssemblyInteractionKind.SparkPlug:
                    if (!TryConsumeEquippedItem(inventory, sparkPlugItem))
                    {
                        resultMessage = "The equipped spark plug is no longer in inventory.";
                        return false;
                    }

                    sparkPlugInstalled[targetIndex] = true;
                    resultMessage = $"Seated spark plug {SparkPlugsInstalled}/{RequiredSparkPlugs}.";
                    break;

                default:
                    return false;
            }

            ClampState();
            RefreshVisuals();
            return true;
        }

        public void ResetAssembly()
        {
            engineBlockInstalled = false;
            EnsureStateLists();
            SetAll(coverPlaced, false);
            SetAll(coverBoltsTightened, false);
            SetAll(sparkPlugInstalled, false);
            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            EnsureStateLists();

            if (engineCoreVisual != null)
            {
                engineCoreVisual.SetActive(engineBlockInstalled);
            }

            for (int index = 0; index < cylinderCoverVisuals.Count; index++)
            {
                if (cylinderCoverVisuals[index] != null)
                {
                    cylinderCoverVisuals[index].SetActive(
                        engineBlockInstalled
                        && IsValidIndex(coverPlaced, index)
                        && coverPlaced[index]);
                }
            }

            for (int index = 0; index < sparkPlugVisuals.Count; index++)
            {
                if (sparkPlugVisuals[index] != null)
                {
                    sparkPlugVisuals[index].SetActive(
                        engineBlockInstalled
                        && IsValidIndex(sparkPlugInstalled, index)
                        && sparkPlugInstalled[index]);
                }
            }

            RefreshTargets(coverPlacementTargets);
            RefreshTargets(coverBoltTargets);
            RefreshTargets(sparkPlugTargets);
        }

        private void FindTrackedInventory()
        {
            PlayerInventory foundInventory = FindFirstObjectByType<PlayerInventory>();
            if (foundInventory == trackedInventory)
            {
                return;
            }

            UnsubscribeFromInventory();
            trackedInventory = foundInventory;
            SubscribeToInventory();
        }

        private void SubscribeToInventory()
        {
            if (trackedInventory != null)
            {
                trackedInventory.InventoryChanged -= HandleInventoryChanged;
                trackedInventory.InventoryChanged += HandleInventoryChanged;
            }
        }

        private void UnsubscribeFromInventory()
        {
            if (trackedInventory != null)
            {
                trackedInventory.InventoryChanged -= HandleInventoryChanged;
            }
        }

        private void HandleInventoryChanged()
        {
            RefreshTargets(coverPlacementTargets);
            RefreshTargets(coverBoltTargets);
            RefreshTargets(sparkPlugTargets);
        }

        private bool TryConsumeEquippedItem(
            PlayerInventory inventory,
            InventoryItemDefinition requiredItem)
        {
            if (inventory == null
                || requiredItem == null
                || inventory.EquippedItem != requiredItem)
            {
                return false;
            }

            for (int slotIndex = 0; slotIndex < inventory.Slots.Count; slotIndex++)
            {
                InventorySlotData slot = inventory.GetSlot(slotIndex);
                if (slot == null || slot.IsEmpty || slot.Item != requiredItem)
                {
                    continue;
                }

                return inventory.TryRemoveFromSlot(
                    slotIndex,
                    1,
                    out InventoryItemDefinition removedItem,
                    out int removedQuantity)
                    && removedItem == requiredItem
                    && removedQuantity == 1;
            }

            return false;
        }

        private bool AreAllCoversSecured()
        {
            if (RequiredCylinderCovers <= 0
                || CylinderCoversInstalled < RequiredCylinderCovers
                || RequiredCoverBolts <= 0)
            {
                return false;
            }

            for (int coverIndex = 0; coverIndex < RequiredCylinderCovers; coverIndex++)
            {
                if (!IsCoverSecured(coverIndex))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsCoverSecured(int coverIndex)
        {
            if (!IsValidIndex(coverPlaced, coverIndex) || !coverPlaced[coverIndex])
            {
                return false;
            }

            bool foundBolt = false;
            for (int index = 0; index < coverBoltTargets.Count; index++)
            {
                EngineAssemblyInteractionTarget target = coverBoltTargets[index];
                if (target == null || target.GroupIndex != coverIndex)
                {
                    continue;
                }

                foundBolt = true;
                if (!IsValidIndex(coverBoltsTightened, target.TargetIndex)
                    || !coverBoltsTightened[target.TargetIndex])
                {
                    return false;
                }
            }

            return foundBolt;
        }

        private int CountSecuredCovers()
        {
            int count = 0;
            for (int index = 0; index < RequiredCylinderCovers; index++)
            {
                if (IsCoverSecured(index))
                {
                    count++;
                }
            }

            return count;
        }

        private void EnsureStateLists()
        {
            ResizeBoolList(coverPlaced, RequiredCylinderCovers);
            ResizeBoolList(coverBoltsTightened, coverBoltTargets.Count);
            ResizeBoolList(sparkPlugInstalled, RequiredSparkPlugs);
        }

        private void ClampState()
        {
            EnsureStateLists();

            if (!engineBlockInstalled)
            {
                SetAll(coverPlaced, false);
                SetAll(coverBoltsTightened, false);
                SetAll(sparkPlugInstalled, false);
                return;
            }

            for (int index = 0; index < coverBoltTargets.Count; index++)
            {
                EngineAssemblyInteractionTarget target = coverBoltTargets[index];
                if (target == null
                    || !IsValidIndex(coverPlaced, target.GroupIndex)
                    || coverPlaced[target.GroupIndex])
                {
                    continue;
                }

                if (IsValidIndex(coverBoltsTightened, target.TargetIndex))
                {
                    coverBoltsTightened[target.TargetIndex] = false;
                }
            }

            if (!AreAllCoversSecured())
            {
                SetAll(sparkPlugInstalled, false);
            }
        }

        private static void RefreshTargets(
            List<EngineAssemblyInteractionTarget> targets)
        {
            for (int index = 0; index < targets.Count; index++)
            {
                if (targets[index] != null)
                {
                    targets[index].RefreshFromStation();
                }
            }
        }

        private static void ResizeBoolList(List<bool> list, int requiredCount)
        {
            requiredCount = Mathf.Max(0, requiredCount);

            while (list.Count < requiredCount)
            {
                list.Add(false);
            }

            if (list.Count > requiredCount)
            {
                list.RemoveRange(requiredCount, list.Count - requiredCount);
            }
        }

        private static void SetAll(List<bool> list, bool value)
        {
            for (int index = 0; index < list.Count; index++)
            {
                list[index] = value;
            }
        }

        private static int CountCompleted(List<bool> list)
        {
            int count = 0;
            for (int index = 0; index < list.Count; index++)
            {
                if (list[index])
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsValidIndex(List<bool> list, int index)
        {
            return index >= 0 && index < list.Count;
        }

        private void OnValidate()
        {
            EnsureStateLists();
            ClampState();
            RefreshVisuals();
        }
    }
}
