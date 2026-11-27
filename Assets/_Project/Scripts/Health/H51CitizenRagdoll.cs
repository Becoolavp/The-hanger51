using System.Collections.Generic;
using Hanger51.NPCs;
using UnityEngine;
using UnityEngine.AI;

namespace Hanger51.Health
{
    [DisallowMultipleComponent]
    public sealed class H51CitizenRagdoll : MonoBehaviour
    {
        readonly List<Rigidbody> bodies = new();
        readonly List<Collider> colliders = new();
        readonly Dictionary<H51BodyRegion,Rigidbody> regionBodies = new();

        H51AnatomicalHealth health;
        H51BoZoCitizenVisual visual;
        Animator animator;
        NavMeshAgent agent;
        CharacterController controller;
        H51CitizenBrain brain;

        bool built;
        bool ragdolled;
        bool permanent;
        float recoverAfter;
        Vector3 pendingDirection;
        float pendingImpulse;
        H51BodyRegion pendingRegion = H51BodyRegion.Chest;

        public bool IsRagdolled => ragdolled;

        void Awake()
        {
            health = GetComponent<H51AnatomicalHealth>();
            visual = GetComponent<H51BoZoCitizenVisual>();
            agent = GetComponent<NavMeshAgent>();
            controller = GetComponent<CharacterController>();
            brain = GetComponent<H51CitizenBrain>();

            if (health)
                health.OnDeath += HandleDeath;
        }

        void Start() => EnsureRig();

        void OnDestroy()
        {
            if (health)
                health.OnDeath -= HandleDeath;
        }

        void Update()
        {
            if (!built)
                EnsureRig();

            if (!health || !ragdolled || permanent)
                return;

            if (Time.time >= recoverAfter &&
                !health.IsDead &&
                health.Consciousness >= 35f &&
                health.Shock < 70f &&
                health.BloodFraction > .42f)
            {
                Recover();
            }
        }

        public void RegisterImpact(
            H51BodyRegion region,
            Vector3 direction,
            float impulse,
            float trauma)
        {
            pendingRegion = region;
            pendingDirection = direction.sqrMagnitude > .001f
                ? direction.normalized
                : transform.forward;
            pendingImpulse = Mathf.Max(pendingImpulse,impulse);

            if (!health || health.IsDead)
                return;

            bool knockdown =
                trauma >= 48f ||
                ((region == H51BodyRegion.LeftLeg || region == H51BodyRegion.RightLeg) && trauma >= 28f) ||
                ((region == H51BodyRegion.Head || region == H51BodyRegion.Neck) && trauma >= 24f) ||
                health.Consciousness < 18f;

            if (knockdown)
                KnockDown(Mathf.Lerp(1.0f,3.0f,Mathf.Clamp01(trauma/95f)));
        }

        public void KnockDown(float seconds)
        {
            permanent = false;
            recoverAfter = Mathf.Max(recoverAfter,Time.time+Mathf.Max(.6f,seconds));
            ActivateRagdoll(false);
        }

        void HandleDeath(H51AnatomicalHealth _)
        {
            permanent = true;
            ActivateRagdoll(true);
        }

        public void EnsureRig()
        {
            if (built)
                return;

            if (!visual)
                visual = GetComponent<H51BoZoCitizenVisual>();

            animator = visual ? visual.VisualAnimator : GetComponentInChildren<Animator>(true);
            if (!animator || !animator.isHuman)
                return;

            built = true;

            Rigidbody hips = AddBody(HumanBodyBones.Hips,H51BodyRegion.Pelvis,12f,.13f);
            Rigidbody spine = AddBody(HumanBodyBones.Spine,H51BodyRegion.Abdomen,10f,.12f);
            Rigidbody chest = AddBody(HumanBodyBones.Chest,H51BodyRegion.Chest,12f,.14f);
            Rigidbody head = AddBody(HumanBodyBones.Head,H51BodyRegion.Head,5f,.11f);

            Rigidbody lua = AddBody(HumanBodyBones.LeftUpperArm,H51BodyRegion.LeftArm,3.2f,.075f);
            Rigidbody lla = AddBody(HumanBodyBones.LeftLowerArm,H51BodyRegion.LeftArm,2.2f,.065f);
            Rigidbody rua = AddBody(HumanBodyBones.RightUpperArm,H51BodyRegion.RightArm,3.2f,.075f);
            Rigidbody rla = AddBody(HumanBodyBones.RightLowerArm,H51BodyRegion.RightArm,2.2f,.065f);
            Rigidbody lul = AddBody(HumanBodyBones.LeftUpperLeg,H51BodyRegion.LeftLeg,7.5f,.095f);
            Rigidbody lll = AddBody(HumanBodyBones.LeftLowerLeg,H51BodyRegion.LeftLeg,5f,.08f);
            Rigidbody rul = AddBody(HumanBodyBones.RightUpperLeg,H51BodyRegion.RightLeg,7.5f,.095f);
            Rigidbody rll = AddBody(HumanBodyBones.RightLowerLeg,H51BodyRegion.RightLeg,5f,.08f);

            Joint(spine,hips);
            Joint(chest,spine);
            Joint(head,chest);
            Joint(lua,chest);
            Joint(lla,lua);
            Joint(rua,chest);
            Joint(rla,rua);
            Joint(lul,hips);
            Joint(lll,lul);
            Joint(rul,hips);
            Joint(rll,rul);

            SetPhysics(false);
        }

