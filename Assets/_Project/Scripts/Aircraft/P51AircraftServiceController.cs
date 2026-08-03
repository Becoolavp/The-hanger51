using UnityEngine;

namespace Hanger51.Aircraft
{
    public sealed class P51AircraftServiceController : MonoBehaviour
    {
        [Header("Top Cowling")]
        [SerializeField] private GameObject topCowlingPanel;
        [SerializeField] private Transform cowlingInstalledPose;
        [SerializeField] private Transform cowlingRemovedPose;
        [SerializeField, Min(1)] private int cowlingScrewCount = 10;
        [SerializeField] private bool[] cowlingScrewsTightened = new bool[10];
        [SerializeField] private bool topCowlingInstalled = true;

        [Header("Engine Installation")]
        [SerializeField] private AircraftEngineMountReceiver engineMountReceiver;

        public bool IsTopCowlingInstalled => topCowlingInstalled;
        public bool IsTopCowlingRemoved => !topCowlingInstalled;
        public int CowlingScrewCount => cowlingScrewsTightened != null
            ? cowlingScrewsTightened.Length
            : 0;
        public AircraftEngineMountReceiver EngineMountReceiver => engineMountReceiver;

        private void Awake()
        {
            EnsureScrewArray();
            RefreshTargetsAndVisuals();
        }

        private void OnEnable()
        {
            EnsureScrewArray();
            RefreshTargetsAndVisuals();
        }

        public void Configure(
            GameObject configuredTopCowlingPanel,
            Transform configuredInstalledPose,
            Transform configuredRemovedPose,
            AircraftEngineMountReceiver configuredEngineMountReceiver,
            int configuredScrewCount)
        {
            topCowlingPanel = configuredTopCowlingPanel;
            cowlingInstalledPose = configuredInstalledPose;
            cowlingRemovedPose = configuredRemovedPose;
            engineMountReceiver = configuredEngineMountReceiver;
            cowlingScrewCount = Mathf.Max(1, configuredScrewCount);
            cowlingScrewsTightened = new bool[cowlingScrewCount];
            for (int index = 0; index < cowlingScrewsTightened.Length; index++)
            {
                cowlingScrewsTightened[index] = true;
            }

            topCowlingInstalled = true;
            RefreshTargetsAndVisuals();
        }

        public bool CanInstallTarget(
            AircraftServiceInteractionKind interactionKind,
            int targetIndex)
        {
            switch (interactionKind)
            {
                case AircraftServiceInteractionKind.CowlingScrew:
                    return topCowlingInstalled
                        && IsValidScrewIndex(targetIndex)
                        && !cowlingScrewsTightened[targetIndex];

                case AircraftServiceInteractionKind.CowlingPanel:
                    return !topCowlingInstalled
                        && (engineMountReceiver == null
                            || !engineMountReceiver.EnginePositioned
                            || engineMountReceiver.AllMountBoltsTightened);

                case AircraftServiceInteractionKind.EngineMountBolt:
                    return engineMountReceiver != null
                        && engineMountReceiver.CanInstallMountBolt(targetIndex);

                default:
                    return false;
            }
        }

        public bool CanRemoveTarget(
            AircraftServiceInteractionKind interactionKind,
            int targetIndex)
        {
            switch (interactionKind)
            {
                case AircraftServiceInteractionKind.CowlingScrew:
                    return topCowlingInstalled
                        && IsValidScrewIndex(targetIndex)
                        && cowlingScrewsTightened[targetIndex];

                case AircraftServiceInteractionKind.CowlingPanel:
                    return topCowlingInstalled && AreAllCowlingScrewsLoose();

                case AircraftServiceInteractionKind.EngineMountBolt:
                    return engineMountReceiver != null
                        && engineMountReceiver.CanRemoveMountBolt(targetIndex);

                default:
                    return false;
            }
        }

        public bool IsTargetComplete(
            AircraftServiceInteractionKind interactionKind,
            int targetIndex)
        {
            switch (interactionKind)
            {
                case AircraftServiceInteractionKind.CowlingScrew:
                    return IsValidScrewIndex(targetIndex)
                        && cowlingScrewsTightened[targetIndex];

                case AircraftServiceInteractionKind.CowlingPanel:
                    return topCowlingInstalled;

                case AircraftServiceInteractionKind.EngineMountBolt:
                    return engineMountReceiver != null
                        && engineMountReceiver.IsMountBoltTightened(targetIndex);

                default:
                    return false;
            }
        }

