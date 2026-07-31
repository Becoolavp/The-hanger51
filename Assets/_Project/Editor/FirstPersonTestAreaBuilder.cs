using Hanger51.Player;
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
        private const string SceneFolderPath = "Assets/_Project/Scenes";
        private const string ScenePath = SceneFolderPath + "/FirstPersonMovementTest.unity";

        [MenuItem("Hanger 51/Setup/Create First-Person Test Area")]
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
            CreatePlayer();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeGameObject = GameObject.Find(PlayerObjectName);
            Debug.Log($"Created first-person movement test scene at '{ScenePath}'.");
        }

        [MenuItem("Hanger 51/Setup/Apply First-Person Controller Defaults")]
        public static void ApplyFirstPersonControllerDefaults()
        {
            GameObject player = GameObject.Find(PlayerObjectName);
            if (player == null)
            {
                Debug.LogError($"Could not find a GameObject named '{PlayerObjectName}' in the active scene.");
                return;
            }

            CharacterController controller = player.GetComponent<CharacterController>();
            FirstPersonController firstPersonController = player.GetComponent<FirstPersonController>();
            Camera playerCamera = player.GetComponentInChildren<Camera>();

            if (controller == null || firstPersonController == null || playerCamera == null)
            {
                Debug.LogError(
                    "The Player must have a CharacterController, a FirstPersonController, and a child Camera.",
                    player);
                return;
            }

            Undo.RecordObject(player.transform, "Apply first-person controller defaults");
            Undo.RecordObject(controller, "Apply first-person controller defaults");
            Undo.RecordObject(firstPersonController, "Apply first-person controller defaults");

            if (SceneManager.GetActiveScene().name == "FirstPersonMovementTest")
            {
                Vector3 position = player.transform.position;
                position.y = 0.02f;
                player.transform.position = position;
            }

            ConfigureCharacterController(controller);
            ConfigureFirstPersonController(firstPersonController, playerCamera);

            EditorUtility.SetDirty(player.transform);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(firstPersonController);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Selection.activeGameObject = player;
            Debug.Log("Applied the responsive first-person controller defaults.", player);
        }

        // Kept temporarily so an existing instruction or habit still works.
        [MenuItem("Hanger 51/Setup/Apply First-Person Smoothing Defaults")]
        public static void ApplyLegacySmoothingDefaultsMenu()
        {
            ApplyFirstPersonControllerDefaults();
        }

        [MenuItem("Hanger 51/Setup/Validate First-Person Project Settings")]
        public static void ValidateFirstPersonProjectSettings()
        {
            InputSettings settings = InputSystem.settings;
            if (settings == null)
            {
                Debug.LogError(
                    "Unity could not find the Input System settings asset. Open "
                    + "Edit > Project Settings > Input System Package and review the settings.");
                return;
            }

            if (settings.updateMode != InputSettings.UpdateMode.ProcessEventsInDynamicUpdate)
            {
                Debug.LogError(
                    "Incorrect Input System Update Mode. Open Edit > Project Settings > "
                    + "Input System Package and set Update Mode to 'Process Events In Dynamic Update'. "
                    + "The first-person controller reads input in Update(), so Fixed Update mode can "
                    + "cause stuttering and missed jump presses.");
                return;
            }

            Debug.Log(
                $"First-person project settings passed. Unity version: {Application.unityVersion}. "
                + "Input System Update Mode: Process Events In Dynamic Update.");
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

        private static void CreatePlayer()
        {
            GameObject player = new GameObject(PlayerObjectName);
            player.transform.position = new Vector3(0f, 0.02f, -10f);

            CharacterController controller = player.AddComponent<CharacterController>();
            ConfigureCharacterController(controller);

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
        }

        private static void ConfigureCharacterController(CharacterController controller)
        {
            controller.height = 2f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 1f, 0f);
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 45f;
            controller.skinWidth = 0.04f;
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
            serializedController.FindProperty("groundedVelocity").floatValue = -2f;
            serializedController.FindProperty("terminalVelocity").floatValue = 50f;
            serializedController.FindProperty("mouseSensitivity").floatValue = 0.12f;
            serializedController.FindProperty("verticalLookLimit").floatValue = 85f;
            serializedController.FindProperty("lockCursorOnStart").boolValue = true;

            serializedController.ApplyModifiedPropertiesWithoutUndo();
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
