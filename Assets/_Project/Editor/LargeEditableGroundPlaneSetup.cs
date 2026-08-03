using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class LargeEditableGroundPlaneSetup
    {
        private const string PlaneName = "Plane";
        private const string RunwayRootName = "P-51 Flight Test Runway";
        private const string RunwaySurfaceName = "Runway Asphalt Surface";
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";

        private const string EnvironmentFolder = "Assets/_Project/Environment";
        private const string MeshFolder = EnvironmentFolder + "/Meshes";
        private const string MaterialFolder = EnvironmentFolder + "/Materials";
        private const string GroundMeshPath = MeshFolder + "/LargeEditableGroundPlane.asset";
        private const string GroundMaterialPath = MaterialFolder + "/LargeGroundPlane.mat";

        private const float DefaultPlaneScale = 250f;
        private const float ExpectedMinimumWorldSize = 2000f;

        [MenuItem("Hanger 51/Environment/1 - Add Large Editable Ground Plane")]
        public static void AddLargeEditableGroundPlane()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Environment Step 1 failed. Exit Play mode before creating the ground Plane.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("Environment Step 1 failed. Open the project test scene first.");
                return;
            }

            EnsureFolder(EnvironmentFolder);
            EnsureFolder(MeshFolder);
            EnsureFolder(MaterialFolder);

            GameObject plane = GameObject.Find(PlaneName);
            bool createdPlane = plane == null;
            if (createdPlane)
            {
                plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                Undo.RegisterCreatedObjectUndo(plane, "Create large editable ground Plane");
                plane.name = PlaneName;
                plane.transform.SetPositionAndRotation(
                    DetermineDefaultCenter(),
                    Quaternion.identity);
                plane.transform.localScale = new Vector3(
                    DefaultPlaneScale,
                    1f,
                    DefaultPlaneScale);
            }

            MeshFilter meshFilter = plane.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = plane.GetComponent<MeshRenderer>();
            MeshCollider meshCollider = plane.GetComponent<MeshCollider>();
            if (meshFilter == null)
            {
                meshFilter = Undo.AddComponent<MeshFilter>(plane);
            }
            if (meshRenderer == null)
            {
                meshRenderer = Undo.AddComponent<MeshRenderer>(plane);
            }
            if (meshCollider == null)
            {
                meshCollider = Undo.AddComponent<MeshCollider>(plane);
            }

            EnsurePlaneHasEditableMesh(meshFilter, meshCollider);

            Material groundMaterial = CreateOrUpdateGroundMaterial();
            if (createdPlane || meshRenderer.sharedMaterial == null)
            {
                meshRenderer.sharedMaterial = groundMaterial;
            }
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            meshRenderer.receiveShadows = true;

            meshCollider.convex = false;
            meshCollider.isTrigger = false;
            plane.isStatic = false;

            EditorUtility.SetDirty(plane);
            EditorUtility.SetDirty(meshFilter);
            EditorUtility.SetDirty(meshRenderer);
            EditorUtility.SetDirty(meshCollider);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (string.IsNullOrWhiteSpace(scene.path)
                || !EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("Environment Step 1 created the Plane but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("Environment Step 1 created the Plane, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = plane;
            SceneView.lastActiveSceneView?.FrameSelected();

            Bounds bounds = meshRenderer.bounds;
            string action = createdPlane
                ? "Created"
                : "Preserved and verified";
            Debug.Log(
                $"Environment Step 1 complete. {action} the editable scene object '{PlaneName}' at approximately "
                + $"{bounds.size.x:F0} m × {bounds.size.z:F0} m. Its unique mesh, renderer, material, and MeshCollider are ready. "
                + "Move or scale the Plane directly in the Inspector; rerunning this setup will not reset your transform or mesh edits.",
                plane);
        }

        [MenuItem("Hanger 51/Environment/2 - Validate Large Editable Ground Plane")]
        public static void ValidateLargeEditableGroundPlane()
        {
            bool passed = true;
            GameObject plane = GameObject.Find(PlaneName);
            if (plane == null)
            {
                Debug.LogError("Environment Step 2 failed: no active GameObject named exactly 'Plane' exists.");
                return;
            }

            MeshFilter meshFilter = plane.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = plane.GetComponent<MeshRenderer>();
            MeshCollider meshCollider = plane.GetComponent<MeshCollider>();

            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogError("Environment Step 2 failed: Plane has no editable MeshFilter mesh.", plane);
                passed = false;
            }
            if (meshRenderer == null || meshRenderer.sharedMaterial == null)
            {
                Debug.LogError("Environment Step 2 failed: Plane has no MeshRenderer or ground material.", plane);
                passed = false;
            }
            if (meshCollider == null || meshCollider.sharedMesh == null || meshCollider.isTrigger)
            {
                Debug.LogError("Environment Step 2 failed: Plane has no usable non-trigger MeshCollider.", plane);
                passed = false;
            }
            if (plane.GetComponent<Rigidbody>() != null)
            {
                Debug.LogError("Environment Step 2 failed: the ground Plane should not have a Rigidbody.", plane);
                passed = false;
            }

            Bounds bounds = meshRenderer != null
                ? meshRenderer.bounds
                : new Bounds(plane.transform.position, Vector3.zero);
            if (bounds.size.x < ExpectedMinimumWorldSize
                || bounds.size.z < ExpectedMinimumWorldSize)
            {
                Debug.LogError(
                    $"Environment Step 2 failed: Plane is only {bounds.size.x:F0} m × {bounds.size.z:F0} m; "
                    + $"expected at least {ExpectedMinimumWorldSize:F0} m in both horizontal directions for the initial large area.",
                    plane);
                passed = false;
            }

            string meshPath = meshFilter != null && meshFilter.sharedMesh != null
                ? AssetDatabase.GetAssetPath(meshFilter.sharedMesh)
                : string.Empty;
            if (string.IsNullOrWhiteSpace(meshPath)
                || !meshPath.StartsWith("Assets/"))
            {
                Debug.LogError(
                    "Environment Step 2 failed: Plane still uses Unity's shared built-in mesh instead of its own editable project mesh.",
                    plane);
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("Environment Step 2 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"Environment Step 2 passed. '{PlaneName}' is an editable {bounds.size.x:F0} m × {bounds.size.z:F0} m ground object "
                    + "with a unique saved mesh, visible material, non-trigger MeshCollider, no Rigidbody, and valid Build and Run setup.",
                    plane);
            }
        }

        private static Vector3 DetermineDefaultCenter()
        {
            GameObject runway = GameObject.Find(RunwayRootName);
            Transform runwaySurface = runway != null
                ? FindDescendant(runway.transform, RunwaySurfaceName)
                : null;
            if (runway != null)
            {
                float y = runway.transform.position.y - 0.20f;
                Renderer runwayRenderer = runwaySurface != null
                    ? runwaySurface.GetComponent<Renderer>()
                    : null;
                if (runwayRenderer != null)
                {
                    y = runwayRenderer.bounds.min.y - 0.025f;
                }

                return new Vector3(
                    runway.transform.position.x,
                    y,
                    runway.transform.position.z);
            }

            GameObject aircraft = GameObject.Find(AircraftRootName);
            if (aircraft != null)
            {
                return new Vector3(
                    aircraft.transform.position.x,
                    aircraft.transform.position.y - 0.20f,
                    aircraft.transform.position.z);
            }

            return new Vector3(0f, -0.20f, 0f);
        }

        private static void EnsurePlaneHasEditableMesh(
            MeshFilter meshFilter,
            MeshCollider meshCollider)
        {
            if (meshFilter == null)
            {
                return;
            }

            Mesh currentMesh = meshFilter.sharedMesh;
            string currentPath = currentMesh != null
                ? AssetDatabase.GetAssetPath(currentMesh)
                : string.Empty;

            if (currentMesh == null)
            {
                Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(GroundMeshPath);
                if (savedMesh != null)
                {
                    meshFilter.sharedMesh = savedMesh;
                    meshCollider.sharedMesh = savedMesh;
                }
                return;
            }

            bool isProjectMesh = !string.IsNullOrWhiteSpace(currentPath)
                && currentPath.StartsWith("Assets/");
            if (!isProjectMesh)
            {
                Mesh editableMesh = Object.Instantiate(currentMesh);
                editableMesh.name = "Large Editable Ground Plane";

                Mesh existingAsset = AssetDatabase.LoadAssetAtPath<Mesh>(GroundMeshPath);
                if (existingAsset != null)
                {
                    AssetDatabase.DeleteAsset(GroundMeshPath);
                }

                AssetDatabase.CreateAsset(editableMesh, GroundMeshPath);
                meshFilter.sharedMesh = editableMesh;
                meshCollider.sharedMesh = editableMesh;
                return;
            }

            meshCollider.sharedMesh = currentMesh;
        }

        private static Material CreateOrUpdateGroundMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Hidden/InternalErrorShader");

            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "Large Ground Plane"
                };
                AssetDatabase.CreateAsset(material, GroundMaterialPath);
            }
            else if (shader != null && material.shader != shader)
            {
                material.shader = shader;
            }

            Color groundColor = new Color(0.20f, 0.29f, 0.12f, 1f);
            material.color = groundColor;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", groundColor);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", groundColor);
            }
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.05f);
            }
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0.05f);
            }
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
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
                Transform candidate = transforms[index];
                if (candidate != null && candidate.name == objectName)
                {
                    return candidate;
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
