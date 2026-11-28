using UnityEngine;

namespace Rng.Blood
{
    [CreateAssetMenu(fileName = "BloodSettings", menuName = "RNG/Blood Settings")]
    public class BloodSettings : ScriptableObject
    {
        public const string DefaultDropMaterialGuid = "d23556456cc848588f23d1095f7f86e4";
        public const string DefaultStainMaterialGuid = "e137792fe2da476cba8360ca0fc4cafb";

        [field: Header("Rendering")]
        [field: SerializeField, Tooltip("Material for drops in flight. Use the Blood Drop material or a copy of it.")]
        public Material DropMaterial { get; set; }

        [field: SerializeField, Tooltip("Material for stains, pools and footprints. Use the Blood Stain material or a copy of it.")]
        public Material StainMaterial { get; set; }

        [field: SerializeField, Tooltip("High: stains projected onto any surface with bevelled wet edges. Medium: the same projection without the bevel, cheaper to shade. Low: flat stains on the plane they landed on; needs no Depth Texture, best for mobile and low-end hardware.")]
        public BloodQuality Quality { get; set; } = BloodQuality.High;

        [field: SerializeField, Tooltip("Layers drops can land on. Put floors and walls here and keep characters out.")]
        public LayerMask Surfaces { get; set; } = 1;

        [field: SerializeField, Range(0.01f, 0.2f), Tooltip("How high above the surface it landed on a splat still paints, in metres. Splats are projected, so without a limit they would paint anything near the floor: a body lying there, a crate, a chair leg. Low keeps them clean; raise it if splats should climb bumpy ground or the foot of a wall. 0.2 paints as far as the projection reaches.")]
        public float StainRise { get; set; } = 0.05f;

        [field: SerializeField, Tooltip("Colour of fresh blood.")]
        public Color Colour { get; set; } = new(0.42f, 0.018f, 0.015f, 1f);

        [field: SerializeField, Tooltip("Colour used when blood is shown with alternate = true, for a censored mode.")]
        public Color AlternateColour { get; set; } = new(0.35f, 0.9f, 0.1f, 1f);

        [field: SerializeField, Min(1f), Tooltip("Seconds a splat or footprint stays before it is gone.")]
        public float StainLifetime { get; set; } = 30f;

        [field: SerializeField, Tooltip("Drops that hit a renderer whose material name contains this text stain the pane itself. Leave empty to turn off.")]
        public string GlassMaterialKeyword { get; set; } = "Glass";

        [field: Header("Massive")]
        [field: SerializeField, Tooltip("Paints floor splats into one texture instead of keeping each as its own stain, so thousands of them cost the same as one. For large battles and top-down games. Walls, pools, footprints and smears stay as they are. Needs High or Medium quality and the Depth Texture.")]
        public bool Massive { get; set; }

        [field: SerializeField, Min(4f), Tooltip("Side in metres of the square floor area the texture covers. The area follows the camera.")]
        public float MassiveSize { get; set; } = 40f;

        [field: SerializeField, Range(512, 4096), Tooltip("Texture size in pixels, rounded to a power of two. 2048 over 40 m gives about 2 cm per pixel and uses 32 MB.")]
        public int MassiveResolution { get; set; } = 2048;

        [field: SerializeField, Tooltip("Floor blood behaves as one liquid: pools spread, stop where they get thin and merge with each other, and runs from walls drain into them. Uses a texture that follows the camera, with the Massive size. Needs High or Medium quality and the Depth Texture.")]
        public bool Liquid { get; set; }

        [field: SerializeField, Tooltip("Something dragged through liquid on the floor (a Blood Smear) pushes it along and wipes it into streaks, leaving a thinner trail behind it. Needs Liquid.")]
        public bool LiquidDrag { get; set; }

        [field: SerializeField, Range(0.1f, 1f), Tooltip("How much of the liquid under a dragged object moves with it.")]
        public float LiquidDragStrength { get; set; } = 0.6f;

        [field: SerializeField, Tooltip("A smear dragged out of a pool starts as thick, bright and glossy as the pool and thins into a normal smear, so the pool and the trail read as one.")]
        public bool WetSmears { get; set; }

        [field: SerializeField, Min(0.1f), Tooltip("Metres after leaving a pool over which a wet smear turns into a normal one.")]
        public float WetSmearLength { get; set; } = 2.5f;

        [field: Header("Limits")]
        [field: SerializeField, Range(64, 8192), Tooltip("Drops in flight at once. The oldest drop is reused when the limit is reached. Above about 1500 the drops need 32-bit indices, which a few old mobile GPUs lack.")]
        public int MaxDrops { get; set; } = 1024;

        [field: SerializeField, Range(16, 6000), Tooltip("Splats on floors and walls at once. The oldest splat is reused when the limit is reached. Lower it on mobile.")]
        public int MaxSplats { get; set; } = 800;

        [field: SerializeField, Range(4, 500), Tooltip("Pools under bodies and merged puddles at once.")]
        public int MaxPools { get; set; } = 120;

