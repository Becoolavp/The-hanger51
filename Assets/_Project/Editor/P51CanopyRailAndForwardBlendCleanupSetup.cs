using System.Collections.Generic;
using Hanger51.Aircraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51CanopyRailAndForwardBlendCleanupSetup
    {
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string SmoothCanopyName = "P-51 Smooth Sealed Canopy Assembly";
        private const string SmoothGlassName = "P-51 Smooth Bubble Canopy Glass";
        private const string WindshieldBowName = "P-51 Windshield Transition Bow";
        private const string LeftCanopyRailName = "P-51 Left Canopy Rail";
        private const string RightCanopyRailName = "P-51 Right Canopy Rail";
        private const string OldRaisedTransitionName = "P-51 Raised Cockpit Nose Transition";
        private const string CompactBlendName = "P-51 Compact Windshield Cowling Blend";
        private const string CompactRearSealName = "P-51 Compact Windshield Rear Seal";
        private const string CompactFrontSealName = "P-51 Compact Cowling Front Seal";

        private const string CompactBlendMeshPath =
            "Assets/_Project/Aircraft/P51/Meshes/P51D_CompactWindshieldCowlingBlend.asset";
        private const string MetalMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/PolishedAluminum.mat";
        private const string DarkMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/DarkAircraftMetal.mat";

        // The Step 77 canopy remains at the Step 81 raised root height. The support bow is rebuilt
        // directly on the glass at a slightly more aft station, rather than floating up/forward
        // as a separately offset arch. Thin tubing keeps it out of the pilot's primary sightline.
        private const float BowZ = 0.52f;
        private const float BowHalfWidth = 0.564f;
        private const float BowSillY = 1.705f;
        private const float BowCrownY = 2.276f;
        private const float BowThickness = 0.018f;
        private const float ArchExponent = 0.78f;
        private const int BowSegments = 24;
        private const int BlendCrossSegments = 18;

        private readonly struct BlendStation
        {
            internal readonly float Z;
            internal readonly float HalfWidth;
            internal readonly float EdgeY;
            internal readonly float CrownY;

            internal BlendStation(float z, float halfWidth, float edgeY, float crownY)
            {
                Z = z;
                HalfWidth = halfWidth;
                EdgeY = edgeY;
                CrownY = crownY;
            }
        }

        // This bridge only occupies the windshield-base / cockpit-cut junction. The Step 81
        // transition extended to Z=1.72 and visibly laid over the removable engine cowling.
        // Here the final station ends at Z=1.40, only a few centimeters beyond the true cut edge.
        private static readonly BlendStation[] BlendStations =
        {
            new BlendStation(1.12f, 0.465f, 1.990f, 2.018f),
            new BlendStation(1.20f, 0.475f, 1.998f, 2.030f),
            new BlendStation(1.27f, 0.525f, 2.008f, 2.055f),
            new BlendStation(1.34f, 0.585f, 2.018f, 2.085f),
            new BlendStation(1.40f, 0.595f, 2.025f, 2.098f)
        };

        [MenuItem("Hanger 51/P-51 Mustang/83 - Refit Canopy Rail to Glass and Clean Front Cowling Blend")]
        public static void RefitCanopyRailAndFrontBlend()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 Step 83 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("P-51 Step 83 failed. Open the saved Hanger 51 gameplay scene first.");
                return;
            }

            Material metal = AssetDatabase.LoadAssetAtPath<Material>(MetalMaterialPath);
            Material dark = AssetDatabase.LoadAssetAtPath<Material>(DarkMaterialPath);
            if (metal == null || dark == null)
            {
                Debug.LogError("P-51 Step 83 failed. Required P-51 materials are missing.");
                return;
            }

            Mesh compactBlendMesh = CreateOrUpdateCompactBlendMesh();
            if (compactBlendMesh == null)
            {
                Debug.LogError("P-51 Step 83 failed. Compact windshield/cowling blend mesh could not be created.");
                return;
            }

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft == null || aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 83 failed. No P-51 aircraft were found in the scene.");
                return;
            }

            int aircraftUpdated = 0;
            int oldTransitionsDisabled = 0;
            P51FlightController master = null;

            for (int i = 0; i < aircraft.Length; i++)
            {
                P51FlightController flight = aircraft[i];
                if (flight == null || !flight.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (flight.name == AircraftRootName)
                {
                    master = flight;
                }

                Transform canopy = FindDescendant(flight.transform, SmoothCanopyName);
                Transform glass = FindDescendant(canopy, SmoothGlassName);
                if (canopy == null || glass == null)
                {
                    Debug.LogWarning(
                        $"P-51 Step 83 skipped '{flight.name}' because its raised Step 77 smooth canopy is missing.",
                        flight);
                    continue;
                }

                RebuildWindshieldSupportBow(canopy, dark);
                RealignSideRail(canopy, LeftCanopyRailName);
                RealignSideRail(canopy, RightCanopyRailName);

                Transform oldTransition = FindDescendant(flight.transform, OldRaisedTransitionName);
                if (oldTransition != null && oldTransition.gameObject.activeSelf)
                {
                    Undo.RecordObject(oldTransition.gameObject, "Disable oversized P-51 raised cowling flap");
                    oldTransition.gameObject.SetActive(false);
                    EditorUtility.SetDirty(oldTransition.gameObject);
                    oldTransitionsDisabled++;
                }

                BuildOrUpdateCompactBlend(flight.transform, compactBlendMesh, metal, dark);
                EditorUtility.SetDirty(canopy);
                EditorUtility.SetDirty(flight);
                aircraftUpdated++;
            }

            EditorUtility.SetDirty(compactBlendMesh);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 83 made the canopy/front-blend changes but Unity could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 Step 83 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = master != null ? master.gameObject : aircraft[0].gameObject;
            Debug.Log(
                $"P-51 Step 83 complete. Updated {aircraftUpdated} aircraft, disabled {oldTransitionsDisabled} oversized Step 81 cowling transition(s), "
                + $"rebuilt the windshield support bow directly on the canopy glass at Z={BowZ:F2} with {BowThickness:F3} m tubing, reset both canopy side-rail roots to the glass assembly, "
                + "and replaced the forward flap with a compact windshield-to-cowling blend that ends just beyond the cockpit cut instead of extending across the engine cowling.",
                master != null ? master.gameObject : null);
        }

        [MenuItem("Hanger 51/P-51 Mustang/84 - Validate Canopy Rail Fit and Compact Cowling Blend")]
        public static void ValidateCanopyRailAndFrontBlend()
        {
            bool passed = true;
            int aircraftChecked = 0;

            Mesh compactBlendMesh = AssetDatabase.LoadAssetAtPath<Mesh>(CompactBlendMeshPath);
            if (compactBlendMesh == null)
            {
                Debug.LogError("P-51 Step 84 failed. Compact windshield/cowling blend mesh is missing.");
                return;
            }

            Bounds blendBounds = compactBlendMesh.bounds;
            if (blendBounds.min.z > 1.13f
                || blendBounds.max.z < 1.38f
                || blendBounds.max.z > 1.43f
                || blendBounds.size.x < 1.10f
                || blendBounds.max.y > 2.13f)
            {
                Debug.LogError(
                    $"P-51 Step 84 failed. Compact windshield/cowling blend has unexpected bounds {blendBounds}.",
                    compactBlendMesh);
                passed = false;
            }

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft == null || aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 84 failed. No P-51 aircraft were found.");
                return;
            }

            for (int i = 0; i < aircraft.Length; i++)
            {
                P51FlightController flight = aircraft[i];
                if (flight == null || !flight.gameObject.scene.IsValid())
                {
                    continue;
                }

                Transform canopy = FindDescendant(flight.transform, SmoothCanopyName);
                Transform glass = FindDescendant(canopy, SmoothGlassName);
                Transform bow = FindDescendant(canopy, WindshieldBowName);
                Transform leftRail = FindDescendant(canopy, LeftCanopyRailName);
                Transform rightRail = FindDescendant(canopy, RightCanopyRailName);
                Transform compactBlend = FindDescendant(flight.transform, CompactBlendName);
                Transform rearSeal = FindDescendant(compactBlend, CompactRearSealName);
                Transform frontSeal = FindDescendant(compactBlend, CompactFrontSealName);
                Transform oldTransition = FindDescendant(flight.transform, OldRaisedTransitionName);

                if (canopy == null || glass == null || bow == null || leftRail == null || rightRail == null
                    || compactBlend == null || rearSeal == null || frontSeal == null)
                {
                    Debug.LogError(
                        $"P-51 Step 84 failed. '{flight.name}' is missing canopy rail/bow or compact windshield blend geometry.",
                        flight);
                    passed = false;
                    continue;
                }

                aircraftChecked++;

                if (bow.childCount < BowSegments
                    || Mathf.Abs(bow.localPosition.x) > 0.002f
                    || Mathf.Abs(bow.localPosition.y) > 0.002f
                    || Mathf.Abs(bow.localPosition.z) > 0.002f)
                {
                    Debug.LogError(
                        $"P-51 Step 84 failed. '{flight.name}' windshield support bow is not rebuilt directly on the canopy glass.",
                        bow);
                    passed = false;
                }

                if (leftRail.localPosition.sqrMagnitude > 0.000004f
                    || rightRail.localPosition.sqrMagnitude > 0.000004f)
                {
                    Debug.LogError(
                        $"P-51 Step 84 failed. '{flight.name}' canopy side rails are offset from their glass assembly.",
                        canopy);
                    passed = false;
                }

                if (oldTransition != null && oldTransition.gameObject.activeSelf)
                {
                    Debug.LogError(
                        $"P-51 Step 84 failed. '{flight.name}' still has the oversized Step 81 cowling flap active.",
                        oldTransition);
                    passed = false;
                }

                MeshFilter filter = compactBlend.GetComponent<MeshFilter>();
                MeshRenderer renderer = compactBlend.GetComponent<MeshRenderer>();
                if (filter == null || filter.sharedMesh != compactBlendMesh || renderer == null || !renderer.enabled)
                {
                    Debug.LogError(
                        $"P-51 Step 84 failed. '{flight.name}' compact windshield/cowling blend is not rendering the shared mesh.",
                        compactBlend);
                    passed = false;
                }

                Collider[] bowColliders = bow.GetComponentsInChildren<Collider>(true);
                Collider[] blendColliders = compactBlend.GetComponentsInChildren<Collider>(true);
                if (bowColliders.Length != 0 || blendColliders.Length != 0)
                {
                    Debug.LogError(
                        $"P-51 Step 84 failed. '{flight.name}' canopy support/front blend must remain visual-only. "
                        + $"Bow colliders={bowColliders.Length}, blend colliders={blendColliders.Length}.",
                        flight);
                    passed = false;
                }
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("P-51 Step 84 failed. Standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"P-51 Step 84 passed. Aircraft checked={aircraftChecked}. The support bow is thin and rebuilt on the canopy glass, both canopy side rails are aligned to the glass assembly, "
                    + "the oversized Step 81 cowling flap is disabled, and the compact forward blend terminates at the cockpit/cowling junction without extending across the removable engine cowling.");
            }
        }

        private static Mesh CreateOrUpdateCompactBlendMesh()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(CompactBlendMeshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "P-51D Compact Windshield Cowling Blend" };
                AssetDatabase.CreateAsset(mesh, CompactBlendMeshPath);
            }
            else
            {
                Undo.RegisterCompleteObjectUndo(mesh, "Rebuild P-51 compact windshield/cowling blend");
                mesh.Clear();
                mesh.name = "P-51D Compact Windshield Cowling Blend";
            }

            int verticesPerStation = BlendCrossSegments + 1;
            List<Vector3> vertices = new List<Vector3>(BlendStations.Length * verticesPerStation);
            List<Vector2> uvs = new List<Vector2>(BlendStations.Length * verticesPerStation);
            List<int> triangles = new List<int>((BlendStations.Length - 1) * BlendCrossSegments * 6);

            for (int stationIndex = 0; stationIndex < BlendStations.Length; stationIndex++)
            {
                BlendStation station = BlendStations[stationIndex];
                float longitudinalT = stationIndex / (float)(BlendStations.Length - 1);
                for (int segment = 0; segment <= BlendCrossSegments; segment++)
                {
                    float crossT = segment / (float)BlendCrossSegments;
                    float normalizedX = Mathf.Lerp(-1f, 1f, crossT);
                    float radial = Mathf.Sqrt(Mathf.Max(0f, 1f - normalizedX * normalizedX));
                    float arch = Mathf.Pow(radial, ArchExponent);
                    float x = normalizedX * station.HalfWidth;
                    float y = Mathf.Lerp(station.EdgeY, station.CrownY, arch);
                    vertices.Add(new Vector3(x, y, station.Z));
                    uvs.Add(new Vector2(crossT, longitudinalT));
                }
            }

            for (int stationIndex = 0; stationIndex < BlendStations.Length - 1; stationIndex++)
            {
                int row = stationIndex * verticesPerStation;
                int nextRow = (stationIndex + 1) * verticesPerStation;
                for (int segment = 0; segment < BlendCrossSegments; segment++)
                {
                    int a = row + segment;
                    int b = row + segment + 1;
                    int c = nextRow + segment;
                    int d = nextRow + segment + 1;
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static void RebuildWindshieldSupportBow(Transform canopy, Material dark)
        {
            Transform oldBow = FindDirectChild(canopy, WindshieldBowName);
            if (oldBow != null)
            {
                Undo.DestroyObjectImmediate(oldBow.gameObject);
            }

            GameObject bowObject = new GameObject(WindshieldBowName);
            Undo.RegisterCreatedObjectUndo(bowObject, "Rebuild P-51 canopy support bow on glass");
            Transform bow = bowObject.transform;
            bow.SetParent(canopy, false);
            bow.localPosition = Vector3.zero;
            bow.localRotation = Quaternion.identity;
            bow.localScale = Vector3.one;

            Vector3 previous = Vector3.zero;
            bool havePrevious = false;
            float height = BowCrownY - BowSillY;
            for (int i = 0; i <= BowSegments; i++)
            {
                float t = i / (float)BowSegments;
                float normalizedX = Mathf.Lerp(-1f, 1f, t);
                float radial = Mathf.Sqrt(Mathf.Max(0f, 1f - normalizedX * normalizedX));
                float arch = Mathf.Pow(radial, ArchExponent);
                Vector3 point = new Vector3(
                    normalizedX * BowHalfWidth,
                    BowSillY + height * arch + 0.008f,
                    BowZ);

                if (havePrevious)
                {
                    CreateBeamBetween(
                        bow,
                        $"Support Segment {i:00}",
                        previous,
                        point,
                        BowThickness,
                        BowThickness,
                        dark);
                }
                previous = point;
                havePrevious = true;
            }

            RemoveColliders(bow.gameObject);
            EditorUtility.SetDirty(bow);
        }

        private static void RealignSideRail(Transform canopy, string railName)
        {
            Transform rail = FindDirectChild(canopy, railName);
            if (rail == null)
            {
                return;
            }

            Undo.RecordObject(rail, $"Realign {railName} with canopy glass");
            rail.localPosition = Vector3.zero;
            rail.localRotation = Quaternion.identity;
            rail.localScale = Vector3.one;
            EditorUtility.SetDirty(rail);
        }

        private static void BuildOrUpdateCompactBlend(
            Transform aircraft,
            Mesh mesh,
            Material metal,
            Material dark)
        {
            Transform root = FindDirectChild(aircraft, CompactBlendName);
            if (root == null)
            {
                GameObject rootObject = new GameObject(CompactBlendName);
                Undo.RegisterCreatedObjectUndo(rootObject, "Create compact P-51 windshield/cowling blend");
                root = rootObject.transform;
                root.SetParent(aircraft, false);
            }
            else
            {
                Undo.RecordObject(root.gameObject, "Refit compact P-51 windshield/cowling blend");
                root.gameObject.SetActive(true);
            }

            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;

            MeshFilter filter = root.GetComponent<MeshFilter>();
            if (filter == null)
            {
                filter = Undo.AddComponent<MeshFilter>(root.gameObject);
            }
            filter.sharedMesh = mesh;

            MeshRenderer renderer = root.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = Undo.AddComponent<MeshRenderer>(root.gameObject);
            }
            renderer.sharedMaterial = metal;
            renderer.receiveShadows = true;

            CreateOrUpdateVisualCube(
                root,
                CompactRearSealName,
                new Vector3(0f, 1.995f, 1.155f),
                new Vector3(0.99f, 0.030f, 0.090f),
                dark);
            CreateOrUpdateVisualCube(
                root,
                CompactFrontSealName,
                new Vector3(0f, 2.055f, 1.375f),
                new Vector3(1.16f, 0.025f, 0.080f),
                metal);

            RemoveColliders(root.gameObject);
            EditorUtility.SetDirty(filter);
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(root);
        }

        private static Transform CreateBeamBetween(
            Transform parent,
            string name,
            Vector3 start,
            Vector3 end,
            float width,
            float height,
            Material material)
        {
            Vector3 delta = end - start;
            float length = delta.magnitude;
            if (length < 0.0001f)
            {
                return null;
            }

            GameObject beamObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beamObject.name = name;
            Undo.RegisterCreatedObjectUndo(beamObject, $"Create {name}");
            Transform beam = beamObject.transform;
            beam.SetParent(parent, false);
            beam.localPosition = (start + end) * 0.5f;
            beam.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            beam.localScale = new Vector3(width, height, length);
            RemoveColliders(beamObject);

            Renderer renderer = beamObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                EditorUtility.SetDirty(renderer);
            }
            return beam;
        }

        private static Transform CreateOrUpdateVisualCube(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            Transform existing = FindDirectChild(parent, name);
            GameObject part;
            if (existing == null)
            {
                part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                part.name = name;
                Undo.RegisterCreatedObjectUndo(part, $"Create {name}");
                part.transform.SetParent(parent, false);
            }
            else
            {
                part = existing.gameObject;
                Undo.RecordObject(part.transform, $"Refit {name}");
                part.SetActive(true);
            }

            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = localScale;
            RemoveColliders(part);

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                EditorUtility.SetDirty(renderer);
            }
            EditorUtility.SetDirty(part.transform);
            return part.transform;
        }

        private static void RemoveColliders(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = colliders.Length - 1; i >= 0; i--)
            {
                if (colliders[i] != null)
                {
                    Object.DestroyImmediate(colliders[i]);
                }
            }
        }

        private static Transform FindDirectChild(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child != null && child.name == childName)
                {
                    return child;
                }
            }
            return null;
        }

        private static Transform FindDescendant(Transform root, string descendantName)
        {
            if (root == null || string.IsNullOrEmpty(descendantName))
            {
                return null;
            }

            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                if (descendants[i] != null && descendants[i].name == descendantName)
                {
                    return descendants[i];
                }
            }
            return null;
        }
    }
}
