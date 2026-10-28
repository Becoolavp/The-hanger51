using System;
using System.Reflection;
using Hanger51.Inventory;
using UnityEngine;

namespace Hanger51.EngineAssembly
{
    [DefaultExecutionOrder(940)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EngineConditionController))]
    public sealed class EnginePartConditionPersistenceController : MonoBehaviour
    {
        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;

        private EngineConditionController condition;
        private FieldInfo engineBlockHealthField;
        private FieldInfo coverHealthField;
        private FieldInfo sparkPlugHealthField;
        private FieldInfo oilQuantityField;
        private FieldInfo oilCapacityField;
        private FieldInfo previousCoverInstalledField;
        private FieldInfo previousPlugInstalledField;
        private MethodInfo ensureStateArraysMethod;
        private MethodInfo recalculateConditionMethod;
        private MethodInfo refreshConditionVisualsMethod;
        private bool bindingErrorLogged;

        public bool IsReady { get; private set; }

        private void Awake()
        {
            ResolveBindings(true);
        }

        private void OnEnable()
        {
            ResolveBindings(true);
        }

        public bool InitializeNow()
        {
            ResolveBindings(false);
            return IsReady;
        }

        public EnginePartConditionData CapturePart(
            EngineAssemblyInteractionKind kind,
            int partIndex)
        {
            ResolveBindings(false);
            if (condition == null)
            {
                return null;
            }

            switch (kind)
            {
                case EngineAssemblyInteractionKind.CoverPlacement:
                    return EnginePartConditionData.Create(
                        EnginePartConditionKind.CylinderCover,
                        condition.GetCoverHealth(partIndex));

                case EngineAssemblyInteractionKind.SparkPlug:
                    return EnginePartConditionData.Create(
                        EnginePartConditionKind.SparkPlug,
                        condition.GetSparkPlugHealth(partIndex));

                default:
                    return null;
            }
        }

        public EnginePartConditionData CaptureEngineBlock()
        {
            ResolveBindings(false);
            return condition != null
                ? EnginePartConditionData.Create(
                    EnginePartConditionKind.EngineBlock,
                    condition.EngineBlockHealth,
                    condition.OilQuantityLiters,
                    condition.OilCapacityLiters)
                : null;
        }

        public void ApplyInstalledPart(
            EngineAssemblyInteractionKind kind,
            int partIndex,
            EnginePartConditionData installedCondition)
        {
            ResolveBindings(false);
            if (!IsReady || condition == null)
            {
                return;
            }

            try
            {
                ensureStateArraysMethod.Invoke(condition, null);
                switch (kind)
                {
                    case EngineAssemblyInteractionKind.CoverPlacement:
                        ApplyCover(partIndex, installedCondition);
                        break;
                    case EngineAssemblyInteractionKind.SparkPlug:
                        ApplySparkPlug(partIndex, installedCondition);
                        break;
                    default:
                        return;
                }
                RefreshCondition();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"Could not restore installed engine-part condition: {exception.Message}",
                    this);
            }
        }

        public void ApplyInstalledEngineBlock(
            EnginePartConditionData installedCondition)
        {
            ResolveBindings(false);
            if (!IsReady || condition == null)
            {
                return;
            }

            try
            {
                ensureStateArraysMethod.Invoke(condition, null);
                EnginePartConditionData state = Normalize(
                    installedCondition,
                    EnginePartConditionKind.EngineBlock);
                engineBlockHealthField.SetValue(condition, state.Health);

                float capacity = state.OilCapacityLiters > 0.1f
                    ? state.OilCapacityLiters
                    : condition.OilCapacityLiters;
                oilCapacityField.SetValue(condition, Mathf.Max(1f, capacity));
                oilQuantityField.SetValue(
                    condition,
                    Mathf.Clamp(state.OilQuantityLiters, 0f, Mathf.Max(1f, capacity)));
                RefreshCondition();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"Could not restore installed engine-block condition: {exception.Message}",
                    this);
            }
        }

        public bool ValidateConfiguration(out string details)
        {
            ResolveBindings(false);
            details = IsReady
                ? "block, cover, plug, oil, installation-history, and visual refresh bindings are ready"
                : "one or more condition persistence bindings are missing";
            return IsReady;
        }

        private void ApplyCover(
            int coverIndex,
            EnginePartConditionData installedCondition)
        {
            float[] health = coverHealthField.GetValue(condition) as float[];
            bool[] previous = previousCoverInstalledField.GetValue(condition) as bool[];
            if (health == null || coverIndex < 0 || coverIndex >= health.Length)
            {
                return;
            }

            EnginePartConditionData state = Normalize(
                installedCondition,
                EnginePartConditionKind.CylinderCover);
            health[coverIndex] = state.Health;
            if (previous != null && coverIndex < previous.Length)
            {
                previous[coverIndex] = true;
            }
        }

        private void ApplySparkPlug(
            int plugIndex,
            EnginePartConditionData installedCondition)
        {
            float[] health = sparkPlugHealthField.GetValue(condition) as float[];
            bool[] previous = previousPlugInstalledField.GetValue(condition) as bool[];
            if (health == null || plugIndex < 0 || plugIndex >= health.Length)
            {
                return;
            }

            EnginePartConditionData state = Normalize(
                installedCondition,
                EnginePartConditionKind.SparkPlug);
            health[plugIndex] = state.Health;
            if (previous != null && plugIndex < previous.Length)
            {
                previous[plugIndex] = true;
            }
        }

        private static EnginePartConditionData Normalize(
            EnginePartConditionData source,
            EnginePartConditionKind requiredKind)
        {
            if (source != null && source.Kind == requiredKind)
            {
                source.EnsureValid();
                return source;
            }

            return requiredKind == EnginePartConditionKind.EngineBlock
                ? EnginePartConditionData.Create(requiredKind, 100f, 20f, 20f)
                : EnginePartConditionData.Create(requiredKind, 100f);
        }

        private void RefreshCondition()
        {
            recalculateConditionMethod?.Invoke(condition, null);
            refreshConditionVisualsMethod?.Invoke(condition, new object[] { true });
        }

        private void ResolveBindings(bool logFailure)
        {
            condition = GetComponent<EngineConditionController>();
            Type type = typeof(EngineConditionController);
            engineBlockHealthField = type.GetField("engineBlockHealth", PrivateInstance);
            coverHealthField = type.GetField("coverHealth", PrivateInstance);
            sparkPlugHealthField = type.GetField("sparkPlugHealth", PrivateInstance);
            oilQuantityField = type.GetField("oilQuantityLiters", PrivateInstance);
            oilCapacityField = type.GetField("oilCapacityLiters", PrivateInstance);
            previousCoverInstalledField = type.GetField(
                "previousCoverInstalled",
                PrivateInstance);
            previousPlugInstalledField = type.GetField(
                "previousPlugInstalled",
                PrivateInstance);
            ensureStateArraysMethod = type.GetMethod("EnsureStateArrays", PrivateInstance);
            recalculateConditionMethod = type.GetMethod(
                "RecalculateCondition",
                PrivateInstance);
            refreshConditionVisualsMethod = type.GetMethod(
                "RefreshConditionVisuals",
                PrivateInstance);

            IsReady = condition != null
                && engineBlockHealthField != null
                && coverHealthField != null
                && sparkPlugHealthField != null
                && oilQuantityField != null
                && oilCapacityField != null
                && previousCoverInstalledField != null
                && previousPlugInstalledField != null
                && ensureStateArraysMethod != null
                && recalculateConditionMethod != null
                && refreshConditionVisualsMethod != null;

            if (IsReady)
            {
                bindingErrorLogged = false;
                return;
            }

            if (logFailure && !bindingErrorLogged)
            {
                bindingErrorLogged = true;
                Debug.LogError(
                    "Engine-part condition persistence could not bind to the Merlin condition system. Re-run the latest setup after compiling.",
                    this);
            }
        }

        private void OnValidate()
        {
            ResolveBindings(false);
        }
    }
}
