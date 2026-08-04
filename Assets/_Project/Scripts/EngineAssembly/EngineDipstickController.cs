using UnityEngine;

namespace Hanger51.EngineAssembly
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class EngineDipstickController : MonoBehaviour
    {
        [SerializeField] private EngineConditionController condition;
        [SerializeField] private Transform dipstickVisual;
        [SerializeField] private Transform oilStainVisual;
        [SerializeField] private Vector3 insertedLocalPosition;
        [SerializeField] private Vector3 pulledLocalPosition = new Vector3(0f, 0.42f, 0f);
        [SerializeField, Min(1f)] private float movementSharpness = 12f;
        [SerializeField, Min(0.01f)] private float maximumOilStainLength = 0.24f;
        [SerializeField] private bool pulled;

        public EngineConditionController Condition => condition;
        public bool IsPulled => pulled;
        public bool CanInteract => condition != null && condition.CanService;
        public string InteractionText
        {
            get
            {
                if (!CanInteract)
                {
                    return condition != null && condition.EngineRunning
                        ? "Stop the engine before checking the oil dipstick"
                        : "The dipstick cannot be used while the engine is suspended";
                }

                return pulled
                    ? $"E: reinsert oil dipstick — {condition.GetOilReadingText()}"
                    : "E: pull oil dipstick";
            }
        }

        private void Awake()
        {
            ResolveReferences();
            if (dipstickVisual != null && insertedLocalPosition == Vector3.zero)
            {
                insertedLocalPosition = dipstickVisual.localPosition;
            }
            RefreshOilStain();
        }

        private void Update()
        {
            ResolveReferences();
            if (dipstickVisual != null)
            {
                Vector3 target = pulled ? pulledLocalPosition : insertedLocalPosition;
                float blend = 1f - Mathf.Exp(-movementSharpness * Time.deltaTime);
                dipstickVisual.localPosition = Vector3.Lerp(
                    dipstickVisual.localPosition,
                    target,
                    blend);
            }

            RefreshOilStain();
        }

        public void Configure(
            EngineConditionController configuredCondition,
            Transform configuredDipstickVisual,
            Transform configuredOilStainVisual,
            Vector3 configuredInsertedLocalPosition,
            Vector3 configuredPulledLocalPosition,
            float configuredMaximumOilStainLength)
        {
            condition = configuredCondition;
            dipstickVisual = configuredDipstickVisual;
            oilStainVisual = configuredOilStainVisual;
            insertedLocalPosition = configuredInsertedLocalPosition;
            pulledLocalPosition = configuredPulledLocalPosition;
            maximumOilStainLength = Mathf.Max(0.01f, configuredMaximumOilStainLength);
            pulled = false;
            if (dipstickVisual != null)
            {
                dipstickVisual.localPosition = insertedLocalPosition;
            }
            RefreshOilStain();
        }

        public bool TryToggle(out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!CanInteract)
            {
                resultMessage = InteractionText;
                return false;
            }

            pulled = !pulled;
            resultMessage = pulled
                ? $"Dipstick removed. {condition.GetOilReadingText()}"
                : "Oil dipstick reinserted.";
            RefreshOilStain();
            return true;
        }

        private void RefreshOilStain()
        {
            if (oilStainVisual == null || condition == null)
            {
                return;
            }

            float fraction = condition.OilFraction;
            float length = Mathf.Max(0.006f, maximumOilStainLength * fraction);
            Vector3 scale = oilStainVisual.localScale;
            scale.y = length;
            oilStainVisual.localScale = scale;
            oilStainVisual.gameObject.SetActive(pulled && fraction > 0.001f);
        }

        private void ResolveReferences()
        {
            if (condition == null)
            {
                condition = GetComponentInParent<EngineConditionController>();
            }
            if (dipstickVisual == null)
            {
                dipstickVisual = transform;
            }
        }

        private void OnValidate()
        {
            movementSharpness = Mathf.Max(1f, movementSharpness);
            maximumOilStainLength = Mathf.Max(0.01f, maximumOilStainLength);
            ResolveReferences();
        }
    }
}
