using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class P51AftEquipmentSlot : MonoBehaviour
    {
        [SerializeField] private P51AftEquipmentBay bay;
        [SerializeField] private P51AftEquipmentKind acceptedKind;
        [SerializeField] private int slotIndex;
        [SerializeField] private GameObject placementHighlightRoot;

        private Vector3 highlightBaseScale = Vector3.one;
        private bool placementHighlighted;

        public P51AftEquipmentBay Bay => bay;
        public P51AftEquipmentKind AcceptedKind => acceptedKind;
        public int SlotIndex => slotIndex;
        public P51AftEquipmentItem InstalledItem => bay != null ? bay.GetInstalledItem(slotIndex) : null;
        public GameObject PlacementHighlightRoot => placementHighlightRoot;
        public bool IsPlacementHighlighted => placementHighlighted;

        public void Configure(P51AftEquipmentBay configuredBay, P51AftEquipmentKind kind, int index)
        {
            bay = configuredBay;
            acceptedKind = kind;
            slotIndex = Mathf.Max(0, index);
        }

        public void ConfigurePlacementHighlight(GameObject configuredRoot)
        {
            placementHighlightRoot = configuredRoot;
            if (placementHighlightRoot != null)
            {
                highlightBaseScale = placementHighlightRoot.transform.localScale;
                placementHighlightRoot.SetActive(false);
            }
            placementHighlighted = false;
        }

        public void SetPlacementHighlighted(bool highlighted)
        {
            placementHighlighted = highlighted;
            if (placementHighlightRoot == null)
            {
                return;
            }

            if (placementHighlightRoot.activeSelf != highlighted)
            {
                placementHighlightRoot.SetActive(highlighted);
            }

            if (!highlighted)
            {
                placementHighlightRoot.transform.localScale = highlightBaseScale;
            }
        }

        private void Update()
        {
            if (!placementHighlighted || placementHighlightRoot == null)
            {
                return;
            }

            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 5.5f) * 0.045f;
            placementHighlightRoot.transform.localScale = highlightBaseScale * pulse;
        }

        private void OnDisable()
        {
            SetPlacementHighlighted(false);
        }
    }
}
