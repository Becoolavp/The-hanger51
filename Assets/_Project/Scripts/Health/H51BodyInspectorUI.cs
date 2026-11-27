using Hanger51.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Health
{
    [DisallowMultipleComponent]
    public sealed class H51BodyInspectorUI : MonoBehaviour
    {
        [SerializeField] H51AnatomicalHealth target;
        [SerializeField] bool visible;
        [SerializeField] Key toggleKey = Key.H;

        FirstPersonController controller;
        H51BodyRegion selectedRegion = H51BodyRegion.Chest;
        H51OrganType? selectedOrgan;

        GUIStyle titleStyle;
        GUIStyle headingStyle;
        GUIStyle labelStyle;
        GUIStyle smallStyle;
        GUIStyle buttonStyle;
        GUIStyle organMarkerStyle;
        GUIStyle centeredStyle;

        public bool Visible => visible;
        public H51AnatomicalHealth Target => target;

        void Awake()
        {
            if (!target)
                target = GetComponent<H51AnatomicalHealth>();

            controller =
                GetComponent<FirstPersonController>();
        }

        void OnDisable()
        {
            if (visible)
                SetVisible(false);
        }

        void Update()
        {
            Keyboard keyboard =
                Keyboard.current;

            if (keyboard == null)
                return;

            if (keyboard[toggleKey].wasPressedThisFrame)
            {
                SetVisible(!visible);
                return;
            }

            if (visible &&
                keyboard.escapeKey.wasPressedThisFrame)
                SetVisible(false);
        }

        public void SetTarget(
            H51AnatomicalHealth newTarget)
        {
            target =
                newTarget;
            selectedOrgan =
                null;
        }

        public void SetVisible(
            bool show)
        {
            visible =
                show;

            if (!controller)
                controller =
                    GetComponent<FirstPersonController>();

            if (controller)
                controller.SetExternalInputBlocked(show);
            else
            {
                Cursor.lockState =
                    show
                        ? CursorLockMode.None
                        : CursorLockMode.Locked;
                Cursor.visible =
                    show;
            }
        }

        void OnGUI()
        {
            if (!visible ||
                !target)
                return;

            EnsureStyles();

            float width =
                Mathf.Min(
                    1180f,
                    Screen.width - 60f);
            float height =
                Mathf.Min(
                    720f,
                    Screen.height - 50f);

            Rect panel =
                new Rect(
                    (Screen.width-width) * .5f,
                    (Screen.height-height) * .5f,
                    width,
                    height);

            Color old =
                GUI.color;
            GUI.color =
                new Color(0f,0f,0f,.94f);
            GUI.DrawTexture(
                panel,
                Texture2D.whiteTexture);
            GUI.color =
                old;

            float pad = 28f;
            Rect inner =
                new Rect(
                    panel.x+pad,
                    panel.y+pad,
                    panel.width-pad*2f,
                    panel.height-pad*2f);

            GUI.Label(
                new Rect(
                    inner.x,
                    inner.y,
                    370f,
                    38f),
                "♥  BODY INSPECTOR",
                titleStyle);

            GUI.Label(
                new Rect(
                    inner.x+390f,
                    inner.y+4f,
                    350f,
                    30f),
                target.gameObject.name,
                headingStyle);

            GUI.Label(
                new Rect(
                    inner.x+inner.width-260f,
                    inner.y+4f,
                    260f,
                    30f),
                target.IsDead
                    ? "STATUS: DECEASED"
                    : "STATUS: " + target.OverallStatus,
                headingStyle);

            float bodyX =
                inner.x;
            float top =
                inner.y + 62f;
            float bodyW =
                245f;
            float middleX =
                inner.x + 285f;
            float middleW =
                330f;
            float vitalsX =
                inner.x + 655f;
            float vitalsW =
                inner.width - 655f;

            DrawBodyDiagram(
                new Rect(
                    bodyX,
                    top,
                    bodyW,
                    470f));

            DrawSelectedPanel(
                new Rect(
                    middleX,
                    top,
                    middleW,
                    470f));

            DrawVitals(
                new Rect(
                    vitalsX,
                    top,
                    vitalsW,
                    470f));

            GUI.Label(
                new Rect(
                    inner.x,
                    inner.y+inner.height-42f,
                    390f,
                    28f),
                "H  CLOSE INSPECTOR",
                headingStyle);

            if (Debug.isDebugBuild ||
                Application.isEditor)
            {
                DrawDevelopmentTests(
                    new Rect(
                        middleX,
                        inner.y+inner.height-58f,
                        inner.width-(middleX-inner.x),
                        46f));
            }
        }

        void DrawBodyDiagram(
            Rect area)
        {
            GUI.Label(
                new Rect(
                    area.x,
                    area.y,
                    area.width,
                    26f),
                "BODY REGIONS",
                headingStyle);

            float cx =
                area.x +
                area.width * .5f;

            RegionButton(
                H51BodyRegion.Head,
                new Rect(cx-28f,area.y+42f,56f,48f),
                "HEAD");

            RegionButton(
                H51BodyRegion.Neck,
                new Rect(cx-22f,area.y+94f,44f,30f),
                "NECK");

            RegionButton(
                H51BodyRegion.Chest,
                new Rect(cx-52f,area.y+128f,104f,78f),
                "CHEST");

            RegionButton(
                H51BodyRegion.Abdomen,
                new Rect(cx-47f,area.y+210f,94f,62f),
                "ABDOMEN");

            RegionButton(
                H51BodyRegion.Pelvis,
                new Rect(cx-43f,area.y+276f,86f,46f),
                "PELVIS");

            RegionButton(
                H51BodyRegion.LeftArm,
                new Rect(cx-88f,area.y+138f,30f,160f),
                "L\nARM");

            RegionButton(
                H51BodyRegion.RightArm,
                new Rect(cx+58f,area.y+138f,30f,160f),
                "R\nARM");

            RegionButton(
                H51BodyRegion.LeftLeg,
                new Rect(cx-42f,area.y+326f,34f,128f),
                "L\nLEG");

            RegionButton(
                H51BodyRegion.RightLeg,
                new Rect(cx+8f,area.y+326f,34f,128f),
                "R\nLEG");

            // Anatomical overlay. These compact markers sit approximately
            // where the corresponding organs live inside the body silhouette.
            // They are drawn after the region buttons so they remain clickable.
            OrganMarker(
                H51OrganType.Brain,
                H51BodyRegion.Head,
                new Rect(cx-13f,area.y+57f,26f,18f),
                "BR");

            OrganMarker(
                H51OrganType.LeftLung,
                H51BodyRegion.Chest,
                new Rect(cx-43f,area.y+143f,27f,24f),
                "L");

            OrganMarker(
                H51OrganType.RightLung,
                H51BodyRegion.Chest,
                new Rect(cx+16f,area.y+143f,27f,24f),
                "R");

            OrganMarker(
                H51OrganType.Heart,
                H51BodyRegion.Chest,
                new Rect(cx-13f,area.y+169f,26f,23f),
                "♥");

            OrganMarker(
                H51OrganType.Spine,
                H51BodyRegion.Chest,
                new Rect(cx-10f,area.y+132f,20f,20f),
                "SP");

            OrganMarker(
                H51OrganType.Liver,
                H51BodyRegion.Abdomen,
                new Rect(cx-42f,area.y+218f,30f,20f),
                "LI");

            OrganMarker(
                H51OrganType.Stomach,
                H51BodyRegion.Abdomen,
                new Rect(cx+12f,area.y+218f,30f,20f),
                "ST");

            OrganMarker(
                H51OrganType.LeftKidney,
                H51BodyRegion.Abdomen,
                new Rect(cx-37f,area.y+242f,28f,19f),
                "LK");

            OrganMarker(
                H51OrganType.RightKidney,
                H51BodyRegion.Abdomen,
                new Rect(cx+9f,area.y+242f,28f,19f),
                "RK");

            OrganMarker(
                H51OrganType.Intestines,
                H51BodyRegion.Abdomen,
                new Rect(cx-21f,area.y+264f,42f,18f),
                "INT");

            GUI.Label(
                new Rect(
                    area.x,
                    area.y+456f,
                    area.width,
                    18f),
                "ORGAN COLOR = LIVE CONDITION",
                smallStyle);
        }

        void RegionButton(
            H51BodyRegion region,
            Rect rect,
            string label)
        {
            H51BodyRegionState state =
                target.GetRegion(region);

            float condition =
                state != null
                    ? state.condition
                    : 0f;

            Color old =
                GUI.color;

            GUI.color =
                selectedRegion == region &&
                !selectedOrgan.HasValue
                    ? new Color(.45f,.85f,1f,1f)
                    : ConditionColor(condition);

            if (GUI.Button(
                    rect,
                    label,
                    buttonStyle))
            {
                selectedRegion =
                    region;
                selectedOrgan =
                    null;
            }

            GUI.color =
                old;
        }

        void OrganMarker(
            H51OrganType organType,
            H51BodyRegion containingRegion,
            Rect rect,
            string shortLabel)
        {
            H51OrganState organ =
                target.GetOrgan(organType);

            if (organ == null)
                return;

            Color old =
                GUI.color;

            Color conditionColor =
                OrganConditionColor(
                    organ.condition);

            bool selected =
                selectedOrgan.HasValue &&
                selectedOrgan.Value == organType;

            GUI.color =
                selected
                    ? Color.Lerp(
                        conditionColor,
                        new Color(.35f,.90f,1f,1f),
                        .42f)
                    : conditionColor;

            GUIContent content =
                new GUIContent(
                    shortLabel,
                    organ.displayName +
                    ": " +
                    organ.condition.ToString("0") +
                    "%");

            if (GUI.Button(
                    rect,
                    content,
                    organMarkerStyle))
            {
                selectedOrgan =
                    organType;

                // Keep the development trauma controls anatomically coherent:
                // selecting an organ also selects the body region surrounding it.
                selectedRegion =
                    containingRegion;
            }

            GUI.color =
                old;
        }

        void DrawSelectedPanel(
            Rect area)
        {
            GUI.Label(
                new Rect(
                    area.x,
                    area.y,
                    area.width,
                    28f),
                selectedOrgan.HasValue
                    ? "SELECTED ANATOMY: " +
                      Friendly(selectedOrgan.Value.ToString()).ToUpperInvariant()
                    : "SELECTED ANATOMY: " +
                      Friendly(selectedRegion.ToString()).ToUpperInvariant(),
                headingStyle);

            float y =
                area.y + 38f;

            if (selectedOrgan.HasValue)
            {
                H51OrganState organ =
                    target.GetOrgan(
                        selectedOrgan.Value);

                if (organ != null)
                {
                    GUI.Label(
                        new Rect(area.x,y,area.width,24f),
                        "Condition: " +
                        organ.condition.ToString("0.0") +
                        "%",
                        labelStyle);
                    y += 25f;

                    GUI.Label(
                        new Rect(area.x,y,area.width,24f),
                        "Internal bleeding: " +
                        organ.internalBleedMlPerMinute.ToString("0") +
                        " mL/min",
                        labelStyle);
                    y += 25f;

                    GUI.Label(
                        new Rect(area.x,y,area.width,24f),
                        "Critical: " +
                        (organ.critical ? "YES" : "no"),
                        labelStyle);
                    y += 35f;
                }
            }
            else
            {
                H51BodyRegionState region =
                    target.GetRegion(
                        selectedRegion);

                if (region != null)
                {
                    GUI.Label(
                        new Rect(area.x,y,area.width,24f),
                        "Condition: " +
                        region.condition.ToString("0.0") +
                        "%",
                        labelStyle);
                    y += 25f;

                    GUI.Label(
                        new Rect(area.x,y,area.width,24f),
                        "External bleeding: " +
                        region.externalBleedMlPerMinute.ToString("0") +
                        " mL/min",
                        labelStyle);
                    y += 25f;

                    GUI.Label(
                        new Rect(area.x,y,area.width,24f),
                        "Fracture: " +
                        (region.fractured ? "YES" : "no") +
                        "    Function: " +
                        (region.disabled ? "DISABLED" : "active"),
                        labelStyle);
                    y += 35f;
                }
            }

            GUI.Label(
                new Rect(
                    area.x,
                    y,
                    area.width,
                    26f),
                "ORGANS",
                headingStyle);
            y += 30f;

            for (int i=0;i<target.Organs.Count;i++)
            {
                H51OrganState organ =
                    target.Organs[i];
                if (organ == null)
                    continue;

                Rect row =
                    new Rect(
                        area.x,
                        y,
                        area.width,
                        28f);

                Color old =
                    GUI.color;
                Color organColor =
                    OrganConditionColor(
                        organ.condition);

                GUI.color =
                    selectedOrgan.HasValue &&
                    selectedOrgan.Value == organ.organ
                        ? Color.Lerp(
                            organColor,
                            new Color(.35f,.90f,1f,1f),
                            .42f)
                        : organColor;

                if (GUI.Button(
                        row,
                        organ.displayName +
                        "   " +
                        organ.condition.ToString("0") +
                        "%",
                        buttonStyle))
                {
                    selectedOrgan =
                        organ.organ;
                }

                GUI.color =
                    old;
                y += 31f;

                if (y > area.yMax - 28f)
                    break;
            }
        }

        void DrawVitals(
            Rect area)
        {
            GUI.Label(
                new Rect(
                    area.x,
                    area.y,
                    area.width,
                    28f),
                "PHYSIOLOGY",
                headingStyle);

            float y =
                area.y + 42f;

            DrawBar(
                ref y,
                area,
                "Condition",
                OverallCondition() / 100f,
                OverallCondition().ToString("0") + "%",
                new Color(.30f,.85f,.95f));

            DrawBar(
                ref y,
                area,
                "Blood",
                target.BloodFraction,
                target.BloodVolumeLiters.ToString("0.00") +
                " / " +
                target.NormalBloodVolumeLiters.ToString("0.00") +
                " L",
                new Color(.95f,.28f,.35f));

            DrawBar(
                ref y,
                area,
                "Oxygen",
                target.OxygenSaturation,
                (target.OxygenSaturation*100f).ToString("0.0") +
                "%",
                new Color(.42f,.45f,1f));

            DrawBar(
                ref y,
                area,
                "Consciousness",
                target.Consciousness / 100f,
                target.Consciousness.ToString("0") + "%",
                new Color(.83f,.83f,.83f));

            DrawBar(
                ref y,
                area,
                "Pain",
                target.Pain / 100f,
                target.Pain.ToString("0") + "%",
                new Color(.90f,.75f,.72f));

            DrawBar(
                ref y,
                area,
                "Shock",
                target.Shock / 100f,
                target.Shock.ToString("0") + "%",
                new Color(.68f,.34f,.18f));

            DrawBar(
                ref y,
                area,
                "Adrenaline",
                target.Adrenaline / 100f,
                target.Adrenaline.ToString("0") + "%",
                new Color(.90f,.50f,.18f));

            y += 10f;

            GUI.Label(
                new Rect(area.x,y,area.width,24f),
                "Pulse: " +
                target.PulseBpm.ToString("0") +
                " bpm",
                labelStyle);
            y += 25f;

            GUI.Label(
                new Rect(area.x,y,area.width,24f),
                "Systolic pressure: " +
                target.SystolicPressure.ToString("0") +
                " mmHg",
                labelStyle);
            y += 25f;

            GUI.Label(
                new Rect(area.x,y,area.width,24f),
                "External bleeding: " +
                target.ExternalBleedMlPerMinute.ToString("0") +
                " mL/min",
                labelStyle);
            y += 25f;

            GUI.Label(
                new Rect(area.x,y,area.width,24f),
                "Internal bleeding: " +
                target.InternalBleedMlPerMinute.ToString("0") +
                " mL/min",
                labelStyle);
            y += 30f;

            if (target.IsDead)
            {
                Color old =
                    GUI.color;
                GUI.color =
                    new Color(1f,.28f,.28f);
                GUI.Label(
                    new Rect(area.x,y,area.width,50f),
                    "CAUSE OF DEATH\n" +
                    target.CauseOfDeath,
                    headingStyle);
                GUI.color =
                    old;
            }
        }

        void DrawBar(
            ref float y,
            Rect area,
            string label,
            float value,
            string valueText,
            Color fill)
        {
            GUI.Label(
                new Rect(
                    area.x,
                    y,
                    132f,
                    24f),
                label,
                labelStyle);

            Rect bar =
                new Rect(
                    area.x+138f,
                    y+2f,
                    Mathf.Max(80f,area.width-220f),
                    18f);

            Color old =
                GUI.color;
            GUI.color =
                new Color(.28f,.28f,.28f,1f);
            GUI.DrawTexture(
                bar,
                Texture2D.whiteTexture);

            GUI.color =
                fill;
            GUI.DrawTexture(
                new Rect(
                    bar.x,
                    bar.y,
                    bar.width*Mathf.Clamp01(value),
                    bar.height),
                Texture2D.whiteTexture);

            GUI.color =
                old;

            GUI.Label(
                new Rect(
                    bar.xMax+8f,
                    y,
                    110f,
                    24f),
                valueText,
                smallStyle);

            y += 38f;
        }

        void DrawDevelopmentTests(
            Rect area)
        {
            float w =
                142f;

            GUI.Label(
                new Rect(
                    area.x,
                    area.y-22f,
                    260f,
                    22f),
                "DEVELOPMENT TEST CONTROLS",
                smallStyle);

            if (GUI.Button(
                    new Rect(area.x,area.y,w,34f),
                    "BLUNT 10",
                    buttonStyle))
                target.ApplyDamage(
                    selectedRegion,
                    10f,
                    H51DamageType.Blunt,
                    0f);

            if (GUI.Button(
                    new Rect(area.x+w+8f,area.y,w,34f),
                    "PENETRATING 20",
                    buttonStyle))
                target.ApplyDamage(
                    selectedRegion,
                    20f,
                    H51DamageType.Penetrating,
                    .75f);

            if (GUI.Button(
                    new Rect(area.x+(w+8f)*2f,area.y,w,34f),
                    "STABILIZE",
                    buttonStyle))
                target.StabilizeAllBleeding(.90f);

            if (GUI.Button(
                    new Rect(area.x+(w+8f)*3f,area.y,w,34f),
                    "RESET BODY",
                    buttonStyle))
                target.ResetHealthy();
        }

        float OverallCondition()
        {
            float total =
                0f;
            int count =
                0;

            for (int i=0;i<target.Regions.Count;i++)
            {
                H51BodyRegionState region =
                    target.Regions[i];
                if (region == null)
                    continue;
                total += region.condition;
                count++;
            }

            for (int i=0;i<target.Organs.Count;i++)
            {
                H51OrganState organ =
                    target.Organs[i];
                if (organ == null)
                    continue;
                total += organ.condition;
                count++;
            }

            return
                count > 0
                    ? total / count
                    : 0f;
        }

        static Color ConditionColor(
            float condition)
        {
            if (condition >= 75f)
                return new Color(.72f,.78f,.80f,1f);
            if (condition >= 45f)
                return new Color(1f,.72f,.28f,1f);
            return new Color(1f,.30f,.30f,1f);
        }

        static Color OrganConditionColor(
            float condition)
        {
            if (condition >= 90f)
                return new Color(.32f,.76f,.62f,1f);
            if (condition >= 70f)
                return new Color(.68f,.78f,.32f,1f);
            if (condition >= 45f)
                return new Color(1f,.72f,.22f,1f);
            if (condition >= 25f)
                return new Color(1f,.42f,.16f,1f);
            return new Color(.95f,.16f,.16f,1f);
        }

        void EnsureStyles()
        {
            if (titleStyle != null)
                return;

            titleStyle =
                new GUIStyle(GUI.skin.label)
                {
                    fontSize = 24,
                    fontStyle = FontStyle.Bold,
                    normal =
                    {
                        textColor = Color.white
                    }
                };

            headingStyle =
                new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    normal =
                    {
                        textColor = Color.white
                    }
                };

            labelStyle =
                new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    normal =
                    {
                        textColor =
                            new Color(.90f,.90f,.90f)
                    }
                };

            smallStyle =
                new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    normal =
                    {
                        textColor =
                            new Color(.80f,.80f,.80f)
                    }
                };

            buttonStyle =
                new GUIStyle(GUI.skin.button)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };

            organMarkerStyle =
                new GUIStyle(GUI.skin.button)
                {
                    fontSize = 9,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(1,1,1,1),
                    margin = new RectOffset(0,0,0,0)
                };

            centeredStyle =
                new GUIStyle(labelStyle)
                {
                    alignment =
                        TextAnchor.MiddleCenter
                };
        }

        static string Friendly(
            string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            System.Text.StringBuilder builder =
                new System.Text.StringBuilder();

            for (int i=0;i<value.Length;i++)
            {
                char c =
                    value[i];
                if (i > 0 &&
                    char.IsUpper(c))
                    builder.Append(' ');
                builder.Append(c);
            }

            return builder.ToString();
        }
    }
}
