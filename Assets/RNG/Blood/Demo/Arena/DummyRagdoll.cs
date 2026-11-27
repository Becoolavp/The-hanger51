using System.Collections.Generic;
using UnityEngine;

namespace Rng.Blood.Demo
{
    public static class DummyRagdoll
    {
        public const int BodyLayer = 2;

        private struct Bone
        {
            public string Name;
            public string Parent;
            public float Mass;
            public float Low;
            public float High;
            public float Swing;
            public float Twist;
        }

        private static readonly Bone[] Bones =
        {
            new() { Name = "Pelvis", Mass = 14f },
            new() { Name = "Spine", Parent = "Pelvis", Mass = 10f, Low = -20f, High = 20f, Swing = 15f, Twist = 15f },
            new() { Name = "Chest", Parent = "Spine", Mass = 16f, Low = -20f, High = 20f, Swing = 15f, Twist = 15f },
            new() { Name = "Head", Parent = "Chest", Mass = 5f, Low = -40f, High = 40f, Swing = 30f, Twist = 40f },
            new() { Name = "Shoulder Left", Parent = "Chest", Mass = 2.5f, Low = -120f, High = 50f, Swing = 70f, Twist = 30f },
            new() { Name = "Shoulder Right", Parent = "Chest", Mass = 2.5f, Low = -120f, High = 50f, Swing = 70f, Twist = 30f },
            new() { Name = "Elbow Left", Parent = "Shoulder Left", Mass = 1.8f, Low = -120f, High = 5f, Swing = 5f, Twist = 10f },
            new() { Name = "Elbow Right", Parent = "Shoulder Right", Mass = 1.8f, Low = -120f, High = 5f, Swing = 5f, Twist = 10f },
            new() { Name = "Hip Left", Parent = "Pelvis", Mass = 8f, Low = -90f, High = 20f, Swing = 30f, Twist = 20f },
            new() { Name = "Hip Right", Parent = "Pelvis", Mass = 8f, Low = -90f, High = 20f, Swing = 30f, Twist = 20f },
            new() { Name = "Knee Left", Parent = "Hip Left", Mass = 4f, Low = -5f, High = 110f, Swing = 5f, Twist = 5f },
            new() { Name = "Knee Right", Parent = "Hip Right", Mass = 4f, Low = -5f, High = 110f, Swing = 5f, Twist = 5f },
        };

#if UNITY_6000_0_OR_NEWER
        private static PhysicsMaterial _friction;
#else
        private static PhysicMaterial _friction;
#endif

        public static Dictionary<string, Rigidbody> Build(GameObject body)
        {
            if (_friction == null)
            {
#if UNITY_6000_0_OR_NEWER
                _friction = new PhysicsMaterial("Body") { dynamicFriction = 0.7f, staticFriction = 0.8f, frictionCombine = PhysicsMaterialCombine.Average, bounciness = 0f };
#else
                _friction = new PhysicMaterial("Body") { dynamicFriction = 0.7f, staticFriction = 0.8f, frictionCombine = PhysicMaterialCombine.Average, bounciness = 0f };
#endif
            }

            var colliders = new List<Collider>();
            foreach (Transform child in body.GetComponentsInChildren<Transform>())
            {
                child.gameObject.layer = BodyLayer;
                if (child.GetComponent<Collider>() != null || !child.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null) continue;

                Vector3 scale = child.localScale;
                float largest = Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
                if (largest < 0.07f || Mathf.Min(scale.x, Mathf.Min(scale.y, scale.z)) < 0.01f) continue;

                Collider shape;
                if (filter.sharedMesh.name.StartsWith("Capsule")) shape = child.gameObject.AddComponent<CapsuleCollider>();
                else if (largest > 0.19f) shape = child.gameObject.AddComponent<BoxCollider>();
                else shape = child.gameObject.AddComponent<SphereCollider>();
                if (shape is BoxCollider box) box.size = new Vector3(0.82f, 0.82f, 0.82f);
                shape.sharedMaterial = _friction;
                colliders.Add(shape);
            }

            var parts = new Dictionary<string, Rigidbody>();
            foreach (Bone bone in Bones)
            {
                Transform node = Find(body.transform, bone.Name);
                if (node == null) continue;

                var part = node.gameObject.AddComponent<Rigidbody>();
                part.mass = bone.Mass;
#if UNITY_6000_0_OR_NEWER
                part.linearDamping = 0.2f;
                part.angularDamping = 3f;
#else
                part.drag = 0.2f;
                part.angularDrag = 3f;
#endif
                part.interpolation = Application.isPlaying ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
                part.solverIterations = 16;
                part.solverVelocityIterations = 4;
                parts[bone.Name] = part;
                if (bone.Parent == null || !parts.TryGetValue(bone.Parent, out Rigidbody parent)) continue;

                var joint = node.gameObject.AddComponent<CharacterJoint>();
                joint.connectedBody = parent;
                joint.axis = Vector3.right;
                joint.swingAxis = Vector3.forward;
                joint.lowTwistLimit = new SoftJointLimit { limit = bone.Low };
                joint.highTwistLimit = new SoftJointLimit { limit = bone.High };
                joint.swing1Limit = new SoftJointLimit { limit = bone.Swing };
                joint.swing2Limit = new SoftJointLimit { limit = bone.Twist };
                joint.enableProjection = true;
                joint.enablePreprocessing = false;
            }

            for (int i = 0; i < colliders.Count; i++)
                for (int j = i + 1; j < colliders.Count; j++)
                    if (Physics.ComputePenetration(colliders[i], colliders[i].transform.position, colliders[i].transform.rotation, colliders[j], colliders[j].transform.position, colliders[j].transform.rotation, out _, out _))
                        Physics.IgnoreCollision(colliders[i], colliders[j]);

            return parts;
        }

        public static Transform Find(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>())
                if (child.name == name)
                    return child;

            return null;
        }
    }
}
