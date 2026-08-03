using System.Collections.Generic;
using System.Reflection;
using Hanger51.Inventory;
using UnityEngine;

namespace Hanger51.EngineAssembly
{
    [RequireComponent(typeof(EngineAssemblyStation))]
    public sealed class EngineAssemblyRemovalController : MonoBehaviour
    {
        [SerializeField, Min(0.2f)] private float engineRemovalHoldDuration = 1.5f;

        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;

        private EngineAssemblyStation station;
        private FieldInfo engineBlockItemField;
        private FieldInfo cylinderCoverItemField;
        private FieldInfo sparkPlugItemField;
        private FieldInfo engineBlockInstalledField;
        private FieldInfo coverPlacedField;
        private FieldInfo coverBoltsTightenedField;
        private FieldInfo sparkPlugInstalledField;
        private MethodInfo ensureStateListsMethod;
        private MethodInfo refreshVisualsMethod;

        private float engineRemovalProgress;

        public bool IsReady { get; private set; }
        public float EngineRemovalProgress => engineRemovalProgress;
        public bool CanRemoveEngineBlock
        {
            get
            {
                if (!TryGetState(out bool engineInstalled, out List<bool> covers, out _, out _))
                {
                    return false;
                }

                return engineInstalled && CountTrue(covers) == 0;
            }
        }

        public string EngineRemovalInteractionText
        {
            get
            {
                if (!CanRemoveEngineBlock)
                {
                    return string.Empty;
                }

                int percent = Mathf.RoundToInt(engineRemovalProgress * 100f);
                return engineRemovalProgress > 0f
                    ? $"Hold R to remove the V-1650 engine block ({percent}%)"
                    : "Hold R to remove the bare V-1650 engine block";
            }
        }

        private void Awake()
        {
            ResolveReflection();
        }

        private void OnEnable()
        {
            ResolveReflection();
        }

        public bool CanRemoveTarget(
            EngineAssemblyInteractionKind kind,
            int groupIndex,
            int targetIndex)
        {
            if (!TryGetState(
                    out bool engineInstalled,
                    out List<bool> covers,
                    out List<bool> bolts,
                    out List<bool> plugs)
                || !engineInstalled)
            {
                return false;
            }

            switch (kind)
            {
                case EngineAssemblyInteractionKind.SparkPlug:
                    return IsTrue(plugs, targetIndex);

                case EngineAssemblyInteractionKind.CoverBolt:
                    return IsTrue(covers, groupIndex)
                        && IsTrue(bolts, targetIndex)
                        && !HasInstalledSparkPlugOnBank(groupIndex, plugs);

                case EngineAssemblyInteractionKind.CoverPlacement:
                    return IsTrue(covers, groupIndex)
                        && !HasInstalledSparkPlugOnBank(groupIndex, plugs)
                        && AreAllBoltsLooseForBank(groupIndex, bolts);

                default:
                    return false;
            }
        }

        public string GetRemovalInteractionText(
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
                case EngineAssemblyInteractionKind.SparkPlug:
                    return $"Hold R to unscrew spark plug {targetIndex + 1}{progress}";

                case EngineAssemblyInteractionKind.CoverBolt:
                    return $"Hold R to loosen cover bolt {targetIndex + 1}{progress}";

                case EngineAssemblyInteractionKind.CoverPlacement:
                    return $"Hold R to lift off the {bankName} cylinder cover{progress}";

                default:
                    return string.Empty;
            }
        }

