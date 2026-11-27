using UnityEngine;
#if BLOOD_UGUI
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if BLOOD_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
#endif

namespace Rng.Blood.Demo
{
#if BLOOD_UGUI
    public class BloodTuner : MonoBehaviour
    {
        public static bool Available => true;

        private const float PanelWidth = 560f;
        private const float RowHeight = 36f;
        private const float RebuildDelay = 0.35f;

        private static readonly Color Ink = new(0.93f, 0.91f, 0.88f);
        private static readonly Color Dim = new(0.62f, 0.6f, 0.58f);
        private static readonly Color Accent = new(0.62f, 0.05f, 0.04f);
        private static readonly Color Back = new(0.04f, 0.04f, 0.045f, 0.92f);
        private static readonly Color Control = new(0.12f, 0.12f, 0.13f, 1f);
        private static readonly Color ControlHover = new(0.2f, 0.2f, 0.22f, 1f);

        [SerializeField] private bool _openOnStart;

        private readonly List<Action> _refresh = new();
        private BloodSettings _original;
        private BloodSettings _tuned;
        private Font _font;
        private GameObject _canvas;
        private Text _hint;
        private float _applyAt = -1f;

        public bool Open => _canvas != null && _canvas.activeSelf;

        private void Start()
        {
            if (_openOnStart) Show(true);
        }

        private void Update()
        {
            if (_applyAt < 0f || Time.unscaledTime < _applyAt || _tuned == null) return;

            _applyAt = -1f;
            Blood.Apply(_tuned);
        }

        private void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas);
            if (_tuned == null) return;

            if (Blood.Settings == _tuned && _original != null) Blood.Apply(_original);
            Destroy(_tuned);
        }

        public void Toggle()
        {
            Show(!Open);
        }

        public void Show(bool open)
        {
            if (open && !Bind()) return;

            if (open && _canvas == null) Build();
            if (_canvas != null) _canvas.SetActive(open);
        }

        public void ResetValues()
        {
            if (_original == null || _tuned == null || _hint == null) return;

            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(_original), _tuned);
            foreach (Action refresh in _refresh) refresh();
            Blood.Apply(_tuned);
            _hint.text = "Back to the values of " + _original.name + ".";
        }

        private bool Bind()
        {
            BloodSettings current = Blood.Settings;
            if (current == null) return false;
            if (current == _tuned) return true;

            _original = current;
            if (_tuned != null) Destroy(_tuned);
            _tuned = Instantiate(current);
            _tuned.name = current.name + " (Tuned)";
            Blood.Apply(_tuned);
            if (_canvas != null)
            {
                Destroy(_canvas);
                _canvas = null;
                _refresh.Clear();
            }

            return true;
        }

        private void Changed(bool rebuild)
        {
            _applyAt = Time.unscaledTime + (rebuild ? RebuildDelay : 0f);
        }

        private void Build()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (EventSystem.current == null && FindAnyObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem));
#if BLOOD_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
                events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
#else
                events.AddComponent<StandaloneInputModule>();
#endif
            }

            _canvas = new GameObject("Blood Tuner", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            RectTransform panel = Box("Panel", _canvas.transform, Back);
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 0.5f);
            panel.offsetMin = new Vector2(24f, 24f);
            panel.offsetMax = new Vector2(24f + PanelWidth, -24f);

            Place(AddLabel("Title", panel, "BLOOD SETTINGS", 34, Ink, FontStyle.Bold), 24f, -20f, PanelWidth - 120f, 44f);
            Place(AddLabel("Subtitle", panel, "Live: every change shows on the next hit.", 18, new Color(0.82f, 0.25f, 0.21f)), 24f, -62f, PanelWidth - 48f, 26f);
            Place(AddButton("Close", panel, "X", 22, () => Show(false)), PanelWidth - 72f, -20f, 48f, 44f);
            Place(AddButton("Reset", panel, "Reset", 18, ResetValues), 24f, -100f, 120f, 38f);
#if UNITY_EDITOR
            Place(AddButton("Save", panel, "Save as asset", 18, Save), 152f, -100f, 180f, 38f);
