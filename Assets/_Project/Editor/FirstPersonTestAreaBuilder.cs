using System.Collections.Generic;
using Hanger51.Player;
using Hanger51.Systems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class FirstPersonTestAreaBuilder
    {
        private const string PlayerObjectName = "Player";
        private const string SystemsObjectName = "Game Systems";
        private const string SceneFolderPath = "Assets/_Project/Scenes";
        private const string ScenePath = SceneFolderPath + "/FirstPersonMovementTest.unity";

        [MenuItem("Hanger 51/Setup/1 - Create or Recreate Test Area")]
        public static void CreateTestArea()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureFolderExists("Assets", "_Project");
            EnsureFolderExists("Assets/_Project", "Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "FirstPersonMovementTest";

            CreateLighting();
            CreateEnvironment();
            CreateGameSystems();
            CreatePlayer();

            EditorSceneManager.SaveScene(scene, ScenePath);
            ConfigureInputAndFramePacing();
            AddTestSceneToBuild();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeGameObject = GameObject.Find(PlayerObjectName);
            Debug.Log(
                $"Step 1 complete. Created the test scene at '{ScenePath}' and added it to the build.");
        }

        [MenuItem("Hanger 51/Setup/2 - Apply Movement and Camera Fix")]
        public static void ApplyMovementAndCameraFix()
        {
            GameObject player = GameObject.Find(PlayerObjectName);
            if (player == null)
            {
                Debug.LogError(
                    $"Step 2 failed. Could not find a GameObject named '{PlayerObjectName}' in the active scene.");
                return;
            }

            CharacterController characterController = player.GetComponent<CharacterController>();
            FirstPersonController firstPersonController = player.GetComponent<FirstPersonController>();
            Camera playerCamera = player.GetComponentInChildren<Camera>();

            if (characterController == null || firstPersonController == null || playerCamera == null)
            {
                Debug.LogError(
                    "Step 2 failed. Player needs a CharacterController, FirstPersonController, and child Camera.",
                    player);
                return;
            }

            FirstPersonCameraSmoother cameraSmoother =
                playerCamera.GetComponent<FirstPersonCameraSmoother>();

            if (cameraSmoother == null)
            {
                cameraSmoother = Undo.AddComponent<FirstPersonCameraSmoother>(playerCamera.gameObject);
            }

            Undo.RecordObject(player.transform, "Apply movement and camera fix");
            Undo.RecordObject(characterController, "Apply movement and camera fix");
            Undo.RecordObject(firstPersonController, "Apply movement and camera fix");
            Undo.RecordObject(cameraSmoother, "Apply movement and camera fix");

            if (SceneManager.GetActiveScene().name == "FirstPersonMovementTest")
            {
                Vector3 playerPosition = player.transform.position;
                playerPosition.y = 0.02f;
                player.transform.position = playerPosition;
            }

            ConfigureCharacterController(characterController);
            ConfigureFirstPersonController(firstPersonController, playerCamera);
            ConfigureCameraSmoother(cameraSmoother, player.transform, firstPersonController);
            EnsureGameSystemsExist();

            EditorUtility.SetDirty(player.transform);
            EditorUtility.SetDirty(characterController);
            EditorUtility.SetDirty(firstPersonController);
            EditorUtility.SetDirty(cameraSmoother);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Selection.activeGameObject = player;
            Debug.Log("Step 2 complete. Applied movement, grounding, and camera defaults.", player);
        }

        [MenuItem("Hanger 51/Setup/3 - Configure Input and Frame Pacing")]
        public static void ConfigureInputAndFramePacing()
        {
            InputSettings inputSettings = InputSystem.settings;

            if (inputSettings == null)
            {
                Debug.LogError(
                    "Step 3 failed. Unity could not find Input System settings. Confirm the Input System package is installed.");
                return;
            }

            inputSettings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            EditorUtility.SetDirty(inputSettings);

            QualitySettings.vSyncCount = 1;
            AssetDatabase.SaveAssets();

            Debug.Log(
                "Step 3 complete. Input System uses Dynamic Update and VSync is enabled for the active quality level.");
        }

        [MenuItem("Hanger 51/Setup/4 - Add Test Scene to Build")]
        public static void AddTestSceneToBuild()
        {
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (sceneAsset == null)
            {
                Debug.LogError(
                    $"Step 4 failed. The scene does not exist at '{ScenePath}'. Run Step 1 first.");
                return;
            }

            List<EditorBuildSettingsScene> scenes =
                new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            for (int index = scenes.Count - 1; index >= 0; index--)
            {
                if (scenes[index].path == ScenePath)
                {
                    scenes.RemoveAt(index);
                }
            }

            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log(
                $"Step 4 complete. '{ScenePath}' is now the first enabled scene in the build.");
        }

        [MenuItem("Hanger 51/Setup/5 - Validate All Setup")]
        public static void ValidateAllSetup()
        {
            bool passed = true;

            if (!Application.unityVersion.StartsWith("6000.3"))
            {
                Debug.LogWarning(
                    $"Unity version warning: this project is running {Application.unityVersion}. "
                    + "The project target is Unity 6.3 LTS (6000.3.x). Version differences can affect packages and settings.");
            }

            InputSettings inputSettings = InputSystem.settings;
            if (inputSettings == null
                || inputSettings.updateMode != InputSettings.UpdateMode.ProcessEventsInDynamicUpdate)
            {
                Debug.LogError("Validation failed: Input System Update Mode is not Dynamic Update.");
                passed = false;
            }

            if (QualitySettings.vSyncCount < 1)
            {
                Debug.LogError("Validation failed: VSync is disabled for the active quality level.");
                passed = false;
            }

            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (sceneAsset == null)
            {
                Debug.LogError($"Validation failed: missing scene '{ScenePath}'.");
                passed = false;
            }

            bool sceneIsFirstAndEnabled = EditorBuildSettings.scenes.Length > 0
                && EditorBuildSettings.scenes[0].path == ScenePath
                && EditorBuildSettings.scenes[0].enabled;

            if (!sceneIsFirstAndEnabled)
            {
                Debug.LogError(
                    "Validation failed: the first-person test scene is not the first enabled build scene.");
                passed = false;
            }

            GameObject player = GameObject.Find(PlayerObjectName);
            if (player == null)
            {
                Debug.LogError("Validation failed: the active scene has no GameObject named 'Player'.");
                passed = false;
            }
            else
            {
                Camera playerCamera = player.GetComponentInChildren<Camera>();
                if (playerCamera == null
                    || playerCamera.GetComponent<FirstPersonCameraSmoother>() == null)
                {
                    Debug.LogError(
                        "Validation failed: Player Camera is missing FirstPersonCameraSmoother.",
                        player);
                    passed = false;
                }
            }

            if (GameObject.Find(SystemsObjectName) == null)
            {
                Debug.LogError("Validation failed: the active scene has no 'Game Systems' object.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"Step 5 passed. Unity {Application.unityVersion}; Dynamic Input; VSync enabled; "
                    + "test scene is first in the build; Player and Game Systems are present.");
            }
        }

        private static void CreateLighting()
        {
            GameObject lightObject = new GameObject("Directional Light");
            Light directionalLight = lightObject.AddComponent<Light>();
            directionalLight.type = LightType.Directional;
            directionalLight.intensity = 1.2f;
            directionalLight.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.62f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.35f, 0.38f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.18f, 0.2f);
        }

        private static void CreateEnvironment()
        {
            GameObject environmentRoot = new GameObject("Environment");

            CreatePrimitive(
                "Floor",
                PrimitiveType.Cube,
                new Vector3(0f, -0.5f, 0f),
                new Vector3(30f, 1f, 30f),
                environmentRoot.transform);

            CreatePrimitive("North Wall", PrimitiveType.Cube, new Vector3(0f, 2f, 15f), new Vector3(30f, 5f, 1f), environmentRoot.transform);
            CreatePrimitive("South Wall", PrimitiveType.Cube, new Vector3(0f, 2f, -15f), new Vector3(30f, 5f, 1f), environmentRoot.transform);
            CreatePrimitive("East Wall", PrimitiveType.Cube, new Vector3(15f, 2f, 0f), new Vector3(1f, 5f, 30f), environmentRoot.transform);
            CreatePrimitive("West Wall", PrimitiveType.Cube, new Vector3(-15f, 2f, 0f), new Vector3(1f, 5f, 30f), environmentRoot.transform);

            CreatePrimitive("Low Step", PrimitiveType.Cube, new Vector3(0f, 0.25f, 6f), new Vector3(4f, 0.5f, 3f), environmentRoot.transform);
            CreatePrimitive("Tall Platform", PrimitiveType.Cube, new Vector3(8f, 1f, 7f), new Vector3(5f, 2f, 5f), environmentRoot.transform);
            CreatePrimitive("Narrow Passage Left", PrimitiveType.Cube, new Vector3(-4f, 1.5f, -3f), new Vector3(1f, 3f, 8f), environmentRoot.transform);
            CreatePrimitive("Narrow Passage Right", PrimitiveType.Cube, new Vector3(0f, 1.5f, -3f), new Vector3(1f, 3f, 8f), environmentRoot.transform);
            CreatePrimitive("Reference Cube", PrimitiveType.Cube, new Vector3(-8f, 1f, 7f), new Vector3(2f, 2f, 2f), environmentRoot.transform);
        }

        private static void CreateGameSystems()
        {
            GameObject systemsObject = new GameObject(SystemsObjectName);
            systemsObject.AddComponent<FramePacingController>();
        }

        private static void EnsureGameSystemsExist()
        {
            GameObject systemsObject = GameObject.Find(SystemsObjectName);
            if (systemsObject == null)
            {
                systemsObject = new GameObject(SystemsObjectName);
                Undo.RegisterCreatedObjectUndo(systemsObject, "Create Game Systems");
            }

            if (systemsObject.GetComponent<FramePacingController>() == null)
            {
                Undo.AddComponent<FramePacingController>(systemsObject);
            }

            EditorUtility.SetDirty(systemsObject);
        }

        private static void CreatePlayer()
        {
            GameObject player = new GameObject(PlayerObjectName);
            player.transform.position = new Vector3(0f, 0.02f, -10f);

            CharacterController characterController = player.AddComponent<CharacterController>();
            ConfigureCharacterController(characterController);

            GameObject cameraObject = new GameObject("Player Camera");
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            cameraObject.tag = "MainCamera";

            Camera playerCamera = cameraObject.AddComponent<Camera>();
            playerCamera.nearClipPlane = 0.05f;
            playerCamera.fieldOfView = 75f;
            cameraObject.AddComponent<AudioListener>();

            FirstPersonController firstPersonController = player.AddComponent<FirstPersonController>();
            ConfigureFirstPersonController(firstPersonController, playerCamera);

            FirstPersonCameraSmoother cameraSmoother =
                cameraObject.AddComponent<FirstPersonCameraSmoother>();
            ConfigureCameraSmoother(cameraSmoother, player.transform, firstPersonController);
        }

        private static void ConfigureCharacterController(CharacterController controller)
        {
            controller.height = 2f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 1f, 0f);
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 45f;
            controller.skinWidth = 0.08f;
            controller.minMoveDistance = 0f;
        }

        private static void ConfigureFirstPersonController(
            FirstPersonController firstPersonController,
            Camera playerCamera)
        {
            SerializedObject serializedController = new SerializedObject(firstPersonController);

            serializedController.FindProperty("playerCamera").objectReferenceValue = playerCamera;
            serializedController.FindProperty("walkSpeed").floatValue = 5f;
            serializedController.FindProperty("sprintSpeed").floatValue = 8f;
            serializedController.FindProperty("jumpHeight").floatValue = 1.2f;
            serializedController.FindProperty("gravity").floatValue = -24f;
            serializedController.FindProperty("terminalVelocity").floatValue = 50f;
            serializedController.FindProperty("groundLayers").intValue = ~0;
            serializedController.FindProperty("groundProbeDistance").floatValue = 0.12f;
            serializedController.FindProperty("groundProbeStartOffset").floatValue = 0.05f;
            serializedController.FindProperty("groundProbeRadiusInset").floatValue = 0.04f;
            serializedController.FindProperty("mouseSensitivity").floatValue = 0.12f;
            serializedController.FindProperty("verticalLookLimit").floatValue = 85f;
            serializedController.FindProperty("lockCursorOnStart").boolValue = true;

            serializedController.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureCameraSmoother(
            FirstPersonCameraSmoother cameraSmoother,
            Transform playerTransform,
            FirstPersonController firstPersonController)
        {
            SerializedObject serializedSmoother = new SerializedObject(cameraSmoother);

            serializedSmoother.FindProperty("followTarget").objectReferenceValue = playerTransform;
            serializedSmoother.FindProperty("playerController").objectReferenceValue = firstPersonController;
            serializedSmoother.FindProperty("eyeOffset").vector3Value = new Vector3(0f, 1.65f, 0f);
            serializedSmoother.FindProperty("positionSmoothTime").floatValue = 0.025f;

            serializedSmoother.ApplyModifiedPropertiesWithoutUndo();
            cameraSmoother.SnapToTarget();
        }

        private static GameObject CreatePrimitive(
            string objectName,
            PrimitiveType primitiveType,
            Vector3 position,
            Vector3 scale,
            Transform parent)
        {
            GameObject primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = objectName;
            primitive.transform.SetParent(parent);
            primitive.transform.position = position;
            primitive.transform.localScale = scale;
            return primitive;
        }

        private static void EnsureFolderExists(string parentFolder, string folderName)
        {
            string fullPath = parentFolder + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(parentFolder, folderName);
            }
        }
    }
}