        public bool TryRemoveTarget(
            EngineAssemblyInteractionKind kind,
            int groupIndex,
            int targetIndex,
            PlayerInventory inventory,
            out string resultMessage)
        {
            resultMessage = "That engine part cannot be removed yet.";

            if (inventory == null || !CanRemoveTarget(kind, groupIndex, targetIndex))
            {
                return false;
            }

            EnsureStationState();
            List<bool> covers = GetBoolList(coverPlacedField);
            List<bool> bolts = GetBoolList(coverBoltsTightenedField);
            List<bool> plugs = GetBoolList(sparkPlugInstalledField);

            switch (kind)
            {
                case EngineAssemblyInteractionKind.SparkPlug:
                {
                    InventoryItemDefinition item = GetItem(sparkPlugItemField);
                    if (!TryReturnItem(inventory, item))
                    {
                        resultMessage = "Inventory is full. Make room before removing the spark plug.";
                        return false;
                    }

                    plugs[targetIndex] = false;
                    resultMessage = $"Removed spark plug {targetIndex + 1} and returned it to inventory.";
                    break;
                }

                case EngineAssemblyInteractionKind.CoverBolt:
                    bolts[targetIndex] = false;
                    resultMessage = $"Loosened cover bolt {targetIndex + 1}.";
                    break;

                case EngineAssemblyInteractionKind.CoverPlacement:
                {
                    InventoryItemDefinition item = GetItem(cylinderCoverItemField);
                    if (!TryReturnItem(inventory, item))
                    {
                        resultMessage = "Inventory is full. Make room before removing the cylinder cover.";
                        return false;
                    }

                    covers[groupIndex] = false;
                    resultMessage = $"Removed the {(groupIndex == 0 ? "left" : "right")} cylinder cover and returned it to inventory.";
                    break;
                }

                default:
                    return false;
            }

            RefreshStationVisuals();
            return true;
        }

        public bool ProcessEngineRemovalHold(
            PlayerInventory inventory,
            bool isHeld,
            float deltaTime,
            out string resultMessage)
        {
            resultMessage = string.Empty;

            if (!CanRemoveEngineBlock || inventory == null)
            {
                CancelEngineRemovalHold();
                return false;
            }

            if (!isHeld)
            {
                CancelEngineRemovalHold();
                return false;
            }

            engineRemovalProgress = Mathf.Clamp01(
                engineRemovalProgress
                + Mathf.Max(0f, deltaTime) / engineRemovalHoldDuration);

            if (engineRemovalProgress < 1f)
            {
                return false;
            }

            InventoryItemDefinition engineItem = GetItem(engineBlockItemField);
            if (!TryReturnItem(inventory, engineItem))
            {
                resultMessage = "Inventory is full. Make room before removing the engine block.";
                engineRemovalProgress = 0f;
                return false;
            }

            engineBlockInstalledField.SetValue(station, false);
            engineRemovalProgress = 0f;
            RefreshStationVisuals();
            resultMessage = "Removed the V-1650 engine block and returned it to inventory.";
            return true;
        }

        public void CancelEngineRemovalHold()
        {
            engineRemovalProgress = 0f;
        }

        private void ResolveReflection()
        {
            station = GetComponent<EngineAssemblyStation>();
            System.Type stationType = typeof(EngineAssemblyStation);

            engineBlockItemField = stationType.GetField("engineBlockItem", PrivateInstance);
            cylinderCoverItemField = stationType.GetField("cylinderCoverItem", PrivateInstance);
            sparkPlugItemField = stationType.GetField("sparkPlugItem", PrivateInstance);
            engineBlockInstalledField = stationType.GetField("engineBlockInstalled", PrivateInstance);
            coverPlacedField = stationType.GetField("coverPlaced", PrivateInstance);
            coverBoltsTightenedField = stationType.GetField("coverBoltsTightened", PrivateInstance);
            sparkPlugInstalledField = stationType.GetField("sparkPlugInstalled", PrivateInstance);
            ensureStateListsMethod = stationType.GetMethod("EnsureStateLists", PrivateInstance);
            refreshVisualsMethod = stationType.GetMethod("RefreshVisuals", PrivateInstance);

            IsReady = station != null
                && engineBlockItemField != null
                && cylinderCoverItemField != null
                && sparkPlugItemField != null
                && engineBlockInstalledField != null
                && coverPlacedField != null
                && coverBoltsTightenedField != null
                && sparkPlugInstalledField != null
                && ensureStateListsMethod != null
                && refreshVisualsMethod != null;

            if (!IsReady)
            {
                Debug.LogError(
                    "EngineAssemblyRemovalController could not bind to the current EngineAssemblyStation state. Re-run the latest setup after compiling.",
                    this);
            }
        }

