using Hanger51.Aircraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class P51CowlingAndWheelPhysicsRepairSetup
    {
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string FlightGearRootName = "P-51 Flight Landing Gear Colliders";
        private const string CowlingGuideRootName = "P-51 Cowling Reinstall Guidance";
        private const string HighlightMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/AircraftInstallHighlight.mat";

        [MenuItem("Hanger 51/P-51 Mustang/10 - Repair Cowling Reinstall and Add Rolling Wheels")]
        public static void RepairCowlingReinstallAndAddRollingWheels()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("P-51 Step 10 failed. Exit Play mode before applying the repair.");
                return;
            }

            GameObject aircraft = GameObject.Find(AircraftRootName);
            if (aircraft == null)
            {
                Debug.LogError("P-51 Step 10 failed. The current P-51 aircraft is missing.");
                return;
            }

            P51AircraftServiceController service =
                aircraft.GetComponent<P51AircraftServiceController>();
            P51FlightController flightController =
                aircraft.GetComponent<P51FlightController>();
            Rigidbody body = aircraft.GetComponent<Rigidbody>();
            if (service == null || flightController == null || body == null)
            {
                Debug.LogError(
                    "P-51 Step 10 failed. Run P-51 Step 8 first so the service controller, flight controller, and Rigidbody exist.",
                    aircraft);
                return;
            }

            AircraftServiceInteractionTarget cowlingTarget = FindCowlingPanelTarget(aircraft);
            if (cowlingTarget == null)
            {
                Debug.LogError("P-51 Step 10 failed. The top-cowling service target is missing.", aircraft);
                return;
            }

            Material highlightMaterial = AssetDatabase.LoadAssetAtPath<Material>(HighlightMaterialPath);
            if (highlightMaterial == null)
            {
                Debug.LogError(
                    $"P-51 Step 10 failed. The installation-highlight material is missing at '{HighlightMaterialPath}'.",
                    aircraft);
                return;
            }

            BuildCowlingReinstallGuide(cowlingTarget.transform, service, highlightMaterial);

            Transform gearRoot = aircraft.transform.Find(FlightGearRootName);
            if (gearRoot == null)
            {
                Debug.LogError(
                    "P-51 Step 10 failed. The Step 8 landing-gear physics root is missing.",
                    aircraft);
                return;
            }

            Transform leftPhysics = gearRoot.Find("Left Main Wheel Physics");
            Transform rightPhysics = gearRoot.Find("Right Main Wheel Physics");
            Transform tailPhysics = gearRoot.Find("Tailwheel Physics");
            if (leftPhysics == null || rightPhysics == null || tailPhysics == null)
            {
                Debug.LogError(
                    "P-51 Step 10 failed. One or more of the three original wheel-contact objects are missing.",
                    gearRoot.gameObject);
                return;
            }

            WheelCollider leftWheel = ConvertToWheelCollider(leftPhysics, 0.38f, false);
            WheelCollider rightWheel = ConvertToWheelCollider(rightPhysics, 0.38f, false);
            WheelCollider tailWheel = ConvertToWheelCollider(tailPhysics, 0.16f, true);

            Transform leftGear = FindDescendant(aircraft.transform, "Left Main Landing Gear");
            Transform rightGear = FindDescendant(aircraft.transform, "Right Main Landing Gear");
            Transform tailGear = FindDescendant(aircraft.transform, "Tailwheel Assembly");
            Transform leftVisual = leftGear != null
                ? FindDescendant(leftGear, "Main Tire")
                : null;
            Transform rightVisual = rightGear != null
                ? FindDescendant(rightGear, "Main Tire")
                : null;
            Transform tailVisual = tailGear != null
                ? FindDescendant(tailGear, "Tailwheel Tire")
                : null;

            if (leftVisual == null || rightVisual == null || tailVisual == null)
            {
                Debug.LogError(
                    "P-51 Step 10 failed. The visible left, right, or tail tire could not be found.",
                    aircraft);
                return;
            }

            P51WheelLandingGear rollingGear = aircraft.GetComponent<P51WheelLandingGear>();
            if (rollingGear == null)
            {
                rollingGear = Undo.AddComponent<P51WheelLandingGear>(aircraft);
            }
            rollingGear.Configure(
                flightController,
                body,
                leftWheel,
                rightWheel,
                tailWheel,
                leftVisual,
                rightVisual,
                tailVisual);

            body.centerOfMass = new Vector3(0f, 0.96f, -0.72f);
            EditorUtility.SetDirty(body);
            EditorUtility.SetDirty(rollingGear);
            EditorUtility.SetDirty(service);
            EditorUtility.SetDirty(cowlingTarget);

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (string.IsNullOrWhiteSpace(scene.path)
                || !EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 10 repaired the aircraft but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 Step 10 repaired the aircraft, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = aircraft;
            Debug.Log(
                "P-51 Step 10 complete. Added a persistent raised cowling reinstall guide, allowed reinstalling a carried or freely placed cowling, "
                + "replaced the three rigid sphere contacts with suspended free-rolling WheelColliders, connected visible tire rotation, added tailwheel steering and wheel brakes, "
                + "and moved the center of mass aft for stable taildragger ground handling. The edited aircraft visuals were not rebuilt.",
                aircraft);
        }

        [MenuItem("Hanger 51/P-51 Mustang/11 - Validate Cowling Reinstall and Rolling Wheels")]
        public static void ValidateCowlingReinstallAndRollingWheels()
        {
            bool passed = true;
            GameObject aircraft = GameObject.Find(AircraftRootName);
            if (aircraft == null)
            {
                Debug.LogError("P-51 Step 11 failed: the P-51 aircraft is missing.");
                return;
            }

            P51AircraftServiceController service =
                aircraft.GetComponent<P51AircraftServiceController>();
            AircraftServiceInteractionTarget cowlingTarget = FindCowlingPanelTarget(aircraft);
            P51CowlingReinstallGuide guide = cowlingTarget != null
                ? cowlingTarget.GetComponent<P51CowlingReinstallGuide>()
                : null;
            Transform guideRoot = cowlingTarget != null
                ? cowlingTarget.transform.Find(CowlingGuideRootName)
                : null;
            if (service == null
                || cowlingTarget == null
                || guide == null
                || !guide.IsConfigured
                || guideRoot == null
                || guideRoot.GetComponentsInChildren<Renderer>(true).Length < 7)
            {
                Debug.LogError(
                    "P-51 Step 11 failed: the persistent cowling opening guide or its service connection is incomplete.",
                    aircraft);
                passed = false;
            }

            Transform gearRoot = aircraft.transform.Find(FlightGearRootName);
            WheelCollider[] wheelColliders = gearRoot != null
                ? gearRoot.GetComponentsInChildren<WheelCollider>(true)
                : new WheelCollider[0];
            SphereCollider[] oldSphereContacts = gearRoot != null
                ? gearRoot.GetComponentsInChildren<SphereCollider>(true)
                : new SphereCollider[0];
            if (gearRoot == null || wheelColliders.Length != 3)
            {
                Debug.LogError(
                    $"P-51 Step 11 failed: expected 3 WheelColliders, found {wheelColliders.Length}.",
                    aircraft);
                passed = false;
            }
            if (oldSphereContacts.Length != 0)
            {
                Debug.LogError(
                    $"P-51 Step 11 failed: found {oldSphereContacts.Length} obsolete rigid sphere wheel contact(s).",
                    gearRoot != null ? gearRoot.gameObject : aircraft);
                passed = false;
            }

            P51WheelLandingGear rollingGear = aircraft.GetComponent<P51WheelLandingGear>();
            Rigidbody body = aircraft.GetComponent<Rigidbody>();
            if (rollingGear == null
                || rollingGear.ConfiguredWheelCount != 3
                || !rollingGear.HasAllWheelVisuals)
            {
                Debug.LogError(
                    "P-51 Step 11 failed: the rolling landing-gear controller or tire visual references are incomplete.",
                    aircraft);
                passed = false;
            }

            if (body == null || body.centerOfMass.z > -0.50f || body.centerOfMass.y > 1.15f)
            {
                Vector3 center = body != null ? body.centerOfMass : Vector3.zero;
                Debug.LogError(
                    $"P-51 Step 11 failed: taildragger center of mass is {center}; expected it aft of the main gear and below 1.15 m.",
                    aircraft);
                passed = false;
            }

            for (int index = 0; index < wheelColliders.Length; index++)
            {
                WheelCollider wheel = wheelColliders[index];
                if (wheel.radius < 0.14f
                    || wheel.suspensionDistance < 0.12f
                    || wheel.forwardFriction.stiffness > 1.0f)
                {
                    Debug.LogError(
                        $"P-51 Step 11 failed: '{wheel.name}' has invalid rolling-wheel radius, suspension, or forward friction.",
                        wheel);
                    passed = false;
                }
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("P-51 Step 11 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "P-51 Step 11 passed. The cowling opening remains visible and installable after the engine is secured, all three tires use suspended free-rolling WheelColliders, "
                    + "visible wheel rotation and brakes are connected, the obsolete sphere contacts are gone, and the center of mass is configured for taildragger ground handling.",
                    aircraft);
            }
        }

        private static void BuildCowlingReinstallGuide(
            Transform cowlingTarget,
            P51AircraftServiceController service,
            Material highlightMaterial)
        {
            Transform oldGuide = cowlingTarget.Find(CowlingGuideRootName);
            if (oldGuide != null)
            {
                Undo.DestroyObjectImmediate(oldGuide.gameObject);
            }

            GameObject guideRoot = new GameObject(CowlingGuideRootName);
            Undo.RegisterCreatedObjectUndo(guideRoot, "Create P-51 cowling reinstall guide");
            guideRoot.transform.SetParent(cowlingTarget, false);
            guideRoot.transform.localPosition = Vector3.zero;
            guideRoot.transform.localRotation = Quaternion.identity;

            CreateGuidePart(
                guideRoot.transform,
                PrimitiveType.Cube,
                "Left Raised Cowling Edge Guide",
                new Vector3(-0.68f, 0.43f, 0f),
                new Vector3(0.055f, 0.030f, 3.05f),
                highlightMaterial);
            CreateGuidePart(
                guideRoot.transform,
                PrimitiveType.Cube,
                "Right Raised Cowling Edge Guide",
                new Vector3(0.68f, 0.43f, 0f),
                new Vector3(0.055f, 0.030f, 3.05f),
                highlightMaterial);
            CreateGuidePart(
                guideRoot.transform,
                PrimitiveType.Cube,
                "Front Raised Cowling Edge Guide",
                new Vector3(0f, 0.43f, 1.50f),
                new Vector3(1.42f, 0.030f, 0.055f),
                highlightMaterial);
            CreateGuidePart(
                guideRoot.transform,
                PrimitiveType.Cube,
                "Rear Raised Cowling Edge Guide",
                new Vector3(0f, 0.43f, -1.50f),
                new Vector3(1.42f, 0.030f, 0.055f),
                highlightMaterial);

            for (int index = -1; index <= 1; index++)
            {
                CreateGuidePart(
                    guideRoot.transform,
                    PrimitiveType.Sphere,
                    $"Cowling Placement Beacon {index + 2}",
                    new Vector3(index * 0.52f, 0.58f, 0f),
                    Vector3.one * 0.11f,
                    highlightMaterial);
            }

            P51CowlingReinstallGuide guide =
                cowlingTarget.GetComponent<P51CowlingReinstallGuide>();
            if (guide == null)
            {
                guide = Undo.AddComponent<P51CowlingReinstallGuide>(cowlingTarget.gameObject);
            }
            guide.Configure(service, guideRoot);
            EditorUtility.SetDirty(guide);
        }

        private static WheelCollider ConvertToWheelCollider(
            Transform wheelTransform,
            float radius,
            bool tailwheel)
        {
            SphereCollider oldSphere = wheelTransform.GetComponent<SphereCollider>();
            if (oldSphere != null)
            {
                Undo.DestroyObjectImmediate(oldSphere);
            }

            WheelCollider wheel = wheelTransform.GetComponent<WheelCollider>();
            if (wheel == null)
            {
                wheel = Undo.AddComponent<WheelCollider>(wheelTransform.gameObject);
            }

            wheel.center = Vector3.zero;
            wheel.radius = radius;
            wheel.mass = tailwheel ? 18f : 42f;
            wheel.wheelDampingRate = tailwheel ? 0.35f : 0.45f;
            wheel.suspensionDistance = tailwheel ? 0.18f : 0.24f;
            wheel.forceAppPointDistance = tailwheel ? 0.08f : 0.12f;

            JointSpring spring = wheel.suspensionSpring;
            spring.spring = tailwheel ? 48000f : 155000f;
            spring.damper = tailwheel ? 6000f : 14500f;
            spring.targetPosition = tailwheel ? 0.52f : 0.48f;
            wheel.suspensionSpring = spring;

            WheelFrictionCurve forward = wheel.forwardFriction;
            forward.extremumSlip = 0.40f;
            forward.extremumValue = 1f;
            forward.asymptoteSlip = 0.82f;
            forward.asymptoteValue = 0.55f;
            forward.stiffness = tailwheel ? 0.55f : 0.72f;
            wheel.forwardFriction = forward;

            WheelFrictionCurve sideways = wheel.sidewaysFriction;
            sideways.extremumSlip = tailwheel ? 0.32f : 0.24f;
            sideways.extremumValue = 1f;
            sideways.asymptoteSlip = tailwheel ? 0.72f : 0.52f;
            sideways.asymptoteValue = tailwheel ? 0.62f : 0.74f;
            sideways.stiffness = tailwheel ? 0.82f : 1.22f;
            wheel.sidewaysFriction = sideways;

            EditorUtility.SetDirty(wheel);
            return wheel;
        }

        private static GameObject CreateGuidePart(
            Transform parent,
            PrimitiveType primitiveType,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType);
            part.name = objectName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = localScale;
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
            return part;
        }

        private static AircraftServiceInteractionTarget FindCowlingPanelTarget(GameObject aircraft)
        {
            AircraftServiceInteractionTarget[] targets =
                aircraft.GetComponentsInChildren<AircraftServiceInteractionTarget>(true);
            for (int index = 0; index < targets.Length; index++)
            {
                if (targets[index] != null
                    && targets[index].InteractionKind == AircraftServiceInteractionKind.CowlingPanel)
                {
                    return targets[index];
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

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index] != null && transforms[index].name == objectName)
                {
                    return transforms[index];
                }
            }
            return null;
        }
    }
}
