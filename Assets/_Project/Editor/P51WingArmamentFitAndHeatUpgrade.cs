using System;
using System.Collections.Generic;
using Hanger51.Aircraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class P51WingArmamentFitAndHeatUpgrade
    {
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string ArmamentRootName = "P-51 Serviceable Wing Armament";
        private const string GunDarkPath = "Assets/_Project/Aircraft/P51/Armament/Materials/WingGunDark.mat";

        private static readonly Vector3 InstalledGunScale = new Vector3(0.68f, 0.46f, 1.00f);
        private static readonly Vector3 InstalledAmmoScale = new Vector3(0.78f, 0.42f, 0.80f);

        [MenuItem("Hanger 51/P-51 Mustang/36 - Fit Wing Guns, Ammo and Add Barrel Heat")]
        public static void FitWingArmamentAndAddHeat()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("P-51 Step 36 failed. Exit Play mode first.");
                return;
            }
            if (EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 Step 36 failed. Wait for Unity to finish compiling.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            GameObject aircraft = GameObject.Find(AircraftRootName);
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path) || aircraft == null)
            {
                Debug.LogError("P-51 Step 36 failed. Open the saved hangar scene containing the P-51 first.");
                return;
            }

            P51WingArmamentSystem system = aircraft.GetComponent<P51WingArmamentSystem>();
            Transform armamentRoot = aircraft.transform.Find(ArmamentRootName);
            if (system == null || armamentRoot == null)
            {
                Debug.LogError("P-51 Step 36 failed. Run the wing armament visual upgrade first.", aircraft);
                return;
            }

            Material gunDark = AssetDatabase.LoadAssetAtPath<Material>(GunDarkPath);
            if (gunDark == null)
            {
                Debug.LogError("P-51 Step 36 failed. WingGunDark material is missing.");
                return;
            }

            Renderer[] heatedBarrels = new Renderer[6];
            GameObject[] bayInteriorRoots = new GameObject[2];
            int fittedGuns = 0;
            int fittedAmmoBoxes = 0;

            for (int wingIndex = 0; wingIndex < 2; wingIndex++)
            {
                string wingName = wingIndex == 0 ? "Left" : "Right";
                Transform interior = FindChildRecursive(armamentRoot, $"{wingName} Wing Armament Bay Interior");
                if (interior == null)
                {
                    Debug.LogError($"P-51 Step 36 failed. {wingName} armament bay interior is missing.");
                    return;
                }
                bayInteriorRoots[wingIndex] = interior.gameObject;

                for (int localStation = 1; localStation <= 3; localStation++)
                {
                    int stationIndex = wingIndex * 3 + (localStation - 1);
                    Transform gunTarget = FindChildRecursive(interior, $"{wingName} Gun Mount {localStation}");
                    Transform ammoTarget = FindChildRecursive(interior, $"{wingName} Ammo Bay {localStation}");
                    if (gunTarget == null || ammoTarget == null)
                    {
                        Debug.LogError($"P-51 Step 36 failed. {wingName} station {localStation} hierarchy is incomplete.");
                        return;
                    }

                    Vector3 gunTargetPosition = gunTarget.localPosition;
                    gunTargetPosition.y = 0.085f;
                    gunTarget.localPosition = gunTargetPosition;

                    Transform mountedGun = FindChildRecursive(gunTarget, "Installed M2 Wing Gun");
                    if (mountedGun == null)
                    {
                        Debug.LogError($"P-51 Step 36 failed. Installed gun visual is missing at {wingName} station {localStation}.");
                        return;
                    }
                    mountedGun.localPosition = new Vector3(0f, 0f, 0.035f);
                    mountedGun.localScale = InstalledGunScale;
                    fittedGuns++;

                    // Keep the full longitudinal barrel length so the muzzle remains ahead of the
                    // leading edge, but compress the receiver vertically and laterally to fit the wing.
                    Renderer barrelJacket = SetBarrelPartsBlackAndGetHeatRenderer(mountedGun, gunDark);
                    heatedBarrels[stationIndex] = barrelJacket;

                    Vector3 ammoTargetPosition = ammoTarget.localPosition;
                    ammoTargetPosition.y = 0.075f;
                    ammoTarget.localPosition = ammoTargetPosition;

                    Transform mountedAmmo = FindChildRecursive(ammoTarget, "Installed Wing Ammo Box");
                    if (mountedAmmo == null)
                    {
                        Debug.LogError($"P-51 Step 36 failed. Installed ammo visual is missing at {wingName} station {localStation}.");
                        return;
                    }
                    mountedAmmo.localPosition = Vector3.zero;
                    mountedAmmo.localScale = InstalledAmmoScale;
                    fittedAmmoBoxes++;

                    // The muzzle/ejection transforms are children of the service target, not the
                    // visual gun prefab, so they retain their original firing geometry and barrel reach.
                    Transform muzzle = FindChildRecursive(gunTarget, "Muzzle");
                    if (muzzle != null)
                    {
                        Vector3 muzzlePosition = muzzle.localPosition;
                        muzzlePosition.y = 0.10f;
                        muzzlePosition.z = Mathf.Max(muzzlePosition.z, 1.70f);
                        muzzle.localPosition = muzzlePosition;
                    }
                }
            }

            P51WingGunBarrelHeatController heatController = aircraft.GetComponent<P51WingGunBarrelHeatController>();
            if (heatController == null)
            {
                heatController = Undo.AddComponent<P51WingGunBarrelHeatController>(aircraft);
            }
            heatController.Configure(system, heatedBarrels);
            EditorUtility.SetDirty(heatController);

            P51WingArmamentBayPersistence persistence = aircraft.GetComponent<P51WingArmamentBayPersistence>();
            if (persistence == null)
            {
                persistence = Undo.AddComponent<P51WingArmamentBayPersistence>(aircraft);
            }
            persistence.Configure(bayInteriorRoots);
            EditorUtility.SetDirty(persistence);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 36 changed the armament fit/heat system but Unity could not save the scene.");
                return;
            }

            Debug.Log(
                $"P-51 Step 36 complete. Fitted {fittedGuns} wing guns and {fittedAmmoBoxes} ammunition boxes inside the wing volume, "
                + "kept full-length barrels protruding through the leading edge, kept installed armament present with covers closed, "
                + "and added independent red-hot barrel heating/cooling for all six stations.",
                aircraft);
        }

        [MenuItem("Hanger 51/P-51 Mustang/37 - Validate Wing Gun Fit and Barrel Heat")]
        public static void ValidateWingArmamentFitAndHeat()
        {
            bool passed = true;
            GameObject aircraft = GameObject.Find(AircraftRootName);
            if (aircraft == null)
            {
                Debug.LogError("P-51 Step 37 failed: P-51 aircraft is missing.");
                return;
            }

            Transform armamentRoot = aircraft.transform.Find(ArmamentRootName);
            if (armamentRoot == null)
            {
                Debug.LogError("P-51 Step 37 failed: armament root is missing.");
                return;
            }

            P51WingGunBarrelHeatController heatController = aircraft.GetComponent<P51WingGunBarrelHeatController>();
            P51WingArmamentBayPersistence persistence = aircraft.GetComponent<P51WingArmamentBayPersistence>();
            if (heatController == null)
            {
                Debug.LogError("P-51 Step 37 failed: barrel heat controller is missing.");
                passed = false;
            }
            if (persistence == null)
            {
                Debug.LogError("P-51 Step 37 failed: closed-panel armament persistence is missing.");
                passed = false;
            }

            int gunCount = 0;
            int ammoCount = 0;
            int blackBarrelCount = 0;
            for (int wingIndex = 0; wingIndex < 2; wingIndex++)
            {
                string wingName = wingIndex == 0 ? "Left" : "Right";
                Transform interior = FindChildRecursive(armamentRoot, $"{wingName} Wing Armament Bay Interior");
                if (interior == null)
                {
                    Debug.LogError($"P-51 Step 37 failed: {wingName} bay interior is missing.");
                    passed = false;
                    continue;
                }

                for (int localStation = 1; localStation <= 3; localStation++)
                {
                    Transform gunTarget = FindChildRecursive(interior, $"{wingName} Gun Mount {localStation}");
                    Transform ammoTarget = FindChildRecursive(interior, $"{wingName} Ammo Bay {localStation}");
                    Transform mountedGun = gunTarget != null ? FindChildRecursive(gunTarget, "Installed M2 Wing Gun") : null;
                    Transform mountedAmmo = ammoTarget != null ? FindChildRecursive(ammoTarget, "Installed Wing Ammo Box") : null;
                    Transform barrelJacket = mountedGun != null ? FindChildRecursive(mountedGun, "Barrel Jacket") : null;
                    Transform muzzle = gunTarget != null ? FindChildRecursive(gunTarget, "Muzzle") : null;

                    if (mountedGun != null
                        && Approximately(mountedGun.localScale, InstalledGunScale)
                        && muzzle != null
                        && muzzle.localPosition.z >= 1.69f)
                    {
                        gunCount++;
                    }
                    else
                    {
                        Debug.LogError($"P-51 Step 37 failed: {wingName} gun {localStation} is not fitted or its muzzle no longer reaches the leading edge.");
                        passed = false;
                    }

                    if (mountedAmmo != null && Approximately(mountedAmmo.localScale, InstalledAmmoScale))
                    {
                        ammoCount++;
                    }
                    else
                    {
                        Debug.LogError($"P-51 Step 37 failed: {wingName} ammo box {localStation} is not using the fitted scale.");
                        passed = false;
                    }

                    Renderer barrelRenderer = barrelJacket != null ? barrelJacket.GetComponent<Renderer>() : null;
                    if (barrelRenderer != null && barrelRenderer.sharedMaterial != null
                        && barrelRenderer.sharedMaterial.name.Contains("WingGunDark", StringComparison.OrdinalIgnoreCase))
                    {
                        blackBarrelCount++;
                    }
                    else
                    {
                        Debug.LogError($"P-51 Step 37 failed: {wingName} gun {localStation} barrel is not using the black cold material.");
                        passed = false;
                    }
                }
            }

            if (passed)
            {
                Debug.Log(
                    $"P-51 Step 37 passed. Guns={gunCount}/6, ammo boxes={ammoCount}/6, black heated barrels={blackBarrelCount}/6, "
                    + "closed-panel persistence and independent barrel heat/cooling are installed.");
            }
        }

        private static Renderer SetBarrelPartsBlackAndGetHeatRenderer(Transform mountedGun, Material gunDark)
        {
            string[] barrelPartNames = { "Barrel Jacket", "Barrel", "Muzzle Collar", "Muzzle Opening" };
            Renderer heatRenderer = null;
            for (int index = 0; index < barrelPartNames.Length; index++)
            {
                Transform part = FindChildRecursive(mountedGun, barrelPartNames[index]);
                Renderer renderer = part != null ? part.GetComponent<Renderer>() : null;
                if (renderer == null) continue;
                renderer.sharedMaterial = gunDark;
                EditorUtility.SetDirty(renderer);
                if (barrelPartNames[index] == "Barrel Jacket") heatRenderer = renderer;
            }
            return heatRenderer;
        }

        private static bool Approximately(Vector3 a, Vector3 b)
        {
            return Mathf.Abs(a.x - b.x) < 0.001f
                && Mathf.Abs(a.y - b.y) < 0.001f
                && Mathf.Abs(a.z - b.z) < 0.001f;
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
