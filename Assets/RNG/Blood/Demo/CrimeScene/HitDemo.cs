using System.Collections;
using UnityEngine;

namespace Rng.Blood.Demo
{
    public class HitDemo : MonoBehaviour
    {
        private const float AutoEvery = 0.32f;
        private const float FlashSeconds = 0.07f;
        private const float ShakeSeconds = 0.14f;

        private static readonly (string Name, BloodHitKind Kind)[] Weapons =
        {
            ("Pistol", BloodHitKind.Bullet),
            ("Point blank", BloodHitKind.BulletClose),
            ("Shotgun", BloodHitKind.Shotgun),
            ("Headshot", BloodHitKind.Headshot),
            ("Knife", BloodHitKind.Sharp),
            ("Bat", BloodHitKind.Blunt),
            ("Explosion", BloodHitKind.Explosion),
        };

        private static readonly (string Name, Color Colour)[] Colours =
        {
            ("Crimson", new Color(0.42f, 0.018f, 0.015f)),
            ("Alien", new Color(0.22f, 0.42f, 0.04f)),
            ("Coolant", new Color(0.05f, 0.16f, 0.42f)),
            ("Oil", new Color(0.035f, 0.032f, 0.03f)),
            ("Venom", new Color(0.27f, 0.05f, 0.34f)),
            ("Toxic", new Color(0.35f, 0.9f, 0.1f)),
        };

        [SerializeField] private CrimeSceneDirector _director;
        [SerializeField] private CrashDummy _dummy;
        [SerializeField] private Light _flash;

        private Camera _camera;
        private BloodTuner _tuner;
        private Vector3 _cameraPosition;
        private float _start;
        private float _nextAuto;
        private float _flashUntil;
        private float _shakeUntil;
        private int _weapon = 1;
        private int _view;
        private bool _random = true;
        private bool _auto;
        private bool _slow;
        private int _colour;
        private int _hits;
        private float _frameTime = 1f / 60f;
        private GUIStyle _title;
        private GUIStyle _subtitle;
        private GUIStyle _button;
        private GUIStyle _selected;
        private GUIStyle _hit;
        private GUIStyle _small;
        private GUIStyle _center;
        private Texture2D _panel;
        private Texture2D _outline;
        private Texture2D _frame;
        private Texture2D[] _swatches;

        private void Start()
        {
            _camera = Camera.main;
            _tuner = gameObject.AddComponent<BloodTuner>();
            if (_dummy != null && !_dummy.TryGetComponent(out BloodWounds _)) _dummy.gameObject.AddComponent<BloodWounds>();
            _director.Begin();
            _director.Advance(CrimeSceneDirector.AgedSeconds);
            _start = Time.time;
            ShowView(0);
            if (_flash != null) _flash.intensity = 0f;
        }

        private void Update()
        {
            _frameTime = Mathf.Lerp(_frameTime, Time.unscaledDeltaTime, 0.05f);
            _director.Advance(CrimeSceneDirector.AgedSeconds + Time.time - _start);
            if (DemoInput.Space()) Hit();
            if (BloodTuner.Available && DemoInput.Tab()) _tuner.Toggle();
            if (_auto && Time.time >= _nextAuto)
            {
                _nextAuto = Time.time + AutoEvery;
                Hit();
            }

            if (_flash != null) _flash.intensity = Time.unscaledTime < _flashUntil ? 2.5f : 0f;
            if (_camera == null) return;

            float shake = Mathf.Clamp01((_shakeUntil - Time.unscaledTime) / ShakeSeconds);
            float seed = Time.unscaledTime * 80f;
            _camera.transform.position = _cameraPosition + new Vector3(Mathf.PerlinNoise(seed, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, seed) - 0.5f, 0f) * (0.05f * shake * shake);
        }

        public void Hit()
        {
            if (_dummy == null) return;

            BloodHitKind kind = _random ? Weapons[Random.Range(0, Weapons.Length)].Kind : Weapons[_weapon].Kind;
            Vector3 back = -_dummy.transform.forward;
            float yaw = Random.Range(-55f, 55f);
            float pitch = Random.Range(-16f, 10f);
            Vector3 direction = Quaternion.AngleAxis(yaw, Vector3.up) * (Quaternion.AngleAxis(pitch, Vector3.Cross(Vector3.up, back)) * back);
            Transform bone = _dummy.Take(kind, direction);
            _hits++;

            if (_flash != null && bone != null)
            {
                _flash.transform.position = bone.position + direction * 0.3f;
                _flashUntil = Time.unscaledTime + FlashSeconds;
            }

            _shakeUntil = Time.unscaledTime + ShakeSeconds;
        }

