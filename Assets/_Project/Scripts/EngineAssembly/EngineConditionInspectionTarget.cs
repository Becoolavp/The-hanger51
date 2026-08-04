using UnityEngine;

namespace Hanger51.EngineAssembly
{
    public enum EngineConditionInspectionKind
    {
        EngineBlock,
        CylinderCover,
        SparkPlug,
        OilFiller
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class EngineConditionInspectionTarget : MonoBehaviour
    {
        [SerializeField] private EngineConditionController condition;
        [SerializeField] private EngineConditionInspectionKind inspectionKind;
        [SerializeField, Min(0)] private int partIndex;

        public EngineConditionController Condition => condition;
        public EngineConditionInspectionKind InspectionKind => inspectionKind;
        public int PartIndex => partIndex;

        public string InspectionPrompt
        {
            get
            {
                switch (inspectionKind)
                {
                    case EngineConditionInspectionKind.EngineBlock:
                        return "X: inspect engine block condition";
                    case EngineConditionInspectionKind.CylinderCover:
                        return $"X: inspect {(partIndex == 0 ? "left" : "right")} cylinder cover condition";
                    case EngineConditionInspectionKind.SparkPlug:
                        return $"X: inspect cylinder {partIndex / 2 + 1} spark plug";
                    case EngineConditionInspectionKind.OilFiller:
                        return "Oil filler neck";
                    default:
                        return string.Empty;
                }
            }
        }

        public void Configure(
            EngineConditionController configuredCondition,
            EngineConditionInspectionKind configuredKind,
            int configuredPartIndex)
        {
            condition = configuredCondition;
            inspectionKind = configuredKind;
            partIndex = Mathf.Max(0, configuredPartIndex);
        }

        public string GetInspectionText()
        {
            if (condition == null)
            {
                return "Engine condition system is unavailable.";
            }

            switch (inspectionKind)
            {
                case EngineConditionInspectionKind.EngineBlock:
                    return condition.GetBlockInspectionText();

                case EngineConditionInspectionKind.CylinderCover:
                {
                    float health = condition.GetCoverHealth(partIndex);
                    bool cracked = health <= 35f;
                    EngineAssemblyInteractionTarget assemblyTarget =
                        GetComponent<EngineAssemblyInteractionTarget>();
                    string installation = assemblyTarget != null
                        ? assemblyTarget.IsComplete ? "installed" : "removed"
                        : "condition recorded";
                    string side = partIndex == 0 ? "Left" : "Right";
                    return $"{side} cylinder cover: {health:F1}% — "
                        + $"{(cracked ? "CRACKED" : "intact")} — {installation}.";
                }

                case EngineConditionInspectionKind.SparkPlug:
                {
                    float health = condition.GetSparkPlugHealth(partIndex);
                    EngineAssemblyInteractionTarget assemblyTarget =
                        GetComponent<EngineAssemblyInteractionTarget>();
                    string installation = assemblyTarget != null
                        ? assemblyTarget.IsComplete ? "installed" : "removed"
                        : "condition recorded";
                    int cylinder = partIndex / 2 + 1;
                    string position = partIndex % 2 == 0 ? "A" : "B";
                    return $"Cylinder {cylinder} plug {position}: "
                        + $"{health:F2}% — {installation}.";
                }

                case EngineConditionInspectionKind.OilFiller:
                    return condition.GetOilReadingText();

                default:
                    return string.Empty;
            }
        }

        private void Awake()
        {
            ResolveCondition();
        }

        private void ResolveCondition()
        {
            if (condition != null)
            {
                return;
            }

            EngineConditionLink link = GetComponentInParent<EngineConditionLink>();
            condition = link != null
                ? link.Condition
                : GetComponentInParent<EngineConditionController>();
        }

        private void OnValidate()
        {
            partIndex = Mathf.Max(0, partIndex);
            ResolveCondition();
        }
    }
}
