using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hanger51.EditorTools
{
    internal static class P51MustangMeshFactory
    {
        internal static readonly Vector3 CowlingCenter =
            new Vector3(0f, 1.96f, 2.875f);

        private readonly struct FuselageSection
        {
            internal readonly float Z;
            internal readonly float CenterY;
            internal readonly float RadiusX;
            internal readonly float RadiusY;

            internal FuselageSection(
                float z,
                float centerY,
                float radiusX,
                float radiusY)
            {
                Z = z;
                CenterY = centerY;
                RadiusX = radiusX;
                RadiusY = radiusY;
            }
        }

        private static readonly FuselageSection[] Sections =
        {
            new FuselageSection(-4.85f, 1.58f, 0.08f, 0.10f),
            new FuselageSection(-4.48f, 1.61f, 0.28f, 0.34f),
            new FuselageSection(-3.90f, 1.63f, 0.46f, 0.50f),
            new FuselageSection(-3.10f, 1.57f, 0.54f, 0.57f),
            new FuselageSection(-2.15f, 1.48f, 0.61f, 0.63f),
            new FuselageSection(-1.20f, 1.42f, 0.67f, 0.68f),
            new FuselageSection(-0.20f, 1.37f, 0.72f, 0.72f),
            new FuselageSection(0.75f, 1.38f, 0.72f, 0.70f),
            new FuselageSection(1.35f, 1.44f, 0.68f, 0.67f),
            new FuselageSection(1.80f, 1.49f, 0.66f, 0.64f),
            new FuselageSection(2.40f, 1.52f, 0.63f, 0.61f),
            new FuselageSection(3.05f, 1.53f, 0.59f, 0.57f),
            new FuselageSection(3.65f, 1.52f, 0.54f, 0.51f),
            new FuselageSection(4.15f, 1.51f, 0.46f, 0.43f),
            new FuselageSection(4.45f, 1.50f, 0.35f, 0.33f)
        };

        internal static Mesh CreateOrUpdateFuselage(string assetPath)
        {
            const int radialSegments = 28;
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            for (int sectionIndex = 0; sectionIndex < Sections.Length; sectionIndex++)
            {
                FuselageSection section = Sections[sectionIndex];
                for (int radial = 0; radial < radialSegments; radial++)
                {
                    float angle = radial / (float)radialSegments * Mathf.PI * 2f;
                    vertices.Add(new Vector3(
                        Mathf.Cos(angle) * section.RadiusX,
                        section.CenterY + Mathf.Sin(angle) * section.RadiusY,
                        section.Z));
                }
            }

            for (int sectionIndex = 0; sectionIndex < Sections.Length - 1; sectionIndex++)
            {
                FuselageSection current = Sections[sectionIndex];
                FuselageSection next = Sections[sectionIndex + 1];
                bool engineBaySegment = current.Z >= 1.34f && next.Z <= 4.46f;

                for (int radial = 0; radial < radialSegments; radial++)
                {
                    int nextRadial = (radial + 1) % radialSegments;
                    float midpointDegrees = ((radial + 0.5f) / radialSegments) * 360f;
                    bool topOpening = engineBaySegment
                        && midpointDegrees >= 38f
                        && midpointDegrees <= 142f;
                    if (topOpening)
                    {
                        continue;
                    }

                    int a = sectionIndex * radialSegments + radial;
                    int b = sectionIndex * radialSegments + nextRadial;
                    int c = (sectionIndex + 1) * radialSegments + nextRadial;
                    int d = (sectionIndex + 1) * radialSegments + radial;
                    AddQuad(triangles, a, b, c, d);
                }
            }

            AddSectionCap(vertices, triangles, 0, radialSegments, true);
            AddSectionCap(
                vertices,
                triangles,
                (Sections.Length - 1) * radialSegments,
                radialSegments,
                false);

            return SaveMesh(assetPath, vertices, triangles, "P-51D Fuselage");
        }

        internal static Mesh CreateOrUpdateTopCowling(string assetPath)
        {
            const int longitudinalSegments = 10;
            const int arcSegments = 12;
            const float startZ = 1.36f;
            const float endZ = 4.42f;
            const float startAngle = 38f;
            const float endAngle = 142f;
            const float innerScale = 0.92f;

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            int rowSize = arcSegments + 1;
            for (int longitudinal = 0; longitudinal <= longitudinalSegments; longitudinal++)
            {
                float t = longitudinal / (float)longitudinalSegments;
                float z = Mathf.Lerp(startZ, endZ, t);
                FuselageSection section = SampleSection(z);

                for (int arc = 0; arc <= arcSegments; arc++)
                {
                    float arcT = arc / (float)arcSegments;
                    float angle = Mathf.Deg2Rad * Mathf.Lerp(startAngle, endAngle, arcT);
                    Vector3 outer = new Vector3(
                        Mathf.Cos(angle) * section.RadiusX,
                        section.CenterY + Mathf.Sin(angle) * section.RadiusY,
                        z) - CowlingCenter;
                    vertices.Add(outer);
                }
            }

            int outerCount = vertices.Count;
            for (int longitudinal = 0; longitudinal <= longitudinalSegments; longitudinal++)
            {
                float t = longitudinal / (float)longitudinalSegments;
                float z = Mathf.Lerp(startZ, endZ, t);
                FuselageSection section = SampleSection(z);

                for (int arc = 0; arc <= arcSegments; arc++)
                {
                    float arcT = arc / (float)arcSegments;
                    float angle = Mathf.Deg2Rad * Mathf.Lerp(startAngle, endAngle, arcT);
                    Vector3 inner = new Vector3(
                        Mathf.Cos(angle) * section.RadiusX * innerScale,
                        section.CenterY + Mathf.Sin(angle) * section.RadiusY * innerScale,
                        z) - CowlingCenter;
                    vertices.Add(inner);
                }
            }

            for (int longitudinal = 0; longitudinal < longitudinalSegments; longitudinal++)
            {
                for (int arc = 0; arc < arcSegments; arc++)
                {
                    int a = longitudinal * rowSize + arc;
                    int b = a + 1;
                    int c = (longitudinal + 1) * rowSize + arc + 1;
                    int d = (longitudinal + 1) * rowSize + arc;
                    AddQuad(triangles, a, b, c, d);

                    int innerA = outerCount + a;
                    int innerB = outerCount + d;
                    int innerC = outerCount + c;
                    int innerD = outerCount + b;
                    AddQuad(triangles, innerA, innerB, innerC, innerD);
                }
            }

            for (int longitudinal = 0; longitudinal < longitudinalSegments; longitudinal++)
            {
                int leftOuterA = longitudinal * rowSize;
                int leftOuterB = (longitudinal + 1) * rowSize;
                int leftInnerB = outerCount + leftOuterB;
                int leftInnerA = outerCount + leftOuterA;
                AddQuad(triangles, leftOuterA, leftOuterB, leftInnerB, leftInnerA);

                int rightOuterA = longitudinal * rowSize + arcSegments;
                int rightOuterB = (longitudinal + 1) * rowSize + arcSegments;
                int rightInnerB = outerCount + rightOuterB;
                int rightInnerA = outerCount + rightOuterA;
                AddQuad(triangles, rightOuterB, rightOuterA, rightInnerA, rightInnerB);
            }

            for (int arc = 0; arc < arcSegments; arc++)
            {
                int frontA = arc;
                int frontB = arc + 1;
                int frontInnerB = outerCount + frontB;
                int frontInnerA = outerCount + frontA;
                AddQuad(triangles, frontB, frontA, frontInnerA, frontInnerB);

                int rearA = longitudinalSegments * rowSize + arc;
                int rearB = rearA + 1;
                int rearInnerB = outerCount + rearB;
                int rearInnerA = outerCount + rearA;
                AddQuad(triangles, rearA, rearB, rearInnerB, rearInnerA);
            }

            return SaveMesh(assetPath, vertices, triangles, "P-51D Removable Top Cowling");
        }

        internal static Mesh CreateOrUpdateWing(string assetPath, bool leftWing)
        {
            float sign = leftWing ? -1f : 1f;
            float[] spans = { 0.38f, 3.15f, 5.64f };
            float[] leading = { 1.18f, 0.69f, 0.18f };
            float[] trailing = { -1.36f, -0.94f, -0.54f };
            float[] centerY = { 1.24f, 1.35f, 1.48f };
            float[] thickness = { 0.22f, 0.14f, 0.065f };

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            for (int station = 0; station < spans.Length; station++)
            {
                float x = sign * spans[station];
                vertices.Add(new Vector3(x, centerY[station] + thickness[station], leading[station]));
                vertices.Add(new Vector3(x, centerY[station] + thickness[station] * 0.58f, trailing[station]));
                vertices.Add(new Vector3(x, centerY[station] - thickness[station], leading[station]));
                vertices.Add(new Vector3(x, centerY[station] - thickness[station] * 0.62f, trailing[station]));
            }

            for (int station = 0; station < spans.Length - 1; station++)
            {
                int a = station * 4;
                int b = (station + 1) * 4;
                AddQuad(triangles, a, b, b + 1, a + 1);
                AddQuad(triangles, a + 3, b + 3, b + 2, a + 2);
                AddQuad(triangles, a + 2, b + 2, b, a);
                AddQuad(triangles, a + 1, b + 1, b + 3, a + 3);
            }

            AddQuad(triangles, 0, 1, 3, 2);
            int tip = (spans.Length - 1) * 4;
            AddQuad(triangles, tip + 2, tip + 3, tip + 1, tip);

            return SaveMesh(
                assetPath,
                vertices,
                triangles,
                leftWing ? "P-51D Left Wing" : "P-51D Right Wing");
        }

        internal static Mesh CreateOrUpdateTailplane(string assetPath, bool leftSide)
        {
            float sign = leftSide ? -1f : 1f;
            float rootX = 0.30f;
            float tipX = 2.15f;
            float rootLeading = -3.52f;
            float rootTrailing = -4.58f;
            float tipLeading = -3.86f;
            float tipTrailing = -4.52f;
            float rootY = 1.78f;
            float tipY = 1.88f;
            float thickness = 0.065f;

            List<Vector3> vertices = new List<Vector3>
            {
                new Vector3(sign * rootX, rootY + thickness, rootLeading),
                new Vector3(sign * rootX, rootY + thickness * 0.5f, rootTrailing),
                new Vector3(sign * rootX, rootY - thickness, rootLeading),
                new Vector3(sign * rootX, rootY - thickness * 0.5f, rootTrailing),
                new Vector3(sign * tipX, tipY + thickness * 0.55f, tipLeading),
                new Vector3(sign * tipX, tipY + thickness * 0.25f, tipTrailing),
                new Vector3(sign * tipX, tipY - thickness * 0.55f, tipLeading),
                new Vector3(sign * tipX, tipY - thickness * 0.25f, tipTrailing)
            };

            List<int> triangles = new List<int>();
            AddQuad(triangles, 0, 4, 5, 1);
            AddQuad(triangles, 3, 7, 6, 2);
            AddQuad(triangles, 2, 6, 4, 0);
            AddQuad(triangles, 1, 5, 7, 3);
            AddQuad(triangles, 0, 1, 3, 2);
            AddQuad(triangles, 6, 7, 5, 4);

            return SaveMesh(
                assetPath,
                vertices,
                triangles,
                leftSide ? "P-51D Left Stabilizer" : "P-51D Right Stabilizer");
        }

        internal static Mesh CreateOrUpdateVerticalFin(string assetPath)
        {
            Vector2[] profile =
            {
                new Vector2(-3.62f, 1.64f),
                new Vector2(-3.96f, 3.28f),
                new Vector2(-4.25f, 3.70f),
                new Vector2(-4.52f, 3.55f),
                new Vector2(-4.70f, 2.35f),
                new Vector2(-4.66f, 1.67f)
            };
            const float halfThickness = 0.09f;

            List<Vector3> vertices = new List<Vector3>();
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? -halfThickness : halfThickness;
                for (int index = 0; index < profile.Length; index++)
                {
                    vertices.Add(new Vector3(x, profile[index].y, profile[index].x));
                }
            }

            List<int> triangles = new List<int>();
            for (int index = 1; index < profile.Length - 1; index++)
            {
                triangles.Add(0);
                triangles.Add(index + 1);
                triangles.Add(index);

                triangles.Add(profile.Length);
                triangles.Add(profile.Length + index);
                triangles.Add(profile.Length + index + 1);
            }

            for (int index = 0; index < profile.Length; index++)
            {
                int next = (index + 1) % profile.Length;
                AddQuad(
                    triangles,
                    index,
                    next,
                    profile.Length + next,
                    profile.Length + index);
            }

            return SaveMesh(assetPath, vertices, triangles, "P-51D Vertical Fin");
        }

        internal static Mesh CreateOrUpdateSpinner(string assetPath)
        {
            const int radialSegments = 28;
            const int lengthSegments = 7;
            const float length = 0.53f;
            const float baseRadius = 0.36f;

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            for (int lengthIndex = 0; lengthIndex <= lengthSegments; lengthIndex++)
            {
                float t = lengthIndex / (float)lengthSegments;
                float z = t * length;
                float radius = baseRadius * Mathf.Pow(1f - t, 0.62f);
                for (int radial = 0; radial < radialSegments; radial++)
                {
                    float angle = radial / (float)radialSegments * Mathf.PI * 2f;
                    vertices.Add(new Vector3(
                        Mathf.Cos(angle) * radius,
                        Mathf.Sin(angle) * radius,
                        z));
                }
            }

            for (int lengthIndex = 0; lengthIndex < lengthSegments; lengthIndex++)
            {
                for (int radial = 0; radial < radialSegments; radial++)
                {
                    int next = (radial + 1) % radialSegments;
                    int a = lengthIndex * radialSegments + radial;
                    int b = lengthIndex * radialSegments + next;
                    int c = (lengthIndex + 1) * radialSegments + next;
                    int d = (lengthIndex + 1) * radialSegments + radial;
                    AddQuad(triangles, a, b, c, d);
                }
            }

            AddSectionCap(vertices, triangles, 0, radialSegments, true);
            return SaveMesh(assetPath, vertices, triangles, "P-51D Spinner");
        }

        internal static Mesh CreateOrUpdatePropellerBlade(string assetPath)
        {
            const float thickness = 0.035f;
            Vector2[] outline =
            {
                new Vector2(-0.09f, 0.22f),
                new Vector2(-0.15f, 0.72f),
                new Vector2(-0.13f, 1.20f),
                new Vector2(-0.07f, 1.58f),
                new Vector2(0.05f, 1.61f),
                new Vector2(0.12f, 1.18f),
                new Vector2(0.16f, 0.70f),
                new Vector2(0.09f, 0.22f)
            };

            List<Vector3> vertices = new List<Vector3>();
            for (int side = 0; side < 2; side++)
            {
                float z = side == 0 ? -thickness : thickness;
                for (int index = 0; index < outline.Length; index++)
                {
                    vertices.Add(new Vector3(outline[index].x, outline[index].y, z));
                }
            }

            List<int> triangles = new List<int>();
            for (int index = 1; index < outline.Length - 1; index++)
            {
                triangles.Add(0);
                triangles.Add(index);
                triangles.Add(index + 1);
                triangles.Add(outline.Length);
                triangles.Add(outline.Length + index + 1);
                triangles.Add(outline.Length + index);
            }

            for (int index = 0; index < outline.Length; index++)
            {
                int next = (index + 1) % outline.Length;
                AddQuad(
                    triangles,
                    index,
                    outline.Length + index,
                    outline.Length + next,
                    next);
            }

            return SaveMesh(assetPath, vertices, triangles, "P-51D Propeller Blade");
        }

        internal static FuselageSurfaceSample SampleCowlingSurface(float z, float x)
        {
            FuselageSection section = SampleSection(z);
            float normalizedX = Mathf.Clamp(x / Mathf.Max(0.01f, section.RadiusX), -0.98f, 0.98f);
            float normalizedY = Mathf.Sqrt(Mathf.Max(0f, 1f - normalizedX * normalizedX));
            float y = section.CenterY + section.RadiusY * normalizedY;
            Vector3 normal = new Vector3(
                x / Mathf.Max(0.01f, section.RadiusX * section.RadiusX),
                (y - section.CenterY) / Mathf.Max(0.01f, section.RadiusY * section.RadiusY),
                0f).normalized;
            return new FuselageSurfaceSample(new Vector3(x, y, z), normal);
        }

        internal readonly struct FuselageSurfaceSample
        {
            internal readonly Vector3 Position;
            internal readonly Vector3 Normal;

            internal FuselageSurfaceSample(Vector3 position, Vector3 normal)
            {
                Position = position;
                Normal = normal;
            }
        }

        private static FuselageSection SampleSection(float z)
        {
            if (z <= Sections[0].Z)
            {
                return Sections[0];
            }

            for (int index = 0; index < Sections.Length - 1; index++)
            {
                FuselageSection a = Sections[index];
                FuselageSection b = Sections[index + 1];
                if (z > b.Z)
                {
                    continue;
                }

                float t = Mathf.InverseLerp(a.Z, b.Z, z);
                return new FuselageSection(
                    z,
                    Mathf.Lerp(a.CenterY, b.CenterY, t),
                    Mathf.Lerp(a.RadiusX, b.RadiusX, t),
                    Mathf.Lerp(a.RadiusY, b.RadiusY, t));
            }

            return Sections[Sections.Length - 1];
        }

        private static void AddSectionCap(
            List<Vector3> vertices,
            List<int> triangles,
            int ringStart,
            int ringCount,
            bool reverse)
        {
            Vector3 center = Vector3.zero;
            for (int index = 0; index < ringCount; index++)
            {
                center += vertices[ringStart + index];
            }
            center /= ringCount;
            int centerIndex = vertices.Count;
            vertices.Add(center);

            for (int index = 0; index < ringCount; index++)
            {
                int next = (index + 1) % ringCount;
                if (reverse)
                {
                    triangles.Add(centerIndex);
                    triangles.Add(ringStart + next);
                    triangles.Add(ringStart + index);
                }
                else
                {
                    triangles.Add(centerIndex);
                    triangles.Add(ringStart + index);
                    triangles.Add(ringStart + next);
                }
            }
        }

        private static void AddQuad(
            List<int> triangles,
            int a,
            int b,
            int c,
            int d)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(d);
        }

        private static Mesh SaveMesh(
            string assetPath,
            List<Vector3> vertices,
            List<int> triangles,
            string meshName)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (mesh == null)
            {
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, assetPath);
            }

            mesh.name = meshName;
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}
