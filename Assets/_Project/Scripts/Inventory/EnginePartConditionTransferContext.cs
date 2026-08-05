namespace Hanger51.Inventory
{
    public static class EnginePartConditionTransferContext
    {
        private static EnginePartConditionData pendingCondition;
        private static int transferDepth;

        public static bool IsActive => transferDepth > 0
            && pendingCondition != null
            && pendingCondition.IsTracked;

        public static void Begin(EnginePartConditionData condition)
        {
            transferDepth++;
            if (condition != null && condition.IsTracked)
            {
                pendingCondition = condition.Clone();
            }
        }

        public static EnginePartConditionData PeekForItem(
            InventoryItemDefinition item)
        {
            if (!IsActive || item == null)
            {
                return null;
            }

            return EnginePartConditionData.InferKind(item) == pendingCondition.Kind
                ? pendingCondition.Clone()
                : null;
        }

        public static void End()
        {
            transferDepth = System.Math.Max(0, transferDepth - 1);
            if (transferDepth == 0)
            {
                pendingCondition = null;
            }
        }

        public static void Clear()
        {
            transferDepth = 0;
            pendingCondition = null;
        }
    }
}
