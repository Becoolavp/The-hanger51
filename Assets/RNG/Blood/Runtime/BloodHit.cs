using UnityEngine;

namespace Rng.Blood
{
    public enum BloodHitKind
    {
        Bullet,
        BulletClose,
        Shotgun,
        Headshot,
        Blunt,
        Sharp,
        Explosion
    }

    public readonly struct BloodHit
    {
        public readonly Vector3 Position;
        public readonly Vector3 Normal;
        public readonly Vector3 Direction;
        public readonly float Force;
        public readonly BloodHitKind Kind;
        public readonly Transform Bone;

        public BloodHit(Vector3 position, Vector3 normal, Vector3 direction, float force, BloodHitKind kind, Transform bone)
        {
            Position = position;
            Normal = normal;
            Direction = direction;
            Force = force;
            Kind = kind;
            Bone = bone;
        }
    }
}
