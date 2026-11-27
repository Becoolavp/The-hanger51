using UnityEngine;

namespace Hanger51.Health
{
    [DisallowMultipleComponent]
    public sealed class H51AnatomicalHitbox : MonoBehaviour
    {
        [SerializeField] H51AnatomicalHealth health;
        [SerializeField] H51BodyRegion region;

        public H51AnatomicalHealth Health => health;
        public H51BodyRegion Region => region;

        public void Configure(
            H51AnatomicalHealth configuredHealth,
            H51BodyRegion configuredRegion)
        {
            health = configuredHealth;
            region = configuredRegion;
        }
    }
}
