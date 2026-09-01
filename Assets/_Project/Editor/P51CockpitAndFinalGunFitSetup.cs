using Hanger51.Aircraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51CockpitAndFinalGunFitSetup
    {
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string CockpitRootName = "P-51 Cockpit Interior";
        private const string InstrumentPanelName = "P-51 Cockpit Instrument Panel";
        private const string GaugeName = "P-51 Main Fuel Quantity Gauge";
        private const string ArmamentRootName = "P-51 Serviceable Wing Armament";
        private const string InstalledGunName = "Installed M2 Wing Gun";

        private static readonly Vector3 FinalGunScale = new Vector3(0.68f, 0.22f, 1.00f);
        private static readonly Vector3 FinalGunLocalPosition = new Vector3(0f, 0.018f, 0.035f);
        private const float FinalGunMountLocalY = 0.122f;

        private const string MetalMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/PolishedAluminum.mat";
        private const string DarkMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/DarkAircraftMetal.mat";
        private const string ServiceMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/ServiceHardware.mat";

        [MenuItem("Hanger 51/P-51 Mustang/69 - Build Cockpit Shell and Finalize Wing Gun Fit")]
        public static void BuildCockpitAndFinalizeGunFit()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 Step 69 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("P-51 Step 69 failed. Open the saved Hanger 51 gameplay scene first.");
                return;
            }

            Material metal = AssetDatabase.LoadAssetAtPath<Material>(MetalMaterialPath);
            Material dark = AssetDatabase.LoadAssetAtPath<Material>(DarkMaterialPath);
            Material service = AssetDatabase.LoadAssetAtPath<Material>(ServiceMaterialPath);
            if (metal == null || dark == null || service == null)
            {
                Debug.LogError("P-51 Step 69 failed. Required P-51 cockpit materials are missing.");
                return;
            }

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 69 failed. No P-51 aircraft were found in the scene.");
                return;
            }

            int cockpitsBuilt = 0;
            int gaugesMovedOrBuilt = 0;
            int gunsRefit = 0;
            P51FlightController master = null;

            for (int aircraftIndex = 0; aircraftIndex < aircraft.Length; aircraftIndex++)
            {
                P51FlightController flight = aircraft[aircraftIndex];
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
                P51FuelSystem fuelSystem = flight.GetComponent<P51FuelSystem>();
                if (seat == null || seat.CameraAnchor == null || fuelSystem == null)
                {
                    Debug.LogWarning(
                        $"P-51 Step 69 skipped cockpit construction on '{flight.name}' because its pilot camera anchor or fuel system is missing.",
                        flight);
                }
                else
                {
                    Transform panel = BuildOrRefitCockpit(
                        flight,
                        seat.CameraAnchor,
                        metal,
                        dark,
                        service);
                    if (panel != null)
                    {
                        cockpitsBuilt++;
                        if (MoveOrBuildFuelGauge(flight, fuelSystem, panel, metal, dark, service))
                        {
                            gaugesMovedOrBuilt++;
                        }
                    }
                }

                gunsRefit += FinalizeGunFit(flight.transform);
                EditorUtility.SetDirty(flight);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 69 made the cockpit/gun changes but Unity could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 Step 69 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = master != null ? master.gameObject : aircraft[0].gameObject;
            Debug.Log(
                $"P-51 Step 69 complete. Cockpit shells built/refit={cockpitsBuilt}, fuel gauges moved/built={gaugesMovedOrBuilt}, "
                + $"installed wing guns refit={gunsRefit}. The P-51 now has a real aircraft-mounted cockpit shell with floor, sidewalls, seat, "
                + "instrument panel, glare shield, consoles, controls and future switch-panel space. The live fuel gauge is mounted in the panel, "
                + "and the six M2 receiver visuals are vertically compressed/centered without moving the actual muzzle anchors.",
                master != null ? master.gameObject : null);
        }

        [MenuItem("Hanger 51/P-51 Mustang/70 - Validate Cockpit Shell and Wing Gun Fit")]
        public static void ValidateCockpitAndGunFit()
        {
            bool passed = true;
            int aircraftChecked = 0;
            int gunsChecked = 0;

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 70 failed. No P-51 aircraft were found.");
                return;
            }

            for (int aircraftIndex = 0; aircraftIndex < aircraft.Length; aircraftIndex++)
            {
                P51FlightController flight = aircraft[aircraftIndex];
                if (flight == null || !flight.gameObject.scene.IsValid())
                {
                    continue;
                }

                aircraftChecked++;
                Transform cockpit = FindDescendant(flight.transform, CockpitRootName);
                Transform panel = FindDescendant(cockpit, InstrumentPanelName);
                Transform floor = FindDescendant(cockpit, "Cockpit Floor");
                Transform leftWall = FindDescendant(cockpit, "Cockpit Left Sidewall");
                Transform rightWall = FindDescendant(cockpit, "Cockpit Right Sidewall");
                Transform seatBack = FindDescendant(cockpit, "Pilot Seat Back");
                Transform controlStick = FindDescendant(cockpit, "Control Stick Shaft");
                P51FuelSystem fuelSystem = flight.GetComponent<P51FuelSystem>();

                if (cockpit == null
                    || panel == null
                    || floor == null
                    || leftWall == null
                    || rightWall == null
                    || seatBack == null
                    || controlStick == null)
                {
                    Debug.LogError(
                        $"P-51 Step 70 failed. '{flight.name}' cockpit shell is incomplete.",
                        flight);
                    passed = false;
                }

                P51FuelQuantityGauge[] gauges =
                    flight.GetComponentsInChildren<P51FuelQuantityGauge>(true);
                P51FuelQuantityGauge activePanelGauge = null;
                int activeExternalGauges = 0;
                for (int gaugeIndex = 0; gaugeIndex < gauges.Length; gaugeIndex++)
                {
                    P51FuelQuantityGauge gauge = gauges[gaugeIndex];
                    if (gauge == null || !gauge.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    if (panel != null && gauge.transform.IsChildOf(panel))
                    {
                        activePanelGauge = gauge;
                    }
                    else
                    {
                        activeExternalGauges++;
                    }
                }

                if (activePanelGauge == null
                    || !activePanelGauge.IsConfigured
                    || activePanelGauge.FuelSystem != fuelSystem
                    || activeExternalGauges != 0)
                {
                    Debug.LogError(
                        $"P-51 Step 70 failed. '{flight.name}' must have one configured panel-mounted fuel gauge reading its own fuel system "
                        + $"and zero active external gauges. External active gauges={activeExternalGauges}.",
                        flight);
                    passed = false;
                }

                Transform armamentRoot = FindDescendant(flight.transform, ArmamentRootName);
                if (armamentRoot != null)
                {
                    for (int wingIndex = 0; wingIndex < 2; wingIndex++)
                    {
                        string wingName = wingIndex == 0 ? "Left" : "Right";
                        Transform interior = FindDescendant(
                            armamentRoot,
                            $"{wingName} Wing Armament Bay Interior");
                        if (interior == null)
                        {
                            continue;
                        }

                        for (int station = 1; station <= 3; station++)
                        {
                            Transform gunTarget = FindDescendant(
                                interior,
                                $"{wingName} Gun Mount {station}");
                            Transform mountedGun = gunTarget != null
                                ? FindDescendant(gunTarget, InstalledGunName)
                                : null;
                            if (gunTarget == null || mountedGun == null)
                            {
                                Debug.LogError(
                                    $"P-51 Step 70 failed. '{flight.name}' {wingName.ToLowerInvariant()} gun station {station} is incomplete.",
                                    flight);
                                passed = false;
                                continue;
                            }

                            gunsChecked++;
                            bool scaleCorrect = Approximately(mountedGun.localScale, FinalGunScale, 0.004f);
                            bool mountCorrect = Mathf.Abs(gunTarget.localPosition.y - FinalGunMountLocalY) <= 0.004f;
                            bool visualCorrect = Mathf.Abs(mountedGun.localPosition.y - FinalGunLocalPosition.y) <= 0.004f;
                            if (!scaleCorrect || !mountCorrect || !visualCorrect)
                            {
                                Debug.LogError(
                                    $"P-51 Step 70 failed. '{flight.name}' {wingName.ToLowerInvariant()} gun {station} does not use the final contained wing fit. "
                                    + $"MountY={gunTarget.localPosition.y:F3}, visualY={mountedGun.localPosition.y:F3}, scaleY={mountedGun.localScale.y:F3}.",
                                    mountedGun);
                                passed = false;
                            }
                        }
                    }
                }
            }

            P51FlightController master = GameObject.Find(AircraftRootName)?.GetComponent<P51FlightController>();
            if (master == null)
            {
                Debug.LogError("P-51 Step 70 failed. Master P-51 is missing.");
                passed = false;
            }
            else
            {
                Transform masterArmament = FindDescendant(master.transform, ArmamentRootName);
                int masterGunCount = CountInstalledGuns(masterArmament);
                if (masterGunCount != 6)
                {
                    Debug.LogError(
                        $"P-51 Step 70 failed. Master P-51 must contain six installed M2 gun visuals; found {masterGunCount}.",
                        master);
                    passed = false;
                }
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("P-51 Step 70 failed. Standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"P-51 Step 70 passed. Aircraft checked={aircraftChecked}, gun stations checked={gunsChecked}. "
                    + "The cockpit shell is aircraft-mounted, the live fuel instrument is inside the instrument panel with no active exterior copy, "
                    + "and all installed M2 receiver visuals use the final low-profile wing-bay fit while muzzle geometry remains independent.");
            }
        }

        private static Transform BuildOrRefitCockpit(
            P51FlightController flight,
            Transform cameraAnchor,
            Material metal,
            Material dark,
            Material service)
        {
            if (flight == null || cameraAnchor == null)
            {
                return null;
            }

            Transform aircraft = flight.transform;
            Transform cockpit = FindDirectChild(aircraft, CockpitRootName);
            if (cockpit == null)
            {
                GameObject cockpitObject = new GameObject(CockpitRootName);
                Undo.RegisterCreatedObjectUndo(cockpitObject, "Create P-51 cockpit interior");
                cockpit = cockpitObject.transform;
                cockpit.SetParent(aircraft, false);
            }
            else
            {
                Undo.RecordObject(cockpit, "Refit P-51 cockpit interior");
            }

            cockpit.localPosition = Vector3.zero;
            cockpit.localRotation = Quaternion.identity;
            cockpit.localScale = Vector3.one;

            Vector3 head = aircraft.InverseTransformPoint(cameraAnchor.position);

            // Structural shell. All pieces are visual-only so the cockpit cannot become a new
            // physics contact surface or interfere with the Player/canopy interaction.
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Cockpit Floor",
                new Vector3(0f, head.y - 0.72f, head.z + 0.03f),
                new Vector3(0.86f, 0.055f, 1.34f), Vector3.zero, dark);
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Cockpit Left Sidewall",
                new Vector3(-0.43f, head.y - 0.37f, head.z + 0.02f),
                new Vector3(0.055f, 0.68f, 1.30f), Vector3.zero, dark);
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Cockpit Right Sidewall",
                new Vector3(0.43f, head.y - 0.37f, head.z + 0.02f),
                new Vector3(0.055f, 0.68f, 1.30f), Vector3.zero, dark);
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Cockpit Rear Bulkhead",
                new Vector3(0f, head.y - 0.36f, head.z - 0.61f),
                new Vector3(0.82f, 0.72f, 0.055f), Vector3.zero, dark);

            // Canopy/sill structure gives the interior a physical boundary when viewed from
            // both first person and through the transparent canopy from outside.
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Left Canopy Sill",
                new Vector3(-0.43f, head.y - 0.02f, head.z + 0.02f),
                new Vector3(0.075f, 0.075f, 1.26f), Vector3.zero, metal);
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Right Canopy Sill",
                new Vector3(0.43f, head.y - 0.02f, head.z + 0.02f),
                new Vector3(0.075f, 0.075f, 1.26f), Vector3.zero, metal);

            // Pilot seat.
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Pilot Seat Base",
                new Vector3(0f, head.y - 0.61f, head.z - 0.22f),
                new Vector3(0.36f, 0.08f, 0.38f), new Vector3(-4f, 0f, 0f), service);
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Pilot Seat Back",
                new Vector3(0f, head.y - 0.36f, head.z - 0.43f),
                new Vector3(0.38f, 0.52f, 0.075f), new Vector3(-10f, 0f, 0f), service);
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Pilot Headrest",
                new Vector3(0f, head.y - 0.08f, head.z - 0.46f),
                new Vector3(0.27f, 0.16f, 0.085f), new Vector3(-8f, 0f, 0f), dark);

            // Instrument panel and glare shield.
            Transform panel = CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, InstrumentPanelName,
                new Vector3(0f, head.y - 0.28f, head.z + 0.73f),
                new Vector3(0.82f, 0.50f, 0.060f), new Vector3(-7f, 0f, 0f), dark);
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Instrument Panel Glare Shield",
                new Vector3(0f, head.y - 0.015f, head.z + 0.68f),
                new Vector3(0.86f, 0.065f, 0.22f), new Vector3(-3f, 0f, 0f), dark);

            // Side consoles and future switch-control areas.
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Left Cockpit Console",
                new Vector3(-0.31f, head.y - 0.56f, head.z + 0.02f),
                new Vector3(0.19f, 0.15f, 0.83f), Vector3.zero, dark);
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Right Cockpit Console",
                new Vector3(0.31f, head.y - 0.56f, head.z + 0.02f),
                new Vector3(0.19f, 0.15f, 0.83f), Vector3.zero, dark);
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Left Future Switch Panel",
                new Vector3(-0.315f, head.y - 0.455f, head.z + 0.19f),
                new Vector3(0.17f, 0.035f, 0.43f), new Vector3(-8f, 0f, 0f), service);
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Right Future Switch Panel",
                new Vector3(0.315f, head.y - 0.455f, head.z + 0.19f),
                new Vector3(0.17f, 0.035f, 0.43f), new Vector3(-8f, 0f, 0f), service);

            // Control stick and rudder pedals.
            Transform stick = CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cylinder, "Control Stick Shaft",
                new Vector3(0f, head.y - 0.51f, head.z + 0.15f),
                new Vector3(0.032f, 0.22f, 0.032f), new Vector3(8f, 0f, 0f), service);
            CreateOrUpdatePrimitive(
                stick, PrimitiveType.Cylinder, "Control Stick Grip",
                new Vector3(0f, 0.97f, 0f),
                new Vector3(1.65f, 0.20f, 1.65f), new Vector3(90f, 0f, 0f), dark);
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Left Rudder Pedal",
                new Vector3(-0.14f, head.y - 0.64f, head.z + 0.52f),
                new Vector3(0.13f, 0.16f, 0.035f), new Vector3(18f, 0f, 0f), service);
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Right Rudder Pedal",
                new Vector3(0.14f, head.y - 0.64f, head.z + 0.52f),
                new Vector3(0.13f, 0.16f, 0.035f), new Vector3(18f, 0f, 0f), service);

            // Non-functional throttle-quadrant placeholder. Its hierarchy is deliberately
            // named for the future interactive throttle/mixture/control implementation.
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Throttle Quadrant Housing",
                new Vector3(-0.36f, head.y - 0.39f, head.z - 0.02f),
                new Vector3(0.12f, 0.23f, 0.31f), Vector3.zero, dark);
            Transform throttleLever = CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cylinder, "Throttle Lever Placeholder",
                new Vector3(-0.36f, head.y - 0.25f, head.z - 0.01f),
                new Vector3(0.018f, 0.13f, 0.018f), new Vector3(30f, 0f, -8f), service);
            CreateOrUpdatePrimitive(
                throttleLever, PrimitiveType.Sphere, "Throttle Lever Knob",
                new Vector3(0f, 1.03f, 0f),
                Vector3.one * 1.75f, Vector3.zero, dark);

            BuildPlaceholderInstrumentBezel(panel, "Primary Instrument Bezel 1", -0.24f, 0.10f, metal, dark);
            BuildPlaceholderInstrumentBezel(panel, "Primary Instrument Bezel 2", 0.00f, 0.10f, metal, dark);
            BuildPlaceholderInstrumentBezel(panel, "Primary Instrument Bezel 3", 0.24f, 0.10f, metal, dark);
            BuildPlaceholderInstrumentBezel(panel, "Primary Instrument Bezel 4", -0.18f, -0.13f, metal, dark);
            BuildPlaceholderInstrumentBezel(panel, "Primary Instrument Bezel 5", 0.02f, -0.13f, metal, dark);

            EditorUtility.SetDirty(cockpit);
            return panel;
        }

        private static void BuildPlaceholderInstrumentBezel(
            Transform panel,
            string name,
            float localX,
            float localY,
            Material metal,
            Material dark)
        {
            Transform bezel = CreateOrUpdatePrimitive(
                panel, PrimitiveType.Cylinder, name,
                new Vector3(localX, localY, -0.060f),
                new Vector3(0.145f, 0.012f, 0.145f),
                new Vector3(90f, 0f, 0f), metal);
            CreateOrUpdatePrimitive(
                bezel, PrimitiveType.Cylinder, name + " Face",
                new Vector3(0f, -1.08f, 0f),
                new Vector3(0.82f, 0.26f, 0.82f),
                Vector3.zero, dark);
        }

        private static bool MoveOrBuildFuelGauge(
            P51FlightController flight,
            P51FuelSystem fuelSystem,
            Transform panel,
            Material metal,
            Material dark,
            Material service)
        {
            if (flight == null || fuelSystem == null || panel == null)
            {
                return false;
            }

            P51FuelQuantityGauge[] gauges =
                flight.GetComponentsInChildren<P51FuelQuantityGauge>(true);
            P51FuelQuantityGauge panelGauge = null;

            for (int index = 0; index < gauges.Length; index++)
            {
                P51FuelQuantityGauge gauge = gauges[index];
                if (gauge == null)
                {
                    continue;
                }

                if (panelGauge == null)
                {
                    panelGauge = gauge;
                    continue;
                }

                // Preserve duplicates for debugging/undo history but keep them from rendering.
                Undo.RecordObject(gauge.gameObject, "Disable duplicate P-51 fuel gauge");
                gauge.gameObject.SetActive(false);
                EditorUtility.SetDirty(gauge.gameObject);
            }

            if (panelGauge == null)
            {
                panelGauge = BuildFuelGauge(panel, fuelSystem, metal, dark, service);
            }
            else
            {
                Undo.RecordObject(panelGauge.transform, "Move P-51 fuel gauge into cockpit panel");
                Undo.RecordObject(panelGauge.gameObject, "Enable P-51 cockpit fuel gauge");
                panelGauge.gameObject.SetActive(true);
                panelGauge.transform.SetParent(panel, false);
                panelGauge.transform.localPosition = new Vector3(0.245f, -0.125f, -0.072f);
                panelGauge.transform.localRotation = Quaternion.identity;
                panelGauge.transform.localScale = Vector3.one * 0.72f;

                SerializedObject gaugeSerialized = new SerializedObject(panelGauge);
                SerializedProperty fuelProperty = gaugeSerialized.FindProperty("fuelSystem");
                if (fuelProperty != null)
                {
                    fuelProperty.objectReferenceValue = fuelSystem;
                    gaugeSerialized.ApplyModifiedPropertiesWithoutUndo();
                }
                panelGauge.RefreshGauge();
                EditorUtility.SetDirty(panelGauge);
            }

            return panelGauge != null;
        }

        private static P51FuelQuantityGauge BuildFuelGauge(
            Transform panel,
            P51FuelSystem fuelSystem,
            Material metal,
            Material dark,
            Material service)
        {
            GameObject gaugeObject = new GameObject(GaugeName);
            Undo.RegisterCreatedObjectUndo(gaugeObject, "Create cockpit P-51 fuel quantity gauge");
            gaugeObject.transform.SetParent(panel, false);
            gaugeObject.transform.localPosition = new Vector3(0.245f, -0.125f, -0.072f);
            gaugeObject.transform.localRotation = Quaternion.identity;
            gaugeObject.transform.localScale = Vector3.one * 0.72f;

            CreateOrUpdatePrimitive(
                gaugeObject.transform, PrimitiveType.Cylinder, "Fuel Gauge Metal Bezel",
                Vector3.zero, new Vector3(0.27f, 0.018f, 0.27f),
                new Vector3(90f, 0f, 0f), metal);
            CreateOrUpdatePrimitive(
                gaugeObject.transform, PrimitiveType.Cylinder, "Fuel Gauge Dark Face",
                new Vector3(0f, 0f, -0.020f), new Vector3(0.235f, 0.010f, 0.235f),
                new Vector3(90f, 0f, 0f), dark);

            const float emptyAngle = 110f;
            const float fullAngle = -110f;
            const float tickRadius = 0.090f;
            for (int tickIndex = 0; tickIndex <= 8; tickIndex++)
            {
                float t = tickIndex / 8f;
                float angle = Mathf.Lerp(emptyAngle, fullAngle, t);
                Vector3 direction = Quaternion.Euler(0f, 0f, angle) * Vector3.up;
                CreateOrUpdatePrimitive(
                    gaugeObject.transform,
                    PrimitiveType.Cube,
                    $"Fuel Gauge Tick {tickIndex + 1}",
                    direction * tickRadius + new Vector3(0f, 0f, -0.036f),
                    tickIndex % 2 == 0
                        ? new Vector3(0.010f, 0.030f, 0.006f)
                        : new Vector3(0.007f, 0.020f, 0.005f),
                    new Vector3(0f, 0f, angle),
                    service);
            }

            GameObject pivotObject = new GameObject("Fuel Gauge Needle Pivot");
            pivotObject.transform.SetParent(gaugeObject.transform, false);
            pivotObject.transform.localPosition = new Vector3(0f, 0f, -0.050f);
            Transform needlePivot = pivotObject.transform;
            CreateOrUpdatePrimitive(
                needlePivot, PrimitiveType.Cube, "Fuel Gauge Needle",
                new Vector3(0f, 0.055f, 0f), new Vector3(0.010f, 0.105f, 0.006f),
                Vector3.zero, service);
            CreateOrUpdatePrimitive(
                gaugeObject.transform, PrimitiveType.Sphere, "Fuel Gauge Needle Hub",
                new Vector3(0f, 0f, -0.054f), Vector3.one * 0.025f,
                Vector3.zero, metal);

            TextMesh title = CreateOrUpdateText(
                gaugeObject.transform, "Fuel Gauge Label", "FUEL",
                new Vector3(0f, 0.050f, -0.058f), 0.0105f, 56);
            TextMesh gallons = CreateOrUpdateText(
                gaugeObject.transform, "Fuel Gauge Gallon Readout", "-- / 269 GAL",
                new Vector3(0f, -0.054f, -0.058f), 0.0090f, 50);
            TextMesh percent = CreateOrUpdateText(
                gaugeObject.transform, "Fuel Gauge Percent Readout", "--%",
                new Vector3(0f, -0.082f, -0.058f), 0.0082f, 46);
            title.fontStyle = FontStyle.Bold;

            P51FuelQuantityGauge gauge = Undo.AddComponent<P51FuelQuantityGauge>(gaugeObject);
            gauge.Configure(fuelSystem, needlePivot, gallons, percent);
            EditorUtility.SetDirty(gauge);
            return gauge;
        }

        private static int FinalizeGunFit(Transform aircraft)
        {
            Transform armamentRoot = FindDescendant(aircraft, ArmamentRootName);
            if (armamentRoot == null)
            {
                return 0;
            }

            int repaired = 0;
            for (int wingIndex = 0; wingIndex < 2; wingIndex++)
            {
                string wingName = wingIndex == 0 ? "Left" : "Right";
                Transform interior = FindDescendant(
                    armamentRoot,
                    $"{wingName} Wing Armament Bay Interior");
                if (interior == null)
                {
                    continue;
                }

                for (int station = 1; station <= 3; station++)
                {
                    Transform gunTarget = FindDescendant(
                        interior,
                        $"{wingName} Gun Mount {station}");
                    Transform mountedGun = gunTarget != null
                        ? FindDescendant(gunTarget, InstalledGunName)
                        : null;
                    if (gunTarget == null || mountedGun == null)
                    {
                        continue;
                    }

                    // Only the rendered gun body is being contained. Muzzle/ejection anchors
                    // are siblings on the service target and deliberately remain untouched.
                    Undo.RecordObject(gunTarget, "Center P-51 gun inside wing bay");
                    Vector3 targetPosition = gunTarget.localPosition;
                    targetPosition.y = FinalGunMountLocalY;
                    gunTarget.localPosition = targetPosition;
                    EditorUtility.SetDirty(gunTarget);

                    Undo.RecordObject(mountedGun, "Reduce P-51 installed gun height");
                    mountedGun.localPosition = FinalGunLocalPosition;
                    mountedGun.localScale = FinalGunScale;
                    EditorUtility.SetDirty(mountedGun);
                    repaired++;
                }
            }

            return repaired;
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

            Collider[] colliders = part.GetComponents<Collider>();
            for (int index = colliders.Length - 1; index >= 0; index--)
            {
                if (colliders[index] != null)
                {
                    Object.DestroyImmediate(colliders[index]);
                }
            }

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
                EditorUtility.SetDirty(renderer);
            }
            EditorUtility.SetDirty(part.transform);
            return part.transform;
        }

        private static TextMesh CreateOrUpdateText(
            Transform parent,
            string objectName,
            string text,
            Vector3 localPosition,
            float characterSize,
            int fontSize)
        {
            Transform existing = FindDirectChild(parent, objectName);
            GameObject textObject;
            TextMesh textMesh;
            if (existing == null)
            {
                textObject = new GameObject(objectName);
                Undo.RegisterCreatedObjectUndo(textObject, $"Create {objectName}");
                textObject.transform.SetParent(parent, false);
                textMesh = textObject.AddComponent<TextMesh>();
            }
            else
            {
                textObject = existing.gameObject;
                textMesh = textObject.GetComponent<TextMesh>();
                if (textMesh == null)
                {
                    textMesh = Undo.AddComponent<TextMesh>(textObject);
                }
            }

            textObject.transform.localPosition = localPosition;
            textObject.transform.localRotation = Quaternion.identity;
            textObject.transform.localScale = Vector3.one;
            textMesh.text = text;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.characterSize = characterSize;
            textMesh.fontSize = fontSize;
            textMesh.color = Color.white;
            EditorUtility.SetDirty(textMesh);
            return textMesh;
        }

        private static int CountInstalledGuns(Transform armamentRoot)
        {
            if (armamentRoot == null)
            {
                return 0;
            }

            int count = 0;
            Transform[] all = armamentRoot.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < all.Length; index++)
            {
                if (all[index] != null && all[index].name == InstalledGunName)
                {
                    count++;
                }
            }
            return count;
        }

        private static bool Approximately(Vector3 a, Vector3 b, float tolerance)
        {
            return Mathf.Abs(a.x - b.x) <= tolerance
                && Mathf.Abs(a.y - b.y) <= tolerance
                && Mathf.Abs(a.z - b.z) <= tolerance;
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
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
            for (int index = 0; index < all.Length; index++)
            {
                if (all[index] != null && all[index].name == name)
                {
                    return all[index];
                }
            }
            return null;
        }
    }
}
