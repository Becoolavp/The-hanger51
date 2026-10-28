using System.Collections.Generic;
using Hanger51.Aircraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51SmoothCanopyAndSealSetup
    {
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string CockpitRootName = "P-51 Cockpit Interior";
        private const string OpeningRimRootName = "P-51 True Cockpit Opening Rim";
        private const string OldCanopyAssemblyName = "P-51 Corrected Full-Length Canopy Assembly";
        private const string NewCanopyAssemblyName = "P-51 Smooth Sealed Canopy Assembly";
        private const string GlassObjectName = "P-51 Smooth Bubble Canopy Glass";
        private const string SealRootName = "P-51 Canopy Fuselage Seals";

        private const string SmoothCanopyMeshPath =
            "Assets/_Project/Aircraft/P51/Meshes/P51D_SmoothSealedCanopy.asset";
        private const string SourceGlassMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/CanopyGlass.mat";
        private const string SmoothGlassMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/CanopyGlassSmooth.mat";
        private const string MetalMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/PolishedAluminum.mat";
        private const string DarkMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/DarkAircraftMetal.mat";

        private const int LongitudinalRings = 65;
        private const int CrossSectionSegments = 32;
        private const float ArchExponent = 0.78f;

        private readonly struct ProfileStation
        {
            internal readonly float Z;
            internal readonly float HalfWidth;
            internal readonly float SillY;
            internal readonly float CrownY;

            internal ProfileStation(float z, float halfWidth, float sillY, float crownY)
            {
                Z = z;
                HalfWidth = halfWidth;
                SillY = sillY;
                CrownY = crownY;
            }
        }

        // The rear half is the P-51D bubble.  The forward stations smoothly lower and flatten
        // into the windshield instead of forming a separate faceted wedge.  The side edges sit
        // just below the upper opening rim so the seal pieces can overlap both glass and body.
        private static readonly ProfileStation[] Profile =
        {
            new ProfileStation(-1.18f, 0.500f, 1.715f, 1.925f),
            new ProfileStation(-1.06f, 0.535f, 1.705f, 2.105f),
            new ProfileStation(-0.88f, 0.575f, 1.695f, 2.275f),
            new ProfileStation(-0.64f, 0.605f, 1.688f, 2.395f),
            new ProfileStation(-0.36f, 0.620f, 1.684f, 2.455f),
            new ProfileStation(-0.08f, 0.620f, 1.685f, 2.468f),
            new ProfileStation( 0.18f, 0.605f, 1.690f, 2.425f),
            new ProfileStation( 0.42f, 0.580f, 1.700f, 2.335f),
            new ProfileStation( 0.64f, 0.545f, 1.710f, 2.205f),
            new ProfileStation( 0.82f, 0.515f, 1.722f, 2.075f),
            new ProfileStation( 1.00f, 0.490f, 1.733f, 1.920f),
            new ProfileStation( 1.20f, 0.465f, 1.742f, 1.790f)
        };

        [MenuItem("Hanger 51/P-51 Mustang/77 - Rebuild Smooth P-51 Canopy and Close Fuselage Gaps")]
        public static void RebuildSmoothCanopyAndCloseGaps()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 Step 77 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("P-51 Step 77 failed. Open the saved Hanger 51 gameplay scene first.");
                return;
            }

            Material sourceGlass = AssetDatabase.LoadAssetAtPath<Material>(SourceGlassMaterialPath);
            Material metal = AssetDatabase.LoadAssetAtPath<Material>(MetalMaterialPath);
            Material dark = AssetDatabase.LoadAssetAtPath<Material>(DarkMaterialPath);
            if (sourceGlass == null || metal == null || dark == null)
            {
                Debug.LogError("P-51 Step 77 failed. Required P-51 canopy materials are missing.");
                return;
            }

            Material smoothGlass = CreateOrUpdateSmoothGlassMaterial(sourceGlass);
            Mesh smoothCanopy = CreateOrUpdateSmoothCanopyMesh();
            if (smoothGlass == null || smoothCanopy == null)
            {
                Debug.LogError("P-51 Step 77 failed. Smooth canopy material or mesh could not be created.");
                return;
            }

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft == null || aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 77 failed. No P-51 aircraft were found in the scene.");
                return;
            }

            int aircraftUpdated = 0;
            int oldAssembliesDisabled = 0;
            P51FlightController master = null;

            for (int i = 0; i < aircraft.Length; i++)
            {
                P51FlightController flight = aircraft[i];
                if (flight == null || !flight.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (flight.name == AircraftRootName)
                {
                    master = flight;
                }

                Transform cockpit = FindDescendant(flight.transform, CockpitRootName);
                Transform openingRim = FindDescendant(flight.transform, OpeningRimRootName);
                if (cockpit == null || openingRim == null)
                {
                    Debug.LogWarning(
                        $"P-51 Step 77 skipped '{flight.name}' because the Step 73 hollow cockpit/rim is missing.",
                        flight);
                    continue;
                }

                Transform oldAssembly = FindDescendant(flight.transform, OldCanopyAssemblyName);
                if (oldAssembly != null && oldAssembly.gameObject.activeSelf)
                {
                    Undo.RecordObject(oldAssembly.gameObject, "Disable faceted P-51 canopy assembly");
                    oldAssembly.gameObject.SetActive(false);
                    oldAssembliesDisabled++;
                    EditorUtility.SetDirty(oldAssembly.gameObject);
                }

                DisableStrayCorrectedCanopyGlass(flight.transform);
                BuildSmoothCanopyAssembly(flight.transform, smoothCanopy, smoothGlass, metal, dark);
                aircraftUpdated++;
                EditorUtility.SetDirty(flight);
            }

            EditorUtility.SetDirty(smoothCanopy);
            EditorUtility.SetDirty(smoothGlass);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 77 made the canopy changes but Unity could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 Step 77 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = master != null ? master.gameObject : aircraft[0].gameObject;
            Debug.Log(
                $"P-51 Step 77 complete. Rebuilt a shared-vertex {smoothCanopy.vertexCount}-vertex smooth canopy, "
                + $"updated {aircraftUpdated} aircraft and disabled {oldAssembliesDisabled} old faceted canopy assembly instance(s). "
                + "The new canopy uses a continuous bubble-to-windshield profile, lower-reflection glass, overlapping side seals, "
                + "front/rear closure strips and fitted frame rails so the cockpit opening no longer relies on exact edge-to-edge contact.",
                master != null ? master.gameObject : null);
        }

        [MenuItem("Hanger 51/P-51 Mustang/78 - Validate Smooth Canopy Shape and Seals")]
        public static void ValidateSmoothCanopyShapeAndSeals()
        {
            bool passed = true;
            int aircraftChecked = 0;

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(SmoothCanopyMeshPath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(SmoothGlassMaterialPath);
            if (mesh == null || material == null)
            {
                Debug.LogError("P-51 Step 78 failed. Smooth canopy mesh/material assets are missing.");
                return;
            }

            if (mesh.vertexCount < 1800 || mesh.triangles.Length / 3 < 3500)
            {
                Debug.LogError(
                    $"P-51 Step 78 failed. Smooth canopy mesh is too coarse. Vertices={mesh.vertexCount}, triangles={mesh.triangles.Length / 3}.",
                    mesh);
                passed = false;
            }

            Bounds bounds = mesh.bounds;
            if (bounds.size.x < 1.05f || bounds.size.y < 0.62f || bounds.size.z < 2.30f)
            {
                Debug.LogError(
                    $"P-51 Step 78 failed. Smooth canopy does not span the required opening. Bounds size={bounds.size}.",
                    mesh);
                passed = false;
            }

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft == null || aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 78 failed. No P-51 aircraft were found.");
                return;
            }

            for (int i = 0; i < aircraft.Length; i++)
            {
                P51FlightController flight = aircraft[i];
                if (flight == null || !flight.gameObject.scene.IsValid())
                {
                    continue;
                }

                aircraftChecked++;
                Transform assembly = FindDescendant(flight.transform, NewCanopyAssemblyName);
                Transform glass = FindDescendant(assembly, GlassObjectName);
                Transform seals = FindDescendant(assembly, SealRootName);
                Transform leftSeal = FindDescendant(seals, "P-51 Left Canopy Seal");
                Transform rightSeal = FindDescendant(seals, "P-51 Right Canopy Seal");
                Transform frontSeal = FindDescendant(seals, "P-51 Front Windshield Base Seal");
                Transform rearSeal = FindDescendant(seals, "P-51 Rear Canopy Skirt Seal");
                Transform leftRail = FindDescendant(assembly, "P-51 Left Canopy Rail");
                Transform rightRail = FindDescendant(assembly, "P-51 Right Canopy Rail");
                Transform windshieldBow = FindDescendant(assembly, "P-51 Windshield Transition Bow");

                if (assembly == null || glass == null || seals == null
                    || leftSeal == null || rightSeal == null || frontSeal == null || rearSeal == null
                    || leftRail == null || rightRail == null || windshieldBow == null)
                {
                    Debug.LogError($"P-51 Step 78 failed. '{flight.name}' is missing smooth canopy/seal/frame geometry.", flight);
                    passed = false;
                    continue;
                }

                if (!assembly.gameObject.activeSelf || !glass.gameObject.activeSelf)
                {
                    Debug.LogError($"P-51 Step 78 failed. '{flight.name}' smooth canopy assembly is disabled locally.", flight);
                    passed = false;
                }

                MeshFilter filter = glass.GetComponent<MeshFilter>();
                MeshRenderer renderer = glass.GetComponent<MeshRenderer>();
                if (filter == null || filter.sharedMesh != mesh || renderer == null || renderer.sharedMaterial != material)
                {
                    Debug.LogError($"P-51 Step 78 failed. '{flight.name}' is not using the shared smooth canopy mesh/material.", glass);
                    passed = false;
                }

                Collider[] canopyColliders = assembly.GetComponentsInChildren<Collider>(true);
                if (canopyColliders.Length != 0)
                {
                    Debug.LogError(
                        $"P-51 Step 78 failed. '{flight.name}' canopy is visual-only and must not contain colliders; found {canopyColliders.Length}.",
                        assembly);
                    passed = false;
                }

                Transform oldAssembly = FindDescendant(flight.transform, OldCanopyAssemblyName);
                if (oldAssembly != null && oldAssembly.gameObject.activeSelf)
                {
                    Debug.LogError($"P-51 Step 78 failed. '{flight.name}' still has the old faceted canopy active.", oldAssembly);
                    passed = false;
                }
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("P-51 Step 78 failed. Standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"P-51 Step 78 passed. Aircraft checked={aircraftChecked}. Smooth canopy vertices={mesh.vertexCount}, "
                    + $"triangles={mesh.triangles.Length / 3}. Every aircraft has the smooth glass, overlapping side seals, "
                    + "front/rear closure strips, fitted rails and windshield transition bow, with the old faceted canopy disabled.");
            }
        }

        private static Mesh CreateOrUpdateSmoothCanopyMesh()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(SmoothCanopyMeshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "P-51D Smooth Sealed Canopy" };
                AssetDatabase.CreateAsset(mesh, SmoothCanopyMeshPath);
            }
            else
            {
                mesh.Clear();
                mesh.name = "P-51D Smooth Sealed Canopy";
            }

            int verticesPerRing = CrossSectionSegments + 1;
            List<Vector3> vertices = new List<Vector3>(LongitudinalRings * verticesPerRing);
            List<Vector2> uvs = new List<Vector2>(LongitudinalRings * verticesPerRing);
            List<int> triangles = new List<int>((LongitudinalRings - 1) * CrossSectionSegments * 6);

            float minZ = Profile[0].Z;
            float maxZ = Profile[Profile.Length - 1].Z;
            for (int ring = 0; ring < LongitudinalRings; ring++)
            {
                float longitudinalT = ring / (float)(LongitudinalRings - 1);
                float z = Mathf.Lerp(minZ, maxZ, longitudinalT);
                EvaluateProfile(z, out float halfWidth, out float sillY, out float crownY);
                float height = Mathf.Max(0.02f, crownY - sillY);

                for (int segment = 0; segment <= CrossSectionSegments; segment++)
                {
                    float crossT = segment / (float)CrossSectionSegments;
                    float xNormalized = Mathf.Lerp(-1f, 1f, crossT);
                    float radial = Mathf.Sqrt(Mathf.Max(0f, 1f - xNormalized * xNormalized));
                    float arch = Mathf.Pow(radial, ArchExponent);
                    float x = xNormalized * halfWidth;
                    float y = sillY + height * arch;
                    vertices.Add(new Vector3(x, y, z));
                    uvs.Add(new Vector2(crossT, longitudinalT));
                }
            }

            for (int ring = 0; ring < LongitudinalRings - 1; ring++)
            {
                int row = ring * verticesPerRing;
                int nextRow = (ring + 1) * verticesPerRing;
                for (int segment = 0; segment < CrossSectionSegments; segment++)
                {
                    int a = row + segment;
                    int b = row + segment + 1;
                    int c = nextRow + segment;
                    int d = nextRow + segment + 1;

                    // Winding points outward/upward. Shared vertices are intentionally retained
                    // so RecalculateNormals produces a continuous smooth surface instead of a
                    // separate normal for every triangle/quad.
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static void EvaluateProfile(float z, out float halfWidth, out float sillY, out float crownY)
        {
            if (z <= Profile[0].Z)
            {
                halfWidth = Profile[0].HalfWidth;
                sillY = Profile[0].SillY;
                crownY = Profile[0].CrownY;
                return;
            }

            int last = Profile.Length - 1;
            if (z >= Profile[last].Z)
            {
                halfWidth = Profile[last].HalfWidth;
                sillY = Profile[last].SillY;
                crownY = Profile[last].CrownY;
                return;
            }

            int index = 0;
            for (int i = 0; i < last; i++)
            {
                if (z >= Profile[i].Z && z <= Profile[i + 1].Z)
                {
                    index = i;
                    break;
                }
            }

            ProfileStation p0 = Profile[Mathf.Max(0, index - 1)];
            ProfileStation p1 = Profile[index];
            ProfileStation p2 = Profile[index + 1];
            ProfileStation p3 = Profile[Mathf.Min(last, index + 2)];
            float span = Mathf.Max(0.0001f, p2.Z - p1.Z);
            float t = Mathf.Clamp01((z - p1.Z) / span);

            halfWidth = HermiteValue(
                p0.HalfWidth, p1.HalfWidth, p2.HalfWidth, p3.HalfWidth,
                p0.Z, p1.Z, p2.Z, p3.Z, t);
            sillY = HermiteValue(
                p0.SillY, p1.SillY, p2.SillY, p3.SillY,
                p0.Z, p1.Z, p2.Z, p3.Z, t);
            crownY = HermiteValue(
                p0.CrownY, p1.CrownY, p2.CrownY, p3.CrownY,
                p0.Z, p1.Z, p2.Z, p3.Z, t);

            halfWidth = Mathf.Clamp(halfWidth, 0.44f, 0.64f);
            sillY = Mathf.Clamp(sillY, 1.67f, 1.76f);
            crownY = Mathf.Max(crownY, sillY + 0.035f);
        }

        private static float HermiteValue(
            float v0, float v1, float v2, float v3,
            float z0, float z1, float z2, float z3,
            float t)
        {
            float segmentLength = Mathf.Max(0.0001f, z2 - z1);
            float m1Denominator = Mathf.Max(0.0001f, z2 - z0);
            float m2Denominator = Mathf.Max(0.0001f, z3 - z1);
            float m1 = (v2 - v0) / m1Denominator * segmentLength;
            float m2 = (v3 - v1) / m2Denominator * segmentLength;

            float t2 = t * t;
            float t3 = t2 * t;
            float h00 = 2f * t3 - 3f * t2 + 1f;
            float h10 = t3 - 2f * t2 + t;
            float h01 = -2f * t3 + 3f * t2;
            float h11 = t3 - t2;
            return h00 * v1 + h10 * m1 + h01 * v2 + h11 * m2;
        }

        private static Material CreateOrUpdateSmoothGlassMaterial(Material source)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(SmoothGlassMaterialPath);
            if (material == null)
            {
                material = new Material(source) { name = "P-51 Smooth Canopy Glass" };
                AssetDatabase.CreateAsset(material, SmoothGlassMaterialPath);
            }
            else
            {
                material.CopyPropertiesFromMaterial(source);
                material.name = "P-51 Smooth Canopy Glass";
            }

            SetAlpha(material, "_BaseColor", 0.12f);
            SetAlpha(material, "_Color", 0.12f);
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.32f);
            }
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0.32f);
            }
            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            material.doubleSidedGI = true;
            if (source.renderQueue >= 2500)
            {
                material.renderQueue = source.renderQueue;
            }
            else
            {
                material.renderQueue = 3000;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetAlpha(Material material, string property, float alpha)
        {
            if (material == null || !material.HasProperty(property))
            {
                return;
            }

            Color color = material.GetColor(property);
            color.a = alpha;
            material.SetColor(property, color);
        }

        private static void BuildSmoothCanopyAssembly(
            Transform aircraft,
            Mesh mesh,
            Material glassMaterial,
            Material metal,
            Material dark)
        {
            Transform assembly = FindDirectChild(aircraft, NewCanopyAssemblyName);
            if (assembly == null)
            {
                GameObject assemblyObject = new GameObject(NewCanopyAssemblyName);
                Undo.RegisterCreatedObjectUndo(assemblyObject, "Create smooth P-51 canopy assembly");
                assembly = assemblyObject.transform;
                assembly.SetParent(aircraft, false);
            }
            else
            {
                Undo.RecordObject(assembly.gameObject, "Enable smooth P-51 canopy assembly");
                assembly.gameObject.SetActive(true);
                ClearChildren(assembly);
            }

            assembly.localPosition = Vector3.zero;
            assembly.localRotation = Quaternion.identity;
            assembly.localScale = Vector3.one;

            GameObject glassObject = new GameObject(GlassObjectName);
            glassObject.transform.SetParent(assembly, false);
            MeshFilter filter = glassObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = glassObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = glassMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            BuildSealAndRailGeometry(assembly, metal, dark);
            EditorUtility.SetDirty(filter);
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(assembly);
        }

        private static void BuildSealAndRailGeometry(Transform assembly, Material metal, Material dark)
        {
            GameObject sealRootObject = new GameObject(SealRootName);
            sealRootObject.transform.SetParent(assembly, false);
            Transform sealRoot = sealRootObject.transform;

            Transform leftSeal = new GameObject("P-51 Left Canopy Seal").transform;
            leftSeal.SetParent(sealRoot, false);
            Transform rightSeal = new GameObject("P-51 Right Canopy Seal").transform;
            rightSeal.SetParent(sealRoot, false);
            Transform leftRail = new GameObject("P-51 Left Canopy Rail").transform;
            leftRail.SetParent(assembly, false);
            Transform rightRail = new GameObject("P-51 Right Canopy Rail").transform;
            rightRail.SetParent(assembly, false);

            const int sideSamples = 24;
            Vector3 previousLeft = Vector3.zero;
            Vector3 previousRight = Vector3.zero;
            bool havePrevious = false;
            for (int i = 0; i < sideSamples; i++)
            {
                float t = i / (float)(sideSamples - 1);
                float z = Mathf.Lerp(Profile[0].Z, Profile[Profile.Length - 1].Z, t);
                EvaluateProfile(z, out float halfWidth, out float sillY, out _);
                Vector3 left = new Vector3(-halfWidth + 0.006f, sillY - 0.010f, z);
                Vector3 right = new Vector3(halfWidth - 0.006f, sillY - 0.010f, z);

                if (havePrevious)
                {
                    CreateBeamBetween(
                        leftSeal, $"Left Seal Segment {i:00}", previousLeft, left,
                        0.085f, 0.035f, dark);
                    CreateBeamBetween(
                        rightSeal, $"Right Seal Segment {i:00}", previousRight, right,
                        0.085f, 0.035f, dark);
                    CreateBeamBetween(
                        leftRail, $"Left Rail Segment {i:00}", previousLeft + Vector3.up * 0.025f, left + Vector3.up * 0.025f,
                        0.038f, 0.040f, metal);
                    CreateBeamBetween(
                        rightRail, $"Right Rail Segment {i:00}", previousRight + Vector3.up * 0.025f, right + Vector3.up * 0.025f,
                        0.038f, 0.040f, metal);
                }

                previousLeft = left;
                previousRight = right;
                havePrevious = true;
            }

            EvaluateProfile(Profile[0].Z, out float rearHalfWidth, out float rearSill, out float rearCrown);
            EvaluateProfile(Profile[Profile.Length - 1].Z, out float frontHalfWidth, out float frontSill, out _);

            Transform rearSeal = new GameObject("P-51 Rear Canopy Skirt Seal").transform;
            rearSeal.SetParent(sealRoot, false);
            CreateBeamBetween(
                rearSeal, "Rear Closure Strip",
                new Vector3(-rearHalfWidth - 0.018f, rearSill - 0.012f, Profile[0].Z - 0.015f),
                new Vector3(rearHalfWidth + 0.018f, rearSill - 0.012f, Profile[0].Z - 0.015f),
                0.105f, 0.040f, dark);

            Transform frontSeal = new GameObject("P-51 Front Windshield Base Seal").transform;
            frontSeal.SetParent(sealRoot, false);
            CreateBeamBetween(
                frontSeal, "Front Closure Strip",
                new Vector3(-frontHalfWidth - 0.018f, frontSill - 0.010f, Profile[Profile.Length - 1].Z + 0.012f),
                new Vector3(frontHalfWidth + 0.018f, frontSill - 0.010f, Profile[Profile.Length - 1].Z + 0.012f),
                0.095f, 0.038f, dark);

            BuildArchFrame(
                assembly,
                "P-51 Rear Canopy Bow",
                Profile[0].Z + 0.035f,
                rearHalfWidth,
                rearSill,
                rearCrown,
                18,
                0.032f,
                dark);

            EvaluateProfile(0.76f, out float bowHalfWidth, out float bowSill, out float bowCrown);
            BuildArchFrame(
                assembly,
                "P-51 Windshield Transition Bow",
                0.76f,
                bowHalfWidth,
                bowSill,
                bowCrown,
                20,
                0.027f,
                dark);

            // A low rear skirt overlaps the opening rim and closes the visually obvious slot at
            // the bubble's aft end without adding collision or hiding cockpit equipment.
            CreateVisualPrimitive(
                assembly, PrimitiveType.Cube, "P-51 Rear Canopy Lower Skirt",
                new Vector3(0f, rearSill - 0.035f, Profile[0].Z - 0.035f),
                new Vector3(rearHalfWidth * 2f + 0.12f, 0.055f, 0.115f),
                Vector3.zero, metal);

            CreateVisualPrimitive(
                assembly, PrimitiveType.Cube, "P-51 Windshield Lower Fairing",
                new Vector3(0f, frontSill - 0.030f, Profile[Profile.Length - 1].Z + 0.020f),
                new Vector3(frontHalfWidth * 2f + 0.10f, 0.050f, 0.105f),
                Vector3.zero, metal);
        }

        private static void BuildArchFrame(
            Transform parent,
            string rootName,
            float z,
            float halfWidth,
            float sillY,
            float crownY,
            int segments,
            float thickness,
            Material material)
        {
            Transform root = new GameObject(rootName).transform;
            root.SetParent(parent, false);
            Vector3 previous = Vector3.zero;
            bool havePrevious = false;
            float height = crownY - sillY;

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float xNormalized = Mathf.Lerp(-1f, 1f, t);
                float radial = Mathf.Sqrt(Mathf.Max(0f, 1f - xNormalized * xNormalized));
                float arch = Mathf.Pow(radial, ArchExponent);
                Vector3 point = new Vector3(xNormalized * halfWidth, sillY + height * arch + 0.010f, z);
                if (havePrevious)
                {
                    CreateBeamBetween(root, $"Frame Segment {i:00}", previous, point, thickness, thickness, material);
                }
                previous = point;
                havePrevious = true;
            }
        }

        private static Transform CreateBeamBetween(
            Transform parent,
            string name,
            Vector3 start,
            Vector3 end,
            float width,
            float height,
            Material material)
        {
            Vector3 delta = end - start;
            float length = delta.magnitude;
            if (length < 0.0001f)
            {
                return null;
            }

            Transform beam = CreateVisualPrimitive(
                parent, PrimitiveType.Cube, name,
                (start + end) * 0.5f,
                new Vector3(width, height, length),
                Vector3.zero,
                material);
            beam.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            EditorUtility.SetDirty(beam);
            return beam;
        }

        private static Transform CreateVisualPrimitive(
            Transform parent,
            PrimitiveType primitiveType,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEuler,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(localEuler);
            part.transform.localScale = localScale;

            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            return part.transform;
        }

        private static void DisableStrayCorrectedCanopyGlass(Transform aircraft)
        {
            if (aircraft == null)
            {
                return;
            }

            Transform[] all = aircraft.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform current = all[i];
                if (current == null || current.name != "P-51 Corrected Canopy Glass")
                {
                    continue;
                }

                if (current.gameObject.activeSelf)
                {
                    Undo.RecordObject(current.gameObject, "Disable stray corrected canopy glass");
                    current.gameObject.SetActive(false);
                    EditorUtility.SetDirty(current.gameObject);
                }
            }
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child != null)
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child != null && child.name == name)
                {
                    return child;
                }
            }
            return null;
        }

        private static Transform FindDescendant(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            Transform[] all = parent.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name)
                {
                    return all[i];
                }
            }
            return null;
        }
    }
}
