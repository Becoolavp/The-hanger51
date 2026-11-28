using System.Collections.Generic;
using Hanger51.EngineAssembly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class MerlinFinalPlacementVisualRepairSetup
    {
        private const string StationName = "V-1650 Engine Stand";
        private const string LeftCoverName = "Installed Left Cylinder Cover";
        private const string RightCoverName = "Installed Right Cylinder Cover";
        private const string HighlightMaterialPath =
            "Assets/_Project/EngineAssembly/Materials/InstallHighlight.mat";

        private const float BoltSeatLocalY = 0.405f;
        private const float SparkPlugRootLocalY = 0.31f;
        private const float SparkPlugMarkerLocalY = 0.50f;

        private static readonly float[] BoltZPositions =
        {
            -1.08f,
            0f,
            1.08f
        };

        [MenuItem("Hanger 51/Test Hangar/10 - Final Placement Visual Repair")]
        public static void ApplyFinalPlacementVisualRepair()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Test Hangar Step 10 failed. Exit Play mode first.");
                return;
            }

            EngineAssemblyStation station = FindStation();
            if (station == null)
            {
                Debug.LogError(
                    "Test Hangar Step 10 failed. The V-1650 engine stand is missing. Run the earlier setup steps first.");
                return;
            }

            Transform leftCover = station.transform.Find(LeftCoverName);
            Transform rightCover = station.transform.Find(RightCoverName);
            if (leftCover == null || rightCover == null)
            {
                Debug.LogError(
                    "Test Hangar Step 10 failed. The installed cylinder covers are missing.",
                    station);
                return;
            }

            Material highlightMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(HighlightMaterialPath);
            MakeHighlightMaterialHighlyVisible(highlightMaterial);

            CoverFootprint leftFootprint = MeasureLowerFlange(leftCover);
            CoverFootprint rightFootprint = MeasureLowerFlange(rightCover);
            if (!leftFootprint.IsValid || !rightFootprint.IsValid)
            {
                Debug.LogError(
                    "Test Hangar Step 10 failed. A cylinder-cover Lower Flange could not be measured.",
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
                CoverFootprint footprint = target.GroupIndex == 0
                    ? leftFootprint
                    : rightFootprint;

                switch (target.InteractionKind)
                {
                    case EngineAssemblyInteractionKind.CoverPlacement:
                        RepairCoverPlacementVisual(
                            target,
                            station,
                            cover,
                            footprint,
                            highlightMaterial);
                        repairedCovers++;
                        break;

                    case EngineAssemblyInteractionKind.CoverBolt:
                        RepairBoltPositionAndVisual(
                            target,
                            station,
                            cover,
                            highlightMaterial);
                        repairedBolts++;
                        break;

                    case EngineAssemblyInteractionKind.SparkPlug:
                        RepairSparkPlugPlacementVisual(
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

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (string.IsNullOrWhiteSpace(scene.path)
                || !EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError(
                    "Test Hangar Step 10 repaired the placement visuals but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError(
                    "Test Hangar Step 10 repaired the placement visuals, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = station.gameObject;
            Debug.Log(
                $"Test Hangar Step 10 complete. Rebuilt {repairedCovers} compact cover placement areas, "
                + $"normalized {repairedBolts} cover bolts to one seating plane, and created {repairedPlugs} highly visible spark-plug placement markers.",
                station);
        }

        [MenuItem("Hanger 51/Test Hangar/11 - Validate Final Placement Visuals")]
        public static void ValidateFinalPlacementVisuals()
        {
            bool passed = true;
            EngineAssemblyStation station = FindStation();
            if (station == null)
            {
                Debug.LogError("Test Hangar Step 11 failed: the V-1650 engine stand is missing.");
                return;
            }

            Transform leftCover = station.transform.Find(LeftCoverName);
            Transform rightCover = station.transform.Find(RightCoverName);
            if (leftCover == null || rightCover == null)
            {
                Debug.LogError(
                    "Test Hangar Step 11 failed: the installed cylinder covers are missing.",
                    station);
                return;
            }

            CoverFootprint leftFootprint = MeasureLowerFlange(leftCover);
            CoverFootprint rightFootprint = MeasureLowerFlange(rightCover);

            int coverCount = 0;
            int boltCount = 0;
            int plugCount = 0;
            List<float> boltHeights = new List<float>();

            EngineAssemblyInteractionTarget[] targets =
                station.GetComponentsInChildren<EngineAssemblyInteractionTarget>(true);

            for (int index = 0; index < targets.Length; index++)
            {
                EngineAssemblyInteractionTarget target = targets[index];
                Transform cover = target.GroupIndex == 0 ? leftCover : rightCover;
                CoverFootprint footprint = target.GroupIndex == 0
                    ? leftFootprint
                    : rightFootprint;

                switch (target.InteractionKind)
                {
                    case EngineAssemblyInteractionKind.CoverPlacement:
                        coverCount++;
                        passed &= ValidateCoverPlacementVisual(
                            target,
                            cover,
                            footprint);
                        break;

                    case EngineAssemblyInteractionKind.CoverBolt:
                        boltCount++;
                        passed &= ValidateBoltPositionAndVisual(
                            target,
                            cover,
                            boltHeights);
                        break;

                    case EngineAssemblyInteractionKind.SparkPlug:
                        plugCount++;
                        passed &= ValidateSparkPlugPlacementVisual(
                            target,
                            cover);
                        break;
                }
            }

            if (coverCount != 2)
            {
                Debug.LogError(
                    $"Test Hangar Step 11 failed: expected 2 cover placement areas, found {coverCount}.");
                passed = false;
            }

            if (boltCount != 12)
            {
                Debug.LogError(
                    $"Test Hangar Step 11 failed: expected 12 cover bolts, found {boltCount}.");
                passed = false;
            }

            if (plugCount != 24)
            {
                Debug.LogError(
                    $"Test Hangar Step 11 failed: expected 24 spark-plug markers, found {plugCount}.");
                passed = false;
            }

            if (boltHeights.Count > 0)
            {
                float minimum = boltHeights[0];
                float maximum = boltHeights[0];
                for (int index = 1; index < boltHeights.Count; index++)
                {
                    minimum = Mathf.Min(minimum, boltHeights[index]);
                    maximum = Mathf.Max(maximum, boltHeights[index]);
                }

                if (maximum - minimum > 0.001f)
                {
                    Debug.LogError(
                        $"Test Hangar Step 11 failed: bolt seating heights differ by {maximum - minimum:F4} local units.");
                    passed = false;
                }
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("Test Hangar Step 11 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Test Hangar Step 11 passed. Both cover placement areas match the scaled lower flanges, all 12 bolts share the correct seating height, and all 24 spark-plug positions have visible surface rings and raised beacons.",
                    station);
            }
        }

        private static void RepairCoverPlacementVisual(
            EngineAssemblyInteractionTarget target,
            EngineAssemblyStation station,
            Transform cover,
            CoverFootprint footprint,
            Material highlightMaterial)
        {
            target.transform.SetPositionAndRotation(cover.position, cover.rotation);
            target.transform.localScale = Vector3.one;

            Vector3 markerWorldPosition = cover.TransformPoint(
                new Vector3(
                    footprint.LocalBounds.center.x,
                    footprint.LocalBounds.min.y + 0.018f,
                    footprint.LocalBounds.center.z));

            // The marker is deliberately slightly smaller than the lower
            // flange. It indicates the seating area without visually
            // swallowing the scaled cylinder bank.
            Vector3 markerWorldSize = new Vector3(
                footprint.WorldWidth * 0.88f,
                0.012f,
                footprint.WorldLength * 0.92f);

            GameObject highlight = ReplaceHighlightWithPrimitive(
                target,
                "Compact Cover Placement Area",
                PrimitiveType.Cube,
                highlightMaterial);
            highlight.transform.SetPositionAndRotation(
                markerWorldPosition,
                cover.rotation);
            SetWorldScale(highlight.transform, markerWorldSize);

            BoxCollider collider = target.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = Undo.AddComponent<BoxCollider>(target.gameObject);
            }

            Vector3 colliderWorldSize = new Vector3(
                footprint.WorldWidth + 0.10f,
                Mathf.Max(0.24f, footprint.WorldHeight + 0.12f),
                footprint.WorldLength + 0.10f);
            collider.center = target.transform.InverseTransformPoint(
                cover.TransformPoint(footprint.LocalBounds.center));
            collider.size = DivideByLossyScale(
                colliderWorldSize,
                target.transform.lossyScale);

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

        private static void RepairBoltPositionAndVisual(
            EngineAssemblyInteractionTarget target,
            EngineAssemblyStation station,
            Transform cover,
            Material highlightMaterial)
        {
            if (target.transform.parent != cover)
            {
                Undo.SetTransformParent(
                    target.transform,
                    cover,
                    "Parent final bolt target to cover");
            }

            int withinCover = Mathf.Abs(target.TargetIndex) % 6;
            int sideIndex = withinCover / 3;
            int longitudinalIndex = withinCover % 3;
            float localX = sideIndex == 0 ? -0.20f : 0.20f;
            float localZ = BoltZPositions[longitudinalIndex];

            target.transform.localPosition = new Vector3(
                localX,
                BoltSeatLocalY,
                localZ);
            target.transform.localRotation = Quaternion.identity;
            target.transform.localScale = Vector3.one;

            GameObject assembly = GetAnimatedVisual(target);
            if (assembly != null)
            {
                Undo.SetTransformParent(
                    assembly.transform,
                    target.transform,
                    "Normalize final bolt assembly");
                assembly.transform.localPosition = Vector3.zero;
                assembly.transform.localRotation = Quaternion.identity;
                assembly.transform.localScale = Vector3.one;

                SetNamedChildPose(
                    assembly.transform,
                    "Threaded Bolt Shaft",
                    new Vector3(0f, -0.070f, 0f),
                    new Vector3(0.018f, 0.070f, 0.018f));
                SetNamedChildPose(
                    assembly.transform,
                    "Bolt Washer",
                    new Vector3(0f, 0.004f, 0f),
                    new Vector3(0.040f, 0.004f, 0.040f));
                SetNamedChildPose(
                    assembly.transform,
                    "Hex Bolt Head",
                    new Vector3(0f, 0.020f, 0f),
                    new Vector3(0.035f, 0.024f, 0.035f));
            }

            GameObject highlight = ReplaceHighlightWithPrimitive(
                target,
                "Centered Bolt Highlight Ring",
                PrimitiveType.Cylinder,
                highlightMaterial);
            highlight.transform.localPosition = new Vector3(0f, 0.012f, 0f);
            highlight.transform.localRotation = Quaternion.identity;
            SetWorldScale(
                highlight.transform,
                new Vector3(0.060f, 0.008f, 0.060f));

            SphereCollider collider = target.GetComponent<SphereCollider>();
            if (collider == null)
            {
                collider = Undo.AddComponent<SphereCollider>(target.gameObject);
            }

            collider.center = new Vector3(0f, 0.025f, 0f);
            collider.radius = DivideByLossyScale(
                Vector3.one * 0.085f,
                target.transform.lossyScale).x;

            target.Configure(
                station,
                EngineAssemblyInteractionKind.CoverBolt,
                target.GroupIndex,
                target.TargetIndex,
                0.9f,
                highlight,
                assembly,
                0.08f,
                3f);
            EditorUtility.SetDirty(target);
        }

        private static void RepairSparkPlugPlacementVisual(
            EngineAssemblyInteractionTarget target,
            EngineAssemblyStation station,
            Transform cover,
            Material highlightMaterial)
        {
            int cylinderIndex = Mathf.Clamp(target.TargetIndex / 4, 0, 5);
            int indexWithinCylinder = Mathf.Abs(target.TargetIndex) % 4;
            bool outerPlug = indexWithinCylinder % 2 == 0;
            float localX = outerPlug ? -0.16f : 0.16f;
            float localZ = -1.35f + cylinderIndex * 0.54f;

            Vector3 plugWorldPosition = cover.TransformPoint(
                new Vector3(localX, SparkPlugRootLocalY, localZ));
            target.transform.SetPositionAndRotation(
                plugWorldPosition,
                cover.rotation);
            target.transform.localScale = Vector3.one;

            Vector3 markerWorldPosition = cover.TransformPoint(
                new Vector3(localX, SparkPlugMarkerLocalY, localZ));

            GameObject markerRoot = ReplaceHighlightWithRoot(
                target,
                "Visible Spark Plug Placement Marker");
            markerRoot.transform.SetPositionAndRotation(
                markerWorldPosition,
                cover.rotation);
            markerRoot.transform.localScale = Vector3.one;

            GameObject surfaceRing = CreateVisualPrimitive(
                markerRoot.transform,
                PrimitiveType.Cylinder,
                "Spark Plug Surface Ring",
                Vector3.zero,
                Quaternion.identity,
                highlightMaterial);
            SetWorldScale(
                surfaceRing.transform,
                new Vector3(0.105f, 0.010f, 0.105f));

            GameObject raisedBeacon = CreateVisualPrimitive(
                markerRoot.transform,
                PrimitiveType.Sphere,
                "Spark Plug Raised Beacon",
                new Vector3(0f, 0.075f, 0f),
                Quaternion.identity,
                highlightMaterial);
            SetWorldScale(
                raisedBeacon.transform,
                Vector3.one * 0.045f);

            GameObject beaconStem = CreateVisualPrimitive(
                markerRoot.transform,
                PrimitiveType.Cylinder,
                "Spark Plug Beacon Stem",
                new Vector3(0f, 0.038f, 0f),
                Quaternion.identity,
                highlightMaterial);
            SetWorldScale(
                beaconStem.transform,
                new Vector3(0.018f, 0.070f, 0.018f));

            BoxCollider collider = target.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = Undo.AddComponent<BoxCollider>(target.gameObject);
            }

            Vector3 markerOffset = target.transform.InverseTransformPoint(
                markerWorldPosition);
            collider.center = new Vector3(0f, markerOffset.y * 0.55f, 0f);
            collider.size = DivideByLossyScale(
                new Vector3(0.14f, 0.24f, 0.14f),
                target.transform.lossyScale);

            GameObject animatedVisual = GetAnimatedVisual(target);
            if (animatedVisual != null)
            {
                animatedVisual.transform.SetPositionAndRotation(
                    plugWorldPosition,
                    cover.rotation);
            }

            target.Configure(
                station,
                EngineAssemblyInteractionKind.SparkPlug,
                target.GroupIndex,
                target.TargetIndex,
                1.25f,
                markerRoot,
                animatedVisual,
                0.16f,
                4f);
            EditorUtility.SetDirty(target);
        }

        private static bool ValidateCoverPlacementVisual(
            EngineAssemblyInteractionTarget target,
            Transform cover,
            CoverFootprint footprint)
        {
            GameObject highlight = GetHighlight(target);
            if (highlight == null)
            {
                Debug.LogError(
                    $"Final placement validation failed: '{target.name}' has no cover placement area.",
                    target);
                return false;
            }

            Vector3 expectedPosition = cover.TransformPoint(
                new Vector3(
                    footprint.LocalBounds.center.x,
                    footprint.LocalBounds.min.y + 0.018f,
                    footprint.LocalBounds.center.z));
            Vector3 expectedSize = new Vector3(
                footprint.WorldWidth * 0.88f,
                0.012f,
                footprint.WorldLength * 0.92f);
            Vector3 actualSize = GetWorldScale(highlight.transform);

            float positionError = Vector3.Distance(
                highlight.transform.position,
                expectedPosition);
            bool sizeMatches = Approximately(actualSize, expectedSize, 0.015f);

            if (positionError > 0.010f || !sizeMatches)
            {
                Debug.LogError(
                    $"Final placement validation failed: '{target.name}' cover area is mis-sized or misplaced. "
                    + $"Position error {positionError:F3}; actual size {actualSize}, expected {expectedSize}.",
                    target);
                return false;
            }

            return true;
        }

        private static bool ValidateBoltPositionAndVisual(
            EngineAssemblyInteractionTarget target,
            Transform cover,
            List<float> boltHeights)
        {
            int withinCover = Mathf.Abs(target.TargetIndex) % 6;
            int sideIndex = withinCover / 3;
            int longitudinalIndex = withinCover % 3;
            Vector3 expectedPosition = new Vector3(
                sideIndex == 0 ? -0.20f : 0.20f,
                BoltSeatLocalY,
                BoltZPositions[longitudinalIndex]);

            boltHeights.Add(target.transform.localPosition.y);
            float targetError = Vector3.Distance(
                target.transform.localPosition,
                expectedPosition);

            GameObject assembly = GetAnimatedVisual(target);
            float assemblyError = assembly != null
                ? Vector3.Distance(assembly.transform.localPosition, Vector3.zero)
                : float.MaxValue;

            if (target.transform.parent != cover
                || targetError > 0.006f
                || assemblyError > 0.004f)
            {
                Debug.LogError(
                    $"Final placement validation failed: '{target.name}' is not on the shared bolt seating plane. "
                    + $"Target error {targetError:F3}; assembly error {assemblyError:F3}.",
                    target);
                return false;
            }

            return true;
        }

        private static bool ValidateSparkPlugPlacementVisual(
            EngineAssemblyInteractionTarget target,
            Transform cover)
        {
            GameObject marker = GetHighlight(target);
            if (marker == null)
            {
                Debug.LogError(
                    $"Final placement validation failed: '{target.name}' has no spark-plug marker.",
                    target);
                return false;
            }

            int cylinderIndex = Mathf.Clamp(target.TargetIndex / 4, 0, 5);
            int indexWithinCylinder = Mathf.Abs(target.TargetIndex) % 4;
            bool outerPlug = indexWithinCylinder % 2 == 0;
            float localX = outerPlug ? -0.16f : 0.16f;
            float localZ = -1.35f + cylinderIndex * 0.54f;

            Vector3 expectedMarkerPosition = cover.TransformPoint(
                new Vector3(localX, SparkPlugMarkerLocalY, localZ));
            float markerError = Vector3.Distance(
                marker.transform.position,
                expectedMarkerPosition);

            Transform ring = marker.transform.Find("Spark Plug Surface Ring");
            Transform beacon = marker.transform.Find("Spark Plug Raised Beacon");
            Renderer[] renderers = marker.GetComponentsInChildren<Renderer>(true);

            if (markerError > 0.010f
                || ring == null
                || beacon == null
                || renderers.Length < 3
                || GetWorldScale(ring).x < 0.090f)
            {
                Debug.LogError(
                    $"Final placement validation failed: '{target.name}' does not have a visible, correctly positioned spark-plug marker. "
                    + $"Marker error {markerError:F3}.",
                    target);
                return false;
            }

            return true;
        }

        private static CoverFootprint MeasureLowerFlange(Transform cover)
        {
            Transform lowerFlange = FindDescendant(cover, "Lower Flange");
            MeshFilter filter = lowerFlange != null
                ? lowerFlange.GetComponent<MeshFilter>()
                : null;

            if (lowerFlange == null || filter == null || filter.sharedMesh == null)
            {
                return new CoverFootprint(default, 0f, 0f, 0f, false);
            }

            Bounds localBounds = CalculateMeshBoundsRelativeTo(
                filter,
                cover);

            float worldWidth = cover.TransformVector(
                new Vector3(localBounds.size.x, 0f, 0f)).magnitude;
            float worldHeight = cover.TransformVector(
                new Vector3(0f, localBounds.size.y, 0f)).magnitude;
            float worldLength = cover.TransformVector(
                new Vector3(0f, 0f, localBounds.size.z)).magnitude;

            return new CoverFootprint(
                localBounds,
                worldWidth,
                worldHeight,
                worldLength,
                localBounds.size.sqrMagnitude > 0.001f);
        }

        private static Bounds CalculateMeshBoundsRelativeTo(
            MeshFilter filter,
            Transform reference)
        {
            Bounds meshBounds = filter.sharedMesh.bounds;
            bool initialized = false;
            Bounds result = new Bounds(Vector3.zero, Vector3.zero);

            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = new Vector3(
                    (corner & 1) == 0 ? meshBounds.min.x : meshBounds.max.x,
                    (corner & 2) == 0 ? meshBounds.min.y : meshBounds.max.y,
                    (corner & 4) == 0 ? meshBounds.min.z : meshBounds.max.z);
                Vector3 referencePoint = reference.InverseTransformPoint(
                    filter.transform.TransformPoint(point));

                if (!initialized)
                {
                    result = new Bounds(referencePoint, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    result.Encapsulate(referencePoint);
                }
            }

            return result;
        }

        private static GameObject ReplaceHighlightWithPrimitive(
            EngineAssemblyInteractionTarget target,
            string name,
            PrimitiveType primitiveType,
            Material material)
        {
            DestroyCurrentHighlight(target);
            return CreateVisualPrimitive(
                target.transform,
                primitiveType,
                name,
                Vector3.zero,
                Quaternion.identity,
                material);
        }

        private static GameObject ReplaceHighlightWithRoot(
            EngineAssemblyInteractionTarget target,
            string name)
        {
            DestroyCurrentHighlight(target);
            GameObject root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, $"Create {name}");
            root.transform.SetParent(target.transform, false);
            return root;
        }

        private static void DestroyCurrentHighlight(
            EngineAssemblyInteractionTarget target)
        {
            GameObject current = GetHighlight(target);
            if (current != null)
            {
                Undo.DestroyObjectImmediate(current);
            }
        }

        private static GameObject CreateVisualPrimitive(
            Transform parent,
            PrimitiveType primitiveType,
            string name,
            Vector3 localPosition,
            Quaternion localRotation,
            Material material)
        {
            GameObject visual = GameObject.CreatePrimitive(primitiveType);
            Undo.RegisterCreatedObjectUndo(visual, $"Create {name}");
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localRotation = localRotation;

            Renderer renderer = visual.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            Collider collider = visual.GetComponent<Collider>();
            if (collider != null)
            {
                Undo.DestroyObjectImmediate(collider);
            }

            return visual;
        }

        private static void SetNamedChildPose(
            Transform root,
            string childName,
            Vector3 localPosition,
            Vector3 localScale)
        {
            Transform child = FindDescendant(root, childName);
            if (child == null)
            {
                return;
            }

            child.localPosition = localPosition;
            child.localRotation = Quaternion.identity;
            child.localScale = localScale;
            EditorUtility.SetDirty(child);
        }

        private static void MakeHighlightMaterialHighlyVisible(Material material)
        {
            if (material == null)
            {
                return;
            }

            Color color = new Color(1f, 0.72f, 0.05f, 0.90f);
            material.color = color;

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2.8f);
            }

            EditorUtility.SetDirty(material);
        }

        private static void SetWorldScale(
            Transform target,
            Vector3 desiredWorldScale)
        {
            Transform parent = target.parent;
            Vector3 parentScale = parent != null
                ? parent.lossyScale
                : Vector3.one;
            target.localScale = DivideByLossyScale(
                desiredWorldScale,
                parentScale);
        }

        private static Vector3 GetWorldScale(Transform target)
        {
            return new Vector3(
                target.TransformVector(Vector3.right).magnitude,
                target.TransformVector(Vector3.up).magnitude,
                target.TransformVector(Vector3.forward).magnitude);
        }

        private static Vector3 DivideByLossyScale(
            Vector3 value,
            Vector3 scale)
        {
            return new Vector3(
                value.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                value.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                value.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
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

        private static Transform FindDescendant(
            Transform root,
            string name)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index].name == name)
                {
                    return transforms[index];
                }
            }

            return null;
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

        private readonly struct CoverFootprint
        {
            public CoverFootprint(
                Bounds localBounds,
                float worldWidth,
                float worldHeight,
                float worldLength,
                bool isValid)
            {
                LocalBounds = localBounds;
                WorldWidth = worldWidth;
                WorldHeight = worldHeight;
                WorldLength = worldLength;
                IsValid = isValid;
            }

            public Bounds LocalBounds { get; }
            public float WorldWidth { get; }
            public float WorldHeight { get; }
            public float WorldLength { get; }
            public bool IsValid { get; }
        }
    }
}
