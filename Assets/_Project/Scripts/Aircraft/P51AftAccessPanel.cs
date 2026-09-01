using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    public sealed class P51AftAccessPanel : MonoBehaviour
    {
        [SerializeField] private P51AftEquipmentBay bay;
        [SerializeField] private Transform installedAnchor;
        [SerializeField] private bool installed = true;

        private Rigidbody body;
        private bool held;

        public P51AftEquipmentBay Bay => bay;
        public bool IsInstalled => installed;
        public int SecuredFastenerCount
        {
            get
            {
                P51AftPanelFastener[] fasteners = GetComponentsInChildren<P51AftPanelFastener>(true);
                int count = 0;
                for (int i = 0; i < fasteners.Length; i++)
                {
                    if (fasteners[i] != null && fasteners[i].IsSecured)
                    {
                        count++;
                    }
                }
                return count;
            }
        }
        public int FastenerCount => GetComponentsInChildren<P51AftPanelFastener>(true).Length;
        public bool CanRemove => installed && SecuredFastenerCount == 0;

        private void Awake()
        {
            ResolvePhysics();
            RefreshState();
        }

        private void OnEnable()
        {
            ResolvePhysics();
            RefreshState();
        }

        public void Configure(P51AftEquipmentBay configuredBay, Transform configuredAnchor, bool startsInstalled)
        {
            bay = configuredBay;
            installedAnchor = configuredAnchor;
            installed = startsInstalled;
            held = false;
            ResolvePhysics();
            RefreshState();
        }

        public bool TryRemoveFromAircraft(out string message)
        {
            message = string.Empty;
            if (!installed)
            {
                message = "The aft access panel is already removed.";
                return false;
            }
            int remaining = SecuredFastenerCount;
            if (remaining > 0)
            {
                message = $"Release the {remaining} remaining aft-panel fastener{(remaining == 1 ? string.Empty : "s")} first.";
                return false;
            }

            installed = false;
            transform.SetParent(null, true);
            RefreshState();
            message = "Aft equipment bay opened.";
            return true;
        }

        // Compatibility for older callers. Fasteners still gate the removal.
        public void RemoveFromAircraft()
        {
            TryRemoveFromAircraft(out _);
        }

        public void InstallOnAircraft(P51AftEquipmentBay targetBay)
        {
            if (targetBay != null)
            {
                bay = targetBay;
                installedAnchor = targetBay.PanelAnchor;
            }

            installed = true;
            held = false;
            if (installedAnchor != null)
            {
                transform.SetParent(installedAnchor, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
            RefreshState();
        }

        public void SetHeld(bool isHeld)
        {
            held = isHeld;
            ResolvePhysics();
            RefreshState();
        }

        private void RefreshState()
        {
            ResolvePhysics();
            if (installed && installedAnchor != null)
            {
                transform.SetParent(installedAnchor, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }

            // This panel is a hand-serviced part, not a physics projectile. Keeping it kinematic
            // and using trigger-only interaction colliders prevents a removed panel from shoving
            // the CharacterController or launching the player while it is picked up.
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                {
                    continue;
                }
                MeshCollider mesh = collider as MeshCollider;
                if (mesh != null)
                {
                    mesh.convex = true;
                }
                collider.isTrigger = true;
                collider.enabled = !held;
            }
        }

        private void ResolvePhysics()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
            }
        }
    }
}
