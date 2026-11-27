using System.Collections.Generic;
using UnityEngine;

namespace Rng.Blood.Demo
{
    public class ArenaDemo : MonoBehaviour
    {
        private const int CorpseLimit = 14;
        private static readonly Color PlayerTint = new(1f, 0.96f, 0.86f);
        private static readonly Color EnemyTint = new(0.42f, 0.43f, 0.46f);

        public struct Command
        {
            public Vector3 Move;
            public Vector3 Aim;
            public bool Attack;
            public bool Drag;
            public int Weapon;
        }

        private struct Grenade
        {
            public Rigidbody Body;
            public float At;
        }

        private struct Flash
        {
            public GameObject Thing;
            public float Until;
        }

        private class Corpse
        {
            public GameObject Body;
            public List<Rigidbody> Parts;
        }

        [SerializeField] private BloodSettings _settings;
        [SerializeField] private GameObject _dummy;
        [SerializeField] private Camera _camera;
        [SerializeField, Tooltip("Unlit material for muzzle flashes, tracers and explosions.")] private Material _glowMaterial;
        [SerializeField, Tooltip("Half size of the floor inside the walls.")] private Vector2 _room = new(6.5f, 6.5f);
        [SerializeField, Range(6f, 20f)] private float _height = 9f;
        [SerializeField, Range(1, 8)] private int _crowd = 4;

        private readonly List<ArenaFighter> _enemies = new();
        private readonly List<Corpse> _corpses = new();
        private readonly List<float> _cooldowns = new();
        private readonly List<Grenade> _grenades = new();
        private readonly List<Flash> _flashes = new();
        private Rigidbody _hand;
        private SpringJoint _drag;
        private Material _glow;
        private ArenaFighter _player;
        private BloodTuner _tuner;
        private float _freeze;
        private float _shake;
        private float _spawnAt;
        private float _clock;
        private int _kills;
        private Vector3 _aim;
        private float _frameTime = 1f / 60f;
        private GUIStyle _title;
        private GUIStyle _small;
        private GUIStyle _button;
        private Texture2D _panel;

        public bool Scripted { get; set; }
        public Command Script { get; set; }
        public ArenaFighter Player => _player;
        public IReadOnlyList<ArenaFighter> Enemies => _enemies;
        public int Kills => _kills;

        public float Height
        {
            get => _height;
            set => _height = Mathf.Clamp(value, 3f, 20f);
        }
        public int Crowd
        {
            get => _crowd;
            set => _crowd = Mathf.Clamp(value, 0, 8);
        }

        private void Start()
        {
            _tuner = gameObject.AddComponent<BloodTuner>();
            Begin();
        }

        public void Begin()
        {
            Blood.Initialize(_settings);
            _player = Spawn(transform.position, ArenaFighter.Style.Bat, PlayerTint, 0f);
            _player.Speed = 4.4f;
            _player.Health = float.PositiveInfinity;
            _spawnAt = 1.2f;
            Follow(-1f);
        }

        private void Update()
        {
            _frameTime = Mathf.Lerp(_frameTime, Time.unscaledDeltaTime, 0.05f);
            if (_tuner != null && BloodTuner.Available && DemoInput.Tab()) _tuner.Toggle();
            float scale = Step(Time.unscaledDeltaTime);
            Time.timeScale = scale;
        }

        private void FixedUpdate()
        {
            Weigh();
        }

        public void Weigh()
        {
            foreach (Corpse corpse in _corpses)
                foreach (Rigidbody part in corpse.Parts)
                    if (part != null && !part.IsSleeping())
                        part.AddForce(Physics.gravity * 0.7f, ForceMode.Acceleration);
        }

        private void OnDisable()
        {
            if (Application.isPlaying) Time.timeScale = 1f;
        }

