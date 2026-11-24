using UnityEngine;
using UnityEngine.AI;
using Hanger51.World;

namespace Hanger51.NPCs
{
    public enum H51CitizenRoutineState
    {
        Home,
        Commuting,
        Work,
        Shopping,
        Leisure
    }

    [RequireComponent(typeof(H51CitizenIdentity))]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class H51CitizenBrain : MonoBehaviour
    {
        [SerializeField] H51CitizenRoutineState state = H51CitizenRoutineState.Home;
        [SerializeField] float thinkInterval = .7f;
        [SerializeField] float localWanderRadius = 8f;

        H51CitizenIdentity identity;
        H51CitizenRegistry registry;
        H51CitizenAnchor home;
        H51CitizenAnchor work;
        H51CitizenAnchor leisure;
        H51CitizenAnchor shop;
        H51CitizenAnchor medical;
        NavMeshAgent agent;
        Animator animator;
        float nextThink;
        float pauseUntil;
        System.Random rng;
        bool hasSpeed;
        bool hasWave;
        bool hasInteract;
        string currentActivityDescription = "I'm just going about my day.";
        bool conversationPaused;

        H51CitizenVehicle ownedVehicle;
        Transform parentBeforeVehicle;
        Renderer[] citizenRenderers;
        bool[] rendererStates;
        Collider[] citizenColliders;
        bool[] colliderStates;
        bool ridingInVehicle;
        H51CitizenRoutineState vehicleDestinationState;
        string pendingVehicleDestinationAnchorId;
        string lastVehicleDestinationAnchorId;
        float suppressVehicleUntil;

        H51CitizenActivitySpot activeActivitySpot;
        float activeActivityUntil;
        float nextActivityGesture;

        public H51CitizenRoutineState State => state;
        public string CurrentActivityDescription => currentActivityDescription;

        void Awake()
        {
            identity = GetComponent<H51CitizenIdentity>();
            agent = GetComponent<NavMeshAgent>();
            if (!GetComponent<H51CitizenGrounding>())
                gameObject.AddComponent<H51CitizenGrounding>();
            animator = GetComponentInChildren<Animator>(true);
            rng = new System.Random(identity ? identity.AppearanceSeed : GetInstanceID());
            citizenRenderers = GetComponentsInChildren<Renderer>(true);
            rendererStates = new bool[citizenRenderers.Length];
            citizenColliders = GetComponentsInChildren<Collider>(true);
            colliderStates = new bool[citizenColliders.Length];
            CacheAnimatorParameters();
        }

        void Start()
        {
            registry = H51CitizenRegistry.Instance;
            if (!registry) registry = FindFirstObjectByType<H51CitizenRegistry>();
            ResolveAnchors();
            ResolveOwnedVehicle();
            SnapToNavMesh();
            nextThink = Time.time + (float)rng.NextDouble() * thinkInterval;
        }

        void Update()
        {
            if (hasSpeed && animator)
                animator.SetFloat(
                    "Speed",
                    conversationPaused || ridingInVehicle
                        ? 0f
                        : (agent && agent.enabled && agent.isOnNavMesh ? agent.velocity.magnitude : 0f),
                    .15f,
                    Time.deltaTime);

            if (conversationPaused) return;
            if (ridingInVehicle)
            {
                UpdateVehicleRide();
                return;
            }

            if (activeActivitySpot)
            {
                UpdateActiveActivity();
                if (activeActivitySpot) return;
            }

            if (Time.time < nextThink) return;
            nextThink = Time.time + thinkInterval;
            Think();
        }

        public void SetConversationPaused(bool paused, Vector3 speakerPosition)
        {
            conversationPaused = paused;
            if (agent && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = paused;
                if (paused) agent.ResetPath();
            }

            if (paused)
            {
                Vector3 look = speakerPosition - transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > .01f)
                    transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
                currentActivityDescription = "I'm talking with you right now.";
            }
            else
            {
                nextThink = Time.time + .2f;
            }
        }

