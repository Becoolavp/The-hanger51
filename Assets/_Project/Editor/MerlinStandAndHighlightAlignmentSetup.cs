using System.Collections.Generic;
using Hanger51.EngineAssembly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class MerlinStandAndHighlightAlignmentSetup
    {
        private const string StationName = "V-1650 Engine Stand";
        private const string EngineVisualName = "Installed Engine Core";
        private const string LeftCoverName = "Installed Left Cylinder Cover";
        private const string RightCoverName = "Installed Right Cylinder Cover";
        private const string HighlightMaterialPath =
            "Assets/_Project/EngineAssembly/Materials/InstallHighlight.mat";

        private const float CoverTopLocalY = 0.447f;
        private const float SparkPlugRootLocalY = 0.31f;

        [MenuItem("Hanger 51/Test Hangar/6 - Rescale Stand and Align Highlights")]
        public static void RescaleStandAndAlignHighlights()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Test Hangar Step 6 failed. Exit Play mode first.");
                return;
            }

            EngineAssemblyStation station = FindStation();
            if (station == null)
            {
                Debug.LogError(
                    "Test Hangar Step 6 failed. The V-1650 engine stand is missing. Run the earlier Merlin setup steps first.");
                return;
            }

            Transform installedEngine = station.transform.Find(EngineVisualName);
            Transform leftCover = station.transform.Find(LeftCoverName);
            Transform rightCover = station.transform.Find(RightCoverName);
            if (installedEngine == null || leftCover == null || rightCover == null)
            {
                Debug.LogError(
                    "Test Hangar Step 6 failed. The installed engine or cylinder-cover visuals are missing.",
                    station);
                return;
            }

            Material highlightMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(HighlightMaterialPath);

            Bounds engineBounds = CalculateBoundsRelativeTo(
                installedEngine.gameObject,
                station.transform);
            RescaleStand(station, engineBounds);

            EngineAssemblyInteractionTarget[] targets =
                station.GetComponentsInChildren<EngineAssemblyInteractionTarget>(true);

            int coverHighlights = 0;
            int boltHighlights = 0;
            int plugHighlights = 0;

            for (int index = 0; index < targets.Length; index++)
            {
                EngineAssemblyInteractionTarget target = targets[index];
                switch (target.InteractionKind)
                {
                    case EngineAssemblyInteractionKind.CoverPlacement:
                    {
                        Transform cover = target.GroupIndex == 0 ? leftCover : rightCover;
                        AlignCoverPlacementTarget(
                            target,
                            station,
                            cover,
                            highlightMaterial);
                        coverHighlights++;
                        break;
                    }

                    case EngineAssemblyInteractionKind.CoverBolt:
                    {
                        Transform cover = target.GroupIndex == 0 ? leftCover : rightCover;
                        AlignBoltTarget(
                            target,
                            station,
                            cover,
                            highlightMaterial);
                        boltHighlights++;
                        break;
                    }

                    case EngineAssemblyInteractionKind.SparkPlug:
                    {
                        Transform cover = target.GroupIndex == 0 ? leftCover : rightCover;
                        AlignSparkPlugTarget(
                            target,
                            station,
                            cover,
                            highlightMaterial);
                        plugHighlights++;
                        break;
                    }
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
                Debug.LogError(
                    "Test Hangar Step 6 aligned the stand and highlights but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError(
                    "Test Hangar Step 6 aligned the stand and highlights, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = station.gameObject;
            Debug.Log(
                $"Test Hangar Step 6 complete. Rescaled the maintenance stand to the current engine, aligned {coverHighlights} cover highlights, "
                + $"{boltHighlights} bolt highlights, and {plugHighlights} spark-plug highlights, saved the scene, and prepared Build and Run.",
                station);
        }

        [MenuItem("Hanger 51/Test Hangar/7 - Validate Stand and Highlights")]
        public static void ValidateStandAndHighlights()
        {
            bool passed = true;
            EngineAssemblyStation station = FindStation();
            if (station == null)
            {
                Debug.LogError("Test Hangar Step 7 failed: the V-1650 engine stand is missing.");
                return;
            }

            Transform engine = station.transform.Find(EngineVisualName);
            Transform leftCover = station.transform.Find(LeftCoverName);
            Transform rightCover = station.transform.Find(RightCoverName);
            if (engine == null || leftCover == null || rightCover == null)
            {
                Debug.LogError(
                    "Test Hangar Step 7 failed: the installed engine or cover visuals are missing.",
                    station);
                return;
            }

            Bounds engineBounds = CalculateBoundsRelativeTo(engine.gameObject, station.transform);
            passed &= ValidateStand(station, engineBounds);

            EngineAssemblyInteractionTarget[] targets =
                station.GetComponentsInChildren<EngineAssemblyInteractionTarget>(true);
            int coverCount = 0;
            int boltCount = 0;
            int plugCount = 0;

            for (int index = 0; index < targets.Length; index++)
            {
                EngineAssemblyInteractionTarget target = targets[index];
                Transform cover = target.GroupIndex == 0 ? leftCover : rightCover;

                switch (target.InteractionKind)
                {
                    case EngineAssemblyInteractionKind.CoverPlacement:
                        coverCount++;
                        passed &= ValidateCoverHighlight(target, cover);
                        break;

                    case EngineAssemblyInteractionKind.CoverBolt:
                        boltCount++;
                        passed &= ValidateBoltHighlight(target, cover);
                        break;

                    case EngineAssemblyInteractionKind.SparkPlug:
                        plugCount++;
                        passed &= ValidateSparkPlugHighlight(target, cover);
                        break;
                }
            }

            if (coverCount != 2)
            {
                Debug.LogError(
                    $"Test Hangar Step 7 failed: expected 2 cover highlights, found {coverCount}.");
                passed = false;
            }

            if (boltCount != 12)
            {
                Debug.LogError(
                    $"Test Hangar Step 7 failed: expected 12 bolt highlights, found {boltCount}.");
                passed = false;
            }

            if (plugCount != 24)
            {
                Debug.LogError(
                    $"Test Hangar Step 7 failed: expected 24 spark-plug highlights, found {plugCount}.");
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("Test Hangar Step 7 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Test Hangar Step 7 passed. The engine stand fits the current real-scale engine, both cover mounting highlights match their covers, "
                    + "all 12 bolt highlights are centered on inset fasteners, all 24 plug-well highlights sit on the cover surfaces, and Build and Run is ready.",
                    station);
            }
        }

        private static void RescaleStand(
            EngineAssemblyStation station,
            Bounds engineBounds)
        {
            float engineLength = Mathf.Max(1.9f, engineBounds.size.z);
            float engineWidth = Mathf.Max(0.8f, engineBounds.size.x);

            float standLength = Mathf.Clamp(engineLength + 0.48f, 2.45f, 3.15f);
            float standWidth = Mathf.Clamp(engineWidth + 0.38f, 1.22f, 1.70f);
            float halfLength = standLength * 0.5f;
            float halfWidth = standWidth * 0.5f;
            float baseY = 0.13f;
            float saddleY = Mathf.Clamp(engineBounds.min.y - 0.055f, 0.78f, 1.14f);
            float postHeight = Mathf.Max(0.55f, saddleY - baseY);
            float postCenterY = baseY + postHeight * 0.5f;
            float postX = halfWidth - 0.12f;
            float postZ = Mathf.Min(engineLength * 0.35f, halfLength - 0.28f);
            float saddleX = Mathf.Clamp(engineWidth * 0.24f, 0.22f, 0.34f);
            float saddleLength = Mathf.Clamp(engineLength * 0.78f, 1.55f, 2.15f);
            float wheelX = halfWidth - 0.04f;
            float wheelZ = halfLength - 0.14f;

            SetPartPose(station.transform, "Left Base Rail",
                new Vector3(-halfWidth + 0.07f, baseY, 0f),
                new Vector3(0.12f, 0.16f, standLength),
                Vector3.zero);
            SetPartPose(station.transform, "Right Base Rail",
                new Vector3(halfWidth - 0.07f, baseY, 0f),
                new Vector3(0.12f, 0.16f, standLength),
                Vector3.zero);
            SetPartPose(station.transform, "Front Cross Rail",
                new Vector3(0f, baseY, halfLength - 0.07f),
                new Vector3(standWidth, 0.16f, 0.12f),
                Vector3.zero);
            SetPartPose(station.transform, "Rear Cross Rail",
                new Vector3(0f, baseY, -halfLength + 0.07f),
                new Vector3(standWidth, 0.16f, 0.12f),
                Vector3.zero);

            for (int side = -1; side <= 1; side += 2)
            {
                SetPartPose(station.transform, $"Vertical Post {side} Front",
                    new Vector3(side * postX, postCenterY, postZ),
                    new Vector3(0.11f, postHeight, 0.11f),
                    Vector3.zero);
                SetPartPose(station.transform, $"Vertical Post {side} Rear",
                    new Vector3(side * postX, postCenterY, -postZ),
                    new Vector3(0.11f, postHeight, 0.11f),
                    Vector3.zero);
                SetPartPose(station.transform, $"Engine Saddle {side}",
                    new Vector3(side * saddleX, saddleY, 0f),
                    new Vector3(0.11f, 0.10f, saddleLength),
                    Vector3.zero);

                for (int zSide = -1; zSide <= 1; zSide += 2)
                {
                    SetPartPose(station.transform, $"Caster Wheel {side} {zSide}",
                        new Vector3(side * wheelX, 0.075f, zSide * wheelZ),
                        new Vector3(0.14f, 0.05f, 0.14f),
                        new Vector3(0f, 0f, 90f));
                }
            }

            SetPartPose(station.transform, "Front Diagonal Brace",
                new Vector3(0f, postCenterY, halfLength - 0.24f),
                new Vector3(standWidth * 0.82f, 0.07f, 0.07f),
                new Vector3(0f, 0f, 20f));
            SetPartPose(station.transform, "Rear Diagonal Brace",
                new Vector3(0f, postCenterY, -halfLength + 0.24f),
                new Vector3(standWidth * 0.82f, 0.07f, 0.07f),
                new Vector3(0f, 0f, -20f));

            BoxCollider stationCollider = station.GetComponent<BoxCollider>();
            if (stationCollider == null)
            {
                stationCollider = Undo.AddComponent<BoxCollider>(station.gameObject);
            }

            float maximumY = Mathf.Max(engineBounds.max.y, saddleY + 0.16f);
            stationCollider.center = new Vector3(
                engineBounds.center.x,
                maximumY * 0.5f,
                engineBounds.center.z);
            stationCollider.size = new Vector3(
                Mathf.Max(standWidth + 0.22f, engineBounds.size.x + 0.20f),
                maximumY + 0.18f,
                Mathf.Max(standLength + 0.22f, engineBounds.size.z + 0.20f));
            EditorUtility.SetDirty(stationCollider);
        }

        private static void AlignCoverPlacementTarget(
            EngineAssemblyInteractionTarget target,
            EngineAssemblyStation station,
            Transform cover,
            Material highlightMaterial)
        {
            if (cover == null)
            {
                return;
            }

            Undo.RecordObject(target.transform, "Align cover mounting highlight");
            target.transform.SetPositionAndRotation(cover.position, cover.rotation);
            target.transform.localScale = Vector3.one;

            Bounds coverBounds = CalculateBoundsRelativeTo(
                cover.gameObject,
                target.transform);
            float width = Mathf.Clamp(coverBounds.size.x * 1.06f, 0.22f, 0.42f);
            float length = Mathf.Clamp(coverBounds.size.z * 1.03f, 1.05f, 1.65f);

            GameObject highlight = GetOrCreateHighlight(
                target,
                "Highlighted Cover Mount",
                PrimitiveType.Cube,
                highlightMaterial);
            highlight.transform.localPosition = new Vector3(
                coverBounds.center.x,
                coverBounds.min.y + 0.009f,
                coverBounds.center.z);
            highlight.transform.localRotation = Quaternion.identity;
            highlight.transform.localScale = new Vector3(width, 0.016f, length);
            DisableCollider(highlight);

            BoxCollider collider = target.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = Undo.AddComponent<BoxCollider>(target.gameObject);
            }

            collider.center = new Vector3(
                coverBounds.center.x,
                Mathf.Max(0.08f, coverBounds.center.y),
                coverBounds.center.z);
            collider.size = new Vector3(
                width + 0.12f,
                Mathf.Max(0.28f, coverBounds.size.y + 0.12f),
                length + 0.12f);

            target.Configure(
                station,
                EngineAssemblyInteractionKind.CoverPlacement,
                target.GroupIndex,
                target.TargetIndex,
                0.9f,
                highlight,
                cover.gameObject,
                0.22f,
                0f);
            EditorUtility.SetDirty(target);
        }

        private static void AlignBoltTarget(
            EngineAssemblyInteractionTarget target,
            EngineAssemblyStation station,
            Transform cover,
            Material highlightMaterial)
        {
            if (cover == null)
            {
                return;
            }

            if (target.transform.parent != cover)
            {
                Undo.SetTransformParent(target.transform, cover, "Reparent bolt target to cover");
            }

            Bounds coverLocalBounds = CalculateBoundsRelativeTo(
                cover.gameObject,
                cover);
            int withinCover = target.TargetIndex - target.GroupIndex * 6;
            withinCover = Mathf.Clamp(withinCover, 0, 5);
            int sideIndex = withinCover / 3;
            int longitudinalIndex = withinCover % 3;
            float side = sideIndex == 0 ? -1f : 1f;
            float insetX = Mathf.Min(0.20f, coverLocalBounds.extents.x * 0.58f);
            float endMargin = Mathf.Min(0.55f, coverLocalBounds.size.z * 0.18f);
            float z = Mathf.Lerp(
                coverLocalBounds.min.z + endMargin,
                coverLocalBounds.max.z - endMargin,
                longitudinalIndex * 0.5f);
            float y = coverLocalBounds.max.y - 0.040f;

            target.transform.localPosition = new Vector3(side * insetX, y, z);
            target.transform.localRotation = Quaternion.identity;
            target.transform.localScale = Vector3.one;

            GameObject highlight = GetOrCreateHighlight(
                target,
                "Bolt Highlight Ring",
                PrimitiveType.Cylinder,
                highlightMaterial);
            highlight.transform.localPosition = new Vector3(0f, 0.010f, 0f);
            highlight.transform.localRotation = Quaternion.identity;
            highlight.transform.localScale = new Vector3(0.13f, 0.006f, 0.13f);
            DisableCollider(highlight);

            SphereCollider collider = target.GetComponent<SphereCollider>();
            if (collider == null)
            {
                collider = Undo.AddComponent<SphereCollider>(target.gameObject);
            }

            collider.center = new Vector3(0f, 0.03f, 0f);
            collider.radius = 0.30f;

            GameObject animatedVisual = GetAnimatedVisual(target);
            target.Configure(
                station,
                EngineAssemblyInteractionKind.CoverBolt,
                target.GroupIndex,
                target.TargetIndex,
                0.9f,
                highlight,
                animatedVisual,
                0.08f,
                3f);
            EditorUtility.SetDirty(target);
        }

        private static void AlignSparkPlugTarget(
            EngineAssemblyInteractionTarget target,
            EngineAssemblyStation station,
            Transform cover,
            Material highlightMaterial)
        {
            if (cover == null)
            {
                return;
            }

            int cylinderIndex = Mathf.Clamp(target.TargetIndex / 4, 0, 5);
            int indexWithinCylinder = target.TargetIndex % 4;
            bool outerPlug = indexWithinCylinder % 2 == 0;
            float localX = outerPlug ? -0.16f : 0.16f;
            float localZ = -1.35f + cylinderIndex * 0.54f;

            Vector3 finalPosition = cover.TransformPoint(
                new Vector3(localX, SparkPlugRootLocalY, localZ));
            target.transform.SetPositionAndRotation(finalPosition, cover.rotation);
            target.transform.localScale = Vector3.one;

            Vector3 surfacePosition = cover.TransformPoint(
                new Vector3(localX, CoverTopLocalY + 0.006f, localZ));

            GameObject highlight = GetOrCreateHighlight(
                target,
                "Spark Plug Well Highlight",
                PrimitiveType.Cylinder,
                highlightMaterial);
            highlight.transform.localPosition =
                target.transform.InverseTransformPoint(surfacePosition);
            highlight.transform.localRotation = Quaternion.identity;
            highlight.transform.localScale = new Vector3(0.050f, 0.006f, 0.050f);
            DisableCollider(highlight);

            BoxCollider collider = target.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = Undo.AddComponent<BoxCollider>(target.gameObject);
            }

            collider.center = new Vector3(0f, 0.075f, 0f);
            collider.size = new Vector3(0.13f, 0.23f, 0.13f);

            GameObject animatedVisual = GetAnimatedVisual(target);
            if (animatedVisual != null)
            {
                animatedVisual.transform.SetPositionAndRotation(
                    finalPosition,
                    cover.rotation);
            }

            target.Configure(
                station,
                EngineAssemblyInteractionKind.SparkPlug,
                target.GroupIndex,
                target.TargetIndex,
                1.25f,
                highlight,
                animatedVisual,
                0.16f,
                4f);
            EditorUtility.SetDirty(target);
        }

        private static bool ValidateStand(
            EngineAssemblyStation station,
            Bounds engineBounds)
        {
            Transform leftRail = station.transform.Find("Left Base Rail");
            Transform rightRail = station.transform.Find("Right Base Rail");
            Transform frontRail = station.transform.Find("Front Cross Rail");
            Transform rearRail = station.transform.Find("Rear Cross Rail");
            if (leftRail == null || rightRail == null || frontRail == null || rearRail == null)
            {
                Debug.LogError("Stand validation failed: one or more base rails are missing.", station);
                return false;
            }

            float standLength = Mathf.Max(leftRail.localScale.z, rightRail.localScale.z);
            float standWidth = Mathf.Max(frontRail.localScale.x, rearRail.localScale.x);
            bool lengthFits = standLength >= engineBounds.size.z + 0.20f
                && standLength <= engineBounds.size.z + 0.90f;
            bool widthFits = standWidth >= engineBounds.size.x + 0.15f
                && standWidth <= engineBounds.size.x + 0.70f;

            if (!lengthFits || !widthFits)
            {
                Debug.LogError(
                    $"Stand validation failed: stand is {standWidth:F2} m wide by {standLength:F2} m long, "
                    + $"while the engine is {engineBounds.size.x:F2} m wide by {engineBounds.size.z:F2} m long.",
                    station);
                return false;
            }

            return true;
        }

        private static bool ValidateCoverHighlight(
            EngineAssemblyInteractionTarget target,
            Transform cover)
        {
            GameObject highlight = GetHighlight(target);
            if (cover == null || highlight == null)
            {
                Debug.LogError($"Highlight validation failed: '{target.name}' is missing its cover or highlight.", target);
                return false;
            }

            float positionError = Vector3.Distance(target.transform.position, cover.position);
            float rotationError = Quaternion.Angle(target.transform.rotation, cover.rotation);
            Bounds coverBounds = CalculateBoundsRelativeTo(cover.gameObject, target.transform);
            Renderer renderer = highlight.GetComponentInChildren<Renderer>(true);
            if (renderer == null)
            {
                Debug.LogError($"Highlight validation failed: '{target.name}' has no visible cover highlight.", target);
                return false;
            }

            Vector3 highlightSize = renderer.bounds.size;
            bool sizeFits = highlightSize.x >= coverBounds.size.x * 0.75f
                && highlightSize.x <= coverBounds.size.x * 1.35f
                && highlightSize.z >= coverBounds.size.z * 0.75f
                && highlightSize.z <= coverBounds.size.z * 1.35f;

            if (positionError > 0.01f || rotationError > 0.5f || !sizeFits)
            {
                Debug.LogError(
                    $"Highlight validation failed: '{target.name}' does not match its scaled cover. "
                    + $"Position error {positionError:F3}, rotation error {rotationError:F2} degrees.",
                    target);
                return false;
            }

            return true;
        }

        private static bool ValidateBoltHighlight(
            EngineAssemblyInteractionTarget target,
            Transform cover)
        {
            GameObject highlight = GetHighlight(target);
            Renderer renderer = highlight != null
                ? highlight.GetComponentInChildren<Renderer>(true)
                : null;
            if (cover == null || highlight == null || renderer == null)
            {
                Debug.LogError($"Highlight validation failed: '{target.name}' is missing its bolt highlight.", target);
                return false;
            }

            Bounds coverBounds = CalculateBoundsRelativeTo(cover.gameObject, cover);
            Vector3 localPosition = target.transform.localPosition;
            bool insideCover = Mathf.Abs(localPosition.x) < coverBounds.extents.x - 0.035f
                && localPosition.z > coverBounds.min.z + 0.20f
                && localPosition.z < coverBounds.max.z - 0.20f
                && Mathf.Abs(localPosition.y - (coverBounds.max.y - 0.040f)) < 0.025f;
            bool highlightSizeFits = renderer.bounds.size.x > 0.018f
                && renderer.bounds.size.x < 0.09f
                && renderer.bounds.size.z > 0.018f
                && renderer.bounds.size.z < 0.09f;

            if (!insideCover || !highlightSizeFits)
            {
                Debug.LogError(
                    $"Highlight validation failed: '{target.name}' is not centered on an inset, correctly scaled cover bolt.",
                    target);
                return false;
            }

            return true;
        }

        private static bool ValidateSparkPlugHighlight(
            EngineAssemblyInteractionTarget target,
            Transform cover)
        {
            GameObject highlight = GetHighlight(target);
            Renderer renderer = highlight != null
                ? highlight.GetComponentInChildren<Renderer>(true)
                : null;
            if (cover == null || highlight == null || renderer == null)
            {
                Debug.LogError($"Highlight validation failed: '{target.name}' is missing its plug-well highlight.", target);
                return false;
            }

            int cylinderIndex = Mathf.Clamp(target.TargetIndex / 4, 0, 5);
            int indexWithinCylinder = target.TargetIndex % 4;
            bool outerPlug = indexWithinCylinder % 2 == 0;
            float localX = outerPlug ? -0.16f : 0.16f;
            float localZ = -1.35f + cylinderIndex * 0.54f;
            Vector3 expectedTargetPosition = cover.TransformPoint(
                new Vector3(localX, SparkPlugRootLocalY, localZ));
            Vector3 expectedSurfacePosition = cover.TransformPoint(
                new Vector3(localX, CoverTopLocalY + 0.006f, localZ));

            float targetError = Vector3.Distance(target.transform.position, expectedTargetPosition);
            float highlightError = Vector3.Distance(renderer.bounds.center, expectedSurfacePosition);
            bool highlightSizeFits = renderer.bounds.size.x > 0.018f
                && renderer.bounds.size.x < 0.10f
                && renderer.bounds.size.z > 0.018f
                && renderer.bounds.size.z < 0.10f;

            if (targetError > 0.012f || highlightError > 0.035f || !highlightSizeFits)
            {
                Debug.LogError(
                    $"Highlight validation failed: '{target.name}' is not aligned with its scaled plug well. "
                    + $"Target error {targetError:F3}, highlight error {highlightError:F3}.",
                    target);
                return false;
            }

            return true;
        }

        private static GameObject GetOrCreateHighlight(
            EngineAssemblyInteractionTarget target,
            string objectName,
            PrimitiveType primitiveType,
            Material material)
        {
            GameObject highlight = GetHighlight(target);
            if (highlight == null)
            {
                highlight = GameObject.CreatePrimitive(primitiveType);
                Undo.RegisterCreatedObjectUndo(highlight, $"Create {objectName}");
                highlight.name = objectName;
                highlight.transform.SetParent(target.transform, false);
            }

            Renderer renderer = highlight.GetComponentInChildren<Renderer>(true);
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            return highlight;
        }

        private static GameObject GetHighlight(
            EngineAssemblyInteractionTarget target)
        {
            SerializedObject serializedTarget = new SerializedObject(target);
            SerializedProperty property = serializedTarget.FindProperty("highlightRoot");
            return property != null
                ? property.objectReferenceValue as GameObject
                : null;
        }

        private static GameObject GetAnimatedVisual(
            EngineAssemblyInteractionTarget target)
        {
            SerializedObject serializedTarget = new SerializedObject(target);
            SerializedProperty property = serializedTarget.FindProperty("animatedVisual");
            return property != null
                ? property.objectReferenceValue as GameObject
                : null;
        }

        private static void SetPartPose(
            Transform root,
            string partName,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEulerAngles)
        {
            Transform part = root.Find(partName);
            if (part == null)
            {
                Debug.LogWarning($"Could not resize missing stand part '{partName}'.", root);
                return;
            }

            Undo.RecordObject(part, "Resize real-scale engine stand");
            part.localPosition = localPosition;
            part.localScale = localScale;
            part.localRotation = Quaternion.Euler(localEulerAngles);
            EditorUtility.SetDirty(part);
        }

        private static void DisableCollider(GameObject visual)
        {
            Collider[] colliders = visual.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                colliders[index].enabled = false;
                EditorUtility.SetDirty(colliders[index]);
            }
        }

        private static Bounds CalculateBoundsRelativeTo(
            GameObject root,
            Transform reference)
        {
            MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            bool hasPoint = false;
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);

            for (int filterIndex = 0; filterIndex < meshFilters.Length; filterIndex++)
            {
                MeshFilter filter = meshFilters[filterIndex];
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                Bounds meshBounds = filter.sharedMesh.bounds;
                Vector3 min = meshBounds.min;
                Vector3 max = meshBounds.max;

                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 localCorner = new Vector3(
                        (corner & 1) == 0 ? min.x : max.x,
                        (corner & 2) == 0 ? min.y : max.y,
                        (corner & 4) == 0 ? min.z : max.z);
                    Vector3 worldCorner = filter.transform.TransformPoint(localCorner);
                    Vector3 referencePoint = reference.InverseTransformPoint(worldCorner);

                    if (!hasPoint)
                    {
                        bounds = new Bounds(referencePoint, Vector3.zero);
                        hasPoint = true;
                    }
                    else
                    {
                        bounds.Encapsulate(referencePoint);
                    }
                }
            }

            if (hasPoint)
            {
                return bounds;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(reference.InverseTransformPoint(root.transform.position), Vector3.zero);
            }

            Bounds worldBounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                worldBounds.Encapsulate(renderers[index].bounds);
            }

            return TransformWorldBounds(worldBounds, reference);
        }

        private static Bounds TransformWorldBounds(
            Bounds worldBounds,
            Transform reference)
        {
            Bounds result = new Bounds(
                reference.InverseTransformPoint(worldBounds.center),
                Vector3.zero);
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;

            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 worldCorner = new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z);
                result.Encapsulate(reference.InverseTransformPoint(worldCorner));
            }

            return result;
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
    }
}
