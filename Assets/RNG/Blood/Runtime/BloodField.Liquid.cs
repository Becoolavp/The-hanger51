using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rng.Blood
{
    public partial class BloodField
    {
        private const int FeedCapacity = 16;
        private const int LiquidResolutionMax = 1024;
        private const float LiquidFilm = 1.5f;
        private const float LiquidStepsPerSecond = 180f;
        private const int LiquidStepsMax = 6;
        private const float LiquidResidueFade = 4f;
        private const float RunPourDelay = 1.4f;

        private static readonly int LiquidId = Shader.PropertyToID("_BloodLiquid");
        private static readonly int LiquidSourceId = Shader.PropertyToID("_BloodLiquidSource");
        private static readonly int LiquidFilmId = Shader.PropertyToID("_BloodLiquidFilm");
        private static readonly int LiquidTexelId = Shader.PropertyToID("_BloodLiquidTexel");
        private static readonly int FeedsId = Shader.PropertyToID("_LiquidFeeds");
        private static readonly int FeedHeightsId = Shader.PropertyToID("_LiquidFeedHeights");
        private static readonly int FeedCountId = Shader.PropertyToID("_LiquidFeedCount");
        private static readonly int LiquidStepId = Shader.PropertyToID("_LiquidStep");
        private static readonly int LiquidFrameId = Shader.PropertyToID("_LiquidFrame");
        private static readonly int LiquidLifeId = Shader.PropertyToID("_LiquidLife");
        private static readonly int DragsId = Shader.PropertyToID("_LiquidDrags");
        private static readonly int DragSizesId = Shader.PropertyToID("_LiquidDragSizes");
        private static readonly int DragCountId = Shader.PropertyToID("_LiquidDragCount");
        private const int DragCapacity = 8;
        private static readonly int ChannelsId = Shader.PropertyToID("_LiquidChannels");
        private static readonly int ChannelFrameId = Shader.PropertyToID("_LiquidChannelFrame");
        private static readonly int ChannelDepthId = Shader.PropertyToID("_LiquidChannelDepth");

        private struct Feed
        {
            public Vector3 Point;
            public float Radius;
            public float Rate;
            public float Start;
            public float End;
        }

        private struct Puddle
        {
            public Vector3 Point;
            public float Radius;
            public float Start;
            public float End;
        }

        private readonly List<Feed> _feeds = new();
        private readonly List<Puddle> _puddles = new();
        private readonly Vector4[] _feedData = new Vector4[FeedCapacity];
        private readonly Vector4[] _feedHeights = new Vector4[FeedCapacity];
        private RenderTexture _liquid;
        private RenderTexture _liquidSpare;
        private int _liquidSlot = -1;
        private int _liquidResolution;
        private bool _liquidOn;
        private float _liquidLast = Dormant;
        private readonly Vector4[] _dragData = new Vector4[DragCapacity];
        private readonly Vector4[] _dragSizes = new Vector4[DragCapacity];
        private int _dragCount;
        private Texture _channels;
        private Vector4 _channelFrame;
        private float _channelDepth;

        public bool Liquid => _liquidOn;

        /// <summary>
        /// Reads how deep the liquid is within <paramref name="radius"/> of a point, as a share of a full film: about 1 inside a pool,
        /// near 0 where only a trace has run. The answer comes back a frame or two later through <paramref name="done"/>. Returns false,
        /// and never calls back, when Liquid is off, the point is off the liquid map or this GPU cannot read back.
        /// </summary>
        public bool ReadLiquid(Vector3 point, float radius, System.Action<float> done)
        {
            if (!_liquidOn || _liquid == null || done == null || !SystemInfo.supportsAsyncGPUReadback || !OnMap(point)) return false;

            int size = _liquid.width;
            float half = _mapSize * 0.5f;
            int x = Mathf.FloorToInt((point.x - _mapCentre.x + half) / _mapSize * size);
            int y = Mathf.FloorToInt((_mapCentre.z - point.z + half) / _mapSize * size);
            int r = Mathf.Clamp(Mathf.CeilToInt(radius / _mapSize * size), 0, 4);
            int x0 = Mathf.Clamp(x - r, 0, size - 1), x1 = Mathf.Clamp(x + r, 0, size - 1);
            int y0 = Mathf.Clamp(y - r, 0, size - 1), y1 = Mathf.Clamp(y + r, 0, size - 1);
            AsyncGPUReadback.Request(_liquid, 0, x0, x1 - x0 + 1, y0, y1 - y0 + 1, 0, 1, request =>
            {
                if (request.hasError) return;

                NativeArray<ushort> data = request.GetData<ushort>();
                float deepest = 0f;
                for (int i = 0; i + 3 < data.Length; i += 4) deepest = Mathf.Max(deepest, Mathf.HalfToFloat(data[i]));
                done(deepest / LiquidFilm);
            });
            return true;
        }

        /// <summary>
        /// Something moved through the liquid floor from <paramref name="from"/> to <paramref name="to"/>: the liquid under it is pushed
        /// along and wiped into streaks. Needs Liquid; does nothing when Liquid Drag is off in the settings. Keep <paramref name="seed"/>
        /// the same for one dragged object so its streaks stay in place from frame to frame.
        /// </summary>
        public void DragLiquid(Vector3 from, Vector3 to, float width, float seed = 0f)
        {
            if (!_liquidOn || Settings == null || !Settings.LiquidDrag || _dragCount >= DragCapacity) return;

            float half = _mapSize * 0.5f;
            Vector2 Uv(Vector3 p) => new((p.x - _mapCentre.x + half) / _mapSize, (_mapCentre.z - p.z + half) / _mapSize);
            Vector2 a = Uv(from), b = Uv(to);
            _dragData[_dragCount] = new Vector4(a.x, a.y, b.x, b.y);
            _dragSizes[_dragCount] = new Vector4(width * 0.5f / _mapSize, Settings.LiquidDragStrength, from.y, seed);
            _dragCount++;
        }
        public int LiquidPours { get; private set; }

        /// <summary>
        /// Cuts grooves into the liquid floor: white texels of <paramref name="mask"/> are channels that pools run into and flow along, like
        /// drains, gutters or letters cut into concrete. The mask lies flat on the floor, centred on <paramref name="centre"/> and
        /// <paramref name="size"/> metres across (x along world x, y along world z, top of the texture towards +z). Null removes them.
        /// </summary>
        public void Channels(Texture mask, Vector3 centre, Vector2 size, float depth)
        {
            _channels = mask;
            _channelFrame = new Vector4(centre.x - size.x * 0.5f, centre.z + size.y * 0.5f, Mathf.Max(0.001f, size.x), Mathf.Max(0.001f, size.y));
            _channelDepth = mask != null ? Mathf.Max(0f, depth) : 0f;
            if (_stainMaterial == null) return;

            _stainMaterial.SetTexture(ChannelsId, mask != null ? mask : Texture2D.blackTexture);
            _stainMaterial.SetVector(ChannelFrameId, _channelFrame);
            _stainMaterial.SetFloat(ChannelDepthId, _channelDepth);
        }

        private static bool WantsLiquid(BloodSettings settings)
        {
            return settings.Liquid && settings.Quality != BloodQuality.Low && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf);
        }

        private static int LiquidResolution(BloodSettings settings)
        {
            return Mathf.Min(Resolution(settings), LiquidResolutionMax);
        }

        private void BuildLiquid(BloodSettings settings)
        {
            _liquidResolution = LiquidResolution(settings);
            _liquid = LiquidTexture("Blood Liquid");
            _liquidSpare = LiquidTexture("Blood Liquid Spare");
            _stainMaterial.SetTexture(LiquidId, _liquid);
            _stainMaterial.SetFloat(LiquidFilmId, LiquidFilm);
            _stainMaterial.SetFloat(LiquidTexelId, 1f / _liquidResolution);
            ClearLiquid();
        }

        private RenderTexture LiquidTexture(string objectName)
        {
            var texture = new RenderTexture(_liquidResolution, _liquidResolution, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            {
                name = objectName,
                useMipMap = false,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.Create();
            return texture;
        }

        private void ClearLiquid()
        {
            _feeds.Clear();
            _puddles.Clear();
            if (_commands == null || _liquid == null) return;

            _commands.Clear();
            _commands.SetRenderTarget(_liquid);
            _commands.ClearRenderTarget(false, true, Color.clear);
            _commands.SetRenderTarget(_liquidSpare);
            _commands.ClearRenderTarget(false, true, Color.clear);
            Graphics.ExecuteCommandBuffer(_commands);
        }

        private bool Pour(Vector3 point, float radius, float litres, float seconds, float delay)
        {
            if (!_liquidOn || radius <= 0f || litres <= 0f || !OnMap(point)) return false;

            float now = Now;
            seconds = Mathf.Max(0.05f, seconds);
            float area = Mathf.PI * radius * radius / 3f;
            _feeds.Add(new Feed { Point = point, Radius = radius, Rate = litres / area / seconds, Start = now + delay, End = now + delay + seconds });
            float spread = Mathf.Sqrt(litres / (LiquidFilm * Mathf.PI)) * 0.85f;
            if (spread > radius * 0.6f) _puddles.Add(new Puddle { Point = point, Radius = spread, Start = now + delay, End = now + delay + _stainLifetime * _dryEnd });
            LiquidPours++;
            KeepStains(now + delay + seconds + _stainLifetime);
            return true;
        }

        private void PourBelow(Vector3 point, Vector3 normal, float reach, float halfWidth, float delay)
        {
            if (!_liquidOn) return;
            if (!Physics.Raycast(point + normal * 0.04f, Vector3.down, out RaycastHit floor, reach, _surfaces, QueryTriggerInteraction.Ignore) || floor.normal.y < 0.7f) return;

            float radius = Mathf.Max(0.04f, halfWidth * 1.2f);
            float litres = LiquidFilm * Mathf.PI * (halfWidth * 1.8f) * (halfWidth * 1.8f);
            Pour(floor.point + normal * radius * 0.6f, radius, litres, 2.5f, delay * Mathf.Clamp01(floor.distance / Mathf.Max(0.01f, reach)) + 0.3f);
        }

        private bool WetPuddle(Vector3 point, float margin, float now)
        {
            for (int i = 0; i < _puddles.Count; i++)
            {
                Puddle puddle = _puddles[i];
                if (now < puddle.Start || now > puddle.End) continue;

                float grown = Mathf.Clamp01((now - puddle.Start) / 3f);
                float reach = puddle.Radius * Mathf.Sqrt(grown) + margin;
                float dx = point.x - puddle.Point.x;
                float dz = point.z - puddle.Point.z;
                if (dx * dx + dz * dz <= reach * reach && Mathf.Abs(point.y - puddle.Point.y) < 1f) return true;
            }

            return false;
        }

        private void StepLiquid()
        {
            float now = Now;
            if (_liquidLast <= Dormant + 1f) _liquidLast = now;
            float elapsed = Mathf.Clamp(now - _liquidLast, 0f, 0.1f);
            _liquidLast = now;
            for (int i = _puddles.Count - 1; i >= 0; i--)
                if (now > _puddles[i].End) _puddles.RemoveAt(i);
            if (elapsed <= 0f || now > _stainsUntil) return;

            float half = _mapSize * 0.5f;
            int count = 0;
            for (int i = _feeds.Count - 1; i >= 0; i--)
            {
                Feed feed = _feeds[i];
                if (now > feed.End + 0.1f)
                {
                    _feeds.RemoveAt(i);
                    continue;
                }

                if (now < feed.Start || count >= FeedCapacity) continue;

                _feedData[count] = new Vector4((feed.Point.x - _mapCentre.x + half) / _mapSize, (_mapCentre.z - feed.Point.z + half) / _mapSize, feed.Radius / _mapSize, feed.Rate);
                _feedHeights[count] = new Vector4(feed.Point.y, 0f, 0f, 0f);
                count++;
            }

            for (int i = count; i < FeedCapacity; i++) _feedData[i] = Vector4.zero;
            int steps = Mathf.Clamp(Mathf.CeilToInt(elapsed * LiquidStepsPerSecond), 1, LiquidStepsMax);
            float step = elapsed / steps;
            float dryDelay = _stainLifetime * _dryStart;
            float evaporate = LiquidFilm * 1.6f / Mathf.Max(0.5f, _stainLifetime * (_dryEnd - _dryStart));
            _stamper.SetVectorArray(FeedsId, _feedData);
            _stamper.SetVectorArray(FeedHeightsId, _feedHeights);
            _stamper.SetFloat(FeedCountId, count);
            _stamper.SetVector(LiquidStepId, new Vector4(step, LiquidFilm, evaporate, now));
            _stamper.SetVector(LiquidFrameId, new Vector4(_mapCentre.x - half, _mapCentre.z + half, _mapSize, 1f / _liquidResolution));
            _stamper.SetVector(LiquidLifeId, new Vector4(dryDelay, _stainLifetime, LiquidResidueFade, 0f));
            _stamper.SetTexture(ChannelsId, _channels != null ? _channels : Texture2D.blackTexture);
            _stamper.SetVector(ChannelFrameId, _channelFrame);
            _stamper.SetFloat(ChannelDepthId, _channels != null ? _channelDepth * LiquidFilm : 0f);

            _commands.Clear();
            _commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.Ortho(0f, 1f, 0f, 1f, -1f, 1f));
            RenderTexture source = _liquid;
            RenderTexture target = _liquidSpare;
            Matrix4x4 full = Matrix4x4.TRS(new Vector3(0.5f, 0.5f, 0f), Quaternion.identity, new Vector3(0.5f, 0.5f, 1f));
            for (int i = 0; i < steps; i++)
            {
                _commands.SetRenderTarget(target);
                _commands.SetGlobalTexture(LiquidSourceId, source);
                _commands.DrawMesh(_quad, full, _stamper, 0, 2);
                (source, target) = (target, source);
            }

            // what was dragged through the liquid since the last step pushes it along, once per frame
            if (_dragCount > 0)
            {
                for (int i = _dragCount; i < DragCapacity; i++) _dragData[i] = Vector4.zero;
                _stamper.SetVectorArray(DragsId, _dragData);
                _stamper.SetVectorArray(DragSizesId, _dragSizes);
                _stamper.SetFloat(DragCountId, _dragCount);
                _commands.SetRenderTarget(target);
                _commands.SetGlobalTexture(LiquidSourceId, source);
                _commands.DrawMesh(_quad, full, _stamper, 0, 3);
                (source, target) = (target, source);
                _dragCount = 0;
            }

            Graphics.ExecuteCommandBuffer(_commands);
            _liquid = source;
            _liquidSpare = target;
            _stainMaterial.SetTexture(LiquidId, _liquid);
        }

        private void ShiftLiquid(Vector3 shift)
        {
            if (!_liquidOn) return;

            _commands.Clear();
            _commands.SetRenderTarget(_liquidSpare);
            _commands.ClearRenderTarget(false, true, Color.clear);
            _commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.Ortho(0f, 1f, 0f, 1f, -1f, 1f));
            _commands.SetGlobalTexture(MapSourceId, _liquid);
            _commands.SetGlobalVector(ShiftOffsetId, new Vector4(shift.x / _mapSize, -shift.z / _mapSize, 0f, 0f));
            _commands.DrawMesh(_quad, Matrix4x4.TRS(new Vector3(0.5f, 0.5f, 0f), Quaternion.identity, new Vector3(0.5f, 0.5f, 1f)), _stamper, 0, 1);
            Graphics.ExecuteCommandBuffer(_commands);
            (_liquid, _liquidSpare) = (_liquidSpare, _liquid);
            _stainMaterial.SetTexture(LiquidId, _liquid);
        }

        private void ReleaseLiquid()
        {
            if (_liquid != null) _liquid.Release();
            if (_liquidSpare != null) _liquidSpare.Release();
            Release(_liquid);
            Release(_liquidSpare);
        }
    }
}