#endif

            RectTransform view = Box("View", panel, Color.clear);
            view.anchorMin = Vector2.zero;
            view.anchorMax = Vector2.one;
            view.offsetMin = new Vector2(12f, 92f);
            view.offsetMax = new Vector2(-12f, -152f);
            view.gameObject.AddComponent<RectMask2D>();

            RectTransform content = AddRect("Content", view);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            Stack(content.gameObject, 4f);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = view;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            _hint = AddLabel("Hint", panel, "Point at a setting to read what it does.", 16, Dim);
            _hint.alignment = TextAnchor.UpperLeft;
            RectTransform hint = _hint.rectTransform;
            hint.anchorMin = new Vector2(0f, 0f);
            hint.anchorMax = new Vector2(1f, 0f);
            hint.pivot = new Vector2(0.5f, 0f);
            hint.offsetMin = new Vector2(24f, 14f);
            hint.offsetMax = new Vector2(-24f, 84f);

            Rows(content);
        }

        private void Rows(Transform content)
        {
            BloodSettings s = _tuned;
            Type t = typeof(BloodSettings);

            Transform look = Section(content, "Look", true);
            ColourRows(look, "Colour", t, nameof(BloodSettings.Colour), () => s.Colour, v => s.Colour = v);
            ColourRows(look, "Alternate colour", t, nameof(BloodSettings.AlternateColour), () => s.AlternateColour, v => s.AlternateColour = v);
            AddSlider(look, "Drop scale", t, nameof(BloodSettings.DropScale), 0.2f, 3f, false, () => s.DropScale, v => s.DropScale = v);
            AddSlider(look, "Stain lifetime", t, nameof(BloodSettings.StainLifetime), 1f, 300f, true, () => s.StainLifetime, v => s.StainLifetime = v);

            Transform dry = Section(content, "Drying", true);
            AddSlider(dry, "Dry start", t, nameof(BloodSettings.DryStart), 0f, 1f, false, () => s.DryStart, v => s.DryStart = v);
            AddSlider(dry, "Dry end", t, nameof(BloodSettings.DryEnd), 0f, 1f, false, () => s.DryEnd, v => s.DryEnd = v);
            AddSlider(dry, "Fade start", t, nameof(BloodSettings.FadeStart), 0f, 1f, false, () => s.FadeStart, v => s.FadeStart = v);
            AddSlider(dry, "Dry brightness", t, nameof(BloodSettings.DryBrightness), 0f, 1f, false, () => s.DryBrightness, v => s.DryBrightness = v);
            AddSlider(dry, "Dry brown", t, nameof(BloodSettings.DryBrown), 0f, 1f, false, () => s.DryBrown, v => s.DryBrown = v);
            AddSlider(dry, "Dry smoothness", t, nameof(BloodSettings.DrySmoothness), 0f, 1f, false, () => s.DrySmoothness, v => s.DrySmoothness = v);

            Transform pace = Section(content, "Pace", false);
            AddSlider(pace, "Flight pace", t, nameof(BloodSettings.FlightPace), 0.5f, 2f, false, () => s.FlightPace, v => s.FlightPace = v);
            AddSlider(pace, "Drop speed", t, nameof(BloodSettings.DropSpeed), 0.5f, 2f, false, () => s.DropSpeed, v => s.DropSpeed = v);
            AddSlider(pace, "Drop reach", t, nameof(BloodSettings.DropReach), 0.5f, 2f, false, () => s.DropReach, v => s.DropReach = v);
            AddSlider(pace, "Drop spread", t, nameof(BloodSettings.DropSpread), 0.5f, 2f, false, () => s.DropSpread, v => s.DropSpread = v);

            Transform merge = Section(content, "Stains that run together", false);
            AddSlider(merge, "Merge gap", t, nameof(BloodSettings.MergeGap), 0f, 0.5f, false, () => s.MergeGap, v => s.MergeGap = v);
            AddSlider(merge, "Merge overlap", t, nameof(BloodSettings.MergeOverlap), 0.1f, 1.5f, false, () => s.MergeOverlap, v => s.MergeOverlap = v);
            AddSlider(merge, "Merge spread", t, nameof(BloodSettings.MergeSpread), 0.1f, 2f, false, () => s.MergeSpread, v => s.MergeSpread = v);
            AddSlider(merge, "Largest puddle", t, nameof(BloodSettings.MergeRadiusMax), 0.1f, 3f, false, () => s.MergeRadiusMax, v => s.MergeRadiusMax = v);
            AddSlider(merge, "Grow seconds", t, nameof(BloodSettings.MergeGrowSeconds), 0.05f, 3f, false, () => s.MergeGrowSeconds, v => s.MergeGrowSeconds = v);
            AddSlider(merge, "Merge dryness", t, nameof(BloodSettings.MergeDryness), 0f, 1f, false, () => s.MergeDryness, v => s.MergeDryness = v);

            Transform body = Section(content, "Walls and body", false);
            AddSlider(body, "Wall run size", t, nameof(BloodSettings.WallRunSize), 0f, 1f, false, () => s.WallRunSize, v => s.WallRunSize = v);
            AddSlider(body, "Chest height", t, nameof(BloodSettings.ChestHeight), 0f, 2f, false, () => s.ChestHeight, v => s.ChestHeight = v);

            Transform pool = Section(content, "Pool under a body", false);
            AddSlider(pool, "Delay", t, nameof(BloodSettings.PoolDelay), 0f, 5f, false, () => s.PoolDelay, v => s.PoolDelay = v);
            AddSlider(pool, "Grow seconds", t, nameof(BloodSettings.PoolGrowSeconds), 0.1f, 10f, false, () => s.PoolGrowSeconds, v => s.PoolGrowSeconds = v);
            AddSlider(pool, "Stays for", t, nameof(BloodSettings.PoolSeconds), 1f, 300f, true, () => s.PoolSeconds, v => s.PoolSeconds = v);
            AddSlider(pool, "Radius bullet", t, nameof(BloodSettings.PoolRadiusBullet), 0f, 3f, false, () => s.PoolRadiusBullet, v => s.PoolRadiusBullet = v);
            AddSlider(pool, "Radius headshot", t, nameof(BloodSettings.PoolRadiusHeadshot), 0f, 3f, false, () => s.PoolRadiusHeadshot, v => s.PoolRadiusHeadshot = v);
            AddSlider(pool, "Radius blade", t, nameof(BloodSettings.PoolRadiusSharp), 0f, 3f, false, () => s.PoolRadiusSharp, v => s.PoolRadiusSharp = v);
            AddSlider(pool, "Radius blunt", t, nameof(BloodSettings.PoolRadiusBlunt), 0f, 3f, false, () => s.PoolRadiusBlunt, v => s.PoolRadiusBlunt = v);
            AddSlider(pool, "Radius explosion", t, nameof(BloodSettings.PoolRadiusExplosion), 0f, 3f, false, () => s.PoolRadiusExplosion, v => s.PoolRadiusExplosion = v);

            Transform prints = Section(content, "Footprints", false);
            AddSlider(prints, "Prints", t, nameof(BloodSettings.PrintCount), 0f, 20f, true, () => s.PrintCount, v => s.PrintCount = (int)v);
            AddSlider(prints, "Print length", t, nameof(BloodSettings.PrintLength), 0.05f, 1f, false, () => s.PrintLength, v => s.PrintLength = v);
            AddSlider(prints, "Print side", t, nameof(BloodSettings.PrintSide), 0f, 0.5f, false, () => s.PrintSide, v => s.PrintSide = v);
            AddSlider(prints, "Print reach", t, nameof(BloodSettings.PrintReach), 0f, 1f, false, () => s.PrintReach, v => s.PrintReach = v);
            AddSlider(prints, "Stride length", t, nameof(BloodSettings.StrideLength), 0.1f, 3f, false, () => s.StrideLength, v => s.StrideLength = v);

            Transform limits = Section(content, "Quality and limits (changing one clears the blood)", false);
            AddChoice(limits, "Quality", t, nameof(BloodSettings.Quality), () => s.Quality, v => s.Quality = v, true);
            AddChoice(limits, "Massive", t, nameof(BloodSettings.Massive), () => s.Massive ? Switch.On : Switch.Off, v => s.Massive = v == Switch.On, true);
            AddChoice(limits, "Liquid", t, nameof(BloodSettings.Liquid), () => s.Liquid ? Switch.On : Switch.Off, v => s.Liquid = v == Switch.On, true);
            AddSlider(limits, "Drops", t, nameof(BloodSettings.MaxDrops), 64f, 8192f, true, () => s.MaxDrops, v => s.MaxDrops = (int)v, true);
            AddSlider(limits, "Splats", t, nameof(BloodSettings.MaxSplats), 16f, 6000f, true, () => s.MaxSplats, v => s.MaxSplats = (int)v, true);
            AddSlider(limits, "Pools", t, nameof(BloodSettings.MaxPools), 4f, 500f, true, () => s.MaxPools, v => s.MaxPools = (int)v, true);
            AddSlider(limits, "Footprints", t, nameof(BloodSettings.MaxFootprints), 1f, 1000f, true, () => s.MaxFootprints, v => s.MaxFootprints = (int)v, true);

            BurstRows(content, "Burst: gout", () => s.Gout);
            BurstRows(content, "Burst: bullet", () => s.BulletFar);
            BurstRows(content, "Burst: point blank", () => s.BulletClose);
            BurstRows(content, "Burst: back spatter", () => s.BackSpatter);
            BurstRows(content, "Burst: shotgun", () => s.Shotgun);
            BurstRows(content, "Burst: headshot", () => s.Headshot);
            BurstRows(content, "Burst: blunt", () => s.Blunt);
            BurstRows(content, "Burst: blade", () => s.Sharp);
            BurstRows(content, "Burst: explosion", () => s.Explosion);
            BurstRows(content, "Burst: death", () => s.Death);
            BurstRows(content, "Burst: mist", () => s.Mist);
            BurstRows(content, "Burst: wound drip", () => s.WoundDrip);
        }

        private void BurstRows(Transform content, string title, Func<BloodBurst> burst)
        {
            Type t = typeof(BloodBurst);
            Transform section = Section(content, title, false);
            AddSlider(section, "Fewest drops", t, nameof(BloodBurst.CountMin), 0f, 80f, true, () => burst().CountMin, v => burst().CountMin = (int)v);
            AddSlider(section, "Most drops", t, nameof(BloodBurst.CountMax), 0f, 80f, true, () => burst().CountMax, v => burst().CountMax = (int)v);
            AddSlider(section, "Cone", t, nameof(BloodBurst.ConeDegrees), 0f, 180f, true, () => burst().ConeDegrees, v => burst().ConeDegrees = v);
            AddSlider(section, "Slowest", t, nameof(BloodBurst.SpeedMin), 0f, 15f, false, () => burst().SpeedMin, v => burst().SpeedMin = v);
            AddSlider(section, "Fastest", t, nameof(BloodBurst.SpeedMax), 0f, 15f, false, () => burst().SpeedMax, v => burst().SpeedMax = v);
            AddSlider(section, "Smallest drop", t, nameof(BloodBurst.SizeMin), 0f, 0.1f, false, () => burst().SizeMin, v => burst().SizeMin = v);
            AddSlider(section, "Largest drop", t, nameof(BloodBurst.SizeMax), 0f, 0.1f, false, () => burst().SizeMax, v => burst().SizeMax = v);
            AddSlider(section, "Stain scale", t, nameof(BloodBurst.StainScale), 0f, 15f, false, () => burst().StainScale, v => burst().StainScale = v);
            AddSlider(section, "Lift", t, nameof(BloodBurst.Lift), -1f, 1f, false, () => burst().Lift, v => burst().Lift = v);
            AddChoice(section, "Splat shape", t, nameof(BloodBurst.Style), () => burst().Style, v => burst().Style = v);
            AddChoice(section, "Floor mark shape", t, nameof(BloodBurst.MarkStyle), () => burst().MarkStyle, v => burst().MarkStyle = v);
            AddSlider(section, "Floor mark size", t, nameof(BloodBurst.MarkSize), 0f, 2f, false, () => burst().MarkSize, v => burst().MarkSize = v);
            AddSlider(section, "Floor mark length", t, nameof(BloodBurst.MarkStretch), 1f, 8f, false, () => burst().MarkStretch, v => burst().MarkStretch = v);
        }

        private Transform Section(Transform content, string title, bool open)
        {
            RectTransform head = AddRect(title, content);
            Size(head.gameObject, 44f);
            Button button = AddButton("Head", head, "", 20, null);
            Fill(button.GetComponent<RectTransform>());
            Text label = button.GetComponentInChildren<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            label.fontStyle = FontStyle.Bold;
            label.rectTransform.offsetMin = new Vector2(22f, 0f);
            RectTransform bar = Box("Bar", head, Accent);
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(0f, 1f);
            bar.offsetMin = Vector2.zero;
            bar.offsetMax = new Vector2(5f, 0f);

            RectTransform rows = AddRect(title + " Rows", content);
            Stack(rows.gameObject, 2f);
            rows.gameObject.SetActive(open);
            string caption = title.ToUpperInvariant();
            void Caption() => label.text = (rows.gameObject.activeSelf ? "-  " : "+  ") + caption;
            Caption();
            button.onClick.AddListener(() =>
            {
                rows.gameObject.SetActive(!rows.gameObject.activeSelf);
                Caption();
            });
            return rows;
        }

        private void AddSlider(Transform parent, string name, Type owner, string property, float min, float max, bool whole, Func<float> get, Action<float> set, bool rebuild = false)
        {
            RectTransform row = Row(parent, name, owner, property);
            var slider = DefaultControls.CreateSlider(new DefaultControls.Resources()).GetComponent<Slider>();
            Style(slider, Accent);
            RectTransform area = slider.GetComponent<RectTransform>();
            area.SetParent(row, false);
            Span(area, 0.42f, 0.82f);
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = whole;

            Text value = AddLabel("Value", row, "", 17, Ink);
            value.alignment = TextAnchor.MiddleRight;
            Span(value.rectTransform, 0.84f, 1f);

            string format = whole ? "0" : max <= 0.2f ? "0.000" : "0.00";
            void Display() => value.text = get().ToString(format, CultureInfo.InvariantCulture);
            _refresh.Add(() =>
            {
                slider.SetValueWithoutNotify(get());
                Display();
            });
            slider.SetValueWithoutNotify(get());
            Display();
            slider.onValueChanged.AddListener(v =>
            {
                set(v);
                Display();
                Changed(rebuild);
            });
        }

        private void ColourRows(Transform parent, string name, Type owner, string property, Func<Color> get, Action<Color> set)
        {
            RectTransform row = Row(parent, name, owner, property);
            RectTransform swatch = Box("Swatch", row, get());
            Span(swatch, 0.42f, 1f);
            swatch.offsetMin = new Vector2(0f, 6f);
            swatch.offsetMax = new Vector2(0f, -6f);
            var image = swatch.GetComponent<Image>();
            _refresh.Add(() => image.color = get());

            string[] channels = { "   red", "   green", "   blue" };
            Color[] fills = { new(0.75f, 0.12f, 0.1f), new(0.2f, 0.6f, 0.15f), new(0.15f, 0.3f, 0.75f) };
            for (int i = 0; i < 3; i++)
            {
                int channel = i;
                AddSlider(parent, channels[i], owner, property, 0f, 1f, false, () => get()[channel], v =>
                {
                    Color colour = get();
                    colour[channel] = v;
                    set(colour);
                    image.color = colour;
                });
                Transform last = parent.GetChild(parent.childCount - 1);
                Style(last.GetComponentInChildren<Slider>(), fills[i]);
            }
        }

        private enum Switch
        {
            Off,
            On
        }

        private void AddChoice<T>(Transform parent, string name, Type owner, string property, Func<T> get, Action<T> set, bool rebuild = false) where T : struct, Enum
        {
            RectTransform row = Row(parent, name, owner, property);
            Button button = AddButton("Choice", row, "", 17, null);
            Span(button.GetComponent<RectTransform>(), 0.42f, 1f);
            Text label = button.GetComponentInChildren<Text>();
            var values = (T[])Enum.GetValues(typeof(T));
            void Display() => label.text = get().ToString();
            _refresh.Add(Display);
            Display();
            button.onClick.AddListener(() =>
            {
                int index = Array.IndexOf(values, get());
                set(values[(index + 1) % values.Length]);
                Display();
                Changed(rebuild);
            });
        }

        private RectTransform Row(Transform parent, string name, Type owner, string property)
        {
            RectTransform row = AddRect(name, parent);
            Size(row.gameObject, RowHeight);
            row.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.025f);
            Text label = AddLabel("Label", row, name, 18, Dim);
            Span(label.rectTransform, 0f, 0.4f);
            label.rectTransform.offsetMin = new Vector2(14f, 0f);
            string tip = Tip(owner, property);
            row.gameObject.AddComponent<Hover>().Enter = () => _hint.text = tip;
            return row;
        }

        private static string Tip(Type owner, string property)
        {
            FieldInfo field = owner.GetField($"<{property}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            var tip = field != null ? field.GetCustomAttribute<TooltipAttribute>() : null;
            return tip != null ? tip.tooltip : "";
        }

#if UNITY_EDITOR
        private void Save()
        {
            string path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath("Assets/" + _original.name + " Tuned.asset");
            BloodSettings copy = Instantiate(_tuned);
            UnityEditor.AssetDatabase.CreateAsset(copy, path);
            UnityEditor.AssetDatabase.SaveAssets();
            _hint.text = "Saved to " + path + ". Pass it to Blood.Initialize or replace the default settings with it.";
        }
#endif

        private static void Style(Slider slider, Color fill)
        {
            foreach (Image image in slider.GetComponentsInChildren<Image>(true))
            {
                image.color = image.name switch
                {
                    "Background" => new Color(0.18f, 0.18f, 0.2f, 1f),
                    "Fill" => fill,
                    _ => Ink
                };
            }

            var background = (RectTransform)slider.transform.Find("Background");
            background.anchorMin = new Vector2(0f, 0.4f);
            background.anchorMax = new Vector2(1f, 0.6f);
            var fillArea = (RectTransform)slider.transform.Find("Fill Area");
            fillArea.anchorMin = new Vector2(0f, 0.4f);
            fillArea.anchorMax = new Vector2(1f, 0.6f);
            var handle = (RectTransform)slider.handleRect;
            handle.sizeDelta = new Vector2(10f, -14f);
            ColorBlock colours = slider.colors;
            colours.highlightedColor = new Color(1f, 0.85f, 0.8f);
            slider.colors = colours;
        }

        private Button AddButton(string name, Transform parent, string caption, int size, Action click)
        {
            RectTransform rect = Box(name, parent, Color.white);
            var button = rect.gameObject.AddComponent<Button>();
            ColorBlock colours = button.colors;
            colours.normalColor = Control;
            colours.highlightedColor = ControlHover;
            colours.pressedColor = Accent;
            colours.selectedColor = Control;
            button.colors = colours;
            Text label = AddLabel("Text", rect, caption, size, Ink);
            label.alignment = TextAnchor.MiddleCenter;
            Fill(label.rectTransform);
            if (click != null) button.onClick.AddListener(() => click());
            return button;
        }

        private Text AddLabel(string name, Transform parent, string caption, int size, Color colour, FontStyle style = FontStyle.Normal)
        {
            RectTransform rect = AddRect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = _font;
            text.text = caption;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = colour;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform Box(string name, Transform parent, Color colour)
        {
            RectTransform rect = AddRect(name, parent);
            rect.gameObject.AddComponent<Image>().color = colour;
            return rect;
        }

        private static RectTransform AddRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Place(Component component, float x, float y, float width, float height)
        {
            var rect = (RectTransform)component.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Span(RectTransform rect, float from, float to)
        {
            rect.anchorMin = new Vector2(from, 0f);
            rect.anchorMax = new Vector2(to, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Fill(RectTransform rect)
        {
            Span(rect, 0f, 1f);
        }

        private static void Size(GameObject item, float height)
        {
            item.AddComponent<LayoutElement>().preferredHeight = height;
        }

        private static void Stack(GameObject item, float spacing)
        {
            var stack = item.AddComponent<VerticalLayoutGroup>();
            stack.spacing = spacing;
            stack.childControlWidth = true;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;
        }

        private sealed class Hover : MonoBehaviour, IPointerEnterHandler
        {
            public Action Enter;

            public void OnPointerEnter(PointerEventData eventData)
            {
                Enter?.Invoke();
            }
        }
    }
#else
    public class BloodTuner : MonoBehaviour
    {
        public static bool Available => false;

        public bool Open => false;

        public void Toggle()
        {
            Show(true);
        }

        public void Show(bool open)
        {
            if (open) Debug.LogWarning("[BloodTuner] The settings panel needs the Unity UI package (com.unity.ugui). Add it in the Package Manager.", this);
        }
    }
#endif
}
