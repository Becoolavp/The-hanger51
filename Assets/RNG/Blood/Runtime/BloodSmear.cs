using UnityEngine;

namespace Rng.Blood
{
    [AddComponentMenu("RNG/Blood Smear")]
    public class BloodSmear : MonoBehaviour
    {
        [SerializeField, Min(0.02f), Tooltip("Width of the smear in metres.")] private float _width = 0.35f;
        [SerializeField, Min(0.1f), Tooltip("Metres this object keeps smearing after it soaks in a wet pool or after Soak is called. The smear gets fainter over this distance.")] private float _reach = 4f;
        [SerializeField, Min(0.05f), Tooltip("Distance moved between two pieces of the smear.")] private float _step = 0.25f;
        [SerializeField, Min(0.05f), Tooltip("Height above the floor within which this object counts as touching it.")] private float _contact = 0.6f;
        [SerializeField, Tooltip("Smear at full strength whenever it moves, for example a bleeding body that is dragged.")] private bool _bleeding;
        [SerializeField, Tooltip("Smear the surface in front of this object (its blue axis) instead of the floor below, for example a palm moving over a wall.")] private bool _forward;
        [SerializeField, Tooltip("Smear whatever surface this object touches: the floor below it or a wall it slides along, for example a body knocked against a wall.")] private bool _anySurface;

        private Vector3 _last;
        private float _left;
        private bool _tracking;
        private bool _smearing;
        private Vector3 _surface = Vector3.up;
        private Vector3 _motion;
        private BloodSupply _supply;
        private bool _supplyFound;
        private bool _inLiquid;
        private float _wetFor = -1f;
        private System.Action<float> _readBack;
        private float _seed;

        public bool Bleeding
        {
            get => _bleeding;
            set => _bleeding = value;
        }

        public bool Forward
        {
            get => _forward;
            set => _forward = value;
        }

        public bool AnySurface
        {
            get => _anySurface;
            set => _anySurface = value;
        }

        public float Width
        {
            get => _width;
            set => _width = Mathf.Max(0.02f, value);
        }

        private void OnEnable()
        {
            _tracking = false;
            _supplyFound = false;
            _seed = Random.value * 100f;
        }

        /// <summary>The Blood Supply this smear spends, found on this object or a parent. Null when there is none: the smear is then as before.</summary>
        public BloodSupply Supply
        {
            get
            {
                if (!_supplyFound)
                {
                    _supply = GetComponentInParent<BloodSupply>();
                    _supplyFound = true;
                }

                return _supply;
            }
            set
            {
                _supply = value;
                _supplyFound = true;
            }
        }

        private void Update()
        {
            Track();
        }

        public void Soak(float metres = -1f)
        {
            _left = metres < 0f ? _reach : metres;
        }

        public void Track()
        {
            Vector3 position = transform.position;
            if (!_tracking)
            {
                _last = position;
                _tracking = true;
                return;
            }

            float moved = _forward || _anySurface ? Vector3.Distance(position, _last) : Mathf.Sqrt((position.x - _last.x) * (position.x - _last.x) + (position.z - _last.z) * (position.z - _last.z));
            if (moved < _step) return;

            bool wet = Blood.Wet(position, _width * 0.5f);
            if (wet) _left = _reach;
            if (wet && Blood.Field != null && Blood.Field.Liquid && Blood.Settings != null && Blood.Settings.LiquidDrag) Blood.Field.DragLiquid(_last, position, _width, _seed);
            float strength = _bleeding ? 1f : Mathf.Clamp01(_left / _reach);
            bool drawn = false;
            _motion = (position - _last) / Mathf.Max(0.0001f, moved);
            BloodSupply supply = Supply;
            float width = _width;
            float dryness = 0f;
            float wetness = 0f;
            BloodSettings settings = Blood.Settings;
            // fresh out of a pool the smear looks like the pool itself, then thins into a normal smear
            if (settings != null && settings.WetSmears) wetness = Wetness(position, wet, moved, settings.WetSmearLength);
            if (supply != null)
            {
                // a body with less blood leaves a narrower, streakier, broken and drier trail, and spends what it lays down; blood it
                // picked up from a pool is not its own, so a wet smear stays full and costs nothing
                float own = 1f - wetness;
                width *= Mathf.Lerp(1f, supply.WidthScale, own);
                strength *= Mathf.Lerp(1f, supply.StrengthScale, own);
                dryness = supply.Dryness;
                if (Random.value < supply.GapChance * own * own) strength = 0f;
            }

            if (strength > 0.02f && Ground(_last, out Vector3 from, out Vector3 normal) && Ground(position, out Vector3 to, out Vector3 normalTo) && Vector3.Dot(normal, normalTo) > 0.7f)
            {
                if (supply != null || wetness > 0f)
                {
                    Blood.Smear(from, to, normal, width, strength, !_smearing, false, dryness, wetness);
                    if (supply != null) supply.Drain(supply.SmearCost * width * moved * strength * (1f - wetness));
                }
                else
                {
                    Blood.Smear(from, to, normal, width, strength, !_smearing, false);
                }

                drawn = true;
            }

            _smearing = drawn;

            _left = Mathf.Max(0f, _left - moved);
            _last = position;
        }

        // 1 while this object touches a pool and just after, falling to 0 over the wet length once it is out
        private float Wetness(Vector3 position, bool wet, float moved, float length)
        {
            bool touching = false;
            if (wet)
            {
                // the quick wet test reaches past the visible edge of liquid on the floor, so ask the liquid itself where the GPU can
                BloodField field = Blood.Field;
                _readBack ??= depth => _inLiquid = depth > 0.4f;
                touching = field == null || !field.ReadLiquid(position, _width * 0.5f, _readBack) || _inLiquid || field.WetPool(position);
            }
            else
            {
                _inLiquid = false;
            }

            if (touching) _wetFor = 0f;
            else if (_wetFor >= 0f) _wetFor += moved;
            if (_wetFor < 0f || _wetFor >= length) return 0f;

            return 1f - Mathf.SmoothStep(0f, 1f, _wetFor / length);
        }

        private bool Ground(Vector3 point, out Vector3 ground, out Vector3 normal)
        {
            ground = point;
            normal = Vector3.up;
            BloodSettings settings = Blood.Settings;
            if (settings == null) return false;

            if (_anySurface)
            {
                Vector3 sideways = Vector3.ProjectOnPlane(_motion, Vector3.up);
                if (!Touch(point, -_surface, settings.Surfaces, out RaycastHit touched) && !Touch(point, Vector3.down, settings.Surfaces, out touched)
                    && (sideways.sqrMagnitude < 0.01f || !Touch(point, sideways.normalized, settings.Surfaces, out touched))) return false;

                _surface = touched.normal;
                ground = touched.point;
                normal = touched.normal;
                return true;
            }

            Vector3 reach = _forward ? transform.forward : Vector3.down;
            if (!Touch(point, reach, settings.Surfaces, out RaycastHit hit)) return false;

            ground = hit.point;
            normal = hit.normal;
            return true;
        }

        private bool Touch(Vector3 point, Vector3 direction, LayerMask surfaces, out RaycastHit hit)
        {
            return Physics.Raycast(point - direction * 0.1f, direction, out hit, _contact + 0.1f, surfaces, QueryTriggerInteraction.Ignore);
        }
    }
}