        Rigidbody AddBody(HumanBodyBones bone,H51BodyRegion region,float mass,float radius)
        {
            Transform t = animator.GetBoneTransform(bone);
            if (!t)
                return null;

            H51RagdollBone marker = t.GetComponent<H51RagdollBone>();
            if (!marker)
                marker = t.gameObject.AddComponent<H51RagdollBone>();
            marker.Configure(region);
            t.gameObject.layer = 2;

            Rigidbody rb = t.GetComponent<Rigidbody>();
            if (!rb)
                rb = t.gameObject.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.linearDamping = .08f;
            rb.angularDamping = .18f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.maxAngularVelocity = 18f;

            Collider col = t.GetComponent<Collider>();
            if (!col)
            {
                Transform child = FirstBoneChild(t);
                if (child)
                {
                    Vector3 local = t.InverseTransformPoint(child.position);
                    float length = Mathf.Max(radius*2.2f,local.magnitude);
                    CapsuleCollider cap = t.gameObject.AddComponent<CapsuleCollider>();
                    Vector3 a = new(Mathf.Abs(local.x),Mathf.Abs(local.y),Mathf.Abs(local.z));
                    cap.direction = a.x >= a.y && a.x >= a.z ? 0 : a.z >= a.y ? 2 : 1;
                    cap.center = local*.5f;
                    cap.radius = Mathf.Min(radius,length*.28f);
                    cap.height = Mathf.Max(cap.radius*2f,length);
                    col = cap;
                }
                else
                {
                    SphereCollider sphere = t.gameObject.AddComponent<SphereCollider>();
                    sphere.radius = radius;
                    col = sphere;
                }
            }

            bodies.Add(rb);
            colliders.Add(col);
            if (!regionBodies.ContainsKey(region))
                regionBodies.Add(region,rb);
            return rb;
        }

        static Transform FirstBoneChild(Transform parent)
        {
            for (int i=0;i<parent.childCount;i++)
            {
                Transform child = parent.GetChild(i);
                if (child)
                    return child;
            }
            return null;
        }

        static void Joint(Rigidbody body,Rigidbody connected)
        {
            if (!body || !connected || body.GetComponent<CharacterJoint>())
                return;

            CharacterJoint joint = body.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = connected;
            joint.enableProjection = true;
            joint.enablePreprocessing = true;

            SoftJointLimit low = joint.lowTwistLimit;
            low.limit = -28f;
            joint.lowTwistLimit = low;

            SoftJointLimit high = joint.highTwistLimit;
            high.limit = 28f;
            joint.highTwistLimit = high;

            SoftJointLimit swing1 = joint.swing1Limit;
            swing1.limit = 42f;
            joint.swing1Limit = swing1;

            SoftJointLimit swing2 = joint.swing2Limit;
            swing2.limit = 28f;
            joint.swing2Limit = swing2;
        }

        void ActivateRagdoll(bool dead)
        {
            EnsureRig();
            if (!built)
                return;

            ragdolled = true;
            permanent |= dead;

            if (brain)
                brain.enabled = false;

            if (agent && agent.enabled)
            {
                if (agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    if (agent.hasPath)
                        agent.ResetPath();
                }
                agent.enabled = false;
            }

            if (controller)
                controller.enabled = false;

            if (animator)
            {
                animator.speed = 1f;
                animator.enabled = false;
            }

            SetPhysics(true);

            if (!regionBodies.TryGetValue(pendingRegion,out Rigidbody hitBody))
                hitBody = regionBodies.TryGetValue(H51BodyRegion.Chest,out Rigidbody chest) ? chest : null;

            if (hitBody && pendingImpulse > .01f)
                hitBody.AddForce(pendingDirection*Mathf.Clamp(pendingImpulse,.5f,8f),ForceMode.Impulse);
        }

        void Recover()
        {
            if (!ragdolled || permanent)
                return;

            Vector3 position = transform.position;
            if (regionBodies.TryGetValue(H51BodyRegion.Pelvis,out Rigidbody hips) && hips)
                position = hips.position;

            SetPhysics(false);

            transform.position = position;
            transform.rotation = Quaternion.Euler(0f,transform.eulerAngles.y,0f);

            if (animator)
            {
                animator.enabled = true;
                animator.speed = 1f;
                animator.Rebind();
                animator.Update(0f);
            }

            if (controller)
                controller.enabled = true;

            if (agent)
            {
                agent.enabled = true;
                if (NavMesh.SamplePosition(position,out NavMeshHit hit,5f,NavMesh.AllAreas))
                    agent.Warp(hit.position);
                if (agent.isOnNavMesh)
                    agent.isStopped = false;
            }

            if (brain && health && !health.IsDead)
                brain.enabled = true;

            ragdolled = false;
            pendingImpulse = 0f;
        }

        void SetPhysics(bool active)
        {
            for (int i=0;i<bodies.Count;i++)
            {
                Rigidbody rb = bodies[i];
                if (!rb) continue;
                rb.isKinematic = !active;
                rb.detectCollisions = active;
                rb.useGravity = active;
                if (!active)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }

            for (int i=0;i<colliders.Count;i++)
                if (colliders[i])
                    colliders[i].enabled = active;
        }
    }
}
