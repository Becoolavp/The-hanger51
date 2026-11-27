using UnityEngine;

namespace Hanger51.Health
{
    [DisallowMultipleComponent]
    public sealed class H51AnatomicalHitboxRig : MonoBehaviour
    {
        const string RootName = "H51 Anatomical Hitboxes";
        [SerializeField] Transform hitboxRoot;

        void Awake()
        {
            EnsureBuilt();
        }

        public void EnsureBuilt()
        {
            if (hitboxRoot)
                return;

            Transform existing = transform.Find(RootName);
            if (existing)
            {
                hitboxRoot = existing;
                return;
            }

            GameObject root = new GameObject(RootName);
            root.transform.SetParent(transform,false);
            hitboxRoot = root.transform;

            Box(H51BodyRegion.Head,new Vector3(0f,1.63f,0f),new Vector3(.38f,.34f,.32f));
            Box(H51BodyRegion.Neck,new Vector3(0f,1.43f,0f),new Vector3(.22f,.16f,.22f));
            Box(H51BodyRegion.Chest,new Vector3(0f,1.17f,0f),new Vector3(.52f,.44f,.32f));
            Box(H51BodyRegion.Abdomen,new Vector3(0f,.88f,0f),new Vector3(.46f,.28f,.30f));
            Box(H51BodyRegion.Pelvis,new Vector3(0f,.66f,0f),new Vector3(.44f,.22f,.30f));
            Box(H51BodyRegion.LeftArm,new Vector3(-.39f,1.08f,0f),new Vector3(.18f,.68f,.20f));
            Box(H51BodyRegion.RightArm,new Vector3(.39f,1.08f,0f),new Vector3(.18f,.68f,.20f));
            Box(H51BodyRegion.LeftLeg,new Vector3(-.15f,.34f,0f),new Vector3(.22f,.66f,.24f));
            Box(H51BodyRegion.RightLeg,new Vector3(.15f,.34f,0f),new Vector3(.22f,.66f,.24f));
        }

        void Box(H51BodyRegion region,Vector3 center,Vector3 size)
        {
            GameObject child = new GameObject(region + " Hitbox");
            child.transform.SetParent(hitboxRoot,false);
            child.transform.localPosition = center;

            BoxCollider collider = child.AddComponent<BoxCollider>();
            collider.size = size;
            collider.isTrigger = true;

            H51AnatomicalHitbox hitbox =
                child.AddComponent<H51AnatomicalHitbox>();
            hitbox.Configure(GetComponent<H51AnatomicalHealth>(),region);
        }
    }

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
