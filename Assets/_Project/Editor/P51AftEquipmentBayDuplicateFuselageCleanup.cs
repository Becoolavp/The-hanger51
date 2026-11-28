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
    public static class P51AftEquipmentBayDuplicateFuselageCleanup
    {
        private const string BayRootName = "P-51 Aft Equipment Bay";
        private const string PanelName = "P-51 Aft Equipment Access Panel";
        private const string CutMeshPath = "Assets/_Project/Aircraft/P51/Meshes/P51D_Fuselage_AftEquipmentBayCut_v3.asset";
        private const string PanelMeshPath = "Assets/_Project/Aircraft/P51/Meshes/P51D_AftEquipmentAccessPanelSkin_v3.asset";

        [MenuItem("Hanger 51/P-51 Mustang/Current/90 - Remove Duplicate Fuselage Shell from Aft Bay")]
        public static void RemoveDuplicateFuselageShell()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 Step 90 requires Edit mode with Unity finished compiling.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("P-51 Step 90 requires the saved gameplay scene to be open.");
                return;
            }

            Mesh cutMesh = AssetDatabase.LoadAssetAtPath<Mesh>(CutMeshPath);
            if (cutMesh == null)
            {
                Debug.LogError("P-51 Step 90 could not find the Step 88 aft-bay-cut fuselage mesh. Run Step 88 first.");
                return;
            }

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft == null || aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 90 could not find any P-51 aircraft in the current scene.");
                return;
            }

            int repairedAircraft = 0;
            int removedDuplicateShells = 0;

            for (int i = 0; i < aircraft.Length; i++)
            {
                P51FlightController flight = aircraft[i];
                if (flight == null || !flight.gameObject.scene.IsValid())
                {
                    continue;
                }

                List<MeshFilter> cutUsers = FindCutMeshUsers(flight, cutMesh);
                if (cutUsers.Count == 0)
                {
                    Debug.LogError($"P-51 Step 90 could not find a renderer using the Step 88 fuselage on '{flight.name}'.", flight);
                    continue;
                }

                MeshFilter primary = ChoosePrimaryFuselage(flight, cutUsers);
                if (primary == null)
                {
                    Debug.LogError($"P-51 Step 90 could not identify the real fuselage renderer on '{flight.name}'.", flight);
                    continue;
                }

                Undo.RecordObject(primary, "Preserve primary P-51 aft-bay fuselage");
                primary.sharedMesh = cutMesh;
                EditorUtility.SetDirty(primary);

                for (int f = 0; f < cutUsers.Count; f++)
                {
                    MeshFilter filter = cutUsers[f];
                    if (filter == null || filter == primary)
                    {
                        continue;
                    }

                    Undo.RecordObject(filter, "Remove accidental duplicate P-51 fuselage shell");
                    filter.sharedMesh = null;
                    EditorUtility.SetDirty(filter);

                    MeshCollider meshCollider = filter.GetComponent<MeshCollider>();
                    if (meshCollider != null && meshCollider.sharedMesh == cutMesh)
                    {
                        Undo.RecordObject(meshCollider, "Clear accidental duplicate fuselage collider");
                        meshCollider.sharedMesh = null;
                        EditorUtility.SetDirty(meshCollider);
                    }

                    removedDuplicateShells++;
                    Debug.Log(
                        $"P-51 Step 90 removed accidental full-fuselage mesh assignment from '{GetHierarchyPath(filter.transform, flight.transform)}'.",
                        filter);
                }

                repairedAircraft++;
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 90 cleaned the duplicate fuselage shells but Unity could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 Step 90 completed the geometry cleanup, but build preparation failed.");
                return;
            }

            Debug.Log(
                $"P-51 Step 90 complete. Aircraft repaired={repairedAircraft}; accidental full-fuselage duplicate shells removed={removedDuplicateShells}. "
                + "The curved aft access panel, rack, battery, oxygen bottles and starter system were left unchanged.");
        }

        [MenuItem("Hanger 51/P-51 Mustang/Current/91 - Validate Single Fuselage and Aft Bay Panel")]
        public static void ValidateSingleFuselageAndPanel()
        {
            bool passed = true;
            int checkedAircraft = 0;
            Mesh cutMesh = AssetDatabase.LoadAssetAtPath<Mesh>(CutMeshPath);
            Mesh panelMesh = AssetDatabase.LoadAssetAtPath<Mesh>(PanelMeshPath);

            if (cutMesh == null || panelMesh == null)
            {
                Debug.LogError("P-51 Step 91 failed: Step 88 cut/panel mesh assets are missing.");
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
                List<MeshFilter> cutUsers = FindCutMeshUsers(flight, cutMesh);
                if (cutUsers.Count != 1)
                {
                    Debug.LogError(
                        $"P-51 Step 91 failed: '{flight.name}' has {cutUsers.Count} renderers using the full aft-bay fuselage mesh; exactly one is required.",
                        flight);
                    passed = false;
                }
                else
                {
                    MeshFilter primary = ChoosePrimaryFuselage(flight, cutUsers);
                    if (primary != cutUsers[0])
                    {
                        Debug.LogError($"P-51 Step 91 failed: '{flight.name}' remaining aft-cut mesh is not on the expected primary fuselage renderer.", flight);
                        passed = false;
                    }
                }

                P51AftEquipmentBay bay = flight.GetComponentInChildren<P51AftEquipmentBay>(true);
                P51AftAccessPanel panel = bay != null ? bay.AccessPanel : null;
                MeshFilter panelFilter = panel != null ? panel.GetComponent<MeshFilter>() : null;
                if (bay == null || panel == null || panelFilter == null || panelFilter.sharedMesh != panelMesh)
                {
                    Debug.LogError($"P-51 Step 91 failed: '{flight.name}' lost its curved aft access panel while cleaning duplicate fuselage shells.", flight);
                    passed = false;
                }

                if (bay != null)
                {
                    if (bay.InstalledBattery == null)
                    {
                        Debug.LogError($"P-51 Step 91 failed: '{flight.name}' has no installed aft-bay battery.", flight);
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
                        Debug.LogError($"P-51 Step 91 failed: '{flight.name}' has {oxygenCount}/3 oxygen bottles installed.", flight);
                        passed = false;
                    }
                }
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"P-51 Step 91 passed. Aircraft checked={checkedAircraft}. Each aircraft has exactly one aft-cut fuselage renderer, "
                    + "the curved removable panel remains installed, and the battery/O2 rack is intact.");
            }
        }

        private static List<MeshFilter> FindCutMeshUsers(P51FlightController flight, Mesh cutMesh)
        {
            List<MeshFilter> matches = new List<MeshFilter>();
            MeshFilter[] filters = flight.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                if (filter == null || IsAftBayGeneratedObject(filter.transform))
                {
                    continue;
                }

                Mesh mesh = filter.sharedMesh;
                if (mesh == cutMesh
                    || (mesh != null && mesh.name.IndexOf("Fuselage Aft Equipment Bay Cut v3", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    matches.Add(filter);
                }
            }
            return matches;
        }

        private static MeshFilter ChoosePrimaryFuselage(P51FlightController flight, List<MeshFilter> candidates)
        {
            MeshFilter best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                MeshFilter candidate = candidates[i];
                if (candidate == null)
                {
                    continue;
                }

                int score = ScorePrimaryFuselage(flight, candidate);
                if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }
            return best;
        }

        private static int ScorePrimaryFuselage(P51FlightController flight, MeshFilter filter)
        {
            int score = 0;
            string name = filter.name ?? string.Empty;

            if (name.Equals("P-51D Fuselage", StringComparison.OrdinalIgnoreCase)) score += 1000;
            if (name.IndexOf("Fuselage", StringComparison.OrdinalIgnoreCase) >= 0) score += 500;
            if (name.IndexOf("Skin", StringComparison.OrdinalIgnoreCase) >= 0) score += 80;

            if (ContainsAny(name, "Stripe", "Trim", "Band", "Decal", "Marking", "Fairing", "Cowling", "Canopy", "Windshield", "Wing", "Tailplane", "Rudder", "Elevator", "Aileron", "Propeller"))
            {
                score -= 1200;
            }

            Vector3 relativePosition = flight.transform.InverseTransformPoint(filter.transform.position);
            float positionError = relativePosition.magnitude;
            if (positionError < 0.02f) score += 450;
            else if (positionError < 0.08f) score += 300;
            else if (positionError < 0.25f) score += 100;
            else score -= Mathf.RoundToInt(positionError * 80f);

            Quaternion relativeRotation = Quaternion.Inverse(flight.transform.rotation) * filter.transform.rotation;
            float rotationError = Quaternion.Angle(Quaternion.identity, relativeRotation);
            if (rotationError < 1f) score += 250;
            else if (rotationError < 5f) score += 120;
            else score -= Mathf.RoundToInt(rotationError * 4f);

            Vector3 relativeScale = GetRelativeLossyScale(flight.transform, filter.transform);
            float scaleError = Mathf.Abs(relativeScale.x - 1f)
                + Mathf.Abs(relativeScale.y - 1f)
                + Mathf.Abs(relativeScale.z - 1f);
            if (scaleError < 0.03f) score += 180;
            else if (scaleError < 0.15f) score += 70;
            else score -= Mathf.RoundToInt(scaleError * 120f);

            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    string materialName = materials[i] != null ? materials[i].name : string.Empty;
                    if (materialName.IndexOf("Aluminum", StringComparison.OrdinalIgnoreCase) >= 0
                        || materialName.IndexOf("Silver", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        score += 220;
                    }
                    if (materialName.IndexOf("DarkAircraftMetal", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        score -= 120;
                    }
                }
            }

            int depth = HierarchyDepthFrom(filter.transform, flight.transform);
            if (depth == 1) score += 100;
            else if (depth == 2) score += 50;

            return score;
        }

        private static Vector3 GetRelativeLossyScale(Transform root, Transform child)
        {
            Vector3 rootScale = root.lossyScale;
            Vector3 childScale = child.lossyScale;
            return new Vector3(
                Mathf.Abs(rootScale.x) > 0.0001f ? childScale.x / rootScale.x : childScale.x,
                Mathf.Abs(rootScale.y) > 0.0001f ? childScale.y / rootScale.y : childScale.y,
                Mathf.Abs(rootScale.z) > 0.0001f ? childScale.z / rootScale.z : childScale.z);
        }

        private static int HierarchyDepthFrom(Transform child, Transform root)
        {
            int depth = 0;
            Transform current = child;
            while (current != null && current != root)
            {
                depth++;
                current = current.parent;
            }
            return current == root ? depth : 999;
        }

        private static bool IsAftBayGeneratedObject(Transform transform)
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

        private static string GetHierarchyPath(Transform transform, Transform stopAt)
        {
            if (transform == null)
            {
                return "<null>";
            }

            string path = transform.name;
            Transform current = transform.parent;
            while (current != null && current != stopAt)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
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
    }
}
