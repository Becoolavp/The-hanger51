using System.Collections.Generic;
using Hanger51.EngineAssembly;
using UnityEngine;

namespace Hanger51.Aircraft
{
    public sealed class AircraftEngineMountReceiver : MonoBehaviour
    {
        private static readonly List<AircraftEngineMountReceiver> ActiveReceivers =
            new List<AircraftEngineMountReceiver>();

        [Header("Aircraft Service")]
        [SerializeField] private P51AircraftServiceController serviceController;
        [SerializeField] private Transform engineMountAnchor;
        [SerializeField] private GameObject placementHighlightRoot;

        [Header("Engine Mount")]
        [SerializeField, Min(1)] private int mountBoltCount = 4;
        [SerializeField] private bool[] mountBoltsTightened = new bool[4];
        [SerializeField] private EngineAssemblyTransportController installedTransport;
        [SerializeField] private bool enginePositioned;

        [Header("Placement Preview")]
        [SerializeField, Min(0.5f)] private float previewRefreshInterval = 0.20f;

        private float nextPreviewRefreshTime;

        public Transform EngineMountAnchor => engineMountAnchor;
        public GameObject PlacementHighlightRoot => placementHighlightRoot;
        public EngineAssemblyTransportController InstalledTransport => installedTransport;
        public bool EnginePositioned => enginePositioned;
        public int MountBoltCount => mountBoltsTightened != null
            ? mountBoltsTightened.Length
            : 0;
        public bool AllMountBoltsTightened =>
            enginePositioned && AreAllBoltsInState(true);
        public bool AllMountBoltsLoose => AreAllBoltsInState(false);
        public Vector3 PlacementReferencePosition => engineMountAnchor != null
            ? engineMountAnchor.position
            : transform.position;
        public Quaternion PlacementReferenceRotation => engineMountAnchor != null
            ? engineMountAnchor.rotation
            : transform.rotation;

        private void OnEnable()
        {
            if (!ActiveReceivers.Contains(this))
            {
                ActiveReceivers.Add(this);
            }

            EnsureBoltArray();
            RefreshPlacementPreview(true);
        }

        private void OnDisable()
        {
            ActiveReceivers.Remove(this);
            if (placementHighlightRoot != null)
            {
                placementHighlightRoot.SetActive(false);
            }
        }

        private void Update()
        {
            if (Time.unscaledTime < nextPreviewRefreshTime)
            {
                return;
            }

            nextPreviewRefreshTime = Time.unscaledTime
                + Mathf.Max(0.05f, previewRefreshInterval);
            RefreshPlacementPreview(false);
        }

        public void Configure(
            P51AircraftServiceController configuredServiceController,
            Transform configuredEngineMountAnchor,
            GameObject configuredPlacementHighlightRoot,
            int configuredMountBoltCount)
        {
            serviceController = configuredServiceController;
            engineMountAnchor = configuredEngineMountAnchor;
            placementHighlightRoot = configuredPlacementHighlightRoot;
            mountBoltCount = Mathf.Max(1, configuredMountBoltCount);
            mountBoltsTightened = new bool[mountBoltCount];
            installedTransport = null;
            enginePositioned = false;

            RefreshPlacementPreview(true);
        }

        public static AircraftEngineMountReceiver FindReceiverForTransport(
            EngineAssemblyTransportController transport)
        {
            if (transport == null)
            {
                return null;
            }

            for (int index = 0; index < ActiveReceivers.Count; index++)
            {
                AircraftEngineMountReceiver receiver = ActiveReceivers[index];
                if (receiver != null
                    && receiver.enginePositioned
                    && receiver.installedTransport == transport)
                {
                    return receiver;
                }
            }

            return null;
        }

