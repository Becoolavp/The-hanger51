using Hanger51.NPCs;
using Hanger51.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hanger51.World
{
    [DisallowMultipleComponent]
    public sealed class H51WorldTimeHUD : MonoBehaviour
    {
        [SerializeField] H51WorldClock clock;
        [SerializeField] Key toggleKey = Key.K;
        [SerializeField] bool compactAlwaysVisible = true;

        Canvas canvas;
        GameObject panel;
        Text compactText;
        Text detailText;
        bool expanded;
        float nextRefresh;

        public void Configure(H51WorldClock configuredClock)
        {
            clock = configuredClock;
            if (!Application.isPlaying)
                return;

            EnsureUI();
            Refresh();
        }

        void Awake()
        {
            Resolve();
            EnsureUI();
            Refresh();
        }

        void OnEnable()
        {
            Resolve();
            EnsureUI();
            Refresh();
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                var key = keyboard[toggleKey];
                if (key != null && key.wasPressedThisFrame)
                {
                    expanded = !expanded;
                    ApplyVisibility();
                    Refresh();
                }
            }

            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + .25f;
                Refresh();
            }
        }

        void Resolve()
        {
            if (!clock) clock = H51WorldClock.Instance;
            if (!clock) clock = FindFirstObjectByType<H51WorldClock>(FindObjectsInactive.Include);
        }

        void EnsureUI()
        {
            if (canvas) return;

            GameObject root = new GameObject("H51 World Clock HUD");
            root.transform.SetParent(transform,false);

            canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 145;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f,1080f);
            scaler.matchWidthOrHeight = .5f;
            root.AddComponent<GraphicRaycaster>();

            panel = new GameObject("Clock Calendar Panel");
            panel.transform.SetParent(root.transform,false);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f,1f);
            panelRect.anchorMax = new Vector2(1f,1f);
            panelRect.pivot = new Vector2(1f,1f);
            panelRect.anchoredPosition = new Vector2(-22f,-22f);
            panelRect.sizeDelta = new Vector2(360f,118f);

            Image background = panel.AddComponent<Image>();
            background.color = new Color(.018f,.025f,.035f,.72f);
            background.raycastTarget = false;

            compactText = CreateText(
                panel.transform,
                "Compact Time",
                new Vector2(-12f,-9f),
                new Vector2(336f,32f),
                20,
                TextAnchor.UpperRight);

            detailText = CreateText(
                panel.transform,
                "Calendar Detail",
                new Vector2(-12f,-42f),
                new Vector2(336f,66f),
                16,
                TextAnchor.UpperRight);

            ApplyVisibility();
        }

        Text CreateText(
            Transform parent,
            string objectName,
            Vector2 anchoredPosition,
            Vector2 size,
            int fontSize,
            TextAnchor alignment)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(parent,false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f,1f);
            rect.anchorMax = new Vector2(1f,1f);
            rect.pivot = new Vector2(1f,1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Text text = go.AddComponent<Text>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (!font)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.font = font;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        void ApplyVisibility()
        {
            if (!panel) return;
            panel.SetActive(compactAlwaysVisible || expanded);
            if (detailText) detailText.gameObject.SetActive(expanded);
            RectTransform rect = panel.GetComponent<RectTransform>();
            if (rect)
                rect.sizeDelta = expanded
                    ? new Vector2(360f,118f)
                    : new Vector2(360f,46f);
        }

        void Refresh()
        {
            Resolve();
            if (!clock)
            {
                if (compactText) compactText.text = "WORLD TIME unavailable";
                return;
            }

            if (compactText)
            {
                compactText.text =
                    clock.TimeText + "  •  " +
                    clock.ShortDateText +
                    "  [K]";
            }

            if (detailText && expanded)
            {
                string workday = clock.IsWeekend ? "Weekend" : "Weekday";
                string solar = H51DayNightCycle.Instance
                    ? "Sunrise " + FormatHour(H51DayNightCycle.Instance.SunriseHour) +
                      "  •  Sunset " + FormatHour(H51DayNightCycle.Instance.SunsetHour)
                    : string.Empty;

                detailText.text =
                    clock.DateText + "\n" +
                    workday +
                    (string.IsNullOrWhiteSpace(solar) ? string.Empty : "  •  " + solar);
            }
        }

        static string FormatHour(float hour)
        {
            int totalMinutes = Mathf.RoundToInt(Mathf.Repeat(hour,24f) * 60f);
            int h = (totalMinutes / 60) % 24;
            int m = totalMinutes % 60;
            int display = h % 12;
            if (display == 0) display = 12;
            return display + ":" + m.ToString("00") + (h >= 12 ? " PM" : " AM");
        }
    }
}