        public float Step(float deltaTime)
        {
            float scale = _freeze > 0f ? 0.05f : 1f;
            _freeze -= deltaTime;
            float dt = Mathf.Min(deltaTime, 0.05f) * scale;
            _clock += dt;
            if (_player == null) return scale;

            Command command = Scripted ? Script : Read();
            _aim = command.Aim;
            if (command.Weapon > 0 && command.Weapon - 1 != (int)_player.Weapon) _player.Arm((ArenaFighter.Style)(command.Weapon - 1));
            Drag(command.Drag);
            _player.Burden = _drag != null ? 0.6f : 1f;
            _player.Dragging = _drag != null;
            _player.Drive(command.Move, command.Aim, command.Attack && _drag == null, dt);
            if (_drag != null && _hand != null)
            {
                _hand.MovePosition(_player.Hand);
                if (!Application.isPlaying) _hand.position = _player.Hand;
            }

            if (_player.Striking) Swing(_player, true);
            Tick();

            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                ArenaFighter enemy = _enemies[i];
                if (enemy == null)
                {
                    _enemies.RemoveAt(i);
                    _cooldowns.RemoveAt(i);
                    continue;
                }

                Think(enemy, i, dt);
                if (enemy.Striking) Swing(enemy, false);
            }

            Separate(dt);
            Kick(dt);
            if (_clock >= _spawnAt && _enemies.Count < _crowd)
            {
                _spawnAt = _clock + Random.Range(0.9f, 1.8f);
                Vector3 at = Edge();
                ArenaFighter enemy = Spawn(at, ArenaFighter.Style.Fists, EnemyTint, Mathf.Atan2(-at.x, -at.z) * Mathf.Rad2Deg);
                enemy.Speed = Random.Range(2.3f, 3f);
                enemy.Acceleration = 14f;
                enemy.AttackSeconds = 0.55f;
                enemy.Health = 3f;
                _enemies.Add(enemy);
                _cooldowns.Add(Random.Range(0.4f, 1f));
            }

            Follow(dt);
            return scale;
        }

        private Command Read()
        {
            var command = new Command();
            Vector2 move = DemoInput.Move();
            Vector3 forward = _camera.transform.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            command.Move = right * move.x + forward * move.y;
            Ray ray = _camera.ScreenPointToRay(DemoInput.Pointer());
            float floor = _player.transform.position.y;
            command.Aim = new Plane(Vector3.up, Vector3.up * floor).Raycast(ray, out float distance) ? ray.GetPoint(distance) : _player.transform.position + _player.Facing;
            bool overPanel = _tuner != null && _tuner.Open;
            command.Attack = !overPanel && GUIUtility.hotControl == 0 && DemoInput.Held(0);
            command.Drag = !overPanel && DemoInput.Held(1);
            for (int digit = 1; digit <= 5; digit++)
                if (DemoInput.Digit(digit))
                    command.Weapon = digit;
            return command;
        }

        private void Think(ArenaFighter enemy, int index, float dt)
        {
            Vector3 to = _player.transform.position - enemy.transform.position;
            to.y = 0f;
            float distance = to.magnitude;
            _cooldowns[index] -= dt;
            Vector3 move = distance > 1.15f ? to / Mathf.Max(0.001f, distance) : Vector3.zero;
            bool attack = distance < 1.35f && _cooldowns[index] <= 0f && !enemy.Attacking;
            if (attack) _cooldowns[index] = Random.Range(1.1f, 1.9f);
            enemy.Drive(move, _player.transform.position, attack, dt);
        }