        public static AircraftEngineMountReceiver FindNearestReceiver(
            Vector3 worldPosition,
            float maximumHorizontalDistance)
        {
            AircraftEngineMountReceiver nearest = null;
            float nearestDistance = Mathf.Max(0f, maximumHorizontalDistance);

            for (int index = 0; index < ActiveReceivers.Count; index++)
            {
                AircraftEngineMountReceiver receiver = ActiveReceivers[index];
                if (receiver == null || receiver.engineMountAnchor == null)
                {
                    continue;
                }

                float distance = receiver.HorizontalDistanceFrom(worldPosition);
                if (distance <= nearestDistance)
                {
                    nearest = receiver;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        public float HorizontalDistanceFrom(Vector3 worldPosition)
        {
            Vector3 reference = PlacementReferencePosition;
            return Vector2.Distance(
                new Vector2(worldPosition.x, worldPosition.z),
                new Vector2(reference.x, reference.z));
        }

        public bool CanAcceptEngine(
            EngineAssemblyTransportController transport,
            Vector3 hookPosition,
            float maximumHorizontalDistance,
            out string reason)
        {
            reason = string.Empty;

            if (serviceController == null || !serviceController.IsTopCowlingRemoved)
            {
                reason = "Remove the P-51 top engine cowling before lowering the engine into the bay.";
                return false;
            }

            if (enginePositioned || installedTransport != null)
            {
                reason = "The P-51 engine bay already contains an engine.";
                return false;
            }

            if (transport == null || !transport.HasEngine || transport.TransportRoot == null)
            {
                reason = "The hoist is not carrying a valid engine assembly.";
                return false;
            }

            if (engineMountAnchor == null)
            {
                reason = "The P-51 engine mount anchor is not configured.";
                return false;
            }

            float distance = HorizontalDistanceFrom(hookPosition);
            if (distance > maximumHorizontalDistance)
            {
                reason = $"Move the hoist hook closer to the P-51 engine bay ({distance:F1} m away).";
                return false;
            }

            return true;
        }

        public bool CanReleaseEngineForHoist(
            EngineAssemblyTransportController transport,
            out string reason)
        {
            reason = string.Empty;

            if (!enginePositioned || installedTransport != transport)
            {
                reason = "This engine is not positioned in the P-51 engine bay.";
                return false;
            }

            if (serviceController == null || !serviceController.IsTopCowlingRemoved)
            {
                reason = "Remove the P-51 top engine cowling before lifting the engine.";
                return false;
            }

            if (!AllMountBoltsLoose)
            {
                reason = "Loosen all four highlighted engine-mount bolts before lifting the engine.";
                return false;
            }

            return true;
        }

        public void GetEngineRootTargetPose(
            out Vector3 worldPosition,
            out Quaternion worldRotation)
        {
            worldPosition = PlacementReferencePosition;
            worldRotation = PlacementReferenceRotation;
        }

        public void CompleteEnginePlacement(
            EngineAssemblyTransportController transport)
        {
            if (transport == null || transport.TransportRoot == null || engineMountAnchor == null)
            {
                return;
            }

            installedTransport = transport;
            enginePositioned = true;
            EnsureBoltArray();
            for (int index = 0; index < mountBoltsTightened.Length; index++)
            {
                mountBoltsTightened[index] = false;
            }

            Vector3 preservedScale = transport.TransportRoot.localScale;
            transport.TransportRoot.SetParent(engineMountAnchor, true);
            transport.TransportRoot.localPosition = Vector3.zero;
            transport.TransportRoot.localRotation = Quaternion.identity;
            transport.TransportRoot.localScale = preservedScale;

            RefreshPlacementPreview(true);
            serviceController?.RefreshTargetsAndVisuals();
        }

        public bool PrepareEngineForHoist(
            EngineAssemblyTransportController transport,
            out string reason)
        {
            if (!CanReleaseEngineForHoist(transport, out reason))
            {
                return false;
            }

            if (transport.TransportRoot != null)
            {
                transport.TransportRoot.SetParent(null, true);
            }

            installedTransport = null;
            enginePositioned = false;
            EnsureBoltArray();
            for (int index = 0; index < mountBoltsTightened.Length; index++)
            {
                mountBoltsTightened[index] = false;
            }

            RefreshPlacementPreview(true);
            serviceController?.RefreshTargetsAndVisuals();
            reason = string.Empty;
            return true;
        }

        public bool CanInstallMountBolt(int boltIndex)
        {
            EnsureBoltArray();
            return enginePositioned
                && IsValidBoltIndex(boltIndex)
                && !mountBoltsTightened[boltIndex];
        }

        public bool CanRemoveMountBolt(int boltIndex)
        {
            EnsureBoltArray();
            return enginePositioned
                && IsValidBoltIndex(boltIndex)
                && mountBoltsTightened[boltIndex];
        }

        public bool IsMountBoltTightened(int boltIndex)
        {
            EnsureBoltArray();
            return IsValidBoltIndex(boltIndex)
                && mountBoltsTightened[boltIndex];
        }

        public bool TryInstallMountBolt(int boltIndex, out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!CanInstallMountBolt(boltIndex))
            {
                resultMessage = enginePositioned
                    ? "That engine-mount bolt is already tight."
                    : "Lower the engine into the P-51 engine bay first.";
                return false;
            }

            mountBoltsTightened[boltIndex] = true;
            resultMessage = AllMountBoltsTightened
                ? "All four P-51 engine-mount bolts are secure. The engine is installed."
                : $"Tightened P-51 engine-mount bolt {boltIndex + 1} of {MountBoltCount}.";
            serviceController?.RefreshTargetsAndVisuals();
            return true;
        }

        public bool TryRemoveMountBolt(int boltIndex, out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!CanRemoveMountBolt(boltIndex))
            {
                resultMessage = enginePositioned
                    ? "That engine-mount bolt is already loose."
                    : "There is no engine installed in the P-51 engine bay.";
                return false;
            }

            mountBoltsTightened[boltIndex] = false;
            resultMessage = AllMountBoltsLoose
                ? "All four engine-mount bolts are loose. Position the hoist and press F to lift the engine."
                : $"Loosened P-51 engine-mount bolt {boltIndex + 1}.";
            serviceController?.RefreshTargetsAndVisuals();
            return true;
        }

        public void ResetReceiver()
        {
            installedTransport = null;
            enginePositioned = false;
            mountBoltCount = Mathf.Max(1, mountBoltCount);
            mountBoltsTightened = new bool[mountBoltCount];
            RefreshPlacementPreview(true);
            serviceController?.RefreshTargetsAndVisuals();
        }

        private void RefreshPlacementPreview(bool force)
        {
            if (placementHighlightRoot == null)
            {
                return;
            }

            bool loadedHoistExists = false;
            EngineHoistController[] hoists = FindObjectsByType<EngineHoistController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int index = 0; index < hoists.Length; index++)
            {
                if (hoists[index] != null && hoists[index].HasAttachedEngine)
                {
                    loadedHoistExists = true;
                    break;
                }
            }

            bool shouldShow = !enginePositioned
                && serviceController != null
                && serviceController.IsTopCowlingRemoved
                && loadedHoistExists;

            if (force || placementHighlightRoot.activeSelf != shouldShow)
            {
                placementHighlightRoot.SetActive(shouldShow);
            }
        }

        private bool AreAllBoltsInState(bool desiredState)
        {
            EnsureBoltArray();
            for (int index = 0; index < mountBoltsTightened.Length; index++)
            {
                if (mountBoltsTightened[index] != desiredState)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsValidBoltIndex(int boltIndex)
        {
            return boltIndex >= 0
                && mountBoltsTightened != null
                && boltIndex < mountBoltsTightened.Length;
        }

        private void EnsureBoltArray()
        {
            mountBoltCount = Mathf.Max(1, mountBoltCount);
            if (mountBoltsTightened == null
                || mountBoltsTightened.Length != mountBoltCount)
            {
                bool[] previous = mountBoltsTightened;
                mountBoltsTightened = new bool[mountBoltCount];
                if (previous != null)
                {
                    int copyCount = Mathf.Min(previous.Length, mountBoltsTightened.Length);
                    for (int index = 0; index < copyCount; index++)
                    {
                        mountBoltsTightened[index] = previous[index];
                    }
                }
            }
        }

        private void OnValidate()
        {
            mountBoltCount = Mathf.Max(1, mountBoltCount);
            previewRefreshInterval = Mathf.Max(0.05f, previewRefreshInterval);
            EnsureBoltArray();
        }
    }
}
