using UnityEngine;

namespace Rng.Blood
{
    [AddComponentMenu("RNG/Blood Emitter")]
    public class BloodEmitter : MonoBehaviour
    {
        [SerializeField, Tooltip("Preset used by Spawn and by the pool size of Death.")] private BloodHitKind _kind = BloodHitKind.Bullet;
        [SerializeField, Range(0f, 2f), Tooltip("Scales the count and speed of Spawn.")] private float _intensity = 1f;
        [SerializeField, Tooltip("The body a pool grows under and follows after Death. Empty uses the root of this object.")] private Transform _body;
        [SerializeField, Tooltip("Where the feet are for Death. Empty uses the root of this object.")] private Transform _feet;
        [SerializeField, Min(0f), Tooltip("Scales the pool left by Death. 0 leaves no pool.")] private float _poolScale = 1f;

        public BloodHitKind Kind
        {
            get => _kind;
            set => _kind = value;
        }

        public float Intensity
        {
            get => _intensity;
            set => _intensity = value;
        }

        private Transform Body => _body != null ? _body : transform.root;
        private Vector3 Feet => _feet != null ? _feet.position : transform.root.position;

        public void Spawn()
        {
            Blood.Spawn(transform.position, -transform.forward, transform.forward, _intensity, _kind, transform);
        }

        public void SpawnKind(int kind)
        {
            Blood.Spawn(transform.position, -transform.forward, transform.forward, _intensity, (BloodHitKind)kind, transform);
        }

        public void Death()
        {
            Blood.Death(Feet, transform.forward, Body, _kind, _poolScale);
        }

        public void Drip()
        {
            Blood.Drip(transform.position);
        }
    }
}
