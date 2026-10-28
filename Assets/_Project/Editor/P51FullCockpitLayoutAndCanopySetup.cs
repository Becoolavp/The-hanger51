using System.Collections.Generic;
using Hanger51.Aircraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51FullCockpitLayoutAndCanopySetup
    {
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string CockpitRootName = "P-51 Cockpit Interior";
        private const string InstrumentPanelName = "P-51 Cockpit Instrument Panel";
        private const string OpeningRimRootName = "P-51 True Cockpit Opening Rim";
        private const string CanopyAssemblyName = "P-51 Corrected Full-Length Canopy Assembly";
        private const string CanopyGlassName = "P-51 Corrected Canopy Glass";

        private const string CanopyMeshPath =
            "Assets/_Project/Aircraft/P51/Meshes/P51D_CorrectedCanopy.asset";
        private const string MetalMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/PolishedAluminum.mat";
        private const string DarkMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/DarkAircraftMetal.mat";
        private const string InteriorMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/CockpitInterior.mat";
        private const string ServiceMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/ServiceHardware.mat";
        private const string GlassMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/CanopyGlass.mat";

        // The Step 73 fuselage opening runs from approximately -1.20 to +1.35 aircraft-local Z.
        // Step 75 deliberately uses most of that cavity instead of clustering everything around
        // the camera anchor as the first cockpit-shell pass did.
        private static readonly Vector3 CameraLocalPosition = new Vector3(0f, 1.94f, -0.56f);
        private static readonly Vector3 PanelLocalPosition = new Vector3(0f, 1.47f, 0.52f);
        private static readonly Vector3 SeatBackLocalPosition = new Vector3(0f, 1.43f, -0.84f);
        private static readonly Vector3 StickLocalPosition = new Vector3(0f, 1.22f, -0.05f);

        private readonly struct CanopyStation
        {
            internal readonly float Z;
            internal readonly float HalfWidth;
            internal readonly float SillY;
            internal readonly float CrownY;

            internal CanopyStation(float z, float halfWidth, float sillY, float crownY)
            {
                Z = z;
                HalfWidth = halfWidth;
                SillY = sillY;
                CrownY = crownY;
            }
        }

        private static readonly CanopyStation[] CanopyStations =
        {
            new CanopyStation(-1.14f, 0.51f, 1.73f, 2.06f),
            new CanopyStation(-0.94f, 0.57f, 1.71f, 2.29f),
            new CanopyStation(-0.64f, 0.61f, 1.69f, 2.44f),
            new CanopyStation(-0.24f, 0.63f, 1.68f, 2.50f),
            new CanopyStation(0.18f, 0.61f, 1.69f, 2.45f),
            new CanopyStation(0.55f, 0.56f, 1.71f, 2.28f),
            new CanopyStation(0.83f, 0.53f, 1.73f, 2.10f),
            new CanopyStation(1.16f, 0.50f, 1.75f, 1.93f)
        };

        private static readonly string[] LegacyCanopyNames =
        {
            "Bubble Canopy Glass",
            "Canopy Left Rail",
            "Canopy Right Rail",
            "Canopy Front Bow",
            "Canopy Rear Bow",
            "Canopy Center Frame"
        };

        [MenuItem("Hanger 51/P-51 Mustang/75 - Use Full Cockpit Space and Rebuild Canopy")]
        public static void UseFullCockpitAndRebuildCanopy()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 Step 75 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("P-51 Step 75 failed. Open the saved Hanger 51 gameplay scene first.");
                return;
            }

            Material metal = AssetDatabase.LoadAssetAtPath<Material>(MetalMaterialPath);
            Material dark = AssetDatabase.LoadAssetAtPath<Material>(DarkMaterialPath);
            Material interior = AssetDatabase.LoadAssetAtPath<Material>(InteriorMaterialPath);
            Material service = AssetDatabase.LoadAssetAtPath<Material>(ServiceMaterialPath);
            Material glass = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
            if (metal == null || dark == null || interior == null || service == null || glass == null)
            {
                Debug.LogError("P-51 Step 75 failed. One or more required P-51 cockpit/canopy materials are missing.");
                return;
            }

            Mesh canopyMesh = CreateOrUpdateCanopyMesh();
            if (canopyMesh == null)
            {
                Debug.LogError("P-51 Step 75 failed. The corrected canopy mesh could not be created.");
                return;
            }

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft == null || aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 75 failed. No P-51 aircraft were found in the scene.");
                return;
            }

            int aircraftUpdated = 0;
            int legacyVisualsDisabled = 0;
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
                Transform openingRim = FindDescendant(flight.transform, OpeningRimRootName);
                Transform panel = FindDescendant(cockpit, InstrumentPanelName);
                P51PilotSeat[] seats = flight.GetComponentsInChildren<P51PilotSeat>(true);
                P51PilotSeat seat = seats.Length > 0 ? seats[0] : null;

                if (cockpit == null || openingRim == null || panel == null || seat == null || seat.CameraAnchor == null)
                {
                    Debug.LogWarning(
                        $"P-51 Step 75 skipped '{flight.name}' because the true Step 73 opening, cockpit hierarchy, panel, or pilot camera is missing.",
                        flight);
                    continue;
                }

                legacyVisualsDisabled += DisableLegacyCockpitAndCanopyVisuals(flight.transform, cockpit);
                ReLayoutCockpit(flight.transform, cockpit, panel, seat.CameraAnchor, metal, dark, interior, service);
                RebuildCanopyAssembly(flight.transform, canopyMesh, glass, dark, metal);

                aircraftUpdated++;
                EditorUtility.SetDirty(flight);
                EditorUtility.SetDirty(cockpit);
                EditorUtility.SetDirty(seat.CameraAnchor);
            }

            EditorUtility.SetDirty(canopyMesh);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 75 made the cockpit/canopy changes but Unity could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 Step 75 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = master != null ? master.gameObject : aircraft[0].gameObject;
            Debug.Log(
                $"P-51 Step 75 complete. Re-laid out {aircraftUpdated} cockpit(s), disabled {legacyVisualsDisabled} obsolete cockpit/canopy visual(s), "
                + "moved the pilot eye aft, moved the instrument panel forward into the newly opened fuselage space, spread the seat/controls/pedals through the cavity, "
                + "and replaced the old aft-offset sphere canopy with a full-length frameless-bubble/windscreen canopy following the actual cockpit opening.",
                master != null ? master.gameObject : null);
        }

        [MenuItem("Hanger 51/P-51 Mustang/76 - Validate Full Cockpit Layout and Corrected Canopy")]
        public static void ValidateFullCockpitAndCanopy()
        {
            bool passed = true;
            int checkedAircraft = 0;
            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            if (aircraft == null || aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 76 failed. No P-51 aircraft were found.");
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
                Transform openingRim = FindDescendant(flight.transform, OpeningRimRootName);
                Transform panel = FindDescendant(cockpit, InstrumentPanelName);
                Transform seatBack = FindDescendant(cockpit, "Pilot Seat Back");
                Transform stick = FindDescendant(cockpit, "Control Stick Shaft");
                Transform floor = FindDescendant(cockpit, "Cockpit Floor");
                Transform leftConsole = FindDescendant(cockpit, "Left Cockpit Console");
                Transform rightConsole = FindDescendant(cockpit, "Right Cockpit Console");
                Transform canopy = FindDescendant(flight.transform, CanopyAssemblyName);
                Transform canopyGlass = FindDescendant(canopy, CanopyGlassName);
                P51PilotSeat[] seats = flight.GetComponentsInChildren<P51PilotSeat>(true);
                P51PilotSeat seat = seats.Length > 0 ? seats[0] : null;

                if (cockpit == null || openingRim == null || panel == null || seatBack == null || stick == null
                    || floor == null || leftConsole == null || rightConsole == null
                    || canopy == null || canopyGlass == null || seat == null || seat.CameraAnchor == null)
                {
                    Debug.LogError($"P-51 Step 76 failed. '{flight.name}' is missing one or more full-cockpit/canopy parts.", flight);
                    passed = false;
                    continue;
                }

                checkedAircraft++;

                Vector3 panelPosition = panel.localPosition;
                Vector3 seatPosition = seatBack.localPosition;
                Vector3 stickPosition = stick.localPosition;
                Vector3 cameraPosition = flight.transform.InverseTransformPoint(seat.CameraAnchor.position);
                float panelCameraSeparation = panelPosition.z - cameraPosition.z;
                float seatPanelSeparation = panelPosition.z - seatPosition.z;

                if (panelPosition.z < 0.40f
                    || seatPosition.z > -0.72f
                    || stickPosition.z < -0.28f
                    || stickPosition.z > 0.22f
                    || cameraPosition.z < -0.78f
                    || cameraPosition.z > -0.38f
                    || panelCameraSeparation < 0.92f
                    || seatPanelSeparation < 1.20f)
                {
                    Debug.LogError(
                        $"P-51 Step 76 failed. '{flight.name}' cockpit is still clustered instead of using the cavity. "
                        + $"SeatZ={seatPosition.z:F2}, EyeZ={cameraPosition.z:F2}, StickZ={stickPosition.z:F2}, PanelZ={panelPosition.z:F2}, "
                        + $"eye-to-panel={panelCameraSeparation:F2} m, seat-to-panel={seatPanelSeparation:F2} m.",
                        flight);
                    passed = false;
                }

                MeshFilter canopyFilter = canopyGlass.GetComponent<MeshFilter>();
                MeshRenderer canopyRenderer = canopyGlass.GetComponent<MeshRenderer>();
                if (canopyFilter == null || canopyFilter.sharedMesh == null || canopyRenderer == null || !canopyRenderer.enabled)
                {
                    Debug.LogError($"P-51 Step 76 failed. '{flight.name}' corrected canopy glass is not rendering a mesh.", flight);
                    passed = false;
                }
                else
                {
                    Bounds bounds = canopyFilter.sharedMesh.bounds;
                    if (bounds.size.z < 2.15f || bounds.size.x < 1.00f || bounds.max.y < 2.35f)
                    {
                        Debug.LogError(
                            $"P-51 Step 76 failed. '{flight.name}' canopy does not span the cockpit opening. "
                            + $"Mesh bounds={bounds.size} maxY={bounds.max.y:F2}.",
                            canopyGlass);
                        passed = false;
                    }
                }

                if (CountActiveLegacyCanopyVisuals(flight.transform, cockpit) != 0)
                {
                    Debug.LogError($"P-51 Step 76 failed. '{flight.name}' still has active legacy sphere-canopy geometry.", flight);
                    passed = false;
                }

                P51FuelQuantityGauge gauge = panel.GetComponentInChildren<P51FuelQuantityGauge>(true);
                if (gauge == null || !gauge.gameObject.activeSelf)
                {
                    Debug.LogError($"P-51 Step 76 failed. '{flight.name}' lost its live panel fuel gauge during the cockpit re-layout.", flight);
                    passed = false;
                }
            }

            P51FlightController master = GameObject.Find(AircraftRootName)?.GetComponent<P51FlightController>();
            if (master == null)
            {
                Debug.LogError("P-51 Step 76 failed. Master P-51 is missing.");
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("P-51 Step 76 failed. Standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"P-51 Step 76 passed. Aircraft checked={checkedAircraft}. The seat, pilot eye, stick, consoles, pedals and panel now occupy the full Step 73 cockpit cavity; "
                    + "the panel has more than 0.9 m of viewing distance from the pilot eye, the old sphere canopy is disabled, the corrected transparent canopy spans the opening, "
                    + "and the live fuel instrument remains mounted in the panel.");
            }
        }

        private static void ReLayoutCockpit(
            Transform aircraft,
            Transform cockpit,
            Transform panel,
            Transform cameraAnchor,
            Material metal,
            Material dark,
            Material interior,
            Material service)
        {
            cockpit.localPosition = Vector3.zero;
            cockpit.localRotation = Quaternion.identity;
            cockpit.localScale = Vector3.one;

            Undo.RecordObject(cameraAnchor, "Move P-51 pilot eye into full cockpit cavity");
            cameraAnchor.position = aircraft.TransformPoint(CameraLocalPosition);
            cameraAnchor.rotation = aircraft.rotation;

            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Cockpit Floor",
                new Vector3(0f, 0.99f, 0.02f),
                new Vector3(0.92f, 0.055f, 2.05f), Vector3.zero, dark);

            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Cockpit Left Sidewall",
                new Vector3(-0.52f, 1.29f, 0.00f),
                new Vector3(0.050f, 0.55f, 2.04f), Vector3.zero, interior);
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Cockpit Right Sidewall",
                new Vector3(0.52f, 1.29f, 0.00f),
                new Vector3(0.050f, 0.55f, 2.04f), Vector3.zero, interior);

            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Cockpit Left Inner Skin",
                new Vector3(-0.485f, 1.38f, -0.02f),
                new Vector3(0.028f, 0.38f, 1.96f), Vector3.zero, dark);
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Cockpit Right Inner Skin",
                new Vector3(0.485f, 1.38f, -0.02f),
                new Vector3(0.028f, 0.38f, 1.96f), Vector3.zero, dark);

            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Cockpit Rear Bulkhead",
                new Vector3(0f, 1.36f, -1.08f),
                new Vector3(0.94f, 0.82f, 0.055f), new Vector3(-3f, 0f, 0f), interior);

            // The true opening rim from Step 73 is now the canopy sill. Retire the old straight
            // Step 69/71 sill blocks instead of stacking another pair above the fuselage cut.
            SetNamedChildActive(cockpit, "Left Canopy Sill", false);
            SetNamedChildActive(cockpit, "Right Canopy Sill", false);

            // Seat occupies the aft third of the opening.
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Pilot Seat Base",
                new Vector3(0f, 1.12f, -0.61f),
                new Vector3(0.39f, 0.10f, 0.48f), new Vector3(-5f, 0f, 0f), service);
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Pilot Seat Back",
                SeatBackLocalPosition,
                new Vector3(0.42f, 0.58f, 0.080f), new Vector3(-11f, 0f, 0f), service);
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Pilot Headrest",
                new Vector3(0f, 1.78f, -0.96f),
                new Vector3(0.28f, 0.18f, 0.09f), new Vector3(-8f, 0f, 0f), dark);

            // Instrument panel now lives forward near the windshield instead of immediately in
            // front of the camera. Existing gauge/bezel children stay attached and move with it.
            Undo.RecordObject(panel, "Move P-51 instrument panel forward");
            panel.localPosition = PanelLocalPosition;
            panel.localRotation = Quaternion.Euler(-7f, 0f, 0f);
            panel.localScale = new Vector3(0.90f, 0.52f, 0.060f);
            RemoveLocalColliders(panel.gameObject);
            Renderer panelRenderer = panel.GetComponent<Renderer>();
            if (panelRenderer != null)
            {
                panelRenderer.sharedMaterial = dark;
                EditorUtility.SetDirty(panelRenderer);
            }

            P51FuelQuantityGauge gauge = panel.GetComponentInChildren<P51FuelQuantityGauge>(true);
            if (gauge != null)
            {
                gauge.transform.localPosition = new Vector3(0.22f, -0.115f, -0.072f);
                gauge.transform.localRotation = Quaternion.identity;
                gauge.transform.localScale = Vector3.one * 0.64f;
                gauge.RefreshGauge();
                EditorUtility.SetDirty(gauge.transform);
                EditorUtility.SetDirty(gauge);
            }

            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Instrument Panel Glare Shield",
                new Vector3(0f, 1.75f, 0.49f),
                new Vector3(0.95f, 0.045f, 0.25f), new Vector3(-4f, 0f, 0f), dark);
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Cockpit Forward Coaming",
                new Vector3(0f, 1.68f, 0.76f),
                new Vector3(0.90f, 0.075f, 0.40f), new Vector3(-9f, 0f, 0f), interior);
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Cockpit Lower Dash Fairing",
                new Vector3(0f, 1.29f, 0.57f),
                new Vector3(0.80f, 0.25f, 0.24f), new Vector3(18f, 0f, 0f), dark);
            SetNamedChildActive(cockpit, "Cockpit Upper Nose Cover", false);
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Cockpit Panel Support",
                new Vector3(0f, 1.18f, 0.48f),
                new Vector3(0.32f, 0.10f, 0.20f), new Vector3(10f, 0f, 0f), service);

            // Consoles fill the center section without swallowing the pilot's forward view.
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Left Cockpit Console",
                new Vector3(-0.39f, 1.15f, 0.02f),
                new Vector3(0.18f, 0.16f, 1.50f), Vector3.zero, dark);
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Right Cockpit Console",
                new Vector3(0.39f, 1.15f, 0.02f),
                new Vector3(0.18f, 0.16f, 1.50f), Vector3.zero, dark);
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Left Future Switch Panel",
                new Vector3(-0.39f, 1.26f, 0.15f),
                new Vector3(0.16f, 0.035f, 0.74f), new Vector3(-7f, 0f, 0f), service);
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Right Future Switch Panel",
                new Vector3(0.39f, 1.26f, 0.15f),
                new Vector3(0.16f, 0.035f, 0.74f), new Vector3(-7f, 0f, 0f), service);

            // Stick sits between the seat and panel. Pedals finally move forward into the nose end
            // of the cavity instead of being packed directly under the panel/camera.
            Transform stick = CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cylinder, "Control Stick Shaft",
                StickLocalPosition,
                new Vector3(0.034f, 0.24f, 0.034f), new Vector3(8f, 0f, 0f), service);
            CreateOrUpdatePrimitive(
                stick, PrimitiveType.Cylinder, "Control Stick Grip",
                new Vector3(0f, 0.98f, 0f),
                new Vector3(1.60f, 0.20f, 1.60f), new Vector3(90f, 0f, 0f), dark);
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Left Rudder Pedal",
                new Vector3(-0.16f, 1.08f, 0.78f),
                new Vector3(0.14f, 0.18f, 0.040f), new Vector3(20f, 0f, 0f), service);
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Right Rudder Pedal",
                new Vector3(0.16f, 1.08f, 0.78f),
                new Vector3(0.14f, 0.18f, 0.040f), new Vector3(20f, 0f, 0f), service);

            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Throttle Quadrant Housing",
                new Vector3(-0.43f, 1.39f, -0.18f),
                new Vector3(0.12f, 0.23f, 0.34f), Vector3.zero, dark);
            Transform throttle = CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cylinder, "Throttle Lever Placeholder",
                new Vector3(-0.43f, 1.54f, -0.16f),
                new Vector3(0.019f, 0.14f, 0.019f), new Vector3(28f, 0f, -8f), service);
            CreateOrUpdatePrimitive(
                throttle, PrimitiveType.Sphere, "Throttle Lever Knob",
                new Vector3(0f, 1.02f, 0f), Vector3.one * 1.72f, Vector3.zero, dark);

            EditorUtility.SetDirty(panel);
            EditorUtility.SetDirty(cockpit);
        }

        private static int DisableLegacyCockpitAndCanopyVisuals(Transform aircraft, Transform currentCockpit)
        {
            int disabled = 0;
            Transform[] transforms = aircraft.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null || candidate == aircraft || candidate.IsChildOf(currentCockpit))
                {
                    continue;
                }

                bool legacyCanopy = false;
                for (int nameIndex = 0; nameIndex < LegacyCanopyNames.Length; nameIndex++)
                {
                    if (candidate.name == LegacyCanopyNames[nameIndex])
                    {
                        legacyCanopy = true;
                        break;
                    }
                }

                bool legacyCockpit = candidate.name == "Pilot Seat Back"
                    || candidate.name == "Pilot Seat Bottom"
                    || candidate.name == "Instrument Panel";

                if (!legacyCanopy && !legacyCockpit)
                {
                    continue;
                }

                if (candidate.gameObject.activeSelf)
                {
                    Undo.RecordObject(candidate.gameObject, "Disable obsolete P-51 cockpit/canopy visual");
                    candidate.gameObject.SetActive(false);
                    EditorUtility.SetDirty(candidate.gameObject);
                    disabled++;
                }
            }

            return disabled;
        }

        private static int CountActiveLegacyCanopyVisuals(Transform aircraft, Transform currentCockpit)
        {
            int count = 0;
            Transform[] transforms = aircraft.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null || candidate == aircraft || candidate.IsChildOf(currentCockpit) || !candidate.gameObject.activeSelf)
                {
                    continue;
                }

                for (int nameIndex = 0; nameIndex < LegacyCanopyNames.Length; nameIndex++)
                {
                    if (candidate.name == LegacyCanopyNames[nameIndex])
                    {
                        count++;
                        break;
                    }
                }
            }

            return count;
        }

        private static void RebuildCanopyAssembly(
            Transform aircraft,
            Mesh canopyMesh,
            Material glass,
            Material dark,
            Material metal)
        {
            Transform existing = FindDirectChild(aircraft, CanopyAssemblyName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            GameObject assemblyObject = new GameObject(CanopyAssemblyName);
            Undo.RegisterCreatedObjectUndo(assemblyObject, "Create corrected P-51 canopy assembly");
            Transform assembly = assemblyObject.transform;
            assembly.SetParent(aircraft, false);
            assembly.localPosition = Vector3.zero;
            assembly.localRotation = Quaternion.identity;
            assembly.localScale = Vector3.one;

            GameObject glassObject = new GameObject(CanopyGlassName);
            Undo.RegisterCreatedObjectUndo(glassObject, "Create corrected P-51 canopy glass");
            glassObject.transform.SetParent(assembly, false);
            MeshFilter filter = Undo.AddComponent<MeshFilter>(glassObject);
            MeshRenderer renderer = Undo.AddComponent<MeshRenderer>(glassObject);
            filter.sharedMesh = canopyMesh;
            renderer.sharedMaterial = glass;

            // Lower rails align with the real Step 73 fuselage opening.
            CreateBeam(assembly, "Corrected Left Canopy Rail",
                new Vector3(-0.51f, 1.73f, -1.14f),
                new Vector3(-0.56f, 1.71f, 0.57f), 0.040f, dark);
            CreateBeam(assembly, "Corrected Right Canopy Rail",
                new Vector3(0.51f, 1.73f, -1.14f),
                new Vector3(0.56f, 1.71f, 0.57f), 0.040f, dark);

            // Aft bow: modest structure at the back of the bubble, not a huge bar through the view.
            CreateBeam(assembly, "Corrected Rear Bow Left Lower",
                new Vector3(-0.51f, 1.73f, -1.14f),
                new Vector3(-0.28f, 1.98f, -1.14f), 0.034f, dark);
            CreateBeam(assembly, "Corrected Rear Bow Left Upper",
                new Vector3(-0.28f, 1.98f, -1.14f),
                new Vector3(0f, 2.06f, -1.14f), 0.034f, dark);
            CreateBeam(assembly, "Corrected Rear Bow Right Lower",
                new Vector3(0.51f, 1.73f, -1.14f),
                new Vector3(0.28f, 1.98f, -1.14f), 0.034f, dark);
            CreateBeam(assembly, "Corrected Rear Bow Right Upper",
                new Vector3(0.28f, 1.98f, -1.14f),
                new Vector3(0f, 2.06f, -1.14f), 0.034f, dark);

            // Windshield/bubble junction at the front of the sliding bubble.
            CreateBeam(assembly, "Corrected Windshield Bow Left Lower",
                new Vector3(-0.56f, 1.71f, 0.55f),
                new Vector3(-0.31f, 2.10f, 0.55f), 0.038f, dark);
            CreateBeam(assembly, "Corrected Windshield Bow Left Upper",
                new Vector3(-0.31f, 2.10f, 0.55f),
                new Vector3(0f, 2.28f, 0.55f), 0.038f, dark);
            CreateBeam(assembly, "Corrected Windshield Bow Right Lower",
                new Vector3(0.56f, 1.71f, 0.55f),
                new Vector3(0.31f, 2.10f, 0.55f), 0.038f, dark);
            CreateBeam(assembly, "Corrected Windshield Bow Right Upper",
                new Vector3(0.31f, 2.10f, 0.55f),
                new Vector3(0f, 2.28f, 0.55f), 0.038f, dark);

            // Forward windshield perimeter slopes down into the nose instead of leaving the old
            // ellipsoid floating over the aft half of the cockpit.
            CreateBeam(assembly, "Corrected Left Windshield Lower Frame",
                new Vector3(-0.56f, 1.71f, 0.55f),
                new Vector3(-0.50f, 1.75f, 1.16f), 0.035f, metal);
            CreateBeam(assembly, "Corrected Right Windshield Lower Frame",
                new Vector3(0.56f, 1.71f, 0.55f),
                new Vector3(0.50f, 1.75f, 1.16f), 0.035f, metal);
            CreateBeam(assembly, "Corrected Windshield Center Brace",
                new Vector3(0f, 2.28f, 0.55f),
                new Vector3(0f, 1.93f, 1.16f), 0.028f, dark);
            CreateBeam(assembly, "Corrected Windshield Front Left",
                new Vector3(-0.50f, 1.75f, 1.16f),
                new Vector3(0f, 1.93f, 1.16f), 0.032f, dark);
            CreateBeam(assembly, "Corrected Windshield Front Right",
                new Vector3(0.50f, 1.75f, 1.16f),
                new Vector3(0f, 1.93f, 1.16f), 0.032f, dark);

            EditorUtility.SetDirty(filter);
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(assembly);
        }

        private static Mesh CreateOrUpdateCanopyMesh()
        {
            const int arcSegments = 14;
            int rowSize = arcSegments + 1;
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            for (int stationIndex = 0; stationIndex < CanopyStations.Length; stationIndex++)
            {
                CanopyStation station = CanopyStations[stationIndex];
                for (int arc = 0; arc <= arcSegments; arc++)
                {
                    float t = arc / (float)arcSegments;
                    float angle = Mathf.PI - t * Mathf.PI;
                    float x = Mathf.Cos(angle) * station.HalfWidth;
                    float y = station.SillY + Mathf.Sin(angle) * (station.CrownY - station.SillY);
                    vertices.Add(new Vector3(x, y, station.Z));
                }
            }

            for (int stationIndex = 0; stationIndex < CanopyStations.Length - 1; stationIndex++)
            {
                for (int arc = 0; arc < arcSegments; arc++)
                {
                    int a = stationIndex * rowSize + arc;
                    int b = a + 1;
                    int c = (stationIndex + 1) * rowSize + arc + 1;
                    int d = (stationIndex + 1) * rowSize + arc;
                    AddDoubleSidedQuad(triangles, a, b, c, d);
                }
            }

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(CanopyMeshPath);
            if (mesh == null)
            {
                mesh = new Mesh();
                mesh.name = "P-51D Corrected Full-Length Canopy";
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, CanopyMeshPath);
            }
            else
            {
                mesh.Clear();
                mesh.name = "P-51D Corrected Full-Length Canopy";
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                EditorUtility.SetDirty(mesh);
            }

            return mesh;
        }

        private static void AddDoubleSidedQuad(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(d);

            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(b);
            triangles.Add(a);
            triangles.Add(d);
            triangles.Add(c);
        }

        private static Transform CreateBeam(
            Transform parent,
            string name,
            Vector3 start,
            Vector3 end,
            float thickness,
            Material material)
        {
            Vector3 direction = end - start;
            float length = direction.magnitude;
            if (length <= 0.0001f)
            {
                return null;
            }

            Transform beam = CreateOrUpdatePrimitive(
                parent,
                PrimitiveType.Cube,
                name,
                (start + end) * 0.5f,
                new Vector3(thickness, length, thickness),
                Vector3.zero,
                material);
            beam.localRotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            EditorUtility.SetDirty(beam);
            return beam;
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
                if (!part.activeSelf)
                {
                    Undo.RecordObject(part, $"Enable {objectName}");
                    part.SetActive(true);
                }
            }

            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(localEuler);
            part.transform.localScale = localScale;
            RemoveLocalColliders(part);

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
                renderer.enabled = true;
                EditorUtility.SetDirty(renderer);
            }

            EditorUtility.SetDirty(part.transform);
            return part.transform;
        }

        private static void SetNamedChildActive(Transform parent, string name, bool active)
        {
            Transform child = FindDescendant(parent, name);
            if (child == null || child.gameObject.activeSelf == active)
            {
                return;
            }

            Undo.RecordObject(child.gameObject, active ? $"Enable {name}" : $"Disable {name}");
            child.gameObject.SetActive(active);
            EditorUtility.SetDirty(child.gameObject);
        }

        private static void RemoveLocalColliders(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            Collider[] colliders = gameObject.GetComponents<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    Undo.DestroyObjectImmediate(colliders[i]);
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

            if (parent.name == name)
            {
                return parent;
            }

            Transform[] transforms = parent.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name == name)
                {
                    return transforms[i];
                }
            }

            return null;
        }
    }
}