        public bool ShouldHighlightTarget(
            AircraftServiceInteractionKind interactionKind,
            int targetIndex)
        {
            return CanInstallTarget(interactionKind, targetIndex)
                || CanRemoveTarget(interactionKind, targetIndex);
        }

        public bool ShouldShowAnimatedVisual(
            AircraftServiceInteractionKind interactionKind)
        {
            switch (interactionKind)
            {
                case AircraftServiceInteractionKind.CowlingPanel:
                case AircraftServiceInteractionKind.CowlingScrew:
                    return true;

                case AircraftServiceInteractionKind.EngineMountBolt:
                    return engineMountReceiver != null
                        && engineMountReceiver.EnginePositioned;

                default:
                    return false;
            }
        }

        public string GetInteractionText(
            AircraftServiceInteractionKind interactionKind,
            int targetIndex,
            bool removing,
            float holdProgress)
        {
            int percent = Mathf.RoundToInt(Mathf.Clamp01(holdProgress) * 100f);
            string progressText = holdProgress > 0f ? $" ({percent}%)" : string.Empty;

            switch (interactionKind)
            {
                case AircraftServiceInteractionKind.CowlingScrew:
                    return removing
                        ? $"Hold R to unscrew top-cowling screw {targetIndex + 1}{progressText}"
                        : $"Hold E to tighten top-cowling screw {targetIndex + 1}{progressText}";

                case AircraftServiceInteractionKind.CowlingPanel:
                    return removing
                        ? $"Hold R to lift the top cowling onto its service cradle{progressText}"
                        : $"Hold E to place the top cowling over the engine bay{progressText}";

                case AircraftServiceInteractionKind.EngineMountBolt:
                    return removing
                        ? $"Hold R to loosen P-51 engine-mount bolt {targetIndex + 1}{progressText}"
                        : $"Hold E to tighten P-51 engine-mount bolt {targetIndex + 1}{progressText}";

                default:
                    return string.Empty;
            }
        }

        public bool TryInstallTarget(
            AircraftServiceInteractionKind interactionKind,
            int targetIndex,
            out string resultMessage)
        {
            resultMessage = string.Empty;

            switch (interactionKind)
            {
                case AircraftServiceInteractionKind.CowlingScrew:
                    if (!CanInstallTarget(interactionKind, targetIndex))
                    {
                        resultMessage = "That cowling screw cannot be tightened right now.";
                        return false;
                    }

                    cowlingScrewsTightened[targetIndex] = true;
                    resultMessage = AreAllCowlingScrewsTight()
                        ? "All top-cowling screws are secure."
                        : $"Tightened top-cowling screw {targetIndex + 1}.";
                    break;

                case AircraftServiceInteractionKind.CowlingPanel:
                    if (!CanInstallTarget(interactionKind, targetIndex))
                    {
                        resultMessage = engineMountReceiver != null
                            && engineMountReceiver.EnginePositioned
                            && !engineMountReceiver.AllMountBoltsTightened
                                ? "Secure all four engine-mount bolts before replacing the cowling."
                                : "The top cowling cannot be installed right now.";
                        return false;
                    }

                    topCowlingInstalled = true;
                    EnsureScrewArray();
                    for (int index = 0; index < cowlingScrewsTightened.Length; index++)
                    {
                        cowlingScrewsTightened[index] = false;
                    }
                    resultMessage = "Placed the top cowling. Tighten all highlighted screws to secure it.";
                    break;

                case AircraftServiceInteractionKind.EngineMountBolt:
                    if (engineMountReceiver == null
                        || !engineMountReceiver.TryInstallMountBolt(targetIndex, out resultMessage))
                    {
                        return false;
                    }
                    break;

                default:
                    return false;
            }

            RefreshTargetsAndVisuals();
            return true;
        }

