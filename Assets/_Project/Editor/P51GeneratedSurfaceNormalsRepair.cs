using System;
using UnityEditor;
using UnityEngine;

namespace Hanger51.EditorTools
{
    public static class P51GeneratedSurfaceNormalsRepair
    {
        private const string MeshFolder = "Assets/_Project/Aircraft/P51/Meshes/";
        private const string CanonicalRightWingPath = MeshFolder + "P51D_RightWing_FixedWithAileronCutout.asset";

        private static readonly string[] RepairPaths =
        {
            MeshFolder + "P51D_LeftWing_FixedWithAileronCutout.asset",
            MeshFolder + "P51D_LeftAileron.asset",
            MeshFolder + "P51D_LeftStabilizer_Fixed.asset",
            MeshFolder + "P51D_LeftElevator.asset",
            MeshFolder + "P51D_VerticalFin_Fixed.asset",
            MeshFolder + "P51D_Rudder.asset"
        };

        private static readonly string[] RepairLabels =
        {
            "left fixed wing",
            "left aileron",
            "left fixed horizontal stabilizer",
            "left elevator",
            "vertical fin",
            "rudder"
        };

        [MenuItem("Hanger 51/P-51 Mustang/Current/Repair Generated Surface Normals")]
        public static void RepairGeneratedSurfaceNormals()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 surface-normal repair requires Edit mode with Unity finished compiling.");
                return;
            }

            Mesh canonical = AssetDatabase.LoadAssetAtPath<Mesh>(CanonicalRightWingPath);
            if (!TryGetWindingSign(canonical, out int canonicalSign, out double canonicalVolume))
            {
                Debug.LogError(
                    "P-51 surface-normal repair could not establish the known-good right-wing winding. "
                    + $"Expected mesh at '{CanonicalRightWingPath}'.");
                return;
            }

            int repaired = 0;
            int alreadyCorrect = 0;
            bool loadedAll = true;

            for (int i = 0; i < RepairPaths.Length; i++)
            {
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(RepairPaths[i]);
                if (mesh == null)
                {
                    Debug.LogError($"P-51 surface-normal repair could not find the {RepairLabels[i]} mesh at '{RepairPaths[i]}'.");
                    loadedAll = false;
                    continue;
                }

                if (!TryGetWindingSign(mesh, out int sign, out double volume))
                {
                    Debug.LogError($"P-51 surface-normal repair could not measure the closed winding of the {RepairLabels[i]} mesh.", mesh);
                    loadedAll = false;
                    continue;
                }

                if (sign == canonicalSign)
                {
                    alreadyCorrect++;
                    continue;
                }

                Undo.RegisterCompleteObjectUndo(mesh, $"Repair P-51 {RepairLabels[i]} normals");
                ReverseTriangleWinding(mesh);
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                EditorUtility.SetDirty(mesh);

                if (!TryGetWindingSign(mesh, out int repairedSign, out double repairedVolume) || repairedSign != canonicalSign)
                {
                    Debug.LogError(
                        $"P-51 surface-normal repair reversed the {RepairLabels[i]}, but validation still found the wrong winding. "
                        + $"Before volume={volume:F6}, after volume={repairedVolume:F6}, reference volume={canonicalVolume:F6}.",
                        mesh);
                    loadedAll = false;
                    continue;
                }

                repaired++;
                Debug.Log($"P-51 repaired outward triangle winding on the {RepairLabels[i]}.", mesh);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SceneView.RepaintAll();

            if (!loadedAll)
            {
                Debug.LogError(
                    "P-51 generated surface-normal repair finished with one or more validation errors. "
                    + "Do not rebuild the aircraft hierarchy; inspect the Console messages above instead.");
                return;
            }

            Debug.Log(
                $"P-51 generated surface-normal repair complete. Repaired {repaired} mesh asset(s); "
                + $"{alreadyCorrect} were already correct. The control-surface hierarchy, pivots, animation controller, colliders and scene transforms were not changed.");
        }

        [MenuItem("Hanger 51/P-51 Mustang/Current/Validate Generated Surface Normals")]
        public static void ValidateGeneratedSurfaceNormals()
        {
            Mesh canonical = AssetDatabase.LoadAssetAtPath<Mesh>(CanonicalRightWingPath);
            if (!TryGetWindingSign(canonical, out int canonicalSign, out double canonicalVolume))
            {
                Debug.LogError("P-51 surface-normal validation failed because the right fixed-wing reference mesh is missing or invalid.");
                return;
            }

            bool passed = true;
            for (int i = 0; i < RepairPaths.Length; i++)
            {
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(RepairPaths[i]);
                if (!TryGetWindingSign(mesh, out int sign, out double volume))
                {
                    Debug.LogError($"P-51 surface-normal validation could not measure the {RepairLabels[i]} mesh.", mesh);
                    passed = false;
                    continue;
                }

                if (sign != canonicalSign)
                {
                    Debug.LogError(
                        $"P-51 surface-normal validation failed: the {RepairLabels[i]} is still inside out "
                        + $"(signed volume {volume:F6}; right-wing reference {canonicalVolume:F6}).",
                        mesh);
                    passed = false;
                }
            }

            if (passed)
            {
                Debug.Log(
                    "P-51 generated surface-normal validation passed. The left wing/aileron, left stabilizer/elevator, vertical fin and rudder all use the same outward winding as the known-good right wing. "
                    + "No transform or control-surface animation data was modified.");
            }
        }

        private static bool TryGetWindingSign(Mesh mesh, out int sign, out double signedVolume)
        {
            sign = 0;
            signedVolume = 0d;
            if (mesh == null || mesh.vertexCount < 4 || mesh.subMeshCount < 1)
            {
                return false;
            }

            Vector3[] vertices = mesh.vertices;
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                int[] triangles = mesh.GetTriangles(subMesh);
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    Vector3 a = vertices[triangles[i]];
                    Vector3 b = vertices[triangles[i + 1]];
                    Vector3 c = vertices[triangles[i + 2]];
                    signedVolume += Vector3.Dot(a, Vector3.Cross(b, c)) / 6d;
                }
            }

            if (Math.Abs(signedVolume) <= 0.0000001d)
            {
                return false;
            }

            sign = signedVolume > 0d ? 1 : -1;
            return true;
        }

        private static void ReverseTriangleWinding(Mesh mesh)
        {
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                int[] triangles = mesh.GetTriangles(subMesh);
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    int second = triangles[i + 1];
                    triangles[i + 1] = triangles[i + 2];
                    triangles[i + 2] = second;
                }

                mesh.SetTriangles(triangles, subMesh, false);
            }
        }
    }
}