        [field: SerializeField, Range(1, 1000), Tooltip("Footprints at once.")]
        public int MaxFootprints { get; set; } = 150;

        [field: Header("Drying")]
        [field: SerializeField, Range(0f, 1f), Tooltip("Part of the lifetime after which blood starts to dry.")]
        public float DryStart { get; set; } = 0.25f;

        [field: SerializeField, Range(0f, 1f), Tooltip("Part of the lifetime at which blood is fully dry.")]
        public float DryEnd { get; set; } = 0.65f;

        [field: SerializeField, Range(0f, 1f), Tooltip("Part of the lifetime after which a stain fades out.")]
        public float FadeStart { get; set; } = 0.75f;

        [field: SerializeField, Range(0f, 1f), Tooltip("Brightness of dry blood compared to fresh blood.")]
        public float DryBrightness { get; set; } = 0.7f;

        [field: SerializeField, Range(0f, 1f), Tooltip("How far dry blood shifts towards brown.")]
        public float DryBrown { get; set; } = 0.62f;

        [field: SerializeField, Range(0f, 1f), Tooltip("Gloss of dry blood. Fresh blood uses the smoothness of the stain material.")]
        public float DrySmoothness { get; set; } = 0.15f;

        [field: Header("Amount")]
        [field: SerializeField, Min(0.1f), Tooltip("Scales the size of every drop and every splat.")]
        public float DropScale { get; set; } = 1.38f;

        [field: SerializeField, Tooltip("The heavy gout every hit adds on top of its own burst.")]
        public BloodBurst Gout { get; set; } = new(1, 2, 34f, 1.5f, 3.6f, 0.034f, 0.055f, 7f, 0.1f, BloodStainStyle.Heavy);

        [field: Header("Pace")]
        [field: SerializeField, Range(0.5f, 2f), Tooltip("Speeds up or slows down the whole flight without changing where drops land.")]
        public float FlightPace { get; set; } = 1.25f;

        [field: SerializeField, Range(0.5f, 2f), Tooltip("Scales the launch speed of every burst.")]
        public float DropSpeed { get; set; } = 1.4f;

        [field: SerializeField, Range(0.5f, 2f), Tooltip("Scales how far drops fly.")]
        public float DropReach { get; set; } = 1.5f;

        [field: SerializeField, Range(0.5f, 2f), Tooltip("Scales the cone of every burst.")]
        public float DropSpread { get; set; } = 1.25f;

        [field: Header("Stains That Run Together")]
        [field: SerializeField, Min(0f), Tooltip("Extra gap in metres across which fresh splats still merge.")]
        public float MergeGap { get; set; } = 0.08f;

        [field: SerializeField, Range(0.1f, 1.5f), Tooltip("How much two splats must overlap to merge.")]
        public float MergeOverlap { get; set; } = 0.65f;

        [field: SerializeField, Min(0.1f), Tooltip("How large a puddle grows for the blood that ran into it.")]
        public float MergeSpread { get; set; } = 0.78f;

        [field: SerializeField, Min(0.1f), Tooltip("Largest radius in metres a merged puddle can reach.")]
        public float MergeRadiusMax { get; set; } = 1.1f;

        [field: SerializeField, Min(0.05f), Tooltip("Seconds a puddle takes to grow to its new size.")]
        public float MergeGrowSeconds { get; set; } = 0.7f;

        [field: SerializeField, Range(0f, 1f), Tooltip("Splats drier than this no longer merge.")]
        public float MergeDryness { get; set; } = 0.3f;

        [field: Header("Walls")]
        [field: SerializeField, Min(0f), Tooltip("Splats on walls at least this large (half width in metres) run down. Smaller ones stay as spatter. 0 makes every splat run.")]
        public float WallRunSize { get; set; } = 0.28f;

        [field: Header("Body")]
        [field: SerializeField, Min(0f), Tooltip("Height above the feet that deaths and explosions spray from.")]
        public float ChestHeight { get; set; } = 1.1f;

        [field: Header("Pool Under A Body")]
        [field: SerializeField, Min(0f), Tooltip("Seconds after a death before the pool starts to grow.")]
        public float PoolDelay { get; set; } = 0.9f;

        [field: SerializeField, Min(0.1f), Tooltip("Seconds a pool takes to reach its full size.")]
        public float PoolGrowSeconds { get; set; } = 3.8f;

        [field: SerializeField, Min(1f), Tooltip("Seconds a pool stays while its body exists. When the body is destroyed the pool fades out.")]
        public float PoolSeconds { get; set; } = 123f;

        [field: SerializeField, Min(0f), Tooltip("Pool radius in metres for bullet and shotgun deaths.")]
        public float PoolRadiusBullet { get; set; } = 1.3f;

        [field: SerializeField, Min(0f), Tooltip("Pool radius in metres for headshot deaths.")]
        public float PoolRadiusHeadshot { get; set; } = 1.8f;

        [field: SerializeField, Min(0f), Tooltip("Pool radius in metres for blade deaths.")]
        public float PoolRadiusSharp { get; set; } = 1.7f;

