using Hanger51.Health;
using UnityEngine;
using UnityEngine.AI;

namespace Hanger51.NPCs
{
    [DisallowMultipleComponent]
    public sealed class H51CitizenEmergencyState : MonoBehaviour
    {
        [SerializeField] H51EmergencyDispatchController dispatch;
        [SerializeField] bool activeEmergency;
        [SerializeField] bool requiresAmbulance;
        [SerializeField] bool requiresPolice;
        [SerializeField] bool requiresFire;
        [SerializeField, Range(0f,100f)] float injurySeverity;
        [SerializeField] string emergencyReason;

        H51CitizenBrain brain;
        H51CitizenIdentity identity;
        H51AnatomicalHealth anatomicalHealth;
        NavMeshAgent agent;
        GameObject phoneVisual;
        bool phoneRequestedVisible;
        bool travelVisualSuppressed;
        bool callPlaced;
        bool pausedForEmergency;
        bool beingTransported;
        float callAt;
        float pauseAt;

        Transform transportOriginalParent;
        Renderer[] transportRenderers;
        bool[] transportRendererStates;
        Collider[] transportColliders;
        bool[] transportColliderStates;
        bool brainWasEnabled = true;
        bool agentWasEnabled = true;

        public bool IsActive => activeEmergency;
        public bool RequiresAmbulance => requiresAmbulance;
        public bool RequiresPolice => requiresPolice;
        public bool RequiresFire => requiresFire;
        public float InjurySeverity => injurySeverity;
        public bool NeedsTransport
        {
            get
            {
                if (!anatomicalHealth)
                    anatomicalHealth = GetComponent<H51AnatomicalHealth>();

                return requiresAmbulance &&
                    injurySeverity >= 35f &&
                    (!anatomicalHealth || !anatomicalHealth.IsDead);
            }
        }

        public string CurrentStatusText
        {
            get
            {
                if (!activeEmergency) return "I'm okay.";
                if (beingTransported) return "I'm being taken to the hospital.";
                if (!callPlaced) return "I'm pulling out my phone to call for help.";

                if (requiresAmbulance && requiresPolice)
                    return "I've called for an ambulance and police. I'm waiting for them.";
                if (requiresAmbulance)
                    return "I've called for an ambulance. I'm waiting for the medical team.";
                if (requiresFire)
                    return "I've called the fire department. I'm waiting for help.";
                if (requiresPolice)
                    return "I've called the police. I'm waiting for an officer.";
                return "Help has arrived and the situation is being handled.";
            }
        }

        void Awake()
        {
            brain = GetComponent<H51CitizenBrain>();
            identity = GetComponent<H51CitizenIdentity>();
            anatomicalHealth = GetComponent<H51AnatomicalHealth>();
            agent = GetComponent<NavMeshAgent>();
        }

        public void Configure(H51EmergencyDispatchController configuredDispatch)
        {
            dispatch = configuredDispatch;
        }

        public void ApplyInjury(float severity, string reason, bool policeAlso = false)
        {
            TriggerMedicalEmergency(severity, reason, policeAlso);
        }

        public void TriggerMedicalEmergency(float severity, string reason, bool policeAlso = false)
        {
            BeginEmergency(
                true,
                policeAlso,
                false,
                Mathf.Clamp(severity, 1f, 100f),
                string.IsNullOrWhiteSpace(reason) ? "medical emergency" : reason);
        }

        public void TriggerSafetyEmergency(string reason)
        {
            BeginEmergency(
                false,
                true,
                false,
                0f,
                string.IsNullOrWhiteSpace(reason) ? "safety emergency" : reason);
        }

        public void TriggerFireEmergency(string reason)
        {
            BeginEmergency(
                false,
                true,
                true,
                0f,
                string.IsNullOrWhiteSpace(reason) ? "fire emergency" : reason);
        }

