using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    public sealed class P51BulletStreakMover : MonoBehaviour
    {
        private LineRenderer line;
        private Vector3 start;
        private Vector3 direction;
        private float speed;
        private float maxDistance;
        private float streakLength;
        private float travelled;

        public void Configure(
            LineRenderer configuredLine,
            Vector3 configuredStart,
            Vector3 configuredVelocity,
            float configuredMaxDistance,
            float configuredStreakLength)
        {
            line = configuredLine;
            start = configuredStart;
            speed = Mathf.Max(1f, configuredVelocity.magnitude);
            direction = configuredVelocity.sqrMagnitude > 0.0001f
                ? configuredVelocity.normalized
                : transform.forward;
            maxDistance = Mathf.Max(0.25f, configuredMaxDistance);
            streakLength = Mathf.Clamp(configuredStreakLength, 0.25f, maxDistance);
            travelled = 0f;
            UpdateLine();
        }

        private void Update()
        {
            if (line == null)
            {
                Destroy(gameObject);
                return;
            }

            travelled += speed * Time.deltaTime;
            UpdateLine();

            if (travelled >= maxDistance)
            {
                Destroy(gameObject);
            }
        }

        private void UpdateLine()
        {
            float headDistance = Mathf.Min(travelled, maxDistance);
            float tailDistance = Mathf.Max(0f, headDistance - streakLength);
            line.SetPosition(0, start + direction * tailDistance);
            line.SetPosition(1, start + direction * headDistance);
        }
    }
}
