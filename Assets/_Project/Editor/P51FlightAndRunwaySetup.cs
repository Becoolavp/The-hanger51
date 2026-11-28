using System.Collections.Generic;
using System.IO;
using Hanger51.Aircraft;
using Hanger51.Inventory;
using Hanger51.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class P51FlightAndRunwaySetup
    {
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string PropellerName = "Four-Blade Hamilton Standard Propeller";
        private const string FlightGearRootName = "P-51 Flight Landing Gear Colliders";
        private const string CockpitTargetName = "P-51 Cockpit Entry Target";
        private const string CameraAnchorName = "P-51 Pilot Camera Anchor";
        private const string ExitPointName = "P-51 Pilot Exit Point";
        private const string RunwayRootName = "P-51 Flight Test Runway";
        private const string RunwaySurfaceName = "Runway Asphalt Surface";

        private const string FlightMaterialFolder =
            "Assets/_Project/Aircraft/P51/Materials/Flight";
        private const string AsphaltMaterialPath = FlightMaterialFolder + "/RunwayAsphalt.mat";
        private const string MarkingMaterialPath = FlightMaterialFolder + "/RunwayMarkingWhite.mat";
        private const string ShoulderMaterialPath = FlightMaterialFolder + "/RunwayShoulderGrass.mat";
        private const string LightMaterialPath = FlightMaterialFolder + "/RunwayEdgeLight.mat";

        private const float RunwayLength = 1100f;
        private const float RunwayWidth = 34f;

        [MenuItem("Hanger 51/P-51 Mustang/8 - Add Flight Controls and Test Runway")]
        public static void AddFlightControlsAndTestRunway()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("P-51 Step 8 failed. Exit Play mode before installing flight controls.");
                return;
            }

            GameObject aircraft = GameObject.Find(AircraftRootName);
            if (aircraft == null)
            {
                Debug.LogError("P-51 Step 8 failed. Build the P-51 before installing flight controls.");
                return;
            }

            AircraftEngineMountReceiver receiver =
                aircraft.GetComponent<AircraftEngineMountReceiver>();
            P51AircraftServiceController service =
                aircraft.GetComponent<P51AircraftServiceController>();
            Transform propeller = FindDescendant(aircraft.transform, PropellerName);
            if (receiver == null || service == null || propeller == null)
            {
                Debug.LogError(
                    "P-51 Step 8 failed. The engine receiver, service controller, or propeller is missing.",
                    aircraft);
                return;
            }

            Rigidbody body = aircraft.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = Undo.AddComponent<Rigidbody>(aircraft);
            }

            Transform[] gearContacts = BuildLandingGearPhysics(aircraft.transform);

            P51FlightController flightController =
                aircraft.GetComponent<P51FlightController>();
            if (flightController == null)
            {
                flightController = Undo.AddComponent<P51FlightController>(aircraft);
            }
            flightController.Configure(receiver, propeller, gearContacts);

            P51PilotSeat pilotSeat = BuildCockpitInteraction(
                aircraft.transform,
                flightController,
                service);
            InstallPlayerPilotInteractor();
            BuildRunway(aircraft.transform);

            EditorUtility.SetDirty(body);
            EditorUtility.SetDirty(flightController);
            EditorUtility.SetDirty(pilotSeat);

            Scene activeScene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (string.IsNullOrWhiteSpace(activeScene.path)
                || !EditorSceneManager.SaveScene(activeScene))
            {
                Debug.LogError("P-51 Step 8 installed flight systems but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 Step 8 installed flight systems, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = aircraft;
            Debug.Log(
                "P-51 Step 8 complete. Added engine-gated cockpit controls, propeller RPM, Rigidbody flight physics, "
                + "three-point landing gear collision, cockpit camera handoff, HUD, wheel brakes, and a 1,100 m test runway. "
                + "The current aircraft visuals and the shapes you removed were not rebuilt.",
                aircraft);
        }

        [MenuItem("Hanger 51/P-51 Mustang/9 - Validate Flight Controls and Test Runway")]
        public static void ValidateFlightControlsAndTestRunway()
        {
            bool passed = true;
            GameObject aircraft = GameObject.Find(AircraftRootName);
            if (aircraft == null)
            {
                Debug.LogError("P-51 Step 9 failed: the P-51 aircraft is missing.");
                return;
            }

            Rigidbody body = aircraft.GetComponent<Rigidbody>();
            P51FlightController flightController =
                aircraft.GetComponent<P51FlightController>();
            AircraftEngineMountReceiver receiver =
                aircraft.GetComponent<AircraftEngineMountReceiver>();
            P51PilotSeat pilotSeat = aircraft.GetComponentInChildren<P51PilotSeat>(true);

            if (body == null || flightController == null || receiver == null)
            {
                Debug.LogError(
                    "P-51 Step 9 failed: Rigidbody, flight controller, or engine receiver is missing.",
                    aircraft);
                passed = false;
            }
            else
            {
                if (body.mass < 3500f || body.mass > 5000f)
                {
                    Debug.LogError(
                        $"P-51 Step 9 failed: aircraft mass is {body.mass:F0} kg; expected a flyable loaded mass between 3,500 and 5,000 kg.",
                        aircraft);
                    passed = false;
                }

                if (flightController.EngineReceiver != receiver
                    || flightController.PropellerRoot == null)
                {
                    Debug.LogError(
                        "P-51 Step 9 failed: the flight controller is not connected to the engine receiver or propeller.",
                        aircraft);
                    passed = false;
                }
            }

            Transform gearRoot = aircraft.transform.Find(FlightGearRootName);
            SphereCollider[] gearColliders = gearRoot != null
                ? gearRoot.GetComponentsInChildren<SphereCollider>(true)
                : new SphereCollider[0];
            if (gearRoot == null || gearColliders.Length != 3)
            {
                Debug.LogError(
                    $"P-51 Step 9 failed: expected 3 landing-gear contact colliders, found {gearColliders.Length}.",
                    aircraft);
                passed = false;
            }

            if (pilotSeat == null
                || pilotSeat.CameraAnchor == null
                || pilotSeat.ExitPoint == null
                || pilotSeat.InteractionCollider == null
                || pilotSeat.FlightController != flightController)
            {
                Debug.LogError(
                    "P-51 Step 9 failed: the cockpit seat, camera anchor, exit point, or interaction collider is incomplete.",
                    aircraft);
                passed = false;
            }

            InventoryInteractor inventoryInteractor =
                Object.FindFirstObjectByType<InventoryInteractor>();
            P51PilotPlayerInteractor playerInteractor = inventoryInteractor != null
                ? inventoryInteractor.GetComponent<P51PilotPlayerInteractor>()
                : null;
            if (playerInteractor == null)
            {
                Debug.LogError("P-51 Step 9 failed: the Player cockpit interactor is missing.");
                passed = false;
            }

            GameObject runway = GameObject.Find(RunwayRootName);
            Transform runwaySurface = runway != null
                ? FindDescendant(runway.transform, RunwaySurfaceName)
                : null;
            BoxCollider runwayCollider = runwaySurface != null
                ? runwaySurface.GetComponent<BoxCollider>()
                : null;
            if (runway == null
                || runwaySurface == null
                || runwayCollider == null
                || runwaySurface.localScale.z < 900f
                || runwaySurface.localScale.x < 25f)
            {
                Debug.LogError(
                    "P-51 Step 9 failed: the long paved runway or its collision surface is missing or undersized.");
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("P-51 Step 9 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "P-51 Step 9 passed. The engine-gated starter, Q/Z throttle, W/S pitch, A/D roll, propeller RPM, "
                    + "cockpit entry/exit, landing gear physics, 1,100 m runway, and Build and Run setup are ready.",
                    aircraft);
            }
        }

        private static Transform[] BuildLandingGearPhysics(Transform aircraft)
        {
            Transform oldRoot = aircraft.Find(FlightGearRootName);
            if (oldRoot != null)
            {
                Undo.DestroyObjectImmediate(oldRoot.gameObject);
            }

            Transform root = new GameObject(FlightGearRootName).transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Create P-51 landing gear physics");
            root.SetParent(aircraft, false);

            Transform[] contacts = new Transform[3];
            contacts[0] = CreateWheelContact(
                root,
                "Left Main Wheel Physics",
                new Vector3(-1.62f, 0.42f, 0.12f),
                0.38f);
            contacts[1] = CreateWheelContact(
                root,
                "Right Main Wheel Physics",
                new Vector3(1.62f, 0.42f, 0.12f),
                0.38f);
            contacts[2] = CreateWheelContact(
                root,
                "Tailwheel Physics",
                new Vector3(0f, 0.19f, -4.23f),
                0.16f);
            return contacts;
        }

        private static Transform CreateWheelContact(
            Transform parent,
            string objectName,
            Vector3 localCenter,
            float radius)
        {
            GameObject wheelObject = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(wheelObject, $"Create {objectName}");
            wheelObject.transform.SetParent(parent, false);
            wheelObject.transform.localPosition = localCenter;
            wheelObject.transform.localRotation = Quaternion.identity;

            SphereCollider collider = wheelObject.AddComponent<SphereCollider>();
            collider.center = Vector3.zero;
            collider.radius = radius;

            Transform contact = new GameObject(objectName + " Ground Contact").transform;
            contact.SetParent(wheelObject.transform, false);
            contact.localPosition = Vector3.down * radius;
            contact.localRotation = Quaternion.identity;
            return contact;
        }

        private static P51PilotSeat BuildCockpitInteraction(
            Transform aircraft,
            P51FlightController flightController,
            P51AircraftServiceController serviceController)
        {
            Transform cameraAnchor = FindOrCreateMarker(
                aircraft,
                CameraAnchorName,
                new Vector3(0f, 2.16f, -1.18f),
                Quaternion.identity);
            Transform exitPoint = FindOrCreateMarker(
                aircraft,
                ExitPointName,
                new Vector3(-1.70f, 0.06f, -1.45f),
                Quaternion.Euler(0f, 90f, 0f));

            Transform target = aircraft.Find(CockpitTargetName);
            if (target == null)
            {
                target = new GameObject(CockpitTargetName).transform;
                Undo.RegisterCreatedObjectUndo(target.gameObject, "Create P-51 cockpit interaction");
                target.SetParent(aircraft, false);
            }
            target.localPosition = new Vector3(0f, 1.98f, -1.12f);
            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one;

            BoxCollider collider = target.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = Undo.AddComponent<BoxCollider>(target.gameObject);
            }
            collider.center = Vector3.zero;
            collider.size = new Vector3(1.25f, 1.05f, 1.85f);
            collider.isTrigger = false;

            P51PilotSeat seat = target.GetComponent<P51PilotSeat>();
            if (seat == null)
            {
                seat = Undo.AddComponent<P51PilotSeat>(target.gameObject);
            }
            seat.Configure(
                flightController,
                serviceController,
                cameraAnchor,
                exitPoint,
                collider);
            return seat;
        }

        private static void InstallPlayerPilotInteractor()
        {
            InventoryInteractor inventoryInteractor =
                Object.FindFirstObjectByType<InventoryInteractor>();
            if (inventoryInteractor == null)
            {
                Debug.LogWarning("P-51 flight setup could not find the Player InventoryInteractor.");
                return;
            }

            GameObject player = inventoryInteractor.gameObject;
            P51PilotPlayerInteractor pilotInteractor =
                player.GetComponent<P51PilotPlayerInteractor>();
            if (pilotInteractor == null)
            {
                pilotInteractor = Undo.AddComponent<P51PilotPlayerInteractor>(player);
            }

            Camera camera = player.GetComponentInChildren<Camera>();
            FirstPersonController controller = player.GetComponent<FirstPersonController>();
            FirstPersonCameraSmoother smoother = camera != null
                ? camera.GetComponent<FirstPersonCameraSmoother>()
                : null;
            CharacterController characterController = player.GetComponent<CharacterController>();
            InventoryUI inventoryUI = Object.FindFirstObjectByType<InventoryUI>();

            pilotInteractor.Configure(
                camera,
                controller,
                smoother,
                characterController,
                inventoryInteractor,
                inventoryUI);
            EditorUtility.SetDirty(pilotInteractor);
        }

        private static void BuildRunway(Transform aircraft)
        {
            GameObject existing = GameObject.Find(RunwayRootName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            EnsureFolder("Assets/_Project/Aircraft");
            EnsureFolder("Assets/_Project/Aircraft/P51");
            EnsureFolder("Assets/_Project/Aircraft/P51/Materials");
            EnsureFolder(FlightMaterialFolder);

            Material asphalt = CreateMaterial(
                AsphaltMaterialPath,
                new Color(0.075f, 0.078f, 0.082f, 1f),
                0.05f,
                0.20f);
            Material marking = CreateMaterial(
                MarkingMaterialPath,
                new Color(0.90f, 0.90f, 0.86f, 1f),
                0.02f,
                0.22f);
            Material shoulder = CreateMaterial(
                ShoulderMaterialPath,
                new Color(0.18f, 0.28f, 0.10f, 1f),
                0.01f,
                0.10f);
            Material light = CreateEmissiveMaterial(
                LightMaterialPath,
                new Color(0.72f, 0.86f, 1f, 1f),
                new Color(2.2f, 3.0f, 4.2f, 1f));

            GameObject runwayRoot = new GameObject(RunwayRootName);
            Undo.RegisterCreatedObjectUndo(runwayRoot, "Create P-51 test runway");
            Vector3 flatForward = Vector3.ProjectOnPlane(aircraft.forward, Vector3.up).normalized;
            if (flatForward.sqrMagnitude < 0.01f)
            {
                flatForward = Vector3.forward;
            }
            float yaw = Quaternion.LookRotation(flatForward, Vector3.up).eulerAngles.y;
            runwayRoot.transform.position = aircraft.position
                + flatForward * 480f
                + Vector3.down * 0.09f;
            runwayRoot.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            GameObject asphaltObject = CreatePart(
                runwayRoot.transform,
                PrimitiveType.Cube,
                RunwaySurfaceName,
                Vector3.zero,
                new Vector3(RunwayWidth, 0.18f, RunwayLength),
                Vector3.zero,
                asphalt,
                true);
            BoxCollider asphaltCollider = asphaltObject.GetComponent<BoxCollider>();
            if (asphaltCollider != null)
            {
                asphaltCollider.center = Vector3.zero;
            }

            CreatePart(
                runwayRoot.transform,
                PrimitiveType.Cube,
                "Left Runway Shoulder",
                new Vector3(-23f, -0.025f, 0f),
                new Vector3(12f, 0.13f, RunwayLength + 30f),
                Vector3.zero,
                shoulder,
                true);
            CreatePart(
                runwayRoot.transform,
                PrimitiveType.Cube,
                "Right Runway Shoulder",
                new Vector3(23f, -0.025f, 0f),
                new Vector3(12f, 0.13f, RunwayLength + 30f),
                Vector3.zero,
                shoulder,
                true);

            CreatePart(
                runwayRoot.transform,
                PrimitiveType.Cube,
                "Left Runway Edge Line",
                new Vector3(-16.35f, 0.105f, 0f),
                new Vector3(0.24f, 0.018f, RunwayLength - 10f),
                Vector3.zero,
                marking,
                false);
            CreatePart(
                runwayRoot.transform,
                PrimitiveType.Cube,
                "Right Runway Edge Line",
                new Vector3(16.35f, 0.105f, 0f),
                new Vector3(0.24f, 0.018f, RunwayLength - 10f),
                Vector3.zero,
                marking,
                false);

            for (float z = -500f; z <= 500f; z += 30f)
            {
                CreatePart(
                    runwayRoot.transform,
                    PrimitiveType.Cube,
                    $"Runway Centerline {z:F0}",
                    new Vector3(0f, 0.108f, z),
                    new Vector3(0.42f, 0.020f, 15f),
                    Vector3.zero,
                    marking,
                    false);
            }

            BuildThresholdMarkings(runwayRoot.transform, -515f, marking, "South");
            BuildThresholdMarkings(runwayRoot.transform, 515f, marking, "North");
            BuildTouchdownZone(runwayRoot.transform, -390f, 1f, marking, "South");
            BuildTouchdownZone(runwayRoot.transform, 390f, -1f, marking, "North");

            for (float z = -525f; z <= 525f; z += 50f)
            {
                CreateRunwayLight(runwayRoot.transform, -17.25f, z, light, "Left");
                CreateRunwayLight(runwayRoot.transform, 17.25f, z, light, "Right");
            }
        }

        private static void BuildThresholdMarkings(
            Transform parent,
            float z,
            Material material,
            string endName)
        {
            for (int index = 0; index < 8; index++)
            {
                float x = -12.25f + index * 3.5f;
                CreatePart(
                    parent,
                    PrimitiveType.Cube,
                    $"{endName} Threshold Stripe {index + 1}",
                    new Vector3(x, 0.11f, z),
                    new Vector3(1.45f, 0.020f, 12f),
                    Vector3.zero,
                    material,
                    false);
            }
        }

        private static void BuildTouchdownZone(
            Transform parent,
            float firstZ,
            float direction,
            Material material,
            string endName)
        {
            for (int group = 0; group < 3; group++)
            {
                float z = firstZ + direction * group * 55f;
                CreatePart(
                    parent,
                    PrimitiveType.Cube,
                    $"{endName} Left Touchdown Bar {group + 1}",
                    new Vector3(-6.2f, 0.11f, z),
                    new Vector3(4.2f, 0.020f, 1.0f),
                    Vector3.zero,
                    material,
                    false);
                CreatePart(
                    parent,
                    PrimitiveType.Cube,
                    $"{endName} Right Touchdown Bar {group + 1}",
                    new Vector3(6.2f, 0.11f, z),
                    new Vector3(4.2f, 0.020f, 1.0f),
                    Vector3.zero,
                    material,
                    false);
            }
        }

        private static void CreateRunwayLight(
            Transform parent,
            float x,
            float z,
            Material material,
            string side)
        {
            GameObject baseObject = CreatePart(
                parent,
                PrimitiveType.Cylinder,
                $"{side} Runway Light Base {z:F0}",
                new Vector3(x, 0.12f, z),
                new Vector3(0.08f, 0.12f, 0.08f),
                Vector3.zero,
                material,
                false);
            baseObject.transform.localRotation = Quaternion.identity;

            CreatePart(
                parent,
                PrimitiveType.Sphere,
                $"{side} Runway Light Lens {z:F0}",
                new Vector3(x, 0.28f, z),
                Vector3.one * 0.13f,
                Vector3.zero,
                material,
                false);
        }

        private static Transform FindOrCreateMarker(
            Transform parent,
            string objectName,
            Vector3 localPosition,
            Quaternion localRotation)
        {
            Transform marker = parent.Find(objectName);
            if (marker == null)
            {
                marker = new GameObject(objectName).transform;
                Undo.RegisterCreatedObjectUndo(marker.gameObject, $"Create {objectName}");
                marker.SetParent(parent, false);
            }

            marker.localPosition = localPosition;
            marker.localRotation = localRotation;
            marker.localScale = Vector3.one;
            return marker;
        }

        private static GameObject CreatePart(
            Transform parent,
            PrimitiveType primitiveType,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEulerAngles,
            Material material,
            bool keepCollider)
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType);
            Undo.RegisterCreatedObjectUndo(part, $"Create {objectName}");
            part.name = objectName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(localEulerAngles);
            part.transform.localScale = localScale;

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            if (!keepCollider)
            {
                Collider collider = part.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.DestroyImmediate(collider);
                }
            }

            return part;
        }

        private static Material CreateMaterial(
            string assetPath,
            Color color,
            float metallic,
            float smoothness)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, assetPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateEmissiveMaterial(
            string assetPath,
            Color color,
            Color emission)
        {
            Material material = CreateMaterial(assetPath, color, 0.12f, 0.62f);
            if (material != null && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
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

        private static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath)
                || folderPath == "Assets"
                || AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
            string folderName = Path.GetFileName(folderPath);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(folderName))
            {
                return;
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