        private void Swing(ArenaFighter attacker, bool player)
        {
            Vector3 origin = attacker.transform.position;
            Vector3 facing = attacker.Facing;
            bool knife = player && attacker.Weapon == ArenaFighter.Style.Knife;
            float reach = player ? (knife ? 1.35f : 1.75f) : 1.45f;
            float arc = player ? (knife ? 0.45f : 0.2f) : 0.55f;
            if (player && attacker.Weapon == ArenaFighter.Style.Pistol)
            {
                Fire(1, 1.5f, 1.6f, 3f);
                return;
            }

            if (player && attacker.Weapon == ArenaFighter.Style.Shotgun)
            {
                Fire(8, 9f, 0.7f, 1.7f);
                return;
            }

            if (player && attacker.Weapon == ArenaFighter.Style.Grenade)
            {
                Throw();
                return;
            }

            if (player)
            {
                bool landed = false;
                for (int i = _enemies.Count - 1; i >= 0; i--)
                {
                    ArenaFighter enemy = _enemies[i];
                    if (enemy == null || !Reaches(origin, facing, enemy.transform.position, reach, arc)) continue;

                    landed = true;
                    Vector3 direction = knife ? Flat(enemy.transform.position - origin) : Flat(enemy.transform.position - origin) * 0.6f + Vector3.Cross(Vector3.up, facing) * -0.4f;
                    bool head = Random.value < (knife ? 0.2f : 0.3f);
                    Transform bone = enemy.Bone(head ? "Head" : "Chest");
                    Vector3 point = bone.position + Vector3.up * (head ? 0.08f : 0.12f) - direction.normalized * 0.15f;
                    BloodHitKind kind = knife ? BloodHitKind.Sharp : BloodHitKind.Blunt;
                    enemy.Hurt(point, direction, kind, knife ? 1.2f : head ? 1.5f : 1f, knife ? 2.5f : 4.5f, bone);
                    if (enemy.Health > 0f) continue;

                    Kill(i, direction, knife ? 2.5f : 4.2f, head && !knife ? BloodHitKind.Headshot : kind);
                }

                foreach (Corpse corpse in _corpses)
                {
                    if (corpse.Body == null || corpse.Parts.Count == 0 || corpse.Parts[0] == null) continue;
                    if (!Reaches(origin, facing, corpse.Parts[0].position, reach, arc)) continue;

                    Vector3 direction = Flat(corpse.Parts[0].position - origin);
                    foreach (Rigidbody part in corpse.Parts)
                        if (part != null) part.AddForce((direction + Vector3.up * 0.15f) * 2.6f, ForceMode.VelocityChange);
                    landed = true;
                }

                if (landed)
                {
                    _freeze = Mathf.Max(_freeze, 0.065f);
                    _shake = Mathf.Max(_shake, 0.22f);
                }

                return;
            }

            if (!Reaches(origin, facing, _player.transform.position, reach, arc)) return;

            Vector3 blow = Flat(_player.transform.position - origin);
            Transform target = _player.Bone(Random.value < 0.4f ? "Head" : "Chest");
            _player.Hurt(target.position - blow * 0.12f + Vector3.up * 0.08f, blow, BloodHitKind.Blunt, 0f, 2.5f, target);
            _shake = Mathf.Max(_shake, 0.15f);
        }

        private void Kill(int index, Vector3 direction, float force, BloodHitKind kind)
        {
            ArenaFighter enemy = _enemies[index];
            Dictionary<string, Rigidbody> parts = enemy.Die(direction, force, kind);
            var corpse = new Corpse { Body = enemy.Body, Parts = new List<Rigidbody>(parts.Values) };
            _corpses.Add(corpse);
            Ignore(corpse, _player);
            foreach (ArenaFighter other in _enemies)
                if (other != null && other != enemy)
                    Ignore(corpse, other);
            _enemies.RemoveAt(index);
            _cooldowns.RemoveAt(index);
            _kills++;
            Trim();
            _freeze = Mathf.Max(_freeze, 0.11f);
            _shake = Mathf.Max(_shake, 0.45f);
        }

