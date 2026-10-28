using System.Collections.Generic;
using Hanger51.EngineAssembly;
using Hanger51.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class HangarAndHardwarePolishSetup
    {
        private const string ExpandedEnvironmentName = "Expanded Test Environment";
        private const string HangarRootName = "Hanger 51 Test Hangar";
        private const string MerlinRootName = "V-1650 Assembly Test";
        private const string StationName = "V-1650 Engine Stand";
        private const string PlayerName = "Player";

        private const string MaterialFolder =
            "Assets/_Project/EngineAssembly/Materials/HangarTest";
        private const string MeshFolder =
            "Assets/_Project/EngineAssembly/Meshes";
        private const string HexBoltMeshPath =
            MeshFolder + "/HexBoltHead.asset";

        private const float CoverSurfaceLocalY = 0.448f;
        private const float SparkPlugRootLocalY = 0.292f;

        [MenuItem("Hanger 51/Test Hangar/1 - Build Expanded Hangar and Move Parts")]
        public static void BuildExpandedHangarAndMoveParts()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Test Hangar Step 1 failed. Exit Play mode first.");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || string.IsNullOrWhiteSpace(activeScene.path))
            {
                Debug.LogError("Test Hangar Step 1 failed. Open and save the movement test scene first.");
                return;
            }

            EnsureFolder("Assets/_Project/EngineAssembly");
            EnsureFolder(MaterialFolder);
            EnsureFolder(MeshFolder);

            Material floorMaterial = CreateMaterial(
                MaterialFolder + "/ExpandedFloor.mat",
                new Color(0.19f, 0.20f, 0.22f, 1f),
                0.08f,
                0.28f);
            Material wallMaterial = CreateMaterial(
                MaterialFolder + "/HangarWall.mat",
                new Color(0.34f, 0.38f, 0.41f, 1f),
                0.68f,
                0.34f);
            Material frameMaterial = CreateMaterial(
                MaterialFolder + "/HangarFrame.mat",
                new Color(0.10f, 0.14f, 0.17f, 1f),
                0.82f,
                0.48f);
            Material roofMaterial = CreateMaterial(
                MaterialFolder + "/HangarRoof.mat",
                new Color(0.20f, 0.24f, 0.27f, 1f),
                0.74f,
                0.40f);
            Material stripeMaterial = CreateMaterial(
                MaterialFolder + "/SafetyYellow.mat",
                new Color(0.95f, 0.62f, 0.04f, 1f),
                0.18f,
                0.38f);
            Material benchMaterial = CreateMaterial(
                MaterialFolder + "/WorkBench.mat",
                new Color(0.28f, 0.17f, 0.08f, 1f),
                0.05f,
                0.30f);

            RemoveExistingEnvironment();

            GameObject environmentRoot = new GameObject(ExpandedEnvironmentName);
            Undo.RegisterCreatedObjectUndo(environmentRoot, "Create expanded test environment");

            CreatePart(
                environmentRoot.transform,
                PrimitiveType.Cube,
                "Expanded Test Floor",
                new Vector3(0f, -0.5f, 10f),
                new Vector3(70f, 1f, 80f),
                Vector3.zero,
                floorMaterial);

            CreatePart(environmentRoot.transform, PrimitiveType.Cube, "North Perimeter Wall",
                new Vector3(0f, 2.5f, 50f), new Vector3(70f, 5f, 0.6f), Vector3.zero, wallMaterial);
            CreatePart(environmentRoot.transform, PrimitiveType.Cube, "South Perimeter Wall",
                new Vector3(0f, 2.5f, -30f), new Vector3(70f, 5f, 0.6f), Vector3.zero, wallMaterial);
            CreatePart(environmentRoot.transform, PrimitiveType.Cube, "East Perimeter Wall",
                new Vector3(35f, 2.5f, 10f), new Vector3(0.6f, 5f, 80f), Vector3.zero, wallMaterial);
            CreatePart(environmentRoot.transform, PrimitiveType.Cube, "West Perimeter Wall",
                new Vector3(-35f, 2.5f, 10f), new Vector3(0.6f, 5f, 80f), Vector3.zero, wallMaterial);

            GameObject hangarRoot = new GameObject(HangarRootName);
            Undo.RegisterCreatedObjectUndo(hangarRoot, "Create Hanger 51 test hangar");
            hangarRoot.transform.SetParent(environmentRoot.transform, false);
            hangarRoot.transform.position = new Vector3(0f, 0f, 18f);

            CreateHangarShell(
                hangarRoot.transform,
                floorMaterial,
                wallMaterial,
                frameMaterial,
                roofMaterial,
                stripeMaterial,
                benchMaterial);

            MoveMerlinAssemblyIntoHangar();
            MoveLooseInventoryPartsToBench(hangarRoot.transform);
            MovePlayerToHangarEntrance();

            EditorSceneManager.MarkSceneDirty(activeScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!EditorSceneManager.SaveScene(activeScene))
            {
                Debug.LogError("Test Hangar Step 1 created the hangar but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("Test Hangar Step 1 created the hangar, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = hangarRoot;
            Debug.Log(
                "Test Hangar Step 1 complete. Expanded the test area, created a full maintenance hangar, "
                + "moved the V-1650 assembly and loose inventory parts inside, saved the scene, and prepared Build and Run.",
                hangarRoot);
        }

        [MenuItem("Hanger 51/Test Hangar/2 - Polish Bolts and Spark Plug Seating")]
        public static void PolishBoltsAndSparkPlugSeating()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Test Hangar Step 2 failed. Exit Play mode first.");
                return;
            }

            EngineAssemblyStation station = FindStation();
            if (station == null)
            {
                Debug.LogError(
                    "Test Hangar Step 2 failed. The V-1650 engine stand is missing. Run the Merlin setup first.");
                return;
            }

            // Re-establish the corrected cover mount and regenerate the fastener
            // targets before applying the final visual and seating polish.
            MerlinCoverMountRepairSetup.RepairCylinderCoverMountPositions();
            station = FindStation();
            if (station == null)
            {
                Debug.LogError("Test Hangar Step 2 failed after rebuilding the cover mounts.");
                return;
            }

            Transform leftCover = station.transform.Find("Installed Left Cylinder Cover");
            Transform rightCover = station.transform.Find("Installed Right Cylinder Cover");
            if (leftCover == null || rightCover == null)
            {
                Debug.LogError("Test Hangar Step 2 failed. Installed cover visuals are missing.", station);
                return;
            }

            EnsureFolder(MeshFolder);
            Mesh hexBoltMesh = CreateOrRefreshHexBoltMesh();
            Material aluminum = LoadOrCreateMaterial(
                "Assets/_Project/EngineAssembly/Materials/MachinedAluminum.mat",
                new Color(0.67f, 0.70f, 0.74f, 1f),
                0.90f,
                0.78f);
            Material darkSteel = LoadOrCreateMaterial(
                "Assets/_Project/EngineAssembly/Materials/DarkSteel.mat",
                new Color(0.17f, 0.18f, 0.20f, 1f),
                0.88f,
                0.62f);
            Material highlight = LoadOrCreateMaterial(
                "Assets/_Project/EngineAssembly/Materials/InstallHighlight.mat",
                new Color(1f, 0.72f, 0.05f, 0.48f),
                0.15f,
                0.42f);

            EngineAssemblyInteractionTarget[] targets =
                station.GetComponentsInChildren<EngineAssemblyInteractionTarget>(true);

            int polishedBolts = 0;
            int seatedSparkPlugs = 0;

            for (int index = 0; index < targets.Length; index++)
            {
                EngineAssemblyInteractionTarget target = targets[index];
                if (target == null)
                {
                    continue;
                }

                if (target.InteractionKind == EngineAssemblyInteractionKind.CoverBolt)
                {
                    PolishBoltTarget(
                        target,
                        station,
                        hexBoltMesh,
                        aluminum,
                        darkSteel,
                        highlight);
                    polishedBolts++;
                }
                else if (target.InteractionKind == EngineAssemblyInteractionKind.SparkPlug)
                {
                    SeatSparkPlugTarget(
                        target,
                        station,
                        leftCover,
                        rightCover,
                        highlight);
                    seatedSparkPlugs++;
                }
            }

            station.ResetAssembly();
            EditorUtility.SetDirty(station);

            Scene activeScene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (string.IsNullOrWhiteSpace(activeScene.path)
                || !EditorSceneManager.SaveScene(activeScene))
            {
                Debug.LogError("Test Hangar Step 2 polished the hardware but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("Test Hangar Step 2 polished the hardware, but build preparation failed.");
                return;
            }

            Debug.Log(
                $"Test Hangar Step 2 complete. Rebuilt {polishedBolts} realistic seated bolts and set "
                + $"{seatedSparkPlugs} spark plugs to the correct threaded depth inside the covers.",
                station);
        }

        [MenuItem("Hanger 51/Test Hangar/3 - Validate Hangar and Hardware")]
        public static void ValidateHangarAndHardware()
        {
            bool passed = true;

            GameObject hangar = GameObject.Find(HangarRootName);
            if (hangar == null)
            {
                Debug.LogError("Test Hangar Step 3 failed: the generated hangar is missing.");
                passed = false;
            }

            GameObject merlinRoot = GameObject.Find(MerlinRootName);
            if (merlinRoot == null)
            {
                Debug.LogError("Test Hangar Step 3 failed: the V-1650 assembly root is missing.");
                passed = false;
            }
            else if (!IsInsideHangar(merlinRoot.transform.position))
            {
                Debug.LogError("Test Hangar Step 3 failed: the V-1650 assembly is outside the hangar.", merlinRoot);
                passed = false;
            }

            EngineAssemblyStation station = FindStation();
            if (station == null)
            {
                Debug.LogError("Test Hangar Step 3 failed: the engine stand is missing.");
                passed = false;
            }
            else
            {
                EngineAssemblyInteractionTarget[] targets =
                    station.GetComponentsInChildren<EngineAssemblyInteractionTarget>(true);

                int boltCount = 0;
                int plugCount = 0;

                Transform leftCover = station.transform.Find("Installed Left Cylinder Cover");
                Transform rightCover = station.transform.Find("Installed Right Cylinder Cover");

                for (int index = 0; index < targets.Length; index++)
                {
                    EngineAssemblyInteractionTarget target = targets[index];
                    if (target.InteractionKind == EngineAssemblyInteractionKind.CoverBolt)
                    {
                        boltCount++;
                        passed &= ValidateBoltTarget(target);
                    }
                    else if (target.InteractionKind == EngineAssemblyInteractionKind.SparkPlug)
                    {
                        plugCount++;
                        passed &= ValidateSparkPlugTarget(target, leftCover, rightCover);
                    }
                }

                if (boltCount != 12)
                {
                    Debug.LogError($"Test Hangar Step 3 failed: expected 12 polished bolts, found {boltCount}.");
                    passed = false;
                }

                if (plugCount != 24)
                {
                    Debug.LogError($"Test Hangar Step 3 failed: expected 24 seated spark plugs, found {plugCount}.");
                    passed = false;
                }
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("Test Hangar Step 3 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Test Hangar Step 3 passed. The expanded hangar contains the engine test area, "
                    + "all 12 bolt heads seat flush on the covers, all 24 spark plugs are threaded to the correct depth, "
                    + "and standalone Build and Run is ready.");
            }
        }

        private static void CreateHangarShell(
            Transform hangar,
            Material floor,
            Material wall,
            Material frame,
            Material roof,
            Material stripe,
            Material bench)
        {
            const float halfWidth = 15f;
            const float halfLength = 16f;
            const float wallHeight = 10f;

            CreatePart(hangar, PrimitiveType.Cube, "Hangar Concrete Slab",
                new Vector3(0f, -0.12f, 0f), new Vector3(30f, 0.24f, 32f), Vector3.zero, floor);

            CreatePart(hangar, PrimitiveType.Cube, "Left Hangar Wall",
                new Vector3(-halfWidth, wallHeight * 0.5f, 0f),
                new Vector3(0.35f, wallHeight, 32f), Vector3.zero, wall);
            CreatePart(hangar, PrimitiveType.Cube, "Right Hangar Wall",
                new Vector3(halfWidth, wallHeight * 0.5f, 0f),
                new Vector3(0.35f, wallHeight, 32f), Vector3.zero, wall);
            CreatePart(hangar, PrimitiveType.Cube, "Rear Hangar Wall",
                new Vector3(0f, wallHeight * 0.5f, halfLength),
                new Vector3(30f, wallHeight, 0.35f), Vector3.zero, wall);

            CreatePart(hangar, PrimitiveType.Cube, "Front Wall Left",
                new Vector3(-10.5f, wallHeight * 0.5f, -halfLength),
                new Vector3(9f, wallHeight, 0.35f), Vector3.zero, wall);
            CreatePart(hangar, PrimitiveType.Cube, "Front Wall Right",
                new Vector3(10.5f, wallHeight * 0.5f, -halfLength),
                new Vector3(9f, wallHeight, 0.35f), Vector3.zero, wall);
            CreatePart(hangar, PrimitiveType.Cube, "Front Door Header",
                new Vector3(0f, 8.65f, -halfLength),
                new Vector3(12f, 2.7f, 0.35f), Vector3.zero, wall);

            float roofAngle = 18.5f;
            CreatePart(hangar, PrimitiveType.Cube, "Left Roof Panel",
                new Vector3(-7.5f, 12.45f, 0f),
                new Vector3(15.85f, 0.30f, 32.4f),
                new Vector3(0f, 0f, roofAngle),
                roof);
            CreatePart(hangar, PrimitiveType.Cube, "Right Roof Panel",
                new Vector3(7.5f, 12.45f, 0f),
                new Vector3(15.85f, 0.30f, 32.4f),
                new Vector3(0f, 0f, -roofAngle),
                roof);

            float[] frameZ = { -15.5f, -10f, -4f, 2f, 8f, 14f, 15.5f };
            for (int index = 0; index < frameZ.Length; index++)
            {
                float z = frameZ[index];
                CreatePart(hangar, PrimitiveType.Cube, $"Frame Left Post {index + 1}",
                    new Vector3(-14.7f, 5f, z), new Vector3(0.28f, 10f, 0.28f), Vector3.zero, frame);
                CreatePart(hangar, PrimitiveType.Cube, $"Frame Right Post {index + 1}",
                    new Vector3(14.7f, 5f, z), new Vector3(0.28f, 10f, 0.28f), Vector3.zero, frame);
                CreatePart(hangar, PrimitiveType.Cube, $"Frame Left Rafter {index + 1}",
                    new Vector3(-7.4f, 12.35f, z),
                    new Vector3(15.65f, 0.22f, 0.22f),
                    new Vector3(0f, 0f, roofAngle),
                    frame);
                CreatePart(hangar, PrimitiveType.Cube, $"Frame Right Rafter {index + 1}",
                    new Vector3(7.4f, 12.35f, z),
                    new Vector3(15.65f, 0.22f, 0.22f),
                    new Vector3(0f, 0f, -roofAngle),
                    frame);
            }

            CreatePart(hangar, PrimitiveType.Cube, "Door Track",
                new Vector3(0f, 9.7f, -15.75f), new Vector3(13f, 0.22f, 0.22f), Vector3.zero, frame);
            CreatePart(hangar, PrimitiveType.Cube, "Open Door Left Panel",
                new Vector3(-12.4f, 4f, -15.72f), new Vector3(4.2f, 8f, 0.18f), Vector3.zero, roof);
            CreatePart(hangar, PrimitiveType.Cube, "Open Door Right Panel",
                new Vector3(12.4f, 4f, -15.72f), new Vector3(4.2f, 8f, 0.18f), Vector3.zero, roof);

            CreatePart(hangar, PrimitiveType.Cube, "Center Safety Line",
                new Vector3(0f, 0.012f, -5f), new Vector3(0.18f, 0.024f, 20f), Vector3.zero, stripe);
            CreatePart(hangar, PrimitiveType.Cube, "Engine Bay Left Stripe",
                new Vector3(-8.8f, 0.013f, 0f), new Vector3(0.14f, 0.026f, 20f), Vector3.zero, stripe);
            CreatePart(hangar, PrimitiveType.Cube, "Engine Bay Right Stripe",
                new Vector3(8.8f, 0.013f, 0f), new Vector3(0.14f, 0.026f, 20f), Vector3.zero, stripe);

            CreateWorkbench(hangar, "General Parts Workbench", new Vector3(10.5f, 0f, -4f), bench, frame);
            CreateWorkbench(hangar, "Tool Workbench", new Vector3(-11f, 0f, -5f), bench, frame);

            float[] lightZ = { -10f, -2f, 6f, 14f };
            for (int index = 0; index < lightZ.Length; index++)
            {
                GameObject lightObject = new GameObject($"Hangar Light {index + 1}");
                lightObject.transform.SetParent(hangar, false);
                lightObject.transform.localPosition = new Vector3(0f, 9.2f, lightZ[index]);

                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 18f;
                light.intensity = 1450f;
                light.color = new Color(0.88f, 0.93f, 1f, 1f);
                light.shadows = LightShadows.Soft;

                CreatePart(lightObject.transform, PrimitiveType.Cube, "Light Fixture",
                    Vector3.zero, new Vector3(2.4f, 0.12f, 0.45f), Vector3.zero, frame);
            }
        }

        private static void CreateWorkbench(
            Transform parent,
            string name,
            Vector3 position,
            Material topMaterial,
            Material frameMaterial)
        {
            GameObject bench = new GameObject(name);
            bench.transform.SetParent(parent, false);
            bench.transform.localPosition = position;

            CreatePart(bench.transform, PrimitiveType.Cube, "Bench Top",
                new Vector3(0f, 1.02f, 0f), new Vector3(5.5f, 0.18f, 1.6f), Vector3.zero, topMaterial);

            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    CreatePart(bench.transform, PrimitiveType.Cube, $"Leg {x} {z}",
                        new Vector3(x * 2.35f, 0.5f, z * 0.58f),
                        new Vector3(0.16f, 1f, 0.16f), Vector3.zero, frameMaterial);
                }
            }
        }

        private static void MoveMerlinAssemblyIntoHangar()
        {
            GameObject merlinRoot = GameObject.Find(MerlinRootName);
            if (merlinRoot == null)
            {
                Debug.LogWarning(
                    "The expanded hangar was created, but the V-1650 assembly root was not found. Run Merlin Step 1, then rerun Test Hangar Step 1.");
                return;
            }

            Undo.RecordObject(merlinRoot.transform, "Move V-1650 assembly into hangar");
            merlinRoot.transform.position = new Vector3(0f, 0f, 8f);
            merlinRoot.transform.rotation = Quaternion.identity;
            merlinRoot.transform.localScale = Vector3.one;
            EditorUtility.SetDirty(merlinRoot.transform);
        }

        private static void MoveLooseInventoryPartsToBench(Transform hangarRoot)
        {
            InventoryPickup[] allPickups = Object.FindObjectsByType<InventoryPickup>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            GameObject merlinRoot = GameObject.Find(MerlinRootName);
            List<InventoryPickup> loosePickups = new List<InventoryPickup>();

            for (int index = 0; index < allPickups.Length; index++)
            {
                InventoryPickup pickup = allPickups[index];
                if (pickup == null
                    || !pickup.gameObject.scene.IsValid()
                    || (merlinRoot != null && pickup.transform.IsChildOf(merlinRoot.transform)))
                {
                    continue;
                }

                loosePickups.Add(pickup);
            }

            loosePickups.Sort((left, right) => string.CompareOrdinal(left.name, right.name));

            for (int index = 0; index < loosePickups.Count; index++)
            {
                int column = index % 4;
                int row = index / 4;
                Vector3 localPosition = new Vector3(
                    8.8f + column * 1.05f,
                    1.18f,
                    -4.6f + row * 1.0f);

                Undo.RecordObject(loosePickups[index].transform, "Move loose inventory part into hangar");
                loosePickups[index].transform.position = hangarRoot.TransformPoint(localPosition);
                loosePickups[index].transform.rotation = Quaternion.identity;
                EditorUtility.SetDirty(loosePickups[index].transform);
            }
        }

        private static void MovePlayerToHangarEntrance()
        {
            GameObject player = GameObject.Find(PlayerName);
            if (player == null)
            {
                return;
            }

            Undo.RecordObject(player.transform, "Move Player to hangar entrance");
            player.transform.position = new Vector3(0f, 0.02f, -7f);
            player.transform.rotation = Quaternion.identity;
            EditorUtility.SetDirty(player.transform);
        }

        private static void PolishBoltTarget(
            EngineAssemblyInteractionTarget target,
            EngineAssemblyStation station,
            Mesh hexMesh,
            Material aluminum,
            Material darkSteel,
            Material highlightMaterial)
        {
            ClearChildren(target.transform);

            Vector3 localPosition = target.transform.localPosition;
            localPosition.y = CoverSurfaceLocalY;
            target.transform.localPosition = localPosition;
            target.transform.localRotation = Quaternion.identity;

            SphereCollider collider = target.GetComponent<SphereCollider>();
            if (collider == null)
            {
                collider = target.gameObject.AddComponent<SphereCollider>();
            }

            collider.center = new Vector3(0f, 0.035f, 0f);
            collider.radius = 0.13f;

            GameObject assembly = new GameObject("Realistic Bolt Assembly");
            assembly.transform.SetParent(target.transform, false);

            CreatePart(assembly.transform, PrimitiveType.Cylinder, "Threaded Bolt Shaft",
                new Vector3(0f, -0.066f, 0f), new Vector3(0.026f, 0.066f, 0.026f), Vector3.zero, darkSteel);
            CreatePart(assembly.transform, PrimitiveType.Cylinder, "Bolt Washer",
                new Vector3(0f, 0.006f, 0f), new Vector3(0.076f, 0.006f, 0.076f), Vector3.zero, aluminum);

            GameObject hexHead = CreateMeshPart(
                assembly.transform,
                "Hex Bolt Head",
                hexMesh,
                new Vector3(0f, 0.038f, 0f),
                new Vector3(0.068f, 0.027f, 0.068f),
                aluminum);

            GameObject socket = CreateMeshPart(
                hexHead.transform,
                "Dark Socket Recess",
                hexMesh,
                new Vector3(0f, 0.54f, 0f),
                new Vector3(0.40f, 0.10f, 0.40f),
                darkSteel);
            socket.transform.localRotation = Quaternion.Euler(0f, 30f, 0f);

            GameObject highlight = CreatePart(
                target.transform,
                PrimitiveType.Cylinder,
                "Bolt Highlight Ring",
                new Vector3(0f, 0.010f, 0f),
                new Vector3(0.115f, 0.008f, 0.115f),
                Vector3.zero,
                highlightMaterial);

            target.Configure(
                station,
                EngineAssemblyInteractionKind.CoverBolt,
                target.GroupIndex,
                target.TargetIndex,
                0.85f,
                highlight,
                assembly,
                0.14f,
                3f);
        }

        private static void SeatSparkPlugTarget(
            EngineAssemblyInteractionTarget target,
            EngineAssemblyStation station,
            Transform leftCover,
            Transform rightCover,
            Material highlightMaterial)
        {
            SerializedObject serializedTarget = new SerializedObject(target);
            SerializedProperty animatedVisualProperty = serializedTarget.FindProperty("animatedVisual");
            GameObject plugVisual = animatedVisualProperty != null
                ? animatedVisualProperty.objectReferenceValue as GameObject
                : null;

            if (plugVisual == null)
            {
                Debug.LogWarning($"Could not find the spark-plug visual for '{target.name}'.", target);
                return;
            }

            int globalIndex = target.TargetIndex;
            int cylinderIndex = globalIndex / 4;
            int indexWithinCylinder = globalIndex % 4;
            bool leftBank = indexWithinCylinder < 2;
            bool outerPlug = indexWithinCylinder % 2 == 0;
            Transform cover = leftBank ? leftCover : rightCover;

            float localX = outerPlug ? -0.16f : 0.16f;
            float localZ = -1.35f + cylinderIndex * 0.54f;
            Vector3 finalWorldPosition = cover.TransformPoint(
                new Vector3(localX, SparkPlugRootLocalY, localZ));
            Quaternion finalWorldRotation = cover.rotation;

            target.transform.position = finalWorldPosition;
            target.transform.rotation = finalWorldRotation;
            plugVisual.transform.position = finalWorldPosition;
            plugVisual.transform.rotation = finalWorldRotation;

            ClearChildren(target.transform);

            BoxCollider collider = target.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = target.gameObject.AddComponent<BoxCollider>();
            }

            collider.center = new Vector3(0f, 0.17f, 0f);
            collider.size = new Vector3(0.30f, 0.42f, 0.30f);

            GameObject highlight = CreatePart(
                target.transform,
                PrimitiveType.Cylinder,
                "Spark Plug Well Highlight",
                new Vector3(0f, 0.158f, 0f),
                new Vector3(0.135f, 0.012f, 0.135f),
                Vector3.zero,
                highlightMaterial);

            target.Configure(
                station,
                EngineAssemblyInteractionKind.SparkPlug,
                target.GroupIndex,
                target.TargetIndex,
                1.25f,
                highlight,
                plugVisual,
                0.38f,
                4f);
        }

        private static bool ValidateBoltTarget(EngineAssemblyInteractionTarget target)
        {
            Transform assembly = target.transform.Find("Realistic Bolt Assembly");
            Transform washer = assembly != null ? assembly.Find("Bolt Washer") : null;
            Transform head = assembly != null ? assembly.Find("Hex Bolt Head") : null;
            Transform shaft = assembly != null ? assembly.Find("Threaded Bolt Shaft") : null;

            if (assembly == null || washer == null || head == null || shaft == null)
            {
                Debug.LogError($"Hardware validation failed: '{target.name}' is missing realistic bolt geometry.", target);
                return false;
            }

            if (Mathf.Abs(target.transform.localPosition.y - CoverSurfaceLocalY) > 0.01f)
            {
                Debug.LogError(
                    $"Hardware validation failed: '{target.name}' is not seated flush with the cover surface.",
                    target);
                return false;
            }

            return true;
        }

        private static bool ValidateSparkPlugTarget(
            EngineAssemblyInteractionTarget target,
            Transform leftCover,
            Transform rightCover)
        {
            SerializedObject serializedTarget = new SerializedObject(target);
            SerializedProperty animatedVisualProperty = serializedTarget.FindProperty("animatedVisual");
            GameObject plugVisual = animatedVisualProperty != null
                ? animatedVisualProperty.objectReferenceValue as GameObject
                : null;

            if (plugVisual == null || leftCover == null || rightCover == null)
            {
                Debug.LogError($"Hardware validation failed: '{target.name}' is missing its plug visual or cover.", target);
                return false;
            }

            int indexWithinCylinder = target.TargetIndex % 4;
            Transform cover = indexWithinCylinder < 2 ? leftCover : rightCover;
            Vector3 coverLocalPosition = cover.InverseTransformPoint(plugVisual.transform.position);

            if (Mathf.Abs(coverLocalPosition.y - SparkPlugRootLocalY) > 0.015f)
            {
                Debug.LogError(
                    $"Hardware validation failed: '{target.name}' spark plug depth is {coverLocalPosition.y:F3}; "
                    + $"expected approximately {SparkPlugRootLocalY:F3}.",
                    target);
                return false;
            }

            return true;
        }

        private static Mesh CreateOrRefreshHexBoltMesh()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(HexBoltMeshPath);
            if (mesh == null)
            {
                mesh = new Mesh
                {
                    name = "Hex Bolt Head"
                };
                AssetDatabase.CreateAsset(mesh, HexBoltMeshPath);
            }

            Vector3[] vertices = new Vector3[14];
            vertices[0] = new Vector3(0f, -0.5f, 0f);
            vertices[1] = new Vector3(0f, 0.5f, 0f);

            for (int index = 0; index < 6; index++)
            {
                float angle = Mathf.Deg2Rad * (index * 60f + 30f);
                float x = Mathf.Cos(angle);
                float z = Mathf.Sin(angle);
                vertices[2 + index] = new Vector3(x, -0.5f, z);
                vertices[8 + index] = new Vector3(x, 0.5f, z);
            }

            List<int> triangles = new List<int>();
            for (int index = 0; index < 6; index++)
            {
                int next = (index + 1) % 6;
                int bottom = 2 + index;
                int bottomNext = 2 + next;
                int top = 8 + index;
                int topNext = 8 + next;

                triangles.Add(0);
                triangles.Add(bottomNext);
                triangles.Add(bottom);

                triangles.Add(1);
                triangles.Add(top);
                triangles.Add(topNext);

                triangles.Add(bottom);
                triangles.Add(bottomNext);
                triangles.Add(topNext);

                triangles.Add(bottom);
                triangles.Add(topNext);
                triangles.Add(top);
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static EngineAssemblyStation FindStation()
        {
            EngineAssemblyStation[] stations = Object.FindObjectsByType<EngineAssemblyStation>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int index = 0; index < stations.Length; index++)
            {
                if (stations[index] != null && stations[index].name == StationName)
                {
                    return stations[index];
                }
            }

            return null;
        }

        private static bool IsInsideHangar(Vector3 worldPosition)
        {
            return worldPosition.x > -14.5f
                && worldPosition.x < 14.5f
                && worldPosition.z > 2.5f
                && worldPosition.z < 33.5f;
        }

        private static void RemoveExistingEnvironment()
        {
            GameObject oldEnvironment = GameObject.Find("Environment");
            if (oldEnvironment != null)
            {
                Undo.DestroyObjectImmediate(oldEnvironment);
            }

            GameObject expandedEnvironment = GameObject.Find(ExpandedEnvironmentName);
            if (expandedEnvironment != null)
            {
                Undo.DestroyObjectImmediate(expandedEnvironment);
            }
        }

        private static GameObject CreatePart(
            Transform parent,
            PrimitiveType type,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEulerAngles,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            Undo.RegisterCreatedObjectUndo(part, $"Create {name}");
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(localEulerAngles);
            part.transform.localScale = localScale;

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            return part;
        }

        private static GameObject CreateMeshPart(
            Transform parent,
            string name,
            Mesh mesh,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            Undo.RegisterCreatedObjectUndo(part, $"Create {name}");
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = localScale;

            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }

        private static void ClearChildren(Transform parent)
        {
            List<GameObject> children = new List<GameObject>();
            for (int index = 0; index < parent.childCount; index++)
            {
                children.Add(parent.GetChild(index).gameObject);
            }

            for (int index = 0; index < children.Count; index++)
            {
                Undo.DestroyObjectImmediate(children[index]);
            }
        }

        private static Material LoadOrCreateMaterial(
            string path,
            Color color,
            float metallic,
            float smoothness)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            return material != null
                ? material
                : CreateMaterial(path, color, metallic, smoothness);
        }

        private static Material CreateMaterial(
            string path,
            Color color,
            float metallic,
            float smoothness)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                Debug.LogError($"Could not find a shader for material '{path}'.");
                return material;
            }

            if (material == null)
            {
                EnsureFolder(System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/"));
                material = new Material(shader);
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

            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(folderName))
            {
                return;
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
