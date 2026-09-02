using UnityEngine;

namespace Hanger51.EngineAssembly
{
    [DisallowMultipleComponent]
    public sealed class EngineConditionLink : MonoBehaviour
    {
        [SerializeField] private EngineConditionController condition;

        public EngineConditionController Condition => condition;

        public void Configure(EngineConditionController configuredCondition)
        {
            condition = configuredCondition;
        }
    }
}
