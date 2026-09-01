using System;
using System.Collections.Generic;
using Hanger51.Aircraft;
using Hanger51.Commerce;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51AftEquipmentBayProfileRepair
    {
        private const string BayRootName = "P-51 Aft Equipment Bay";
        private const string PanelName = "P-51 Aft Equipment Access Panel";
        private const string PanelAnchorName = "Aft Access Panel Canonical Skin Anchor";
        private const string CanonicalFuselagePath = "Assets/_Project/Aircraft/P51/Meshes/P51D_Fuselage.asset";
        private const string CutMeshPath = "Assets/_Project/Aircraft/P51/Meshes/P51D_Fuselage_AftEquipmentBayCut_v3.asset";
        private const string PanelMeshPath = "Assets/_Project/Aircraft/P51/Meshes/P51D_AftEquipmentAccessPanelSkin_v3.asset";
        private const string DarkMaterialPath = "Assets/_Project/Aircraft/P51/Materials/DarkAircraftMetal.mat";

        // This is intentionally a little larger than the visible service door. The canonical
        // fuselage is relatively low-poly longitudinally, so selecting complete skin triangles
        // gives us a guaranteed watertight opening and a panel made from those exact triangles.
        private const float OpeningForwardZ = -2.46f;
        private const float OpeningRearZ = -3.82f;
        private const float OpeningBottomY = 1.06f;
        private const float OpeningTopY = 1.91f;
        private const float LeftSideThresholdX = -0.08f;
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

        [MenuItem("Hanger 51/P-51 Mustang/Current/88 - Repair Aft Bay with Canonical Curved Skin")]
        public static void RepairAftBayWithCanonicalSkin()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 Step 88 requires Edit mode with Unity finished compiling.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("P-51 Step 88 requires the saved gameplay scene to be open.");
                return;
            }

            Mesh canonical = AssetDatabase.LoadAssetAtPath<Mesh>(CanonicalFuselagePath);
            if (canonical == null)
            {
                Debug.LogError($"P-51 Step 88 could not load the canonical fuselage mesh at '{CanonicalFuselagePath}'.");
                return;
            }

            Mesh repairedCut;
            Mesh curvedPanel;
            Vector3 panelPivot;
            if (!BuildMeshesFromCanonical(canonical, out repairedCut, out curvedPanel, out panelPivot))
            {
                return;
            }

            Material fallbackDark = AssetDatabase.LoadAssetAtPath<Material>(DarkMaterialPath);
            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft == null || aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 88 could not find any P-51 aircraft in the scene.");
                return;
            }

            int repairedAircraft = 0;
            for (int i = 0; i < aircraft.Length; i++)
            {
                P51FlightController flight = aircraft[i];
                if (flight == null || !flight.gameObject.scene.IsValid())
                {
                    continue;
                }

                List<MeshFilter> fuselageFilters = FindFuselageFilters(flight);
                if (fuselageFilters.Count == 0)
                {
                    Debug.LogError($"P-51 Step 88 could not resolve the fuselage renderer on '{flight.name}'.", flight);
                    continue;
                }

                // Bind every genuine fuselage renderer. This intentionally repairs inactive/live
                // duplicate render paths as well, so an old full skin cannot hide the opening and
                // older validation helpers cannot accidentally inspect a stale full fuselage.
                for (int f = 0; f < fuselageFilters.Count; f++)
                {
                    MeshFilter filter = fuselageFilters[f];
                    Undo.RecordObject(filter, "Assign P-51 canonical aft-bay cut fuselage");
                    filter.sharedMesh = repairedCut;
                    EditorUtility.SetDirty(filter);
                }

                MeshFilter primary = fuselageFilters[0];
                P51AftEquipmentBay bay = flight.GetComponentInChildren<P51AftEquipmentBay>(true);
                if (bay == null)
                {
                    Debug.LogError($"P-51 Step 88 found no aft equipment bay on '{flight.name}'. Run the aft-equipment installer first.", flight);
                    continue;
                }

                RebuildCurvedPanel(primary, bay, curvedPanel, panelPivot, fallbackDark);
                RemoveOldExteriorGeometry(bay.transform);
                EnsureRecessedFrame(bay.transform, fallbackDark);
                repairedAircraft++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 88 repaired the geometry but Unity could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 Step 88 completed the aft-bay repair, but build preparation failed.");
                return;
            }

            Debug.Log(
                $"P-51 Step 88 complete. Repaired {repairedAircraft} aircraft from the canonical fuselage source. "
                + "The access panel is now the exact curved skin removed from the fuselage, and stale full-fuselage renderers were rebound to the cut mesh.");
        }

        [MenuItem("Hanger 51/P-51 Mustang/Current/89 - Validate Aft Bay Curved Skin and Equipment")]
        public static void ValidateAftBayCurvedSkinAndEquipment()
        {
            bool passed = true;
            int checkedAircraft = 0;
            Mesh expectedCut = AssetDatabase.LoadAssetAtPath<Mesh>(CutMeshPath);
            Mesh expectedPanel = AssetDatabase.LoadAssetAtPath<Mesh>(PanelMeshPath);

            if (expectedCut == null || expectedPanel == null)
            {
                Debug.LogError("P-51 Step 89 failed: Step 88 mesh assets are missing.");
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
                List<MeshFilter> fuselageFilters = FindFuselageFilters(flight);
                if (fuselageFilters.Count == 0)
                {
                    Debug.LogError($"P-51 Step 89 failed: '{flight.name}' has no resolved fuselage renderer.", flight);
                    passed = false;
                }
                else
                {
                    for (int f = 0; f < fuselageFilters.Count; f++)
                    {
                        if (fuselageFilters[f].sharedMesh != expectedCut)
                        {
                            Debug.LogError($"P-51 Step 89 failed: '{flight.name}' still has a stale full-fuselage renderer '{fuselageFilters[f].name}'.", flight);
                            passed = false;
                        }
                    }
                }

                P51AftEquipmentBay bay = flight.GetComponentInChildren<P51AftEquipmentBay>(true);
                P51AftAccessPanel panel = bay != null ? bay.AccessPanel : null;
                MeshFilter panelFilter = panel != null ? panel.GetComponent<MeshFilter>() : null;
                MeshCollider panelCollider = panel != null ? panel.GetComponent<MeshCollider>() : null;
                if (bay == null || panel == null || panelFilter == null || panelFilter.sharedMesh != expectedPanel)
                {
                    Debug.LogError($"P-51 Step 89 failed: '{flight.name}' is not using the canonical curved aft access panel.", flight);
                    passed = false;
                }
                else if (expectedPanel.vertexCount < 12)
                {
                    Debug.LogError("P-51 Step 89 failed: the generated curved panel mesh is unexpectedly sparse.");
                    passed = false;
                }

                if (panelCollider == null || panelCollider.sharedMesh != expectedPanel || !panelCollider.convex)
                {
                    Debug.LogError($"P-51 Step 89 failed: '{flight.name}' curved panel is missing its convex mesh-shaped interaction collider.", flight);
                    passed = false;
                }

                if (bay != null)
                {
                    if (HasOldExteriorGeometry(bay.transform))
                    {
                        Debug.LogError($"P-51 Step 89 failed: '{flight.name}' still contains old rectangular exterior panel geometry.", flight);
                        passed = false;
                    }

                    if (bay.InstalledBattery == null || bay.InstalledBattery.EquipmentKind != P51AftEquipmentKind.Battery)
                    {
                        Debug.LogError($"P-51 Step 89 failed: '{flight.name}' has no installed aft-rack battery.", flight);
                        passed = false;
                    }

                    int oxygenCount = 0;
                    for (int slot = 1; slot <= 3; slot++)
                    {
                        P51AftEquipmentItem item = bay.GetInstalledItem(slot);
                        if (item != null && item.EquipmentKind == P51AftEquipmentKind.OxygenBottle)
                        {
                            oxygenCount++;
                        }
                    }
                    if (oxygenCount != 3)
                    {
                        Debug.LogError($"P-51 Step 89 failed: '{flight.name}' should have three installed oxygen bottles; found {oxygenCount}.", flight);
                        passed = false;
                    }
                }

                if (flight.GetComponent<P51BatteryStartInterlock>() == null)
                {
                    Debug.LogError($"P-51 Step 89 failed: '{flight.name}' is missing its battery starter interlock.", flight);
                    passed = false;
                }
            }

            HangarShopTerminal terminal = FindFirstIncludingInactive<HangarShopTerminal>();
            if (terminal == null || !CatalogContains(terminal, "p51-24v-battery") || !CatalogContains(terminal, "p51-oxygen-bottle"))
            {
                Debug.LogError("P-51 Step 89 failed: the hangar shop is missing the replacement battery and/or oxygen-bottle products.");
                passed = false;
            }

            if (FindFirstIncludingInactive<P51BatteryTester>() == null
                || FindFirstIncludingInactive<P51AftEquipmentPlayerInteractor>() == null)
            {
                Debug.LogError("P-51 Step 89 failed: the handheld battery tester or aft-equipment player interactor is missing.");
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"P-51 Step 89 passed. Aircraft checked={checkedAircraft}. Curved removable skin panel, real fuselage opening, battery, three oxygen bottles, tester, shop products and starter interlock are all configured.");
            }
        }

        private static bool BuildMeshesFromCanonical(
            Mesh canonical,
            out Mesh cutMesh,
            out Mesh panelMesh,
            out Vector3 panelPivot)
        {
            cutMesh = null;
            panelMesh = null;
            panelPivot = Vector3.zero;

            Vector3[] vertices = canonical.vertices;
            if (vertices == null || vertices.Length == 0)
            {
                Debug.LogError("P-51 Step 88 failed: the canonical fuselage mesh contains no vertices.");
                return false;
            }

            List<SelectedTriangle> selected = new List<SelectedTriangle>();
            List<int>[] keptBySubMesh = new List<int>[canonical.subMeshCount];
            HashSet<int> selectedVertexIndices = new HashSet<int>();

            for (int subMesh = 0; subMesh < canonical.subMeshCount; subMesh++)
            {
                int[] triangles = canonical.GetTriangles(subMesh);
                List<int> kept = new List<int>(triangles.Length);
                keptBySubMesh[subMesh] = kept;

                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    int ia = triangles[i];
                    int ib = triangles[i + 1];
                    int ic = triangles[i + 2];
                    Vector3 a = vertices[ia];
                    Vector3 b = vertices[ib];
                    Vector3 c = vertices[ic];

                    if (TriangleBelongsToDoor(a, b, c))
                    {
                        selected.Add(new SelectedTriangle
                        {
                            SubMesh = subMesh,
                            A = ia,
                            B = ib,
                            C = ic
                        });
                        selectedVertexIndices.Add(ia);
                        selectedVertexIndices.Add(ib);
                        selectedVertexIndices.Add(ic);
                    }
                    else
                    {
                        kept.Add(ia);
                        kept.Add(ib);
                        kept.Add(ic);
                    }
                }
            }

            if (selected.Count < 4 || selectedVertexIndices.Count < 6)
            {
                Debug.LogError(
                    $"P-51 Step 88 failed: the canonical fuselage produced only {selected.Count} aft-door triangles. "
                    + "No live aircraft geometry was changed.");
                return false;
            }

            Vector3 pivotSum = Vector3.zero;
            foreach (int index in selectedVertexIndices)
            {
                pivotSum += vertices[index];
            }
            panelPivot = pivotSum / selectedVertexIndices.Count;

            Mesh newCut = Object.Instantiate(canonical);
            newCut.name = "P-51D Fuselage Aft Equipment Bay Cut v3";
            for (int subMesh = 0; subMesh < newCut.subMeshCount; subMesh++)
            {
                newCut.SetTriangles(keptBySubMesh[subMesh], subMesh, false);
            }
            newCut.RecalculateBounds();
            cutMesh = SaveOrReplaceMesh(newCut, CutMeshPath);
            if (cutMesh == null)
            {
                return false;
            }

            Mesh newPanel = BuildPanelMesh(canonical, selected, panelPivot);
            if (newPanel == null)
            {
                return false;
            }
            newPanel.name = "P-51D Aft Equipment Access Panel Canonical Skin v3";
            panelMesh = SaveOrReplaceMesh(newPanel, PanelMeshPath);
            return panelMesh != null;
        }

        private static bool TriangleBelongsToDoor(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 center = (a + b + c) / 3f;
            float minZ = Mathf.Min(a.z, Mathf.Min(b.z, c.z));
            float maxZ = Mathf.Max(a.z, Mathf.Max(b.z, c.z));
            float minY = Mathf.Min(a.y, Mathf.Min(b.y, c.y));
            float maxY = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
            int leftVertices = (a.x < LeftSideThresholdX ? 1 : 0)
                + (b.x < LeftSideThresholdX ? 1 : 0)
                + (c.x < LeftSideThresholdX ? 1 : 0);

            bool overlapsDoorRectangle = maxZ >= OpeningRearZ
                && minZ <= OpeningForwardZ
                && maxY >= OpeningBottomY
                && minY <= OpeningTopY;

            bool centerNearDoor = center.z >= OpeningRearZ - 0.12f
                && center.z <= OpeningForwardZ + 0.12f
                && center.y >= OpeningBottomY - 0.16f
                && center.y <= OpeningTopY + 0.16f;

            // Two left-side vertices keeps top, bottom and opposite-side triangles out, while the
            // overlap test catches the coarse longitudinal triangles crossing the door boundary.
            return leftVertices >= 2 && overlapsDoorRectangle && centerNearDoor;
        }

        private static Mesh BuildPanelMesh(Mesh source, List<SelectedTriangle> selected, Vector3 pivot)
        {
            Vector3[] sourceVertices = source.vertices;
            Vector3[] sourceNormals = source.normals;
            Vector2[] sourceUv = source.uv;

            Dictionary<int, int> remap = new Dictionary<int, int>();
            List<Vector3> frontVertices = new List<Vector3>();
            List<Vector3> frontNormals = new List<Vector3>();
            List<Vector2> frontUv = new List<Vector2>();
            List<int>[] frontTriangles = new List<int>[Mathf.Max(1, source.subMeshCount)];
            Dictionary<EdgeKey, int> edgeCounts = new Dictionary<EdgeKey, int>();

            for (int i = 0; i < frontTriangles.Length; i++)
            {
                frontTriangles[i] = new List<int>();
            }

            for (int i = 0; i < selected.Count; i++)
            {
                SelectedTriangle tri = selected[i];
                int a = MapVertex(tri.A, sourceVertices, sourceNormals, sourceUv, pivot, remap, frontVertices, frontNormals, frontUv);
                int b = MapVertex(tri.B, sourceVertices, sourceNormals, sourceUv, pivot, remap, frontVertices, frontNormals, frontUv);
                int c = MapVertex(tri.C, sourceVertices, sourceNormals, sourceUv, pivot, remap, frontVertices, frontNormals, frontUv);
                int subMesh = Mathf.Clamp(tri.SubMesh, 0, frontTriangles.Length - 1);
                frontTriangles[subMesh].Add(a);
                frontTriangles[subMesh].Add(b);
                frontTriangles[subMesh].Add(c);
                CountEdge(edgeCounts, tri.A, tri.B);
                CountEdge(edgeCounts, tri.B, tri.C);
                CountEdge(edgeCounts, tri.C, tri.A);
            }

            int frontCount = frontVertices.Count;
            if (frontCount < 6)
            {
                Debug.LogError("P-51 Step 88 failed while creating the curved panel: too few unique skin vertices were selected.");
                return null;
            }

            List<Vector3> vertices = new List<Vector3>(frontCount * 2 + edgeCounts.Count * 4);
            List<Vector3> normals = new List<Vector3>(frontCount * 2 + edgeCounts.Count * 4);
            List<Vector2> uv = new List<Vector2>(frontCount * 2 + edgeCounts.Count * 4);
            vertices.AddRange(frontVertices);
            normals.AddRange(frontNormals);
            uv.AddRange(frontUv);

            for (int i = 0; i < frontCount; i++)
            {
                Vector3 normal = frontNormals[i].sqrMagnitude > 0.0001f
                    ? frontNormals[i].normalized
                    : Vector3.left;
                vertices.Add(frontVertices[i] - normal * PanelThickness);
                normals.Add(-normal);
                uv.Add(frontUv[i]);
            }

            List<int>[] allTriangles = new List<int>[frontTriangles.Length];
            for (int subMesh = 0; subMesh < allTriangles.Length; subMesh++)
            {
                allTriangles[subMesh] = new List<int>(frontTriangles[subMesh].Count * 2 + 64);
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

            foreach (KeyValuePair<EdgeKey, int> pair in edgeCounts)
            {
                if (pair.Value != 1)
                {
                    continue;
                }

                if (!remap.TryGetValue(pair.Key.A, out int a)
                    || !remap.TryGetValue(pair.Key.B, out int b))
                {
                    continue;
                }

                Vector3 fa = vertices[a];
                Vector3 fb = vertices[b];
                Vector3 ba = vertices[a + frontCount];
                Vector3 bb = vertices[b + frontCount];
                Vector3 sideNormal = Vector3.Cross(fb - fa, ba - fa).normalized;
                if (sideNormal.sqrMagnitude < 0.0001f)
                {
                    sideNormal = Vector3.forward;
                }

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
                name = "P-51D Aft Equipment Access Panel Canonical Skin v3"
            };
            if (vertices.Count > 65535)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = allTriangles.Length;
            for (int subMesh = 0; subMesh < allTriangles.Length; subMesh++)
            {
                mesh.SetTriangles(allTriangles[subMesh], subMesh, false);
            }
            mesh.RecalculateBounds();
            return mesh;
        }

        private static int MapVertex(
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
            counts.TryGetValue(key, out int count);
            counts[key] = count + 1;
        }

        private static void RebuildCurvedPanel(
            MeshFilter fuselageFilter,
            P51AftEquipmentBay bay,
            Mesh panelMesh,
            Vector3 panelPivot,
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
            Undo.RegisterCreatedObjectUndo(anchor.gameObject, "Create canonical curved aft panel anchor");
            anchor.SetParent(fuselageFilter.transform, false);
            anchor.localPosition = panelPivot;
            anchor.localRotation = Quaternion.identity;
            anchor.localScale = Vector3.one;

            GameObject panelObject = new GameObject(PanelName);
            Undo.RegisterCreatedObjectUndo(panelObject, "Create canonical curved aft access panel");
            panelObject.transform.SetParent(anchor, false);
            panelObject.transform.localPosition = Vector3.zero;
            panelObject.transform.localRotation = Quaternion.identity;
            panelObject.transform.localScale = Vector3.one;

            MeshFilter panelFilter = Undo.AddComponent<MeshFilter>(panelObject);
            panelFilter.sharedMesh = panelMesh;
            MeshRenderer panelRenderer = Undo.AddComponent<MeshRenderer>(panelObject);
            MeshRenderer fuselageRenderer = fuselageFilter.GetComponent<MeshRenderer>();
            if (fuselageRenderer != null
                && fuselageRenderer.sharedMaterials != null
                && fuselageRenderer.sharedMaterials.Length > 0)
            {
                Material[] sourceMaterials = fuselageRenderer.sharedMaterials;
                Material[] materials = new Material[Mathf.Max(1, panelMesh.subMeshCount)];
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = sourceMaterials[Mathf.Min(i, sourceMaterials.Length - 1)];
                }
                panelRenderer.sharedMaterials = materials;
            }
            else if (fallbackDark != null)
            {
                panelRenderer.sharedMaterial = fallbackDark;
            }

            MeshCollider collider = Undo.AddComponent<MeshCollider>(panelObject);
            collider.sharedMesh = panelMesh;
            collider.convex = true;

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
            EditorUtility.SetDirty(collider);
            EditorUtility.SetDirty(panel);
            EditorUtility.SetDirty(bay);
        }

        private static List<MeshFilter> FindFuselageFilters(P51FlightController flight)
        {
            List<MeshFilter> matches = new List<MeshFilter>();
            if (flight == null)
            {
                return matches;
            }

            MeshFilter[] filters = flight.GetComponentsInChildren<MeshFilter>(true);
            List<KeyValuePair<MeshFilter, int>> scored = new List<KeyValuePair<MeshFilter, int>>();
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (filter == null || mesh == null || IsGeneratedBayObject(filter.transform))
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
                if (objectName.IndexOf("Fuselage", StringComparison.OrdinalIgnoreCase) >= 0) score += 180;
                if (meshName.IndexOf("Fuselage", StringComparison.OrdinalIgnoreCase) >= 0) score += 160;
                if (b.size.z > 7.0f) score += 80;
                if (b.size.z > 8.5f) score += 35;
                if (b.size.x > 0.7f && b.size.x < 2.5f) score += 45;
                if (b.size.y > 0.5f && b.size.y < 2.6f) score += 25;
                if (filter.GetComponent<MeshRenderer>() != null) score += 10;

                if (score >= 140)
                {
                    scored.Add(new KeyValuePair<MeshFilter, int>(filter, score));
                }
            }

            scored.Sort((left, right) => right.Value.CompareTo(left.Value));
            for (int i = 0; i < scored.Count; i++)
            {
                matches.Add(scored[i].Key);
            }
            return matches;
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

        private static void RemoveOldExteriorGeometry(Transform bayRoot)
        {
            string[] names =
            {
                "Aft Opening Top Rim",
                "Aft Opening Bottom Rim",
                "Aft Opening Forward Rim",
                "Aft Opening Rear Rim",
                "Panel Forward Stiffener",
                "Panel Rear Stiffener"
            };

            for (int i = 0; i < names.Length; i++)
            {
                Transform old = FindDescendant(bayRoot, names[i]);
                if (old != null)
                {
                    Undo.DestroyObjectImmediate(old.gameObject);
                }
            }
        }

        private static void EnsureRecessedFrame(Transform bayRoot, Material dark)
        {
            if (FindDescendant(bayRoot, "Aft Bay Recessed Opening Frame") != null)
            {
                return;
            }

            GameObject frame = new GameObject("Aft Bay Recessed Opening Frame");
            Undo.RegisterCreatedObjectUndo(frame, "Create recessed aft-bay frame");
            frame.transform.SetParent(bayRoot, false);
            frame.transform.localPosition = Vector3.zero;
            frame.transform.localRotation = Quaternion.identity;

            CreateFrameBeam(frame.transform, "Recessed Top Rail",
                new Vector3(-0.28f, 1.82f, -3.14f), new Vector3(0.035f, 0.035f, 1.12f), dark);
            CreateFrameBeam(frame.transform, "Recessed Bottom Rail",
                new Vector3(-0.28f, 1.15f, -3.14f), new Vector3(0.035f, 0.035f, 1.12f), dark);
            CreateFrameBeam(frame.transform, "Recessed Forward Rail",
                new Vector3(-0.28f, 1.49f, -2.58f), new Vector3(0.035f, 0.67f, 0.035f), dark);
            CreateFrameBeam(frame.transform, "Recessed Rear Rail",
                new Vector3(-0.28f, 1.49f, -3.70f), new Vector3(0.035f, 0.67f, 0.035f), dark);
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

        private static bool HasOldExteriorGeometry(Transform bayRoot)
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

        private static bool CatalogContains(HangarShopTerminal terminal, string productId)
        {
            if (terminal == null || terminal.Catalog == null)
            {
                return false;
            }
            for (int i = 0; i < terminal.Catalog.Count; i++)
            {
                ShopCatalogEntry entry = terminal.Catalog[i];
                if (entry != null && entry.ProductId == productId)
                {
                    return true;
                }
            }
            return false;
        }

        private static T FindFirstIncludingInactive<T>() where T : Object
        {
            T[] objects = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return objects != null && objects.Length > 0 ? objects[0] : null;
        }
    }
}
