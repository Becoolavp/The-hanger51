using System.Collections.Generic;
using Hanger51.Aircraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51ForwardWindshieldDeckAndPilotEyeSetup
    {
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string BridgeName = "P-51 Forward Windshield Deck Bridge";
        private const string RearSealName = "P-51 Windshield Deck Overlap Seal";
        private const string SmoothCanopyName = "P-51 Smooth Sealed Canopy Assembly";
        private const string SmoothGlassName = "P-51 Smooth Bubble Canopy Glass";

        private const string BridgeMeshPath =
            "Assets/_Project/Aircraft/P51/Meshes/P51D_ForwardWindshieldDeckBridge.asset";
        private const string MetalMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/PolishedAluminum.mat";
        private const string DarkMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/DarkAircraftMetal.mat";

        // Step 75 placed the eye at Y=1.94. The Step 77 bubble has enough crown clearance to
        // move the pilot up 22 cm without moving the cockpit shell, seat, panel or controls.
        private static readonly Vector3 PilotEyeLocalPosition = new Vector3(0f, 2.16f, -0.56f);

        private const int CrossSectionSegments = 16;
        private const float ArchExponent = 0.72f;

        private readonly struct DeckStation
        {
            internal readonly float Z;
            internal readonly float HalfWidth;
            internal readonly float EdgeY;
            internal readonly float CrownY;

            internal DeckStation(float z, float halfWidth, float edgeY, float crownY)
            {
                Z = z;
                HalfWidth = halfWidth;
                EdgeY = edgeY;
                CrownY = crownY;
            }
        }

        // Rear stations tuck underneath the Step 77 windshield base. The middle stations fan
        // outward across the final portion of the true cockpit cutout, and the forward stations
        // climb into/overlap the intact upper fuselage. This overlap is deliberate: it prevents
        // a daylight seam even when the two independently generated meshes land a few mm apart.
        private static readonly DeckStation[] DeckStations =
        {
            new DeckStation(1.08f, 0.470f, 1.720f, 1.775f),
            new DeckStation(1.18f, 0.485f, 1.730f, 1.795f),
            new DeckStation(1.26f, 0.545f, 1.740f, 1.865f),
            new DeckStation(1.34f, 0.600f, 1.755f, 2.015f),
            new DeckStation(1.42f, 0.600f, 1.770f, 2.075f),
            new DeckStation(1.52f, 0.580f, 1.790f, 2.110f),
            new DeckStation(1.62f, 0.560f, 1.810f, 2.130f)
        };

        [MenuItem("Hanger 51/P-51 Mustang/79 - Close Forward Canopy Gap and Raise Pilot Eye")]
        public static void CloseForwardGapAndRaisePilotEye()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 Step 79 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("P-51 Step 79 failed. Open the saved Hanger 51 gameplay scene first.");
                return;
            }

            Material metal = AssetDatabase.LoadAssetAtPath<Material>(MetalMaterialPath);
            Material dark = AssetDatabase.LoadAssetAtPath<Material>(DarkMaterialPath);
            if (metal == null || dark == null)
            {
                Debug.LogError("P-51 Step 79 failed. Required P-51 materials are missing.");
                return;
            }

            Mesh bridgeMesh = CreateOrUpdateBridgeMesh();
            if (bridgeMesh == null)
            {
                Debug.LogError("P-51 Step 79 failed. The forward windshield deck mesh could not be created.");
                return;
            }

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft == null || aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 79 failed. No P-51 aircraft were found in the scene.");
                return;
            }

            int aircraftUpdated = 0;
            int eyesRaised = 0;
            int bridgesBuilt = 0;
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
                P51PilotSeat[] seats = flight.GetComponentsInChildren<P51PilotSeat>(true);
                P51PilotSeat seat = seats.Length > 0 ? seats[0] : null;

                if (canopy == null || glass == null || seat == null || seat.CameraAnchor == null)
                {
                    Debug.LogWarning(
                        $"P-51 Step 79 skipped '{flight.name}' because its Step 77 canopy or pilot camera anchor is missing.",
                        flight);
                    continue;
                }

                Undo.RecordObject(seat.CameraAnchor, "Raise P-51 pilot eye");
                seat.CameraAnchor.position = flight.transform.TransformPoint(PilotEyeLocalPosition);
                seat.CameraAnchor.rotation = flight.transform.rotation;
                EditorUtility.SetDirty(seat.CameraAnchor);
                eyesRaised++;

                BuildOrUpdateForwardDeck(flight.transform, bridgeMesh, metal, dark);
                bridgesBuilt++;
                aircraftUpdated++;
                EditorUtility.SetDirty(flight);
            }

            EditorUtility.SetDirty(bridgeMesh);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 79 made the changes but Unity could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 Step 79 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = master != null ? master.gameObject : aircraft[0].gameObject;
            Debug.Log(
                $"P-51 Step 79 complete. Updated {aircraftUpdated} aircraft, raised {eyesRaised} pilot eye anchor(s) from the old 1.94 m height to {PilotEyeLocalPosition.y:F2} m, "
                + $"and built {bridgesBuilt} fitted forward windshield deck bridge(s). The bridge overlaps the windshield base at the rear, spans the last open portion of the true cockpit cutout, "
                + "and overlaps the intact upper fuselage at the front so the former daylight trough cannot remain open.",
                master != null ? master.gameObject : null);
        }

        [MenuItem("Hanger 51/P-51 Mustang/80 - Validate Forward Deck Closure and Pilot Visibility")]
        public static void ValidateForwardDeckAndPilotVisibility()
        {
            bool passed = true;
            int aircraftChecked = 0;

            Mesh bridgeMesh = AssetDatabase.LoadAssetAtPath<Mesh>(BridgeMeshPath);
            if (bridgeMesh == null)
            {
                Debug.LogError("P-51 Step 80 failed. Forward windshield deck bridge mesh asset is missing.");
                return;
            }

            Bounds bridgeBounds = bridgeMesh.bounds;
            if (bridgeBounds.size.x < 1.05f
                || bridgeBounds.size.z < 0.50f
                || bridgeBounds.max.z < 1.58f
                || bridgeBounds.min.z > 1.10f
                || bridgeBounds.max.y < 2.08f)
            {
                Debug.LogError(
                    $"P-51 Step 80 failed. Forward deck bridge does not overlap enough of the windshield/cowling transition. Bounds={bridgeBounds}.",
                    bridgeMesh);
                passed = false;
            }

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft == null || aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 80 failed. No P-51 aircraft were found.");
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
                Transform bridge = FindDescendant(flight.transform, BridgeName);
                Transform rearSeal = FindDescendant(bridge, RearSealName);
                P51PilotSeat[] seats = flight.GetComponentsInChildren<P51PilotSeat>(true);
                P51PilotSeat seat = seats.Length > 0 ? seats[0] : null;

                if (canopy == null || glass == null || bridge == null || rearSeal == null
                    || seat == null || seat.CameraAnchor == null)
                {
                    Debug.LogError(
                        $"P-51 Step 80 failed. '{flight.name}' is missing the smooth canopy, forward deck bridge, overlap seal, or pilot camera anchor.",
                        flight);
                    passed = false;
                    continue;
                }

                aircraftChecked++;
                Vector3 eye = flight.transform.InverseTransformPoint(seat.CameraAnchor.position);
                if (Mathf.Abs(eye.x - PilotEyeLocalPosition.x) > 0.015f
                    || Mathf.Abs(eye.y - PilotEyeLocalPosition.y) > 0.015f
                    || Mathf.Abs(eye.z - PilotEyeLocalPosition.z) > 0.015f)
                {
                    Debug.LogError(
                        $"P-51 Step 80 failed. '{flight.name}' pilot eye is not at the improved visibility position. "
                        + $"Current local eye={eye}, expected={PilotEyeLocalPosition}.",
                        seat.CameraAnchor);
                    passed = false;
                }

                MeshFilter filter = bridge.GetComponent<MeshFilter>();
                MeshRenderer renderer = bridge.GetComponent<MeshRenderer>();
                if (filter == null || filter.sharedMesh != bridgeMesh || renderer == null || !renderer.enabled)
                {
                    Debug.LogError(
                        $"P-51 Step 80 failed. '{flight.name}' forward deck bridge is not rendering the shared bridge mesh.",
                        bridge);
                    passed = false;
                }

                Collider[] colliders = bridge.GetComponentsInChildren<Collider>(true);
                if (colliders.Length != 0)
                {
                    Debug.LogError(
                        $"P-51 Step 80 failed. '{flight.name}' forward deck is visual-only and must have zero colliders; found {colliders.Length}.",
                        bridge);
                    passed = false;
                }
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("P-51 Step 80 failed. Standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"P-51 Step 80 passed. Aircraft checked={aircraftChecked}. The windshield-to-fuselage opening is covered by an overlapping forward deck bridge, "
                    + $"and every pilot eye is at aircraft-local {PilotEyeLocalPosition}, 22 cm above the previous cockpit viewpoint for improved over-the-nose visibility.");
            }
        }

        private static Mesh CreateOrUpdateBridgeMesh()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(BridgeMeshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "P-51D Forward Windshield Deck Bridge" };
                AssetDatabase.CreateAsset(mesh, BridgeMeshPath);
            }
            else
            {
                Undo.RegisterCompleteObjectUndo(mesh, "Rebuild P-51 forward windshield deck bridge");
                mesh.Clear();
                mesh.name = "P-51D Forward Windshield Deck Bridge";
            }

            int verticesPerStation = CrossSectionSegments + 1;
            List<Vector3> vertices = new List<Vector3>(DeckStations.Length * verticesPerStation);
            List<Vector2> uvs = new List<Vector2>(DeckStations.Length * verticesPerStation);
            List<int> triangles = new List<int>((DeckStations.Length - 1) * CrossSectionSegments * 6);

            for (int stationIndex = 0; stationIndex < DeckStations.Length; stationIndex++)
            {
                DeckStation station = DeckStations[stationIndex];
                float longitudinalT = stationIndex / (float)(DeckStations.Length - 1);
                for (int segment = 0; segment <= CrossSectionSegments; segment++)
                {
                    float crossT = segment / (float)CrossSectionSegments;
                    float xNormalized = Mathf.Lerp(-1f, 1f, crossT);
                    float radial = Mathf.Sqrt(Mathf.Max(0f, 1f - xNormalized * xNormalized));
                    float arch = Mathf.Pow(radial, ArchExponent);
                    float x = xNormalized * station.HalfWidth;
                    float y = Mathf.Lerp(station.EdgeY, station.CrownY, arch);
                    vertices.Add(new Vector3(x, y, station.Z));
                    uvs.Add(new Vector2(crossT, longitudinalT));
                }
            }

            for (int stationIndex = 0; stationIndex < DeckStations.Length - 1; stationIndex++)
            {
                int row = stationIndex * verticesPerStation;
                int nextRow = (stationIndex + 1) * verticesPerStation;
                for (int segment = 0; segment < CrossSectionSegments; segment++)
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

        private static void BuildOrUpdateForwardDeck(
            Transform aircraft,
            Mesh bridgeMesh,
            Material metal,
            Material dark)
        {
            Transform bridge = FindDirectChild(aircraft, BridgeName);
            if (bridge == null)
            {
                GameObject bridgeObject = new GameObject(BridgeName);
                Undo.RegisterCreatedObjectUndo(bridgeObject, "Create P-51 forward windshield deck bridge");
                bridge = bridgeObject.transform;
                bridge.SetParent(aircraft, false);
            }
            else
            {
                Undo.RecordObject(bridge.gameObject, "Refit P-51 forward windshield deck bridge");
                bridge.gameObject.SetActive(true);
            }

            bridge.localPosition = Vector3.zero;
            bridge.localRotation = Quaternion.identity;
            bridge.localScale = Vector3.one;

            MeshFilter filter = bridge.GetComponent<MeshFilter>();
            if (filter == null)
            {
                filter = Undo.AddComponent<MeshFilter>(bridge.gameObject);
            }
            filter.sharedMesh = bridgeMesh;

            MeshRenderer renderer = bridge.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = Undo.AddComponent<MeshRenderer>(bridge.gameObject);
            }
            renderer.sharedMaterial = metal;
            renderer.receiveShadows = true;

            Collider bridgeCollider = bridge.GetComponent<Collider>();
            if (bridgeCollider != null)
            {
                Undo.DestroyObjectImmediate(bridgeCollider);
            }

            Transform seal = FindDirectChild(bridge, RearSealName);
            if (seal == null)
            {
                GameObject sealObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sealObject.name = RearSealName;
                Undo.RegisterCreatedObjectUndo(sealObject, "Create P-51 windshield deck overlap seal");
                seal = sealObject.transform;
                seal.SetParent(bridge, false);
            }

            seal.localPosition = new Vector3(0f, 1.737f, 1.145f);
            seal.localRotation = Quaternion.identity;
            seal.localScale = new Vector3(1.02f, 0.040f, 0.105f);
            MeshRenderer sealRenderer = seal.GetComponent<MeshRenderer>();
            if (sealRenderer != null)
            {
                sealRenderer.sharedMaterial = dark;
            }
            Collider sealCollider = seal.GetComponent<Collider>();
            if (sealCollider != null)
            {
                Undo.DestroyObjectImmediate(sealCollider);
            }

            EditorUtility.SetDirty(filter);
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(bridge);
            EditorUtility.SetDirty(seal);
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