        void ResolveAnchors()
        {
            if (!registry || !identity) return;
            home = registry.FindAnchor(identity.HomeAnchorId);
            work = registry.FindAnchor(identity.WorkAnchorId);
            leisure = registry.PickAnchor(identity.TownName, H51CitizenAnchorKind.Entertainment, identity.AppearanceSeed + 23)
                      ?? registry.PickAnchor(identity.TownName, H51CitizenAnchorKind.Park, identity.AppearanceSeed + 31)
                      ?? registry.PickAnchor(identity.TownName, H51CitizenAnchorKind.Leisure, identity.AppearanceSeed + 41);
            shop = registry.PickAnchor(identity.TownName, H51CitizenAnchorKind.Shop, identity.AppearanceSeed + 53);
            medical = registry.PickAnchor(identity.TownName, H51CitizenAnchorKind.Medical, identity.AppearanceSeed + 67);
        }

        H51CitizenAnchor PickCurrentLeisureDestination(float hour)
        {
            if (!registry || !identity) return leisure;

            int bucket = DestinationTimeBucket(hour);
            int seed = identity.AppearanceSeed + bucket * 97;
            H51CitizenAnchorKind primary =
                ((identity.AppearanceSeed + bucket) % 3 == 0)
                    ? H51CitizenAnchorKind.Park
                    : H51CitizenAnchorKind.Entertainment;

            return registry.PickAnchor(identity.TownName, primary, seed)
                   ?? registry.PickAnchor(identity.TownName, H51CitizenAnchorKind.Leisure, seed + 11)
                   ?? registry.PickAnchor(identity.TownName, H51CitizenAnchorKind.Park, seed + 23)
                   ?? registry.PickAnchor(identity.TownName, H51CitizenAnchorKind.Entertainment, seed + 37)
                   ?? leisure;
        }

        H51CitizenAnchor PickCurrentShopDestination(float hour)
        {
            if (!registry || !identity) return shop;
            int bucket = DestinationTimeBucket(hour);
            return registry.PickAnchor(
                       identity.TownName,
                       H51CitizenAnchorKind.Shop,
                       identity.AppearanceSeed + bucket * 131)
                   ?? shop;
        }

        bool TryGetMedicalAppointment(float hour, out H51CitizenAnchor appointment)
        {
            appointment = null;
            if (!registry || !identity) return false;

            H51WorldClock clock = H51WorldClock.Instance;
            int day = clock ? clock.DayIndex : Mathf.FloorToInt(Time.time / 480f);
            int schedule = (identity.AppearanceSeed & 0x7fffffff) + day * 17;

            // Roughly one scheduled visit every nine game-days per citizen.
            if (schedule % 9 != 0) return false;

            float appointmentHour = 9.25f + (schedule % 8) * .75f;
            if (hour < appointmentHour || hour >= appointmentHour + .75f)
                return false;

            appointment = registry.PickAnchor(
                              identity.TownName,
                              H51CitizenAnchorKind.Medical,
                              identity.AppearanceSeed + day * 211)
                          ?? medical;
            return appointment;
        }

        int DestinationTimeBucket(float hour)
        {
            H51WorldClock clock = H51WorldClock.Instance;
            int day = clock ? clock.DayIndex : 0;
            return day * 64 + Mathf.FloorToInt(hour * 2f);
        }

        bool IsEssentialShiftWorker()
        {
            if (!identity || string.IsNullOrWhiteSpace(identity.Occupation))
                return false;

            string job = identity.Occupation.ToLowerInvariant();
            return
                job.Contains("power plant") ||
                job.Contains("utility") ||
                job.Contains("police") ||
                job.Contains("firefighter") ||
                job.Contains("paramedic") ||
                job.Contains("ambulance") ||
                job.Contains("emergency");
        }

        bool ShouldBeAtWork(float hour, H51WorldClock clock)
        {
            if (!work || !identity) return false;

            if (IsEssentialShiftWorker())
            {
                int shift = Mathf.Abs(identity.AppearanceSeed) % 3;
                float shiftStart = shift * 8f;
                // Begin routing toward work shortly before the eight-hour shift starts.
                return IsWrappedHourWindow(hour,shiftStart - .35f,shiftStart + 8.05f);
            }

            if (clock && clock.IsWeekend) return false;

            return
                (hour >= 7.25f && hour < 12f) ||
                (hour >= 13.0f && hour < 17.25f);
        }

