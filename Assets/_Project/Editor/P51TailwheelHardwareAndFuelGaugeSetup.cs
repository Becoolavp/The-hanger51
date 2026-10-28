using Hanger51.Aircraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class P51TailwheelHardwareAndFuelGaugeSetup
    {
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string GearSystemRootName = "P-51 Serviceable Retractable Landing Gear";
        private const string GaugeName = "P-51 Main Fuel Quantity Gauge";

        private const string MetalMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/PolishedAluminum.mat";
        private const string DarkMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/DarkAircraftMetal.mat";
        private const string ServiceMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/ServiceHardware.mat";

        [MenuItem("Hanger 51/P-51 Mustang/59 - Align Tailwheel Wheel Hardware and Add Fuel Gauge")]
        public static void AlignTailwheelAndAddFuelGauge()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 Step 59 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            GameObject aircraft = GameObject.Find(AircraftRootName);
            if (!scene.IsValid()
                || string.IsNullOrWhiteSpace(scene.path)
                || aircraft == null)
            {
                Debug.LogError(
                    "P-51 Step 59 failed. Open the saved movement-test scene containing the master P-51.");
                return;
            }

            P51FuelSystem fuelSystem = aircraft.GetComponent<P51FuelSystem>();
            P51LandingGearMaintenanceController maintenance =
                aircraft.GetComponent<P51LandingGearMaintenanceController>();
            P51LandingGearVisualSuspensionFollower visualFollower =
                aircraft.GetComponent<P51LandingGearVisualSuspensionFollower>();
            P51LandingGearServiceAttachmentFollower serviceFollower =
                aircraft.GetComponent<P51LandingGearServiceAttachmentFollower>();
            P51PilotSeat[] seats = aircraft.GetComponentsInChildren<P51PilotSeat>(true);
            P51PilotSeat seat = seats.Length > 0 ? seats[0] : null;

            if (fuelSystem == null
                || maintenance == null
                || visualFollower == null
                || seat == null
                || seat.CameraAnchor == null)
            {
                Debug.LogError(
                    "P-51 Step 59 failed. Fuel system, landing-gear follower, maintenance controller, or pilot camera anchor is missing.",
                    aircraft);
                return;
            }

            if (serviceFollower == null)
            {
                serviceFollower = Undo.AddComponent<P51LandingGearServiceAttachmentFollower>(aircraft);
            }
            serviceFollower.RepairHierarchy();

            Transform gearSystem = aircraft.transform.Find(GearSystemRootName);
            Transform tailTire = FindDescendant(gearSystem, "Tailwheel Tire Visual");
            Transform tailRim = FindDescendant(gearSystem, "Tailwheel Rim Visual");
            Transform tailBoltTarget = FindDescendant(
                gearSystem,
                "Tailwheel Large Mount Bolt Service Target");
            Transform tailValveTarget = FindDescendant(
                gearSystem,
                "Tailwheel Tire and Valve Service Target");

            if (tailTire == null
                || tailRim == null
                || tailBoltTarget == null
                || tailValveTarget == null)
            {
                Debug.LogError(
                    "P-51 Step 59 failed. The tailwheel tire, rim, bolt target, or valve target could not be found.",
                    aircraft);
                return;
            }

            // Persist the corrected lower-wheel service hierarchy immediately. Runtime
            // suspension will move the rim and tire together; the bolt follows the rim while
            // the valve follows the rubber tire.
            tailBoltTarget.SetParent(tailRim, false);
            tailBoltTarget.localPosition = Vector3.zero;
            tailBoltTarget.localRotation = Quaternion.identity;
            tailBoltTarget.localScale = Vector3.one;

            tailValveTarget.SetParent(tailTire, false);
            tailValveTarget.localPosition = Vector3.zero;
            tailValveTarget.localRotation = Quaternion.identity;
            tailValveTarget.localScale = Vector3.one;

            Material metal = AssetDatabase.LoadAssetAtPath<Material>(MetalMaterialPath);
            Material dark = AssetDatabase.LoadAssetAtPath<Material>(DarkMaterialPath);
            Material service = AssetDatabase.LoadAssetAtPath<Material>(ServiceMaterialPath);
            if (metal == null || dark == null || service == null)
            {
                Debug.LogError(
                    "P-51 Step 59 failed. Existing P-51 cockpit materials are missing.",
                    aircraft);
                return;
            }

            BuildFuelGauge(seat.CameraAnchor, fuelSystem, metal, dark, service);

            EditorUtility.SetDirty(aircraft);
            EditorUtility.SetDirty(fuelSystem);
            EditorUtility.SetDirty(maintenance);
            EditorUtility.SetDirty(visualFollower);
            EditorUtility.SetDirty(serviceFollower);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 59 made the changes but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 Step 59 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = aircraft;
            Debug.Log(
                "P-51 Step 59 complete. The tailwheel rim/hub now follows the grounded tire, the tailwheel bolt follows the rim, the valve target follows the tire, and a live 0-269 gallon main-fuel gauge was added to the cockpit.",
                aircraft);
        }

        [MenuItem("Hanger 51/P-51 Mustang/60 - Validate Tailwheel Wheel Hardware and Fuel Gauge")]
        public static void ValidateTailwheelAndFuelGauge()
        {
            bool passed = true;
            GameObject aircraft = GameObject.Find(AircraftRootName);
            if (aircraft == null)
            {
                Debug.LogError("P-51 Step 60 failed. Master P-51 is missing.");
                return;
            }

            Transform gearSystem = aircraft.transform.Find(GearSystemRootName);
            Transform tailTire = FindDescendant(gearSystem, "Tailwheel Tire Visual");
            Transform tailRim = FindDescendant(gearSystem, "Tailwheel Rim Visual");
            Transform tailBoltTarget = FindDescendant(
                gearSystem,
                "Tailwheel Large Mount Bolt Service Target");
            Transform tailValveTarget = FindDescendant(
                gearSystem,
                "Tailwheel Tire and Valve Service Target");

            if (tailTire == null || tailRim == null)
            {
                Debug.LogError("P-51 Step 60 failed. Tailwheel tire or rim hierarchy is missing.", aircraft);
                passed = false;
            }
            if (tailBoltTarget == null || tailBoltTarget.parent != tailRim)
            {
                Debug.LogError(
                    "P-51 Step 60 failed. Tailwheel bolt/service assembly is not attached to the moving rim.",
                    aircraft);
                passed = false;
            }
            if (tailValveTarget == null || tailValveTarget.parent != tailTire)
            {
                Debug.LogError(
                    "P-51 Step 60 failed. Tailwheel tire/valve target is not attached to the moving tire.",
                    aircraft);
                passed = false;
            }

            P51LandingGearVisualSuspensionFollower visualFollower =
                aircraft.GetComponent<P51LandingGearVisualSuspensionFollower>();
            P51LandingGearServiceAttachmentFollower serviceFollower =
                aircraft.GetComponent<P51LandingGearServiceAttachmentFollower>();
            if (visualFollower == null || serviceFollower == null)
            {
                Debug.LogError(
                    "P-51 Step 60 failed. Tailwheel suspension/service followers are missing.",
                    aircraft);
                passed = false;
            }

            P51FuelSystem fuelSystem = aircraft.GetComponent<P51FuelSystem>();
            P51FuelQuantityGauge gauge =
                aircraft.GetComponentInChildren<P51FuelQuantityGauge>(true);
            if (fuelSystem == null || gauge == null || !gauge.IsConfigured)
            {
                Debug.LogError(
                    "P-51 Step 60 failed. Fuel system or configured cockpit fuel gauge is missing.",
                    aircraft);
                passed = false;
            }
            else
            {
                if (gauge.FuelSystem != fuelSystem)
                {
                    Debug.LogError(
                        "P-51 Step 60 failed. Cockpit gauge is not reading this aircraft's own fuel system.",
                        gauge);
                    passed = false;
                }
                if (Mathf.Abs(fuelSystem.TotalCapacityGallons - 269f) > 0.1f)
                {
                    Debug.LogError(
                        $"P-51 Step 60 failed. Expected 269-gallon main tank; found {fuelSystem.TotalCapacityGallons:F1} gallons.",
                        fuelSystem);
                    passed = false;
                }
            }

            P51PilotSeat[] seats = aircraft.GetComponentsInChildren<P51PilotSeat>(true);
            P51PilotSeat seat = seats.Length > 0 ? seats[0] : null;
            Transform gaugeRoot = FindDescendant(aircraft.transform, GaugeName);
            if (seat == null
                || seat.CameraAnchor == null
                || gaugeRoot == null
                || gaugeRoot.parent != seat.CameraAnchor)
            {
                Debug.LogError(
                    "P-51 Step 60 failed. Fuel gauge is not installed at the pilot cockpit camera/instrument position.",
                    aircraft);
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("P-51 Step 60 failed. Standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "P-51 Step 60 passed. Tailwheel tire/rim/bolt/valve hardware follows one grounded wheel assembly, and the cockpit has a live analog + gallon fuel gauge reading this P-51's independent 269-gallon main tank.",
                    aircraft);
            }
        }

        private static void BuildFuelGauge(
            Transform cameraAnchor,
            P51FuelSystem fuelSystem,
            Material metal,
            Material dark,
            Material service)
        {
            Transform existing = FindDescendant(cameraAnchor, GaugeName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            GameObject gaugeObject = new GameObject(GaugeName);
            Undo.RegisterCreatedObjectUndo(gaugeObject, "Create P-51 fuel quantity gauge");
            gaugeObject.transform.SetParent(cameraAnchor, false);

            // The camera is mounted at local zero on this same anchor while piloting. This
            // places the instrument down/right and forward like a small dashboard gauge while
            // remaining fixed to the airplane as the pilot looks around.
            gaugeObject.transform.localPosition = new Vector3(0.42f, -0.31f, 0.78f);
            gaugeObject.transform.localRotation = Quaternion.identity;
            gaugeObject.transform.localScale = Vector3.one;

            CreatePrimitive(
                gaugeObject.transform,
                PrimitiveType.Cylinder,
                "Fuel Gauge Metal Bezel",
                Vector3.zero,
                new Vector3(0.27f, 0.018f, 0.27f),
                new Vector3(90f, 0f, 0f),
                metal);
            CreatePrimitive(
                gaugeObject.transform,
                PrimitiveType.Cylinder,
                "Fuel Gauge Dark Face",
                new Vector3(0f, 0f, -0.020f),
                new Vector3(0.235f, 0.010f, 0.235f),
                new Vector3(90f, 0f, 0f),
                dark);

            const float emptyAngle = 110f;
            const float fullAngle = -110f;
            const float tickRadius = 0.090f;
            for (int tickIndex = 0; tickIndex <= 8; tickIndex++)
            {
                float t = tickIndex / 8f;
                float angle = Mathf.Lerp(emptyAngle, fullAngle, t);
                Vector3 direction = Quaternion.Euler(0f, 0f, angle) * Vector3.up;
                GameObject tick = CreatePrimitive(
                    gaugeObject.transform,
                    PrimitiveType.Cube,
                    $"Fuel Gauge Tick {tickIndex + 1}",
                    direction * tickRadius + new Vector3(0f, 0f, -0.036f),
                    tickIndex % 2 == 0
                        ? new Vector3(0.010f, 0.030f, 0.006f)
                        : new Vector3(0.007f, 0.020f, 0.005f),
                    new Vector3(0f, 0f, angle),
                    service);
                tick.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            GameObject needlePivotObject = new GameObject("Fuel Gauge Needle Pivot");
            needlePivotObject.transform.SetParent(gaugeObject.transform, false);
            needlePivotObject.transform.localPosition = new Vector3(0f, 0f, -0.050f);
            Transform needlePivot = needlePivotObject.transform;

            CreatePrimitive(
                needlePivot,
                PrimitiveType.Cube,
                "Fuel Gauge Needle",
                new Vector3(0f, 0.055f, 0f),
                new Vector3(0.010f, 0.105f, 0.006f),
                Vector3.zero,
                service);
            CreatePrimitive(
                gaugeObject.transform,
                PrimitiveType.Sphere,
                "Fuel Gauge Needle Hub",
                new Vector3(0f, 0f, -0.054f),
                Vector3.one * 0.025f,
                Vector3.zero,
                metal);

            TextMesh title = CreateText(
                gaugeObject.transform,
                "Fuel Gauge Label",
                "FUEL",
                new Vector3(0f, 0.050f, -0.058f),
                0.0105f,
                56);
            TextMesh gallons = CreateText(
                gaugeObject.transform,
                "Fuel Gauge Gallon Readout",
                "-- / 269 GAL",
                new Vector3(0f, -0.054f, -0.058f),
                0.0090f,
                50);
            TextMesh percent = CreateText(
                gaugeObject.transform,
                "Fuel Gauge Percent Readout",
                "--%",
                new Vector3(0f, -0.082f, -0.058f),
                0.0082f,
                46);

            title.fontStyle = FontStyle.Bold;

            P51FuelQuantityGauge gauge =
                Undo.AddComponent<P51FuelQuantityGauge>(gaugeObject);
            gauge.Configure(fuelSystem, needlePivot, gallons, percent);
            EditorUtility.SetDirty(gauge);
        }

        private static GameObject CreatePrimitive(
            Transform parent,
            PrimitiveType primitiveType,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEuler,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType);
            part.name = objectName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(localEuler);
            part.transform.localScale = localScale;

            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
            return part;
        }

        private static TextMesh CreateText(
            Transform parent,
            string objectName,
            string text,
            Vector3 localPosition,
            float characterSize,
            int fontSize)
        {
            GameObject textObject = new GameObject(objectName);
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = localPosition;
            textObject.transform.localRotation = Quaternion.identity;

            TextMesh textMesh = textObject.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.characterSize = characterSize;
            textMesh.fontSize = fontSize;
            textMesh.color = Color.white;
            return textMesh;
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < all.Length; index++)
            {
                if (all[index] != null && all[index].name == objectName)
                {
                    return all[index];
                }
            }
            return null;
        }
    }
}
