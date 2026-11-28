using System.Collections.Generic;
using Hanger51.Aircraft;
using Hanger51.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class P51PortableCowlingAndAirframeRefinementSetup
    {
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string VisualRootName = "P-51D Airframe Visuals";
        private const string CowlingName = "Removable Top Engine Cowling";
        private const string PanelTargetName = "Top Cowling Panel Service Target";
        private const string PlacementHighlightName = "P-51 Engine Bay Placement Highlight";
        private const string RefinementRootName = "P-51 Attached Airframe Refinement";
        private const string MountHardwareRootName = "P-51 Internal Engine Mount Refinement";

        private const string AluminumPath =
            "Assets/_Project/Aircraft/P51/Materials/PolishedAluminum.mat";
        private const string DarkMetalPath =
            "Assets/_Project/Aircraft/P51/Materials/DarkAircraftMetal.mat";
        private const string HardwarePath =
            "Assets/_Project/Aircraft/P51/Materials/ServiceHardware.mat";
        private const string HighlightPath =
            "Assets/_Project/Aircraft/P51/Materials/AircraftInstallHighlight.mat";
        private const string OlivePath =
            "Assets/_Project/Aircraft/P51/Materials/AntiGlareOlive.mat";
        private const string BlackPath =
            "Assets/_Project/Aircraft/P51/Materials/PropellerBlack.mat";
        private const string HexBoltMeshPath =
            "Assets/_Project/EngineAssembly/Meshes/HexBoltHead.asset";

        private static readonly Vector3[] CorrectMountBoltPositions =
        {
            new Vector3(-0.43f, 1.245f, 1.82f),
            new Vector3(0.43f, 1.245f, 1.82f),
            new Vector3(-0.43f, 1.245f, 3.66f),
            new Vector3(0.43f, 1.245f, 3.66f)
        };

        private static readonly string[] RemovedExactNames =
        {
            "Top Cowling Service Cradle",
            "Top Cowling Service-Cradle Pose",
            "Nose Anti-Glare Panel",
            "Left Flap Seam",
            "Right Flap Seam",
            "Left Aileron Seam",
            "Right Aileron Seam",
            "Red Tail Stripe 1",
            "Red Tail Stripe 2"
        };

        private static readonly string[] RemovedNamePrefixes =
        {
            "Red Propeller Tip"
        };

        [MenuItem("Hanger 51/P-51 Mustang/4 - Add Portable Cowling and Refine Airframe")]
        public static void AddPortableCowlingAndRefineAirframe()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("P-51 Step 4 failed. Exit Play mode first.");
                return;
            }

            GameObject aircraft = GameObject.Find(AircraftRootName);
            if (aircraft == null)
            {
                Debug.LogError("P-51 Step 4 failed. Run P-51 Step 1 before applying this refinement.");
                return;
            }

            P51AircraftServiceController service =
                aircraft.GetComponent<P51AircraftServiceController>();
            Transform cowling = FindDescendant(aircraft.transform, CowlingName);
            Transform panelTargetTransform = aircraft.transform.Find(PanelTargetName);
            AircraftServiceInteractionTarget panelTarget = panelTargetTransform != null
                ? panelTargetTransform.GetComponent<AircraftServiceInteractionTarget>()
                : null;
            if (service == null || cowling == null || panelTarget == null)
            {
                Debug.LogError(
                    "P-51 Step 4 failed. The aircraft service controller, removable cowling, or cowling target is missing.",
                    aircraft);
                return;
            }

            Material aluminum = AssetDatabase.LoadAssetAtPath<Material>(AluminumPath);
            Material darkMetal = AssetDatabase.LoadAssetAtPath<Material>(DarkMetalPath);
            Material hardware = AssetDatabase.LoadAssetAtPath<Material>(HardwarePath);
            Material highlight = AssetDatabase.LoadAssetAtPath<Material>(HighlightPath);
            Material olive = AssetDatabase.LoadAssetAtPath<Material>(OlivePath);
            Material black = AssetDatabase.LoadAssetAtPath<Material>(BlackPath);
            Mesh hexBoltMesh = AssetDatabase.LoadAssetAtPath<Mesh>(HexBoltMeshPath);

            RemoveDetachedGeneratedDetails(aircraft.transform);
            ConfigurePortableCowling(service, cowling, panelTarget, highlight);
            RebuildInternalMountBolts(
                aircraft.transform,
                service,
                aluminum,
                darkMetal,
                hardware,
                highlight,
                hexBoltMesh);
            BuildAttachedAirframeRefinement(
                aircraft.transform,
                aluminum,
                darkMetal,
                olive,
                black);

            service.RefreshTargetsAndVisuals();
            EditorUtility.SetDirty(service);

            Scene activeScene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (string.IsNullOrWhiteSpace(activeScene.path)
                || !EditorSceneManager.SaveScene(activeScene))
            {
                Debug.LogError("P-51 Step 4 completed the repair but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 Step 4 completed the repair, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = aircraft;
            Debug.Log(
                "P-51 Step 4 complete. Removed the fixed cowling cradle and detached decoration, added free cowling carry/placement, "
                + "rebuilt four vertical internal engine-mount bolts, added attached airframe refinements, saved the scene, and prepared Build and Run.",
                aircraft);
        }

        [MenuItem("Hanger 51/P-51 Mustang/5 - Validate Portable Cowling and Refined Airframe")]
        public static void ValidatePortableCowlingAndRefinedAirframe()
        {
            bool passed = true;
            GameObject aircraft = GameObject.Find(AircraftRootName);
            if (aircraft == null)
            {
                Debug.LogError("P-51 Step 5 failed: the P-51 aircraft is missing.");
                return;
            }

            P51AircraftServiceController service =
                aircraft.GetComponent<P51AircraftServiceController>();
            Transform cowling = FindDescendant(aircraft.transform, CowlingName);
            P51PortableCowlingPanel portablePanel = cowling != null
                ? cowling.GetComponent<P51PortableCowlingPanel>()
                : null;
            BoxCollider cowlingCollider = cowling != null
                ? cowling.GetComponent<BoxCollider>()
                : null;
            if (service == null
                || portablePanel == null
                || cowlingCollider == null
                || portablePanel.ServiceController != service)
            {
                Debug.LogError(
                    "P-51 Step 5 failed: the portable cowling component, pickup collider, or service reference is missing.",
                    aircraft);
                passed = false;
            }

            if (FindDescendant(aircraft.transform, "Top Cowling Service Cradle") != null
                || FindDescendant(aircraft.transform, "Top Cowling Service-Cradle Pose") != null)
            {
                Debug.LogError("P-51 Step 5 failed: the obsolete dedicated cowling cradle still exists.", aircraft);
                passed = false;
            }

            AircraftServiceInteractionTarget[] targets =
                aircraft.GetComponentsInChildren<AircraftServiceInteractionTarget>(true);
            int mountBoltCount = 0;
            for (int index = 0; index < targets.Length; index++)
            {
                AircraftServiceInteractionTarget target = targets[index];
                if (target == null
                    || target.InteractionKind != AircraftServiceInteractionKind.EngineMountBolt)
                {
                    continue;
                }

                mountBoltCount++;
                int boltIndex = target.TargetIndex;
                if (boltIndex < 0 || boltIndex >= CorrectMountBoltPositions.Length)
                {
                    Debug.LogError($"P-51 Step 5 failed: '{target.name}' has an invalid mount-bolt index.", target);
                    passed = false;
                    continue;
                }

                Vector3 localPosition = aircraft.transform.InverseTransformPoint(target.transform.position);
                float positionError = Vector3.Distance(localPosition, CorrectMountBoltPositions[boltIndex]);
                float verticalError = Vector3.Angle(target.transform.up, aircraft.transform.up);
                if (positionError > 0.015f || verticalError > 1.5f || Mathf.Abs(localPosition.x) > 0.50f)
                {
                    Debug.LogError(
                        $"P-51 Step 5 failed: '{target.name}' is not a vertical internal rail bolt. "
                        + $"Position error {positionError:F3}, vertical error {verticalError:F1} degrees.",
                        target);
                    passed = false;
                }
            }

            if (mountBoltCount != 4)
            {
                Debug.LogError($"P-51 Step 5 failed: expected 4 internal engine-mount bolts, found {mountBoltCount}.");
                passed = false;
            }

            for (int index = 0; index < RemovedExactNames.Length; index++)
            {
                if (FindDescendant(aircraft.transform, RemovedExactNames[index]) != null)
                {
                    Debug.LogError(
                        $"P-51 Step 5 failed: detached generated object '{RemovedExactNames[index]}' still exists.",
                        aircraft);
                    passed = false;
                }
            }

            Transform[] allTransforms = aircraft.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < allTransforms.Length; index++)
            {
                for (int prefixIndex = 0; prefixIndex < RemovedNamePrefixes.Length; prefixIndex++)
                {
                    if (allTransforms[index] != null
                        && allTransforms[index].name.StartsWith(RemovedNamePrefixes[prefixIndex]))
                    {
                        Debug.LogError(
                            $"P-51 Step 5 failed: detached generated object '{allTransforms[index].name}' still exists.",
                            allTransforms[index]);
                        passed = false;
                    }
                }
            }

            Transform refinementRoot = FindDescendant(aircraft.transform, RefinementRootName);
            int refinementRendererCount = refinementRoot != null
                ? refinementRoot.GetComponentsInChildren<Renderer>(true).Length
                : 0;
            if (refinementRoot == null || refinementRendererCount < 12)
            {
                Debug.LogError(
                    $"P-51 Step 5 failed: expected at least 12 attached refinement renderers, found {refinementRendererCount}.",
                    aircraft);
                passed = false;
            }

            InventoryInteractor inventoryInteractor =
                Object.FindFirstObjectByType<InventoryInteractor>();
            AircraftServicePlayerInteractor playerInteractor = inventoryInteractor != null
                ? inventoryInteractor.GetComponent<AircraftServicePlayerInteractor>()
                : null;
            if (playerInteractor == null)
            {
                Debug.LogError("P-51 Step 5 failed: the Player aircraft-service interactor is missing.");
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("P-51 Step 5 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"P-51 Step 5 passed. The cowling is freely portable, all four mount bolts are vertical and inside the engine bay, "
                    + $"obsolete floating objects are removed, {refinementRendererCount} attached refinements are present, and Build and Run is ready.",
                    aircraft);
            }
        }

        private static void ConfigurePortableCowling(
            P51AircraftServiceController service,
            Transform cowling,
            AircraftServiceInteractionTarget panelTarget,
            Material highlightMaterial)
        {
            BoxCollider pickupCollider = cowling.GetComponent<BoxCollider>();
            if (pickupCollider == null)
            {
                pickupCollider = Undo.AddComponent<BoxCollider>(cowling.gameObject);
            }
            pickupCollider.center = Vector3.zero;
            pickupCollider.size = new Vector3(1.38f, 0.54f, 3.20f);

            P51PortableCowlingPanel portablePanel =
                cowling.GetComponent<P51PortableCowlingPanel>();
            if (portablePanel == null)
            {
                portablePanel = Undo.AddComponent<P51PortableCowlingPanel>(cowling.gameObject);
            }
            portablePanel.Configure(service, pickupCollider);
            service.ConfigurePortableCowling(portablePanel);

            GameObject panelHighlight = FindDescendant(panelTarget.transform, "Top Cowling Placement Highlight")?.gameObject;
            if (panelHighlight == null)
            {
                MeshFilter cowlingFilter = cowling.GetComponent<MeshFilter>();
                if (cowlingFilter != null && cowlingFilter.sharedMesh != null)
                {
                    panelHighlight = CreateMeshPart(
                        panelTarget.transform,
                        "Top Cowling Placement Highlight",
                        cowlingFilter.sharedMesh,
                        Vector3.zero,
                        Quaternion.identity,
                        Vector3.one * 1.012f,
                        highlightMaterial);
                }
            }

            panelTarget.Configure(
                service,
                AircraftServiceInteractionKind.CowlingPanel,
                0,
                1.10f,
                panelHighlight,
                cowling.gameObject,
                null,
                0.42f,
                0f);

            service.RefreshTargetsAndVisuals();
            EditorUtility.SetDirty(portablePanel);
            EditorUtility.SetDirty(panelTarget);
        }

        private static void RebuildInternalMountBolts(
            Transform aircraft,
            P51AircraftServiceController service,
            Material aluminum,
            Material darkMetal,
            Material hardware,
            Material highlight,
            Mesh hexBoltMesh)
        {
            Transform placementHighlight = FindDescendant(aircraft, PlacementHighlightName);
            if (placementHighlight != null)
            {
                RemoveChildrenStartingWith(placementHighlight, "Engine Receiver Pad Highlight");
            }

            Transform oldHardware = aircraft.Find(MountHardwareRootName);
            if (oldHardware != null)
            {
                Undo.DestroyObjectImmediate(oldHardware.gameObject);
            }

            Transform hardwareRoot = new GameObject(MountHardwareRootName).transform;
            Undo.RegisterCreatedObjectUndo(hardwareRoot.gameObject, "Create internal P-51 engine mount refinement");
            hardwareRoot.SetParent(aircraft, false);

            AircraftServiceInteractionTarget[] targets =
                aircraft.GetComponentsInChildren<AircraftServiceInteractionTarget>(true);
            for (int index = 0; index < targets.Length; index++)
            {
                AircraftServiceInteractionTarget target = targets[index];
                if (target == null
                    || target.InteractionKind != AircraftServiceInteractionKind.EngineMountBolt)
                {
                    continue;
                }

                int boltIndex = target.TargetIndex;
                if (boltIndex < 0 || boltIndex >= CorrectMountBoltPositions.Length)
                {
                    continue;
                }

                ClearChildren(target.transform);
                target.transform.SetParent(aircraft, false);
                target.transform.localPosition = CorrectMountBoltPositions[boltIndex];
                target.transform.localRotation = Quaternion.identity;
                target.transform.localScale = Vector3.one;

                SphereCollider collider = target.GetComponent<SphereCollider>();
                if (collider == null)
                {
                    collider = Undo.AddComponent<SphereCollider>(target.gameObject);
                }
                collider.center = new Vector3(0f, 0.025f, 0f);
                collider.radius = 0.145f;

                GameObject bracket = CreatePart(
                    hardwareRoot,
                    PrimitiveType.Cube,
                    $"Engine Mount Saddle {boltIndex + 1}",
                    CorrectMountBoltPositions[boltIndex] + new Vector3(0f, -0.085f, 0f),
                    new Vector3(0.25f, 0.10f, 0.30f),
                    Vector3.zero,
                    darkMetal);
                RemoveCollider(bracket);

                GameObject assembly = new GameObject("Internal Vertical Engine Mount Bolt Assembly");
                assembly.transform.SetParent(target.transform, false);

                CreatePart(assembly.transform, PrimitiveType.Cylinder, "Mount Bolt Threaded Shaft",
                    new Vector3(0f, -0.082f, 0f),
                    new Vector3(0.035f, 0.090f, 0.035f),
                    Vector3.zero,
                    darkMetal);
                CreatePart(assembly.transform, PrimitiveType.Cylinder, "Mount Bolt Washer",
                    new Vector3(0f, 0.004f, 0f),
                    new Vector3(0.075f, 0.008f, 0.075f),
                    Vector3.zero,
                    aluminum);

                if (hexBoltMesh != null)
                {
                    CreateMeshPart(
                        assembly.transform,
                        "Mount Bolt Hex Head",
                        hexBoltMesh,
                        new Vector3(0f, 0.038f, 0f),
                        Quaternion.identity,
                        new Vector3(0.066f, 0.030f, 0.066f),
                        hardware);
                }
                else
                {
                    CreatePart(assembly.transform, PrimitiveType.Cylinder, "Mount Bolt Head",
                        new Vector3(0f, 0.038f, 0f),
                        new Vector3(0.066f, 0.030f, 0.066f),
                        Vector3.zero,
                        hardware);
                }

                CreatePart(assembly.transform, PrimitiveType.Cylinder, "Captive Lock Nut",
                    new Vector3(0f, -0.185f, 0f),
                    new Vector3(0.060f, 0.026f, 0.060f),
                    Vector3.zero,
                    hardware);

                GameObject boltHighlight = CreatePart(
                    target.transform,
                    PrimitiveType.Cylinder,
                    "Engine Mount Bolt Highlight",
                    new Vector3(0f, 0.010f, 0f),
                    new Vector3(0.115f, 0.006f, 0.115f),
                    Vector3.zero,
                    highlight);

                if (placementHighlight != null)
                {
                    CreatePart(
                        placementHighlight,
                        PrimitiveType.Cylinder,
                        $"Engine Receiver Pad Highlight {boltIndex + 1}",
                        CorrectMountBoltPositions[boltIndex],
                        new Vector3(0.13f, 0.006f, 0.13f),
                        Vector3.zero,
                        highlight);
                }

                target.Configure(
                    service,
                    AircraftServiceInteractionKind.EngineMountBolt,
                    boltIndex,
                    0.95f,
                    boltHighlight,
                    assembly,
                    null,
                    0.16f,
                    3f);
                EditorUtility.SetDirty(target);
            }

            BuildMountBracing(hardwareRoot, darkMetal, aluminum);
        }

        private static void BuildMountBracing(
            Transform parent,
            Material darkMetal,
            Material aluminum)
        {
            CreateCylinderBetween(parent, "Left Rear Mount Brace",
                new Vector3(-0.43f, 1.17f, 1.82f),
                new Vector3(-0.54f, 1.05f, 1.43f),
                0.032f,
                darkMetal);
            CreateCylinderBetween(parent, "Right Rear Mount Brace",
                new Vector3(0.43f, 1.17f, 1.82f),
                new Vector3(0.54f, 1.05f, 1.43f),
                0.032f,
                darkMetal);
            CreateCylinderBetween(parent, "Left Front Mount Brace",
                new Vector3(-0.43f, 1.17f, 3.66f),
                new Vector3(-0.55f, 1.05f, 4.02f),
                0.032f,
                darkMetal);
            CreateCylinderBetween(parent, "Right Front Mount Brace",
                new Vector3(0.43f, 1.17f, 3.66f),
                new Vector3(0.55f, 1.05f, 4.02f),
                0.032f,
                darkMetal);
            CreatePart(parent, PrimitiveType.Cube, "Left Engine Foot Rail",
                new Vector3(-0.43f, 1.155f, 2.74f),
                new Vector3(0.16f, 0.055f, 2.22f),
                Vector3.zero,
                aluminum);
            CreatePart(parent, PrimitiveType.Cube, "Right Engine Foot Rail",
                new Vector3(0.43f, 1.155f, 2.74f),
                new Vector3(0.16f, 0.055f, 2.22f),
                Vector3.zero,
                aluminum);
        }

        private static void BuildAttachedAirframeRefinement(
            Transform aircraft,
            Material aluminum,
            Material darkMetal,
            Material olive,
            Material black)
        {
            Transform visualRoot = aircraft.Find(VisualRootName);
            if (visualRoot == null)
            {
                return;
            }

            Transform oldRoot = visualRoot.Find(RefinementRootName);
            if (oldRoot != null)
            {
                Undo.DestroyObjectImmediate(oldRoot.gameObject);
            }

            Transform root = new GameObject(RefinementRootName).transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Create attached P-51 airframe refinement");
            root.SetParent(visualRoot, false);

            GameObject leftFairing = CreatePart(root, PrimitiveType.Sphere, "Left Wing Root Fairing",
                new Vector3(-0.54f, 1.36f, 0.04f),
                new Vector3(0.72f, 0.18f, 1.30f),
                new Vector3(0f, 0f, -3f),
                aluminum);
            RemoveCollider(leftFairing);
            GameObject rightFairing = CreatePart(root, PrimitiveType.Sphere, "Right Wing Root Fairing",
                new Vector3(0.54f, 1.36f, 0.04f),
                new Vector3(0.72f, 0.18f, 1.30f),
                new Vector3(0f, 0f, 3f),
                aluminum);
            RemoveCollider(rightFairing);

            GameObject tailFillet = CreatePart(root, PrimitiveType.Sphere, "Dorsal Fin Root Fillet",
                new Vector3(0f, 2.08f, -3.63f),
                new Vector3(0.27f, 0.55f, 0.82f),
                new Vector3(-10f, 0f, 0f),
                aluminum);
            RemoveCollider(tailFillet);

            GameObject scoopFairing = CreatePart(root, PrimitiveType.Sphere, "Radiator Scoop Transition Fairing",
                new Vector3(0f, 0.91f, -1.19f),
                new Vector3(0.55f, 0.26f, 1.08f),
                new Vector3(-4f, 0f, 0f),
                aluminum);
            RemoveCollider(scoopFairing);

            CreatePart(root, PrimitiveType.Cube, "Armored Windshield Lower Frame",
                new Vector3(0f, 1.98f, -0.10f),
                new Vector3(1.00f, 0.055f, 0.08f),
                new Vector3(-10f, 0f, 0f),
                darkMetal);
            CreatePart(root, PrimitiveType.Cube, "Left Windshield Post",
                new Vector3(-0.48f, 2.17f, -0.13f),
                new Vector3(0.045f, 0.52f, 0.055f),
                new Vector3(0f, 0f, -12f),
                darkMetal);
            CreatePart(root, PrimitiveType.Cube, "Right Windshield Post",
                new Vector3(0.48f, 2.17f, -0.13f),
                new Vector3(0.045f, 0.52f, 0.055f),
                new Vector3(0f, 0f, 12f),
                darkMetal);

            CreatePart(root, PrimitiveType.Cube, "Attached Anti-Glare Deck",
                new Vector3(0f, 2.025f, 0.57f),
                new Vector3(0.66f, 0.018f, 0.78f),
                new Vector3(4f, 0f, 0f),
                olive);

            Transform propeller = FindDescendant(visualRoot, "Four-Blade Hamilton Standard Propeller");
            if (propeller != null)
            {
                CreatePart(propeller, PrimitiveType.Cylinder, "Spinner Backplate",
                    new Vector3(0f, 0f, 0.13f),
                    new Vector3(0.39f, 0.025f, 0.39f),
                    new Vector3(90f, 0f, 0f),
                    aluminum);

                for (int bladeIndex = 1; bladeIndex <= 4; bladeIndex++)
                {
                    Transform blade = FindDescendant(propeller, $"Propeller Blade {bladeIndex}");
                    if (blade == null)
                    {
                        continue;
                    }

                    GameObject cuff = CreatePart(blade, PrimitiveType.Sphere, $"Blade Root Cuff {bladeIndex}",
                        new Vector3(0f, 0.24f, 0.025f),
                        new Vector3(0.18f, 0.30f, 0.075f),
                        Vector3.zero,
                        black);
                    RemoveCollider(cuff);
                }
            }

            CreatePart(root, PrimitiveType.Cube, "Left Exhaust Shroud",
                new Vector3(-0.615f, 1.63f, 2.73f),
                new Vector3(0.025f, 0.24f, 2.32f),
                Vector3.zero,
                darkMetal);
            CreatePart(root, PrimitiveType.Cube, "Right Exhaust Shroud",
                new Vector3(0.615f, 1.63f, 2.73f),
                new Vector3(0.025f, 0.24f, 2.32f),
                Vector3.zero,
                darkMetal);
        }

        private static void RemoveDetachedGeneratedDetails(Transform aircraft)
        {
            for (int index = 0; index < RemovedExactNames.Length; index++)
            {
                Transform found = FindDescendant(aircraft, RemovedExactNames[index]);
                if (found != null)
                {
                    Undo.DestroyObjectImmediate(found.gameObject);
                }
            }

            Transform[] transforms = aircraft.GetComponentsInChildren<Transform>(true);
            List<GameObject> toRemove = new List<GameObject>();
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index] == null)
                {
                    continue;
                }

                for (int prefixIndex = 0; prefixIndex < RemovedNamePrefixes.Length; prefixIndex++)
                {
                    if (transforms[index].name.StartsWith(RemovedNamePrefixes[prefixIndex]))
                    {
                        toRemove.Add(transforms[index].gameObject);
                        break;
                    }
                }
            }

            for (int index = 0; index < toRemove.Count; index++)
            {
                if (toRemove[index] != null)
                {
                    Undo.DestroyObjectImmediate(toRemove[index]);
                }
            }
        }

        private static void RemoveChildrenStartingWith(Transform parent, string prefix)
        {
            List<GameObject> children = new List<GameObject>();
            foreach (Transform child in parent)
            {
                if (child.name.StartsWith(prefix))
                {
                    children.Add(child.gameObject);
                }
            }

            for (int index = 0; index < children.Count; index++)
            {
                Undo.DestroyObjectImmediate(children[index]);
            }
        }

        private static void ClearChildren(Transform parent)
        {
            List<GameObject> children = new List<GameObject>();
            foreach (Transform child in parent)
            {
                children.Add(child.gameObject);
            }

            for (int index = 0; index < children.Count; index++)
            {
                Undo.DestroyObjectImmediate(children[index]);
            }
        }

        private static GameObject CreatePart(
            Transform parent,
            PrimitiveType type,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEulerAngles,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
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

            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
            return part;
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
            Undo.RegisterCreatedObjectUndo(part, $"Create {objectName}");
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation;
            part.transform.localScale = localScale;
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }

        private static GameObject CreateCylinderBetween(
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
            return cylinder;
        }

        private static void RemoveCollider(GameObject target)
        {
            Collider collider = target != null ? target.GetComponent<Collider>() : null;
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
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
    }
}
