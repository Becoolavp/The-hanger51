using UnityEngine;

namespace Hanger51.EngineAssembly
{
    public enum EngineConditionInspectionKind
    {
        EngineBlock,
        CylinderCover,
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
                        return $"X: inspect {(partIndex == 0 ? "left" : "right")} cylinder cover";
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
                    return condition.GetCoverInspectionText(partIndex);
                case EngineConditionInspectionKind.OilFiller:
                    return condition.GetOilReadingText();
                default:
                    return string.Empty;
            }
        }

        private void Awake()
        {
            if (condition == null)
            {
                condition = GetComponentInParent<EngineConditionController>();
            }
        }

        private void OnValidate()
        {
            partIndex = Mathf.Max(0, partIndex);
            if (condition == null)
            {
                condition = GetComponentInParent<EngineConditionController>();
            }
        }
    }
}
