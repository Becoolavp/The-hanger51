using UnityEngine;
using Hanger51.World;

namespace Hanger51.NPCs
{
    [DisallowMultipleComponent]
    public sealed class H51InteriorLightProximity : MonoBehaviour
    {
        [SerializeField] Light interiorLight;
        [SerializeField, Min(5f)] float activationDistance = 28f;
        [SerializeField, Min(.1f)] float refreshSeconds = .35f;

        Transform viewer;
        H51UtilityConsumer utilityConsumer;
        float nextRefresh;

        public void Configure(Light target, float distance)
        {
            interiorLight = target;
            activationDistance = Mathf.Max(5f, distance);
        }

        void Awake()
        {
            if (!interiorLight) interiorLight = GetComponentInChildren<Light>(true);
            utilityConsumer = GetComponentInParent<H51UtilityConsumer>();
            if (interiorLight) interiorLight.enabled = false;
        }

        void Update()
        {
            if (!interiorLight || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + refreshSeconds;

            if (!utilityConsumer)
                utilityConsumer = GetComponentInParent<H51UtilityConsumer>();

            if (utilityConsumer && !utilityConsumer.HasElectricity)
            {
                interiorLight.enabled = false;
                return;
            }

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
