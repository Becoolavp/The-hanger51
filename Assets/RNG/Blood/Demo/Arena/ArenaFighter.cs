using System.Collections.Generic;
using UnityEngine;

namespace Rng.Blood.Demo
{
    public class ArenaFighter : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static Material _surface;
        private static readonly string[] Animated =
        {
            "Pelvis", "Spine", "Chest", "Neck", "Head", "Shoulder Left", "Shoulder Right", "Elbow Left", "Elbow Right",
            "Hip Left", "Hip Right", "Knee Left", "Knee Right", "Ankle Left", "Ankle Right"
        };

        public enum Style
        {
            Bat,
            Knife,
            Pistol,
            Shotgun,
            Grenade,
            Fists
        }

        private readonly Dictionary<string, Transform> _bones = new();
        private readonly Dictionary<string, Quaternion> _rest = new();
        private readonly Dictionary<string, Vector3> _pose = new();
        private readonly List<Renderer> _renderers = new();
        private CharacterController _controller;
        private MaterialPropertyBlock _block;
        private Transform _grip;
        private GameObject _body;
        private Style _style;
        private Vector3 _velocity;
        private Vector3 _knock;
        private Vector3 _flinch;
        private float _yaw;
        private float _phase;
        private float _attack = -1f;
        private bool _struck;
        private float _flash;
        private float _pelvisHeight;
        private float _lean;
        private Color[] _colours;

        public float Speed = 4.4f;
        public float Acceleration = 34f;
        public float AttackSeconds = 0.42f;
        public float Health = 3f;
        public float Burden = 1f;
        public bool Dragging;

        public bool Alive { get; private set; } = true;
        public bool Attacking => _attack >= 0f;
        public bool Striking { get; private set; }
        public Vector3 Facing => Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
        public Vector3 Velocity => _velocity;
        public Transform Chest => Bone("Chest");
        public BloodWounds Wounds { get; private set; }
        public GameObject Body => _body;
        public Style Weapon => _style;
        public bool Ranged => _style == Style.Pistol || _style == Style.Shotgun || _style == Style.Grenade;
        public Vector3 Muzzle => _grip != null ? _grip.TransformPoint(0f, _style == Style.Shotgun ? 0.62f : 0.2f, 0f) : Chest.position;
        public Vector3 Hand => Bone("Elbow Right").TransformPoint(0f, -0.3f, 0f);

        public void Setup(GameObject dummy, Style style, Color tint)
        {
            _style = style;
            _controller = gameObject.AddComponent<CharacterController>();
            _controller.height = 1.8f;
            _controller.radius = 0.32f;
            _controller.center = new Vector3(0f, 0.9f, 0f);
            _controller.stepOffset = 0.2f;
            _controller.skinWidth = 0.03f;
            gameObject.layer = DummyRagdoll.BodyLayer;

            _body = Instantiate(dummy, transform);
            _body.name = "Body";
            _body.transform.localPosition = Vector3.zero;
            _body.transform.localRotation = Quaternion.identity;
            foreach (MonoBehaviour script in _body.GetComponents<MonoBehaviour>())
                if (!(script is BloodWounds))
                    Remove(script);

            foreach (Transform child in _body.GetComponentsInChildren<Transform>()) child.gameObject.layer = DummyRagdoll.BodyLayer;
            foreach (string name in Animated)
            {
                Transform bone = DummyRagdoll.Find(_body.transform, name);
                if (bone == null) continue;
                _bones[name] = bone;
                _rest[name] = bone.localRotation;
            }

            _pelvisHeight = Bone("Pelvis").localPosition.y;
            _block = new MaterialPropertyBlock();
            _body.GetComponentsInChildren(_renderers);
            if (_surface == null && _renderers.Count > 0 && _renderers[0].sharedMaterial != null) _surface = _renderers[0].sharedMaterial;
            _colours = new Color[_renderers.Count];
            for (int i = 0; i < _renderers.Count; i++)
            {
                Material material = _renderers[i].sharedMaterial;
                Color colour = material != null && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.white;
                _colours[i] = material != null && material.name.Contains("Shell") ? colour * tint : colour;
                Paint(i, 0f);
            }

            if (!_body.TryGetComponent(out BloodWounds wounds)) wounds = _body.AddComponent<BloodWounds>();
            Wounds = wounds;
            Arm(style);
            _yaw = transform.eulerAngles.y;
            transform.rotation = Quaternion.identity;
            Pose(0f);
        }

