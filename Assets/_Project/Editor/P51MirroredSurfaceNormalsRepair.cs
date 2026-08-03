using UnityEditor;
using UnityEngine;

namespace Hanger51.EditorTools
{
    public static class P51MirroredSurfaceNormalsRepair
    {
        private const string MeshFolder = "Assets/_Project/Aircraft/P51/Meshes";
        private const string LeftWingPath = MeshFolder + "/P51D_LeftWing.asset";
        private const string RightWingPath = MeshFolder + "/P51D_RightWing.asset";
        private const string LeftTailPath = MeshFolder + "/P51D_LeftTailplane.asset";
        private const string RightTailPath = MeshFolder + "/P51D_RightTailplane.asset";

        [MenuItem("Hanger 51/P-51 Mustang/6 - Repair Mirrored Wing Normals")]
        public static void RepairMirroredWingNormals()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("P-51 Step 6 failed. Exit Play mode before repairing mesh normals.");
                return;
            }

            bool loadedAll = true;
            int repairedCount = 0;

            repairedCount += RepairIfInsideOut(LeftWingPath, "left wing", ref loadedAll) ? 1 : 0;
            repairedCount += RepairIfInsideOut(RightWingPath, "right wing", ref loadedAll) ? 1 : 0;
            repairedCount += RepairIfInsideOut(LeftTailPath, "left horizontal stabilizer", ref loadedAll) ? 1 : 0;
            repairedCount += RepairIfInsideOut(RightTailPath, "right horizontal stabilizer", ref loadedAll) ? 1 : 0;

            if (!loadedAll)
            {
                Debug.LogError(
                    "P-51 Step 6 could not find every wing and stabilizer mesh. Run the original P-51 build step only if those mesh assets are genuinely missing.");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (repairedCount == 0)
            {
                Debug.Log("P-51 Step 6 complete. All wing and stabilizer exterior normals were already facing outward.");
            }
            else
            {
                Debug.Log(
                    $"P-51 Step 6 complete. Repaired {repairedCount} inside-out mirrored mesh asset(s). "
                    + "The current aircraft hierarchy and your deleted visual shapes were not rebuilt or changed.");
            }
        }

        [MenuItem("Hanger 51/P-51 Mustang/7 - Validate Wing Normals")]
        public static void ValidateWingNormals()
        {
            bool passed = true;
            passed &= ValidateMesh(LeftWingPath, "left wing");
            passed &= ValidateMesh(RightWingPath, "right wing");
            passed &= ValidateMesh(LeftTailPath, "left horizontal stabilizer");
            passed &= ValidateMesh(RightTailPath, "right horizontal stabilizer");

            if (passed)
            {
                Debug.Log(
                    "P-51 Step 7 passed. Both wings and both horizontal stabilizers have outward-facing upper surfaces.");
            }
        }

        private static bool RepairIfInsideOut(
            string assetPath,
            string surfaceName,
            ref bool loadedAll)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (mesh == null)
            {
                Debug.LogError($"P-51 Step 6 failed: the {surfaceName} mesh is missing at '{assetPath}'.");
                loadedAll = false;
                return false;
            }

            if (!TryMeasureUpperSurfaceDirection(mesh, out float directionScore))
            {
                Debug.LogError(
                    $"P-51 Step 6 could not identify an upper surface on the {surfaceName} mesh.",
                    mesh);
                loadedAll = false;
                return false;
            }

            if (directionScore > 0f)
            {
                return false;
            }

            Undo.RecordObject(mesh, $"Repair P-51 {surfaceName} normals");
            int[] triangles = mesh.triangles;
            for (int index = 0; index + 2 < triangles.Length; index += 3)
            {
                int second = triangles[index + 1];
                triangles[index + 1] = triangles[index + 2];
                triangles[index + 2] = second;
            }

            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);

            Debug.Log($"Repaired inward-facing normals on the P-51 {surfaceName}.", mesh);
            return true;
        }

        private static bool ValidateMesh(string assetPath, string surfaceName)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (mesh == null)
            {
                Debug.LogError($"P-51 Step 7 failed: the {surfaceName} mesh is missing at '{assetPath}'.");
                return false;
            }

            if (!TryMeasureUpperSurfaceDirection(mesh, out float directionScore))
            {
                Debug.LogError(
                    $"P-51 Step 7 failed: an upper surface could not be identified on the {surfaceName}.",
                    mesh);
                return false;
            }

            if (directionScore <= 0f)
            {
                Debug.LogError(
                    $"P-51 Step 7 failed: the {surfaceName} is still inside out. Run P-51 Step 6.",
                    mesh);
                return false;
            }

            return true;
        }

        private static bool TryMeasureUpperSurfaceDirection(Mesh mesh, out float directionScore)
        {
            directionScore = 0f;
            if (mesh == null)
            {
                return false;
            }

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            if (vertices == null || vertices.Length == 0 || triangles == null || triangles.Length < 3)
            {
                return false;
            }

            float centerY = mesh.bounds.center.y;
            int measuredTriangles = 0;

            for (int index = 0; index + 2 < triangles.Length; index += 3)
            {
                Vector3 a = vertices[triangles[index]];
                Vector3 b = vertices[triangles[index + 1]];
                Vector3 c = vertices[triangles[index + 2]];
                float centroidY = (a.y + b.y + c.y) / 3f;
                if (centroidY <= centerY)
                {
                    continue;
                }

                Vector3 faceNormal = Vector3.Cross(b - a, c - a);
                float area = faceNormal.magnitude;
                if (area <= 0.000001f)
                {
                    continue;
                }

                float verticalShare = Mathf.Abs(faceNormal.y) / area;
                if (verticalShare < 0.35f)
                {
                    continue;
                }

                directionScore += faceNormal.y;
                measuredTriangles++;
            }

            return measuredTriangles > 0;
        }
    }
}
