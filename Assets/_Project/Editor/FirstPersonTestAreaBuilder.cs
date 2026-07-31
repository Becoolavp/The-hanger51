using Hanger51.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class FirstPersonTestAreaBuilder
    {
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

            Selection.activeGameObject = GameObject.Find("Player");
            Debug.Log($"Created first-person movement test scene at '{ScenePath}'.");
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
            GameObject player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 1.05f, -10f);

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 1f, 0f);
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 45f;
            controller.skinWidth = 0.08f;

            GameObject cameraObject = new GameObject("Player Camera");
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            cameraObject.tag = "MainCamera";

            Camera playerCamera = cameraObject.AddComponent<Camera>();
            playerCamera.nearClipPlane = 0.05f;
            playerCamera.fieldOfView = 75f;
            cameraObject.AddComponent<AudioListener>();

            FirstPersonController firstPersonController = player.AddComponent<FirstPersonController>();

            SerializedObject serializedController = new SerializedObject(firstPersonController);
            serializedController.FindProperty("playerCamera").objectReferenceValue = playerCamera;
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
