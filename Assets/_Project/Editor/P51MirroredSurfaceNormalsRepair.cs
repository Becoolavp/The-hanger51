using UnityEditor;
using UnityEngine;

namespace Hanger51.EditorTools
{
    public static class P51MirroredSurfaceNormalsRepair
    {
        private enum SurfaceOrientation
        {
            UpperSurface,
            VerticalSides
        }

        private const string MeshFolder = "Assets/_Project/Aircraft/P51/Meshes";
        private const string LeftWingPath = MeshFolder + "/P51D_LeftWing.asset";
        private const string RightWingPath = MeshFolder + "/P51D_RightWing.asset";
        private const string LeftTailPath = MeshFolder + "/P51D_LeftTailplane.asset";
        private const string RightTailPath = MeshFolder + "/P51D_RightTailplane.asset";
        private const string VerticalFinPath = MeshFolder + "/P51D_VerticalFin.asset";

        [MenuItem("Hanger 51/P-51 Mustang/6 - Repair Airframe Surface Normals")]
        public static void RepairAirframeSurfaceNormals()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("P-51 Step 6 failed. Exit Play mode before repairing mesh normals.");
                return;
            }

            bool loadedAll = true;
            int repairedCount = 0;

            repairedCount += RepairIfInsideOut(
                LeftWingPath,
                "left wing",
                SurfaceOrientation.UpperSurface,
                ref loadedAll) ? 1 : 0;
            repairedCount += RepairIfInsideOut(
                RightWingPath,
                "right wing",
                SurfaceOrientation.UpperSurface,
                ref loadedAll) ? 1 : 0;
            repairedCount += RepairIfInsideOut(
                LeftTailPath,
                "left horizontal stabilizer",
                SurfaceOrientation.UpperSurface,
                ref loadedAll) ? 1 : 0;
            repairedCount += RepairIfInsideOut(
                RightTailPath,
                "right horizontal stabilizer",
                SurfaceOrientation.UpperSurface,
                ref loadedAll) ? 1 : 0;
            repairedCount += RepairIfInsideOut(
                VerticalFinPath,
                "vertical fin and rudder",
                SurfaceOrientation.VerticalSides,
                ref loadedAll) ? 1 : 0;

            if (!loadedAll)
            {
                Debug.LogError(
                    "P-51 Step 6 could not find or evaluate every wing and tail mesh. "
                    + "Run the original P-51 build step only if those mesh assets are genuinely missing.");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (repairedCount == 0)
            {
                Debug.Log(
                    "P-51 Step 6 complete. Both wings, both horizontal stabilizers, and the vertical fin/rudder were already facing outward.");
            }
            else
            {
                Debug.Log(
                    $"P-51 Step 6 complete. Repaired {repairedCount} inside-out airframe mesh asset(s). "
                    + "The current aircraft hierarchy and your deleted visual shapes were not rebuilt or changed.");
            }
        }

        [MenuItem("Hanger 51/P-51 Mustang/7 - Validate Airframe Surface Normals")]
        public static void ValidateAirframeSurfaceNormals()
        {
            bool passed = true;
            passed &= ValidateMesh(LeftWingPath, "left wing", SurfaceOrientation.UpperSurface);
            passed &= ValidateMesh(RightWingPath, "right wing", SurfaceOrientation.UpperSurface);
            passed &= ValidateMesh(LeftTailPath, "left horizontal stabilizer", SurfaceOrientation.UpperSurface);
            passed &= ValidateMesh(RightTailPath, "right horizontal stabilizer", SurfaceOrientation.UpperSurface);
            passed &= ValidateMesh(VerticalFinPath, "vertical fin and rudder", SurfaceOrientation.VerticalSides);

            if (passed)
            {
                Debug.Log(
                    "P-51 Step 7 passed. Both wings, both horizontal stabilizers, and both sides of the vertical fin/rudder face outward.");
            }
        }

        private static bool RepairIfInsideOut(
            string assetPath,
            string surfaceName,
            SurfaceOrientation orientation,
            ref bool loadedAll)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (mesh == null)
            {
                Debug.LogError($"P-51 Step 6 failed: the {surfaceName} mesh is missing at '{assetPath}'.");
                loadedAll = false;
                return false;
            }

            if (!TryMeasureOutwardDirection(mesh, orientation, out float directionScore))
            {
                Debug.LogError(
                    $"P-51 Step 6 could not identify the exterior faces on the {surfaceName} mesh.",
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

        private static bool ValidateMesh(
            string assetPath,
            string surfaceName,
            SurfaceOrientation orientation)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (mesh == null)
            {
                Debug.LogError($"P-51 Step 7 failed: the {surfaceName} mesh is missing at '{assetPath}'.");
                return false;
            }

            if (!TryMeasureOutwardDirection(mesh, orientation, out float directionScore))
            {
                Debug.LogError(
                    $"P-51 Step 7 failed: the exterior faces could not be identified on the {surfaceName}.",
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

        private static bool TryMeasureOutwardDirection(
            Mesh mesh,
            SurfaceOrientation orientation,
            out float directionScore)
        {
            switch (orientation)
            {
                case SurfaceOrientation.VerticalSides:
                    return TryMeasureVerticalSideDirection(mesh, out directionScore);
                default:
                    return TryMeasureUpperSurfaceDirection(mesh, out directionScore);
            }
        }

        private static bool TryMeasureUpperSurfaceDirection(Mesh mesh, out float directionScore)
        {
            directionScore = 0f;
            if (!TryGetMeshData(mesh, out Vector3[] vertices, out int[] triangles))
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

        private static bool TryMeasureVerticalSideDirection(Mesh mesh, out float directionScore)
        {
            directionScore = 0f;
            if (!TryGetMeshData(mesh, out Vector3[] vertices, out int[] triangles))
            {
                return false;
            }

            float centerX = mesh.bounds.center.x;
            float sideThreshold = Mathf.Max(mesh.bounds.extents.x * 0.25f, 0.001f);
            int measuredTriangles = 0;

            for (int index = 0; index + 2 < triangles.Length; index += 3)
            {
                Vector3 a = vertices[triangles[index]];
                Vector3 b = vertices[triangles[index + 1]];
                Vector3 c = vertices[triangles[index + 2]];
                float centroidX = (a.x + b.x + c.x) / 3f;

                float expectedDirection;
                if (centroidX < centerX - sideThreshold)
                {
                    expectedDirection = -1f;
                }
                else if (centroidX > centerX + sideThreshold)
                {
                    expectedDirection = 1f;
                }
                else
                {
                    continue;
                }

                Vector3 faceNormal = Vector3.Cross(b - a, c - a);
                float area = faceNormal.magnitude;
                if (area <= 0.000001f)
                {
                    continue;
                }

                float lateralShare = Mathf.Abs(faceNormal.x) / area;
                if (lateralShare < 0.70f)
                {
                    continue;
                }

                directionScore += expectedDirection * faceNormal.x;
                measuredTriangles++;
            }

            return measuredTriangles > 0;
        }

        private static bool TryGetMeshData(
            Mesh mesh,
            out Vector3[] vertices,
            out int[] triangles)
        {
            vertices = null;
            triangles = null;
            if (mesh == null)
            {
                return false;
            }

            vertices = mesh.vertices;
            triangles = mesh.triangles;
            return vertices != null
                && vertices.Length > 0
                && triangles != null
                && triangles.Length >= 3;
        }
    }
}
