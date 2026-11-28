using System.Collections.Generic;
using Hanger51.EngineAssembly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class MerlinVisibleOilServiceHardwareRepairSetup
    {
        private const string ServiceRootName = "Merlin Visible Oil Service Hardware";
        private const string MaterialFolder =
            "Assets/_Project/EngineAssembly/Materials/Condition";

        private sealed class ServiceMaterials
        {
            public Material Metal;
            public Material Yellow;
            public Material Oil;
            public Material Dark;
        }

        [MenuItem("Hanger 51/Merlin Condition/7 - Rebuild Visible Dipstick and Oil Cap")]
        public static void RebuildVisibleDipstickAndOilCap()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError(
                    "Merlin Condition Step 7 failed. Exit Play mode first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError(
                    "Merlin Condition Step 7 failed. Open and save the movement-test scene first.");
                return;
            }

            EngineConditionController[] conditions =
                Object.FindObjectsByType<EngineConditionController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            if (conditions.Length == 0)
            {
                Debug.LogError(
                    "Merlin Condition Step 7 failed. Run Merlin Condition Step 1 first.");
                return;
            }

            EnsureMaterialFolder();
            ServiceMaterials materials = CreateMaterials();
            if (materials == null)
            {
                return;
            }

            int rebuilt = 0;
            GameObject selected = null;
            for (int index = 0; index < conditions.Length; index++)
            {
                EngineConditionController condition = conditions[index];
                if (condition != null && RebuildCondition(condition, materials))
                {
                    rebuilt++;
                    if (selected == null && condition.gameObject.activeInHierarchy)
                    {
                        selected = condition.gameObject;
                    }
                }
            }

            if (rebuilt == 0)
            {
                Debug.LogError(
                    "Merlin Condition Step 7 failed. No complete condition setup could be rebuilt.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError(
                    "Merlin Condition Step 7 rebuilt the service hardware but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError(
                    "Merlin Condition Step 7 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = selected != null
                ? selected
                : conditions[0].gameObject;
            Debug.Log(
                $"Merlin Condition Step 7 complete. Rebuilt clearly visible, trigger-only dipstick and oil-cap hardware on {rebuilt} engine condition setup(s), including the complete-engine shipment template.",
                Selection.activeGameObject);
        }

        [MenuItem("Hanger 51/Merlin Condition/8 - Validate Visible Dipstick and Oil Cap")]
        public static void ValidateVisibleDipstickAndOilCap()
        {
            bool passed = true;
            EngineConditionController[] conditions =
                Object.FindObjectsByType<EngineConditionController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            if (conditions.Length == 0)
            {
                Debug.LogError(
                    "Merlin Condition Step 8 failed: no condition systems exist.");
                passed = false;
            }

            for (int index = 0; index < conditions.Length; index++)
            {
                EngineConditionController condition = conditions[index];
                if (condition == null)
                {
                    continue;
                }

                ValidateCondition(condition, ref passed);
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError(
                    "Merlin Condition Step 8 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Merlin Condition Step 8 passed. Every current engine and complete-engine shipment template has one visible, trigger-only dipstick and one visible, trigger-only oil filler attached to its engine block.");
            }
        }

        private static bool RebuildCondition(
            EngineConditionController condition,
            ServiceMaterials materials)
        {
            EngineAssemblyStation station =
                condition.GetComponent<EngineAssemblyStation>();
            EngineAssemblyTransportController transport =
                condition.GetComponent<EngineAssemblyTransportController>();
            if (station == null || transport == null || transport.TransportRoot == null)
            {
                Debug.LogWarning(
                    $"Skipped '{condition.name}' because its station or portable root is missing.",
                    condition);
                return false;
            }

            SerializedObject serializedStation = new SerializedObject(station);
            GameObject engineCore = GetObject<GameObject>(
                serializedStation,
                "engineCoreVisual");
            if (engineCore == null)
            {
                Debug.LogWarning(
                    $"Skipped '{condition.name}' because its engine-block visual is missing.",
                    condition);
                return false;
            }

            RemoveExistingServiceHardware(transport.TransportRoot);

            Transform existingRoot = FindDirectChild(engineCore.transform, ServiceRootName);
            if (existingRoot != null)
            {
                Undo.DestroyObjectImmediate(existingRoot.gameObject);
            }

            Bounds blockBounds = CalculateLocalBounds(
                engineCore.transform,
                engineCore.GetComponentsInChildren<Renderer>(true));

            GameObject serviceRootObject = new GameObject(ServiceRootName);
            Undo.RegisterCreatedObjectUndo(
                serviceRootObject,
                "Create visible Merlin oil service hardware");
            Transform serviceRoot = serviceRootObject.transform;
            serviceRoot.SetParent(engineCore.transform, false);
            serviceRoot.localPosition = Vector3.zero;
            serviceRoot.localRotation = Quaternion.identity;
            serviceRoot.localScale = InverseLossyScale(engineCore.transform);

            BuildDipstick(
                serviceRoot,
                engineCore.transform,
                blockBounds,
                condition,
                materials);
            BuildFiller(
                serviceRoot,
                engineCore.transform,
                blockBounds,
                condition,
                materials);

            EditorUtility.SetDirty(serviceRootObject);
            EditorUtility.SetDirty(condition);
            EditorUtility.SetDirty(station);
            EditorUtility.SetDirty(transport);
            return true;
        }

        private static void RemoveExistingServiceHardware(Transform portableRoot)
        {
            EngineDipstickController[] dipsticks =
                portableRoot.GetComponentsInChildren<EngineDipstickController>(true);
            for (int index = dipsticks.Length - 1; index >= 0; index--)
            {
                if (dipsticks[index] != null)
                {
                    Undo.DestroyObjectImmediate(dipsticks[index].gameObject);
                }
            }

            EngineConditionInspectionTarget[] targets =
                portableRoot.GetComponentsInChildren<EngineConditionInspectionTarget>(true);
            for (int index = targets.Length - 1; index >= 0; index--)
            {
                EngineConditionInspectionTarget target = targets[index];
                if (target != null
                    && target.InspectionKind == EngineConditionInspectionKind.OilFiller)
                {
                    Undo.DestroyObjectImmediate(target.gameObject);
                }
            }
        }

        private static void BuildDipstick(
            Transform serviceRoot,
            Transform engineCore,
            Bounds bounds,
            EngineConditionController condition,
            ServiceMaterials materials)
        {
            GameObject dipstickRoot = new GameObject("Merlin Oil Dipstick Interaction");
            dipstickRoot.transform.SetParent(serviceRoot, false);
            dipstickRoot.transform.position = engineCore.TransformPoint(
                bounds.center + new Vector3(
                    -bounds.extents.x * 0.72f,
                    bounds.extents.y * 1.04f,
                    -bounds.extents.z * 0.18f));
            dipstickRoot.transform.rotation = engineCore.rotation;
            dipstickRoot.transform.localScale = Vector3.one;

            BoxCollider collider = dipstickRoot.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.center = new Vector3(0f, 0.08f, 0f);
            collider.size = new Vector3(0.34f, 0.54f, 0.34f);

            GameObject visual = new GameObject("Dipstick Visual");
            visual.transform.SetParent(dipstickRoot.transform, false);

            CreateVisualPart(
                visual.transform,
                PrimitiveType.Cylinder,
                "Dipstick Tube",
                new Vector3(0f, -0.16f, 0f),
                new Vector3(0.045f, 0.18f, 0.045f),
                Vector3.zero,
                materials.Dark);
            CreateVisualPart(
                visual.transform,
                PrimitiveType.Cylinder,
                "Dipstick Rod",
                new Vector3(0f, -0.32f, 0f),
                new Vector3(0.018f, 0.28f, 0.018f),
                Vector3.zero,
                materials.Metal);
            CreateVisualPart(
                visual.transform,
                PrimitiveType.Cylinder,
                "Yellow Dipstick Handle",
                new Vector3(0f, 0.11f, 0f),
                new Vector3(0.19f, 0.045f, 0.19f),
                new Vector3(90f, 0f, 0f),
                materials.Yellow);
            CreateVisualPart(
                visual.transform,
                PrimitiveType.Cylinder,
                "Dipstick Handle Center",
                new Vector3(0f, 0.11f, 0f),
                new Vector3(0.085f, 0.052f, 0.085f),
                new Vector3(90f, 0f, 0f),
                materials.Dark);

            GameObject stain = CreateVisualPart(
                visual.transform,
                PrimitiveType.Cube,
                "Visible Oil Level on Dipstick",
                new Vector3(0f, -0.52f, 0f),
                new Vector3(0.034f, 0.24f, 0.034f),
                Vector3.zero,
                materials.Oil);

            EngineDipstickController controller =
                dipstickRoot.AddComponent<EngineDipstickController>();
            controller.Configure(
                condition,
                visual.transform,
                stain.transform,
                Vector3.zero,
                new Vector3(0f, 0.62f, 0f),
                0.24f);

            EditorUtility.SetDirty(collider);
            EditorUtility.SetDirty(controller);
        }

        private static void BuildFiller(
            Transform serviceRoot,
            Transform engineCore,
            Bounds bounds,
            EngineConditionController condition,
            ServiceMaterials materials)
        {
            GameObject filler = new GameObject("Merlin Oil Filler Target");
            filler.transform.SetParent(serviceRoot, false);
            filler.transform.position = engineCore.TransformPoint(
                bounds.center + new Vector3(
                    bounds.extents.x * 0.62f,
                    bounds.extents.y * 1.03f,
                    -bounds.extents.z * 0.10f));
            filler.transform.rotation = engineCore.rotation;
            filler.transform.localScale = Vector3.one;

            BoxCollider collider = filler.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.center = new Vector3(0f, 0.10f, 0f);
            collider.size = new Vector3(0.44f, 0.38f, 0.44f);

            CreateVisualPart(
                filler.transform,
                PrimitiveType.Cylinder,
                "Oil Filler Neck",
                Vector3.zero,
                new Vector3(0.13f, 0.13f, 0.13f),
                Vector3.zero,
                materials.Metal);
            CreateVisualPart(
                filler.transform,
                PrimitiveType.Cylinder,
                "Yellow Oil Filler Cap",
                new Vector3(0f, 0.18f, 0f),
                new Vector3(0.21f, 0.075f, 0.21f),
                Vector3.zero,
                materials.Yellow);
            CreateVisualPart(
                filler.transform,
                PrimitiveType.Cube,
                "Oil Cap Grip Bar 1",
                new Vector3(0f, 0.26f, 0f),
                new Vector3(0.30f, 0.055f, 0.07f),
                Vector3.zero,
                materials.Dark);
            CreateVisualPart(
                filler.transform,
                PrimitiveType.Cube,
                "Oil Cap Grip Bar 2",
                new Vector3(0f, 0.26f, 0f),
                new Vector3(0.07f, 0.055f, 0.30f),
                Vector3.zero,
                materials.Dark);

            EngineConditionInspectionTarget target =
                filler.AddComponent<EngineConditionInspectionTarget>();
            target.Configure(
                condition,
                EngineConditionInspectionKind.OilFiller,
                0);

            EditorUtility.SetDirty(collider);
            EditorUtility.SetDirty(target);
        }

        private static GameObject CreateVisualPart(
            Transform parent,
            PrimitiveType primitive,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEuler,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(localEuler);
            part.transform.localScale = localScale;

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
            return part;
        }

        private static void ValidateCondition(
            EngineConditionController condition,
            ref bool passed)
        {
            EngineAssemblyStation station =
                condition.GetComponent<EngineAssemblyStation>();
            EngineAssemblyTransportController transport =
                condition.GetComponent<EngineAssemblyTransportController>();
            if (station == null || transport == null || transport.TransportRoot == null)
            {
                Debug.LogError(
                    $"Merlin Condition Step 8 failed: '{condition.name}' has no valid station or portable root.",
                    condition);
                passed = false;
                return;
            }

            SerializedObject serializedStation = new SerializedObject(station);
            GameObject engineCore = GetObject<GameObject>(
                serializedStation,
                "engineCoreVisual");
            if (engineCore == null)
            {
                Debug.LogError(
                    $"Merlin Condition Step 8 failed: '{condition.name}' has no engine-block visual.",
                    condition);
                passed = false;
                return;
            }

            Transform serviceRoot = FindDirectChild(engineCore.transform, ServiceRootName);
            EngineDipstickController[] dipsticks =
                transport.TransportRoot.GetComponentsInChildren<EngineDipstickController>(true);
            List<EngineConditionInspectionTarget> fillers =
                FindFillers(transport.TransportRoot);

            if (serviceRoot == null || !serviceRoot.gameObject.activeSelf)
            {
                Debug.LogError(
                    $"Merlin Condition Step 8 failed: '{condition.name}' is missing its visible service-hardware root.",
                    condition);
                passed = false;
            }

            if (dipsticks.Length != 1
                || !dipsticks[0].transform.IsChildOf(engineCore.transform)
                || !HasVisibleRenderers(dipsticks[0].transform, 3)
                || !ValidateTrigger(dipsticks[0].GetComponent<BoxCollider>(), 0.70f))
            {
                Debug.LogError(
                    $"Merlin Condition Step 8 failed: '{condition.name}' does not have one visible, trigger-only dipstick attached to the engine block.",
                    condition);
                passed = false;
            }

            if (fillers.Count != 1
                || !fillers[0].transform.IsChildOf(engineCore.transform)
                || !HasVisibleRenderers(fillers[0].transform, 3)
                || !ValidateTrigger(fillers[0].GetComponent<BoxCollider>(), 0.60f))
            {
                Debug.LogError(
                    $"Merlin Condition Step 8 failed: '{condition.name}' does not have one visible, trigger-only oil filler attached to the engine block.",
                    condition);
                passed = false;
            }
        }

        private static List<EngineConditionInspectionTarget> FindFillers(
            Transform portableRoot)
        {
            EngineConditionInspectionTarget[] targets =
                portableRoot.GetComponentsInChildren<EngineConditionInspectionTarget>(true);
            List<EngineConditionInspectionTarget> result =
                new List<EngineConditionInspectionTarget>();
            for (int index = 0; index < targets.Length; index++)
            {
                if (targets[index] != null
                    && targets[index].InspectionKind == EngineConditionInspectionKind.OilFiller)
                {
                    result.Add(targets[index]);
                }
            }
            return result;
        }

        private static bool HasVisibleRenderers(Transform root, int minimum)
        {
            Renderer[] renderers = root != null
                ? root.GetComponentsInChildren<Renderer>(true)
                : new Renderer[0];
            return renderers.Length >= minimum;
        }

        private static bool ValidateTrigger(
            BoxCollider collider,
            float maximumDimension)
        {
            if (collider == null || !collider.isTrigger)
            {
                return false;
            }

            Vector3 size = collider.size;
            return size.x <= maximumDimension
                && size.y <= maximumDimension
                && size.z <= maximumDimension;
        }

        private static Bounds CalculateLocalBounds(
            Transform root,
            Renderer[] renderers)
        {
            bool initialized = false;
            Bounds result = new Bounds(Vector3.zero, new Vector3(2f, 1.4f, 2.8f));
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null
                    || renderer.GetComponentInParent<EngineDipstickController>() != null)
                {
                    continue;
                }

                EngineConditionInspectionTarget inspection =
                    renderer.GetComponentInParent<EngineConditionInspectionTarget>();
                if (inspection != null
                    && inspection.InspectionKind == EngineConditionInspectionKind.OilFiller)
                {
                    continue;
                }

                Bounds worldBounds = renderer.bounds;
                Vector3 min = worldBounds.min;
                Vector3 max = worldBounds.max;
                for (int x = 0; x <= 1; x++)
                {
                    for (int y = 0; y <= 1; y++)
                    {
                        for (int z = 0; z <= 1; z++)
                        {
                            Vector3 localCorner = root.InverseTransformPoint(
                                new Vector3(
                                    x == 0 ? min.x : max.x,
                                    y == 0 ? min.y : max.y,
                                    z == 0 ? min.z : max.z));
                            if (!initialized)
                            {
                                result = new Bounds(localCorner, Vector3.zero);
                                initialized = true;
                            }
                            else
                            {
                                result.Encapsulate(localCorner);
                            }
                        }
                    }
                }
            }

            if (!initialized || result.size.sqrMagnitude < 0.01f)
            {
                result = new Bounds(Vector3.zero, new Vector3(2f, 1.4f, 2.8f));
            }
            return result;
        }

        private static Vector3 InverseLossyScale(Transform parent)
        {
            Vector3 scale = parent != null ? parent.lossyScale : Vector3.one;
            return new Vector3(
                SafeInverse(scale.x),
                SafeInverse(scale.y),
                SafeInverse(scale.z));
        }

        private static float SafeInverse(float value)
        {
            return Mathf.Abs(value) > 0.0001f ? 1f / value : 1f;
        }

        private static ServiceMaterials CreateMaterials()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            if (shader == null)
            {
                Debug.LogError(
                    "Merlin Condition Step 7 failed: no compatible Lit or Standard shader was found.");
                return null;
            }

            return new ServiceMaterials
            {
                Metal = LoadOrCreateMaterial(
                    "AircraftOilCanMetal",
                    shader,
                    new Color(0.48f, 0.52f, 0.58f, 1f),
                    0.90f,
                    0.70f),
                Yellow = LoadOrCreateMaterial(
                    "AircraftOilCanCap",
                    shader,
                    new Color(0.98f, 0.66f, 0.03f, 1f),
                    0.50f,
                    0.42f),
                Oil = LoadOrCreateMaterial(
                    "ConditionOil",
                    shader,
                    new Color(0.11f, 0.045f, 0.008f, 1f),
                    0.18f,
                    0.78f),
                Dark = LoadOrCreateMaterial(
                    "MerlinServiceHardwareDark",
                    shader,
                    new Color(0.055f, 0.060f, 0.065f, 1f),
                    0.62f,
                    0.34f)
            };
        }

        private static Material LoadOrCreateMaterial(
            string assetName,
            Shader shader,
            Color color,
            float metallic,
            float smoothness)
        {
            string path = $"{MaterialFolder}/{assetName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = assetName };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureMaterialFolder()
        {
            string parent = "Assets/_Project/EngineAssembly/Materials";
            if (!AssetDatabase.IsValidFolder(parent))
            {
                string engineFolder = "Assets/_Project/EngineAssembly";
                if (!AssetDatabase.IsValidFolder(engineFolder))
                {
                    AssetDatabase.CreateFolder("Assets/_Project", "EngineAssembly");
                }
                AssetDatabase.CreateFolder(engineFolder, "Materials");
            }
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                AssetDatabase.CreateFolder(parent, "Condition");
            }
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (child != null && child.name == name)
                {
                    return child;
                }
            }
            return null;
        }

        private static T GetObject<T>(
            SerializedObject serialized,
            string propertyName)
            where T : Object
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            return property != null
                ? property.objectReferenceValue as T
                : null;
        }
    }
}
