using Hanger51.Aircraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51FinalServiceAndRetractionHousekeepingSetup
    {
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string RadiatorRootName = "P-51 Functional Belly Radiator";
        private const string CoolantSightName = "Visible Coolant Sight Detail";
        private const string CoolantSightFrameName = "Radiator Coolant Sight External Frame";
        private const string ArmamentRootName = "P-51 Serviceable Wing Armament";
        private const string InstalledGunName = "Installed M2 Wing Gun";

        private static readonly Vector3 TailwheelRetractionOffset = new Vector3(0f, 0.88f, 0.48f);
        private static readonly Vector3 TailwheelRetractedEuler = new Vector3(82f, 0f, 0f);
        private static readonly Vector3 CorrectedGunScale = new Vector3(0.68f, 0.30f, 1.00f);
        private static readonly Vector3 CorrectedGunLocalPosition = new Vector3(0f, 0.035f, 0.035f);

        private const string HardwarePath =
            "Assets/_Project/Aircraft/P51/Materials/ServiceHardware.mat";

        [MenuItem("Hanger 51/P-51 Mustang/67 - Final Radiator, Gear, Guns and Service Housekeeping")]
        public static void ApplyHousekeeping()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 Step 67 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("P-51 Step 67 failed. Open the saved Hanger 51 gameplay scene first.");
                return;
            }

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 67 failed. No P-51 aircraft were found in the scene.");
                return;
            }

            Material hardware = AssetDatabase.LoadAssetAtPath<Material>(HardwarePath);
            if (hardware == null)
            {
                Debug.LogError("P-51 Step 67 failed. ServiceHardware material is missing.");
                return;
            }

            int sightsMoved = 0;
            int tailwheelPosesFixed = 0;
            int gunsRaised = 0;
            int cowlingLocksAdded = 0;
            P51FlightController master = null;

            for (int index = 0; index < aircraft.Length; index++)
            {
                P51FlightController flight = aircraft[index];
                if (flight == null || !flight.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (flight.name == AircraftRootName)
                {
                    master = flight;
                }

                if (MoveCoolantSightToServiceSide(flight.transform, hardware))
                {
                    sightsMoved++;
                }

                P51LandingGearMaintenanceController maintenance =
                    flight.GetComponent<P51LandingGearMaintenanceController>();
                if (RepairTailwheelRetraction(maintenance))
                {
                    tailwheelPosesFixed++;
                }

                gunsRaised += RepairWingGunFit(flight.transform);

                P51CowlingEngineServiceLock cowlingLock =
                    flight.GetComponent<P51CowlingEngineServiceLock>();
                if (cowlingLock == null)
                {
                    cowlingLock = Undo.AddComponent<P51CowlingEngineServiceLock>(flight.gameObject);
                    cowlingLocksAdded++;
                }
                EditorUtility.SetDirty(cowlingLock);
                EditorUtility.SetDirty(flight);
            }

            P51PilotPlayerInteractor pilotInteractor =
                Object.FindFirstObjectByType<P51PilotPlayerInteractor>(FindObjectsInactive.Include);
            if (pilotInteractor == null)
            {
                Debug.LogError("P-51 Step 67 failed. Player cockpit interactor is missing.");
                return;
            }

            P51CockpitMaintenanceSuppression cockpitSuppression =
                pilotInteractor.GetComponent<P51CockpitMaintenanceSuppression>();
            if (cockpitSuppression == null)
            {
                cockpitSuppression = Undo.AddComponent<P51CockpitMaintenanceSuppression>(pilotInteractor.gameObject);
            }
            EditorUtility.SetDirty(cockpitSuppression);
            EditorUtility.SetDirty(pilotInteractor);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 67 made the housekeeping changes but Unity could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 Step 67 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = master != null ? master.gameObject : aircraft[0].gameObject;
            Debug.Log(
                $"P-51 Step 67 complete. External coolant sights={sightsMoved}, corrected tailwheel retracted poses={tailwheelPosesFixed}, "
                + $"raised/compressed installed gun visuals={gunsRaised}, new cowling engine locks={cowlingLocksAdded}. "
                + "The coolant sight is visible from the radiator's right side; the tailwheel now moves up/forward and folds into the aft fuselage; "
                + "gun bodies stay inside the wing; cockpit occupancy suppresses maintenance/armament prompts; and an installed top cowling locks internal Merlin service colliders.",
                master != null ? master.gameObject : null);
        }

        [MenuItem("Hanger 51/P-51 Mustang/68 - Validate Final Service and Retraction Housekeeping")]
        public static void ValidateHousekeeping()
        {
            bool passed = true;
            int aircraftChecked = 0;
            int gunsChecked = 0;

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 68 failed. No P-51 aircraft were found.");
                return;
            }

            for (int index = 0; index < aircraft.Length; index++)
            {
                P51FlightController flight = aircraft[index];
                if (flight == null || !flight.gameObject.scene.IsValid())
                {
                    continue;
                }

                aircraftChecked++;
                Transform radiator = FindDescendant(flight.transform, RadiatorRootName);
                Transform sight = radiator != null ? FindDescendant(radiator, CoolantSightName) : null;
                Transform frame = radiator != null ? FindDescendant(radiator, CoolantSightFrameName) : null;
                if (radiator == null || sight == null || frame == null
                    || sight.localPosition.x < 0.39f
                    || sight.localScale.x > 0.04f)
                {
                    Debug.LogError(
                        $"P-51 Step 68 failed. '{flight.name}' coolant sight is not exposed on the radiator's right service side.",
                        flight);
                    passed = false;
                }

                P51LandingGearMaintenanceController maintenance =
                    flight.GetComponent<P51LandingGearMaintenanceController>();
                if (!ValidateTailwheelRetraction(maintenance, flight.name))
                {
                    passed = false;
                }

                Transform armamentRoot = FindDescendant(flight.transform, ArmamentRootName);
                if (armamentRoot != null)
                {
                    Transform[] all = armamentRoot.GetComponentsInChildren<Transform>(true);
                    for (int partIndex = 0; partIndex < all.Length; partIndex++)
                    {
                        Transform mountedGun = all[partIndex];
                        if (mountedGun == null || mountedGun.name != InstalledGunName)
                        {
                            continue;
                        }

                        gunsChecked++;
                        if (mountedGun.localScale.y > 0.31f
                            || mountedGun.localPosition.y < 0.03f
                            || mountedGun.parent == null
                            || mountedGun.parent.localPosition.y < 0.145f)
                        {
                            Debug.LogError(
                                $"P-51 Step 68 failed. '{flight.name}' has an installed wing gun that is still too low/thick for the wing bay.",
                                mountedGun);
                            passed = false;
                        }
                    }
                }

                if (flight.GetComponent<P51CowlingEngineServiceLock>() == null)
                {
                    Debug.LogError(
                        $"P-51 Step 68 failed. '{flight.name}' is missing the installed-cowling Merlin service lock.",
                        flight);
                    passed = false;
                }
            }

            P51PilotPlayerInteractor pilotInteractor =
                Object.FindFirstObjectByType<P51PilotPlayerInteractor>(FindObjectsInactive.Include);
            P51CockpitMaintenanceSuppression suppression = pilotInteractor != null
                ? pilotInteractor.GetComponent<P51CockpitMaintenanceSuppression>()
                : null;
            if (pilotInteractor == null || suppression == null)
            {
                Debug.LogError("P-51 Step 68 failed. Player cockpit maintenance suppression is missing.");
                passed = false;
            }

            if (gunsChecked < 6)
            {
                Debug.LogError($"P-51 Step 68 failed. Expected at least six installed wing-gun visuals; found {gunsChecked}.");
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("P-51 Step 68 failed. Standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"P-51 Step 68 passed. Aircraft checked={aircraftChecked}, installed guns checked={gunsChecked}. "
                    + "Coolant sights are externally visible, tailwheel retraction tucks up/forward into the tail, gun visuals are contained by the wing, "
                    + "cockpit service-prompt suppression is installed, and every P-51 carries the cowling-controlled Merlin service lock.");
            }
        }

        private static bool MoveCoolantSightToServiceSide(Transform aircraft, Material hardware)
        {
            Transform radiator = FindDescendant(aircraft, RadiatorRootName);
            Transform sight = radiator != null ? FindDescendant(radiator, CoolantSightName) : null;
            if (radiator == null || sight == null)
            {
                return false;
            }

            Undo.RecordObject(sight, "Expose P-51 coolant sight");
            sight.localPosition = new Vector3(0.408f, 0.70f, -1.78f);
            sight.localRotation = Quaternion.identity;
            sight.localScale = new Vector3(0.022f, 0.15f, 0.22f);
            EditorUtility.SetDirty(sight);

            Transform oldFrame = FindDirectChild(radiator, CoolantSightFrameName);
            if (oldFrame != null)
            {
                Undo.DestroyObjectImmediate(oldFrame.gameObject);
            }

            GameObject frameObject = new GameObject(CoolantSightFrameName);
            Undo.RegisterCreatedObjectUndo(frameObject, "Create radiator coolant sight frame");
            frameObject.transform.SetParent(radiator, false);
            Transform frame = frameObject.transform;

            CreateFrameBar(frame, "Coolant Sight Top Frame",
                new Vector3(0.421f, 0.786f, -1.78f), new Vector3(0.025f, 0.016f, 0.25f), hardware);
            CreateFrameBar(frame, "Coolant Sight Bottom Frame",
                new Vector3(0.421f, 0.614f, -1.78f), new Vector3(0.025f, 0.016f, 0.25f), hardware);
            CreateFrameBar(frame, "Coolant Sight Forward Frame",
                new Vector3(0.421f, 0.70f, -1.905f), new Vector3(0.025f, 0.19f, 0.016f), hardware);
            CreateFrameBar(frame, "Coolant Sight Aft Frame",
                new Vector3(0.421f, 0.70f, -1.655f), new Vector3(0.025f, 0.19f, 0.016f), hardware);
            return true;
        }

        private static bool RepairTailwheelRetraction(P51LandingGearMaintenanceController maintenance)
        {
            if (maintenance == null)
            {
                return false;
            }

            SerializedObject serialized = new SerializedObject(maintenance);
            SerializedProperty deployed = serialized.FindProperty("deployedLocalPositions");
            SerializedProperty retracted = serialized.FindProperty("retractedLocalPositions");
            SerializedProperty retractedEulers = serialized.FindProperty("retractedLocalEulers");
            if (deployed == null || retracted == null || retractedEulers == null
                || deployed.arraySize < 3 || retracted.arraySize < 3 || retractedEulers.arraySize < 3)
            {
                Debug.LogWarning($"P-51 Step 67 could not repair '{maintenance.name}' tailwheel retraction arrays.", maintenance);
                return false;
            }

            Vector3 deployedTail = deployed.GetArrayElementAtIndex(2).vector3Value;
            retracted.GetArrayElementAtIndex(2).vector3Value = deployedTail + TailwheelRetractionOffset;
            retractedEulers.GetArrayElementAtIndex(2).vector3Value = TailwheelRetractedEuler;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(maintenance);
            return true;
        }

        private static int RepairWingGunFit(Transform aircraft)
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
                Transform interior = FindDescendant(armamentRoot, $"{wingName} Wing Armament Bay Interior");
                if (interior == null)
                {
                    continue;
                }

                for (int station = 1; station <= 3; station++)
                {
                    Transform gunTarget = FindDescendant(interior, $"{wingName} Gun Mount {station}");
                    Transform mountedGun = gunTarget != null
                        ? FindDescendant(gunTarget, InstalledGunName)
                        : null;
                    if (gunTarget == null || mountedGun == null)
                    {
                        continue;
                    }

                    Undo.RecordObject(gunTarget, "Raise P-51 installed wing gun");
                    Vector3 targetPosition = gunTarget.localPosition;
                    targetPosition.y = 0.155f;
                    gunTarget.localPosition = targetPosition;
                    EditorUtility.SetDirty(gunTarget);

                    Undo.RecordObject(mountedGun, "Compress P-51 installed wing gun");
                    mountedGun.localPosition = CorrectedGunLocalPosition;
                    mountedGun.localScale = CorrectedGunScale;
                    EditorUtility.SetDirty(mountedGun);
                    repaired++;
                }
            }

            return repaired;
        }

        private static bool ValidateTailwheelRetraction(
            P51LandingGearMaintenanceController maintenance,
            string aircraftName)
        {
            if (maintenance == null)
            {
                Debug.LogError($"P-51 Step 68 failed. '{aircraftName}' landing-gear maintenance controller is missing.");
                return false;
            }

            SerializedObject serialized = new SerializedObject(maintenance);
            SerializedProperty deployed = serialized.FindProperty("deployedLocalPositions");
            SerializedProperty retracted = serialized.FindProperty("retractedLocalPositions");
            SerializedProperty retractedEulers = serialized.FindProperty("retractedLocalEulers");
            if (deployed == null || retracted == null || retractedEulers == null
                || deployed.arraySize < 3 || retracted.arraySize < 3 || retractedEulers.arraySize < 3)
            {
                Debug.LogError($"P-51 Step 68 failed. '{aircraftName}' tailwheel retraction arrays are incomplete.", maintenance);
                return false;
            }

            Vector3 deployedTail = deployed.GetArrayElementAtIndex(2).vector3Value;
            Vector3 retractedTail = retracted.GetArrayElementAtIndex(2).vector3Value;
            Vector3 delta = retractedTail - deployedTail;
            Vector3 euler = retractedEulers.GetArrayElementAtIndex(2).vector3Value;
            bool valid = delta.y >= 0.84f
                && delta.z >= 0.44f
                && Mathf.Abs(Mathf.DeltaAngle(euler.x, TailwheelRetractedEuler.x)) <= 2f;
            if (!valid)
            {
                Debug.LogError(
                    $"P-51 Step 68 failed. '{aircraftName}' tailwheel does not have the corrected up/forward retracted pose. "
                    + $"Offset={delta}, X rotation={euler.x:F1} degrees.",
                    maintenance);
            }
            return valid;
        }

        private static GameObject CreateFrameBar(
            Transform parent,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(part, "Create radiator coolant sight frame detail");
            part.name = objectName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = localScale;

            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                Undo.DestroyObjectImmediate(collider);
            }

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
            return part;
        }

        private static Transform FindDirectChild(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }
            for (int index = 0; index < root.childCount; index++)
            {
                Transform child = root.GetChild(index);
                if (child != null && child.name == objectName)
                {
                    return child;
                }
            }
            return null;
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
                Transform candidate = all[index];
                if (candidate != null && candidate.name == objectName)
                {
                    return candidate;
                }
            }
            return null;
        }
    }
}
