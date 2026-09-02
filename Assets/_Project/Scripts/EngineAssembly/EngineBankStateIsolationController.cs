using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Hanger51.EngineAssembly
{
    [DefaultExecutionOrder(950)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EngineAssemblyStation))]
    public sealed class EngineBankStateIsolationController : MonoBehaviour
    {
        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;

        private EngineAssemblyStation station;
        private FieldInfo coverPlacedField;
        private FieldInfo coverBoltsTightenedField;
        private FieldInfo sparkPlugInstalledField;
        private FieldInfo coverBoltTargetsField;
        private FieldInfo sparkPlugTargetsField;
        private MethodInfo ensureStateListsMethod;
        private MethodInfo refreshVisualsMethod;

        private readonly List<bool> previousCovers = new List<bool>();
        private readonly List<bool> previousBolts = new List<bool>();
        private readonly List<bool> previousPlugs = new List<bool>();

        private bool initialized;
        private bool bindingErrorLogged;

        public bool IsReady { get; private set; }

        private void Awake()
        {
            InitializeNow();
        }

        private void OnEnable()
        {
            InitializeNow();
        }

        public bool InitializeNow()
        {
            ResolveBindings(false);
            if (!IsReady || !TryReadState(
                    out List<bool> covers,
                    out List<bool> bolts,
                    out List<bool> plugs))
            {
                initialized = false;
                return false;
            }

            CaptureState(covers, bolts, plugs);
            initialized = true;
            return true;
        }

        public bool ValidateConfiguration(out string details)
        {
            ResolveBindings(false);
            if (!IsReady)
            {
                details = "reflection bindings are incomplete";
                return false;
            }

            List<EngineAssemblyInteractionTarget> bolts = GetRegisteredTargets(
                coverBoltTargetsField,
                EngineAssemblyInteractionKind.CoverBolt);
            List<EngineAssemblyInteractionTarget> plugs = GetRegisteredTargets(
                sparkPlugTargetsField,
                EngineAssemblyInteractionKind.SparkPlug);

            int leftBolts = CountTargetsOnBank(bolts, 0);
            int rightBolts = CountTargetsOnBank(bolts, 1);
            int leftPlugs = CountTargetsOnBank(plugs, 0);
            int rightPlugs = CountTargetsOnBank(plugs, 1);

            details = $"left bolts={leftBolts}, right bolts={rightBolts}, "
                + $"left plugs={leftPlugs}, right plugs={rightPlugs}";
            return leftBolts == 6
                && rightBolts == 6
                && leftPlugs == 12
                && rightPlugs == 12;
        }

        private void LateUpdate()
        {
            if (!TryReadState(
                    out List<bool> covers,
                    out List<bool> bolts,
                    out List<bool> plugs))
            {
                initialized = false;
                return;
            }

            if (!initialized)
            {
                CaptureState(covers, bolts, plugs);
                initialized = true;
                return;
            }

            bool coverInstalledThisFrame = HasFalseToTrueTransition(
                previousCovers,
                covers);
            bool boltTightenedThisFrame = HasFalseToTrueTransition(
                previousBolts,
                bolts);

            bool restoredAny = false;
            if (coverInstalledThisFrame || boltTightenedThisFrame)
            {
                restoredAny = RestoreUntouchedSecuredBankPlugs(
                    covers,
                    bolts,
                    plugs);
            }

            if (restoredAny && refreshVisualsMethod != null)
            {
                try
                {
                    refreshVisualsMethod.Invoke(station, null);
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"Could not refresh isolated Merlin bank visuals: {exception.Message}",
                        this);
                }
            }

            CaptureState(covers, bolts, plugs);
        }

        private bool RestoreUntouchedSecuredBankPlugs(
            List<bool> covers,
            List<bool> bolts,
            List<bool> plugs)
        {
            List<EngineAssemblyInteractionTarget> boltTargets = GetRegisteredTargets(
                coverBoltTargetsField,
                EngineAssemblyInteractionKind.CoverBolt);
            List<EngineAssemblyInteractionTarget> plugTargets = GetRegisteredTargets(
                sparkPlugTargetsField,
                EngineAssemblyInteractionKind.SparkPlug);

            bool restored = false;
            for (int index = 0; index < plugTargets.Count; index++)
            {
                EngineAssemblyInteractionTarget target = plugTargets[index];
                if (target == null
                    || !IsValidIndex(previousPlugs, target.TargetIndex)
                    || !IsValidIndex(plugs, target.TargetIndex)
                    || !previousPlugs[target.TargetIndex]
                    || plugs[target.TargetIndex]
                    || !IsBankSecured(
                        target.GroupIndex,
                        covers,
                        bolts,
                        boltTargets))
                {
                    continue;
                }

                plugs[target.TargetIndex] = true;
                restored = true;
            }

            return restored;
        }

        private static bool IsBankSecured(
            int bankIndex,
            List<bool> covers,
            List<bool> bolts,
            List<EngineAssemblyInteractionTarget> boltTargets)
        {
            if (!IsValidIndex(covers, bankIndex) || !covers[bankIndex])
            {
                return false;
            }

            bool foundBolt = false;
            for (int index = 0; index < boltTargets.Count; index++)
            {
                EngineAssemblyInteractionTarget target = boltTargets[index];
                if (target == null || target.GroupIndex != bankIndex)
                {
                    continue;
                }

                foundBolt = true;
                if (!IsValidIndex(bolts, target.TargetIndex)
                    || !bolts[target.TargetIndex])
                {
                    return false;
                }
            }

            return foundBolt;
        }

        private bool TryReadState(
            out List<bool> covers,
            out List<bool> bolts,
            out List<bool> plugs)
        {
            covers = null;
            bolts = null;
            plugs = null;

            if (!IsReady)
            {
                ResolveBindings(false);
            }

            if (!IsReady || station == null || ensureStateListsMethod == null)
            {
                return false;
            }

            try
            {
                ensureStateListsMethod.Invoke(station, null);
                covers = GetBoolList(coverPlacedField);
                bolts = GetBoolList(coverBoltsTightenedField);
                plugs = GetBoolList(sparkPlugInstalledField);
                return covers != null && bolts != null && plugs != null;
            }
            catch (Exception exception)
            {
                IsReady = false;
                if (Application.isPlaying)
                {
                    Debug.LogError(
                        $"Could not read isolated Merlin bank state: {exception.Message}",
                        this);
                }
                return false;
            }
        }

        private void ResolveBindings(bool logFailure)
        {
            station = GetComponent<EngineAssemblyStation>();
            Type stationType = typeof(EngineAssemblyStation);

            coverPlacedField = stationType.GetField("coverPlaced", PrivateInstance);
            coverBoltsTightenedField = stationType.GetField(
                "coverBoltsTightened",
                PrivateInstance);
            sparkPlugInstalledField = stationType.GetField(
                "sparkPlugInstalled",
                PrivateInstance);
            coverBoltTargetsField = stationType.GetField(
                "coverBoltTargets",
                PrivateInstance);
            sparkPlugTargetsField = stationType.GetField(
                "sparkPlugTargets",
                PrivateInstance);
            ensureStateListsMethod = stationType.GetMethod(
                "EnsureStateLists",
                PrivateInstance);
            refreshVisualsMethod = stationType.GetMethod(
                "RefreshVisuals",
                PrivateInstance);

            IsReady = station != null
                && coverPlacedField != null
                && coverBoltsTightenedField != null
                && sparkPlugInstalledField != null
                && coverBoltTargetsField != null
                && sparkPlugTargetsField != null
                && ensureStateListsMethod != null
                && refreshVisualsMethod != null;

            if (IsReady)
            {
                bindingErrorLogged = false;
                return;
            }

            if (logFailure && !bindingErrorLogged)
            {
                bindingErrorLogged = true;
                Debug.LogError(
                    "Engine bank isolation could not bind to the Merlin assembly state. Re-run the latest Merlin Condition repair after compiling.",
                    this);
            }
        }

        private List<bool> GetBoolList(FieldInfo field)
        {
            return field != null && station != null
                ? field.GetValue(station) as List<bool>
                : null;
        }

        private List<EngineAssemblyInteractionTarget> GetRegisteredTargets(
            FieldInfo field,
            EngineAssemblyInteractionKind expectedKind)
        {
            List<EngineAssemblyInteractionTarget> result =
                new List<EngineAssemblyInteractionTarget>();
            if (field == null || station == null)
            {
                return result;
            }

            List<EngineAssemblyInteractionTarget> registered =
                field.GetValue(station) as List<EngineAssemblyInteractionTarget>;
            if (registered == null)
            {
                return result;
            }

            for (int index = 0; index < registered.Count; index++)
            {
                EngineAssemblyInteractionTarget target = registered[index];
                if (target != null
                    && target.InteractionKind == expectedKind
                    && !result.Contains(target))
                {
                    result.Add(target);
                }
            }

            return result;
        }

        private void CaptureState(
            List<bool> covers,
            List<bool> bolts,
            List<bool> plugs)
        {
            CopyList(covers, previousCovers);
            CopyList(bolts, previousBolts);
            CopyList(plugs, previousPlugs);
        }

        private static void CopyList(List<bool> source, List<bool> destination)
        {
            destination.Clear();
            if (source == null)
            {
                return;
            }

            for (int index = 0; index < source.Count; index++)
            {
                destination.Add(source[index]);
            }
        }

        private static bool HasFalseToTrueTransition(
            List<bool> previous,
            List<bool> current)
        {
            int count = Mathf.Min(previous.Count, current != null ? current.Count : 0);
            for (int index = 0; index < count; index++)
            {
                if (!previous[index] && current[index])
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountTargetsOnBank(
            List<EngineAssemblyInteractionTarget> targets,
            int bankIndex)
        {
            int count = 0;
            for (int index = 0; index < targets.Count; index++)
            {
                if (targets[index] != null && targets[index].GroupIndex == bankIndex)
                {
                    count++;
                }
            }
            return count;
        }

        private static bool IsValidIndex(List<bool> values, int index)
        {
            return values != null && index >= 0 && index < values.Count;
        }

        private void OnDisable()
        {
            initialized = false;
        }

        private void OnValidate()
        {
            ResolveBindings(false);
        }
    }
}
