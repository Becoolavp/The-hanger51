using System;
using System.Collections.Generic;
using Hanger51.Aircraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51ControlSurfaceOwnerMappingRepairSetup
    {
        private const string LeftAileronName = "P-51 Left Aileron Pivot";
        private const string RightAileronName = "P-51 Right Aileron Pivot";
        private const string LeftElevatorName = "P-51 Left Elevator Pivot";
        private const string RightElevatorName = "P-51 Right Elevator Pivot";
        private const string RudderName = "P-51 Rudder Pivot";

        private const string MeshFolder = "Assets/_Project/Aircraft/P51/Meshes/";
        private const string LeftFixedWingPath = MeshFolder + "P51D_LeftWing_FixedWithAileronCutout.asset";
        private const string RightFixedWingPath = MeshFolder + "P51D_RightWing_FixedWithAileronCutout.asset";
        private const string LeftAileronPath = MeshFolder + "P51D_LeftAileron.asset";
        private const string RightAileronPath = MeshFolder + "P51D_RightAileron.asset";
        private const string LeftFixedStabilizerPath = MeshFolder + "P51D_LeftStabilizer_Fixed.asset";
        private const string RightFixedStabilizerPath = MeshFolder + "P51D_RightStabilizer_Fixed.asset";
        private const string LeftElevatorPath = MeshFolder + "P51D_LeftElevator.asset";
        private const string RightElevatorPath = MeshFolder + "P51D_RightElevator.asset";
        private const string FixedFinPath = MeshFolder + "P51D_VerticalFin_Fixed.asset";
        private const string RudderPath = MeshFolder + "P51D_Rudder.asset";

        private enum FixedSurfaceKind
        {
            LeftWing,
            RightWing,
            LeftStabilizer,
            RightStabilizer,
            VerticalFin
        }

        private readonly struct SurfaceMeshes
        {
            internal readonly Mesh LeftWing;
            internal readonly Mesh RightWing;
            internal readonly Mesh LeftAileron;
            internal readonly Mesh RightAileron;
            internal readonly Mesh LeftStabilizer;
            internal readonly Mesh RightStabilizer;
            internal readonly Mesh LeftElevator;
            internal readonly Mesh RightElevator;
            internal readonly Mesh Fin;
            internal readonly Mesh Rudder;

            internal SurfaceMeshes(
                Mesh leftWing,
                Mesh rightWing,
                Mesh leftAileron,
                Mesh rightAileron,
                Mesh leftStabilizer,
                Mesh rightStabilizer,
                Mesh leftElevator,
                Mesh rightElevator,
                Mesh fin,
                Mesh rudder)
            {
                LeftWing = leftWing;
                RightWing = rightWing;
                LeftAileron = leftAileron;
                RightAileron = rightAileron;
                LeftStabilizer = leftStabilizer;
                RightStabilizer = rightStabilizer;
                LeftElevator = leftElevator;
                RightElevator = rightElevator;
                Fin = fin;
                Rudder = rudder;
            }

            internal bool IsComplete => LeftWing != null
                && RightWing != null
                && LeftAileron != null
                && RightAileron != null
                && LeftStabilizer != null
                && RightStabilizer != null
                && LeftElevator != null
                && RightElevator != null
                && Fin != null
                && Rudder != null;
        }

        [MenuItem("Hanger 51/P-51 Mustang/87 - Repair Control Surface Owner Mapping")]
        public static void RepairControlSurfaceOwnerMapping()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 Step 87 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("P-51 Step 87 failed. Open the saved Hanger 51 gameplay scene first.");
                return;
            }

            SurfaceMeshes meshes = LoadSurfaceMeshes();
            if (!meshes.IsComplete)
            {
                Debug.LogError(
                    "P-51 Step 87 failed. The Step 85 control-surface mesh assets are missing. Run Step 85 once, let it finish, then run Step 87 again.");
                return;
            }

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft == null || aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 87 failed. No P-51 aircraft were found in the scene.");
                return;
            }

            int repaired = 0;
            int failed = 0;
            for (int index = 0; index < aircraft.Length; index++)
            {
                P51FlightController flight = aircraft[index];
                if (flight == null || !flight.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (RepairAircraft(flight, meshes))
                {
                    repaired++;
                }
                else
                {
                    failed++;
                }
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 87 made repairs but Unity could not save the scene.");
                return;
            }

            if (failed > 0)
            {
                Debug.LogError(
                    $"P-51 Step 87 repaired {repaired} aircraft but could not repair {failed}. See the specific owner-resolution error(s) above.");
                return;
            }

            Debug.Log(
                $"P-51 Step 87 complete. Repaired {repaired} aircraft. The fixed wing/stabilizer/fin renderers now use the hinge-cut meshes and all five movable control-surface pivots were rebuilt in aircraft-local coordinates. Run Step 86 now.");
        }

        private static bool RepairAircraft(P51FlightController flight, SurfaceMeshes meshes)
        {
            MeshFilter leftWing = ResolveFixedSurfaceOwner(flight.transform, FixedSurfaceKind.LeftWing);
            MeshFilter rightWing = ResolveFixedSurfaceOwner(flight.transform, FixedSurfaceKind.RightWing);
            MeshFilter leftStabilizer = ResolveFixedSurfaceOwner(flight.transform, FixedSurfaceKind.LeftStabilizer);
            MeshFilter rightStabilizer = ResolveFixedSurfaceOwner(flight.transform, FixedSurfaceKind.RightStabilizer);
            MeshFilter fin = ResolveFixedSurfaceOwner(flight.transform, FixedSurfaceKind.VerticalFin);

            bool complete = true;
            complete &= ReportMissingOwner(flight, leftWing, "left wing");
            complete &= ReportMissingOwner(flight, rightWing, "right wing");
            complete &= ReportMissingOwner(flight, leftStabilizer, "left horizontal stabilizer");
            complete &= ReportMissingOwner(flight, rightStabilizer, "right horizontal stabilizer");
            complete &= ReportMissingOwner(flight, fin, "vertical fin");
            if (!complete)
            {
                return false;
            }

            AssignFixedMesh(leftWing, meshes.LeftWing);
            AssignFixedMesh(rightWing, meshes.RightWing);
            AssignFixedMesh(leftStabilizer, meshes.LeftStabilizer);
            AssignFixedMesh(rightStabilizer, meshes.RightStabilizer);
            AssignFixedMesh(fin, meshes.Fin);

            DestroyGeneratedPivot(flight.transform, LeftAileronName);
            DestroyGeneratedPivot(flight.transform, RightAileronName);
            DestroyGeneratedPivot(flight.transform, LeftElevatorName);
            DestroyGeneratedPivot(flight.transform, RightElevatorName);
            DestroyGeneratedPivot(flight.transform, RudderName);

            Transform leftAileron = CreateSurfacePivot(
                flight.transform,
                LeftAileronName,
                CalculateAileronPivot(true),
                meshes.LeftAileron,
                GetMaterial(leftWing));
            Transform rightAileron = CreateSurfacePivot(
                flight.transform,
                RightAileronName,
                CalculateAileronPivot(false),
                meshes.RightAileron,
                GetMaterial(rightWing));
            Transform leftElevator = CreateSurfacePivot(
                flight.transform,
                LeftElevatorName,
                CalculateElevatorPivot(true),
                meshes.LeftElevator,
                GetMaterial(leftStabilizer));
            Transform rightElevator = CreateSurfacePivot(
                flight.transform,
                RightElevatorName,
                CalculateElevatorPivot(false),
                meshes.RightElevator,
                GetMaterial(rightStabilizer));
            Transform rudder = CreateSurfacePivot(
                flight.transform,
                RudderName,
                CalculateRudderPivot(),
                meshes.Rudder,
                GetMaterial(fin));

            if (leftAileron == null || rightAileron == null
                || leftElevator == null || rightElevator == null || rudder == null)
            {
                Debug.LogError(
                    $"P-51 Step 87 failed while rebuilding one or more movable surfaces on '{flight.name}'.",
                    flight);
                return false;
            }

            P51ControlSurfaceVisualController controller = flight.GetComponent<P51ControlSurfaceVisualController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<P51ControlSurfaceVisualController>(flight.gameObject);
            }

            controller.Configure(
                flight,
                flight.GetComponent<P51LandingAndRudderController>(),
                leftAileron,
                rightAileron,
                leftElevator,
                rightElevator,
                rudder);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(flight);

            if (!controller.IsConfigured)
            {
                Debug.LogError(
                    $"P-51 Step 87 rebuilt the surfaces on '{flight.name}', but its control-surface controller did not retain all five pivot references.",
                    flight);
                return false;
            }

            return true;
        }

        private static bool ReportMissingOwner(P51FlightController flight, MeshFilter filter, string label)
        {
            if (filter != null)
            {
                return true;
            }

            Debug.LogError(
                $"P-51 Step 87 could not identify the {label} renderer on '{flight.name}'. No geometry was changed for that aircraft.",
                flight);
            return false;
        }

        private static SurfaceMeshes LoadSurfaceMeshes()
        {
            return new SurfaceMeshes(
                AssetDatabase.LoadAssetAtPath<Mesh>(LeftFixedWingPath),
                AssetDatabase.LoadAssetAtPath<Mesh>(RightFixedWingPath),
                AssetDatabase.LoadAssetAtPath<Mesh>(LeftAileronPath),
                AssetDatabase.LoadAssetAtPath<Mesh>(RightAileronPath),
                AssetDatabase.LoadAssetAtPath<Mesh>(LeftFixedStabilizerPath),
                AssetDatabase.LoadAssetAtPath<Mesh>(RightFixedStabilizerPath),
                AssetDatabase.LoadAssetAtPath<Mesh>(LeftElevatorPath),
                AssetDatabase.LoadAssetAtPath<Mesh>(RightElevatorPath),
                AssetDatabase.LoadAssetAtPath<Mesh>(FixedFinPath),
                AssetDatabase.LoadAssetAtPath<Mesh>(RudderPath));
        }

        private static MeshFilter ResolveFixedSurfaceOwner(Transform aircraftRoot, FixedSurfaceKind kind)
        {
            MeshFilter exact = ResolveByHierarchyAliases(aircraftRoot, kind);
            if (exact != null)
            {
                return exact;
            }

            MeshFilter canonical = ResolveByCanonicalMeshName(aircraftRoot, kind);
            if (canonical != null)
            {
                return canonical;
            }

            MeshFilter[] filters = aircraftRoot.GetComponentsInChildren<MeshFilter>(true);
            MeshFilter best = null;
            float bestScore = float.NegativeInfinity;
            for (int index = 0; index < filters.Length; index++)
            {
                MeshFilter filter = filters[index];
                if (!IsEligibleFixedSurfaceCandidate(filter, aircraftRoot))
                {
                    continue;
                }

                Bounds localBounds = GetAircraftLocalBounds(filter, aircraftRoot);
                float geometryScore = ScoreGeometry(localBounds, kind);
                if (float.IsNegativeInfinity(geometryScore))
                {
                    continue;
                }

                float nameScore = ScoreHierarchyName(filter.transform, aircraftRoot, kind);
                float score = geometryScore + nameScore;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = filter;
                }
            }

            return bestScore >= 10f ? best : null;
        }

        private static MeshFilter ResolveByHierarchyAliases(Transform aircraftRoot, FixedSurfaceKind kind)
        {
            string[] aliases = GetHierarchyAliases(kind);
            Transform[] all = aircraftRoot.GetComponentsInChildren<Transform>(true);
            for (int aliasIndex = 0; aliasIndex < aliases.Length; aliasIndex++)
            {
                string alias = aliases[aliasIndex];
                for (int index = 0; index < all.Length; index++)
                {
                    Transform current = all[index];
                    if (current == null
                        || !string.Equals(current.name, alias, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    MeshFilter direct = current.GetComponent<MeshFilter>();
                    if (IsEligibleFixedSurfaceCandidate(direct, aircraftRoot))
                    {
                        return direct;
                    }

                    MeshFilter[] descendants = current.GetComponentsInChildren<MeshFilter>(true);
                    MeshFilter best = ChooseLargestEligible(descendants, aircraftRoot);
                    if (best != null)
                    {
                        return best;
                    }
                }
            }

            return null;
        }

        private static string[] GetHierarchyAliases(FixedSurfaceKind kind)
        {
            switch (kind)
            {
                case FixedSurfaceKind.LeftWing:
                    return new[] { "Left Laminar Flow Wing", "P-51D Left Wing", "Left Wing" };
                case FixedSurfaceKind.RightWing:
                    return new[] { "Right Laminar Flow Wing", "P-51D Right Wing", "Right Wing" };
                case FixedSurfaceKind.LeftStabilizer:
                    return new[] { "Left Horizontal Stabilizer", "P-51D Left Stabilizer", "Left Stabilizer", "Left Tailplane" };
                case FixedSurfaceKind.RightStabilizer:
                    return new[] { "Right Horizontal Stabilizer", "P-51D Right Stabilizer", "Right Stabilizer", "Right Tailplane" };
                default:
                    return new[] { "Vertical Stabilizer", "P-51D Vertical Fin", "Vertical Fin", "Tail Fin" };
            }
        }

        private static MeshFilter ResolveByCanonicalMeshName(Transform aircraftRoot, FixedSurfaceKind kind)
        {
            string[] names;
            switch (kind)
            {
                case FixedSurfaceKind.LeftWing:
                    names = new[] { "P-51D Left Wing", "P-51D Left Wing Fixed" };
                    break;
                case FixedSurfaceKind.RightWing:
                    names = new[] { "P-51D Right Wing", "P-51D Right Wing Fixed" };
                    break;
                case FixedSurfaceKind.LeftStabilizer:
                    names = new[] { "P-51D Left Stabilizer", "P-51D Left Stabilizer Fixed" };
                    break;
                case FixedSurfaceKind.RightStabilizer:
                    names = new[] { "P-51D Right Stabilizer", "P-51D Right Stabilizer Fixed" };
                    break;
                default:
                    names = new[] { "P-51D Vertical Fin", "P-51D Vertical Fin Fixed" };
                    break;
            }

            MeshFilter[] filters = aircraftRoot.GetComponentsInChildren<MeshFilter>(true);
            for (int index = 0; index < filters.Length; index++)
            {
                MeshFilter filter = filters[index];
                if (!IsEligibleFixedSurfaceCandidate(filter, aircraftRoot) || filter.sharedMesh == null)
                {
                    continue;
                }

                for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
                {
                    if (string.Equals(filter.sharedMesh.name, names[nameIndex], StringComparison.Ordinal))
                    {
                        return filter;
                    }
                }
            }

            return null;
        }

        private static MeshFilter ChooseLargestEligible(MeshFilter[] filters, Transform aircraftRoot)
        {
            MeshFilter best = null;
            float bestMagnitude = -1f;
            for (int index = 0; index < filters.Length; index++)
            {
                MeshFilter filter = filters[index];
                if (!IsEligibleFixedSurfaceCandidate(filter, aircraftRoot))
                {
                    continue;
                }

                Bounds bounds = GetAircraftLocalBounds(filter, aircraftRoot);
                float magnitude = bounds.size.sqrMagnitude;
                if (magnitude > bestMagnitude)
                {
                    bestMagnitude = magnitude;
                    best = filter;
                }
            }
            return best;
        }

        private static bool IsEligibleFixedSurfaceCandidate(MeshFilter filter, Transform aircraftRoot)
        {
            if (filter == null || filter.sharedMesh == null)
            {
                return false;
            }

            Transform current = filter.transform;
            while (current != null && current != aircraftRoot)
            {
                string name = current.name;
                if (name == LeftAileronName
                    || name == RightAileronName
                    || name == LeftElevatorName
                    || name == RightElevatorName
                    || name == RudderName
                    || name.IndexOf("Aileron Visual", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Elevator Visual", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Rudder Visual", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return false;
                }
                current = current.parent;
            }

            return true;
        }

        private static float ScoreHierarchyName(Transform transform, Transform aircraftRoot, FixedSurfaceKind kind)
        {
            string text = string.Empty;
            Transform current = transform;
            int depth = 0;
            while (current != null && current != aircraftRoot && depth < 5)
            {
                text += " " + current.name.ToLowerInvariant();
                current = current.parent;
                depth++;
            }

            float score = 0f;
            bool left = text.Contains("left");
            bool right = text.Contains("right");
            bool wing = text.Contains("wing");
            bool stabilizer = text.Contains("stabilizer") || text.Contains("tailplane") || text.Contains("horizontal tail");
            bool fin = text.Contains("vertical") || text.Contains(" fin") || text.Contains("tail fin");

            switch (kind)
            {
                case FixedSurfaceKind.LeftWing:
                    if (left) score += 18f;
                    if (right) score -= 40f;
                    if (wing) score += 18f;
                    break;
                case FixedSurfaceKind.RightWing:
                    if (right) score += 18f;
                    if (left) score -= 40f;
                    if (wing) score += 18f;
                    break;
                case FixedSurfaceKind.LeftStabilizer:
                    if (left) score += 18f;
                    if (right) score -= 40f;
                    if (stabilizer) score += 18f;
                    break;
                case FixedSurfaceKind.RightStabilizer:
                    if (right) score += 18f;
                    if (left) score -= 40f;
                    if (stabilizer) score += 18f;
                    break;
                case FixedSurfaceKind.VerticalFin:
                    if (fin) score += 24f;
                    if (text.Contains("rudder")) score -= 50f;
                    break;
            }

            return score;
        }

        private static float ScoreGeometry(Bounds bounds, FixedSurfaceKind kind)
        {
            Vector3 center = bounds.center;
            Vector3 size = bounds.size;

            switch (kind)
            {
                case FixedSurfaceKind.LeftWing:
                    if (center.x >= -0.8f || size.x < 3.5f || center.z < -2f || center.z > 2f
                        || center.y < 0.3f || center.y > 2.8f)
                    {
                        return float.NegativeInfinity;
                    }
                    return 35f + size.x * 6f - Mathf.Abs(center.z) * 2f;

                case FixedSurfaceKind.RightWing:
                    if (center.x <= 0.8f || size.x < 3.5f || center.z < -2f || center.z > 2f
                        || center.y < 0.3f || center.y > 2.8f)
                    {
                        return float.NegativeInfinity;
                    }
                    return 35f + size.x * 6f - Mathf.Abs(center.z) * 2f;

                case FixedSurfaceKind.LeftStabilizer:
                    if (center.x >= -0.35f || size.x < 1.2f || size.x > 3.2f
                        || center.z > -3f || center.z < -5.4f
                        || center.y < 1f || center.y > 2.7f)
                    {
                        return float.NegativeInfinity;
                    }
                    return 30f + size.x * 7f - Mathf.Abs(center.z + 4.05f) * 4f;

                case FixedSurfaceKind.RightStabilizer:
                    if (center.x <= 0.35f || size.x < 1.2f || size.x > 3.2f
                        || center.z > -3f || center.z < -5.4f
                        || center.y < 1f || center.y > 2.7f)
                    {
                        return float.NegativeInfinity;
                    }
                    return 30f + size.x * 7f - Mathf.Abs(center.z + 4.05f) * 4f;

                case FixedSurfaceKind.VerticalFin:
                    if (Mathf.Abs(center.x) > 0.8f || size.x > 1.1f || size.y < 1.2f
                        || center.z > -3f || center.z < -5.4f
                        || center.y < 1.7f)
                    {
                        return float.NegativeInfinity;
                    }
                    return 35f + size.y * 8f - size.x * 3f - Mathf.Abs(center.z + 4.15f) * 4f;
            }

            return float.NegativeInfinity;
        }

        private static Bounds GetAircraftLocalBounds(MeshFilter filter, Transform aircraftRoot)
        {
            Bounds meshBounds = filter.sharedMesh.bounds;
            Vector3 min = meshBounds.min;
            Vector3 max = meshBounds.max;
            Vector3 first = aircraftRoot.InverseTransformPoint(
                filter.transform.TransformPoint(new Vector3(min.x, min.y, min.z)));
            Bounds result = new Bounds(first, Vector3.zero);

            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        Vector3 meshPoint = new Vector3(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z);
                        Vector3 aircraftPoint = aircraftRoot.InverseTransformPoint(
                            filter.transform.TransformPoint(meshPoint));
                        result.Encapsulate(aircraftPoint);
                    }
                }
            }

            return result;
        }

        private static void AssignFixedMesh(MeshFilter owner, Mesh mesh)
        {
            if (owner == null || mesh == null)
            {
                return;
            }

            Undo.RecordObject(owner, "Repair P-51 hinge-cut fixed surface mesh");
            owner.sharedMesh = mesh;
            EditorUtility.SetDirty(owner);
        }

        private static Transform CreateSurfacePivot(
            Transform aircraftRoot,
            string pivotName,
            Vector3 aircraftLocalPosition,
            Mesh surfaceMesh,
            Material material)
        {
            if (aircraftRoot == null || surfaceMesh == null)
            {
                return null;
            }

            GameObject pivotObject = new GameObject(pivotName);
            Undo.RegisterCreatedObjectUndo(pivotObject, $"Create {pivotName}");
            Transform pivot = pivotObject.transform;
            pivot.SetParent(aircraftRoot, false);
            pivot.localPosition = aircraftLocalPosition;
            pivot.localRotation = Quaternion.identity;
            pivot.localScale = Vector3.one;

            GameObject visual = new GameObject(pivotName.Replace(" Pivot", " Visual"));
            Undo.RegisterCreatedObjectUndo(visual, $"Create {visual.name}");
            visual.transform.SetParent(pivot, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            MeshFilter filter = visual.AddComponent<MeshFilter>();
            filter.sharedMesh = surfaceMesh;
            MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.receiveShadows = true;

            return pivot;
        }

        private static Material GetMaterial(MeshFilter owner)
        {
            if (owner == null)
            {
                return null;
            }

            Renderer renderer = owner.GetComponent<Renderer>();
            if (renderer != null)
            {
                return renderer.sharedMaterial;
            }

            renderer = owner.GetComponentInChildren<Renderer>(true);
            return renderer != null ? renderer.sharedMaterial : null;
        }

        private static void DestroyGeneratedPivot(Transform aircraftRoot, string pivotName)
        {
            Transform[] all = aircraftRoot.GetComponentsInChildren<Transform>(true);
            for (int index = all.Length - 1; index >= 0; index--)
            {
                Transform current = all[index];
                if (current != null && current != aircraftRoot && current.name == pivotName)
                {
                    Undo.DestroyObjectImmediate(current.gameObject);
                }
            }
        }

        private static Vector3 CalculateAileronPivot(bool left)
        {
            const float rootSpan = 3.25f;
            const float tipSpan = 5.35f;
            SampleWing(rootSpan, out float rootLeading, out float rootTrailing, out float rootY);
            SampleWing(tipSpan, out float tipLeading, out float tipTrailing, out float tipY);
            float rootHinge = rootTrailing + (rootLeading - rootTrailing) * 0.265f;
            float tipHinge = tipTrailing + (tipLeading - tipTrailing) * 0.265f;
            float sign = left ? -1f : 1f;
            return new Vector3(
                sign * (rootSpan + tipSpan) * 0.5f,
                (rootY + tipY) * 0.5f,
                (rootHinge + tipHinge) * 0.5f);
        }

        private static void SampleWing(float span, out float leading, out float trailing, out float centerY)
        {
            float[] spans = { 0.38f, 3.15f, 5.64f };
            float[] leadingValues = { 1.18f, 0.69f, 0.18f };
            float[] trailingValues = { -1.36f, -0.94f, -0.54f };
            float[] yValues = { 1.24f, 1.35f, 1.48f };
            leading = Interpolate(span, spans, leadingValues);
            trailing = Interpolate(span, spans, trailingValues);
            centerY = Interpolate(span, spans, yValues);
        }

        private static float Interpolate(float value, float[] keys, float[] values)
        {
            if (value <= keys[0])
            {
                return values[0];
            }

            int last = keys.Length - 1;
            if (value >= keys[last])
            {
                return values[last];
            }

            for (int index = 0; index < last; index++)
            {
                if (value >= keys[index] && value <= keys[index + 1])
                {
                    float t = Mathf.InverseLerp(keys[index], keys[index + 1], value);
                    return Mathf.Lerp(values[index], values[index + 1], t);
                }
            }

            return values[last];
        }

        private static Vector3 CalculateElevatorPivot(bool left)
        {
            const float rootX = 0.30f;
            const float tipX = 2.15f;
            const float rootLeading = -3.52f;
            const float rootTrailing = -4.58f;
            const float tipLeading = -3.86f;
            const float tipTrailing = -4.52f;
            const float rootY = 1.78f;
            const float tipY = 1.88f;
            float rootHinge = rootTrailing + (rootLeading - rootTrailing) * 0.30f;
            float tipHinge = tipTrailing + (tipLeading - tipTrailing) * 0.30f;
            float sign = left ? -1f : 1f;
            return new Vector3(
                sign * (rootX + tipX) * 0.5f,
                (rootY + tipY) * 0.5f,
                (rootHinge + tipHinge) * 0.5f);
        }

        private static Vector3 CalculateRudderPivot()
        {
            const float bottomZ = -4.42f;
            const float bottomY = 1.67f;
            const float topZ = -4.39f;
            const float topY = 3.52f;
            return new Vector3(0f, (bottomY + topY) * 0.5f, (bottomZ + topZ) * 0.5f);
        }
    }
}
