using UnityEngine;

namespace Hanger51.NPCs
{
    [DisallowMultipleComponent]
    public sealed class H51InteriorLightProximity : MonoBehaviour
    {
        [SerializeField] Light interiorLight;
        [SerializeField, Min(5f)] float activationDistance = 28f;
        [SerializeField, Min(.1f)] float refreshSeconds = .35f;

        Transform viewer;
        float nextRefresh;

        public void Configure(Light target, float distance)
        {
            interiorLight = target;
            activationDistance = Mathf.Max(5f, distance);
        }

        void Awake()
        {
            if (!interiorLight) interiorLight = GetComponentInChildren<Light>(true);
            if (interiorLight) interiorLight.enabled = false;
        }

        void Update()
        {
            if (!interiorLight || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + refreshSeconds;

            if (!viewer)
            {
                Camera camera = Camera.main;
                if (camera) viewer = camera.transform;
            }

            if (!viewer)
            {
                interiorLight.enabled = false;
                return;
            }

            Vector3 delta = viewer.position - transform.position;
            interiorLight.enabled = delta.sqrMagnitude <= activationDistance * activationDistance;
        }
    }
}
