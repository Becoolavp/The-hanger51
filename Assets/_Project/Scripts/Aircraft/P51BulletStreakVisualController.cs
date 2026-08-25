using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class P51BulletStreakVisualController : MonoBehaviour
    {
        [SerializeField] private P51WingArmamentSystem system;
        [SerializeField] private Transform[] muzzles = new Transform[6];
        [SerializeField, Min(0.04f)] private float secondsBetweenStreaks = 0.095f;
        [SerializeField, Min(100f)] private float bulletSpeedMetersPerSecond = 760f;
        [SerializeField, Min(0.25f)] private float streakLengthMeters = 5.5f;
        [SerializeField, Min(0.001f)] private float streakWidthMeters = 0.009f;
        [SerializeField, Min(50f)] private float maxVisualRangeMeters = 850f;

        private P51FlightController flightController;
        private Material streakMaterial;
        private float nextStreakTime;

        public void Configure(P51WingArmamentSystem configuredSystem, Transform[] configuredMuzzles)
        {
            system = configuredSystem;
            muzzles = new Transform[6];
            if (configuredMuzzles != null)
            {
                int count = Mathf.Min(muzzles.Length, configuredMuzzles.Length);
                for (int index = 0; index < count; index++)
                {
                    muzzles[index] = configuredMuzzles[index];
                }
            }
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            nextStreakTime = 0f;
        }

        private void Update()
        {
            ResolveReferences();
            if (!ShouldFireVisuals())
            {
                return;
            }

            if (Time.time < nextStreakTime)
            {
                return;
            }
            nextStreakTime = Time.time + Mathf.Max(0.04f, secondsBetweenStreaks);

            for (int stationIndex = 0; stationIndex < 6; stationIndex++)
            {
                if (stationIndex >= muzzles.Length
                    || muzzles[stationIndex] == null
                    || !system.IsGunInstalled(stationIndex)
                    || !system.IsAmmoInstalled(stationIndex)
                    || system.GetAmmoRemaining(stationIndex) <= 0)
                {
                    continue;
                }

                SpawnMovingStreak(muzzles[stationIndex]);
            }
        }

        private void LateUpdate()
        {
            // The original armament code briefly creates a full-length LineRenderer from muzzle
            // to impact. Hide those legacy lines before rendering so the player sees only the
            // short moving streaks produced by this controller.
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.leftCtrlKey.isPressed)
            {
                return;
            }

            LineRenderer[] lines = Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None);
            for (int index = 0; index < lines.Length; index++)
            {
                LineRenderer line = lines[index];
                if (line != null && line.gameObject.name == "P-51 Gun Tracer")
                {
                    line.enabled = false;
                }
            }
        }

        private bool ShouldFireVisuals()
        {
            if (system == null || flightController == null || !flightController.PilotPresent)
            {
                return false;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.leftCtrlKey.isPressed)
            {
                return false;
            }

            return !system.IsPanelOpen(0) && !system.IsPanelOpen(1);
        }

        private void SpawnMovingStreak(Transform muzzle)
        {
            Vector3 start = muzzle.position + muzzle.forward * 0.16f;
            Vector3 aircraftVelocity = flightController != null && flightController.AircraftBody != null
                ? flightController.AircraftBody.linearVelocity
                : Vector3.zero;
            Vector3 velocity = muzzle.forward * Mathf.Max(100f, bulletSpeedMetersPerSecond) + aircraftVelocity;
            Vector3 direction = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : muzzle.forward;
            float maxDistance = FindVisualTravelDistance(start, direction);

            GameObject streakObject = new GameObject("P-51 Bullet Streak");
            LineRenderer line = streakObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = Mathf.Max(0.001f, streakWidthMeters);
            line.endWidth = Mathf.Max(0.0005f, streakWidthMeters * 0.55f);
            line.numCapVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.sharedMaterial = GetStreakMaterial();

            P51BulletStreakMover mover = streakObject.AddComponent<P51BulletStreakMover>();
            mover.Configure(
                line,
                start,
                velocity,
                maxDistance,
                Mathf.Max(0.25f, streakLengthMeters));
        }

        private float FindVisualTravelDistance(Vector3 start, Vector3 direction)
        {
            float maxRange = Mathf.Max(50f, maxVisualRangeMeters);
            RaycastHit[] hits = Physics.RaycastAll(
                start,
                direction,
                maxRange,
                ~0,
                QueryTriggerInteraction.Ignore);

            if (hits == null || hits.Length == 0)
            {
                return maxRange;
            }

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int index = 0; index < hits.Length; index++)
            {
                Collider collider = hits[index].collider;
                if (collider == null || collider.transform.IsChildOf(transform))
                {
                    continue;
                }
                return Mathf.Max(0.25f, hits[index].distance);
            }

            return maxRange;
        }

        private Material GetStreakMaterial()
        {
            if (streakMaterial != null)
            {
                return streakMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) return null;

            Color warmWhite = new Color(1f, 0.82f, 0.46f, 1f);
            streakMaterial = new Material(shader)
            {
                name = "P-51 Moving Bullet Streak Material",
                color = warmWhite
            };
            if (streakMaterial.HasProperty("_BaseColor"))
                streakMaterial.SetColor("_BaseColor", warmWhite);
            if (streakMaterial.HasProperty("_Color"))
                streakMaterial.SetColor("_Color", warmWhite);
            return streakMaterial;
        }

        private void ResolveReferences()
        {
            if (system == null) system = GetComponent<P51WingArmamentSystem>();
            if (flightController == null) flightController = GetComponent<P51FlightController>();
        }

        private void OnDestroy()
        {
            if (streakMaterial != null)
            {
                Destroy(streakMaterial);
            }
        }
    }
}
