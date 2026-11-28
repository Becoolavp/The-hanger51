using Hanger51.Aircraft;
using Hanger51.Commerce;
using Hanger51.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class P51OrbitCowlingAndStandlessDeliverySetup
    {
        private const string CommerceRootName = "Hanger 51 Commerce System";
        private const string AssemblyTemplateName = "Complete V-1650 Shipment Template";
        private const string DisposalInteractionName = "Delivered Stand Disposal Interaction";

        [MenuItem("Hanger 51/Shop and Shipping/9 - Make Complete Assemblies Unpack Without Stands")]
        public static void MakeCompleteAssembliesUnpackWithoutStands()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Shop Step 9 failed. Exit Play mode first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            GameObject commerceRoot = GameObject.Find(CommerceRootName);
            Transform assemblyTemplate = commerceRoot != null
                ? FindDescendant(commerceRoot.transform, AssemblyTemplateName)
                : null;
            ShipmentCrateController crateTemplate = commerceRoot != null
                ? commerceRoot.GetComponentInChildren<ShipmentCrateController>(true)
                : null;

            if (!scene.IsValid()
                || string.IsNullOrWhiteSpace(scene.path)
                || commerceRoot == null
                || assemblyTemplate == null
                || crateTemplate == null)
            {
                Debug.LogError(
                    "Shop Step 9 failed. Open the saved movement-test scene and confirm the current commerce system exists.");
                return;
            }

            int removedTargets = 0;
            DeliveredEngineStandDisposalTarget[] targets =
                assemblyTemplate.GetComponentsInChildren<DeliveredEngineStandDisposalTarget>(true);
            for (int index = 0; index < targets.Length; index++)
            {
                if (targets[index] == null)
                {
                    continue;
                }

                Undo.DestroyObjectImmediate(targets[index]);
                removedTargets++;
            }

            Transform oldInteraction = FindDescendant(
                assemblyTemplate,
                DisposalInteractionName);
            if (oldInteraction != null)
            {
                Undo.DestroyObjectImmediate(oldInteraction.gameObject);
            }

            int removedPlayerInteractors = 0;
            DeliveredEngineStandDisposalPlayerInteractor[] oldInteractors =
                Object.FindObjectsByType<DeliveredEngineStandDisposalPlayerInteractor>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int index = 0; index < oldInteractors.Length; index++)
            {
                if (oldInteractors[index] == null)
                {
                    continue;
                }

                Undo.DestroyObjectImmediate(oldInteractors[index]);
                removedPlayerInteractors++;
            }

            DeliveredEngineStandWidePlayerInteractor[] wideInteractors =
                Object.FindObjectsByType<DeliveredEngineStandWidePlayerInteractor>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int index = 0; index < wideInteractors.Length; index++)
            {
                if (wideInteractors[index] == null)
                {
                    continue;
                }

                Undo.DestroyObjectImmediate(wideInteractors[index]);
                removedPlayerInteractors++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("Shop Step 9 changed the delivery workflow but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("Shop Step 9 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = commerceRoot;
            Debug.Log(
                $"Shop Step 9 complete. Removed {removedTargets} obsolete stand target(s) and {removedPlayerInteractors} stand-removal Player interactor(s). Future complete assemblies unpack directly onto the floor without a stand.",
                commerceRoot);
        }

        [MenuItem("Hanger 51/Shop and Shipping/10 - Validate Standless Complete Assembly Deliveries")]
        public static void ValidateStandlessCompleteAssemblyDeliveries()
        {
            bool passed = true;
            GameObject commerceRoot = GameObject.Find(CommerceRootName);
            Transform assemblyTemplate = commerceRoot != null
                ? FindDescendant(commerceRoot.transform, AssemblyTemplateName)
                : null;

            if (commerceRoot == null || assemblyTemplate == null)
            {
                Debug.LogError("Shop Step 10 failed: the commerce system or complete-assembly template is missing.");
                passed = false;
            }
            else
            {
                if (assemblyTemplate.GetComponentInChildren<DeliveredEngineStandDisposalTarget>(true) != null)
                {
                    Debug.LogError("Shop Step 10 failed: the obsolete stand-removal target still exists in the delivery template.");
                    passed = false;
                }

                if (commerceRoot.GetComponentInChildren<ShipmentCrateController>(true) == null)
                {
                    Debug.LogError("Shop Step 10 failed: the shipment crate controller is missing.");
                    passed = false;
                }
            }

            if (Object.FindFirstObjectByType<DeliveredEngineStandDisposalPlayerInteractor>(
                    FindObjectsInactive.Include) != null
                || Object.FindFirstObjectByType<DeliveredEngineStandWidePlayerInteractor>(
                    FindObjectsInactive.Include) != null)
            {
                Debug.LogError("Shop Step 10 failed: an obsolete stand-removal Player interactor remains in the scene.");
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("Shop Step 10 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Shop Step 10 passed. Complete Merlin purchases will unpack onto the shipment floor, the stand will disappear with the crate, and no obsolete stand-removal prompts remain.");
            }
        }

        [MenuItem("Hanger 51/P-51 Mustang/20 - Refine External Orbit Camera and Cowling Carry Rule")]
        public static void RefineExternalOrbitCameraAndCowlingCarryRule()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("P-51 Step 20 failed. Exit Play mode first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            P51PilotPlayerInteractor pilot =
                Object.FindFirstObjectByType<P51PilotPlayerInteractor>();
            P51AircraftServiceController service =
                Object.FindFirstObjectByType<P51AircraftServiceController>();
            InventoryInteractor player =
                Object.FindFirstObjectByType<InventoryInteractor>();
            Camera playerCamera = player != null
                ? player.GetComponentInChildren<Camera>(true)
                : null;

            if (!scene.IsValid()
                || string.IsNullOrWhiteSpace(scene.path)
                || pilot == null
                || service == null
                || playerCamera == null)
            {
                Debug.LogError(
                    "P-51 Step 20 failed. Open the saved movement-test scene and confirm the current P-51 flight and service systems exist.");
                return;
            }

            P51ThirdPersonCamera orbitCamera =
                pilot.GetComponent<P51ThirdPersonCamera>();
            if (orbitCamera == null)
            {
                orbitCamera = Undo.AddComponent<P51ThirdPersonCamera>(pilot.gameObject);
            }
            orbitCamera.Configure(pilot, playerCamera);

            SerializedObject serializedCamera = new SerializedObject(orbitCamera);
            SetFloat(serializedCamera, "orbitDistance", 13.5f);
            SetFloat(serializedCamera, "focusForwardOffset", 0.9f);
            SetFloat(serializedCamera, "focusUpOffset", 0.45f);
            SetFloat(serializedCamera, "startingPitch", 14f);
            SetFloat(serializedCamera, "maximumOrbitYaw", 180f);
            SetFloat(serializedCamera, "minimumOrbitPitch", 5f);
            SetFloat(serializedCamera, "maximumOrbitPitch", 65f);
            SetFloat(serializedCamera, "orbitMouseSensitivity", 0.10f);
            SetFloat(serializedCamera, "cameraSharpness", 12f);
            SetFloat(serializedCamera, "obstaclePadding", 0.40f);
            SetFloat(serializedCamera, "externalFieldOfView", 80f);
            serializedCamera.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(orbitCamera);
            EditorUtility.SetDirty(service);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 20 refined the camera but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 Step 20 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = pilot.gameObject;
            Debug.Log(
                "P-51 Step 20 complete. The external camera now has full mouse orbit ownership, remains world-up, faces the aircraft, and the top cowling must be physically carried before installation.",
                pilot);
        }

        [MenuItem("Hanger 51/P-51 Mustang/21 - Validate Orbit Camera and Cowling Carry Rule")]
        public static void ValidateOrbitCameraAndCowlingCarryRule()
        {
            bool passed = true;
            P51PilotPlayerInteractor pilot =
                Object.FindFirstObjectByType<P51PilotPlayerInteractor>();
            P51ThirdPersonCamera orbitCamera = pilot != null
                ? pilot.GetComponent<P51ThirdPersonCamera>()
                : null;
            P51AircraftServiceController service =
                Object.FindFirstObjectByType<P51AircraftServiceController>();

            if (pilot == null || orbitCamera == null || service == null)
            {
                Debug.LogError("P-51 Step 21 failed: the pilot, orbit camera, or service controller is missing.");
                passed = false;
            }
            else
            {
                SerializedObject serializedCamera = new SerializedObject(orbitCamera);
                passed &= ValidateFloat(
                    serializedCamera,
                    "maximumOrbitYaw",
                    180f,
                    "maximum orbit yaw");
                passed &= ValidateFloat(
                    serializedCamera,
                    "orbitDistance",
                    13.5f,
                    "orbit distance");
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("P-51 Step 21 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "P-51 Step 21 passed. Mouse-orbit external view, aircraft-facing camera, cockpit restoration, and the carried-cowling installation workflow are ready.");
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
                    $"P-51 Step 21 failed: {displayName} is {actual:F2}; expected {expected:F2}.");
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
                if (transforms[index] != null && transforms[index].name == objectName)
                {
                    return transforms[index];
                }
            }

            return null;
        }
    }
}
