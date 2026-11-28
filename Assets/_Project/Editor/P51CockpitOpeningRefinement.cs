using Hanger51.Aircraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51CockpitOpeningRefinement
    {
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string CockpitRootName = "P-51 Cockpit Interior";
        private const string InstrumentPanelName = "P-51 Cockpit Instrument Panel";

        private const string MetalMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/PolishedAluminum.mat";
        private const string DarkMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/DarkAircraftMetal.mat";
        private const string ServiceMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/ServiceHardware.mat";

        [MenuItem("Hanger 51/P-51 Mustang/71 - Refine Cockpit Opening and Lower Interior Walls")]
        public static void RefineCockpitOpeningAndWalls()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 Step 71 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("P-51 Step 71 failed. Open the saved Hanger 51 gameplay scene first.");
                return;
            }

            Material metal = AssetDatabase.LoadAssetAtPath<Material>(MetalMaterialPath);
            Material dark = AssetDatabase.LoadAssetAtPath<Material>(DarkMaterialPath);
            Material service = AssetDatabase.LoadAssetAtPath<Material>(ServiceMaterialPath);

            if (metal == null || dark == null || service == null)
            {
                Debug.LogError("P-51 Step 71 failed. Required cockpit materials are missing.");
                return;
            }

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            if (aircraft == null || aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 71 failed. No P-51 aircraft were found.");
                return;
            }

            int refinedCount = 0;
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

                P51PilotSeat[] seats = flight.GetComponentsInChildren<P51PilotSeat>(true);
                P51PilotSeat seat = seats.Length > 0 ? seats[0] : null;
                if (seat == null || seat.CameraAnchor == null)
                {
                    Debug.LogWarning(
                        $"P-51 Step 71 skipped '{flight.name}' because its pilot seat or camera anchor is missing.",
                        flight);
                    continue;
                }

                Transform cockpit = FindDescendant(flight.transform, CockpitRootName);
                if (cockpit == null)
                {
                    Debug.LogWarning(
                        $"P-51 Step 71 skipped '{flight.name}' because Step 69 cockpit shell was not found.",
                        flight);
                    continue;
                }

                Transform panel = FindDescendant(cockpit, InstrumentPanelName);
                if (panel == null)
                {
                    Debug.LogWarning(
                        $"P-51 Step 71 skipped '{flight.name}' because the cockpit instrument panel was not found.",
                        flight);
                    continue;
                }

                RefineCockpitGeometry(
                    flight.transform,
                    cockpit,
                    panel,
                    seat.CameraAnchor,
                    metal,
                    dark,
                    service);
                RefineFuelGauge(panel);
                refinedCount++;

                EditorUtility.SetDirty(flight);
                EditorUtility.SetDirty(cockpit);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 71 made the cockpit refinements but Unity could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 Step 71 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = master != null ? master.gameObject : aircraft[0].gameObject;

            Debug.Log(
                $"P-51 Step 71 complete. Refined cockpit opening/interior on {refinedCount} aircraft. "
                + "The cockpit sidewalls and canopy sills were lowered, the panel was repositioned, "
                + "and new interior fairing/coaming pieces were added to keep the outer fuselage skin from dominating the pilot view.",
                master != null ? master.gameObject : null);
        }

        [MenuItem("Hanger 51/P-51 Mustang/72 - Validate Cockpit Opening Refinement")]
        public static void ValidateCockpitOpeningRefinement()
        {
            bool passed = true;
            int aircraftChecked = 0;

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            if (aircraft == null || aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 72 failed. No P-51 aircraft were found.");
                return;
            }

            for (int i = 0; i < aircraft.Length; i++)
            {
                P51FlightController flight = aircraft[i];
                if (flight == null || !flight.gameObject.scene.IsValid())
                {
                    continue;
                }

                aircraftChecked++;

                Transform cockpit = FindDescendant(flight.transform, CockpitRootName);
                Transform panel = FindDescendant(cockpit, InstrumentPanelName);

                Transform leftWall = FindDescendant(cockpit, "Cockpit Left Sidewall");
                Transform rightWall = FindDescendant(cockpit, "Cockpit Right Sidewall");
                Transform leftSill = FindDescendant(cockpit, "Left Canopy Sill");
                Transform rightSill = FindDescendant(cockpit, "Right Canopy Sill");
                Transform forwardCoaming = FindDescendant(cockpit, "Cockpit Forward Coaming");
                Transform lowerDashFairing = FindDescendant(cockpit, "Cockpit Lower Dash Fairing");
                Transform upperNoseCover = FindDescendant(cockpit, "Cockpit Upper Nose Cover");
                Transform leftInnerSkin = FindDescendant(cockpit, "Cockpit Left Inner Skin");
                Transform rightInnerSkin = FindDescendant(cockpit, "Cockpit Right Inner Skin");

                if (cockpit == null
                    || panel == null
                    || leftWall == null
                    || rightWall == null
                    || leftSill == null
                    || rightSill == null
                    || forwardCoaming == null
                    || lowerDashFairing == null
                    || upperNoseCover == null
                    || leftInnerSkin == null
                    || rightInnerSkin == null)
                {
                    Debug.LogError(
                        $"P-51 Step 72 failed. '{flight.name}' is missing one or more refined cockpit-opening parts.",
                        flight);
                    passed = false;
                    continue;
                }

                P51FuelQuantityGauge gauge = panel.GetComponentInChildren<P51FuelQuantityGauge>(true);
                if (gauge == null)
                {
                    Debug.LogError(
                        $"P-51 Step 72 failed. '{flight.name}' does not have a panel-mounted fuel gauge after refinement.",
                        flight);
                    passed = false;
                }
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("P-51 Step 72 failed. Standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"P-51 Step 72 passed. Aircraft checked={aircraftChecked}. "
                    + "Refined cockpit geometry exists, lowered walls/sills are present, forward interior fairings were created, "
                    + "and the fuel gauge remains mounted in the panel.");
            }
        }

        private static void RefineCockpitGeometry(
            Transform aircraft,
            Transform cockpit,
            Transform panel,
            Transform cameraAnchor,
            Material metal,
            Material dark,
            Material service)
        {
            Vector3 head = aircraft.InverseTransformPoint(cameraAnchor.position);

            // Keep the cockpit walls inside the external fuselage/canopy opening rather than
            // extending upward as visible slabs beside the pilot.
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Cockpit Left Sidewall",
                new Vector3(-0.38f, head.y - 0.50f, head.z + 0.01f),
                new Vector3(0.040f, 0.36f, 1.18f), Vector3.zero, dark);

            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Cockpit Right Sidewall",
                new Vector3(0.38f, head.y - 0.50f, head.z + 0.01f),
                new Vector3(0.040f, 0.36f, 1.18f), Vector3.zero, dark);

            // Lower the canopy sills to track the aircraft skin line instead of rising above it.
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Left Canopy Sill",
                new Vector3(-0.38f, head.y - 0.15f, head.z + 0.00f),
                new Vector3(0.065f, 0.035f, 1.18f), Vector3.zero, metal);

            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Right Canopy Sill",
                new Vector3(0.38f, head.y - 0.15f, head.z + 0.00f),
                new Vector3(0.065f, 0.035f, 1.18f), Vector3.zero, metal);

            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Cockpit Floor",
                new Vector3(0f, head.y - 0.70f, head.z + 0.03f),
                new Vector3(0.78f, 0.050f, 1.18f), Vector3.zero, dark);

            // Pull the panel aft/down into the actual cockpit opening so the aircraft nose skin
            // does not sit between the camera and the instrument face.
            panel.localPosition = new Vector3(0f, head.y - 0.31f, head.z + 0.58f);
            panel.localRotation = Quaternion.Euler(-10f, 0f, 0f);
            panel.localScale = new Vector3(0.76f, 0.38f, 0.055f);
            RemoveLocalColliders(panel.gameObject);
            Renderer panelRenderer = panel.GetComponent<Renderer>();
            if (panelRenderer != null)
            {
                panelRenderer.sharedMaterial = dark;
                EditorUtility.SetDirty(panelRenderer);
            }
            EditorUtility.SetDirty(panel);

            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Instrument Panel Glare Shield",
                new Vector3(0f, head.y - 0.13f, head.z + 0.50f),
                new Vector3(0.80f, 0.050f, 0.18f), new Vector3(-4f, 0f, 0f), dark);

            // Interior coaming/fairings give the open cockpit a proper inner surface instead
            // of exposing the exterior fuselage volume to the pilot camera.
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Cockpit Forward Coaming",
                new Vector3(0f, head.y - 0.24f, head.z + 0.40f),
                new Vector3(0.74f, 0.080f, 0.30f), new Vector3(-8f, 0f, 0f), dark);

            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Cockpit Lower Dash Fairing",
                new Vector3(0f, head.y - 0.47f, head.z + 0.35f),
                new Vector3(0.72f, 0.24f, 0.34f), new Vector3(24f, 0f, 0f), dark);

            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Cockpit Upper Nose Cover",
                new Vector3(0f, head.y - 0.33f, head.z + 0.48f),
                new Vector3(0.70f, 0.090f, 0.22f), new Vector3(-18f, 0f, 0f), dark);

            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Cockpit Left Inner Skin",
                new Vector3(-0.33f, head.y - 0.34f, head.z + 0.02f),
                new Vector3(0.030f, 0.42f, 1.00f), Vector3.zero, dark);

            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Cockpit Right Inner Skin",
                new Vector3(0.33f, head.y - 0.34f, head.z + 0.02f),
                new Vector3(0.030f, 0.42f, 1.00f), Vector3.zero, dark);

            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Cockpit Panel Support",
                new Vector3(0f, head.y - 0.41f, head.z + 0.47f),
                new Vector3(0.34f, 0.10f, 0.18f), new Vector3(10f, 0f, 0f), service);

            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Pilot Seat Back",
                new Vector3(0f, head.y - 0.39f, head.z - 0.42f),
                new Vector3(0.38f, 0.46f, 0.075f), new Vector3(-10f, 0f, 0f), service);
        }

        private static void RefineFuelGauge(Transform panel)
        {
            if (panel == null)
            {
                return;
            }

            P51FuelQuantityGauge gauge = panel.GetComponentInChildren<P51FuelQuantityGauge>(true);
            if (gauge == null)
            {
                return;
            }

            Undo.RecordObject(gauge.transform, "Refine cockpit fuel gauge placement");
            gauge.transform.localPosition = new Vector3(0.20f, 0.015f, -0.072f);
            gauge.transform.localRotation = Quaternion.identity;
            gauge.transform.localScale = Vector3.one * 0.72f;
            gauge.RefreshGauge();
            EditorUtility.SetDirty(gauge.transform);
            EditorUtility.SetDirty(gauge);
        }

        private static Transform CreateOrUpdatePrimitive(
            Transform parent,
            PrimitiveType primitiveType,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEuler,
            Material material)
        {
            Transform existing = FindDirectChild(parent, objectName);
            GameObject part;

            if (existing == null)
            {
                part = GameObject.CreatePrimitive(primitiveType);
                part.name = objectName;
                Undo.RegisterCreatedObjectUndo(part, $"Create {objectName}");
                part.transform.SetParent(parent, false);
            }
            else
            {
                part = existing.gameObject;
                Undo.RecordObject(part.transform, $"Update {objectName}");
            }

            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(localEuler);
            part.transform.localScale = localScale;
            RemoveLocalColliders(part);

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
                EditorUtility.SetDirty(renderer);
            }

            EditorUtility.SetDirty(part.transform);
            return part.transform;
        }

        private static void RemoveLocalColliders(GameObject part)
        {
            if (part == null)
            {
                return;
            }

            Collider[] colliders = part.GetComponents<Collider>();
            for (int i = colliders.Length - 1; i >= 0; i--)
            {
                if (colliders[i] != null)
                {
                    Object.DestroyImmediate(colliders[i]);
                }
            }
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child != null && child.name == name)
                {
                    return child;
                }
            }

            return null;
        }

        private static Transform FindDescendant(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            Transform[] all = parent.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name)
                {
                    return all[i];
                }
            }

            return null;
        }
    }
}
