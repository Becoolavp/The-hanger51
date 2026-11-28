using System.Collections.Generic;
using UnityEngine;

namespace Rng.Blood.Demo
{
    /// <summary>
    /// The logo cut into a concrete slab, seen from above. A grenade blows a group of crash dummies apart, or a dummy drops into a
    /// ceiling fan: the blood lands as real RNG Blood hits, pools grow, and the liquid floor runs into the grooves until RNG BLOOD
    /// stands in blood. 1 and 2 pick the scene, Space replays.
    /// </summary>
    [AddComponentMenu("RNG/Blood Demo/Showcase Demo")]
    public class ShowcaseDemo : MonoBehaviour
    {
        public enum Scene
        {
            Grenade,
            Fan,
        }

        private struct Glow
        {
            public Transform Thing;
            public Light Light;
            public float Birth;
            public float Seconds;
            public float Size;
            public float Intensity;
        }

        [SerializeField, Tooltip("Blood settings of the showcase. Liquid must be on for the grooves to fill.")] private BloodSettings _settings;
        [SerializeField, Tooltip("Grooves of the slab for the liquid floor: white where the letters are cut.")] private Texture2D _channels;
        [SerializeField, Tooltip("Size of the groove texture on the floor in metres, centred on this object.")] private Vector2 _channelSize = new(6f, 0.985f);
        [SerializeField, Tooltip("How deep the grooves are, in films of liquid.")] private float _channelDepth = 6f;
        [SerializeField, Tooltip("The crash dummy.")] private GameObject _dummy;
        [SerializeField, Tooltip("Unlit material for the blast flash.")] private Material _glowMaterial;
        [SerializeField, Tooltip("Camera that looks down on the floor.")] private Camera _camera;
        [SerializeField, Tooltip("The ceiling fan, turned by this component in the fan scene.")] private Transform _fan;
        [SerializeField, Tooltip("Radius of the fan blades in metres.")] private float _fanRadius = 0.95f;
        [SerializeField, Tooltip("Scene played on start.")] private Scene _scene;
        [SerializeField, Tooltip("Seed of the spins the bodies take, so every replay falls the same way.")] private int _seed = 7;

        private readonly List<GameObject> _spawned = new();
        private readonly List<Dictionary<string, Rigidbody>> _bodies = new();
        private readonly List<Glow> _glows = new();
        private readonly System.Random _random = new(5);
        private float _clock;
        private float _start;
        private bool _blasted;
        private bool _pooled;
        private GameObject _grenade;
        private float _fanAngle;
        private float _fanSpeed;
        private float _hitAt;

        public float Clock => _clock;
        public Camera View => _camera;
        public Scene Current => _scene;
        /// <summary>Seconds into the scene when the blast went off or the body hit the fan, negative before.</summary>
        public float HitTime => _blasted ? (_scene == Scene.Fan ? _hitAt : 1.15f) : -1f;
        /// <summary>True once the pool that fills the letters has started to spread.</summary>
        public bool Pooled => _pooled;

        private void Start()
        {
            Play(_scene);
        }

        private void Update()
        {
            if (DemoInput.Digit(1)) Play(Scene.Grenade);
            else if (DemoInput.Digit(2)) Play(Scene.Fan);
            else if (DemoInput.Space()) Play(_scene);
            Step(Time.deltaTime);
        }

        /// <summary>Starts a scene from the beginning: clears the blood and sets the dummies up.</summary>
        public void Play(Scene scene)
        {
            _scene = scene;
            // the same fall every time: the spins the bodies take come from a fixed seed
            Random.InitState(_seed);
            if (_settings == null || !_settings.Liquid) Debug.LogWarning("[ShowcaseDemo] the blood settings need Liquid on, or the grooves never fill", this);
            if (Blood.Field == null || Blood.Settings != _settings) Blood.Initialize(_settings);
            Blood.Clear();
            Blood.Channels(_channels, transform.position, _channelSize, _channelDepth);
            foreach (GameObject thing in _spawned)
                if (thing != null) Remove(thing);
            _spawned.Clear();
            _bodies.Clear();
            foreach (Glow glow in _glows)
                if (glow.Thing != null) Remove(glow.Thing.gameObject);
            _glows.Clear();
            _start = _clock;
            _blasted = false;
            _pooled = false;
            _fanSpeed = 0f;
            if (_fan != null) _fan.gameObject.SetActive(scene == Scene.Fan);
            if (scene == Scene.Fan)
            {
                _fanSpeed = 820f;
                Vector3 hub = _fan != null ? _fan.position : transform.position + Vector3.up * 2.7f;
                Spawn(new Vector3(hub.x + 0.3f, hub.y + 2.5f, hub.z + 0.1f), Quaternion.Euler(80f, 30f, 0f), false);
                return;
            }

            // a ragged line behind the slab, so the blast throws the bodies away from it and the letters stay clear
            for (int i = 0; i < 6; i++)
            {
                float x = -2.6f + i * 1.04f + 0.12f * Mathf.Sin(i * 2.7f);
                var at = new Vector3(x, 0f, 1.15f + 0.1f * Mathf.Cos(i * 1.9f));
                Spawn(transform.position + at, Quaternion.Euler(0f, 160f + 25f * Mathf.Sin(i * 1.3f), 0f), true);
            }

            _grenade = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _grenade.name = "Grenade";
            Remove(_grenade.GetComponent<Collider>());
            _grenade.transform.localScale = Vector3.one * 0.11f;
            var shell = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Grenade" };
            shell.SetColor("_BaseColor", new Color(0.16f, 0.2f, 0.12f));
            shell.SetFloat("_Smoothness", 0.4f);
            _grenade.GetComponent<Renderer>().sharedMaterial = shell;
            _spawned.Add(_grenade);
        }

