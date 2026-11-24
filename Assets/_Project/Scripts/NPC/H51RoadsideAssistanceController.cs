using System.Collections.Generic;
using UnityEngine;

namespace Hanger51.NPCs
{
    public sealed class H51RoadsideAssistanceController : MonoBehaviour
    {
        enum ServiceState
        {
            Idle,
            Outbound,
            Servicing,
            Returning
        }

        [SerializeField] H51TrafficNetwork trafficNetwork;
        [SerializeField] Transform serviceTruck;
        [SerializeField] Vector3 basePosition;
        [SerializeField] Quaternion baseRotation = Quaternion.identity;
        [SerializeField, Min(1f)] float cruiseSpeed = 11f;
        [SerializeField, Min(1f)] float turnSpeed = 4.5f;
        [SerializeField, Min(1f)] float serviceSeconds = 5f;
        [SerializeField] float groundRideOffset = .05f;

        readonly List<Vector3> route = new List<Vector3>();
        ServiceState state;
        H51CitizenVehicle target;
        int routeIndex;
        float nextScan;
        float serviceUntil;

        public bool IsBusy => state != ServiceState.Idle;
        public H51CitizenVehicle CurrentTarget => target;

        public void Configure(
            H51TrafficNetwork network,
            Transform truck,
            Vector3 homePosition,
            Quaternion homeRotation)
        {
            trafficNetwork = network;
            serviceTruck = truck;
            basePosition = homePosition;
            baseRotation = homeRotation;

            if (serviceTruck)
            {
                serviceTruck.SetPositionAndRotation(basePosition, baseRotation);
                AlignTruckToGround(true);
            }
        }

        void Start()
        {
            if (!trafficNetwork)
                trafficNetwork = FindFirstObjectByType<H51TrafficNetwork>();

            if (serviceTruck && basePosition == Vector3.zero)
            {
                basePosition = serviceTruck.position;
                baseRotation = serviceTruck.rotation;
            }
        }

        void Update()
        {
            if (!serviceTruck) return;

            switch (state)
            {
                case ServiceState.Idle:
                    UpdateIdle();
                    break;
                case ServiceState.Outbound:
                    UpdateOutbound();
                    break;
                case ServiceState.Servicing:
                    UpdateServicing();
                    break;
                case ServiceState.Returning:
                    UpdateReturning();
                    break;
            }
        }

        void UpdateIdle()
        {
            if (Time.time < nextScan) return;
            nextScan = Time.time + 2.5f;

            H51CitizenVehicle found = FindBrokenVehicle();
            if (!found) return;

            target = found;
            BuildRoute(serviceTruck.position, target.transform.position);
            state = ServiceState.Outbound;
        }

        void UpdateOutbound()
        {
            if (!target || !target.IsBrokenDown)
            {
                BeginReturn();
                return;
            }

            if (MoveAlongRoute())
                return;

            Vector3 targetPoint = target.transform.position - target.transform.right * 3.25f;
            if (Planar(serviceTruck.position, targetPoint) > 1.1f)
            {
                MoveToward(targetPoint, cruiseSpeed * .55f);
                return;
            }

            state = ServiceState.Servicing;
            serviceUntil = Time.time + serviceSeconds;
        }

        void UpdateServicing()
        {
            if (!target)
            {
                BeginReturn();
                return;
            }

            Vector3 look = target.transform.position - serviceTruck.position;
            look.y = 0f;
            if (look.sqrMagnitude > .02f)
            {
                Quaternion wanted = Quaternion.LookRotation(look.normalized, Vector3.up);
                serviceTruck.rotation = Quaternion.Slerp(
                    serviceTruck.rotation,
                    wanted,
                    1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
            }

            if (Time.time < serviceUntil) return;

            H51CitizenIdentity owner = FindOwner(target.OwnerCitizenId);
            target.RepairFully();

            if (owner)
            {
                owner.Remember(
                    "roadside_assistance",
                    "Roadside assistance came out when my car broke down.",
                    target.VehicleId,
                    4f,
                    5f);
            }

            BeginReturn();
        }

        void UpdateReturning()
        {
            if (MoveAlongRoute())
                return;

            if (Planar(serviceTruck.position, basePosition) > .35f)
            {
                MoveToward(basePosition, cruiseSpeed * .65f);
                return;
            }

            serviceTruck.SetPositionAndRotation(basePosition, baseRotation);
            AlignTruckToGround(true);
            target = null;
            route.Clear();
            routeIndex = 0;
            state = ServiceState.Idle;
            nextScan = Time.time + 2f;
        }

        void BeginReturn()
        {
            target = null;
            BuildRoute(serviceTruck.position, basePosition);
            state = ServiceState.Returning;
        }

        H51CitizenVehicle FindBrokenVehicle()
        {
            H51CitizenVehicle[] vehicles = FindObjectsByType<H51CitizenVehicle>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            H51CitizenVehicle best = null;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < vehicles.Length; i++)
            {
                H51CitizenVehicle vehicle = vehicles[i];
                if (!vehicle || !vehicle.IsBrokenDown) continue;

                float distance = Planar(serviceTruck.position, vehicle.transform.position);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = vehicle;
            }

            return best;
        }

