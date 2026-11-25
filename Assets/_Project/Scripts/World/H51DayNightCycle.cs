using Hanger51.NPCs;
using UnityEngine;

namespace Hanger51.World
{
    [DisallowMultipleComponent]
    public sealed class H51DayNightCycle : MonoBehaviour
    {
        public static H51DayNightCycle Instance { get; private set; }

        [SerializeField] H51WorldClock clock;
        [SerializeField] Light sun;
        [SerializeField] Light moon;
        [SerializeField] Transform moonVisual;
        [SerializeField, Min(100f)] float moonVisualDistance = 900f;
        [SerializeField, Min(5f)] float moonVisualDiameter = 48f;
        [SerializeField] Material nightSkybox;
        [SerializeField, Range(.2f,.95f)] float nightSkySwitchFactor = .58f;
        [SerializeField, Range(.05f,2f)] float dedicatedNightSkyExposure = .72f;

        [Header("Solar Timing")]
        [SerializeField, Range(0f,12f)] float baseSunriseHour = 6f;
        [SerializeField, Range(12f,24f)] float baseSunsetHour = 20f;
        [SerializeField, Range(0f,3f)] float seasonalShiftHours = 1.45f;
        [SerializeField, Range(.1f,2f)] float twilightHours = .8f;
        [SerializeField, Range(0f,360f)] float sunYaw = 155f;

        [Header("Lighting")]
        [SerializeField, Min(0f)] float daySunIntensity = 1.15f;
        [SerializeField, Min(0f)] float moonIntensity = .11f;
        [SerializeField, Range(0f,2f)] float dayAmbientIntensity = 1f;
        [SerializeField, Range(0f,2f)] float nightAmbientIntensity = .22f;
        [SerializeField] Color daySunColor = new Color(1f,.965f,.88f,1f);
        [SerializeField] Color sunsetSunColor = new Color(1f,.58f,.32f,1f);
        [SerializeField] Color moonColor = new Color(.55f,.68f,1f,1f);
        [SerializeField] Color dayFogColor = new Color(.70f,.82f,.92f,1f);
        [SerializeField] Color nightFogColor = new Color(.035f,.055f,.095f,1f);
        [SerializeField, Min(.05f)] float daySkyExposure = 1f;
        [SerializeField, Min(.01f)] float nightSkyExposure = .13f;

        float daylightFactor = 1f;
        float nightFactor;
        float sunriseHour;
        float sunsetHour;
        Material sourceSky;
        Material runtimeSky;

        public float DaylightFactor => daylightFactor;
        public float NightFactor => nightFactor;
        public float SunriseHour => sunriseHour;
        public float SunsetHour => sunsetHour;
        public bool NightLightingActive => nightFactor >= .35f;

        public void Configure(H51WorldClock configuredClock, Light configuredSun, Light configuredMoon)
        {
            clock = configuredClock;
            sun = configuredSun;
            moon = configuredMoon;
            Resolve();
            ApplyCycle();
        }

        public void ConfigureNightSky(Material material)
        {
            nightSkybox = material;
            ApplyCycle();
        }

        public bool HasNightSky => nightSkybox;

        public void ConfigureMoonVisual(
            Transform visual,
            float distance = 1800f,
            float diameter = 95f)
        {
            moonVisual = visual;
            moonVisualDistance = Mathf.Max(250f,distance);
            moonVisualDiameter = Mathf.Max(5f,diameter);
            ApplyMoonVisual();
        }

        void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            Resolve();
            PrepareRuntimeSky();
            ApplyCycle();
        }

        void OnEnable()
        {
            Resolve();
            PrepareRuntimeSky();
            ApplyCycle();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (runtimeSky)
            {
                if (RenderSettings.skybox == runtimeSky)
                    RenderSettings.skybox = sourceSky;
                Destroy(runtimeSky);
                runtimeSky = null;
            }
        }

        void Update()
        {
            Resolve();
            ApplyCycle();
        }