        private void Fire(int pellets, float spread, float damage, float force)
        {
            Vector3 facing = _player.Facing;
            Vector3 origin = _player.transform.position + Vector3.up * 1.3f + facing * 0.3f;
            Vector3 muzzle = _player.Muzzle;
            Glow(muzzle, 0.12f, 0.05f, new Color(1f, 0.75f, 0.4f), 3.5f);
            _shake = Mathf.Max(_shake, pellets > 1 ? 0.4f : 0.16f);
            int mask = (1 << DummyRagdoll.BodyLayer) | (Blood.Settings != null ? Blood.Settings.Surfaces.value : 1);
            var hurt = new HashSet<ArenaFighter>();
            for (int p = 0; p < pellets; p++)
            {
                Vector3 direction = Quaternion.Euler(0f, Random.Range(-spread, spread), 0f) * facing;
                Vector3 end = origin + direction * 25f;
                RaycastHit[] hits = Physics.RaycastAll(origin, direction, 25f, mask, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (RaycastHit hit in hits)
                {
                    if (hit.collider.GetComponentInParent<ArenaFighter>() is ArenaFighter target)
                    {
                        if (target == _player) continue;

                        int index = _enemies.IndexOf(target);
                        if (index < 0) continue;

                        bool head = Random.value < (pellets > 1 ? 0.12f : 0.25f);
                        Transform bone = target.Bone(head ? "Head" : "Chest");
                        BloodHitKind kind = pellets > 1 ? (hurt.Contains(target) ? BloodHitKind.Bullet : BloodHitKind.Shotgun) : head ? BloodHitKind.Headshot : hit.distance < 2.5f ? BloodHitKind.BulletClose : BloodHitKind.Bullet;
                        Vector3 point = new Vector3(hit.point.x, bone.position.y + (head ? 0.1f : 0.12f), hit.point.z);
                        target.Hurt(point, direction, kind, head ? damage * 2f : damage, force, bone);
                        hurt.Add(target);
                        end = point;
                        if (target.Health <= 0f) Kill(index, direction, pellets > 1 ? 7f : 4.5f, kind);
                        break;
                    }

                    if (hit.rigidbody != null && hit.collider.gameObject.layer == DummyRagdoll.BodyLayer)
                    {
                        hit.rigidbody.AddForce(direction * 3f, ForceMode.VelocityChange);
                        end = hit.point;
                        break;
                    }

                    end = hit.point;
                    break;
                }

                Tracer(muzzle, end);
            }

            if (hurt.Count > 0) _freeze = Mathf.Max(_freeze, 0.05f);
        }

        private void Throw()
        {
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Grenade";
            ball.layer = 1;
            ball.transform.position = _player.Hand + Vector3.up * 0.1f;
            ball.transform.localScale = Vector3.one * 0.09f;
            ball.GetComponent<Renderer>().sharedMaterial = Glow(Color.black, 0f);
            var body = ball.AddComponent<Rigidbody>();
            body.mass = 0.4f;
#if UNITY_6000_0_OR_NEWER
            body.angularDamping = 0.5f;
#else
            body.angularDrag = 0.5f;
#endif
            if (_player.TryGetComponent(out CharacterController controller)) Physics.IgnoreCollision(ball.GetComponent<Collider>(), controller);
            Vector3 target = _aim;
            Vector3 start = ball.transform.position;
            Vector3 offset = target - start;
            offset.y = 0f;
            if (offset.magnitude > 9f) target = start + offset.normalized * 9f;
            const float flight = 0.75f;
            Vector3 velocity = (target - start) / flight - 0.5f * Physics.gravity * flight;
#if UNITY_6000_0_OR_NEWER
            body.linearVelocity = velocity;
#else
            body.velocity = velocity;
#endif
            _grenades.Add(new Grenade { Body = body, At = _clock + 1.35f });
        }

        private void Explode(Vector3 at)
        {
            Glow(at + Vector3.up * 0.3f, 0.9f, 0.14f, new Color(1f, 0.6f, 0.25f), 40f);
            _shake = Mathf.Max(_shake, 1f);
            _freeze = Mathf.Max(_freeze, 0.12f);
            for (int i = _enemies.Count - 1; i >= 0; i--)
            {
                ArenaFighter enemy = _enemies[i];
                if (enemy == null) continue;

                Vector3 away = enemy.transform.position - at;
                float distance = new Vector2(away.x, away.z).magnitude;
                if (distance > 3.4f) continue;

                Vector3 direction = Flat(away) + Vector3.up * 0.15f;
                Transform chest = enemy.Chest;
                enemy.Hurt(chest.position - Flat(away) * 0.15f, Flat(away), BloodHitKind.Explosion, 10f, 4f, chest);
                Kill(i, direction, Mathf.Lerp(7f, 3f, distance / 3.4f), BloodHitKind.Explosion);
            }

            foreach (Corpse corpse in _corpses)
                foreach (Rigidbody part in corpse.Parts)
                {
                    if (part == null) continue;
                    Vector3 away = part.position - at;
                    float distance = away.magnitude;
                    if (distance > 3.6f) continue;
                    part.AddForce((Flat(away) + Vector3.up * 0.25f) * Mathf.Lerp(5f, 1f, distance / 3.6f), ForceMode.VelocityChange);
                }

            Vector3 toPlayer = _player.transform.position - at;
            if (toPlayer.magnitude < 2.2f)
            {
                Transform chest = _player.Chest;
                _player.Hurt(chest.position, Flat(toPlayer), BloodHitKind.Blunt, 0f, 5f, chest);
            }
        }

        private void Tick()
        {
            for (int i = _grenades.Count - 1; i >= 0; i--)
            {
                Grenade grenade = _grenades[i];
                if (grenade.Body == null)
                {
                    _grenades.RemoveAt(i);
                    continue;
                }

                if (_clock < grenade.At) continue;

                Vector3 at = grenade.Body.position;
                Remove(grenade.Body.gameObject);
                _grenades.RemoveAt(i);
                Explode(at);
            }

            for (int i = _flashes.Count - 1; i >= 0; i--)
            {
                if (_flashes[i].Thing != null && _clock < _flashes[i].Until) continue;

                if (_flashes[i].Thing != null) Remove(_flashes[i].Thing);
                _flashes.RemoveAt(i);
            }
        }

        private void Glow(Vector3 at, float size, float seconds, Color colour, float intensity)
        {
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Flash";
            Remove(ball.GetComponent<Collider>());
            ball.layer = DummyRagdoll.BodyLayer;
            ball.transform.position = at;
            ball.transform.localScale = Vector3.one * size;
            ball.GetComponent<Renderer>().sharedMaterial = Glow(colour * 6f, 1f);
            ball.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(ball.transform, false);
            light.type = LightType.Point;
            light.color = colour;
            light.intensity = intensity;
            light.range = size * 8f;
            _flashes.Add(new Flash { Thing = ball, Until = _clock + seconds });
        }

        private Material Glow(Color colour, float unused)
        {
            if (_glow == null) _glow = _glowMaterial != null ? _glowMaterial : new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Arena Glow" };
            var material = new Material(_glow);
            material.SetColor("_BaseColor", colour);
            return material;
        }

        private void Tracer(Vector3 from, Vector3 to)
        {
            var line = new GameObject("Tracer").AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.startWidth = 0.025f;
            line.endWidth = 0.01f;
            line.sharedMaterial = Glow(new Color(1f, 0.85f, 0.55f) * 3f, 0f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _flashes.Add(new Flash { Thing = line.gameObject, Until = _clock + 0.05f });
        }

        private void Drag(bool held)
        {
            if (!held)
            {
                if (_drag != null) Remove(_drag);
                _drag = null;
                return;
            }

            if (_drag != null) return;

            Rigidbody best = null;
            float bestDistance = 1.3f;
            foreach (Corpse corpse in _corpses)
                foreach (Rigidbody part in corpse.Parts)
                {
                    if (part == null) continue;
                    float distance = Vector3.Distance(new Vector3(part.position.x, 0f, part.position.z), new Vector3(_aim.x, 0f, _aim.z));
                    float reach = Vector3.Distance(new Vector3(part.position.x, 0f, part.position.z), new Vector3(_player.transform.position.x, 0f, _player.transform.position.z));
                    if (distance < bestDistance && reach < 2.4f)
                    {
                        bestDistance = distance;
                        best = part;
                    }
                }

            if (best == null) return;

            if (_hand == null)
            {
                _hand = new GameObject("Hand").AddComponent<Rigidbody>();
                _hand.isKinematic = true;
            }

            _hand.position = _player.Hand;
            _hand.transform.position = _player.Hand;
            _drag = best.gameObject.AddComponent<SpringJoint>();
            _drag.connectedBody = _hand;
            _drag.autoConfigureConnectedAnchor = false;
            _drag.anchor = Vector3.zero;
            _drag.connectedAnchor = Vector3.zero;
            _drag.spring = 3200f;
            _drag.damper = 220f;
            _drag.maxDistance = 0.05f;
        }

        private static void Remove(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        public bool NearestBody(Vector3 from, out Vector3 point)
        {
            point = from;
            float best = float.MaxValue;
            foreach (Corpse corpse in _corpses)
            {
                if (corpse.Parts.Count < 3 || corpse.Parts[2] == null) continue;
                float distance = Vector3.Distance(corpse.Parts[2].position, from);
                if (distance >= best) continue;
                best = distance;
                point = corpse.Parts[2].position;
            }

            return best < float.MaxValue;
        }

        private static bool Reaches(Vector3 origin, Vector3 facing, Vector3 target, float reach, float arc)
        {
            Vector3 to = Flat(target - origin);
            float distance = Vector3.Distance(new Vector3(origin.x, 0f, origin.z), new Vector3(target.x, 0f, target.z));
            return distance < reach && Vector3.Dot(to, facing) > arc;
        }

        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector.sqrMagnitude > 0.0001f ? vector.normalized : Vector3.forward;
        }

        private void Separate(float dt)
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                ArenaFighter a = _enemies[i];
                if (a == null) continue;

                for (int j = i + 1; j < _enemies.Count; j++)
                {
                    ArenaFighter b = _enemies[j];
                    if (b == null) continue;

                    Vector3 apart = a.transform.position - b.transform.position;
                    apart.y = 0f;
                    float distance = apart.magnitude;
                    if (distance > 0.8f || distance < 0.0001f) continue;

                    Vector3 push = apart / distance * (0.8f - distance) * 4f * dt;
                    a.GetComponent<CharacterController>().Move(push);
                    b.GetComponent<CharacterController>().Move(-push);
                }
            }
        }

