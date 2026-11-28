using System.Collections.Generic;
using Hanger51.EngineAssembly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class MerlinScaledHighlightRepairSetup
    {
        private const string StationName = "V-1650 Engine Stand";
        private const string LeftCoverName = "Installed Left Cylinder Cover";
        private const string RightCoverName = "Installed Right Cylinder Cover";
        private const string HighlightMaterialPath =
            "Assets/_Project/EngineAssembly/Materials/InstallHighlight.mat";

        private const float SparkPlugRootLocalY = 0.31f;
        private const float SparkPlugSeatLocalY = 0.453f;

        [MenuItem("Hanger 51/Test Hangar/8 - Repair Scaled Highlight Geometry")]
        public static void RepairScaledHighlightGeometry()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Test Hangar Step 8 failed. Exit Play mode first.");
                return;
            }

            EngineAssemblyStation station = FindStation();
            if (station == null)
            {
                Debug.LogError(
                    "Test Hangar Step 8 failed. The V-1650 engine stand is missing. Run the earlier setup steps first.");
                return;
            }

            Transform leftCover = station.transform.Find(LeftCoverName);
            Transform rightCover = station.transform.Find(RightCoverName);
            if (leftCover == null || rightCover == null)
            {
                Debug.LogError(
                    "Test Hangar Step 8 failed. The installed cylinder-cover visuals are missing.",
                    station);
                return;
            }

            Material highlightMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(HighlightMaterialPath);

            CoverGeometry leftGeometry = MeasureCoverGeometry(leftCover);
            CoverGeometry rightGeometry = MeasureCoverGeometry(rightCover);
            if (!leftGeometry.IsValid || !rightGeometry.IsValid)
            {
                Debug.LogError(
                    "Test Hangar Step 8 failed. Could not measure the physical cover meshes without interaction geometry.",
                    station);
                return;
            }

            EngineAssemblyInteractionTarget[] targets =
                station.GetComponentsInChildren<EngineAssemblyInteractionTarget>(true);

            int repairedCovers = 0;
            int repairedBolts = 0;
            int repairedPlugs = 0;

            for (int index = 0; index < targets.Length; index++)
            {
                EngineAssemblyInteractionTarget target = targets[index];
                Transform cover = target.GroupIndex == 0 ? leftCover : rightCover;
                CoverGeometry geometry = target.GroupIndex == 0
                    ? leftGeometry
                    : rightGeometry;

                switch (target.InteractionKind)
                {
                    case EngineAssemblyInteractionKind.CoverPlacement:
                        RepairCoverHighlight(
                            target,
                            station,
                            cover,
                            geometry,
                            highlightMaterial);
                        repairedCovers++;
                        break;

                    case EngineAssemblyInteractionKind.CoverBolt:
                        RepairBoltHighlight(
                            target,
                            station,
                            cover,
                            geometry,
                            highlightMaterial);
                        repairedBolts++;
                        break;

                    case EngineAssemblyInteractionKind.SparkPlug:
                        RepairSparkPlugHighlight(
                            target,
                            station,
                            cover,
                            highlightMaterial);
                        repairedPlugs++;
                        break;
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
                    "Test Hangar Step 8 repaired the highlights but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError(
                    "Test Hangar Step 8 repaired the highlights, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = station.gameObject;
            Debug.Log(
                $"Test Hangar Step 8 complete. Repaired {repairedCovers} cover highlights, "
                + $"{repairedBolts} bolt highlights, and {repairedPlugs} spark-plug highlights using clean physical cover bounds.",
                station);
        }

        [MenuItem("Hanger 51/Test Hangar/9 - Validate Repaired Highlights")]
        public static void ValidateRepairedHighlights()
        {
            bool passed = true;
            EngineAssemblyStation station = FindStation();
            if (station == null)
            {
                Debug.LogError("Test Hangar Step 9 failed: the V-1650 engine stand is missing.");
                return;
            }

            Transform leftCover = station.transform.Find(LeftCoverName);
            Transform rightCover = station.transform.Find(RightCoverName);
            if (leftCover == null || rightCover == null)
            {
                Debug.LogError(
                    "Test Hangar Step 9 failed: the installed cylinder-cover visuals are missing.",
                    station);
                return;
            }

            CoverGeometry leftGeometry = MeasureCoverGeometry(leftCover);
            CoverGeometry rightGeometry = MeasureCoverGeometry(rightCover);
            if (!leftGeometry.IsValid || !rightGeometry.IsValid)
            {
                Debug.LogError(
                    "Test Hangar Step 9 failed: clean cover geometry could not be measured.",
                    station);
                return;
            }

            int coverCount = 0;
            int boltCount = 0;
            int plugCount = 0;

            EngineAssemblyInteractionTarget[] targets =
                station.GetComponentsInChildren<EngineAssemblyInteractionTarget>(true);

            for (int index = 0; index < targets.Length; index++)
            {
                EngineAssemblyInteractionTarget target = targets[index];
                Transform cover = target.GroupIndex == 0 ? leftCover : rightCover;
                CoverGeometry geometry = target.GroupIndex == 0
                    ? leftGeometry
                    : rightGeometry;

                switch (target.InteractionKind)
                {
                    case EngineAssemblyInteractionKind.CoverPlacement:
                        coverCount++;
                        passed &= ValidateCoverHighlight(target, cover, geometry);
                        break;

                    case EngineAssemblyInteractionKind.CoverBolt:
                        boltCount++;
                        passed &= ValidateBoltHighlight(target, cover, geometry);
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
                    $"Test Hangar Step 9 failed: expected 2 cover highlights, found {coverCount}.");
                passed = false;
            }

            if (boltCount != 12)
            {
                Debug.LogError(
                    $"Test Hangar Step 9 failed: expected 12 bolt highlights, found {boltCount}.");
                passed = false;
            }

            if (plugCount != 24)
            {
                Debug.LogError(
                    $"Test Hangar Step 9 failed: expected 24 spark-plug highlights, found {plugCount}.");
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("Test Hangar Step 9 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Test Hangar Step 9 passed. Both cover footprints, all 12 bolt rings, and all 24 plug-well rings are correctly scaled and aligned using interaction-free cover geometry.",
                    station);
            }
        }

        private static void RepairCoverHighlight(
            EngineAssemblyInteractionTarget target,
            EngineAssemblyStation station,
            Transform cover,
            CoverGeometry geometry,
            Material highlightMaterial)
        {
            Undo.RecordObject(target.transform, "Repair scaled cover highlight");
            target.transform.SetPositionAndRotation(cover.position, cover.rotation);
            target.transform.localScale = Vector3.one;

            Bounds scaledBounds = CalculatePhysicalBounds(
                cover.gameObject,
                target.transform);

            Vector3 highlightLocalPosition = new Vector3(
                scaledBounds.center.x,
                scaledBounds.min.y + 0.006f,
                scaledBounds.center.z);
            Vector3 highlightLocalScale = new Vector3(
                scaledBounds.size.x * 1.025f,
                0.012f,
                scaledBounds.size.z * 1.012f);

            GameObject highlight = GetOrCreateHighlight(
                target,
                "Highlighted Cover Mount",
                PrimitiveType.Cube,
                highlightMaterial);
            SetHighlightLocalPose(
                highlight,
                target.transform,
                highlightLocalPosition,
                Quaternion.identity,
                highlightLocalScale);

            BoxCollider collider = target.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = Undo.AddComponent<BoxCollider>(target.gameObject);
            }

            collider.center = new Vector3(
                scaledBounds.center.x,
                scaledBounds.center.y,
                scaledBounds.center.z);
            collider.size = new Vector3(
                scaledBounds.size.x + 0.08f,
                Mathf.Max(0.20f, scaledBounds.size.y + 0.08f),
                scaledBounds.size.z + 0.08f);

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

        private static void RepairBoltHighlight(
            EngineAssemblyInteractionTarget target,
            EngineAssemblyStation station,
            Transform cover,
            CoverGeometry geometry,
            Material highlightMaterial)
        {
            if (target.transform.parent != cover)
            {
                Undo.SetTransformParent(
                    target.transform,
                    cover,
                    "Reparent repaired bolt target to cover");
            }

            int withinCover = Mathf.Clamp(
                target.TargetIndex - target.GroupIndex * 6,
                0,
                5);
            int sideIndex = withinCover / 3;
            int longitudinalIndex = withinCover % 3;
            float side = sideIndex == 0 ? -1f : 1f;

            float insetX = geometry.FullBounds.extents.x * 0.56f;
            float endMargin = geometry.FullBounds.size.z * 0.18f;
            float localZ = Mathf.Lerp(
                geometry.FullBounds.min.z + endMargin,
                geometry.FullBounds.max.z - endMargin,
                longitudinalIndex * 0.5f);
            float localY = geometry.BoltSeatY - 0.004f;

            target.transform.localPosition = new Vector3(
                side * insetX,
                localY,
                localZ);
            target.transform.localRotation = Quaternion.identity;
            target.transform.localScale = Vector3.one;

            GameObject highlight = GetOrCreateHighlight(
                target,
                "Bolt Highlight Ring",
                PrimitiveType.Cylinder,
                highlightMaterial);
            SetHighlightLocalPose(
                highlight,
                target.transform,
                new Vector3(0f, 0.010f, 0f),
                Quaternion.identity,
                new Vector3(0.11f, 0.006f, 0.11f));

            SphereCollider collider = target.GetComponent<SphereCollider>();
            if (collider == null)
            {
                collider = Undo.AddComponent<SphereCollider>(target.gameObject);
            }

            collider.center = new Vector3(0f, 0.025f, 0f);
            collider.radius = 0.22f;

            target.Configure(
                station,
                EngineAssemblyInteractionKind.CoverBolt,
                target.GroupIndex,
                target.TargetIndex,
                0.9f,
                highlight,
                GetAnimatedVisual(target),
                0.08f,
                3f);
            EditorUtility.SetDirty(target);
        }

        private static void RepairSparkPlugHighlight(
            EngineAssemblyInteractionTarget target,
            EngineAssemblyStation station,
            Transform cover,
            Material highlightMaterial)
        {
            int cylinderIndex = Mathf.Clamp(target.TargetIndex / 4, 0, 5);
            int indexWithinCylinder = target.TargetIndex % 4;
            bool outerPlug = indexWithinCylinder % 2 == 0;
            float localX = outerPlug ? -0.16f : 0.16f;
            float localZ = -1.35f + cylinderIndex * 0.54f;

            Vector3 targetWorldPosition = cover.TransformPoint(
                new Vector3(localX, SparkPlugRootLocalY, localZ));
            Quaternion targetWorldRotation = cover.rotation;
            target.transform.SetPositionAndRotation(
                targetWorldPosition,
                targetWorldRotation);
            target.transform.localScale = Vector3.one;

            Vector3 highlightWorldPosition = cover.TransformPoint(
                new Vector3(localX, SparkPlugSeatLocalY, localZ));

            GameObject highlight = GetOrCreateHighlight(
                target,
                "Spark Plug Well Highlight",
                PrimitiveType.Cylinder,
                highlightMaterial);
            Undo.SetTransformParent(
                highlight.transform,
                target.transform,
                "Reparent repaired spark-plug highlight");
            highlight.transform.SetPositionAndRotation(
                highlightWorldPosition,
                targetWorldRotation);
            highlight.transform.localScale = new Vector3(0.045f, 0.006f, 0.045f);
            DisableCollider(highlight);

            BoxCollider collider = target.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = Undo.AddComponent<BoxCollider>(target.gameObject);
            }

            Vector3 surfaceOffset = target.transform.InverseTransformPoint(
                highlightWorldPosition);
            collider.center = new Vector3(0f, surfaceOffset.y * 0.55f, 0f);
            collider.size = new Vector3(
                0.12f,
                Mathf.Max(0.14f, surfaceOffset.y + 0.10f),
                0.12f);

            GameObject animatedVisual = GetAnimatedVisual(target);
            if (animatedVisual != null)
            {
                animatedVisual.transform.SetPositionAndRotation(
                    targetWorldPosition,
                    targetWorldRotation);
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

        private static bool ValidateCoverHighlight(
            EngineAssemblyInteractionTarget target,
            Transform cover,
            CoverGeometry geometry)
        {
            GameObject highlight = GetHighlight(target);
            if (highlight == null)
            {
                Debug.LogError(
                    $"Repaired highlight validation failed: '{target.name}' has no cover highlight.",
                    target);
                return false;
            }

            Bounds scaledBounds = CalculatePhysicalBounds(
                cover.gameObject,
                target.transform);
            Vector3 expectedLocalPosition = new Vector3(
                scaledBounds.center.x,
                scaledBounds.min.y + 0.006f,
                scaledBounds.center.z);
            Vector3 expectedLocalScale = new Vector3(
                scaledBounds.size.x * 1.025f,
                0.012f,
                scaledBounds.size.z * 1.012f);

            float targetPositionError = Vector3.Distance(
                target.transform.position,
                cover.position);
            float targetRotationError = Quaternion.Angle(
                target.transform.rotation,
                cover.rotation);
            float highlightPositionError = Vector3.Distance(
                highlight.transform.localPosition,
                expectedLocalPosition);
            bool scaleMatches = Approximately(
                highlight.transform.localScale,
                expectedLocalScale,
                0.012f);

            if (targetPositionError > 0.008f
                || targetRotationError > 0.5f
                || highlightPositionError > 0.010f
                || !scaleMatches)
            {
                Debug.LogError(
                    $"Repaired highlight validation failed: '{target.name}' does not match its cover footprint. "
                    + $"Target position {targetPositionError:F3}, rotation {targetRotationError:F2}, highlight position {highlightPositionError:F3}.",
                    target);
                return false;
            }

            return true;
        }

        private static bool ValidateBoltHighlight(
            EngineAssemblyInteractionTarget target,
            Transform cover,
            CoverGeometry geometry)
        {
            GameObject highlight = GetHighlight(target);
            if (highlight == null)
            {
                Debug.LogError(
                    $"Repaired highlight validation failed: '{target.name}' has no bolt ring.",
                    target);
                return false;
            }

            int withinCover = Mathf.Clamp(
                target.TargetIndex - target.GroupIndex * 6,
                0,
                5);
            int sideIndex = withinCover / 3;
            int longitudinalIndex = withinCover % 3;
            float side = sideIndex == 0 ? -1f : 1f;
            float insetX = geometry.FullBounds.extents.x * 0.56f;
            float endMargin = geometry.FullBounds.size.z * 0.18f;
            float expectedZ = Mathf.Lerp(
                geometry.FullBounds.min.z + endMargin,
                geometry.FullBounds.max.z - endMargin,
                longitudinalIndex * 0.5f);
            Vector3 expectedTargetPosition = new Vector3(
                side * insetX,
                geometry.BoltSeatY - 0.004f,
                expectedZ);

            float targetError = Vector3.Distance(
                target.transform.localPosition,
                expectedTargetPosition);
            float ringError = Vector3.Distance(
                highlight.transform.localPosition,
                new Vector3(0f, 0.010f, 0f));
            float ringWorldDiameter = highlight.transform.TransformVector(
                Vector3.right).magnitude;

            bool insidePhysicalCover =
                Mathf.Abs(target.transform.localPosition.x)
                    < geometry.FullBounds.extents.x - 0.045f
                && target.transform.localPosition.z
                    > geometry.FullBounds.min.z + 0.30f
                && target.transform.localPosition.z
                    < geometry.FullBounds.max.z - 0.30f;
            bool diameterValid = ringWorldDiameter > 0.025f
                && ringWorldDiameter < 0.065f;

            if (target.transform.parent != cover
                || targetError > 0.012f
                || ringError > 0.008f
                || !insidePhysicalCover
                || !diameterValid)
            {
                Debug.LogError(
                    $"Repaired highlight validation failed: '{target.name}' is misaligned. "
                    + $"Target error {targetError:F3}, ring error {ringError:F3}, ring diameter {ringWorldDiameter:F3}.",
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
            if (highlight == null)
            {
                Debug.LogError(
                    $"Repaired highlight validation failed: '{target.name}' has no plug-well ring.",
                    target);
                return false;
            }

            int cylinderIndex = Mathf.Clamp(target.TargetIndex / 4, 0, 5);
            int indexWithinCylinder = target.TargetIndex % 4;
            bool outerPlug = indexWithinCylinder % 2 == 0;
            float localX = outerPlug ? -0.16f : 0.16f;
            float localZ = -1.35f + cylinderIndex * 0.54f;

            Vector3 expectedTargetPosition = cover.TransformPoint(
                new Vector3(localX, SparkPlugRootLocalY, localZ));
            Vector3 expectedHighlightPosition = cover.TransformPoint(
                new Vector3(localX, SparkPlugSeatLocalY, localZ));

            float targetError = Vector3.Distance(
                target.transform.position,
                expectedTargetPosition);
            float highlightError = Vector3.Distance(
                highlight.transform.position,
                expectedHighlightPosition);
            float ringWorldDiameter = highlight.transform.TransformVector(
                Vector3.right).magnitude;

            if (targetError > 0.010f
                || highlightError > 0.010f
                || ringWorldDiameter < 0.025f
                || ringWorldDiameter > 0.070f)
            {
                Debug.LogError(
                    $"Repaired highlight validation failed: '{target.name}' is misaligned. "
                    + $"Target error {targetError:F3}, highlight error {highlightError:F3}, ring diameter {ringWorldDiameter:F3}.",
                    target);
                return false;
            }

            return true;
        }

        private static CoverGeometry MeasureCoverGeometry(Transform cover)
        {
            Bounds fullBounds = CalculatePhysicalBounds(
                cover.gameObject,
                cover);

            Transform paintedBody = FindDescendant(
                cover,
                "Painted Cover Body");
            Bounds bodyBounds = paintedBody != null
                ? CalculatePhysicalBounds(paintedBody.gameObject, cover)
                : fullBounds;

            return new CoverGeometry(
                fullBounds,
                bodyBounds.max.y,
                fullBounds.size.sqrMagnitude > 0.001f);
        }

        private static Bounds CalculatePhysicalBounds(
            GameObject root,
            Transform reference)
        {
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            bool hasPoint = false;
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);

            for (int index = 0; index < filters.Length; index++)
            {
                MeshFilter filter = filters[index];
                if (filter == null
                    || filter.sharedMesh == null
                    || IsInteractionGeometry(filter.transform, root.transform))
                {
                    continue;
                }

                Bounds meshBounds = filter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 localCorner = new Vector3(
                        (corner & 1) == 0 ? meshBounds.min.x : meshBounds.max.x,
                        (corner & 2) == 0 ? meshBounds.min.y : meshBounds.max.y,
                        (corner & 4) == 0 ? meshBounds.min.z : meshBounds.max.z);
                    Vector3 point = reference.InverseTransformPoint(
                        filter.transform.TransformPoint(localCorner));

                    if (!hasPoint)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        hasPoint = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }

            return hasPoint
                ? bounds
                : new Bounds(
                    reference.InverseTransformPoint(root.transform.position),
                    Vector3.zero);
        }

        private static bool IsInteractionGeometry(
            Transform candidate,
            Transform physicalRoot)
        {
            Transform current = candidate;
            while (current != null && current != physicalRoot)
            {
                if (current.GetComponent<EngineAssemblyInteractionTarget>() != null)
                {
                    return true;
                }

                current = current.parent;
            }

            return candidate.name.Contains("Highlight")
                || candidate.name.Contains("Bolt Assembly");
        }

        private static Transform FindDescendant(
            Transform root,
            string objectName)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index].name == objectName)
                {
                    return transforms[index];
                }
            }

            return null;
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
                Undo.RegisterCreatedObjectUndo(
                    highlight,
                    $"Create repaired {objectName}");
                highlight.name = objectName;
            }

            Undo.SetTransformParent(
                highlight.transform,
                target.transform,
                $"Parent repaired {objectName}");

            Renderer renderer = highlight.GetComponentInChildren<Renderer>(true);
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            DisableCollider(highlight);
            return highlight;
        }

        private static void SetHighlightLocalPose(
            GameObject highlight,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale)
        {
            Undo.SetTransformParent(
                highlight.transform,
                parent,
                $"Parent {highlight.name}");
            highlight.transform.localPosition = localPosition;
            highlight.transform.localRotation = localRotation;
            highlight.transform.localScale = localScale;
            DisableCollider(highlight);
            EditorUtility.SetDirty(highlight.transform);
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

        private static void DisableCollider(GameObject visual)
        {
            Collider[] colliders = visual.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                colliders[index].enabled = false;
                EditorUtility.SetDirty(colliders[index]);
            }
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

        private static EngineAssemblyStation FindStation()
        {
            EngineAssemblyStation[] stations = Object.FindObjectsByType<EngineAssemblyStation>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int index = 0; index < stations.Length; index++)
            {
                if (stations[index] != null
                    && stations[index].name == StationName)
                {
                    return stations[index];
                }
            }

            return null;
        }

        private readonly struct CoverGeometry
        {
            public CoverGeometry(
                Bounds fullBounds,
                float boltSeatY,
                bool isValid)
            {
                FullBounds = fullBounds;
                BoltSeatY = boltSeatY;
                IsValid = isValid;
            }

            public Bounds FullBounds { get; }
            public float BoltSeatY { get; }
            public bool IsValid { get; }
        }
    }
}