        /// <summary>Advances the scene. Update calls it; call it yourself when you drive the demo from edit mode.</summary>
        public void Step(float deltaTime)
        {
            _clock += deltaTime;
            Blood.Tick(_clock);
            if (_scene == Scene.Grenade) Grenade(_clock - _start);
            else Fan(_clock - _start, deltaTime);
            for (int i = _glows.Count - 1; i >= 0; i--)
            {
                Glow glow = _glows[i];
                float k = (_clock - glow.Birth) / glow.Seconds;
                if (k >= 1f || glow.Thing == null)
                {
                    if (glow.Thing != null) Remove(glow.Thing.gameObject);
                    _glows.RemoveAt(i);
                    continue;
                }

                glow.Thing.localScale = Vector3.one * Mathf.Max(0.01f, glow.Size) * Mathf.Lerp(0.4f, 1.6f, Mathf.Sqrt(k));
                if (glow.Light != null) glow.Light.intensity = glow.Intensity * (1f - k) * (1f - k);
            }
        }

        private void Grenade(float t)
        {
            const float rollStart = 0.2f;
            const float boom = 1.15f;
            if (_grenade != null)
            {
                float k = Mathf.Clamp01((t - rollStart) / (boom - rollStart));
                float ease = 1f - (1f - k) * (1f - k);
                _grenade.transform.position = transform.position + Vector3.Lerp(new Vector3(-0.6f, 0.055f, -3.9f), new Vector3(0.1f, 0.055f, -1.35f), ease);
                _grenade.transform.rotation = Quaternion.Euler(ease * 900f, 0f, 0f);
            }

            // once the bodies lie still, each bleeds a pool onto the slab and the liquid floor carries it into the grooves
            if (_blasted && !_pooled && t >= boom + 1.1f)
            {
                _pooled = true;
                // the blast leaves the biggest pool where the dummies stood nearest it, over the gutter under the letters
                Blood.Pool(transform.position + new Vector3(0.1f, 0f, -1.42f), Vector3.up, 3.9f, 3f, 600f);
                foreach (Dictionary<string, Rigidbody> parts in _bodies)
                {
                    if (!parts.TryGetValue("Chest", out Rigidbody chest)) continue;

                    var under = new Vector3(chest.position.x, transform.position.y, chest.position.z);
                    Blood.Pool(under, Vector3.up, 0.28f, 2.5f, 600f);
                }
            }

            if (_blasted || t < boom) return;

            _blasted = true;
            Vector3 centre = _grenade != null ? _grenade.transform.position : transform.position;
            if (_grenade != null) Remove(_grenade);
            // the blast is light: a hot core for two frames and a light that lights the floor and the bodies around it
            Flash(centre + Vector3.up * 0.4f, 0f, 0.16f, new Color(1f, 0.55f, 0.22f), 140f);
            Flash(centre + Vector3.up * 0.15f, 0.22f, 0.07f, new Color(1f, 0.85f, 0.6f), 0f);
            Blood.Spawn(centre + Vector3.up * 0.3f, Vector3.up, Vector3.up, 2f, BloodHitKind.Explosion);

            foreach (Dictionary<string, Rigidbody> parts in _bodies)
            {
                Vector3 away = Vector3.forward;
                if (parts.TryGetValue("Chest", out Rigidbody chest))
                {
                    away = chest.position - centre;
                    away.y = 0f;
                    away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward;
                    // each body takes the blast as an ordinary explosion hit and leaves its pool where it comes to rest
                    Blood.Spawn(chest.position, -away, away + Vector3.up * 0.4f, 1.6f, BloodHitKind.Explosion, chest.transform);
                    Blood.Death(new Vector3(chest.position.x, transform.position.y, chest.position.z), away, chest.transform, BloodHitKind.Explosion, 0f);
                }

                foreach (Rigidbody part in parts.Values)
                {
                    part.isKinematic = false;
                    Vector3 from = part.position - centre;
                    from.y = 0f;
                    float distance = from.magnitude;
                    // a small charge: the bodies are knocked down around the slab, not thrown across the room
                    Vector3 push = away * Mathf.Lerp(1.4f, 0.8f, distance / 3f) + Vector3.up * Mathf.Lerp(2.8f, 2f, distance / 3f);
                    part.AddForce(push * (0.9f + 0.2f * (float)_random.NextDouble()), ForceMode.VelocityChange);
                    part.AddTorque(Random.onUnitSphere * 10f, ForceMode.VelocityChange);
                }
            }
        }

