using UnityEngine;

namespace Hanger51.Health
{
    [DisallowMultipleComponent]
    public sealed class H51OrganHitbox : MonoBehaviour
    {
        [SerializeField] H51AnatomicalHealth health;
        [SerializeField] H51OrganType organ;
        [SerializeField] H51BodyRegion containingRegion;

        public H51AnatomicalHealth Health => health;
        public H51OrganType Organ => organ;
        public H51BodyRegion ContainingRegion => containingRegion;

        public void Configure(
            H51AnatomicalHealth configuredHealth,
            H51OrganType configuredOrgan,
            H51BodyRegion configuredRegion)
        {
            health = configuredHealth;
            organ = configuredOrgan;
            containingRegion = configuredRegion;
        }
    }
}
