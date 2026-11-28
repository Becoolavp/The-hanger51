using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Rng.Blood.EditorTools
{
    public static class BloodSetup
    {
        public const string CrimeSceneGuid = "c30c4b982ce15e44d8a17031b6cff06e";
        public const string HitDemoGuid = "ae7ad9c103638fe4ba8716309acf199b";
        public const string ArenaGuid = "cb9e8d4c2b1d9eb4689cd8b1e4e00adc";
        public const string ShowcaseGuid = "fa076aaf67ae4c246928002a7019e25a";
        public const string DocumentationGuid = "4851d34de10dc03419bc45382e0d741b";

        public struct Issue
        {
            public string Message;
            public System.Action Fix;
        }

        public static List<Issue> Check()
        {
            var issues = new List<Issue>();
            List<UniversalRenderPipelineAsset> pipelines = Pipelines();
            if (pipelines.Count == 0)
            {
                issues.Add(new Issue { Message = "The project does not use the Universal Render Pipeline. RNG Blood needs URP: assign a URP asset in Project Settings > Graphics." });
                return issues;
            }

            BloodSettings settings = Resources.Load<BloodSettings>(Blood.DefaultSettingsResource);
            bool flat = settings != null && settings.Quality == BloodQuality.Low;
            foreach (UniversalRenderPipelineAsset pipeline in pipelines)
            {
                if (flat) break;

                UniversalRenderPipelineAsset asset = pipeline;
                if (!asset.supportsCameraDepthTexture)
                {
                    issues.Add(new Issue
                    {
                        Message = $"Depth Texture is off in \"{asset.name}\". Stains, pools and footprints need it.",
                        Fix = () =>
                        {
                            Undo.RecordObject(asset, "Turn on Depth Texture");
                            asset.supportsCameraDepthTexture = true;
                            EditorUtility.SetDirty(asset);
                        }
                    });
                }

                foreach (UniversalRendererData renderer in Renderers(asset))
                {
                    UniversalRendererData data = renderer;
                    if (data.copyDepthMode == CopyDepthMode.AfterOpaques) continue;

                    issues.Add(new Issue
                    {
                        Message = $"Depth Texture Mode of \"{data.name}\" is not After Opaques. Stains read depth while transparents draw and stay invisible otherwise.",
                        Fix = () =>
                        {
                            Undo.RecordObject(data, "Set Depth Texture Mode");
                            data.copyDepthMode = CopyDepthMode.AfterOpaques;
                            EditorUtility.SetDirty(data);
                        }
                    });
                }
            }

            if (settings == null)
            {
                issues.Add(new Issue { Message = $"No BloodSettings asset named \"{Blood.DefaultSettingsResource}\" in a Resources folder. Pass your own settings to Blood.Initialize or restore Runtime/Resources/BloodSettings.asset." });
            }
            else
            {
                if (settings.DropMaterial == null || settings.StainMaterial == null)
                {
                    issues.Add(new Issue
                    {
                        Message = "The default BloodSettings has no drop or stain material.",
                        Fix = () =>
                        {
                            var serialized = new SerializedObject(settings);
                            Assign(serialized, "<DropMaterial>k__BackingField", BloodSettings.DefaultDropMaterialGuid);
                            Assign(serialized, "<StainMaterial>k__BackingField", BloodSettings.DefaultStainMaterialGuid);
                            serialized.ApplyModifiedProperties();
                        }
                    });
                }

                if (settings.Surfaces.value == 0)
                    issues.Add(new Issue { Message = "Surfaces in the default BloodSettings is Nothing, so drops never land. Pick the layers of your floors and walls." });
            }

            return issues;
        }

        public static int FixAll()
        {
            int fixedCount = 0;
            foreach (Issue issue in Check())
            {
                if (issue.Fix == null) continue;

                issue.Fix();
                fixedCount++;
            }

            AssetDatabase.SaveAssets();
            return fixedCount;
        }

        [MenuItem("Tools/RNG Blood/Open Crime Scene Demo", priority = 2)]
        public static void OpenCrimeScene()
        {
            string path = AssetDatabase.GUIDToAssetPath(CrimeSceneGuid);
            if (string.IsNullOrEmpty(path) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.OpenScene(path);
        }

        [MenuItem("Tools/RNG Blood/Open Hit Demo", priority = 3)]
        public static void OpenHitDemo()
        {
            string path = AssetDatabase.GUIDToAssetPath(HitDemoGuid);
            if (string.IsNullOrEmpty(path) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.OpenScene(path);
        }

        [MenuItem("Tools/RNG Blood/Open Arena Demo", priority = 3)]
        public static void OpenArena()
        {
            string path = AssetDatabase.GUIDToAssetPath(ArenaGuid);
            if (string.IsNullOrEmpty(path) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.OpenScene(path);
        }

        [MenuItem("Tools/RNG Blood/Open Showcase Demo", priority = 3)]
        public static void OpenShowcase()
        {
            string path = AssetDatabase.GUIDToAssetPath(ShowcaseGuid);
            if (string.IsNullOrEmpty(path) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.OpenScene(path);
        }

        /// <summary>Opens a scene found by name, after asking to save the open one.</summary>
        public static void OpenScene(string name)
        {
            string[] found = AssetDatabase.FindAssets(name + " t:Scene");
            if (found.Length == 0 || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EditorSceneManager.OpenScene(AssetDatabase.GUIDToAssetPath(found[0]));
        }

        [MenuItem("Tools/RNG Blood/Documentation", priority = 4)]
        public static void OpenDocumentation()
        {
            Object document = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(DocumentationGuid));
            if (document != null) AssetDatabase.OpenAsset(document);
        }

        [MenuItem("Tools/RNG Blood/Select Default Settings", priority = 6)]
        public static void SelectSettings()
        {
            BloodSettings settings = Resources.Load<BloodSettings>(Blood.DefaultSettingsResource);
            if (settings == null) return;

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        private static void Assign(SerializedObject serialized, string property, string guid)
        {
            SerializedProperty field = serialized.FindProperty(property);
            if (field != null && field.objectReferenceValue == null)
                field.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
        }

        private static List<UniversalRenderPipelineAsset> Pipelines()
        {
            var pipelines = new List<UniversalRenderPipelineAsset>();
            Add(pipelines, GraphicsSettings.defaultRenderPipeline);
            for (int i = 0; i < QualitySettings.names.Length; i++) Add(pipelines, QualitySettings.GetRenderPipelineAssetAt(i));
            return pipelines;
        }

        private static void Add(List<UniversalRenderPipelineAsset> pipelines, RenderPipelineAsset asset)
        {
            if (asset is UniversalRenderPipelineAsset universal && !pipelines.Contains(universal)) pipelines.Add(universal);
        }

        private static IEnumerable<UniversalRendererData> Renderers(UniversalRenderPipelineAsset pipeline)
        {
            SerializedProperty list = new SerializedObject(pipeline).FindProperty("m_RendererDataList");
            if (list == null) yield break;

            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is UniversalRendererData data)
                    yield return data;
        }
    }

    public class BloodSetupWindow : EditorWindow
    {
        private const string ShownKey = "Rng.Blood.SetupShown.";
        private const string WelcomeKey = "Rng.Blood.WelcomeShown.";
        private const string DismembermentKey = "Rng.Blood.DismembermentShown.";
        private const string ContactKey = "Rng.Blood.ContactShown.";

        private List<BloodSetup.Issue> _issues = new();
        private Vector2 _scroll;

        [MenuItem("Tools/RNG Blood/Setup", priority = 1)]
        public static void Open()
        {
            var window = GetWindow<BloodSetupWindow>(true, "RNG Blood");
            window.minSize = new Vector2(520f, 470f);
            window.Refresh();
        }

        [InitializeOnLoadMethod]
        private static void OpenOnFirstImport()
        {
            if (Application.isBatchMode) return;

            // opens once per project after the package is imported, and once more when the Dismemberment add-on arrives
            EditorApplication.delayCall += () =>
            {
                int project = Application.dataPath.GetHashCode();
                bool open = false;
                if (!EditorPrefs.GetBool(WelcomeKey + project))
                {
                    EditorPrefs.SetBool(WelcomeKey + project, true);
                    EditorPrefs.SetBool(ShownKey + project, true);
                    open = true;
                }

                if (BloodAgents.Dismemberment && !EditorPrefs.GetBool(DismembermentKey + project))
                {
                    EditorPrefs.SetBool(DismembermentKey + project, true);
                    open = true;
                }

                if (BloodAgents.Contact && !EditorPrefs.GetBool(ContactKey + project))
                {
                    EditorPrefs.SetBool(ContactKey + project, true);
                    open = true;
                }

                if (open) Open();
            };
        }

        private void OnFocus()
        {
            Refresh();
        }

        private void Refresh()
        {
            _issues = BloodSetup.Check();
            Repaint();
        }

        private void OnGUI()
        {
            bool addon = BloodAgents.Dismemberment;
            bool contact = BloodAgents.Contact;
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.Space(6f);
            string addOns = BloodAgents.AddOns.Replace(" and ", " + ");
            EditorGUILayout.LabelField(addOns.Length > 0 ? "RNG Blood + " + addOns : "RNG Blood", new GUIStyle(EditorStyles.boldLabel) { fontSize = 16 });
            EditorGUILayout.LabelField(
                "Procedural blood for URP: drops in flight, splats, wall runs, pools, a liquid floor, smears, footprints and wounds on characters, in two draw calls."
                + (addon ? " The Dismemberment add-on cuts skinned characters along their bones and keeps jets of blood going from the cuts." : "")
                + (contact ? " The Contact add-on lets bodies soak up pools, hands and tyres leave prints, weapons get bloody and water wash it all away." : ""),
                EditorStyles.wordWrappedLabel);

            // 1: the project, only spelled out when something needs fixing
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("1. Project check", EditorStyles.boldLabel);
            if (_issues.Count == 0)
            {
                EditorGUILayout.HelpBox("Everything is set up.", MessageType.Info);
            }
            else
            {
                foreach (BloodSetup.Issue issue in _issues)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.HelpBox(issue.Message, issue.Fix != null ? MessageType.Warning : MessageType.Error);
                    using (new EditorGUI.DisabledScope(issue.Fix == null))
                    {
                        if (GUILayout.Button("Fix", GUILayout.Width(48f), GUILayout.Height(38f)))
                        {
                            issue.Fix();
                            AssetDatabase.SaveAssets();
                            Refresh();
                            GUIUtility.ExitGUI();
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                }

                using (new EditorGUI.DisabledScope(!_issues.Exists(issue => issue.Fix != null)))
                {
                    if (GUILayout.Button("Fix All", GUILayout.Height(28f)))
                    {
                        BloodSetup.FixAll();
                        Refresh();
                        GUIUtility.ExitGUI();
                    }
                }
            }

            // 2: two ways in, an agent or by hand
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("2. Add it to your game", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("With an AI coding agent: copy the prompt (it holds this project's path and the guides" + (BloodAgents.AddOns.Length > 0 ? ", the add-on guides included" : "")
                                    + "), paste it into Claude Code, Codex, Cursor or any agent, write what you want after \"What I want\", and it wires the blood into your weapons and characters.", MessageType.None);
            if (GUILayout.Button("Copy AI Prompt", GUILayout.Height(32f)))
            {
                BloodAgents.CopyPrompt();
                ShowNotification(new GUIContent("Prompt copied"));
            }

            EditorGUILayout.BeginHorizontal();
            string skill = BloodAgents.SkillCurrent ? "Claude Skill Installed" : BloodAgents.SkillInstalled ? "Update Claude Skill" : "Install Claude Skill";
            using (new EditorGUI.DisabledScope(BloodAgents.SkillCurrent))
            {
                if (GUILayout.Button(skill)) BloodAgents.InstallSkillWithPrompt();
            }

            using (new EditorGUI.DisabledScope(BloodAgents.NoteAdded))
            {
                if (GUILayout.Button(BloodAgents.NoteAdded ? "AGENTS.md Note Added" : "Add Note To AGENTS.md")) BloodAgents.AddNoteWithPrompt();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox("By hand: call Blood.Spawn(hit, direction) where your weapon hits, or add a Blood Emitter and trigger it from a UnityEvent or an Animation Event. "
                                    + "Floors and walls must be on a layer in Surfaces (Default out of the box), characters on another one."
                                    + (addon ? " For cutting, add Blood Body to a character: its inspector turns on Read/Write, builds the ragdoll and moves the body to its own layer, one button each." : ""), MessageType.None);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Documentation")) BloodSetup.OpenDocumentation();
            if (GUILayout.Button("Default Settings")) BloodSetup.SelectSettings();
            EditorGUILayout.EndHorizontal();

            // 3: the demos
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("3. See it", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Crime Scene")) BloodSetup.OpenCrimeScene();
            if (GUILayout.Button("Hit Demo")) BloodSetup.OpenHitDemo();
            if (GUILayout.Button("Arena")) BloodSetup.OpenArena();
            if (GUILayout.Button("Showcase")) BloodSetup.OpenShowcase();
            if (addon && GUILayout.Button("Dismemberment")) BloodSetup.OpenScene("Dismemberment Demo");
            if (contact && GUILayout.Button("Contact")) BloodSetup.OpenScene("Contact Demo");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(6f);
            EditorGUILayout.EndScrollView();
        }
    }

    [CustomEditor(typeof(BloodSettings))]
    public class BloodSettingsInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Check Project Setup")) BloodSetupWindow.Open();
            EditorGUILayout.Space(4f);
            DrawDefaultInspector();
        }
    }
}
