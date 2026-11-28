using System.Collections.Generic;
using Hanger51.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class H51ParticleVelocityCurveRepair
    {
        [MenuItem("Hanger 51/World/Current/Repair All Particle Velocity Curves")]
        public static void Repair()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("Particle velocity repair failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            ParticleSystem[] systems = Object.FindObjectsByType<ParticleSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            int malformed = 0;
            List<string> repairedPaths = new List<string>();

            for (int index = 0; index < systems.Length; index++)
            {
                ParticleSystem system = systems[index];
                if (system == null)
                {
                    continue;
                }

                bool badVelocity =
                    H51ParticleVelocitySanitizer.HasMismatchedVelocityModes(system);
                bool badLimit =
                    H51ParticleVelocitySanitizer.HasMismatchedLimitVelocityModes(system);

                if (!badVelocity && !badLimit)
                {
                    continue;
                }

                Undo.RecordObject(system,"Repair malformed particle velocity curves");

                if (badVelocity)
                {
                    ParticleSystem.VelocityOverLifetimeModule velocity =
                        system.velocityOverLifetime;
                    velocity.enabled = false;
                }

                if (badLimit)
                {
                    ParticleSystem.LimitVelocityOverLifetimeModule limit =
                        system.limitVelocityOverLifetime;
                    limit.enabled = false;
                }

                malformed++;
                repairedPaths.Add(HierarchyPath(system.transform));
                EditorUtility.SetDirty(system);
            }

            Scene scene = SceneManager.GetActiveScene();
            if (malformed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveOpenScenes();
                AssetDatabase.SaveAssets();
            }

            Debug.Log(
                "Particle velocity repair complete. Particle systems scanned=" +
                systems.Length +
                ", malformed systems repaired=" + malformed +
                (repairedPaths.Count > 0
                    ? ". Repaired: " + string.Join(" | ",repairedPaths)
                    : ". No enabled mismatched velocity modules were found in the open scene.") +
                " Runtime sanitizer is also installed for malformed systems created later.");
        }

        [MenuItem("Hanger 51/World/Current/Validate Particle Velocity Curves")]
        public static void Validate()
        {
            ParticleSystem[] systems = Object.FindObjectsByType<ParticleSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            List<string> bad = new List<string>();
            for (int index = 0; index < systems.Length; index++)
            {
                ParticleSystem system = systems[index];
                if (system == null)
                {
                    continue;
                }

                if (H51ParticleVelocitySanitizer.HasMismatchedVelocityModes(system) ||
                    H51ParticleVelocitySanitizer.HasMismatchedLimitVelocityModes(system))
                {
                    bad.Add(HierarchyPath(system.transform));
                }
            }

            if (bad.Count > 0)
            {
                Debug.LogError(
                    "Particle velocity validation failed. Malformed systems=" +
                    bad.Count + ": " + string.Join(" | ",bad));
                return;
            }

            Debug.Log(
                "Particle velocity validation passed. Scanned " +
                systems.Length +
                " particle systems; no enabled mismatched velocity curve sets remain.");
        }

        private static string HierarchyPath(Transform target)
        {
            if (target == null)
            {
                return "(missing particle)";
            }

            string path = target.name;
            Transform parent = target.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }
    }
}
