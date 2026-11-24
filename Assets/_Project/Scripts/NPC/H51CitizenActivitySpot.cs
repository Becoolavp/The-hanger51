using UnityEngine;

namespace Hanger51.NPCs
{
    public sealed class H51CitizenActivitySpot : MonoBehaviour
    {
        [SerializeField] string activityLabel = "working";
        [SerializeField] string animationTrigger = "Interact";
        [SerializeField, Min(.5f)] float minimumDuration = 3f;
        [SerializeField, Min(.5f)] float maximumDuration = 8f;

        public string ActivityLabel => string.IsNullOrWhiteSpace(activityLabel) ? "working" : activityLabel;
        public string AnimationTrigger => animationTrigger;
        public float MinimumDuration => minimumDuration;
        public float MaximumDuration => Mathf.Max(minimumDuration, maximumDuration);

        public void Configure(string label, string trigger = "Interact", float minDuration = 3f, float maxDuration = 8f)
        {
            activityLabel = label;
            animationTrigger = trigger;
            minimumDuration = Mathf.Max(.5f, minDuration);
            maximumDuration = Mathf.Max(minimumDuration, maxDuration);
        }
    }
}
