using UnityEngine;

namespace Hanger51.NPCs
{
    [DisallowMultipleComponent]
    public sealed class H51TownDoor : MonoBehaviour
    {
        [SerializeField] Vector3 closedLocalPosition;
        [SerializeField] Vector3 openLocalPosition;
        [SerializeField] Quaternion closedLocalRotation = Quaternion.identity;
        [SerializeField] Quaternion openLocalRotation = Quaternion.identity;
        [SerializeField] bool manualOpen = true;
        [SerializeField] bool autoOpenForCitizens = true;
        [SerializeField, Min(.1f)] float transitionSpeed = 4.5f;
        [SerializeField, Min(.5f)] float citizenOpenRadius = 1.65f;

        float openAmount = 1f;
        readonly Collider[] nearby = new Collider[16];

        public bool IsOpen => openAmount >= .5f;
        public string InteractionText => manualOpen ? "E  Close door" : "E  Open door";

        public void Configure(
            Vector3 closedPosition,
            Quaternion closedRotation,
            Vector3 openPosition,
            Quaternion openRotation,
            bool startsOpen = true)
        {
            closedLocalPosition = closedPosition;
            closedLocalRotation = closedRotation;
            openLocalPosition = openPosition;
            openLocalRotation = openRotation;
            manualOpen = startsOpen;
            openAmount = startsOpen ? 1f : 0f;
            ApplyPose(openAmount);
        }

        void OnEnable()
        {
            openAmount = manualOpen ? 1f : 0f;
            ApplyPose(openAmount);
        }

        void Update()
        {
            bool wantsOpen = manualOpen || (autoOpenForCitizens && CitizenNearby());
            float target = wantsOpen ? 1f : 0f;
            openAmount = Mathf.MoveTowards(openAmount, target, transitionSpeed * Time.deltaTime);
            ApplyPose(openAmount);
        }

        public void ToggleFromPlayer()
        {
            manualOpen = !manualOpen;
        }

        bool CitizenNearby()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                citizenOpenRadius,
                nearby,
                ~0,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider hit = nearby[i];
                nearby[i] = null;
                if (!hit) continue;
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                if (hit.GetComponentInParent<H51CitizenIdentity>())
                    return true;
            }

            return false;
        }

        void ApplyPose(float t)
        {
            transform.localPosition = Vector3.Lerp(closedLocalPosition, openLocalPosition, t);
            transform.localRotation = Quaternion.Slerp(closedLocalRotation, openLocalRotation, t);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, citizenOpenRadius);
        }
#endif
    }
}
