using System.Collections.Generic;
using Hanger51.Aircraft;
using Hanger51.Commerce;
using Hanger51.EngineAssembly;
using Hanger51.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class HangarStandAndP51TurnTuningSetup
    {
        private const string CommerceRootName = "Hanger 51 Commerce System";
        private const string AssemblyTemplateName =
            "Complete V-1650 Shipment Template";
        private const string ChairName = "Shop Desk Chair";
        private const string DisposalInteractionName =
            "Delivered Stand Disposal Interaction";

        [MenuItem("Hanger 51/Shop and Shipping/5 - Repair Chair and Add Removable Delivered Stands")]
        public static void RepairChairAndAddRemovableDeliveredStands()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Shop Step 5 failed. Exit Play mode first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            GameObject commerceRoot = GameObject.Find(CommerceRootName);
            InventoryUI inventoryUI = Object.FindFirstObjectByType<InventoryUI>();
            PlayerInventory playerInventory = Object.FindFirstObjectByType<PlayerInventory>();
            HangarShopUI shopUI = Object.FindFirstObjectByType<HangarShopUI>();

            if (!scene.IsValid()
                || string.IsNullOrWhiteSpace(scene.path)
                || commerceRoot == null
                || inventoryUI == null
                || playerInventory == null)
            {
                Debug.LogError(
                    "Shop Step 5 failed. Open the saved movement-test scene and confirm Shop Step 1 has been completed.");
                return;
            }

            Transform chair = FindDescendant(commerceRoot.transform, ChairName);
            if (chair == null)
            {
                Debug.LogError("Shop Step 5 failed. The shop desk chair is missing.");
                return;
            }

            Undo.RecordObject(chair, "Face shop chair toward desk");
            chair.localRotation = Quaternion.Euler(0f, 180f, 0f);
            EditorUtility.SetDirty(chair);

            Transform assemblyTemplate = FindDescendant(
                commerceRoot.transform,
                AssemblyTemplateName);
            EngineAssemblyStation templateStation = assemblyTemplate != null
                ? assemblyTemplate.GetComponentInChildren<EngineAssemblyStation>(true)
                : null;
            EngineAssemblyTransportController templateTransport = templateStation != null
                ? templateStation.GetComponent<EngineAssemblyTransportController>()
                : null;

            if (templateStation == null
                || templateTransport == null
                || templateTransport.TransportRoot == null)
            {
                Debug.LogError(
                    "Shop Step 5 failed. The complete V-1650 shipment template or its transport controller is missing. Run Shop Step 1, then rerun Step 5.");
                return;
            }

            DeliveredEngineStandDisposalTarget disposalTarget =
                AddOrRefreshDisposalTarget(templateStation, templateTransport);

            DeliveredEngineStandDisposalPlayerInteractor playerInteractor =
                playerInventory.GetComponent<DeliveredEngineStandDisposalPlayerInteractor>();
            if (playerInteractor == null)
            {
                playerInteractor = Undo.AddComponent<DeliveredEngineStandDisposalPlayerInteractor>(
                    playerInventory.gameObject);
            }
            playerInteractor.Configure(
                playerInventory.GetComponentInChildren<Camera>(),
                inventoryUI);
            EditorUtility.SetDirty(playerInteractor);

            AddBlockedShopBehaviour(shopUI, playerInteractor);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("Shop Step 5 made the repairs but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("Shop Step 5 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = disposalTarget.gameObject;
            Debug.Log(
                "Shop Step 5 complete. Turned the chair toward the computer and added a hold-R removal interaction to empty delivered engine stands.",
                disposalTarget);
        }

        [MenuItem("Hanger 51/Shop and Shipping/6 - Validate Chair and Removable Delivered Stands")]
        public static void ValidateChairAndRemovableDeliveredStands()
        {
            bool passed = true;
            GameObject commerceRoot = GameObject.Find(CommerceRootName);
            Transform chair = commerceRoot != null
                ? FindDescendant(commerceRoot.transform, ChairName)
                : null;

            if (chair == null
                || Quaternion.Angle(
                    chair.localRotation,
                    Quaternion.Euler(0f, 180f, 0f)) > 0.5f)
            {
                Debug.LogError(
                    "Shop Step 6 failed: the desk chair is not facing the computer.");
                passed = false;
            }

            Transform assemblyTemplate = commerceRoot != null
                ? FindDescendant(commerceRoot.transform, AssemblyTemplateName)
                : null;
            DeliveredEngineStandDisposalTarget target = assemblyTemplate != null
                ? assemblyTemplate.GetComponentInChildren<DeliveredEngineStandDisposalTarget>(true)
                : null;
            if (target == null
                || !target.IsConfigured
                || target.EngineTransport == null)
            {
                Debug.LogError(
                    "Shop Step 6 failed: the complete assembly template has no configured stand-removal target.");
                passed = false;
            }

            DeliveredEngineStandDisposalPlayerInteractor playerInteractor =
                Object.FindFirstObjectByType<DeliveredEngineStandDisposalPlayerInteractor>();
            if (playerInteractor == null)
            {
                Debug.LogError(
                    "Shop Step 6 failed: the Player stand-removal interactor is missing.");
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("Shop Step 6 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Shop Step 6 passed. The chair faces the desk, future complete-engine deliveries include removable stands, and the Player interaction is ready.");
            }
        }

        [MenuItem("Hanger 51/P-51 Mustang/16 - Tune Sustained Turning and Stall Behavior")]
        public static void TuneSustainedTurningAndStallBehavior()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("P-51 Step 16 failed. Exit Play mode first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            P51FlightController flightController =
                Object.FindFirstObjectByType<P51FlightController>();
            if (!scene.IsValid()
                || string.IsNullOrWhiteSpace(scene.path)
                || flightController == null)
            {
                Debug.LogError(
                    "P-51 Step 16 failed. Open the saved movement-test scene and confirm the current P-51 flight system exists.");
                return;
            }

            SerializedObject serializedFlight = new SerializedObject(flightController);
            SetFloat(serializedFlight, "maximumLiftCoefficient", 1.72f);
            SetFloat(serializedFlight, "inducedDragFactor", 0.032f);
            SetFloat(serializedFlight, "fullStallSpeedMetersPerSecond", 18.5f);
            SetFloat(serializedFlight, "liftRecoverySpeedMetersPerSecond", 33f);
            SetFloat(serializedFlight, "sideDragCoefficient", 0.62f);
            SetFloat(serializedFlight, "fullControlSpeedMetersPerSecond", 38f);
            serializedFlight.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(flightController);

            P51TurnPerformanceAssist assist =
                flightController.GetComponent<P51TurnPerformanceAssist>();
            if (assist == null)
            {
                assist = Undo.AddComponent<P51TurnPerformanceAssist>(
                    flightController.gameObject);
            }
            assist.Configure(
                20f,
                34f,
                0.62f,
                0.85f,
                18000f,
                70f);
            EditorUtility.SetDirty(assist);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 16 tuned the aircraft but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 Step 16 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = flightController.gameObject;
            Debug.Log(
                "P-51 Step 16 complete. Softened the low-speed lift cutoff, reduced induced and sideslip drag, retained control authority sooner, and added partial bank-load plus coordinated-turn support.",
                flightController);
        }

        [MenuItem("Hanger 51/P-51 Mustang/17 - Validate Sustained Turning and Stall Behavior")]
        public static void ValidateSustainedTurningAndStallBehavior()
        {
            bool passed = true;
            P51FlightController flightController =
                Object.FindFirstObjectByType<P51FlightController>();
            if (flightController == null)
            {
                Debug.LogError("P-51 Step 17 failed: the flight controller is missing.");
                return;
            }

            SerializedObject serializedFlight = new SerializedObject(flightController);
            passed &= ValidateFloat(
                serializedFlight,
                "maximumLiftCoefficient",
                1.72f,
                "maximum lift coefficient");
            passed &= ValidateFloat(
                serializedFlight,
                "inducedDragFactor",
                0.032f,
                "induced drag factor");
            passed &= ValidateFloat(
                serializedFlight,
                "fullStallSpeedMetersPerSecond",
                18.5f,
                "full-stall speed");
            passed &= ValidateFloat(
                serializedFlight,
                "liftRecoverySpeedMetersPerSecond",
                33f,
                "lift-recovery speed");
            passed &= ValidateFloat(
                serializedFlight,
                "sideDragCoefficient",
                0.62f,
                "side-drag coefficient");

            P51TurnPerformanceAssist assist =
                flightController.GetComponent<P51TurnPerformanceAssist>();
            if (assist == null
                || assist.BankLiftSupport < 0.55f
                || assist.CoordinatedYawTorque < 15000f
                || assist.FullAssistSpeedMetersPerSecond > 36f)
            {
                Debug.LogError(
                    "P-51 Step 17 failed: the sustained-turn assist is missing or outside the expected tuning range.",
                    flightController);
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("P-51 Step 17 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "P-51 Step 17 passed. The revised stall range, lower turn drag, earlier control authority, partial bank-load support, and coordinated yaw are configured.");
            }
        }

        private static DeliveredEngineStandDisposalTarget AddOrRefreshDisposalTarget(
            EngineAssemblyStation station,
            EngineAssemblyTransportController transport)
        {
            Transform interaction = station.transform.Find(DisposalInteractionName);
            if (interaction == null)
            {
                GameObject interactionObject = new GameObject(DisposalInteractionName);
                Undo.RegisterCreatedObjectUndo(
                    interactionObject,
                    "Create delivered stand disposal interaction");
                interaction = interactionObject.transform;
                interaction.SetParent(station.transform, false);
            }

            interaction.localPosition = Vector3.zero;
            interaction.localRotation = Quaternion.identity;
            interaction.localScale = Vector3.one;

            BoxCollider collider = interaction.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = Undo.AddComponent<BoxCollider>(interaction.gameObject);
            }
            collider.center = new Vector3(0f, 0.38f, 2.48f);
            collider.size = new Vector3(3.0f, 0.75f, 0.48f);
            collider.isTrigger = false;

            DeliveredEngineStandDisposalTarget target =
                interaction.GetComponent<DeliveredEngineStandDisposalTarget>();
            if (target == null)
            {
                target = Undo.AddComponent<DeliveredEngineStandDisposalTarget>(
                    interaction.gameObject);
            }
            target.Configure(transport, collider);

            EditorUtility.SetDirty(collider);
            EditorUtility.SetDirty(target);
            return target;
        }

        private static void AddBlockedShopBehaviour(
            HangarShopUI shopUI,
            Behaviour behaviour)
        {
            if (shopUI == null || behaviour == null)
            {
                return;
            }

            SerializedObject serializedUi = new SerializedObject(shopUI);
            SerializedProperty behaviours = serializedUi.FindProperty(
                "gameplayBehavioursToDisable");
            if (behaviours == null)
            {
                return;
            }

            for (int index = 0; index < behaviours.arraySize; index++)
            {
                if (behaviours.GetArrayElementAtIndex(index).objectReferenceValue == behaviour)
                {
                    return;
                }
            }

            int newIndex = behaviours.arraySize;
            behaviours.InsertArrayElementAtIndex(newIndex);
            behaviours.GetArrayElementAtIndex(newIndex).objectReferenceValue = behaviour;
            serializedUi.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(shopUI);
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
            float expectedValue,
            string displayName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null
                || Mathf.Abs(property.floatValue - expectedValue) > 0.001f)
            {
                float actual = property != null ? property.floatValue : float.NaN;
                Debug.LogError(
                    $"P-51 Step 17 failed: {displayName} is {actual:F3}; expected {expectedValue:F3}.");
                return false;
            }

            return true;
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