        static bool IsWrappedHourWindow(float hour, float start, float end)
        {
            hour = Mathf.Repeat(hour,24f);
            start = Mathf.Repeat(start,24f);
            end = Mathf.Repeat(end,24f);

            if (start < end)
                return hour >= start && hour < end;

            return hour >= start || hour < end;
        }

        void SnapToNavMesh()
        {
            if (!agent || agent.isOnNavMesh) return;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 18f, NavMesh.AllAreas))
                agent.Warp(hit.position);
        }

        void Think()
        {
            if (ridingInVehicle)
            {
                UpdateVehicleRide();
                return;
            }

            if (!agent || !agent.enabled) return;
            if (!agent.isOnNavMesh) { SnapToNavMesh(); if (!agent.isOnNavMesh) return; }
            if (!registry) { registry = H51CitizenRegistry.Instance; ResolveAnchors(); }

            H51WorldClock worldClock = H51WorldClock.Instance;
            float hour = worldClock
                ? worldClock.Hour
                : Mathf.Repeat(Time.time / 60f + 7f,24f);
            H51CitizenAnchor desired;
            H51CitizenRoutineState desiredState;

            H51CitizenAnchor currentLeisure = PickCurrentLeisureDestination(hour);
            H51CitizenAnchor currentShop = PickCurrentShopDestination(hour);
            bool essentialShiftWorker = IsEssentialShiftWorker();
            bool scheduledWork = ShouldBeAtWork(hour,worldClock);
            bool medicalAppointment =
                !scheduledWork &&
                TryGetMedicalAppointment(hour,out H51CitizenAnchor appointment);
            bool weekend = worldClock && worldClock.IsWeekend;

            if (scheduledWork)
            {
                desired = work ?? home;
                desiredState = work
                    ? H51CitizenRoutineState.Work
                    : H51CitizenRoutineState.Home;
            }
            else if (medicalAppointment)
            {
                desired = appointment;
                desiredState = H51CitizenRoutineState.Leisure;
            }
            else if (hour < 7.0f || hour >= 22.5f)
            {
                desired = home;
                desiredState = H51CitizenRoutineState.Home;
            }
            else if (weekend && !essentialShiftWorker)
            {
                if (hour < 10f)
                {
                    desired = home;
                    desiredState = H51CitizenRoutineState.Home;
                }
                else if (hour < 13f)
                {
                    desired = currentShop ?? currentLeisure ?? home;
                    desiredState = currentShop
                        ? H51CitizenRoutineState.Shopping
                        : H51CitizenRoutineState.Leisure;
                }
                else if (hour < 19.5f)
                {
                    desired = currentLeisure ?? currentShop ?? home;
                    desiredState = currentLeisure
                        ? H51CitizenRoutineState.Leisure
                        : currentShop
                            ? H51CitizenRoutineState.Shopping
                            : H51CitizenRoutineState.Home;
                }
                else
                {
                    int weekendEvening = DestinationTimeBucket(hour);
                    bool social =
                        identity &&
                        identity.Sociability > .32f &&
                        ((identity.AppearanceSeed + weekendEvening * 5) & 3) != 0;
                    desired = social ? (currentLeisure ?? home) : home;
                    desiredState = social && currentLeisure
                        ? H51CitizenRoutineState.Leisure
                        : H51CitizenRoutineState.Home;
                }
            }
            else if (hour >= 12f && hour < 13.25f && !essentialShiftWorker)
            {
                desired = currentLeisure ?? currentShop ?? home;
                desiredState = H51CitizenRoutineState.Leisure;
            }
            else if (hour < 19.5f)
            {
                bool errands =
                    hour >= 17.25f ||
                    (!essentialShiftWorker && hour >= 7f && hour < 7.25f);
                desired = errands
                    ? (currentShop ?? currentLeisure ?? home)
                    : (currentLeisure ?? currentShop ?? home);
                desiredState = errands && currentShop
                    ? H51CitizenRoutineState.Shopping
                    : currentLeisure
                        ? H51CitizenRoutineState.Leisure
                        : H51CitizenRoutineState.Home;
            }
            else
            {
                int eveningBucket = DestinationTimeBucket(hour);
                bool socialEvening = identity &&
                    identity.Sociability > .38f &&
                    ((identity.AppearanceSeed + eveningBucket * 7) & 3) != 0;
                desired = socialEvening ? (currentLeisure ?? home) : home;
                desiredState = socialEvening && currentLeisure
                    ? H51CitizenRoutineState.Leisure
                    : H51CitizenRoutineState.Home;
            }

            if (!desired)
            {
                currentActivityDescription = "I don't have anywhere I need to be right now.";
                return;
            }

            if (desiredState == H51CitizenRoutineState.Home)
                currentActivityDescription = $"I'm heading home in {identity.TownName}.";
            else if (desiredState == H51CitizenRoutineState.Work)
                currentActivityDescription = $"I'm working as a {identity.Occupation}.";
            else if (desiredState == H51CitizenRoutineState.Shopping)
                currentActivityDescription = "I'm out running errands.";
            else
                currentActivityDescription = "I'm taking some personal time around town.";

            float distance = Planar(transform.position, desired.transform.position);

            if (TryHandleVehicleCommute(desired, desiredState, distance))
                return;

            if (distance > Mathf.Max(2.2f, agent.stoppingDistance + .8f))
            {
                state = H51CitizenRoutineState.Commuting;
                currentActivityDescription = desiredState == H51CitizenRoutineState.Work
                    ? $"I'm on my way to work as a {identity.Occupation}."
                    : desiredState == H51CitizenRoutineState.Home
                        ? "I'm on my way home."
                        : "I'm on my way somewhere in town.";
                if (!agent.pathPending && (agent.destination - desired.transform.position).sqrMagnitude > 2.5f)
                    agent.SetDestination(desired.transform.position);
                return;
            }

            state = desiredState;
            if (Time.time < pauseUntil) return;

            H51CitizenActivitySpot[] spots = desired.GetComponentsInChildren<H51CitizenActivitySpot>(true);
            if (spots != null && spots.Length > 0)
            {
                H51CitizenActivitySpot spot = PickActivitySpot(spots);
                currentActivityDescription = ActivityDescriptionFor(desiredState, spot.ActivityLabel);

                if (NavMesh.SamplePosition(spot.transform.position, out NavMeshHit activityHit, 4f, NavMesh.AllAreas))
                {
                    activeActivitySpot = spot;
                    activeActivityUntil = 0f;
                    nextActivityGesture = 0f;
                    agent.isStopped = false;
                    agent.SetDestination(activityHit.position);
                }
                return;
            }

            pauseUntil = Time.time + Mathf.Lerp(3f, 10f, (float)rng.NextDouble());
            if (rng.NextDouble() < .38)
                SetLocalWander(desired.transform.position);

            if (animator && rng.NextDouble() < .18)
            {
                if (hasInteract && rng.NextDouble() < .75) animator.SetTrigger("Interact");
                else if (hasWave) animator.SetTrigger("Wave");
            }
        }

        void UpdateActiveActivity()
        {
            if (!activeActivitySpot || !agent || !agent.enabled || !agent.isOnNavMesh)
            {
                ClearActiveActivity();
                return;
            }

            float distance = Planar(transform.position, activeActivitySpot.transform.position);
            currentActivityDescription = ActivityDescriptionFor(state, activeActivitySpot.ActivityLabel);

            if (distance > 1.05f)
            {
                agent.isStopped = false;
                if (!agent.pathPending &&
                    (agent.destination - activeActivitySpot.transform.position).sqrMagnitude > 1.25f &&
                    NavMesh.SamplePosition(activeActivitySpot.transform.position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                }
                return;
            }

            agent.isStopped = true;
            if (agent.hasPath) agent.ResetPath();

            Vector3 facing = activeActivitySpot.transform.forward;
            facing.y = 0f;
            if (facing.sqrMagnitude > .01f)
            {
                Quaternion wanted = Quaternion.LookRotation(facing.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    wanted,
                    1f - Mathf.Exp(-8f * Time.deltaTime));
            }

            if (activeActivityUntil <= 0f)
            {
                activeActivityUntil = Time.time + Mathf.Lerp(
                    activeActivitySpot.MinimumDuration,
                    activeActivitySpot.MaximumDuration,
                    (float)rng.NextDouble());
                nextActivityGesture = Time.time + .15f;
            }

            if (Time.time >= nextActivityGesture)
            {
                nextActivityGesture = Time.time + Mathf.Lerp(3.2f, 5.8f, (float)rng.NextDouble());
                if (animator && hasInteract && !string.IsNullOrWhiteSpace(activeActivitySpot.AnimationTrigger))
                    animator.SetTrigger(activeActivitySpot.AnimationTrigger);
            }

            if (Time.time < activeActivityUntil) return;

            CompleteRoleWorkIfApplicable(activeActivitySpot);
            ClearActiveActivity();
            nextThink = Time.time + .15f;
        }

        H51CitizenActivitySpot PickActivitySpot(H51CitizenActivitySpot[] spots)
        {
            if (spots == null || spots.Length == 0) return null;

            bool mechanic =
                identity &&
                !string.IsNullOrWhiteSpace(identity.Occupation) &&
                (identity.Occupation.IndexOf("mechanic", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                 identity.Occupation.IndexOf("repair", System.StringComparison.OrdinalIgnoreCase) >= 0);

            if (!mechanic)
                return spots[rng.Next(spots.Length)];

            H51CitizenVehicle repairable = FindRepairableVehicle();
            if (repairable)
            {
                for (int i=0;i<spots.Length;i++)
                {
                    H51CitizenActivitySpot spot = spots[(i + rng.Next(spots.Length)) % spots.Length];
                    if (spot && spot.ActivityLabel.IndexOf("service bay", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return spot;
                }
            }

            H51CitizenActivitySpot[] nonService = System.Array.FindAll(
                spots,
                s => s && s.ActivityLabel.IndexOf("service bay", System.StringComparison.OrdinalIgnoreCase) < 0);

            return nonService.Length > 0
                ? nonService[rng.Next(nonService.Length)]
                : spots[rng.Next(spots.Length)];
        }

        H51CitizenVehicle FindRepairableVehicle()
        {
            if (!work) return null;

            H51CitizenVehicle[] vehicles = FindObjectsByType<H51CitizenVehicle>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            H51CitizenVehicle best = null;
            float bestDistance = 22f;
            for (int i=0;i<vehicles.Length;i++)
            {
                H51CitizenVehicle vehicle = vehicles[i];
                if (!vehicle || vehicle.Condition >= 85f) continue;

                float distance = Planar(vehicle.transform.position, work.transform.position);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = vehicle;
            }
            return best;
        }

        void CompleteRoleWorkIfApplicable(H51CitizenActivitySpot spot)
        {
            if (!spot || !identity) return;

            string occupation = identity.Occupation ?? string.Empty;
            bool powerPlantWorker =
                occupation.IndexOf("power plant", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                occupation.IndexOf("turbine", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                occupation.IndexOf("electrical technician", System.StringComparison.OrdinalIgnoreCase) >= 0;

            if (powerPlantWorker)
            {
                H51PowerPlantController plant = FindFirstObjectByType<H51PowerPlantController>();
                if (plant)
                {
                    bool fuelService =
                        spot.ActivityLabel.IndexOf("fuel service", System.StringComparison.OrdinalIgnoreCase) >= 0;

                    if (fuelService)
                    {
                        if (plant.PerformScheduledFuelService())
                            currentActivityDescription = "I'm completing the plant's scheduled fuel service.";
                    }
                    else
                    {
                        plant.PerformOperatorMaintenance();
                        currentActivityDescription = "I'm completing a plant equipment inspection and maintenance round.";
                    }
                }
                return;
            }

            bool mechanic =
                occupation.IndexOf("mechanic", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                occupation.IndexOf("repair", System.StringComparison.OrdinalIgnoreCase) >= 0;

            if (!mechanic ||
                spot.ActivityLabel.IndexOf("service bay", System.StringComparison.OrdinalIgnoreCase) < 0)
                return;

            H51CitizenVehicle vehicle = FindRepairableVehicle();
            if (!vehicle) return;

            vehicle.RepairFully();
            currentActivityDescription = "I'm finishing up a vehicle repair at the shop.";
        }

        void ClearActiveActivity()
        {
            activeActivitySpot = null;
            activeActivityUntil = 0f;
            nextActivityGesture = 0f;

            if (agent && agent.enabled && agent.isOnNavMesh)
                agent.isStopped = false;
        }

        void ResolveOwnedVehicle()
        {
            if (!identity) return;
            H51CitizenVehicle[] vehicles = FindObjectsByType<H51CitizenVehicle>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < vehicles.Length; i++)
            {
                if (vehicles[i] && vehicles[i].IsOwnedBy(identity))
                {
                    ownedVehicle = vehicles[i];
                    return;
                }
            }
        }

        bool TryHandleVehicleCommute(
            H51CitizenAnchor desired,
            H51CitizenRoutineState desiredState,
            float destinationDistance)
        {
            // Towns are intentionally spread out, but citizens should still use cars for ordinary
            // cross-town work/shopping trips rather than only for very long journeys.
            if (!desired || destinationDistance < 26f) return false;
            if (Time.time < suppressVehicleUntil) return false;

            if (!string.IsNullOrEmpty(lastVehicleDestinationAnchorId))
            {
                if (desired.AnchorId == lastVehicleDestinationAnchorId)
                    return false;

                lastVehicleDestinationAnchorId = string.Empty;
            }

            if (!ownedVehicle) ResolveOwnedVehicle();
            if (!ownedVehicle || ownedVehicle.IsBrokenDown) return false;

            H51ParkingSpot destinationParking = FindDestinationParking(desired);
            if (!destinationParking) return false;

            float toCar = Planar(transform.position, ownedVehicle.transform.position);
            if (toCar > 2.4f)
            {
                state = H51CitizenRoutineState.Commuting;
                currentActivityDescription = desiredState == H51CitizenRoutineState.Work
                    ? "I'm walking to my car so I can drive to work."
                    : desiredState == H51CitizenRoutineState.Home
                        ? "I'm walking to my car so I can drive home."
                        : "I'm walking to my car so I can head into town.";

                if (agent && agent.enabled && agent.isOnNavMesh &&
                    (!agent.pathPending || (agent.destination - ownedVehicle.transform.position).sqrMagnitude > 3f))
                {
                    if (NavMesh.SamplePosition(ownedVehicle.transform.position, out NavMeshHit carHit, 8f, NavMesh.AllAreas))
                        agent.SetDestination(carHit.position);
                }
                return true;
            }

            if (!ownedVehicle.BeginTrip(identity, destinationParking))
                return false;

            vehicleDestinationState = desiredState;
            pendingVehicleDestinationAnchorId = desired.AnchorId;
            EnterVehicle();
            return true;
        }

        H51ParkingSpot FindDestinationParking(H51CitizenAnchor desired)
        {
            if (!desired || !ownedVehicle) return null;

            H51ParkingSpot[] spots = FindObjectsByType<H51ParkingSpot>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            H51ParkingSpot best = null;
            float bestScore = float.PositiveInfinity;

            for (int i = 0; i < spots.Length; i++)
            {
                H51ParkingSpot spot = spots[i];
                if (!spot || !spot.IsAvailableFor(ownedVehicle)) continue;
                if (!string.IsNullOrEmpty(desired.TownName) &&
                    !string.IsNullOrEmpty(spot.TownName) &&
                    desired.TownName != spot.TownName)
                    continue;

                float distance = Planar(spot.transform.position, desired.transform.position);
                if (distance > 85f) continue;

                float score = distance;
                if (ownedVehicle.CurrentParkingSpot == spot) score += 1000f;
                if (score >= bestScore) continue;

                bestScore = score;
                best = spot;
            }

            return best;
        }

        void EnterVehicle()
        {
            if (!ownedVehicle || ridingInVehicle) return;

            ClearActiveActivity();
            parentBeforeVehicle = transform.parent;

            for (int i = 0; i < citizenRenderers.Length; i++)
            {
                if (!citizenRenderers[i]) continue;
                rendererStates[i] = citizenRenderers[i].enabled;
                citizenRenderers[i].enabled = false;
            }

            for (int i = 0; i < citizenColliders.Length; i++)
            {
                if (!citizenColliders[i]) continue;
                colliderStates[i] = citizenColliders[i].enabled;
                citizenColliders[i].enabled = false;
            }

            if (agent && agent.enabled)
            {
                if (agent.isOnNavMesh) agent.ResetPath();
                agent.enabled = false;
            }

            transform.SetParent(ownedVehicle.transform, true);
            transform.localPosition = new Vector3(-.35f, 1.05f, .05f);
            transform.localRotation = Quaternion.identity;
            ridingInVehicle = true;
            state = H51CitizenRoutineState.Commuting;
            currentActivityDescription = vehicleDestinationState == H51CitizenRoutineState.Work
                ? "I'm driving to work."
                : vehicleDestinationState == H51CitizenRoutineState.Home
                    ? "I'm driving home."
                    : "I'm driving into town.";
        }

        void UpdateVehicleRide()
        {
            if (!ridingInVehicle || !ownedVehicle)
            {
                ridingInVehicle = false;
                return;
            }

            if (!ownedVehicle.TripComplete && !ownedVehicle.IsBrokenDown)
            {
                currentActivityDescription = vehicleDestinationState == H51CitizenRoutineState.Work
                    ? "I'm driving to work."
                    : vehicleDestinationState == H51CitizenRoutineState.Home
                        ? "I'm driving home."
                        : "I'm driving into town.";
                return;
            }

            bool brokeDown = ownedVehicle.IsBrokenDown;

            if (!brokeDown && !string.IsNullOrEmpty(pendingVehicleDestinationAnchorId))
            {
                lastVehicleDestinationAnchorId = pendingVehicleDestinationAnchorId;
                suppressVehicleUntil = Time.time + 20f;
            }
            else if (brokeDown)
            {
                suppressVehicleUntil = Time.time + 8f;
            }

            pendingVehicleDestinationAnchorId = string.Empty;
            ExitVehicle();
            currentActivityDescription = brokeDown
                ? "My car broke down, so I'm dealing with that."
                : vehicleDestinationState == H51CitizenRoutineState.Work
                    ? "I just parked and I'm heading into work."
                    : vehicleDestinationState == H51CitizenRoutineState.Home
                        ? "I just parked and I'm heading home."
                        : "I just parked and I'm finishing my errand.";
        }

        void ExitVehicle()
        {
            if (!ridingInVehicle || !ownedVehicle) return;

            Vector3 exit = ownedVehicle.transform.position + ownedVehicle.transform.right * 1.8f;
            transform.SetParent(parentBeforeVehicle, true);
            transform.position = exit;
            transform.rotation = ownedVehicle.transform.rotation;

            for (int i = 0; i < citizenRenderers.Length; i++)
                if (citizenRenderers[i]) citizenRenderers[i].enabled = rendererStates[i];

            for (int i = 0; i < citizenColliders.Length; i++)
                if (citizenColliders[i]) citizenColliders[i].enabled = colliderStates[i];

            if (agent)
            {
                agent.enabled = true;
                if (NavMesh.SamplePosition(exit, out NavMeshHit hit, 14f, NavMesh.AllAreas))
                    agent.Warp(hit.position);
            }

            ownedVehicle.ReleaseDriver();
            ridingInVehicle = false;
            nextThink = Time.time + .25f;
        }

        string ActivityDescriptionFor(H51CitizenRoutineState activityState, string label)
        {
            string clean = string.IsNullOrWhiteSpace(label) ? "taking care of something" : label.Trim();
            if (activityState == H51CitizenRoutineState.Home)
                return $"I'm {clean} at home.";
            if (activityState == H51CitizenRoutineState.Work)
                return $"I'm {clean} at work.";
            if (activityState == H51CitizenRoutineState.Shopping)
                return $"I'm {clean} while I'm out shopping.";
            return $"I'm {clean}.";
        }

        void SetLocalWander(Vector3 center)
        {
            float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
            float radius = Mathf.Lerp(1.5f, localWanderRadius, (float)rng.NextDouble());
            Vector3 sample = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            if (NavMesh.SamplePosition(sample, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);
        }

        void CacheAnimatorParameters()
        {
            if (!animator || !animator.runtimeAnimatorController) return;
            AnimatorControllerParameter[] p = animator.parameters;
            for (int i = 0; i < p.Length; i++)
            {
                hasSpeed |= p[i].name == "Speed" && p[i].type == AnimatorControllerParameterType.Float;
                hasWave |= p[i].name == "Wave" && p[i].type == AnimatorControllerParameterType.Trigger;
                hasInteract |= p[i].name == "Interact" && p[i].type == AnimatorControllerParameterType.Trigger;
            }
        }

        static float Planar(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return Mathf.Sqrt(x*x + z*z);
        }
    }
}
