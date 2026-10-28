using UnityEngine;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(120)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(P51LandingGearMaintenanceController))]
    public sealed class P51TireWearVisualController : MonoBehaviour
    {
        [SerializeField] private P51LandingGearMaintenanceController maintenance;
        [SerializeField] private Transform[] tireRoots = new Transform[3];
        [SerializeField] private Transform[] valveTargets = new Transform[3];
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.15f;

        private MaterialPropertyBlock propertyBlock;
        private float nextRefreshTime;

        public void Configure(
            P51LandingGearMaintenanceController configuredMaintenance,
            Transform[] configuredTireRoots,
            Transform[] configuredValveTargets)
        {
            maintenance = configuredMaintenance;
            tireRoots = Copy(configuredTireRoots);
            valveTargets = Copy(configuredValveTargets);
            ApplyVisuals();
        }

        private void Awake()
        {
            ResolveReferences();
            ApplyVisuals();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ApplyVisuals();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
            ResolveReferences();
            ApplyVisuals();
        }

        private void ApplyVisuals()
        {
            if (maintenance == null)
            {
                return;
            }

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            for (int wheelIndex = 0; wheelIndex < 3; wheelIndex++)
            {
                float health01 = Mathf.Clamp01(maintenance.GetTireHealth(wheelIndex) / 100f);
                bool failed = maintenance.IsTireFailed(wheelIndex);
                Transform tireRoot = wheelIndex < tireRoots.Length ? tireRoots[wheelIndex] : null;
                if (tireRoot != null)
                {
                    Renderer[] renderers = tireRoot.GetComponentsInChildren<Renderer>(true);
                    for (int index = 0; index < renderers.Length; index++)
                    {
                        Renderer renderer = renderers[index];
                        if (renderer == null)
                        {
                            continue;
                        }

                        Material material = renderer.sharedMaterial;
                        Color baseColor = new Color(0.06f, 0.06f, 0.06f, 1f);
                        if (material != null)
                        {
                            if (material.HasProperty("_BaseColor"))
                            {
                                baseColor = material.GetColor("_BaseColor");
                            }
                            else if (material.HasProperty("_Color"))
                            {
                                baseColor = material.GetColor("_Color");
                            }
                        }

                        Color badlyWorn = failed
                            ? new Color(0.22f, 0.105f, 0.055f, 1f)
                            : new Color(0.17f, 0.14f, 0.11f, 1f);
                        Color visibleColor = Color.Lerp(badlyWorn, baseColor, health01);
                        propertyBlock.Clear();
                        renderer.GetPropertyBlock(propertyBlock);
                        if (material != null && material.HasProperty("_BaseColor"))
                        {
                            propertyBlock.SetColor("_BaseColor", visibleColor);
                        }
                        if (material != null && material.HasProperty("_Color"))
                        {
                            propertyBlock.SetColor("_Color", visibleColor);
                        }
                        renderer.SetPropertyBlock(propertyBlock);
                    }
                }

                Transform valve = wheelIndex < valveTargets.Length ? valveTargets[wheelIndex] : null;
                if (valve != null)
                {
                    bool showValve = maintenance.IsGearInstalled(wheelIndex)
                        && maintenance.IsTireInstalled(wheelIndex)
                        && maintenance.DeploymentFraction >= 0.94f;
                    Renderer[] valveRenderers = valve.GetComponentsInChildren<Renderer>(true);
                    for (int index = 0; index < valveRenderers.Length; index++)
                    {
                        if (valveRenderers[index] != null)
                        {
                            valveRenderers[index].enabled = showValve;
                        }
                    }
                }
            }
        }

        private void ResolveReferences()
        {
            if (maintenance == null)
            {
                maintenance = GetComponent<P51LandingGearMaintenanceController>();
            }
            tireRoots = Resize(tireRoots);
            valveTargets = Resize(valveTargets);
        }

        private static Transform[] Copy(Transform[] source)
        {
            Transform[] result = new Transform[3];
            if (source != null)
            {
                System.Array.Copy(source, result, Mathf.Min(source.Length, result.Length));
            }
            return result;
        }

        private static Transform[] Resize(Transform[] source)
        {
            return source != null && source.Length == 3 ? source : Copy(source);
        }

        private void OnValidate()
        {
            refreshInterval = Mathf.Max(0.05f, refreshInterval);
            ResolveReferences();
        }
    }
}
