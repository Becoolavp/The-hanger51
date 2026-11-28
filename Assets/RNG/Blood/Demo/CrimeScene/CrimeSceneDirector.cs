using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

namespace Rng.Blood.Demo
{
    public class CrimeSceneDirector : MonoBehaviour
    {
        public const float AgedSeconds = 60f;

        private const float Step = 1f / 30f;

        [Serializable]
        public struct View
        {
            public string Name;
            public Vector3 Position;
            public Vector3 Target;
            public Vector3 Focus;
            [Range(10f, 90f)] public float FieldOfView;
            [Range(1f, 32f)] public float Aperture;
            [Range(10f, 300f)] public float FocalLength;
        }

        private struct Cue
        {
            public float Time;
            public Action Act;
        }

        [SerializeField, Tooltip("Blood settings for this scene. Stains live longer here so old blood has time to dry.")] private BloodSettings _settings;
        [SerializeField] private Camera _camera;
        [SerializeField] private Volume _volume;
        [SerializeField] private View[] _views;
        [SerializeField] private int _seed = 61;

        private readonly List<Cue> _cues = new();
        private float _clock;
        private float _playStart;
        private int _next;
        private int _view;
        private bool _manual;
        private bool _help = true;
        private GUIStyle _style;

        public int ViewCount => _views != null ? _views.Length : 0;
        public float Clock => _clock;

        public string ViewName(int index)
        {
            return _views != null && index >= 0 && index < _views.Length ? _views[index].Name : "";
        }

        private void Start()
        {
            Begin();
            Advance(AgedSeconds);
            _playStart = Time.time;
            ShowView(0);
        }

        private void Update()
        {
            if (_manual) return;

            Controls();
            Advance(AgedSeconds + Time.time - _playStart);
        }

        public void Simulate(float time)
        {
            _manual = true;
            Advance(time);
        }

        public void Begin()
        {
            Blood.Initialize(_settings);
            Random.InitState(_seed);
            _cues.Clear();
            Story();
            _cues.Sort((a, b) => a.Time.CompareTo(b.Time));
            _next = 0;
            _clock = 0f;
            Blood.Tick(0f);
        }

        public void Advance(float time)
        {
            while (_clock < time - 0.00001f)
            {
                _clock = Mathf.Min(time, _clock + Step);
                Blood.Tick(_clock);
                while (_next < _cues.Count && _cues[_next].Time <= _clock) _cues[_next++].Act();
            }
        }

        public void ShowView(int index)
        {
            if (_views == null || _views.Length == 0 || _camera == null) return;

            _view = (index % _views.Length + _views.Length) % _views.Length;
            View view = _views[_view];
            _camera.transform.SetPositionAndRotation(view.Position, Quaternion.LookRotation(view.Target - view.Position));
            _camera.fieldOfView = view.FieldOfView;
            if (_volume == null || !_volume.profile.TryGet(out DepthOfField depth)) return;

            depth.focusDistance.Override(Vector3.Distance(view.Position, view.Focus));
            depth.aperture.Override(view.Aperture);
            depth.focalLength.Override(view.FocalLength);
        }

        public void Strike(Vector3 point, Vector3 direction, BloodHitKind kind)
        {
            Blood.Spawn(point, -direction.normalized, direction.normalized, 1f, kind);
        }

        private void Story()
        {
            const float fresh = AgedSeconds;
            Hit(0.2f, new Vector3(-1.1f, 1.45f, 1.7f), new Vector3(-0.1f, 0f, 1f), BloodHitKind.BulletClose);
            Hit(0.4f, new Vector3(-1.3f, 1.1f, 0.5f), new Vector3(0.1f, -0.3f, 1f), BloodHitKind.Shotgun);
            Add(0.5f, () => Blood.Pool(new Vector3(-1.4f, 0f, 1.0f), Vector3.up, 0.9f, 2f, 150f));
            Spray(fresh + 0.2f, new Vector3(0f, 1.1f, -0.3f), new Vector3(-0.03f, 0.04f, 1f), 0.8f, 4.2f, 0.004f, 0.022f);
            Spray(fresh + 0.22f, new Vector3(0.02f, 1.1f, -0.3f), new Vector3(0.02f, 0.07f, 1f), 0.9f, 4.6f, 0.003f, 0.016f);
            Hit(fresh + 0.2f, new Vector3(0f, 1.1f, -0.3f), new Vector3(0f, -0.05f, 1f), BloodHitKind.Sharp);
            Hit(fresh + 0.3f, new Vector3(-0.1f, 1.4f, 1.7f), new Vector3(-0.05f, 0f, 1f), BloodHitKind.BulletClose);
            Hit(fresh + 0.45f, new Vector3(0.25f, 1.55f, 1.9f), new Vector3(0.1f, 0.05f, 1f), BloodHitKind.Headshot);
            Add(fresh + 0.6f, () => Blood.Pool(new Vector3(0.55f, 0f, 1.15f), Vector3.up, 1.1f, 3.8f, 90f));
        }

        private void Hit(float time, Vector3 point, Vector3 direction, BloodHitKind kind)
        {
            Add(time, () => Strike(point, direction, kind));
        }

        private void Spray(float time, Vector3 point, Vector3 direction, float speedMin, float speedMax, float sizeMin, float sizeMax)
        {
            var burst = new BloodBurst(40, 52, 4f, speedMin, speedMax, sizeMin, sizeMax, 9f, 0f, BloodStainStyle.Rays, BloodStainStyle.Streak, 0.1f, 6f);
            Add(time, () => Blood.Emit(burst, point, direction.normalized));
        }

        private void Add(float time, Action act)
        {
            _cues.Add(new Cue { Time = time, Act = act });
        }

        private void Controls()
        {
            for (int i = 0; i < ViewCount && i < 9; i++)
                if (DemoInput.Digit(i + 1)) ShowView(i);

            if (DemoInput.Letter('h')) _help = !_help;
            if (DemoInput.Letter('r'))
            {
                Begin();
                Advance(AgedSeconds);
                _playStart = Time.time;
            }

            bool left = DemoInput.Click(0);
            bool right = DemoInput.Click(1);
            if ((!left && !right) || _camera == null) return;

            Ray ray = _camera.ScreenPointToRay(DemoInput.Pointer());
            if (!Physics.Raycast(ray, out RaycastHit hit, 50f, _settings != null ? _settings.Surfaces.value : 1, QueryTriggerInteraction.Ignore)) return;

            Vector3 direction = (-hit.normal + ray.direction * 0.6f).normalized;
            Vector3 origin = hit.point + hit.normal * 0.7f + Vector3.up * (hit.normal.y > 0.5f ? 0.5f : 0f);
            Strike(origin, (hit.point - origin).normalized + direction * 0.2f, left ? BloodHitKind.BulletClose : BloodHitKind.Shotgun);
        }

        private void OnGUI()
        {
            if (_manual || !_help) return;

            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true };
            string name = _views != null && _views.Length > 0 ? _views[_view].Name : "";
            GUI.Box(new Rect(10f, 10f, 560f, 64f), GUIContent.none);
            GUI.Label(new Rect(20f, 14f, 550f, 24f), $"1-{ViewCount} camera ({name})   R replay   H hide this", _style);
            GUI.Label(new Rect(20f, 40f, 550f, 24f), "Left click a wall or the floor: hit   Right click: shotgun", _style);
        }
    }
}