        private bool TryGetState(
            out bool engineInstalled,
            out List<bool> covers,
            out List<bool> bolts,
            out List<bool> plugs)
        {
            engineInstalled = false;
            covers = null;
            bolts = null;
            plugs = null;

            if (!IsReady)
            {
                ResolveReflection();
            }

            if (!IsReady)
            {
                return false;
            }

            EnsureStationState();
            engineInstalled = (bool)engineBlockInstalledField.GetValue(station);
            covers = GetBoolList(coverPlacedField);
            bolts = GetBoolList(coverBoltsTightenedField);
            plugs = GetBoolList(sparkPlugInstalledField);
            return covers != null && bolts != null && plugs != null;
        }

        private void EnsureStationState()
        {
            ensureStateListsMethod?.Invoke(station, null);
        }

        private void RefreshStationVisuals()
        {
            // The station's ClampState method was written for forward-only
            // assembly and clears installed plugs whenever any bolt is loose.
            // Disassembly intentionally preserves the opposite bank, so only
            // refresh visual state here.
            refreshVisualsMethod?.Invoke(station, null);
        }

        private InventoryItemDefinition GetItem(FieldInfo field)
        {
            return field?.GetValue(station) as InventoryItemDefinition;
        }

        private List<bool> GetBoolList(FieldInfo field)
        {
            return field?.GetValue(station) as List<bool>;
        }

        private bool HasInstalledSparkPlugOnBank(int bankIndex, List<bool> plugs)
        {
            EngineAssemblyInteractionTarget[] targets =
                station.GetComponentsInChildren<EngineAssemblyInteractionTarget>(true);

            for (int index = 0; index < targets.Length; index++)
            {
                EngineAssemblyInteractionTarget target = targets[index];
                if (target.InteractionKind == EngineAssemblyInteractionKind.SparkPlug
                    && target.GroupIndex == bankIndex
                    && IsTrue(plugs, target.TargetIndex))
                {
                    return true;
                }
            }

            return false;
        }

        private bool AreAllBoltsLooseForBank(int bankIndex, List<bool> bolts)
        {
            bool foundBolt = false;
            EngineAssemblyInteractionTarget[] targets =
                station.GetComponentsInChildren<EngineAssemblyInteractionTarget>(true);

            for (int index = 0; index < targets.Length; index++)
            {
                EngineAssemblyInteractionTarget target = targets[index];
                if (target.InteractionKind != EngineAssemblyInteractionKind.CoverBolt
                    || target.GroupIndex != bankIndex)
                {
                    continue;
                }

                foundBolt = true;
                if (IsTrue(bolts, target.TargetIndex))
                {
                    return false;
                }
            }

            return foundBolt;
        }

        private static bool TryReturnItem(
            PlayerInventory inventory,
            InventoryItemDefinition item)
        {
            return item != null && inventory.AddItem(item, 1) == 0;
        }

        private static bool IsTrue(List<bool> values, int index)
        {
            return values != null
                && index >= 0
                && index < values.Count
                && values[index];
        }

        private static int CountTrue(List<bool> values)
        {
            int count = 0;
            if (values == null)
            {
                return count;
            }

            for (int index = 0; index < values.Count; index++)
            {
                if (values[index])
                {
                    count++;
                }
            }

            return count;
        }

        private void OnDisable()
        {
            CancelEngineRemovalHold();
        }

        private void OnValidate()
        {
            engineRemovalHoldDuration = Mathf.Max(0.2f, engineRemovalHoldDuration);
        }
    }
}
