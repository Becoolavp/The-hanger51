using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rng.Blood
{
    [AddComponentMenu("")]
    public partial class BloodField : MonoBehaviour
    {
        private const int RecentCapacity = 48;
        private const float MergeStep = 0.4f;
        private const float PoolBody = 0.55f;
        private const float PoolProbeHeight = 0.6f;
        private const float PoolProbeReach = 3f;
        private const int BoxCorners = 8;
        private const int BoxIndices = 36;
        private const int TrajectorySteps = 10;
        private const float TrajectoryStepSeconds = 0.2f;
        private const float BoxDepth = 0.25f;
        private const float FlatBoxDepth = 0.03f;
        private const float MapHeight = 30f;
        private const float MapFollow = 0.25f;
        private const float MapFarShare = 0.4f;
        private const float MapBoxSlack = 0.5f;
        private const string StampResource = "BloodStamp";
        private const float WorldExtent = 4000f;
        private const float MinimumGrazing = 0.28f;
        private const float MaximumStretch = 3.5f;
        private const float Dormant = -1000f;
        private const float MistSecondsMin = 0.25f;
        private const float MistSecondsMax = 0.55f;
        private const float WallSlope = 0.5f;
        private const float RunLength = 2.4f;
        private const float RunWidth = 1.1f;
        private const float MarkReach = 4f;
        private const float MarkDelay = 0.3f;
        private const float GlassMargin = 0.2f;
        private const float HideMargin = 0.1f;
        private const float SmearPieceMin = 0.12f;
        private const float SmearPieceWidths = 3f;
        private const int SmearPiecesMax = 16;
        private const float SmearTailStrength = 0.45f;
        private const float SmearMiterMax = 0.7f;
        private const int TrailCapacity = 16;
        private const float SmearRunStrength = 0.55f;
        private const float SmearPad = 1.5f;
        private const float SmearRunChance = 0.3f;
        private const MeshUpdateFlags Quiet = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers;

        private static readonly int TimeId = Shader.PropertyToID("_BloodTime");
        private static readonly int GravityId = Shader.PropertyToID("_BloodGravity");
        private static readonly int ColourId = Shader.PropertyToID("_BloodColor");
        private static readonly int PoolFadeId = Shader.PropertyToID("_PoolFadeSeconds");
        private static readonly int DryStartId = Shader.PropertyToID("_DryStart");
        private static readonly int RiseId = Shader.PropertyToID("_Rise");
        private static readonly int DryEndId = Shader.PropertyToID("_DryEnd");
        private static readonly int FadeStartId = Shader.PropertyToID("_FadeStart");
        private static readonly int DryBrightnessId = Shader.PropertyToID("_DryBrightness");
        private static readonly int DryBrownId = Shader.PropertyToID("_DryBrown");
        private static readonly int DryGlossId = Shader.PropertyToID("_DryGloss");
        private static readonly int ZTestId = Shader.PropertyToID("_BloodZTest");
        private static readonly int MapId = Shader.PropertyToID("_BloodMap");
        private static readonly int MapLifeId = Shader.PropertyToID("_BloodMapLife");
        private static readonly int MapTexelId = Shader.PropertyToID("_BloodMapTexel");
        private static readonly int StampShapeId = Shader.PropertyToID("_StampShape");
        private static readonly int StampValueId = Shader.PropertyToID("_StampValue");
        private static readonly int ShiftOffsetId = Shader.PropertyToID("_ShiftOffset");
        private static readonly int MapSourceId = Shader.PropertyToID("_BloodMapSource");

        [StructLayout(LayoutKind.Sequential)]
        private struct DropVertex
        {
            public Vector3 Position;
            public Vector4 Origin;
            public Vector4 Velocity;
            public Vector4 Misc;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct StainVertex
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector4 Centre;
            public Vector4 AxisU;
            public Vector4 AxisV;
            public Vector4 Life;
        }

        private struct Landing
        {
            public float Time;
            public Vector3 Point;
            public Vector3 Normal;
            public Vector3 Travel;
            public float HalfWidth;
            public float Stretch;
            public float Style;
            public bool Gathers;
            public bool Glass;
        }

        private struct Stamp
        {
            public Vector3 Point;
            public Vector3 Travel;
            public float HalfU;
            public float HalfV;
            public float Style;
            public float Birth;
        }

        private struct Trail
        {
            public int Slot;
            public Vector3 End;
            public Vector3 Heading;
            public float Distance;
            public float Look;
        }

        private static Trail[] NewTrails()
        {
            var trails = new Trail[TrailCapacity];
            for (int i = 0; i < trails.Length; i++) trails[i].Slot = -1;
            return trails;
        }

        private struct Recent
        {
            public Vector3 Point;
            public float HalfWidth;
            public float Birth;
        }

        private struct PoolOrder
        {
            public float Time;
            public Transform Body;
            public Vector3 Fallback;
            public float Radius;
            public float Grow;
            public float Lifetime;
        }

        private struct Spurt
        {
            public Transform Source;
            public Vector3 Axis;
            public bool Local;
            public float Start;
            public float End;
            public float Next;
            public float Interval;
            public BloodBurst Burst;
        }

        private struct PoolRecord
        {
            public int Slot;
            public Transform Body;
            public bool Follows;
            public Vector3 Point;
            public float Radius;
            public float Birth;
            public float Grow;
            public float End;
            public bool Gathers;
            public float Area;
        }

        private readonly Recent[] _recent = new Recent[RecentCapacity];
        private readonly List<Landing> _landings = new();
        private readonly List<PoolOrder> _poolOrders = new();
        private readonly List<PoolRecord> _pools = new();
        private readonly List<Spurt> _spurts = new();
        private readonly Dictionary<Collider, Renderer[]> _panes = new();
        private readonly List<Stamp> _stamps = new();
        private RenderTexture _map;
        private RenderTexture _mapSpare;
        private Material _stamper;
        private Mesh _quad;
        private CommandBuffer _commands;
        private MaterialPropertyBlock _stampBlock;
        private Vector3 _mapCentre;
        private Vector3 _mapBox;
        private float _mapSize;
        private int _mapResolution;
        private int _mapSlot = -1;
        private bool _massive;
        private NativeArray<DropVertex> _dropVertices;
        private NativeArray<StainVertex> _stainVertices;
        private Vector3[] _blob;
        private Mesh _dropMesh;
        private Mesh _stainMesh;
        private Material _dropMaterial;
        private Material _stainMaterial;
        private MeshRenderer _dropRenderer;
        private MeshRenderer _stainRenderer;
        private Color _colour;
        private Color _alternateColour;
        private LayerMask _surfaces;
        private string _glassKeyword;
        private float _stainLifetime;
        private float _gravity;
        private float _pace = 1f;
        private float _launch = 1f;
        private float _spread = 1f;
        private float _poolFadeSeconds;
        private float _dryStart = 0.25f;
        private float _dryEnd = 0.65f;
        private int _dropCapacity;
        private int _poolCapacity;
        private int _printCapacity;
        private int _splashCapacity;
        private int _nextDrop;
        private int _nextStain;
        private int _nextPool;
        private int _nextPrint;
        private int _nextRecent;
        private float _dropScale = 1f;
        private readonly Trail[] _trails = NewTrails();
        private int _nextTrail;
        private float _boxDepth = BoxDepth;
        private BloodQuality _quality;
        private Color _appliedColour;
        private Color _appliedAlternateColour;
        private float _mergeGap;
        private float _mergeOverlap;
        private float _mergeSpread;
        private float _mergeRadiusMax;
        private float _mergeGrowSeconds;
        private float _mergeDryness;
        private float _wallRunSize;
        private bool _manualClock;
        private float _clock;
        private float _dropsUntil = Dormant;
        private float _stainsUntil = Dormant;

        public BloodSettings Settings { get; private set; }
        public int DropsEmitted { get; private set; }
        public int DropsLanded { get; private set; }
        public int StainsPlaced { get; private set; }
        public Vector3 LastLanding { get; private set; }
        public int WallStains { get; private set; }
        public int GlassStains { get; private set; }
        public int PoolsAlive => Count(false);
        public int PuddlesAlive => Count(true);
        public int PuddlesFormed { get; private set; }
        public int PuddleGrowths { get; private set; }
        public int PrintsPlaced { get; private set; }
        public int StampsPainted { get; private set; }
        public bool Massive => _massive;
        public Vector3 LastPoolPoint { get; private set; }

        public bool Shown { get; private set; }
        public bool Alternate { get; private set; }
        public Color Colour => Alternate ? new Color(_alternateColour.r, _alternateColour.g, _alternateColour.b, _colour.a) : _colour;
        public bool Ready => _dropVertices.IsCreated;
        public float Now => _manualClock ? _clock : Time.time;

        public bool Initialize(BloodSettings settings)
        {
            if (settings == null || settings.DropMaterial == null || settings.StainMaterial == null)
            {
                Debug.LogError("[BloodField] No BloodSettings with both materials was found; there will be no blood.", this);
                return false;
            }

            Settings = settings;
            _colour = settings.Colour;
            _alternateColour = settings.AlternateColour;
            _appliedColour = _colour;
            _appliedAlternateColour = _alternateColour;
            for (int i = 0; i < _recent.Length; i++) _recent[i].Birth = Dormant;
            _poolCapacity = Mathf.Max(1, settings.MaxPools);
            _printCapacity = Mathf.Max(1, settings.MaxFootprints);
            _splashCapacity = Mathf.Max(1, settings.MaxSplats);
            Build(settings.DropMaterial, settings.StainMaterial, settings, Mathf.Max(1, settings.MaxDrops), settings.DropSpeed, settings.DropReach);
            Read(settings);
            Show(true, false);
            return true;
        }

        public bool Apply(BloodSettings settings)
        {
            if (settings == null || _dropRenderer == null) return false;
            if (settings.DropMaterial != Settings.DropMaterial || settings.StainMaterial != Settings.StainMaterial) return false;
            if (Mathf.Max(1, settings.MaxDrops) != _dropCapacity || Mathf.Max(1, settings.MaxSplats) != _splashCapacity) return false;
            if (Mathf.Max(1, settings.MaxPools) != _poolCapacity || Mathf.Max(1, settings.MaxFootprints) != _printCapacity) return false;
            if (settings.Quality != _quality) return false;
            if (Wants(settings) != _massive) return false;
            if (WantsLiquid(settings) != _liquidOn) return false;
            if ((_massive || _liquidOn) && (!Mathf.Approximately(settings.MassiveSize, _mapSize) || Resolution(settings) != _mapResolution)) return false;

            Settings = settings;
            if (settings.Colour != _appliedColour) _colour = settings.Colour;
            if (settings.AlternateColour != _appliedAlternateColour) _alternateColour = settings.AlternateColour;
            _appliedColour = settings.Colour;
            _appliedAlternateColour = settings.AlternateColour;
            _surfaces = settings.Surfaces;
            _stainLifetime = settings.StainLifetime;
            Launch(settings.FlightPace, settings.DropSpeed, settings.DropReach);
            Read(settings);
            Show(Shown, Alternate);
            PushGlobals();
            return true;
        }

        private void Read(BloodSettings settings)
        {
            _glassKeyword = settings.GlassMaterialKeyword;
            _dropScale = settings.DropScale;
            if (_massive || _liquidOn) _stainMaterial.SetFloat(MapLifeId, _stainLifetime);
            _mergeGap = settings.MergeGap;
            _mergeOverlap = settings.MergeOverlap;
            _mergeSpread = settings.MergeSpread;
            _mergeRadiusMax = settings.MergeRadiusMax;
            _mergeGrowSeconds = settings.MergeGrowSeconds;
            _mergeDryness = settings.MergeDryness;
            _wallRunSize = settings.WallRunSize;
            _spread = settings.DropSpread;
            Dry(settings);
        }

        private void Launch(float pace, float speed, float reach)
        {
            _pace = pace;
            _launch = speed;
            _gravity = Physics.gravity.y * _pace * _pace * (speed * speed) / (reach * reach);
        }

        public void Tick(float time)
        {
            _manualClock = true;
            _clock = time;
            Step();
        }

        public void Recolour(Color colour, Color alternateColour)
        {
            _colour = colour;
            _alternateColour = alternateColour;
            if (_dropRenderer != null) Show(Shown, Alternate);
        }

        private void Dry(BloodSettings settings)
        {
            _dryStart = settings.DryStart;
            _dryEnd = Mathf.Max(settings.DryStart + 0.001f, settings.DryEnd);
            _stainMaterial.SetFloat(DryStartId, _dryStart);
            _stainMaterial.SetFloat(DryEndId, _dryEnd);
            _stainMaterial.SetFloat(FadeStartId, settings.FadeStart);
            _stainMaterial.SetFloat(RiseId, settings.StainRise);
            _stainMaterial.SetFloat(DryBrightnessId, settings.DryBrightness);
            _stainMaterial.SetFloat(DryBrownId, settings.DryBrown);
            _stainMaterial.SetFloat(DryGlossId, settings.DrySmoothness);
        }

        private void Build(Material drop, Material stain, BloodSettings settings, int dropCapacity, float speed, float reach)
        {
            _surfaces = settings.Surfaces;
            _stainLifetime = settings.StainLifetime;
            Launch(settings.FlightPace, speed, reach);
            _dropCapacity = dropCapacity;
            _quality = settings.Quality;
            _boxDepth = _quality == BloodQuality.Low ? FlatBoxDepth : BoxDepth;
            _dropMaterial = new Material(drop) { name = drop.name + " (Field)" };
            _stainMaterial = new Material(stain) { name = stain.name + " (Field)" };
            Keyword(_stainMaterial, "_BLOOD_MEDIUM", _quality == BloodQuality.Medium);
            Keyword(_stainMaterial, "_BLOOD_LOW", _quality == BloodQuality.Low);
            _stainMaterial.SetFloat(ZTestId, (float)(_quality == BloodQuality.Low ? CompareFunction.LessEqual : CompareFunction.Always));
            _poolFadeSeconds = Mathf.Max(0.1f, _stainMaterial.GetFloat(PoolFadeId));
            _massive = Wants(settings);
            _liquidOn = WantsLiquid(settings);
            _dropRenderer = BuildDrops(_dropMaterial);
            _stainRenderer = BuildStains(_stainMaterial);
            if (_massive || _liquidOn) BuildMap(settings);
            PushGlobals();
        }

        public void Show(bool shown, bool alternate)
        {
            if (_dropRenderer == null) return;

            if (Shown && !shown) Clear();
            Shown = shown;
            Alternate = alternate;
            _dropMaterial.SetColor(ColourId, Colour);
            _stainMaterial.SetColor(ColourId, Colour);
            Refresh();
        }

        public void Clear()
        {
            if (!_dropVertices.IsCreated) return;

            _landings.Clear();
            _panes.Clear();
            _poolOrders.Clear();
            _pools.Clear();
            _spurts.Clear();
            for (int i = 0; i < _recent.Length; i++) _recent[i].Birth = Dormant;
            var dormantDrop = new Vector4(0f, 0f, 0f, Dormant);
            for (int i = 0; i < _dropVertices.Length; i++)
            {
                DropVertex vertex = _dropVertices[i];
                vertex.Origin = dormantDrop;
                vertex.Velocity = Vector4.zero;
                _dropVertices[i] = vertex;
            }

            var dormantStain = new Vector4(Dormant, 0f, 0f, 0f);
            for (int i = 0; i < _stainVertices.Length; i++)
            {
                StainVertex vertex = _stainVertices[i];
                vertex.Position = Vector3.zero;
                vertex.Life = dormantStain;
                _stainVertices[i] = vertex;
            }

            _dropMesh.SetVertexBufferData(_dropVertices, 0, 0, _dropVertices.Length, 0, Quiet);
            _stainMesh.SetVertexBufferData(_stainVertices, 0, 0, _stainVertices.Length, 0, Quiet);
            _dropsUntil = Dormant;
            _stainsUntil = Dormant;
            _stamps.Clear();
            _washes.Clear();
            for (int i = 0; i < _trails.Length; i++) _trails[i].Slot = -1;
            if (_massive || _liquidOn)
            {
                ClearMap();
                ClearLiquid();
                WriteMapSlot();
            }

            Refresh();
        }

        private void Refresh()
        {
            if (_dropRenderer == null) return;

            float now = Now;
            _dropRenderer.enabled = Shown && now <= _dropsUntil + HideMargin;
            _stainRenderer.enabled = Shown && now <= _stainsUntil + HideMargin;
        }

        private void KeepStains(float end)
        {
            if (end <= _stainsUntil) return;

            _stainsUntil = end;
            Refresh();
        }

        public void Emit(BloodBurst burst, Vector3 origin, Vector3 travel, float strength = 1f)
        {
            if (burst == null || burst.CountMax <= 0) return;

            Vector3 direction = travel + Vector3.up * burst.Lift;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.up;
            int count = Mathf.RoundToInt(Random.Range(burst.CountMin, burst.CountMax + 1) * strength);
            Burst(origin, direction, count, burst.ConeDegrees, burst.SpeedMin * strength, burst.SpeedMax * strength, burst.SizeMin * _dropScale, burst.SizeMax * _dropScale, burst.StainScale, burst.Style);
            if (strength >= 1f) Mark(origin, travel, burst.MarkStyle, burst.MarkSize, burst.MarkStretch);
        }

        public void Fountain(Transform source, Vector3 axis, bool local, float seconds, float interval, BloodBurst burst)
        {
            if (!Shown || source == null || burst == null || seconds <= 0f) return;

            float now = Now;
            _spurts.Add(new Spurt { Source = source, Axis = axis, Local = local, Start = now, End = now + seconds, Next = now, Interval = Mathf.Max(0.02f, interval), Burst = burst });
        }

        public void Mark(Vector3 origin, Vector3 travel, BloodStainStyle style, float halfWidth, float stretch)
        {
            if (!Shown || !_stainVertices.IsCreated || halfWidth <= 0f) return;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit ground, MarkReach, _surfaces, QueryTriggerInteraction.Ignore)) return;

            _landings.Add(new Landing
            {
                Time = Now + MarkDelay / _pace,
                Point = ground.point,
                Normal = ground.normal,
                Travel = travel,
                HalfWidth = halfWidth,
                Stretch = Mathf.Max(1f, stretch),
                Style = (float)style,
            });
        }

        public void Burst(Vector3 origin, Vector3 direction, int count, float coneDegrees, float speedMin, float speedMax, float sizeMin, float sizeMax, float stainScale,
            BloodStainStyle style = BloodStainStyle.Splash)
        {
            if (!Shown || !_dropVertices.IsCreated || count <= 0) return;

            float now = Now;
            int first = _nextDrop;
            Quaternion aim = Quaternion.LookRotation(direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward);
            for (int i = 0; i < count; i++)
            {
                float lean = Mathf.Min(180f, coneDegrees * _spread) * Mathf.Sqrt(Random.value);
                Vector3 heading = aim * (Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)) * (Quaternion.Euler(lean, 0f, 0f) * Vector3.forward));
                Vector3 velocity = heading * (Random.Range(speedMin, speedMax) * _pace * _launch);
                float size = Mathf.Lerp(sizeMin, sizeMax, Random.value * Random.value);
                RaycastHit hit = default;
                bool landed = false;
                bool lands = stainScale > 0f;
                float flight = lands ? Fly(origin, velocity, out hit, out landed) : Random.Range(MistSecondsMin, MistSecondsMax) / _pace;

                WriteDrop(_nextDrop, origin, now, velocity, flight, size);
                _nextDrop = (_nextDrop + 1) % _dropCapacity;
                _dropsUntil = Mathf.Max(_dropsUntil, now + flight);
                DropsEmitted++;
                if (!landed) continue;

                Vector3 impact = velocity + Vector3.up * (_gravity * flight);
                float grazing = Mathf.Abs(Vector3.Dot(impact.normalized, hit.normal));
                bool glass = OnGlass(hit);
                _landings.Add(new Landing
                {
                    Time = now + flight,
                    Point = hit.point,
                    Normal = hit.normal,
                    Travel = impact,
                    HalfWidth = size * stainScale,
                    Stretch = Mathf.Clamp(1f / Mathf.Max(MinimumGrazing, grazing), 1f, MaximumStretch),
                    Style = (float)style,
                    Gathers = !glass && (style == BloodStainStyle.Splash || style == BloodStainStyle.Heavy),
                    Glass = glass,
                });
            }

            UploadDrops(first, count);
            Refresh();
        }

        private void UploadDrops(int first, int count)
        {
            int size = _blob.Length;
            if (count >= _dropCapacity)
            {
                _dropMesh.SetVertexBufferData(_dropVertices, 0, 0, _dropVertices.Length, 0, Quiet);
                return;
            }

            int head = Mathf.Min(count, _dropCapacity - first);
            _dropMesh.SetVertexBufferData(_dropVertices, first * size, first * size, head * size, 0, Quiet);
            if (count > head) _dropMesh.SetVertexBufferData(_dropVertices, 0, 0, (count - head) * size, 0, Quiet);
        }

        public void Pool(Vector3 point, Vector3 normal, float radius, float growSeconds, float lifetime)
        {
            if (!Shown) return;

            AddPool(point, normal, radius, growSeconds, lifetime, null, false);
        }

        public void PoolUnder(Transform body, Vector3 fallback, float delay, float radius, float growSeconds, float lifetime)
        {
            if (!Shown || radius <= 0f) return;

            _poolOrders.Add(new PoolOrder { Time = Now + delay, Body = body, Fallback = fallback, Radius = radius, Grow = growSeconds, Lifetime = lifetime });
        }

        public bool Wet(Vector3 point, float margin = 0f)
        {
            float now = Now;
            if (_liquidOn && WetPuddle(point, margin, now)) return true;
            return WetPool(point, margin, now);
        }

        /// <summary>True on a pool from <see cref="Pool"/> or a death that is not dry yet. Liquid on the floor is left out.</summary>
        internal bool WetPool(Vector3 point, float margin = 0f)
        {
            return WetPool(point, margin, Now);
        }

        private bool WetPool(Vector3 point, float margin, float now)
        {
            for (int i = 0; i < _pools.Count; i++)
            {
                PoolRecord pool = _pools[i];
                float dx = point.x - pool.Point.x;
                float dz = point.z - pool.Point.z;
                float reach = Reach(pool, now);
                if (reach <= 0.01f || Dryness(pool.Birth, pool.End, now) >= 1f) continue;

                reach += margin;
                if (dx * dx + dz * dz <= reach * reach && Mathf.Abs(point.y - pool.Point.y) < 1f) return true;
            }

            return false;
        }

        public void Smear(Vector3 from, Vector3 to, Vector3 normal, float width, float strength, bool start = true, bool end = true)
        {
            Smear(from, to, normal, width, strength, start, end, 0f);
        }

        /// <summary>
        /// A smear that is laid down already partly dry or extra wet. <paramref name="dryness"/> 0 is fresh, 1 looks like blood that dried on
        /// the floor: dark, matte and broken. <paramref name="wetness"/> 1 looks like the blood of a pool: thick, bright and glossy. Wetness
        /// wins over dryness. The smear then dries and fades as usual.
        /// </summary>
        public void Smear(Vector3 from, Vector3 to, Vector3 normal, float width, float strength, bool start, bool end, float dryness, float wetness = 0f)
        {
            if (!Shown || !_stainVertices.IsCreated || width <= 0f || strength <= 0f) return;

            Vector3 up = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
            Vector3 path = Vector3.ProjectOnPlane(to - from, up);
            float length = path.magnitude;
            if (length < 0.001f) return;

            float half = width * 0.5f;
            int trail = -1;
            if (!start)
                for (int i = 0; i < _trails.Length; i++)
                    if (_trails[i].Slot >= 0 && (from - _trails[i].End).sqrMagnitude < 0.0004f) trail = i;

            // how dry (above 0) or wet (below 0) the smear looks; it blends from where the trail left off over the pieces laid now
            wetness = Mathf.Clamp01(wetness);
            float look = Mathf.Clamp01(dryness) * (1f - wetness) - wetness;
            float lookBefore = trail >= 0 ? _trails[trail].Look : look;
            float distance = trail >= 0 ? _trails[trail].Distance : Random.Range(1, 40) * 100f;
            if (distance % 100f > 90f) distance = (Mathf.Floor(distance / 100f) % 40f + 1f) * 100f + 10f;
            int pieces = Mathf.Clamp(Mathf.CeilToInt(length / Mathf.Max(SmearPieceMin, width * SmearPieceWidths)), 1, SmearPiecesMax);
            float step = length / pieces;
            Vector3 heading = path / length;
            float miter = 0f;
            if (trail >= 0)
            {
                miter = Mathf.Clamp(Mathf.Tan(Vector3.SignedAngle(_trails[trail].Heading, heading, up) * Mathf.Deg2Rad * 0.5f), -SmearMiterMax, SmearMiterMax);
                Miter(_trails[trail].Slot, miter);
            }
            else
            {
                trail = _nextTrail;
                _nextTrail = (_nextTrail + 1) % _trails.Length;
            }


            int last = -1;
            for (int i = 0; i < pieces; i++)
            {
                float fade = Mathf.Lerp(1f, end ? SmearTailStrength : 1f, pieces > 1 ? i / (pieces - 1f) : 0f);
                float piece = Mathf.Clamp01(strength * fade);
                if (piece <= 0.02f) continue;

                Vector3 centre = from + heading * (step * (i + 0.5f));
                int caps = (start && i == 0 ? 1 : 0) + (end && i == pieces - 1 ? 2 : 0);
                float code = (float)BloodStainStyle.Smear + caps * 0.25f + piece * 0.24f;
                last = WriteSmear(centre, up, heading, step * 0.5f, half * SmearPad, code, distance + step * (i + 0.5f), i == 0 ? miter : 0f, !end && i == pieces - 1,
                    PackLook(Mathf.Lerp(lookBefore, look, i / (float)pieces), Mathf.Lerp(lookBefore, look, (i + 1f) / pieces)));
                if (Mathf.Abs(up.y) < WallSlope && piece > SmearRunStrength && Random.value < SmearRunChance)
                {
                    float runHalf = half * Random.Range(0.25f, 0.45f);
                    float runLength = runHalf * RunLength * Random.Range(1.2f, 2.4f);
                    Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, up).normalized;
                    Vector3 side = Vector3.Cross(up, downhill) * (Random.Range(-0.6f, 0.6f) * half);
                    WriteStain(centre + side + downhill * (half * 0.6f + runLength * 0.5f), up, Vector3.down, runLength, runHalf * RunWidth, 0f, (float)BloodStainStyle.Run, _stainLifetime);
                    PourBelow(centre + side, up, half * 0.6f + runLength * 1.35f, runHalf * RunWidth * 0.5f, RunPourDelay);
                }
            }

            _trails[trail] = end || last < 0 ? new Trail { Slot = -1 } : new Trail { Slot = last, End = to, Heading = heading, Distance = distance + length, Look = look };
        }

        public void Print(Vector3 point, Vector3 normal, Vector3 heading, float length, bool left, float strength, float lifetime)
        {
            if (!Shown) return;

            PrintsPlaced++;
            WriteStain(point, normal, heading, length * 0.5f, length * 0.42f, 2f, (left ? -1f : 1f) * Mathf.Clamp(strength, 0.05f, 1f), lifetime);
        }

        /// <summary>
        /// A print of any shape: a hand on a wall, a knee, a hoof or a piece of tyre tread. <paramref name="length"/> runs along
        /// <paramref name="heading"/> (the fingers, the front of the hoof, the way the wheel rolls) and <paramref name="width"/> across it.
        /// <paramref name="left"/> mirrors a hand. For tread pass the distance along the track as <paramref name="seed"/>, so pieces laid one
        /// after another line up. Prints share the Max Footprints slots.
        /// </summary>
        public void Print(Vector3 point, Vector3 normal, Vector3 heading, BloodPrintShape shape, float length, float width, bool left, float strength, float lifetime, float seed = 0f)
        {
            if (!Shown || length <= 0f || width <= 0f) return;

            PrintsPlaced++;
            float code = (int)shape * 2f + Mathf.Clamp(strength, 0.05f, 1f);
            WriteStain(point, normal, heading, length * 0.5f, width * 0.5f, 2f, left ? -code : code, lifetime, false, Mathf.Max(0f, seed));
        }

        private void Update()
        {
            if (_manualClock) return;

            Step();
        }

        private void Step()
        {
            if (!_dropVertices.IsCreated) return;

            PushGlobals();
            float now = Now;
            for (int i = _spurts.Count - 1; i >= 0; i--)
            {
                Spurt spurt = _spurts[i];
                if (spurt.Source == null || now > spurt.End)
                {
                    _spurts.RemoveAt(i);
                    continue;
                }

                if (now < spurt.Next) continue;

                spurt.Next = now + spurt.Interval;
                _spurts[i] = spurt;
                float left = 1f - (now - spurt.Start) / (spurt.End - spurt.Start);
                Vector3 aim = spurt.Local ? spurt.Source.TransformDirection(spurt.Axis) : spurt.Axis;
                Emit(spurt.Burst, spurt.Source.position, aim, Mathf.Lerp(0.35f, 0.99f, left));
            }

            for (int i = _poolOrders.Count - 1; i >= 0; i--)
            {
                PoolOrder order = _poolOrders[i];
                if (order.Time > now) continue;

                _poolOrders[i] = _poolOrders[_poolOrders.Count - 1];
                _poolOrders.RemoveAt(_poolOrders.Count - 1);
                Vector3 from = (order.Body != null ? order.Body.position : order.Fallback) + Vector3.up * PoolProbeHeight;
                if (!Physics.Raycast(from, Vector3.down, out RaycastHit ground, PoolProbeReach, _surfaces, QueryTriggerInteraction.Ignore)) continue;

                AddPool(ground.point, ground.normal, order.Radius, order.Grow, order.Lifetime, order.Body, false);
            }

            for (int i = _pools.Count - 1; i >= 0; i--)
            {
                PoolRecord pool = _pools[i];
                if (now >= pool.End)
                {
                    _pools.RemoveAt(i);
                    continue;
                }

                if (!pool.Follows || pool.Body != null) continue;

                pool.Follows = false;
                pool.End = Mathf.Min(pool.End, now + _poolFadeSeconds);
                _pools[i] = pool;
                Shorten(pool);
            }

            for (int i = _landings.Count - 1; i >= 0; i--)
            {
                Landing landing = _landings[i];
                if (landing.Time > now) continue;

                _landings[i] = _landings[_landings.Count - 1];
                _landings.RemoveAt(_landings.Count - 1);
                DropsLanded++;
                LastLanding = landing.Point;
                if (landing.Glass) GlassStains++;
                bool wall = Mathf.Abs(landing.Normal.y) < WallSlope;
                if (wall) WallStains++;
                if (wall && landing.HalfWidth >= _wallRunSize)
                {
                    float runLength = landing.HalfWidth * RunLength;
                    Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, landing.Normal).normalized;
                    WriteStain(landing.Point + downhill * (runLength * 0.5f), landing.Normal, Vector3.down, runLength, landing.HalfWidth * RunWidth, 0f, (float)BloodStainStyle.Run, _stainLifetime, landing.Glass);
                    if (!landing.Glass) PourBelow(landing.Point, landing.Normal, runLength * 1.35f, landing.HalfWidth * RunWidth * 0.5f, RunPourDelay);
                    continue;
                }

                bool liquid = _liquidOn && !wall && landing.Gathers && OnMap(landing.Point);
                if (liquid) Pour(landing.Point, landing.HalfWidth * 1.3f, LiquidFilm * 0.35f * Mathf.PI * landing.HalfWidth * landing.HalfWidth, 0.25f, 0f);
                if (!wall && landing.Gathers && !liquid && Gather(landing, now)) continue;

                if (_massive && !wall && !landing.Glass && OnMap(landing.Point))
                    _stamps.Add(new Stamp { Point = landing.Point, Travel = landing.Travel, HalfU = landing.HalfWidth * landing.Stretch, HalfV = landing.HalfWidth, Style = landing.Style, Birth = now });
                else
                    WriteStain(landing.Point, landing.Normal, landing.Travel, landing.HalfWidth * landing.Stretch, landing.HalfWidth, 0f, landing.Style, _stainLifetime, landing.Glass);
                if (wall || !landing.Gathers) continue;

                _recent[_nextRecent] = new Recent { Point = landing.Point, HalfWidth = landing.HalfWidth, Birth = now };
                _nextRecent = (_nextRecent + 1) % RecentCapacity;
            }

            if (_massive || _liquidOn)
            {
                Follow();
                if (_massive) PaintStamps();
                if (_liquidOn) StepLiquid();
            }

            StepWash();
            Refresh();
        }

        private static bool Wants(BloodSettings settings)
        {
            return settings.Massive && settings.Quality != BloodQuality.Low && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf);
        }

        private static int Resolution(BloodSettings settings)
        {
            return Mathf.ClosestPowerOfTwo(Mathf.Clamp(settings.MassiveResolution, 512, 4096));
        }

        private void BuildMap(BloodSettings settings)
        {
            var stamp = Resources.Load<Material>(StampResource);
            if (stamp == null)
            {
                _massive = false;
                return;
            }

            _mapSize = settings.MassiveSize;
            _mapResolution = Resolution(settings);
            if (_massive)
            {
                _map = MapTexture("Blood Map");
                _mapSpare = MapTexture("Blood Map Spare");
            }

            _stamper = new Material(stamp) { name = stamp.name + " (Field)" };
            _stamper.SetFloat("_SplatSeconds", _stainMaterial.GetFloat("_SplatSeconds"));
            _commands = new CommandBuffer { name = "Blood Map" };
            _stampBlock = new MaterialPropertyBlock();
            _quad = new Mesh { name = "Blood Stamp" };
            _quad.SetVertices(new[] { new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f), new Vector3(-1f, 1f, 0f), new Vector3(1f, 1f, 0f) });
            _quad.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
            if (_massive) _stainMaterial.SetTexture(MapId, _map);
            _stainMaterial.SetFloat(MapLifeId, _stainLifetime);
            _stainMaterial.SetFloat(MapTexelId, 1f / _mapResolution);
            if (_liquidOn) BuildLiquid(settings);
            Camera eye = Camera.main;
            _mapCentre = Snap(eye != null ? Focus(eye) : Vector3.zero);
            ClearMap();
            WriteMapSlot();
        }

        private RenderTexture MapTexture(string objectName)
        {
            var texture = new RenderTexture(_mapResolution, _mapResolution, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            {
                name = objectName,
                useMipMap = false,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.Create();
            return texture;
        }

        private void ClearMap()
        {
            if (_commands == null || _map == null) return;

            _commands.Clear();
            _commands.SetRenderTarget(_map);
            _commands.ClearRenderTarget(false, true, Color.clear);
            Graphics.ExecuteCommandBuffer(_commands);
        }

        private void WriteMapSlot()
        {
            WriteMapSlot(_mapSlot, 0f);
            WriteMapSlot(_liquidSlot, 1f);
        }

        private void WriteMapSlot(int slot, float layer)
        {
            if (slot < 0) return;

            float half = _mapSize * 0.5f;
            Camera eye = Camera.main;
            float reach = eye != null ? Mathf.Min(half, eye.farClipPlane * MapFarShare) : half;
            float height = eye != null ? Mathf.Min(MapHeight, eye.farClipPlane * MapFarShare) : MapHeight;
            Vector3 focus = eye != null ? Focus(eye) : _mapCentre;
            var box = new Vector3(Mathf.Clamp(focus.x, _mapCentre.x - half + reach, _mapCentre.x + half - reach), eye != null ? eye.transform.position.y : _mapCentre.y, Mathf.Clamp(focus.z, _mapCentre.z - half + reach, _mapCentre.z + half - reach));
            _mapBox = box;
            int start = slot * BoxCorners;
            var centre = new Vector4(_mapCentre.x, _mapCentre.y, _mapCentre.z, 1f);
            var axisU = new Vector4(1f, 0f, 0f, 1f / half);
            var axisV = new Vector4(0f, 0f, -1f, 1f / half);
            var life = new Vector4(Dormant, float.MaxValue * 0.5f, 3f, layer);
            for (int corner = 0; corner < BoxCorners; corner++)
            {
                float u = (corner & 1) != 0 ? -1f : 1f;
                float h = (corner & 2) != 0 ? 1f : -1f;
                float v = (corner & 4) != 0 ? 1f : -1f;
                _stainVertices[start + corner] = new StainVertex
                {
                    Position = box + new Vector3(u * reach, h * height, -v * reach),
                    Normal = Vector3.up,
                    Centre = centre,
                    AxisU = axisU,
                    AxisV = axisV,
                    Life = life,
                };
            }

            _stainMesh.SetVertexBufferData(_stainVertices, start, start, BoxCorners, 0, Quiet);
        }

        private bool OnMap(Vector3 point)
        {
            float half = _mapSize * 0.5f;
            return Mathf.Abs(point.x - _mapCentre.x) < half && Mathf.Abs(point.z - _mapCentre.z) < half && Mathf.Abs(point.y - _mapCentre.y) < MapHeight;
        }

        private Vector3 Snap(Vector3 point)
        {
            float texel = _mapSize / (_liquidOn ? Mathf.Min(_mapResolution, _liquidResolution) : _mapResolution);
            return new Vector3(Mathf.Round(point.x / texel) * texel, point.y, Mathf.Round(point.z / texel) * texel);
        }

        private static Vector3 Focus(Camera eye)
        {
            Vector3 origin = eye.transform.position;
            Vector3 forward = eye.transform.forward;
            if (forward.y < -0.05f)
            {
                float distance = Mathf.Min(origin.y / -forward.y, eye.farClipPlane * 0.5f);
                if (distance > 0f) return origin + forward * distance;
            }

            return origin;
        }

        private void Follow()
        {
            Camera eye = Camera.main;
            if (eye == null) return;

            Vector3 focus = Focus(eye);
            float limit = _mapSize * MapFollow;
            if (Mathf.Abs(focus.x - _mapCentre.x) < limit && Mathf.Abs(focus.z - _mapCentre.z) < limit)
            {
                Vector3 seen = new(focus.x, eye.transform.position.y, focus.z);
                if ((seen - _mapBox).sqrMagnitude > MapBoxSlack * MapBoxSlack) WriteMapSlot();
                return;
            }

            Vector3 next = Snap(new Vector3(focus.x, _mapCentre.y, focus.z));
            Vector3 shift = next - _mapCentre;
            ShiftLiquid(shift);
            if (_map == null)
            {
                _mapCentre = next;
                WriteMapSlot();
                return;
            }

            _commands.Clear();
            _commands.SetRenderTarget(_mapSpare);
            _commands.ClearRenderTarget(false, true, Color.clear);
            _commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.Ortho(0f, 1f, 0f, 1f, -1f, 1f));
            _commands.SetGlobalTexture(MapSourceId, _map);
            _commands.SetGlobalVector(ShiftOffsetId, new Vector4(shift.x / _mapSize, -shift.z / _mapSize, 0f, 0f));
            _commands.DrawMesh(_quad, Matrix4x4.TRS(new Vector3(0.5f, 0.5f, 0f), Quaternion.identity, new Vector3(0.5f, 0.5f, 1f)), _stamper, 0, 1);
            Graphics.ExecuteCommandBuffer(_commands);
            (_map, _mapSpare) = (_mapSpare, _map);
            _mapCentre = next;
            _stainMaterial.SetTexture(MapId, _map);
            WriteMapSlot();
        }

        private void PaintStamps()
        {
            if (_stamps.Count == 0) return;

            float half = _mapSize * 0.5f;
            _commands.Clear();
            _commands.SetRenderTarget(_map);
            _commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.Ortho(0f, 1f, 0f, 1f, -1f, 1f));
            float end = 0f;
            foreach (Stamp stamp in _stamps)
            {
                Vector3 along = new Vector3(stamp.Travel.x, 0f, stamp.Travel.z);
                if (along.sqrMagnitude < 0.0001f) along = Vector3.right;
                along.Normalize();
                float angle = Mathf.Atan2(-along.z, along.x) * Mathf.Rad2Deg;
                var position = new Vector3((stamp.Point.x - _mapCentre.x + half) / _mapSize, (_mapCentre.z - stamp.Point.z + half) / _mapSize, 0f);
                var scale = new Vector3(Mathf.Max(0.001f, stamp.HalfU) / _mapSize, Mathf.Max(0.001f, stamp.HalfV) / _mapSize, 1f);
                _stampBlock.SetVector(StampShapeId, new Vector4(Random.Range(1f, 100f), stamp.HalfV / Mathf.Max(0.001f, stamp.HalfU), stamp.Style, 0f));
                _stampBlock.SetVector(StampValueId, new Vector4(stamp.Birth, stamp.Point.y, 0f, 0f));
                _commands.DrawMesh(_quad, Matrix4x4.TRS(position, Quaternion.Euler(0f, 0f, angle), scale), _stamper, 0, 0, _stampBlock);
                end = Mathf.Max(end, stamp.Birth + _stainLifetime);
                StampsPainted++;
                StainsPlaced++;
            }

            Graphics.ExecuteCommandBuffer(_commands);
            _stamps.Clear();
            KeepStains(end);
        }

        private bool Gather(Landing landing, float now)
        {
            Vector3 point = landing.Point;
            float half = landing.HalfWidth;
            for (int i = 0; i < _pools.Count; i++)
            {
                PoolRecord pool = _pools[i];
                if (!pool.Gathers || !Fresh(pool.Birth, pool.End, now) || Mathf.Abs(point.y - pool.Point.y) > MergeStep) continue;

                float reach = pool.Radius * PoolBody + half * _mergeOverlap + _mergeGap;
                float dx = point.x - pool.Point.x;
                float dz = point.z - pool.Point.z;
                if (dx * dx + dz * dz > reach * reach) continue;

                pool.Area += half * half;
                _pools[i] = pool;
                float shown = Mathf.Min(_mergeRadiusMax, _mergeSpread * Mathf.Sqrt(pool.Area));
                Resize(i, Mathf.Max(pool.Radius, shown / PoolBody), now);
                PuddleGrowths++;
                return true;
            }

            for (int i = 0; i < _recent.Length; i++)
            {
                Recent recent = _recent[i];
                if (!Fresh(recent.Birth, recent.Birth + _stainLifetime, now) || Mathf.Abs(point.y - recent.Point.y) > MergeStep) continue;

                float reach = (recent.HalfWidth + half) * _mergeOverlap + _mergeGap;
                float dx = point.x - recent.Point.x;
                float dz = point.z - recent.Point.z;
                float apart = dx * dx + dz * dz;
                if (apart > reach * reach) continue;

                float area = recent.HalfWidth * recent.HalfWidth + half * half;
                float cover = Mathf.Sqrt(apart) * 0.5f + Mathf.Max(half, recent.HalfWidth) * 0.6f;
                float shown = Mathf.Min(_mergeRadiusMax, Mathf.Max(_mergeSpread * Mathf.Sqrt(area), cover));
                Vector3 centre = Vector3.Lerp(recent.Point, point, half * half / area);
                int index = AddPool(centre, landing.Normal, shown / PoolBody, _mergeGrowSeconds, _stainLifetime + _poolFadeSeconds, null, true);
                if (index < 0) return false;

                PoolRecord formed = _pools[index];
                formed.Area = area;
                _pools[index] = formed;
                _recent[i].Birth = Dormant;
                PuddlesFormed++;
                return true;
            }

            return false;
        }

        private void Resize(int index, float radius, float now)
        {
            PoolRecord pool = _pools[index];
            float empty = 1f - Mathf.Clamp01((now - pool.Birth) / pool.Grow);
            float shown = pool.Radius * (1f - empty * empty * empty);
            float kept = 1f - Mathf.Pow(1f - Mathf.Clamp01(shown / radius), 1f / 3f);
            pool.Radius = radius;
            pool.Grow = _mergeGrowSeconds;
            pool.Birth = now - kept * pool.Grow;
            pool.End = now + _stainLifetime + _poolFadeSeconds;
            _pools[index] = pool;
            KeepStains(pool.End);

            int start = pool.Slot * BoxCorners;
            StainVertex first = _stainVertices[start];
            Vector3 centre = first.Centre;
            Vector3 along = first.AxisU;
            Vector3 across = first.AxisV;
            Vector3 up = first.Normal;
            var axisU = new Vector4(along.x, along.y, along.z, 1f / radius);
            var axisV = new Vector4(across.x, across.y, across.z, 1f / radius);
            var life = new Vector4(pool.Birth, pool.End - pool.Birth, 1f, pool.Grow);
            for (int corner = 0; corner < BoxCorners; corner++)
            {
                float u = (corner & 1) != 0 ? 1f : -1f;
                float h = (corner & 2) != 0 ? 1f : -1f;
                float v = (corner & 4) != 0 ? 1f : -1f;
                StainVertex vertex = _stainVertices[start + corner];
                vertex.Position = centre + along * (u * radius) + up * (h * _boxDepth) + across * (v * radius);
                vertex.AxisU = axisU;
                vertex.AxisV = axisV;
                vertex.Life = life;
                _stainVertices[start + corner] = vertex;
            }

            _stainMesh.SetVertexBufferData(_stainVertices, start, start, BoxCorners, 0, Quiet);
        }

        private int Count(bool gathers)
        {
            int count = 0;
            for (int i = 0; i < _pools.Count; i++)
                if (_pools[i].Gathers == gathers) count++;

            return count;
        }

        private void OnDestroy()
        {
            if (_dropVertices.IsCreated) _dropVertices.Dispose();
            if (_stainVertices.IsCreated) _stainVertices.Dispose();
            Release(_dropMesh);
            Release(_stainMesh);
            Release(_quad);
            Release(_stamper);
            ReleaseLiquid();
            if (_map != null) _map.Release();
            if (_mapSpare != null) _mapSpare.Release();
            Release(_map);
            Release(_mapSpare);
            _commands?.Release();
            Release(_dropMaterial);
            Release(_stainMaterial);
        }

        private static void Release(Object target)
        {
            if (target == null) return;

            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        private void PushGlobals()
        {
            Shader.SetGlobalFloat(TimeId, Now);
            Shader.SetGlobalFloat(GravityId, _gravity);
        }

        private float Fly(Vector3 origin, Vector3 velocity, out RaycastHit hit, out bool landed)
        {
            Vector3 from = origin;
            float before = 0f;
            float step = TrajectoryStepSeconds / _pace;
            for (int i = 0; i < TrajectorySteps; i++)
            {
                float after = before + step;
                Vector3 to = origin + velocity * after + Vector3.up * (0.5f * _gravity * after * after);
                Vector3 leg = to - from;
                float length = leg.magnitude;
                if (length > 0.0001f && Physics.Raycast(from, leg / length, out hit, length, _surfaces, QueryTriggerInteraction.Ignore))
                {
                    landed = true;
                    return before + step * (hit.distance / length);
                }

                from = to;
                before = after;
            }

            hit = default;
            landed = false;
            return before;
        }

        private void WriteDrop(int slot, Vector3 origin, float birth, Vector3 velocity, float flight, float size)
        {
            int start = slot * _blob.Length;
            var originData = new Vector4(origin.x, origin.y, origin.z, birth);
            var velocityData = new Vector4(velocity.x, velocity.y, velocity.z, flight);
            var misc = new Vector4(size, Random.value, 0f, 0f);
            for (int i = 0; i < _blob.Length; i++)
                _dropVertices[start + i] = new DropVertex { Position = _blob[i], Origin = originData, Velocity = velocityData, Misc = misc };
        }

        private int AddPool(Vector3 point, Vector3 normal, float radius, float growSeconds, float lifetime, Transform body, bool gathers)
        {
            if (_liquidOn && normal.y > 0.7f)
            {
                float spread = radius * PoolBody * 1.05f;
                if (Pour(point, Mathf.Max(0.05f, radius * 0.35f), LiquidFilm * Mathf.PI * spread * spread, Mathf.Max(0.3f, growSeconds), 0f))
                {
                    if (!gathers) LastPoolPoint = point;
                    return -1;
                }
            }

            int slot = WriteStain(point, normal, Vector3.forward, radius, radius, 1f, growSeconds, lifetime);
            if (slot < 0) return -1;

            for (int i = _pools.Count - 1; i >= 0; i--)
                if (_pools[i].Slot == slot) _pools.RemoveAt(i);

            float now = Now;
            _pools.Add(new PoolRecord { Slot = slot, Body = body, Follows = body != null, Point = point, Radius = radius, Birth = now, Grow = Mathf.Max(0.01f, growSeconds), End = now + lifetime, Gathers = gathers });
            if (!gathers) LastPoolPoint = point;
            return _pools.Count - 1;
        }

        private bool Fresh(float birth, float end, float now)
        {
            return now < end && Dryness(birth, end, now) <= _mergeDryness;
        }

        private float Dryness(float birth, float end, float now)
        {
            float t = Mathf.Clamp01((now - birth) / Mathf.Max(0.01f, end - birth));
            float x = Mathf.Clamp01((t - _dryStart) / (_dryEnd - _dryStart));
            return x * x * (3f - 2f * x);
        }

        private float Reach(PoolRecord pool, float now)
        {
            float fill = Mathf.Clamp01((now - pool.Birth) / pool.Grow);
            float leaving = Mathf.Clamp01((now - (pool.End - _poolFadeSeconds)) / _poolFadeSeconds);
            float empty = 1f - fill;
            return pool.Radius * PoolBody * (1f - empty * empty * empty) * (1f - leaving * leaving);
        }

        private void Shorten(PoolRecord pool)
        {
            int start = pool.Slot * BoxCorners;
            for (int i = 0; i < BoxCorners; i++)
            {
                StainVertex vertex = _stainVertices[start + i];
                vertex.Life.y = pool.End - pool.Birth;
                _stainVertices[start + i] = vertex;
            }

            _stainMesh.SetVertexBufferData(_stainVertices, start, start, BoxCorners, 0, Quiet);
        }

        private int Reserve(float kind)
        {
            if (kind > 2.5f) return _mapSlot;

            if (kind > 1.5f)
            {
                int print = _poolCapacity + _nextPrint;
                _nextPrint = (_nextPrint + 1) % _printCapacity;
                return print;
            }

            if (kind > 0.5f)
            {
                int pool = _nextPool;
                _nextPool = (_nextPool + 1) % _poolCapacity;
                return pool;
            }

            int splash = _poolCapacity + _printCapacity + _nextStain;
            _nextStain = (_nextStain + 1) % _splashCapacity;
            return splash;
        }

        private bool OnGlass(RaycastHit hit)
        {
            Collider surface = hit.collider;
            if (surface == null || string.IsNullOrEmpty(_glassKeyword)) return false;

            if (!_panes.TryGetValue(surface, out Renderer[] panes))
            {
                panes = FindPanes(surface, _glassKeyword);
                _panes[surface] = panes;
            }

            foreach (Renderer pane in panes)
            {
                if (pane == null) continue;

                Bounds bounds = pane.bounds;
                bounds.Expand(GlassMargin);
                if (bounds.Contains(hit.point)) return true;
            }

            return false;
        }

        private static Renderer[] FindPanes(Collider surface, string keyword)
        {
            var panes = new List<Renderer>();
            foreach (Renderer candidate in surface.GetComponentsInChildren<Renderer>())
            {
                foreach (Material material in candidate.sharedMaterials)
                {
                    if (material == null || !material.name.Contains(keyword)) continue;

                    panes.Add(candidate);
                    break;
                }
            }

            return panes.ToArray();
        }

        private int WriteStain(Vector3 point, Vector3 normal, Vector3 travel, float halfU, float halfV, float kind, float parameter, float lifetime, bool glass = false, float seed = 0f)
        {
            if (!_stainVertices.IsCreated) return -1;

            Vector3 up = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
            Vector3 along = Vector3.ProjectOnPlane(travel, up);
            if (along.sqrMagnitude < 0.0001f) along = Vector3.ProjectOnPlane(Mathf.Abs(up.y) > 0.9f ? Vector3.forward : Vector3.up, up);
            along.Normalize();
            Vector3 across = Vector3.Cross(up, along);

            int slot = Reserve(kind);
            int start = slot * BoxCorners;
            StainsPlaced++;

            var centre = new Vector4(point.x, point.y, point.z, (seed > 0f ? seed : Random.Range(1f, 100f)) * (glass ? -1f : 1f));
            var axisU = new Vector4(along.x, along.y, along.z, 1f / Mathf.Max(0.001f, halfU));
            var axisV = new Vector4(across.x, across.y, across.z, 1f / Mathf.Max(0.001f, halfV));
            var life = new Vector4(Now, lifetime, kind, parameter);
            for (int corner = 0; corner < BoxCorners; corner++)
            {
                float u = (corner & 1) != 0 ? 1f : -1f;
                float h = (corner & 2) != 0 ? 1f : -1f;
                float v = (corner & 4) != 0 ? 1f : -1f;
                _stainVertices[start + corner] = new StainVertex
                {
                    Position = point + along * (u * halfU) + up * (h * _boxDepth) + across * (v * halfV),
                    Normal = up,
                    Centre = centre,
                    AxisU = axisU,
                    AxisV = axisV,
                    Life = life,
                };
            }

            _stainMesh.SetVertexBufferData(_stainVertices, start, start, BoxCorners, 0, Quiet);
            KeepStains(life.x + life.y);
            return slot;
        }

        private static void Keyword(Material material, string keyword, bool on)
        {
            if (on) material.EnableKeyword(keyword);
            else material.DisableKeyword(keyword);
        }

        // the look at the start and end of a smear piece, packed into the fraction of its open flag as two bytes; 0 keeps the plain smear
        private static float PackLook(float from, float to)
        {
            if (Mathf.Abs(from) < 0.002f && Mathf.Abs(to) < 0.002f) return 0f;

            float a = Mathf.Round((Mathf.Clamp(from, -1f, 1f) + 1f) * 127f) + 1f;
            float b = Mathf.Round((Mathf.Clamp(to, -1f, 1f) + 1f) * 127f) + 1f;
            return (a * 256f + b) / 65536f;
        }

        private int WriteSmear(Vector3 point, Vector3 up, Vector3 along, float halfU, float halfV, float code, float distance, float startMiter, bool open, float look = 0f)
        {
            Vector3 across = Vector3.Cross(up, along);
            int slot = Reserve(0f);
            int start = slot * BoxCorners;
            StainsPlaced++;
            float reach = halfU + halfV * SmearMiterMax;
            var centre = new Vector4(point.x, point.y, point.z, distance);
            var axisU = new Vector4(along.x, along.y, along.z, 1f / Mathf.Max(0.001f, halfU));
            var axisV = new Vector4(across.x, across.y, across.z, 1f / Mathf.Max(0.001f, halfV));
            var life = new Vector4(Now, _stainLifetime, 0f, code);
            var miter = new Vector3(startMiter, 0f, (open ? 1f : 0f) + look);
            for (int corner = 0; corner < BoxCorners; corner++)
            {
                float u = (corner & 1) != 0 ? 1f : -1f;
                float h = (corner & 2) != 0 ? 1f : -1f;
                float v = (corner & 4) != 0 ? 1f : -1f;
                _stainVertices[start + corner] = new StainVertex
                {
                    Position = point + along * (u * reach) + up * (h * _boxDepth) + across * (v * halfV),
                    Normal = miter,
                    Centre = centre,
                    AxisU = axisU,
                    AxisV = axisV,
                    Life = life,
                };
            }

            _stainMesh.SetVertexBufferData(_stainVertices, start, start, BoxCorners, 0, Quiet);
            KeepStains(life.x + life.y);
            return slot;
        }

        private void Miter(int slot, float endMiter)
        {
            int start = slot * BoxCorners;
            for (int corner = 0; corner < BoxCorners; corner++)
            {
                StainVertex vertex = _stainVertices[start + corner];
                vertex.Normal.y = endMiter;
                vertex.Normal.z -= Mathf.Floor(vertex.Normal.z);
                _stainVertices[start + corner] = vertex;
            }

            _stainMesh.SetVertexBufferData(_stainVertices, start, start, BoxCorners, 0, Quiet);
        }

        private MeshRenderer BuildDrops(Material material)
        {
            _blob = BuildBlob(_quality != BloodQuality.Low, out int[] blobTriangles);
            int vertexCount = _dropCapacity * _blob.Length;
            _dropVertices = new NativeArray<DropVertex>(vertexCount, Allocator.Persistent);
            var dormant = new Vector4(0f, 0f, 0f, Dormant);
            for (int i = 0; i < vertexCount; i++)
                _dropVertices[i] = new DropVertex { Position = _blob[i % _blob.Length], Origin = dormant };

            _dropMesh = new Mesh { name = "Blood Drops" };
            _dropMesh.MarkDynamic();
            _dropMesh.SetVertexBufferParams(vertexCount,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4));
            _dropMesh.SetVertexBufferData(_dropVertices, 0, 0, vertexCount, 0, Quiet);
            int indexCount = _dropCapacity * blobTriangles.Length;
            if (vertexCount <= ushort.MaxValue)
            {
                var indices = new NativeArray<ushort>(indexCount, Allocator.Temp);
                for (int slot = 0; slot < _dropCapacity; slot++)
                    for (int i = 0; i < blobTriangles.Length; i++)
                        indices[slot * blobTriangles.Length + i] = (ushort)(slot * _blob.Length + blobTriangles[i]);
                MeshRenderer small = Finish(_dropMesh, indices, "Blood Drops", material);
                indices.Dispose();
                return small;
            }

            // more drops than 16-bit indices can reach
            var wide = new NativeArray<uint>(indexCount, Allocator.Temp);
            for (int slot = 0; slot < _dropCapacity; slot++)
                for (int i = 0; i < blobTriangles.Length; i++)
                    wide[slot * blobTriangles.Length + i] = (uint)(slot * _blob.Length + blobTriangles[i]);
            _dropMesh.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
            _dropMesh.SetIndexBufferData(wide, 0, 0, indexCount, Quiet);
            wide.Dispose();
            return Finish(_dropMesh, indexCount, "Blood Drops", material);
        }

        private MeshRenderer BuildStains(Material material)
        {
            int stainCapacity = _poolCapacity + _printCapacity + _splashCapacity + (_massive ? 1 : 0) + (_liquidOn ? 1 : 0);
            _liquidSlot = _liquidOn ? stainCapacity - 1 : -1;
            _mapSlot = _massive ? stainCapacity - 1 - (_liquidOn ? 1 : 0) : -1;
            int vertexCount = stainCapacity * BoxCorners;
            _stainVertices = new NativeArray<StainVertex>(vertexCount, Allocator.Persistent);
            var dormant = new Vector4(Dormant, 0f, 0f, 0f);
            for (int i = 0; i < vertexCount; i++)
                _stainVertices[i] = new StainVertex { Normal = Vector3.up, Life = dormant };

            int[] box = BuildBox();
            _stainMesh = new Mesh { name = "Blood Stains" };
            _stainMesh.MarkDynamic();
            _stainMesh.SetVertexBufferParams(vertexCount,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 4));
            _stainMesh.SetVertexBufferData(_stainVertices, 0, 0, vertexCount, 0, Quiet);
            int indexCount = stainCapacity * BoxIndices;
            if (vertexCount <= ushort.MaxValue)
            {
                var indices = new NativeArray<ushort>(indexCount, Allocator.Temp);
                for (int slot = 0; slot < stainCapacity; slot++)
                    for (int i = 0; i < BoxIndices; i++)
                        indices[slot * BoxIndices + i] = (ushort)(slot * BoxCorners + box[i]);
                MeshRenderer small = Finish(_stainMesh, indices, "Blood Stains", material);
                indices.Dispose();
                return small;
            }

            // more stain slots than 16-bit indices can reach: the last slots, the liquid floor among them, would wrap and vanish
            var wide = new NativeArray<uint>(indexCount, Allocator.Temp);
            for (int slot = 0; slot < stainCapacity; slot++)
                for (int i = 0; i < BoxIndices; i++)
                    wide[slot * BoxIndices + i] = (uint)(slot * BoxCorners + box[i]);
            _stainMesh.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
            _stainMesh.SetIndexBufferData(wide, 0, 0, indexCount, Quiet);
            wide.Dispose();
            return Finish(_stainMesh, indexCount, "Blood Stains", material);
        }

        private MeshRenderer Finish(Mesh mesh, NativeArray<ushort> indices, string objectName, Material material)
        {
            mesh.SetIndexBufferParams(indices.Length, IndexFormat.UInt16);
            mesh.SetIndexBufferData(indices, 0, 0, indices.Length, Quiet);
            return Finish(mesh, indices.Length, objectName, material);
        }

        private MeshRenderer Finish(Mesh mesh, int indexCount, string objectName, Material material)
        {
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, indexCount), Quiet);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * WorldExtent);

            var holder = new GameObject(objectName);
            holder.transform.SetParent(transform, false);
            holder.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            holder.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = holder.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            return meshRenderer;
        }

        private static int[] BuildBox()
        {
            int[] quads =
            {
                0, 1, 3, 2,
                4, 6, 7, 5,
                0, 4, 5, 1,
                2, 3, 7, 6,
                0, 2, 6, 4,
                1, 5, 7, 3,
            };
            var corners = new Vector3[BoxCorners];
            for (int corner = 0; corner < BoxCorners; corner++)
                corners[corner] = new Vector3((corner & 1) != 0 ? 1f : -1f, (corner & 2) != 0 ? 1f : -1f, (corner & 4) != 0 ? 1f : -1f);

            var triangles = new int[BoxIndices];
            for (int face = 0; face < 6; face++)
            {
                int a = quads[face * 4];
                int b = quads[face * 4 + 1];
                int c = quads[face * 4 + 2];
                int d = quads[face * 4 + 3];
                Write(triangles, face * 6, corners, a, b, c);
                Write(triangles, face * 6 + 3, corners, a, c, d);
            }

            return triangles;
        }

        private static Vector3[] BuildBlob(bool smooth, out int[] triangles)
        {
            float golden = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var points = new List<Vector3>
            {
                new(-1f, golden, 0f), new(1f, golden, 0f), new(-1f, -golden, 0f), new(1f, -golden, 0f),
                new(0f, -1f, golden), new(0f, 1f, golden), new(0f, -1f, -golden), new(0f, 1f, -golden),
                new(golden, 0f, -1f), new(golden, 0f, 1f), new(-golden, 0f, -1f), new(-golden, 0f, 1f),
            };
            for (int i = 0; i < points.Count; i++) points[i] = points[i].normalized;

            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            if (!smooth)
            {
                Vector3[] corners = points.ToArray();
                triangles = new int[faces.Length];
                for (int i = 0; i < faces.Length; i += 3)
                    Write(triangles, i, corners, faces[i], faces[i + 1], faces[i + 2]);

                return corners;
            }

            var midpoints = new Dictionary<long, int>();
            var result = new List<int>(faces.Length * 4);
            for (int i = 0; i < faces.Length; i += 3)
            {
                int a = faces[i];
                int b = faces[i + 1];
                int c = faces[i + 2];
                int ab = Midpoint(points, midpoints, a, b);
                int bc = Midpoint(points, midpoints, b, c);
                int ca = Midpoint(points, midpoints, c, a);
                result.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }

            Vector3[] vertices = points.ToArray();
            triangles = new int[result.Count];
            for (int i = 0; i < result.Count; i += 3)
                Write(triangles, i, vertices, result[i], result[i + 1], result[i + 2]);

            return vertices;
        }

        private static int Midpoint(List<Vector3> points, Dictionary<long, int> midpoints, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (midpoints.TryGetValue(key, out int index)) return index;

            points.Add(((points[a] + points[b]) * 0.5f).normalized);
            index = points.Count - 1;
            midpoints[key] = index;
            return index;
        }

        private static void Write(int[] triangles, int at, Vector3[] vertices, int a, int b, int c)
        {
            Vector3 facing = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            bool outward = Vector3.Dot(facing, vertices[a] + vertices[b] + vertices[c]) > 0f;
            triangles[at] = a;
            triangles[at + 1] = outward ? b : c;
            triangles[at + 2] = outward ? c : b;
        }
    }
}