        public void Arm(Style style)
        {
            _style = style;
            _attack = -1f;
            if (_grip != null) Remove(_grip.gameObject);
            _grip = null;
            Transform hand = Bone("Elbow Right");
            switch (style)
            {
                case Style.Bat:
                    _grip = MakeBat(hand);
                    AttackSeconds = 0.42f;
                    break;
                case Style.Knife:
                    _grip = MakeKnife(hand);
                    AttackSeconds = 0.26f;
                    break;
                case Style.Pistol:
                    _grip = MakeGun(hand, false);
                    AttackSeconds = 0.22f;
                    break;
                case Style.Shotgun:
                    _grip = MakeGun(hand, true);
                    AttackSeconds = 0.75f;
                    break;
                case Style.Grenade:
                    _grip = MakeGrenade(hand);
                    AttackSeconds = 0.6f;
                    break;
                default:
                    AttackSeconds = 0.55f;
                    break;
            }
        }

        public void Drive(Vector3 move, Vector3 aim, bool attack, float deltaTime)
        {
            if (!Alive || deltaTime <= 0f) return;

            move.y = 0f;
            if (move.sqrMagnitude > 1f) move.Normalize();
            float slow = Attacking && !Ranged ? 0.35f : Attacking ? 0.75f : 1f;
            Vector3 wanted = move * (Speed * slow * Burden);
            _velocity = Vector3.MoveTowards(_velocity, wanted, Acceleration * deltaTime);
            _knock = Vector3.MoveTowards(_knock, Vector3.zero, 18f * deltaTime);

            Vector3 look = aim - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
            {
                float target = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
                _yaw = Mathf.MoveTowardsAngle(_yaw, target, (Attacking && !Ranged ? 260f : 900f) * deltaTime);
            }

            if (attack && !Attacking)
            {
                _attack = 0f;
                _struck = false;
            }

            Striking = false;
            if (Attacking)
            {
                float before = _attack;
                _attack += deltaTime / AttackSeconds;
                float hit = _style switch
                {
                    Style.Bat => 0.45f,
                    Style.Knife => 0.4f,
                    Style.Pistol => 0.02f,
                    Style.Shotgun => 0.02f,
                    Style.Grenade => 0.45f,
                    _ => 0.47f
                };
                if (before < hit && _attack >= hit && !_struck)
                {
                    _struck = true;
                    Striking = true;
                    if (_style == Style.Shotgun) _knock -= Facing * 2.2f;
                    else if (!Ranged) _velocity += Facing * (_style == Style.Bat ? 3f : 2f);
                }

                if (_attack >= 1f) _attack = -1f;
            }

            Vector3 step = (_velocity + _knock) * deltaTime + Vector3.down * 2f * deltaTime;
            _controller.Move(step);
            Vector3 planar = new(_velocity.x, 0f, _velocity.z);
            float speed = planar.magnitude;
            float stride = Mathf.Lerp(1.3f, 2.1f, Mathf.Clamp01(speed / 4.4f));
            float stepBefore = _phase;
            _phase += speed / stride * deltaTime;
            bool footfall = Mathf.Floor(stepBefore) != Mathf.Floor(_phase);
            if (speed > 0.3f)
                Blood.Walk(this, new Vector3(transform.position.x, transform.position.y, transform.position.z), planar / speed, footfall);

            Pose(deltaTime);
        }

