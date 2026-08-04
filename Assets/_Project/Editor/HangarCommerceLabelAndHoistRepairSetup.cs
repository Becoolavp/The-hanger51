using Hanger51.EngineAssembly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class HangarCommerceLabelAndHoistRepairSetup
    {
        private const string CommerceRootName = "Hanger 51 Commerce System";
        private const string MaterialFolder = "Assets/_Project/Commerce/Materials";
        private const string WorldTextMaterialPath =
            MaterialFolder + "/WorldTextDepth.mat";
        private const string WorldTextShaderName = "Hanger51/WorldTextDepth";

        [MenuItem("Hanger 51/Shop and Shipping/3 - Repair Labels and Multi-Engine Hoist")]
        public static void RepairLabelsAndMultiEngineHoist()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Shop Step 3 failed. Exit Play mode first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            GameObject commerceRoot = GameObject.Find(CommerceRootName);
            if (!scene.IsValid()
                || string.IsNullOrWhiteSpace(scene.path)
                || commerceRoot == null)
            {
                Debug.LogError(
                    "Shop Step 3 failed. Open the saved movement-test scene and run Shop Step 1 first.");
                return;
            }

            TextMesh[] worldTexts = commerceRoot.GetComponentsInChildren<TextMesh>(true);
            if (worldTexts.Length == 0)
            {
                Debug.LogError("Shop Step 3 failed. No commerce world labels were found.", commerceRoot);
                return;
            }

            Font sourceFont = FindFirstFont(worldTexts);
            Material worldTextMaterial = CreateOrRefreshWorldTextMaterial(sourceFont);
            if (worldTextMaterial == null)
            {
                Debug.LogError(
                    "Shop Step 3 failed. The depth-tested world-text shader has not compiled yet. Wait for Unity to finish importing, then run Step 3 again.");
                return;
            }

            int repairedLabels = 0;
            for (int index = 0; index < worldTexts.Length; index++)
            {
                TextMesh text = worldTexts[index];
                if (text == null)
                {
                    continue;
                }

                Undo.RecordObject(text.transform, "Repair commerce label orientation");
                Undo.RecordObject(text.GetComponent<Renderer>(), "Repair commerce label material");

                if (text.name == "Shipment Receiving Sign"
                    || text.name == "Shipping Label Text")
                {
                    text.transform.localRotation = Quaternion.identity;
                }

                Renderer renderer = text.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = worldTextMaterial;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.sortingOrder = 0;
                    EditorUtility.SetDirty(renderer);
                }

                EditorUtility.SetDirty(text.transform);
                EditorUtility.SetDirty(text);
                repairedLabels++;
            }

            EngineHoistController[] hoists = Object.FindObjectsByType<EngineHoistController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            int repairedHoists = 0;
            for (int index = 0; index < hoists.Length; index++)
            {
                EngineHoistController hoist = hoists[index];
                if (hoist == null || !hoist.gameObject.scene.IsValid())
                {
                    continue;
                }

                EngineHoistMultiEngineSelector selector =
                    hoist.GetComponent<EngineHoistMultiEngineSelector>();
                if (selector == null)
                {
                    selector = Undo.AddComponent<EngineHoistMultiEngineSelector>(
                        hoist.gameObject);
                }

                EditorUtility.SetDirty(selector);
                repairedHoists++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("Shop Step 3 made the repairs but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("Shop Step 3 repaired the scene, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = commerceRoot;
            Debug.Log(
                $"Shop Step 3 complete. Corrected {repairedLabels} depth-tested commerce labels and enabled nearest-engine selection on {repairedHoists} hoist(s).",
                commerceRoot);
        }

        [MenuItem("Hanger 51/Shop and Shipping/4 - Validate Labels and Multi-Engine Hoist")]
        public static void ValidateLabelsAndMultiEngineHoist()
        {
            bool passed = true;
            GameObject commerceRoot = GameObject.Find(CommerceRootName);
            if (commerceRoot == null)
            {
                Debug.LogError("Shop Step 4 failed: the commerce system root is missing.");
                passed = false;
            }
            else
            {
                TextMesh[] worldTexts =
                    commerceRoot.GetComponentsInChildren<TextMesh>(true);
                if (worldTexts.Length == 0)
                {
                    Debug.LogError("Shop Step 4 failed: no commerce labels were found.");
                    passed = false;
                }

                int shippingLabels = 0;
                int receivingSigns = 0;
                for (int index = 0; index < worldTexts.Length; index++)
                {
                    TextMesh text = worldTexts[index];
                    Renderer renderer = text != null
                        ? text.GetComponent<Renderer>()
                        : null;
                    Material material = renderer != null
                        ? renderer.sharedMaterial
                        : null;

                    if (material == null
                        || material.shader == null
                        || material.shader.name != WorldTextShaderName)
                    {
                        Debug.LogError(
                            $"Shop Step 4 failed: '{text?.name ?? "Unknown label"}' is not using the depth-tested world-text material.",
                            text);
                        passed = false;
                    }

                    if (text != null && text.name == "Shipping Label Text")
                    {
                        shippingLabels++;
                        if (Quaternion.Angle(text.transform.localRotation, Quaternion.identity) > 0.5f)
                        {
                            Debug.LogError(
                                "Shop Step 4 failed: a crate shipping label is still reversed.",
                                text);
                            passed = false;
                        }
                    }
                    else if (text != null && text.name == "Shipment Receiving Sign")
                    {
                        receivingSigns++;
                        if (Quaternion.Angle(text.transform.localRotation, Quaternion.identity) > 0.5f)
                        {
                            Debug.LogError(
                                "Shop Step 4 failed: the shipment receiving sign is still reversed.",
                                text);
                            passed = false;
                        }
                    }
                }

                if (shippingLabels < 1 || receivingSigns != 1)
                {
                    Debug.LogError(
                        $"Shop Step 4 failed: expected at least one crate-label template and one receiving sign; found {shippingLabels} label(s) and {receivingSigns} sign(s).");
                    passed = false;
                }
            }

            EngineHoistController[] hoists = Object.FindObjectsByType<EngineHoistController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (hoists.Length == 0)
            {
                Debug.LogError("Shop Step 4 failed: no engine hoist exists in the scene.");
                passed = false;
            }

            for (int index = 0; index < hoists.Length; index++)
            {
                if (hoists[index] != null
                    && hoists[index].GetComponent<EngineHoistMultiEngineSelector>() == null)
                {
                    Debug.LogError(
                        $"Shop Step 4 failed: '{hoists[index].name}' has no multi-engine selector.",
                        hoists[index]);
                    passed = false;
                }
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("Shop Step 4 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Shop Step 4 passed. Commerce labels face the correct direction, obey scene depth, and every engine hoist can select the nearest original or purchased Merlin assembly.");
            }
        }

        private static Font FindFirstFont(TextMesh[] worldTexts)
        {
            for (int index = 0; index < worldTexts.Length; index++)
            {
                if (worldTexts[index] != null && worldTexts[index].font != null)
                {
                    return worldTexts[index].font;
                }
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static Material CreateOrRefreshWorldTextMaterial(Font sourceFont)
        {
            EnsureFolder(MaterialFolder);
            Shader shader = Shader.Find(WorldTextShaderName);
            if (shader == null)
            {
                return null;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                WorldTextMaterialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "World Text Depth"
                };
                AssetDatabase.CreateAsset(material, WorldTextMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            Texture fontTexture = sourceFont != null
                && sourceFont.material != null
                    ? sourceFont.material.mainTexture
                    : null;
            if (fontTexture != null)
            {
                material.SetTexture("_MainTex", fontTexture);
            }

            material.SetColor("_Color", Color.white);
            material.SetFloat("_Cutoff", 0.02f);
            material.renderQueue = 2450;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)
                || folderPath == "Assets"
                || AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
            string folderName = System.IO.Path.GetFileName(folderPath);
            if (string.IsNullOrWhiteSpace(parent)
                || string.IsNullOrWhiteSpace(folderName))
            {
                return;
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
