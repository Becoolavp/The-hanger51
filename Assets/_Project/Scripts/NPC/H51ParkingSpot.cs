using UnityEngine;

namespace Hanger51.NPCs
{
    public sealed class H51ParkingSpot : MonoBehaviour
    {
        [SerializeField] string parkingId;
        [SerializeField] string buildingId;
        [SerializeField] string townName;
        [SerializeField] Vector3 roadApproachPosition;
        [SerializeField] H51CitizenVehicle reservedBy;

        public string ParkingId => parkingId;
        public string BuildingId => buildingId;
        public string TownName => townName;
        public Vector3 RoadApproachPosition => roadApproachPosition;
        public bool IsOccupied => reservedBy != null;
        public H51CitizenVehicle ReservedBy => reservedBy;

        public void Configure(string id, string ownerBuildingId, string town, Vector3 roadApproach)
        {
            parkingId = id;
            buildingId = ownerBuildingId;
            townName = town;
            roadApproachPosition = roadApproach;
        }

        public bool IsAvailableFor(H51CitizenVehicle vehicle)
        {
            return reservedBy == null || reservedBy == vehicle;
        }

        public bool TryReserve(H51CitizenVehicle vehicle)
        {
            if (!vehicle || !IsAvailableFor(vehicle)) return false;
            reservedBy = vehicle;
            return true;
        }

        public void Release(H51CitizenVehicle vehicle)
        {
            if (reservedBy == vehicle) reservedBy = null;
        }

        public void ForceReserve(H51CitizenVehicle vehicle)
        {
            reservedBy = vehicle;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = IsOccupied ? new Color(1f,.45f,.15f,.85f) : new Color(.2f,.85f,1f,.85f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * .15f, new Vector3(2.1f,.3f,4.8f));
            Gizmos.DrawLine(transform.position + Vector3.up * .25f, roadApproachPosition + Vector3.up * .25f);
        }
#endif
    }
}
