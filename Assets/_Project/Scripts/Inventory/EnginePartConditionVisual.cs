using System.Collections.Generic;
using UnityEngine;

namespace Hanger51.Inventory
{
    [DisallowMultipleComponent]
    public sealed class EnginePartConditionVisual : MonoBehaviour
    {
        private const string CrackRootName = "Persistent Condition Crack Marks";

        [SerializeField] private EnginePartConditionData condition;

        private readonly List<Material> runtimeMaterials = new List<Material>();
        private MaterialPropertyBlock propertyBlock;

        public EnginePartConditionData Condition => condition;

        public void Configure(EnginePartConditionData configuredCondition)
        {
            condition = configuredCondition != null
                ? configuredCondition.Clone()
                : null;
            condition?.EnsureValid();
            ApplyVisuals();
        }

        public void ApplyVisuals()
        {
            RemoveGeneratedCracks();

            if (condition == null || !condition.IsTracked)
            {
                ClearRendererOverrides();
                return;
            }

            ApplyWearTint();
            if (condition.IsCracked)
            {
                CreateCrackMarks();
            }
        }

        private void ApplyWearTint()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            float health01 = Mathf.Clamp01(condition.Health / 100f);
            Color damageColor;
            switch (condition.Kind)
            {
                case EnginePartConditionKind.SparkPlug:
                    damageColor = new Color(0.12f, 0.075f, 0.035f, 1f);
                    break;
                case EnginePartConditionKind.CylinderCover:
                    damageColor = new Color(0.18f, 0.10f, 0.055f, 1f);
                    break;
                case EnginePartConditionKind.EngineBlock:
                    damageColor = new Color(0.10f, 0.085f, 0.07f, 1f);
                    break;
                default:
                    damageColor = Color.gray;
                    break;
            }

            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null || renderer is LineRenderer)
                {
                    continue;
                }

                Color baseColor = Color.white;
                Material shared = renderer.sharedMaterial;
                if (shared != null)
                {
                    if (shared.HasProperty("_BaseColor"))
                    {
                        baseColor = shared.GetColor("_BaseColor");
                    }
                    else if (shared.HasProperty("_Color"))
                    {
                        baseColor = shared.GetColor("_Color");
                    }
                }

