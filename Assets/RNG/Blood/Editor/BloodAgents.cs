using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Rng.Blood.EditorTools
{
    public static class BloodAgents
    {
        public const string GuideGuid = "8f3c2d71a94e4b6c9d0e15a7b2c4f863";
        // the AGENTS.md of the Dismemberment add-on, when it is installed
        public const string DismembermentGuideGuid = "d718d70634e654740a5e4872e0900c2d";

        private const string SkillHeader = "---\nname: rng-blood\ndescription: Use when adding, changing or debugging blood in this Unity project, such as hit and death effects, bleeding wounds, footprints, a gore or censored option, blood colour or blood performance. The project uses the RNG Blood package (namespace Rng.Blood); this skill holds its API, rules and recipes.\n---\n\n";
        private const string NoteMarker = "RNG Blood";

        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
        public static string GuidePath => AssetDatabase.GUIDToAssetPath(GuideGuid);
        public static string DismembermentGuidePath => AssetDatabase.GUIDToAssetPath(DismembermentGuideGuid);
        public static bool Dismemberment => !string.IsNullOrEmpty(DismembermentGuidePath) && File.Exists(DismembermentGuidePath);

        // the AGENTS.md of the Contact add-on, when it is installed
        public const string ContactGuideGuid = "7d2c618dfa8344fb84c370742ea8e433";
        public static string ContactGuidePath => AssetDatabase.GUIDToAssetPath(ContactGuideGuid);
        public static bool Contact => !string.IsNullOrEmpty(ContactGuidePath) && File.Exists(ContactGuidePath);

        // the installed add-ons by name, for prompts and the window title
        public static string AddOns
        {
            get
            {
                var names = new List<string>();
                if (Dismemberment) names.Add("Dismemberment");
                if (Contact) names.Add("Contact");
                return string.Join(" and ", names);
            }
        }
        public static string SkillPath => Path.Combine(ProjectRoot, ".claude", "skills", "rng-blood", "SKILL.md");
        public static string NotesPath => Path.Combine(ProjectRoot, "AGENTS.md");

        public static bool SkillCurrent => File.Exists(SkillPath) && File.ReadAllText(SkillPath) == Skill();
        public static bool SkillInstalled => File.Exists(SkillPath);
        public static bool NoteAdded => File.Exists(NotesPath) && File.ReadAllText(NotesPath).Contains(NoteMarker);

        [MenuItem("Tools/RNG Blood/Install AI Agent Skill", priority = 20)]
        public static void InstallSkillWithPrompt()
        {
            if (string.IsNullOrEmpty(GuidePath)) return;
            if (!EditorUtility.DisplayDialog("Install AI agent skill", $"Writes the RNG Blood skill for Claude Code to:\n{SkillPath}\n\nIt holds the same text as {GuidePath}. Nothing else in the project changes.", "Install", "Cancel")) return;

            InstallSkill();
        }

        [MenuItem("Tools/RNG Blood/Add Note To AGENTS.md", priority = 21)]
        public static void AddNoteWithPrompt()
        {
            if (string.IsNullOrEmpty(GuidePath) || NoteAdded) return;
            if (!EditorUtility.DisplayDialog("Add note to AGENTS.md", $"Adds one line to {NotesPath} (it is created if missing) that points coding agents such as Codex, Cursor and Copilot to {GuidePath}.", "Add", "Cancel")) return;

            AddNote();
        }

        [MenuItem("Tools/RNG Blood/Copy AI Prompt", priority = 19)]
        public static void CopyPrompt()
        {
            string prompt = Prompt();
            if (prompt == null) return;

            EditorGUIUtility.systemCopyBuffer = prompt;
            Debug.Log("RNG Blood: the AI prompt is on the clipboard. Paste it into Claude Code, Codex, Cursor or any coding agent that can read this project.");
        }

        /// <summary>
        /// A prompt for a coding agent: where the project is, which guides to read first and what to do. Lists the Dismemberment guide too
        /// when the add-on is installed.
        /// </summary>
        public static string Prompt()
        {
            if (string.IsNullOrEmpty(GuidePath)) return null;

            var guides = new List<string> { Full(GuidePath) + " (RNG Blood)" };
            if (Dismemberment) guides.Add(Full(DismembermentGuidePath) + " (RNG Blood: Dismemberment add-on, read after the first one)");
            if (Contact) guides.Add(Full(ContactGuidePath) + " (RNG Blood: Contact add-on, read after the first one)");
            string addOns = AddOns;
            string packages = addOns.Length > 0 ? $"RNG Blood and its {addOns} add-on{(Dismemberment && Contact ? "s" : "")}" : "RNG Blood";
            return $"My Unity project at {ProjectRoot.Replace('\\', '/')} uses {packages}, a procedural blood system for URP.\n\n"
                   + "Before you write any code, read these guides for AI coding agents completely. They hold the API, the rules that keep the blood working and ready recipes:\n"
                   + string.Join("\n", guides.ConvertAll(guide => "- " + guide)) + "\n\n"
                   + "Then add the blood to my game:\n"
                   + "1. Find where my weapons, projectiles and characters deal and take damage, and where characters die.\n"
                   + "2. Wire the package there exactly as the guides describe, with the hit kind that matches each weapon.\n"
                   + "3. Check the project setup the guides ask for (URP depth texture, surface layers, character colliders off those layers).\n"
                   + "4. Do not edit the package files. Put new code in my own scripts.\n"
                   + "5. When you are done, list what you changed and tell me how to see it in Play mode.\n\n"
                   + "What I want: <describe it here, or delete this line and let the agent choose sensible defaults>";
        }

        private static string Full(string assetPath)
        {
            return Path.Combine(ProjectRoot, assetPath).Replace('\\', '/');
        }

        public static void InstallSkill()
        {
            string skill = Skill();
            if (skill == null) return;

            Directory.CreateDirectory(Path.GetDirectoryName(SkillPath));
            File.WriteAllText(SkillPath, skill);
        }

        public static void AddNote()
        {
            if (string.IsNullOrEmpty(GuidePath) || NoteAdded) return;

            string line = $"- {NoteMarker} (procedural blood for URP) is installed. Before adding or changing blood effects, read `{GuidePath}`"
                          + (Dismemberment ? $", for cutting and jets `{DismembermentGuidePath}`" : "")
                          + (Contact ? $", for soaking, prints, bloody weapons and washing `{ContactGuidePath}`" : "") + ".\n";
            if (File.Exists(NotesPath))
            {
                string text = File.ReadAllText(NotesPath);
                File.AppendAllText(NotesPath, (text.Length == 0 || text.EndsWith("\n") ? "" : "\n") + line);
            }
            else
            {
                File.WriteAllText(NotesPath, "# Notes for coding agents\n\n" + line);
            }
        }

        private static string Skill()
        {
            string guide = GuidePath;
            if (string.IsNullOrEmpty(guide) || !File.Exists(guide)) return null;

            string skill = SkillHeader + File.ReadAllText(guide).Replace("\r\n", "\n");
            if (Dismemberment) skill += "\n\n" + File.ReadAllText(DismembermentGuidePath).Replace("\r\n", "\n");
            if (Contact) skill += "\n\n" + File.ReadAllText(ContactGuidePath).Replace("\r\n", "\n");
            return skill;
        }
    }
}
