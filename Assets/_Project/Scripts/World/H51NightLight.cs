using Hanger51.NPCs;
using UnityEngine;

namespace Hanger51.World
{
    [DisallowMultipleComponent]
    public sealed class H51NightLight : MonoBehaviour
    {
        [SerializeField] Light realtimeLight;
        [SerializeField] Renderer[] emissiveRenderers = new Renderer[0];
        [SerializeField] H51UtilityGridController grid;
        [SerializeField] bool requiresGridPower = true;
        [SerializeField] bool nightOnly = true;
        [SerializeField] bool requestedOn = true;
        [SerializeField, Min(10f)] float realtimeLightDistance = 110f;
        [SerializeField, Min(0f)] float demandMegawatts = .0015f;
        [SerializeField] Color emissionColor = new Color(1f,.78f,.42f,1f);
        [SerializeField, Min(0f)] float emissionMultiplier = 2.5f;

        Transform viewer;
        MaterialPropertyBlock block;
        float nextRefresh;
        bool effectiveOn;

        public bool RequestedOn => requestedOn;
        public bool EffectiveOn => effectiveOn;
        public float DemandMegawatts =>
            ShouldRequestPower() ? Mathf.Max(0f,demandMegawatts) : 0f;

        public void Configure(
            Light light,
            Renderer[] renderers,
            H51UtilityGridController configuredGrid,
            float megawatts,
            float lightDistance,
            Color emission)
        {
            realtimeLight = light;
            emissiveRenderers = renderers ?? new Renderer[0];
            grid = configuredGrid;
            demandMegawatts = Mathf.Max(0f,megawatts);
            realtimeLightDistance = Mathf.Max(10f,lightDistance);
            emissionColor = emission;
            Refresh(true);
        }

        void Awake()
        {
            if (!realtimeLight) realtimeLight = GetComponentInChildren<Light>(true);
            if (emissiveRenderers == null || emissiveRenderers.Length == 0)
                emissiveRenderers = GetComponentsInChildren<Renderer>(true);
            block = new MaterialPropertyBlock();
            Refresh(true);
        }

        void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .35f + (GetInstanceID() & 15) * .0125f;
            Refresh(false);
        }

        public void ConfigureFlightVisibility(
            float lightDistance,
            float emissionStrength,
            float lightRange,
            float lightIntensity)
        {
            realtimeLightDistance = Mathf.Max(10f,lightDistance);
            emissionMultiplier = Mathf.Max(0f,emissionStrength);

            if (realtimeLight)
            {
                realtimeLight.range = Mathf.Max(1f,lightRange);
                realtimeLight.intensity = Mathf.Max(0f,lightIntensity);
                realtimeLight.shadows = LightShadows.None;
            }

            Refresh(true);
        }

        public void SetRequested(bool on)
        {
            requestedOn = on;
            Refresh(true);
        }

        void Refresh(bool force)
        {
            if (!grid)
                grid = FindFirstObjectByType<H51UtilityGridController>(FindObjectsInactive.Include);

            bool power = !requiresGridPower || (grid && grid.GridOnline);
            bool schedule = !nightOnly || IsNight();
            bool wanted = requestedOn && schedule && power;

            if (wanted != effectiveOn || force)
            {
                effectiveOn = wanted;
                ApplyEmission(effectiveOn);
            }

            if (!viewer)
            {
                Camera camera = Camera.main;
                if (camera) viewer = camera.transform;
            }

            bool nearEnough = true;
            if (viewer)
            {
                Vector3 delta = viewer.position - transform.position;
                nearEnough =
                    delta.sqrMagnitude <=
                    realtimeLightDistance * realtimeLightDistance;
            }

            if (realtimeLight)
                realtimeLight.enabled = effectiveOn && nearEnough;
        }

        bool ShouldRequestPower()
        {
            if (!requestedOn) return false;
            return !nightOnly || IsNight();
        }

        static bool IsNight()
        {
            if (H51DayNightCycle.Instance)
                return H51DayNightCycle.Instance.NightLightingActive;

            H51WorldClock clock = H51WorldClock.Instance;
            if (!clock) return false;
            return clock.Hour < 6f || clock.Hour >= 19.5f;
        }

        void ApplyEmission(bool on)
        {
            if (block == null) block = new MaterialPropertyBlock();
            Color emission = on
                ? emissionColor * Mathf.Max(0f,emissionMultiplier)
                : Color.black;

            for (int i=0;i<emissiveRenderers.Length;i++)
            {
                Renderer renderer = emissiveRenderers[i];
                if (!renderer) continue;

                renderer.GetPropertyBlock(block);
                Material shared = renderer.sharedMaterial;
                if (shared && shared.HasProperty("_EmissionColor"))
                    block.SetColor("_EmissionColor",emission);
                if (shared && shared.HasProperty("_BaseColor"))
                {
                    Color baseColor = on
                        ? Color.Lerp(Color.black,emissionColor,.65f)
                        : Color.Lerp(Color.black,emissionColor,.10f);
                    block.SetColor("_BaseColor",baseColor);
                }
                renderer.SetPropertyBlock(block);
            }
        }

        void OnDisable()
        {
            if (realtimeLight) realtimeLight.enabled = false;
        }

        void OnValidate()
        {
            realtimeLightDistance = Mathf.Max(10f,realtimeLightDistance);
            demandMegawatts = Mathf.Max(0f,demandMegawatts);
            emissionMultiplier = Mathf.Max(0f,emissionMultiplier);
        }
    }
}