        void BeginEmergency(
            bool ambulance,
            bool police,
            bool fire,
            float severity,
            string reason)
        {
            requiresAmbulance |= ambulance;
            requiresPolice |= police;
            requiresFire |= fire;
            injurySeverity = Mathf.Max(injurySeverity, severity);
            emergencyReason = reason;

            if (!activeEmergency)
            {
                activeEmergency = true;
                callPlaced = false;
                pausedForEmergency = false;
                beingTransported = false;
                pauseAt = Time.time + 1.0f;
                callAt = Time.time + 1.35f;
                ShowPhone(true);
            }

            if (identity)
            {
                identity.Remember(
                    "emergency",
                    "I needed emergency help because of " + emergencyReason + ".",
                    "EMERGENCY",
                    -8f,
                    Mathf.Max(10f, injurySeverity));
            }
        }

        public void SetTravelVisualSuppressed(bool suppressed)
        {
            travelVisualSuppressed = suppressed;

            if (phoneVisual)
                phoneVisual.SetActive(
                    phoneRequestedVisible &&
                    !travelVisualSuppressed &&
                    !beingTransported);
        }

        void Update()
        {
            if (!activeEmergency || beingTransported) return;

            if (!pausedForEmergency && Time.time >= pauseAt)
            {
                if (agent && !agent.enabled && Time.time < pauseAt + 3f)
                    return;

                PauseRoutine();
            }

            if (!callPlaced && Time.time >= callAt)
                PlaceCall();
        }

        void PauseRoutine()
        {
            pausedForEmergency = true;
            brainWasEnabled = brain && brain.enabled;
            agentWasEnabled = agent && agent.enabled;

            if (brain) brain.enabled = false;

            if (agent && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                if (agent.hasPath) agent.ResetPath();
            }
        }

        void PlaceCall()
        {
            if (!dispatch)
                dispatch = FindFirstObjectByType<H51EmergencyDispatchController>();
            if (!dispatch) return;

            callPlaced = true;
            dispatch.RequestEmergency(
                this,
                requiresAmbulance,
                requiresPolice,
                requiresFire,
                emergencyReason);
        }

        public void OnServiceArrived(H51EmergencyServiceType serviceType)
        {
            ShowPhone(false);

            if (serviceType == H51EmergencyServiceType.Police)
                requiresPolice = false;
            else if (serviceType == H51EmergencyServiceType.Fire)
                requiresFire = false;

            if (serviceType != H51EmergencyServiceType.Ambulance)
                ResolveIfComplete();
        }

        public void CompleteOnSceneMedicalTreatment()
        {
            if (!anatomicalHealth)
                anatomicalHealth = GetComponent<H51AnatomicalHealth>();

            if (anatomicalHealth)
                anatomicalHealth.StabilizeAllBleeding(.92f);

            injurySeverity = 0f;
            requiresAmbulance = false;
            ResolveIfComplete();
        }

        public void BeginAmbulanceTransport(Transform vehicle)
        {
            if (!activeEmergency || beingTransported || !vehicle) return;

            beingTransported = true;
            ShowPhone(false);
            PauseRoutine();

            transportOriginalParent = transform.parent;
            transportRenderers = GetComponentsInChildren<Renderer>(true);
            transportRendererStates = new bool[transportRenderers.Length];
            for (int i = 0; i < transportRenderers.Length; i++)
            {
                if (!transportRenderers[i]) continue;
                transportRendererStates[i] = transportRenderers[i].enabled;
                transportRenderers[i].enabled = false;
            }

            transportColliders = GetComponentsInChildren<Collider>(true);
            transportColliderStates = new bool[transportColliders.Length];
            for (int i = 0; i < transportColliders.Length; i++)
            {
                if (!transportColliders[i]) continue;
                transportColliderStates[i] = transportColliders[i].enabled;
                transportColliders[i].enabled = false;
            }

            if (agent) agent.enabled = false;
            transform.SetParent(vehicle, true);
            transform.localPosition = new Vector3(0f,1.1f,-.5f);
        }

