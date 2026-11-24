using System.Collections.Generic;
using UnityEngine;

namespace Hanger51.NPCs
{
    public enum H51CitizenVehicleState
    {
        Parked,
        Driving,
        Arrived,
        BrokenDown
    }

    [DisallowMultipleComponent]
    public sealed class H51CitizenVehicle : MonoBehaviour
    {
        [SerializeField] string vehicleId;
        [SerializeField] string ownerCitizenId;
        [SerializeField] string displayName = "Car";
        [SerializeField] H51TrafficNetwork trafficNetwork;
        [SerializeField, Min(1f)] float cruiseSpeed = 10f;
        [SerializeField, Min(1f)] float turnSpeed = 4f;
        [SerializeField, Range(0f,100f)] float condition = 100f;
        [SerializeField] H51CitizenVehicleState state = H51CitizenVehicleState.Parked;

        readonly List<Vector3> route = new List<Vector3>();
        int routeIndex;
        H51CitizenIdentity currentDriver;
        float nextIncidentCheck;

        public string VehicleId => vehicleId;
        public string OwnerCitizenId => ownerCitizenId;
        public string DisplayName => displayName;
        public H51CitizenVehicleState State => state;
        public H51CitizenIdentity CurrentDriver => currentDriver;
        public bool TripComplete => state == H51CitizenVehicleState.Arrived;
        public bool IsBrokenDown => state == H51CitizenVehicleState.BrokenDown;
        public float Condition => condition;

        public void Configure(
            string id,
            string ownerId,
            string label,
            H51TrafficNetwork network,
            float speed)
        {
            vehicleId = id;
            ownerCitizenId = ownerId;
            displayName = label;
            trafficNetwork = network;
            cruiseSpeed = Mathf.Max(1f, speed);
            state = H51CitizenVehicleState.Parked;
            condition = Mathf.Clamp(condition <= 0f ? 100f : condition, 1f, 100f);
        }

        public bool IsOwnedBy(H51CitizenIdentity citizen)
        {
            return citizen && !string.IsNullOrEmpty(ownerCitizenId) && citizen.CitizenId == ownerCitizenId;
        }

        public bool BeginTrip(H51CitizenIdentity driver, Vector3 destination)
        {
            if (!driver || !IsOwnedBy(driver) || !trafficNetwork || IsBrokenDown) return false;
            if (!trafficNetwork.TryBuildPath(transform.position, destination, route) || route.Count == 0)
                return false;

            OffsetRouteToRightLane();

            currentDriver = driver;
            routeIndex = 0;
            state = H51CitizenVehicleState.Driving;
            nextIncidentCheck = Time.time + 12f;
            return true;
        }

        public void ReleaseDriver()
        {
            currentDriver = null;
            route.Clear();
            routeIndex = 0;
            if (state == H51CitizenVehicleState.Arrived)
                state = H51CitizenVehicleState.Parked;
        }

        public void ApplyDamage(float amount, string reason)
        {
            if (amount <= 0f) return;
            condition = Mathf.Clamp(condition - amount, 0f, 100f);
            if (condition > 18f) return;

            state = H51CitizenVehicleState.BrokenDown;
            route.Clear();
            if (currentDriver)
            {
                currentDriver.Remember(
                    "vehicle_incident",
                    string.IsNullOrWhiteSpace(reason)
                        ? "My car was damaged badly enough to stop."
                        : reason,
                    vehicleId,
                    15f,
                    0f);
            }
        }

        public void RepairFully()
        {
            condition = 100f;
            if (state == H51CitizenVehicleState.BrokenDown)
                state = H51CitizenVehicleState.Parked;
        }

        void Update()
        {
            if (state != H51CitizenVehicleState.Driving || route.Count == 0) return;

            if (routeIndex >= route.Count)
            {
                FinishArrival();
                return;
            }

            Vector3 target = route[routeIndex];
            Vector3 flat = target - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 2.25f)
            {
                routeIndex++;
                if (routeIndex >= route.Count)
                {
                    FinishArrival();
                    return;
                }
                target = route[routeIndex];
                flat = target - transform.position;
                flat.y = 0f;
            }

            if (flat.sqrMagnitude > .01f)
            {
                Quaternion wanted = Quaternion.LookRotation(flat.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    wanted,
                    1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
            }

            float actualSpeed = TrafficBlockedAhead() ? 0f : cruiseSpeed;
            Vector3 next = Vector3.MoveTowards(
                transform.position,
                target,
                actualSpeed * Time.deltaTime);
            transform.position = next;

            if (Time.time >= nextIncidentCheck)
            {
                nextIncidentCheck = Time.time + 12f;
                float wearRisk = Mathf.InverseLerp(100f, 20f, condition);
                float chance = Mathf.Lerp(.0005f, .015f, wearRisk);
                if (Random.value < chance)
                {
                    condition = Mathf.Max(0f, condition - Random.Range(12f, 30f));
                    if (condition <= 18f)
                    {
                        state = H51CitizenVehicleState.BrokenDown;
                        route.Clear();
                        if (currentDriver)
                            currentDriver.Remember(
                                "vehicle_breakdown",
                                "My car broke down while I was traveling.",
                                vehicleId,
                                12f,
                                0f);
                    }
                }
            }
        }

        void FinishArrival()
        {
            float ownerJitter = string.IsNullOrEmpty(ownerCitizenId)
                ? 0f
                : ((ownerCitizenId.GetHashCode() & 0x7fffffff) % 7 - 3) * .35f;
            transform.position += transform.right * 3.1f + transform.forward * ownerJitter;
            state = H51CitizenVehicleState.Arrived;
        }

        void OffsetRouteToRightLane()
        {
            if (route.Count < 2) return;
            float laneOffset = 1.35f;

            for (int i = 0; i < route.Count; i++)
            {
                Vector3 tangent;
                if (i == 0) tangent = route[1] - route[0];
                else if (i == route.Count - 1) tangent = route[i] - route[i - 1];
                else tangent = route[i + 1] - route[i - 1];

                tangent.y = 0f;
                if (tangent.sqrMagnitude < .01f) continue;
                tangent.Normalize();
                Vector3 right = Vector3.Cross(Vector3.up, tangent);
                route[i] += right * laneOffset;
            }
        }

        bool TrafficBlockedAhead()
        {
            Vector3 origin = transform.position + transform.forward * 2.35f + Vector3.up * .75f;
            if (!Physics.Raycast(origin, transform.forward, out RaycastHit hit, 7f, ~0, QueryTriggerInteraction.Ignore))
                return false;

            H51CitizenVehicle other = hit.collider ? hit.collider.GetComponentInParent<H51CitizenVehicle>() : null;
            return other && other != this;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (collision == null || collision.relativeVelocity.magnitude < 5f) return;
            float damage = Mathf.Clamp(collision.relativeVelocity.magnitude * 1.5f, 4f, 35f);
            ApplyDamage(damage, "My car was involved in a collision.");
        }
    }
}
