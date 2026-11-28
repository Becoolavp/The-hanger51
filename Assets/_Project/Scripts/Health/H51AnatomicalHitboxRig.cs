using UnityEngine;

namespace Hanger51.Health
{
    [DisallowMultipleComponent]
    public sealed class H51AnatomicalHitboxRig : MonoBehaviour
    {
        const string RootName = "H51 Anatomical Hitboxes";
        const string OrganPrefix = "H51 Organ - ";

        [SerializeField] Transform hitboxRoot;

        H51AnatomicalHealth health;
        Animator humanoidAnimator;

        void Awake()
        {
            EnsureBuilt();
        }

        void Start()
        {
            // BoZo characters can finish creating their Humanoid Animator
            // after Awake. A second pass makes the organ volumes follow the
            // real animated bones whenever they are available.
            EnsureBuilt();
        }

        public void EnsureBuilt()
        {
            if (!health)
                health = GetComponent<H51AnatomicalHealth>();

            if (!hitboxRoot)
            {
                Transform existing =
                    transform.Find(RootName);

                if (existing)
                {
                    hitboxRoot = existing;
                }
                else
                {
                    GameObject root =
                        new GameObject(RootName);

                    root.layer = 2;
                    root.transform.SetParent(transform,false);
                    hitboxRoot = root.transform;
                }
            }

            SetHitboxLayer(hitboxRoot);
            EnsureRegionHitboxes();
            EnsureOrganHitboxes();
        }

        void EnsureRegionHitboxes()
        {
            if (!hitboxRoot)
                return;

            if (!FindRegionHitbox(H51BodyRegion.Head))
                Box(H51BodyRegion.Head,new Vector3(0f,1.63f,0f),new Vector3(.38f,.34f,.32f));
            if (!FindRegionHitbox(H51BodyRegion.Neck))
                Box(H51BodyRegion.Neck,new Vector3(0f,1.43f,0f),new Vector3(.22f,.16f,.22f));
            if (!FindRegionHitbox(H51BodyRegion.Chest))
                Box(H51BodyRegion.Chest,new Vector3(0f,1.17f,0f),new Vector3(.52f,.44f,.32f));
            if (!FindRegionHitbox(H51BodyRegion.Abdomen))
                Box(H51BodyRegion.Abdomen,new Vector3(0f,.88f,0f),new Vector3(.46f,.28f,.30f));
            if (!FindRegionHitbox(H51BodyRegion.Pelvis))
                Box(H51BodyRegion.Pelvis,new Vector3(0f,.66f,0f),new Vector3(.44f,.22f,.30f));
            if (!FindRegionHitbox(H51BodyRegion.LeftArm))
                Box(H51BodyRegion.LeftArm,new Vector3(-.39f,1.08f,0f),new Vector3(.18f,.68f,.20f));
            if (!FindRegionHitbox(H51BodyRegion.RightArm))
                Box(H51BodyRegion.RightArm,new Vector3(.39f,1.08f,0f),new Vector3(.18f,.68f,.20f));
            if (!FindRegionHitbox(H51BodyRegion.LeftLeg))
                Box(H51BodyRegion.LeftLeg,new Vector3(-.15f,.34f,0f),new Vector3(.22f,.66f,.24f));
            if (!FindRegionHitbox(H51BodyRegion.RightLeg))
                Box(H51BodyRegion.RightLeg,new Vector3(.15f,.34f,0f),new Vector3(.22f,.66f,.24f));
        }

