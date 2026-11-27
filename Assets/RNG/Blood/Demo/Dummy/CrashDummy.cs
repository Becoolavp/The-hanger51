using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Rng.Blood.Demo
{
    public class CrashDummy : MonoBehaviour
    {
        public enum Region
        {
            Head,
            Torso,
            Arms,
            Legs
        }

        [Serializable]
        public class Joint
        {
            public Transform Bone;
            [Min(1f)] public float Stiffness = 60f;
            [Min(0f)] public float Damping = 9f;
            [Range(1f, 90f)] public float Limit = 35f;
            [Range(0f, 2f)] public float Response = 1f;
        }

        [Serializable]
        public struct Part
        {
            public Transform Bone;
            public Region Region;
            public Vector3 Centre;
            [Min(0.01f)] public float Radius;
        }

        private const float SubStep = 1f / 120f;
        private const float Gain = 1200f;

        [SerializeField, Tooltip("Joints that react to hits, from the hips up.")] private Joint[] _joints;
        [SerializeField, Tooltip("Body parts a hit can land on.")] private Part[] _parts;
        [SerializeField, Tooltip("The joint that breathes while the dummy stands still.")] private Transform _chest;
        [SerializeField, Min(0f), Tooltip("Scales how hard hits throw the dummy around.")] private float _force = 1f;

        private Quaternion[] _rest;
        private Vector3[] _bend;
        private Vector3[] _spin;
        private float _breath;
        private float _carry;

        public Part[] Parts => _parts;

        private void Awake()
        {
            Capture();
        }

        private void LateUpdate()
        {
            Step(Time.deltaTime);
        }

        public void Step(float deltaTime)
        {
            if (_rest == null) Capture();

            _carry += deltaTime;
            while (_carry >= SubStep)
            {
                _carry -= SubStep;
                for (int i = 0; i < _joints.Length; i++)
                {
                    Joint joint = _joints[i];
                    _spin[i] += (-joint.Stiffness * _bend[i] - joint.Damping * _spin[i]) * SubStep;
                    _bend[i] += _spin[i] * SubStep;
                    if (_bend[i].magnitude > joint.Limit)
                    {
                        _bend[i] = _bend[i].normalized * joint.Limit;
                        _spin[i] *= 0.5f;
                    }
                }
            }

            _breath += deltaTime;
            for (int i = 0; i < _joints.Length; i++)
            {
                Vector3 bend = _bend[i];
                Quaternion pose = bend.sqrMagnitude > 0.000001f ? Quaternion.AngleAxis(bend.magnitude, bend.normalized) : Quaternion.identity;
                Quaternion rest = _joints[i].Bone == _chest ? _rest[i] * Quaternion.Euler(Mathf.Sin(_breath * 1.6f) * 1.6f, 0f, 0f) : _rest[i];
                _joints[i].Bone.localRotation = rest * pose;
            }
        }

        public Transform Take(BloodHitKind kind, Vector3 direction, float intensity = 1f)
        {
            if (_parts == null || _parts.Length == 0) return null;

            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : -transform.forward;
            Region region = Pick(kind);
            Part part = _parts[Random.Range(0, _parts.Length)];
            for (int tries = 0; tries < 12 && part.Region != region; tries++) part = _parts[Random.Range(0, _parts.Length)];

            Vector3 centre = part.Bone.TransformPoint(part.Centre);
            Vector3 entry = centre - direction * part.Radius;
            float strength = kind switch
            {
                BloodHitKind.Explosion => 3f,
                BloodHitKind.Shotgun => 1.8f,
                BloodHitKind.Blunt => 1.6f,
                BloodHitKind.Headshot => 1.4f,
                BloodHitKind.BulletClose => 1.2f,
                _ => 1f
            };

            if (kind == BloodHitKind.Explosion)
            {
                Push(transform.position + Vector3.up * 0.4f - direction * 0.3f, (direction + Vector3.up * 0.6f) * strength * intensity);
                Blood.Spawn(transform.position, Vector3.up, direction, intensity, kind, part.Bone);
            }
            else
            {
                Push(entry, direction * strength * intensity);
                Blood.Spawn(centre + direction * (part.Radius * 0.6f), -direction, direction, intensity, kind, part.Bone);
                if (TryGetComponent(out BloodWounds wounds)) wounds.Add(entry, kind, intensity, part.Bone);
            }

            return part.Bone;
        }

        public void Push(Vector3 point, Vector3 impulse)
        {
            if (_rest == null) Capture();

            for (int i = 0; i < _joints.Length; i++)
            {
                Joint joint = _joints[i];
                Vector3 lever = point - joint.Bone.position;
                float weight = Mathf.Exp(-lever.magnitude * 2.2f) * joint.Response;
                Vector3 torque = Vector3.Cross(lever, impulse * _force) * weight;
                _spin[i] += Quaternion.Inverse(joint.Bone.rotation) * torque * Gain;
            }
        }

        private static Region Pick(BloodHitKind kind)
        {
            float roll = Random.value;
            return kind switch
            {
                BloodHitKind.Headshot => Region.Head,
                BloodHitKind.Blunt => roll < 0.6f ? Region.Head : Region.Torso,
                BloodHitKind.Explosion => Region.Torso,
                _ => roll < 0.15f ? Region.Head : roll < 0.3f ? Region.Arms : Region.Torso
            };
        }

        private void Capture()
        {
            _rest = new Quaternion[_joints.Length];
            _bend = new Vector3[_joints.Length];
            _spin = new Vector3[_joints.Length];
            for (int i = 0; i < _joints.Length; i++) _rest[i] = _joints[i].Bone.localRotation;
        }
    }
}
