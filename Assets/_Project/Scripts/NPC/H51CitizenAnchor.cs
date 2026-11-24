using UnityEngine;

namespace Hanger51.NPCs
{
    public enum H51CitizenAnchorKind
    {
        Home,
        Work,
        Shop,
        Leisure,
        Park,
        Farm,
        Civic
    }

    public sealed class H51CitizenAnchor : MonoBehaviour
    {
        [SerializeField] string anchorId;
        [SerializeField] string townName;
        [SerializeField] H51CitizenAnchorKind kind;
        [SerializeField] string occupationHint;
        [SerializeField] string displayLabel;
        [SerializeField] string workplaceId;
        [SerializeField, Min(1)] int capacity = 4;

        public string AnchorId => anchorId;
        public string TownName => townName;
        public H51CitizenAnchorKind Kind => kind;
        public string OccupationHint => occupationHint;
        public string DisplayLabel => string.IsNullOrWhiteSpace(displayLabel) ? name : displayLabel;
        public string WorkplaceId => workplaceId;
        public int Capacity => capacity;

        public void Configure(string id, string town, H51CitizenAnchorKind anchorKind, string occupation, int maxCapacity, string label = null, string linkedWorkplaceId = "")
        {
            anchorId = id;
            townName = town;
            kind = anchorKind;
            occupationHint = occupation;
            displayLabel = string.IsNullOrWhiteSpace(label) ? name : label;
            workplaceId = linkedWorkplaceId;
            capacity = Mathf.Max(1, maxCapacity);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Color c = kind == H51CitizenAnchorKind.Home ? new Color(.25f,.75f,1f,.75f) :
                      kind == H51CitizenAnchorKind.Work ? new Color(1f,.65f,.15f,.75f) :
                      kind == H51CitizenAnchorKind.Farm ? new Color(.45f,.8f,.2f,.75f) :
                      new Color(.75f,.45f,1f,.75f);
            Gizmos.color = c;
            Gizmos.DrawWireSphere(transform.position, .75f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);
        }
#endif
    }
}
