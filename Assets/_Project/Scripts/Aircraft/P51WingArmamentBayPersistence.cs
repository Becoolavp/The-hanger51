using UnityEngine;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    public sealed class P51WingArmamentBayPersistence : MonoBehaviour
    {
        [SerializeField] private GameObject[] bayInteriorRoots = new GameObject[2];

        public void Configure(GameObject[] configuredBayInteriorRoots)
        {
            bayInteriorRoots = new GameObject[2];
            if (configuredBayInteriorRoots == null) return;

            int count = Mathf.Min(bayInteriorRoots.Length, configuredBayInteriorRoots.Length);
            for (int index = 0; index < count; index++)
            {
                bayInteriorRoots[index] = configuredBayInteriorRoots[index];
            }
            EnsureVisible();
        }

        private void Awake()
        {
            EnsureVisible();
        }

        private void OnEnable()
        {
            EnsureVisible();
        }

        private void LateUpdate()
        {
            // The original armament animation hid the whole bay when a panel closed.
            // Keeping the bay active lets the closed skin physically conceal the receiver/ammo,
            // while the gun barrels can remain visible through the wing leading edge.
            EnsureVisible();
        }

        private void EnsureVisible()
        {
            if (bayInteriorRoots == null) return;
            for (int index = 0; index < bayInteriorRoots.Length; index++)
            {
                GameObject root = bayInteriorRoots[index];
                if (root != null && !root.activeSelf)
                {
                    root.SetActive(true);
                }
            }
        }
    }
}
