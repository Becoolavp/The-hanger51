using UnityEngine;
using UnityEngine.AI;

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
        NavMeshAgent agent;
        Animator animator;
        float nextThink;
        float pauseUntil;
        System.Random rng;
        bool hasSpeed;
        bool hasWave;
        bool hasInteract;
        string currentActivityDescription = "I'm just going about my day.";

        public H51CitizenRoutineState State => state;
        public string CurrentActivityDescription => currentActivityDescription;

        void Awake()
        {
            identity = GetComponent<H51CitizenIdentity>();
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponentInChildren<Animator>(true);
            rng = new System.Random(identity ? identity.AppearanceSeed : GetInstanceID());
            CacheAnimatorParameters();
        }

        void Start()
        {
            registry = H51CitizenRegistry.Instance;
            if (!registry) registry = FindFirstObjectByType<H51CitizenRegistry>();
            ResolveAnchors();
            SnapToNavMesh();
            nextThink = Time.time + (float)rng.NextDouble() * thinkInterval;
        }

        void Update()
        {
            if (hasSpeed && animator)
                animator.SetFloat("Speed", agent && agent.isOnNavMesh ? agent.velocity.magnitude : 0f, .15f, Time.deltaTime);

            if (Time.time < nextThink) return;
            nextThink = Time.time + thinkInterval;
            Think();
        }

        void ResolveAnchors()
        {
            if (!registry || !identity) return;
            home = registry.FindAnchor(identity.HomeAnchorId);
            work = registry.FindAnchor(identity.WorkAnchorId);
            leisure = registry.PickAnchor(identity.TownName, H51CitizenAnchorKind.Park, identity.AppearanceSeed + 31)
                      ?? registry.PickAnchor(identity.TownName, H51CitizenAnchorKind.Leisure, identity.AppearanceSeed + 41);
            shop = registry.PickAnchor(identity.TownName, H51CitizenAnchorKind.Shop, identity.AppearanceSeed + 53);
        }

        void SnapToNavMesh()
        {
            if (!agent || agent.isOnNavMesh) return;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 18f, NavMesh.AllAreas))
                agent.Warp(hit.position);
        }

        void Think()
        {
            if (!agent || !agent.enabled) return;
            if (!agent.isOnNavMesh) { SnapToNavMesh(); if (!agent.isOnNavMesh) return; }
            if (!registry) { registry = H51CitizenRegistry.Instance; ResolveAnchors(); }

            float hour = H51WorldClock.Instance ? H51WorldClock.Instance.Hour : Mathf.Repeat(Time.time / 60f + 7f, 24f);
            H51CitizenAnchor desired;
            H51CitizenRoutineState desiredState;

            if (hour < 5.5f || hour >= 22.5f)
            {
                desired = home; desiredState = H51CitizenRoutineState.Home;
            }
            else if (hour < 12f)
            {
                desired = work ?? home; desiredState = work ? H51CitizenRoutineState.Work : H51CitizenRoutineState.Leisure;
            }
            else if (hour < 13.25f)
            {
                desired = leisure ?? shop ?? home; desiredState = H51CitizenRoutineState.Leisure;
            }
            else if (hour < 17.25f)
            {
                desired = work ?? home; desiredState = work ? H51CitizenRoutineState.Work : H51CitizenRoutineState.Leisure;
            }
            else if (hour < 19.5f)
            {
                desired = shop ?? leisure ?? home; desiredState = shop ? H51CitizenRoutineState.Shopping : H51CitizenRoutineState.Leisure;
            }
            else
            {
                bool socialEvening = identity && identity.Sociability > .45f && rng.NextDouble() < .58;
                desired = socialEvening ? (leisure ?? home) : home;
                desiredState = socialEvening && leisure ? H51CitizenRoutineState.Leisure : H51CitizenRoutineState.Home;
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

            if (desiredState == H51CitizenRoutineState.Work && desired == work)
            {
                H51CitizenActivitySpot[] spots = work.GetComponentsInChildren<H51CitizenActivitySpot>(true);
                if (spots != null && spots.Length > 0)
                {
                    H51CitizenActivitySpot spot = spots[rng.Next(spots.Length)];
                    currentActivityDescription = $"I'm {spot.ActivityLabel} at work.";
                    if (NavMesh.SamplePosition(spot.transform.position, out NavMeshHit workHit, 4f, NavMesh.AllAreas))
                        agent.SetDestination(workHit.position);

                    pauseUntil = Time.time + Mathf.Lerp(spot.MinimumDuration, spot.MaximumDuration, (float)rng.NextDouble());
                    if (animator && hasInteract && !string.IsNullOrWhiteSpace(spot.AnimationTrigger))
                        animator.SetTrigger(spot.AnimationTrigger);
                    return;
                }
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
