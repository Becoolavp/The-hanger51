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
            if (runtimeSky && runtimeSky != RenderSettings.skybox)
                Destroy(runtimeSky);
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

            Material sky = runtimeSky ? runtimeSky : RenderSettings.skybox;
            if (sky && sky.HasProperty("_Exposure"))
            {
                sky.SetFloat(
                    "_Exposure",
                    Mathf.Lerp(nightSkyExposure,daySkyExposure,daylightFactor));
            }
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
        }
    }
}
