using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.World
{
    [DefaultExecutionOrder(-10000)]
    public sealed class H51ParticleVelocitySanitizer : MonoBehaviour
    {
        private static H51ParticleVelocitySanitizer instance;
        private float nextScanTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null)
            {
                return;
            }

            GameObject root = new GameObject("H51 Particle Velocity Sanitizer");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<H51ParticleVelocitySanitizer>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                instance = null;
            }
        }

        private void Start()
        {
            RepairLoadedParticleSystems(true);
        }

        private void Update()
        {
            if (Time.unscaledTime < nextScanTime)
            {
                return;
            }

            nextScanTime = Time.unscaledTime + 2f;
            RepairLoadedParticleSystems(false);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _ = scene;
            _ = mode;
            RepairLoadedParticleSystems(true);
        }

        public static int RepairLoadedParticleSystems(bool logRepairs)
        {
            ParticleSystem[] systems = Object.FindObjectsByType<ParticleSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            int repaired = 0;
            for (int index = 0; index < systems.Length; index++)
            {
                ParticleSystem system = systems[index];
                if (system == null)
                {
                    continue;
                }

                bool badVelocity = HasMismatchedVelocityModes(system);
                bool badLimit = HasMismatchedLimitVelocityModes(system);
                if (!badVelocity && !badLimit)
                {
                    continue;
                }

                if (badVelocity)
                {
                    ParticleSystem.VelocityOverLifetimeModule velocity =
                        system.velocityOverLifetime;
                    velocity.enabled = false;
                }

                if (badLimit)
                {
                    ParticleSystem.LimitVelocityOverLifetimeModule limit =
                        system.limitVelocityOverLifetime;
                    limit.enabled = false;
                }

                repaired++;
                if (logRepairs)
                {
                    Debug.LogWarning(
                        "Hanger 51 repaired malformed particle velocity curves on '" +
                        HierarchyPath(system.transform) +
                        "'. Disabled " +
                        (badVelocity ? "Velocity over Lifetime" : string.Empty) +
                        (badVelocity && badLimit ? " and " : string.Empty) +
                        (badLimit ? "Limit Velocity over Lifetime" : string.Empty) +
                        " so Unity will not spam 'Particle Velocity curves must all be in the same mode'.",
                        system);
                }
            }

            return repaired;
        }

        public static bool HasMismatchedVelocityModes(ParticleSystem system)
        {
            if (system == null)
            {
                return false;
            }

            ParticleSystem.VelocityOverLifetimeModule velocity =
                system.velocityOverLifetime;
            if (!velocity.enabled)
            {
                return false;
            }

            bool linearMismatch =
                velocity.x.mode != velocity.y.mode ||
                velocity.x.mode != velocity.z.mode;

            bool orbitalMismatch =
                velocity.orbitalX.mode != velocity.orbitalY.mode ||
                velocity.orbitalX.mode != velocity.orbitalZ.mode;

            return linearMismatch || orbitalMismatch;
        }

        public static bool HasMismatchedLimitVelocityModes(ParticleSystem system)
        {
            if (system == null)
            {
                return false;
            }

            ParticleSystem.LimitVelocityOverLifetimeModule limit =
                system.limitVelocityOverLifetime;
            if (!limit.enabled || !limit.separateAxes)
            {
                return false;
            }

            return limit.limitX.mode != limit.limitY.mode ||
                limit.limitX.mode != limit.limitZ.mode;
        }

        private static string HierarchyPath(Transform target)
        {
            if (target == null)
            {
                return "(missing particle)";
            }

            string path = target.name;
            Transform parent = target.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }
    }
}
