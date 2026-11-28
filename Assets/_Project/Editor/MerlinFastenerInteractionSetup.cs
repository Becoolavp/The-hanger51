using System.Collections.Generic;
using Hanger51.EngineAssembly;
using Hanger51.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class MerlinFastenerInteractionSetup
    {
        private const string StationName = "V-1650 Engine Stand";
        private const string FastenerRootName = "Interactive Fastener System";
        private const string CoverBoltRootName = "Interactive Cover Bolts";
        private const string HighlightMaterialPath =
            "Assets/_Project/EngineAssembly/Materials/InstallHighlight.mat";
        private const string BoltMaterialPath =
            "Assets/_Project/EngineAssembly/Materials/MachinedAluminum.mat";
        private const string CoverItemPath =
            "Assets/_Project/Inventory/Items/MerlinCylinderCover.asset";
        private const string SparkPlugItemPath =
            "Assets/_Project/Inventory/Items/SparkPlug.asset";

        [MenuItem("Hanger 51/Merlin Assembly/4 - Add Highlights and Fastener Interactions")]
        public static void AddHighlightsAndFastenerInteractions()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Merlin Step 4 failed. Exit Play mode before running setup.");
                return;
            }

            EngineAssemblyStation station = FindStation();
            if (station == null)
            {
                Debug.LogError(
                    "Merlin Step 4 failed. Run Merlin Step 1 first so the V-1650 engine stand exists.");
                return;
            }

            Transform leftCover = station.transform.Find("Installed Left Cylinder Cover");
            Transform rightCover = station.transform.Find("Installed Right Cylinder Cover");
            if (leftCover == null || rightCover == null)
            {
                Debug.LogError(
                    "Merlin Step 4 failed. The generated left or right cylinder cover visual is missing.",
                    station);
                return;
            }

            InventoryItemDefinition coverItem =
                AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(CoverItemPath);
            InventoryItemDefinition sparkPlugItem =
                AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(SparkPlugItemPath);

            if (coverItem == null || sparkPlugItem == null)
            {
                Debug.LogError(
                    "Merlin Step 4 failed. The generated cover or spark-plug item asset is missing. Run Merlin Step 1.");
                return;
            }

            ConfigureItemForEquipping(
                coverItem,
                "A long cam and valve cover for one six-cylinder bank. Equip it to reveal the highlighted mounting area, then secure it with six bolts.");
            ConfigureItemForEquipping(
                sparkPlugItem,
                "A detailed aircraft spark plug. Equip it to reveal the open wells, then hold E to screw it into the cover. Two plugs are required per cylinder.");

            Material highlightMaterial = CreateOrRefreshHighlightMaterial();
            Material boltMaterial = AssetDatabase.LoadAssetAtPath<Material>(BoltMaterialPath);
            if (boltMaterial == null)
            {
                boltMaterial = highlightMaterial;
            }

            RemoveExistingFastenerObjects(station.transform, leftCover, rightCover);

            GameObject fastenerRoot = new GameObject(FastenerRootName);
            Undo.RegisterCreatedObjectUndo(fastenerRoot, "Create interactive fastener system");
            fastenerRoot.transform.SetParent(station.transform, false);

            List<EngineAssemblyInteractionTarget> coverTargets =
                new List<EngineAssemblyInteractionTarget>();
            List<EngineAssemblyInteractionTarget> boltTargets =
                new List<EngineAssemblyInteractionTarget>();
            List<EngineAssemblyInteractionTarget> sparkPlugTargets =
                new List<EngineAssemblyInteractionTarget>();

            Transform[] covers = { leftCover, rightCover };
            for (int coverIndex = 0; coverIndex < covers.Length; coverIndex++)
            {
                coverTargets.Add(CreateCoverPlacementTarget(
                    station,
                    fastenerRoot.transform,
                    covers[coverIndex],
                    coverIndex,
                    highlightMaterial));

                CreateCoverBoltTargets(
                    station,
                    covers[coverIndex],
                    coverIndex,
                    boltMaterial,
                    highlightMaterial,
                    boltTargets);
            }

            RepositionSparkPlugsAndCreateTargets(
                station,
                fastenerRoot.transform,
                covers,
                highlightMaterial,
                sparkPlugTargets);

            station.ConfigureFastenerSystem(
                coverTargets,
                boltTargets,
                sparkPlugTargets);
            station.ResetAssembly();

            EditorUtility.SetDirty(coverItem);
            EditorUtility.SetDirty(sparkPlugItem);
            EditorUtility.SetDirty(station);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Scene activeScene = SceneManager.GetActiveScene();
            if (string.IsNullOrWhiteSpace(activeScene.path)
                || !EditorSceneManager.SaveScene(activeScene))
            {
                Debug.LogError("Merlin Step 4 created the interactions but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError(
                    "Merlin Step 4 created the interactions, but build preparation failed. Run Build Step 1.");
                return;
            }

            Selection.activeGameObject = station.gameObject;
            Debug.Log(
                "Merlin Step 4 complete. Added two highlighted cover mounts, 12 screw-down cover bolts, "
                + "24 highlighted top-cover spark-plug wells, hold-E tightening animations, and prepared Build and Run.",
                station);
        }

        [MenuItem("Hanger 51/Merlin Assembly/5 - Validate Highlights and Fasteners")]
        public static void ValidateHighlightsAndFasteners()
        {
            bool passed = true;
            EngineAssemblyStation station = FindStation();

            if (station == null)
            {
                Debug.LogError("Merlin Step 5 failed: the V-1650 engine stand is missing.");
                return;
            }

            EngineAssemblyInteractionTarget[] targets =
                station.GetComponentsInChildren<EngineAssemblyInteractionTarget>(true);

            int coverTargets = CountTargets(
                targets,
                EngineAssemblyInteractionKind.CoverPlacement);
            int boltTargets = CountTargets(
                targets,
                EngineAssemblyInteractionKind.CoverBolt);
            int sparkPlugTargets = CountTargets(
                targets,
                EngineAssemblyInteractionKind.SparkPlug);

            if (coverTargets != 2)
            {
                Debug.LogError(
                    $"Merlin Step 5 failed: expected 2 cover mounting targets but found {coverTargets}.");
                passed = false;
            }

            if (boltTargets != 12 || station.RequiredCoverBolts != 12)
            {
                Debug.LogError(
                    $"Merlin Step 5 failed: expected 12 interactive cover bolts but found {boltTargets} targets and {station.RequiredCoverBolts} configured bolts.");
                passed = false;
            }

            if (sparkPlugTargets != 24 || station.RequiredSparkPlugs != 24)
            {
                Debug.LogError(
                    $"Merlin Step 5 failed: expected 24 spark-plug targets but found {sparkPlugTargets} targets and {station.RequiredSparkPlugs} installed visuals.");
                passed = false;
            }

            InventoryItemDefinition coverItem =
                AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(CoverItemPath);
            InventoryItemDefinition sparkPlugItem =
                AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(SparkPlugItemPath);

            if (coverItem == null || !coverItem.CanEquip)
            {
                Debug.LogError("Merlin Step 5 failed: the cylinder cover is not equippable.");
                passed = false;
            }

            if (sparkPlugItem == null || !sparkPlugItem.CanEquip)
            {
                Debug.LogError("Merlin Step 5 failed: the spark plug is not equippable.");
                passed = false;
            }

            for (int cylinder = 1; cylinder <= 6; cylinder++)
            {
                passed &= ValidatePlugVisual(station.transform, "Left", "Outer", cylinder);
                passed &= ValidatePlugVisual(station.transform, "Left", "Inner", cylinder);
                passed &= ValidatePlugVisual(station.transform, "Right", "Outer", cylinder);
                passed &= ValidatePlugVisual(station.transform, "Right", "Inner", cylinder);
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("Merlin Step 5 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Merlin Step 5 passed. Cover mounting highlights, 12 interactive bolts, two plugs per cylinder, "
                    + "threading animations, equipment rules, and standalone build setup are ready.");
            }
        }

        private static EngineAssemblyInteractionTarget CreateCoverPlacementTarget(
            EngineAssemblyStation station,
            Transform parent,
            Transform coverVisual,
            int coverIndex,
            Material highlightMaterial)
        {
            GameObject targetObject = new GameObject(
                coverIndex == 0 ? "Left Cover Mount Target" : "Right Cover Mount Target");
            Undo.RegisterCreatedObjectUndo(targetObject, "Create cover mounting target");
            targetObject.transform.SetParent(parent, false);
            targetObject.transform.localPosition = coverVisual.localPosition;
            targetObject.transform.localRotation = coverVisual.localRotation;

            BoxCollider collider = targetObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.28f, 0f);
            collider.size = new Vector3(0.95f, 0.62f, 3.85f);

            GameObject highlight = CreatePrimitive(
                targetObject.transform,
                PrimitiveType.Cube,
                "Highlighted Cover Mount",
                new Vector3(0f, 0.48f, 0f),
                new Vector3(0.78f, 0.055f, 3.72f),
                highlightMaterial,
                Vector3.zero);

            EngineAssemblyInteractionTarget target =
                targetObject.AddComponent<EngineAssemblyInteractionTarget>();
            target.Configure(
                station,
                EngineAssemblyInteractionKind.CoverPlacement,
                coverIndex,
                coverIndex,
                0.9f,
                highlight,
                coverVisual.gameObject,
                0.38f,
                0f);

            return target;
        }

        private static void CreateCoverBoltTargets(
            EngineAssemblyStation station,
            Transform cover,
            int coverIndex,
            Material boltMaterial,
            Material highlightMaterial,
            List<EngineAssemblyInteractionTarget> boltTargets)
        {
            GameObject boltRoot = new GameObject(CoverBoltRootName);
            Undo.RegisterCreatedObjectUndo(boltRoot, "Create interactive cover bolts");
            boltRoot.transform.SetParent(cover, false);

            float[] zPositions = { -1.18f, 0f, 1.18f };
            int boltWithinCover = 0;

            for (int sideIndex = 0; sideIndex < 2; sideIndex++)
            {
                float side = sideIndex == 0 ? -1f : 1f;

                for (int zIndex = 0; zIndex < zPositions.Length; zIndex++)
                {
                    int globalBoltIndex = coverIndex * 6 + boltWithinCover;
                    GameObject targetObject = new GameObject(
                        $"Cover {coverIndex + 1} Bolt Target {boltWithinCover + 1}");
                    Undo.RegisterCreatedObjectUndo(targetObject, "Create cover bolt target");
                    targetObject.transform.SetParent(boltRoot.transform, false);
                    targetObject.transform.localPosition =
                        new Vector3(side * 0.30f, 0.47f, zPositions[zIndex]);

                    SphereCollider collider = targetObject.AddComponent<SphereCollider>();
                    collider.radius = 0.14f;

                    GameObject boltVisual = CreatePrimitive(
                        targetObject.transform,
                        PrimitiveType.Cylinder,
                        "Interactive Bolt Head",
                        Vector3.zero,
                        new Vector3(0.065f, 0.035f, 0.065f),
                        boltMaterial,
                        Vector3.zero);

                    GameObject highlight = CreatePrimitive(
                        targetObject.transform,
                        PrimitiveType.Cylinder,
                        "Bolt Highlight Ring",
                        new Vector3(0f, 0.012f, 0f),
                        new Vector3(0.12f, 0.012f, 0.12f),
                        highlightMaterial,
                        Vector3.zero);

                    EngineAssemblyInteractionTarget target =
                        targetObject.AddComponent<EngineAssemblyInteractionTarget>();
                    target.Configure(
                        station,
                        EngineAssemblyInteractionKind.CoverBolt,
                        coverIndex,
                        globalBoltIndex,
                        0.75f,
                        highlight,
                        boltVisual,
                        0.07f,
                        2.5f);

                    boltTargets.Add(target);
                    boltWithinCover++;
                }
            }
        }

        private static void RepositionSparkPlugsAndCreateTargets(
            EngineAssemblyStation station,
            Transform targetParent,
            Transform[] covers,
            Material highlightMaterial,
            List<EngineAssemblyInteractionTarget> sparkPlugTargets)
        {
            int globalPlugIndex = 0;

            for (int cylinder = 0; cylinder < 6; cylinder++)
            {
                float coverLocalZ = -1.35f + cylinder * 0.54f;

                for (int bankIndex = 0; bankIndex < 2; bankIndex++)
                {
                    string bankName = bankIndex == 0 ? "Left" : "Right";
                    Transform cover = covers[bankIndex];

                    for (int plugInCylinder = 0; plugInCylinder < 2; plugInCylinder++)
                    {
                        string plugName = plugInCylinder == 0 ? "Outer" : "Inner";
                        Transform plugVisual = station.transform.Find(
                            $"Installed {bankName} {plugName} Spark Plug {cylinder + 1}");

                        if (plugVisual == null)
                        {
                            Debug.LogError(
                                $"Could not find Installed {bankName} {plugName} Spark Plug {cylinder + 1}.",
                                station);
                            continue;
                        }

                        float coverLocalX = plugInCylinder == 0 ? -0.16f : 0.16f;
                        Vector3 worldPosition = cover.TransformPoint(
                            new Vector3(coverLocalX, 0.50f, coverLocalZ));

                        plugVisual.position = worldPosition;
                        plugVisual.rotation = cover.rotation;

                        GameObject targetObject = new GameObject(
                            $"{bankName} Cylinder {cylinder + 1} {plugName} Plug Target");
                        Undo.RegisterCreatedObjectUndo(targetObject, "Create spark-plug target");
                        targetObject.transform.SetParent(targetParent, false);
                        targetObject.transform.position = plugVisual.position;
                        targetObject.transform.rotation = plugVisual.rotation;

                        BoxCollider collider = targetObject.AddComponent<BoxCollider>();
                        collider.center = new Vector3(0f, 0.08f, 0f);
                        collider.size = new Vector3(0.29f, 0.36f, 0.29f);

                        GameObject highlight = CreatePrimitive(
                            targetObject.transform,
                            PrimitiveType.Cylinder,
                            "Spark Plug Well Highlight",
                            new Vector3(0f, 0.018f, 0f),
                            new Vector3(0.135f, 0.014f, 0.135f),
                            highlightMaterial,
                            Vector3.zero);

                        EngineAssemblyInteractionTarget target =
                            targetObject.AddComponent<EngineAssemblyInteractionTarget>();
                        target.Configure(
                            station,
                            EngineAssemblyInteractionKind.SparkPlug,
                            bankIndex,
                            globalPlugIndex,
                            1.15f,
                            highlight,
                            plugVisual.gameObject,
                            0.30f,
                            3.5f);

                        sparkPlugTargets.Add(target);
                        globalPlugIndex++;
                    }
                }
            }
        }

        private static GameObject CreatePrimitive(
            Transform parent,
            PrimitiveType primitiveType,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            Vector3 localEulerAngles)
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType);
            Undo.RegisterCreatedObjectUndo(part, $"Create {objectName}");
            part.name = objectName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(localEulerAngles);
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

        private static Material CreateOrRefreshHighlightMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(HighlightMaterialPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "InstallHighlight"
                };
                AssetDatabase.CreateAsset(material, HighlightMaterialPath);
            }
            else if (shader != null)
            {
                material.shader = shader;
            }

            Color highlightColor = new Color(1f, 0.72f, 0.05f, 0.48f);
            material.color = highlightColor;

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", highlightColor);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", highlightColor);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(2.4f, 1.25f, 0.08f, 1f));
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = 3000;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureItemForEquipping(
            InventoryItemDefinition item,
            string description)
        {
            SerializedObject serializedItem = new SerializedObject(item);
            SerializedProperty canEquip = serializedItem.FindProperty("canEquip");
            SerializedProperty descriptionProperty = serializedItem.FindProperty("description");

            if (canEquip != null)
            {
                canEquip.boolValue = true;
            }

            if (descriptionProperty != null)
            {
                descriptionProperty.stringValue = description;
            }

            serializedItem.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RemoveExistingFastenerObjects(
            Transform station,
            Transform leftCover,
            Transform rightCover)
        {
            Transform existingRoot = station.Find(FastenerRootName);
            if (existingRoot != null)
            {
                Undo.DestroyObjectImmediate(existingRoot.gameObject);
            }

            RemoveChildIfPresent(leftCover, CoverBoltRootName);
            RemoveChildIfPresent(rightCover, CoverBoltRootName);
        }

        private static void RemoveChildIfPresent(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        private static EngineAssemblyStation FindStation()
        {
            GameObject stationObject = GameObject.Find(StationName);
            return stationObject != null
                ? stationObject.GetComponent<EngineAssemblyStation>()
                : null;
        }

        private static int CountTargets(
            EngineAssemblyInteractionTarget[] targets,
            EngineAssemblyInteractionKind kind)
        {
            int count = 0;
            for (int index = 0; index < targets.Length; index++)
            {
                if (targets[index].InteractionKind == kind)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool ValidatePlugVisual(
            Transform station,
            string bank,
            string plugLocation,
            int cylinder)
        {
            Transform plug = station.Find(
                $"Installed {bank} {plugLocation} Spark Plug {cylinder}");
            if (plug != null)
            {
                return true;
            }

            Debug.LogError(
                $"Merlin Step 5 failed: missing Installed {bank} {plugLocation} Spark Plug {cylinder}.");
            return false;
        }
    }
}
