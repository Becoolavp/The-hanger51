using System;
using System.Collections.Generic;
using Hanger51.Aircraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51AftBayFastenerRackAndTailwheelPolish
    {
        private const string RackOffsetName = "Aft Equipment Internal Rack Offset";
        private const string UpperTailwheelStrutName = "Tailwheel Upper Oleo Housing";
        private const string UpperTailwheelMountName = "Tailwheel Upper Oleo Mount";
        private const string ServiceMaterialPath = "Assets/_Project/Aircraft/P51/Materials/ServiceHardware.mat";
        private const string DarkMaterialPath = "Assets/_Project/Aircraft/P51/Materials/DarkAircraftMetal.mat";
        private static readonly Vector3 RackOffset = new Vector3(0.12f, 0.18f, 0f);

        [MenuItem("Hanger 51/P-51 Mustang/Current/92 - Fit Aft Rack, Add Panel Fasteners and Restore Tailwheel Top")]
        public static void ApplyPolish()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 Step 92 requires Edit mode with Unity finished compiling.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("P-51 Step 92 requires the saved gameplay scene to be open.");
                return;
            }

            Material service = AssetDatabase.LoadAssetAtPath<Material>(ServiceMaterialPath);
            Material dark = AssetDatabase.LoadAssetAtPath<Material>(DarkMaterialPath);
            if (service == null || dark == null)
            {
                Debug.LogError("P-51 Step 92 is missing the existing service/dark aircraft materials.");
                return;
            }

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft == null || aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 92 could not find any P-51 aircraft in the scene.");
                return;
            }

            int repaired = 0;
            for (int i = 0; i < aircraft.Length; i++)
            {
                P51FlightController flight = aircraft[i];
                if (flight == null || !flight.gameObject.scene.IsValid())
                {
                    continue;
                }

                P51AftEquipmentBay bay = flight.GetComponentInChildren<P51AftEquipmentBay>(true);
                if (bay == null || bay.AccessPanel == null)
                {
                    Debug.LogError($"P-51 Step 92: '{flight.name}' has no configured aft equipment bay/panel.", flight);
                    continue;
                }

                FitRackInsideFuselage(bay);
                BuildPanelFasteners(bay.AccessPanel, service, dark);
                MakePanelPlayerSafe(bay.AccessPanel);
                RestoreTailwheelUpperConnection(flight, service, dark);
                repaired++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 92 applied the repairs but Unity could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 Step 92 completed the geometry/service changes, but build preparation failed.");
                return;
            }

            Debug.Log(
                $"P-51 Step 92 complete on {repaired} aircraft. The aft rack was moved inward/upward, "
                + "eight captive quarter-turn panel fasteners were added, the panel was converted to non-launching trigger-only handling, "
                + "and the upper tailwheel oleo connection was restored.");
        }

        [MenuItem("Hanger 51/P-51 Mustang/Current/93 - Validate Aft Fasteners, Rack Fit and Tailwheel Top")]
        public static void ValidatePolish()
        {
            bool passed = true;
            int checkedAircraft = 0;
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

                P51AftEquipmentBay bay = flight.GetComponentInChildren<P51AftEquipmentBay>(true);
                P51AftAccessPanel panel = bay != null ? bay.AccessPanel : null;
                if (bay == null || panel == null)
                {
                    Debug.LogError($"P-51 Step 93 failed: '{flight.name}' has no aft bay/panel.", flight);
                    passed = false;
                    continue;
                }

                Transform rackOffset = FindDescendant(bay.transform, RackOffsetName);
                if (rackOffset == null
                    || rackOffset.localPosition.x < RackOffset.x - 0.01f
                    || rackOffset.localPosition.y < RackOffset.y - 0.01f)
                {
                    Debug.LogError($"P-51 Step 93 failed: '{flight.name}' internal aft rack has not been pulled inward/upward.", flight);
                    passed = false;
                }

                P51AftPanelFastener[] fasteners = panel.GetComponentsInChildren<P51AftPanelFastener>(true);
                if (fasteners.Length != 8)
                {
                    Debug.LogError($"P-51 Step 93 failed: '{flight.name}' aft panel should have 8 service fasteners; found {fasteners.Length}.", panel);
                    passed = false;
                }

                Rigidbody body = panel.GetComponent<Rigidbody>();
                if (body == null || !body.isKinematic || body.useGravity)
                {
                    Debug.LogError($"P-51 Step 93 failed: '{flight.name}' aft panel is not configured as a safe kinematic service part.", panel);
                    passed = false;
                }

                Collider[] colliders = panel.GetComponentsInChildren<Collider>(true);
                for (int c = 0; c < colliders.Length; c++)
                {
                    if (colliders[c] != null && !colliders[c].isTrigger)
                    {
                        Debug.LogError($"P-51 Step 93 failed: '{flight.name}' aft panel still has a physical collision collider that can shove the player.", colliders[c]);
                        passed = false;
                        break;
                    }
                }

                if (FindDescendant(flight.transform, UpperTailwheelStrutName) == null
                    || FindDescendant(flight.transform, UpperTailwheelMountName) == null)
                {
                    Debug.LogError($"P-51 Step 93 failed: '{flight.name}' is missing the restored upper tailwheel strut/mount visual.", flight);
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
                    $"P-51 Step 93 passed. Aircraft checked={checkedAircraft}. Rack fit, 8 panel fasteners, non-launching panel handling, "
                    + "and the upper tailwheel oleo connection are all configured.");
            }
        }

        private static void FitRackInsideFuselage(P51AftEquipmentBay bay)
        {
            Transform root = bay.transform;
            Transform rackOffset = FindDirectChild(root, RackOffsetName);
            if (rackOffset == null)
            {
                GameObject rackObject = new GameObject(RackOffsetName);
                Undo.RegisterCreatedObjectUndo(rackObject, "Create P-51 aft rack internal offset");
                rackOffset = rackObject.transform;
                rackOffset.SetParent(root, false);
                rackOffset.localPosition = Vector3.zero;
                rackOffset.localRotation = Quaternion.identity;
                rackOffset.localScale = Vector3.one;

                string[] structuralNames =
                {
                    "Aft Bay Inner Backplate",
                    "Aft Bay Lower Shelf",
                    "Aft Bay Upper Rail"
                };
                for (int i = 0; i < structuralNames.Length; i++)
                {
                    Transform part = FindDescendant(root, structuralNames[i]);
                    if (part != null && part.parent != rackOffset)
                    {
                        Undo.SetTransformParent(part, rackOffset, "Move aft rack structure inside fuselage");
                    }
                }

                P51AftEquipmentSlot[] slots = bay.GetComponentsInChildren<P51AftEquipmentSlot>(true);
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] != null && slots[i].transform.parent != rackOffset)
                    {
                        Undo.SetTransformParent(slots[i].transform, rackOffset, "Move aft equipment slot inside fuselage");
                    }
                }
            }

            Undo.RecordObject(rackOffset, "Fit P-51 aft rack inside fuselage");
            rackOffset.localPosition = RackOffset;
            rackOffset.localRotation = Quaternion.identity;
            rackOffset.localScale = Vector3.one;
            EditorUtility.SetDirty(rackOffset);
        }

        private static void BuildPanelFasteners(P51AftAccessPanel panel, Material service, Material dark)
        {
            if (panel == null)
            {
                return;
            }

            List<Transform> old = new List<Transform>();
            for (int i = 0; i < panel.transform.childCount; i++)
            {
                Transform child = panel.transform.GetChild(i);
                if (child != null && child.name.StartsWith("Aft Panel Fastener ", StringComparison.Ordinal))
                {
                    old.Add(child);
                }
            }
            for (int i = 0; i < old.Count; i++)
            {
                Undo.DestroyObjectImmediate(old[i].gameObject);
            }

            MeshFilter filter = panel.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null || mesh.vertexCount == 0)
            {
                Debug.LogError("P-51 Step 92 could not add panel fasteners because the curved aft panel has no mesh.", panel);
                return;
            }

            Bounds bounds = mesh.bounds;
            float topY = bounds.max.y - Mathf.Min(0.07f, bounds.size.y * 0.10f);
            float bottomY = bounds.min.y + Mathf.Min(0.07f, bounds.size.y * 0.10f);
            float frontZ = bounds.max.z - Mathf.Min(0.08f, bounds.size.z * 0.08f);
            float rearZ = bounds.min.z + Mathf.Min(0.08f, bounds.size.z * 0.08f);

            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            int index = 0;
            for (int row = 0; row < 2; row++)
            {
                float targetY = row == 0 ? topY : bottomY;
                for (int column = 0; column < 4; column++)
                {
                    float t = column / 3f;
                    float targetZ = Mathf.Lerp(rearZ, frontZ, t);
                    FindOuterSurfacePoint(vertices, normals, targetY, targetZ, out Vector3 position, out Vector3 normal);
                    CreateFastener(panel, index, position + normal * 0.014f, normal, service, dark);
                    index++;
                }
            }
        }

        private static void FindOuterSurfacePoint(
            Vector3[] vertices,
            Vector3[] normals,
            float targetY,
            float targetZ,
            out Vector3 position,
            out Vector3 normal)
        {
            int best = 0;
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 n = normals != null && normals.Length == vertices.Length ? normals[i] : Vector3.left;
                float outwardPenalty = n.x < -0.12f ? 0f : 2.0f;
                float dy = vertices[i].y - targetY;
                float dz = vertices[i].z - targetZ;
                float score = dy * dy + dz * dz + outwardPenalty;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }

            position = vertices[best];
            normal = normals != null && normals.Length == vertices.Length && normals[best].sqrMagnitude > 0.0001f
                ? normals[best].normalized
                : Vector3.left;
            if (normal.x > 0f)
            {
                normal = -normal;
            }
        }

        private static void CreateFastener(
            P51AftAccessPanel panel,
            int index,
            Vector3 localPosition,
            Vector3 localNormal,
            Material service,
            Material dark)
        {
            GameObject fastenerObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Undo.RegisterCreatedObjectUndo(fastenerObject, "Create P-51 aft panel fastener");
            fastenerObject.name = $"Aft Panel Fastener {index + 1}";
            fastenerObject.transform.SetParent(panel.transform, false);
            fastenerObject.transform.localPosition = localPosition;
            fastenerObject.transform.localRotation = Quaternion.FromToRotation(Vector3.up, localNormal);
            fastenerObject.transform.localScale = new Vector3(0.027f, 0.008f, 0.027f);
            Renderer renderer = fastenerObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = service;
            }
            Collider collider = fastenerObject.GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }

            GameObject slot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(slot, "Create P-51 aft fastener slot");
            slot.name = "Quarter-Turn Slot";
            slot.transform.SetParent(fastenerObject.transform, false);
            slot.transform.localPosition = new Vector3(0f, 1.08f, 0f);
            slot.transform.localRotation = Quaternion.identity;
            slot.transform.localScale = new Vector3(1.15f, 0.12f, 0.18f);
            Renderer slotRenderer = slot.GetComponent<Renderer>();
            if (slotRenderer != null)
            {
                slotRenderer.sharedMaterial = dark;
            }
            Collider slotCollider = slot.GetComponent<Collider>();
            if (slotCollider != null)
            {
                Object.DestroyImmediate(slotCollider);
            }

            P51AftPanelFastener fastener = Undo.AddComponent<P51AftPanelFastener>(fastenerObject);
            fastener.Configure(panel, index, true);
            EditorUtility.SetDirty(fastener);
        }

        private static void MakePanelPlayerSafe(P51AftAccessPanel panel)
        {
            if (panel == null)
            {
                return;
            }

            BoxCollider[] boxes = panel.GetComponents<BoxCollider>();
            for (int i = 0; i < boxes.Length; i++)
            {
                if (boxes[i] != null)
                {
                    Undo.DestroyObjectImmediate(boxes[i]);
                }
            }

            MeshFilter filter = panel.GetComponent<MeshFilter>();
            MeshCollider meshCollider = panel.GetComponent<MeshCollider>();
            if (meshCollider == null && filter != null && filter.sharedMesh != null)
            {
                meshCollider = Undo.AddComponent<MeshCollider>(panel.gameObject);
                meshCollider.sharedMesh = filter.sharedMesh;
            }
            if (meshCollider != null)
            {
                meshCollider.convex = true;
                meshCollider.isTrigger = true;
                EditorUtility.SetDirty(meshCollider);
            }

            Rigidbody body = panel.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = Undo.AddComponent<Rigidbody>(panel.gameObject);
            }
            body.isKinematic = true;
            body.useGravity = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            EditorUtility.SetDirty(body);

            Collider[] colliders = panel.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].isTrigger = true;
                    EditorUtility.SetDirty(colliders[i]);
                }
            }
            panel.SetHeld(false);
            EditorUtility.SetDirty(panel);
        }

        private static void RestoreTailwheelUpperConnection(P51FlightController flight, Material service, Material dark)
        {
            Transform oleo = FindDescendant(flight.transform, "Tailwheel Oleo Strut");
            if (oleo == null)
            {
                Debug.LogWarning($"P-51 Step 92 could not find the existing tailwheel oleo on '{flight.name}'.", flight);
                return;
            }

            Transform oldStrut = FindDescendant(flight.transform, UpperTailwheelStrutName);
            if (oldStrut != null)
            {
                Undo.DestroyObjectImmediate(oldStrut.gameObject);
            }
            Transform oldMount = FindDescendant(flight.transform, UpperTailwheelMountName);
            if (oldMount != null)
            {
                Undo.DestroyObjectImmediate(oldMount.gameObject);
            }

            Vector3 lowerWorld = oleo.TransformPoint(Vector3.up);
            Vector3 upperWorld = lowerWorld + flight.transform.up * 0.30f + flight.transform.forward * 0.08f;
            Transform housing = CreateCylinderBetween(
                flight.transform,
                UpperTailwheelStrutName,
                lowerWorld,
                upperWorld,
                0.045f,
                service);

            GameObject mount = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(mount, "Create P-51 tailwheel upper mount");
            mount.name = UpperTailwheelMountName;
            mount.transform.SetParent(flight.transform, true);
            mount.transform.position = upperWorld;
            mount.transform.rotation = flight.transform.rotation;
            mount.transform.localScale = new Vector3(0.16f, 0.08f, 0.18f);
            Renderer mountRenderer = mount.GetComponent<Renderer>();
            if (mountRenderer != null)
            {
                mountRenderer.sharedMaterial = dark;
            }
            Collider mountCollider = mount.GetComponent<Collider>();
            if (mountCollider != null)
            {
                Object.DestroyImmediate(mountCollider);
            }

            if (housing != null)
            {
                EditorUtility.SetDirty(housing);
            }
            EditorUtility.SetDirty(mount.transform);
        }

        private static Transform CreateCylinderBetween(
            Transform parent,
            string name,
            Vector3 worldA,
            Vector3 worldB,
            float radius,
            Material material)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Undo.RegisterCreatedObjectUndo(obj, $"Create {name}");
            obj.name = name;
            obj.transform.SetParent(parent, true);
            Vector3 delta = worldB - worldA;
            float length = Mathf.Max(0.02f, delta.magnitude);
            obj.transform.position = (worldA + worldB) * 0.5f;
            obj.transform.rotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
            Vector3 parentScale = parent.lossyScale;
            float xScale = Mathf.Abs(parentScale.x) > 0.0001f ? radius / Mathf.Abs(parentScale.x) : radius;
            float yScale = Mathf.Abs(parentScale.y) > 0.0001f ? length * 0.5f / Mathf.Abs(parentScale.y) : length * 0.5f;
            float zScale = Mathf.Abs(parentScale.z) > 0.0001f ? radius / Mathf.Abs(parentScale.z) : radius;
            obj.transform.localScale = new Vector3(xScale, yScale, zScale);
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
            Collider collider = obj.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
            return obj.transform;
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

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
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
