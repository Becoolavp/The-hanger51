using System.Collections.Generic;
using Hanger51.Aircraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51TrueCockpitFuselageCutoutSetup
    {
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string CockpitRootName = "P-51 Cockpit Interior";
        private const string InstrumentPanelName = "P-51 Cockpit Instrument Panel";
        private const string OpeningRimRootName = "P-51 True Cockpit Opening Rim";
        private const string FuselageMeshPath = "Assets/_Project/Aircraft/P51/Meshes/P51D_Fuselage.asset";
        private const string MetalMaterialPath = "Assets/_Project/Aircraft/P51/Materials/PolishedAluminum.mat";
        private const string InteriorMaterialPath = "Assets/_Project/Aircraft/P51/Materials/CockpitInterior.mat";
        private const string DarkMaterialPath = "Assets/_Project/Aircraft/P51/Materials/DarkAircraftMetal.mat";

        private const float CockpitRearZ = -1.20f;
        private const float CockpitFrontZ = 1.35f;
        private const float OpeningStartAngle = 28f;
        private const float OpeningEndAngle = 152f;

        private static readonly float[] SectionZ =
        {
            -4.85f, -4.48f, -3.90f, -3.10f, -2.15f, -1.20f, -0.20f, 0.75f,
            1.35f, 1.80f, 2.40f, 3.05f, 3.65f, 4.15f, 4.45f
        };

        private static readonly float[] SectionCenterY =
        {
            1.58f, 1.61f, 1.63f, 1.57f, 1.48f, 1.42f, 1.37f, 1.38f,
            1.44f, 1.49f, 1.52f, 1.53f, 1.52f, 1.51f, 1.50f
        };

        private static readonly float[] SectionRadiusX =
        {
            0.08f, 0.28f, 0.46f, 0.54f, 0.61f, 0.67f, 0.72f, 0.72f,
            0.68f, 0.66f, 0.63f, 0.59f, 0.54f, 0.46f, 0.35f
        };

        private static readonly float[] SectionRadiusY =
        {
            0.10f, 0.34f, 0.50f, 0.57f, 0.63f, 0.68f, 0.72f, 0.70f,
            0.67f, 0.64f, 0.61f, 0.57f, 0.51f, 0.43f, 0.33f
        };

        [MenuItem("Hanger 51/P-51 Mustang/73 - Cut True Hollow Cockpit into Fuselage")]
        public static void CutTrueCockpitOpening()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 Step 73 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("P-51 Step 73 failed. Open the saved Hanger 51 gameplay scene first.");
                return;
            }

            Material metal = AssetDatabase.LoadAssetAtPath<Material>(MetalMaterialPath);
            Material interior = AssetDatabase.LoadAssetAtPath<Material>(InteriorMaterialPath);
            Material dark = AssetDatabase.LoadAssetAtPath<Material>(DarkMaterialPath);
            if (metal == null || interior == null || dark == null)
            {
                Debug.LogError("P-51 Step 73 failed. Required P-51 fuselage/cockpit materials are missing.");
                return;
            }

            // Always rebuild the canonical fuselage first. That makes Step 73 safe to rerun and
            // prevents repeated triangle removal from progressively destroying the body mesh.
            Mesh fuselage = P51MustangMeshFactory.CreateOrUpdateFuselage(FuselageMeshPath);
            if (fuselage == null)
            {
                Debug.LogError("P-51 Step 73 failed. The canonical P-51 fuselage mesh could not be rebuilt.");
                return;
            }

            int removedTriangles = CutCockpitOpeningFromMesh(fuselage);
            if (removedTriangles < 24)
            {
                Debug.LogError(
                    $"P-51 Step 73 aborted because only {removedTriangles} cockpit-skin triangles were identified. The fuselage asset was not accepted as a valid cockpit cut.",
                    fuselage);
                return;
            }

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft == null || aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 73 failed. No P-51 aircraft were found in the scene.");
                return;
            }

            int aircraftUpdated = 0;
            int fuselageFiltersUpdated = 0;
            P51FlightController master = null;

            for (int aircraftIndex = 0; aircraftIndex < aircraft.Length; aircraftIndex++)
            {
                P51FlightController flight = aircraft[aircraftIndex];
                if (flight == null || !flight.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (flight.name == AircraftRootName)
                {
                    master = flight;
                }

                MeshFilter[] filters = flight.GetComponentsInChildren<MeshFilter>(true);
                for (int filterIndex = 0; filterIndex < filters.Length; filterIndex++)
                {
                    MeshFilter filter = filters[filterIndex];
                    if (filter == null || filter.sharedMesh == null)
                    {
                        continue;
                    }

                    if (filter.sharedMesh == fuselage || filter.sharedMesh.name == "P-51D Fuselage")
                    {
                        Undo.RecordObject(filter, "Use hollow-cockpit P-51 fuselage mesh");
                        filter.sharedMesh = fuselage;
                        EditorUtility.SetDirty(filter);
                        fuselageFiltersUpdated++;
                    }
                }

                Transform cockpit = FindDescendant(flight.transform, CockpitRootName);
                Transform panel = FindDescendant(cockpit, InstrumentPanelName);
                if (cockpit == null || panel == null)
                {
                    Debug.LogWarning(
                        $"P-51 Step 73 cut the shared fuselage but skipped cockpit trim on '{flight.name}' because the Step 69 cockpit hierarchy is missing.",
                        flight);
                    continue;
                }

                RefitInteriorToTrueOpening(cockpit, panel, metal, interior, dark);
                BuildOpeningRim(cockpit, metal, interior);
                aircraftUpdated++;
                EditorUtility.SetDirty(flight);
            }

            EditorUtility.SetDirty(fuselage);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 73 made the fuselage/cockpit changes but Unity could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 Step 73 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = master != null ? master.gameObject : aircraft[0].gameObject;
            Debug.Log(
                $"P-51 Step 73 complete. Removed {removedTriangles} triangles from the real upper fuselage skin to create the cockpit cavity, "
                + $"updated {fuselageFiltersUpdated} fuselage renderers and fitted cockpit rims/interiors on {aircraftUpdated} aircraft. "
                + "The cockpit is now an actual opening in the P-51 body rather than an interior hidden beneath a continuous exterior tube.",
                master != null ? master.gameObject : null);
        }

        [MenuItem("Hanger 51/P-51 Mustang/74 - Validate True Hollow Cockpit Fuselage")]
        public static void ValidateTrueCockpitOpening()
        {
            bool passed = true;
            Mesh fuselage = AssetDatabase.LoadAssetAtPath<Mesh>(FuselageMeshPath);
            if (fuselage == null)
            {
                Debug.LogError("P-51 Step 74 failed. P-51 fuselage mesh asset is missing.");
                return;
            }

            int blockingTriangles = CountBlockingCockpitTriangles(fuselage);
            if (blockingTriangles != 0)
            {
                Debug.LogError(
                    $"P-51 Step 74 failed. {blockingTriangles} upper-fuselage triangles still cross the intended cockpit opening.",
                    fuselage);
                passed = false;
            }

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            int aircraftChecked = 0;
            int fuselageFiltersChecked = 0;

            for (int aircraftIndex = 0; aircraftIndex < aircraft.Length; aircraftIndex++)
            {
                P51FlightController flight = aircraft[aircraftIndex];
                if (flight == null || !flight.gameObject.scene.IsValid())
                {
                    continue;
                }

                aircraftChecked++;
                MeshFilter[] filters = flight.GetComponentsInChildren<MeshFilter>(true);
                bool foundFuselage = false;
                for (int filterIndex = 0; filterIndex < filters.Length; filterIndex++)
                {
                    MeshFilter filter = filters[filterIndex];
                    if (filter != null && filter.sharedMesh != null && filter.sharedMesh.name == "P-51D Fuselage")
                    {
                        foundFuselage = true;
                        fuselageFiltersChecked++;
                        if (filter.sharedMesh != fuselage)
                        {
                            Debug.LogError(
                                $"P-51 Step 74 failed. '{flight.name}' is not using the current hollow-cockpit fuselage asset.",
                                filter);
                            passed = false;
                        }
                    }
                }

                if (!foundFuselage)
                {
                    Debug.LogError($"P-51 Step 74 failed. '{flight.name}' has no P-51D fuselage renderer.", flight);
                    passed = false;
                }

                Transform cockpit = FindDescendant(flight.transform, CockpitRootName);
                Transform panel = FindDescendant(cockpit, InstrumentPanelName);
                Transform rim = FindDescendant(cockpit, OpeningRimRootName);
                Transform leftWall = FindDescendant(cockpit, "Cockpit Left Sidewall");
                Transform rightWall = FindDescendant(cockpit, "Cockpit Right Sidewall");
                if (cockpit == null || panel == null || rim == null || leftWall == null || rightWall == null)
                {
                    Debug.LogError($"P-51 Step 74 failed. '{flight.name}' true cockpit trim/interior is incomplete.", flight);
                    passed = false;
                    continue;
                }

                float leftTop = leftWall.localPosition.y + Mathf.Abs(leftWall.localScale.y) * 0.5f;
                float rightTop = rightWall.localPosition.y + Mathf.Abs(rightWall.localScale.y) * 0.5f;
                if (leftTop > 1.73f || rightTop > 1.73f)
                {
                    Debug.LogError(
                        $"P-51 Step 74 failed. '{flight.name}' cockpit walls still rise above the fuselage opening. LeftTop={leftTop:F3}, RightTop={rightTop:F3}.",
                        flight);
                    passed = false;
                }

                P51FuelQuantityGauge gauge = panel.GetComponentInChildren<P51FuelQuantityGauge>(true);
                if (gauge == null || !gauge.IsConfigured || gauge.FuelSystem != flight.GetComponent<P51FuelSystem>())
                {
                    Debug.LogError(
                        $"P-51 Step 74 failed. '{flight.name}' panel fuel gauge is missing or no longer reads that aircraft's own fuel system.",
                        flight);
                    passed = false;
                }
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("P-51 Step 74 failed. Standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"P-51 Step 74 passed. Aircraft checked={aircraftChecked}, fuselage renderers checked={fuselageFiltersChecked}. "
                    + "The real fuselage has no upper skin across the cockpit cavity, the cockpit walls remain below the opening rim, "
                    + "and the live fuel instrument remains installed inside the panel.");
            }
        }

        private static int CutCockpitOpeningFromMesh(Mesh fuselage)
        {
            Vector3[] vertices = fuselage.vertices;
            int[] triangles = fuselage.triangles;
            List<int> kept = new List<int>(triangles.Length);
            int removed = 0;

            for (int index = 0; index < triangles.Length; index += 3)
            {
                if (index + 2 >= triangles.Length)
                {
                    break;
                }

                int a = triangles[index];
                int b = triangles[index + 1];
                int c = triangles[index + 2];
                Vector3 centroid = (vertices[a] + vertices[b] + vertices[c]) / 3f;

                if (IsCockpitBlockingPoint(centroid))
                {
                    removed++;
                    continue;
                }

                kept.Add(a);
                kept.Add(b);
                kept.Add(c);
            }

            Undo.RegisterCompleteObjectUndo(fuselage, "Cut true P-51 cockpit opening");
            fuselage.triangles = kept.ToArray();
            fuselage.RecalculateNormals();
            fuselage.RecalculateBounds();
            EditorUtility.SetDirty(fuselage);
            return removed;
        }

        private static int CountBlockingCockpitTriangles(Mesh fuselage)
        {
            Vector3[] vertices = fuselage.vertices;
            int[] triangles = fuselage.triangles;
            int blocking = 0;
            for (int index = 0; index + 2 < triangles.Length; index += 3)
            {
                Vector3 centroid = (
                    vertices[triangles[index]]
                    + vertices[triangles[index + 1]]
                    + vertices[triangles[index + 2]]) / 3f;
                if (IsCockpitBlockingPoint(centroid))
                {
                    blocking++;
                }
            }
            return blocking;
        }

        private static bool IsCockpitBlockingPoint(Vector3 point)
        {
            if (point.z < CockpitRearZ + 0.01f || point.z > CockpitFrontZ - 0.01f)
            {
                return false;
            }

            float centerY = SampleProfile(point.z).CenterY;
            float angle = Mathf.Atan2(point.y - centerY, point.x) * Mathf.Rad2Deg;
            if (angle < 0f)
            {
                angle += 360f;
            }

            return angle >= OpeningStartAngle && angle <= OpeningEndAngle;
        }

        private static void RefitInteriorToTrueOpening(
            Transform cockpit,
            Transform panel,
            Material metal,
            Material interior,
            Material dark)
        {
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Cockpit Left Sidewall",
                new Vector3(-0.53f, 1.49f, 0.075f),
                new Vector3(0.045f, 0.36f, 2.30f), Vector3.zero, interior);
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Cockpit Right Sidewall",
                new Vector3(0.53f, 1.49f, 0.075f),
                new Vector3(0.045f, 0.36f, 2.30f), Vector3.zero, interior);
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Cockpit Floor",
                new Vector3(0f, 1.10f, 0.075f),
                new Vector3(0.88f, 0.055f, 2.18f), Vector3.zero, interior);
            CreateOrUpdatePrimitive(
                cockpit, PrimitiveType.Cube, "Cockpit Rear Bulkhead",
                new Vector3(0f, 1.48f, -1.13f),
                new Vector3(0.98f, 0.68f, 0.055f), Vector3.zero, interior);

            // The old temporary sills were rectangular slabs tied to the camera height. Hide
            // them now; the new rim below follows the actual fuselage cut instead.
            DisableDescendant(cockpit, "Left Canopy Sill");
            DisableDescendant(cockpit, "Right Canopy Sill");

            // Step 71's temporary upper nose cover was useful while the fuselage was solid,
            // but with a true opening it becomes another obstruction between pilot and panel.
            DisableDescendant(cockpit, "Cockpit Upper Nose Cover");

            Renderer panelRenderer = panel.GetComponent<Renderer>();
            if (panelRenderer != null)
            {
                panelRenderer.sharedMaterial = dark;
                EditorUtility.SetDirty(panelRenderer);
            }

            P51FuelQuantityGauge gauge = panel.GetComponentInChildren<P51FuelQuantityGauge>(true);
            if (gauge != null)
            {
                gauge.gameObject.SetActive(true);
                gauge.RefreshGauge();
                EditorUtility.SetDirty(gauge);
            }
        }

        private static void BuildOpeningRim(Transform cockpit, Material metal, Material interior)
        {
            Transform oldRoot = FindDirectChild(cockpit, OpeningRimRootName);
            if (oldRoot != null)
            {
                Undo.DestroyObjectImmediate(oldRoot.gameObject);
            }

            GameObject rootObject = new GameObject(OpeningRimRootName);
            Undo.RegisterCreatedObjectUndo(rootObject, "Create P-51 true cockpit opening rim");
            Transform root = rootObject.transform;
            root.SetParent(cockpit, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;

            float[] railZ = { CockpitRearZ, -0.20f, 0.75f, CockpitFrontZ };
            for (int sideIndex = 0; sideIndex < 2; sideIndex++)
            {
                bool left = sideIndex == 0;
                float angle = left ? OpeningEndAngle : OpeningStartAngle;
                for (int segment = 0; segment < railZ.Length - 1; segment++)
                {
                    Vector3 start = SampleSurface(railZ[segment], angle);
                    Vector3 end = SampleSurface(railZ[segment + 1], angle);
                    start.y -= 0.015f;
                    end.y -= 0.015f;
                    CreateBeamBetween(
                        root,
                        $"{(left ? "Left" : "Right")} Cockpit Rim Segment {segment + 1}",
                        start,
                        end,
                        0.075f,
                        0.050f,
                        metal);
                }
            }

            Vector3 rearLeft = SampleSurface(CockpitRearZ, OpeningEndAngle);
            Vector3 rearRight = SampleSurface(CockpitRearZ, OpeningStartAngle);
            Vector3 frontLeft = SampleSurface(CockpitFrontZ, OpeningEndAngle);
            Vector3 frontRight = SampleSurface(CockpitFrontZ, OpeningStartAngle);
            rearLeft.y -= 0.015f;
            rearRight.y -= 0.015f;
            frontLeft.y -= 0.015f;
            frontRight.y -= 0.015f;

            CreateBeamBetween(root, "Rear Cockpit Opening Rim", rearLeft, rearRight, 0.070f, 0.050f, metal);
            CreateBeamBetween(root, "Forward Cockpit Opening Rim", frontLeft, frontRight, 0.070f, 0.050f, metal);

            // A dark inner lip prevents the cut edge from reading as paper-thin when viewed
            // through the canopy or from outside the airplane.
            CreateBeamBetween(
                root,
                "Left Cockpit Inner Lip",
                rearLeft + new Vector3(0.055f, -0.060f, 0f),
                frontLeft + new Vector3(0.055f, -0.060f, 0f),
                0.045f,
                0.080f,
                interior);
            CreateBeamBetween(
                root,
                "Right Cockpit Inner Lip",
                rearRight + new Vector3(-0.055f, -0.060f, 0f),
                frontRight + new Vector3(-0.055f, -0.060f, 0f),
                0.045f,
                0.080f,
                interior);
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
            Vector3 direction = end - start;
            float length = direction.magnitude;
            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = name;
            Undo.RegisterCreatedObjectUndo(beam, $"Create {name}");
            beam.transform.SetParent(parent, false);
            beam.transform.localPosition = (start + end) * 0.5f;
            beam.transform.localRotation = length > 0.0001f
                ? Quaternion.LookRotation(direction.normalized, Vector3.up)
                : Quaternion.identity;
            beam.transform.localScale = new Vector3(width, height, Mathf.Max(0.001f, length));
            RemoveLocalColliders(beam);
            Renderer renderer = beam.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                EditorUtility.SetDirty(renderer);
            }
            return beam.transform;
        }

        private static Transform CreateOrUpdatePrimitive(
            Transform parent,
            PrimitiveType primitiveType,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEuler,
            Material material)
        {
            Transform existing = FindDirectChild(parent, name);
            GameObject part;
            if (existing == null)
            {
                part = GameObject.CreatePrimitive(primitiveType);
                part.name = name;
                Undo.RegisterCreatedObjectUndo(part, $"Create {name}");
                part.transform.SetParent(parent, false);
            }
            else
            {
                part = existing.gameObject;
                Undo.RecordObject(part.transform, $"Refit {name}");
                part.SetActive(true);
            }

            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(localEuler);
            part.transform.localScale = localScale;
            RemoveLocalColliders(part);
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                EditorUtility.SetDirty(renderer);
            }
            EditorUtility.SetDirty(part.transform);
            return part.transform;
        }

        private static void DisableDescendant(Transform root, string name)
        {
            Transform target = FindDescendant(root, name);
            if (target == null)
            {
                return;
            }
            Undo.RecordObject(target.gameObject, $"Disable obsolete {name}");
            target.gameObject.SetActive(false);
            EditorUtility.SetDirty(target.gameObject);
        }

        private static void RemoveLocalColliders(GameObject gameObject)
        {
            Collider[] colliders = gameObject.GetComponents<Collider>();
            for (int index = colliders.Length - 1; index >= 0; index--)
            {
                if (colliders[index] != null)
                {
                    Object.DestroyImmediate(colliders[index]);
                }
            }
        }

        private static Vector3 SampleSurface(float z, float degrees)
        {
            ProfileSample sample = SampleProfile(z);
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector3(
                Mathf.Cos(radians) * sample.RadiusX,
                sample.CenterY + Mathf.Sin(radians) * sample.RadiusY,
                z);
        }

        private static ProfileSample SampleProfile(float z)
        {
            if (z <= SectionZ[0])
            {
                return new ProfileSample(SectionCenterY[0], SectionRadiusX[0], SectionRadiusY[0]);
            }

            for (int index = 0; index < SectionZ.Length - 1; index++)
            {
                if (z > SectionZ[index + 1])
                {
                    continue;
                }

                float range = SectionZ[index + 1] - SectionZ[index];
                float t = range > 0.0001f ? (z - SectionZ[index]) / range : 0f;
                return new ProfileSample(
                    Mathf.Lerp(SectionCenterY[index], SectionCenterY[index + 1], t),
                    Mathf.Lerp(SectionRadiusX[index], SectionRadiusX[index + 1], t),
                    Mathf.Lerp(SectionRadiusY[index], SectionRadiusY[index + 1], t));
            }

            int last = SectionZ.Length - 1;
            return new ProfileSample(SectionCenterY[last], SectionRadiusX[last], SectionRadiusY[last]);
        }

        private readonly struct ProfileSample
        {
            internal readonly float CenterY;
            internal readonly float RadiusX;
            internal readonly float RadiusY;

            internal ProfileSample(float centerY, float radiusX, float radiusY)
            {
                CenterY = centerY;
                RadiusX = radiusX;
                RadiusY = radiusY;
            }
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
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
            for (int index = 0; index < all.Length; index++)
            {
                if (all[index] != null && all[index].name == name)
                {
                    return all[index];
                }
            }
            return null;
        }
    }
}
