using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    public sealed class P51WingGunBarrelHeatController : MonoBehaviour
    {
        private const int GunCount = 6;
        private const int HeatedPartsPerGun = 4;
        private const int HeatedRendererCount = GunCount * HeatedPartsPerGun;

        [SerializeField] private P51WingArmamentSystem system;
        [SerializeField] private Renderer[] barrelRenderers = new Renderer[HeatedRendererCount];
        [SerializeField, Min(0.01f)] private float heatPerShot = 0.055f;
        [SerializeField, Min(0.01f)] private float coolingPerSecond = 0.13f;
        [SerializeField, Range(0f, 1f)] private float visibleRedThreshold = 0.18f;
        [SerializeField] private Color coolColor = new Color(0.018f, 0.020f, 0.024f, 1f);
        [SerializeField] private Color dullRedColor = new Color(0.42f, 0.015f, 0.005f, 1f);
        [SerializeField] private Color hotRedColor = new Color(1f, 0.055f, 0.012f, 1f);
        [SerializeField, Min(0f)] private float maxEmissionMultiplier = 5.5f;

        private readonly int[] previousAmmo = new int[GunCount];
        private readonly float[] heat = new float[GunCount];
        private readonly Material[] runtimeMaterials = new Material[HeatedRendererCount];
        private bool initialized;

        public void Configure(P51WingArmamentSystem configuredSystem, Renderer[] configuredBarrelRenderers)
        {
            system = configuredSystem;
            barrelRenderers = new Renderer[HeatedRendererCount];

            if (configuredBarrelRenderers != null)
            {
                if (configuredBarrelRenderers.Length == GunCount)
                {
                    // Backward compatibility with the first heat pass, which supplied one renderer
                    // per gun. Put that renderer in the primary barrel slot for each station.
                    for (int stationIndex = 0; stationIndex < GunCount; stationIndex++)
                    {
                        barrelRenderers[stationIndex * HeatedPartsPerGun] = configuredBarrelRenderers[stationIndex];
                    }
                }
                else
                {
                    int count = Mathf.Min(HeatedRendererCount, configuredBarrelRenderers.Length);
                    for (int index = 0; index < count; index++)
                    {
                        barrelRenderers[index] = configuredBarrelRenderers[index];
                    }
                }
            }

            ClearRuntimeMaterials();
            initialized = false;
            EnsureRuntimeMaterials();
            CaptureAmmoState();
            ApplyAllHeat();
        }

        private void Awake()
        {
            ResolveReferences();
            EnsureRuntimeMaterials();
            CaptureAmmoState();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureRuntimeMaterials();
            CaptureAmmoState();
            ApplyAllHeat();
        }

        private void Update()
        {
            ResolveReferences();
            if (system == null)
            {
                return;
            }

            EnsureRuntimeMaterials();
            if (!initialized)
            {
                CaptureAmmoState();
            }

            float cooling = Mathf.Max(0.01f, coolingPerSecond) * Time.deltaTime;
            for (int stationIndex = 0; stationIndex < GunCount; stationIndex++)
            {
                int currentAmmo = system.GetAmmoRemaining(stationIndex);
                int roundsFired = Mathf.Max(0, previousAmmo[stationIndex] - currentAmmo);
                if (roundsFired > 0)
                {
                    heat[stationIndex] = Mathf.Clamp01(
                        heat[stationIndex] + roundsFired * Mathf.Max(0.01f, heatPerShot));
                }
                else
                {
                    heat[stationIndex] = Mathf.MoveTowards(heat[stationIndex], 0f, cooling);
                }

                previousAmmo[stationIndex] = currentAmmo;
                ApplyHeat(stationIndex);
            }
        }

        private void CaptureAmmoState()
        {
            if (system == null)
            {
                return;
            }

            for (int stationIndex = 0; stationIndex < GunCount; stationIndex++)
            {
                previousAmmo[stationIndex] = system.GetAmmoRemaining(stationIndex);
            }
            initialized = true;
        }

        private void ApplyAllHeat()
        {
            for (int stationIndex = 0; stationIndex < GunCount; stationIndex++)
            {
                ApplyHeat(stationIndex);
            }
        }

        private void ApplyHeat(int stationIndex)
        {
            if (stationIndex < 0 || stationIndex >= GunCount)
            {
                return;
            }

            float normalizedHeat = Mathf.Clamp01(heat[stationIndex]);
            Color baseColor;
            if (normalizedHeat <= visibleRedThreshold)
            {
                float t = visibleRedThreshold <= 0.0001f
                    ? 1f
                    : normalizedHeat / visibleRedThreshold;
                baseColor = Color.Lerp(coolColor, dullRedColor, t * 0.35f);
            }
            else
            {
                float t = Mathf.InverseLerp(visibleRedThreshold, 1f, normalizedHeat);
                baseColor = Color.Lerp(dullRedColor, hotRedColor, t);
            }

            Color emission = Color.black;
            if (normalizedHeat > visibleRedThreshold)
            {
                float emissionT = Mathf.InverseLerp(visibleRedThreshold, 1f, normalizedHeat);
                emission = Color.Lerp(dullRedColor, hotRedColor, emissionT)
                    * (emissionT * Mathf.Max(0f, maxEmissionMultiplier));
            }

            int start = stationIndex * HeatedPartsPerGun;
            int end = Mathf.Min(start + HeatedPartsPerGun, runtimeMaterials.Length);
            for (int index = start; index < end; index++)
            {
                Material material = runtimeMaterials[index];
                if (material == null)
                {
                    continue;
                }

                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", baseColor);
                if (material.HasProperty("_Color")) material.SetColor("_Color", baseColor);

                if (material.HasProperty("_EmissionColor"))
                {
                    material.SetColor("_EmissionColor", emission);
                    if (emission.maxColorComponent > 0.001f)
                        material.EnableKeyword("_EMISSION");
                    else
                        material.DisableKeyword("_EMISSION");
                }
            }
        }

        private void EnsureRuntimeMaterials()
        {
            if (barrelRenderers == null || barrelRenderers.Length != HeatedRendererCount)
            {
                Renderer[] resized = new Renderer[HeatedRendererCount];
                if (barrelRenderers != null)
                {
                    int count = Mathf.Min(HeatedRendererCount, barrelRenderers.Length);
                    for (int index = 0; index < count; index++) resized[index] = barrelRenderers[index];
                }
                barrelRenderers = resized;
            }

            for (int index = 0; index < HeatedRendererCount; index++)
            {
                Renderer renderer = barrelRenderers[index];
                if (renderer == null || runtimeMaterials[index] != null)
                {
                    continue;
                }

                Material source = renderer.sharedMaterial;
                if (source == null)
                {
                    continue;
                }

                int stationIndex = index / HeatedPartsPerGun;
                int partIndex = index % HeatedPartsPerGun;
                Material runtime = new Material(source)
                {
                    name = $"P-51 Wing Gun {stationIndex + 1} Heated Barrel Part {partIndex + 1} Material"
                };
                renderer.material = runtime;
                runtimeMaterials[index] = runtime;
            }
        }

        private void ResolveReferences()
        {
            if (system == null) system = GetComponent<P51WingArmamentSystem>();
        }

        private void ClearRuntimeMaterials()
        {
            for (int index = 0; index < runtimeMaterials.Length; index++)
            {
                if (runtimeMaterials[index] != null)
                {
                    DestroyImmediate(runtimeMaterials[index]);
                    runtimeMaterials[index] = null;
                }
            }
        }

        private void OnDestroy()
        {
            for (int index = 0; index < runtimeMaterials.Length; index++)
            {
                if (runtimeMaterials[index] != null)
                {
                    Destroy(runtimeMaterials[index]);
                }
            }
        }
    }
}
