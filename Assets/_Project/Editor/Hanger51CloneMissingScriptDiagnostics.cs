using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class Hanger51CloneMissingScriptDiagnostics
    {
        [MenuItem("Hanger 51/Build/91 - Report Missing Scripts Created By Root Cloning")]
        public static void ReportMissingScriptsCreatedByRootCloning()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Clone missing-script report failed. Exit Play mode first.");
                return;
            }
            if (EditorApplication.isCompiling)
            {
                Debug.LogError("Clone missing-script report failed. Wait for Unity to finish compiling.");
                return;
            }

            Scene sourceScene = SceneManager.GetActiveScene();
            if (!sourceScene.IsValid() || !sourceScene.isLoaded || string.IsNullOrWhiteSpace(sourceScene.path))
            {
                Debug.LogError("Clone missing-script report failed. Open the saved real game scene first.");
                return;
            }

            Scene tempScene = default;
            try
            {
                List<GameObject> roots = new List<GameObject>(sourceScene.GetRootGameObjects());
                roots.Sort((a, b) =>
                {
                    int nameCompare = string.Compare(a.name, b.name, StringComparison.Ordinal);
                    return nameCompare != 0 ? nameCompare : a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex());
                });

                tempScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                List<string> lines = new List<string>();
                int totalMissing = 0;

                for (int rootIndex = 0; rootIndex < roots.Count; rootIndex++)
                {
                    GameObject sourceRoot = roots[rootIndex];
                    GameObject cloneRoot = Object.Instantiate(sourceRoot);
                    cloneRoot.name = sourceRoot.name;
                    SceneManager.MoveGameObjectToScene(cloneRoot, tempScene);

                    int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(cloneRoot);
                    totalMissing += missing;
                    lines.Add($"ROOT [{rootIndex:D2}] {sourceRoot.name}: MissingScriptsAfterClone={missing}");

                    if (missing > 0)
                    {
                        CompareHierarchy(sourceRoot.transform, cloneRoot.transform, lines);
                    }

                    Object.DestroyImmediate(cloneRoot);
                }

                Debug.Log(
                    $"Clone missing-script report for '{sourceScene.path}': TotalMissingAfterIndependentRootClone={totalMissing}.\n"
                    + string.Join("\n", lines));
            }
            finally
            {
                if (tempScene.IsValid() && tempScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(tempScene, true);
                }
            }
        }

        private static void CompareHierarchy(Transform source, Transform clone, List<string> lines)
        {
            if (source == null || clone == null) return;

            Component[] sourceComponents = source.GetComponents<Component>();
            Component[] cloneComponents = clone.GetComponents<Component>();
            int count = Mathf.Max(sourceComponents.Length, cloneComponents.Length);

            for (int index = 0; index < count; index++)
            {
                Component sourceComponent = index < sourceComponents.Length ? sourceComponents[index] : null;
                Component cloneComponent = index < cloneComponents.Length ? cloneComponents[index] : null;

                if (cloneComponent != null) continue;
                if (sourceComponent == null) continue;

                string sourceType = sourceComponent.GetType().FullName ?? sourceComponent.GetType().Name;
                lines.Add(
                    "  MISSING CLONE COMPONENT: " + GetPath(source) +
                    " | componentIndex=" + index +
                    " | sourceType=" + sourceType);
            }

            int childCount = Mathf.Min(source.childCount, clone.childCount);
            for (int index = 0; index < childCount; index++)
            {
                CompareHierarchy(source.GetChild(index), clone.GetChild(index), lines);
            }
        }

        private static string GetPath(Transform transform)
        {
            List<string> names = new List<string>();
            Transform cursor = transform;
            while (cursor != null)
            {
                names.Add(cursor.name);
                cursor = cursor.parent;
            }
            names.Reverse();
            return string.Join("/", names);
        }
    }
}
