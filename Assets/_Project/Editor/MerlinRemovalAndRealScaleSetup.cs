using System.Collections.Generic;
using Hanger51.EngineAssembly;
using Hanger51.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class MerlinRemovalAndRealScaleSetup
    {
        private const string StationName = "V-1650 Engine Stand";
        private const string EngineVisualName = "Installed Engine Core";
        private const string LeftCoverName = "Installed Left Cylinder Cover";
        private const string RightCoverName = "Installed Right Cylinder Cover";

        private const string EngineItemPath =
            "Assets/_Project/Inventory/Items/MerlinEngineBlock.asset";
        private const string CoverItemPath =
            "Assets/_Project/Inventory/Items/MerlinCylinderCover.asset";
        private const string SparkPlugItemPath =
            "Assets/_Project/Inventory/Items/SparkPlug.asset";

        // The generated engine is approximately 6.15 units long. A 0.36
        // scale produces a 2.21 m engine, matching the Smithsonian V-1650-7
        // overall length closely enough for first-person scale testing.
        private static readonly Vector3 EngineScale = Vector3.one * 0.36f;
        private static readonly Vector3 CoverScale = Vector3.one * 0.36f;
        private static readonly Vector3 SparkPlugScale = Vector3.one * 0.22f;

        private const float InstalledEngineRootY = 1.02f;
        private const float BoltInsetX = 0.22f;
        private const float BoltSurfaceY = 0.405f;
        private const float SparkPlugRootY = 0.31f;

        [MenuItem("Hanger 51/Test Hangar/4 - Add Removal and Apply Real Scale")]
        public static void AddRemovalAndApplyRealScale()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Test Hangar Step 4 failed. Exit Play mode first.");
                return;
            }

            EngineAssemblyStation station = FindStation();
            if (station == null)
            {
                Debug.LogError(
                    "Test Hangar Step 4 failed. Run the Merlin and Test Hangar setup steps first.");
                return;
            }

            EngineAssemblyRemovalController removalController =
                station.GetComponent<EngineAssemblyRemovalController>();
            if (removalController == null)
            {
                removalController = Undo.AddComponent<EngineAssemblyRemovalController>(station.gameObject);
            }

            ConfigureItemScales();
            ScaleWorldPickups();

            Transform installedEngine = station.transform.Find(EngineVisualName);
            Transform leftCover = station.transform.Find(LeftCoverName);
            Transform rightCover = station.transform.Find(RightCoverName);

            if (installedEngine == null || leftCover == null || rightCover == null)
            {
                Debug.LogError(
                    "Test Hangar Step 4 failed. Installed engine or cover visuals are missing.",
                    station);
                return;
            }

            Undo.RecordObject(installedEngine, "Scale V-1650 engine");
            installedEngine.localScale = EngineScale;
            Vector3 enginePosition = installedEngine.localPosition;
            enginePosition.y = InstalledEngineRootY;
            installedEngine.localPosition = enginePosition;

            Undo.RecordObject(leftCover, "Scale left cylinder cover");
            Undo.RecordObject(rightCover, "Scale right cylinder cover");
            leftCover.localScale = CoverScale;
            rightCover.localScale = CoverScale;

            // Rebuild the mount targets after scaling. EngineCoverMountSnapper
            // now preserves cover scale while deriving the bank-relative pose.
            MerlinCoverMountRepairSetup.RepairCylinderCoverMountPositions();
            HangarAndHardwarePolishSetup.PolishBoltsAndSparkPlugSeating();

            station = FindStation();
            if (station == null)
            {
                Debug.LogError("Test Hangar Step 4 failed after rebuilding the assembly targets.");
                return;
            }

            removalController = station.GetComponent<EngineAssemblyRemovalController>();
            if (removalController == null)
            {
                removalController = Undo.AddComponent<EngineAssemblyRemovalController>(station.gameObject);
            }

            leftCover = station.transform.Find(LeftCoverName);
            rightCover = station.transform.Find(RightCoverName);
            if (leftCover == null || rightCover == null)
            {
                Debug.LogError("Test Hangar Step 4 failed. Corrected cover visuals are missing.", station);
                return;
            }

            leftCover.localScale = CoverScale;
            rightCover.localScale = CoverScale;

            EngineAssemblyInteractionTarget[] targets =
                station.GetComponentsInChildren<EngineAssemblyInteractionTarget>(true);

            int adjustedBolts = 0;
            int adjustedSparkPlugs = 0;

            for (int index = 0; index < targets.Length; index++)
            {
                EngineAssemblyInteractionTarget target = targets[index];
                if (target.InteractionKind == EngineAssemblyInteractionKind.CoverBolt)
                {
                    AdjustBoltTarget(target, station);
                    adjustedBolts++;
                }
                else if (target.InteractionKind == EngineAssemblyInteractionKind.SparkPlug)
                {
                    AdjustSparkPlugTarget(target, station, leftCover, rightCover);
                    adjustedSparkPlugs++;
                }
            }

            station.ResetAssembly();
            EditorUtility.SetDirty(station);
            EditorUtility.SetDirty(removalController);
            EditorUtility.SetDirty(installedEngine);
            EditorUtility.SetDirty(leftCover);
            EditorUtility.SetDirty(rightCover);

            Scene activeScene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (string.IsNullOrWhiteSpace(activeScene.path)
                || !EditorSceneManager.SaveScene(activeScene))
            {
                Debug.LogError("Test Hangar Step 4 applied the feature but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("Test Hangar Step 4 applied the feature, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = station.gameObject;
            Debug.Log(
                $"Test Hangar Step 4 complete. Applied real-world V-1650 scale, corrected {adjustedBolts} inset bolt positions, "
                + $"set {adjustedSparkPlugs} spark plugs to seated depth, added R-key disassembly, saved the scene, and prepared Build and Run.",
                station);
        }

        [MenuItem("Hanger 51/Test Hangar/5 - Validate Removal and Real Scale")]
        public static void ValidateRemovalAndRealScale()
        {
            bool passed = true;
            EngineAssemblyStation station = FindStation();

            if (station == null)
            {
                Debug.LogError("Test Hangar Step 5 failed: the V-1650 engine stand is missing.");
                return;
            }

            EngineAssemblyRemovalController removalController =
                station.GetComponent<EngineAssemblyRemovalController>();
            if (removalController == null)
            {
                Debug.LogError("Test Hangar Step 5 failed: removal controller is missing.", station);
                passed = false;
            }

            Transform installedEngine = station.transform.Find(EngineVisualName);
            Transform leftCover = station.transform.Find(LeftCoverName);
            Transform rightCover = station.transform.Find(RightCoverName);

            if (installedEngine == null
                || !Approximately(installedEngine.localScale, EngineScale, 0.005f))
            {
                Debug.LogError("Test Hangar Step 5 failed: installed engine does not use real-world scale.", station);
                passed = false;
            }

            if (leftCover == null
                || rightCover == null
                || !Approximately(leftCover.localScale, CoverScale, 0.005f)
                || !Approximately(rightCover.localScale, CoverScale, 0.005f))
            {
                Debug.LogError("Test Hangar Step 5 failed: installed covers do not match engine scale.", station);
                passed = false;
            }

            if (installedEngine != null)
            {
                Bounds engineBounds = CalculateRendererBounds(installedEngine.gameObject);
                if (engineBounds.size.z < 1.9f || engineBounds.size.z > 2.5f)
                {
                    Debug.LogError(
                        $"Test Hangar Step 5 failed: engine length is {engineBounds.size.z:F2} m; expected approximately 2.21 m.",
                        installedEngine);
                    passed = false;
                }
            }

            EngineAssemblyInteractionTarget[] targets =
                station.GetComponentsInChildren<EngineAssemblyInteractionTarget>(true);
            int boltCount = 0;
            int plugCount = 0;

            for (int index = 0; index < targets.Length; index++)
            {
                EngineAssemblyInteractionTarget target = targets[index];
                if (target.InteractionKind == EngineAssemblyInteractionKind.CoverBolt)
                {
                    boltCount++;
                    if (Mathf.Abs(Mathf.Abs(target.transform.localPosition.x) - BoltInsetX) > 0.015f
                        || Mathf.Abs(target.transform.localPosition.y - BoltSurfaceY) > 0.015f)
                    {
                        Debug.LogError(
                            $"Test Hangar Step 5 failed: '{target.name}' is not inset into the cover body.",
                            target);
                        passed = false;
                    }
                }
                else if (target.InteractionKind == EngineAssemblyInteractionKind.SparkPlug)
                {
                    plugCount++;
                    Transform cover = target.GroupIndex == 0 ? leftCover : rightCover;
                    if (cover == null)
                    {
                        passed = false;
                        continue;
                    }

                    Vector3 localPosition = cover.InverseTransformPoint(target.transform.position);
                    if (Mathf.Abs(localPosition.y - SparkPlugRootY) > 0.02f)
                    {
                        Debug.LogError(
                            $"Test Hangar Step 5 failed: '{target.name}' uses spark-plug depth {localPosition.y:F3}, expected {SparkPlugRootY:F3}.",
                            target);
                        passed = false;
                    }
                }
            }

            if (boltCount != 12)
            {
                Debug.LogError($"Test Hangar Step 5 failed: expected 12 removable bolts, found {boltCount}.");
                passed = false;
            }

            if (plugCount != 24)
            {
                Debug.LogError($"Test Hangar Step 5 failed: expected 24 removable spark plugs, found {plugCount}.");
                passed = false;
            }

            passed &= ValidateItemScale(EngineItemPath, EngineScale, "engine block");
            passed &= ValidateItemScale(CoverItemPath, CoverScale, "cylinder cover");
            passed &= ValidateItemScale(SparkPlugItemPath, SparkPlugScale, "spark plug");

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("Test Hangar Step 5 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Test Hangar Step 5 passed. Engine and parts use believable player-relative scale, bolts are inset into the covers, "
                    + "spark plugs seat at the corrected depth, R-key disassembly is installed, and Build and Run is ready.");
            }
        }

        private static void ConfigureItemScales()
        {
            SetItemWorldScale(EngineItemPath, EngineScale);
            SetItemWorldScale(CoverItemPath, CoverScale);
            SetItemWorldScale(SparkPlugItemPath, SparkPlugScale);
        }

        private static void SetItemWorldScale(string path, Vector3 scale)
        {
            InventoryItemDefinition item =
                AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(path);
            if (item == null)
            {
                Debug.LogWarning($"Could not set world scale because item asset '{path}' is missing.");
                return;
            }

            SerializedObject serializedItem = new SerializedObject(item);
            SerializedProperty scaleProperty = serializedItem.FindProperty("worldScale");
            if (scaleProperty != null)
            {
                scaleProperty.vector3Value = scale;
                serializedItem.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(item);
            }
        }

        private static void ScaleWorldPickups()
        {
            InventoryPickup[] pickups = Object.FindObjectsByType<InventoryPickup>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int index = 0; index < pickups.Length; index++)
            {
                InventoryPickup pickup = pickups[index];
                if (pickup == null || pickup.Item == null || !pickup.gameObject.scene.IsValid())
                {
                    continue;
                }

                Vector3 targetScale;
                switch (pickup.Item.ItemId)
                {
                    case "merlin-engine-block":
                        targetScale = EngineScale;
                        break;
                    case "merlin-cylinder-cover":
                        targetScale = CoverScale;
                        break;
                    case "spark-plug":
                        targetScale = SparkPlugScale;
                        break;
                    default:
                        continue;
                }

                ScaleKeepingBottom(pickup.transform, targetScale);
            }
        }

        private static void ScaleKeepingBottom(Transform target, Vector3 targetScale)
        {
            Bounds before = CalculateRendererBounds(target.gameObject);
            float oldBottom = before.min.y;

            Undo.RecordObject(target, "Apply real-world part scale");
            target.localScale = targetScale;

            Bounds after = CalculateRendererBounds(target.gameObject);
            Vector3 position = target.position;
            position.y += oldBottom - after.min.y;
            target.position = position;
            EditorUtility.SetDirty(target);
        }

        private static void AdjustBoltTarget(
            EngineAssemblyInteractionTarget target,
            EngineAssemblyStation station)
        {
            Vector3 localPosition = target.transform.localPosition;
            localPosition.x = Mathf.Sign(localPosition.x == 0f ? 1f : localPosition.x) * BoltInsetX;
            localPosition.y = BoltSurfaceY;
            target.transform.localPosition = localPosition;
            target.transform.localRotation = Quaternion.identity;

            Transform assembly = target.transform.Find("Realistic Bolt Assembly");
            Transform shaft = assembly != null ? assembly.Find("Threaded Bolt Shaft") : null;
            Transform washer = assembly != null ? assembly.Find("Bolt Washer") : null;
            Transform head = assembly != null ? assembly.Find("Hex Bolt Head") : null;
            Transform socket = head != null ? head.Find("Dark Socket Recess") : null;
            Transform highlightTransform = target.transform.Find("Bolt Highlight Ring");

            if (assembly == null || shaft == null || washer == null || head == null)
            {
                Debug.LogWarning($"Could not fully polish '{target.name}' because bolt geometry is missing.", target);
                return;
            }

            shaft.localPosition = new Vector3(0f, -0.09f, 0f);
            shaft.localScale = new Vector3(0.018f, 0.09f, 0.018f);
            washer.localPosition = new Vector3(0f, 0.004f, 0f);
            washer.localScale = new Vector3(0.042f, 0.004f, 0.042f);
            head.localPosition = new Vector3(0f, 0.018f, 0f);
            head.localScale = new Vector3(0.036f, 0.026f, 0.036f);

            if (socket != null)
            {
                socket.localPosition = new Vector3(0f, 0.46f, 0f);
                socket.localScale = new Vector3(0.42f, 0.12f, 0.42f);
            }

            if (highlightTransform != null)
            {
                highlightTransform.localPosition = new Vector3(0f, 0.006f, 0f);
                highlightTransform.localScale = new Vector3(0.075f, 0.006f, 0.075f);
            }

            SphereCollider collider = target.GetComponent<SphereCollider>();
            if (collider != null)
            {
                collider.center = new Vector3(0f, 0.03f, 0f);
                collider.radius = 0.30f;
            }

            target.Configure(
                station,
                EngineAssemblyInteractionKind.CoverBolt,
                target.GroupIndex,
                target.TargetIndex,
                0.9f,
                highlightTransform != null ? highlightTransform.gameObject : null,
                assembly.gameObject,
                0.08f,
                3f);
        }

        private static void AdjustSparkPlugTarget(
            EngineAssemblyInteractionTarget target,
            EngineAssemblyStation station,
            Transform leftCover,
            Transform rightCover)
        {
            Transform cover = target.GroupIndex == 0 ? leftCover : rightCover;
            if (cover == null)
            {
                return;
            }

            int cylinderIndex = target.TargetIndex / 4;
            int indexWithinCylinder = target.TargetIndex % 4;
            bool outerPlug = indexWithinCylinder % 2 == 0;
            float localX = outerPlug ? -0.16f : 0.16f;
            float localZ = -1.35f + cylinderIndex * 0.54f;

            Vector3 finalWorldPosition = cover.TransformPoint(
                new Vector3(localX, SparkPlugRootY, localZ));
            target.transform.SetPositionAndRotation(finalWorldPosition, cover.rotation);

            SerializedObject serializedTarget = new SerializedObject(target);
            SerializedProperty animatedVisualProperty = serializedTarget.FindProperty("animatedVisual");
            SerializedProperty highlightProperty = serializedTarget.FindProperty("highlightRoot");
            GameObject plugVisual = animatedVisualProperty != null
                ? animatedVisualProperty.objectReferenceValue as GameObject
                : null;
            GameObject highlight = highlightProperty != null
                ? highlightProperty.objectReferenceValue as GameObject
                : null;

            if (plugVisual == null)
            {
                Debug.LogWarning($"Could not scale installed spark plug for '{target.name}'.", target);
                return;
            }

            plugVisual.transform.SetPositionAndRotation(finalWorldPosition, cover.rotation);
            plugVisual.transform.localScale = SparkPlugScale;

            BoxCollider collider = target.GetComponent<BoxCollider>();
            if (collider != null)
            {
                collider.center = new Vector3(0f, 0.08f, 0f);
                collider.size = new Vector3(0.14f, 0.28f, 0.14f);
            }

            if (highlight != null)
            {
                highlight.transform.localPosition = new Vector3(0f, 0.015f, 0f);
                highlight.transform.localScale = new Vector3(0.065f, 0.010f, 0.065f);
            }

            target.Configure(
                station,
                EngineAssemblyInteractionKind.SparkPlug,
                target.GroupIndex,
                target.TargetIndex,
                1.25f,
                highlight,
                plugVisual,
                0.18f,
                4f);
        }

        private static bool ValidateItemScale(
            string path,
            Vector3 expectedScale,
            string itemName)
        {
            InventoryItemDefinition item =
                AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(path);

            if (item != null && Approximately(item.WorldScale, expectedScale, 0.005f))
            {
                return true;
            }

            Debug.LogError($"Test Hangar Step 5 failed: {itemName} world scale is missing or incorrect.");
            return false;
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

        private static Bounds CalculateRendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(root.transform.position, Vector3.zero);
            }

            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        private static bool Approximately(
            Vector3 value,
            Vector3 expected,
            float tolerance)
        {
            return Mathf.Abs(value.x - expected.x) <= tolerance
                && Mathf.Abs(value.y - expected.y) <= tolerance
                && Mathf.Abs(value.z - expected.z) <= tolerance;
        }
    }
}
