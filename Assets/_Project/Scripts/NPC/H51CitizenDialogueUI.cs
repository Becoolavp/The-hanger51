using Hanger51.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.NPCs
{
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

        public bool IsOpen => citizen != null;

        void Awake()
        {
            if (!firstPersonController) firstPersonController = GetComponent<FirstPersonController>();
        }

        void Update()
        {
            if (!IsOpen) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                Close();
        }

        public void Open(H51CitizenDialogue target)
        {
            if (!target) return;
            citizen = target;
            response = citizen.Greeting();
            citizen.RegisterConversation();
            IsAnyDialogueOpen = true;
            if (firstPersonController) firstPersonController.SetExternalInputBlocked(true);
        }

        public void Close()
        {
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

            float width = Mathf.Min(720f, Screen.width - 40f);
            float height = Mathf.Min(560f, Screen.height - 60f);
            Rect window = new Rect((Screen.width - width) * .5f, Screen.height - height - 24f, width, height);
            GUI.Box(window, GUIContent.none);

            GUILayout.BeginArea(new Rect(window.x + 20f, window.y + 16f, window.width - 40f, window.height - 32f));
            GUILayout.Label(citizen.Identity.DisplayName, titleStyle);
            GUILayout.Label($"{citizen.Identity.Age} years old  •  {citizen.Identity.Occupation}  •  {citizen.Identity.TownName}", bodyStyle);
            GUILayout.Space(10f);

            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(120f));
            GUILayout.Label(response, bodyStyle);
            GUILayout.EndScrollView();
            GUILayout.Space(8f);

            if (GUILayout.Button("Who are you?", buttonStyle)) Ask(H51CitizenQuestion.Identity);
            if (GUILayout.Button("What do you do for work?", buttonStyle)) Ask(H51CitizenQuestion.Job);
            if (GUILayout.Button("Where do you live?", buttonStyle)) Ask(H51CitizenQuestion.Home);
            if (GUILayout.Button("What are you doing right now?", buttonStyle)) Ask(H51CitizenQuestion.CurrentActivity);
            if (GUILayout.Button($"What do you think of {citizen.Identity.TownName}?", buttonStyle)) Ask(H51CitizenQuestion.Town);
            if (GUILayout.Button("What do you think of me?", buttonStyle)) Ask(H51CitizenQuestion.OpinionOfPlayer);
            if (GUILayout.Button("What do you remember about me?", buttonStyle)) Ask(H51CitizenQuestion.PlayerMemories);

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Goodbye  [Esc]", buttonStyle)) Close();
            GUILayout.EndArea();
        }

        void Ask(H51CitizenQuestion question)
        {
            if (!citizen) return;
            response = citizen.Answer(question);
            scroll = Vector2.zero;
        }

        void EnsureStyles()
        {
            if (titleStyle != null) return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleLeft,
                fixedHeight = 34f
            };
        }
    }
}
