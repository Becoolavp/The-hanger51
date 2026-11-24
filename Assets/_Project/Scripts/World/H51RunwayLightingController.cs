using Hanger51.NPCs;
using UnityEngine;

namespace Hanger51.World
{
    public enum H51RunwayLightMode
    {
        Off,
        Auto,
        On
    }

    [DisallowMultipleComponent]
    public sealed class H51RunwayLightingController : MonoBehaviour
    {
        [SerializeField] H51RunwayLightMode mode = H51RunwayLightMode.Auto;
        [SerializeField] H51WorldClock clock;
        [SerializeField] H51UtilityGridController grid;
        [SerializeField] Renderer[] fixtures = new Renderer[0];
        [SerializeField] Light[] realtimeLights = new Light[0];
        [SerializeField, Min(20f)] float realtimeLightDistance = 260f;
        [SerializeField] Color emissionColor = new Color(.70f,.88f,1f,1f);
        [SerializeField, Min(0f)] float emissionMultiplier = 4f;
        [SerializeField, Min(0f)] float totalDemandMegawatts = .085f;

        Transform viewer;
        MaterialPropertyBlock block;
        float nextRefresh;
        bool effectiveOn;

        public H51RunwayLightMode Mode => mode;
        public bool EffectiveOn => effectiveOn;
        public float DemandMegawatts => WantsLights() ? totalDemandMegawatts : 0f;
        public string StatusText =>
            "RUNWAY LIGHTS " + mode.ToString().ToUpperInvariant() +
            (effectiveOn ? " — LIT" : " — DARK");

        public void Configure(
            Renderer[] configuredFixtures,
            Light[] configuredLights,
            H51UtilityGridController configuredGrid,
            H51WorldClock configuredClock)
        {
            fixtures = configuredFixtures ?? new Renderer[0];
            realtimeLights = configuredLights ?? new Light[0];
            grid = configuredGrid;
            clock = configuredClock;
            Refresh(true);
        }

        void Awake()
        {
            block = new MaterialPropertyBlock();
            Resolve();
            Refresh(true);
        }

        void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .20f;
            Resolve();
            Refresh(false);
        }

        public void CycleMode()
        {
            mode = mode == H51RunwayLightMode.Off
                ? H51RunwayLightMode.Auto
                : mode == H51RunwayLightMode.Auto
                    ? H51RunwayLightMode.On
                    : H51RunwayLightMode.Off;
            Refresh(true);
        }

        public void SetMode(H51RunwayLightMode newMode)
        {
            mode = newMode;
            Refresh(true);
        }

        bool WantsLights()
        {
            if (mode == H51RunwayLightMode.On) return true;
            if (mode == H51RunwayLightMode.Off) return false;

            if (H51DayNightCycle.Instance)
                return H51DayNightCycle.Instance.NightLightingActive;

            return clock && (clock.Hour < 6f || clock.Hour >= 19.5f);
        }

        void Resolve()
        {
            if (!clock) clock = H51WorldClock.Instance;
            if (!clock) clock = FindFirstObjectByType<H51WorldClock>(FindObjectsInactive.Include);
            if (!grid) grid = FindFirstObjectByType<H51UtilityGridController>(FindObjectsInactive.Include);
            if (!viewer)
            {
                Camera camera = Camera.main;
                if (camera) viewer = camera.transform;
            }
        }

        void Refresh(bool force)
        {
            bool gridPower = grid && grid.GridOnline;
            bool wanted = WantsLights();
            bool nextEffective = wanted && gridPower;

            if (force || nextEffective != effectiveOn)
            {
                effectiveOn = nextEffective;
                ApplyFixtureEmission(effectiveOn);
            }

            for (int i=0;i<realtimeLights.Length;i++)
            {
                Light light = realtimeLights[i];
                if (!light) continue;

                bool nearEnough = true;
                if (viewer)
                {
                    Vector3 delta = viewer.position - light.transform.position;
                    nearEnough =
                        delta.sqrMagnitude <=
                        realtimeLightDistance * realtimeLightDistance;
                }
                light.enabled = effectiveOn && nearEnough;
            }
        }

        void ApplyFixtureEmission(bool on)
        {
            if (block == null) block = new MaterialPropertyBlock();
            Color emission = on
                ? emissionColor * emissionMultiplier
                : Color.black;

            for (int i=0;i<fixtures.Length;i++)
            {
                Renderer renderer = fixtures[i];
                if (!renderer) continue;

                renderer.GetPropertyBlock(block);
                Material shared = renderer.sharedMaterial;
                if (shared && shared.HasProperty("_EmissionColor"))
                    block.SetColor("_EmissionColor",emission);
                if (shared && shared.HasProperty("_BaseColor"))
                    block.SetColor(
                        "_BaseColor",
                        on
                            ? Color.Lerp(Color.black,emissionColor,.72f)
                            : new Color(.055f,.065f,.075f,1f));
                renderer.SetPropertyBlock(block);
            }
        }

        void OnDisable()
        {
            for (int i=0;i<realtimeLights.Length;i++)
                if (realtimeLights[i]) realtimeLights[i].enabled = false;
        }

        void OnValidate()
        {
            realtimeLightDistance = Mathf.Max(20f,realtimeLightDistance);
            emissionMultiplier = Mathf.Max(0f,emissionMultiplier);
            totalDemandMegawatts = Mathf.Max(0f,totalDemandMegawatts);
        }
    }
}