        void EnsureOrganHitboxes()
        {
            if (!health)
                return;

            if (!humanoidAnimator)
            {
                Animator[] animators =
                    GetComponentsInChildren<Animator>(true);

                for (int i=0;i<animators.Length;i++)
                {
                    Animator candidate =
                        animators[i];

                    if (candidate &&
                        candidate.isHuman)
                    {
                        humanoidAnimator = candidate;
                        break;
                    }
                }
            }

            OrganSphere(
                H51OrganType.Brain,
                H51BodyRegion.Head,
                HumanBodyBones.Head,
                new Vector3(0f,1.64f,.010f),
                .145f);

            OrganBox(
                H51OrganType.Heart,
                H51BodyRegion.Chest,
                HumanBodyBones.Chest,
                new Vector3(0f,1.18f,.015f),
                new Vector3(.145f,.165f,.120f));

            OrganBox(
                H51OrganType.LeftLung,
                H51BodyRegion.Chest,
                HumanBodyBones.Chest,
                new Vector3(-.115f,1.185f,.015f),
                new Vector3(.145f,.245f,.125f));

            OrganBox(
                H51OrganType.RightLung,
                H51BodyRegion.Chest,
                HumanBodyBones.Chest,
                new Vector3(.115f,1.185f,.015f),
                new Vector3(.145f,.245f,.125f));

            OrganBox(
                H51OrganType.Liver,
                H51BodyRegion.Abdomen,
                HumanBodyBones.Spine,
                new Vector3(-.115f,.925f,.025f),
                new Vector3(.205f,.125f,.125f));

            OrganBox(
                H51OrganType.Stomach,
                H51BodyRegion.Abdomen,
                HumanBodyBones.Spine,
                new Vector3(.085f,.925f,.035f),
                new Vector3(.135f,.115f,.115f));

            OrganBox(
                H51OrganType.Intestines,
                H51BodyRegion.Abdomen,
                HumanBodyBones.Spine,
                new Vector3(0f,.785f,.025f),
                new Vector3(.255f,.195f,.135f));

            OrganBox(
                H51OrganType.LeftKidney,
                H51BodyRegion.Abdomen,
                HumanBodyBones.Spine,
                new Vector3(-.115f,.87f,-.075f),
                new Vector3(.075f,.115f,.070f));

            OrganBox(
                H51OrganType.RightKidney,
                H51BodyRegion.Abdomen,
                HumanBodyBones.Spine,
                new Vector3(.115f,.87f,-.075f),
                new Vector3(.075f,.115f,.070f));

            OrganBox(
                H51OrganType.Spine,
                H51BodyRegion.Chest,
                HumanBodyBones.Spine,
                new Vector3(0f,1.11f,-.115f),
                new Vector3(.060f,.54f,.070f));
        }

        bool FindRegionHitbox(
            H51BodyRegion region)
        {
            H51AnatomicalHitbox[] hitboxes =
                hitboxRoot
                    ? hitboxRoot.GetComponentsInChildren<H51AnatomicalHitbox>(true)
                    : System.Array.Empty<H51AnatomicalHitbox>();

            for (int i=0;i<hitboxes.Length;i++)
            {
                if (hitboxes[i] &&
                    hitboxes[i].Region == region)
                    return true;
            }

            return false;
        }

        H51OrganHitbox FindOrganHitbox(
            H51OrganType organ)
        {
            H51OrganHitbox[] hitboxes =
                GetComponentsInChildren<H51OrganHitbox>(true);

            for (int i=0;i<hitboxes.Length;i++)
            {
                if (hitboxes[i] &&
                    hitboxes[i].Organ == organ)
                    return hitboxes[i];
            }

            return null;
        }

        void Box(
            H51BodyRegion region,
            Vector3 center,
            Vector3 size)
        {
            if (!hitboxRoot)
                return;

            GameObject child =
                new GameObject(
                    region + " Hitbox");

            child.layer = 2;
            child.transform.SetParent(hitboxRoot,false);
            child.transform.localPosition = center;

            BoxCollider collider =
                child.AddComponent<BoxCollider>();

            collider.size = size;
            collider.isTrigger = true;

            H51AnatomicalHitbox hitbox =
                child.AddComponent<H51AnatomicalHitbox>();

            hitbox.Configure(
                health,
                region);
        }

        void OrganSphere(
            H51OrganType organ,
            H51BodyRegion region,
            HumanBodyBones bone,
            Vector3 rootLocalCenter,
            float radius)
        {
            H51OrganHitbox existing =
                FindOrganHitbox(organ);

            if (existing)
            {
                existing.Configure(
                    health,
                    organ,
                    region);

                ReanchorExistingOrgan(
                    existing.transform,
                    bone,
                    rootLocalCenter);

                Collider existingCollider =
                    existing.GetComponent<Collider>();

                if (existingCollider)
                {
                    existingCollider.enabled = true;

                    SphereCollider sphere =
                        existingCollider as SphereCollider;

                    if (sphere)
                        sphere.radius =
                            Mathf.Max(.02f,radius);
                }

                existing.gameObject.layer = 2;
                return;
            }

            GameObject child =
                CreateOrganObject(
                    organ,
                    bone,
                    rootLocalCenter);

            SphereCollider collider =
                child.AddComponent<SphereCollider>();

            collider.radius =
                Mathf.Max(.02f,radius);
            collider.isTrigger = true;

            H51OrganHitbox hitbox =
                child.AddComponent<H51OrganHitbox>();

            hitbox.Configure(
                health,
                organ,
                region);
        }

