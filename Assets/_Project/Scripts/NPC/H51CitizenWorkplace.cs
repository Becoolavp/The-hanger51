using UnityEngine;

namespace Hanger51.NPCs
{
    public sealed class H51CitizenWorkplace : MonoBehaviour
    {
        [SerializeField] string workplaceId;
        [SerializeField] string displayName;
        [SerializeField] string townName;
        [SerializeField] string category;
        [SerializeField] string primaryOccupation;
        [SerializeField, Min(1)] int capacity = 6;

        public string WorkplaceId => workplaceId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string TownName => townName;
        public string Category => category;
        public string PrimaryOccupation => primaryOccupation;
        public int Capacity => capacity;

        public void Configure(string id, string label, string town, string workplaceCategory, string occupation, int maxCapacity)
        {
            workplaceId = id;
            displayName = label;
            townName = town;
            category = workplaceCategory;
            primaryOccupation = occupation;
            capacity = Mathf.Max(1, maxCapacity);
        }
    }
}