                Color wornColor = Color.Lerp(damageColor, baseColor, health01);
                propertyBlock.Clear();
                renderer.GetPropertyBlock(propertyBlock);
                if (shared != null && shared.HasProperty("_BaseColor"))
                {
                    propertyBlock.SetColor("_BaseColor", wornColor);
                }
                if (shared != null && shared.HasProperty("_Color"))
                {
                    propertyBlock.SetColor("_Color", wornColor);
                }
                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void ClearRendererOverrides()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] != null)
                {
                    renderers[index].SetPropertyBlock(null);
                }
            }
        }

        private void CreateCrackMarks()
        {
            Bounds localBounds = CalculateLocalRendererBounds();
            if (localBounds.size.sqrMagnitude < 0.0001f)
            {
                return;
            }

            GameObject crackRoot = new GameObject(CrackRootName);
            crackRoot.transform.SetParent(transform, false);

            Material crackMaterial = CreateCrackMaterial();
            if (crackMaterial == null)
            {
                Destroy(crackRoot);
                return;
            }

            runtimeMaterials.Add(crackMaterial);
            float width = Mathf.Max(
                0.006f,
                Mathf.Max(localBounds.size.x, localBounds.size.z) * 0.012f);
            float top = localBounds.max.y + Mathf.Max(0.004f, localBounds.size.y * 0.012f);
            Vector3 center = new Vector3(localBounds.center.x, top, localBounds.center.z);

            CreateCrackLine(
                crackRoot.transform,
                crackMaterial,
                width,
                new[]
                {
                    center + new Vector3(-localBounds.extents.x * 0.58f, 0f, -localBounds.extents.z * 0.32f),
                    center + new Vector3(-localBounds.extents.x * 0.18f, 0f, -localBounds.extents.z * 0.08f),
                    center + new Vector3(localBounds.extents.x * 0.08f, 0f, localBounds.extents.z * 0.02f),
                    center + new Vector3(localBounds.extents.x * 0.52f, 0f, localBounds.extents.z * 0.34f)
                });

            CreateCrackLine(
                crackRoot.transform,
                crackMaterial,
                width * 0.82f,
                new[]
                {
                    center + new Vector3(-localBounds.extents.x * 0.18f, 0.001f, -localBounds.extents.z * 0.08f),
                    center + new Vector3(-localBounds.extents.x * 0.36f, 0.001f, localBounds.extents.z * 0.20f),
                    center + new Vector3(-localBounds.extents.x * 0.48f, 0.001f, localBounds.extents.z * 0.46f)
                });

            CreateCrackLine(
                crackRoot.transform,
                crackMaterial,
                width * 0.74f,
                new[]
                {
                    center + new Vector3(localBounds.extents.x * 0.08f, 0.002f, localBounds.extents.z * 0.02f),
                    center + new Vector3(localBounds.extents.x * 0.30f, 0.002f, -localBounds.extents.z * 0.22f),
                    center + new Vector3(localBounds.extents.x * 0.38f, 0.002f, -localBounds.extents.z * 0.44f)
                });
        }

        private static void CreateCrackLine(
            Transform parent,
            Material material,
            float width,
            Vector3[] positions)
        {
            GameObject lineObject = new GameObject("Condition Crack");
            lineObject.transform.SetParent(parent, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = false;
            line.positionCount = positions.Length;
            line.SetPositions(positions);
            line.startWidth = width;
            line.endWidth = width * 0.55f;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.sharedMaterial = material;
            line.startColor = new Color(0.08f, 0.01f, 0.005f, 1f);
            line.endColor = new Color(0.22f, 0.025f, 0.01f, 1f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        private Bounds CalculateLocalRendererBounds()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            bool initialized = false;
            Bounds result = new Bounds(Vector3.zero, Vector3.zero);

            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null
                    || renderer is LineRenderer
                    || renderer.transform.IsChildOf(
                        transform.Find(CrackRootName)))
                {
                    continue;
                }

                Bounds worldBounds = renderer.bounds;
                Vector3 min = worldBounds.min;
                Vector3 max = worldBounds.max;
                for (int x = 0; x <= 1; x++)
                {
                    for (int y = 0; y <= 1; y++)
                    {
                        for (int z = 0; z <= 1; z++)
                        {
                            Vector3 local = transform.InverseTransformPoint(
                                new Vector3(
                                    x == 0 ? min.x : max.x,
                                    y == 0 ? min.y : max.y,
                                    z == 0 ? min.z : max.z));
                            if (!initialized)
                            {
                                result = new Bounds(local, Vector3.zero);
                                initialized = true;
                            }
                            else
                            {
                                result.Encapsulate(local);
                            }
                        }
                    }
                }
            }

            return initialized
                ? result
                : new Bounds(Vector3.zero, Vector3.one * 0.5f);
        }

        private Material CreateCrackMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            if (shader == null)
            {
                return null;
            }

            Material material = new Material(shader)
            {
                name = "Persistent Cracked Cover Material",
                color = new Color(0.10f, 0.008f, 0.004f, 1f)
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", material.color);
            }
            return material;
        }

        private void RemoveGeneratedCracks()
        {
            Transform existing = transform.Find(CrackRootName);
            if (existing != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(existing.gameObject);
                }
                else
                {
                    DestroyImmediate(existing.gameObject);
                }
            }

            for (int index = 0; index < runtimeMaterials.Count; index++)
            {
                if (runtimeMaterials[index] != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(runtimeMaterials[index]);
                    }
                    else
                    {
                        DestroyImmediate(runtimeMaterials[index]);
                    }
                }
            }
            runtimeMaterials.Clear();
        }

        private void OnEnable()
        {
            ApplyVisuals();
        }

        private void OnDestroy()
        {
            RemoveGeneratedCracks();
        }
    }
}
