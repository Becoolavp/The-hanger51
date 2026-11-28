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
    public static class P51WingArmamentVisualUpgrade
    {
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string ArmamentRootName = "P-51 Serviceable Wing Armament";
        private const string MeshFolder = "Assets/_Project/Aircraft/P51/Meshes";
        private const string LeftWingMeshPath = MeshFolder + "/P51D_LeftWing_ArmamentBay.asset";
        private const string RightWingMeshPath = MeshFolder + "/P51D_RightWing_ArmamentBay.asset";
        private const string LeftPanelMeshPath = MeshFolder + "/P51D_LeftWing_ArmamentPanel.asset";
        private const string RightPanelMeshPath = MeshFolder + "/P51D_RightWing_ArmamentPanel.asset";
        private const string AluminumPath = "Assets/_Project/Aircraft/P51/Materials/PolishedAluminum.mat";
        private const string HardwarePath = "Assets/_Project/Aircraft/P51/Materials/ServiceHardware.mat";
        private const string BayDarkPath = "Assets/_Project/Aircraft/P51/Armament/Materials/ArmamentBayDark.mat";

        private const float BayInnerSpan = 1.15f;
        private const float BayOuterSpan = 3.75f;
        private const float BayRearZ = -0.78f;
        private const float BayFrontZ = 0.50f;
        private const float BayInteriorY = 1.22f;

        private static readonly float[] WingSpans = { 0.38f, 3.15f, 5.64f };
        private static readonly float[] WingLeading = { 1.18f, 0.69f, 0.18f };
        private static readonly float[] WingTrailing = { -1.36f, -0.94f, -0.54f };
        private static readonly float[] WingCenterY = { 1.24f, 1.35f, 1.48f };
        private static readonly float[] WingThickness = { 0.22f, 0.14f, 0.065f };

        [MenuItem("Hanger 51/P-51 Mustang/34 - Upgrade Wing Armament Bays and Bullet Streaks")]
        public static void UpgradeWingArmamentVisuals()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("P-51 Step 34 failed. Exit Play mode first.");
                return;
            }
            if (EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 Step 34 failed. Wait for Unity to finish compiling.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            GameObject aircraft = GameObject.Find(AircraftRootName);
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path) || aircraft == null)
            {
                Debug.LogError("P-51 Step 34 failed. Open the saved hangar scene containing the P-51 first.");
                return;
            }

            P51WingArmamentSystem system = aircraft.GetComponent<P51WingArmamentSystem>();
            Transform armamentRoot = aircraft.transform.Find(ArmamentRootName);
            if (system == null || armamentRoot == null)
            {
                Debug.LogError("P-51 Step 34 failed. The serviceable wing armament system is missing.", aircraft);
                return;
            }

            Material aluminum = AssetDatabase.LoadAssetAtPath<Material>(AluminumPath);
            Material hardware = AssetDatabase.LoadAssetAtPath<Material>(HardwarePath);
            Material bayDark = AssetDatabase.LoadAssetAtPath<Material>(BayDarkPath);
            if (aluminum == null || hardware == null || bayDark == null)
            {
                Debug.LogError("P-51 Step 34 failed. Required P-51 aluminum, hardware, or armament-bay material is missing.");
                return;
            }

            Transform leftWingVisual = FindChildRecursive(aircraft.transform, "Left Laminar Flow Wing");
            Transform rightWingVisual = FindChildRecursive(aircraft.transform, "Right Laminar Flow Wing");
            if (leftWingVisual == null || rightWingVisual == null)
            {
                Debug.LogError("P-51 Step 34 failed. The current P-51 wing visual objects could not be found.", aircraft);
                return;
            }

            Mesh leftWingMesh = CreateOrUpdateWingWithServiceBay(LeftWingMeshPath, true);
            Mesh rightWingMesh = CreateOrUpdateWingWithServiceBay(RightWingMeshPath, false);
            Mesh leftPanelMesh = CreateOrUpdatePanelMesh(LeftPanelMeshPath, true);
            Mesh rightPanelMesh = CreateOrUpdatePanelMesh(RightPanelMeshPath, false);

            ApplyWingMesh(leftWingVisual, leftWingMesh, aluminum);
            ApplyWingMesh(rightWingVisual, rightWingMesh, aluminum);

            UpgradeOneWing(
                armamentRoot,
                true,
                leftPanelMesh,
                leftWingVisual.GetComponent<MeshRenderer>()?.sharedMaterial ?? aluminum,
                hardware,
                bayDark);
            UpgradeOneWing(
                armamentRoot,
                false,
                rightPanelMesh,
                rightWingVisual.GetComponent<MeshRenderer>()?.sharedMaterial ?? aluminum,
                hardware,
                bayDark);

            Transform[] muzzles = ReadMuzzles(system);
            P51BulletStreakVisualController streakController = aircraft.GetComponent<P51BulletStreakVisualController>();
            if (streakController == null)
            {
                streakController = Undo.AddComponent<P51BulletStreakVisualController>(aircraft);
            }
            streakController.Configure(system, muzzles);
            EditorUtility.SetDirty(streakController);
            EditorUtility.SetDirty(system);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 34 changed the armament visuals but Unity could not save the scene.");
                return;
            }

            Debug.Log(
                "P-51 Step 34 complete. Replaced the flat armament covers with flush cambered aluminum wing-skin panels, "
                + "cut real openings into the visual wing meshes, recessed the gun/ammo bays into the wing, and added short moving bullet streaks. "
                + "Flight colliders and flight physics were not changed.",
                aircraft);
        }

        [MenuItem("Hanger 51/P-51 Mustang/35 - Validate Wing Armament Visual Upgrade")]
        public static void ValidateWingArmamentVisualUpgrade()
        {
            bool passed = true;
            GameObject aircraft = GameObject.Find(AircraftRootName);
            if (aircraft == null)
            {
                Debug.LogError("P-51 Step 35 failed: aircraft is missing.");
                return;
            }

            Transform armamentRoot = aircraft.transform.Find(ArmamentRootName);
            if (armamentRoot == null)
            {
                Debug.LogError("P-51 Step 35 failed: armament root is missing.");
                return;
            }

            P51BulletStreakVisualController streakController = aircraft.GetComponent<P51BulletStreakVisualController>();
            if (streakController == null)
            {
                Debug.LogError("P-51 Step 35 failed: moving bullet-streak controller is missing.");
                passed = false;
            }

            P51WingArmamentServicePoint[] safePoints = armamentRoot.GetComponentsInChildren<P51WingArmamentServicePoint>(true);
            if (safePoints.Length != 14)
            {
                Debug.LogError($"P-51 Step 35 failed: expected 14 safe armament service points, found {safePoints.Length}.");
                passed = false;
            }

            for (int wingIndex = 0; wingIndex < 2; wingIndex++)
            {
                string wingName = wingIndex == 0 ? "Left" : "Right";
                Transform pivot = FindChildRecursive(armamentRoot, $"{wingName} Wing Armament Panel Pivot");
                Transform interior = FindChildRecursive(armamentRoot, $"{wingName} Wing Armament Bay Interior");
                if (pivot == null || pivot.GetComponentInChildren<MeshFilter>(true) == null)
                {
                    Debug.LogError($"P-51 Step 35 failed: {wingName.ToLowerInvariant()} flush panel mesh is missing.");
                    passed = false;
                }
                if (interior == null || FindChildRecursive(interior, $"{wingName} Armament Bay Floor") == null)
                {
                    Debug.LogError($"P-51 Step 35 failed: {wingName.ToLowerInvariant()} recessed bay geometry is missing.");
                    passed = false;
                }
            }

            Transform leftWing = FindChildRecursive(aircraft.transform, "Left Laminar Flow Wing");
            Transform rightWing = FindChildRecursive(aircraft.transform, "Right Laminar Flow Wing");
            string leftMeshName = leftWing != null && leftWing.GetComponent<MeshFilter>()?.sharedMesh != null
                ? leftWing.GetComponent<MeshFilter>().sharedMesh.name
                : string.Empty;
            string rightMeshName = rightWing != null && rightWing.GetComponent<MeshFilter>()?.sharedMesh != null
                ? rightWing.GetComponent<MeshFilter>().sharedMesh.name
                : string.Empty;
            if (!leftMeshName.Contains("Armament Bay") || !rightMeshName.Contains("Armament Bay"))
            {
                Debug.LogError("P-51 Step 35 failed: one or both wings are not using the armament-bay cutout meshes.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log("P-51 Step 35 passed. Flush wing-skin panels, real wing openings, recessed bays, 14 safe service points, and moving bullet streaks are installed.");
            }
        }

        private static void UpgradeOneWing(
            Transform armamentRoot,
            bool left,
            Mesh panelMesh,
            Material wingMaterial,
            Material hardware,
            Material bayDark)
        {
            string wingName = left ? "Left" : "Right";
            float sign = left ? -1f : 1f;
            float centerSpan = (BayInnerSpan + BayOuterSpan) * 0.5f;
            float centerX = sign * centerSpan;
            float hingeY = SampleTopY(centerSpan, BayRearZ);

            Transform pivot = FindChildRecursive(armamentRoot, $"{wingName} Wing Armament Panel Pivot");
            Transform interior = FindChildRecursive(armamentRoot, $"{wingName} Wing Armament Bay Interior");
            Transform panelTarget = FindChildRecursive(armamentRoot, $"{wingName} Wing Armament Panel Service Target");
            if (pivot == null || interior == null || panelTarget == null)
            {
                throw new InvalidOperationException($"{wingName} armament panel hierarchy is incomplete.");
            }

            DeleteAllChildren(pivot);
            pivot.localPosition = new Vector3(centerX, hingeY, BayRearZ);
            pivot.localRotation = Quaternion.identity;

            GameObject panel = new GameObject($"{wingName} Flush Wing Armament Skin Panel");
            Undo.RegisterCreatedObjectUndo(panel, "Create flush P-51 armament panel");
            panel.transform.SetParent(pivot, false);
            MeshFilter panelFilter = panel.AddComponent<MeshFilter>();
            panelFilter.sharedMesh = panelMesh;
            MeshRenderer panelRenderer = panel.AddComponent<MeshRenderer>();
            panelRenderer.sharedMaterial = wingMaterial;
            AddPanelHardware(panel.transform, left, hardware);

            float targetY = SampleTopY(centerSpan, (BayRearZ + BayFrontZ) * 0.5f) + 0.035f;
            panelTarget.localPosition = new Vector3(centerX, targetY, (BayRearZ + BayFrontZ) * 0.5f);
            BoxCollider targetCollider = panelTarget.GetComponent<BoxCollider>();
            if (targetCollider != null)
            {
                targetCollider.center = Vector3.zero;
                targetCollider.size = new Vector3(BayOuterSpan - BayInnerSpan + 0.08f, 0.18f, BayFrontZ - BayRearZ + 0.08f);
            }

            PreserveServiceTargetsAndClearOldBayGeometry(interior, wingName);
            interior.localPosition = new Vector3(centerX, BayInteriorY, 0f);
            interior.localRotation = Quaternion.identity;
            CreateBayGeometry(interior, wingName, wingMaterial, hardware, bayDark);
            LowerInstalledHardware(interior, wingName);
        }

        private static void PreserveServiceTargetsAndClearOldBayGeometry(Transform interior, string wingName)
        {
            List<GameObject> remove = new List<GameObject>();
            for (int index = 0; index < interior.childCount; index++)
            {
                Transform child = interior.GetChild(index);
                bool preserve = child.name.StartsWith($"{wingName} Gun Mount ", StringComparison.Ordinal)
                    || child.name.StartsWith($"{wingName} Ammo Bay ", StringComparison.Ordinal);
                if (!preserve)
                {
                    remove.Add(child.gameObject);
                }
            }

            for (int index = 0; index < remove.Count; index++)
            {
                Undo.DestroyObjectImmediate(remove[index]);
            }
        }

        private static void LowerInstalledHardware(Transform interior, string wingName)
        {
            for (int station = 1; station <= 3; station++)
            {
                Transform gun = FindChildRecursive(interior, $"{wingName} Gun Mount {station}");
                if (gun != null)
                {
                    Vector3 position = gun.localPosition;
                    position.y = 0f;
                    gun.localPosition = position;
                    Transform muzzle = FindChildRecursive(gun, "Muzzle");
                    if (muzzle != null)
                    {
                        Vector3 muzzlePosition = muzzle.localPosition;
                        muzzlePosition.y = 0.12f;
                        muzzle.localPosition = muzzlePosition;
                    }
                }

                Transform ammo = FindChildRecursive(interior, $"{wingName} Ammo Bay {station}");
                if (ammo != null)
                {
                    Vector3 position = ammo.localPosition;
                    position.y = -0.07f;
                    ammo.localPosition = position;
                }
            }
        }

        private static void CreateBayGeometry(
            Transform interior,
            string wingName,
            Material aluminum,
            Material hardware,
            Material bayDark)
        {
            float width = BayOuterSpan - BayInnerSpan;
            float depth = BayFrontZ - BayRearZ;
            float centerZ = (BayFrontZ + BayRearZ) * 0.5f;
            float halfWidth = width * 0.5f;

            CreateCube(interior, $"{wingName} Armament Bay Floor",
                new Vector3(0f, -0.035f, centerZ),
                new Vector3(width - 0.10f, 0.05f, depth - 0.10f),
                bayDark);

            CreateCube(interior, "Front Bay Wall",
                new Vector3(0f, 0.10f, BayFrontZ - 0.025f),
                new Vector3(width, 0.26f, 0.05f),
                bayDark);
            CreateCube(interior, "Rear Bay Wall",
                new Vector3(0f, 0.10f, BayRearZ + 0.025f),
                new Vector3(width, 0.26f, 0.05f),
                bayDark);
            CreateCube(interior, "Inner Bay Wall",
                new Vector3(-halfWidth + 0.025f, 0.10f, centerZ),
                new Vector3(0.05f, 0.26f, depth),
                bayDark);
            CreateCube(interior, "Outer Bay Wall",
                new Vector3(halfWidth - 0.025f, 0.10f, centerZ),
                new Vector3(0.05f, 0.26f, depth),
                bayDark);

            for (int rib = -2; rib <= 2; rib++)
            {
                CreateCube(interior, "Armament Bay Rib",
                    new Vector3(rib * width / 6f, 0.015f, centerZ),
                    new Vector3(0.035f, 0.075f, depth - 0.12f),
                    hardware);
            }

            const float lipY = 0.245f;
            CreateCube(interior, "Front Flush Aluminum Lip",
                new Vector3(0f, lipY, BayFrontZ),
                new Vector3(width + 0.06f, 0.026f, 0.045f),
                aluminum);
            CreateCube(interior, "Rear Flush Aluminum Lip",
                new Vector3(0f, lipY - 0.035f, BayRearZ),
                new Vector3(width + 0.06f, 0.026f, 0.045f),
                aluminum);
            CreateCube(interior, "Inner Flush Aluminum Lip",
                new Vector3(-halfWidth, lipY - 0.01f, centerZ),
                new Vector3(0.045f, 0.026f, depth + 0.04f),
                aluminum);
            CreateCube(interior, "Outer Flush Aluminum Lip",
                new Vector3(halfWidth, lipY + 0.015f, centerZ),
                new Vector3(0.045f, 0.026f, depth + 0.04f),
                aluminum);
        }

        private static void AddPanelHardware(Transform panel, bool left, Material hardware)
        {
            float centerSpan = (BayInnerSpan + BayOuterSpan) * 0.5f;
            float sign = left ? -1f : 1f;
            float pivotY = SampleTopY(centerSpan, BayRearZ);
            float[] xOffsets = { -1.05f, -0.70f, -0.35f, 0f, 0.35f, 0.70f, 1.05f };

            for (int index = 0; index < xOffsets.Length; index++)
            {
                float localX = xOffsets[index];
                float globalX = sign * centerSpan + localX;
                float absX = Mathf.Abs(globalX);
                AddFastener(panel, localX, SampleTopY(absX, BayRearZ) - pivotY + 0.014f, 0.035f, hardware);
                AddFastener(panel, localX, SampleTopY(absX, BayFrontZ) - pivotY + 0.014f, BayFrontZ - BayRearZ - 0.035f, hardware);
            }

            GameObject hinge = CreatePrimitive(panel, PrimitiveType.Cylinder, "Armament Panel Piano Hinge",
                new Vector3(0f, 0.015f, 0.018f),
                new Vector3(0.028f, (BayOuterSpan - BayInnerSpan) * 0.48f, 0.028f),
                new Vector3(0f, 0f, 90f),
                hardware);
            RemoveCollider(hinge);
        }

        private static void AddFastener(Transform parent, float x, float y, float z, Material material)
        {
            GameObject fastener = CreatePrimitive(parent, PrimitiveType.Sphere, "Flush Panel Fastener",
                new Vector3(x, y, z),
                Vector3.one * 0.032f,
                Vector3.zero,
                material);
            RemoveCollider(fastener);
        }

        private static Mesh CreateOrUpdateWingWithServiceBay(string path, bool left)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            float[] spans = { 0.38f, BayInnerSpan, BayOuterSpan, 5.64f };

            for (int spanIndex = 0; spanIndex < spans.Length - 1; spanIndex++)
            {
                float x0 = spans[spanIndex];
                float x1 = spans[spanIndex + 1];
                float midpoint = (x0 + x1) * 0.5f;
                bool crossesBay = midpoint > BayInnerSpan && midpoint < BayOuterSpan;

                if (crossesBay)
                {
                    AddWingSurfaceQuad(vertices, triangles,
                        WingPoint(x0, GetLeading(x0), true, left),
                        WingPoint(x1, GetLeading(x1), true, left),
                        WingPoint(x1, BayFrontZ, true, left),
                        WingPoint(x0, BayFrontZ, true, left),
                        Vector3.up);
                    AddWingSurfaceQuad(vertices, triangles,
                        WingPoint(x0, BayRearZ, true, left),
                        WingPoint(x1, BayRearZ, true, left),
                        WingPoint(x1, GetTrailing(x1), true, left),
                        WingPoint(x0, GetTrailing(x0), true, left),
                        Vector3.up);
                }
                else
                {
                    AddWingSurfaceQuad(vertices, triangles,
                        WingPoint(x0, GetLeading(x0), true, left),
                        WingPoint(x1, GetLeading(x1), true, left),
                        WingPoint(x1, GetTrailing(x1), true, left),
                        WingPoint(x0, GetTrailing(x0), true, left),
                        Vector3.up);
                }

                AddWingSurfaceQuad(vertices, triangles,
                    WingPoint(x0, GetTrailing(x0), false, left),
                    WingPoint(x1, GetTrailing(x1), false, left),
                    WingPoint(x1, GetLeading(x1), false, left),
                    WingPoint(x0, GetLeading(x0), false, left),
                    Vector3.down);

                Vector3 leadingNormal = Vector3.forward;
                AddWingSurfaceQuad(vertices, triangles,
                    WingPoint(x0, GetLeading(x0), false, left),
                    WingPoint(x1, GetLeading(x1), false, left),
                    WingPoint(x1, GetLeading(x1), true, left),
                    WingPoint(x0, GetLeading(x0), true, left),
                    leadingNormal);

                Vector3 trailingNormal = Vector3.back;
                AddWingSurfaceQuad(vertices, triangles,
                    WingPoint(x0, GetTrailing(x0), true, left),
                    WingPoint(x1, GetTrailing(x1), true, left),
                    WingPoint(x1, GetTrailing(x1), false, left),
                    WingPoint(x0, GetTrailing(x0), false, left),
                    trailingNormal);
            }

            float rootSpan = spans[0];
            float tipSpan = spans[spans.Length - 1];
            AddWingSurfaceQuad(vertices, triangles,
                WingPoint(rootSpan, GetLeading(rootSpan), true, left),
                WingPoint(rootSpan, GetTrailing(rootSpan), true, left),
                WingPoint(rootSpan, GetTrailing(rootSpan), false, left),
                WingPoint(rootSpan, GetLeading(rootSpan), false, left),
                left ? Vector3.right : Vector3.left);
            AddWingSurfaceQuad(vertices, triangles,
                WingPoint(tipSpan, GetLeading(tipSpan), false, left),
                WingPoint(tipSpan, GetTrailing(tipSpan), false, left),
                WingPoint(tipSpan, GetTrailing(tipSpan), true, left),
                WingPoint(tipSpan, GetLeading(tipSpan), true, left),
                left ? Vector3.left : Vector3.right);

            return SaveMesh(path, vertices, triangles, left ? "P-51D Left Wing Armament Bay" : "P-51D Right Wing Armament Bay");
        }

        private static Mesh CreateOrUpdatePanelMesh(string path, bool left)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            float centerSpan = (BayInnerSpan + BayOuterSpan) * 0.5f;
            float pivotY = SampleTopY(centerSpan, BayRearZ);
            float sign = left ? -1f : 1f;
            float[] spans = { BayInnerSpan, centerSpan, BayOuterSpan };
            float[] zSamples = { BayRearZ, (BayRearZ + BayFrontZ) * 0.5f, BayFrontZ };
            const float thickness = 0.025f;

            Vector3[,] top = new Vector3[spans.Length, zSamples.Length];
            Vector3[,] bottom = new Vector3[spans.Length, zSamples.Length];
            for (int xIndex = 0; xIndex < spans.Length; xIndex++)
            {
                float globalX = sign * spans[xIndex];
                float localX = globalX - sign * centerSpan;
                for (int zIndex = 0; zIndex < zSamples.Length; zIndex++)
                {
                    float localZ = zSamples[zIndex] - BayRearZ;
                    float localY = SampleTopY(spans[xIndex], zSamples[zIndex]) - pivotY + 0.004f;
                    top[xIndex, zIndex] = new Vector3(localX, localY, localZ);
                    bottom[xIndex, zIndex] = new Vector3(localX, localY - thickness, localZ);
                }
            }

            for (int xIndex = 0; xIndex < spans.Length - 1; xIndex++)
            {
                for (int zIndex = 0; zIndex < zSamples.Length - 1; zIndex++)
                {
                    AddWingSurfaceQuad(vertices, triangles,
                        top[xIndex, zIndex],
                        top[xIndex + 1, zIndex],
                        top[xIndex + 1, zIndex + 1],
                        top[xIndex, zIndex + 1],
                        Vector3.up);
                    AddWingSurfaceQuad(vertices, triangles,
                        bottom[xIndex, zIndex + 1],
                        bottom[xIndex + 1, zIndex + 1],
                        bottom[xIndex + 1, zIndex],
                        bottom[xIndex, zIndex],
                        Vector3.down);
                }
            }

            for (int xIndex = 0; xIndex < spans.Length - 1; xIndex++)
            {
                AddWingSurfaceQuad(vertices, triangles,
                    bottom[xIndex, 0], bottom[xIndex + 1, 0], top[xIndex + 1, 0], top[xIndex, 0], Vector3.back);
                int front = zSamples.Length - 1;
                AddWingSurfaceQuad(vertices, triangles,
                    top[xIndex, front], top[xIndex + 1, front], bottom[xIndex + 1, front], bottom[xIndex, front], Vector3.forward);
            }

            for (int zIndex = 0; zIndex < zSamples.Length - 1; zIndex++)
            {
                Vector3 rootNormal = left ? Vector3.right : Vector3.left;
                AddWingSurfaceQuad(vertices, triangles,
                    top[0, zIndex], top[0, zIndex + 1], bottom[0, zIndex + 1], bottom[0, zIndex], rootNormal);
                int outer = spans.Length - 1;
                Vector3 tipNormal = left ? Vector3.left : Vector3.right;
                AddWingSurfaceQuad(vertices, triangles,
                    bottom[outer, zIndex], bottom[outer, zIndex + 1], top[outer, zIndex + 1], top[outer, zIndex], tipNormal);
            }

            return SaveMesh(path, vertices, triangles, left ? "P-51D Left Flush Armament Panel" : "P-51D Right Flush Armament Panel");
        }

        private static void ApplyWingMesh(Transform wingVisual, Mesh mesh, Material fallbackMaterial)
        {
            MeshFilter filter = wingVisual.GetComponent<MeshFilter>();
            MeshRenderer renderer = wingVisual.GetComponent<MeshRenderer>();
            if (filter == null || renderer == null)
            {
                throw new InvalidOperationException($"Wing visual '{wingVisual.name}' does not contain a MeshFilter and MeshRenderer.");
            }
            filter.sharedMesh = mesh;
            if (renderer.sharedMaterial == null) renderer.sharedMaterial = fallbackMaterial;
            EditorUtility.SetDirty(filter);
            EditorUtility.SetDirty(renderer);
        }

        private static Transform[] ReadMuzzles(P51WingArmamentSystem system)
        {
            Transform[] result = new Transform[6];
            SerializedObject serialized = new SerializedObject(system);
            SerializedProperty property = serialized.FindProperty("muzzles");
            if (property == null || !property.isArray)
            {
                return result;
            }
            int count = Mathf.Min(result.Length, property.arraySize);
            for (int index = 0; index < count; index++)
            {
                result[index] = property.GetArrayElementAtIndex(index).objectReferenceValue as Transform;
            }
            return result;
        }

        private static Vector3 WingPoint(float absSpan, float z, bool top, bool left)
        {
            return new Vector3(
                (left ? -1f : 1f) * absSpan,
                top ? SampleTopY(absSpan, z) : SampleBottomY(absSpan, z),
                z);
        }

        private static float SampleTopY(float absSpan, float z)
        {
            SampleWingParameters(absSpan, out float leading, out float trailing, out float centerY, out float thickness);
            float chordT = Mathf.Clamp01((z - leading) / (trailing - leading));
            return Mathf.Lerp(centerY + thickness, centerY + thickness * 0.58f, chordT);
        }

        private static float SampleBottomY(float absSpan, float z)
        {
            SampleWingParameters(absSpan, out float leading, out float trailing, out float centerY, out float thickness);
            float chordT = Mathf.Clamp01((z - leading) / (trailing - leading));
            return Mathf.Lerp(centerY - thickness, centerY - thickness * 0.62f, chordT);
        }

        private static float GetLeading(float absSpan)
        {
            SampleWingParameters(absSpan, out float leading, out _, out _, out _);
            return leading;
        }

        private static float GetTrailing(float absSpan)
        {
            SampleWingParameters(absSpan, out _, out float trailing, out _, out _);
            return trailing;
        }

        private static void SampleWingParameters(
            float absSpan,
            out float leading,
            out float trailing,
            out float centerY,
            out float thickness)
        {
            absSpan = Mathf.Clamp(absSpan, WingSpans[0], WingSpans[WingSpans.Length - 1]);
            int segment = 0;
            for (int index = 0; index < WingSpans.Length - 1; index++)
            {
                if (absSpan >= WingSpans[index] && absSpan <= WingSpans[index + 1])
                {
                    segment = index;
                    break;
                }
            }

            float t = Mathf.InverseLerp(WingSpans[segment], WingSpans[segment + 1], absSpan);
            leading = Mathf.Lerp(WingLeading[segment], WingLeading[segment + 1], t);
            trailing = Mathf.Lerp(WingTrailing[segment], WingTrailing[segment + 1], t);
            centerY = Mathf.Lerp(WingCenterY[segment], WingCenterY[segment + 1], t);
            thickness = Mathf.Lerp(WingThickness[segment], WingThickness[segment + 1], t);
        }

        private static void AddWingSurfaceQuad(
            List<Vector3> vertices,
            List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d,
            Vector3 expectedNormal)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);

            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(normal, expectedNormal) >= 0f)
            {
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
            }
            else
            {
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 1);
                triangles.Add(start);
                triangles.Add(start + 3);
                triangles.Add(start + 2);
            }
        }

        private static Mesh SaveMesh(string path, List<Vector3> vertices, List<int> triangles, string meshName)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, path);
            }
            else
            {
                mesh.Clear();
            }

            mesh.name = meshName;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static GameObject CreateCube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject cube = CreatePrimitive(parent, PrimitiveType.Cube, name, position, scale, Vector3.zero, material);
            RemoveCollider(cube);
            return cube;
        }

        private static GameObject CreatePrimitive(
            Transform parent,
            PrimitiveType primitiveType,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEuler,
            Material material)
        {
            GameObject created = GameObject.CreatePrimitive(primitiveType);
            created.name = name;
            Undo.RegisterCreatedObjectUndo(created, $"Create {name}");
            created.transform.SetParent(parent, false);
            created.transform.localPosition = localPosition;
            created.transform.localRotation = Quaternion.Euler(localEuler);
            created.transform.localScale = localScale;
            Renderer renderer = created.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            return created;
        }

        private static void RemoveCollider(GameObject gameObject)
        {
            Collider collider = gameObject != null ? gameObject.GetComponent<Collider>() : null;
            if (collider != null) Object.DestroyImmediate(collider);
        }

        private static void DeleteAllChildren(Transform parent)
        {
            List<GameObject> remove = new List<GameObject>();
            for (int index = 0; index < parent.childCount; index++)
            {
                remove.Add(parent.GetChild(index).gameObject);
            }
            for (int index = 0; index < remove.Count; index++)
            {
                Undo.DestroyObjectImmediate(remove[index]);
            }
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent == null) return null;
            Transform[] transforms = parent.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index] != null && transforms[index].name == name)
                {
                    return transforms[index];
                }
            }
            return null;
        }
    }
}
