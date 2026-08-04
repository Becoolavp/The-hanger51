using System.Collections.Generic;
using Hanger51.Aircraft;
using Hanger51.EngineAssembly;
using Hanger51.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class MerlinConditionAndP51EnvelopeSetup
    {
        private const string ConditionVisualRootName = "Merlin Oil and Condition Visuals";
        private const string OilSupplyRootName = "Merlin Oil Service Supplies";
        private const string MaterialFolder = "Assets/_Project/EngineAssembly/Materials/Condition";

        private sealed class ConditionMaterials
        {
            public Material Crack;
            public Material Burn;
            public Material Oil;
            public Material Fire;
            public Material CanBody;
            public Material CanMetal;
            public Material CanCap;
        }

        [MenuItem("Hanger 51/P-51 Mustang/24 - Repair Extreme Banks and Wheel Contact")]
        public static void RepairExtremeBanksAndWheelContact()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("P-51 Step 24 failed. Exit Play mode first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            P51FlightController flight = Object.FindFirstObjectByType<P51FlightController>();
            P51RaycastLandingGear gear = flight != null
                ? flight.GetComponent<P51RaycastLandingGear>()
                : null;
            P51LandingAndRudderController landing = flight != null
                ? flight.GetComponent<P51LandingAndRudderController>()
                : null;
            P51TurnPerformanceAssist turn = flight != null
                ? flight.GetComponent<P51TurnPerformanceAssist>()
                : null;

            if (!scene.IsValid()
                || string.IsNullOrWhiteSpace(scene.path)
                || flight == null
                || gear == null
                || landing == null)
            {
                Debug.LogError(
                    "P-51 Step 24 failed. Open the saved movement-test scene and confirm Steps 22 and 23 are already installed.");
                return;
            }

            P51ExtremeBankLiftReserve reserve =
                flight.GetComponent<P51ExtremeBankLiftReserve>();
            if (reserve == null)
            {
                reserve = Undo.AddComponent<P51ExtremeBankLiftReserve>(flight.gameObject);
            }
            reserve.Configure(58f, 84f, 27f, 40f, 0.70f, 9000f);
            EditorUtility.SetDirty(reserve);

            SerializedObject serializedGear = new SerializedObject(gear);
            SetFloat(serializedGear, "minimumSupportingForce", 1600f);
            SetFloat(serializedGear, "releaseWhileClimbingSpeed", 0.22f);
            SetFloat(serializedGear, "visualPositionSharpness", 18f);
            SetFloat(serializedGear, "airborneVisualReturnSharpness", 14f);
            serializedGear.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(gear);

            SerializedObject serializedLanding = new SerializedObject(landing);
            SetFloat(serializedLanding, "rolloutAdhesionAcceleration", 0f);
            SetFloat(serializedLanding, "touchdownDampingWindowSeconds", 0.55f);
            SetFloat(serializedLanding, "upwardReboundDamping", 8.5f);
            serializedLanding.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(landing);

            if (turn != null)
            {
                SerializedObject serializedTurn = new SerializedObject(turn);
                SetFloat(serializedTurn, "bankLiftSupport", 0.62f);
                SetFloat(serializedTurn, "maximumExtraLoadG", 1.10f);
                SetFloat(serializedTurn, "maximumAssistedBankDegrees", 82f);
                serializedTurn.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(turn);
            }

            SaveSceneAndPrepareBuild(
                scene,
                "P-51 Step 24",
                flight.gameObject,
                "P-51 Step 24 complete. Steep banks now retain a limited vertical lift reserve, wheel ray hits are separated from load-bearing contact, wheel visuals extend smoothly, and continuous ground adhesion is disabled.");
        }

        [MenuItem("Hanger 51/P-51 Mustang/25 - Validate Extreme Banks and Wheel Contact")]
        public static void ValidateExtremeBanksAndWheelContact()
        {
            bool passed = true;
            P51FlightController flight = Object.FindFirstObjectByType<P51FlightController>();
            P51RaycastLandingGear gear = flight != null
                ? flight.GetComponent<P51RaycastLandingGear>()
                : null;
            P51LandingAndRudderController landing = flight != null
                ? flight.GetComponent<P51LandingAndRudderController>()
                : null;
            P51ExtremeBankLiftReserve reserve = flight != null
                ? flight.GetComponent<P51ExtremeBankLiftReserve>()
                : null;

            if (flight == null || gear == null || landing == null || reserve == null)
            {
                Debug.LogError(
                    "P-51 Step 25 failed: the flight, landing gear, landing controller, or steep-bank reserve is missing.");
                passed = false;
            }
            else
            {
                SerializedObject serializedGear = new SerializedObject(gear);
                passed &= ValidateFloat(
                    serializedGear,
                    "minimumSupportingForce",
                    1600f,
                    "minimum supporting wheel force");
                passed &= ValidateFloat(
                    serializedGear,
                    "releaseWhileClimbingSpeed",
                    0.22f,
                    "wheel release climb speed");

                SerializedObject serializedLanding = new SerializedObject(landing);
                passed &= ValidateFloat(
                    serializedLanding,
                    "rolloutAdhesionAcceleration",
                    0f,
                    "continuous rollout adhesion");

                if (reserve.MaximumVerticalGravitySupport < 0.65f
                    || reserve.FullSupportDegrees < 80f)
                {
                    Debug.LogError(
                        "P-51 Step 25 failed: the steep-bank lift reserve is outside the expected range.",
                        reserve);
                    passed = false;
                }
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("P-51 Step 25 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "P-51 Step 25 passed. Steep-bank lift reserve, load-bearing wheel detection, smooth wheel extension, climb release, and non-sticky touchdown damping are configured.");
            }
        }

        [MenuItem("Hanger 51/Merlin Condition/1 - Add Oil, Wear, Damage, and Inspection")]
        public static void AddOilWearDamageAndInspection()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Merlin Condition Step 1 failed. Exit Play mode first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            EngineAssemblyStation[] stations = Object.FindObjectsByType<EngineAssemblyStation>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            InventoryInteractor player = Object.FindFirstObjectByType<InventoryInteractor>();
            InventoryUI inventoryUI = Object.FindFirstObjectByType<InventoryUI>();
            Camera playerCamera = player != null
                ? player.GetComponentInChildren<Camera>(true)
                : null;

            if (!scene.IsValid()
                || string.IsNullOrWhiteSpace(scene.path)
                || stations.Length == 0
                || player == null
                || inventoryUI == null
                || playerCamera == null)
            {
                Debug.LogError(
                    "Merlin Condition Step 1 failed. Open the saved movement-test scene and confirm the Merlin assembly and Player inventory systems exist.");
                return;
            }

            EnsureFolder("Assets/_Project/EngineAssembly/Materials", "Condition");
            ConditionMaterials materials = CreateMaterials();

            int configuredCount = 0;
            EngineAssemblyStation activeReferenceStation = null;
            for (int index = 0; index < stations.Length; index++)
            {
                EngineAssemblyStation station = stations[index];
                if (station == null)
                {
                    continue;
                }

                if (ConfigureConditionForStation(station, materials))
                {
                    configuredCount++;
                    if (activeReferenceStation == null && station.gameObject.activeInHierarchy)
                    {
                        activeReferenceStation = station;
                    }
                }
            }

            if (activeReferenceStation == null)
            {
                activeReferenceStation = stations[0];
            }

            BuildOilServiceSupplies(activeReferenceStation, materials);

            EngineConditionPlayerInteractor conditionInteractor =
                player.GetComponent<EngineConditionPlayerInteractor>();
            if (conditionInteractor == null)
            {
                conditionInteractor = Undo.AddComponent<EngineConditionPlayerInteractor>(
                    player.gameObject);
            }
            conditionInteractor.Configure(playerCamera, inventoryUI);
            EditorUtility.SetDirty(conditionInteractor);

            P51FlightController flight = Object.FindFirstObjectByType<P51FlightController>();
            if (flight != null)
            {
                P51EngineConditionPowerBridge bridge =
                    flight.GetComponent<P51EngineConditionPowerBridge>();
                if (bridge == null)
                {
                    bridge = Undo.AddComponent<P51EngineConditionPowerBridge>(
                        flight.gameObject);
                }

                SerializedObject serializedFlight = new SerializedObject(flight);
                SerializedProperty maximumThrust =
                    serializedFlight.FindProperty("maximumThrustNewtons");
                bridge.Configure(maximumThrust != null
                    ? maximumThrust.floatValue
                    : 24000f);
                EditorUtility.SetDirty(bridge);
            }

            SaveSceneAndPrepareBuild(
                scene,
                "Merlin Condition Step 1",
                activeReferenceStation != null
                    ? activeReferenceStation.gameObject
                    : player.gameObject,
                $"Merlin Condition Step 1 complete. Configured {configuredCount} engine condition owner(s), dipsticks, oil fillers, block/plug/cover wear, crack/fire/oil-leak visuals, inspection targets, two carryable oil cans, and installed-engine power reduction.");
        }

        [MenuItem("Hanger 51/Merlin Condition/2 - Validate Oil, Wear, Damage, and Inspection")]
        public static void ValidateOilWearDamageAndInspection()
        {
            bool passed = true;
            EngineConditionController[] conditions =
                Object.FindObjectsByType<EngineConditionController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            if (conditions.Length == 0)
            {
                Debug.LogError("Merlin Condition Step 2 failed: no engine condition controllers exist.");
                passed = false;
            }

            for (int index = 0; index < conditions.Length; index++)
            {
                EngineConditionController condition = conditions[index];
                EngineAssemblyTransportController transport =
                    condition.GetComponent<EngineAssemblyTransportController>();
                if (transport == null
                    || transport.TransportRoot == null
                    || transport.TransportRoot.GetComponent<EngineConditionLink>() == null)
                {
                    Debug.LogError(
                        $"Merlin Condition Step 2 failed: '{condition.name}' has no portable condition link.",
                        condition);
                    passed = false;
                }

                if (condition.OilCapacityLiters < 19.9f
                    || condition.OilQuantityLiters < 19.9f)
                {
                    Debug.LogError(
                        $"Merlin Condition Step 2 failed: '{condition.name}' does not begin with a full 20 L oil system.",
                        condition);
                    passed = false;
                }
            }

            if (Object.FindFirstObjectByType<EngineConditionPlayerInteractor>() == null)
            {
                Debug.LogError("Merlin Condition Step 2 failed: the Player condition interactor is missing.");
                passed = false;
            }

            EngineOilCanController[] cans = Object.FindObjectsByType<EngineOilCanController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (cans.Length < 2)
            {
                Debug.LogError(
                    $"Merlin Condition Step 2 failed: expected at least two oil cans, found {cans.Length}.");
                passed = false;
            }

            P51FlightController flight = Object.FindFirstObjectByType<P51FlightController>();
            if (flight == null
                || flight.GetComponent<P51EngineConditionPowerBridge>() == null)
            {
                Debug.LogError("Merlin Condition Step 2 failed: the P-51 engine-condition power bridge is missing.");
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("Merlin Condition Step 2 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Merlin Condition Step 2 passed. Oil quantity, dipstick, filler, oil cans, independent plug/cover/block condition, inspection, damage visuals, leaks, fire, and thrust reduction are configured.");
            }
        }

        [MenuItem("Hanger 51/Merlin Condition/3 - Apply Visible Test Damage to Selected Engine")]
        public static void ApplyVisibleTestDamage()
        {
            EngineConditionController condition = FindSelectedCondition();
            if (condition == null)
            {
                Debug.LogError("No engine condition controller could be found for the selected object.");
                return;
            }

            condition.ApplyDebugWear(42f, 38f, 28f, 68f, 5f);
            EditorUtility.SetDirty(condition);
            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
            Debug.Log(
                "Applied test damage: low oil, worn plugs, a cracked left cover, a weakened right cover, and a damaged block.",
                condition);
        }

        [MenuItem("Hanger 51/Merlin Condition/4 - Restore Selected Engine to New Condition")]
        public static void RestoreSelectedEngineCondition()
        {
            EngineConditionController condition = FindSelectedCondition();
            if (condition == null)
            {
                Debug.LogError("No engine condition controller could be found for the selected object.");
                return;
            }

            condition.InitializeNewEngineCondition();
            EditorUtility.SetDirty(condition);
            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
            Debug.Log("Restored the selected Merlin to full oil and 100% component condition.", condition);
        }

        private static bool ConfigureConditionForStation(
            EngineAssemblyStation station,
            ConditionMaterials materials)
        {
            EngineAssemblyTransportController transport =
                station.GetComponent<EngineAssemblyTransportController>();
            if (transport == null || transport.TransportRoot == null)
            {
                Debug.LogWarning(
                    $"Skipped '{station.name}' because its portable engine transport is missing.",
                    station);
                return false;
            }

            SerializedObject serializedStation = new SerializedObject(station);
            GameObject engineCore = GetObjectReference<GameObject>(
                serializedStation,
                "engineCoreVisual");
            GameObject[] coverVisuals = GetObjectReferenceArray<GameObject>(
                serializedStation,
                "cylinderCoverVisuals");
            GameObject[] sparkVisuals = GetObjectReferenceArray<GameObject>(
                serializedStation,
                "sparkPlugVisuals");
            if (engineCore == null || coverVisuals.Length < 2 || sparkVisuals.Length < 24)
            {
                Debug.LogWarning(
                    $"Skipped '{station.name}' because its configured engine visuals are incomplete.",
                    station);
                return false;
            }

            bool newlyAdded = false;
            EngineConditionController condition =
                station.GetComponent<EngineConditionController>();
            if (condition == null)
            {
                condition = Undo.AddComponent<EngineConditionController>(station.gameObject);
                newlyAdded = true;
            }

            EngineConditionLink link =
                transport.TransportRoot.GetComponent<EngineConditionLink>();
            if (link == null)
            {
                link = Undo.AddComponent<EngineConditionLink>(
                    transport.TransportRoot.gameObject);
            }
            link.Configure(condition);
            EditorUtility.SetDirty(link);

            Transform oldVisualRoot = FindDirectChild(
                transport.TransportRoot,
                ConditionVisualRootName);
            if (oldVisualRoot != null)
            {
                Undo.DestroyObjectImmediate(oldVisualRoot.gameObject);
            }

            GameObject visualRootObject = new GameObject(ConditionVisualRootName);
            Undo.RegisterCreatedObjectUndo(visualRootObject, "Create Merlin condition visuals");
            visualRootObject.transform.SetParent(transport.TransportRoot, false);
            Transform visualRoot = visualRootObject.transform;

            EngineAssemblyInteractionTarget[] allTargets =
                station.GetComponentsInChildren<EngineAssemblyInteractionTarget>(true);
            EngineAssemblyInteractionTarget[] coverTargets =
                new EngineAssemblyInteractionTarget[2];
            EngineAssemblyInteractionTarget[] sparkTargets =
                new EngineAssemblyInteractionTarget[24];
            for (int index = 0; index < allTargets.Length; index++)
            {
                EngineAssemblyInteractionTarget target = allTargets[index];
                if (target.InteractionKind == EngineAssemblyInteractionKind.CoverPlacement
                    && target.GroupIndex >= 0
                    && target.GroupIndex < coverTargets.Length)
                {
                    coverTargets[target.GroupIndex] = target;
                }
                else if (target.InteractionKind == EngineAssemblyInteractionKind.SparkPlug
                    && target.TargetIndex >= 0
                    && target.TargetIndex < sparkTargets.Length)
                {
                    sparkTargets[target.TargetIndex] = target;
                }
            }

            Renderer[] plugRenderers = new Renderer[24];
            for (int index = 0; index < plugRenderers.Length; index++)
            {
                plugRenderers[index] = FindPreferredRenderer(sparkVisuals[index]);
            }

            Bounds blockBounds = CalculateLocalBounds(
                transport.TransportRoot,
                engineCore.GetComponentsInChildren<Renderer>(true));
            Bounds[] coverBounds = new Bounds[2];
            for (int index = 0; index < coverBounds.Length; index++)
            {
                coverBounds[index] = CalculateLocalBounds(
                    transport.TransportRoot,
                    coverVisuals[index].GetComponentsInChildren<Renderer>(true));
            }

            GameObject[] blockDamageStages = BuildBlockDamageStages(
                visualRoot,
                blockBounds,
                materials);
            GameObject[] coverCrackRoots = new GameObject[2];
            ParticleSystem[] fireEffects = new ParticleSystem[2];
            ParticleSystem[] leakEffects = new ParticleSystem[2];
            for (int index = 0; index < 2; index++)
            {
                BuildCoverDamageVisuals(
                    visualRoot,
                    coverBounds[index],
                    index,
                    materials,
                    out coverCrackRoots[index],
                    out fireEffects[index],
                    out leakEffects[index]);
            }

            CreateFollowerInspectionTarget(
                visualRoot,
                "Engine Block Condition Inspection",
                engineCore.transform,
                engineCore.GetComponentsInChildren<Renderer>(true),
                condition,
                EngineConditionInspectionKind.EngineBlock,
                0);
            for (int index = 0; index < 2; index++)
            {
                CreateFollowerInspectionTarget(
                    visualRoot,
                    $"Cylinder Cover {index + 1} Condition Inspection",
                    coverVisuals[index].transform,
                    coverVisuals[index].GetComponentsInChildren<Renderer>(true),
                    condition,
                    EngineConditionInspectionKind.CylinderCover,
                    index);
            }

            for (int index = 0; index < sparkTargets.Length; index++)
            {
                if (sparkTargets[index] == null)
                {
                    continue;
                }

                EngineConditionInspectionTarget inspection =
                    sparkTargets[index].GetComponent<EngineConditionInspectionTarget>();
                if (inspection == null)
                {
                    inspection = Undo.AddComponent<EngineConditionInspectionTarget>(
                        sparkTargets[index].gameObject);
                }
                inspection.Configure(
                    condition,
                    EngineConditionInspectionKind.SparkPlug,
                    index);
                EditorUtility.SetDirty(inspection);
            }

            BuildDipstickAndFiller(
                visualRoot,
                blockBounds,
                condition,
                materials);

            condition.Configure(
                station,
                transport,
                coverTargets,
                sparkTargets,
                plugRenderers,
                blockDamageStages,
                coverCrackRoots,
                fireEffects,
                leakEffects,
                20f);
            if (newlyAdded)
            {
                condition.InitializeNewEngineCondition();
            }
            EditorUtility.SetDirty(condition);
            return true;
        }

        private static GameObject[] BuildBlockDamageStages(
            Transform parent,
            Bounds bounds,
            ConditionMaterials materials)
        {
            GameObject[] stages = new GameObject[3];
            for (int stage = 0; stage < stages.Length; stage++)
            {
                GameObject root = new GameObject($"Block Damage Stage {stage + 1}");
                root.transform.SetParent(parent, false);
                stages[stage] = root;

                int markCount = 3 + stage * 3;
                for (int index = 0; index < markCount; index++)
                {
                    float side = index % 2 == 0 ? -1f : 1f;
                    float normalized = markCount > 1
                        ? index / (float)(markCount - 1)
                        : 0.5f;
                    Vector3 position = bounds.center + new Vector3(
                        side * bounds.extents.x * 0.72f,
                        Mathf.Lerp(-bounds.extents.y * 0.50f, bounds.extents.y * 0.55f, normalized),
                        Mathf.Lerp(-bounds.extents.z * 0.55f, bounds.extents.z * 0.60f, 1f - normalized));
                    Vector3 scale = new Vector3(
                        Mathf.Max(0.025f, bounds.size.x * 0.025f),
                        Mathf.Max(0.035f, bounds.size.y * (0.10f + stage * 0.025f)),
                        Mathf.Max(0.025f, bounds.size.z * 0.025f));
                    CreatePart(
                        root.transform,
                        PrimitiveType.Cube,
                        $"Damage Mark {index + 1}",
                        position,
                        scale,
                        new Vector3(20f + index * 13f, 15f * side, 35f + index * 19f),
                        stage >= 2 ? materials.Burn : materials.Crack,
                        false);
                }
                root.SetActive(false);
            }
            return stages;
        }

        private static void BuildCoverDamageVisuals(
            Transform parent,
            Bounds bounds,
            int bankIndex,
            ConditionMaterials materials,
            out GameObject crackRoot,
            out ParticleSystem fire,
            out ParticleSystem leak)
        {
            crackRoot = new GameObject(
                $"{(bankIndex == 0 ? "Left" : "Right")} Cover Crack Damage");
            crackRoot.transform.SetParent(parent, false);

            for (int index = 0; index < 5; index++)
            {
                float normalized = index / 4f;
                Vector3 position = bounds.center + new Vector3(
                    Mathf.Lerp(-bounds.extents.x * 0.55f, bounds.extents.x * 0.55f, normalized),
                    bounds.extents.y * 0.92f,
                    Mathf.Sin(index * 1.8f) * bounds.extents.z * 0.34f);
                CreatePart(
                    crackRoot.transform,
                    PrimitiveType.Cube,
                    $"Crack Segment {index + 1}",
                    position,
                    new Vector3(
                        Mathf.Max(0.025f, bounds.size.x * 0.12f),
                        Mathf.Max(0.012f, bounds.size.y * 0.018f),
                        Mathf.Max(0.018f, bounds.size.z * 0.025f)),
                    new Vector3(0f, index * 27f, 18f - index * 11f),
                    materials.Crack,
                    false);
            }
            crackRoot.SetActive(false);

            Vector3 firePosition = bounds.center + new Vector3(
                0f,
                bounds.extents.y * 1.05f,
                bounds.extents.z * 0.15f);
            fire = CreateParticleSystem(
                parent,
                $"{(bankIndex == 0 ? "Left" : "Right")} Cover Fire",
                firePosition,
                true,
                materials.Fire);

            Vector3 leakPosition = bounds.center + new Vector3(
                bankIndex == 0 ? -bounds.extents.x * 0.45f : bounds.extents.x * 0.45f,
                -bounds.extents.y * 0.55f,
                -bounds.extents.z * 0.10f);
            leak = CreateParticleSystem(
                parent,
                $"{(bankIndex == 0 ? "Left" : "Right")} Cover Oil Leak",
                leakPosition,
                false,
                materials.Oil);
        }

        private static void BuildDipstickAndFiller(
            Transform parent,
            Bounds blockBounds,
            EngineConditionController condition,
            ConditionMaterials materials)
        {
            Vector3 dipstickPosition = blockBounds.center + new Vector3(
                -blockBounds.extents.x * 0.48f,
                blockBounds.extents.y * 0.54f,
                blockBounds.extents.z * 0.12f);
            GameObject dipstickRoot = new GameObject("Merlin Oil Dipstick Interaction");
            dipstickRoot.transform.SetParent(parent, false);
            dipstickRoot.transform.localPosition = dipstickPosition;
            BoxCollider dipstickCollider = dipstickRoot.AddComponent<BoxCollider>();
            dipstickCollider.center = new Vector3(0f, 0.12f, 0f);
            dipstickCollider.size = new Vector3(0.34f, 0.62f, 0.34f);

            GameObject dipstickVisual = new GameObject("Dipstick Visual");
            dipstickVisual.transform.SetParent(dipstickRoot.transform, false);
            CreatePart(
                dipstickVisual.transform,
                PrimitiveType.Cylinder,
                "Dipstick Rod",
                new Vector3(0f, -0.20f, 0f),
                new Vector3(0.022f, 0.24f, 0.022f),
                Vector3.zero,
                materials.CanMetal,
                false);
            CreatePart(
                dipstickVisual.transform,
                PrimitiveType.Torus,
                "Dipstick Ring Handle",
                new Vector3(0f, 0.14f, 0f),
                new Vector3(0.16f, 0.05f, 0.16f),
                new Vector3(90f, 0f, 0f),
                materials.CanCap,
                false);
            GameObject stain = CreatePart(
                dipstickVisual.transform,
                PrimitiveType.Cube,
                "Visible Oil Level on Dipstick",
                new Vector3(0f, -0.36f, 0f),
                new Vector3(0.038f, 0.24f, 0.038f),
                Vector3.zero,
                materials.Oil,
                false);

            EngineDipstickController dipstick =
                dipstickRoot.AddComponent<EngineDipstickController>();
            dipstick.Configure(
                condition,
                dipstickVisual.transform,
                stain.transform,
                Vector3.zero,
                new Vector3(0f, 0.58f, 0f),
                0.24f);

            Vector3 fillerPosition = blockBounds.center + new Vector3(
                blockBounds.extents.x * 0.40f,
                blockBounds.extents.y * 0.58f,
                blockBounds.extents.z * 0.05f);
            GameObject fillerRoot = new GameObject("Merlin Oil Filler Target");
            fillerRoot.transform.SetParent(parent, false);
            fillerRoot.transform.localPosition = fillerPosition;
            BoxCollider fillerCollider = fillerRoot.AddComponent<BoxCollider>();
            fillerCollider.center = new Vector3(0f, 0.12f, 0f);
            fillerCollider.size = new Vector3(0.48f, 0.42f, 0.48f);
            CreatePart(
                fillerRoot.transform,
                PrimitiveType.Cylinder,
                "Oil Filler Neck",
                Vector3.zero,
                new Vector3(0.12f, 0.12f, 0.12f),
                Vector3.zero,
                materials.CanMetal,
                false);
            CreatePart(
                fillerRoot.transform,
                PrimitiveType.Cylinder,
                "Oil Filler Cap",
                new Vector3(0f, 0.18f, 0f),
                new Vector3(0.16f, 0.06f, 0.16f),
                Vector3.zero,
                materials.CanCap,
                false);
            EngineConditionInspectionTarget fillerTarget =
                fillerRoot.AddComponent<EngineConditionInspectionTarget>();
            fillerTarget.Configure(
                condition,
                EngineConditionInspectionKind.OilFiller,
                0);
        }

        private static void CreateFollowerInspectionTarget(
            Transform parent,
            string name,
            Transform visual,
            Renderer[] renderers,
            EngineConditionController condition,
            EngineConditionInspectionKind kind,
            int index)
        {
            Bounds localBounds = CalculateLocalBounds(visual, renderers);
            GameObject target = new GameObject(name);
            target.transform.SetParent(parent, false);
            BoxCollider collider = target.AddComponent<BoxCollider>();
            collider.center = Vector3.zero;
            collider.size = new Vector3(
                Mathf.Max(0.25f, localBounds.size.x * 1.05f),
                Mathf.Max(0.25f, localBounds.size.y * 1.05f),
                Mathf.Max(0.25f, localBounds.size.z * 1.05f));
            EngineConditionInspectionTarget inspection =
                target.AddComponent<EngineConditionInspectionTarget>();
            inspection.Configure(condition, kind, index);
            EngineConditionInspectionFollower follower =
                target.AddComponent<EngineConditionInspectionFollower>();
            follower.Configure(
                visual,
                collider,
                localBounds.center,
                Quaternion.identity);
        }

        private static void BuildOilServiceSupplies(
            EngineAssemblyStation referenceStation,
            ConditionMaterials materials)
        {
            GameObject existing = GameObject.Find(OilSupplyRootName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            GameObject root = new GameObject(OilSupplyRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Merlin oil supplies");
            Vector3 origin = referenceStation != null
                ? referenceStation.transform.position
                    + referenceStation.transform.right * 4.2f
                    - referenceStation.transform.forward * 2.4f
                : new Vector3(-7f, 0f, 4f);

            for (int index = 0; index < 2; index++)
            {
                CreateOilCan(
                    root.transform,
                    index + 1,
                    origin + Vector3.right * index * 0.85f,
                    materials);
            }
        }

        private static void CreateOilCan(
            Transform parent,
            int index,
            Vector3 worldPosition,
            ConditionMaterials materials)
        {
            GameObject can = new GameObject($"Aircraft Oil Can {index}");
            can.transform.SetParent(parent, false);
            can.transform.position = worldPosition + Vector3.up * 0.42f;
            BoxCollider collider = can.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.02f, 0f);
            collider.size = new Vector3(0.58f, 0.82f, 0.38f);

            CreatePart(
                can.transform,
                PrimitiveType.Cube,
                "Oil Can Body",
                Vector3.zero,
                new Vector3(0.56f, 0.78f, 0.36f),
                Vector3.zero,
                materials.CanBody,
                false);
            CreatePart(
                can.transform,
                PrimitiveType.Cube,
                "Handle Top",
                new Vector3(0f, 0.48f, -0.02f),
                new Vector3(0.38f, 0.08f, 0.10f),
                Vector3.zero,
                materials.CanMetal,
                false);
            CreatePart(
                can.transform,
                PrimitiveType.Cube,
                "Handle Left",
                new Vector3(-0.18f, 0.37f, -0.02f),
                new Vector3(0.08f, 0.30f, 0.10f),
                Vector3.zero,
                materials.CanMetal,
                false);
            CreatePart(
                can.transform,
                PrimitiveType.Cube,
                "Handle Right",
                new Vector3(0.18f, 0.37f, -0.02f),
                new Vector3(0.08f, 0.30f, 0.10f),
                Vector3.zero,
                materials.CanMetal,
                false);
            CreatePart(
                can.transform,
                PrimitiveType.Cylinder,
                "Pour Spout",
                new Vector3(0.24f, 0.37f, 0.08f),
                new Vector3(0.09f, 0.24f, 0.09f),
                new Vector3(55f, 0f, -28f),
                materials.CanMetal,
                false);

            GameObject capPivot = new GameObject("Oil Can Cap Pivot");
            capPivot.transform.SetParent(can.transform, false);
            capPivot.transform.localPosition = new Vector3(0.36f, 0.51f, 0.14f);
            CreatePart(
                capPivot.transform,
                PrimitiveType.Cylinder,
                "Oil Can Cap",
                Vector3.zero,
                new Vector3(0.12f, 0.055f, 0.12f),
                new Vector3(0f, 0f, 90f),
                materials.CanCap,
                false);

            ParticleSystem pour = CreateParticleSystem(
                can.transform,
                "Oil Pour Stream",
                new Vector3(0.42f, 0.53f, 0.20f),
                false,
                materials.Oil);
            ParticleSystem.ShapeModule shape = pour.shape;
            shape.angle = 3f;
            ParticleSystem.MainModule main = pour.main;
            main.startSpeed = 1.2f;
            main.startLifetime = 0.55f;
            main.gravityModifier = 0.7f;
            ParticleSystem.EmissionModule emission = pour.emission;
            emission.rateOverTime = 70f;

            EngineOilCanController controller =
                can.AddComponent<EngineOilCanController>();
            controller.Configure(20f, 4f, capPivot.transform, pour, collider);
        }

        private static ParticleSystem CreateParticleSystem(
            Transform parent,
            string name,
            Vector3 localPosition,
            bool fire,
            Material material)
        {
            GameObject particleObject = new GameObject(name);
            particleObject.transform.SetParent(parent, false);
            particleObject.transform.localPosition = localPosition;
            ParticleSystem particle = particleObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particle.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = fire ? 0.52f : 0.90f;
            main.startSpeed = fire ? 1.2f : 0.30f;
            main.startSize = fire ? 0.34f : 0.09f;
            main.gravityModifier = fire ? -0.08f : 0.75f;
            main.startColor = fire
                ? new ParticleSystem.MinMaxGradient(
                    new Color(1f, 0.18f, 0.02f, 1f),
                    new Color(1f, 0.78f, 0.08f, 0.85f))
                : new Color(0.12f, 0.055f, 0.015f, 0.95f);

            ParticleSystem.EmissionModule emission = particle.emission;
            emission.rateOverTime = fire ? 48f : 24f;
            ParticleSystem.ShapeModule shape = particle.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = fire ? 20f : 7f;
            shape.radius = fire ? 0.14f : 0.04f;
            ParticleSystemRenderer renderer =
                particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.material = material;
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return particle;
        }

        private static ConditionMaterials CreateMaterials()
        {
            return new ConditionMaterials
            {
                Crack = CreateMaterial(
                    MaterialFolder + "/ConditionCrack.mat",
                    new Color(0.025f, 0.018f, 0.015f, 1f),
                    0.05f,
                    0.10f,
                    Color.black),
                Burn = CreateMaterial(
                    MaterialFolder + "/ConditionBurn.mat",
                    new Color(0.12f, 0.035f, 0.015f, 1f),
                    0.08f,
                    0.14f,
                    new Color(0.18f, 0.025f, 0.005f, 1f)),
                Oil = CreateMaterial(
                    MaterialFolder + "/ConditionOil.mat",
                    new Color(0.11f, 0.045f, 0.008f, 1f),
                    0.18f,
                    0.78f,
                    new Color(0.02f, 0.006f, 0f, 1f)),
                Fire = CreateMaterial(
                    MaterialFolder + "/ConditionFire.mat",
                    new Color(1f, 0.18f, 0.015f, 1f),
                    0f,
                    0.20f,
                    new Color(1f, 0.08f, 0f, 1f)),
                CanBody = CreateMaterial(
                    MaterialFolder + "/AircraftOilCanBody.mat",
                    new Color(0.12f, 0.24f, 0.52f, 1f),
                    0.72f,
                    0.48f,
                    Color.black),
                CanMetal = CreateMaterial(
                    MaterialFolder + "/AircraftOilCanMetal.mat",
                    new Color(0.48f, 0.52f, 0.58f, 1f),
                    0.92f,
                    0.74f,
                    Color.black),
                CanCap = CreateMaterial(
                    MaterialFolder + "/AircraftOilCanCap.mat",
                    new Color(0.95f, 0.62f, 0.05f, 1f),
                    0.52f,
                    0.42f,
                    Color.black)
            };
        }

        private static Material CreateMaterial(
            string path,
            Color color,
            float metallic,
            float smoothness,
            Color emission)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (material == null)
            {
                material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (shader != null)
            {
                material.shader = shader;
            }

            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (emission.maxColorComponent > 0.001f)
            {
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor"))
                {
                    material.SetColor("_EmissionColor", emission);
                }
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreatePart(
            Transform parent,
            PrimitiveType primitiveType,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEuler,
            Material material,
            bool keepCollider)
        {
            // Unity has no built-in torus primitive. A flattened cylinder gives
            // the dipstick handle a clear ring-like silhouette without adding a
            // package dependency.
            PrimitiveType resolvedType = primitiveType == PrimitiveType.Torus
                ? PrimitiveType.Cylinder
                : primitiveType;
            GameObject part = GameObject.CreatePrimitive(resolvedType);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(localEuler);
            part.transform.localScale = localScale;
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            Collider collider = part.GetComponent<Collider>();
            if (!keepCollider && collider != null)
            {
                Object.DestroyImmediate(collider);
            }
            return part;
        }

        private static Renderer FindPreferredRenderer(GameObject visual)
        {
            if (visual == null)
            {
                return null;
            }

            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index].name.ToLowerInvariant().Contains("ceramic"))
                {
                    return renderers[index];
                }
            }
            return renderers.Length > 0 ? renderers[0] : null;
        }

        private static Bounds CalculateLocalBounds(Transform root, Renderer[] renderers)
        {
            bool initialized = false;
            Bounds result = new Bounds(Vector3.zero, Vector3.one * 0.5f);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                Bounds world = renderer.bounds;
                Vector3 min = world.min;
                Vector3 max = world.max;
                for (int x = 0; x <= 1; x++)
                {
                    for (int y = 0; y <= 1; y++)
                    {
                        for (int z = 0; z <= 1; z++)
                        {
                            Vector3 worldCorner = new Vector3(
                                x == 0 ? min.x : max.x,
                                y == 0 ? min.y : max.y,
                                z == 0 ? min.z : max.z);
                            Vector3 localCorner = root.InverseTransformPoint(worldCorner);
                            if (!initialized)
                            {
                                result = new Bounds(localCorner, Vector3.zero);
                                initialized = true;
                            }
                            else
                            {
                                result.Encapsulate(localCorner);
                            }
                        }
                    }
                }
            }

            if (!initialized || result.size.sqrMagnitude < 0.01f)
            {
                result = new Bounds(Vector3.zero, new Vector3(1.8f, 1.2f, 2.6f));
            }
            return result;
        }

        private static T GetObjectReference<T>(
            SerializedObject serializedObject,
            string propertyName)
            where T : Object
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null ? property.objectReferenceValue as T : null;
        }

        private static T[] GetObjectReferenceArray<T>(
            SerializedObject serializedObject,
            string propertyName)
            where T : Object
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || !property.isArray)
            {
                return new T[0];
            }

            T[] result = new T[property.arraySize];
            for (int index = 0; index < property.arraySize; index++)
            {
                result[index] = property.GetArrayElementAtIndex(index).objectReferenceValue as T;
            }
            return result;
        }

        private static EngineConditionController FindSelectedCondition()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected != null)
            {
                EngineConditionController direct =
                    selected.GetComponentInParent<EngineConditionController>();
                if (direct != null)
                {
                    return direct;
                }

                EngineConditionLink link = selected.GetComponentInParent<EngineConditionLink>();
                if (link != null && link.Condition != null)
                {
                    return link.Condition;
                }
            }

            return Object.FindFirstObjectByType<EngineConditionController>(
                FindObjectsInactive.Include);
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (child != null && child.name == name)
                {
                    return child;
                }
            }
            return null;
        }

        private static void EnsureFolder(string parentPath, string childName)
        {
            string path = parentPath + "/" + childName;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parentPath, childName);
            }
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

        private static bool ValidateFloat(
            SerializedObject serializedObject,
            string propertyName,
            float expected,
            string displayName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || Mathf.Abs(property.floatValue - expected) > 0.001f)
            {
                float actual = property != null ? property.floatValue : float.NaN;
                Debug.LogError(
                    $"Validation failed: {displayName} is {actual:F3}; expected {expected:F3}.");
                return false;
            }
            return true;
        }

        private static void SaveSceneAndPrepareBuild(
            Scene scene,
            string stepName,
            GameObject selection,
            string successMessage)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError($"{stepName} changed the scene but could not save it.");
                return;
            }
            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError($"{stepName} completed, but build preparation failed.");
                return;
            }
            Selection.activeGameObject = selection;
            Debug.Log(successMessage, selection);
        }
    }
}
