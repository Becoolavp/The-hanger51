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
            NormalizePlacementHighlight();
        }

        public void ConfigurePlacementHighlight(GameObject configuredRoot)
        {
            placementHighlightRoot = configuredRoot;
            NormalizePlacementHighlight();
            if (placementHighlightRoot != null)
            {
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

        private void OnEnable()
        {
            // Step 94 originally built oxygen guides with their long dimension on local Y even
            // though the actual bottle capsule and cradle run fore/aft on local Z. Normalize old
            // saved scenes as soon as the slot loads, and also normalize newly configured guides.
            NormalizePlacementHighlight();
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

        private void NormalizePlacementHighlight()
        {
            if (placementHighlightRoot == null)
            {
                return;
            }

            Transform highlightTransform = placementHighlightRoot.transform;
            highlightTransform.localRotation = acceptedKind == P51AftEquipmentKind.OxygenBottle
                ? Quaternion.Euler(90f, 0f, 0f)
                : Quaternion.identity;
            highlightBaseScale = highlightTransform.localScale;
        }
    }
}