        public void Hurt(Vector3 point, Vector3 direction, BloodHitKind kind, float damage, float force, Transform bone)
        {
            if (!Alive) return;

            direction.y = 0f;
            direction.Normalize();
            Health -= damage;
            _knock += direction * force;
            _flash = 1f;
            Vector3 local = Quaternion.Euler(0f, -_yaw, 0f) * direction;
            _flinch = new Vector3(local.z, 0f, -local.x) * Mathf.Clamp(force * 6f, 10f, 34f);
            if (Wounds != null) Wounds.Add(point, kind, 1f, bone != null ? bone : Chest);
            Blood.Spawn(point, -direction, direction + Vector3.up * 0.2f, damage > 0f ? 1.2f : 0.5f, kind, bone);
        }

        public Dictionary<string, Rigidbody> Die(Vector3 direction, float force, BloodHitKind kind)
        {
            Alive = false;
            _attack = -1f;
            Blood.Forget(this);
            _body.transform.SetParent(null, true);
            for (int i = 0; i < _renderers.Count; i++) Paint(i, 0f);
            Dictionary<string, Rigidbody> parts = DummyRagdoll.Build(_body);
            Vector3 push = direction.normalized * force + Vector3.up * force * 0.15f;
            foreach (Rigidbody part in parts.Values)
            {
#if UNITY_6000_0_OR_NEWER
                part.linearVelocity = _velocity * 0.4f + push * Random.Range(0.75f, 1.05f);
#else
                part.velocity = _velocity * 0.4f + push * Random.Range(0.75f, 1.05f);
#endif
            }

            if (parts.TryGetValue("Chest", out Rigidbody chest))
            {
                chest.AddTorque(Vector3.Cross(Vector3.up, direction) * force * 2f, ForceMode.VelocityChange);
                Smear(chest, 0.42f, true);
            }

            if (parts.TryGetValue("Head", out Rigidbody head)) Smear(head, 0.18f, true);
            if (parts.TryGetValue("Pelvis", out Rigidbody pelvis)) Smear(pelvis, 0.3f, false);
            Blood.Death(_body.transform.position, direction, chest != null ? chest.transform : null, kind, 0.8f);
            Remove(gameObject);
            return parts;
        }

        private static void Smear(Rigidbody part, float width, bool bleeding)
        {
            var smear = part.gameObject.AddComponent<BloodSmear>();
            smear.Width = width;
            smear.Bleeding = bleeding;
            smear.AnySurface = true;
        }

        public Transform Bone(string name)
        {
            return _bones.TryGetValue(name, out Transform bone) ? bone : null;
        }