        private void ShowView(int index)
        {
            _view = index;
            _director.ShowView(index);
            if (_camera != null) _cameraPosition = _camera.transform.position;
        }

        private void OnGUI()
        {
            Styles();
            float scale = Mathf.Max(0.6f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            if (!_tuner.Open)
            {
                GUI.Label(new Rect(40f, 28f, 900f, 80f), "RNG BLOOD", _title);
                GUI.Label(new Rect(44f, 100f, 900f, 40f), "Procedural GPU blood for Unity URP. Every hit comes from a new angle.", _subtitle);
                if (BloodTuner.Available && GUI.Button(new Rect(30f, height - 530f, 260f, 48f), "Blood settings  (Tab)", _button)) _tuner.Toggle();

                GUI.DrawTexture(new Rect(30f, height - 470f, 260f, 440f), _panel);
                GUI.Label(new Rect(50f, height - 455f, 220f, 30f), "WEAPON", _small);
                if (GUI.Button(new Rect(50f, height - 420f, 220f, 40f), "Random", _random ? _selected : _button)) _random = true;
                for (int i = 0; i < Weapons.Length; i++)
                {
                    bool on = !_random && _weapon == i;
                    if (GUI.Button(new Rect(50f, height - 372f + i * 48f, 220f, 40f), Weapons[i].Name, on ? _selected : _button))
                    {
                        _weapon = i;
                        _random = false;
                    }
                }
            }

            if (GUI.Button(new Rect(width * 0.5f - 160f, height - 160f, 320f, 110f), "HIT", _hit)) Hit();
            GUI.Label(new Rect(width * 0.5f - 160f, height - 40f, 320f, 30f), "or press Space", _center);

            GUI.DrawTexture(new Rect(width - 300f, 30f, 270f, 640f), _panel);
            GUI.Label(new Rect(width - 280f, 45f, 230f, 30f), "CAMERA", _small);
            int views = Mathf.Min(_director.ViewCount, 6);
            for (int i = 0; i < views; i++)
                if (GUI.Button(new Rect(width - 280f, 80f + i * 48f, 230f, 40f), _director.ViewName(i), _view == i ? _selected : _button)) ShowView(i);

            GUI.Label(new Rect(width - 280f, 380f, 230f, 30f), "TIME AND LOOK", _small);
            if (GUI.Button(new Rect(width - 280f, 412f, 230f, 40f), "Slow motion", _slow ? _selected : _button))
            {
                _slow = !_slow;
                Time.timeScale = _slow ? 0.2f : 1f;
            }

            if (GUI.Button(new Rect(width - 280f, 460f, 230f, 40f), "Auto fire", _auto ? _selected : _button)) _auto = !_auto;
            if (GUI.Button(new Rect(width - 280f, 508f, 112f, 40f), "Smear", _button)) StartCoroutine(Smear());
            if (GUI.Button(new Rect(width - 162f, 508f, 112f, 40f), "Clear", _button)) Blood.Clear();

            GUI.Label(new Rect(width - 280f, 560f, 230f, 30f), $"COLOUR   {Colours[_colour].Name}", _small);
            for (int i = 0; i < Colours.Length; i++)
            {
                var swatch = new Rect(width - 280f + i * 39.2f, 594f, 34f, 34f);
                GUI.DrawTexture(new Rect(swatch.x - 3f, swatch.y - 3f, swatch.width + 6f, swatch.height + 6f), i == _colour ? _outline : _frame);
                GUI.DrawTexture(swatch, _swatches[i]);
                if (GUI.Button(swatch, GUIContent.none, GUIStyle.none)) Recolour(i);
            }
            int stains = Blood.Field != null ? Blood.Field.StainsPlaced : 0;
            GUI.Label(new Rect(width - 420f, height - 40f, 390f, 30f), $"{_hits} hits   {stains} stains   {1f / Mathf.Max(0.0001f, _frameTime):0} fps", _small);
        }

        private void Recolour(int index)
        {
            _colour = index;
            BloodSettings settings = Blood.Settings;
            if (settings == null) return;

            Color colour = index == 0 ? settings.Colour : Colours[index].Colour;
            Blood.Recolour(colour, settings.AlternateColour);
            Blood.Show(true, index == Colours.Length - 1);
        }

        private IEnumerator Smear()
        {
            if (_dummy == null) yield break;

            Vector3 feet = _dummy.transform.position;
            Vector3 away = Quaternion.AngleAxis(Random.Range(-70f, 70f), Vector3.up) * _dummy.transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, away);
            Blood.Pool(feet, Vector3.up, 0.45f, 0.4f, Blood.Settings != null ? Blood.Settings.StainLifetime : 30f);
            var drag = new GameObject("Drag");
            var smear = drag.AddComponent<BloodSmear>();
            smear.Width = 0.45f;
            smear.Soak();
            float bend = Random.Range(-0.6f, 0.6f);
            for (float t = 0f; t <= 1f; t += Time.unscaledDeltaTime / 1.4f)
            {
                drag.transform.position = feet + away * (0.2f + 2.4f * t) + side * (bend * t * t) + Vector3.up * 0.1f;
                yield return null;
            }

            Destroy(drag);
            if (!Physics.Raycast(feet + Vector3.up * 1.3f, _dummy.transform.forward * -1f, out RaycastHit wall, 6f) && !Physics.Raycast(feet + Vector3.up * 1.3f, _dummy.transform.forward, out wall, 6f)) yield break;

            Vector3 down = Vector3.ProjectOnPlane(Vector3.down, wall.normal).normalized;
            Vector3 across = Vector3.Cross(wall.normal, down).normalized;
            Vector3 start = wall.point + across * Random.Range(-0.6f, 0.6f);
            float slant = Random.Range(-0.25f, 0.25f);
            for (int finger = 0; finger < 4; finger++)
            {
                Vector3 top = start + across * (finger * 0.055f) + down * (Mathf.Abs(finger - 1.5f) * 0.03f);
                Vector3 bottom = top + down * Random.Range(0.45f, 0.65f) + across * slant;
                Blood.Smear(top, bottom, wall.normal, 0.04f, 0.9f);
            }
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            if (_colour != 0) Recolour(0);
        }

