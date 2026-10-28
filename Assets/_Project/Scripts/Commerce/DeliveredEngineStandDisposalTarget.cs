using System.Reflection;
using Hanger51.EngineAssembly;
using UnityEngine;

namespace Hanger51.Commerce
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class DeliveredEngineStandDisposalTarget : MonoBehaviour
    {
        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly FieldInfo StandLocalPositionField =
            typeof(EngineAssemblyTransportController).GetField(
                "standLocalPosition",
                PrivateInstance);
        private static readonly FieldInfo IsOnStandField =
            typeof(EngineAssemblyTransportController).GetField(
                "isOnStand",
                PrivateInstance);

        [SerializeField] private EngineAssemblyTransportController engineTransport;
        [SerializeField] private Collider interactionCollider;
        [SerializeField, Min(0.4f)] private float dismantleHoldDuration = 1.25f;
        [SerializeField, Min(0.5f)] private float minimumEngineClearance = 2.6f;
        [SerializeField] private bool standRemoved;

        private float dismantleProgress;

        public EngineAssemblyTransportController EngineTransport => engineTransport;
        public bool StandRemoved => standRemoved;
        public bool IsConfigured => engineTransport != null && interactionCollider != null;

        public string InteractionText
        {
            get
            {
                if (standRemoved)
                {
                    return string.Empty;
                }

                if (!CanDismantle(out string reason))
                {
                    return reason;
                }

                int percent = Mathf.RoundToInt(dismantleProgress * 100f);
                return dismantleProgress > 0f
                    ? $"Hold R to remove empty delivered engine stand ({percent}%)"
                    : "Hold R to dismantle and remove empty delivered engine stand";
            }
        }

        private void Awake()
        {
            ResolveReferences();
            ApplyColliderState();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ApplyColliderState();
        }

        public void Configure(
            EngineAssemblyTransportController configuredTransport,
            Collider configuredInteractionCollider)
        {
            engineTransport = configuredTransport;
            interactionCollider = configuredInteractionCollider;
            standRemoved = false;
            dismantleProgress = 0f;
            ApplyColliderState();
        }

        public bool CanDismantle(out string reason)
        {
            ResolveReferences();
            reason = string.Empty;

            if (standRemoved)
            {
                reason = "The delivered engine stand has already been removed.";
                return false;
            }

            if (engineTransport == null || engineTransport.TransportRoot == null)
            {
                reason = "This delivered stand is missing its engine transport connection.";
                return false;
            }

            if (engineTransport.IsSuspended)
            {
                reason = "Lower the engine clear of the stand before removing the holder.";
                return false;
            }

            if (!engineTransport.HasEngine)
            {
                return true;
            }

            if (engineTransport.IsOnStand)
            {
                reason = "Lift the engine off this delivered stand before removing the holder.";
                return false;
            }

            Vector2 standPosition = new Vector2(
                engineTransport.transform.position.x,
                engineTransport.transform.position.z);
            Vector2 enginePosition = new Vector2(
                engineTransport.TransportRoot.position.x,
                engineTransport.TransportRoot.position.z);
            float clearance = Vector2.Distance(standPosition, enginePosition);
            if (clearance < minimumEngineClearance)
            {
                reason = $"Move the engine farther from the stand ({clearance:F1}/{minimumEngineClearance:F1} m).";
                return false;
            }

            return true;
        }

        public bool ProcessDismantleHold(
            bool isHeld,
            float deltaTime,
            out string resultMessage)
        {
            resultMessage = string.Empty;

            if (!CanDismantle(out _))
            {
                CancelHold();
                return false;
            }

            if (!isHeld)
            {
                CancelHold();
                return false;
            }

            dismantleProgress = Mathf.Clamp01(
                dismantleProgress
                + Mathf.Max(0f, deltaTime) / Mathf.Max(0.4f, dismantleHoldDuration));
            if (dismantleProgress < 1f)
            {
                return false;
            }

            DismantleStand();
            resultMessage = "Dismantled and removed the empty delivered engine stand.";
            return true;
        }

        public void CancelHold()
        {
            dismantleProgress = 0f;
        }

        private void DismantleStand()
        {
            if (standRemoved || engineTransport == null)
            {
                return;
            }

            Transform stationRoot = engineTransport.transform;
            Transform portableRoot = engineTransport.TransportRoot;

            Renderer[] renderers = stationRoot.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null
                    || (portableRoot != null && renderer.transform.IsChildOf(portableRoot)))
                {
                    continue;
                }

                renderer.enabled = false;
            }

            Collider[] colliders = stationRoot.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                if (collider == null
                    || collider == interactionCollider
                    || (portableRoot != null && collider.transform.IsChildOf(portableRoot)))
                {
                    continue;
                }

                collider.enabled = false;
            }

            ShipmentDeliveryOccupancy occupancy =
                stationRoot.GetComponent<ShipmentDeliveryOccupancy>();
            if (occupancy != null)
            {
                // Removing the holder is the point at which this delivered
                // assembly has been cleared from receiving. Destroying the
                // occupancy component releases its reserved shipment bay.
                Destroy(occupancy);
            }

            // Keep the invisible station/controller root alive because the
            // portable engine's assembly state still belongs to it. Moving the
            // saved stand pose far below the world prevents the hoist from
            // offering to return an engine to a stand that no longer exists.
            StandLocalPositionField?.SetValue(
                engineTransport,
                new Vector3(0f, -10000f, 0f));
            IsOnStandField?.SetValue(engineTransport, false);

            standRemoved = true;
            dismantleProgress = 0f;
            ApplyColliderState();
        }

        private void ResolveReferences()
        {
            if (engineTransport == null)
            {
                engineTransport = GetComponentInParent<EngineAssemblyTransportController>();
            }

            if (interactionCollider == null)
            {
                interactionCollider = GetComponent<Collider>();
            }
        }

        private void ApplyColliderState()
        {
            if (interactionCollider != null)
            {
                interactionCollider.enabled = !standRemoved;
            }
        }

        private void OnDisable()
        {
            CancelHold();
        }

        private void OnValidate()
        {
            dismantleHoldDuration = Mathf.Max(0.4f, dismantleHoldDuration);
            minimumEngineClearance = Mathf.Max(0.5f, minimumEngineClearance);
            ResolveReferences();
        }
    }
}
