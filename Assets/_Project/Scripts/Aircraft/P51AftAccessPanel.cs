using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class P51AftAccessPanel : MonoBehaviour
    {
        [SerializeField] private P51AftEquipmentBay bay;
        [SerializeField] private Transform installedAnchor;
        [SerializeField] private bool installed = true;

        private Rigidbody body;
        private Collider interactionCollider;

        public P51AftEquipmentBay Bay => bay;
        public bool IsInstalled => installed;

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
            ResolvePhysics();
            RefreshState();
        }

        public void RemoveFromAircraft()
        {
            installed = false;
            transform.SetParent(null, true);
            RefreshState();
        }

        public void InstallOnAircraft(P51AftEquipmentBay targetBay)
        {
            if (targetBay != null)
            {
                bay = targetBay;
                installedAnchor = targetBay.PanelAnchor;
            }

            installed = true;
            if (installedAnchor != null)
            {
                transform.SetParent(installedAnchor, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
            RefreshState();
        }

        public void SetHeld(bool held)
        {
            ResolvePhysics();
            if (body != null)
            {
                body.isKinematic = held || installed;
                body.useGravity = !held && !installed;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            if (interactionCollider != null)
            {
                interactionCollider.enabled = !held;
            }
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

            if (body != null)
            {
                body.isKinematic = installed;
                body.useGravity = !installed;
                if (installed)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }
            if (interactionCollider != null)
            {
                interactionCollider.enabled = true;
            }
        }

        private void ResolvePhysics()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }
            if (interactionCollider == null)
            {
                interactionCollider = GetComponent<Collider>();
            }
        }
    }
}
