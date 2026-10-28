using System;
using Hanger51.Aircraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class P51WingArmamentFinalFitAndMuzzleAlignment
    {
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string ArmamentRootName = "P-51 Serviceable Wing Armament";
        private const string GunDarkPath = "Assets/_Project/Aircraft/P51/Armament/Materials/WingGunDark.mat";
        private const int GunCount = 6;
        private const int HeatedPartsPerGun = 4;

        [MenuItem("Hanger 51/P-51 Mustang/38 - Tuck Wing Hardware and Align Gun Muzzles")]
        public static void TuckHardwareAndAlignMuzzles()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("P-51 Step 38 failed. Exit Play mode first.");
                return;
            }
            if (EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 Step 38 failed. Wait for Unity to finish compiling.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            GameObject aircraft = GameObject.Find(AircraftRootName);
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path) || aircraft == null)
            {
                Debug.LogError("P-51 Step 38 failed. Open the saved hangar scene containing the P-51 first.");
                return;
            }

            Transform armamentRoot = aircraft.transform.Find(ArmamentRootName);
            P51WingArmamentSystem system = aircraft.GetComponent<P51WingArmamentSystem>();
            if (armamentRoot == null || system == null)
            {
                Debug.LogError("P-51 Step 38 failed. The serviceable wing armament system is missing.", aircraft);
                return;
            }

            Material gunDark = AssetDatabase.LoadAssetAtPath<Material>(GunDarkPath);
            if (gunDark == null)
            {
                Debug.LogError("P-51 Step 38 failed. WingGunDark material is missing.");
                return;
            }

            Transform[] actualMuzzles = new Transform[GunCount];
            Renderer[] heatedParts = new Renderer[GunCount * HeatedPartsPerGun];
            int tuckedBolts = 0;
            int tuckedLatches = 0;
            int fittedHighlights = 0;

            for (int wingIndex = 0; wingIndex < 2; wingIndex++)
            {
                string wingName = wingIndex == 0 ? "Left" : "Right";
                Transform interior = FindChildRecursive(armamentRoot, $"{wingName} Wing Armament Bay Interior");
                if (interior == null)
                {
                    Debug.LogError($"P-51 Step 38 failed. {wingName} wing bay interior is missing.");
                    return;
                }

                for (int localStation = 1; localStation <= 3; localStation++)
                {
                    int stationIndex = wingIndex * 3 + localStation - 1;
                    Transform gunTarget = FindChildRecursive(interior, $"{wingName} Gun Mount {localStation}");
                    Transform ammoTarget = FindChildRecursive(interior, $"{wingName} Ammo Bay {localStation}");
                    if (gunTarget == null || ammoTarget == null)
                    {
                        Debug.LogError($"P-51 Step 38 failed. {wingName} station {localStation} is incomplete.");
                        return;
                    }

                    tuckedBolts += TuckGunHoldDownBolts(gunTarget);
                    tuckedLatches += TuckAmmoLatches(ammoTarget);
                    fittedHighlights += FitInstallHighlight(gunTarget);
                    fittedHighlights += FitInstallHighlight(ammoTarget);

                    Transform mountedGun = FindChildRecursive(gunTarget, "Installed M2 Wing Gun");
                    if (mountedGun == null)
                    {
                        Debug.LogError($"P-51 Step 38 failed. Installed gun visual missing at {wingName} station {localStation}.");
                        return;
                    }

                    Transform muzzleOpening = FindChildRecursive(mountedGun, "Muzzle Opening");
                    if (muzzleOpening == null)
                    {
                        Debug.LogError($"P-51 Step 38 failed. Visible muzzle opening missing at {wingName} station {localStation}.");
                        return;
                    }

                    Transform actualMuzzle = mountedGun.Find("Actual Gun Muzzle Anchor");
                    if (actualMuzzle == null)
                    {
                        GameObject anchorObject = new GameObject("Actual Gun Muzzle Anchor");
                        Undo.RegisterCreatedObjectUndo(anchorObject, "Create actual P-51 gun muzzle anchor");
                        actualMuzzle = anchorObject.transform;
                        actualMuzzle.SetParent(mountedGun, false);
                    }

                    Vector3 openingCenterInGun = mountedGun.InverseTransformPoint(muzzleOpening.position);
                    actualMuzzle.localPosition = openingCenterInGun + Vector3.forward * 0.026f;
                    actualMuzzle.localRotation = Quaternion.identity;
                    actualMuzzle.localScale = Vector3.one;
                    actualMuzzles[stationIndex] = actualMuzzle;

                    string[] heatedNames = { "Barrel Jacket", "Barrel", "Muzzle Collar", "Muzzle Opening" };
                    for (int partIndex = 0; partIndex < heatedNames.Length; partIndex++)
                    {
                        Transform part = FindChildRecursive(mountedGun, heatedNames[partIndex]);
                        Renderer renderer = part != null ? part.GetComponent<Renderer>() : null;
                        if (renderer == null)
                        {
                            Debug.LogError($"P-51 Step 38 failed. {heatedNames[partIndex]} missing at {wingName} station {localStation}.");
                            return;
                        }
                        renderer.sharedMaterial = gunDark;
                        EditorUtility.SetDirty(renderer);
                        heatedParts[stationIndex * HeatedPartsPerGun + partIndex] = renderer;
                    }
                }
            }

            SerializedObject serializedSystem = new SerializedObject(system);
            SerializedProperty muzzlesProperty = serializedSystem.FindProperty("muzzles");
            if (muzzlesProperty == null || !muzzlesProperty.isArray)
            {
                Debug.LogError("P-51 Step 38 failed. Armament-system muzzle array could not be found.");
                return;
            }
            muzzlesProperty.arraySize = GunCount;
            for (int stationIndex = 0; stationIndex < GunCount; stationIndex++)
            {
                muzzlesProperty.GetArrayElementAtIndex(stationIndex).objectReferenceValue = actualMuzzles[stationIndex];
            }
            serializedSystem.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(system);

            P51BulletStreakVisualController streakController = aircraft.GetComponent<P51BulletStreakVisualController>();
            if (streakController != null)
            {
                streakController.Configure(system, actualMuzzles);
                EditorUtility.SetDirty(streakController);
            }

            P51WingGunBarrelHeatController heatController = aircraft.GetComponent<P51WingGunBarrelHeatController>();
            if (heatController == null)
            {
                heatController = Undo.AddComponent<P51WingGunBarrelHeatController>(aircraft);
            }
            heatController.Configure(system, heatedParts);
            EditorUtility.SetDirty(heatController);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 38 completed its edits but Unity could not save the scene.");
                return;
            }

            Debug.Log(
                $"P-51 Step 38 complete. Tucked {tuckedBolts} gun hold-down bolts, {tuckedLatches} ammo latches, "
                + $"refitted {fittedHighlights} install-highlight frames, aligned all six firing origins to the visible gun muzzles, "
                + "and made each gun's jacket, barrel, collar, and muzzle tip share the independent heat glow.",
                aircraft);
        }

        [MenuItem("Hanger 51/P-51 Mustang/39 - Validate Wing Hardware and Muzzle Alignment")]
        public static void ValidateHardwareAndMuzzleAlignment()
        {
            bool passed = true;
            GameObject aircraft = GameObject.Find(AircraftRootName);
            if (aircraft == null)
            {
                Debug.LogError("P-51 Step 39 failed: P-51 aircraft is missing.");
                return;
            }

            Transform armamentRoot = aircraft.transform.Find(ArmamentRootName);
            P51WingArmamentSystem system = aircraft.GetComponent<P51WingArmamentSystem>();
            P51WingGunBarrelHeatController heatController = aircraft.GetComponent<P51WingGunBarrelHeatController>();
            if (armamentRoot == null || system == null || heatController == null)
            {
                Debug.LogError("P-51 Step 39 failed: armament root, armament system, or barrel heat controller is missing.");
                return;
            }

            SerializedObject serializedSystem = new SerializedObject(system);
            SerializedProperty muzzlesProperty = serializedSystem.FindProperty("muzzles");
            int alignedMuzzles = 0;
            int fittedBoltSets = 0;
            int fittedLatchSets = 0;
            int glowingTipSets = 0;

            for (int wingIndex = 0; wingIndex < 2; wingIndex++)
            {
                string wingName = wingIndex == 0 ? "Left" : "Right";
                Transform interior = FindChildRecursive(armamentRoot, $"{wingName} Wing Armament Bay Interior");
                for (int localStation = 1; localStation <= 3; localStation++)
                {
                    int stationIndex = wingIndex * 3 + localStation - 1;
                    Transform gunTarget = interior != null ? FindChildRecursive(interior, $"{wingName} Gun Mount {localStation}") : null;
                    Transform ammoTarget = interior != null ? FindChildRecursive(interior, $"{wingName} Ammo Bay {localStation}") : null;
                    Transform mountedGun = gunTarget != null ? FindChildRecursive(gunTarget, "Installed M2 Wing Gun") : null;
                    Transform actualMuzzle = mountedGun != null ? mountedGun.Find("Actual Gun Muzzle Anchor") : null;
                    Transform muzzleOpening = mountedGun != null ? FindChildRecursive(mountedGun, "Muzzle Opening") : null;

                    Transform serializedMuzzle = muzzlesProperty != null && stationIndex < muzzlesProperty.arraySize
                        ? muzzlesProperty.GetArrayElementAtIndex(stationIndex).objectReferenceValue as Transform
                        : null;
                    if (actualMuzzle != null && muzzleOpening != null && serializedMuzzle == actualMuzzle
                        && Vector3.Distance(actualMuzzle.position, muzzleOpening.position) < 0.08f)
                    {
                        alignedMuzzles++;
                    }
                    else
                    {
                        Debug.LogError($"P-51 Step 39 failed: {wingName} gun {localStation} firing origin is not attached to its visible muzzle tip.");
                        passed = false;
                    }

                    Transform[] bolts = FindChildrenNamed(gunTarget, "Gun Hold-Down Bolt");
                    bool boltsFit = bolts.Length == 4;
                    for (int index = 0; index < bolts.Length; index++)
                    {
                        boltsFit &= bolts[index].localPosition.y <= 0.055f && bolts[index].localScale.y <= 0.0205f;
                    }
                    if (boltsFit) fittedBoltSets++; else passed = false;

                    Transform[] latches = FindChildrenNamed(ammoTarget, "Ammo Box Latch");
                    bool latchesFit = latches.Length == 2;
                    for (int index = 0; index < latches.Length; index++)
                    {
                        latchesFit &= latches[index].localPosition.y <= 0.035f && latches[index].localScale.y <= 0.042f;
                    }
                    if (latchesFit) fittedLatchSets++; else passed = false;

                    Transform barrel = mountedGun != null ? FindChildRecursive(mountedGun, "Barrel") : null;
                    Transform collar = mountedGun != null ? FindChildRecursive(mountedGun, "Muzzle Collar") : null;
                    Transform opening = mountedGun != null ? FindChildRecursive(mountedGun, "Muzzle Opening") : null;
                    if (barrel != null && collar != null && opening != null
                        && barrel.GetComponent<Renderer>() != null
                        && collar.GetComponent<Renderer>() != null
                        && opening.GetComponent<Renderer>() != null)
                    {
                        glowingTipSets++;
                    }
                    else
                    {
                        passed = false;
                    }
                }
            }

            if (passed)
            {
                Debug.Log(
                    $"P-51 Step 39 passed. Actual muzzle anchors={alignedMuzzles}/6, fitted bolt sets={fittedBoltSets}/6, "
                    + $"fitted latch sets={fittedLatchSets}/6, heated barrel/tip sets={glowingTipSets}/6.");
            }
            else
            {
                Debug.LogError(
                    $"P-51 Step 39 failed. Actual muzzle anchors={alignedMuzzles}/6, fitted bolt sets={fittedBoltSets}/6, "
                    + $"fitted latch sets={fittedLatchSets}/6, heated barrel/tip sets={glowingTipSets}/6.");
            }
        }

        private static int TuckGunHoldDownBolts(Transform gunTarget)
        {
            Transform[] bolts = FindChildrenNamed(gunTarget, "Gun Hold-Down Bolt");
            for (int index = 0; index < bolts.Length; index++)
            {
                Transform bolt = bolts[index];
                Vector3 position = bolt.localPosition;
                position.x *= 0.78f;
                position.y = 0.045f;
                position.z *= 0.88f;
                bolt.localPosition = position;
                bolt.localScale = new Vector3(0.026f, 0.018f, 0.026f);
            }
            return bolts.Length;
        }

        private static int TuckAmmoLatches(Transform ammoTarget)
        {
            Transform[] latches = FindChildrenNamed(ammoTarget, "Ammo Box Latch");
            for (int index = 0; index < latches.Length; index++)
            {
                Transform latch = latches[index];
                float side = Mathf.Sign(latch.localPosition.x);
                Vector3 position = latch.localPosition;
                position.x = side == 0f ? 0f : side * 0.155f;
                position.y = 0.028f;
                latch.localPosition = position;
                latch.localScale = new Vector3(0.040f, 0.038f, 0.235f);
            }
            return latches.Length;
        }

        private static int FitInstallHighlight(Transform serviceTarget)
        {
            Transform highlight = FindChildRecursive(serviceTarget, "Armament Install Highlight");
            if (highlight == null)
            {
                return 0;
            }

            Vector3 rootPosition = highlight.localPosition;
            rootPosition.y = -0.09f;
            highlight.localPosition = rootPosition;

            Transform[] children = highlight.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < children.Length; index++)
            {
                Transform child = children[index];
                if (child == null || child == highlight)
                {
                    continue;
                }

                Vector3 position = child.localPosition;
                position.y *= 0.28f;
                child.localPosition = position;

                Vector3 scale = child.localScale;
                scale.x *= 0.78f;
                scale.y *= 0.28f;
                scale.z *= 0.78f;
                child.localScale = scale;
            }
            return 1;
        }

        private static Transform[] FindChildrenNamed(Transform parent, string name)
        {
            if (parent == null) return Array.Empty<Transform>();
            Transform[] all = parent.GetComponentsInChildren<Transform>(true);
            int count = 0;
            for (int index = 0; index < all.Length; index++)
            {
                if (all[index] != null && all[index].name == name) count++;
            }

            Transform[] result = new Transform[count];
            int write = 0;
            for (int index = 0; index < all.Length; index++)
            {
                if (all[index] != null && all[index].name == name)
                {
                    result[write++] = all[index];
                }
            }
            return result;
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent == null) return null;
            Transform[] transforms = parent.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index] != null && transforms[index].name == name)
                {
                    return transforms[index];
                }
            }
            return null;
        }
    }
}