        H51CitizenIdentity FindOwner(string citizenId)
        {
            if (string.IsNullOrWhiteSpace(citizenId)) return null;

            H51CitizenRegistry registry = H51CitizenRegistry.Instance;
            if (!registry) return null;

            IReadOnlyList<H51CitizenIdentity> citizens = registry.Citizens;
            for (int i = 0; i < citizens.Count; i++)
            {
                H51CitizenIdentity citizen = citizens[i];
                if (citizen && citizen.CitizenId == citizenId)
                    return citizen;
            }

            return null;
        }

        void BuildRoute(Vector3 from, Vector3 to)
        {
            route.Clear();
            routeIndex = 0;

            if (trafficNetwork)
                trafficNetwork.TryBuildPath(from, to, route);

            OffsetRouteToRightLane();

            if (route.Count == 0 || Planar(route[route.Count - 1], to) > 8f)
                route.Add(to);
        }

        bool MoveAlongRoute()
        {
            while (routeIndex < route.Count &&
                   Planar(serviceTruck.position, route[routeIndex]) < 1.15f)
                routeIndex++;

            if (routeIndex >= route.Count)
                return false;

            MoveToward(route[routeIndex], cruiseSpeed);
            return true;
        }

        void MoveToward(Vector3 targetPoint, float speed)
        {
            Vector3 flat = targetPoint - serviceTruck.position;
            flat.y = 0f;

            if (flat.sqrMagnitude > .01f)
            {
                Quaternion wanted = Quaternion.LookRotation(flat.normalized, Vector3.up);
                serviceTruck.rotation = Quaternion.Slerp(
                    serviceTruck.rotation,
                    wanted,
                    1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
            }

            serviceTruck.position = Vector3.MoveTowards(
                serviceTruck.position,
                targetPoint,
                speed * Time.deltaTime);

            AlignTruckToGround(false);
        }

        void OffsetRouteToRightLane()
        {
            if (route.Count < 2) return;

            const float laneOffset = 1.15f;
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

        void AlignTruckToGround(bool immediate)
        {
            Vector3 origin = serviceTruck.position + Vector3.up * 4f;
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                12f,
                ~0,
                QueryTriggerInteraction.Ignore);

            bool found = false;
            float bestY = float.NegativeInfinity;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (!hit.collider) continue;
                if (hit.collider.transform == serviceTruck ||
                    hit.collider.transform.IsChildOf(serviceTruck))
                    continue;
                if (hit.normal.y < .5f) continue;

                string n = hit.collider.name.ToLowerInvariant();
                bool drivable =
                    hit.collider is TerrainCollider ||
                    n.Contains("road") ||
                    n.Contains("parking") ||
                    n.Contains("driveway") ||
                    n.Contains("ground") ||
                    n.Contains("pad");

                if (!drivable || hit.point.y <= bestY) continue;
                bestY = hit.point.y;
                found = true;
            }

            if (!found) return;

            Vector3 p = serviceTruck.position;
            float y = bestY + groundRideOffset;
            p.y = immediate ? y : Mathf.MoveTowards(p.y, y, 7f * Time.deltaTime);
            serviceTruck.position = p;
        }

        static float Planar(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return Mathf.Sqrt(x*x + z*z);
        }
    }
}