        private void Pose(float deltaTime)
        {
            transform.rotation = Quaternion.identity;
            _body.transform.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            foreach (string name in Animated) _pose[name] = Vector3.zero;

            Vector3 planar = new(_velocity.x, 0f, _velocity.z);
            float run = Mathf.Clamp01(planar.magnitude / 4.4f);
            Vector3 local = Quaternion.Euler(0f, -_yaw, 0f) * planar / 4.4f;
            float forward = Mathf.Clamp(local.z, -1f, 1f);
            float side = Mathf.Clamp(local.x, -1f, 1f);
            float angle = _phase * Mathf.PI * 2f;
            float sin = Mathf.Sin(angle);
            float cos = Mathf.Cos(angle);
            float swing = 38f * run;

            float legForward = forward >= 0f ? 1f : -0.8f;
            float reach = Mathf.Max(Mathf.Abs(forward), 0.25f) * legForward;
            Add("Hip Left", -sin * swing * reach, 0f, sin * swing * 0.5f * side);
            Add("Hip Right", sin * swing * reach, 0f, -sin * swing * 0.5f * side);
            float kneeLeft = run * (8f + 62f * Mathf.Pow(Mathf.Max(0f, cos), 2f));
            float kneeRight = run * (8f + 62f * Mathf.Pow(Mathf.Max(0f, -cos), 2f));
            Add("Knee Left", kneeLeft, 0f, 0f);
            Add("Knee Right", kneeRight, 0f, 0f);
            Add("Ankle Left", -kneeLeft * 0.35f + sin * swing * reach * 0.4f, 0f, 0f);
            Add("Ankle Right", -kneeRight * 0.35f - sin * swing * reach * 0.4f, 0f, 0f);

            float bob = run * 0.05f * (0.5f - 0.5f * Mathf.Cos(angle * 2f));
            _lean = Mathf.Lerp(_lean, forward * 14f * run, 1f - Mathf.Exp(-deltaTime * 10f));
            Add("Pelvis", 0f, sin * 8f * run, 0f);
            Add("Spine", _lean * 0.5f, -sin * 5f * run, 0f);
            Add("Chest", _lean * 0.5f, -sin * 6f * run, 0f);
            Add("Shoulder Left", sin * 34f * run, 0f, -8f);
            Add("Shoulder Right", -sin * 34f * run, 0f, 8f);
            Add("Elbow Left", -25f * run, 0f, 0f);
            Add("Elbow Right", -25f * run, 0f, 0f);

            float breathe = Mathf.Sin(Time.realtimeSinceStartup * 2.1f) * (1f - run);
            Add("Chest", breathe * 1.5f, 0f, 0f);
            switch (_style)
            {
                case Style.Bat:
                    Bat();
                    break;
                case Style.Knife:
                    Knife();
                    break;
                case Style.Pistol:
                case Style.Shotgun:
                    Gun(_style == Style.Shotgun);
                    break;
                case Style.Grenade:
                    Throw();
                    break;
                default:
                    Fists();
                    break;
            }

            if (Dragging)
            {
                _pose["Shoulder Right"] = new Vector3(35f, -10f, 18f);
                _pose["Elbow Right"] = new Vector3(-8f, 0f, 0f);
                Add("Chest", 12f, -10f, 0f);
                Add("Spine", 8f, 0f, 0f);
            }

            _flinch = Vector3.Lerp(_flinch, Vector3.zero, 1f - Mathf.Exp(-deltaTime * 7f));
            Add("Spine", -_flinch.x * 0.5f, 0f, _flinch.z * 0.4f);
            Add("Chest", -_flinch.x * 0.6f, 0f, _flinch.z * 0.5f);
            Add("Head", -_flinch.x * 0.8f, 0f, _flinch.z * 0.6f);

            foreach (KeyValuePair<string, Vector3> entry in _pose)
                if (_bones.TryGetValue(entry.Key, out Transform bone))
                    bone.localRotation = _rest[entry.Key] * Quaternion.Euler(entry.Value);

            Transform pelvis = Bone("Pelvis");
            pelvis.localPosition = new Vector3(pelvis.localPosition.x, _pelvisHeight - bob - Mathf.Abs(_flinch.x) * 0.002f, pelvis.localPosition.z);
            if (!Dragging && (_style == Style.Pistol || _style == Style.Shotgun)) Aim(_style == Style.Shotgun);

            if (_flash > 0f)
            {
                _flash = Mathf.Max(0f, _flash - deltaTime * 12f);
                for (int i = 0; i < _renderers.Count; i++) Paint(i, _flash);
            }
        }

