using Hanger51.Aircraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51AnimatedCockpitControlsSetup
    {
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string CockpitRootName = "P-51 Cockpit Interior";
        private const string StickPivotName = "P-51 Cockpit Control Stick Pivot";
        private const string ThrottlePivotName = "P-51 Cockpit Throttle Pivot";
        private const string ServiceMaterialPath = "Assets/_Project/Aircraft/P51/Materials/ServiceHardware.mat";
        private const string DarkMaterialPath = "Assets/_Project/Aircraft/P51/Materials/DarkAircraftMetal.mat";

        private static readonly Vector3 StickPivotLocalPosition = new Vector3(0f, 1.015f, -0.05f);
        private static readonly Vector3 ThrottlePivotLocalPosition = new Vector3(-0.43f, 1.42f, -0.16f);

        [MenuItem("Hanger 51/P-51 Mustang/Current/Install Animated Cockpit Stick and Throttle")]
        public static void InstallAnimatedCockpitControls()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 animated cockpit controls require Edit mode with Unity finished compiling.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("P-51 animated cockpit controls require the saved Hanger 51 gameplay scene to be open.");
                return;
            }

            Material service = AssetDatabase.LoadAssetAtPath<Material>(ServiceMaterialPath);
            Material dark = AssetDatabase.LoadAssetAtPath<Material>(DarkMaterialPath);
            if (service == null || dark == null)
            {
                Debug.LogError("P-51 animated cockpit controls could not load the service-hardware or dark cockpit material.");
                return;
            }

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft == null || aircraft.Length == 0)
            {
                Debug.LogError("P-51 animated cockpit controls found no P-51 aircraft in the scene.");
                return;
            }

            int updated = 0;
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
                if (cockpit == null)
                {
                    Debug.LogWarning($"P-51 cockpit-control setup skipped '{flight.name}' because its cockpit interior was not found.", flight);
                    continue;
                }

                Transform stickPivot = BuildStick(cockpit, service, dark);
                Transform throttlePivot = BuildThrottle(cockpit, service, dark);

                P51CockpitControlVisualController controller = flight.GetComponent<P51CockpitControlVisualController>();
                if (controller == null)
                {
                    controller = Undo.AddComponent<P51CockpitControlVisualController>(flight.gameObject);
                }

                controller.Configure(flight, stickPivot, throttlePivot);
                EditorUtility.SetDirty(controller);
                EditorUtility.SetDirty(flight);
                EditorUtility.SetDirty(cockpit);
                updated++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 animated cockpit controls were created, but Unity could not save the active scene.");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 animated cockpit controls were installed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = master != null ? master.gameObject : aircraft[0].gameObject;
            Debug.Log(
                $"P-51 animated cockpit controls installed on {updated} aircraft. The control stick now pivots from the cockpit floor with W/S pitch and A/D roll, "
                + "and the throttle lever pivots from the left-side quadrant using the aircraft's real persistent throttle value changed by Q/Z. "
                + "The live-master spawn console will inherit these controls on newly spawned P-51s.",
                master != null ? master.gameObject : null);
        }

        [MenuItem("Hanger 51/P-51 Mustang/Current/Validate Animated Cockpit Stick and Throttle")]
        public static void ValidateAnimatedCockpitControls()
        {
            bool passed = true;
            int checkedAircraft = 0;

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft == null || aircraft.Length == 0)
            {
                Debug.LogError("P-51 cockpit-control validation found no P-51 aircraft.");
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
                Transform cockpit = FindDescendant(flight.transform, CockpitRootName);
                Transform stickPivot = FindDescendant(cockpit, StickPivotName);
                Transform throttlePivot = FindDescendant(cockpit, ThrottlePivotName);
                Transform stickShaft = FindDescendant(stickPivot, "Control Stick Shaft");
                Transform stickGrip = FindDescendant(stickPivot, "Control Stick Grip");
                Transform throttleLever = FindDescendant(throttlePivot, "Throttle Lever");
                Transform throttleKnob = FindDescendant(throttlePivot, "Throttle Lever Knob");
                P51CockpitControlVisualController controller = flight.GetComponent<P51CockpitControlVisualController>();

                bool valid = cockpit != null
                    && stickPivot != null
                    && throttlePivot != null
                    && stickShaft != null
                    && stickGrip != null
                    && throttleLever != null
                    && throttleKnob != null
                    && controller != null
                    && controller.IsConfigured
                    && controller.FlightController == flight
                    && controller.StickPivot == stickPivot
                    && controller.ThrottlePivot == throttlePivot;

                if (!valid)
                {
                    Debug.LogError($"P-51 cockpit-control validation failed on '{flight.name}'. Run the Current cockpit-control installer again.", flight);
                    passed = false;
                    continue;
                }

                if (Vector3.Distance(stickPivot.localPosition, StickPivotLocalPosition) > 0.04f
                    || Vector3.Distance(throttlePivot.localPosition, ThrottlePivotLocalPosition) > 0.04f)
                {
                    Debug.LogError(
                        $"P-51 cockpit-control validation failed on '{flight.name}': one of the control pivots is no longer at its intended cockpit mounting point.",
                        flight);
                    passed = false;
                }
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("P-51 cockpit-control validation failed because the standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"P-51 animated cockpit-control validation passed. Aircraft checked={checkedAircraft}. Each aircraft has a floor-pivoted stick, a quadrant-pivoted throttle, "
                    + "and one configured runtime visual controller tied to that aircraft's pilot controls and throttle state.");
            }
        }

        private static Transform BuildStick(Transform cockpit, Material service, Material dark)
        {
            DestroyNamedDescendants(cockpit, StickPivotName);
            DestroyNamedDescendants(cockpit, "Control Stick Shaft");

            GameObject pivotObject = new GameObject(StickPivotName);
            Undo.RegisterCreatedObjectUndo(pivotObject, "Create P-51 cockpit stick pivot");
            Transform pivot = pivotObject.transform;
            pivot.SetParent(cockpit, false);
            pivot.localPosition = StickPivotLocalPosition;
            pivot.localRotation = Quaternion.identity;
            pivot.localScale = Vector3.one;

            CreatePrimitive(
                pivot,
                PrimitiveType.Cylinder,
                "Control Stick Base Boot",
                new Vector3(0f, 0.045f, 0f),
                new Vector3(0.095f, 0.045f, 0.095f),
                Vector3.zero,
                dark);

            CreatePrimitive(
                pivot,
                PrimitiveType.Cylinder,
                "Control Stick Shaft",
                new Vector3(0f, 0.22f, 0f),
                new Vector3(0.034f, 0.22f, 0.034f),
                Vector3.zero,
                service);

            CreatePrimitive(
                pivot,
                PrimitiveType.Cylinder,
                "Control Stick Grip",
                new Vector3(0f, 0.485f, -0.018f),
                new Vector3(0.062f, 0.105f, 0.062f),
                new Vector3(12f, 0f, 0f),
                dark);

            CreatePrimitive(
                pivot,
                PrimitiveType.Sphere,
                "Control Stick Grip Cap",
                new Vector3(0f, 0.585f, 0.005f),
                Vector3.one * 0.072f,
                Vector3.zero,
                dark);

            return pivot;
        }

        private static Transform BuildThrottle(Transform cockpit, Material service, Material dark)
        {
            DestroyNamedDescendants(cockpit, ThrottlePivotName);
            DestroyNamedDescendants(cockpit, "Throttle Lever Placeholder");

            Transform housing = FindDescendant(cockpit, "Throttle Quadrant Housing");
            if (housing == null)
            {
                housing = CreatePrimitive(
                    cockpit,
                    PrimitiveType.Cube,
                    "Throttle Quadrant Housing",
                    new Vector3(-0.43f, 1.39f, -0.18f),
                    new Vector3(0.12f, 0.23f, 0.34f),
                    Vector3.zero,
                    dark);
            }

            Transform slot = FindDirectChild(cockpit, "Throttle Quadrant Slot");
            if (slot != null)
            {
                Undo.DestroyObjectImmediate(slot.gameObject);
            }
            CreatePrimitive(
                cockpit,
                PrimitiveType.Cube,
                "Throttle Quadrant Slot",
                new Vector3(-0.43f, 1.515f, -0.16f),
                new Vector3(0.055f, 0.018f, 0.26f),
                Vector3.zero,
                dark);

            GameObject pivotObject = new GameObject(ThrottlePivotName);
            Undo.RegisterCreatedObjectUndo(pivotObject, "Create P-51 throttle pivot");
            Transform pivot = pivotObject.transform;
            pivot.SetParent(cockpit, false);
            pivot.localPosition = ThrottlePivotLocalPosition;
            pivot.localRotation = Quaternion.Euler(0f, 0f, -8f);
            pivot.localScale = Vector3.one;

            CreatePrimitive(
                pivot,
                PrimitiveType.Cylinder,
                "Throttle Lever",
                new Vector3(0f, 0.14f, 0f),
                new Vector3(0.019f, 0.14f, 0.019f),
                Vector3.zero,
                service);

            CreatePrimitive(
                pivot,
                PrimitiveType.Sphere,
                "Throttle Lever Knob",
                new Vector3(0f, 0.30f, 0f),
                new Vector3(0.075f, 0.065f, 0.075f),
                Vector3.zero,
                dark);

            return pivot;
        }

        private static Transform CreatePrimitive(
            Transform parent,
            PrimitiveType type,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEulerAngles,
            Material material)
        {
            GameObject gameObject = GameObject.CreatePrimitive(type);
            gameObject.name = name;
            Undo.RegisterCreatedObjectUndo(gameObject, $"Create {name}");
            Transform transform = gameObject.transform;
            transform.SetParent(parent, false);
            transform.localPosition = localPosition;
            transform.localRotation = Quaternion.Euler(localEulerAngles);
            transform.localScale = localScale;

            Collider collider = gameObject.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.receiveShadows = true;
                EditorUtility.SetDirty(renderer);
            }

            return transform;
        }

        private static void DestroyNamedDescendants(Transform root, string targetName)
        {
            if (root == null)
            {
                return;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = transforms.Length - 1; i >= 0; i--)
            {
                Transform candidate = transforms[i];
                if (candidate != null && candidate != root && candidate.name == targetName)
                {
                    Undo.DestroyObjectImmediate(candidate.gameObject);
                }
            }
        }

        private static Transform FindDirectChild(Transform root, string targetName)
        {
            if (root == null)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child != null && child.name == targetName)
                {
                    return child;
                }
            }

            return null;
        }

        private static Transform FindDescendant(Transform root, string targetName)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null && candidate.name == targetName)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
