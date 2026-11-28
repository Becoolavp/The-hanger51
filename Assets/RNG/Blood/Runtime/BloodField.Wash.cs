using System.Collections.Generic;
using UnityEngine;

namespace Rng.Blood
{
    public partial class BloodField
    {
        private const int WashCapacity = 16;
        private static readonly int WashAId = Shader.PropertyToID("_BloodWashA");
        private static readonly int WashBId = Shader.PropertyToID("_BloodWashB");
        private static readonly int WashCId = Shader.PropertyToID("_BloodWashC");
        private static readonly int WashCountId = Shader.PropertyToID("_BloodWashCount");
        private static readonly int DrainAId = Shader.PropertyToID("_BloodDrainA");
        private static readonly int DrainBId = Shader.PropertyToID("_BloodDrainB");
        private static readonly int DrainCountId = Shader.PropertyToID("_BloodDrainCount");

        // A washed area: a capsule from A to B. Amount climbs to 1 as water runs over it; blood laid before it started fades and turns
        // pink, blood laid while it runs fades from the time it landed.
        private struct WashZone
        {
            public Vector3 A;
            public Vector3 B;
            public float Radius;
            public float Start;
            public float Last;
            public float Amount;
            public float Drained;
        }

        private readonly List<WashZone> _washes = new();
        private readonly Vector4[] _washA = new Vector4[WashCapacity];
        private readonly Vector4[] _washB = new Vector4[WashCapacity];
        private readonly Vector4[] _washC = new Vector4[WashCapacity];
        private readonly Vector4[] _drainA = new Vector4[WashCapacity];
        private readonly Vector4[] _drainB = new Vector4[WashCapacity];
        private bool _washesShown;
        private readonly List<Vector4> _presses = new();

        /// <summary>Areas being washed right now.</summary>
        public int WashesActive => _washes.Count;

        /// <summary>
        /// Washes blood off the floor and walls in a capsule from <paramref name="from"/> to <paramref name="to"/>: rain, a hose or a mop.
        /// Call it every frame water runs, with <paramref name="amount"/> the share washed off in that frame (for example
        /// <c>Time.deltaTime / seconds</c>). Blood turns pink and thin as it goes, liquid on the floor drains, and once an area is clean its
        /// stains are gone for good. Nothing changes until it is called.
        /// </summary>
        public void Wash(Vector3 from, Vector3 to, float radius, float amount)
        {
            if (!Shown || radius <= 0f || amount <= 0f) return;

            float now = Now;
            // more water on an area already being washed adds to it; a mop moving on stretches its last stroke a little
            for (int i = 0; i < _washes.Count; i++)
            {
                WashZone zone = _washes[i];
                bool same = (zone.A - from).sqrMagnitude < radius * radius * 0.25f && (zone.B - to).sqrMagnitude < radius * radius * 0.25f && Mathf.Abs(zone.Radius - radius) < radius * 0.5f;
                bool onward = !same && (zone.B - from).sqrMagnitude < radius * radius * 0.25f && Mathf.Abs(zone.Radius - radius) < radius * 0.5f
                              && Vector3.Distance(zone.A, to) < Mathf.Max(1.5f, radius * 3f) && now - zone.Last < 0.5f;
                if (!same && !onward) continue;

                if (onward) zone.B = to;
                zone.Amount = Mathf.Min(1f, zone.Amount + amount);
                zone.Last = now;
                _washes[i] = zone;
                return;
            }

            if (_washes.Count >= WashCapacity) Settle(0);
            _washes.Add(new WashZone { A = from, B = to, Radius = radius, Start = now, Last = now, Amount = Mathf.Min(1f, amount) });
        }

        /// <summary>
        /// Presses liquid on the floor away under a capsule from <paramref name="from"/> to <paramref name="to"/>, keeping
        /// <paramref name="keep"/> of it: the outline a body leaves when it is lifted out of a pool. Needs Liquid.
        /// </summary>
        public void Press(Vector3 from, Vector3 to, float radius, float keep = 0.3f)
        {
            if (!_liquidOn || radius <= 0f || _presses.Count >= WashCapacity * 2) return;

            float half = _mapSize * 0.5f;
            _presses.Add(new Vector4((from.x - _mapCentre.x + half) / _mapSize, (_mapCentre.z - from.z + half) / _mapSize,
                (to.x - _mapCentre.x + half) / _mapSize, (_mapCentre.z - to.z + half) / _mapSize));
            _presses.Add(new Vector4(radius / _mapSize, Mathf.Clamp01(keep), 0f, 0f));
        }

        /// <summary>Washes a round area. See the capsule form.</summary>
        public void Wash(Vector3 centre, float radius, float amount)
        {
            Wash(centre, centre, radius, amount);
        }

