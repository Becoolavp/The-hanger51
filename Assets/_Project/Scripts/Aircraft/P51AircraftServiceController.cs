using UnityEngine;

namespace Hanger51.Aircraft
{
    public sealed class P51AircraftServiceController : MonoBehaviour
    {
        [Header("Top Cowling")]
        [SerializeField] private GameObject topCowlingPanel;
        [SerializeField] private Transform cowlingInstalledPose;
        [SerializeField] private Transform cowlingRemovedPose;
        [SerializeField] private Transform cowlingInstalledParent;
        [SerializeField] private P51PortableCowlingPanel portableCowlingPanel;
        [SerializeField, Min(1)] private int cowlingScrewCount = 10;
        [SerializeField] private bool[] cowlingScrewsTightened = new bool[10];
        [SerializeField] private bool topCowlingInstalled = true;
        [SerializeField] private bool cowlingCarried;

        [Header("Engine Installation")]
        [SerializeField] private AircraftEngineMountReceiver engineMountReceiver;

        public bool IsTopCowlingInstalled => topCowlingInstalled;
        public bool IsTopCowlingRemoved => !topCowlingInstalled;
        public bool IsCowlingCarried => !topCowlingInstalled && cowlingCarried;
        public bool IsCowlingLoose => !topCowlingInstalled && !cowlingCarried;
        public bool IsCowlingInstallAreaReady => IsCowlingCarried
            && topCowlingPanel != null
            && (engineMountReceiver == null
                || !engineMountReceiver.EnginePositioned
                || engineMountReceiver.AllMountBoltsTightened);
        public GameObject TopCowlingPanel => topCowlingPanel;
        public int CowlingScrewCount => cowlingScrewsTightened != null
            ? cowlingScrewsTightened.Length
            : 0;
        public AircraftEngineMountReceiver EngineMountReceiver => engineMountReceiver;

        private void Awake()
        {
            EnsureScrewArray();
            ResolveCowlingReferences();
            RefreshTargetsAndVisuals();
        }

        private void OnEnable()
        {
            EnsureScrewArray();
            ResolveCowlingReferences();
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
            cowlingInstalledParent = configuredTopCowlingPanel != null
                ? configuredTopCowlingPanel.transform.parent
                : transform;
            engineMountReceiver = configuredEngineMountReceiver;
            cowlingScrewCount = Mathf.Max(1, configuredScrewCount);
            cowlingScrewsTightened = new bool[cowlingScrewCount];
            for (int index = 0; index < cowlingScrewsTightened.Length; index++)
            {
                cowlingScrewsTightened[index] = true;
            }

            topCowlingInstalled = true;
            cowlingCarried = false;
            ResolveCowlingReferences();
            RefreshTargetsAndVisuals();
        }

        public void ConfigurePortableCowling(P51PortableCowlingPanel configuredPortablePanel)
        {
            portableCowlingPanel = configuredPortablePanel;
            if (topCowlingPanel == null && portableCowlingPanel != null)
            {
                topCowlingPanel = portableCowlingPanel.gameObject;
            }

            if (cowlingInstalledParent == null && topCowlingPanel != null)
            {
                cowlingInstalledParent = topCowlingPanel.transform.parent;
            }

            portableCowlingPanel?.RefreshFromService();
        }

        public bool TryBeginCowlingCarry(
            Transform carryAnchor,
            Vector3 localCarryPosition,
            Quaternion localCarryRotation,
            out string resultMessage)
        {
            resultMessage = string.Empty;
            if (topCowlingInstalled)
            {
                resultMessage = "Unscrew and remove the top cowling before carrying it.";
                return false;
            }

            if (topCowlingPanel == null || carryAnchor == null)
            {
                resultMessage = "The portable cowling or Player carry point is missing.";
                return false;
            }

            cowlingCarried = true;
            topCowlingPanel.transform.SetParent(carryAnchor, false);
            topCowlingPanel.transform.localPosition = localCarryPosition;
            topCowlingPanel.transform.localRotation = localCarryRotation;
            topCowlingPanel.transform.localScale = Vector3.one;
            portableCowlingPanel?.RefreshFromService();
            RefreshTargetsAndVisuals();
            resultMessage = "Picked up the top cowling. Carry it to the highlighted engine opening and hold E to reinstall it, or press E to set it down.";
            return true;
        }