        public void CompleteAmbulanceTransport(Vector3 hospitalPosition)
        {
            if (!beingTransported) return;

            transform.SetParent(transportOriginalParent, true);
            transform.position = hospitalPosition + new Vector3(1.5f,0f,0f);

            if (transportRenderers != null)
            {
                for (int i = 0; i < transportRenderers.Length; i++)
                    if (transportRenderers[i])
                        transportRenderers[i].enabled = transportRendererStates[i];
            }

            if (transportColliders != null)
            {
                for (int i = 0; i < transportColliders.Length; i++)
                    if (transportColliders[i])
                        transportColliders[i].enabled = transportColliderStates[i];
            }

            if (agent)
            {
                agent.enabled = true;
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 15f, NavMesh.AllAreas))
                    agent.Warp(hit.position);
            }

            beingTransported = false;

            if (!anatomicalHealth)
                anatomicalHealth = GetComponent<H51AnatomicalHealth>();

            if (anatomicalHealth)
                anatomicalHealth.StabilizeAllBleeding(1f);

            injurySeverity = 0f;
            requiresAmbulance = false;
            ResolveIfComplete();
        }

        void ResolveIfComplete()
        {
            if (requiresAmbulance || requiresPolice || requiresFire) return;

            activeEmergency = false;
            callPlaced = false;
            ShowPhone(false);
            ResumeRoutine();
        }

        void ResumeRoutine()
        {
            if (beingTransported) return;

            if (!anatomicalHealth)
                anatomicalHealth = GetComponent<H51AnatomicalHealth>();

            // A deceased test NPC remains in-world and must never resume its
            // routine after ambulance/police callbacks finish.
            if (anatomicalHealth && anatomicalHealth.IsDead)
            {
                if (brain) brain.enabled = false;

                if (agent && agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    if (agent.hasPath) agent.ResetPath();
                }

                pausedForEmergency = true;
                return;
            }

            if (brain)
                brain.enabled = brainWasEnabled;

            if (agent)
            {
                if (!agent.enabled && agentWasEnabled)
                    agent.enabled = true;

                if (agent.enabled)
                {
                    if (!agent.isOnNavMesh &&
                        NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 15f, NavMesh.AllAreas))
                        agent.Warp(hit.position);

                    if (agent.isOnNavMesh)
                        agent.isStopped = false;
                }
            }

            pausedForEmergency = false;
        }

        void ShowPhone(bool show)
        {
            phoneRequestedVisible = show;

            if (!show)
            {
                if (phoneVisual) phoneVisual.SetActive(false);
                return;
            }

            if (travelVisualSuppressed || beingTransported)
            {
                if (phoneVisual) phoneVisual.SetActive(false);
                return;
            }

            if (!phoneVisual)
            {
                phoneVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                phoneVisual.name = "Emergency Phone";
                phoneVisual.transform.SetParent(transform, false);
                phoneVisual.transform.localPosition = new Vector3(.28f,1.25f,.18f);
                phoneVisual.transform.localRotation = Quaternion.Euler(8f,-18f,12f);
                phoneVisual.transform.localScale = new Vector3(.075f,.14f,.018f);

                Collider collider = phoneVisual.GetComponent<Collider>();
                if (collider) Destroy(collider);

                Renderer renderer = phoneVisual.GetComponent<Renderer>();
                if (renderer)
                {
                    Shader shader =
                        Shader.Find("Universal Render Pipeline/Lit") ??
                        Shader.Find("Standard");
                    if (shader)
                    {
                        Material material = new Material(shader);
                        material.name = "Runtime Citizen Phone";
                        Color color = new Color(.025f,.03f,.04f);
                        if (material.HasProperty("_BaseColor"))
                            material.SetColor("_BaseColor", color);
                        else if (material.HasProperty("_Color"))
                            material.SetColor("_Color", color);
                        renderer.sharedMaterial = material;
                    }
                }
            }

            phoneVisual.SetActive(true);
        }
    }
}