        private void Styles()
        {
            if (_button != null) return;

            _panel = Fill(new Color(0.04f, 0.04f, 0.045f, 0.72f));
            _title = new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.93f, 0.91f, 0.88f) } };
            _subtitle = new GUIStyle(GUI.skin.label) { fontSize = 24, normal = { textColor = new Color(0.82f, 0.25f, 0.21f) } };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(0.62f, 0.6f, 0.58f) } };
            _center = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
            _button = Button(new Color(0.12f, 0.12f, 0.13f, 0.95f), new Color(0.2f, 0.2f, 0.22f, 0.95f), new Color(0.85f, 0.83f, 0.8f), 21);
            _selected = Button(new Color(0.55f, 0.06f, 0.05f, 0.97f), new Color(0.68f, 0.09f, 0.07f, 0.97f), Color.white, 21);
            _hit = Button(new Color(0.62f, 0.05f, 0.04f, 0.97f), new Color(0.78f, 0.08f, 0.06f, 0.98f), Color.white, 52);
            _hit.fontStyle = FontStyle.Bold;
            _outline = Fill(new Color(0.93f, 0.91f, 0.88f));
            _frame = Fill(new Color(0.22f, 0.22f, 0.24f));
            BloodSettings settings = Blood.Settings;
            _swatches = new Texture2D[Colours.Length];
            for (int i = 0; i < Colours.Length; i++)
                _swatches[i] = Fill(Colours[i].Colour);
            if (settings != null)
            {
                _swatches[0] = Fill(settings.Colour);
                _swatches[Colours.Length - 1] = Fill(settings.AlternateColour);
            }
        }

        private static GUIStyle Button(Color background, Color hover, Color text, int size)
        {
            var style = new GUIStyle(GUI.skin.button) { fontSize = size, alignment = TextAnchor.MiddleCenter, border = new RectOffset(0, 0, 0, 0) };
            style.normal.background = Fill(background);
            style.hover.background = Fill(hover);
            style.active.background = Fill(hover * 1.15f);
            style.normal.textColor = text;
            style.hover.textColor = text;
            style.active.textColor = text;
            return style;
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
