using UnityEngine;

namespace Hanger51.Health
{
    [DisallowMultipleComponent]
    public sealed class H51RagdollBone : MonoBehaviour
    {
        [SerializeField] H51BodyRegion region;
        public H51BodyRegion Region => region;
        public void Configure(H51BodyRegion configuredRegion) => region = configuredRegion;
    }
}
