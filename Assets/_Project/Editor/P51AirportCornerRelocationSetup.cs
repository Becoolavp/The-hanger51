using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class P51AirportCornerRelocationSetup
    {
        private const string TerrainName = "Hanger 51 Editable Terrain";
        private const string AirportRootName = "Hanger 51 Airport Complex";
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string RunwayRootName = "P-51 Flight Test Runway";
        private const string TestTargetName = "P-51 Gun Test Target";
        private const string BackupPlaneName = "Plane";

        private const float AirportCollectionRadius = 1800f;
        private const float CornerMarginMeters = 300f;
        private const float CornerToleranceMeters = 12f;

        [MenuItem("Hanger 51/Environment/5 - Move Entire Airport to Southwest Terrain Corner")]
        public static void MoveEntireAirportToCorner()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Environment Step 5 failed. Exit Play mode first.");
                return;
            }
            if (EditorApplication.isCompiling)
            {
                Debug.LogError("Environment Step 5 failed. Wait for Unity to finish compiling.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("Environment Step 5 failed. Open the saved hangar scene first.");
                return;
            }

            GameObject terrainObject = GameObject.Find(TerrainName);
            Terrain terrain = terrainObject != null ? terrainObject.GetComponent<Terrain>() : null;
            GameObject aircraft = GameObject.Find(AircraftRootName);
            if (terrain == null || terrain.terrainData == null || aircraft == null)
            {
                Debug.LogError("Environment Step 5 failed. The sculptable Terrain or master P-51 is missing. Run Environment Step 3 first.");
                return;
            }

            GameObject airportRoot = GameObject.Find(AirportRootName);
            if (airportRoot == null)
            {
                airportRoot = new GameObject(AirportRootName);
                Undo.RegisterCreatedObjectUndo(airportRoot, "Create Hanger 51 airport complex root");
                airportRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            }

            int newlyGrouped = GroupAirportRoots(scene, terrainObject, airportRoot, aircraft.transform.position);
            if (!aircraft.transform.IsChildOf(airportRoot.transform))
            {
                Undo.SetTransformParent(aircraft.transform, airportRoot.transform, "Group master P-51 with airport complex");
                newlyGrouped++;
            }

            if (!TryCalculateAirportBounds(airportRoot, out Bounds airportBounds))
            {
                Debug.LogError("Environment Step 5 failed. The airport complex has no usable bounds to relocate.", airportRoot);
                return;
            }

            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;
            float targetMinX = terrainPosition.x + CornerMarginMeters;
            float targetMinZ = terrainPosition.z + CornerMarginMeters;
            Vector3 translation = new Vector3(
                targetMinX - airportBounds.min.x,
                0f,
                targetMinZ - airportBounds.min.z);

            Undo.RecordObject(airportRoot.transform, "Move entire airport to terrain corner");
            airportRoot.transform.position += translation;
            EditorUtility.SetDirty(airportRoot);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("Environment Step 5 moved the airport but Unity could not save the scene.");
                return;
            }

            Hanger51BuildTools.PrepareCurrentSceneForBuild(false);
            Selection.activeGameObject = airportRoot;
            SceneView.lastActiveSceneView?.FrameSelected();

            TryCalculateAirportBounds(airportRoot, out Bounds movedBounds);
            Debug.Log(
                $"Environment Step 5 complete. Grouped {newlyGrouped} additional airport root object(s) under '{AirportRootName}' and moved the entire airport "
                + $"{translation.magnitude:F0} m to the southwest corner of the 6 km Terrain. Airport footprint now begins about "
                + $"{movedBounds.min.x - terrainPosition.x:F0} m from the west edge and {movedBounds.min.z - terrainPosition.z:F0} m from the south edge. "
                + "Hangar, runway, aircraft, Player/start area, shop/shipping, hoists, carts, spawn console/templates, test target and nearby airport equipment keep their existing relative spacing.",
                airportRoot);
        }

        [MenuItem("Hanger 51/Environment/6 - Validate Airport Terrain Corner Location")]
        public static void ValidateAirportCornerLocation()
        {
            bool passed = true;
            GameObject terrainObject = GameObject.Find(TerrainName);
            Terrain terrain = terrainObject != null ? terrainObject.GetComponent<Terrain>() : null;
            GameObject airportRoot = GameObject.Find(AirportRootName);
            GameObject aircraft = GameObject.Find(AircraftRootName);
            GameObject runway = GameObject.Find(RunwayRootName);

            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogError("Environment Step 6 failed: sculptable Terrain is missing.");
                return;
            }
            if (airportRoot == null)
            {
                Debug.LogError("Environment Step 6 failed: Hanger 51 Airport Complex root is missing.");
                return;
            }
            if (aircraft == null || !aircraft.transform.IsChildOf(airportRoot.transform))
            {
                Debug.LogError("Environment Step 6 failed: master P-51 is not grouped under the airport complex.");
                passed = false;
            }
            if (runway == null || !runway.transform.IsChildOf(airportRoot.transform))
            {
                Debug.LogError("Environment Step 6 failed: runway is not grouped under the airport complex.");
                passed = false;
            }
            if (terrain.transform.IsChildOf(airportRoot.transform))
            {
                Debug.LogError("Environment Step 6 failed: Terrain was incorrectly parented to the airport complex.");
                passed = false;
            }

            if (!TryCalculateAirportBounds(airportRoot, out Bounds bounds))
            {
                Debug.LogError("Environment Step 6 failed: airport bounds could not be calculated.");
                return;
            }

            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;
            float westClearance = bounds.min.x - terrainPosition.x;
            float southClearance = bounds.min.z - terrainPosition.z;
            float eastClearance = terrainPosition.x + terrainSize.x - bounds.max.x;
            float northClearance = terrainPosition.z + terrainSize.z - bounds.max.z;

            if (Mathf.Abs(westClearance - CornerMarginMeters) > CornerToleranceMeters
                || Mathf.Abs(southClearance - CornerMarginMeters) > CornerToleranceMeters)
            {
                Debug.LogError(
                    $"Environment Step 6 failed: airport is not positioned at the intended southwest corner. West clearance={westClearance:F1} m, south clearance={southClearance:F1} m; expected about {CornerMarginMeters:F0} m.",
                    airportRoot);
                passed = false;
            }
            if (eastClearance < 50f || northClearance < 50f)
            {
                Debug.LogError(
                    $"Environment Step 6 failed: airport extends too close to or beyond a Terrain edge. East clearance={eastClearance:F1} m, north clearance={northClearance:F1} m.",
                    airportRoot);
                passed = false;
            }

            GameObject target = GameObject.Find(TestTargetName);
            if (target != null && !target.transform.IsChildOf(airportRoot.transform))
            {
                Debug.LogError("Environment Step 6 failed: the gun test target did not move with the airport.", target);
                passed = false;
            }

            if (airportRoot.transform.childCount < 5)
            {
                Debug.LogError($"Environment Step 6 failed: airport complex only contains {airportRoot.transform.childCount} root objects; expected the hangar/runway/support area to be grouped together.", airportRoot);
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("Environment Step 6 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"Environment Step 6 passed. Entire airport complex is grouped and contained inside the Terrain at the southwest corner: west clearance={westClearance:F0} m, south clearance={southClearance:F0} m, "
                    + $"east clearance={eastClearance:F0} m, north clearance={northClearance:F0} m, grouped scene roots={airportRoot.transform.childCount}.",
                    airportRoot);
            }
        }

        private static int GroupAirportRoots(
            Scene scene,
            GameObject terrainObject,
            GameObject airportRoot,
            Vector3 airportReferencePosition)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            List<GameObject> candidates = new List<GameObject>();
            for (int index = 0; index < roots.Length; index++)
            {
                GameObject root = roots[index];
                if (root == null
                    || root == terrainObject
                    || root == airportRoot
                    || ShouldRemainGlobal(root))
                {
                    continue;
                }

                Vector3 delta = root.transform.position - airportReferencePosition;
                delta.y = 0f;
                bool nearAirport = delta.sqrMagnitude <= AirportCollectionRadius * AirportCollectionRadius;
                bool knownAirportObject = IsKnownAirportRoot(root.name);
                if (nearAirport || knownAirportObject)
                {
                    candidates.Add(root);
                }
            }

            int grouped = 0;
            for (int index = 0; index < candidates.Count; index++)
            {
                GameObject candidate = candidates[index];
                if (candidate == null || candidate.transform.parent == airportRoot.transform)
                {
                    continue;
                }
                Undo.SetTransformParent(candidate.transform, airportRoot.transform, "Group airport scene root");
                grouped++;
            }
            return grouped;
        }

        private static bool ShouldRemainGlobal(GameObject root)
        {
            if (root == null) return true;
            if (root.name == BackupPlaneName) return true;
            if (root.GetComponent<Terrain>() != null) return true;
            if (root.GetComponent<Light>() != null) return true;

            string name = root.name;
            return name.Equals("Directional Light", StringComparison.OrdinalIgnoreCase)
                || name.Equals("EventSystem", StringComparison.OrdinalIgnoreCase)
                || name.IndexOf("Global Volume", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Post Process", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Sky", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsKnownAirportRoot(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            return name == AircraftRootName
                || name == RunwayRootName
                || name == TestTargetName
                || name.IndexOf("Hangar", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Hanger 51", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Spawn", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Shipment", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Nitrogen", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Hoist", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Merlin", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool TryCalculateAirportBounds(GameObject airportRoot, out Bounds bounds)
        {
            bounds = new Bounds();
            if (airportRoot == null)
            {
                return false;
            }

            bool initialized = false;
            Renderer[] renderers = airportRoot.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null) continue;
                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            Collider[] colliders = airportRoot.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                if (collider == null) continue;
                if (!initialized)
                {
                    bounds = collider.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            Transform[] transforms = airportRoot.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                Transform transform = transforms[index];
                if (transform == null || transform == airportRoot.transform) continue;
                if (!initialized)
                {
                    bounds = new Bounds(transform.position, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(transform.position);
                }
            }

            return initialized;
        }
    }
}
