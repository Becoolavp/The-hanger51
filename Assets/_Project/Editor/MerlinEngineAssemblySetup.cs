using System.Collections.Generic;
using System.IO;
using Hanger51.EngineAssembly;
using Hanger51.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hanger51.EditorTools
{
    public static class MerlinEngineAssemblySetup
    {
        private const string GeneratedRootName = "V-1650 Assembly Test";
        private const string StationName = "V-1650 Engine Stand";
        private const string InventoryUiName = "Inventory UI";

        private const string EngineFolder = "Assets/_Project/EngineAssembly";
        private const string PrefabFolder = EngineFolder + "/Prefabs";
        private const string MaterialFolder = EngineFolder + "/Materials";
        private const string ItemFolder = "Assets/_Project/Inventory/Items";

        private const string SparkPlugPrefabPath = PrefabFolder + "/V1650SparkPlug.prefab";
        private const string CylinderCoverPrefabPath = PrefabFolder + "/V1650CylinderCover.prefab";
        private const string EngineBlockPrefabPath = PrefabFolder + "/V1650EngineBlock.prefab";

        private const string SparkPlugItemPath = ItemFolder + "/SparkPlug.asset";
        private const string CylinderCoverItemPath = ItemFolder + "/MerlinCylinderCover.asset";
        private const string EngineBlockItemPath = ItemFolder + "/MerlinEngineBlock.asset";

        private sealed class GeneratedMaterials
        {
            public Material EngineBlack;
            public Material DarkSteel;
            public Material Aluminum;
            public Material Ceramic;
            public Material Brass;
            public Material Copper;
            public Material Rubber;
            public Material StandSteel;
            public Material Wood;
        }

        [MenuItem("Hanger 51/Merlin Assembly/1 - Install or Refresh V-1650 Assembly")]
        public static void InstallOrRefreshAssembly()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Merlin Step 1 failed. Exit Play mode before running setup.");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || string.IsNullOrWhiteSpace(activeScene.path))
            {
                Debug.LogError("Merlin Step 1 failed. Open and save the movement test scene first.");
                return;
            }

            EnsureFolder(EngineFolder);
            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(ItemFolder);

            GeneratedMaterials materials = CreateOrRefreshMaterials();
            GameObject sparkPlugPrefab = CreateSparkPlugPrefab(materials);
            GameObject cylinderCoverPrefab = CreateCylinderCoverPrefab(materials);
            GameObject engineBlockPrefab = CreateEngineBlockPrefab(materials);

            InventoryItemDefinition sparkPlugItem = CreateOrUpdateItem(
                SparkPlugItemPath,
                "spark-plug",
                "V-1650 Spark Plug",
                "A detailed 14 mm aircraft spark plug for the Merlin-style V-12. The engine requires two plugs per cylinder.",
                24,
                new Color(0.78f, 0.79f, 0.82f, 1f),
                true,
                sparkPlugPrefab);

            InventoryItemDefinition cylinderCoverItem = CreateOrUpdateItem(
                CylinderCoverItemPath,
                "merlin-cylinder-cover",
                "V-1650 Cylinder Cover",
                "A long cam and valve cover for one six-cylinder bank of the Merlin-style engine.",
                2,
                new Color(0.12f, 0.13f, 0.15f, 1f),
                false,
                cylinderCoverPrefab);

            InventoryItemDefinition engineBlockItem = CreateOrUpdateItem(
                EngineBlockItemPath,
                "merlin-engine-block",
                "Merlin V-1650 Engine Block",
                "A Packard Merlin-inspired 60-degree, liquid-cooled V-12 engine assembly without its removable bank covers and spark plugs.",
                1,
                new Color(0.09f, 0.1f, 0.12f, 1f),
                false,
                engineBlockPrefab);

            EnsureInventoryEquipmentUi();
            ConfigureInstallUi();
            CreateSceneAssembly(
                materials,
                engineBlockPrefab,
                cylinderCoverPrefab,
                sparkPlugPrefab,
                engineBlockItem,
                cylinderCoverItem,
                sparkPlugItem);

            EditorSceneManager.MarkSceneDirty(activeScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!EditorSceneManager.SaveScene(activeScene))
            {
                Debug.LogError("Merlin Step 1 created the assembly but could not save the active scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("Merlin Step 1 created the assembly, but build preparation failed. Run Build Step 1.");
                return;
            }

            GameObject station = GameObject.Find(StationName);
            Selection.activeGameObject = station;

            Debug.Log(
                "Merlin Step 1 complete. Created a detailed V-1650-style engine block, two bank covers, "
                + "24 separate spark plugs, the engine stand, the Install inventory action, and prepared the scene for Build and Run.");
        }

        [MenuItem("Hanger 51/Merlin Assembly/2 - Validate V-1650 Assembly")]
        public static void ValidateAssembly()
        {
            bool passed = true;

            GameObject root = GameObject.Find(GeneratedRootName);
            if (root == null)
            {
                Debug.LogError($"Merlin Step 2 failed: '{GeneratedRootName}' is missing.");
                passed = false;
            }

            EngineAssemblyStation station = root != null
                ? root.GetComponentInChildren<EngineAssemblyStation>(true)
                : null;

            if (station == null)
            {
                Debug.LogError("Merlin Step 2 failed: EngineAssemblyStation is missing.");
                passed = false;
            }
            else
            {
                if (station.RequiredCylinderCovers != 2)
                {
                    Debug.LogError(
                        $"Merlin Step 2 failed: expected 2 installed cover visuals, found {station.RequiredCylinderCovers}.");
                    passed = false;
                }

                if (station.RequiredSparkPlugs != 24)
                {
                    Debug.LogError(
                        $"Merlin Step 2 failed: expected 24 installed spark plug visuals, found {station.RequiredSparkPlugs}.");
                    passed = false;
                }
            }

            ValidatePickupCounts(root, ref passed);
            ValidateItemAsset(SparkPlugItemPath, ref passed);
            ValidateItemAsset(CylinderCoverItemPath, ref passed);
            ValidateItemAsset(EngineBlockItemPath, ref passed);

            GameObject inventoryUiObject = GameObject.Find(InventoryUiName);
            InventoryUI inventoryUi = inventoryUiObject != null
                ? inventoryUiObject.GetComponent<InventoryUI>()
                : null;

            if (inventoryUi == null)
            {
                Debug.LogError("Merlin Step 2 failed: Inventory UI is missing.");
                passed = false;
            }

            Transform installButton = inventoryUiObject != null
                ? inventoryUiObject.transform.Find("Inventory Panel/Selected Item Panel/Install Button")
                : null;

            if (installButton == null)
            {
                Debug.LogError("Merlin Step 2 failed: the inventory Install button is missing.");
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("Merlin Step 2 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Merlin Step 2 passed. Engine stand, detailed parts, 27 separate pickups, Install UI, "
                    + "assembly sequence, and standalone build setup are ready.");
            }
        }

        [MenuItem("Hanger 51/Merlin Assembly/3 - Reset Assembly and Respawn Parts")]
        public static void ResetAssemblyAndRespawnParts()
        {
            InstallOrRefreshAssembly();
        }

        private static GeneratedMaterials CreateOrRefreshMaterials()
        {
            return new GeneratedMaterials
            {
                EngineBlack = CreateMaterial(
                    MaterialFolder + "/EngineBlack.mat",
                    new Color(0.045f, 0.052f, 0.062f, 1f),
                    0.72f,
                    0.46f),
                DarkSteel = CreateMaterial(
                    MaterialFolder + "/DarkSteel.mat",
                    new Color(0.17f, 0.18f, 0.2f, 1f),
                    0.88f,
                    0.62f),
                Aluminum = CreateMaterial(
                    MaterialFolder + "/MachinedAluminum.mat",
                    new Color(0.67f, 0.7f, 0.74f, 1f),
                    0.9f,
                    0.78f),
                Ceramic = CreateMaterial(
                    MaterialFolder + "/SparkPlugCeramic.mat",
                    new Color(0.93f, 0.92f, 0.86f, 1f),
                    0.02f,
                    0.32f),
                Brass = CreateMaterial(
                    MaterialFolder + "/BrassHardware.mat",
                    new Color(0.48f, 0.31f, 0.09f, 1f),
                    0.82f,
                    0.6f),
                Copper = CreateMaterial(
                    MaterialFolder + "/CopperLines.mat",
                    new Color(0.42f, 0.16f, 0.07f, 1f),
                    0.76f,
                    0.55f),
                Rubber = CreateMaterial(
                    MaterialFolder + "/Rubber.mat",
                    new Color(0.025f, 0.025f, 0.028f, 1f),
                    0.05f,
                    0.22f),
                StandSteel = CreateMaterial(
                    MaterialFolder + "/StandSteel.mat",
                    new Color(0.12f, 0.17f, 0.19f, 1f),
                    0.76f,
                    0.4f),
                Wood = CreateMaterial(
                    MaterialFolder + "/PalletWood.mat",
                    new Color(0.3f, 0.17f, 0.07f, 1f),
                    0.02f,
                    0.3f)
            };
        }

        private static GameObject CreateSparkPlugPrefab(GeneratedMaterials materials)
        {
            GameObject root = new GameObject("V-1650 Spark Plug");

            CreatePart(root.transform, PrimitiveType.Cylinder, "Threaded Shell",
                new Vector3(0f, 0.075f, 0f), new Vector3(0.055f, 0.075f, 0.055f), materials.DarkSteel);

            for (int index = 0; index < 6; index++)
            {
                CreatePart(root.transform, PrimitiveType.Cylinder, $"Thread Ring {index + 1}",
                    new Vector3(0f, 0.025f + index * 0.018f, 0f),
                    new Vector3(0.061f, 0.004f, 0.061f), materials.Aluminum);
            }

            CreatePart(root.transform, PrimitiveType.Cylinder, "Copper Gasket",
                new Vector3(0f, 0.155f, 0f), new Vector3(0.073f, 0.008f, 0.073f), materials.Copper);
            CreatePart(root.transform, PrimitiveType.Cylinder, "Hex Shell",
                new Vector3(0f, 0.19f, 0f), new Vector3(0.086f, 0.035f, 0.086f), materials.Aluminum);
            CreatePart(root.transform, PrimitiveType.Cylinder, "Lower Ceramic",
                new Vector3(0f, 0.255f, 0f), new Vector3(0.052f, 0.055f, 0.052f), materials.Ceramic);
            CreatePart(root.transform, PrimitiveType.Cylinder, "Upper Ceramic",
                new Vector3(0f, 0.345f, 0f), new Vector3(0.041f, 0.055f, 0.041f), materials.Ceramic);

            for (int index = 0; index < 4; index++)
            {
                CreatePart(root.transform, PrimitiveType.Cylinder, $"Insulator Rib {index + 1}",
                    new Vector3(0f, 0.265f + index * 0.03f, 0f),
                    new Vector3(0.061f, 0.007f, 0.061f), materials.Ceramic);
            }

            CreatePart(root.transform, PrimitiveType.Cylinder, "Terminal Stud",
                new Vector3(0f, 0.415f, 0f), new Vector3(0.024f, 0.025f, 0.024f), materials.DarkSteel);
            CreatePart(root.transform, PrimitiveType.Cylinder, "Terminal Nut",
                new Vector3(0f, 0.455f, 0f), new Vector3(0.044f, 0.016f, 0.044f), materials.Aluminum);

            CreatePart(root.transform, PrimitiveType.Cylinder, "Center Electrode",
                new Vector3(0f, -0.015f, 0f), new Vector3(0.012f, 0.025f, 0.012f), materials.Brass);
            CreatePart(root.transform, PrimitiveType.Cube, "Ground Electrode Stem",
                new Vector3(0.043f, -0.01f, 0f), new Vector3(0.018f, 0.06f, 0.022f), materials.DarkSteel);
            CreatePart(root.transform, PrimitiveType.Cube, "Ground Electrode Tip",
                new Vector3(0.023f, -0.035f, 0f), new Vector3(0.055f, 0.016f, 0.022f), materials.DarkSteel);

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.21f, 0f);
            collider.size = new Vector3(0.2f, 0.52f, 0.2f);

            GameObject prefab = SavePrefab(root, SparkPlugPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateCylinderCoverPrefab(GeneratedMaterials materials)
        {
            GameObject root = new GameObject("V-1650 Cylinder Cover");

            CreatePart(root.transform, PrimitiveType.Cube, "Lower Flange",
                new Vector3(0f, 0.07f, 0f), new Vector3(0.7f, 0.14f, 3.55f), materials.Aluminum);
            CreatePart(root.transform, PrimitiveType.Cube, "Painted Cover Body",
                new Vector3(0f, 0.25f, 0f), new Vector3(0.58f, 0.32f, 3.35f), materials.EngineBlack);
            CreatePart(root.transform, PrimitiveType.Cylinder, "Front Rounded Cap",
                new Vector3(0f, 0.25f, 1.68f), new Vector3(0.29f, 0.16f, 0.29f), materials.EngineBlack);
            CreatePart(root.transform, PrimitiveType.Cylinder, "Rear Rounded Cap",
                new Vector3(0f, 0.25f, -1.68f), new Vector3(0.29f, 0.16f, 0.29f), materials.EngineBlack);

            for (int index = 0; index < 6; index++)
            {
                float z = -1.35f + index * 0.54f;
                CreatePart(root.transform, PrimitiveType.Cube, $"Top Rib {index + 1}",
                    new Vector3(0f, 0.43f, z), new Vector3(0.48f, 0.035f, 0.045f), materials.DarkSteel);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                for (int index = 0; index < 7; index++)
                {
                    float z = -1.5f + index * 0.5f;
                    CreatePart(root.transform, PrimitiveType.Cylinder,
                        $"Flange Bolt {side} {index + 1}",
                        new Vector3(side * 0.29f, 0.18f, z),
                        new Vector3(0.035f, 0.025f, 0.035f), materials.Aluminum);
                }
            }

            CreatePart(root.transform, PrimitiveType.Cube, "Center Identification Plate",
                new Vector3(0f, 0.43f, 0f), new Vector3(0.32f, 0.025f, 0.7f), materials.Brass);

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.23f, 0f);
            collider.size = new Vector3(0.78f, 0.5f, 3.75f);

            GameObject prefab = SavePrefab(root, CylinderCoverPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateEngineBlockPrefab(GeneratedMaterials materials)
        {
            GameObject root = new GameObject("Merlin V-1650 Engine Block");

            CreatePart(root.transform, PrimitiveType.Cube, "Lower Crankcase",
                new Vector3(0f, 0.65f, 0f), new Vector3(1.38f, 0.78f, 3.75f), materials.EngineBlack);
            CreatePart(root.transform, PrimitiveType.Cube, "Oil Sump",
                new Vector3(0f, 0.2f, 0f), new Vector3(1.0f, 0.42f, 3.15f), materials.DarkSteel);
            CreatePart(root.transform, PrimitiveType.Cube, "Left Crankcase Rail",
                new Vector3(-0.72f, 0.65f, 0f), new Vector3(0.12f, 0.32f, 3.9f), materials.Aluminum);
            CreatePart(root.transform, PrimitiveType.Cube, "Right Crankcase Rail",
                new Vector3(0.72f, 0.65f, 0f), new Vector3(0.12f, 0.32f, 3.9f), materials.Aluminum);

            for (int bank = -1; bank <= 1; bank += 2)
            {
                string bankName = bank < 0 ? "Left Bank" : "Right Bank";
                GameObject bankRoot = new GameObject(bankName);
                bankRoot.transform.SetParent(root.transform, false);
                bankRoot.transform.localPosition = new Vector3(bank * 0.5f, 1.15f, 0f);
                bankRoot.transform.localRotation = Quaternion.Euler(0f, 0f, bank * -30f);

                CreatePart(bankRoot.transform, PrimitiveType.Cube, "Cylinder Jacket",
                    Vector3.zero, new Vector3(0.72f, 0.72f, 3.55f), materials.EngineBlack);
                CreatePart(bankRoot.transform, PrimitiveType.Cube, "Head Deck",
                    new Vector3(0f, 0.42f, 0f), new Vector3(0.76f, 0.2f, 3.65f), materials.Aluminum);

                for (int cylinder = 0; cylinder < 6; cylinder++)
                {
                    float z = -1.45f + cylinder * 0.58f;
                    CreatePart(bankRoot.transform, PrimitiveType.Cylinder,
                        $"Cylinder Barrel {cylinder + 1}",
                        new Vector3(0f, 0.02f, z),
                        new Vector3(0.31f, 0.34f, 0.31f), materials.DarkSteel);
                    CreatePart(bankRoot.transform, PrimitiveType.Cylinder,
                        $"Cylinder Head Boss {cylinder + 1}",
                        new Vector3(0f, 0.47f, z),
                        new Vector3(0.34f, 0.09f, 0.34f), materials.Aluminum);
                }
            }

            CreatePart(root.transform, PrimitiveType.Cube, "Central Intake Manifold",
                new Vector3(0f, 1.55f, 0f), new Vector3(0.48f, 0.32f, 2.8f), materials.DarkSteel);

            for (int index = 0; index < 6; index++)
            {
                float z = -1.38f + index * 0.55f;
                CreatePart(root.transform, PrimitiveType.Cylinder, $"Intake Runner Left {index + 1}",
                    new Vector3(-0.34f, 1.5f, z), new Vector3(0.07f, 0.33f, 0.07f), materials.Aluminum,
                    new Vector3(0f, 0f, 58f));
                CreatePart(root.transform, PrimitiveType.Cylinder, $"Intake Runner Right {index + 1}",
                    new Vector3(0.34f, 1.5f, z), new Vector3(0.07f, 0.33f, 0.07f), materials.Aluminum,
                    new Vector3(0f, 0f, -58f));
            }

            CreatePart(root.transform, PrimitiveType.Cylinder, "Front Reduction Gear Housing",
                new Vector3(0f, 0.88f, 2.15f), new Vector3(0.86f, 0.35f, 0.86f), materials.EngineBlack,
                new Vector3(90f, 0f, 0f));
            CreatePart(root.transform, PrimitiveType.Cylinder, "Front Gear Cover",
                new Vector3(0f, 0.88f, 2.5f), new Vector3(0.68f, 0.11f, 0.68f), materials.Aluminum,
                new Vector3(90f, 0f, 0f));
            CreatePart(root.transform, PrimitiveType.Cylinder, "Propeller Shaft",
                new Vector3(0f, 0.88f, 2.82f), new Vector3(0.18f, 0.28f, 0.18f), materials.DarkSteel,
                new Vector3(90f, 0f, 0f));

            CreatePart(root.transform, PrimitiveType.Sphere, "Rear Supercharger Housing",
                new Vector3(0f, 0.9f, -2.15f), new Vector3(1.0f, 0.92f, 0.72f), materials.EngineBlack);
            CreatePart(root.transform, PrimitiveType.Cylinder, "Supercharger Intake",
                new Vector3(0f, 1.05f, -2.72f), new Vector3(0.42f, 0.32f, 0.42f), materials.Aluminum,
                new Vector3(90f, 0f, 0f));
            CreatePart(root.transform, PrimitiveType.Cylinder, "Supercharger Intake Lip",
                new Vector3(0f, 1.05f, -3.0f), new Vector3(0.48f, 0.05f, 0.48f), materials.DarkSteel,
                new Vector3(90f, 0f, 0f));

            for (int bank = -1; bank <= 1; bank += 2)
            {
                for (int cylinder = 0; cylinder < 6; cylinder++)
                {
                    float z = -1.42f + cylinder * 0.57f;
                    CreatePart(root.transform, PrimitiveType.Cylinder,
                        $"Exhaust Stub {bank} {cylinder + 1}",
                        new Vector3(bank * 1.04f, 1.26f, z),
                        new Vector3(0.105f, 0.28f, 0.105f), materials.Aluminum,
                        new Vector3(0f, 0f, 90f));
                    CreatePart(root.transform, PrimitiveType.Cylinder,
                        $"Exhaust Flange {bank} {cylinder + 1}",
                        new Vector3(bank * 0.82f, 1.26f, z),
                        new Vector3(0.15f, 0.025f, 0.15f), materials.DarkSteel,
                        new Vector3(0f, 0f, 90f));
                }

                CreatePart(root.transform, PrimitiveType.Cylinder,
                    bank < 0 ? "Left Ignition Rail" : "Right Ignition Rail",
                    new Vector3(bank * 1.12f, 1.65f, 0f),
                    new Vector3(0.035f, 1.72f, 0.035f), materials.Copper,
                    new Vector3(90f, 0f, 0f));
            }

            CreatePart(root.transform, PrimitiveType.Cylinder, "Front Coolant Crossover",
                new Vector3(0f, 1.72f, 1.78f), new Vector3(0.09f, 0.72f, 0.09f), materials.Aluminum,
                new Vector3(0f, 0f, 90f));
            CreatePart(root.transform, PrimitiveType.Sphere, "Left Magneto",
                new Vector3(-0.42f, 1.72f, -1.9f), new Vector3(0.28f, 0.23f, 0.28f), materials.DarkSteel);
            CreatePart(root.transform, PrimitiveType.Sphere, "Right Magneto",
                new Vector3(0.42f, 1.72f, -1.9f), new Vector3(0.28f, 0.23f, 0.28f), materials.DarkSteel);

            for (int side = -1; side <= 1; side += 2)
            {
                for (int index = 0; index < 10; index++)
                {
                    float z = -1.72f + index * 0.38f;
                    CreatePart(root.transform, PrimitiveType.Sphere,
                        $"Crankcase Bolt {side} {index + 1}",
                        new Vector3(side * 0.74f, 0.72f, z),
                        Vector3.one * 0.045f, materials.Aluminum);
                }
            }

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.95f, 0f);
            collider.size = new Vector3(2.65f, 2.15f, 6.15f);

            GameObject prefab = SavePrefab(root, EngineBlockPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CreateSceneAssembly(
            GeneratedMaterials materials,
            GameObject engineBlockPrefab,
            GameObject cylinderCoverPrefab,
            GameObject sparkPlugPrefab,
            InventoryItemDefinition engineBlockItem,
            InventoryItemDefinition cylinderCoverItem,
            InventoryItemDefinition sparkPlugItem)
        {
            GameObject existingRoot = GameObject.Find(GeneratedRootName);
            if (existingRoot != null)
            {
                Object.DestroyImmediate(existingRoot);
            }

            GameObject root = new GameObject(GeneratedRootName);

            CreateDisplayPad(root.transform, "Engine Block Pallet",
                new Vector3(-6.4f, 0.12f, 4.8f), new Vector3(3.3f, 0.24f, 6.8f), materials.Wood);
            CreateDisplayPad(root.transform, "Cylinder Cover Pad",
                new Vector3(-3.3f, 0.08f, 5.5f), new Vector3(1.8f, 0.16f, 5.0f), materials.Wood);
            CreateDisplayPad(root.transform, "Spark Plug Tray",
                new Vector3(4.5f, 0.06f, 5.4f), new Vector3(4.6f, 0.12f, 3.5f), materials.DarkSteel);

            GameObject enginePickup = CreatePickup(
                engineBlockPrefab,
                root.transform,
                "Merlin V-1650 Engine Block Pickup",
                engineBlockItem,
                new Vector3(-6.4f, 0.24f, 4.8f),
                Quaternion.Euler(0f, 0f, 0f));
            AlignBottomToY(enginePickup, 0.24f);

            GameObject coverPickupLeft = CreatePickup(
                cylinderCoverPrefab,
                root.transform,
                "V-1650 Cylinder Cover Pickup A",
                cylinderCoverItem,
                new Vector3(-3.3f, 0.16f, 4.5f),
                Quaternion.identity);
            AlignBottomToY(coverPickupLeft, 0.16f);

            GameObject coverPickupRight = CreatePickup(
                cylinderCoverPrefab,
                root.transform,
                "V-1650 Cylinder Cover Pickup B",
                cylinderCoverItem,
                new Vector3(-3.3f, 0.16f, 6.5f),
                Quaternion.identity);
            AlignBottomToY(coverPickupRight, 0.16f);

            for (int index = 0; index < 24; index++)
            {
                int row = index / 6;
                int column = index % 6;
                Vector3 position = new Vector3(
                    3.25f + column * 0.5f,
                    0.12f,
                    4.65f + row * 0.5f);

                GameObject sparkPlugPickup = CreatePickup(
                    sparkPlugPrefab,
                    root.transform,
                    $"V-1650 Spark Plug Pickup {index + 1:00}",
                    sparkPlugItem,
                    position,
                    Quaternion.Euler(0f, index % 2 == 0 ? 0f : 25f, 0f));
                AlignBottomToY(sparkPlugPickup, 0.12f);
            }

            CreateEngineStation(
                root.transform,
                materials,
                engineBlockPrefab,
                cylinderCoverPrefab,
                sparkPlugPrefab,
                engineBlockItem,
                cylinderCoverItem,
                sparkPlugItem);
        }

        private static void CreateEngineStation(
            Transform parent,
            GeneratedMaterials materials,
            GameObject engineBlockPrefab,
            GameObject cylinderCoverPrefab,
            GameObject sparkPlugPrefab,
            InventoryItemDefinition engineBlockItem,
            InventoryItemDefinition cylinderCoverItem,
            InventoryItemDefinition sparkPlugItem)
        {
            GameObject stationObject = new GameObject(StationName);
            stationObject.transform.SetParent(parent, false);
            stationObject.transform.position = new Vector3(0f, 0f, 9f);

            CreateStandVisual(stationObject.transform, materials);

            BoxCollider stationCollider = stationObject.AddComponent<BoxCollider>();
            stationCollider.center = new Vector3(0f, 1.25f, 0f);
            stationCollider.size = new Vector3(3.4f, 2.5f, 6.5f);

            GameObject installedEngine = PrefabUtility.InstantiatePrefab(
                engineBlockPrefab,
                stationObject.transform) as GameObject;
            installedEngine.name = "Installed Engine Core";
            installedEngine.transform.localPosition = new Vector3(0f, 0.78f, 0f);
            installedEngine.transform.localRotation = Quaternion.identity;
            DisableAllColliders(installedEngine);

            List<GameObject> coverVisuals = new List<GameObject>();
            for (int bank = -1; bank <= 1; bank += 2)
            {
                GameObject cover = PrefabUtility.InstantiatePrefab(
                    cylinderCoverPrefab,
                    stationObject.transform) as GameObject;
                cover.name = bank < 0 ? "Installed Left Cylinder Cover" : "Installed Right Cylinder Cover";
                cover.transform.localPosition = new Vector3(bank * 0.68f, 2.45f, 0f);
                cover.transform.localRotation = Quaternion.Euler(0f, 0f, bank * -30f);
                DisableAllColliders(cover);
                coverVisuals.Add(cover);
            }

            List<GameObject> sparkPlugVisuals = new List<GameObject>();
            for (int cylinder = 0; cylinder < 6; cylinder++)
            {
                float z = -1.43f + cylinder * 0.57f;

                for (int bank = -1; bank <= 1; bank += 2)
                {
                    GameObject outerPlug = PrefabUtility.InstantiatePrefab(
                        sparkPlugPrefab,
                        stationObject.transform) as GameObject;
                    outerPlug.name = $"Installed {(bank < 0 ? "Left" : "Right")} Outer Spark Plug {cylinder + 1}";
                    outerPlug.transform.localPosition = new Vector3(bank * 1.05f, 1.92f, z);
                    outerPlug.transform.localRotation = Quaternion.Euler(0f, 0f, bank * -58f);
                    DisableAllColliders(outerPlug);
                    sparkPlugVisuals.Add(outerPlug);

                    GameObject innerPlug = PrefabUtility.InstantiatePrefab(
                        sparkPlugPrefab,
                        stationObject.transform) as GameObject;
                    innerPlug.name = $"Installed {(bank < 0 ? "Left" : "Right")} Inner Spark Plug {cylinder + 1}";
                    innerPlug.transform.localPosition = new Vector3(bank * 0.65f, 2.12f, z);
                    innerPlug.transform.localRotation = Quaternion.Euler(0f, 0f, bank * -34f);
                    DisableAllColliders(innerPlug);
                    sparkPlugVisuals.Add(innerPlug);
                }
            }

            EngineAssemblyStation station = stationObject.AddComponent<EngineAssemblyStation>();
            station.Configure(
                engineBlockItem,
                cylinderCoverItem,
                sparkPlugItem,
                installedEngine,
                coverVisuals,
                sparkPlugVisuals);
            station.ResetAssembly();
        }

        private static void CreateStandVisual(Transform parent, GeneratedMaterials materials)
        {
            CreatePart(parent, PrimitiveType.Cube, "Left Base Rail",
                new Vector3(-1.25f, 0.18f, 0f), new Vector3(0.22f, 0.22f, 5.4f), materials.StandSteel);
            CreatePart(parent, PrimitiveType.Cube, "Right Base Rail",
                new Vector3(1.25f, 0.18f, 0f), new Vector3(0.22f, 0.22f, 5.4f), materials.StandSteel);
            CreatePart(parent, PrimitiveType.Cube, "Front Cross Rail",
                new Vector3(0f, 0.18f, 2.5f), new Vector3(2.7f, 0.22f, 0.22f), materials.StandSteel);
            CreatePart(parent, PrimitiveType.Cube, "Rear Cross Rail",
                new Vector3(0f, 0.18f, -2.5f), new Vector3(2.7f, 0.22f, 0.22f), materials.StandSteel);

            for (int side = -1; side <= 1; side += 2)
            {
                CreatePart(parent, PrimitiveType.Cube, $"Vertical Post {side} Front",
                    new Vector3(side * 1.05f, 0.95f, 1.8f), new Vector3(0.18f, 1.7f, 0.18f), materials.StandSteel);
                CreatePart(parent, PrimitiveType.Cube, $"Vertical Post {side} Rear",
                    new Vector3(side * 1.05f, 0.95f, -1.8f), new Vector3(0.18f, 1.7f, 0.18f), materials.StandSteel);
                CreatePart(parent, PrimitiveType.Cube, $"Engine Saddle {side}",
                    new Vector3(side * 0.62f, 1.45f, 0f), new Vector3(0.2f, 0.18f, 3.8f), materials.Aluminum);

                for (int zSide = -1; zSide <= 1; zSide += 2)
                {
                    CreatePart(parent, PrimitiveType.Cylinder, $"Caster Wheel {side} {zSide}",
                        new Vector3(side * 1.25f, 0.12f, zSide * 2.35f),
                        new Vector3(0.22f, 0.07f, 0.22f), materials.Rubber,
                        new Vector3(0f, 0f, 90f));
                }
            }

            CreatePart(parent, PrimitiveType.Cube, "Front Diagonal Brace",
                new Vector3(0f, 0.82f, 2.08f), new Vector3(2.4f, 0.12f, 0.12f), materials.StandSteel,
                new Vector3(0f, 0f, 20f));
            CreatePart(parent, PrimitiveType.Cube, "Rear Diagonal Brace",
                new Vector3(0f, 0.82f, -2.08f), new Vector3(2.4f, 0.12f, 0.12f), materials.StandSteel,
                new Vector3(0f, 0f, -20f));
        }

        private static GameObject CreatePickup(
            GameObject prefab,
            Transform parent,
            string objectName,
            InventoryItemDefinition item,
            Vector3 position,
            Quaternion rotation)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            instance.name = objectName;
            instance.transform.position = position;
            instance.transform.rotation = rotation;
            instance.SetActive(true);

            InventoryPickup pickup = instance.GetComponent<InventoryPickup>();
            if (pickup == null)
            {
                pickup = instance.AddComponent<InventoryPickup>();
            }

            pickup.Configure(item, 1);
            instance.name = objectName;
            return instance;
        }

        private static void CreateDisplayPad(
            Transform parent,
            string objectName,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = objectName;
            pad.transform.SetParent(parent, false);
            pad.transform.position = position;
            pad.transform.localScale = scale;
            pad.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void ConfigureInstallUi()
        {
            GameObject inventoryUiObject = GameObject.Find(InventoryUiName);
            if (inventoryUiObject == null)
            {
                Debug.LogError("Merlin setup could not find Inventory UI after attempting to create it.");
                return;
            }

            InventoryUI inventoryUi = inventoryUiObject.GetComponent<InventoryUI>();
            Transform detailsPanel = inventoryUiObject.transform.Find("Inventory Panel/Selected Item Panel");
            if (inventoryUi == null || detailsPanel == null)
            {
                Debug.LogError("Merlin setup could not find the inventory selected-item panel.");
                return;
            }

            Text instructionText = FindText(
                inventoryUiObject.transform,
                "Inventory Panel/Instructions");
            if (instructionText != null)
            {
                instructionText.text = "Aim at the engine stand, press I, select a part, then Install. Equip and Drop One remain available.";
                instructionText.fontSize = 15;
            }

            Text equippedText = FindText(detailsPanel, "Equipped Item");
            if (equippedText != null)
            {
                SetAnchoredRect(equippedText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 164f), new Vector2(235f, 28f));
            }

            Text targetText = GetOrCreateText(
                "Installation Target",
                detailsPanel,
                "Install target: None — aim at engine stand, then press I",
                14,
                TextAnchor.MiddleLeft,
                new Color(0.95f, 0.75f, 0.3f, 1f));
            SetAnchoredRect(targetText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 132f), new Vector2(235f, 42f));

            Button equipButton = FindButton(detailsPanel, "Equip Button");
            Button dropButton = FindButton(detailsPanel, "Drop Button");

            if (equipButton != null)
            {
                SetAnchoredRect(equipButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 92f), new Vector2(235f, 34f));
            }

            if (dropButton != null)
            {
                SetAnchoredRect(dropButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 54f), new Vector2(235f, 34f));
            }

            Button installButton = GetOrCreateButton(
                "Install Button",
                detailsPanel,
                "Install",
                new Color(0.2f, 0.55f, 0.28f, 1f),
                out Text installButtonText);
            SetAnchoredRect(installButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(235f, 34f));

            SerializedObject serializedUi = new SerializedObject(inventoryUi);
            serializedUi.FindProperty("installationTargetText").objectReferenceValue = targetText;
            serializedUi.FindProperty("installButton").objectReferenceValue = installButton;
            serializedUi.FindProperty("installButtonText").objectReferenceValue = installButtonText;
            serializedUi.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(inventoryUi);
        }

        private static void EnsureInventoryEquipmentUi()
        {
            GameObject inventoryUiObject = GameObject.Find(InventoryUiName);
            if (inventoryUiObject == null
                || inventoryUiObject.GetComponent<InventoryUI>() == null
                || inventoryUiObject.transform.Find("Inventory Panel/Selected Item Panel") == null)
            {
                InventoryEquipmentSetup.InstallEquipmentAndDropUi();
            }
        }

        private static void ValidatePickupCounts(GameObject root, ref bool passed)
        {
            if (root == null)
            {
                return;
            }

            InventoryPickup[] pickups = root.GetComponentsInChildren<InventoryPickup>(true);
            int engineBlocks = 0;
            int covers = 0;
            int sparkPlugs = 0;

            for (int index = 0; index < pickups.Length; index++)
            {
                string itemId = pickups[index].Item != null ? pickups[index].Item.ItemId : string.Empty;
                switch (itemId)
                {
                    case "merlin-engine-block":
                        engineBlocks++;
                        break;
                    case "merlin-cylinder-cover":
                        covers++;
                        break;
                    case "spark-plug":
                        sparkPlugs++;
                        break;
                }
            }

            if (engineBlocks != 1 || covers != 2 || sparkPlugs != 24)
            {
                Debug.LogError(
                    $"Merlin Step 2 failed: expected pickups 1 engine/2 covers/24 plugs, "
                    + $"found {engineBlocks}/{covers}/{sparkPlugs}.");
                passed = false;
            }
        }

        private static void ValidateItemAsset(string path, ref bool passed)
        {
            InventoryItemDefinition item = AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(path);
            if (item == null)
            {
                Debug.LogError($"Merlin Step 2 failed: item asset '{path}' is missing.");
                passed = false;
                return;
            }

            if (item.WorldPrefab == null)
            {
                Debug.LogError($"Merlin Step 2 failed: '{item.DisplayName}' has no world prefab.");
                passed = false;
            }
        }

        private static InventoryItemDefinition CreateOrUpdateItem(
            string assetPath,
            string itemId,
            string displayName,
            string description,
            int maxStackSize,
            Color placeholderColor,
            bool canEquip,
            GameObject worldPrefab)
        {
            InventoryItemDefinition item = AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(assetPath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<InventoryItemDefinition>();
                AssetDatabase.CreateAsset(item, assetPath);
            }

            SerializedObject serializedItem = new SerializedObject(item);
            serializedItem.FindProperty("itemId").stringValue = itemId;
            serializedItem.FindProperty("displayName").stringValue = displayName;
            serializedItem.FindProperty("description").stringValue = description;
            serializedItem.FindProperty("maxStackSize").intValue = maxStackSize;
            serializedItem.FindProperty("placeholderColor").colorValue = placeholderColor;
            serializedItem.FindProperty("canEquip").boolValue = canEquip;
            serializedItem.FindProperty("worldPrefab").objectReferenceValue = worldPrefab;
            serializedItem.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            return item;
        }

        private static Material CreateMaterial(
            string assetPath,
            Color color,
            float metallic,
            float smoothness)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                Debug.LogError("Merlin setup could not find a usable Lit shader.");
                return material;
            }

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, assetPath);
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

        private static GameObject SavePrefab(GameObject source, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, path);
            if (prefab == null)
            {
                Debug.LogError($"Merlin setup could not save prefab '{path}'.");
            }

            return prefab;
        }

        private static GameObject CreatePart(
            Transform parent,
            PrimitiveType primitiveType,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            Vector3 localEulerAngles = default)
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType);
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

        private static void DisableAllColliders(GameObject root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                colliders[index].enabled = false;
            }
        }

        private static void AlignBottomToY(GameObject root, float targetY)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            root.transform.position += Vector3.up * (targetY - bounds.min.y + 0.01f);
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
            string folderName = Path.GetFileName(folderPath);

            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(folderName))
            {
                return;
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static Text FindText(Transform parent, string path)
        {
            Transform child = parent.Find(path);
            return child != null ? child.GetComponent<Text>() : null;
        }

        private static Button FindButton(Transform parent, string objectName)
        {
            Transform child = parent.Find(objectName);
            return child != null ? child.GetComponent<Button>() : null;
        }

        private static Text GetOrCreateText(
            string objectName,
            Transform parent,
            string value,
            int fontSize,
            TextAnchor alignment,
            Color color)
        {
            Transform existing = parent.Find(objectName);
            Text text;

            if (existing != null)
            {
                text = existing.GetComponent<Text>();
                if (text == null)
                {
                    text = existing.gameObject.AddComponent<Text>();
                }
            }
            else
            {
                GameObject textObject = new GameObject(
                    objectName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Text));
                textObject.transform.SetParent(parent, false);
                text = textObject.GetComponent<Text>();
            }

            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Button GetOrCreateButton(
            string objectName,
            Transform parent,
            string label,
            Color backgroundColor,
            out Text labelText)
        {
            Transform existing = parent.Find(objectName);
            GameObject buttonObject;

            if (existing != null)
            {
                buttonObject = existing.gameObject;
            }
            else
            {
                buttonObject = new GameObject(
                    objectName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button));
                buttonObject.transform.SetParent(parent, false);
            }

            Image image = buttonObject.GetComponent<Image>();
            image.color = backgroundColor;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.55f);
            button.colors = colors;

            labelText = GetOrCreateText(
                "Label",
                buttonObject.transform,
                label,
                17,
                TextAnchor.MiddleCenter,
                Color.white);
            StretchToParent(labelText.rectTransform, 3f);
            return button;
        }

        private static void SetAnchoredRect(
            RectTransform rectTransform,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            rectTransform.anchorMin = anchor;
            rectTransform.anchorMax = anchor;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;
        }

        private static void StretchToParent(RectTransform rectTransform, float inset)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(inset, inset);
            rectTransform.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