        void OrganBox(
            H51OrganType organ,
            H51BodyRegion region,
            HumanBodyBones bone,
            Vector3 rootLocalCenter,
            Vector3 size)
        {
            H51OrganHitbox existing =
                FindOrganHitbox(organ);

            if (existing)
            {
                existing.Configure(
                    health,
                    organ,
                    region);

                ReanchorExistingOrgan(
                    existing.transform,
                    bone,
                    rootLocalCenter);

                Collider existingCollider =
                    existing.GetComponent<Collider>();

                if (existingCollider)
                {
                    existingCollider.enabled = true;

                    BoxCollider box =
                        existingCollider as BoxCollider;

                    if (box)
                    {
                        box.size =
                            new Vector3(
                                Mathf.Max(.02f,size.x),
                                Mathf.Max(.02f,size.y),
                                Mathf.Max(.02f,size.z));
                    }
                }

                existing.gameObject.layer = 2;
                return;
            }

            GameObject child =
                CreateOrganObject(
                    organ,
                    bone,
                    rootLocalCenter);

            BoxCollider collider =
                child.AddComponent<BoxCollider>();

            collider.size =
                new Vector3(
                    Mathf.Max(.02f,size.x),
                    Mathf.Max(.02f,size.y),
                    Mathf.Max(.02f,size.z));

            collider.isTrigger = true;

            H51OrganHitbox hitbox =
                child.AddComponent<H51OrganHitbox>();

            hitbox.Configure(
                health,
                organ,
                region);
        }

        void ReanchorExistingOrgan(
            Transform organTransform,
            HumanBodyBones bone,
            Vector3 rootLocalCenter)
        {
            if (!organTransform)
                return;

            Transform desiredParent =
                hitboxRoot;

            if (humanoidAnimator &&
                humanoidAnimator.isHuman)
            {
                Transform boneTransform =
                    humanoidAnimator.GetBoneTransform(bone);

                if (boneTransform)
                    desiredParent = boneTransform;
            }

            Vector3 desiredWorldPosition =
                transform.TransformPoint(rootLocalCenter);

            Quaternion desiredWorldRotation =
                transform.rotation;

            organTransform.position =
                desiredWorldPosition;

            organTransform.rotation =
                desiredWorldRotation;

            if (desiredParent &&
                organTransform.parent != desiredParent)
            {
                organTransform.SetParent(
                    desiredParent,
                    true);
            }

            Vector3 scale =
                desiredParent
                    ? desiredParent.lossyScale
                    : Vector3.one;

            organTransform.localScale =
                new Vector3(
                    1f/Mathf.Max(.0001f,Mathf.Abs(scale.x)),
                    1f/Mathf.Max(.0001f,Mathf.Abs(scale.y)),
                    1f/Mathf.Max(.0001f,Mathf.Abs(scale.z)));
        }

        GameObject CreateOrganObject(
            H51OrganType organ,
            HumanBodyBones bone,
            Vector3 rootLocalCenter)
        {
            GameObject child =
                new GameObject(
                    OrganPrefix + organ);

            child.layer = 2;

            Vector3 worldPosition =
                transform.TransformPoint(
                    rootLocalCenter);

            Quaternion worldRotation =
                transform.rotation;

            Transform parent =
                hitboxRoot;

            if (humanoidAnimator &&
                humanoidAnimator.isHuman)
            {
                Transform boneTransform =
                    humanoidAnimator.GetBoneTransform(bone);

                if (boneTransform)
                    parent = boneTransform;
            }

            child.transform.position =
                worldPosition;

            child.transform.rotation =
                worldRotation;

            child.transform.SetParent(
                parent,
                true);

            Vector3 scale =
                parent
                    ? parent.lossyScale
                    : Vector3.one;

            child.transform.localScale =
                new Vector3(
                    1f/Mathf.Max(.0001f,Mathf.Abs(scale.x)),
                    1f/Mathf.Max(.0001f,Mathf.Abs(scale.y)),
                    1f/Mathf.Max(.0001f,Mathf.Abs(scale.z)));

            return child;
        }

        static void SetHitboxLayer(
            Transform root)
        {
            if (!root)
                return;

            Transform[] all =
                root.GetComponentsInChildren<Transform>(true);

            for (int i=0;i<all.Length;i++)
            {
                if (all[i])
                    all[i].gameObject.layer = 2;
            }
        }
    }
}
