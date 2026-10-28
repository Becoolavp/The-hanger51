using System.Collections.Generic;
using Hanger51.Aircraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51RaisedCockpitAndNoseTransitionSetup
    {
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string CockpitRootName = "P-51 Cockpit Interior";
        private const string OpeningRimRootName = "P-51 True Cockpit Opening Rim";
        private const string SmoothCanopyName = "P-51 Smooth Sealed Canopy Assembly";
        private const string SmoothGlassName = "P-51 Smooth Bubble Canopy Glass";
        private const string WindshieldBowName = "P-51 Windshield Transition Bow";
        private const string OldBridgeName = "P-51 Forward Windshield Deck Bridge";
        private const string RaisedTransitionName = "P-51 Raised Cockpit Nose Transition";
        private const string TransitionRearSealName = "P-51 Raised Windshield Rear Seal";
        private const string TransitionFrontSealName = "P-51 Raised Windshield Front Seal";
        private const string RaisedSillFairingRootName = "P-51 Raised Cockpit Sill Fairings";

        private const string TransitionMeshPath =
            "Assets/_Project/Aircraft/P51/Meshes/P51D_RaisedCockpitNoseTransition.asset";
        private const string MetalMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/PolishedAluminum.mat";
        private const string DarkMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/DarkAircraftMetal.mat";

        // The cockpit tub and controls rise together. The canopy rises slightly farther so the
        // windshield frame moves above the primary sightline instead of remaining fixed relative
        // to the pilot eye.
        private const float CockpitLift = 0.22f;
        private const float CanopyLift = 0.28f;
        private static readonly Vector3 PilotEyeLocalPosition = new Vector3(0f, 2.38f, -0.56f);
        private static readonly Vector3 WindshieldBowOffset = new Vector3(0f, 0.08f, 0.18f);

        private const int CrossSectionSegments = 20;
        private const float ArchExponent = 0.78f;

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

        // A shallow fairing that begins underneath the raised windshield and gently joins the
        // intact upper fuselage/cowling. It deliberately avoids the tall Step 79 hump.
        private static readonly DeckStation[] DeckStations =
        {
            new DeckStation(1.08f, 0.475f, 1.970f, 2.015f),
            new DeckStation(1.18f, 0.490f, 1.982f, 2.035f),
            new DeckStation(1.28f, 0.535f, 1.995f, 2.070f),
            new DeckStation(1.38f, 0.585f, 2.005f, 2.105f),
            new DeckStation(1.48f, 0.605f, 2.012f, 2.125f),
            new DeckStation(1.60f, 0.590f, 2.015f, 2.135f),
            new DeckStation(1.72f, 0.565f, 2.010f, 2.130f)
        };

        [MenuItem("Hanger 51/P-51 Mustang/81 - Raise Full Cockpit and Refit Nose Transition")]
        public static void RaiseFullCockpitAndRefitNose()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 Step 81 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("P-51 Step 81 failed. Open the saved Hanger 51 gameplay scene first.");
                return;
            }

            Material metal = AssetDatabase.LoadAssetAtPath<Material>(MetalMaterialPath);
            Material dark = AssetDatabase.LoadAssetAtPath<Material>(DarkMaterialPath);
            if (metal == null || dark == null)
            {
                Debug.LogError("P-51 Step 81 failed. Required P-51 materials are missing.");
                return;
            }

            Mesh transitionMesh = CreateOrUpdateTransitionMesh();
            if (transitionMesh == null)
            {
                Debug.LogError("P-51 Step 81 failed. The raised-cockpit nose transition mesh could not be created.");
                return;
            }

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft == null || aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 81 failed. No P-51 aircraft were found in the scene.");
                return;
            }

            int aircraftUpdated = 0;
            int oldBridgesDisabled = 0;
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

                Transform cockpit = FindDescendant(flight.transform, CockpitRootName);
                Transform openingRim = FindDescendant(cockpit, OpeningRimRootName);
                Transform canopy = FindDescendant(flight.transform, SmoothCanopyName);
                Transform glass = FindDescendant(canopy, SmoothGlassName);
                Transform windshieldBow = FindDescendant(canopy, WindshieldBowName);
                P51PilotSeat[] seats = flight.GetComponentsInChildren<P51PilotSeat>(true);
                P51PilotSeat seat = seats.Length > 0 ? seats[0] : null;

                if (cockpit == null || openingRim == null || canopy == null || glass == null
                    || windshieldBow == null || seat == null || seat.CameraAnchor == null)
                {
                    Debug.LogWarning(
                        $"P-51 Step 81 skipped '{flight.name}' because its hollow cockpit, smooth canopy, windshield bow, or pilot camera is missing.",
                        flight);
                    continue;
                }

                // Use canonical offsets instead of += so Step 81 is safe to rerun.
                Undo.RecordObject(cockpit, "Raise complete P-51 cockpit interior");
                cockpit.localPosition = new Vector3(0f, CockpitLift, 0f);
                cockpit.localRotation = Quaternion.identity;
                cockpit.localScale = Vector3.one;

                // The trim ring represents the actual cut edge in the fuselage mesh. Counteract
                // the cockpit-root lift so this ring remains physically attached to the body.
                Undo.RecordObject(openingRim, "Keep P-51 fuselage opening rim on body");
                openingRim.localPosition = new Vector3(0f, -CockpitLift, 0f);
                openingRim.localRotation = Quaternion.identity;
                openingRim.localScale = Vector3.one;

                Undo.RecordObject(canopy, "Raise complete P-51 canopy assembly");
                canopy.localPosition = new Vector3(0f, CanopyLift, 0f);
                canopy.localRotation = Quaternion.identity;
                canopy.localScale = Vector3.one;

                Undo.RecordObject(windshieldBow, "Improve P-51 windshield bow sightline");
                windshieldBow.localPosition = WindshieldBowOffset;

                Undo.RecordObject(seat.CameraAnchor, "Raise P-51 pilot eye with cockpit");
                seat.CameraAnchor.position = flight.transform.TransformPoint(PilotEyeLocalPosition);
                seat.CameraAnchor.rotation = flight.transform.rotation;

                Transform oldBridge = FindDescendant(flight.transform, OldBridgeName);
                if (oldBridge != null && oldBridge.gameObject.activeSelf)
                {
                    Undo.RecordObject(oldBridge.gameObject, "Disable obsolete P-51 forward deck hump");
                    oldBridge.gameObject.SetActive(false);
                    EditorUtility.SetDirty(oldBridge.gameObject);
                    oldBridgesDisabled++;
                }

                BuildOrUpdateRaisedTransition(flight.transform, transitionMesh, metal, dark);
                BuildOrUpdateRaisedSillFairings(flight.transform, metal, dark);

                EditorUtility.SetDirty(cockpit);
                EditorUtility.SetDirty(openingRim);
                EditorUtility.SetDirty(canopy);
                EditorUtility.SetDirty(windshieldBow);
                EditorUtility.SetDirty(seat.CameraAnchor);
                EditorUtility.SetDirty(flight);
                aircraftUpdated++;
            }

            EditorUtility.SetDirty(transitionMesh);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 81 made the cockpit-height changes but Unity could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 Step 81 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = master != null ? master.gameObject : aircraft[0].gameObject;
            Debug.Log(
                $"P-51 Step 81 complete. Raised {aircraftUpdated} complete cockpit interior package(s) by {CockpitLift:F2} m, "
                + $"raised the canopy assemblies by {CanopyLift:F2} m, moved the pilot eyes to local Y={PilotEyeLocalPosition.y:F2} m, "
                + $"disabled {oldBridgesDisabled} obsolete Step 79 nose-hump bridge(s), kept the true fuselage cut rim fixed to the aircraft body, "
                + "added raised sill fairings between the body and canopy, moved the windshield transition bow up/forward, and fitted a shallow nose-to-windshield transition.",
                master != null ? master.gameObject : null);
        }

        [MenuItem("Hanger 51/P-51 Mustang/82 - Validate Raised Cockpit, Visibility and Cowling Clearance")]
        public static void ValidateRaisedCockpit()
        {
            bool passed = true;
            int aircraftChecked = 0;

            Mesh transitionMesh = AssetDatabase.LoadAssetAtPath<Mesh>(TransitionMeshPath);
            if (transitionMesh == null)
            {
                Debug.LogError("P-51 Step 82 failed. Raised cockpit nose-transition mesh asset is missing.");
                return;
            }

            Bounds transitionBounds = transitionMesh.bounds;
            if (transitionBounds.size.x < 1.05f
                || transitionBounds.size.z < 0.55f
                || transitionBounds.min.z > 1.10f
                || transitionBounds.max.z < 1.68f
                || transitionBounds.max.y > 2.18f
                || transitionBounds.min.y < 1.94f)
            {
                Debug.LogError(
                    $"P-51 Step 82 failed. Raised cockpit nose transition has unexpected bounds {transitionBounds}.",
                    transitionMesh);
                passed = false;
            }

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft == null || aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 82 failed. No P-51 aircraft were found.");
                return;
            }

            for (int i = 0; i < aircraft.Length; i++)
            {
                P51FlightController flight = aircraft[i];
                if (flight == null || !flight.gameObject.scene.IsValid())
                {
                    continue;
                }

                Transform cockpit = FindDescendant(flight.transform, CockpitRootName);
                Transform openingRim = FindDescendant(cockpit, OpeningRimRootName);
                Transform canopy = FindDescendant(flight.transform, SmoothCanopyName);
                Transform glass = FindDescendant(canopy, SmoothGlassName);
                Transform windshieldBow = FindDescendant(canopy, WindshieldBowName);
                Transform transition = FindDescendant(flight.transform, RaisedTransitionName);
                Transform rearSeal = FindDescendant(transition, TransitionRearSealName);
                Transform frontSeal = FindDescendant(transition, TransitionFrontSealName);
                Transform sillFairings = FindDescendant(flight.transform, RaisedSillFairingRootName);
                Transform leftMidFairing = FindDescendant(sillFairings, "Left Raised Sill Fairing Mid");
                Transform rightMidFairing = FindDescendant(sillFairings, "Right Raised Sill Fairing Mid");
                Transform oldBridge = FindDescendant(flight.transform, OldBridgeName);
                P51PilotSeat[] seats = flight.GetComponentsInChildren<P51PilotSeat>(true);
                P51PilotSeat seat = seats.Length > 0 ? seats[0] : null;

                if (cockpit == null || openingRim == null || canopy == null || glass == null
                    || windshieldBow == null || transition == null || rearSeal == null || frontSeal == null
                    || sillFairings == null || leftMidFairing == null || rightMidFairing == null
                    || seat == null || seat.CameraAnchor == null)
                {
                    Debug.LogError(
                        $"P-51 Step 82 failed. '{flight.name}' is missing raised-cockpit, canopy, sill-fairing, transition-deck, or pilot-eye parts.",
                        flight);
                    passed = false;
                    continue;
                }

                aircraftChecked++;

                if (Mathf.Abs(cockpit.localPosition.y - CockpitLift) > 0.01f
                    || Mathf.Abs(canopy.localPosition.y - CanopyLift) > 0.01f
                    || Mathf.Abs(openingRim.localPosition.y + CockpitLift) > 0.01f)
                {
                    Debug.LogError(
                        $"P-51 Step 82 failed. '{flight.name}' cockpit/canopy/rim lift is wrong. "
                        + $"CockpitY={cockpit.localPosition.y:F3}, CanopyY={canopy.localPosition.y:F3}, RimLocalY={openingRim.localPosition.y:F3}.",
                        flight);
                    passed = false;
                }

                Vector3 eye = flight.transform.InverseTransformPoint(seat.CameraAnchor.position);
                if ((eye - PilotEyeLocalPosition).sqrMagnitude > 0.0009f)
                {
                    Debug.LogError(
                        $"P-51 Step 82 failed. '{flight.name}' pilot eye is at {eye}, expected {PilotEyeLocalPosition}.",
                        seat.CameraAnchor);
                    passed = false;
                }

                if ((windshieldBow.localPosition - WindshieldBowOffset).sqrMagnitude > 0.0009f)
                {
                    Debug.LogError(
                        $"P-51 Step 82 failed. '{flight.name}' windshield transition bow was not moved out of the primary sightline.",
                        windshieldBow);
                    passed = false;
                }

                if (oldBridge != null && oldBridge.gameObject.activeSelf)
                {
                    Debug.LogError(
                        $"P-51 Step 82 failed. '{flight.name}' still has the obsolete tall Step 79 forward-deck bridge active.",
                        oldBridge);
                    passed = false;
                }

                MeshFilter filter = transition.GetComponent<MeshFilter>();
                MeshRenderer renderer = transition.GetComponent<MeshRenderer>();
                if (filter == null || filter.sharedMesh != transitionMesh || renderer == null || !renderer.enabled)
                {
                    Debug.LogError(
                        $"P-51 Step 82 failed. '{flight.name}' raised nose transition is not rendering the shared transition mesh.",
                        transition);
                    passed = false;
                }

                Collider[] transitionColliders = transition.GetComponentsInChildren<Collider>(true);
                Collider[] fairingColliders = sillFairings.GetComponentsInChildren<Collider>(true);
                if (transitionColliders.Length != 0 || fairingColliders.Length != 0)
                {
                    Debug.LogError(
                        $"P-51 Step 82 failed. '{flight.name}' raised cockpit exterior trim is visual-only. "
                        + $"Transition colliders={transitionColliders.Length}, sill-fairing colliders={fairingColliders.Length}.",
                        flight);
                    passed = false;
                }
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("P-51 Step 82 failed. Standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"P-51 Step 82 passed. Aircraft checked={aircraftChecked}. Complete cockpit interiors are raised {CockpitLift:F2} m, "
                    + $"canopies are raised {CanopyLift:F2} m, pilot eyes are at local {PilotEyeLocalPosition}, the true fuselage rim remains attached to the body, "
                    + "raised sill fairings close the vertical side gap, the windshield bow is shifted up/forward, the obsolete tall nose bridge is disabled, "
                    + "and the new shallow nose transition closes the cowling-to-windshield area without adding physics colliders.");
            }
        }

        private static Mesh CreateOrUpdateTransitionMesh()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(TransitionMeshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "P-51D Raised Cockpit Nose Transition" };
                AssetDatabase.CreateAsset(mesh, TransitionMeshPath);
            }
            else
            {
                Undo.RegisterCompleteObjectUndo(mesh, "Rebuild P-51 raised cockpit nose transition");
                mesh.Clear();
                mesh.name = "P-51D Raised Cockpit Nose Transition";
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
                    float normalizedX = Mathf.Lerp(-1f, 1f, crossT);
                    float radial = Mathf.Sqrt(Mathf.Max(0f, 1f - normalizedX * normalizedX));
                    float arch = Mathf.Pow(radial, ArchExponent);
                    float x = normalizedX * station.HalfWidth;
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

        private static void BuildOrUpdateRaisedTransition(
            Transform aircraft,
            Mesh transitionMesh,
            Material metal,
            Material dark)
        {
            Transform root = FindDirectChild(aircraft, RaisedTransitionName);
            if (root == null)
            {
                GameObject rootObject = new GameObject(RaisedTransitionName);
                Undo.RegisterCreatedObjectUndo(rootObject, "Create P-51 raised cockpit nose transition");
                root = rootObject.transform;
                root.SetParent(aircraft, false);
            }
            else
            {
                Undo.RecordObject(root.gameObject, "Refit P-51 raised cockpit nose transition");
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
            filter.sharedMesh = transitionMesh;

            MeshRenderer renderer = root.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = Undo.AddComponent<MeshRenderer>(root.gameObject);
            }
            renderer.sharedMaterial = metal;
            renderer.receiveShadows = true;

            RemoveColliders(root.gameObject);

            Transform rearSeal = CreateOrUpdateVisualCube(
                root,
                TransitionRearSealName,
                new Vector3(0f, 1.985f, 1.125f),
                new Vector3(1.02f, 0.035f, 0.115f),
                dark);

            Transform frontSeal = CreateOrUpdateVisualCube(
                root,
                TransitionFrontSealName,
                new Vector3(0f, 2.045f, 1.665f),
                new Vector3(1.14f, 0.040f, 0.145f),
                metal);

            EditorUtility.SetDirty(filter);
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(rearSeal);
            EditorUtility.SetDirty(frontSeal);
        }

        private static void BuildOrUpdateRaisedSillFairings(
            Transform aircraft,
            Material metal,
            Material dark)
        {
            Transform root = FindDirectChild(aircraft, RaisedSillFairingRootName);
            if (root == null)
            {
                GameObject rootObject = new GameObject(RaisedSillFairingRootName);
                Undo.RegisterCreatedObjectUndo(rootObject, "Create P-51 raised cockpit sill fairings");
                root = rootObject.transform;
                root.SetParent(aircraft, false);
            }
            else
            {
                Undo.RecordObject(root.gameObject, "Refit P-51 raised cockpit sill fairings");
                root.gameObject.SetActive(true);
            }

            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;

            BuildSillSide(root, true, metal, dark);
            BuildSillSide(root, false, metal, dark);
            RemoveColliders(root.gameObject);
            EditorUtility.SetDirty(root);
        }

        private static void BuildSillSide(Transform root, bool left, Material metal, Material dark)
        {
            float x = left ? -0.555f : 0.555f;
            string side = left ? "Left" : "Right";

            CreateOrUpdateVisualCube(
                root,
                $"{side} Raised Sill Fairing Rear",
                new Vector3(x, 1.835f, -0.72f),
                new Vector3(0.085f, 0.275f, 0.90f),
                metal);
            CreateOrUpdateVisualCube(
                root,
                $"{side} Raised Sill Fairing Mid",
                new Vector3(x, 1.845f, 0.23f),
                new Vector3(0.090f, 0.295f, 1.05f),
                metal);
            CreateOrUpdateVisualCube(
                root,
                $"{side} Raised Sill Fairing Front",
                new Vector3(x, 1.875f, 0.98f),
                new Vector3(0.085f, 0.245f, 0.52f),
                metal);

            // A dark inner strip keeps the raised sill from reading like a single solid block
            // when viewed from the cockpit.
            float innerX = left ? -0.505f : 0.505f;
            CreateOrUpdateVisualCube(
                root,
                $"{side} Raised Sill Inner Trim",
                new Vector3(innerX, 1.850f, 0.02f),
                new Vector3(0.035f, 0.245f, 2.26f),
                dark);
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
            }

            EditorUtility.SetDirty(part.transform);
            if (renderer != null)
            {
                EditorUtility.SetDirty(renderer);
            }
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

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == descendantName)
                {
                    return all[i];
                }
            }
            return null;
        }
    }
}