        public bool TryPlaceCarriedCowling(
            Vector3 worldPosition,
            Quaternion worldRotation,
            out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!IsCowlingCarried || topCowlingPanel == null)
            {
                resultMessage = "You are not carrying the top cowling.";
                return false;
            }

            topCowlingPanel.transform.SetParent(null, true);
            topCowlingPanel.transform.SetPositionAndRotation(worldPosition, worldRotation);
            topCowlingPanel.transform.localScale = Vector3.one;
            cowlingCarried = false;
            portableCowlingPanel?.RefreshFromService();
            RefreshTargetsAndVisuals();
            resultMessage = "Placed the top cowling. Pick it up again before attempting to reinstall it.";
            return true;
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
                    return IsCowlingInstallAreaReady;

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
                        ? $"Hold R to lift and carry the top cowling{progressText}"
                        : $"Hold E to install the cowling you are carrying{progressText}";

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
                        if (!IsCowlingCarried)
                        {
                            resultMessage = "Pick up and carry the top cowling before installing it.";
                        }
                        else if (engineMountReceiver != null
                            && engineMountReceiver.EnginePositioned
                            && !engineMountReceiver.AllMountBoltsTightened)
                        {
                            resultMessage = "Secure all four engine-mount bolts before replacing the cowling.";
                        }
                        else
                        {
                            resultMessage = "The top cowling cannot be installed right now.";
                        }
                        return false;
                    }

                    cowlingCarried = false;
                    topCowlingInstalled = true;
                    AttachCowlingToInstalledParent();
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
                        ? "All top-cowling screws are loose. Aim at the highlighted panel and hold R to lift it."
                        : $"Unscrewed top-cowling screw {targetIndex + 1}.";
                    break;

                case AircraftServiceInteractionKind.CowlingPanel:
                    if (!CanRemoveTarget(interactionKind, targetIndex))
                    {
                        resultMessage = "Unscrew every highlighted top-cowling screw before removing the panel.";
                        return false;
                    }

                    topCowlingInstalled = false;
                    cowlingCarried = false;
                    resultMessage = engineMountReceiver != null
                        && engineMountReceiver.EnginePositioned
                            ? "Lifted the top cowling free. The installed engine and mount bolts are accessible."
                            : "Lifted the top cowling free. The P-51 engine bay is accessible.";
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
            ResolveCowlingReferences();
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

            portableCowlingPanel?.RefreshFromService();
        }

        public void ResetAircraftService()
        {
            cowlingCarried = false;
            topCowlingInstalled = true;
            AttachCowlingToInstalledParent();
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
            if (!topCowlingInstalled || topCowlingPanel == null || cowlingInstalledPose == null)
            {
                return;
            }

            AttachCowlingToInstalledParent();
            topCowlingPanel.transform.SetPositionAndRotation(
                cowlingInstalledPose.position,
                cowlingInstalledPose.rotation);
            topCowlingPanel.transform.localScale = Vector3.one;
        }

        private void AttachCowlingToInstalledParent()
        {
            if (topCowlingPanel == null)
            {
                return;
            }

            Transform targetParent = cowlingInstalledParent != null
                ? cowlingInstalledParent
                : transform;
            topCowlingPanel.transform.SetParent(targetParent, true);
        }

        private void ResolveCowlingReferences()
        {
            if (topCowlingPanel != null && cowlingInstalledParent == null && topCowlingPanel.transform.parent != null)
            {
                cowlingInstalledParent = topCowlingPanel.transform.parent;
            }

            if (portableCowlingPanel == null && topCowlingPanel != null)
            {
                portableCowlingPanel = topCowlingPanel.GetComponent<P51PortableCowlingPanel>();
            }
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
            ResolveCowlingReferences();
        }
    }
}
