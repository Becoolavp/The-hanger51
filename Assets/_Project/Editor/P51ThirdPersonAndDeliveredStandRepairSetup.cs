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
    public static class P51ThirdPersonAndDeliveredStandRepairSetup
    {
        private const string CommerceRootName = "Hanger 51 Commerce System";
        private const string AssemblyTemplateName =
            "Complete V-1650 Shipment Template";
        private const string OldDisposalInteractionName =
            "Delivered Stand Disposal Interaction";

        [MenuItem("Hanger 51/Shop and Shipping/7 - Repair Delivered Stand Removal Interaction")]
        public static void RepairDeliveredStandRemovalInteraction()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Shop Step 7 failed. Exit Play mode first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            GameObject commerceRoot = GameObject.Find(CommerceRootName);
            PlayerInventory playerInventory =
                Object.FindFirstObjectByType<PlayerInventory>();
            InventoryUI inventoryUI = Object.FindFirstObjectByType<InventoryUI>();
            HangarShopUI shopUI = Object.FindFirstObjectByType<HangarShopUI>();

            Transform assemblyTemplate = commerceRoot != null
                ? FindDescendant(commerceRoot.transform, AssemblyTemplateName)
                : null;
            EngineAssemblyStation templateStation = assemblyTemplate != null
                ? assemblyTemplate.GetComponentInChildren<EngineAssemblyStation>(true)
                : null;
            EngineAssemblyTransportController templateTransport = templateStation != null
                ? templateStation.GetComponent<EngineAssemblyTransportController>()
                : null;

            if (!scene.IsValid()
                || string.IsNullOrWhiteSpace(scene.path)
                || commerceRoot == null
                || playerInventory == null
                || inventoryUI == null
                || templateStation == null
                || templateTransport == null
                || templateTransport.TransportRoot == null)
            {
                Debug.LogError(
                    "Shop Step 7 failed. Open the saved movement-test scene and confirm the current shop, Player inventory, and complete-engine shipment template exist.");
                return;
            }

            RemoveOldNarrowDisposalTargets(templateStation);

            Collider standCollider = templateStation.GetComponent<Collider>();
            if (standCollider == null)
            {
                BoxCollider boxCollider = Undo.AddComponent<BoxCollider>(
                    templateStation.gameObject);
                boxCollider.center = new Vector3(0f, 1.25f, 0f);
                boxCollider.size = new Vector3(3.4f, 2.5f, 6.5f);
                standCollider = boxCollider;
            }

            DeliveredEngineStandDisposalTarget rootTarget =
                templateStation.GetComponent<DeliveredEngineStandDisposalTarget>();
            if (rootTarget == null)
            {
                rootTarget = Undo.AddComponent<DeliveredEngineStandDisposalTarget>(
                    templateStation.gameObject);
            }
            rootTarget.Configure(templateTransport, standCollider);
            EditorUtility.SetDirty(rootTarget);
            EditorUtility.SetDirty(standCollider);

            DeliveredEngineStandDisposalPlayerInteractor oldInteractor =
                playerInventory.GetComponent<DeliveredEngineStandDisposalPlayerInteractor>();
            if (oldInteractor != null)
            {
                Undo.DestroyObjectImmediate(oldInteractor);
            }

            DeliveredEngineStandWidePlayerInteractor wideInteractor =
                playerInventory.GetComponent<DeliveredEngineStandWidePlayerInteractor>();
            if (wideInteractor == null)
            {
                wideInteractor = Undo.AddComponent<DeliveredEngineStandWidePlayerInteractor>(
                    playerInventory.gameObject);
            }
            wideInteractor.Configure(
                playerInventory.GetComponentInChildren<Camera>(true),
                inventoryUI);
            EditorUtility.SetDirty(wideInteractor);

            ReplaceBlockedStandInteractor(shopUI, wideInteractor);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("Shop Step 7 repaired the stand interaction but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("Shop Step 7 repaired the stand interaction, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = templateStation.gameObject;
            Debug.Log(
                "Shop Step 7 complete. Replaced the small hidden stand hotspot with stand-wide removal detection across visible rails, posts, braces, saddles, and casters. Engine geometry remains reserved for maintenance interactions.",
                templateStation);
        }

        [MenuItem("Hanger 51/Shop and Shipping/8 - Validate Delivered Stand Removal Interaction")]
        public static void ValidateDeliveredStandRemovalInteraction()
        {
            bool passed = true;
            GameObject commerceRoot = GameObject.Find(CommerceRootName);
            Transform assemblyTemplate = commerceRoot != null
                ? FindDescendant(commerceRoot.transform, AssemblyTemplateName)
                : null;
            EngineAssemblyStation templateStation = assemblyTemplate != null
                ? assemblyTemplate.GetComponentInChildren<EngineAssemblyStation>(true)
                : null;
            DeliveredEngineStandDisposalTarget target = templateStation != null
                ? templateStation.GetComponent<DeliveredEngineStandDisposalTarget>()
                : null;

            if (target == null
                || !target.IsConfigured
                || target.EngineTransport == null
                || target.gameObject != templateStation?.gameObject)
            {
                Debug.LogError(
                    "Shop Step 8 failed: the complete-engine template does not have a configured stand-wide disposal target on its station root.");
                passed = false;
            }

            if (templateStation != null
                && templateStation.transform.Find(OldDisposalInteractionName) != null)
            {
                Debug.LogError(
                    "Shop Step 8 failed: the obsolete small disposal hotspot still exists.");
                passed = false;
            }

            PlayerInventory playerInventory =
                Object.FindFirstObjectByType<PlayerInventory>();
            DeliveredEngineStandWidePlayerInteractor wideInteractor =
                playerInventory != null
                    ? playerInventory.GetComponent<DeliveredEngineStandWidePlayerInteractor>()
                    : null;
            DeliveredEngineStandDisposalPlayerInteractor oldInteractor =
                playerInventory != null
                    ? playerInventory.GetComponent<DeliveredEngineStandDisposalPlayerInteractor>()
                    : null;

            if (wideInteractor == null || oldInteractor != null)
            {
                Debug.LogError(
                    "Shop Step 8 failed: the Player does not have the new stand-wide detector or still has the obsolete narrow detector.");
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("Shop Step 8 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Shop Step 8 passed. Future delivered complete-engine stands are removable by aiming at any visible holder component, while the engine remains available for normal maintenance.");
            }
        }

        [MenuItem("Hanger 51/P-51 Mustang/18 - Add Third-Person Camera and Airspeed Warnings")]
        public static void AddThirdPersonCameraAndAirspeedWarnings()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("P-51 Step 18 failed. Exit Play mode first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            P51FlightController flightController =
                Object.FindFirstObjectByType<P51FlightController>();
            P51PilotPlayerInteractor pilotInteractor =
                Object.FindFirstObjectByType<P51PilotPlayerInteractor>();
            Camera playerCamera = pilotInteractor != null
                ? pilotInteractor.GetComponentInChildren<Camera>(true)
                : null;

            if (!scene.IsValid()
                || string.IsNullOrWhiteSpace(scene.path)
                || flightController == null
                || pilotInteractor == null
                || playerCamera == null)
            {
                Debug.LogError(
                    "P-51 Step 18 failed. Open the saved movement-test scene and confirm the current P-51 cockpit and Player camera exist.");
                return;
            }

            P51ThirdPersonCamera thirdPersonCamera =
                pilotInteractor.GetComponent<P51ThirdPersonCamera>();
            if (thirdPersonCamera == null)
            {
                thirdPersonCamera = Undo.AddComponent<P51ThirdPersonCamera>(
                    pilotInteractor.gameObject);
            }
            thirdPersonCamera.Configure(pilotInteractor, playerCamera);
            EditorUtility.SetDirty(thirdPersonCamera);

            P51AirspeedWarningDisplay airspeedDisplay =
                flightController.GetComponent<P51AirspeedWarningDisplay>();
            if (airspeedDisplay == null)
            {
                airspeedDisplay = Undo.AddComponent<P51AirspeedWarningDisplay>(
                    flightController.gameObject);
            }
            airspeedDisplay.Configure(
                flightController,
                58f,
                74f,
                92f,
                20f);
            EditorUtility.SetDirty(airspeedDisplay);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 18 installed the camera and warning display but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 Step 18 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = flightController.gameObject;
            Debug.Log(
                "P-51 Step 18 complete. Added V-toggle cockpit/external flight views, mouse-orbit chase camera with obstacle avoidance, and a bank-aware color-coded airspeed warning display.",
                flightController);
        }

        [MenuItem("Hanger 51/P-51 Mustang/19 - Validate Third-Person Camera and Airspeed Warnings")]
        public static void ValidateThirdPersonCameraAndAirspeedWarnings()
        {
            bool passed = true;
            P51FlightController flightController =
                Object.FindFirstObjectByType<P51FlightController>();
            P51PilotPlayerInteractor pilotInteractor =
                Object.FindFirstObjectByType<P51PilotPlayerInteractor>();
            P51ThirdPersonCamera thirdPersonCamera = pilotInteractor != null
                ? pilotInteractor.GetComponent<P51ThirdPersonCamera>()
                : null;
            P51AirspeedWarningDisplay airspeedDisplay = flightController != null
                ? flightController.GetComponent<P51AirspeedWarningDisplay>()
                : null;

            if (thirdPersonCamera == null
                || thirdPersonCamera.PilotInteractor != pilotInteractor)
            {
                Debug.LogError(
                    "P-51 Step 19 failed: the Player third-person camera controller is missing or not connected to the cockpit interactor.");
                passed = false;
            }

            if (airspeedDisplay == null
                || airspeedDisplay.RedThresholdKnots < 50f
                || airspeedDisplay.OrangeThresholdKnots <= airspeedDisplay.RedThresholdKnots
                || airspeedDisplay.YellowThresholdKnots <= airspeedDisplay.OrangeThresholdKnots)
            {
                Debug.LogError(
                    "P-51 Step 19 failed: the color-coded airspeed display is missing or its warning thresholds are invalid.");
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("P-51 Step 19 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "P-51 Step 19 passed. Cockpit/external camera toggling, mouse orbit, obstacle avoidance, view indicator, and bank-aware airspeed colors are configured.");
            }
        }

        private static void RemoveOldNarrowDisposalTargets(
            EngineAssemblyStation station)
        {
            if (station == null)
            {
                return;
            }

            Transform oldInteraction = station.transform.Find(
                OldDisposalInteractionName);
            if (oldInteraction != null)
            {
                Undo.DestroyObjectImmediate(oldInteraction.gameObject);
            }

            DeliveredEngineStandDisposalTarget[] targets =
                station.GetComponentsInChildren<DeliveredEngineStandDisposalTarget>(true);
            for (int index = 0; index < targets.Length; index++)
            {
                DeliveredEngineStandDisposalTarget target = targets[index];
                if (target != null && target.gameObject != station.gameObject)
                {
                    Undo.DestroyObjectImmediate(target);
                }
            }
        }

        private static void ReplaceBlockedStandInteractor(
            HangarShopUI shopUI,
            DeliveredEngineStandWidePlayerInteractor wideInteractor)
        {
            if (shopUI == null || wideInteractor == null)
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

            for (int index = behaviours.arraySize - 1; index >= 0; index--)
            {
                Object value = behaviours.GetArrayElementAtIndex(index).objectReferenceValue;
                if (value == null
                    || value is DeliveredEngineStandDisposalPlayerInteractor
                    || value is DeliveredEngineStandWidePlayerInteractor)
                {
                    behaviours.DeleteArrayElementAtIndex(index);
                }
            }

            int newIndex = behaviours.arraySize;
            behaviours.InsertArrayElementAtIndex(newIndex);
            behaviours.GetArrayElementAtIndex(newIndex).objectReferenceValue = wideInteractor;
            serializedUi.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(shopUI);
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