        private void Kick(float dt)
        {
            Vector3 feet = _player.transform.position;
            Vector3 velocity = _player.Velocity;
            if (velocity.sqrMagnitude < 1f) return;

            foreach (Corpse corpse in _corpses)
                foreach (Rigidbody part in corpse.Parts)
                {
                    if (part == null) continue;
                    Vector3 offset = part.position - feet;
                    offset.y = 0f;
                    if (offset.sqrMagnitude > 0.2f) continue;

                    part.AddForce(velocity * (6f * dt), ForceMode.VelocityChange);
                }
        }

        private static void Ignore(Corpse corpse, ArenaFighter fighter)
        {
            if (corpse.Body == null || fighter == null || !fighter.TryGetComponent(out CharacterController controller)) return;

            foreach (Collider part in corpse.Body.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(part, controller);
        }

        private void Trim()
        {
            while (_corpses.Count > CorpseLimit)
            {
                Corpse oldest = _corpses[0];
                _corpses.RemoveAt(0);
                if (oldest.Body == null) continue;
                if (Application.isPlaying) Destroy(oldest.Body);
                else DestroyImmediate(oldest.Body);
            }
        }

        private Vector3 Edge()
        {
            Vector3 best = Vector3.zero;
            float bestDistance = -1f;
            for (int i = 0; i < 6; i++)
            {
                float side = Random.value * 4f;
                float along = Random.Range(-0.85f, 0.85f);
                Vector3 local = side < 1f ? new Vector3(along * _room.x, 0f, _room.y - 0.6f)
                    : side < 2f ? new Vector3(along * _room.x, 0f, -_room.y + 0.6f)
                    : side < 3f ? new Vector3(_room.x - 0.6f, 0f, along * _room.y)
                    : new Vector3(-_room.x + 0.6f, 0f, along * _room.y);
                Vector3 point = transform.position + local;
                float distance = Vector3.Distance(point, _player.transform.position);
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    best = point;
                }
            }

            return best;
        }

