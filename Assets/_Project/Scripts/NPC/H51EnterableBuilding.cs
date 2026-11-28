using UnityEngine;

namespace Hanger51.NPCs
{
    public enum H51EnterableBuildingKind
    {
        Home,
        Store,
        Workplace,
        Civic,
        Farm
    }

    public sealed class H51EnterableBuilding : MonoBehaviour
    {
        [SerializeField] string buildingId;
        [SerializeField] string displayName;
        [SerializeField] string townName;
        [SerializeField] H51EnterableBuildingKind kind;
        [SerializeField] Transform entrance;
        [SerializeField] Transform interiorCenter;

        public string BuildingId => buildingId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string TownName => townName;
        public H51EnterableBuildingKind Kind => kind;
        public Transform Entrance => entrance;
        public Transform InteriorCenter => interiorCenter;

        public void Configure(
            string id,
            string label,
            string town,
            H51EnterableBuildingKind buildingKind,
            Transform entrancePoint,
            Transform insidePoint)
        {
            buildingId = id;
            displayName = label;
            townName = town;
            kind = buildingKind;
            entrance = entrancePoint;
            interiorCenter = insidePoint;
        }
    }
}
