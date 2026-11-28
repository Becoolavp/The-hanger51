using UnityEngine;

namespace Hanger51.NPCs
{
    public sealed class H51CitizenVisualVariation : MonoBehaviour
    {
        [SerializeField] int seed;
        [SerializeField, Range(.9f,1.1f)] float heightScale = 1f;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        public void Configure(int appearanceSeed, float scale)
        {
            seed = appearanceSeed;
            heightScale = Mathf.Clamp(scale, .90f, 1.10f);
        }

        void Awake()
        {
            transform.localScale *= heightScale;
            ApplyColors();
        }

        public void ApplyColors()
        {
            System.Random rng = new System.Random(seed);
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer renderer = renderers[r];
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material m = materials[i];
                    if (!m) continue;
                    string n = m.name.ToLowerInvariant();

                    Color source = m.HasProperty(BaseColor) ? m.GetColor(BaseColor) :
                                   m.HasProperty(ColorId) ? m.GetColor(ColorId) : Color.white;
                    Color target = source;

                    if (n.Contains("skin"))
                    {
                        Color[] skin = {
                            new Color(.95f,.73f,.58f), new Color(.82f,.58f,.42f),
                            new Color(.64f,.42f,.28f), new Color(.43f,.28f,.20f),
                            new Color(.98f,.82f,.68f)
                        };
                        target = skin[Mathf.Abs(seed + i) % skin.Length];
                    }
                    else if (n.Contains("hair"))
                    {
                        Color[] hair = {
                            new Color(.08f,.055f,.04f), new Color(.18f,.10f,.055f),
                            new Color(.40f,.25f,.10f), new Color(.72f,.58f,.30f),
                            new Color(.30f,.12f,.06f)
                        };
                        target = hair[Mathf.Abs(seed * 3 + i) % hair.Length];
                    }
                    else if (!n.Contains("eye") && !n.Contains("teeth") && !n.Contains("shoe") && !n.Contains("black"))
                    {
                        Color.RGBToHSV(source, out float h, out float s, out float v);
                        h = Mathf.Repeat(h + ((float)rng.NextDouble() - .5f) * .16f, 1f);
                        s = Mathf.Clamp01(s * Mathf.Lerp(.75f, 1.18f, (float)rng.NextDouble()));
                        v = Mathf.Clamp01(v * Mathf.Lerp(.78f, 1.08f, (float)rng.NextDouble()));
                        target = Color.HSVToRGB(h, s, v);
                    }

                    MaterialPropertyBlock block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block, i);
                    block.SetColor(BaseColor, target);
                    block.SetColor(ColorId, target);
                    renderer.SetPropertyBlock(block, i);
                }
            }
        }
    }
}
