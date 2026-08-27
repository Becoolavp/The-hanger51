using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class Hanger51OceanWaterSetup
    {
        private const string TerrainObjectName = "Hanger 51 Editable Terrain";
        private const string OceanObjectName = "Ocean Water Surface";
        private const string WaterFolder = "Assets/_Project/Environment/Water";
        private const string GeneratedWaterMaterialPath = WaterFolder + "/Hanger51OceanWater.mat";

        private const float OceanWorldSizeMeters = 18000f;
        private const float WaterHeightAboveTerrainBaseMeters = 65f;
        private const float OriginalFlatSurfaceAboveTerrainBaseMeters = 90f;

        [MenuItem("Hanger 51/Environment/7 - Add Large Ocean Below Sculptable Terrain")]
        public static void AddLargeOceanBelowTerrain()
        {
            if (!CanEdit(out Scene scene))
            {
                return;
            }

            Terrain terrain = FindTerrain();
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogError(
                    "Environment Step 7 failed. The sculptable Hanger 51 Terrain is missing. Run Environment Step 3 first.");
                return;
            }

            Material waterMaterial = FindOrCreateWaterMaterial(out string materialSource);
            if (waterMaterial == null)
            {
                Debug.LogError(
                    "Environment Step 7 failed. No imported water Material or Shader could be found. "
                    + "Make sure the Simple Water Shader asset is imported into Assets, let Unity finish importing it, then rerun Step 7.");
                return;
            }

            GameObject ocean = FindSceneObjectByExactName(OceanObjectName);
            if (ocean == null || ocean.GetComponent<MeshFilter>() == null || ocean.GetComponent<MeshRenderer>() == null)
            {
                if (ocean != null)
                {
                    Undo.DestroyObjectImmediate(ocean);
                }

                ocean = GameObject.CreatePrimitive(PrimitiveType.Plane);
                Undo.RegisterCreatedObjectUndo(ocean, "Create Hanger 51 ocean water");
                ocean.name = OceanObjectName;
            }
            else
            {
                Undo.RecordObject(ocean.transform, "Update Hanger 51 ocean water");
            }

            ocean.transform.SetParent(terrain.transform, false);
            TerrainData data = terrain.terrainData;
            ocean.transform.localPosition = new Vector3(
                data.size.x * 0.5f,
                WaterHeightAboveTerrainBaseMeters,
                data.size.z * 0.5f);
            ocean.transform.localRotation = Quaternion.identity;
            ocean.transform.localScale = new Vector3(
                OceanWorldSizeMeters / 10f,
                1f,
                OceanWorldSizeMeters / 10f);

            Collider collider = ocean.GetComponent<Collider>();
            if (collider != null)
            {
                Undo.DestroyObjectImmediate(collider);
            }

            MeshRenderer renderer = ocean.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = waterMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(ocean.transform);

            GameObjectUtility.SetStaticEditorFlags(ocean, 0);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("Environment Step 7 created the ocean but Unity could not save the scene.");
                return;
            }

            Hanger51BuildTools.PrepareCurrentSceneForBuild(false);
            Selection.activeGameObject = ocean;
            SceneView.lastActiveSceneView?.FrameSelected();

            float belowOriginalSurface = OriginalFlatSurfaceAboveTerrainBaseMeters
                - WaterHeightAboveTerrainBaseMeters;
            Debug.Log(
                $"Environment Step 7 complete. Added a {OceanWorldSizeMeters / 1000f:F0} km × {OceanWorldSizeMeters / 1000f:F0} km water surface centered under the 6 km Terrain, "
                + $"with sea level {belowOriginalSurface:F0} m below the original flat land surface. Sculpt the Terrain down through that level to expose shorelines, lakes, bays, or ocean. "
                + $"The water is visual-only with no collider and is using: {materialSource}.",
                ocean);
        }

        [MenuItem("Hanger 51/Environment/8 - Validate Terrain Island Ocean")]
        public static void ValidateTerrainIslandOcean()
        {
            bool passed = true;
            Terrain terrain = FindTerrain();
            GameObject ocean = FindSceneObjectByExactName(OceanObjectName);
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogError("Environment Step 8 failed. Hanger 51 Editable Terrain is missing.");
                return;
            }
            if (ocean == null)
            {
                Debug.LogError("Environment Step 8 failed. Ocean Water Surface is missing.");
                return;
            }

            MeshRenderer renderer = ocean.GetComponent<MeshRenderer>();
            if (renderer == null || renderer.sharedMaterial == null)
            {
                Debug.LogError("Environment Step 8 failed. Ocean water renderer or water material is missing.", ocean);
                passed = false;
            }
            if (ocean.GetComponent<Collider>() != null)
            {
                Debug.LogError("Environment Step 8 failed. Ocean water should not have a solid collider.", ocean);
                passed = false;
            }
            if (ocean.transform.parent != terrain.transform)
            {
                Debug.LogError("Environment Step 8 failed. Ocean water is not attached to the Terrain world root.", ocean);
                passed = false;
            }

            float expectedWaterY = WaterHeightAboveTerrainBaseMeters;
            if (Mathf.Abs(ocean.transform.localPosition.y - expectedWaterY) > 0.05f)
            {
                Debug.LogError(
                    $"Environment Step 8 failed. Ocean sea level is at Terrain-local Y {ocean.transform.localPosition.y:F2}; expected {expectedWaterY:F2}.",
                    ocean);
                passed = false;
            }

            float requiredOceanSize = Mathf.Max(terrain.terrainData.size.x, terrain.terrainData.size.z) * 2.5f;
            if (renderer != null)
            {
                float actualOceanSize = Mathf.Min(renderer.bounds.size.x, renderer.bounds.size.z);
                if (actualOceanSize < requiredOceanSize)
                {
                    Debug.LogError(
                        $"Environment Step 8 failed. Ocean is only about {actualOceanSize:F0} m across; expected at least {requiredOceanSize:F0} m for the island effect.",
                        ocean);
                    passed = false;
                }
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("Environment Step 8 failed. Standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                string materialPath = renderer != null && renderer.sharedMaterial != null
                    ? AssetDatabase.GetAssetPath(renderer.sharedMaterial)
                    : string.Empty;
                Debug.Log(
                    $"Environment Step 8 passed. The 6 km sculptable Terrain sits above an approximately {OceanWorldSizeMeters / 1000f:F0} km ocean, "
                    + $"sea level is {OriginalFlatSurfaceAboveTerrainBaseMeters - WaterHeightAboveTerrainBaseMeters:F0} m below the original flat surface, "
                    + $"and the water material is '{materialPath}'.",
                    ocean);
            }
        }

        private static Material FindOrCreateWaterMaterial(out string sourceDescription)
        {
            sourceDescription = string.Empty;

            Material existingGenerated = AssetDatabase.LoadAssetAtPath<Material>(GeneratedWaterMaterialPath);
            Material bestMaterial = null;
            string bestMaterialPath = string.Empty;
            int bestMaterialScore = int.MinValue;

            string[] materialGuids = AssetDatabase.FindAssets("t:Material");
            for (int index = 0; index < materialGuids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(materialGuids[index]);
                Material candidate = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (candidate == null)
                {
                    continue;
                }

                string shaderName = candidate.shader != null ? candidate.shader.name : string.Empty;
                int score = ScoreWaterAsset(path, candidate.name, shaderName);
                if (candidate == existingGenerated)
                {
                    score += 25;
                }
                if (score > bestMaterialScore)
                {
                    bestMaterialScore = score;
                    bestMaterial = candidate;
                    bestMaterialPath = path;
                }
            }

            if (bestMaterial != null && bestMaterialScore >= 100)
            {
                sourceDescription = $"water Material '{bestMaterial.name}' ({bestMaterialPath})";
                return bestMaterial;
            }

            Shader bestShader = null;
            string bestShaderPath = string.Empty;
            int bestShaderScore = int.MinValue;
            string[] shaderGuids = AssetDatabase.FindAssets("t:Shader");
            for (int index = 0; index < shaderGuids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(shaderGuids[index]);
                Shader candidate = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (candidate == null)
                {
                    continue;
                }

                int score = ScoreWaterAsset(path, candidate.name, candidate.name);
                if (score > bestShaderScore)
                {
                    bestShaderScore = score;
                    bestShader = candidate;
                    bestShaderPath = path;
                }
            }

            if (bestShader == null || bestShaderScore < 100)
            {
                return null;
            }

            EnsureFolder(WaterFolder);
            Material generated = existingGenerated;
            if (generated == null)
            {
                generated = new Material(bestShader)
                {
                    name = "Hanger 51 Ocean Water"
                };
                AssetDatabase.CreateAsset(generated, GeneratedWaterMaterialPath);
            }
            else if (generated.shader != bestShader)
            {
                generated.shader = bestShader;
                EditorUtility.SetDirty(generated);
            }

            sourceDescription = $"generated Material using water Shader '{bestShader.name}' ({bestShaderPath})";
            return generated;
        }

        private static int ScoreWaterAsset(string path, string assetName, string shaderName)
        {
            string combined = (path + " " + assetName + " " + shaderName).ToLowerInvariant();
            int score = 0;
            if (combined.Contains("water")) score += 100;
            if (combined.Contains("ocean")) score += 30;
            if (combined.Contains("simple")) score += 20;
            if (combined.Contains("sea")) score += 12;
            if (combined.Contains("lake")) score += 8;
            return score;
        }

        private static Terrain FindTerrain()
        {
            GameObject named = FindSceneObjectByExactName(TerrainObjectName);
            Terrain terrain = named != null ? named.GetComponent<Terrain>() : null;
            if (terrain != null)
            {
                return terrain;
            }
            return Object.FindFirstObjectByType<Terrain>(FindObjectsInactive.Include);
        }

        private static GameObject FindSceneObjectByExactName(string objectName)
        {
            Transform[] transforms = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < transforms.Length; index++)
            {
                Transform candidate = transforms[index];
                if (candidate != null
                    && candidate.gameObject.scene.IsValid()
                    && candidate.name.Equals(objectName, StringComparison.Ordinal))
                {
                    return candidate.gameObject;
                }
            }
            return null;
        }

        private static bool CanEdit(out Scene scene)
        {
            scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Environment Step 7 failed. Exit Play mode first.");
                return false;
            }
            if (EditorApplication.isCompiling)
            {
                Debug.LogError("Environment Step 7 failed. Wait for Unity to finish compiling and importing assets.");
                return false;
            }
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("Environment Step 7 failed. Open and save the Hanger 51 test scene first.");
                return false;
            }
            return true;
        }

        private static void EnsureFolder(string assetFolder)
        {
            string normalized = assetFolder.Replace('\\', '/');
            string[] parts = normalized.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
            {
                return;
            }

            string current = "Assets";
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }
                current = next;
            }
        }
    }
}
