using UnityEngine;

namespace Hanger51.Aircraft
{
    public enum AircraftServiceInteractionKind
    {
        CowlingScrew,
        CowlingPanel,
        EngineMountBolt
    }

    [RequireComponent(typeof(Collider))]
    public sealed class AircraftServiceInteractionTarget : MonoBehaviour
    {
        [SerializeField] private P51AircraftServiceController serviceController;
        [SerializeField] private AircraftServiceInteractionKind interactionKind;
        [SerializeField, Min(0)] private int targetIndex;
        [SerializeField, Min(0.1f)] private float holdDuration = 0.9f;
        [SerializeField] private GameObject highlightRoot;
        [SerializeField] private GameObject animatedVisual;
        [SerializeField] private Transform alternatePose;
        [SerializeField, Min(0f)] private float animationLift = 0.12f;
        [SerializeField, Min(0f)] private float rotationTurns = 2f;

        private Collider interactionCollider;
        private float holdProgress;
        private bool isHolding;
        private bool isRemoving;

        private bool cowlingStartPoseCaptured;
        private Vector3 cowlingStartWorldPosition;
        private Quaternion cowlingStartWorldRotation;

        public P51AircraftServiceController ServiceController => serviceController;
        public AircraftServiceInteractionKind InteractionKind => interactionKind;
        public int TargetIndex => targetIndex;
        public float HoldProgress => holdProgress;
        public GameObject HighlightRoot => highlightRoot;
        public GameObject AnimatedVisual => animatedVisual;
        public bool CanInstall => serviceController != null
            && serviceController.CanInstallTarget(interactionKind, targetIndex);
        public bool CanRemove => serviceController != null
            && serviceController.CanRemoveTarget(interactionKind, targetIndex);
        public bool CanInteract => CanInstall || CanRemove;

        public string InteractionText
        {
            get
            {
                if (serviceController == null)
                {
                    return string.Empty;
                }

                bool removing = isHolding
                    ? isRemoving
                    : CanRemove && !CanInstall;
                return serviceController.GetInteractionText(
                    interactionKind,
                    targetIndex,
                    removing,
                    holdProgress);
            }
        }

        private Vector3 FinalWorldPosition => transform.position;
        private Quaternion FinalWorldRotation => transform.rotation;
        private Vector3 RaisedWorldPosition =>
            FinalWorldPosition + transform.up * animationLift;

        private void Awake()
        {
            interactionCollider = GetComponent<Collider>();
            DisableChildColliders();
            RefreshFromController();
        }

        private void OnEnable()
        {
            interactionCollider = GetComponent<Collider>();
            DisableChildColliders();
            RefreshFromController();
        }

        public void Configure(
            P51AircraftServiceController configuredServiceController,
            AircraftServiceInteractionKind configuredKind,
            int configuredTargetIndex,
            float configuredHoldDuration,
            GameObject configuredHighlightRoot,
            GameObject configuredAnimatedVisual,
            Transform configuredAlternatePose,
            float configuredAnimationLift,
            float configuredRotationTurns)
        {
            serviceController = configuredServiceController;
            interactionKind = configuredKind;
            targetIndex = Mathf.Max(0, configuredTargetIndex);
            holdDuration = Mathf.Max(0.1f, configuredHoldDuration);
            highlightRoot = configuredHighlightRoot;
            animatedVisual = configuredAnimatedVisual;
            alternatePose = configuredAlternatePose;
            animationLift = Mathf.Max(0f, configuredAnimationLift);
            rotationTurns = Mathf.Max(0f, configuredRotationTurns);

            interactionCollider = GetComponent<Collider>();
            DisableChildColliders();
            ResetCowlingAnimationState();
            RefreshFromController();
        }

        public bool ProcessInteraction(
            bool installHeld,
            bool removeHeld,
            float deltaTime,
            out string resultMessage)
        {
            resultMessage = string.Empty;

            bool shouldInstall = installHeld && !removeHeld && CanInstall;
            bool shouldRemove = removeHeld && !installHeld && CanRemove;
            if (!shouldInstall && !shouldRemove)
            {
                CancelHold();
                return false;
            }

            bool beginningNewHold = !isHolding || isRemoving != shouldRemove;
            if (beginningNewHold)
            {
                holdProgress = 0f;
                isRemoving = shouldRemove;
                CaptureCowlingAnimationStartPose();
            }

            isHolding = true;
            isRemoving = shouldRemove;
            holdProgress = Mathf.Clamp01(
                holdProgress + Mathf.Max(0f, deltaTime) / holdDuration);
            ApplyAnimatedPose(holdProgress, isRemoving);

            if (holdProgress < 1f)
            {
                return false;
            }

            bool completed = isRemoving
                ? serviceController.TryRemoveTarget(
                    interactionKind,
                    targetIndex,
                    out resultMessage)
                : serviceController.TryInstallTarget(
                    interactionKind,
                    targetIndex,
                    out resultMessage);

            holdProgress = 0f;
            isHolding = false;
            isRemoving = false;
            ResetCowlingAnimationState();
            RefreshFromController();
            return completed;
        }

