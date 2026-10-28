using System;
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
    public static class P51AftEquipmentBaySkinRepair
    {
        private const string BayRootName = "P-51 Aft Equipment Bay";
        private const string PanelName = "P-51 Aft Equipment Access Panel";
        private const string PanelAnchorName = "Aft Access Panel Skin-Matched Anchor";
        private const string CutMeshPath = "Assets/_Project/Aircraft/P51/Meshes/P51D_Fuselage_AftEquipmentBayCut_v2.asset";
        private const string PanelMeshPath = "Assets/_Project/Aircraft/P51/Meshes/P51D_AftEquipmentAccessPanelSkin.asset";
        private const string DarkMaterialPath = "Assets/_Project/Aircraft/P51/Materials/DarkAircraftMetal.mat";

        private const float OpeningForwardZ = -2.62f;
        private const float OpeningRearZ = -3.62f;
        private const float OpeningBottomY = 1.18f;
        private const float OpeningTopY = 1.82f;
        private const float OpeningSideX = -0.22f;
        private const float PanelThickness = 0.018f;

        private sealed class SelectedTriangle
        {
            public int SubMesh;
            public int A;
            public int B;
            public int C;
        }

        private struct EdgeKey : IEquatable<EdgeKey>
        {
            public int A;
            public int B;

            public EdgeKey(int first, int second)
            {
                if (first <= second)
                {
                    A = first;
                    B = second;
                }
                else
                {
                    A = second;
                    B = first;
                }
            }

            public bool Equals(EdgeKey other) => A == other.A && B == other.B;
            public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);
            public override int GetHashCode() => (A * 397) ^ B;
        }

        [MenuItem("Hanger 51/P-51 Mustang/Current/Repair Aft Bay Fuselage Cut and Curved Skin Panel")]
        public static void RepairAftBaySkin()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 aft-bay skin repair requires Edit mode with Unity finished compiling.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("Open the saved Hanger 51 gameplay scene before repairing the aft equipment bay.");
                return;
            }

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft == null || aircraft.Length == 0)
            {
                Debug.LogError("No P-51 aircraft were found in the current scene.");
                return;
            }

            P51FlightController sourceFlight = null;
            MeshFilter sourceFilter = null;
            for (int i = 0; i < aircraft.Length; i++)
            {
                MeshFilter candidate = FindActualFuselageFilter(aircraft[i]);
                if (candidate == null || candidate.sharedMesh == null)
                {
                    continue;
                }

                if (candidate.sharedMesh.name.IndexOf("Aft Equipment Bay Cut", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                sourceFlight = aircraft[i];
                sourceFilter = candidate;
                break;
            }

            if (sourceFilter == null)
            {
                // If every live aircraft was partially changed already, use the best current fuselage
                // and rebuild from it only if it still contains triangles in the intended opening.
                for (int i = 0; i < aircraft.Length && sourceFilter == null; i++)
                {
                    MeshFilter candidate = FindActualFuselageFilter(aircraft[i]);
                    if (candidate != null && candidate.sharedMesh != null)
                    {
                        sourceFlight = aircraft[i];
                        sourceFilter = candidate;
                    }
                }
            }

            if (sourceFilter == null || sourceFlight == null)
            {
                Debug.LogError("Could not resolve the real live P-51 fuselage renderer for the aft-bay repair.");
                return;
            }

            Mesh repairedCut;
            Mesh curvedPanel;
            Vector3 panelPivot;
            if (!BuildCutAndPanelMeshes(sourceFlight, sourceFilter, out repairedCut, out curvedPanel, out panelPivot))
            {
                return;
            }

            Material fallbackDark = AssetDatabase.LoadAssetAtPath<Material>(DarkMaterialPath);
            int repairedAircraft = 0;
            for (int i = 0; i < aircraft.Length; i++)
            {
                P51FlightController flight = aircraft[i];
                if (flight == null || !flight.gameObject.scene.IsValid())
                {
                    continue;
                }

                MeshFilter fuselage = FindActualFuselageFilter(flight);
                if (fuselage == null)
                {
                    Debug.LogError($"Could not find the actual fuselage renderer on '{flight.name}'.", flight);
                    continue;
                }

                Undo.RecordObject(fuselage, "Assign repaired aft-bay-cut fuselage");
                fuselage.sharedMesh = repairedCut;
                EditorUtility.SetDirty(fuselage);

                P51AftEquipmentBay bay = flight.GetComponentInChildren<P51AftEquipmentBay>(true);
                if (bay == null)
                {
                    Debug.LogError($"'{flight.name}' has no aft equipment bay to refit.", flight);
                    continue;
                }

                RebuildCurvedPanel(flight, fuselage, bay, curvedPanel, panelPivot, fallbackDark);
                RecessOpeningFrame(bay.transform, fallbackDark);
                repairedAircraft++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("Aft-bay skin geometry was repaired, but Unity could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("Aft-bay skin repair completed, but build preparation failed.");
                return;
            }

            Debug.Log(
                $"P-51 aft-bay skin repair completed on {repairedAircraft} aircraft. The live fuselage renderer now uses the true cut mesh, "
                + "and the removable panel is the curved fuselage skin extracted from the opening instead of a flat box.");
        }

        [MenuItem("Hanger 51/P-51 Mustang/Current/Validate Curved Aft Bay Skin Panel")]
        public static void ValidateCurvedAftBaySkin()
        {
            bool passed = true;
            int checkedAircraft = 0;
            Mesh expectedCut = AssetDatabase.LoadAssetAtPath<Mesh>(CutMeshPath);
            Mesh expectedPanel = AssetDatabase.LoadAssetAtPath<Mesh>(PanelMeshPath);

            if (expectedCut == null || expectedPanel == null)
            {
                Debug.LogError("The repaired aft-bay fuselage and/or curved panel mesh asset is missing.");
                return;
            }

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < aircraft.Length; i++)
            {
                P51FlightController flight = aircraft[i];
                if (flight == null || !flight.gameObject.scene.IsValid())
                {
                    continue;
                }

                checkedAircraft++;
                MeshFilter fuselage = FindActualFuselageFilter(flight);
                P51AftEquipmentBay bay = flight.GetComponentInChildren<P51AftEquipmentBay>(true);
                P51AftAccessPanel panel = bay != null ? bay.AccessPanel : null;
                MeshFilter panelFilter = panel != null ? panel.GetComponent<MeshFilter>() : null;

                if (fuselage == null || fuselage.sharedMesh != expectedCut)
                {
                    Debug.LogError($"'{flight.name}' is not bound to the repaired aft-bay-cut fuselage mesh.", flight);
                    passed = false;
                }

                if (panel == null || panelFilter == null || panelFilter.sharedMesh != expectedPanel)
                {
                    Debug.LogError($"'{flight.name}' is not using the skin-matched curved aft access panel.", flight);
                    passed = false;
                }
                else if (panelFilter.sharedMesh.vertexCount < 20)
                {
                    Debug.LogError($"'{flight.name}' curved aft panel mesh is unexpectedly sparse.", flight);
                    passed = false;
                }

                if (bay != null && HasOldExteriorBoxGeometry(bay.transform))
                {
                    Debug.LogError($"'{flight.name}' still contains the old exterior rectangular rim/stiffener geometry.", flight);
                    passed = false;
                }
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"P-51 curved aft-bay skin validation passed. Aircraft checked={checkedAircraft}. The access panel is flush curved fuselage skin and the old exterior box geometry is gone.");
            }
        }

        private static bool BuildCutAndPanelMeshes(
            P51FlightController flight,
            MeshFilter sourceFilter,
            out Mesh cutMesh,
            out Mesh panelMesh,
            out Vector3 panelPivot)
        {
            cutMesh = null;
            panelMesh = null;
            panelPivot = Vector3.zero;

            Mesh source = sourceFilter.sharedMesh;
            if (source == null)
            {
                Debug.LogError("The resolved P-51 fuselage has no mesh to cut.", sourceFilter);
                return false;
            }

            Vector3[] vertices = source.vertices;
            if (vertices == null || vertices.Length == 0)
            {
                Debug.LogError("The resolved P-51 fuselage mesh has no vertices.", sourceFilter);
                return false;
            }

            List<SelectedTriangle> selected = new List<SelectedTriangle>();
            List<int>[] keptBySubMesh = new List<int>[source.subMeshCount];
            Vector3 pivotAccumulator = Vector3.zero;
            int pivotSamples = 0;

            for (int subMesh = 0; subMesh < source.subMeshCount; subMesh++)
            {
                int[] triangles = source.GetTriangles(subMesh);
                List<int> kept = new List<int>(triangles.Length);
                keptBySubMesh[subMesh] = kept;

                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    int ia = triangles[i];
                    int ib = triangles[i + 1];
                    int ic = triangles[i + 2];
                    Vector3 localCenter = (vertices[ia] + vertices[ib] + vertices[ic]) / 3f;
                    Vector3 aircraftCenter = flight.transform.InverseTransformPoint(
                        sourceFilter.transform.TransformPoint(localCenter));

                    if (InsideOpening(aircraftCenter))
                    {
                        selected.Add(new SelectedTriangle
                        {
                            SubMesh = subMesh,
                            A = ia,
                            B = ib,
                            C = ic
                        });
                        pivotAccumulator += vertices[ia] + vertices[ib] + vertices[ic];
                        pivotSamples += 3;
                    }
                    else
                    {
                        kept.Add(ia);
                        kept.Add(ib);
                        kept.Add(ic);
                    }
                }
            }

            if (selected.Count < 4 || pivotSamples == 0)
            {
                Debug.LogError(
                    "The current live fuselage did not expose enough skin triangles in the aft access-panel region. The repair stopped without changing the aircraft.",
                    sourceFilter);
                return false;
            }

            panelPivot = pivotAccumulator / pivotSamples;

            Mesh newCut = Object.Instantiate(source);
            newCut.name = "P-51D Fuselage Aft Equipment Bay Cut v2";
            for (int subMesh = 0; subMesh < newCut.subMeshCount; subMesh++)
            {
                newCut.SetTriangles(keptBySubMesh[subMesh], subMesh, false);
            }
            newCut.RecalculateBounds();
            // Preserve the existing source normals/tangents everywhere else. Removing triangles
            // does not require recomputing the whole fuselage shading.

            cutMesh = SaveOrReplaceMesh(newCut, CutMeshPath);
            if (cutMesh == null)
            {
                return false;
            }

            Mesh newPanel = BuildPanelMeshFromSelection(source, selected, panelPivot);
            if (newPanel == null)
            {
                return false;
            }
            newPanel.name = "P-51D Aft Equipment Access Panel Skin";
            panelMesh = SaveOrReplaceMesh(newPanel, PanelMeshPath);
            return panelMesh != null;
        }

        private static Mesh BuildPanelMeshFromSelection(
            Mesh source,
            List<SelectedTriangle> selected,
            Vector3 pivot)
        {
            Vector3[] sourceVertices = source.vertices;
            Vector3[] sourceNormals = source.normals;
            Vector2[] sourceUv = source.uv;

            Dictionary<int, int> remap = new Dictionary<int, int>();
            List<Vector3> frontVertices = new List<Vector3>();
            List<Vector3> frontNormals = new List<Vector3>();
            List<Vector2> frontUv = new List<Vector2>();
            List<int>[] frontTriangles = new List<int>[source.subMeshCount];
            Dictionary<EdgeKey, int> edgeCounts = new Dictionary<EdgeKey, int>();

            for (int i = 0; i < frontTriangles.Length; i++)
            {
                frontTriangles[i] = new List<int>();
            }

            for (int i = 0; i < selected.Count; i++)
            {
                SelectedTriangle tri = selected[i];
                int a = MapPanelVertex(tri.A, sourceVertices, sourceNormals, sourceUv, pivot, remap, frontVertices, frontNormals, frontUv);
                int b = MapPanelVertex(tri.B, sourceVertices, sourceNormals, sourceUv, pivot, remap, frontVertices, frontNormals, frontUv);
                int c = MapPanelVertex(tri.C, sourceVertices, sourceNormals, sourceUv, pivot, remap, frontVertices, frontNormals, frontUv);
                frontTriangles[tri.SubMesh].Add(a);
                frontTriangles[tri.SubMesh].Add(b);
                frontTriangles[tri.SubMesh].Add(c);
                CountEdge(edgeCounts, tri.A, tri.B);
                CountEdge(edgeCounts, tri.B, tri.C);
                CountEdge(edgeCounts, tri.C, tri.A);
            }

            int frontCount = frontVertices.Count;
            List<Vector3> vertices = new List<Vector3>(frontCount * 2 + edgeCounts.Count * 4);
            List<Vector3> normals = new List<Vector3>(frontCount * 2 + edgeCounts.Count * 4);
            List<Vector2> uv = new List<Vector2>(frontCount * 2 + edgeCounts.Count * 4);
            vertices.AddRange(frontVertices);
            normals.AddRange(frontNormals);
            uv.AddRange(frontUv);

            for (int i = 0; i < frontCount; i++)
            {
                Vector3 n = frontNormals[i].sqrMagnitude > 0.0001f ? frontNormals[i].normalized : Vector3.left;
                vertices.Add(frontVertices[i] - n * PanelThickness);
                normals.Add(-n);
                uv.Add(frontUv[i]);
            }

            List<int>[] allTriangles = new List<int>[source.subMeshCount];
            for (int subMesh = 0; subMesh < allTriangles.Length; subMesh++)
            {
                allTriangles[subMesh] = new List<int>(frontTriangles[subMesh].Count * 2);
                allTriangles[subMesh].AddRange(frontTriangles[subMesh]);
                for (int i = 0; i + 2 < frontTriangles[subMesh].Count; i += 3)
                {
                    int a = frontTriangles[subMesh][i];
                    int b = frontTriangles[subMesh][i + 1];
                    int c = frontTriangles[subMesh][i + 2];
                    allTriangles[subMesh].Add(a + frontCount);
                    allTriangles[subMesh].Add(c + frontCount);
                    allTriangles[subMesh].Add(b + frontCount);
                }
            }

            // Close the thin panel perimeter so the removed skin has visible sheet-metal thickness.
            foreach (KeyValuePair<EdgeKey, int> pair in edgeCounts)
            {
                if (pair.Value != 1)
                {
                    continue;
                }

                int sourceA = pair.Key.A;
                int sourceB = pair.Key.B;
                if (!remap.TryGetValue(sourceA, out int a) || !remap.TryGetValue(sourceB, out int b))
                {
                    continue;
                }

                Vector3 fa = vertices[a];
                Vector3 fb = vertices[b];
                Vector3 ba = vertices[a + frontCount];
                Vector3 bb = vertices[b + frontCount];
                Vector3 sideNormal = Vector3.Cross(fb - fa, ba - fa).normalized;
                int start = vertices.Count;
                vertices.Add(fa);
                vertices.Add(fb);
                vertices.Add(bb);
                vertices.Add(ba);
                normals.Add(sideNormal);
                normals.Add(sideNormal);
                normals.Add(sideNormal);
                normals.Add(sideNormal);
                uv.Add(Vector2.zero);
                uv.Add(Vector2.right);
                uv.Add(Vector2.one);
                uv.Add(Vector2.up);

                allTriangles[0].Add(start);
                allTriangles[0].Add(start + 1);
                allTriangles[0].Add(start + 2);
                allTriangles[0].Add(start);
                allTriangles[0].Add(start + 2);
                allTriangles[0].Add(start + 3);
            }

            Mesh mesh = new Mesh
            {
                name = "P-51D Aft Equipment Access Panel Skin"
            };
            if (vertices.Count > 65535)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = Mathf.Max(1, source.subMeshCount);
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                mesh.SetTriangles(allTriangles[subMesh], subMesh, false);
            }
            mesh.RecalculateBounds();
            return mesh;
        }

        private static int MapPanelVertex(
            int sourceIndex,
            Vector3[] sourceVertices,
            Vector3[] sourceNormals,
            Vector2[] sourceUv,
            Vector3 pivot,
            Dictionary<int, int> remap,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uv)
        {
            if (remap.TryGetValue(sourceIndex, out int mapped))
            {
                return mapped;
            }

            mapped = vertices.Count;
            remap[sourceIndex] = mapped;
            vertices.Add(sourceVertices[sourceIndex] - pivot);
            Vector3 normal = sourceNormals != null && sourceNormals.Length == sourceVertices.Length
                ? sourceNormals[sourceIndex]
                : Vector3.left;
            normals.Add(normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.left);
            uv.Add(sourceUv != null && sourceUv.Length == sourceVertices.Length
                ? sourceUv[sourceIndex]
                : Vector2.zero);
            return mapped;
        }

        private static void CountEdge(Dictionary<EdgeKey, int> counts, int a, int b)
        {
            EdgeKey key = new EdgeKey(a, b);
            counts.TryGetValue(key, out int current);
            counts[key] = current + 1;
        }

        private static bool InsideOpening(Vector3 aircraftLocalCenter)
        {
            return aircraftLocalCenter.z >= OpeningRearZ
                && aircraftLocalCenter.z <= OpeningForwardZ
                && aircraftLocalCenter.y >= OpeningBottomY
                && aircraftLocalCenter.y <= OpeningTopY
                && aircraftLocalCenter.x <= OpeningSideX;
        }

        private static void RebuildCurvedPanel(
            P51FlightController flight,
            MeshFilter fuselageFilter,
            P51AftEquipmentBay bay,
            Mesh panelMesh,
            Vector3 sourcePanelPivot,
            Material fallbackDark)
        {
            P51AftEquipmentSlot[] slots = bay.GetComponentsInChildren<P51AftEquipmentSlot>(true);
            Array.Sort(slots, (left, right) => left.SlotIndex.CompareTo(right.SlotIndex));

            P51AftAccessPanel oldPanel = bay.AccessPanel;
            Transform oldAnchor = bay.PanelAnchor;
            if (oldPanel != null)
            {
                Undo.DestroyObjectImmediate(oldPanel.gameObject);
            }
            if (oldAnchor != null && oldAnchor.gameObject != null)
            {
                Undo.DestroyObjectImmediate(oldAnchor.gameObject);
            }

            Transform anchor = new GameObject(PanelAnchorName).transform;
            Undo.RegisterCreatedObjectUndo(anchor.gameObject, "Create skin-matched aft panel anchor");
            anchor.SetParent(fuselageFilter.transform, false);
            anchor.localPosition = sourcePanelPivot;
            anchor.localRotation = Quaternion.identity;
            anchor.localScale = Vector3.one;

            GameObject panelObject = new GameObject(PanelName);
            Undo.RegisterCreatedObjectUndo(panelObject, "Create curved aft access panel");
            panelObject.transform.SetParent(anchor, false);
            panelObject.transform.localPosition = Vector3.zero;
            panelObject.transform.localRotation = Quaternion.identity;
            panelObject.transform.localScale = Vector3.one;

            MeshFilter panelFilter = Undo.AddComponent<MeshFilter>(panelObject);
            panelFilter.sharedMesh = panelMesh;
            MeshRenderer panelRenderer = Undo.AddComponent<MeshRenderer>(panelObject);
            MeshRenderer fuselageRenderer = fuselageFilter.GetComponent<MeshRenderer>();
            if (fuselageRenderer != null && fuselageRenderer.sharedMaterials != null && fuselageRenderer.sharedMaterials.Length > 0)
            {
                panelRenderer.sharedMaterials = fuselageRenderer.sharedMaterials;
            }
            else if (fallbackDark != null)
            {
                panelRenderer.sharedMaterial = fallbackDark;
            }

            BoxCollider collider = Undo.AddComponent<BoxCollider>(panelObject);
            Bounds bounds = panelMesh.bounds;
            collider.center = bounds.center;
            collider.size = new Vector3(
                Mathf.Max(0.06f, bounds.size.x + 0.035f),
                Mathf.Max(0.12f, bounds.size.y + 0.03f),
                Mathf.Max(0.12f, bounds.size.z + 0.03f));

            Rigidbody body = Undo.AddComponent<Rigidbody>(panelObject);
            body.mass = 7.5f;
            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            P51AftAccessPanel panel = Undo.AddComponent<P51AftAccessPanel>(panelObject);
            bay.Configure(anchor, panel, slots);
            panel.Configure(bay, anchor, true);

            EditorUtility.SetDirty(panelFilter);
            EditorUtility.SetDirty(panelRenderer);
            EditorUtility.SetDirty(panel);
            EditorUtility.SetDirty(bay);
        }

        private static void RecessOpeningFrame(Transform bayRoot, Material dark)
        {
            string[] oldNames =
            {
                "Aft Opening Top Rim",
                "Aft Opening Bottom Rim",
                "Aft Opening Forward Rim",
                "Aft Opening Rear Rim"
            };
            for (int i = 0; i < oldNames.Length; i++)
            {
                Transform old = FindDescendant(bayRoot, oldNames[i]);
                if (old != null)
                {
                    Undo.DestroyObjectImmediate(old.gameObject);
                }
            }

            Transform oldFrame = FindDescendant(bayRoot, "Aft Bay Recessed Opening Frame");
            if (oldFrame != null)
            {
                Undo.DestroyObjectImmediate(oldFrame.gameObject);
            }

            GameObject frameObject = new GameObject("Aft Bay Recessed Opening Frame");
            Undo.RegisterCreatedObjectUndo(frameObject, "Create recessed aft opening frame");
            frameObject.transform.SetParent(bayRoot, false);
            frameObject.transform.localPosition = Vector3.zero;
            frameObject.transform.localRotation = Quaternion.identity;

            // These pieces sit well inside the fuselage, so only a narrow dark structural lip is
            // visible through the removed skin opening; nothing protrudes outside the body.
            CreateFrameBeam(frameObject.transform, "Recessed Top Rail",
                new Vector3(-0.28f, OpeningTopY - 0.03f, (OpeningForwardZ + OpeningRearZ) * 0.5f),
                new Vector3(0.035f, 0.035f, OpeningForwardZ - OpeningRearZ), dark);
            CreateFrameBeam(frameObject.transform, "Recessed Bottom Rail",
                new Vector3(-0.28f, OpeningBottomY + 0.03f, (OpeningForwardZ + OpeningRearZ) * 0.5f),
                new Vector3(0.035f, 0.035f, OpeningForwardZ - OpeningRearZ), dark);
            CreateFrameBeam(frameObject.transform, "Recessed Forward Rail",
                new Vector3(-0.28f, (OpeningTopY + OpeningBottomY) * 0.5f, OpeningForwardZ - 0.02f),
                new Vector3(0.035f, OpeningTopY - OpeningBottomY, 0.035f), dark);
            CreateFrameBeam(frameObject.transform, "Recessed Rear Rail",
                new Vector3(-0.28f, (OpeningTopY + OpeningBottomY) * 0.5f, OpeningRearZ + 0.02f),
                new Vector3(0.035f, OpeningTopY - OpeningBottomY, 0.035f), dark);
        }

        private static void CreateFrameBeam(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(beam, $"Create {name}");
            beam.name = name;
            beam.transform.SetParent(parent, false);
            beam.transform.localPosition = localPosition;
            beam.transform.localRotation = Quaternion.identity;
            beam.transform.localScale = localScale;
            Renderer renderer = beam.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }
            Collider collider = beam.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
        }

        private static MeshFilter FindActualFuselageFilter(P51FlightController flight)
        {
            if (flight == null)
            {
                return null;
            }

            MeshFilter[] filters = flight.GetComponentsInChildren<MeshFilter>(true);
            MeshFilter best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (filter == null || mesh == null)
                {
                    continue;
                }
                if (IsGeneratedBayObject(filter.transform))
                {
                    continue;
                }

                string objectName = filter.name ?? string.Empty;
                string meshName = mesh.name ?? string.Empty;
                if (ContainsAny(objectName, "Aileron", "Elevator", "Rudder", "Canopy", "Windshield", "Propeller")
                    || ContainsAny(meshName, "Aileron", "Elevator", "Rudder", "Canopy", "Windshield", "Propeller"))
                {
                    continue;
                }

                Bounds b = mesh.bounds;
                int score = 0;
                if (objectName.IndexOf("Fuselage", StringComparison.OrdinalIgnoreCase) >= 0) score += 160;
                if (meshName.IndexOf("Fuselage", StringComparison.OrdinalIgnoreCase) >= 0) score += 140;
                if (b.size.z > 7.0f) score += 80;
                if (b.size.z > 8.5f) score += 30;
                if (b.size.x > 0.7f && b.size.x < 2.5f) score += 45;
                if (b.size.y > 0.5f && b.size.y < 2.6f) score += 25;
                if (b.center.z < 0.5f && b.center.z > -1.5f) score += 10;
                if (filter.GetComponent<MeshRenderer>() != null) score += 10;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = filter;
                }
            }

            return bestScore >= 100 ? best : null;
        }

        private static bool IsGeneratedBayObject(Transform transform)
        {
            Transform current = transform;
            while (current != null)
            {
                if (current.name == BayRootName || current.name == PanelName)
                {
                    return true;
                }
                current = current.parent;
            }
            return false;
        }

        private static bool ContainsAny(string value, params string[] terms)
        {
            for (int i = 0; i < terms.Length; i++)
            {
                if (value.IndexOf(terms[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static Mesh SaveOrReplaceMesh(Mesh source, string path)
        {
            if (source == null)
            {
                return null;
            }

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(source, path);
                return source;
            }

            EditorUtility.CopySerialized(source, existing);
            Object.DestroyImmediate(source);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static bool HasOldExteriorBoxGeometry(Transform bayRoot)
        {
            return FindDescendant(bayRoot, "Aft Opening Top Rim") != null
                || FindDescendant(bayRoot, "Aft Opening Bottom Rim") != null
                || FindDescendant(bayRoot, "Aft Opening Forward Rim") != null
                || FindDescendant(bayRoot, "Aft Opening Rear Rim") != null
                || FindDescendant(bayRoot, "Panel Forward Stiffener") != null
                || FindDescendant(bayRoot, "Panel Rear Stiffener") != null;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name == name)
                {
                    return transforms[i];
                }
            }
            return null;
        }
    }
}
