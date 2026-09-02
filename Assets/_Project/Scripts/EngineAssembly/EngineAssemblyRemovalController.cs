using System;
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
        private EngineAssemblyTransportController transport;

        private FieldInfo engineBlockItemField;
        private FieldInfo cylinderCoverItemField;
        private FieldInfo sparkPlugItemField;
        private FieldInfo engineBlockInstalledField;
        private FieldInfo coverPlacedField;
        private FieldInfo coverBoltsTightenedField;
        private FieldInfo sparkPlugInstalledField;
        private FieldInfo coverPlacementTargetsField;
        private FieldInfo coverBoltTargetsField;
        private FieldInfo sparkPlugTargetsField;
        private MethodInfo ensureStateListsMethod;
        private MethodInfo refreshVisualsMethod;

        private float engineRemovalProgress;
        private bool bindingErrorLogged;

        public bool IsReady { get; private set; }
        public float EngineRemovalProgress => engineRemovalProgress;

        public bool CanRemoveEngineBlock
        {
            get
            {
                if (!TryGetState(
                        out bool engineInstalled,
                        out List<bool> covers,
                        out _,
                        out _))
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
            ResolveReflection(true);
        }

        private void OnEnable()
        {
            ResolveReflection(true);
        }

        public bool InitializeBindings()
        {
            ResolveReflection(false);
            return IsReady;
        }

        public bool TryGetConfiguredTargetCounts(
            out int coverCount,
            out int boltCount,
            out int plugCount)
        {
            ResolveComponents();
            coverCount = GetRegisteredTargets(
                coverPlacementTargetsField,
                EngineAssemblyInteractionKind.CoverPlacement).Count;
            boltCount = GetRegisteredTargets(
                coverBoltTargetsField,
                EngineAssemblyInteractionKind.CoverBolt).Count;
            plugCount = GetRegisteredTargets(
                sparkPlugTargetsField,
                EngineAssemblyInteractionKind.SparkPlug).Count;
            return coverCount > 0 || boltCount > 0 || plugCount > 0;
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
                        && CountInstalledSparkPlugsOnBank(groupIndex, plugs) == 0;

                case EngineAssemblyInteractionKind.CoverPlacement:
                    return IsTrue(covers, groupIndex)
                        && CountInstalledSparkPlugsOnBank(groupIndex, plugs) == 0
                        && AreAllBoltsLooseForBank(groupIndex, bolts);

                default:
                    return false;
            }
        }

        public string GetRemovalBlockerText(
            EngineAssemblyInteractionKind kind,
            int groupIndex,
            int targetIndex)
        {
            if (kind != EngineAssemblyInteractionKind.CoverPlacement)
            {
                return string.Empty;
            }

            if (!TryGetState(
                    out bool engineInstalled,
                    out List<bool> covers,
                    out List<bool> bolts,
                    out List<bool> plugs))
            {
                return "Cover removal state is not ready. Re-run the latest mounted-engine cover repair.";
            }

            if (!engineInstalled || !IsTrue(covers, groupIndex))
            {
                return string.Empty;
            }

            int installedPlugs = CountInstalledSparkPlugsOnBank(groupIndex, plugs);
            int tightenedBolts = CountTightenedBoltsOnBank(
                groupIndex,
                bolts,
                out bool foundBolts);
            string bankName = groupIndex == 0 ? "left" : "right";

            if (!foundBolts)
            {
                return $"The {bankName} cover target registry is incomplete. Run Merlin Condition Step 22 outside Play mode.";
            }

            if (installedPlugs <= 0 && tightenedBolts <= 0)
            {
                return string.Empty;
            }

            string plugText = installedPlugs > 0
                ? $"remove {installedPlugs} remaining spark plug{(installedPlugs == 1 ? string.Empty : "s")}"
                : string.Empty;
            string boltText = tightenedBolts > 0
                ? $"loosen {tightenedBolts} remaining cover bolt{(tightenedBolts == 1 ? string.Empty : "s")}"
                : string.Empty;

            if (!string.IsNullOrWhiteSpace(plugText)
                && !string.IsNullOrWhiteSpace(boltText))
            {
                return $"To remove the {bankName} cover: {plugText} and {boltText}.";
            }

            return $"To remove the {bankName} cover: "
                + (!string.IsNullOrWhiteSpace(plugText) ? plugText : boltText)
                + ".";
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

            if (!CanRemoveTarget(kind, groupIndex, targetIndex))
            {
                string blocker = GetRemovalBlockerText(kind, groupIndex, targetIndex);
                if (!string.IsNullOrWhiteSpace(blocker))
                {
                    resultMessage = blocker;
                }
                return false;
            }

            if (!TryGetState(
                    out _,
                    out List<bool> covers,
                    out List<bool> bolts,
                    out List<bool> plugs))
            {
                resultMessage = "The engine maintenance state is not ready.";
                return false;
            }

            switch (kind)
            {
                case EngineAssemblyInteractionKind.SparkPlug:
                {
                    InventoryItemDefinition item = GetItem(sparkPlugItemField);
                    if (!TryReturnOrDropItem(
                            inventory,
                            item,
                            kind,
                            groupIndex,
                            targetIndex,
                            out bool dropped))
                    {
                        resultMessage = "The spark plug could not be returned or placed beside the engine.";
                        return false;
                    }

                    plugs[targetIndex] = false;
                    resultMessage = dropped
                        ? $"Removed spark plug {targetIndex + 1} and dropped it beside the engine because inventory was full."
                        : $"Removed spark plug {targetIndex + 1} and returned it to inventory.";
                    break;
                }

                case EngineAssemblyInteractionKind.CoverBolt:
                    bolts[targetIndex] = false;
                    resultMessage = $"Loosened cover bolt {targetIndex + 1}.";
                    break;

                case EngineAssemblyInteractionKind.CoverPlacement:
                {
                    InventoryItemDefinition item = GetItem(cylinderCoverItemField);
                    if (!TryReturnOrDropItem(
                            inventory,
                            item,
                            kind,
                            groupIndex,
                            targetIndex,
                            out bool dropped))
                    {
                        resultMessage = "The cylinder cover could not be returned or placed beside the engine.";
                        return false;
                    }

                    covers[groupIndex] = false;
                    string side = groupIndex == 0 ? "left" : "right";
                    resultMessage = dropped
                        ? $"Removed the {side} cylinder cover and dropped it beside the mounted engine because inventory was full. The bank target still records its condition for inspection."
                        : $"Removed the {side} cylinder cover and returned it to inventory. The bank target still records its condition for inspection.";
                    break;
                }

                default:
                    return false;
            }

            RefreshStationVisuals();
            transport?.RefreshMaintenanceTargets();
            return true;
        }

        public bool ProcessEngineRemovalHold(
            PlayerInventory inventory,
            bool isHeld,
            float deltaTime,
            out string resultMessage)
        {
            resultMessage = string.Empty;

            if (!CanRemoveEngineBlock)
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
            if (!TryReturnOrDropItem(
                    inventory,
                    engineItem,
                    EngineAssemblyInteractionKind.CoverPlacement,
                    0,
                    0,
                    out bool dropped))
            {
                resultMessage = "The engine block could not be returned or placed beside the engine.";
                engineRemovalProgress = 0f;
                return false;
            }

            if (station == null || engineBlockInstalledField == null)
            {
                resultMessage = "The engine maintenance state is not ready.";
                engineRemovalProgress = 0f;
                return false;
            }

            engineBlockInstalledField.SetValue(station, false);
            engineRemovalProgress = 0f;
            RefreshStationVisuals();
            transport?.RefreshMaintenanceTargets();
            resultMessage = dropped
                ? "Removed the V-1650 engine block and dropped it beside the engine because inventory was full."
                : "Removed the V-1650 engine block and returned it to inventory.";
            return true;
        }

        public void CancelEngineRemovalHold()
        {
            engineRemovalProgress = 0f;
        }

        private void ResolveComponents()
        {
            if (station == null)
            {
                station = GetComponent<EngineAssemblyStation>();
            }

            if (transport == null)
            {
                transport = GetComponent<EngineAssemblyTransportController>();
            }
        }

        private void ResolveReflection(bool logFailure)
        {
            ResolveComponents();
            Type stationType = typeof(EngineAssemblyStation);

            engineBlockItemField = stationType.GetField("engineBlockItem", PrivateInstance);
            cylinderCoverItemField = stationType.GetField("cylinderCoverItem", PrivateInstance);
            sparkPlugItemField = stationType.GetField("sparkPlugItem", PrivateInstance);
            engineBlockInstalledField = stationType.GetField("engineBlockInstalled", PrivateInstance);
            coverPlacedField = stationType.GetField("coverPlaced", PrivateInstance);
            coverBoltsTightenedField = stationType.GetField("coverBoltsTightened", PrivateInstance);
            sparkPlugInstalledField = stationType.GetField("sparkPlugInstalled", PrivateInstance);
            coverPlacementTargetsField = stationType.GetField("coverPlacementTargets", PrivateInstance);
            coverBoltTargetsField = stationType.GetField("coverBoltTargets", PrivateInstance);
            sparkPlugTargetsField = stationType.GetField("sparkPlugTargets", PrivateInstance);
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
                && coverPlacementTargetsField != null
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

            if (station == null || !IsReady)
            {
                ResolveReflection(false);
            }

            if (!IsReady
                || station == null
                || engineBlockInstalledField == null
                || coverPlacedField == null
                || coverBoltsTightenedField == null
                || sparkPlugInstalledField == null
                || ensureStateListsMethod == null)
            {
                return false;
            }

            try
            {
                ensureStateListsMethod.Invoke(station, null);
                object installedValue = engineBlockInstalledField.GetValue(station);
                if (!(installedValue is bool))
                {
                    IsReady = false;
                    return false;
                }

                engineInstalled = (bool)installedValue;
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
                        $"Engine removal state could not be read: {exception.Message}",
                        this);
                }
                return false;
            }
        }

        private void RefreshStationVisuals()
        {
            if (station == null || refreshVisualsMethod == null)
            {
                ResolveReflection(false);
            }

            if (station == null || refreshVisualsMethod == null)
            {
                return;
            }

            // Avoid ClampState here. It was written for forward-only assembly
            // and can clear the opposite bank's installed plug state.
            refreshVisualsMethod.Invoke(station, null);
        }

        private InventoryItemDefinition GetItem(FieldInfo field)
        {
            return field != null && station != null
                ? field.GetValue(station) as InventoryItemDefinition
                : null;
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
            ResolveComponents();
            List<EngineAssemblyInteractionTarget> result =
                new List<EngineAssemblyInteractionTarget>();

            if (field != null && station != null)
            {
                List<EngineAssemblyInteractionTarget> registered =
                    field.GetValue(station) as List<EngineAssemblyInteractionTarget>;
                if (registered != null)
                {
                    for (int index = 0; index < registered.Count; index++)
                    {
                        AddTargetIfValid(result, registered[index], expectedKind);
                    }
                }
            }

            if (result.Count > 0)
            {
                return result;
            }

            Transform portableRoot = transport != null ? transport.TransportRoot : null;
            EngineAssemblyInteractionTarget[] fallback = portableRoot != null
                ? portableRoot.GetComponentsInChildren<EngineAssemblyInteractionTarget>(true)
                : station != null
                    ? station.GetComponentsInChildren<EngineAssemblyInteractionTarget>(true)
                    : Array.Empty<EngineAssemblyInteractionTarget>();

            for (int index = 0; index < fallback.Length; index++)
            {
                AddTargetIfValid(result, fallback[index], expectedKind);
            }

            return result;
        }

        private static void AddTargetIfValid(
            List<EngineAssemblyInteractionTarget> targets,
            EngineAssemblyInteractionTarget candidate,
            EngineAssemblyInteractionKind expectedKind)
        {
            if (candidate != null
                && candidate.InteractionKind == expectedKind
                && !targets.Contains(candidate))
            {
                targets.Add(candidate);
            }
        }

        private int CountInstalledSparkPlugsOnBank(
            int bankIndex,
            List<bool> plugs)
        {
            List<EngineAssemblyInteractionTarget> targets = GetRegisteredTargets(
                sparkPlugTargetsField,
                EngineAssemblyInteractionKind.SparkPlug);
            int count = 0;
            for (int index = 0; index < targets.Count; index++)
            {
                EngineAssemblyInteractionTarget target = targets[index];
                if (target.GroupIndex == bankIndex
                    && IsTrue(plugs, target.TargetIndex))
                {
                    count++;
                }
            }
            return count;
        }

        private int CountTightenedBoltsOnBank(
            int bankIndex,
            List<bool> bolts,
            out bool foundBolt)
        {
            List<EngineAssemblyInteractionTarget> targets = GetRegisteredTargets(
                coverBoltTargetsField,
                EngineAssemblyInteractionKind.CoverBolt);
            int count = 0;
            foundBolt = false;

            for (int index = 0; index < targets.Count; index++)
            {
                EngineAssemblyInteractionTarget target = targets[index];
                if (target.GroupIndex != bankIndex)
                {
                    continue;
                }

                foundBolt = true;
                if (IsTrue(bolts, target.TargetIndex))
                {
                    count++;
                }
            }
            return count;
        }

        private bool AreAllBoltsLooseForBank(int bankIndex, List<bool> bolts)
        {
            int tightened = CountTightenedBoltsOnBank(
                bankIndex,
                bolts,
                out bool foundBolt);
            return foundBolt && tightened == 0;
        }

        private Transform FindTarget(
            EngineAssemblyInteractionKind kind,
            int groupIndex,
            int targetIndex)
        {
            FieldInfo field = kind == EngineAssemblyInteractionKind.CoverPlacement
                ? coverPlacementTargetsField
                : kind == EngineAssemblyInteractionKind.CoverBolt
                    ? coverBoltTargetsField
                    : sparkPlugTargetsField;
            List<EngineAssemblyInteractionTarget> targets = GetRegisteredTargets(field, kind);
            for (int index = 0; index < targets.Count; index++)
            {
                EngineAssemblyInteractionTarget target = targets[index];
                if (target.GroupIndex == groupIndex
                    && target.TargetIndex == targetIndex)
                {
                    return target.transform;
                }
            }
            return null;
        }

        private bool TryReturnOrDropItem(
            PlayerInventory inventory,
            InventoryItemDefinition item,
            EngineAssemblyInteractionKind kind,
            int groupIndex,
            int targetIndex,
            out bool dropped)
        {
            dropped = false;
            if (item == null)
            {
                return false;
            }

            if (inventory != null && inventory.AddItem(item, 1) == 0)
            {
                return true;
            }

            dropped = CreateDroppedPickup(item, kind, groupIndex, targetIndex);
            return dropped;
        }

        private bool CreateDroppedPickup(
            InventoryItemDefinition item,
            EngineAssemblyInteractionKind kind,
            int groupIndex,
            int targetIndex)
        {
            ResolveComponents();
            if (item == null || station == null)
            {
                return false;
            }

            Vector3 groundPosition = FindDropGroundPosition(
                kind,
                groupIndex,
                targetIndex);
            GameObject pickupObject;
            if (item.WorldPrefab != null)
            {
                pickupObject = Instantiate(item.WorldPrefab);
                pickupObject.transform.localScale = item.WorldScale;
            }
            else
            {
                pickupObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pickupObject.transform.localScale = Vector3.one * 0.42f;
                ApplyFallbackMaterial(pickupObject, item);
            }

            if (pickupObject == null)
            {
                return false;
            }

            Transform reference = GetPortableReference();
            pickupObject.SetActive(true);
            pickupObject.transform.position = groundPosition + Vector3.up * 0.08f;
            pickupObject.transform.rotation = Quaternion.Euler(
                0f,
                reference.eulerAngles.y,
                0f);

            InventoryPickup pickup = pickupObject.GetComponent<InventoryPickup>();
            if (pickup == null)
            {
                pickup = pickupObject.AddComponent<InventoryPickup>();
            }
            pickup.Configure(item, 1);
            EnsurePickupCollider(pickupObject);
            AlignBottomToGround(pickupObject, groundPosition.y);
            return true;
        }

        private Vector3 FindDropGroundPosition(
            EngineAssemblyInteractionKind kind,
            int groupIndex,
            int targetIndex)
        {
            Transform reference = GetPortableReference();
            Transform matchingTarget = FindTarget(kind, groupIndex, targetIndex);
            Vector3 basePosition = matchingTarget != null
                ? matchingTarget.position
                : reference.position;

            float sideDirection = groupIndex == 0 ? -1f : 1f;
            Vector3 candidate = basePosition
                + reference.right * sideDirection * 1.6f
                + reference.forward * 0.35f;

            RaycastHit[] hits = Physics.RaycastAll(
                candidate + Vector3.up * 3f,
                Vector3.down,
                7f,
                ~0,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            Transform portableRoot = transport != null ? transport.TransportRoot : null;
            for (int index = 0; index < hits.Length; index++)
            {
                Collider collider = hits[index].collider;
                if (collider == null)
                {
                    continue;
                }

                bool belongsToPortableEngine = portableRoot != null
                    && collider.transform.IsChildOf(portableRoot);
                bool belongsToStation = station != null
                    && collider.transform.IsChildOf(station.transform);
                if (belongsToPortableEngine || belongsToStation)
                {
                    continue;
                }

                return hits[index].point;
            }

            return candidate;
        }

        private Transform GetPortableReference()
        {
            ResolveComponents();
            return transport != null && transport.TransportRoot != null
                ? transport.TransportRoot
                : station != null
                    ? station.transform
                    : transform;
        }

        private static void EnsurePickupCollider(GameObject pickupObject)
        {
            Collider[] colliders = pickupObject.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null && colliders[index].enabled)
                {
                    return;
                }
            }

            BoxCollider collider = pickupObject.AddComponent<BoxCollider>();
            collider.size = Vector3.one;
        }

        private static void AlignBottomToGround(GameObject pickupObject, float groundY)
        {
            Renderer[] renderers = pickupObject.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                pickupObject.transform.position = new Vector3(
                    pickupObject.transform.position.x,
                    groundY + 0.04f,
                    pickupObject.transform.position.z);
                return;
            }

            Bounds combined = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                if (renderers[index] != null)
                {
                    combined.Encapsulate(renderers[index].bounds);
                }
            }

            pickupObject.transform.position += Vector3.up
                * (groundY - combined.min.y + 0.02f);
        }

        private static void ApplyFallbackMaterial(
            GameObject pickupObject,
            InventoryItemDefinition item)
        {
            Renderer renderer = pickupObject.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            if (shader == null)
            {
                return;
            }

            Material material = new Material(shader)
            {
                name = $"Dropped {item.DisplayName} Material"
            };
            Color color = item.PlaceholderColor;
            color.a = 1f;
            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            renderer.material = material;
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
            ResolveReflection(false);
        }
    }
}