        [field: SerializeField, Min(0f), Tooltip("Pool radius in metres for blunt deaths.")]
        public float PoolRadiusBlunt { get; set; } = 0.7f;

        [field: SerializeField, Min(0f), Tooltip("Pool radius in metres for explosion deaths.")]
        public float PoolRadiusExplosion { get; set; } = 1.4f;

        [field: Header("Wounds")]
        [field: SerializeField, Tooltip("The burst Blood.Drip lets fall from a wound.")]
        public BloodBurst WoundDrip { get; set; } = new(1, 1, 18f, 0.2f, 0.7f, 0.01f, 0.02f, 5f, 0f, BloodStainStyle.Rays);

        [field: Header("Footprints")]
        [field: SerializeField, Min(0), Tooltip("Prints a walker leaves after stepping into a wet pool. 0 turns footprints off.")]
        public int PrintCount { get; set; } = 8;

        [field: SerializeField, Min(0.05f), Tooltip("Length of one print in metres.")]
        public float PrintLength { get; set; } = 0.5f;

        [field: SerializeField, Min(0f), Tooltip("Distance of each foot from the walker's centre line.")]
        public float PrintSide { get; set; } = 0.16f;

        [field: SerializeField, Min(0f), Tooltip("How close to the edge of a pool a walker soaks its feet.")]
        public float PrintReach { get; set; } = 0.25f;

        [field: SerializeField, Min(0.1f), Tooltip("Distance a walker covers between two pairs of prints.")]
        public float StrideLength { get; set; } = 1.15f;

        [field: Header("Bursts")]
        [field: SerializeField, Tooltip("BloodHitKind.Bullet.")]
        public BloodBurst BulletFar { get; set; } = new(6, 10, 14f, 4f, 7f, 0.012f, 0.026f, 9f, 0.1f);

        [field: SerializeField, Tooltip("BloodHitKind.BulletClose, together with Back Spatter and Mist.")]
        public BloodBurst BulletClose { get; set; } = new(14, 20, 24f, 5f, 9f, 0.012f, 0.03f, 9f, 0.1f);

        [field: SerializeField, Tooltip("Spray back against the shot on close bullet hits.")]
        public BloodBurst BackSpatter { get; set; } = new(5, 8, 38f, 2f, 4f, 0.01f, 0.022f, 8f, 0.2f);

        [field: SerializeField, Tooltip("BloodHitKind.Shotgun, together with Mist.")]
        public BloodBurst Shotgun { get; set; } = new(20, 30, 40f, 4f, 8f, 0.009f, 0.02f, 8f, 0.1f);

        [field: SerializeField, Tooltip("BloodHitKind.Headshot, together with Mist.")]
        public BloodBurst Headshot { get; set; } = new(25, 40, 60f, 3f, 7f, 0.014f, 0.045f, 9f, 0.9f, BloodStainStyle.Splash, BloodStainStyle.Rays, 0.55f, 1.25f);

        [field: SerializeField, Tooltip("BloodHitKind.Blunt.")]
        public BloodBurst Blunt { get; set; } = new(3, 6, 26f, 2f, 4f, 0.026f, 0.042f, 8f, -0.2f, BloodStainStyle.Heavy);

        [field: SerializeField, Tooltip("BloodHitKind.Sharp.")]
        public BloodBurst Sharp { get; set; } = new(8, 12, 9f, 4f, 7f, 0.012f, 0.024f, 9f, 0.15f, BloodStainStyle.Splash, BloodStainStyle.Streak, 0.16f, 5.5f);

        [field: SerializeField, Tooltip("BloodHitKind.Explosion, sprayed from chest height.")]
        public BloodBurst Explosion { get; set; } = new(20, 30, 50f, 4f, 9f, 0.012f, 0.035f, 9f, 0.5f, BloodStainStyle.Splash, BloodStainStyle.Rays, 0.75f, 1.7f);

        [field: SerializeField, Tooltip("The spray of Blood.Death, from chest height.")]
        public BloodBurst Death { get; set; } = new(14, 22, 45f, 2.5f, 5f, 0.016f, 0.04f, 10f, 0.3f);

        [field: SerializeField, Tooltip("Fine mist that vanishes in the air and leaves no stains.")]
        public BloodBurst Mist { get; set; } = new(14, 22, 34f, 3f, 8f, 0.004f, 0.008f, 0f, 0.15f);

        public float PoolRadius(BloodHitKind kind)
        {
            return kind switch
            {
                BloodHitKind.Explosion => PoolRadiusExplosion,
                BloodHitKind.Headshot => PoolRadiusHeadshot,
                BloodHitKind.Blunt => PoolRadiusBlunt,
                BloodHitKind.Sharp => PoolRadiusSharp,
                _ => PoolRadiusBullet
            };
        }

#if UNITY_EDITOR
        private void Reset()
        {
            DropMaterial = Load(DefaultDropMaterialGuid);
            StainMaterial = Load(DefaultStainMaterialGuid);
        }

        private static Material Load(string guid)
        {
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
        }
#endif
    }
}
