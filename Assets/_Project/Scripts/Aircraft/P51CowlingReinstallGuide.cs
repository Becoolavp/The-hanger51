using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    public sealed class P51CowlingReinstallGuide : MonoBehaviour
    {
        [SerializeField] private P51AircraftServiceController serviceController;
        [SerializeField] private GameObject guideVisualRoot;

        public P51AircraftServiceController ServiceController => serviceController;
        public GameObject GuideVisualRoot => guideVisualRoot;
        public bool IsConfigured => serviceController != null && guideVisualRoot != null;

        private void Awake()
        {
            ResolveReferences();
            RefreshVisibility();
        }

        private void OnEnable()
        {
            ResolveReferences();
            RefreshVisibility();
        }

        private void Update()
        {
            RefreshVisibility();
        }

        public void Configure(
            P51AircraftServiceController configuredServiceController,
            GameObject configuredGuideVisualRoot)
        {
            serviceController = configuredServiceController;
            guideVisualRoot = configuredGuideVisualRoot;
            RefreshVisibility();
        }

        public void RefreshVisibility()
        {
            if (guideVisualRoot == null)
            {
                return;
            }

            bool shouldShow = serviceController != null
                && serviceController.IsCowlingInstallAreaReady;
            if (guideVisualRoot.activeSelf != shouldShow)
            {
                guideVisualRoot.SetActive(shouldShow);
            }
        }

        private void ResolveReferences()
        {
            if (serviceController == null)
            {
                serviceController = GetComponentInParent<P51AircraftServiceController>();
            }
        }

        private void OnDisable()
        {
            if (guideVisualRoot != null)
            {
                guideVisualRoot.SetActive(false);
            }
        }
    }
}