        private void Fan(float t, float deltaTime)
        {
            if (_fan == null) return;

            _fanAngle += _fanSpeed * deltaTime;
            _fan.localRotation = Quaternion.Euler(0f, _fanAngle, 0f);
            Vector3 hub = _fan.position;
            if (_bodies.Count == 0 || !_bodies[0].TryGetValue("Chest", out Rigidbody chest)) return;

            // the body drops into the blades and is knocked off them towards the foot of the slab
            if (!_blasted && chest.position.y < hub.y + 0.35f)
            {
                _blasted = true;
                _hitAt = t;
                _fanSpeed = 560f;
                Vector3 landing = transform.position + new Vector3(0.85f, 0f, -1.7f);
                Vector3 across = landing - chest.position;
                across.y = 0f;
                const float fall = 0.75f;
                foreach (Rigidbody part in _bodies[0].Values)
                {
                    part.AddForce(across / fall - new Vector3(Velocity(part).x, 0f, Velocity(part).z), ForceMode.VelocityChange);
                    part.AddTorque(Vector3.up * 20f + Random.onUnitSphere * 6f, ForceMode.VelocityChange);
                }

                Blood.Death(new Vector3(chest.position.x, transform.position.y, chest.position.z), across.normalized, chest.transform, BloodHitKind.Sharp, 0f);
            }

            // for a moment the blades throw blood off their tips, each tip an ordinary cut hit flung along its turn
            if (_blasted && t - _hitAt < 0.7f)
            {
                for (int blade = 0; blade < 4; blade++)
                {
                    if (_random.NextDouble() > 0.85) continue;

                    float angle = (_fanAngle + blade * 90f) * Mathf.Deg2Rad;
                    var spoke = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                    Vector3 tip = hub + spoke * _fanRadius * 0.9f + Vector3.down * 0.05f;
                    Vector3 turn = Vector3.Cross(Vector3.up, spoke);
                    // a blade only throws off what flies away from the slab, so the spiral lands around the fan and the logo stays clean
                    Vector3 toSlab = transform.position - tip;
                    toSlab.y = 0f;
                    if (Vector3.Dot(turn, toSlab.normalized) > -0.1f) continue;

                    Blood.Spawn(tip, -turn, turn + spoke * 0.4f + Vector3.down * 0.25f, 1.1f, BloodHitKind.Sharp);
                }
            }

            // once the body lies still its pool spreads, and what reaches the gutter runs into the letters
            if (_blasted && !_pooled && t - _hitAt > 1.4f)
            {
                _pooled = true;
                // the pool spreads from the end of the body nearest the slab, so it reaches the gutter without running over the letters
                Vector3 nearest = chest.position;
                foreach (Rigidbody part in _bodies[0].Values)
                    if (part.position.z > nearest.z) nearest = part.position;
                Blood.Pool(new Vector3(nearest.x, transform.position.y, Mathf.Min(nearest.z, transform.position.z - 1.42f)), Vector3.up, 3.9f, 3f, 600f);
            }
        }

        private static Vector3 Velocity(Rigidbody body)
        {
#if UNITY_6000_0_OR_NEWER
            return body.linearVelocity;
#else
            return body.velocity;
#endif
        }

        private void Spawn(Vector3 at, Quaternion rotation, bool standing)
        {
            if (_dummy == null) return;

            GameObject body = Instantiate(_dummy, at, rotation);
            body.name = "Dummy";
            foreach (MonoBehaviour behaviour in body.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
            Dictionary<string, Rigidbody> parts = DummyRagdoll.Build(body);
            foreach (Rigidbody part in parts.Values) part.isKinematic = standing;
            _spawned.Add(body);
            _bodies.Add(parts);
        }

        private void Flash(Vector3 at, float size, float seconds, Color colour, float intensity)
        {
            GameObject ball = size > 0f ? GameObject.CreatePrimitive(PrimitiveType.Sphere) : new GameObject();
            ball.name = "Flash";
            if (ball.TryGetComponent(out Collider collider)) Remove(collider);
            ball.transform.position = at;
            ball.transform.localScale = Vector3.one * Mathf.Max(0.01f, size) * 0.4f;
            ball.TryGetComponent(out Renderer renderer);
            if (renderer != null) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (renderer != null && _glowMaterial != null)
            {
                var material = new Material(_glowMaterial);
                material.SetColor("_BaseColor", colour * 6f);
                renderer.sharedMaterial = material;
            }

            Light light = null;
            if (intensity > 0f)
            {
                light = new GameObject("Light").AddComponent<Light>();
                light.transform.SetParent(ball.transform, false);
                light.type = LightType.Point;
                light.color = colour;
                light.intensity = intensity;
                light.range = 6f;
            }

            _glows.Add(new Glow { Thing = ball.transform, Light = light, Birth = _clock, Seconds = seconds, Size = size, Intensity = intensity });
        }

        private static void Remove(Object thing)
        {
            if (Application.isPlaying) Destroy(thing);
            else DestroyImmediate(thing);
        }
    }
}
