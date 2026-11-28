using Hanger51.Aircraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51RadiatorAndTailwheelVisualFitRepair
    {
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string RadiatorRootName = "P-51 Functional Belly Radiator";
        private const string LegacyRadiatorFairingName = "Radiator Scoop Transition Fairing";
        private const string GearSystemRootName = "P-51 Serviceable Retractable Landing Gear";
        private const string TailwheelRootName = "Tailwheel Serviceable Gear Visual";
        private const string TailwheelOleoName = "Tailwheel Oleo Strut";
        private const string TailwheelSleeveName = "Tailwheel Upper Strut Sleeve";
        private const string TailwheelMountCollarName = "Tailwheel Upper Fuselage Mount Collar";
        private const string TailwheelMountYokeName = "Tailwheel Fuselage Mount Yoke";

        private const float TailwheelOleoTopLocalY = 0.92f;
        private const float TailwheelSleeveTopLocalY = 0.95f;

        private const string AluminumPath =
            "Assets/_Project/Aircraft/P51/Materials/PolishedAluminum.mat";
        private const string DarkPath =
            "Assets/_Project/Aircraft/P51/Materials/DarkAircraftMetal.mat";
        private const string HardwarePath =
            "Assets/_Project/Aircraft/P51/Materials/ServiceHardware.mat";

        [MenuItem("Hanger 51/P-51 Mustang/65 - Clean Radiator Fit and Attach Tailwheel Strut")]
        public static void RepairRadiatorAndTailwheelFit()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 Step 65 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("P-51 Step 65 failed. Open the saved Hanger 51 gameplay scene first.");
                return;
            }

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 65 failed. No P-51 aircraft were found in the scene.");
                return;
            }

            bool needsRadiatorBuild = false;
            for (int index = 0; index < aircraft.Length; index++)
            {
                P51FlightController flight = aircraft[index];
                if (flight != null
                    && flight.gameObject.scene.IsValid()
                    && FindDescendant(flight.transform, RadiatorRootName) == null)
                {
                    needsRadiatorBuild = true;
                    break;
                }
            }

            if (needsRadiatorBuild)
            {
                P51RadiatorCoolingSystemSetup.BuildFunctionalRadiatorAndCoolantSystem();
                aircraft = Object.FindObjectsByType<P51FlightController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            }

            Material aluminum = AssetDatabase.LoadAssetAtPath<Material>(AluminumPath);
            Material dark = AssetDatabase.LoadAssetAtPath<Material>(DarkPath);
            Material hardware = AssetDatabase.LoadAssetAtPath<Material>(HardwarePath);
            if (aluminum == null || dark == null || hardware == null)
            {
                Debug.LogError("P-51 Step 65 failed. Required P-51 materials are missing.");
                return;
            }

            int legacyFairingsRemoved = 0;
            int radiatorsRefit = 0;
            int tailwheelsAttached = 0;
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

                legacyFairingsRemoved += RemoveLegacyRadiatorFairings(flight.transform);
                if (RefitFunctionalRadiator(flight))
                {
                    radiatorsRefit++;
                }
                if (AttachTailwheelStrutToFuselage(flight, aluminum, dark, hardware))
                {
                    tailwheelsAttached++;
                }

                EditorUtility.SetDirty(flight);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 65 made the visual-fit repairs but Unity could not save the scene.");
                return;
            }

            Hanger51BuildTools.PrepareCurrentSceneForBuild(false);
            Selection.activeGameObject = master != null ? master.gameObject : aircraft[0].gameObject;

            Debug.Log(
                $"P-51 Step 65 complete. Legacy radiator fairings removed={legacyFairingsRemoved}, "
                + $"functional radiators refit={radiatorsRefit}, tailwheel upper struts attached={tailwheelsAttached}. "
                + "The coolant cap now sits visibly on the radiator's right side, the scoop is slimmer/tucked into the belly, "
                + "and the tailwheel strut reaches the tail without changing the wheel, suspension proxy, or ground-contact physics.",
                master != null ? master.gameObject : null);
        }

        [MenuItem("Hanger 51/P-51 Mustang/66 - Validate Radiator Fit and Tailwheel Strut")]
        public static void ValidateRadiatorAndTailwheelFit()
        {
            bool passed = true;
            int validAircraft = 0;
            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            if (aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 66 failed. No P-51 aircraft were found.");
                return;
            }

            for (int index = 0; index < aircraft.Length; index++)
            {
                P51FlightController flight = aircraft[index];
                if (flight == null || !flight.gameObject.scene.IsValid())
                {
                    continue;
                }

                Transform legacyFairing = FindDescendant(flight.transform, LegacyRadiatorFairingName);
                Transform radiator = FindDescendant(flight.transform, RadiatorRootName);
                Transform capTransform = radiator != null
                    ? FindDescendant(radiator, "Radiator Coolant Cap")
                    : null;
                Transform fillerTransform = radiator != null
                    ? FindDescendant(radiator, "Radiator Coolant Filler Neck")
                    : null;
                Transform topDuct = radiator != null
                    ? FindDescendant(radiator, "Radiator Scoop Top Duct")
                    : null;

                if (legacyFairing != null)
                {
                    Debug.LogError(
                        $"P-51 Step 66 failed. '{flight.name}' still has the obsolete '{LegacyRadiatorFairingName}' overlapping the functional radiator.",
                        legacyFairing);
                    passed = false;
                }
                if (radiator == null || capTransform == null || fillerTransform == null || topDuct == null)
                {
                    Debug.LogError(
                        $"P-51 Step 66 failed. '{flight.name}' is missing the functional radiator, cap, filler, or top duct.",
                        flight);
                    passed = false;
                }
                else
                {
                    if (capTransform.localPosition.x < 0.45f
                        || Mathf.Abs(Mathf.DeltaAngle(capTransform.localEulerAngles.z, 90f)) > 2f)
                    {
                        Debug.LogError(
                            $"P-51 Step 66 failed. '{flight.name}' coolant cap is not exposed on the right side of the radiator.",
                            capTransform);
                        passed = false;
                    }
                    if (topDuct.localScale.y > 0.07f || topDuct.localScale.x > 0.80f)
                    {
                        Debug.LogError(
                            $"P-51 Step 66 failed. '{flight.name}' radiator top duct is still using the oversized blocky fit.",
                            topDuct);
                        passed = false;
                    }
                }

                Transform gearSystem = FindDescendant(flight.transform, GearSystemRootName);
                Transform tailRoot = FindDescendant(gearSystem, TailwheelRootName);
                Transform oleo = FindDescendant(tailRoot, TailwheelOleoName);
                Transform sleeve = FindDescendant(tailRoot, TailwheelSleeveName);
                Transform collar = FindDescendant(tailRoot, TailwheelMountCollarName);
                Transform yoke = FindDescendant(tailRoot, TailwheelMountYokeName);
                P51RaycastLandingGear physics = flight.GetComponent<P51RaycastLandingGear>();

                if (tailRoot == null || oleo == null || sleeve == null || collar == null || yoke == null || physics == null || physics.TailwheelAnchor == null)
                {
                    Debug.LogError(
                        $"P-51 Step 66 failed. '{flight.name}' is missing tailwheel strut/mount geometry or its existing physics anchor.",
                        flight);
                    passed = false;
                }
                else
                {
                    float oleoTop = oleo.localPosition.y + Mathf.Abs(oleo.localScale.y);
                    float sleeveTop = sleeve.localPosition.y + Mathf.Abs(sleeve.localScale.y);
                    if (oleoTop < TailwheelOleoTopLocalY - 0.01f
                        || sleeveTop < TailwheelSleeveTopLocalY - 0.01f)
                    {
                        Debug.LogError(
                            $"P-51 Step 66 failed. '{flight.name}' tailwheel upper strut still stops below the tail attachment. "
                            + $"Oleo top={oleoTop:F2}, sleeve top={sleeveTop:F2}.",
                            oleo);
                        passed = false;
                    }
                    if (collar.GetComponent<Collider>() != null || yoke.GetComponent<Collider>() != null)
                    {
                        Debug.LogError(
                            $"P-51 Step 66 failed. '{flight.name}' new tailwheel upper mount is visual-only and must not alter ground collision.",
                            yoke);
                        passed = false;
                    }
                }

                validAircraft++;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("P-51 Step 66 failed. Standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"P-51 Step 66 passed. Aircraft checked={validAircraft}. Obsolete radiator fairings are gone, "
                    + "functional radiator/cap geometry has the slimmer exposed fit, and every tailwheel upper strut reaches its fuselage mount "
                    + "without adding a new ground collider or moving the existing tailwheel physics anchor.");
            }
        }

        private static int RemoveLegacyRadiatorFairings(Transform aircraft)
        {
            if (aircraft == null)
            {
                return 0;
            }

            int removed = 0;
            Transform[] all = aircraft.GetComponentsInChildren<Transform>(true);
            for (int index = all.Length - 1; index >= 0; index--)
            {
                Transform candidate = all[index];
                if (candidate == null
                    || candidate == aircraft
                    || candidate.name != LegacyRadiatorFairingName)
                {
                    continue;
                }

                Undo.DestroyObjectImmediate(candidate.gameObject);
                removed++;
            }
            return removed;
        }

        private static bool RefitFunctionalRadiator(P51FlightController flight)
        {
            Transform root = FindDescendant(flight.transform, RadiatorRootName);
            P51RadiatorCoolingSystem system = flight.GetComponent<P51RadiatorCoolingSystem>();
            if (root == null || system == null)
            {
                Debug.LogWarning($"P-51 Step 65 skipped radiator refit on '{flight.name}' because its functional radiator is incomplete.", flight);
                return false;
            }

            SetLocalPose(root, "Radiator Scoop Top Duct",
                new Vector3(0f, 0.86f, -1.50f), new Vector3(0.76f, 0.06f, 1.48f), Vector3.zero);
            SetLocalPose(root, "Radiator Scoop Left Wall",
                new Vector3(-0.37f, 0.69f, -1.48f), new Vector3(0.055f, 0.28f, 1.42f), new Vector3(0f, 0f, -4.5f));
            SetLocalPose(root, "Radiator Scoop Right Wall",
                new Vector3(0.37f, 0.69f, -1.48f), new Vector3(0.055f, 0.28f, 1.42f), new Vector3(0f, 0f, 4.5f));
            SetLocalPose(root, "Radiator Intake Lower Lip",
                new Vector3(0f, 0.56f, -0.76f), new Vector3(0.74f, 0.055f, 0.14f), new Vector3(-8f, 0f, 0f));
            SetLocalPose(root, "Radiator Intake Upper Lip",
                new Vector3(0f, 0.84f, -0.76f), new Vector3(0.74f, 0.045f, 0.14f), new Vector3(6f, 0f, 0f));

            Transform coreVolume = FindDescendant(root, "Radiator Core Damage Volume");
            if (coreVolume != null)
            {
                Undo.RecordObject(coreVolume, "Refit P-51 radiator core");
                coreVolume.localPosition = new Vector3(0f, 0.69f, -1.35f);
                BoxCollider coreCollider = coreVolume.GetComponent<BoxCollider>();
                if (coreCollider != null)
                {
                    Undo.RecordObject(coreCollider, "Resize P-51 radiator core collider");
                    coreCollider.size = new Vector3(0.64f, 0.23f, 0.09f);
                    EditorUtility.SetDirty(coreCollider);
                }
            }
            SetLocalPose(root, "Radiator Dark Core",
                Vector3.zero, new Vector3(0.62f, 0.22f, 0.05f), Vector3.zero, true);

            SetLocalPose(root, "Coolant Header Tank",
                new Vector3(0f, 0.78f, -1.72f), new Vector3(0.22f, 0.29f, 0.22f), new Vector3(0f, 0f, 90f));
            SetLocalPose(root, "Coolant Feed Pipe Left",
                new Vector3(-0.25f, 0.75f, -1.56f), new Vector3(0.045f, 0.27f, 0.045f), new Vector3(0f, 0f, 61f));
            SetLocalPose(root, "Coolant Feed Pipe Right",
                new Vector3(0.25f, 0.75f, -1.56f), new Vector3(0.045f, 0.27f, 0.045f), new Vector3(0f, 0f, -61f));
            SetLocalPose(root, "Visible Coolant Sight Detail",
                new Vector3(0f, 0.74f, -1.94f), new Vector3(0.22f, 0.12f, 0.025f), Vector3.zero);

            Transform doorPivot = FindDescendant(root, "Radiator Exit Door Pivot");
            if (doorPivot != null)
            {
                Undo.RecordObject(doorPivot, "Refit P-51 radiator exit door pivot");
                doorPivot.localPosition = new Vector3(0f, 0.57f, -2.18f);
                EditorUtility.SetDirty(doorPivot);
            }
            SetLocalPose(root, "Radiator Exit Door",
                new Vector3(0f, 0f, 0.25f), new Vector3(0.72f, 0.045f, 0.52f), Vector3.zero, true);
            SetLocalPose(root, "Radiator Exit Door Reinforcement",
                new Vector3(0f, 0.027f, 0.25f), new Vector3(0.64f, 0.018f, 0.045f), Vector3.zero, true);

            Transform neck = FindDescendant(root, "Radiator Coolant Filler Neck");
            Transform cap = FindDescendant(root, "Radiator Coolant Cap");
            if (neck != null && cap != null)
            {
                Vector3 neckPosition = new Vector3(0.405f, 0.73f, -1.72f);
                Vector3 capPosition = new Vector3(0.485f, 0.73f, -1.72f);
                Vector3 sideEuler = new Vector3(0f, 0f, 90f);

                Undo.RecordObject(neck, "Move P-51 coolant filler to radiator side");
                neck.localPosition = neckPosition;
                neck.localRotation = Quaternion.Euler(sideEuler);
                neck.localScale = new Vector3(0.072f, 0.050f, 0.072f);

                Undo.RecordObject(cap, "Move P-51 coolant cap to radiator side");
                cap.localPosition = capPosition;
                cap.localRotation = Quaternion.Euler(sideEuler);
                cap.localScale = new Vector3(0.095f, 0.028f, 0.095f);

                BoxCollider fillerCollider = neck.GetComponent<BoxCollider>();
                if (fillerCollider != null)
                {
                    Undo.RecordObject(fillerCollider, "Resize P-51 coolant filler interaction");
                    fillerCollider.size = new Vector3(2.6f, 3.0f, 2.6f);
                    EditorUtility.SetDirty(fillerCollider);
                }
                BoxCollider capCollider = cap.GetComponent<BoxCollider>();
                if (capCollider != null)
                {
                    Undo.RecordObject(capCollider, "Resize P-51 coolant cap interaction");
                    capCollider.size = new Vector3(2.5f, 3.2f, 2.5f);
                    EditorUtility.SetDirty(capCollider);
                }

                P51CoolantCap capComponent = cap.GetComponent<P51CoolantCap>();
                P51CoolantFiller fillerComponent = neck.GetComponent<P51CoolantFiller>();
                if (capComponent != null)
                {
                    capComponent.Configure(
                        system,
                        cap,
                        capPosition,
                        sideEuler,
                        capPosition + new Vector3(0.20f, 0.10f, -0.03f),
                        new Vector3(0f, 0f, 118f));
                    EditorUtility.SetDirty(capComponent);
                }
                if (fillerComponent != null && capComponent != null)
                {
                    fillerComponent.Configure(system, capComponent, 2.2f);
                    EditorUtility.SetDirty(fillerComponent);
                }

                EditorUtility.SetDirty(neck);
                EditorUtility.SetDirty(cap);
            }

            EditorUtility.SetDirty(system);
            return true;
        }

        private static bool AttachTailwheelStrutToFuselage(
            P51FlightController flight,
            Material aluminum,
            Material dark,
            Material hardware)
        {
            Transform gearSystem = FindDescendant(flight.transform, GearSystemRootName);
            Transform tailRoot = FindDescendant(gearSystem, TailwheelRootName);
            Transform oleo = FindDescendant(tailRoot, TailwheelOleoName);
            Transform sleeve = FindDescendant(tailRoot, TailwheelSleeveName);
            if (tailRoot == null || oleo == null || sleeve == null)
            {
                Debug.LogWarning($"P-51 Step 65 skipped tailwheel visual attachment on '{flight.name}' because the serviceable tailwheel hierarchy is incomplete.", flight);
                return false;
            }

            Undo.RecordObject(oleo, "Extend P-51 tailwheel oleo to fuselage");
            Vector3 oleoPosition = oleo.localPosition;
            Vector3 oleoScale = oleo.localScale;
            float oleoHalfHeight = TailwheelOleoTopLocalY * 0.5f;
            oleo.localPosition = new Vector3(oleoPosition.x, oleoHalfHeight, oleoPosition.z);
            oleo.localScale = new Vector3(oleoScale.x, oleoHalfHeight, oleoScale.z);
            EditorUtility.SetDirty(oleo);

            Undo.RecordObject(sleeve, "Extend P-51 tailwheel upper sleeve to fuselage");
            Vector3 sleevePosition = sleeve.localPosition;
            Vector3 sleeveScale = sleeve.localScale;
            const float sleeveBottomY = 0.60f;
            float sleeveHalfHeight = (TailwheelSleeveTopLocalY - sleeveBottomY) * 0.5f;
            sleeve.localPosition = new Vector3(
                sleevePosition.x,
                sleeveBottomY + sleeveHalfHeight,
                sleevePosition.z);
            sleeve.localScale = new Vector3(
                Mathf.Max(sleeveScale.x, 0.065f),
                sleeveHalfHeight,
                Mathf.Max(sleeveScale.z, 0.065f));
            EditorUtility.SetDirty(sleeve);

            Transform collar = FindDescendant(tailRoot, TailwheelMountCollarName);
            if (collar == null)
            {
                collar = CreateVisualPrimitive(
                    tailRoot,
                    PrimitiveType.Cylinder,
                    TailwheelMountCollarName,
                    new Vector3(0f, 0.92f, 0f),
                    new Vector3(0.090f, 0.070f, 0.090f),
                    Vector3.zero,
                    dark).transform;
            }
            else
            {
                ApplyVisualPrimitivePose(
                    collar,
                    new Vector3(0f, 0.92f, 0f),
                    new Vector3(0.090f, 0.070f, 0.090f),
                    Vector3.zero,
                    dark);
            }

            Transform yoke = FindDescendant(tailRoot, TailwheelMountYokeName);
            if (yoke == null)
            {
                yoke = CreateVisualPrimitive(
                    tailRoot,
                    PrimitiveType.Cube,
                    TailwheelMountYokeName,
                    new Vector3(0f, 0.985f, 0.015f),
                    new Vector3(0.28f, 0.10f, 0.24f),
                    new Vector3(-5f, 0f, 0f),
                    hardware).transform;
            }
            else
            {
                ApplyVisualPrimitivePose(
                    yoke,
                    new Vector3(0f, 0.985f, 0.015f),
                    new Vector3(0.28f, 0.10f, 0.24f),
                    new Vector3(-5f, 0f, 0f),
                    hardware);
            }

            P51LandingGearVisualSuspensionFollower visualFollower =
                flight.GetComponent<P51LandingGearVisualSuspensionFollower>();
            if (visualFollower != null)
            {
                // Toggling forces the follower to recapture the newly raised oleo top on the
                // next enable/play cycle while leaving all tire/proxy references intact.
                EditorUtility.SetDirty(visualFollower);
            }

            P51LandingGearServiceAttachmentFollower serviceFollower =
                flight.GetComponent<P51LandingGearServiceAttachmentFollower>();
            if (serviceFollower != null)
            {
                serviceFollower.RepairHierarchy();
                EditorUtility.SetDirty(serviceFollower);
            }

            return true;
        }

        private static void SetLocalPose(
            Transform searchRoot,
            string childName,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEuler,
            bool searchFromChildParent = false)
        {
            Transform target = FindDescendant(searchRoot, childName);
            if (target == null)
            {
                return;
            }

            Undo.RecordObject(target, $"Refit {childName}");
            target.localPosition = localPosition;
            target.localScale = localScale;
            target.localRotation = Quaternion.Euler(localEuler);
            EditorUtility.SetDirty(target);
        }

        private static GameObject CreateVisualPrimitive(
            Transform parent,
            PrimitiveType type,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEuler,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            Undo.RegisterCreatedObjectUndo(part, $"Create {objectName}");
            part.name = objectName;
            part.transform.SetParent(parent, false);
            ApplyVisualPrimitivePose(part.transform, localPosition, localScale, localEuler, material);
            return part;
        }

        private static void ApplyVisualPrimitivePose(
            Transform part,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEuler,
            Material material)
        {
            if (part == null)
            {
                return;
            }

            Undo.RecordObject(part, $"Adjust {part.name}");
            part.localPosition = localPosition;
            part.localScale = localScale;
            part.localRotation = Quaternion.Euler(localEuler);

            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                Undo.DestroyObjectImmediate(collider);
            }

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                Undo.RecordObject(renderer, $"Set {part.name} material");
                renderer.sharedMaterial = material;
                EditorUtility.SetDirty(renderer);
            }
            EditorUtility.SetDirty(part);
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
