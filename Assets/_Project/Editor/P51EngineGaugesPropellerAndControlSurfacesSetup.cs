using System.Collections.Generic;
using System.Linq;
using Hanger51.Aircraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51EngineGaugesPropellerAndControlSurfacesSetup
    {
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string CoolantGaugeName = "P-51 Coolant Temperature Gauge";
        private const string OilGaugeName = "P-51 Oil Pressure Gauge";
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

        private const float CoolantGaugeOffset = -0.285f;
        private const float OilGaugeOffset = 0.285f;

        private readonly struct SurfaceAssetSet
        {
            internal readonly Mesh LeftWing;
            internal readonly Mesh RightWing;
            internal readonly Mesh LeftAileron;
            internal readonly Mesh RightAileron;
            internal readonly Vector3 LeftAileronPivot;
            internal readonly Vector3 RightAileronPivot;
            internal readonly Mesh LeftStabilizer;
            internal readonly Mesh RightStabilizer;
            internal readonly Mesh LeftElevator;
            internal readonly Mesh RightElevator;
            internal readonly Vector3 LeftElevatorPivot;
            internal readonly Vector3 RightElevatorPivot;
            internal readonly Mesh Fin;
            internal readonly Mesh Rudder;
            internal readonly Vector3 RudderPivot;

            internal SurfaceAssetSet(
                Mesh leftWing, Mesh rightWing, Mesh leftAileron, Mesh rightAileron,
                Vector3 leftAileronPivot, Vector3 rightAileronPivot,
                Mesh leftStabilizer, Mesh rightStabilizer, Mesh leftElevator, Mesh rightElevator,
                Vector3 leftElevatorPivot, Vector3 rightElevatorPivot,
                Mesh fin, Mesh rudder, Vector3 rudderPivot)
            {
                LeftWing = leftWing;
                RightWing = rightWing;
                LeftAileron = leftAileron;
                RightAileron = rightAileron;
                LeftAileronPivot = leftAileronPivot;
                RightAileronPivot = rightAileronPivot;
                LeftStabilizer = leftStabilizer;
                RightStabilizer = rightStabilizer;
                LeftElevator = leftElevator;
                RightElevator = rightElevator;
                LeftElevatorPivot = leftElevatorPivot;
                RightElevatorPivot = rightElevatorPivot;
                Fin = fin;
                Rudder = rudder;
                RudderPivot = rudderPivot;
            }
        }

        [MenuItem("Hanger 51/P-51 Mustang/85 - Add Engine Gauges, Align Propeller and Build Flight Controls")]
        public static void InstallEngineGaugesPropellerAndControls()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 Step 85 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("P-51 Step 85 failed. Open the saved Hanger 51 gameplay scene first.");
                return;
            }

            SurfaceAssetSet assets = BuildSurfaceAssets();
            if (assets.LeftWing == null || assets.RightWing == null
                || assets.LeftAileron == null || assets.RightAileron == null
                || assets.LeftStabilizer == null || assets.RightStabilizer == null
                || assets.LeftElevator == null || assets.RightElevator == null
                || assets.Fin == null || assets.Rudder == null)
            {
                Debug.LogError("P-51 Step 85 failed. One or more fixed/control-surface mesh assets could not be built.");
                return;
            }

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft == null || aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 85 failed. No P-51 aircraft were found in the scene.");
                return;
            }

            int aircraftUpdated = 0;
            int gaugesBuilt = 0;
            int propellersAligned = 0;
            int surfacesBuilt = 0;
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

                P51FuelQuantityGauge fuelGauge = FindAircraftFuelGauge(flight);
                P51RadiatorCoolingSystem cooling = flight.GetComponent<P51RadiatorCoolingSystem>();
                if (fuelGauge == null || cooling == null)
                {
                    Debug.LogWarning(
                        $"P-51 Step 85 skipped '{flight.name}' because its moved fuel gauge or radiator cooling system is missing.",
                        flight);
                    continue;
                }

                gaugesBuilt += BuildEngineGaugesFromMovedFuelGauge(flight, fuelGauge, cooling);
                propellersAligned += AlignPropellerBlades(flight);
                surfacesBuilt += InstallControlSurfaces(flight, assets);

                EditorUtility.SetDirty(flight);
                aircraftUpdated++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 85 made the changes but Unity could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 Step 85 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = master != null ? master.gameObject : aircraft[0].gameObject;
            Debug.Log(
                $"P-51 Step 85 complete. Updated {aircraftUpdated} aircraft, built {gaugesBuilt} live engine gauge(s), "
                + $"aligned {propellersAligned} four-blade propeller set(s), and built {surfacesBuilt} complete movable control-surface set(s). "
                + "The user's moved fuel gauge was not repositioned; coolant/oil instruments were cloned from it and placed relative to its current transform. "
                + "Fixed wing/stabilizer/fin skins now stop at their hinge lines, while separate ailerons, elevators and rudder animate from the existing pilot controls.",
                master != null ? master.gameObject : null);
        }

        [MenuItem("Hanger 51/P-51 Mustang/86 - Validate Engine Gauges, Propeller and Flight Controls")]
        public static void ValidateEngineGaugesPropellerAndControls()
        {
            bool passed = true;
            int checkedAircraft = 0;

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft == null || aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 86 failed. No P-51 aircraft were found.");
                return;
            }

            for (int i = 0; i < aircraft.Length; i++)
            {
                P51FlightController flight = aircraft[i];
                if (flight == null || !flight.gameObject.scene.IsValid())
                {
                    continue;
                }

                checkedAircraft++;
                P51FuelQuantityGauge fuel = FindAircraftFuelGauge(flight);
                P51RadiatorCoolingSystem cooling = flight.GetComponent<P51RadiatorCoolingSystem>();
                P51CoolantTemperatureGauge coolantGauge = flight.GetComponentInChildren<P51CoolantTemperatureGauge>(true);
                P51OilPressureGauge oilGauge = flight.GetComponentInChildren<P51OilPressureGauge>(true);
                P51ControlSurfaceVisualController controls = flight.GetComponent<P51ControlSurfaceVisualController>();

                if (fuel == null || cooling == null || coolantGauge == null || oilGauge == null
                    || !coolantGauge.IsConfigured || !oilGauge.IsConfigured
                    || coolantGauge.CoolingSystem != cooling || oilGauge.CoolingSystem != cooling)
                {
                    Debug.LogError(
                        $"P-51 Step 86 failed. '{flight.name}' does not have both configured engine gauges reading its own cooling/installed-engine system.",
                        flight);
                    passed = false;
                }

                if (fuel != null && coolantGauge != null && oilGauge != null)
                {
                    if (fuel.transform.parent != coolantGauge.transform.parent
                        || fuel.transform.parent != oilGauge.transform.parent)
                    {
                        Debug.LogError($"P-51 Step 86 failed. '{flight.name}' engine gauges are not mounted beside the user's fuel gauge.", flight);
                        passed = false;
                    }
                }

                List<Transform> blades = FindPropellerBlades(flight);
                if (blades.Count != 4)
                {
                    Debug.LogError($"P-51 Step 86 failed. '{flight.name}' has {blades.Count} propeller blades; expected exactly 4.", flight);
                    passed = false;
                }
                else
                {
                    for (int bladeIndex = 0; bladeIndex < blades.Count; bladeIndex++)
                    {
                        Transform blade = blades[bladeIndex];
                        if (blade.localPosition.sqrMagnitude > 0.0001f
                            || Mathf.Abs(Mathf.DeltaAngle(blade.localEulerAngles.z, bladeIndex * 90f)) > 0.5f)
                        {
                            Debug.LogError(
                                $"P-51 Step 86 failed. '{flight.name}' propeller blade {bladeIndex + 1} is not centered/coplanar at its 90-degree station.",
                                blade);
                            passed = false;
                        }
                    }
                }

                Transform leftAileron = FindDescendant(flight.transform, LeftAileronName);
                Transform rightAileron = FindDescendant(flight.transform, RightAileronName);
                Transform leftElevator = FindDescendant(flight.transform, LeftElevatorName);
                Transform rightElevator = FindDescendant(flight.transform, RightElevatorName);
                Transform rudder = FindDescendant(flight.transform, RudderName);
                if (controls == null || !controls.IsConfigured
                    || leftAileron == null || rightAileron == null
                    || leftElevator == null || rightElevator == null || rudder == null)
                {
                    Debug.LogError($"P-51 Step 86 failed. '{flight.name}' is missing configured movable flight-control surfaces.", flight);
                    passed = false;
                }
                else
                {
                    Collider[] movableColliders = new[] { leftAileron, rightAileron, leftElevator, rightElevator, rudder }
                        .SelectMany(t => t.GetComponentsInChildren<Collider>(true)).ToArray();
                    if (movableColliders.Length != 0)
                    {
                        Debug.LogError($"P-51 Step 86 failed. '{flight.name}' movable control surfaces are visual-only but contain {movableColliders.Length} collider(s).", flight);
                        passed = false;
                    }
                }

                if (!UsesFixedMesh(flight, "P-51D Left Wing Fixed")
                    || !UsesFixedMesh(flight, "P-51D Right Wing Fixed")
                    || !UsesFixedMesh(flight, "P-51D Left Stabilizer Fixed")
                    || !UsesFixedMesh(flight, "P-51D Right Stabilizer Fixed")
                    || !UsesFixedMesh(flight, "P-51D Vertical Fin Fixed"))
                {
                    Debug.LogError($"P-51 Step 86 failed. '{flight.name}' still has one or more full trailing-edge skins instead of the hinge-cut fixed meshes.", flight);
                    passed = false;
                }
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("P-51 Step 86 failed. Standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"P-51 Step 86 passed. Aircraft checked={checkedAircraft}. Coolant temperature and oil-pressure gauges are live beside each aircraft's moved fuel gauge; "
                    + "all four propeller blades are centered, coplanar and spaced exactly 90 degrees apart; fixed wing/tail meshes terminate at hinge lines; "
                    + "and left/right ailerons, elevators and rudder exist as collider-free movable surfaces driven by the current pilot inputs.");
            }
        }

        private static int BuildEngineGaugesFromMovedFuelGauge(
            P51FlightController flight,
            P51FuelQuantityGauge fuelGauge,
            P51RadiatorCoolingSystem cooling)
        {
            DestroyDescendantsNamed(flight.transform, CoolantGaugeName, fuelGauge.transform);
            DestroyDescendantsNamed(flight.transform, OilGaugeName, fuelGauge.transform);

            GaugeClone coolant = CloneFuelGaugeVisual(fuelGauge, CoolantGaugeName, CoolantGaugeOffset, "COOLANT");
            GaugeClone oil = CloneFuelGaugeVisual(fuelGauge, OilGaugeName, OilGaugeOffset, "OIL PRESS");
            if (!coolant.IsValid || !oil.IsValid)
            {
                if (coolant.Root != null) Undo.DestroyObjectImmediate(coolant.Root.gameObject);
                if (oil.Root != null) Undo.DestroyObjectImmediate(oil.Root.gameObject);
                return 0;
            }

            P51CoolantTemperatureGauge coolantGauge = Undo.AddComponent<P51CoolantTemperatureGauge>(coolant.Root.gameObject);
            coolantGauge.Configure(cooling, coolant.Needle, coolant.PrimaryReadout, coolant.SecondaryReadout);
            coolant.PrimaryReadout.text = $"{cooling.CoolantTemperatureC:0} C";
            coolant.SecondaryReadout.text = "NORMAL";

            P51OilPressureGauge oilGauge = Undo.AddComponent<P51OilPressureGauge>(oil.Root.gameObject);
            oilGauge.Configure(flight, cooling, oil.Needle, oil.PrimaryReadout, oil.SecondaryReadout);
            oil.PrimaryReadout.text = "0 PSI";
            oil.SecondaryReadout.text = "ENGINE OFF";

            RemoveColliders(coolant.Root.gameObject);
            RemoveColliders(oil.Root.gameObject);
            EditorUtility.SetDirty(coolantGauge);
            EditorUtility.SetDirty(oilGauge);
            return 2;
        }

        private readonly struct GaugeClone
        {
            internal readonly Transform Root;
            internal readonly Transform Needle;
            internal readonly TextMesh PrimaryReadout;
            internal readonly TextMesh SecondaryReadout;
            internal bool IsValid => Root != null && Needle != null && PrimaryReadout != null && SecondaryReadout != null;

            internal GaugeClone(Transform root, Transform needle, TextMesh primary, TextMesh secondary)
            {
                Root = root;
                Needle = needle;
                PrimaryReadout = primary;
                SecondaryReadout = secondary;
            }
        }

        private static GaugeClone CloneFuelGaugeVisual(
            P51FuelQuantityGauge source,
            string cloneName,
            float localXOffset,
            string title)
        {
            GameObject clone = Object.Instantiate(source.gameObject, source.transform.parent);
            Undo.RegisterCreatedObjectUndo(clone, $"Create {cloneName}");
            clone.name = cloneName;
            clone.SetActive(true);
            Transform root = clone.transform;
            root.localPosition = source.transform.localPosition
                + source.transform.localRotation * new Vector3(localXOffset, 0f, 0f);
            root.localRotation = source.transform.localRotation;
            root.localScale = source.transform.localScale;

            P51FuelQuantityGauge clonedFuel = clone.GetComponent<P51FuelQuantityGauge>();
            if (clonedFuel == null)
            {
                return new GaugeClone(root, null, null, null);
            }

            SerializedObject serialized = new SerializedObject(clonedFuel);
            Transform needle = serialized.FindProperty("needlePivot")?.objectReferenceValue as Transform;
            TextMesh primary = serialized.FindProperty("gallonReadout")?.objectReferenceValue as TextMesh;
            TextMesh secondary = serialized.FindProperty("percentReadout")?.objectReferenceValue as TextMesh;

            TextMesh[] textMeshes = clone.GetComponentsInChildren<TextMesh>(true);
            for (int i = 0; i < textMeshes.Length; i++)
            {
                TextMesh text = textMeshes[i];
                if (text == null || text == primary || text == secondary)
                {
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(text.text) && text.text.ToUpperInvariant().Contains("FUEL"))
                {
                    text.text = title;
                    EditorUtility.SetDirty(text);
                }
            }

            Undo.DestroyObjectImmediate(clonedFuel);
            return new GaugeClone(root, needle, primary, secondary);
        }

        private static int AlignPropellerBlades(P51FlightController flight)
        {
            List<Transform> blades = FindPropellerBlades(flight);
            if (blades.Count != 4)
            {
                Debug.LogWarning($"P-51 Step 85 found {blades.Count} propeller blades on '{flight.name}', so it did not force four-blade alignment.", flight);
                return 0;
            }

            blades = blades.OrderBy(t => t.name).ToList();
            for (int i = 0; i < blades.Count; i++)
            {
                Transform blade = blades[i];
                Undo.RecordObject(blade, "Align P-51 propeller blade");
                blade.localPosition = Vector3.zero;
                blade.localRotation = Quaternion.Euler(0f, 0f, i * 90f);
                blade.localScale = Vector3.one;
                EditorUtility.SetDirty(blade);
            }
            return 1;
        }

        private static List<Transform> FindPropellerBlades(P51FlightController flight)
        {
            List<Transform> blades = new List<Transform>();
            if (flight == null || flight.PropellerRoot == null)
            {
                return blades;
            }

            MeshFilter[] filters = flight.PropellerRoot.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                if (filter != null && filter.sharedMesh != null
                    && filter.sharedMesh.name.Contains("Propeller Blade"))
                {
                    blades.Add(filter.transform);
                }
            }
            return blades;
        }

        private static int InstallControlSurfaces(P51FlightController flight, SurfaceAssetSet assets)
        {
            Transform leftWing = FindMeshOwner(flight.transform, "P-51D Left Wing", "P-51D Left Wing Fixed");
            Transform rightWing = FindMeshOwner(flight.transform, "P-51D Right Wing", "P-51D Right Wing Fixed");
            Transform leftStab = FindMeshOwner(flight.transform, "P-51D Left Stabilizer", "P-51D Left Stabilizer Fixed");
            Transform rightStab = FindMeshOwner(flight.transform, "P-51D Right Stabilizer", "P-51D Right Stabilizer Fixed");
            Transform fin = FindMeshOwner(flight.transform, "P-51D Vertical Fin", "P-51D Vertical Fin Fixed");
            if (leftWing == null || rightWing == null || leftStab == null || rightStab == null || fin == null)
            {
                Debug.LogWarning($"P-51 Step 85 could not find all canonical wing/tail mesh owners on '{flight.name}'.", flight);
                return 0;
            }

            AssignFixedMesh(leftWing, assets.LeftWing);
            AssignFixedMesh(rightWing, assets.RightWing);
            AssignFixedMesh(leftStab, assets.LeftStabilizer);
            AssignFixedMesh(rightStab, assets.RightStabilizer);
            AssignFixedMesh(fin, assets.Fin);

            Transform leftAileron = BuildSurfacePivot(leftWing, LeftAileronName, assets.LeftAileronPivot, assets.LeftAileron, GetMaterial(leftWing));
            Transform rightAileron = BuildSurfacePivot(rightWing, RightAileronName, assets.RightAileronPivot, assets.RightAileron, GetMaterial(rightWing));
            Transform leftElevator = BuildSurfacePivot(leftStab, LeftElevatorName, assets.LeftElevatorPivot, assets.LeftElevator, GetMaterial(leftStab));
            Transform rightElevator = BuildSurfacePivot(rightStab, RightElevatorName, assets.RightElevatorPivot, assets.RightElevator, GetMaterial(rightStab));
            Transform rudder = BuildSurfacePivot(fin, RudderName, assets.RudderPivot, assets.Rudder, GetMaterial(fin));

            if (leftAileron == null || rightAileron == null || leftElevator == null || rightElevator == null || rudder == null)
            {
                return 0;
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
            return 1;
        }

        private static void AssignFixedMesh(Transform owner, Mesh mesh)
        {
            MeshFilter filter = owner != null ? owner.GetComponent<MeshFilter>() : null;
            if (filter == null || mesh == null)
            {
                return;
            }
            Undo.RecordObject(filter, "Use P-51 hinge-cut fixed surface mesh");
            filter.sharedMesh = mesh;
            EditorUtility.SetDirty(filter);
        }

        private static Transform BuildSurfacePivot(
            Transform fixedOwner,
            string pivotName,
            Vector3 pivotLocalPosition,
            Mesh surfaceMesh,
            Material material)
        {
            Transform old = FindDirectChild(fixedOwner, pivotName);
            if (old != null)
            {
                Undo.DestroyObjectImmediate(old.gameObject);
            }

            GameObject pivotObject = new GameObject(pivotName);
            Undo.RegisterCreatedObjectUndo(pivotObject, $"Create {pivotName}");
            Transform pivot = pivotObject.transform;
            pivot.SetParent(fixedOwner, false);
            pivot.localPosition = pivotLocalPosition;
            pivot.localRotation = Quaternion.identity;
            pivot.localScale = Vector3.one;

            GameObject visual = new GameObject(pivotName.Replace(" Pivot", " Visual"));
            visual.transform.SetParent(pivot, false);
            MeshFilter filter = visual.AddComponent<MeshFilter>();
            filter.sharedMesh = surfaceMesh;
            MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.receiveShadows = true;
            RemoveColliders(pivotObject);
            return pivot;
        }

        private static SurfaceAssetSet BuildSurfaceAssets()
        {
            Mesh leftWing = BuildFixedWing(LeftFixedWingPath, true);
            Mesh rightWing = BuildFixedWing(RightFixedWingPath, false);
            Mesh leftAileron = BuildAileron(LeftAileronPath, true, out Vector3 leftAileronPivot);
            Mesh rightAileron = BuildAileron(RightAileronPath, false, out Vector3 rightAileronPivot);

            Mesh leftStab = BuildFixedStabilizer(LeftFixedStabilizerPath, true);
            Mesh rightStab = BuildFixedStabilizer(RightFixedStabilizerPath, false);
            Mesh leftElevator = BuildElevator(LeftElevatorPath, true, out Vector3 leftElevatorPivot);
            Mesh rightElevator = BuildElevator(RightElevatorPath, false, out Vector3 rightElevatorPivot);

            Mesh fin = BuildFixedFin(FixedFinPath);
            Mesh rudder = BuildRudder(RudderPath, out Vector3 rudderPivot);

            return new SurfaceAssetSet(
                leftWing, rightWing, leftAileron, rightAileron,
                leftAileronPivot, rightAileronPivot,
                leftStab, rightStab, leftElevator, rightElevator,
                leftElevatorPivot, rightElevatorPivot,
                fin, rudder, rudderPivot);
        }

        private static Mesh BuildFixedWing(string path, bool left)
        {
            float[] spans = { 0.38f, 3.15f, 3.25f, 5.35f, 5.64f };
            List<Vector3> vertices = new List<Vector3>();
            for (int i = 0; i < spans.Length; i++)
            {
                SampleWing(spans[i], out float leading, out float originalTrailing, out float centerY, out float thickness);
                float trailing = originalTrailing;
                if (i == 2 || i == 3)
                {
                    trailing = AileronHingeZ(spans[i]);
                }
                AddWingStation(vertices, left, spans[i], leading, trailing, centerY, thickness);
            }
            List<int> triangles = BuildStationPrismTriangles(spans.Length);
            return SaveMesh(path, vertices, triangles, left ? "P-51D Left Wing Fixed" : "P-51D Right Wing Fixed");
        }

        private static Mesh BuildAileron(string path, bool left, out Vector3 pivot)
        {
            float[] spans = { 3.25f, 5.35f };
            List<Vector3> aircraftVertices = new List<Vector3>();
            Vector3[] hingePoints = new Vector3[2];
            for (int i = 0; i < spans.Length; i++)
            {
                SampleWing(spans[i], out _, out float trailing, out float centerY, out float thickness);
                float hinge = AileronHingeZ(spans[i]);
                AddWingStation(aircraftVertices, left, spans[i], hinge, trailing, centerY, thickness * 0.72f);
                hingePoints[i] = new Vector3((left ? -1f : 1f) * spans[i], centerY, hinge);
            }
            pivot = (hingePoints[0] + hingePoints[1]) * 0.5f;
            for (int i = 0; i < aircraftVertices.Count; i++) aircraftVertices[i] -= pivot;
            return SaveMesh(path, aircraftVertices, BuildStationPrismTriangles(2), left ? "P-51D Left Aileron" : "P-51D Right Aileron");
        }

        private static void SampleWing(float span, out float leading, out float trailing, out float centerY, out float thickness)
        {
            float[] spans = { 0.38f, 3.15f, 5.64f };
            float[] leadingValues = { 1.18f, 0.69f, 0.18f };
            float[] trailingValues = { -1.36f, -0.94f, -0.54f };
            float[] yValues = { 1.24f, 1.35f, 1.48f };
            float[] thicknessValues = { 0.22f, 0.14f, 0.065f };
            InterpolateStations(span, spans, leadingValues, out leading);
            InterpolateStations(span, spans, trailingValues, out trailing);
            InterpolateStations(span, spans, yValues, out centerY);
            InterpolateStations(span, spans, thicknessValues, out thickness);
        }

        private static float AileronHingeZ(float span)
        {
            SampleWing(span, out float leading, out float trailing, out _, out _);
            return trailing + (leading - trailing) * 0.265f;
        }

        private static void AddWingStation(
            List<Vector3> vertices, bool left, float span, float leading, float trailing, float centerY, float thickness)
        {
            float x = (left ? -1f : 1f) * span;
            vertices.Add(new Vector3(x, centerY + thickness, leading));
            vertices.Add(new Vector3(x, centerY + thickness * 0.58f, trailing));
            vertices.Add(new Vector3(x, centerY - thickness, leading));
            vertices.Add(new Vector3(x, centerY - thickness * 0.62f, trailing));
        }

        private static Mesh BuildFixedStabilizer(string path, bool left)
        {
            float sign = left ? -1f : 1f;
            float rootX = 0.30f;
            float tipX = 2.15f;
            float rootLeading = -3.52f;
            float rootTrailing = -4.58f;
            float tipLeading = -3.86f;
            float tipTrailing = -4.52f;
            float rootHinge = rootTrailing + (rootLeading - rootTrailing) * 0.30f;
            float tipHinge = tipTrailing + (tipLeading - tipTrailing) * 0.30f;
            float rootY = 1.78f;
            float tipY = 1.88f;
            float thickness = 0.065f;

            List<Vector3> vertices = new List<Vector3>();
            AddTailStation(vertices, sign * rootX, rootLeading, rootHinge, rootY, thickness);
            AddTailStation(vertices, sign * tipX, tipLeading, tipHinge, tipY, thickness * 0.55f);
            return SaveMesh(path, vertices, BuildStationPrismTriangles(2), left ? "P-51D Left Stabilizer Fixed" : "P-51D Right Stabilizer Fixed");
        }

        private static Mesh BuildElevator(string path, bool left, out Vector3 pivot)
        {
            float sign = left ? -1f : 1f;
            float rootX = 0.30f;
            float tipX = 2.15f;
            float rootLeading = -3.52f;
            float rootTrailing = -4.58f;
            float tipLeading = -3.86f;
            float tipTrailing = -4.52f;
            float rootHinge = rootTrailing + (rootLeading - rootTrailing) * 0.30f;
            float tipHinge = tipTrailing + (tipLeading - tipTrailing) * 0.30f;
            float rootY = 1.78f;
            float tipY = 1.88f;
            float thickness = 0.052f;

            Vector3 rootPivot = new Vector3(sign * rootX, rootY, rootHinge);
            Vector3 tipPivot = new Vector3(sign * tipX, tipY, tipHinge);
            pivot = (rootPivot + tipPivot) * 0.5f;

            List<Vector3> vertices = new List<Vector3>();
            AddTailStation(vertices, sign * rootX, rootHinge, rootTrailing, rootY, thickness);
            AddTailStation(vertices, sign * tipX, tipHinge, tipTrailing, tipY, thickness * 0.55f);
            for (int i = 0; i < vertices.Count; i++) vertices[i] -= pivot;
            return SaveMesh(path, vertices, BuildStationPrismTriangles(2), left ? "P-51D Left Elevator" : "P-51D Right Elevator");
        }

        private static void AddTailStation(List<Vector3> vertices, float x, float leading, float trailing, float y, float thickness)
        {
            vertices.Add(new Vector3(x, y + thickness, leading));
            vertices.Add(new Vector3(x, y + thickness * 0.5f, trailing));
            vertices.Add(new Vector3(x, y - thickness, leading));
            vertices.Add(new Vector3(x, y - thickness * 0.5f, trailing));
        }

        private static Mesh BuildFixedFin(string path)
        {
            Vector2[] profile =
            {
                new Vector2(-3.62f, 1.64f),
                new Vector2(-3.96f, 3.28f),
                new Vector2(-4.25f, 3.70f),
                new Vector2(-4.39f, 3.52f),
                new Vector2(-4.42f, 1.67f)
            };
            return BuildExtrudedProfile(path, profile, 0.09f, Vector3.zero, "P-51D Vertical Fin Fixed");
        }

        private static Mesh BuildRudder(string path, out Vector3 pivot)
        {
            Vector2 hingeBottom = new Vector2(-4.42f, 1.67f);
            Vector2 hingeTop = new Vector2(-4.39f, 3.52f);
            Vector2[] profile =
            {
                hingeBottom,
                hingeTop,
                new Vector2(-4.52f, 3.55f),
                new Vector2(-4.70f, 2.35f),
                new Vector2(-4.66f, 1.67f)
            };
            pivot = new Vector3(0f, (hingeBottom.y + hingeTop.y) * 0.5f, (hingeBottom.x + hingeTop.x) * 0.5f);
            return BuildExtrudedProfile(path, profile, 0.075f, pivot, "P-51D Rudder");
        }

        private static Mesh BuildExtrudedProfile(string path, Vector2[] profile, float halfThickness, Vector3 pivot, string meshName)
        {
            List<Vector3> vertices = new List<Vector3>();
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? -halfThickness : halfThickness;
                for (int i = 0; i < profile.Length; i++)
                {
                    vertices.Add(new Vector3(x, profile[i].y, profile[i].x) - pivot);
                }
            }

            List<int> triangles = new List<int>();
            for (int i = 1; i < profile.Length - 1; i++)
            {
                triangles.Add(0); triangles.Add(i + 1); triangles.Add(i);
                triangles.Add(profile.Length); triangles.Add(profile.Length + i); triangles.Add(profile.Length + i + 1);
            }
            for (int i = 0; i < profile.Length; i++)
            {
                int next = (i + 1) % profile.Length;
                AddQuad(triangles, i, next, profile.Length + next, profile.Length + i);
            }
            return SaveMesh(path, vertices, triangles, meshName);
        }

        private static List<int> BuildStationPrismTriangles(int stations)
        {
            List<int> triangles = new List<int>();
            for (int station = 0; station < stations - 1; station++)
            {
                int a = station * 4;
                int b = (station + 1) * 4;
                AddQuad(triangles, a, b, b + 1, a + 1);
                AddQuad(triangles, a + 3, b + 3, b + 2, a + 2);
                AddQuad(triangles, a + 2, b + 2, b, a);
                AddQuad(triangles, a + 1, b + 1, b + 3, a + 3);
            }
            AddQuad(triangles, 0, 1, 3, 2);
            int tip = (stations - 1) * 4;
            AddQuad(triangles, tip + 2, tip + 3, tip + 1, tip);
            return triangles;
        }

        private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(a); triangles.Add(c); triangles.Add(d);
        }

        private static void InterpolateStations(float value, float[] keys, float[] values, out float result)
        {
            if (value <= keys[0]) { result = values[0]; return; }
            int last = keys.Length - 1;
            if (value >= keys[last]) { result = values[last]; return; }
            for (int i = 0; i < last; i++)
            {
                if (value >= keys[i] && value <= keys[i + 1])
                {
                    float t = Mathf.InverseLerp(keys[i], keys[i + 1], value);
                    result = Mathf.Lerp(values[i], values[i + 1], t);
                    return;
                }
            }
            result = values[last];
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
                Undo.RegisterCompleteObjectUndo(mesh, $"Rebuild {meshName}");
                mesh.Clear();
            }
            mesh.name = meshName;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static P51FuelQuantityGauge FindAircraftFuelGauge(P51FlightController flight)
        {
            P51FuelSystem fuelSystem = flight.GetComponent<P51FuelSystem>();
            P51FuelQuantityGauge[] gauges = flight.GetComponentsInChildren<P51FuelQuantityGauge>(true);
            for (int i = 0; i < gauges.Length; i++)
            {
                P51FuelQuantityGauge gauge = gauges[i];
                if (gauge != null && gauge.FuelSystem == fuelSystem && gauge.gameObject.activeSelf)
                {
                    return gauge;
                }
            }
            return gauges.FirstOrDefault(g => g != null && g.gameObject.activeSelf);
        }

        private static Transform FindMeshOwner(Transform root, params string[] meshNames)
        {
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                if (filter == null || filter.sharedMesh == null) continue;
                for (int nameIndex = 0; nameIndex < meshNames.Length; nameIndex++)
                {
                    if (filter.sharedMesh.name == meshNames[nameIndex]) return filter.transform;
                }
            }
            return null;
        }

        private static Material GetMaterial(Transform owner)
        {
            Renderer renderer = owner != null ? owner.GetComponent<Renderer>() : null;
            return renderer != null ? renderer.sharedMaterial : null;
        }

        private static bool UsesFixedMesh(P51FlightController flight, string meshName)
        {
            return FindMeshOwner(flight.transform, meshName) != null;
        }

        private static void RemoveColliders(GameObject root)
        {
            if (root == null) return;
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = colliders.Length - 1; i >= 0; i--)
            {
                if (colliders[i] != null) Object.DestroyImmediate(colliders[i]);
            }
        }

        private static void DestroyDescendantsNamed(Transform root, string name, Transform preserve)
        {
            if (root == null) return;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = all.Length - 1; i >= 0; i--)
            {
                Transform current = all[i];
                if (current != null && current != preserve && current.name == name)
                {
                    Undo.DestroyObjectImmediate(current.gameObject);
                }
            }
        }

        private static Transform FindDirectChild(Transform root, string name)
        {
            if (root == null) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child != null && child.name == name) return child;
            }
            return null;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null) return null;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name) return all[i];
            }
            return null;
        }
    }
}
