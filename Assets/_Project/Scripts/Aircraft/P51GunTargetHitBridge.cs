using System;
using UnityEngine;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(395)]
    [DisallowMultipleComponent]
    public sealed class P51GunTargetHitBridge : MonoBehaviour
    {
        private const int GunCount = 6;

        [SerializeField] private P51WingArmamentSystem system;
        [SerializeField] private Transform[] muzzles = new Transform[GunCount];
        [SerializeField, Min(10f)] private float rangeMeters = 850f;
        [SerializeField, Min(1f)] private float damagePerRound = 34f;

        private readonly int[] previousAmmo = new int[GunCount];
        private bool initialized;

        public bool IsConfigured => system != null && muzzles != null && muzzles.Length == GunCount;

        public void Configure(P51WingArmamentSystem configuredSystem, Transform[] configuredMuzzles)
        {
            system = configuredSystem;
            muzzles = new Transform[GunCount];
            if (configuredMuzzles != null)
            {
                Array.Copy(configuredMuzzles, muzzles, Mathf.Min(GunCount, configuredMuzzles.Length));
            }
            initialized = false;
        }

        private void Awake()
        {
            if (system == null) system = GetComponent<P51WingArmamentSystem>();
            CaptureAmmo();
        }

        private void OnEnable()
        {
            if (system == null) system = GetComponent<P51WingArmamentSystem>();
            CaptureAmmo();
        }

        private void Update()
        {
            if (system == null) return;
            if (!initialized) CaptureAmmo();

            for (int station = 0; station < GunCount; station++)
            {
                int current = system.GetAmmoRemaining(station);
                int fired = Mathf.Max(0, previousAmmo[station] - current);
                if (fired > 0)
                {
                    Transform muzzle = station < muzzles.Length ? muzzles[station] : null;
                    if (muzzle != null)
                    {
                        ResolveShot(muzzle, fired);
                    }
                }
                previousAmmo[station] = current;
            }
        }

        private void ResolveShot(Transform muzzle, int rounds)
        {
            Vector3 start = muzzle.position + muzzle.forward * 0.15f;
            RaycastHit[] hits = Physics.RaycastAll(
                start,
                muzzle.forward,
                rangeMeters,
                ~0,
                QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return;

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int index = 0; index < hits.Length; index++)
            {
                Collider collider = hits[index].collider;
                if (collider == null || collider.transform.IsChildOf(transform)) continue;

                P51GunTestTarget target = collider.GetComponentInParent<P51GunTestTarget>();
                if (target != null)
                {
                    target.RegisterHit(
                        hits[index].point,
                        muzzle.forward,
                        damagePerRound * Mathf.Max(1, rounds));
                }
                break;
            }
        }

        private void CaptureAmmo()
        {
            if (system == null) return;
            for (int index = 0; index < GunCount; index++)
            {
                previousAmmo[index] = system.GetAmmoRemaining(index);
            }
            initialized = true;
        }
    }
}
