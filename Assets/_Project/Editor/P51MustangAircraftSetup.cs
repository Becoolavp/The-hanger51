using System.Collections.Generic;
using System.IO;
using Hanger51.Aircraft;
using Hanger51.EngineAssembly;
using Hanger51.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class P51MustangAircraftSetup
    {
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string AirframeVisualRootName = "P-51D Airframe Visuals";
        private const string TopCowlingName = "Removable Top Engine Cowling";
        private const string EngineMountAnchorName = "P-51 Engine Root Mount Anchor";
        private const string PlacementHighlightName = "P-51 Engine Bay Placement Highlight";

        private const string MaterialFolder =
            "Assets/_Project/Aircraft/P51/Materials";
        private const string MeshFolder =
            "Assets/_Project/Aircraft/P51/Meshes";

        private const string FuselageMeshPath = MeshFolder + "/P51D_Fuselage.asset";
        private const string CowlingMeshPath = MeshFolder + "/P51D_TopCowling.asset";
        private const string LeftWingMeshPath = MeshFolder + "/P51D_LeftWing.asset";
        private const string RightWingMeshPath = MeshFolder + "/P51D_RightWing.asset";
        private const string LeftTailMeshPath = MeshFolder + "/P51D_LeftTailplane.asset";
        private const string RightTailMeshPath = MeshFolder + "/P51D_RightTailplane.asset";
        private const string FinMeshPath = MeshFolder + "/P51D_VerticalFin.asset";
        private const string SpinnerMeshPath = MeshFolder + "/P51D_Spinner.asset";
        private const string PropBladeMeshPath = MeshFolder + "/P51D_PropellerBlade.asset";

        private const int CowlingScrewCount = 10;
        private const int EngineMountBoltCount = 4;

        private static readonly Vector3 AircraftWorldPosition =
            new Vector3(11.5f, 0f, -8.5f);

        [MenuItem("Hanger 51/P-51 Mustang/1 - Build Full P-51 and Engine Installation System")]
        public static void BuildFullP51AndEngineInstallationSystem()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("P-51 Step 1 failed. Exit Play mode first.");
                return;
            }

            EngineAssemblyTransportController transport = FindEngineTransport();
            if (transport == null || transport.TransportRoot == null)
            {
                Debug.LogError(
                    "P-51 Step 1 failed. Install the portable engine hoist before building the P-51 aircraft system.");
                return;
            }

            EnsureFolder("Assets/_Project/Aircraft");
            EnsureFolder("Assets/_Project/Aircraft/P51");
            EnsureFolder(MaterialFolder);
            EnsureFolder(MeshFolder);

            Material aluminum = CreateMaterial(
                MaterialFolder + "/PolishedAluminum.mat",
                new Color(0.68f, 0.71f, 0.75f, 1f),
                0.90f,
                0.74f);
            Material darkMetal = CreateMaterial(
                MaterialFolder + "/DarkAircraftMetal.mat",
                new Color(0.055f, 0.065f, 0.075f, 1f),
                0.82f,
                0.52f);
            Material black = CreateMaterial(
                MaterialFolder + "/PropellerBlack.mat",
                new Color(0.018f, 0.020f, 0.024f, 1f),
                0.20f,
                0.42f);
            Material olive = CreateMaterial(
                MaterialFolder + "/AntiGlareOlive.mat",
                new Color(0.16f, 0.18f, 0.09f, 1f),
                0.12f,
                0.28f);
            Material red = CreateMaterial(
                MaterialFolder + "/MustangRed.mat",
                new Color(0.62f, 0.025f, 0.018f, 1f),
                0.35f,
                0.52f);
            Material blue = CreateMaterial(
                MaterialFolder + "/InsigniaBlue.mat",
                new Color(0.035f, 0.11f, 0.31f, 1f),
                0.18f,
                0.36f);
            Material white = CreateMaterial(
                MaterialFolder + "/InsigniaWhite.mat",
                new Color(0.88f, 0.90f, 0.92f, 1f),
                0.10f,
                0.42f);
            Material tire = CreateMaterial(
                MaterialFolder + "/TireRubber.mat",
                new Color(0.018f, 0.018f, 0.021f, 1f),
                0.01f,
                0.16f);
            Material interior = CreateMaterial(
                MaterialFolder + "/CockpitInterior.mat",
                new Color(0.10f, 0.17f, 0.08f, 1f),
                0.18f,
                0.32f);
            Material leather = CreateMaterial(
                MaterialFolder + "/SeatLeather.mat",
                new Color(0.16f, 0.075f, 0.035f, 1f),
                0.02f,
                0.28f);
            Material brass = CreateMaterial(
                MaterialFolder + "/ServiceHardware.mat",
                new Color(0.70f, 0.48f, 0.15f, 1f),
                0.78f,
                0.62f);
            Material glass = CreateTransparentMaterial(
                MaterialFolder + "/CanopyGlass.mat",
                new Color(0.42f, 0.70f, 0.86f, 0.24f),
                0.08f,
                0.88f);
            Material highlight = CreateTransparentEmissiveMaterial(
                MaterialFolder + "/AircraftInstallHighlight.mat",
                new Color(1f, 0.68f, 0.025f, 0.48f),
                new Color(2.2f, 0.72f, 0.02f, 1f));
            Material engineGhost = CreateTransparentEmissiveMaterial(
                MaterialFolder + "/EnginePlacementGhost.mat",
                new Color(0.15f, 0.95f, 0.26f, 0.20f),
                new Color(0.15f, 2.0f, 0.30f, 1f));

            Mesh fuselageMesh = P51MustangMeshFactory.CreateOrUpdateFuselage(FuselageMeshPath);
            Mesh cowlingMesh = P51MustangMeshFactory.CreateOrUpdateTopCowling(CowlingMeshPath);
            Mesh leftWingMesh = P51MustangMeshFactory.CreateOrUpdateWing(LeftWingMeshPath, true);
            Mesh rightWingMesh = P51MustangMeshFactory.CreateOrUpdateWing(RightWingMeshPath, false);
            Mesh leftTailMesh = P51MustangMeshFactory.CreateOrUpdateTailplane(LeftTailMeshPath, true);
            Mesh rightTailMesh = P51MustangMeshFactory.CreateOrUpdateTailplane(RightTailMeshPath, false);
            Mesh finMesh = P51MustangMeshFactory.CreateOrUpdateVerticalFin(FinMeshPath);
            Mesh spinnerMesh = P51MustangMeshFactory.CreateOrUpdateSpinner(SpinnerMeshPath);
            Mesh propBladeMesh = P51MustangMeshFactory.CreateOrUpdatePropellerBlade(PropBladeMeshPath);

            GameObject existing = GameObject.Find(AircraftRootName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            GameObject aircraft = new GameObject(AircraftRootName);
            Undo.RegisterCreatedObjectUndo(aircraft, "Create P-51D Mustang test aircraft");
            aircraft.transform.position = AircraftWorldPosition;
            aircraft.transform.rotation = Quaternion.identity;
            aircraft.transform.localScale = Vector3.one;

            P51AircraftServiceController serviceController =
                Undo.AddComponent<P51AircraftServiceController>(aircraft);
            AircraftEngineMountReceiver receiver =
                Undo.AddComponent<AircraftEngineMountReceiver>(aircraft);

            Transform visuals = BuildAirframeVisuals(
                aircraft.transform,
                fuselageMesh,
                leftWingMesh,
                rightWingMesh,
                leftTailMesh,
                rightTailMesh,
                finMesh,
                spinnerMesh,
                propBladeMesh,
                aluminum,
                darkMetal,
                black,
                olive,
                red,
                blue,
                white,
                tire,
                interior,
                leather,
                glass);

            BuildMajorColliders(aircraft, visuals);

            BuildCowlingServiceSystem(
                aircraft.transform,
                serviceController,
                receiver,
                cowlingMesh,
                aluminum,
                olive,
                darkMetal,
                brass,
                highlight,
                out GameObject cowlingPanel,
                out Transform cowlingInstalledPose,
                out Transform cowlingRemovedPose);

            BuildEngineBayAndMountSystem(
                aircraft.transform,
                serviceController,
                receiver,
                transport,
                darkMetal,
                aluminum,
                brass,
                highlight,
                engineGhost,
                out Transform engineMountAnchor,
                out GameObject placementHighlight);

            receiver.Configure(
                serviceController,
                engineMountAnchor,
                placementHighlight,
                EngineMountBoltCount);
            serviceController.Configure(
                cowlingPanel,
                cowlingInstalledPose,
                cowlingRemovedPose,
                receiver,
                CowlingScrewCount);

            InstallPlayerInteractor();

            Scene activeScene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (string.IsNullOrWhiteSpace(activeScene.path)
                || !EditorSceneManager.SaveScene(activeScene))
            {
                Debug.LogError("P-51 Step 1 created the aircraft but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 Step 1 created the aircraft, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = aircraft;
            Debug.Log(
                "P-51 Step 1 complete. Created a museum-scale P-51D outside the hangar, removable top cowling with 10 screws, "
                + "highlighted hoist engine receiver, four engine-mount bolts, removal support, and prepared Build and Run.",
                aircraft);
        }

        [MenuItem("Hanger 51/P-51 Mustang/2 - Validate P-51 and Engine Installation System")]
        public static void ValidateP51AndEngineInstallationSystem()
        {
            bool passed = true;
            GameObject aircraft = GameObject.Find(AircraftRootName);
            if (aircraft == null)
            {
                Debug.LogError("P-51 Step 2 failed: the P-51D test aircraft is missing.");
                return;
            }

            Transform visuals = aircraft.transform.Find(AirframeVisualRootName);
            if (visuals == null)
            {
                Debug.LogError("P-51 Step 2 failed: the airframe visual root is missing.", aircraft);
                passed = false;
            }
            else
            {
                Bounds bounds = CalculateRendererBounds(visuals.gameObject);
                float span = bounds.size.x;
                float height = bounds.size.y;
                float length = bounds.size.z;

                if (span < 10.95f || span > 11.65f
                    || length < 9.55f || length > 10.15f
                    || height < 3.40f || height > 4.05f)
                {
                    Debug.LogError(
                        $"P-51 Step 2 failed: airframe dimensions are {length:F2} m long × {span:F2} m span × {height:F2} m high. "
                        + "Expected approximately 9.83 × 11.28 × 3.71 m.",
                        aircraft);
                    passed = false;
                }
                else
                {
                    Debug.Log(
                        $"P-51 dimensions passed: {length:F2} m long × {span:F2} m span × {height:F2} m high.",
                        aircraft);
                }
            }

            P51AircraftServiceController service =
                aircraft.GetComponent<P51AircraftServiceController>();
            AircraftEngineMountReceiver receiver =
                aircraft.GetComponent<AircraftEngineMountReceiver>();
            if (service == null || receiver == null || receiver.EngineMountAnchor == null)
            {
                Debug.LogError("P-51 Step 2 failed: aircraft service or engine receiver references are incomplete.", aircraft);
                passed = false;
            }

            AircraftServiceInteractionTarget[] targets =
                aircraft.GetComponentsInChildren<AircraftServiceInteractionTarget>(true);
            int screwCount = 0;
            int panelCount = 0;
            int mountBoltCount = 0;
            for (int index = 0; index < targets.Length; index++)
            {
                switch (targets[index].InteractionKind)
                {
                    case AircraftServiceInteractionKind.CowlingScrew:
                        screwCount++;
                        break;
                    case AircraftServiceInteractionKind.CowlingPanel:
                        panelCount++;
                        break;
                    case AircraftServiceInteractionKind.EngineMountBolt:
                        mountBoltCount++;
                        break;
                }
            }

            if (screwCount != CowlingScrewCount
                || panelCount != 1
                || mountBoltCount != EngineMountBoltCount)
            {
                Debug.LogError(
                    $"P-51 Step 2 failed: expected 10 cowling screws, 1 cowling panel target, and 4 engine-mount bolts; "
                    + $"found {screwCount}, {panelCount}, and {mountBoltCount}.",
                    aircraft);
                passed = false;
            }

            Transform cowling = FindDescendant(aircraft.transform, TopCowlingName);
            Transform placementHighlight = FindDescendant(aircraft.transform, PlacementHighlightName);
            if (cowling == null || placementHighlight == null)
            {
                Debug.LogError("P-51 Step 2 failed: the removable cowling or engine-bay placement highlight is missing.", aircraft);
                passed = false;
            }

            InventoryInteractor inventoryInteractor =
                Object.FindFirstObjectByType<InventoryInteractor>();
            AircraftServicePlayerInteractor playerInteractor = inventoryInteractor != null
                ? inventoryInteractor.GetComponent<AircraftServicePlayerInteractor>()
                : null;
            if (playerInteractor == null)
            {
                Debug.LogError("P-51 Step 2 failed: the Player aircraft-service interactor is missing.");
                passed = false;
            }

            EngineHoistController hoist =
                Object.FindFirstObjectByType<EngineHoistController>();
            if (hoist == null || hoist.EngineTransport == null)
            {
                Debug.LogError("P-51 Step 2 failed: the portable hoist is not connected to the engine transport.");
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("P-51 Step 2 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "P-51 Step 2 passed. The scaled airframe, removable 10-screw cowling, highlighted engine receiver, "
                    + "four mount bolts, hoist integration, Player service controls, and Build and Run setup are ready.",
                    aircraft);
            }
        }

        [MenuItem("Hanger 51/P-51 Mustang/3 - Reset P-51 Service State")]
        public static void ResetP51ServiceState()
        {
            GameObject aircraft = GameObject.Find(AircraftRootName);
            P51AircraftServiceController service = aircraft != null
                ? aircraft.GetComponent<P51AircraftServiceController>()
                : null;
            if (service == null)
            {
                Debug.LogError("P-51 Step 3 failed: the P-51 service controller is missing.");
                return;
            }

            service.ResetAircraftService();
            EditorUtility.SetDirty(service);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("P-51 Step 3 complete. Restored the cowling and screws and cleared the aircraft engine receiver.", aircraft);
        }

        private static Transform BuildAirframeVisuals(
            Transform aircraft,
            Mesh fuselageMesh,
            Mesh leftWingMesh,
            Mesh rightWingMesh,
            Mesh leftTailMesh,
            Mesh rightTailMesh,
            Mesh finMesh,
            Mesh spinnerMesh,
            Mesh propBladeMesh,
            Material aluminum,
            Material darkMetal,
            Material black,
            Material olive,
            Material red,
            Material blue,
            Material white,
            Material tire,
            Material interior,
            Material leather,
            Material glass)
        {
            GameObject visualsObject = new GameObject(AirframeVisualRootName);
            visualsObject.transform.SetParent(aircraft, false);
            Transform visuals = visualsObject.transform;

            CreateMeshPart(visuals, "Monocoque Fuselage", fuselageMesh, Vector3.zero, Quaternion.identity, Vector3.one, aluminum);
            CreateMeshPart(visuals, "Left Laminar Flow Wing", leftWingMesh, Vector3.zero, Quaternion.identity, Vector3.one, aluminum);
            CreateMeshPart(visuals, "Right Laminar Flow Wing", rightWingMesh, Vector3.zero, Quaternion.identity, Vector3.one, aluminum);
            CreateMeshPart(visuals, "Left Horizontal Stabilizer", leftTailMesh, Vector3.zero, Quaternion.identity, Vector3.one, aluminum);
            CreateMeshPart(visuals, "Right Horizontal Stabilizer", rightTailMesh, Vector3.zero, Quaternion.identity, Vector3.one, aluminum);
            CreateMeshPart(visuals, "Vertical Fin and Rudder", finMesh, Vector3.zero, Quaternion.identity, Vector3.one, aluminum);

            BuildCockpit(visuals, darkMetal, interior, leather, glass);
            BuildPropeller(visuals, spinnerMesh, propBladeMesh, aluminum, black, red);
            BuildLandingGear(visuals, aluminum, darkMetal, tire);
            BuildRadiatorAndIntakes(visuals, aluminum, darkMetal);
            BuildExhaustAndGunDetails(visuals, darkMetal, black);
            BuildMarkingsAndControlDetails(visuals, aluminum, darkMetal, red, blue, white, olive);

            return visuals;
        }

        private static void BuildCockpit(
            Transform parent,
            Material darkMetal,
            Material interior,
            Material leather,
            Material glass)
        {
            CreatePart(parent, PrimitiveType.Cube, "Cockpit Floor",
                new Vector3(0f, 1.43f, -1.12f), new Vector3(0.82f, 0.08f, 1.55f), Vector3.zero, interior);
            CreatePart(parent, PrimitiveType.Cube, "Pilot Seat Back",
                new Vector3(0f, 1.80f, -1.63f), new Vector3(0.48f, 0.72f, 0.12f), new Vector3(-8f, 0f, 0f), leather);
            CreatePart(parent, PrimitiveType.Cube, "Pilot Seat Bottom",
                new Vector3(0f, 1.55f, -1.35f), new Vector3(0.50f, 0.12f, 0.52f), new Vector3(8f, 0f, 0f), leather);
            CreatePart(parent, PrimitiveType.Cube, "Instrument Panel",
                new Vector3(0f, 1.88f, -0.42f), new Vector3(0.88f, 0.58f, 0.10f), new Vector3(-10f, 0f, 0f), darkMetal);

            GameObject canopy = CreatePart(parent, PrimitiveType.Sphere, "Bubble Canopy Glass",
                new Vector3(0f, 2.07f, -1.08f), new Vector3(0.69f, 0.50f, 1.35f), Vector3.zero, glass);
            RemoveCollider(canopy);

            CreatePart(parent, PrimitiveType.Cube, "Canopy Left Rail",
                new Vector3(-0.61f, 1.82f, -1.10f), new Vector3(0.045f, 0.055f, 2.05f), Vector3.zero, darkMetal);
            CreatePart(parent, PrimitiveType.Cube, "Canopy Right Rail",
                new Vector3(0.61f, 1.82f, -1.10f), new Vector3(0.045f, 0.055f, 2.05f), Vector3.zero, darkMetal);
            CreatePart(parent, PrimitiveType.Cube, "Canopy Front Bow",
                new Vector3(0f, 2.08f, -0.10f), new Vector3(1.13f, 0.055f, 0.055f), new Vector3(0f, 0f, 10f), darkMetal);
            CreatePart(parent, PrimitiveType.Cube, "Canopy Rear Bow",
                new Vector3(0f, 2.19f, -2.02f), new Vector3(1.02f, 0.055f, 0.055f), new Vector3(0f, 0f, -10f), darkMetal);
            CreatePart(parent, PrimitiveType.Cube, "Canopy Center Frame",
                new Vector3(0f, 2.54f, -1.08f), new Vector3(0.045f, 0.045f, 1.75f), Vector3.zero, darkMetal);
        }

        private static void BuildPropeller(
            Transform parent,
            Mesh spinnerMesh,
            Mesh propBladeMesh,
            Material aluminum,
            Material black,
            Material red)
        {
            GameObject propeller = new GameObject("Four-Blade Hamilton Standard Propeller");
            propeller.transform.SetParent(parent, false);
            propeller.transform.localPosition = new Vector3(0f, 1.50f, 4.45f);

            CreateMeshPart(propeller.transform, "Spinner", spinnerMesh, Vector3.zero, Quaternion.identity, Vector3.one, aluminum);

            GameObject hub = CreatePart(propeller.transform, PrimitiveType.Cylinder, "Propeller Hub",
                new Vector3(0f, 0f, 0.06f), new Vector3(0.20f, 0.10f, 0.20f), new Vector3(90f, 0f, 0f), darkMaterial: black);
            RemoveCollider(hub);

            for (int index = 0; index < 4; index++)
            {
                Quaternion rotation = Quaternion.Euler(0f, 12f, index * 90f + 8f);
                CreateMeshPart(
                    propeller.transform,
                    $"Propeller Blade {index + 1}",
                    propBladeMesh,
                    new Vector3(0f, 0f, 0.06f),
                    rotation,
                    Vector3.one,
                    black);
            }

            for (int index = 0; index < 4; index++)
            {
                float angle = Mathf.Deg2Rad * (index * 90f + 8f);
                CreatePart(propeller.transform, PrimitiveType.Cube, $"Red Propeller Tip {index + 1}",
                    new Vector3(Mathf.Cos(angle) * 0.02f, Mathf.Sin(angle) * 1.54f, 0.07f),
                    new Vector3(0.16f, 0.15f, 0.055f),
                    new Vector3(0f, 12f, index * 90f + 8f),
                    red);
            }
        }

        private static void BuildLandingGear(
            Transform parent,
            Material aluminum,
            Material darkMetal,
            Material tire)
        {
            BuildMainGear(parent, -1.62f, "Left", aluminum, darkMetal, tire);
            BuildMainGear(parent, 1.62f, "Right", aluminum, darkMetal, tire);

            Transform tailGear = new GameObject("Tailwheel Assembly").transform;
            tailGear.SetParent(parent, false);
            tailGear.localPosition = new Vector3(0f, 0f, -4.23f);
            CreateCylinderBetween(tailGear, "Tailwheel Strut",
                new Vector3(0f, 0.30f, 0f), new Vector3(0f, 0.72f, 0.08f), 0.035f, aluminum);
            GameObject tailWheel = CreatePart(tailGear, PrimitiveType.Cylinder, "Tailwheel Tire",
                new Vector3(0f, 0.19f, 0f), new Vector3(0.16f, 0.075f, 0.16f), new Vector3(0f, 0f, 90f), tire);
            RemoveCollider(tailWheel);
            CreatePart(tailGear, PrimitiveType.Cylinder, "Tailwheel Hub",
                new Vector3(0f, 0.19f, 0f), new Vector3(0.075f, 0.081f, 0.075f), new Vector3(0f, 0f, 90f), darkMetal);
        }

        private static void BuildMainGear(
            Transform parent,
            float x,
            string side,
            Material aluminum,
            Material darkMetal,
            Material tire)
        {
            Transform gear = new GameObject($"{side} Main Landing Gear").transform;
            gear.SetParent(parent, false);
            gear.localPosition = new Vector3(x, 0f, 0.12f);

            CreateCylinderBetween(gear, "Oleo Strut",
                new Vector3(0f, 0.52f, 0f), new Vector3(-Mathf.Sign(x) * 0.18f, 1.25f, 0.02f), 0.055f, aluminum);
            CreateCylinderBetween(gear, "Drag Brace",
                new Vector3(0f, 0.58f, 0.05f), new Vector3(-Mathf.Sign(x) * 0.36f, 1.14f, -0.38f), 0.035f, darkMetal);

            GameObject wheel = CreatePart(gear, PrimitiveType.Cylinder, "Main Tire",
                new Vector3(0f, 0.42f, 0f), new Vector3(0.38f, 0.13f, 0.38f), new Vector3(0f, 0f, 90f), tire);
            RemoveCollider(wheel);
            CreatePart(gear, PrimitiveType.Cylinder, "Wheel Hub",
                new Vector3(0f, 0.42f, 0f), new Vector3(0.18f, 0.14f, 0.18f), new Vector3(0f, 0f, 90f), aluminum);
            CreatePart(gear, PrimitiveType.Cube, "Gear Door",
                new Vector3(-Mathf.Sign(x) * 0.14f, 0.85f, 0f), new Vector3(0.10f, 0.82f, 0.34f), new Vector3(0f, 0f, Mathf.Sign(x) * 8f), aluminum);
        }

        private static void BuildRadiatorAndIntakes(
            Transform parent,
            Material aluminum,
            Material darkMetal)
        {
            CreatePart(parent, PrimitiveType.Cube, "Ventral Radiator Scoop",
                new Vector3(0f, 0.72f, -1.62f), new Vector3(0.88f, 0.52f, 1.62f), new Vector3(-5f, 0f, 0f), aluminum);
            CreatePart(parent, PrimitiveType.Cube, "Radiator Intake Opening",
                new Vector3(0f, 0.73f, -0.77f), new Vector3(0.68f, 0.32f, 0.045f), new Vector3(-5f, 0f, 0f), darkMetal);
            CreatePart(parent, PrimitiveType.Cube, "Radiator Exit Door",
                new Vector3(0f, 0.62f, -2.45f), new Vector3(0.72f, 0.16f, 0.52f), new Vector3(18f, 0f, 0f), darkMetal);

            CreatePart(parent, PrimitiveType.Cube, "Carburetor Intake Chin",
                new Vector3(0f, 1.08f, 4.05f), new Vector3(0.62f, 0.26f, 0.62f), new Vector3(4f, 0f, 0f), aluminum);
            CreatePart(parent, PrimitiveType.Cube, "Carburetor Intake Opening",
                new Vector3(0f, 1.10f, 4.38f), new Vector3(0.46f, 0.16f, 0.035f), Vector3.zero, darkMetal);
        }

        private static void BuildExhaustAndGunDetails(
            Transform parent,
            Material darkMetal,
            Material black)
        {
            for (int sideIndex = 0; sideIndex < 2; sideIndex++)
            {
                float side = sideIndex == 0 ? -1f : 1f;
                for (int stack = 0; stack < 6; stack++)
                {
                    float z = 1.78f + stack * 0.39f;
                    CreateCylinderBetween(parent, $"{(side < 0 ? "Left" : "Right")} Exhaust Stack {stack + 1}",
                        new Vector3(side * 0.55f, 1.62f, z),
                        new Vector3(side * 0.76f, 1.66f, z - 0.08f),
                        0.045f,
                        darkMetal);
                }
            }

            float[] gunX = { 2.05f, 2.85f, 3.65f };
            for (int sideIndex = 0; sideIndex < 2; sideIndex++)
            {
                float side = sideIndex == 0 ? -1f : 1f;
                for (int gun = 0; gun < gunX.Length; gun++)
                {
                    CreateCylinderBetween(parent, $"{(side < 0 ? "Left" : "Right")} Wing Gun {gun + 1}",
                        new Vector3(side * gunX[gun], 1.37f, 0.62f),
                        new Vector3(side * gunX[gun], 1.37f, 0.88f),
                        0.028f,
                        black);
                }
            }
        }

        private static void BuildMarkingsAndControlDetails(
            Transform parent,
            Material aluminum,
            Material darkMetal,
            Material red,
            Material blue,
            Material white,
            Material olive)
        {
            CreatePart(parent, PrimitiveType.Cube, "Nose Anti-Glare Panel",
                new Vector3(0f, 2.10f, 0.78f), new Vector3(0.78f, 0.025f, 1.05f), new Vector3(5f, 0f, 0f), olive);

            CreatePart(parent, PrimitiveType.Cube, "Left Flap Seam",
                new Vector3(-2.36f, 1.39f, -0.93f), new Vector3(3.55f, 0.018f, 0.025f), new Vector3(0f, 0f, 4f), darkMetal);
            CreatePart(parent, PrimitiveType.Cube, "Right Flap Seam",
                new Vector3(2.36f, 1.39f, -0.93f), new Vector3(3.55f, 0.018f, 0.025f), new Vector3(0f, 0f, -4f), darkMetal);
            CreatePart(parent, PrimitiveType.Cube, "Left Aileron Seam",
                new Vector3(-4.35f, 1.49f, -0.48f), new Vector3(1.70f, 0.018f, 0.025f), new Vector3(0f, 0f, 5f), darkMetal);
            CreatePart(parent, PrimitiveType.Cube, "Right Aileron Seam",
                new Vector3(4.35f, 1.49f, -0.48f), new Vector3(1.70f, 0.018f, 0.025f), new Vector3(0f, 0f, -5f), darkMetal);

            BuildRoundel(parent, "Left Wing Insignia", new Vector3(-3.35f, 1.505f, 0.02f), new Vector3(90f, 0f, 0f), blue, white);
            BuildRoundel(parent, "Right Wing Insignia", new Vector3(3.35f, 1.505f, 0.02f), new Vector3(90f, 0f, 0f), blue, white);
            BuildRoundel(parent, "Left Fuselage Insignia", new Vector3(-0.685f, 1.55f, -2.42f), new Vector3(0f, 0f, 90f), blue, white);
            BuildRoundel(parent, "Right Fuselage Insignia", new Vector3(0.685f, 1.55f, -2.42f), new Vector3(0f, 0f, 90f), blue, white);

            CreatePart(parent, PrimitiveType.Cube, "Red Tail Stripe 1",
                new Vector3(0f, 2.62f, -4.38f), new Vector3(0.22f, 0.10f, 0.54f), new Vector3(0f, 0f, 14f), red);
            CreatePart(parent, PrimitiveType.Cube, "Red Tail Stripe 2",
                new Vector3(0f, 2.92f, -4.30f), new Vector3(0.22f, 0.10f, 0.46f), new Vector3(0f, 0f, 14f), red);

            CreatePart(parent, PrimitiveType.Sphere, "Left Navigation Light",
                new Vector3(-5.63f, 1.49f, -0.02f), Vector3.one * 0.09f, Vector3.zero, red);
            CreatePart(parent, PrimitiveType.Sphere, "Right Navigation Light",
                new Vector3(5.63f, 1.49f, -0.02f), Vector3.one * 0.09f, Vector3.zero, white);
        }

        private static void BuildRoundel(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 rotation,
            Material blue,
            Material white)
        {
            CreatePart(parent, PrimitiveType.Cylinder, name + " Blue Disc",
                position, new Vector3(0.42f, 0.008f, 0.42f), rotation, blue);
            CreatePart(parent, PrimitiveType.Cylinder, name + " White Center",
                position + Quaternion.Euler(rotation) * Vector3.up * 0.010f,
                new Vector3(0.20f, 0.009f, 0.20f), rotation, white);
        }

        private static void BuildMajorColliders(GameObject aircraft, Transform visuals)
        {
            CapsuleCollider fuselageCollider = aircraft.AddComponent<CapsuleCollider>();
            fuselageCollider.direction = 2;
            fuselageCollider.center = new Vector3(0f, 1.45f, -0.10f);
            fuselageCollider.radius = 0.72f;
            fuselageCollider.height = 8.95f;

            GameObject wingColliderRoot = new GameObject("Wing Walk Collision");
            wingColliderRoot.transform.SetParent(aircraft.transform, false);
            BoxCollider wingCollider = wingColliderRoot.AddComponent<BoxCollider>();
            wingCollider.center = new Vector3(0f, 1.35f, -0.06f);
            wingCollider.size = new Vector3(11.10f, 0.30f, 2.35f);

            GameObject tailColliderRoot = new GameObject("Tailplane Collision");
            tailColliderRoot.transform.SetParent(aircraft.transform, false);
            BoxCollider tailCollider = tailColliderRoot.AddComponent<BoxCollider>();
            tailCollider.center = new Vector3(0f, 1.82f, -4.05f);
            tailCollider.size = new Vector3(4.25f, 0.18f, 1.10f);
        }

        private static void BuildCowlingServiceSystem(
            Transform aircraft,
            P51AircraftServiceController service,
            AircraftEngineMountReceiver receiver,
            Mesh cowlingMesh,
            Material aluminum,
            Material olive,
            Material darkMetal,
            Material hardware,
            Material highlight,
            out GameObject cowlingPanel,
            out Transform installedPose,
            out Transform removedPose)
        {
            installedPose = CreateMarker(
                aircraft,
                "Top Cowling Installed Pose",
                P51MustangMeshFactory.CowlingCenter,
                Quaternion.identity);
            removedPose = CreateMarker(
                aircraft,
                "Top Cowling Service-Cradle Pose",
                new Vector3(-1.88f, 1.12f, 2.64f),
                Quaternion.Euler(0f, 0f, 26f));

            cowlingPanel = new GameObject(TopCowlingName, typeof(MeshFilter), typeof(MeshRenderer));
            cowlingPanel.transform.SetParent(aircraft, false);
            cowlingPanel.transform.localPosition = installedPose.localPosition;
            cowlingPanel.transform.localRotation = installedPose.localRotation;
            cowlingPanel.GetComponent<MeshFilter>().sharedMesh = cowlingMesh;
            cowlingPanel.GetComponent<MeshRenderer>().sharedMaterial = aluminum;

            CreatePart(cowlingPanel.transform, PrimitiveType.Cube, "Cowling Olive Anti-Glare Strip",
                new Vector3(0f, 0.29f, -0.12f), new Vector3(0.62f, 0.018f, 2.45f), Vector3.zero, olive);
            CreatePart(cowlingPanel.transform, PrimitiveType.Cube, "Cowling Center Hinge Strip",
                new Vector3(0f, 0.315f, 0f), new Vector3(0.035f, 0.022f, 2.75f), Vector3.zero, darkMetal);

            GameObject panelTargetObject = new GameObject("Top Cowling Panel Service Target");
            panelTargetObject.transform.SetParent(aircraft, false);
            panelTargetObject.transform.localPosition = installedPose.localPosition;
            panelTargetObject.transform.localRotation = installedPose.localRotation;
            BoxCollider panelCollider = panelTargetObject.AddComponent<BoxCollider>();
            panelCollider.center = Vector3.zero;
            panelCollider.size = new Vector3(1.38f, 0.54f, 3.20f);

            GameObject panelHighlight = CreateMeshPart(
                panelTargetObject.transform,
                "Top Cowling Placement Highlight",
                cowlingMesh,
                Vector3.zero,
                Quaternion.identity,
                Vector3.one * 1.025f,
                highlight);
            RemoveCollider(panelHighlight);

            AircraftServiceInteractionTarget panelTarget =
                panelTargetObject.AddComponent<AircraftServiceInteractionTarget>();
            panelTarget.Configure(
                service,
                AircraftServiceInteractionKind.CowlingPanel,
                0,
                1.25f,
                panelHighlight,
                cowlingPanel,
                removedPose,
                0.65f,
                0f);

            float[] screwZ = { 1.62f, 2.20f, 2.78f, 3.36f, 4.08f };
            int screwIndex = 0;
            for (int sideIndex = 0; sideIndex < 2; sideIndex++)
            {
                float side = sideIndex == 0 ? -1f : 1f;
                for (int longitudinal = 0; longitudinal < screwZ.Length; longitudinal++)
                {
                    float z = screwZ[longitudinal];
                    P51MustangMeshFactory.FuselageSurfaceSample sample =
                        P51MustangMeshFactory.SampleCowlingSurface(z, side * 0.49f);
                    Vector3 localPosition = sample.Position - P51MustangMeshFactory.CowlingCenter;
                    Quaternion localRotation = Quaternion.FromToRotation(Vector3.up, sample.Normal);

                    GameObject targetObject = new GameObject($"Top Cowling Screw Target {screwIndex + 1}");
                    targetObject.transform.SetParent(cowlingPanel.transform, false);
                    targetObject.transform.localPosition = localPosition;
                    targetObject.transform.localRotation = localRotation;
                    SphereCollider collider = targetObject.AddComponent<SphereCollider>();
                    collider.radius = 0.13f;
                    collider.center = new Vector3(0f, 0.025f, 0f);

                    GameObject screwAssembly = new GameObject("Cowling Screw Assembly");
                    screwAssembly.transform.SetParent(targetObject.transform, false);
                    CreatePart(screwAssembly.transform, PrimitiveType.Cylinder, "Threaded Screw Shaft",
                        new Vector3(0f, -0.055f, 0f), new Vector3(0.020f, 0.060f, 0.020f), Vector3.zero, darkMetal);
                    CreatePart(screwAssembly.transform, PrimitiveType.Cylinder, "Screw Washer",
                        new Vector3(0f, 0.004f, 0f), new Vector3(0.052f, 0.006f, 0.052f), Vector3.zero, hardware);
                    CreatePart(screwAssembly.transform, PrimitiveType.Cylinder, "Flush Screw Head",
                        new Vector3(0f, 0.022f, 0f), new Vector3(0.045f, 0.016f, 0.045f), Vector3.zero, aluminum);
                    CreatePart(screwAssembly.transform, PrimitiveType.Cube, "Screwdriver Slot",
                        new Vector3(0f, 0.039f, 0f), new Vector3(0.052f, 0.004f, 0.010f), Vector3.zero, darkMetal);

                    GameObject screwHighlight = CreatePart(targetObject.transform, PrimitiveType.Cylinder, "Cowling Screw Highlight",
                        new Vector3(0f, 0.010f, 0f), new Vector3(0.095f, 0.006f, 0.095f), Vector3.zero, highlight);

                    AircraftServiceInteractionTarget screwTarget =
                        targetObject.AddComponent<AircraftServiceInteractionTarget>();
                    screwTarget.Configure(
                        service,
                        AircraftServiceInteractionKind.CowlingScrew,
                        screwIndex,
                        0.72f,
                        screwHighlight,
                        screwAssembly,
                        null,
                        0.105f,
                        2.5f);
                    screwIndex++;
                }
            }

            BuildCowlingServiceCradle(aircraft, aluminum, darkMetal);
        }

        private static void BuildCowlingServiceCradle(
            Transform aircraft,
            Material aluminum,
            Material darkMetal)
        {
            Transform cradle = new GameObject("Top Cowling Service Cradle").transform;
            cradle.SetParent(aircraft, false);
            cradle.localPosition = new Vector3(-1.88f, 0f, 2.64f);

            CreatePart(cradle, PrimitiveType.Cube, "Cradle Base",
                new Vector3(0f, 0.12f, 0f), new Vector3(1.45f, 0.16f, 2.70f), Vector3.zero, darkMetal);
            CreatePart(cradle, PrimitiveType.Cube, "Left Padded Rail",
                new Vector3(-0.48f, 0.72f, 0f), new Vector3(0.12f, 0.16f, 2.48f), new Vector3(0f, 0f, -18f), aluminum);
            CreatePart(cradle, PrimitiveType.Cube, "Right Padded Rail",
                new Vector3(0.48f, 0.72f, 0f), new Vector3(0.12f, 0.16f, 2.48f), new Vector3(0f, 0f, 18f), aluminum);
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    CreatePart(cradle, PrimitiveType.Cylinder, $"Cradle Caster {x} {z}",
                        new Vector3(x * 0.56f, 0.05f, z * 1.08f), new Vector3(0.09f, 0.045f, 0.09f), new Vector3(0f, 0f, 90f), darkMetal);
                }
            }
        }

        private static void BuildEngineBayAndMountSystem(
            Transform aircraft,
            P51AircraftServiceController service,
            AircraftEngineMountReceiver receiver,
            EngineAssemblyTransportController transport,
            Material darkMetal,
            Material aluminum,
            Material hardware,
            Material highlight,
            Material engineGhost,
            out Transform engineMountAnchor,
            out GameObject placementHighlight)
        {
            Bounds engineBounds = CalculateEnginePhysicalBounds(
                transport.TransportRoot.gameObject,
                transport.TransportRoot);
            Vector3 desiredEngineCenter = new Vector3(0f, 1.53f, 2.83f);
            Vector3 anchorLocalPosition = desiredEngineCenter - engineBounds.center;

            engineMountAnchor = CreateMarker(
                aircraft,
                EngineMountAnchorName,
                anchorLocalPosition,
                Quaternion.identity);

            Transform bay = new GameObject("Open P-51 Engine Bay Structure").transform;
            bay.SetParent(aircraft, false);
            CreatePart(bay, PrimitiveType.Cube, "Engine Firewall",
                new Vector3(0f, 1.50f, 1.37f), new Vector3(1.18f, 1.22f, 0.08f), Vector3.zero, darkMetal);
            CreatePart(bay, PrimitiveType.Cube, "Left Engine Mount Rail",
                new Vector3(-0.54f, 1.15f, 2.84f), new Vector3(0.11f, 0.11f, 2.82f), Vector3.zero, aluminum);
            CreatePart(bay, PrimitiveType.Cube, "Right Engine Mount Rail",
                new Vector3(0.54f, 1.15f, 2.84f), new Vector3(0.11f, 0.11f, 2.82f), Vector3.zero, aluminum);
            CreatePart(bay, PrimitiveType.Cube, "Front Engine Mount Crossmember",
                new Vector3(0f, 1.15f, 4.05f), new Vector3(1.16f, 0.11f, 0.11f), Vector3.zero, aluminum);
            CreatePart(bay, PrimitiveType.Cube, "Rear Engine Mount Crossmember",
                new Vector3(0f, 1.15f, 1.66f), new Vector3(1.16f, 0.11f, 0.11f), Vector3.zero, aluminum);

            CreateCylinderBetween(bay, "Left Upper Mount Tube",
                new Vector3(-0.53f, 1.21f, 1.45f), new Vector3(-0.58f, 1.68f, 3.85f), 0.038f, darkMetal);
            CreateCylinderBetween(bay, "Right Upper Mount Tube",
                new Vector3(0.53f, 1.21f, 1.45f), new Vector3(0.58f, 1.68f, 3.85f), 0.038f, darkMetal);
            CreateCylinderBetween(bay, "Left Lower Mount Tube",
                new Vector3(-0.53f, 1.12f, 1.45f), new Vector3(-0.58f, 0.98f, 3.85f), 0.038f, darkMetal);
            CreateCylinderBetween(bay, "Right Lower Mount Tube",
                new Vector3(0.53f, 1.12f, 1.45f), new Vector3(0.58f, 0.98f, 3.85f), 0.038f, darkMetal);

            placementHighlight = new GameObject(PlacementHighlightName);
            placementHighlight.transform.SetParent(aircraft, false);
            CreatePart(placementHighlight.transform, PrimitiveType.Cube, "Engine Placement Ghost Volume",
                desiredEngineCenter,
                new Vector3(
                    Mathf.Clamp(engineBounds.size.x * 1.08f, 1.15f, 1.75f),
                    Mathf.Clamp(engineBounds.size.y * 1.05f, 0.90f, 1.55f),
                    Mathf.Clamp(engineBounds.size.z * 1.04f, 2.05f, 2.55f)),
                Vector3.zero,
                engineGhost);
            CreatePart(placementHighlight.transform, PrimitiveType.Cube, "Left Highlighted Mount Rail",
                new Vector3(-0.54f, 1.20f, 2.84f), new Vector3(0.16f, 0.045f, 2.90f), Vector3.zero, highlight);
            CreatePart(placementHighlight.transform, PrimitiveType.Cube, "Right Highlighted Mount Rail",
                new Vector3(0.54f, 1.20f, 2.84f), new Vector3(0.16f, 0.045f, 2.90f), Vector3.zero, highlight);

            Vector3[] mountBoltPositions =
            {
                new Vector3(-0.58f, 1.24f, 1.78f),
                new Vector3(0.58f, 1.24f, 1.78f),
                new Vector3(-0.58f, 1.24f, 3.78f),
                new Vector3(0.58f, 1.24f, 3.78f)
            };

            for (int index = 0; index < mountBoltPositions.Length; index++)
            {
                bool left = mountBoltPositions[index].x < 0f;
                GameObject targetObject = new GameObject($"P-51 Engine Mount Bolt Target {index + 1}");
                targetObject.transform.SetParent(aircraft, false);
                targetObject.transform.localPosition = mountBoltPositions[index];
                targetObject.transform.localRotation = Quaternion.Euler(0f, 0f, left ? -90f : 90f);
                SphereCollider collider = targetObject.AddComponent<SphereCollider>();
                collider.radius = 0.18f;
                collider.center = new Vector3(0f, 0.035f, 0f);

                GameObject boltAssembly = new GameObject("P-51 Engine Mount Bolt Assembly");
                boltAssembly.transform.SetParent(targetObject.transform, false);
                CreatePart(boltAssembly.transform, PrimitiveType.Cylinder, "Mount Bolt Shaft",
                    new Vector3(0f, -0.12f, 0f), new Vector3(0.050f, 0.13f, 0.050f), Vector3.zero, darkMetal);
                CreatePart(boltAssembly.transform, PrimitiveType.Cylinder, "Mount Bolt Washer",
                    new Vector3(0f, 0.008f, 0f), new Vector3(0.105f, 0.012f, 0.105f), Vector3.zero, aluminum);
                CreatePart(boltAssembly.transform, PrimitiveType.Cylinder, "Mount Bolt Hex Head",
                    new Vector3(0f, 0.055f, 0f), new Vector3(0.085f, 0.040f, 0.085f), Vector3.zero, hardware);
                CreatePart(boltAssembly.transform, PrimitiveType.Cylinder, "Mount Bolt Locking Nut",
                    new Vector3(0f, -0.25f, 0f), new Vector3(0.080f, 0.035f, 0.080f), Vector3.zero, hardware);

                GameObject boltHighlight = CreatePart(targetObject.transform, PrimitiveType.Cylinder, "Engine Mount Bolt Highlight",
                    new Vector3(0f, 0.012f, 0f), new Vector3(0.145f, 0.008f, 0.145f), Vector3.zero, highlight);
                CreatePart(placementHighlight.transform, PrimitiveType.Cylinder, $"Engine Receiver Pad Highlight {index + 1}",
                    mountBoltPositions[index], new Vector3(0.17f, 0.010f, 0.17f), new Vector3(0f, 0f, left ? -90f : 90f), highlight);

                AircraftServiceInteractionTarget target =
                    targetObject.AddComponent<AircraftServiceInteractionTarget>();
                target.Configure(
                    service,
                    AircraftServiceInteractionKind.EngineMountBolt,
                    index,
                    1.05f,
                    boltHighlight,
                    boltAssembly,
                    null,
                    0.22f,
                    3f);
            }

            placementHighlight.SetActive(false);
        }

        private static void InstallPlayerInteractor()
        {
            InventoryInteractor inventoryInteractor =
                Object.FindFirstObjectByType<InventoryInteractor>();
            if (inventoryInteractor == null)
            {
                Debug.LogWarning("P-51 setup could not find the Player InventoryInteractor.");
                return;
            }

            AircraftServicePlayerInteractor interactor =
                inventoryInteractor.GetComponent<AircraftServicePlayerInteractor>();
            if (interactor == null)
            {
                interactor = Undo.AddComponent<AircraftServicePlayerInteractor>(inventoryInteractor.gameObject);
            }

            Camera camera = inventoryInteractor.GetComponentInChildren<Camera>();
            InventoryUI inventoryUI = Object.FindFirstObjectByType<InventoryUI>();
            interactor.Configure(camera, inventoryUI);
            EditorUtility.SetDirty(interactor);
        }

        private static EngineAssemblyTransportController FindEngineTransport()
        {
            EngineAssemblyTransportController[] transports =
                Object.FindObjectsByType<EngineAssemblyTransportController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            return transports.Length > 0 ? transports[0] : null;
        }

        private static Transform CreateMarker(
            Transform parent,
            string objectName,
            Vector3 localPosition,
            Quaternion localRotation)
        {
            GameObject markerObject = new GameObject(objectName);
            markerObject.transform.SetParent(parent, false);
            markerObject.transform.localPosition = localPosition;
            markerObject.transform.localRotation = localRotation;
            markerObject.transform.localScale = Vector3.one;
            return markerObject.transform;
        }

        private static GameObject CreateMeshPart(
            Transform parent,
            string objectName,
            Mesh mesh,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Material material)
        {
            GameObject part = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation;
            part.transform.localScale = localScale;
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }

        private static GameObject CreatePart(
            Transform parent,
            PrimitiveType primitiveType,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEulerAngles,
            Material darkMaterial)
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType);
            part.name = objectName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(localEulerAngles);
            part.transform.localScale = localScale;
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = darkMaterial;
            }
            RemoveCollider(part);
            return part;
        }

        private static void CreateCylinderBetween(
            Transform parent,
            string objectName,
            Vector3 startLocal,
            Vector3 endLocal,
            float radius,
            Material material)
        {
            Vector3 direction = endLocal - startLocal;
            float length = direction.magnitude;
            GameObject cylinder = CreatePart(
                parent,
                PrimitiveType.Cylinder,
                objectName,
                (startLocal + endLocal) * 0.5f,
                new Vector3(radius, length * 0.5f, radius),
                Vector3.zero,
                material);
            if (length > 0.0001f)
            {
                cylinder.transform.localRotation =
                    Quaternion.FromToRotation(Vector3.up, direction.normalized);
            }
        }

        private static void RemoveCollider(GameObject gameObject)
        {
            Collider collider = gameObject != null ? gameObject.GetComponent<Collider>() : null;
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
        }

        private static Bounds CalculateEnginePhysicalBounds(
            GameObject root,
            Transform reference)
        {
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            bool hasPoint = false;
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);

            for (int index = 0; index < filters.Length; index++)
            {
                MeshFilter filter = filters[index];
                if (filter == null
                    || filter.sharedMesh == null
                    || IsEngineInteractionVisual(filter.transform, root.transform))
                {
                    continue;
                }

                Bounds meshBounds = filter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 localCorner = new Vector3(
                        (corner & 1) == 0 ? meshBounds.min.x : meshBounds.max.x,
                        (corner & 2) == 0 ? meshBounds.min.y : meshBounds.max.y,
                        (corner & 4) == 0 ? meshBounds.min.z : meshBounds.max.z);
                    Vector3 point = reference.InverseTransformPoint(
                        filter.transform.TransformPoint(localCorner));

                    if (!hasPoint)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        hasPoint = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }

            return hasPoint ? bounds : new Bounds(Vector3.zero, Vector3.one);
        }

        private static bool IsEngineInteractionVisual(Transform candidate, Transform root)
        {
            Transform current = candidate;
            while (current != null && current != root)
            {
                if (current.GetComponent<EngineAssemblyInteractionTarget>() != null)
                {
                    return true;
                }
                current = current.parent;
            }

            string objectName = candidate.name;
            return objectName.Contains("Highlight")
                || objectName.Contains("Placement Beacon")
                || objectName.Contains("Beacon Stem");
        }

        private static Bounds CalculateRendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds bounds = new Bounds(root.transform.position, Vector3.zero);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return bounds;
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

        private static Material CreateMaterial(
            string path,
            Color color,
            float metallic,
            float smoothness)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
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

        private static Material CreateTransparentMaterial(
            string path,
            Color color,
            float metallic,
            float smoothness)
        {
            Material material = CreateMaterial(path, color, metallic, smoothness);
            if (material == null)
            {
                return null;
            }

            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateTransparentEmissiveMaterial(
            string path,
            Color color,
            Color emission)
        {
            Material material = CreateTransparentMaterial(path, color, 0.05f, 0.36f);
            if (material != null && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
                EditorUtility.SetDirty(material);
            }
            return material;
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
