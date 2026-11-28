using System;
using UnityEngine;

namespace Rng.Blood
{
    [Serializable]
    public class BloodBurst
    {
        [field: SerializeField, Min(0), Tooltip("Fewest drops in one burst.")] public int CountMin { get; set; }
        [field: SerializeField, Min(0), Tooltip("Most drops in one burst.")] public int CountMax { get; set; }
        [field: SerializeField, Range(0f, 180f), Tooltip("Half angle of the cone drops fly in, around the hit direction.")] public float ConeDegrees { get; set; }
        [field: SerializeField, Min(0f), Tooltip("Slowest launch speed in metres per second.")] public float SpeedMin { get; set; }
        [field: SerializeField, Min(0f), Tooltip("Fastest launch speed in metres per second.")] public float SpeedMax { get; set; }
        [field: SerializeField, Min(0f), Tooltip("Smallest drop radius in metres.")] public float SizeMin { get; set; }
        [field: SerializeField, Min(0f), Tooltip("Largest drop radius in metres.")] public float SizeMax { get; set; }
        [field: SerializeField, Min(0f), Tooltip("Splat size on landing, as a multiple of the drop size. 0 means the drops vanish in the air.")] public float StainScale { get; set; }
        [field: SerializeField, Range(-1f, 1f), Tooltip("Tilts the burst up (positive) or down (negative).")] public float Lift { get; set; }
        [field: SerializeField, Tooltip("Shape of the splats the drops leave.")] public BloodStainStyle Style { get; set; }
        [field: SerializeField, Tooltip("Shape of the extra mark left on the floor under a full-strength hit.")] public BloodStainStyle MarkStyle { get; set; }
        [field: SerializeField, Min(0f), Tooltip("Half width of that floor mark in metres. 0 leaves no mark.")] public float MarkSize { get; set; }
        [field: SerializeField, Min(1f), Tooltip("How long the floor mark is along the hit direction.")] public float MarkStretch { get; set; } = 1f;

        public BloodBurst()
        {
        }

        public BloodBurst(int countMin, int countMax, float coneDegrees, float speedMin, float speedMax, float sizeMin, float sizeMax, float stainScale, float lift,
            BloodStainStyle style = BloodStainStyle.Splash, BloodStainStyle markStyle = BloodStainStyle.Splash, float markSize = 0f, float markStretch = 1f)
        {
            CountMin = countMin;
            CountMax = countMax;
            ConeDegrees = coneDegrees;
            SpeedMin = speedMin;
            SpeedMax = speedMax;
            SizeMin = sizeMin;
            SizeMax = sizeMax;
            StainScale = stainScale;
            Lift = lift;
            Style = style;
            MarkStyle = markStyle;
            MarkSize = markSize;
            MarkStretch = markStretch;
        }
    }
}
