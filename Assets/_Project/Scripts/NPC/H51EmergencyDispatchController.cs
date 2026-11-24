using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Hanger51.NPCs
{
    [DisallowMultipleComponent]
    public sealed class H51EmergencyDispatchController : MonoBehaviour
    {
        sealed class PendingRequest
        {
            public H51CitizenEmergencyState caller;
            public H51EmergencyServiceType type;
            public Vector3 position;
            public string reason;
        }

        [SerializeField] H51TrafficNetwork trafficNetwork;
        [SerializeField] Transform medicalDestination;
        [SerializeField] bool simulateAmbientIncidents = true;
        [SerializeField, Min(30f)] float minimumAmbientIncidentSeconds = 150f;
        [SerializeField, Min(30f)] float maximumAmbientIncidentSeconds = 300f;

        readonly List<PendingRequest> pending = new List<PendingRequest>();
        H51EmergencyResponseVehicle[] vehicles = new H51EmergencyResponseVehicle[0];
        float nextDispatchRefresh;
        float nextAmbientIncident;

        public int PendingCallCount => pending.Count;

        public void Configure(
            H51TrafficNetwork network,
            Transform configuredMedicalDestination,
            bool enableAmbientIncidents)
        {
            trafficNetwork = network;
            medicalDestination = configuredMedicalDestination;
            simulateAmbientIncidents = enableAmbientIncidents;
            RefreshVehicles();
            ScheduleNextAmbientIncident(75f, 140f);
        }

        void Start()
        {
            if (!trafficNetwork)
                trafficNetwork = FindFirstObjectByType<H51TrafficNetwork>();
            ResolveMedicalDestination();
            RefreshVehicles();
            ScheduleNextAmbientIncident(75f, 140f);
        }

        void Update()
        {
            if (Time.unscaledTime >= nextDispatchRefresh)
            {
                nextDispatchRefresh = Time.unscaledTime + .4f;
                DispatchPendingCalls();
            }

            if (simulateAmbientIncidents &&
                Time.unscaledTime >= nextAmbientIncident)
            {
                TryCreateAmbientIncident();
                ScheduleNextAmbientIncident(
                    minimumAmbientIncidentSeconds,
                    maximumAmbientIncidentSeconds);
            }
        }

        public void RequestEmergency(
            H51CitizenEmergencyState caller,
            bool ambulance,
            bool police,
            bool fire,
            string reason)
        {
            if (!caller || !caller.IsActive) return;

            Vector3 location = caller.transform.position;
            if (ambulance) Queue(caller, H51EmergencyServiceType.Ambulance, location, reason);
            if (police) Queue(caller, H51EmergencyServiceType.Police, location, reason);
            if (fire) Queue(caller, H51EmergencyServiceType.Fire, location, reason);

            DispatchPendingCalls();
        }

        void Queue(
            H51CitizenEmergencyState caller,
            H51EmergencyServiceType type,
            Vector3 location,
            string reason)
        {
            if (pending.Any(p => p.caller == caller && p.type == type))
                return;

            pending.Add(new PendingRequest
            {
                caller = caller,
                type = type,
                position = location,
                reason = reason
            });
        }

        void DispatchPendingCalls()
        {
            if (!trafficNetwork)
                trafficNetwork = FindFirstObjectByType<H51TrafficNetwork>();
            if (!trafficNetwork || pending.Count == 0) return;

            if (vehicles == null || vehicles.Length == 0)
                RefreshVehicles();

            for (int i = pending.Count - 1; i >= 0; i--)
            {
                PendingRequest request = pending[i];
                if (request == null || !request.caller || !request.caller.IsActive)
                {
                    pending.RemoveAt(i);
                    continue;
                }

                H51EmergencyResponseVehicle vehicle =
                    FindNearestAvailableVehicle(request.type, request.position);
                if (!vehicle) continue;

                H51CitizenEmergencyState caller = request.caller;
                H51EmergencyServiceType type = request.type;

                bool dispatched = vehicle.DispatchTo(
                    request.position,
                    arrived => HandleArrival(arrived, caller, type));

                if (dispatched)
                    pending.RemoveAt(i);
            }
        }

        H51EmergencyResponseVehicle FindNearestAvailableVehicle(
            H51EmergencyServiceType type,
            Vector3 location)
        {
            H51EmergencyResponseVehicle best = null;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < vehicles.Length; i++)
            {
                H51EmergencyResponseVehicle vehicle = vehicles[i];
                if (!vehicle ||
                    !vehicle.IsAvailable ||
                    vehicle.ServiceType != type)
                    continue;

                float distance = Vector3.SqrMagnitude(vehicle.transform.position - location);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = vehicle;
            }

            return best;
        }

        void HandleArrival(
            H51EmergencyResponseVehicle vehicle,
            H51CitizenEmergencyState caller,
            H51EmergencyServiceType type)
        {
            if (!vehicle)
                return;

            if (!caller || !caller.IsActive)
            {
                vehicle.ReturnHome();
                return;
            }

            caller.OnServiceArrived(type);

            if (type != H51EmergencyServiceType.Ambulance)
            {
                vehicle.ReturnHome();
                return;
            }

            if (!caller.NeedsTransport)
            {
                caller.CompleteOnSceneMedicalTreatment();
                vehicle.ReturnHome();
                return;
            }

            ResolveMedicalDestination();
            if (!medicalDestination)
            {
                caller.CompleteOnSceneMedicalTreatment();
                vehicle.ReturnHome();
                return;
            }

            caller.BeginAmbulanceTransport(vehicle.transform);
            bool transporting = vehicle.DispatchTo(
                medicalDestination.position,
                arrived =>
                {
                    if (caller)
                        caller.CompleteAmbulanceTransport(medicalDestination.position);
                    arrived.ReturnHome();
                },
                true);

            if (!transporting)
            {
                caller.CompleteOnSceneMedicalTreatment();
                vehicle.ReturnHome();
            }
        }

        void ResolveMedicalDestination()
        {
            if (medicalDestination) return;

            H51CitizenAnchor[] anchors = FindObjectsByType<H51CitizenAnchor>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            H51CitizenAnchor medical = anchors.FirstOrDefault(
                a => a && a.Kind == H51CitizenAnchorKind.Medical);
            if (medical) medicalDestination = medical.transform;
        }

        void RefreshVehicles()
        {
            vehicles = FindObjectsByType<H51EmergencyResponseVehicle>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        }

        void TryCreateAmbientIncident()
        {
            if (pending.Count > 0) return;

            H51CitizenEmergencyState[] citizens = FindObjectsByType<H51CitizenEmergencyState>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            List<H51CitizenEmergencyState> available = citizens
                .Where(c => c && !c.IsActive && c.gameObject.activeInHierarchy)
                .ToList();

            if (available.Count == 0) return;

            H51CitizenEmergencyState caller =
                available[Random.Range(0, available.Count)];
            int roll = Random.Range(0, 100);

            if (roll < 58)
            {
                caller.TriggerMedicalEmergency(
                    Random.Range(28f, 72f),
                    "a sudden medical problem",
                    false);
            }
            else if (roll < 84)
            {
                caller.TriggerSafetyEmergency(
                    "a situation where police assistance is needed");
            }
            else
            {
                caller.TriggerFireEmergency(
                    "smoke or a small fire nearby");
            }
        }

        void ScheduleNextAmbientIncident(float minimum, float maximum)
        {
            minimum = Mathf.Max(30f, minimum);
            maximum = Mathf.Max(minimum, maximum);
            nextAmbientIncident =
                Time.unscaledTime + Random.Range(minimum, maximum);
        }

        void OnValidate()
        {
            minimumAmbientIncidentSeconds =
                Mathf.Max(30f, minimumAmbientIncidentSeconds);
            maximumAmbientIncidentSeconds =
                Mathf.Max(minimumAmbientIncidentSeconds, maximumAmbientIncidentSeconds);
        }
    }
}
