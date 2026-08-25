using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    /// <summary>
    /// Scans the currently open scene for serialization defects that can survive in the Editor but
    /// produce a corrupt standalone level0. In addition to missing references and non-finite values,
    /// this checks MonoBehaviour types that are especially dangerous in player serialization:
    /// editor-only scripts, nested/local component classes, generic component types, and scripts
    /// whose runtime class can no longer be resolved.
    /// </summary>
    public static class Hanger51SceneIntegrityScanner
    {
        [MenuItem("Hanger 51/Build/10 - Scan Current Scene Integrity")]
        public static void ScanCurrentSceneIntegrity()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("Scene integrity scan failed: there is no valid active scene.");
                return;
            }

            int gameObjectCount = 0;
            int componentCount = 0;
            int monoBehaviourCount = 0;
            int problemCount = 0;
            int warningCount = 0;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Transform[] transforms = roots[rootIndex].GetComponentsInChildren<Transform>(true);
                for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
                {
                    Transform transform = transforms[transformIndex];
                    if (transform == null) continue;

                    GameObject gameObject = transform.gameObject;
                    gameObjectCount++;

                    int missingScripts = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
                    if (missingScripts > 0)
                    {
                        Debug.LogError(
                            $"[Scene Integrity] '{GetPath(transform)}' has {missingScripts} missing script component(s).",
                            gameObject);
                        problemCount += missingScripts;
                    }

                    PrefabInstanceStatus prefabStatus = PrefabUtility.GetPrefabInstanceStatus(gameObject);
                    if (prefabStatus == PrefabInstanceStatus.MissingAsset)
                    {
                        Debug.LogError(
                            $"[Scene Integrity] '{GetPath(transform)}' is a prefab instance whose source asset is missing.",
                            gameObject);
                        problemCount++;
                    }

                    if (!IsFinite(transform.localPosition)
                        || !IsFinite(transform.localScale)
                        || !IsFinite(transform.localRotation))
                    {
                        Debug.LogError(
                            $"[Scene Integrity] '{GetPath(transform)}' has a NaN/Infinity Transform value. "
                            + $"Position={transform.localPosition}, Rotation={transform.localRotation}, Scale={transform.localScale}.",
                            gameObject);
                        problemCount++;
                    }

                    Component[] components = gameObject.GetComponents<Component>();
                    for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                    {
                        Component component = components[componentIndex];
                        if (component == null) continue;
                        componentCount++;

                        MonoBehaviour monoBehaviour = component as MonoBehaviour;
                        if (monoBehaviour != null)
                        {
                            monoBehaviourCount++;
                            problemCount += ScanMonoBehaviourType(monoBehaviour, ref warningCount);
                        }

                        problemCount += ScanComponent(component);
                    }
                }
            }

            if (problemCount == 0)
            {
                Debug.Log(
                    $"Scene integrity scan PASSED for '{scene.path}'. Checked {gameObjectCount} GameObjects, "
                    + $"{componentCount} components and {monoBehaviourCount} MonoBehaviours. No fatal missing "
                    + "scripts/prefabs, invalid runtime MonoBehaviour types, broken serialized object references, "
                    + $"or NaN/Infinity values were found. Advisory warnings: {warningCount}.");
            }
            else
            {
                Debug.LogError(
                    $"Scene integrity scan FOUND {problemCount} fatal problem(s) and {warningCount} advisory "
                    + $"warning(s) in '{scene.path}'. Search the Console for '[Scene Integrity]'.");
            }
        }

        private static int ScanMonoBehaviourType(MonoBehaviour behaviour, ref int warningCount)
        {
            if (behaviour == null) return 0;

            int problems = 0;
            string objectPath = GetPath(behaviour.transform);
            MonoScript script = MonoScript.FromMonoBehaviour(behaviour);
            if (script == null)
            {
                Debug.LogError(
                    $"[Scene Integrity] '{objectPath}' component '{behaviour.GetType().Name}' has no resolvable MonoScript asset.",
                    behaviour);
                return 1;
            }

            string scriptPath = AssetDatabase.GetAssetPath(script);
            Type runtimeType = script.GetClass();
            if (runtimeType == null)
            {
                Debug.LogError(
                    $"[Scene Integrity] '{objectPath}' uses script '{scriptPath}', but Unity cannot resolve a runtime class from that MonoScript.",
                    behaviour);
                problems++;
                return problems;
            }

            string normalizedPath = (scriptPath ?? string.Empty).Replace('\\', '/');
            if (normalizedPath.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Debug.LogError(
                    $"[Scene Integrity] '{objectPath}' has editor-only MonoBehaviour '{runtimeType.FullName}' from '{scriptPath}'. "
                    + "An Editor-folder component must never be serialized into a runtime scene.",
                    behaviour);
                problems++;
            }

            if (runtimeType.IsNested)
            {
                Debug.LogError(
                    $"[Scene Integrity] '{objectPath}' has nested MonoBehaviour type '{runtimeType.FullName}' from '{scriptPath}'. "
                    + "Nested/local MonoBehaviour scene types are a known cause of player type-tree/level0 corruption.",
                    behaviour);
                problems++;
            }

            if (runtimeType.IsGenericType || runtimeType.ContainsGenericParameters)
            {
                Debug.LogError(
                    $"[Scene Integrity] '{objectPath}' has generic MonoBehaviour type '{runtimeType.FullName}' from '{scriptPath}'.",
                    behaviour);
                problems++;
            }

            if (runtimeType.IsAbstract)
            {
                Debug.LogError(
                    $"[Scene Integrity] '{objectPath}' has abstract MonoBehaviour type '{runtimeType.FullName}' from '{scriptPath}'.",
                    behaviour);
                problems++;
            }

            // Top-level internal MonoBehaviours can work in Unity, so do not treat visibility alone
            // as fatal. It is still useful evidence when chasing a standalone type-tree mismatch.
            if (!runtimeType.IsPublic && !runtimeType.IsNestedPublic)
            {
                Debug.LogWarning(
                    $"[Scene Integrity] Advisory: '{objectPath}' uses non-public MonoBehaviour "
                    + $"'{runtimeType.FullName}' from '{scriptPath}'.",
                    behaviour);
                warningCount++;
            }

            if (!string.IsNullOrEmpty(scriptPath)
                && File.Exists(scriptPath))
            {
                try
                {
                    string source = File.ReadAllText(scriptPath);
                    if (source.IndexOf("#if UNITY_EDITOR", StringComparison.Ordinal) >= 0
                        || source.IndexOf("#if !UNITY_EDITOR", StringComparison.Ordinal) >= 0)
                    {
                        Debug.LogWarning(
                            $"[Scene Integrity] Advisory: scene component '{runtimeType.FullName}' on '{objectPath}' "
                            + $"contains UNITY_EDITOR conditional compilation in '{scriptPath}'. Verify serialized "
                            + "fields are not conditionally included/excluded between Editor and Player.",
                            behaviour);
                        warningCount++;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"[Scene Integrity] Could not inspect source text for '{scriptPath}': {exception.Message}",
                        behaviour);
                    warningCount++;
                }
            }

            return problems;
        }

        private static int ScanComponent(Component component)
        {
            int problems = 0;
            try
            {
                SerializedObject serialized = new SerializedObject(component);
                SerializedProperty property = serialized.GetIterator();
                bool enterChildren = true;
                while (property.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (property.propertyPath == "m_Script") continue;

                    if (HasNonFiniteValue(property))
                    {
                        Debug.LogError(
                            $"[Scene Integrity] '{GetPath(component.transform)}' component "
                            + $"'{component.GetType().Name}' has a NaN/Infinity value at serialized property "
                            + $"'{property.propertyPath}'.",
                            component);
                        problems++;
                    }

                    if (property.propertyType == SerializedPropertyType.ObjectReference
                        && property.objectReferenceValue == null
                        && property.objectReferenceInstanceIDValue != 0)
                    {
                        Debug.LogError(
                            $"[Scene Integrity] '{GetPath(component.transform)}' component "
                            + $"'{component.GetType().Name}' has a broken object reference at "
                            + $"'{property.propertyPath}' (instance ID {property.objectReferenceInstanceIDValue}).",
                            component);
                        problems++;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[Scene Integrity] Could not inspect component '{component.GetType().Name}' on "
                    + $"'{GetPath(component.transform)}': {exception.Message}",
                    component);
            }

            return problems;
        }

        private static bool HasNonFiniteValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    return !IsFinite(property.doubleValue);
                case SerializedPropertyType.Vector2:
                    return !IsFinite(property.vector2Value);
                case SerializedPropertyType.Vector3:
                    return !IsFinite(property.vector3Value);
                case SerializedPropertyType.Vector4:
                    return !IsFinite(property.vector4Value);
                case SerializedPropertyType.Quaternion:
                    return !IsFinite(property.quaternionValue);
                case SerializedPropertyType.Rect:
                    Rect rect = property.rectValue;
                    return !IsFinite(rect.x) || !IsFinite(rect.y)
                        || !IsFinite(rect.width) || !IsFinite(rect.height);
                case SerializedPropertyType.Bounds:
                    Bounds bounds = property.boundsValue;
                    return !IsFinite(bounds.center) || !IsFinite(bounds.extents);
                case SerializedPropertyType.Color:
                    Color color = property.colorValue;
                    return !IsFinite(color.r) || !IsFinite(color.g)
                        || !IsFinite(color.b) || !IsFinite(color.a);
                default:
                    return false;
            }
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Vector4 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static string GetPath(Transform transform)
        {
            if (transform == null) return "<no transform>";
            string path = transform.name;
            Transform parent = transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
}