        public bool TryRemoveTarget(
            AircraftServiceInteractionKind interactionKind,
            int targetIndex,
            out string resultMessage)
        {
            resultMessage = string.Empty;

            switch (interactionKind)
            {
                case AircraftServiceInteractionKind.CowlingScrew:
                    if (!CanRemoveTarget(interactionKind, targetIndex))
                    {
                        resultMessage = "That cowling screw is already loose.";
                        return false;
                    }

                    cowlingScrewsTightened[targetIndex] = false;
                    resultMessage = AreAllCowlingScrewsLoose()
                        ? "All top-cowling screws are loose. Aim at the highlighted panel and hold R to remove it."
                        : $"Unscrewed top-cowling screw {targetIndex + 1}.";
                    break;

                case AircraftServiceInteractionKind.CowlingPanel:
                    if (!CanRemoveTarget(interactionKind, targetIndex))
                    {
                        resultMessage = "Unscrew every highlighted top-cowling screw before removing the panel.";
                        return false;
                    }

                    topCowlingInstalled = false;
                    resultMessage = engineMountReceiver != null
                        && engineMountReceiver.EnginePositioned
                            ? "Removed the top cowling. The installed engine and mount bolts are accessible."
                            : "Removed the top cowling. The P-51 engine-bay placement area is accessible.";
                    break;

                case AircraftServiceInteractionKind.EngineMountBolt:
                    if (engineMountReceiver == null
                        || !engineMountReceiver.TryRemoveMountBolt(targetIndex, out resultMessage))
                    {
                        return false;
                    }
                    break;

                default:
                    return false;
            }

            RefreshTargetsAndVisuals();
            return true;
        }

        public void RefreshTargetsAndVisuals()
        {
            EnsureScrewArray();
            SyncCowlingPanelPose();

            AircraftServiceInteractionTarget[] targets =
                GetComponentsInChildren<AircraftServiceInteractionTarget>(true);
            for (int index = 0; index < targets.Length; index++)
            {
                if (targets[index] != null)
                {
                    targets[index].RefreshFromController();
                }
            }
        }

        public void ResetAircraftService()
        {
            topCowlingInstalled = true;
            EnsureScrewArray();
            for (int index = 0; index < cowlingScrewsTightened.Length; index++)
            {
                cowlingScrewsTightened[index] = true;
            }

            engineMountReceiver?.ResetReceiver();
            RefreshTargetsAndVisuals();
        }

        private void SyncCowlingPanelPose()
        {
            if (topCowlingPanel == null)
            {
                return;
            }

            Transform targetPose = topCowlingInstalled
                ? cowlingInstalledPose
                : cowlingRemovedPose;
            if (targetPose == null)
            {
                return;
            }

            topCowlingPanel.transform.SetPositionAndRotation(
                targetPose.position,
                targetPose.rotation);
        }

        private bool AreAllCowlingScrewsTight()
        {
            EnsureScrewArray();
            for (int index = 0; index < cowlingScrewsTightened.Length; index++)
            {
                if (!cowlingScrewsTightened[index])
                {
                    return false;
                }
            }

            return true;
        }

        private bool AreAllCowlingScrewsLoose()
        {
            EnsureScrewArray();
            for (int index = 0; index < cowlingScrewsTightened.Length; index++)
            {
                if (cowlingScrewsTightened[index])
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsValidScrewIndex(int screwIndex)
        {
            EnsureScrewArray();
            return screwIndex >= 0 && screwIndex < cowlingScrewsTightened.Length;
        }

        private void EnsureScrewArray()
        {
            cowlingScrewCount = Mathf.Max(1, cowlingScrewCount);
            if (cowlingScrewsTightened == null
                || cowlingScrewsTightened.Length != cowlingScrewCount)
            {
                bool[] previous = cowlingScrewsTightened;
                cowlingScrewsTightened = new bool[cowlingScrewCount];
                for (int index = 0; index < cowlingScrewsTightened.Length; index++)
                {
                    cowlingScrewsTightened[index] = previous == null
                        || index >= previous.Length
                        || previous[index];
                }
            }
        }

        private void OnValidate()
        {
            cowlingScrewCount = Mathf.Max(1, cowlingScrewCount);
            EnsureScrewArray();
        }
    }
}