        private void StepWash()
        {
            if (_washes.Count == 0 && _presses.Count == 0)
            {
                if (_washesShown)
                {
                    Shader.SetGlobalFloat(WashCountId, 0f);
                    _washesShown = false;
                }

                return;
            }

            float now = Now;
            // an area washed clean that no water reaches any more is settled: its stains are removed and it frees its slot
            for (int i = _washes.Count - 1; i >= 0; i--)
                if (_washes[i].Amount >= 0.999f && now - _washes[i].Last > 0.5f) Settle(i);

            Drain();
            for (int i = 0; i < WashCapacity; i++)
            {
                if (i < _washes.Count)
                {
                    WashZone zone = _washes[i];
                    _washA[i] = new Vector4(zone.A.x, zone.A.y, zone.A.z, zone.Radius);
                    _washB[i] = new Vector4(zone.B.x, zone.B.y, zone.B.z, zone.Start);
                    _washC[i] = new Vector4(zone.Amount, zone.Last, 0f, 0f);
                }
                else
                {
                    _washA[i] = _washB[i] = _washC[i] = Vector4.zero;
                }
            }

            Shader.SetGlobalVectorArray(WashAId, _washA);
            Shader.SetGlobalVectorArray(WashBId, _washB);
            Shader.SetGlobalVectorArray(WashCId, _washC);
            Shader.SetGlobalFloat(WashCountId, _washes.Count);
            _washesShown = true;
        }

        // liquid on the floor and the Massive map lose what the water took since the last frame
        private void Drain()
        {
            int count = 0;
            for (int i = 0; i + 1 < _presses.Count && count < WashCapacity; i += 2)
            {
                _drainA[count] = _presses[i];
                _drainB[count] = _presses[i + 1];
                count++;
            }

            _presses.Clear();
            for (int i = 0; i < _washes.Count && count < WashCapacity; i++)
            {
                WashZone zone = _washes[i];
                if (zone.Amount <= zone.Drained + 0.0001f) continue;

                // keep (1 - amount) of what was there before the water started: take the ratio since the last drain
                float keep = zone.Amount >= 0.999f ? 0f : (1f - zone.Amount) / Mathf.Max(0.0001f, 1f - zone.Drained);
                zone.Drained = zone.Amount;
                _washes[i] = zone;
                float half = _mapSize * 0.5f;
                _drainA[count] = new Vector4((zone.A.x - _mapCentre.x + half) / _mapSize, (_mapCentre.z - zone.A.z + half) / _mapSize,
                    (zone.B.x - _mapCentre.x + half) / _mapSize, (_mapCentre.z - zone.B.z + half) / _mapSize);
                _drainB[count] = new Vector4(zone.Radius / _mapSize, keep, 0f, 0f);
                count++;
            }

            if (count == 0 || _commands == null || (!_liquidOn && !_massive)) return;

            for (int i = count; i < WashCapacity; i++) _drainA[i] = _drainB[i] = Vector4.zero;
            _stamper.SetVectorArray(DrainAId, _drainA);
            _stamper.SetVectorArray(DrainBId, _drainB);
            _stamper.SetFloat(DrainCountId, count);
            _commands.Clear();
            _commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.Ortho(0f, 1f, 0f, 1f, -1f, 1f));
            Matrix4x4 full = Matrix4x4.TRS(new Vector3(0.5f, 0.5f, 0f), Quaternion.identity, new Vector3(0.5f, 0.5f, 1f));
            if (_liquidOn && _liquid != null)
            {
                _commands.SetRenderTarget(_liquidSpare);
                _commands.SetGlobalTexture(LiquidSourceId, _liquid);
                _commands.DrawMesh(_quad, full, _stamper, 0, 4);
            }

            if (_massive && _map != null)
            {
                _commands.SetRenderTarget(_mapSpare);
                _commands.SetGlobalTexture(LiquidSourceId, _map);
                _commands.DrawMesh(_quad, full, _stamper, 0, 4);
            }

            Graphics.ExecuteCommandBuffer(_commands);
            if (_liquidOn && _liquid != null)
            {
                (_liquid, _liquidSpare) = (_liquidSpare, _liquid);
                _stainMaterial.SetTexture(LiquidId, _liquid);
            }

            if (_massive && _map != null)
            {
                (_map, _mapSpare) = (_mapSpare, _map);
                _stainMaterial.SetTexture(MapId, _map);
            }
        }

        // removes for good what an area's water washed off and frees the area
        private void Settle(int index)
        {
            WashZone zone = _washes[index];
            _washes.RemoveAt(index);
            if (zone.Amount < 0.5f || !_stainVertices.IsCreated) return;

            float now = Now;
            float reach = zone.Radius * 0.9f;
            int slots = _stainVertices.Length / BoxCorners;
            for (int slot = 0; slot < slots; slot++)
            {
                int start = slot * BoxCorners;
                StainVertex first = _stainVertices[start];
                Vector4 life = first.Life;
                if (life.z > 2.5f || life.x > zone.Last || now > life.x + life.y) continue;

                Vector3 centre = first.Centre;
                if (Along(centre, zone.A, zone.B) > reach) continue;

                life.y = Mathf.Max(0f, now - life.x);
                for (int corner = 0; corner < BoxCorners; corner++)
                {
                    StainVertex vertex = _stainVertices[start + corner];
                    vertex.Life = life;
                    _stainVertices[start + corner] = vertex;
                }

                _stainMesh.SetVertexBufferData(_stainVertices, start, start, BoxCorners, 0, Quiet);
            }

            for (int i = _pools.Count - 1; i >= 0; i--)
                if (Along(_pools[i].Point, zone.A, zone.B) < reach) _pools.RemoveAt(i);
            for (int i = _puddles.Count - 1; i >= 0; i--)
                if (Along(_puddles[i].Point, zone.A, zone.B) < reach) _puddles.RemoveAt(i);
        }

        private static float Along(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(point, a + ab * t);
        }

    }
}
