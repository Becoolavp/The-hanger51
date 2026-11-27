using UnityEngine;

namespace Rng.Blood
{
    [AddComponentMenu("RNG/Blood Footprints")]
    public class BloodFootprints : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("Slower than this in metres per second counts as standing still.")] private float _minimumSpeed = 0.8f;
        [SerializeField, Min(0.1f), Tooltip("A jump longer than this in one frame is a teleport and leaves no prints.")] private float _teleportDistance = 4f;

        private Vector3 _last;
        private float _travelled;
        private bool _tracking;

        private void OnEnable()
        {
            _tracking = false;
            _travelled = 0f;
        }

        private void OnDisable()
        {
            Blood.Forget(this);
        }

        private void Update()
        {
            Track(Time.deltaTime);
        }

        public void Track(float deltaTime)
        {
            Vector3 position = transform.position;
            if (!_tracking)
            {
                _last = position;
                _tracking = true;
                return;
            }

            if (deltaTime <= 0f) return;

            float dx = position.x - _last.x;
            float dz = position.z - _last.z;
            float moved = Mathf.Sqrt(dx * dx + dz * dz);
            _last = position;

            if (moved > _teleportDistance || moved / deltaTime < _minimumSpeed)
            {
                _travelled = 0f;
                return;
            }

            BloodSettings settings = Blood.Settings;
            if (settings == null) return;

            _travelled += moved;
            bool stride = _travelled >= settings.StrideLength;
            if (moved > 0.0001f) Blood.Walk(this, position, new Vector3(dx / moved, 0f, dz / moved), stride);
            if (stride) _travelled -= settings.StrideLength;
        }
    }
}
