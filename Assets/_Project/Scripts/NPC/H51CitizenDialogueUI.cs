using Hanger51.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.NPCs
{
    [DefaultExecutionOrder(-100)]
    public sealed class H51CitizenDialogueUI : MonoBehaviour
    {
        public static bool IsAnyDialogueOpen { get; private set; }

        [SerializeField] FirstPersonController firstPersonController;

        H51CitizenDialogue citizen;
        string response;
        Vector2 scroll;

        GUIStyle titleStyle;
        GUIStyle bodyStyle;
        GUIStyle buttonStyle;
        GUIStyle meterLabelStyle;
        GUIStyle meterValueStyle;
        GUIStyle panelTitleStyle;

        public bool IsOpen => citizen != null;

        void Awake()
        {
            if (!firstPersonController) firstPersonController = GetComponent<FirstPersonController>();
        }

        void Update()
        {
            if (!IsOpen) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.iKey.wasPressedThisFrame))
                Close();
        }

        public void Open(H51CitizenDialogue target)
        {
            if (!target) return;
            citizen = target;
            citizen.CaptureActivityBeforeConversation();
            response = citizen.Greeting();
            citizen.RegisterConversation();
            H51CitizenBrain brain = citizen.GetComponent<H51CitizenBrain>();
            if (brain) brain.SetConversationPaused(true, transform.position);
            IsAnyDialogueOpen = true;
            if (firstPersonController) firstPersonController.SetExternalInputBlocked(true);
        }

        public void Close()
        {
            if (citizen)
            {
                H51CitizenBrain brain = citizen.GetComponent<H51CitizenBrain>();
                if (brain) brain.SetConversationPaused(false, transform.position);
            }
            citizen = null;
            response = string.Empty;
            IsAnyDialogueOpen = false;
            if (firstPersonController) firstPersonController.SetExternalInputBlocked(false);
        }

        void OnDisable()
        {
            if (IsOpen) Close();
        }

        void OnGUI()
        {
            if (!IsOpen || !citizen) return;
            EnsureStyles();

            float width = Mathf.Min(1040f, Screen.width - 40f);
            float height = Mathf.Min(790f, Screen.height - 40f);
            Rect window = new Rect(
                (Screen.width - width) * .5f,
                Screen.height - height - 24f,
                width,
                height);

            GUI.Box(window, GUIContent.none);

            Rect inner = new Rect(
                window.x + 20f,
                window.y + 16f,
                window.width - 40f,
                window.height - 32f);

            GUILayout.BeginArea(inner);
            GUILayout.BeginHorizontal();

            float relationshipWidth = Mathf.Clamp(window.width * .27f, 240f, 285f);
            float conversationWidth = inner.width - relationshipWidth - 20f;

            GUILayout.BeginVertical(GUILayout.Width(conversationWidth));

            GUILayout.Label(citizen.Identity.DisplayName, titleStyle);
            GUILayout.Label(
                $"{citizen.Identity.Age} years old  •  {citizen.Identity.Occupation}  •  {citizen.Identity.TownName}",
                bodyStyle);
            GUILayout.Label("Relationship: " + citizen.RelationshipStatus(), bodyStyle);
            GUILayout.Space(10f);

            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(122f));
            GUILayout.Label(response, bodyStyle);
            GUILayout.EndScrollView();
            GUILayout.Space(8f);

            if (GUILayout.Button("Who are you?", buttonStyle)) Ask(H51CitizenQuestion.Identity);
            if (GUILayout.Button("What do you do for work?", buttonStyle)) Ask(H51CitizenQuestion.Job);
            if (GUILayout.Button("Where do you live?", buttonStyle)) Ask(H51CitizenQuestion.Home);
            if (GUILayout.Button("What are you doing right now?", buttonStyle)) Ask(H51CitizenQuestion.CurrentActivity);
            if (GUILayout.Button($"What do you think of {citizen.Identity.TownName}?", buttonStyle)) Ask(H51CitizenQuestion.Town);
            if (GUILayout.Button("What have you heard around town?", buttonStyle)) Ask(H51CitizenQuestion.TownGossip);
            if (GUILayout.Button("What do you think of me?", buttonStyle)) Ask(H51CitizenQuestion.OpinionOfPlayer);
            if (GUILayout.Button("What do you remember about me?", buttonStyle)) Ask(H51CitizenQuestion.PlayerMemories);

            GUILayout.Space(8f);
            GUILayout.Label("Build the relationship", bodyStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Chat for a while", buttonStyle)) Social(H51CitizenSocialAction.FriendlyChat);
            if (GUILayout.Button("Give a compliment", buttonStyle)) Social(H51CitizenSocialAction.Compliment);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Offer to help", buttonStyle)) Social(H51CitizenSocialAction.OfferHelp);
            if (GUILayout.Button("Apologize", buttonStyle)) Social(H51CitizenSocialAction.Apologize);
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Goodbye  [Esc]", buttonStyle)) Close();
            GUILayout.EndVertical();

            GUILayout.Space(20f);
            GUILayout.BeginVertical(GUILayout.Width(relationshipWidth));
            DrawRelationshipPanel(relationshipWidth);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void DrawRelationshipPanel(float width)
        {
            H51CitizenRelationship r = citizen.Identity.Relationship("PLAYER", false);

            float trust = r != null ? r.trust : 0f;
            float respect = r != null ? r.respect : 0f;
            float affection = r != null ? r.affection : 0f;
            float fear = r != null ? r.fear : 0f;
            float grudge = r != null ? r.grudge : 0f;

            float overall = Mathf.Clamp(
                trust * .38f +
                respect * .22f +
                affection * .26f -
                fear * .10f -
                grudge * .30f,
                -100f,
                100f);

            GUILayout.Label("RELATIONSHIP", panelTitleStyle);
            GUILayout.Label(citizen.RelationshipStatus(), titleStyle);
            GUILayout.Space(8f);

            DrawSignedMeter("Overall", overall, new Color(.30f,.80f,.38f), new Color(.86f,.24f,.22f));
            GUILayout.Space(8f);
            DrawSignedMeter("Trust", trust, new Color(.26f,.76f,.35f), new Color(.82f,.25f,.22f));
            DrawSignedMeter("Respect", respect, new Color(.28f,.63f,.92f), new Color(.78f,.30f,.28f));
            DrawSignedMeter("Affection", affection, new Color(.90f,.38f,.62f), new Color(.68f,.28f,.48f));
            DrawUnsignedMeter("Fear", fear, new Color(.92f,.63f,.18f));
            DrawUnsignedMeter("Grudge", grudge, new Color(.88f,.20f,.18f));

            GUILayout.Space(12f);
            GUILayout.Label("People remember what you do. Important events can now spread through nearby conversations around town.", meterLabelStyle);
        }

        void DrawSignedMeter(string label, float value, Color positive, Color negative)
        {
            value = Mathf.Clamp(value, -100f, 100f);
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, meterLabelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(value.ToString("0"), meterValueStyle);
            GUILayout.EndHorizontal();

            Rect rect = GUILayoutUtility.GetRect(10f, 18f, GUILayout.ExpandWidth(true));
            DrawBackground(rect);

            float half = rect.width * .5f;
            if (value > 0f)
            {
                Rect fill = new Rect(
                    rect.x + half,
                    rect.y + 2f,
                    half * (value / 100f),
                    rect.height - 4f);
                DrawColorRect(fill, positive);
            }
            else if (value < 0f)
            {
                float w = half * (-value / 100f);
                Rect fill = new Rect(
                    rect.x + half - w,
                    rect.y + 2f,
                    w,
                    rect.height - 4f);
                DrawColorRect(fill, negative);
            }

            DrawColorRect(
                new Rect(rect.x + half - 1f, rect.y + 1f, 2f, rect.height - 2f),
                new Color(1f,1f,1f,.42f));
        }

        void DrawUnsignedMeter(string label, float value, Color fillColor)
        {
            value = Mathf.Clamp(value, 0f, 100f);
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, meterLabelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(value.ToString("0"), meterValueStyle);
            GUILayout.EndHorizontal();

            Rect rect = GUILayoutUtility.GetRect(10f, 18f, GUILayout.ExpandWidth(true));
            DrawBackground(rect);
            Rect fill = new Rect(
                rect.x + 2f,
                rect.y + 2f,
                (rect.width - 4f) * (value / 100f),
                rect.height - 4f);
            DrawColorRect(fill, fillColor);
        }

        static void DrawBackground(Rect rect)
        {
            DrawColorRect(rect, new Color(.04f,.04f,.04f,.88f));
            DrawColorRect(
                new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, 1f),
                new Color(1f,1f,1f,.17f));
        }

        static void DrawColorRect(Rect rect, Color color)
        {
            if (rect.width <= 0f || rect.height <= 0f) return;
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        void Ask(H51CitizenQuestion question)
        {
            if (!citizen) return;
            response = citizen.Answer(question);
            scroll = Vector2.zero;
        }

        void Social(H51CitizenSocialAction action)
        {
            if (!citizen) return;
            response = citizen.PerformSocialAction(action);
            scroll = Vector2.zero;
        }

        void EnsureStyles()
        {
            if (titleStyle != null) return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            titleStyle.normal.textColor = Color.white;

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                wordWrap = true
            };
            bodyStyle.normal.textColor = Color.white;

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleLeft,
                fixedHeight = 34f
            };

            meterLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true
            };
            meterLabelStyle.normal.textColor = new Color(.88f,.88f,.88f);

            meterValueStyle = new GUIStyle(meterLabelStyle)
            {
                alignment = TextAnchor.MiddleRight,
                fontStyle = FontStyle.Bold
            };

            panelTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            panelTitleStyle.normal.textColor = new Color(.72f,.78f,.86f);
        }
    }
}
