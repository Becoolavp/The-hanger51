using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rng.Blood
{
    [AddComponentMenu("RNG/Blood Wounds")]
    [DisallowMultipleComponent]
    public class BloodWounds : MonoBehaviour
    {
        public const int MaxWounds = 32;
        public const string DefaultMaterialResource = "BloodCoat";
        private const string CoatName = "Blood Coat";

        private static readonly int WoundsId = Shader.PropertyToID("_BloodWounds");
        private static readonly int DataId = Shader.PropertyToID("_BloodWoundData");
        private static readonly int DownId = Shader.PropertyToID("_BloodWoundDown");
        private static readonly int CountId = Shader.PropertyToID("_BloodWoundCount");
        private static readonly int WashId = Shader.PropertyToID("_BloodWoundWash");
        private static readonly int StableId = Shader.PropertyToID("_BloodCoatStable");
        private static readonly int ColourId = Shader.PropertyToID("_BloodCoatColor");
        private static readonly int DryId = Shader.PropertyToID("_BloodCoatDry");
        private static readonly int LookId = Shader.PropertyToID("_BloodCoatLook");

        [SerializeField, Tooltip("Material that draws blood on the body. Empty uses the Blood Coat material from Resources.")] private Material _material;
        [SerializeField, Range(0.2f, 3f), Tooltip("Scales the size of every wound.")] private float _size = 1f;
        [SerializeField, Min(0.1f), Tooltip("Seconds the blood takes to run down from a wound.")] private float _runSeconds = 4f;
        [SerializeField, Min(0f), Tooltip("Seconds a wound stays on the body. 0 uses the Stain Lifetime of the blood settings.")] private float _lifetime;
        [SerializeField, Tooltip("Add a wound whenever Blood.Spawn hits a bone of this character.")] private bool _fromHits = true;

        private struct Wound
        {
            public Transform Bone;
            public Vector3 Local;
            public Vector3 Down;
            public float Radius;
            public float Birth;
            public float Seed;
            public float Lifetime;
            public float Depth;
            public float Washed;
            // 0 a wound or a soak, 1 a stabbed blade, 2 a splat on a blunt weapon
            public float Shape;
        }

        // A coat draws over one renderer. Wounds are drawn in the coat's own rest space, so blood stays on the skin when the body moves.
        private class Coat
        {
            public Renderer Renderer;
            public Renderer Source;
            public Mesh Copy;
            public Transform[] Bones;
            public Matrix4x4[] Rest;
            public float Scale = 1f;
            public bool Stable;
            public MaterialPropertyBlock Block;
        }

        private readonly Wound[] _wounds = new Wound[MaxWounds];
        private readonly Vector4[] _points = new Vector4[MaxWounds];
        private readonly Vector4[] _data = new Vector4[MaxWounds];
        private readonly Vector4[] _downs = new Vector4[MaxWounds];
        private readonly Vector4[] _washes = new Vector4[MaxWounds];
        private readonly List<Coat> _coats = new();
        private int _count;
        private int _next;
        private bool _coated;

        public int Count => _count;

        private void OnEnable()
        {
            Blood.Hit += OnHit;
            Blood.Cleared += Clear;
        }

        private void OnDisable()
        {
            Blood.Hit -= OnHit;
            Blood.Cleared -= Clear;
            Show(false);
        }

        private void OnDestroy()
        {
            foreach (Coat coat in _coats)
            {
                if (coat.Renderer != null) Remove(coat.Renderer.gameObject);
                if (coat.Copy != null) Remove(coat.Copy);
            }

            _coats.Clear();
        }

        private static void Remove(Object target)
        {
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        private void OnHit(BloodHit hit)
        {
            if (!_fromHits || hit.Bone == null || !hit.Bone.IsChildOf(transform)) return;

            Add(hit.Position, hit.Kind, hit.Force, hit.Bone);
        }

        public void Add(Vector3 point, BloodHitKind kind = BloodHitKind.Bullet, float intensity = 1f, Transform bone = null)
        {
            BloodField field = Blood.Field;
            if (field == null || !field.Shown || intensity <= 0f) return;

            if (!_coated) Build();
            if (bone == null) bone = transform;
            // a round into a hole that is already there adds nothing the coat could show, so it does not take a slot that would
            // push the oldest wound off the body
            if (Covered(point, bone, field.Now)) return;

            float lifetime = _lifetime > 0f ? _lifetime : field.Settings.StainLifetime;
            _wounds[_next] = new Wound
            {
                Bone = bone,
                Local = bone.InverseTransformPoint(point),
                Down = bone.InverseTransformDirection(Vector3.down),
                Radius = Radius(kind) * _size * Mathf.Clamp(intensity, 0.4f, 2f),
                Birth = field.Now,
                Seed = Random.Range(1f, 100f),
                Lifetime = lifetime,
            };
            _next = (_next + 1) % MaxWounds;
            _count = Mathf.Min(_count + 1, MaxWounds);
            Show(true);
        }

        /// <summary>
        /// Blood soaked into the body where it touched a pool: a coat with no hole and no runs over the skin within
        /// <paramref name="radius"/> of <paramref name="point"/>, up to <paramref name="depth"/> above it (the side that lay in the blood).
        /// It dries and fades like a wound. Touching the same place again freshens the patch instead of taking a new slot.
        /// </summary>
        public void Soak(Vector3 point, float radius, float depth, Transform bone = null)
        {
            BloodField field = Blood.Field;
            if (field == null || !field.Shown || radius <= 0f) return;

            if (!_coated) Build();
            if (bone == null) bone = transform;
            float now = field.Now;
            depth = Mathf.Clamp(depth, 0.005f, radius * 2f);
            Vector3 local = bone.InverseTransformPoint(point);
            for (int i = 0; i < _count; i++)
            {
                int index = (_next - _count + i + MaxWounds) % MaxWounds;
                Wound patch = _wounds[index];
                if (patch.Depth <= 0f || patch.Shape != 0f || patch.Bone != bone || now - patch.Birth > patch.Lifetime) continue;
                if ((patch.Local - local).sqrMagnitude > patch.Radius * patch.Radius * 0.36f) continue;

                // a patch already freshly wet only grows to the deeper of the two; an older one is wet again
                patch.Depth = Mathf.Max(patch.Depth, depth);
                patch.Radius = Mathf.Max(patch.Radius, radius);
                if (now - patch.Birth > 1f) patch.Birth = now;
                patch.Washed = 0f;
                _wounds[index] = patch;
                return;
            }

            _wounds[_next] = new Wound
            {
                Bone = bone,
                Local = local,
                Down = bone.InverseTransformDirection(Vector3.down),
                Radius = radius,
                Birth = now,
                Seed = Random.Range(1f, 100f),
                Lifetime = _lifetime > 0f ? _lifetime : field.Settings.StainLifetime,
                Depth = depth,
            };
            _next = (_next + 1) % MaxWounds;
            _count = Mathf.Min(_count + 1, MaxWounds);
            Show(true);
        }

        /// <summary>
        /// Blood on a blade that went into a body: a coat from <paramref name="tip"/> back along the blade to <paramref name="entry"/>, where
        /// the body closed on it. The line there is a little ragged, the blood is thickest just under it, and further down the flesh wiped
        /// it into streaks on the way out. Every stab takes its own slot, so a shallow stab after a deep one freshens only the part that
        /// went in, and the older blood above it stays darker. Use it on a weapon; the Blood Blade of the Contact add-on calls it.
        /// </summary>
        public void Stab(Vector3 tip, Vector3 entry, Transform bone = null)
        {
            BloodField field = Blood.Field;
            Vector3 tipward = tip - entry;
            float depth = tipward.magnitude;
            if (field == null || !field.Shown || depth < 0.005f) return;

            if (!_coated) Build();
            if (bone == null) bone = transform;
            _wounds[_next] = new Wound
            {
                Bone = bone,
                Local = bone.InverseTransformPoint(tip),
                Down = bone.InverseTransformDirection(tipward / depth),
                // the coat reaches a little over two radii from its point, so the radius follows the depth
                Radius = Mathf.Max(0.03f, (depth + 0.06f) / 2.2f),
                Birth = field.Now,
                Seed = Random.Range(1f, 100f),
                Lifetime = _lifetime > 0f ? _lifetime : field.Settings.StainLifetime,
                Depth = depth,
                Shape = 1f,
            };
            _next = (_next + 1) % MaxWounds;
            _count = Mathf.Min(_count + 1, MaxWounds);
            Show(true);
        }

        /// <summary>
        /// Blood splashed on a blunt weapon where it hit: a splat of <paramref name="radius"/> at <paramref name="point"/> with specks around
        /// it, that runs the way down was when it hit and keeps that shape as the weapon swings. A hit on a place already splatted freshens
        /// it instead of taking a new slot. Use it on a weapon; the Blood Blade of the Contact add-on calls it.
        /// </summary>
        public void Splat(Vector3 point, float radius, Transform bone = null)
        {
            BloodField field = Blood.Field;
            if (field == null || !field.Shown || radius <= 0f) return;

            if (!_coated) Build();
            if (bone == null) bone = transform;
            float now = field.Now;
            Vector3 local = bone.InverseTransformPoint(point);
            for (int i = 0; i < _count; i++)
            {
                int index = (_next - _count + i + MaxWounds) % MaxWounds;
                Wound splat = _wounds[index];
                if (splat.Shape != 2f || splat.Bone != bone || now - splat.Birth > splat.Lifetime) continue;
                if ((splat.Local - local).sqrMagnitude > splat.Radius * splat.Radius * 0.25f) continue;

                splat.Radius = Mathf.Max(splat.Radius, radius);
                splat.Birth = now;
                splat.Washed = 0f;
                _wounds[index] = splat;
                return;
            }

            _wounds[_next] = new Wound
            {
                Bone = bone,
                Local = local,
                Down = bone.InverseTransformDirection(Vector3.down),
                Radius = radius,
                Birth = now,
                Seed = Random.Range(1f, 100f),
                Lifetime = _lifetime > 0f ? _lifetime : field.Settings.StainLifetime,
                Shape = 2f,
            };
            _next = (_next + 1) % MaxWounds;
            _count = Mathf.Min(_count + 1, MaxWounds);
            Show(true);
        }

        /// <summary>Washes blood off the body: <paramref name="amount"/> is the share taken off now, 1 washes it clean. It turns thin and pink as it goes.</summary>
        public void Wash(float amount)
        {
            if (amount <= 0f) return;

            for (int i = 0; i < _count; i++)
            {
                int index = (_next - _count + i + MaxWounds) % MaxWounds;
                Wound wound = _wounds[index];
                wound.Washed = Mathf.Min(1f, wound.Washed + amount);
                // a wound washed clean is gone, so its slot is free for the next hit
                if (wound.Washed >= 0.999f) wound.Lifetime = 0f;
                _wounds[index] = wound;
            }
        }

        private bool Covered(Vector3 point, Transform bone, float now)
        {
            for (int i = 0; i < _count; i++)
            {
                Wound wound = _wounds[(_next - _count + i + MaxWounds) % MaxWounds];
                if (wound.Depth > 0f || wound.Shape != 0f || wound.Bone != bone || now - wound.Birth > wound.Lifetime) continue;

                float reach = wound.Radius * 0.5f;
                if ((bone.TransformPoint(wound.Local) - point).sqrMagnitude < reach * reach) return true;
            }

            return false;
        }

        /// <summary>Builds the blood coats again, for example after the meshes of this character were swapped or cut.</summary>
        public void Rebuild()
        {
            OnDestroy();
            _coated = false;
            if (_count == 0) return;

            Build();
            Show(true);
        }

        public void Clear()
        {
            _count = 0;
            _next = 0;
            Show(false);
        }

        private void LateUpdate()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (_count == 0) return;

            BloodField field = Blood.Field;
            if (field == null || !field.Shown)
            {
                Clear();
                return;
            }

            float now = field.Now;
            BloodSettings settings = field.Settings;
            var dry = new Vector4(settings.DryStart, Mathf.Max(settings.DryStart + 0.001f, settings.DryEnd), settings.FadeStart, 0f);
            var look = new Vector4(settings.DryBrightness, settings.DryBrown, settings.DrySmoothness, 0f);
            bool any = false;
            foreach (Coat coat in _coats)
            {
                if (coat.Renderer == null) continue;

                int alive = 0;
                for (int i = 0; i < _count; i++)
                {
                    Wound wound = _wounds[(_next - _count + i + MaxWounds) % MaxWounds];
                    if (wound.Bone == null || now - wound.Birth > wound.Lifetime) continue;
                    if (!Place(coat, wound, out Vector3 point, out Vector3 down)) continue;

                    _points[alive] = new Vector4(point.x, point.y, point.z, wound.Radius);
                    _data[alive] = new Vector4(wound.Birth, wound.Seed, wound.Lifetime, _runSeconds);
                    _downs[alive] = new Vector4(down.x, down.y, down.z, wound.Depth);
                    _washes[alive] = new Vector4(wound.Washed, wound.Shape, 0f, 0f);
                    alive++;
                }

                any |= alive > 0;
                coat.Block ??= new MaterialPropertyBlock();
                coat.Block.SetVectorArray(WoundsId, _points);
                coat.Block.SetVectorArray(DataId, _data);
                coat.Block.SetVectorArray(DownId, _downs);
                coat.Block.SetVectorArray(WashId, _washes);
                coat.Block.SetFloat(CountId, alive);
                coat.Block.SetFloat(StableId, coat.Stable ? 1f : 0f);
                coat.Block.SetColor(ColourId, field.Colour);
                coat.Block.SetVector(DryId, dry);
                coat.Block.SetVector(LookId, look);
                coat.Renderer.SetPropertyBlock(coat.Block);
            }

            if (!any)
            {
                bool expired = true;
                for (int i = 0; i < _count; i++)
                {
                    Wound wound = _wounds[(_next - _count + i + MaxWounds) % MaxWounds];
                    if (wound.Bone != null && now - wound.Birth <= wound.Lifetime) expired = false;
                }

                if (expired) Clear();
            }
        }

        // Moves a wound into the rest space of a coat: bind space for skinned meshes, scaled object space for plain ones.
        private static bool Place(Coat coat, Wound wound, out Vector3 point, out Vector3 down)
        {
            if (!coat.Stable)
            {
                point = wound.Bone.TransformPoint(wound.Local);
                down = Vector3.down;
                return true;
            }

            if (coat.Bones != null)
            {
                int index = -1;
                for (Transform t = wound.Bone; t != null && index < 0; t = t.parent) index = System.Array.IndexOf(coat.Bones, t);
                if (index < 0 || index >= coat.Rest.Length)
                {
                    point = down = Vector3.zero;
                    return false;
                }

                Transform bone = coat.Bones[index];
                Vector3 local = bone.InverseTransformPoint(wound.Bone.TransformPoint(wound.Local));
                Vector3 localDown = bone.InverseTransformDirection(wound.Bone.TransformDirection(wound.Down));
                point = coat.Rest[index].MultiplyPoint3x4(local) * coat.Scale;
                down = coat.Rest[index].MultiplyVector(localDown).normalized;
                return true;
            }

            Transform frame = coat.Renderer.transform;
            Vector3 scale = frame.lossyScale;
            point = Vector3.Scale(frame.InverseTransformPoint(wound.Bone.TransformPoint(wound.Local)), scale);
            down = Vector3.Scale(frame.InverseTransformDirection(wound.Bone.TransformDirection(wound.Down)), scale).normalized;
            return true;
        }

        private void Show(bool shown)
        {
            foreach (Coat coat in _coats)
                if (coat.Renderer != null) coat.Renderer.enabled = shown;
        }

        private void Build()
        {
            _coated = true;
            Material material = _material != null ? _material : Resources.Load<Material>(DefaultMaterialResource);
            if (material == null) return;

            var lowerDetail = new HashSet<Renderer>();
            foreach (LODGroup group in GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] levels = group.GetLODs();
                for (int level = 1; level < levels.Length; level++)
                    foreach (Renderer member in levels[level].renderers)
                        if (member != null) lowerDetail.Add(member);
            }

            foreach (Renderer source in GetComponentsInChildren<Renderer>(true))
            {
                if (source.name == CoatName || lowerDetail.Contains(source)) continue;

                Coat coat = source switch
                {
                    SkinnedMeshRenderer skinned => Skin(skinned),
                    MeshRenderer plain => Plain(plain),
                    _ => null
                };
                if (coat == null) continue;

                int slots = coat.Copy != null ? coat.Copy.subMeshCount : source switch
                {
                    SkinnedMeshRenderer skinned => skinned.sharedMesh.subMeshCount,
                    _ => source.GetComponent<MeshFilter>().sharedMesh.subMeshCount
                };
                var materials = new Material[slots];
                for (int i = 0; i < slots; i++) materials[i] = material;
                coat.Renderer.sharedMaterials = materials;
                coat.Renderer.shadowCastingMode = ShadowCastingMode.Off;
                coat.Renderer.lightProbeUsage = source.lightProbeUsage;
                coat.Renderer.reflectionProbeUsage = source.reflectionProbeUsage;
                coat.Renderer.enabled = false;
                _coats.Add(coat);
            }
        }

        // A copy of the mesh that carries its rest position and normal in uv3 and uv4, so the shader can place wounds on the skin.
        private static Mesh RestCopy(Mesh mesh, float scale, Vector3 axes)
        {
            if (mesh == null || !mesh.isReadable) return null;

            Mesh copy = Instantiate(mesh);
            copy.name = mesh.name + " Coat";
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            mesh.GetVertices(positions);
            mesh.GetNormals(normals);
            for (int i = 0; i < positions.Count; i++)
            {
                positions[i] = Vector3.Scale(positions[i], axes) * scale;
                if (i < normals.Count)
                {
                    Vector3 n = normals[i];
                    normals[i] = new Vector3(n.x / axes.x, n.y / axes.y, n.z / axes.z).normalized;
                }
            }

            while (normals.Count < positions.Count) normals.Add(Vector3.up);
            copy.SetUVs(3, positions);
            copy.SetUVs(4, normals);
            return copy;
        }

        private static Coat Skin(SkinnedMeshRenderer source)
        {
            if (source.sharedMesh == null) return null;

            var holder = new GameObject(CoatName);
            holder.transform.SetParent(source.transform, false);
            var renderer = holder.AddComponent<SkinnedMeshRenderer>();
            Transform[] bones = source.bones;
            Matrix4x4[] bind = source.sharedMesh.bindposes;
            // rest space keeps the units of the mesh; scale it to metres with the first bone's skin matrix
            float scale = 1f;
            if (bones.Length > 0 && bind.Length > 0 && bones[0] != null)
            {
                Vector3 s = (bones[0].localToWorldMatrix * bind[0]).lossyScale;
                scale = Mathf.Abs(s.x) > 1e-6f ? Mathf.Abs(s.x) : 1f;
            }

            Mesh copy = RestCopy(source.sharedMesh, scale, Vector3.one);
            renderer.sharedMesh = copy != null ? copy : source.sharedMesh;
            renderer.bones = bones;
            renderer.rootBone = source.rootBone;
            renderer.localBounds = source.localBounds;
            renderer.updateWhenOffscreen = source.updateWhenOffscreen;
            renderer.quality = source.quality;
            var rest = new Matrix4x4[bind.Length];
            for (int i = 0; i < bind.Length; i++) rest[i] = bind[i].inverse;
            return new Coat { Renderer = renderer, Source = source, Copy = copy, Bones = bones, Rest = rest, Scale = scale, Stable = copy != null };
        }

        private static Coat Plain(MeshRenderer source)
        {
            var filter = source.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return null;

            var holder = new GameObject(CoatName);
            holder.transform.SetParent(source.transform, false);
            Mesh copy = RestCopy(filter.sharedMesh, 1f, source.transform.lossyScale);
            holder.AddComponent<MeshFilter>().sharedMesh = copy != null ? copy : filter.sharedMesh;
            var renderer = holder.AddComponent<MeshRenderer>();
            return new Coat { Renderer = renderer, Source = source, Copy = copy, Stable = copy != null };
        }

        private static float Radius(BloodHitKind kind)
        {
            return kind switch
            {
                BloodHitKind.Explosion => 0.16f,
                BloodHitKind.Shotgun => 0.1f,
                BloodHitKind.Headshot => 0.075f,
                BloodHitKind.BulletClose => 0.065f,
                BloodHitKind.Blunt => 0.07f,
                BloodHitKind.Sharp => 0.06f,
                _ => 0.05f
            };
        }
    }
}
