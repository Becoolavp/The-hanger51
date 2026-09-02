using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Hanger51.EngineAssembly
{
    [RequireComponent(typeof(EngineAssemblyStation))]
    public sealed class EngineAssemblyTransportController : MonoBehaviour
    {
        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo HighlightRootField =
            typeof(EngineAssemblyInteractionTarget).GetField("highlightRoot", PrivateInstance);

        [Header("Portable Assembly")]
        [SerializeField] private Transform transportRoot;
        [SerializeField] private Transform liftPoint;
        [SerializeField] private Transform groundContactPoint;
        [SerializeField] private Transform leftLiftLug;
        [SerializeField] private Transform rightLiftLug;
        [SerializeField] private Collider stationInteractionCollider;

        [Header("Stand Pose")]
        [SerializeField] private Vector3 standLocalPosition;
        [SerializeField] private Quaternion standLocalRotation = Quaternion.identity;
        [SerializeField] private Vector3 standLocalScale = Vector3.one;

        [Header("State")]
        [SerializeField] private bool isOnStand = true;
        [SerializeField] private bool isSuspended;

        private EngineAssemblyStation station;
        private readonly List<EngineAssemblyInteractionTarget> interactionTargets =
            new List<EngineAssemblyInteractionTarget>();

        public Transform TransportRoot => transportRoot;
        public Transform LiftPoint => liftPoint;
        public Transform GroundContactPoint => groundContactPoint;
        public Transform LeftLiftLug => leftLiftLug;
        public Transform RightLiftLug => rightLiftLug;
        public bool IsOnStand => isOnStand;
        public bool IsSuspended => isSuspended;
        public bool HasEngine => station != null && station.EngineBlockInstalled;

        public Vector3 StandWorldPosition => transform.TransformPoint(standLocalPosition);
        public Quaternion StandWorldRotation => transform.rotation * standLocalRotation;

        private void Awake()
        {
            ResolveReferences();
            RefreshInteractionTargets();
            ApplyStationColliderState();
        }

        private void OnEnable()
        {
            ResolveReferences();
            RefreshInteractionTargets();
            ApplyStationColliderState();
        }

        private void LateUpdate()
        {
            if (isSuspended)
            {
                // Inventory changes can ask the assembly station to refresh its
                // targets. Reassert the transport lock after all normal Update
                // work so a hanging engine never exposes maintenance prompts.
                EnforceSuspendedInteractionLock();
            }
        }

        public void Configure(
            Transform configuredTransportRoot,
            Transform configuredLiftPoint,
            Transform configuredGroundContactPoint,
            Transform configuredLeftLiftLug,
            Transform configuredRightLiftLug,
            Collider configuredStationInteractionCollider,
            Vector3 configuredStandLocalPosition,
            Quaternion configuredStandLocalRotation,
            Vector3 configuredStandLocalScale)
        {
            station = GetComponent<EngineAssemblyStation>();
            transportRoot = configuredTransportRoot;
            liftPoint = configuredLiftPoint;
            groundContactPoint = configuredGroundContactPoint;
            leftLiftLug = configuredLeftLiftLug;
            rightLiftLug = configuredRightLiftLug;
            stationInteractionCollider = configuredStationInteractionCollider;
            standLocalPosition = configuredStandLocalPosition;
            standLocalRotation = configuredStandLocalRotation;
            standLocalScale = configuredStandLocalScale;
            isOnStand = true;
            isSuspended = false;

            RefreshInteractionTargets();
            SnapToStand();
        }

        public bool CanAttach(Vector3 hookPosition, float maximumHorizontalDistance, out string reason)
        {
            ResolveReferences();
            reason = string.Empty;

            if (!HasEngine)
            {
                reason = "There is no engine block available to lift.";
                return false;
            }

            if (transportRoot == null || liftPoint == null)
            {
                reason = "The portable engine lift points are not configured.";
                return false;
            }

            if (isSuspended)
            {
                reason = "The engine is already attached to the hoist.";
                return false;
            }

            Vector2 hookHorizontal = new Vector2(hookPosition.x, hookPosition.z);
            Vector2 liftHorizontal = new Vector2(liftPoint.position.x, liftPoint.position.z);
            float horizontalDistance = Vector2.Distance(hookHorizontal, liftHorizontal);
            float verticalDistance = Mathf.Abs(hookPosition.y - liftPoint.position.y);

            if (horizontalDistance > maximumHorizontalDistance)
            {
                reason = $"Move the hook closer to the engine lift point ({horizontalDistance:F1} m away).";
                return false;
            }

            if (verticalDistance > 2.8f)
            {
                reason = "The hook is too high or low to connect to the engine.";
                return false;
            }

            return true;
        }

        public void BeginSuspension()
        {
            ResolveReferences();
            if (!HasEngine || transportRoot == null)
            {
                return;
            }

            isSuspended = true;
            isOnStand = false;
            SetMaintenanceInteractionEnabled(false);
            ApplyStationColliderState();
        }

        public void UpdateSuspendedPose(
            Vector3 desiredLiftPointPosition,
            Quaternion desiredRotation,
            float positionSharpness,
            float rotationSharpness,
            float deltaTime)
        {
            if (!isSuspended || transportRoot == null || liftPoint == null)
            {
                return;
            }

            Quaternion yawRotation = Quaternion.Euler(0f, desiredRotation.eulerAngles.y, 0f);
            Vector3 targetRootPosition = CalculateRootPositionForLiftPoint(
                desiredLiftPointPosition,
                yawRotation);

            float positionBlend = 1f - Mathf.Exp(-Mathf.Max(0.1f, positionSharpness) * deltaTime);
            float rotationBlend = 1f - Mathf.Exp(-Mathf.Max(0.1f, rotationSharpness) * deltaTime);

            transportRoot.position = Vector3.Lerp(
                transportRoot.position,
                targetRootPosition,
                positionBlend);
            transportRoot.rotation = Quaternion.Slerp(
                transportRoot.rotation,
                yawRotation,
                rotationBlend);
        }

        public Vector3 CalculateRootPositionForLiftPoint(
            Vector3 desiredLiftPointPosition,
            Quaternion desiredRootRotation)
        {
            if (transportRoot == null || liftPoint == null)
            {
                return desiredLiftPointPosition;
            }

            Vector3 liftPointLocal = transportRoot.InverseTransformPoint(liftPoint.position);
            Vector3 scaledLiftOffset = Vector3.Scale(liftPointLocal, transportRoot.localScale);
            return desiredLiftPointPosition - desiredRootRotation * scaledLiftOffset;
        }

        public Vector3 CalculateRootPositionForGroundContact(
            Vector3 desiredGroundPosition,
            Quaternion desiredRootRotation)
        {
            if (transportRoot == null || groundContactPoint == null)
            {
                return desiredGroundPosition;
            }

            Vector3 groundLocal = transportRoot.InverseTransformPoint(groundContactPoint.position);
            Vector3 scaledGroundOffset = Vector3.Scale(groundLocal, transportRoot.localScale);
            return desiredGroundPosition - desiredRootRotation * scaledGroundOffset;
        }

        public void SetWorldPose(Vector3 worldPosition, Quaternion worldRotation)
        {
            if (transportRoot == null)
            {
                return;
            }

            transportRoot.SetPositionAndRotation(worldPosition, worldRotation);
        }

        public void CompletePlacement(bool placedOnStand)
        {
            isSuspended = false;
            isOnStand = placedOnStand;

            if (placedOnStand)
            {
                SnapToStand();
            }
            else
            {
                SetMaintenanceInteractionEnabled(true);
                ApplyStationColliderState();
            }
        }

        public void SnapToStand()
        {
            if (transportRoot == null)
            {
                return;
            }

            transportRoot.SetParent(transform, false);
            transportRoot.localPosition = standLocalPosition;
            transportRoot.localRotation = standLocalRotation;
            transportRoot.localScale = standLocalScale;
            isSuspended = false;
            isOnStand = true;

            SetMaintenanceInteractionEnabled(true);
            ApplyStationColliderState();
        }

        public float HorizontalDistanceFromHookToStand(Vector3 hookPosition)
        {
            Vector3 standLiftPosition = GetStandLiftPointWorldPosition();
            return Vector2.Distance(
                new Vector2(hookPosition.x, hookPosition.z),
                new Vector2(standLiftPosition.x, standLiftPosition.z));
        }

        public Vector3 GetStandLiftPointWorldPosition()
        {
            if (transportRoot == null || liftPoint == null)
            {
                return StandWorldPosition;
            }

            Vector3 liftLocal = transportRoot.InverseTransformPoint(liftPoint.position);
            Vector3 scaledOffset = Vector3.Scale(liftLocal, standLocalScale);
            return StandWorldPosition + StandWorldRotation * scaledOffset;
        }

        public void RefreshMaintenanceTargets()
        {
            RefreshInteractionTargets();
            for (int index = 0; index < interactionTargets.Count; index++)
            {
                if (interactionTargets[index] != null)
                {
                    interactionTargets[index].RefreshFromStation();
                }
            }

            if (isSuspended)
            {
                EnforceSuspendedInteractionLock();
            }
        }

        private void ResolveReferences()
        {
            if (station == null)
            {
                station = GetComponent<EngineAssemblyStation>();
            }

            if (stationInteractionCollider == null)
            {
                stationInteractionCollider = GetComponent<Collider>();
            }
        }

        private void RefreshInteractionTargets()
        {
            interactionTargets.Clear();
            if (transportRoot == null)
            {
                return;
            }

            EngineAssemblyInteractionTarget[] foundTargets =
                transportRoot.GetComponentsInChildren<EngineAssemblyInteractionTarget>(true);
            interactionTargets.AddRange(foundTargets);
        }

        private void SetMaintenanceInteractionEnabled(bool enabledState)
        {
            RefreshInteractionTargets();

            for (int index = 0; index < interactionTargets.Count; index++)
            {
                EngineAssemblyInteractionTarget target = interactionTargets[index];
                if (target == null)
                {
                    continue;
                }

                target.CancelHold();
                if (enabledState)
                {
                    target.RefreshFromStation();
                }
                else
                {
                    DisableTargetForTransport(target);
                }
            }
        }

        private void EnforceSuspendedInteractionLock()
        {
            if (interactionTargets.Count == 0)
            {
                RefreshInteractionTargets();
            }

            for (int index = 0; index < interactionTargets.Count; index++)
            {
                EngineAssemblyInteractionTarget target = interactionTargets[index];
                if (target != null)
                {
                    DisableTargetForTransport(target);
                }
            }
        }

        private static void DisableTargetForTransport(
            EngineAssemblyInteractionTarget target)
        {
            Collider targetCollider = target.GetComponent<Collider>();
            if (targetCollider != null)
            {
                targetCollider.enabled = false;
            }

            GameObject highlightRoot =
                HighlightRootField?.GetValue(target) as GameObject;
            if (highlightRoot != null)
            {
                // Disable only the configured root. Child beacon and stem
                // active states remain intact so RefreshFromStation can
                // restore the complete marker after placement.
                highlightRoot.SetActive(false);
            }
        }

        private void ApplyStationColliderState()
        {
            if (stationInteractionCollider == null)
            {
                return;
            }

            // The large stand collider should only handle inventory placement
            // and bare-engine removal while the engine is physically on it.
            stationInteractionCollider.enabled = isOnStand && !isSuspended;
        }

        private void OnValidate()
        {
            if (standLocalScale == Vector3.zero)
            {
                standLocalScale = Vector3.one;
            }
        }
    }
}