        private ArenaFighter Spawn(Vector3 position, ArenaFighter.Style style, Color tint, float yaw)
        {
            var holder = new GameObject(style == ArenaFighter.Style.Bat ? "Player" : "Enemy");
            holder.transform.SetPositionAndRotation(position + Vector3.up * 0.02f, Quaternion.Euler(0f, yaw, 0f));
            var fighter = holder.AddComponent<ArenaFighter>();
            fighter.Setup(_dummy, style, tint);
            foreach (Corpse corpse in _corpses) Ignore(corpse, fighter);
            return fighter;
        }

        public void Follow(float deltaTime)
        {
            if (_camera == null || _player == null) return;

            Vector3 focus = _player.transform.position;
            Vector3 lead = _aim - focus;
            lead.y = 0f;
            focus += Vector3.ClampMagnitude(lead * 0.18f, 2f);
            Vector3 centre = transform.position;
            // On wide screens the view is as wide as the room: keep it centred so no void shows beside the walls.
            float halfWidth = _height * 1.3f * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * _camera.aspect;
            float margin = Mathf.Min(_room.x, Mathf.Max(2.6f, halfWidth - 0.5f));
            focus.x = Mathf.Clamp(focus.x, centre.x - _room.x + margin, centre.x + _room.x - margin);
            focus.z = Mathf.Clamp(focus.z, centre.z - _room.y + 1.6f, centre.z + _room.y - 2.2f);
            Vector3 eye = focus + new Vector3(0f, _height, -_height * 0.62f);
            float blend = deltaTime < 0f ? 1f : 1f - Mathf.Exp(-deltaTime * 6f);
            Vector3 position = Vector3.Lerp(_camera.transform.position - Shake(), eye, blend);
            _shake = Mathf.Max(0f, _shake - Mathf.Max(0f, deltaTime) * 2.2f);
            _camera.transform.SetPositionAndRotation(position + Shake(), Quaternion.LookRotation(focus + Vector3.up * 0.6f - eye));
        }

