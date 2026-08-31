using UnityEngine;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(520)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(P51PilotPlayerInteractor))]
    public sealed class P51CockpitMaintenanceSuppression : MonoBehaviour
    {
        [SerializeField] private P51PilotPlayerInteractor pilotInteractor;

        private readonly Behaviour[] suppressedInteractors = new Behaviour[4];
        private readonly bool[] previouslyEnabled = new bool[4];
        private bool suppressionActive;
        private float nextResolveTime;

        public bool IsSuppressing => suppressionActive;
        public int ConfiguredInteractorCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < suppressedInteractors.Length; index++)
                {
                    if (suppressedInteractors[index] != null) count++;
                }
                return count;
            }
        }

        private void Awake()
        {
            ResolveReferences();
            ApplyPilotState();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ApplyPilotState();
        }

        private void Update()
        {
            if (!suppressionActive && Time.unscaledTime >= nextResolveTime)
            {
                ResolveReferences();
            }

            ApplyPilotState();

            if (suppressionActive)
            {
                // Runtime bootstraps can re-enable or add service interactors after cockpit
                // entry. Keep the seated state authoritative for the entire frame.
                for (int index = 0; index < suppressedInteractors.Length; index++)
                {
                    Behaviour target = suppressedInteractors[index];
                    if (target != null && target.enabled)
                    {
                        target.enabled = false;
                    }
                }
            }
        }

        private void ApplyPilotState()
        {
            bool shouldSuppress = pilotInteractor != null && pilotInteractor.IsPiloting;
            if (shouldSuppress && !suppressionActive)
            {
                BeginSuppression();
            }
            else if (!shouldSuppress && suppressionActive)
            {
                RestoreInteractors();
            }
        }

        private void BeginSuppression()
        {
            ResolveReferences();
            suppressionActive = true;
            for (int index = 0; index < suppressedInteractors.Length; index++)
            {
                Behaviour target = suppressedInteractors[index];
                previouslyEnabled[index] = target != null && target.enabled;
                if (target != null)
                {
                    target.enabled = false;
                }
            }
        }

        private void RestoreInteractors()
        {
            suppressionActive = false;
            for (int index = 0; index < suppressedInteractors.Length; index++)
            {
                Behaviour target = suppressedInteractors[index];
                if (target != null)
                {
                    target.enabled = previouslyEnabled[index];
                }
                previouslyEnabled[index] = false;
            }
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            if (pilotInteractor == null)
            {
                pilotInteractor = GetComponent<P51PilotPlayerInteractor>();
            }

            suppressedInteractors[0] = GetComponent<P51LandingGearServicePlayerInteractor>();
            suppressedInteractors[1] = GetComponent<P51WingArmamentServicePointInteractor>();
            suppressedInteractors[2] = GetComponent<P51CoolantPlayerInteractor>();
            suppressedInteractors[3] = GetComponent<P51TowBarPlayerInteractor>();
            nextResolveTime = Time.unscaledTime + 0.5f;
        }

        private void OnDisable()
        {
            if (suppressionActive)
            {
                RestoreInteractors();
            }
        }
    }
}