        private void Bat()
        {
            float t = Attacking ? _attack : -1f;
            Vector3 guardRight = new(-35f, -10f, 20f);
            float guardElbow = -95f;
            float guardGrip = 70f;
            Vector3 right = guardRight;
            float elbow = guardElbow;
            float grip = guardGrip;
            float chest = 0f;
            float pelvis = 0f;
            float spine = 0f;
            if (t >= 0f)
            {
                var windRight = new Vector3(-20f, 55f, 80f);
                var strikeRight = new Vector3(-15f, -85f, 80f);
                if (t < 0.32f)
                {
                    float k = Ease.OutCubic(t / 0.32f);
                    right = Vector3.Lerp(guardRight, windRight, k);
                    elbow = Mathf.Lerp(guardElbow, -110f, k);
                    grip = Mathf.Lerp(guardGrip, 120f, k);
                    chest = Mathf.Lerp(0f, 42f, k);
                    pelvis = Mathf.Lerp(0f, 14f, k);
                    spine = Mathf.Lerp(0f, -6f, k);
                }
                else if (t < 0.52f)
                {
                    float k = Ease.InOutCubic((t - 0.32f) / 0.2f);
                    right = Vector3.Lerp(windRight, strikeRight, k);
                    elbow = Mathf.Lerp(-110f, -12f, k);
                    grip = Mathf.Lerp(120f, 178f, k);
                    chest = Mathf.Lerp(42f, -55f, k);
                    pelvis = Mathf.Lerp(14f, -18f, k);
                    spine = Mathf.Lerp(-6f, 8f, k);
                }
                else
                {
                    float k = Ease.InOutCubic((t - 0.52f) / 0.48f);
                    right = Vector3.Lerp(strikeRight, guardRight, k);
                    elbow = Mathf.Lerp(-12f, guardElbow, k);
                    grip = Mathf.Lerp(178f, guardGrip, k);
                    chest = Mathf.Lerp(-55f, 0f, k);
                    pelvis = Mathf.Lerp(-18f, 0f, k);
                    spine = Mathf.Lerp(8f, 0f, k);
                }
            }

            _pose["Shoulder Right"] = right;
            _pose["Elbow Right"] = new Vector3(elbow, 0f, 0f);
            Add("Shoulder Left", -28f + chest * 0.25f, 0f, -14f);
            Add("Elbow Left", -60f, 0f, 0f);
            Add("Chest", spine, chest * 0.6f, 0f);
            Add("Spine", 0f, chest * 0.4f, 0f);
            Add("Pelvis", 0f, pelvis, 0f);
            Add("Head", 0f, -chest * 0.5f, 0f);
            if (_grip != null) _grip.localRotation = Quaternion.Euler(grip, 0f, 0f);
        }

        private void Knife()
        {
            float t = Attacking ? _attack : -1f;
            float reach = 0f;
            if (t >= 0f) reach = t < 0.3f ? -0.4f * Ease.OutCubic(t / 0.3f) : t < 0.45f ? Mathf.Lerp(-0.4f, 1f, Ease.OutCubic((t - 0.3f) / 0.15f)) : Mathf.Lerp(1f, 0f, Ease.InOutCubic((t - 0.45f) / 0.55f));

            _pose["Shoulder Right"] = new Vector3(-45f - 45f * reach, -10f * reach, 12f);
            _pose["Elbow Right"] = new Vector3(-95f + 90f * Mathf.Max(0f, reach), 0f, 0f);
            Add("Shoulder Left", -35f, 0f, -16f);
            Add("Elbow Left", -80f, 0f, 0f);
            Add("Chest", 4f + 10f * reach, -26f * reach, 0f);
            Add("Spine", 3f * reach, -10f * reach, 0f);
            Add("Pelvis", 0f, -8f * reach, 0f);
            if (_grip != null) _grip.localRotation = Quaternion.Euler(Mathf.Lerp(90f, 160f, Mathf.Max(0f, reach)), 0f, 0f);
        }

        private void Gun(bool long_)
        {
            float t = Attacking ? _attack : -1f;
            float kick = t >= 0f ? (t < 0.12f ? Ease.OutCubic(t / 0.12f) : 1f - Ease.InOutCubic((t - 0.12f) / (long_ ? 0.5f : 0.88f))) : 0f;
            kick = Mathf.Clamp01(kick);
            float pump = long_ && t > 0.35f && t < 0.85f ? Mathf.Sin((t - 0.35f) / 0.5f * Mathf.PI) : 0f;
            _pose["Shoulder Right"] = new Vector3(-82f - kick * (long_ ? 18f : 22f), long_ ? 8f : -4f, long_ ? 4f : 6f);
            _pose["Elbow Right"] = new Vector3(long_ ? -18f : -6f, 0f, 0f);
            _pose["Shoulder Left"] = new Vector3(long_ ? -70f - kick * 10f + pump * 12f : -72f - kick * 18f, long_ ? -30f : -38f, -18f);
            _pose["Elbow Left"] = new Vector3(long_ ? -42f + pump * 25f : -48f, 0f, 0f);
            Add("Chest", -kick * (long_ ? 9f : 4f), long_ ? 10f : 6f, 0f);
            Add("Spine", -kick * (long_ ? 5f : 2f), 4f, 0f);
            Add("Head", 0f, long_ ? -12f : -6f, 0f);
            if (_grip != null) _grip.localRotation = Quaternion.Euler(180f, 0f, 0f);
        }