        private Vector3 Shake()
        {
            float power = _shake * _shake * 0.35f;
            float t = _clock * 47f;
            return new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(1.7f, t) - 0.5f, Mathf.PerlinNoise(t, 5.1f) - 0.5f) * power;
        }

        private void OnGUI()
        {
            if (_tuner != null && _tuner.Open) return;

            Styles();
            float scale = Mathf.Max(0.6f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            GUI.Label(new Rect(40f, 28f, 900f, 80f), "RNG BLOOD", _title);
            GUI.Label(new Rect(44f, height - 70f, 1600f, 40f), "WASD move     Mouse aim     Left mouse attack     Right mouse drag a body     1-5 weapons     Tab blood settings", _small);
            GUI.Label(new Rect(44f, height - 110f, 900f, 40f), $"WEAPON   {_player.Weapon.ToString().ToUpperInvariant()}", _small);

            GUI.DrawTexture(new Rect(width - 330f, 30f, 300f, 150f), _panel);
            int stains = Blood.Field != null ? Blood.Field.StainsPlaced : 0;
            GUI.Label(new Rect(width - 310f, 45f, 280f, 30f), $"KILLS   {_kills}", _small);
            GUI.Label(new Rect(width - 310f, 75f, 280f, 30f), $"{stains} stains   {1f / Mathf.Max(0.0001f, _frameTime):0} fps", _small);
            if (GUI.Button(new Rect(width - 310f, 115f, 260f, 44f), "Clear blood", _button)) Blood.Clear();
        }

        private void Styles()
        {
            if (_button != null) return;

            _panel = Fill(new Color(0.04f, 0.04f, 0.045f, 0.72f));
            _title = new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.93f, 0.91f, 0.88f) } };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(0.72f, 0.7f, 0.68f) } };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 21, alignment = TextAnchor.MiddleCenter, border = new RectOffset(0, 0, 0, 0) };
            _button.normal.background = Fill(new Color(0.12f, 0.12f, 0.13f, 0.95f));
            _button.hover.background = Fill(new Color(0.2f, 0.2f, 0.22f, 0.95f));
            _button.active.background = Fill(new Color(0.55f, 0.06f, 0.05f, 0.97f));
            _button.normal.textColor = _button.hover.textColor = _button.active.textColor = new Color(0.85f, 0.83f, 0.8f);
        }

        private static Texture2D Fill(Color colour)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, colour);
            texture.Apply();
            return texture;
        }
    }
}
