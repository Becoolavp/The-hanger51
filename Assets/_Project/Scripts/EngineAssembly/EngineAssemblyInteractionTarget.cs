using Hanger51.Inventory;
using UnityEngine;

namespace Hanger51.EngineAssembly
{
    public enum EngineAssemblyInteractionKind
    {
        CoverPlacement,
        CoverBolt,
        SparkPlug
    }

    [RequireComponent(typeof(Collider))]
    public sealed class EngineAssemblyInteractionTarget : MonoBehaviour
    {
        [SerializeField] private EngineAssemblyStation station;
        [SerializeField] private EngineAssemblyInteractionKind interactionKind;
        [SerializeField, Min(0)] private int groupIndex;
        [SerializeField, Min(0)] private int targetIndex;
        [SerializeField, Min(0.1f)] private float holdDuration = 0.9f;
        [SerializeField] private GameObject highlightRoot;
        [SerializeField] private GameObject animatedVisual;
        [SerializeField, Min(0f)] private float animationLift = 0.12f;
        [SerializeField, Min(0f)] private float rotationTurns = 2f;

        private Collider interactionCollider;
        private float holdProgress;
        private bool isHolding;

        public EngineAssemblyInteractionKind InteractionKind => interactionKind;
        public int GroupIndex => groupIndex;
        public int TargetIndex => targetIndex;
        public float HoldProgress => holdProgress;
        public bool IsInteractable => station != null
            && station.IsTargetAvailable(interactionKind, groupIndex, targetIndex);

        public Vector3 FinalWorldPosition => transform.position;
        public Quaternion FinalWorldRotation => transform.rotation;
        public Vector3 RaisedWorldPosition =>
            FinalWorldPosition + transform.up * animationLift;

        public string InteractionText => station != null
            ? station.GetTargetInteractionText(
                interactionKind,
                groupIndex,
                targetIndex,
                holdProgress)
            : string.Empty;

        private void Awake()
        {
            interactionCollider = GetComponent<Collider>();
            RefreshFromStation();
        }

        public void Configure(
            EngineAssemblyStation configuredStation,
            EngineAssemblyInteractionKind configuredKind,
            int configuredGroupIndex,
            int configuredTargetIndex,
            float configuredHoldDuration,
            GameObject configuredHighlightRoot,
            GameObject configuredAnimatedVisual,
            float configuredAnimationLift,
            float configuredRotationTurns)
        {
            station = configuredStation;
            interactionKind = configuredKind;
            groupIndex = Mathf.Max(0, configuredGroupIndex);
            targetIndex = Mathf.Max(0, configuredTargetIndex);
            holdDuration = Mathf.Max(0.1f, configuredHoldDuration);
            highlightRoot = configuredHighlightRoot;
            animatedVisual = configuredAnimatedVisual;
            animationLift = Mathf.Max(0f, configuredAnimationLift);
            rotationTurns = Mathf.Max(0f, configuredRotationTurns);

            interactionCollider = GetComponent<Collider>();
            RefreshFromStation();
        }

        public bool ProcessHold(
            PlayerInventory inventory,
            bool isHeld,
            float deltaTime,
            out string resultMessage)
        {
            resultMessage = string.Empty;

            if (!IsInteractable)
            {
                CancelHold();
                return false;
            }

            if (!isHeld)
            {
                CancelHold();
                return false;
            }

            isHolding = true;
            holdProgress = Mathf.Clamp01(
                holdProgress + Mathf.Max(0f, deltaTime) / holdDuration);
            ApplyAnimatedPose(holdProgress);

            if (holdProgress < 1f)
            {
                return false;
            }

            bool completed = station.TryCompleteTarget(
                interactionKind,
                groupIndex,
                targetIndex,
                inventory,
                out resultMessage);

            holdProgress = 0f;
            isHolding = false;
            RefreshFromStation();
            return completed;
        }

        public void CancelHold()
        {
            if (!isHolding && holdProgress <= 0f)
            {
                return;
            }

            holdProgress = 0f;
            isHolding = false;
            RefreshFromStation();
        }

        public void RefreshFromStation()
        {
            if (interactionCollider == null)
            {
                interactionCollider = GetComponent<Collider>();
            }

            bool available = IsInteractable;
            bool completed = station != null
                && station.IsTargetComplete(interactionKind, groupIndex, targetIndex);
            bool shouldHighlight = station != null
                && station.ShouldHighlightTarget(interactionKind, groupIndex, targetIndex);

            if (interactionCollider != null)
            {
                interactionCollider.enabled = available;
            }

            if (highlightRoot != null)
            {
                highlightRoot.SetActive(shouldHighlight && !completed);
            }

            if (animatedVisual == null || isHolding)
            {
                return;
            }

            if (interactionKind == EngineAssemblyInteractionKind.CoverBolt)
            {
                animatedVisual.SetActive(true);
                SetAnimatedWorldPose(
                    completed ? FinalWorldPosition : RaisedWorldPosition,
                    FinalWorldRotation);
                return;
            }

            SetAnimatedWorldPose(FinalWorldPosition, FinalWorldRotation);
            animatedVisual.SetActive(completed);
        }

        private void ApplyAnimatedPose(float normalizedProgress)
        {
            if (animatedVisual == null)
            {
                return;
            }

            animatedVisual.SetActive(true);

            Vector3 animatedPosition = Vector3.Lerp(
                RaisedWorldPosition,
                FinalWorldPosition,
                normalizedProgress);

            Quaternion spin = Quaternion.AngleAxis(
                360f * rotationTurns * normalizedProgress,
                transform.up);
            Quaternion animatedRotation = spin * FinalWorldRotation;

            SetAnimatedWorldPose(animatedPosition, animatedRotation);
        }

        private void SetAnimatedWorldPose(Vector3 worldPosition, Quaternion worldRotation)
        {
            if (animatedVisual == null)
            {
                return;
            }

            animatedVisual.transform.SetPositionAndRotation(
                worldPosition,
                worldRotation);
        }

        private void OnDisable()
        {
            holdProgress = 0f;
            isHolding = false;
        }

        private void OnValidate()
        {
            groupIndex = Mathf.Max(0, groupIndex);
            targetIndex = Mathf.Max(0, targetIndex);
            holdDuration = Mathf.Max(0.1f, holdDuration);
            animationLift = Mathf.Max(0f, animationLift);
            rotationTurns = Mathf.Max(0f, rotationTurns);
        }
    }
}