        // Points the arms in world space after the pose: the gun barrel follows the facing, the off hand holds the gun.
        private void Aim(bool long_)
        {
            float t = Attacking ? _attack : -1f;
            float kick = t >= 0f ? (t < 0.12f ? Ease.OutCubic(t / 0.12f) : 1f - Ease.InOutCubic((t - 0.12f) / (long_ ? 0.5f : 0.88f))) : 0f;
            kick = Mathf.Clamp01(kick);
            float pump = long_ && t > 0.35f && t < 0.85f ? Mathf.Sin((t - 0.35f) / 0.5f * Mathf.PI) : 0f;
            Vector3 facing = Facing;
            Vector3 right = Vector3.Cross(Vector3.up, facing);
            Vector3 aim = Quaternion.AngleAxis(-kick * (long_ ? 14f : 18f) + 4f, right) * facing;
            Transform chest = Bone("Chest");
            Transform shoulder = Bone("Shoulder Right");
            Vector3 hand = long_
                ? chest.position + facing * 0.3f + right * 0.13f + Vector3.up * 0.1f - aim * kick * 0.06f
                : shoulder.position + Vector3.Lerp(aim, -right, 0.12f).normalized * 0.6f - Vector3.up * 0.04f - aim * kick * 0.05f;
            Reach("Shoulder Right", "Elbow Right", hand, long_ ? -70f : -12f, right);
            if (_grip == null) return;

            Quaternion level = Quaternion.LookRotation(aim, Vector3.up);
            _grip.rotation = level * Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, 180f, 0f);
            Vector3 support = long_ ? _grip.position + aim * (0.3f - pump * 0.1f) - Vector3.up * 0.02f : _grip.position - right * 0.035f - aim * 0.02f;
            Reach("Shoulder Left", "Elbow Left", support, long_ ? -35f : -40f, -right);
        }

        // Bends the elbow so the arm is as long as the distance to the target, then turns the shoulder at it.
        private void Reach(string shoulderName, string elbowName, Vector3 target, float bendSign, Vector3 outward)
        {
            Transform shoulder = Bone(shoulderName);
            Transform elbow = Bone(elbowName);
            float upper = Vector3.Distance(shoulder.position, elbow.position);
            const float lower = 0.3f;
            float distance = Mathf.Clamp(Vector3.Distance(shoulder.position, target), Mathf.Abs(upper - lower) + 0.01f, upper + lower - 0.001f);
            float inner = Mathf.Acos(Mathf.Clamp((upper * upper + lower * lower - distance * distance) / (2f * upper * lower), -1f, 1f)) * Mathf.Rad2Deg;
            elbow.localRotation = _rest[elbowName] * Quaternion.Euler(Mathf.Sign(bendSign) * (180f - inner), 0f, 0f);
            for (int i = 0; i < 2; i++)
            {
                Vector3 hand = elbow.TransformPoint(0f, -lower, 0f);
                shoulder.rotation = Quaternion.FromToRotation(hand - shoulder.position, target - shoulder.position) * shoulder.rotation;
            }

            // Swing the elbow out and down around the shoulder to hand line so it never points up.
            Vector3 axis = (target - shoulder.position).normalized;
            Vector3 elbowDir = Vector3.ProjectOnPlane(elbow.position - shoulder.position, axis);
            Vector3 wanted = Vector3.ProjectOnPlane(outward - Vector3.up * 0.8f, axis);
            if (elbowDir.sqrMagnitude > 1e-6f && wanted.sqrMagnitude > 1e-6f)
                shoulder.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(elbowDir, wanted, axis), axis) * shoulder.rotation;
        }

        private void Throw()
        {
            float t = Attacking ? _attack : -1f;
            Vector3 guard = new(-25f, 0f, 14f);
            Vector3 cock = new(-150f, 20f, 25f);
            Vector3 release = new(-60f, -15f, 8f);
            Vector3 right = guard;
            float elbow = -70f;
            float chest = 0f;
            if (t >= 0f)
            {
                if (t < 0.32f)
                {
                    float k = Ease.OutCubic(t / 0.32f);
                    right = Vector3.Lerp(guard, cock, k);
                    elbow = Mathf.Lerp(-70f, -110f, k);
                    chest = Mathf.Lerp(0f, 30f, k);
                }
                else if (t < 0.5f)
                {
                    float k = Ease.InOutCubic((t - 0.32f) / 0.18f);
                    right = Vector3.Lerp(cock, release, k);
                    elbow = Mathf.Lerp(-110f, -10f, k);
                    chest = Mathf.Lerp(30f, -30f, k);
                }
                else
                {
                    float k = Ease.InOutCubic((t - 0.5f) / 0.5f);
                    right = Vector3.Lerp(release, guard, k);
                    elbow = Mathf.Lerp(-10f, -70f, k);
                    chest = Mathf.Lerp(-30f, 0f, k);
                }
            }

            _pose["Shoulder Right"] = right;
            _pose["Elbow Right"] = new Vector3(elbow, 0f, 0f);
            Add("Shoulder Left", -50f + chest * 0.6f, 0f, -20f);
            Add("Elbow Left", -40f, 0f, 0f);
            Add("Chest", chest * -0.15f, chest * 0.6f, 0f);
            Add("Spine", 0f, chest * 0.4f, 0f);
            if (_grip != null) _grip.gameObject.SetActive(t < 0.45f || t > 0.9f);
        }

        private void Fists()
        {
            float t = Attacking ? _attack : -1f;
            float reach = 0f;
            if (t >= 0f) reach = t < 0.4f ? -0.3f * Ease.OutCubic(t / 0.4f) : t < 0.55f ? Mathf.Lerp(-0.3f, 1f, Ease.OutCubic((t - 0.4f) / 0.15f)) : Mathf.Lerp(1f, 0f, Ease.InOutCubic((t - 0.55f) / 0.45f));

            Add("Shoulder Right", -50f - 40f * reach, 0f, 10f);
            Add("Elbow Right", -100f + 95f * Mathf.Max(0f, reach), 0f, 0f);
            Add("Shoulder Left", -55f, 0f, -10f);
            Add("Elbow Left", -110f, 0f, 0f);
            Add("Chest", 6f + 8f * reach, -22f * reach, 0f);
            Add("Pelvis", 0f, -8f * reach, 0f);
        }

        private void Add(string bone, float x, float y, float z)
        {
            _pose[bone] = (_pose.TryGetValue(bone, out Vector3 value) ? value : Vector3.zero) + new Vector3(x, y, z);
        }

        private void Paint(int index, float flash)
        {
            Renderer part = _renderers[index];
            if (part == null) return;
            part.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, Color.Lerp(_colours[index], new Color(1.5f, 1.35f, 1.3f), flash * 0.7f));
            part.SetPropertyBlock(_block);
        }

        private static Transform MakeBat(Transform hand)
        {
            var grip = new GameObject("Grip").transform;
            grip.SetParent(hand, false);
            grip.localPosition = new Vector3(0f, -0.3f, 0.01f);
            Material wood = Surface("Bat Wood", new Color(0.52f, 0.33f, 0.18f), 0.45f, 0f);
            Piece(grip, PrimitiveType.Cylinder, new Vector3(0f, 0.38f, 0f), new Vector3(0.05f, 0.33f, 0.05f), wood);
            Piece(grip, PrimitiveType.Cylinder, new Vector3(0f, 0.62f, 0f), new Vector3(0.075f, 0.16f, 0.075f), wood);
            Piece(grip, PrimitiveType.Sphere, new Vector3(0f, 0.78f, 0f), new Vector3(0.075f, 0.04f, 0.075f), wood);
            var tape = new Material(wood) { name = "Bat Tape" };
            tape.SetColor(BaseColorId, new Color(0.06f, 0.06f, 0.065f));
            Piece(grip, PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(0.056f, 0.08f, 0.056f), tape);
            return grip;
        }

        private static Material Metal(Color colour, float smoothness)
        {
            return Surface("Weapon", colour, smoothness, 0.6f);
        }

        private static Material Surface(string name, Color colour, float smoothness, float metallic)
        {
            // A copy of the dummy's own Lit material: a material made from Shader.Find can miss its shader in a build.
            Material material = _surface != null ? new Material(_surface) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = name;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", null);
            material.SetColor(BaseColorId, colour);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            return material;
        }

        private static Transform MakeKnife(Transform hand)
        {
            var grip = new GameObject("Grip").transform;
            grip.SetParent(hand, false);
            grip.localPosition = new Vector3(0f, -0.3f, 0.01f);
            Material steel = Metal(new Color(0.75f, 0.76f, 0.78f), 0.8f);
            Material handle = Metal(new Color(0.08f, 0.08f, 0.09f), 0.3f);
            Piece(grip, PrimitiveType.Cube, new Vector3(0f, 0.02f, 0f), new Vector3(0.03f, 0.11f, 0.025f), handle);
            Piece(grip, PrimitiveType.Cube, new Vector3(0f, 0.17f, 0f), new Vector3(0.008f, 0.2f, 0.035f), steel);
            return grip;
        }

        private static Transform MakeGun(Transform hand, bool long_)
        {
            var grip = new GameObject("Grip").transform;
            grip.SetParent(hand, false);
            grip.localPosition = new Vector3(0f, -0.3f, 0.02f);
            Material dark = Metal(new Color(0.07f, 0.07f, 0.08f), 0.55f);
            if (long_)
            {
                Material wood = Metal(new Color(0.35f, 0.2f, 0.1f), 0.35f);
                wood.SetFloat("_Metallic", 0f);
                Piece(grip, PrimitiveType.Cube, new Vector3(0f, -0.12f, -0.03f), new Vector3(0.045f, 0.28f, 0.07f), wood);
                Piece(grip, PrimitiveType.Cylinder, new Vector3(0f, 0.3f, 0.015f), new Vector3(0.035f, 0.32f, 0.035f), dark);
                Piece(grip, PrimitiveType.Cylinder, new Vector3(0f, 0.28f, -0.02f), new Vector3(0.04f, 0.14f, 0.04f), wood);
            }
            else
            {
                Piece(grip, PrimitiveType.Cube, new Vector3(0f, 0.08f, 0.02f), new Vector3(0.03f, 0.2f, 0.04f), dark);
                Piece(grip, PrimitiveType.Cube, new Vector3(0f, -0.01f, -0.03f), new Vector3(0.028f, 0.05f, 0.09f), dark);
            }

            return grip;
        }

        private static Transform MakeGrenade(Transform hand)
        {
            var grip = new GameObject("Grip").transform;
            grip.SetParent(hand, false);
            grip.localPosition = new Vector3(0f, -0.33f, 0.03f);
            Piece(grip, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.075f, 0.09f, 0.075f), Metal(new Color(0.2f, 0.24f, 0.14f), 0.3f));
            return grip;
        }

        private static void Piece(Transform parent, PrimitiveType shape, Vector3 position, Vector3 scale, Material material)
        {
            GameObject piece = GameObject.CreatePrimitive(shape);
            Remove(piece.GetComponent<Collider>());
            piece.layer = DummyRagdoll.BodyLayer;
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = position;
            piece.transform.localScale = scale;
            piece.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void Remove(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        private static class Ease
        {
            public static float OutCubic(float t)
            {
                t = Mathf.Clamp01(t);
                return 1f - (1f - t) * (1f - t) * (1f - t);
            }

            public static float InOutCubic(float t)
            {
                t = Mathf.Clamp01(t);
                return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
            }
        }
    }
}
