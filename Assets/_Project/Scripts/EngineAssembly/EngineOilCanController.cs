using UnityEngine;

namespace Hanger51.EngineAssembly
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class EngineOilCanController : MonoBehaviour
    {
        [Header("Can")]
        [SerializeField, Min(0.1f)] private float capacityLiters = 20f;
        [SerializeField, Min(0f)] private float remainingLiters = 20f;
        [SerializeField, Min(0.1f)] private float pourRateLitersPerSecond = 4f;
        [SerializeField] private Transform capPivot;
        [SerializeField] private Vector3 closedCapEuler;
        [SerializeField] private Vector3 openCapEuler = new Vector3(0f, 0f, -115f);
        [SerializeField] private ParticleSystem pourEffect;
        [SerializeField] private Collider pickupCollider;

        [Header("Carry Pose")]
        [SerializeField] private Vector3 carryLocalPosition = new Vector3(0.48f, -0.36f, 0.82f);
        [SerializeField] private Vector3 carryLocalEuler = new Vector3(8f, -18f, -18f);
        [SerializeField, Min(1f)] private float capAnimationSharpness = 12f;

        [Header("State")]
        [SerializeField] private bool carried;
        [SerializeField] private bool open;
        private Transform carryAnchor;
        private bool pouring;

        public float CapacityLiters => capacityLiters;
        public float RemainingLiters => remainingLiters;
        public bool IsCarried => carried;
        public bool IsOpen => open;
        public bool IsEmpty => remainingLiters <= 0.001f;
        public string InteractionText => carried
            ? open
                ? $"F: close oil can | {remainingLiters:F1} L remaining"
                : $"F: open oil can | {remainingLiters:F1} L remaining"
            : $"E: pick up aircraft oil can — {remainingLiters:F1} L";

        private void Awake()
        {
            ResolveReferences();
            remainingLiters = Mathf.Clamp(remainingLiters, 0f, capacityLiters);
            StopPouring();
        }

        private void Update()
        {
            if (capPivot != null)
            {
                Quaternion target = Quaternion.Euler(open ? openCapEuler : closedCapEuler);
                float blend = 1f - Mathf.Exp(-capAnimationSharpness * Time.deltaTime);
                capPivot.localRotation = Quaternion.Slerp(
                    capPivot.localRotation,
                    target,
                    blend);
            }

            if (carried && carryAnchor != null)
            {
                transform.localPosition = carryLocalPosition;
                transform.localRotation = Quaternion.Euler(carryLocalEuler);
            }

            if (!pouring)
            {
                StopPourEffectOnly();
            }
            pouring = false;
        }

        public void Configure(
            float configuredCapacityLiters,
            float configuredPourRate,
            Transform configuredCapPivot,
            ParticleSystem configuredPourEffect,
            Collider configuredPickupCollider)
        {
            capacityLiters = Mathf.Max(0.1f, configuredCapacityLiters);
            remainingLiters = capacityLiters;
            pourRateLitersPerSecond = Mathf.Max(0.1f, configuredPourRate);
            capPivot = configuredCapPivot;
            pourEffect = configuredPourEffect;
            pickupCollider = configuredPickupCollider;
            carried = false;
            open = false;
            StopPouring();
        }

        public bool TryPickUp(Transform configuredCarryAnchor, out string resultMessage)
        {
            resultMessage = string.Empty;
            if (carried || configuredCarryAnchor == null)
            {
                resultMessage = carried
                    ? "You are already carrying this oil can."
                    : "The Player carry point is missing.";
                return false;
            }

            carryAnchor = configuredCarryAnchor;
            transform.SetParent(carryAnchor, false);
            transform.localPosition = carryLocalPosition;
            transform.localRotation = Quaternion.Euler(carryLocalEuler);
            carried = true;
            if (pickupCollider != null)
            {
                pickupCollider.enabled = false;
            }
            resultMessage = "Picked up the oil can. Press F to open it, then hold E at an engine oil filler to pour.";
            return true;
        }

        public void Drop(Vector3 worldPosition, Quaternion worldRotation)
        {
            StopPouring();
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(worldPosition, worldRotation);
            carried = false;
            carryAnchor = null;
            if (pickupCollider != null)
            {
                pickupCollider.enabled = true;
            }
        }

        public bool TryToggleCap(out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!carried)
            {
                resultMessage = "Pick up the oil can before opening it.";
                return false;
            }

            open = !open;
            if (!open)
            {
                StopPouring();
            }
            resultMessage = open
                ? "Oil can opened. Hold E while aiming at an engine oil filler to pour."
                : "Oil can closed.";
            return true;
        }

        public float PourInto(
            EngineConditionController condition,
            float deltaTime,
            out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!carried)
            {
                resultMessage = "Pick up the oil can first.";
                return 0f;
            }
            if (!open)
            {
                resultMessage = "Open the oil can with F before pouring.";
                return 0f;
            }
            if (IsEmpty)
            {
                resultMessage = "The oil can is empty.";
                return 0f;
            }
            if (condition == null || !condition.CanService)
            {
                resultMessage = condition != null && condition.EngineRunning
                    ? "Stop the engine before adding oil."
                    : "The engine cannot be serviced in its current position.";
                return 0f;
            }

            float requested = Mathf.Min(
                remainingLiters,
                pourRateLitersPerSecond * Mathf.Max(0f, deltaTime));
            float accepted = condition.AddOil(requested);
            remainingLiters = Mathf.Max(0f, remainingLiters - accepted);
            pouring = accepted > 0f;
            if (pouring && pourEffect != null && !pourEffect.isPlaying)
            {
                pourEffect.Play(true);
            }

            resultMessage = accepted > 0f
                ? $"Pouring oil — {condition.GetOilReadingText()} | Can: {remainingLiters:F1} L"
                : condition.OilQuantityLiters >= condition.OilCapacityLiters - 0.01f
                    ? "The engine oil system is full."
                    : "Oil could not be added.";
            return accepted;
        }

        public void StopPouring()
        {
            pouring = false;
            StopPourEffectOnly();
        }

        private void StopPourEffectOnly()
        {
            if (pourEffect != null && pourEffect.isPlaying)
            {
                pourEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void ResolveReferences()
        {
            if (pickupCollider == null)
            {
                pickupCollider = GetComponent<Collider>();
            }
        }

        private void OnDisable()
        {
            StopPouring();
        }

        private void OnValidate()
        {
            capacityLiters = Mathf.Max(0.1f, capacityLiters);
            remainingLiters = Mathf.Clamp(remainingLiters, 0f, capacityLiters);
            pourRateLitersPerSecond = Mathf.Max(0.1f, pourRateLitersPerSecond);
            capAnimationSharpness = Mathf.Max(1f, capAnimationSharpness);
            ResolveReferences();
        }
    }
}