        public void CancelHold()
        {
            if (!isHolding && holdProgress <= 0f)
            {
                return;
            }

            if (interactionKind == AircraftServiceInteractionKind.CowlingPanel
                && animatedVisual != null
                && cowlingStartPoseCaptured)
            {
                SetAnimatedWorldPose(
                    cowlingStartWorldPosition,
                    cowlingStartWorldRotation);
            }

            holdProgress = 0f;
            isHolding = false;
            isRemoving = false;
            ResetCowlingAnimationState();
            RefreshFromController();
        }

        public void RefreshFromController()
        {
            if (interactionCollider == null)
            {
                interactionCollider = GetComponent<Collider>();
            }

            DisableChildColliders();

            if (interactionCollider != null)
            {
                interactionCollider.enabled = CanInteract;
            }

            if (highlightRoot != null)
            {
                highlightRoot.SetActive(
                    serviceController != null
                    && serviceController.ShouldHighlightTarget(interactionKind, targetIndex));
            }

            if (animatedVisual == null || serviceController == null || isHolding)
            {
                return;
            }

            bool shouldShow = serviceController.ShouldShowAnimatedVisual(interactionKind);
            animatedVisual.SetActive(shouldShow);
            if (!shouldShow)
            {
                return;
            }

            if (interactionKind == AircraftServiceInteractionKind.CowlingPanel)
            {
                // The portable panel owns its pose while carried or resting in the
                // world. Only enforce the exact aircraft pose after installation.
                if (serviceController.IsTopCowlingInstalled)
                {
                    SetAnimatedWorldPose(FinalWorldPosition, FinalWorldRotation);
                }
                return;
            }

            bool completed = serviceController.IsTargetComplete(
                interactionKind,
                targetIndex);
            SetAnimatedWorldPose(
                completed ? FinalWorldPosition : RaisedWorldPosition,
                FinalWorldRotation);
        }

        private void CaptureCowlingAnimationStartPose()
        {
            ResetCowlingAnimationState();
            if (interactionKind != AircraftServiceInteractionKind.CowlingPanel
                || animatedVisual == null)
            {
                return;
            }

            cowlingStartWorldPosition = animatedVisual.transform.position;
            cowlingStartWorldRotation = animatedVisual.transform.rotation;
            cowlingStartPoseCaptured = true;
        }

        private void ResetCowlingAnimationState()
        {
            cowlingStartPoseCaptured = false;
            cowlingStartWorldPosition = Vector3.zero;
            cowlingStartWorldRotation = Quaternion.identity;
        }

        private void ApplyAnimatedPose(float normalizedProgress, bool removing)
        {
            if (animatedVisual == null)
            {
                return;
            }

            animatedVisual.SetActive(true);

            float smooth = normalizedProgress * normalizedProgress
                * (3f - 2f * normalizedProgress);

            if (interactionKind == AircraftServiceInteractionKind.CowlingPanel)
            {
                if (!cowlingStartPoseCaptured)
                {
                    CaptureCowlingAnimationStartPose();
                }

                Vector3 endPosition;
                Quaternion endRotation;
                if (removing)
                {
                    // Lift the panel clear of the installed engine before the
                    // Player carry system takes ownership at completion.
                    endPosition = FinalWorldPosition
                        + transform.up * Mathf.Max(0.48f, animationLift)
                        + transform.right * 0.16f;
                    endRotation = FinalWorldRotation
                        * Quaternion.Euler(-8f, 0f, 12f);
                }
                else
                {
                    // Fly the actual loose/carried panel back to its exact mount.
                    endPosition = FinalWorldPosition;
                    endRotation = FinalWorldRotation;
                }

                SetAnimatedWorldPose(
                    Vector3.Lerp(cowlingStartWorldPosition, endPosition, smooth),
                    Quaternion.Slerp(cowlingStartWorldRotation, endRotation, smooth));
                return;
            }

            Vector3 startPosition = removing ? FinalWorldPosition : RaisedWorldPosition;
            Vector3 endFastenerPosition = removing ? RaisedWorldPosition : FinalWorldPosition;
            Quaternion startRotation = FinalWorldRotation;
            Quaternion endFastenerRotation = FinalWorldRotation;

            Vector3 animatedPosition = Vector3.Lerp(
                startPosition,
                endFastenerPosition,
                smooth);
            Quaternion animatedRotation = Quaternion.Slerp(
                startRotation,
                endFastenerRotation,
                smooth);

            float spinDirection = removing ? -1f : 1f;
            Quaternion spin = Quaternion.AngleAxis(
                360f * rotationTurns * normalizedProgress * spinDirection,
                transform.up);
            animatedRotation = spin * FinalWorldRotation;

            SetAnimatedWorldPose(animatedPosition, animatedRotation);
        }

        private void SetAnimatedWorldPose(Vector3 worldPosition, Quaternion worldRotation)
        {
            if (animatedVisual != null)
            {
                animatedVisual.transform.SetPositionAndRotation(worldPosition, worldRotation);
            }
        }

        private void DisableChildColliders()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                if (collider != null && collider != interactionCollider)
                {
                    collider.enabled = false;
                }
            }
        }

        private void OnDisable()
        {
            holdProgress = 0f;
            isHolding = false;
            isRemoving = false;
            ResetCowlingAnimationState();
        }

        private void OnValidate()
        {
            targetIndex = Mathf.Max(0, targetIndex);
            holdDuration = Mathf.Max(0.1f, holdDuration);
            animationLift = Mathf.Max(0f, animationLift);
            rotationTurns = Mathf.Max(0f, rotationTurns);
        }
    }
}