        void Resolve()
        {
            if (!clock) clock = H51WorldClock.Instance;
            if (!clock) clock = FindFirstObjectByType<H51WorldClock>(FindObjectsInactive.Include);

            if (!sun)
            {
                sun = RenderSettings.sun;
                if (!sun)
                {
                    Light[] lights = FindObjectsByType<Light>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None);
                    float best = -1f;
                    for (int i=0;i<lights.Length;i++)
                    {
                        Light candidate = lights[i];
                        if (!candidate || candidate.type != LightType.Directional) continue;
                        if (candidate.intensity <= best) continue;
                        best = candidate.intensity;
                        sun = candidate;
                    }
                }
            }
        }

        void PrepareRuntimeSky()
        {
            Material source = RenderSettings.skybox;
            if (!source || runtimeSky) return;

            sourceSky = source;
            runtimeSky = new Material(source)
            {
                name = source.name + " (H51 Runtime Day Night)"
            };
            RenderSettings.skybox = runtimeSky;
        }

        void ApplyCycle()
        {
            if (!clock) return;

            float seasonal = Mathf.Sin(
                ((clock.DayOfYear - 80f) / 365.25f) * Mathf.PI * 2f);
            sunriseHour = Mathf.Clamp(
                baseSunriseHour - seasonalShiftHours * seasonal,
                3.5f,
                9f);
            sunsetHour = Mathf.Clamp(
                baseSunsetHour + seasonalShiftHours * seasonal,
                16f,
                23f);

            float hour = clock.Hour;
            float dawn = SmoothWindow(
                hour,
                sunriseHour - twilightHours,
                sunriseHour + twilightHours);
            float dusk = 1f - SmoothWindow(
                hour,
                sunsetHour - twilightHours,
                sunsetHour + twilightHours);

            daylightFactor = Mathf.Clamp01(Mathf.Min(dawn,dusk));
            nightFactor = 1f - daylightFactor;

            float solarPitch = Mathf.Repeat(hour / 24f * 360f - 90f,360f);
            Quaternion solarRotation = Quaternion.Euler(solarPitch,sunYaw,0f);

            if (sun)
            {
                sun.transform.rotation = solarRotation;
                float horizonWarmth =
                    1f - Mathf.Clamp01(Mathf.Abs(hour - 12f) / 6f);
                Color warm = Color.Lerp(sunsetSunColor,daySunColor,horizonWarmth);
                sun.color = warm;
                sun.intensity = daySunIntensity * daylightFactor;
                sun.enabled = sun.intensity > .002f;
                RenderSettings.sun = sun;
            }

            if (moon)
            {
                moon.transform.rotation =
                    Quaternion.Euler(
                        Mathf.Repeat(solarPitch + 180f,360f),
                        sunYaw + 180f,
                        0f);
                moon.color = moonColor;
                moon.intensity = moonIntensity * nightFactor;
                moon.enabled = moon.intensity > .002f;
            }

            RenderSettings.ambientIntensity =
                Mathf.Lerp(nightAmbientIntensity,dayAmbientIntensity,daylightFactor);
            RenderSettings.fogColor =
                Color.Lerp(nightFogColor,dayFogColor,daylightFactor);

            ApplyMoonVisual();

            Material daySky = runtimeSky ? runtimeSky : sourceSky;
            bool useNightSky =
                nightSkybox &&
                nightFactor >= nightSkySwitchFactor;

            Material sky = useNightSky
                ? nightSkybox
                : daySky;

            if (sky && RenderSettings.skybox != sky)
                RenderSettings.skybox = sky;

            if (sky && sky.HasProperty("_Exposure"))
            {
                float exposure = useNightSky
                    ? dedicatedNightSkyExposure
                    : Mathf.Lerp(
                        nightSkyExposure,
                        daySkyExposure,
                        daylightFactor);
                sky.SetFloat("_Exposure",exposure);
            }
        }

        void ApplyMoonVisual()
        {
            if (!moonVisual || !moon)
            {
                return;
            }

            Camera camera = Camera.main;
            Vector3 origin = camera
                ? camera.transform.position
                : transform.position;

            // A directional light's forward vector is the direction its rays
            // travel toward the world. The visible celestial body belongs on the
            // opposite side of the sky, where those rays originate.
            Vector3 direction = -moon.transform.forward.normalized;

            float configuredDistance = Mathf.Max(100f,moonVisualDistance);
            float actualDistance = configuredDistance;
            if (camera)
            {
                // Keep the moon safely inside the active camera far clip plane.
                // Preserve its apparent angular size when the camera's clip range
                // forces the visual closer than the configured celestial distance.
                float clipSafeDistance =
                    Mathf.Max(75f,camera.farClipPlane * .62f);
                actualDistance =
                    Mathf.Min(configuredDistance,clipSafeDistance);
            }

            moonVisual.position =
                origin + direction * actualDistance;

            float apparentScale =
                Mathf.Max(5f,moonVisualDiameter) *
                (actualDistance / configuredDistance);
            moonVisual.localScale =
                Vector3.one * Mathf.Max(4f,apparentScale);

            Renderer[] moonRenderers =
                moonVisual.GetComponentsInChildren<Renderer>(true);
            for (int i=0;i<moonRenderers.Length;i++)
            {
                Renderer renderer = moonRenderers[i];
                if (!renderer) continue;
                renderer.enabled = true;
                renderer.forceRenderingOff = false;
            }

            if (camera)
            {
                moonVisual.rotation =
                    Quaternion.LookRotation(
                        moonVisual.position - camera.transform.position,
                        Vector3.up);
            }

            bool aboveUsefulHorizon = direction.y > -.05f;
            bool visible =
                nightFactor > .08f &&
                aboveUsefulHorizon;

            if (moonVisual.gameObject.activeSelf != visible)
                moonVisual.gameObject.SetActive(visible);
        }

        static float SmoothWindow(float value, float start, float end)
        {
            if (end <= start) return value >= end ? 1f : 0f;
            return Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(start,end,value));
        }

        void OnValidate()
        {
            baseSunriseHour = Mathf.Clamp(baseSunriseHour,0f,12f);
            baseSunsetHour = Mathf.Clamp(baseSunsetHour,12f,24f);
            seasonalShiftHours = Mathf.Clamp(seasonalShiftHours,0f,3f);
            twilightHours = Mathf.Clamp(twilightHours,.1f,2f);
            daySunIntensity = Mathf.Max(0f,daySunIntensity);
            moonIntensity = Mathf.Max(0f,moonIntensity);
            moonVisualDistance = Mathf.Max(100f,moonVisualDistance);
            moonVisualDiameter = Mathf.Max(5f,moonVisualDiameter);
            nightSkySwitchFactor = Mathf.Clamp(nightSkySwitchFactor,.2f,.95f);
            dedicatedNightSkyExposure =
                Mathf.Clamp(dedicatedNightSkyExposure,.05f,2f);
            nightSkyExposure = Mathf.Max(.01f,nightSkyExposure);
        }
    }
}
