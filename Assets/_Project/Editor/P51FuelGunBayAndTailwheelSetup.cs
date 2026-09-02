using System;
using Hanger51.Aircraft;
using Hanger51.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51FuelGunBayAndTailwheelSetup
    {
        private const string FuelRootName = "P-51 Fuel System Visuals";
        private const string FuelCanRootName = "Hanger 51 Fuel Cans";
        private const string TailwheelMarkerName = "P-51 Tailwheel Raised Marker";
        private const string AirportComplexName = "Hanger 51 Airport Complex";
        private const float TailwheelRaiseMeters = 0.20f;
        private const float BayFrontZ = 0.50f;
        private const float BayRearZ = -0.78f;
        private const float MainTankCapacityGallons = 269f;
        private const float StartingFuelGallons = 85f;

        private const string AluminumPath = "Assets/_Project/Aircraft/P51/Materials/PolishedAluminum.mat";
        private const string HardwarePath = "Assets/_Project/Aircraft/P51/Materials/ServiceHardware.mat";
        private const string BayDarkPath = "Assets/_Project/Aircraft/P51/Armament/Materials/ArmamentBayDark.mat";

        [MenuItem("Hanger 51/P-51 Mustang/53 - Raise Tailwheel, Finish Gun Bays and Add Fuel System")]
        public static void InstallFuelAndAirframeCorrections()
        {
            if (!CanEdit(out Scene scene))
            {
                return;
            }

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 53 failed. No P-51 flight controller exists in the current scene.");
                return;
            }

            Material aluminum = AssetDatabase.LoadAssetAtPath<Material>(AluminumPath);
            Material hardware = AssetDatabase.LoadAssetAtPath<Material>(HardwarePath);
            Material bayDark = AssetDatabase.LoadAssetAtPath<Material>(BayDarkPath);
            if (aluminum == null || hardware == null || bayDark == null)
            {
                Debug.LogError("P-51 Step 53 failed. Required P-51 aluminum, hardware, or armament-bay material is missing.");
                return;
            }

            int fuelAircraft = 0;
            int tailwheelsRaised = 0;
            int baysFinished = 0;
            P51FlightController master = FindMasterAircraft(aircraft);

            for (int index = 0; index < aircraft.Length; index++)
            {
                P51FlightController flight = aircraft[index];
                if (flight == null || !flight.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (RaiseTailwheelOnce(flight.gameObject))
                {
                    tailwheelsRaised++;
                }

                baysFinished += FinishGunBays(flight.gameObject, bayDark, hardware);
                InstallSingleRearFuelSystem(flight.gameObject, flight, aluminum, hardware, bayDark);
                fuelAircraft++;
            }

            InstallPlayerFuelInteractor();
            if (master != null)
            {
                CreateFuelCans(master, hardware, aluminum);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 53 made the requested changes but Unity could not save the scene.");
                return;
            }

            Debug.Log(
                $"P-51 Step 53 complete. Fuel-equipped aircraft={fuelAircraft}, tailwheel stations newly raised={tailwheelsRaised}, "
                + $"gun bays fitted with shallow internal false bottoms={baysFinished}. Removed all wing fuel tanks/caps/fillers and installed "
                + $"one {MainTankCapacityGallons:F0}-gal main rear tank per P-51 with one removable exterior cap/filler. Portable fuel cans, "
                + "Merlin fuel burn, fuel-starvation shutdown, and live-master spawn inheritance remain active.",
                master != null ? master.gameObject : null);
        }

        [MenuItem("Hanger 51/P-51 Mustang/54 - Validate Tailwheel, Gun Bays and Fuel System")]
        public static void ValidateFuelAndAirframeCorrections()
        {
            bool passed = true;
            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 54 failed: no P-51 aircraft were found.");
                return;
            }

            int validFuelAircraft = 0;
            int validBays = 0;
            int raisedTailwheels = 0;

            for (int index = 0; index < aircraft.Length; index++)
            {
                P51FlightController flight = aircraft[index];
                if (flight == null || !flight.gameObject.scene.IsValid())
                {
                    continue;
                }

                P51FuelSystem fuel = flight.GetComponent<P51FuelSystem>();
                P51FuelCap[] caps = flight.GetComponentsInChildren<P51FuelCap>(true);
                P51FuelFiller[] fillers = flight.GetComponentsInChildren<P51FuelFiller>(true);
                Transform fuelRoot = FindChildRecursive(flight.transform, FuelRootName);
                Transform mainTank = FindChildRecursive(flight.transform, "Main Rear 269 Gallon Fuel Tank");

                if (fuel == null
                    || fuelRoot == null
                    || mainTank == null
                    || caps.Length != 1
                    || fillers.Length != 1)
                {
                    Debug.LogError(
                        $"P-51 Step 54 failed: '{flight.name}' single rear fuel hierarchy is incomplete. "
                        + $"FuelSystem={(fuel != null)}, rearTank={(mainTank != null)}, caps={caps.Length}, fillers={fillers.Length}.",
                        flight);
                    passed = false;
                }
                else if (Mathf.Abs(fuel.TotalCapacityGallons - MainTankCapacityGallons) > 0.1f)
                {
                    Debug.LogError(
                        $"P-51 Step 54 failed: '{flight.name}' main tank capacity is {fuel.TotalCapacityGallons:F1} gal instead of {MainTankCapacityGallons:F0} gal.",
                        fuel);
                    passed = false;
                }
                else if (ContainsAnyWingFuelHardware(fuelRoot))
                {
                    Debug.LogError(
                        $"P-51 Step 54 failed: '{flight.name}' still contains wing fuel tank/cap/filler geometry.",
                        flight);
                    passed = false;
                }
                else
                {
                    validFuelAircraft++;
                }

                if (FindChildRecursive(flight.transform, TailwheelMarkerName) != null)
                {
                    raisedTailwheels++;
                }
                else
                {
                    Debug.LogError($"P-51 Step 54 failed: '{flight.name}' tailwheel raise marker is missing.", flight);
                    passed = false;
                }

                for (int wing = 0; wing < 2; wing++)
                {
                    string wingName = wing == 0 ? "Left" : "Right";
                    Transform interior = FindChildRecursive(flight.transform, $"{wingName} Wing Armament Bay Interior");
                    Transform floor = interior != null
                        ? FindChildRecursive(interior, $"{wingName} Armament Bay Floor")
                        : null;
                    if (floor == null || floor.GetComponent<BoxCollider>() != null || floor.localScale.y > 0.025f)
                    {
                        Debug.LogError(
                            $"P-51 Step 54 failed: '{flight.name}' {wingName.ToLowerInvariant()} gun bay does not have the shallow collider-free internal floor.",
                            flight);
                        passed = false;
                    }
                    else
                    {
                        validBays++;
                    }
                }
            }

            FirstPersonController player = Object.FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            if (player == null || player.GetComponent<P51FuelPlayerInteractor>() == null)
            {
                Debug.LogError("P-51 Step 54 failed: Player fuel interactor is missing.");
                passed = false;
            }

            P51FuelCan[] cans = Object.FindObjectsByType<P51FuelCan>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (cans.Length < 2)
            {
                Debug.LogError($"P-51 Step 54 failed: expected at least two portable fuel cans, found {cans.Length}.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"P-51 Step 54 passed. Single-rear-tank aircraft={validFuelAircraft}, raised tailwheel stations={raisedTailwheels}, "
                    + $"shallow enclosed gun bays={validBays}, portable fuel cans={cans.Length}. Each P-51 has one removable fuel cap and "
                    + $"one {MainTankCapacityGallons:F0}-gal main rear tank; no wing fuel hardware remains.");
            }
        }

        private static bool RaiseTailwheelOnce(GameObject aircraft)
        {
            if (aircraft == null || FindChildRecursive(aircraft.transform, TailwheelMarkerName) != null)
            {
                return false;
            }

            P51LandingGearMaintenanceController maintenance = aircraft.GetComponent<P51LandingGearMaintenanceController>();
            P51RaycastLandingGear physics = aircraft.GetComponent<P51RaycastLandingGear>();
            if (maintenance == null || physics == null)
            {
                return false;
            }

            SerializedObject serialized = new SerializedObject(maintenance);
            SerializedProperty roots = serialized.FindProperty("gearVisualRoots");
            SerializedProperty deployed = serialized.FindProperty("deployedLocalPositions");
            SerializedProperty retracted = serialized.FindProperty("retractedLocalPositions");

            Transform gearRoot = roots != null && roots.arraySize > 2
                ? roots.GetArrayElementAtIndex(2).objectReferenceValue as Transform
                : null;
            Transform anchor = physics.TailwheelAnchor;

            if (deployed != null && deployed.arraySize > 2)
            {
                SerializedProperty value = deployed.GetArrayElementAtIndex(2);
                Vector3 position = value.vector3Value;
                position.y += TailwheelRaiseMeters;
                value.vector3Value = position;
            }

            if (retracted != null && retracted.arraySize > 2)
            {
                SerializedProperty value = retracted.GetArrayElementAtIndex(2);
                Vector3 position = value.vector3Value;
                position.y += TailwheelRaiseMeters;
                value.vector3Value = position;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(maintenance);

            if (gearRoot != null)
            {
                Undo.RecordObject(gearRoot, "Raise P-51 tailwheel assembly");
                gearRoot.localPosition += Vector3.up * TailwheelRaiseMeters;
                EditorUtility.SetDirty(gearRoot);
            }

            if (anchor != null && (gearRoot == null || (anchor != gearRoot && !anchor.IsChildOf(gearRoot))))
            {
                Undo.RecordObject(anchor, "Raise P-51 tailwheel physics anchor");
                anchor.localPosition += Vector3.up * TailwheelRaiseMeters;
                EditorUtility.SetDirty(anchor);
            }

            P51LandingGearServiceAttachmentFollower follower = aircraft.GetComponent<P51LandingGearServiceAttachmentFollower>();
            if (follower != null)
            {
                follower.RepairHierarchy();
            }

            GameObject marker = new GameObject(TailwheelMarkerName);
            Undo.RegisterCreatedObjectUndo(marker, "Mark P-51 tailwheel raise");
            marker.transform.SetParent(aircraft.transform, false);
            marker.transform.localPosition = Vector3.zero;
            return true;
        }

        private static int FinishGunBays(GameObject aircraft, Material bayDark, Material hardware)
        {
            if (aircraft == null)
            {
                return 0;
            }

            int fixedCount = 0;
            for (int wing = 0; wing < 2; wing++)
            {
                string wingName = wing == 0 ? "Left" : "Right";
                Transform interior = FindChildRecursive(aircraft.transform, $"{wingName} Wing Armament Bay Interior");
                if (interior == null)
                {
                    continue;
                }

                DestroyChildByName(interior, $"{wingName} Armament Bay Floor");
                DestroyChildByName(interior, $"{wingName} Armament Bay Cross Brace A");
                DestroyChildByName(interior, $"{wingName} Armament Bay Cross Brace B");

                float centerZ = (BayFrontZ + BayRearZ) * 0.5f;
                CreateCube(
                    interior,
                    $"{wingName} Armament Bay Floor",
                    new Vector3(0f, 0.082f, centerZ),
                    new Vector3(2.46f, 0.018f, 1.12f),
                    bayDark,
                    false);

                CreateCube(
                    interior,
                    $"{wingName} Armament Bay Cross Brace A",
                    new Vector3(0f, 0.103f, -0.45f),
                    new Vector3(2.36f, 0.025f, 0.035f),
                    hardware,
                    false);
                CreateCube(
                    interior,
                    $"{wingName} Armament Bay Cross Brace B",
                    new Vector3(0f, 0.103f, 0.16f),
                    new Vector3(2.36f, 0.025f, 0.035f),
                    hardware,
                    false);

                fixedCount++;
            }

            return fixedCount;
        }

        private static void InstallSingleRearFuelSystem(
            GameObject aircraft,
            P51FlightController flight,
            Material aluminum,
            Material hardware,
            Material bayDark)
        {
            P51FuelSystem fuel = aircraft.GetComponent<P51FuelSystem>();
            if (fuel == null)
            {
                fuel = Undo.AddComponent<P51FuelSystem>(aircraft);
            }

            fuel.Configure(flight, StartingFuelGallons);
            EditorUtility.SetDirty(fuel);

            Transform oldRoot = aircraft.transform.Find(FuelRootName);
            if (oldRoot != null)
            {
                Undo.DestroyObjectImmediate(oldRoot.gameObject);
            }

            GameObject rootObject = new GameObject(FuelRootName);
            Undo.RegisterCreatedObjectUndo(rootObject, "Create single P-51 rear fuel system");
            rootObject.transform.SetParent(aircraft.transform, false);
            Transform root = rootObject.transform;

            // The visual tank lives entirely in the rear fuselage, well inboard of both
            // armament bays. It is intentionally larger than the previous 85-gal visual
            // because it now represents the complete 269-gal game fuel supply.
            CreateCube(
                root,
                "Main Rear 269 Gallon Fuel Tank",
                new Vector3(0f, 1.52f, -1.72f),
                new Vector3(0.98f, 0.82f, 1.46f),
                bayDark,
                false);

            CreateFillerAssembly(
                root,
                fuel,
                "Main Rear",
                new Vector3(-0.46f, 2.09f, -1.58f),
                new Vector3(-0.28f, 0.05f, -0.10f),
                aluminum,
                hardware);
        }

        private static void CreateFillerAssembly(
            Transform root,
            P51FuelSystem fuel,
            string label,
            Vector3 fillerLocalPosition,
            Vector3 capRemovalOffset,
            Material capMaterial,
            Material neckMaterial)
        {
            GameObject neck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Undo.RegisterCreatedObjectUndo(neck, "Create P-51 main fuel filler neck");
            neck.name = $"{label} Fuel Filler";
            neck.transform.SetParent(root, false);
            neck.transform.localPosition = fillerLocalPosition;
            neck.transform.localRotation = Quaternion.identity;
            neck.transform.localScale = new Vector3(0.11f, 0.025f, 0.11f);
            Renderer neckRenderer = neck.GetComponent<Renderer>();
            if (neckRenderer != null)
            {
                neckRenderer.sharedMaterial = neckMaterial;
            }

            Collider originalNeckCollider = neck.GetComponent<Collider>();
            if (originalNeckCollider != null)
            {
                Undo.DestroyObjectImmediate(originalNeckCollider);
            }

            BoxCollider fillerCollider = neck.AddComponent<BoxCollider>();
            fillerCollider.center = Vector3.zero;
            fillerCollider.size = new Vector3(2.7f, 3.8f, 2.7f);

            GameObject capObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Undo.RegisterCreatedObjectUndo(capObject, "Create P-51 main fuel cap");
            capObject.name = $"{label} Fuel Cap";
            capObject.transform.SetParent(root, false);
            Vector3 installedPosition = fillerLocalPosition + Vector3.up * 0.046f;
            capObject.transform.localPosition = installedPosition;
            capObject.transform.localRotation = Quaternion.identity;
            capObject.transform.localScale = new Vector3(0.13f, 0.026f, 0.13f);
            Renderer capRenderer = capObject.GetComponent<Renderer>();
            if (capRenderer != null)
            {
                capRenderer.sharedMaterial = capMaterial;
            }

            P51FuelCap cap = capObject.AddComponent<P51FuelCap>();
            cap.Configure(
                fuel,
                P51FuelTankStation.Fuselage,
                capObject.transform,
                installedPosition,
                Vector3.zero,
                installedPosition + capRemovalOffset,
                new Vector3(70f, 20f, 12f));

            P51FuelFiller filler = neck.AddComponent<P51FuelFiller>();
            filler.Configure(fuel, cap, P51FuelTankStation.Fuselage, 1.35f);

            EditorUtility.SetDirty(cap);
            EditorUtility.SetDirty(filler);
        }

        private static void InstallPlayerFuelInteractor()
        {
            FirstPersonController player = Object.FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            if (player == null)
            {
                Debug.LogWarning("P-51 Step 53 could not find the FirstPersonController, so the fuel interactor was not installed on the Player.");
                return;
            }

            P51FuelPlayerInteractor interactor = player.GetComponent<P51FuelPlayerInteractor>();
            if (interactor == null)
            {
                interactor = Undo.AddComponent<P51FuelPlayerInteractor>(player.gameObject);
            }

            EditorUtility.SetDirty(interactor);
        }

        private static void CreateFuelCans(P51FlightController master, Material hardware, Material aluminum)
        {
            GameObject existing = GameObject.Find(FuelCanRootName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            GameObject root = new GameObject(FuelCanRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create P-51 fuel cans");

            GameObject airportComplex = GameObject.Find(AirportComplexName);
            if (airportComplex != null)
            {
                root.transform.SetParent(airportComplex.transform, true);
            }

            root.transform.position = master.transform.position
                + master.transform.right * 5.0f
                - master.transform.forward * 2.0f;
            root.transform.rotation = master.transform.rotation;

            for (int index = 0; index < 2; index++)
            {
                GameObject can = CreateCube(
                    root.transform,
                    $"5 Gallon Aviation Fuel Can {index + 1}",
                    new Vector3(index * 0.55f, 0.32f, 0f),
                    new Vector3(0.38f, 0.56f, 0.24f),
                    hardware,
                    true);

                CreateCube(
                    can.transform,
                    "Fuel Can Carry Handle",
                    new Vector3(0f, 0.62f, 0f),
                    new Vector3(0.12f, 0.08f, 0.14f),
                    aluminum,
                    false);

                Rigidbody body = can.AddComponent<Rigidbody>();
                body.mass = 16f;
                body.useGravity = true;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                body.interpolation = RigidbodyInterpolation.Interpolate;

                P51FuelCan fuelCan = can.AddComponent<P51FuelCan>();
                fuelCan.Configure(5f, 5f);
                EditorUtility.SetDirty(fuelCan);
            }
        }

        private static bool ContainsAnyWingFuelHardware(Transform fuelRoot)
        {
            if (fuelRoot == null)
            {
                return false;
            }

            Transform[] all = fuelRoot.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < all.Length; index++)
            {
                Transform item = all[index];
                if (item == null)
                {
                    continue;
                }

                string name = item.name ?? string.Empty;
                if (name.IndexOf("Left Wing", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Right Wing", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static P51FlightController FindMasterAircraft(P51FlightController[] aircraft)
        {
            for (int index = 0; index < aircraft.Length; index++)
            {
                P51FlightController candidate = aircraft[index];
                if (candidate != null
                    && candidate.gameObject.activeInHierarchy
                    && candidate.name.IndexOf("Spawned", StringComparison.OrdinalIgnoreCase) < 0
                    && candidate.name.IndexOf("Template", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return candidate;
                }
            }

            return aircraft.Length > 0 ? aircraft[0] : null;
        }

        private static GameObject CreateCube(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool keepCollider)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(cube, "Create P-51 service geometry");
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localRotation = Quaternion.identity;
            cube.transform.localScale = localScale;

            Renderer renderer = cube.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            Collider collider = cube.GetComponent<Collider>();
            if (!keepCollider && collider != null)
            {
                Undo.DestroyObjectImmediate(collider);
            }

            return cube;
        }

        private static void DestroyChildByName(Transform root, string name)
        {
            Transform child = FindChildRecursive(root, name);
            if (child != null)
            {
                Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        private static Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < all.Length; index++)
            {
                Transform item = all[index];
                if (item != null && item.name == name)
                {
                    return item;
                }
            }

            return null;
        }

        private static bool CanEdit(out Scene scene)
        {
            scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("P-51 Step 53 failed. Exit Play mode first.");
                return false;
            }

            if (EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 Step 53 failed. Wait for Unity to finish compiling.");
                return false;
            }

            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("P-51 Step 53 failed. Open and save the main hangar scene first.");
                return false;
            }

            return true;
        }
    }
}
