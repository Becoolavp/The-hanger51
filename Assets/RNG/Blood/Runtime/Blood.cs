using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Rng.Blood
{
    public static class Blood
    {
        public const string DefaultSettingsResource = "BloodSettings";

        private const float HalfStride = 0.55f;
        private const float SmearProbeHeight = 0.6f;
        private const float SmearProbeReach = 3f;

        private struct Soles
        {
            public int Prints;
            public bool Left;
        }

        private static readonly Dictionary<Object, Soles> _soles = new();
        private static BloodField _field;
        private static bool _missing;

        public static event Action<BloodHit> Hit;
        public static event Action Cleared;

        public static BloodField Field => _field != null ? _field : _missing ? null : Initialize();
        public static BloodSettings Settings => Field != null ? _field.Settings : null;
        public static bool Shown => _field != null && _field.Shown;

        public static BloodField Initialize(BloodSettings settings = null, Transform parent = null)
        {
            if (_field != null) Shutdown();

            if (settings == null) settings = Resources.Load<BloodSettings>(DefaultSettingsResource);
            if (settings != null && settings.Quality != BloodQuality.Low) WarnWithoutDepth();
            var holder = new GameObject("Blood");
            if (!Application.isPlaying) holder.hideFlags = HideFlags.DontSave;
            if (parent != null) holder.transform.SetParent(parent, false);
            else if (Application.isPlaying) Object.DontDestroyOnLoad(holder);

            var field = holder.AddComponent<BloodField>();
            if (!field.Initialize(settings))
            {
                Destroy(holder);
                _missing = true;
                return null;
            }

            _missing = false;
            _field = field;
            return _field;
        }

        public static void Shutdown()
        {
            Cleared?.Invoke();
            _soles.Clear();
            if (_field != null) Destroy(_field.gameObject);
            _field = null;
        }

        public static void Spawn(Vector3 position, Vector3 normal, Vector3 direction, float intensity = 1f, BloodHitKind kind = BloodHitKind.Bullet, Transform bone = null)
        {
            Vector3 travel = Travel(direction, normal);
            Hit?.Invoke(new BloodHit(position, normal, travel, intensity, kind, bone));

            BloodField field = Field;
            if (field == null || !field.Shown || intensity <= 0f) return;

            BloodSettings settings = field.Settings;
            Vector3 chest = position + Vector3.up * settings.ChestHeight;
            field.Emit(settings.Gout, kind == BloodHitKind.Explosion ? chest : position, travel, intensity);
            switch (kind)
            {
                case BloodHitKind.Explosion:
                    field.Emit(settings.Explosion, chest, travel, intensity);
                    break;
                case BloodHitKind.Headshot:
                    field.Emit(settings.Headshot, position, travel, intensity);
                    field.Emit(settings.Mist, position, travel, intensity);
                    break;
                case BloodHitKind.Blunt:
                    field.Emit(settings.Blunt, position, travel, intensity);
                    break;
                case BloodHitKind.Sharp:
                    field.Emit(settings.Sharp, position, travel, intensity);
                    break;
                case BloodHitKind.Shotgun:
                    field.Emit(settings.Shotgun, position, travel, intensity);
                    field.Emit(settings.Mist, position, travel, intensity);
                    break;
                case BloodHitKind.BulletClose:
                    field.Emit(settings.BulletClose, position, travel, intensity);
                    field.Emit(settings.BackSpatter, position, -travel, intensity);
                    field.Emit(settings.Mist, position, travel, intensity);
                    break;
                default:
                    field.Emit(settings.BulletFar, position, travel, intensity);
                    break;
            }
        }

        public static void Spawn(RaycastHit hit, Vector3 direction, float intensity = 1f, BloodHitKind kind = BloodHitKind.Bullet)
        {
            Spawn(hit.point, hit.normal, direction, intensity, kind, hit.transform);
        }

        public static void Death(Vector3 position, Vector3 direction, Transform body = null, BloodHitKind kind = BloodHitKind.Bullet, float poolScale = 1f)
        {
            BloodField field = Field;
            if (field == null || !field.Shown) return;

            BloodSettings settings = field.Settings;
            Vector3 travel = Travel(direction, Vector3.zero);
            Vector3 chest = position + Vector3.up * settings.ChestHeight;
            field.Emit(settings.Death, chest, travel);
            field.Emit(settings.Gout, chest, travel);

            // a body that has bled out leaves a smaller pool
            BloodSupply supply = body != null ? body.GetComponentInParent<BloodSupply>() : null;
            float radius = settings.PoolRadius(kind) * poolScale * (supply != null ? supply.PoolScale : 1f);
            if (radius <= 0f) return;

            field.PoolUnder(body, position, settings.PoolDelay, radius * Random.Range(0.85f, 1.15f), settings.PoolGrowSeconds, settings.PoolSeconds);
        }

        public static void Drip(Vector3 origin)
        {
            BloodField field = Field;
            if (field == null || !field.Shown) return;

            field.Emit(field.Settings.WoundDrip, origin, Vector3.down);
        }

        public static void Emit(BloodBurst burst, Vector3 origin, Vector3 direction, float strength = 1f)
        {
            BloodField field = Field;
            if (field == null) return;

            field.Emit(burst, origin, direction, strength);
        }

        public static void Fountain(Transform source, Vector3 axis, bool local, float seconds, float interval, BloodBurst burst)
        {
            BloodField field = Field;
            if (field == null) return;

            field.Fountain(source, axis, local, seconds, interval, burst);
        }

        public static void Pool(Vector3 point, Vector3 normal, float radius, float growSeconds, float lifetime)
        {
            BloodField field = Field;
            if (field == null) return;

            field.Pool(point, normal, radius, growSeconds, lifetime);
        }

        public static void PoolUnder(Transform body, Vector3 fallback, float delay, float radius, float growSeconds, float lifetime)
        {
            BloodField field = Field;
            if (field == null) return;

            field.PoolUnder(body, fallback, delay, radius, growSeconds, lifetime);
        }

        public static void Mark(Vector3 origin, Vector3 direction, BloodStainStyle style, float halfWidth, float stretch = 1f)
        {
            BloodField field = Field;
            if (field == null) return;

            field.Mark(origin, direction, style, halfWidth, stretch);
        }

        /// <summary>
        /// Cuts grooves into the liquid floor (Liquid in the settings): white texels of <paramref name="mask"/> are channels that pools run
        /// into and flow along, like drains, gutters or letters cut into concrete. The mask lies flat, centred on
        /// <paramref name="centre"/>, <paramref name="size"/> metres across, top of the texture towards +z. Pass null to remove them.
        /// </summary>
        public static void Channels(Texture mask, Vector3 centre, Vector2 size, float depth = 1f)
        {
            BloodField field = Field;
            if (field == null) return;

            field.Channels(mask, centre, size, depth);
        }

        public static void Smear(Vector3 from, Vector3 to, Vector3 normal, float width = 0.3f, float strength = 1f, bool start = true, bool end = true)
        {
            BloodField field = Field;
            if (field == null) return;

            field.Smear(from, to, normal, width, strength, start, end);
        }

        /// <summary>
        /// A smear laid down already partly dry or extra wet: <paramref name="dryness"/> 1 looks like blood that dried on the floor,
        /// <paramref name="wetness"/> 1 like the blood of a pool. Wetness wins over dryness.
        /// </summary>
        public static void Smear(Vector3 from, Vector3 to, Vector3 normal, float width, float strength, bool start, bool end, float dryness, float wetness = 0f)
        {
            BloodField field = Field;
            if (field == null) return;

            field.Smear(from, to, normal, width, strength, start, end, dryness, wetness);
        }

        public static void Smear(Vector3 from, Vector3 to, float width = 0.3f, float strength = 1f)
        {
            BloodField field = Field;
            if (field == null) return;

            Vector3 middle = (from + to) * 0.5f + Vector3.up * SmearProbeHeight;
            if (!Physics.Raycast(middle, Vector3.down, out RaycastHit ground, SmearProbeReach, field.Settings.Surfaces, QueryTriggerInteraction.Ignore)) return;

            float lift = ground.point.y - middle.y + SmearProbeHeight;
            field.Smear(from + Vector3.up * lift, to + Vector3.up * lift, ground.normal, width, strength);
        }

        /// <summary>A print of any shape: a hand, a knee, a hoof or a piece of tyre tread. See <see cref="BloodField.Print(Vector3, Vector3, Vector3, BloodPrintShape, float, float, bool, float, float, float)"/>.</summary>
        public static void Print(Vector3 point, Vector3 normal, Vector3 heading, BloodPrintShape shape, float length, float width, bool left = false, float strength = 1f, float lifetime = 0f, float seed = 0f)
        {
            BloodField field = Field;
            if (field == null) return;

            field.Print(point, normal, heading, shape, length, width, left, strength, lifetime > 0f ? lifetime : field.Settings.StainLifetime, seed);
        }

        /// <summary>Washes blood off in a capsule: rain, a hose or a mop. Call it every frame the water runs. See <see cref="BloodField.Wash(Vector3, Vector3, float, float)"/>.</summary>
        public static void Wash(Vector3 from, Vector3 to, float radius, float amount)
        {
            BloodField field = Field;
            if (field == null) return;

            field.Wash(from, to, radius, amount);
        }

        public static bool Wet(Vector3 point, float margin = 0f)
        {
            return _field != null && _field.Wet(point, margin);
        }

        public static void Walk(Object walker, Vector3 position, Vector3 heading, bool stride)
        {
            BloodField field = Field;
            if (walker == null || field == null || !field.Shown) return;

            BloodSettings settings = field.Settings;
            if (settings.PrintCount <= 0) return;

            if (field.Wet(position, settings.PrintReach))
            {
                _soles.TryGetValue(walker, out Soles soaked);
                soaked.Prints = settings.PrintCount;
                _soles[walker] = soaked;
                return;
            }

            if (!stride || !_soles.TryGetValue(walker, out Soles soles) || soles.Prints <= 0) return;

            Vector3 side = Vector3.Cross(Vector3.up, heading);
            Place(field, settings, ref soles, position - heading * HalfStride, heading, side);
            Place(field, settings, ref soles, position, heading, side);
            if (soles.Prints <= 0) _soles.Remove(walker);
            else _soles[walker] = soles;
        }

        public static void Forget(Object walker)
        {
            if (walker != null) _soles.Remove(walker);
        }

        public static void Show(bool shown, bool alternate = false)
        {
            BloodField field = Field;
            if (field == null) return;

            if (!shown)
            {
                _soles.Clear();
                Cleared?.Invoke();
            }

            field.Show(shown, alternate);
        }

        public static void Recolour(Color colour, Color alternateColour)
        {
            BloodField field = Field;
            if (field == null) return;

            field.Recolour(colour, alternateColour);
        }

        public static void Apply(BloodSettings settings = null)
        {
            BloodField field = Field;
            if (field == null) return;

            if (settings == null) settings = field.Settings;
            if (field.Apply(settings)) return;

            bool shown = field.Shown;
            bool alternate = field.Alternate;
            field = Initialize(settings, field.transform.parent);
            if (field != null) field.Show(shown, alternate);
        }

        public static void Clear()
        {
            Cleared?.Invoke();
            _soles.Clear();
            if (_field != null) _field.Clear();
        }

        public static void Tick(float time)
        {
            BloodField field = Field;
            if (field == null) return;

            field.Tick(time);
        }

        private static void Place(BloodField field, BloodSettings settings, ref Soles soles, Vector3 position, Vector3 heading, Vector3 side)
        {
            if (soles.Prints <= 0) return;

            float strength = (float)soles.Prints / settings.PrintCount;
            Vector3 foot = position + side * (soles.Left ? -settings.PrintSide : settings.PrintSide);
            field.Print(foot, Vector3.up, heading, settings.PrintLength, soles.Left, strength, settings.StainLifetime);
            soles.Left = !soles.Left;
            soles.Prints--;
        }

        private static Vector3 Travel(Vector3 direction, Vector3 normal)
        {
            if (direction.sqrMagnitude > 0.0001f) return direction.normalized;
            if (normal.sqrMagnitude > 0.0001f) return -normal.normalized;
            return Vector3.forward;
        }

        private static void WarnWithoutDepth()
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline && !pipeline.supportsCameraDepthTexture)
                Debug.LogWarning("[Blood] The URP asset has Depth Texture off; stains, pools and footprints need it on.");
        }

        private static void Destroy(Object target)
        {
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _soles.Clear();
            _field = null;
            _missing = false;
            Hit = null;
            Cleared = null;
        }
    }
}
