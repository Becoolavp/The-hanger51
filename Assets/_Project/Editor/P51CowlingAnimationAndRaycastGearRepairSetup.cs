using Hanger51.Aircraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class P51CowlingAnimationAndRaycastGearRepairSetup
    {
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string FlightGearRootName = "P-51 Flight Landing Gear Colliders";
        private const string OldGuideRootName = "P-51 Cowling Reinstall Guidance";
        private const string CowlingHighlightName = "Top Cowling Placement Highlight";

        [MenuItem("Hanger 51/P-51 Mustang/12 - Restore Cowling Animation and Repair Ground Physics")]
        public static void RestoreCowlingAnimationAndRepairGroundPhysics()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("P-51 Step 12 failed. Exit Play mode before applying the repair.");
                return;
            }

            GameObject aircraft = GameObject.Find(AircraftRootName);
            if (aircraft == null)
            {
                Debug.LogError("P-51 Step 12 failed. The current P-51 aircraft is missing.");
                return;
            }

            P51AircraftServiceController service =
                aircraft.GetComponent<P51AircraftServiceController>();
            P51FlightController flightController =
                aircraft.GetComponent<P51FlightController>();
            Rigidbody body = aircraft.GetComponent<Rigidbody>();
            AircraftServiceInteractionTarget cowlingTarget = FindCowlingPanelTarget(aircraft);
            if (service == null
                || flightController == null
                || body == null
                || cowlingTarget == null)
            {
                Debug.LogError(
                    "P-51 Step 12 failed. The aircraft service controller, flight controller, Rigidbody, or cowling target is missing.",
                    aircraft);
                return;
            }

            RepairCowlingTarget(cowlingTarget, service);

            Transform gearRoot = aircraft.transform.Find(FlightGearRootName);
            if (gearRoot == null)
            {
                Debug.LogError(
                    "P-51 Step 12 failed. The existing flight landing-gear root is missing. Run P-51 Step 8 once, then return to Step 12.",
                    aircraft);
                return;
            }

            Transform leftAnchor = gearRoot.Find("Left Main Wheel Physics");
            Transform rightAnchor = gearRoot.Find("Right Main Wheel Physics");
            Transform tailAnchor = gearRoot.Find("Tailwheel Physics");
            if (leftAnchor == null || rightAnchor == null || tailAnchor == null)
            {
                Debug.LogError(
                    "P-51 Step 12 failed. One or more landing-gear anchor transforms are missing.",
                    gearRoot.gameObject);
                return;
            }

            RemoveOldWheelPhysics(gearRoot, aircraft);

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
                    "P-51 Step 12 failed. The visible left, right, or tail tire could not be found.",
                    aircraft);
                return;
            }

            P51RaycastLandingGear raycastGear =
                aircraft.GetComponent<P51RaycastLandingGear>();
            if (raycastGear == null)
            {
                raycastGear = Undo.AddComponent<P51RaycastLandingGear>(aircraft);
            }
            raycastGear.Configure(
                flightController,
                body,
                leftAnchor,
                rightAnchor,
                tailAnchor,
                leftVisual,
                rightVisual,
                tailVisual);

            ConfigureFlightControllerForRaycastGear(flightController);
            body.centerOfMass = new Vector3(0f, 0.84f, -1.05f);
            body.linearDamping = 0.005f;
            body.angularDamping = 0.08f;
            body.maxDepenetrationVelocity = 8f;
            body.constraints = RigidbodyConstraints.None;

            service.RefreshTargetsAndVisuals();
            EditorUtility.SetDirty(service);
            EditorUtility.SetDirty(cowlingTarget);
            EditorUtility.SetDirty(raycastGear);
            EditorUtility.SetDirty(flightController);
            EditorUtility.SetDirty(body);

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (string.IsNullOrWhiteSpace(scene.path)
                || !EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 12 repaired the aircraft but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 Step 12 repaired the aircraft, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = aircraft;
            Debug.Log(
                "P-51 Step 12 complete. Removed the box-and-sphere cowling guide, restored animated cowling lift and placement using the actual panel, "
                + "reused the original cowling-shaped highlight, removed the failed SphereCollider/WheelCollider contacts, installed deterministic three-point raycast suspension and rolling tire forces, "
                + "reduced duplicate ground drag, increased test thrust, and preserved the edited P-51 visuals.",
                aircraft);
        }

        [MenuItem("Hanger 51/P-51 Mustang/13 - Validate Cowling Animation and Ground Physics")]
        public static void ValidateCowlingAnimationAndGroundPhysics()
        {
            bool passed = true;
            GameObject aircraft = GameObject.Find(AircraftRootName);
            if (aircraft == null)
            {
                Debug.LogError("P-51 Step 13 failed: the P-51 aircraft is missing.");
                return;
            }

            AircraftServiceInteractionTarget cowlingTarget = FindCowlingPanelTarget(aircraft);
            P51AircraftServiceController service =
                aircraft.GetComponent<P51AircraftServiceController>();
            if (cowlingTarget == null
                || service == null
                || cowlingTarget.AnimatedVisual != service.TopCowlingPanel
                || cowlingTarget.HighlightRoot == null
                || cowlingTarget.HighlightRoot.GetComponentInChildren<Renderer>(true) == null)
            {
                Debug.LogError(
                    "P-51 Step 13 failed: the cowling target is not connected to the real panel and cowling-shaped highlight.",
                    aircraft);
                passed = false;
            }

            Transform oldGuide = cowlingTarget != null
                ? cowlingTarget.transform.Find(OldGuideRootName)
                : null;
            P51CowlingReinstallGuide oldGuideComponent = cowlingTarget != null
                ? cowlingTarget.GetComponent<P51CowlingReinstallGuide>()
                : null;
            if (oldGuide != null || oldGuideComponent != null)
            {
                Debug.LogError(
                    "P-51 Step 13 failed: the obsolete rectangular cowling guide or its beacon controller still exists.",
                    aircraft);
                passed = false;
            }

            Transform gearRoot = aircraft.transform.Find(FlightGearRootName);
            WheelCollider[] wheelColliders = gearRoot != null
                ? gearRoot.GetComponentsInChildren<WheelCollider>(true)
                : new WheelCollider[0];
            SphereCollider[] sphereContacts = gearRoot != null
                ? gearRoot.GetComponentsInChildren<SphereCollider>(true)
                : new SphereCollider[0];
            if (gearRoot == null
                || wheelColliders.Length != 0
                || sphereContacts.Length != 0)
            {
                Debug.LogError(
                    $"P-51 Step 13 failed: expected zero WheelColliders and zero sphere wheel contacts; found {wheelColliders.Length} and {sphereContacts.Length}.",
                    aircraft);
                passed = false;
            }

            if (aircraft.GetComponent<P51WheelLandingGear>() != null)
            {
                Debug.LogError(
                    "P-51 Step 13 failed: the superseded WheelCollider landing-gear controller is still attached.",
                    aircraft);
                passed = false;
            }

            P51RaycastLandingGear raycastGear =
                aircraft.GetComponent<P51RaycastLandingGear>();
            if (raycastGear == null || !raycastGear.IsConfigured)
            {
                Debug.LogError(
                    "P-51 Step 13 failed: the deterministic three-point raycast landing gear is missing or incomplete.",
                    aircraft);
                passed = false;
            }

            Rigidbody body = aircraft.GetComponent<Rigidbody>();
            if (body == null
                || body.centerOfMass.z > -0.85f
                || body.centerOfMass.y > 1.0f
                || body.constraints != RigidbodyConstraints.None)
            {
                Vector3 center = body != null ? body.centerOfMass : Vector3.zero;
                Debug.LogError(
                    $"P-51 Step 13 failed: Rigidbody balance/constraints are invalid. Center of mass: {center}.",
                    aircraft);
                passed = false;
            }

            P51FlightController flightController =
                aircraft.GetComponent<P51FlightController>();
            if (flightController == null || !ValidateFlightTuning(flightController))
            {
                Debug.LogError(
                    "P-51 Step 13 failed: the flight controller still has excessive duplicate ground resistance or insufficient test thrust.",
                    aircraft);
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("P-51 Step 13 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "P-51 Step 13 passed. The actual cowling panel is animated, only the cowling-shaped highlight remains, the temporary box/balls are gone, "
                    + "all failed wheel colliders are removed, three raycast suspension anchors are configured, duplicate ground drag is disabled, and the Rigidbody is balanced for taildragger testing.",
                    aircraft);
            }
        }

        private static void RepairCowlingTarget(
            AircraftServiceInteractionTarget cowlingTarget,
            P51AircraftServiceController service)
        {
            Transform oldGuide = cowlingTarget.transform.Find(OldGuideRootName);
            if (oldGuide != null)
            {
                Undo.DestroyObjectImmediate(oldGuide.gameObject);
            }

            P51CowlingReinstallGuide guideComponent =
                cowlingTarget.GetComponent<P51CowlingReinstallGuide>();
            if (guideComponent != null)
            {
                Undo.DestroyObjectImmediate(guideComponent);
            }

            GameObject cowlingHighlight = cowlingTarget.HighlightRoot;
            if (cowlingHighlight == null)
            {
                Transform foundHighlight = FindDescendant(
                    cowlingTarget.transform,
                    CowlingHighlightName);
                cowlingHighlight = foundHighlight != null
                    ? foundHighlight.gameObject
                    : null;
            }

            SerializedObject targetObject = new SerializedObject(cowlingTarget);
            SerializedProperty serviceProperty = targetObject.FindProperty("serviceController");
            SerializedProperty highlightProperty = targetObject.FindProperty("highlightRoot");
            SerializedProperty animatedProperty = targetObject.FindProperty("animatedVisual");
            SerializedProperty alternatePoseProperty = targetObject.FindProperty("alternatePose");
            SerializedProperty animationLiftProperty = targetObject.FindProperty("animationLift");
            SerializedProperty holdDurationProperty = targetObject.FindProperty("holdDuration");

            if (serviceProperty != null) serviceProperty.objectReferenceValue = service;
            if (highlightProperty != null) highlightProperty.objectReferenceValue = cowlingHighlight;
            if (animatedProperty != null) animatedProperty.objectReferenceValue = service.TopCowlingPanel;
            if (alternatePoseProperty != null) alternatePoseProperty.objectReferenceValue = null;
            if (animationLiftProperty != null) animationLiftProperty.floatValue = 0.62f;
            if (holdDurationProperty != null) holdDurationProperty.floatValue = 1.15f;
            targetObject.ApplyModifiedPropertiesWithoutUndo();

            if (cowlingHighlight != null)
            {
                cowlingHighlight.transform.localPosition = Vector3.zero;
                cowlingHighlight.transform.localRotation = Quaternion.identity;
                cowlingHighlight.transform.localScale = Vector3.one * 1.035f;
            }
        }

        private static void RemoveOldWheelPhysics(
            Transform gearRoot,
            GameObject aircraft)
        {
            WheelCollider[] wheelColliders =
                gearRoot.GetComponentsInChildren<WheelCollider>(true);
            for (int index = 0; index < wheelColliders.Length; index++)
            {
                if (wheelColliders[index] != null)
                {
                    Undo.DestroyObjectImmediate(wheelColliders[index]);
                }
            }

            SphereCollider[] sphereContacts =
                gearRoot.GetComponentsInChildren<SphereCollider>(true);
            for (int index = 0; index < sphereContacts.Length; index++)
            {
                if (sphereContacts[index] != null)
                {
                    Undo.DestroyObjectImmediate(sphereContacts[index]);
                }
            }

            P51WheelLandingGear oldController =
                aircraft.GetComponent<P51WheelLandingGear>();
            if (oldController != null)
            {
                Undo.DestroyObjectImmediate(oldController);
            }
        }

        private static void ConfigureFlightControllerForRaycastGear(
            P51FlightController flightController)
        {
            SerializedObject flightObject = new SerializedObject(flightController);
            SetFloat(flightObject, "maximumThrustNewtons", 24000f);
            SetFloat(flightObject, "rollingResistance", 0f);
            SetFloat(flightObject, "groundLateralGrip", 0f);
            SetFloat(flightObject, "groundSteeringTorque", 0f);
            SetFloat(flightObject, "wheelBrakeStrength", 0f);
            flightObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool ValidateFlightTuning(P51FlightController flightController)
        {
            SerializedObject flightObject = new SerializedObject(flightController);
            SerializedProperty thrust = flightObject.FindProperty("maximumThrustNewtons");
            SerializedProperty rolling = flightObject.FindProperty("rollingResistance");
            SerializedProperty lateral = flightObject.FindProperty("groundLateralGrip");
            SerializedProperty steering = flightObject.FindProperty("groundSteeringTorque");
            SerializedProperty brakes = flightObject.FindProperty("wheelBrakeStrength");
            return thrust != null
                && thrust.floatValue >= 22000f
                && rolling != null && rolling.floatValue <= 0.01f
                && lateral != null && lateral.floatValue <= 0.01f
                && steering != null && steering.floatValue <= 0.01f
                && brakes != null && brakes.floatValue <= 0.01f;
        }

        private static void SetFloat(
            SerializedObject serializedObject,
            string propertyName,
            float value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.floatValue = value;
            }
        }

        private static AircraftServiceInteractionTarget FindCowlingPanelTarget(
            GameObject aircraft)
        {
            AircraftServiceInteractionTarget[] targets =
                aircraft.GetComponentsInChildren<AircraftServiceInteractionTarget>(true);
            for (int index = 0; index < targets.Length; index++)
            {
                if (targets[index] != null
                    && targets[index].InteractionKind
                    == AircraftServiceInteractionKind.CowlingPanel)
                {
                    return targets[index];
                }
            }
            return null;
        }

        private static Transform FindDescendant(
            Transform root,
            string objectName)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index] != null
                    && transforms[index].name == objectName)
                {
                    return transforms[index];
                }
            }
            return null;
        }
    }
}
