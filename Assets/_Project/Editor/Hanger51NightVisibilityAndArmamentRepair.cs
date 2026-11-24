using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hanger51.Aircraft;
using Hanger51.NPCs;
using Hanger51.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51NightVisibilityAndArmamentRepair
    {
        const string WorldName = "Hanger 51 Surrounding Countryside";
        const string RunwayName = "P-51 Flight Test Runway";
        const string LightingRootName = "H51 Time Calendar And Night Lighting";
        const string ArmamentRootName = "P-51 Serviceable Wing Armament";
        const string GeneratedFolder =
            "Assets/_Project/Environment/Generated/TimeAndLighting";
        const string MoonMaterialPath =
            GeneratedFolder + "/H51MoonDisc.mat";

        [MenuItem("Hanger 51/World/Current/156 - Repair Night Visibility Moon Runway And Wing Armament")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError(
                    "Step 156 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            GameObject world = GameObject.Find(WorldName);
            GameObject runway = GameObject.Find(RunwayName);
            H51WorldClock clock =
                UnityEngine.Object.FindFirstObjectByType<H51WorldClock>(
                    FindObjectsInactive.Include);
            H51UtilityGridController grid =
                UnityEngine.Object.FindFirstObjectByType<H51UtilityGridController>(
                    FindObjectsInactive.Include);
            H51DayNightCycle cycle =
                UnityEngine.Object.FindFirstObjectByType<H51DayNightCycle>(
                    FindObjectsInactive.Include);

            if (!world || !runway || !clock || !grid || !cycle)
            {
                Debug.LogError(
                    "Step 156 failed. World clock, grid, runway, or day/night cycle is missing. Run Step 152 first.");
                return;
            }

            int nightLights = UpgradeTownAndFacilityLights();
            int runwayBulbs;
            int runwayRealtimeLights;
            UpgradeRunwayLights(
                runway,
                grid,
                clock,
                out runwayBulbs,
                out runwayRealtimeLights);

            bool controlFixed = RepairRunwayControlLabel();
            bool moonReady = BuildOrRepairVisibleMoon(world.transform,cycle);

            int aircraftSystems = 0;
            int armamentVisualBindings = 0;
            int fullyEquippedStations = 0;
            RepairWingArmament(
                out aircraftSystems,
                out armamentVisualBindings,
                out fullyEquippedStations);

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.SaveOpenScenes();
            Hanger51BuildTools.PrepareCurrentSceneForBuild(false);

            Debug.Log(
                "Step 156 complete. Night-light fixtures upgraded=" + nightLights +
                ", runway bulbs=" + runwayBulbs +
                ", real runway lights=" + runwayRealtimeLights +
                ", runway control label fixed=" + controlFixed +
                ", visible moon ready=" + moonReady +
                ", P-51 armament systems repaired=" + aircraftSystems +
                ", armament visual bindings restored=" + armamentVisualBindings +
                ", fully equipped gun/ammo stations=" + fullyEquippedStations +
                ". Run Step 157 to validate.");
        }

        [MenuItem("Hanger 51/World/Current/157 - Validate Night Visibility Moon Runway And Wing Armament")]
        public static void Validate()
        {
            bool passed = true;

            H51NightLight[] nightLights =
                UnityEngine.Object.FindObjectsByType<H51NightLight>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            int streetLights =
                nightLights.Count(l =>
                    l && l.name.IndexOf(
                        "Street",
                        StringComparison.OrdinalIgnoreCase) >= 0);
            int buildingLights =
                nightLights.Count(l =>
                    l && l.name.IndexOf(
                        "Building",
                        StringComparison.OrdinalIgnoreCase) >= 0);
            int airportLights =
                nightLights.Count(l =>
                    l && l.name.IndexOf(
                        "Apron",
                        StringComparison.OrdinalIgnoreCase) >= 0);

            GameObject runway = GameObject.Find(RunwayName);
            Renderer[] runwayLenses = runway
                ? runway.GetComponentsInChildren<Renderer>(true)
                    .Where(r => r && r.name.IndexOf(
                        "Runway Light Lens",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToArray()
                : Array.Empty<Renderer>();

            Light[] runwayRealtime = runway
                ? runway.GetComponentsInChildren<Light>(true)
                    .Where(l => l && l.name.IndexOf(
                        "Runway Glow",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToArray()
                : Array.Empty<Light>();

            H51RunwayLightingController runwayController =
                runway ? runway.GetComponent<H51RunwayLightingController>() : null;

            H51RunwayLightSwitch runwaySwitch =
                UnityEngine.Object.FindFirstObjectByType<H51RunwayLightSwitch>(
                    FindObjectsInactive.Include);
            TextMesh runwayLabel = runwaySwitch
                ? runwaySwitch.GetComponentInChildren<TextMesh>(true)
                : null;
            bool labelOrientationReady =
                runwayLabel &&
                Quaternion.Angle(
                    runwayLabel.transform.localRotation,
                    Quaternion.identity) < 1f;

            H51DayNightCycle cycle =
                UnityEngine.Object.FindFirstObjectByType<H51DayNightCycle>(
                    FindObjectsInactive.Include);
            Transform moon =
                cycle ? FindDescendant(cycle.transform,"H51 Visible Moon") : null;
            Renderer moonDisc =
                moon ? moon.GetComponentInChildren<Renderer>(true) : null;

            P51WingArmamentSystem[] systems =
                UnityEngine.Object.FindObjectsByType<P51WingArmamentSystem>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                    .Where(s =>
                        s &&
                        s.transform.Find(ArmamentRootName))
                    .ToArray();

            int equippedStations = 0;
            int systemsWithSixVisuals = 0;
            for (int s=0;s<systems.Length;s++)
            {
                P51WingArmamentSystem system = systems[s];

                int stationEquipped = 0;
                for (int i=0;i<6;i++)
                {
                    if (system.IsGunInstalled(i) &&
                        system.IsAmmoInstalled(i) &&
                        system.GetAmmoRemaining(i) > 0)
                    {
                        stationEquipped++;
                        equippedStations++;
                    }
                }

                Transform armamentRoot =
                    system.transform.Find(ArmamentRootName);
                int guns = CountNamed(
                    armamentRoot,
                    "Installed M2 Wing Gun");
                int ammo = CountNamed(
                    armamentRoot,
                    "Installed Wing Ammo Box");

                if (stationEquipped == 6 && guns == 6 && ammo == 6)
                    systemsWithSixVisuals++;
            }

            if (streetLights < 10 || buildingLights < 10)
            {
                Debug.LogError(
                    "Step 157 failed: not enough town/house night-light fixtures remain. Street=" +
                    streetLights + ", building=" + buildingLights + ".");
                passed = false;
            }

            if (runwayLenses.Length < 40 ||
                runwayRealtime.Length < runwayLenses.Length ||
                !runwayController)
            {
                Debug.LogError(
                    "Step 157 failed: runway lighting is incomplete. Lenses=" +
                    runwayLenses.Length +
                    ", real glow lights=" + runwayRealtime.Length + ".");
                passed = false;
            }

            if (!runwaySwitch || !labelOrientationReady)
            {
                Debug.LogError(
                    "Step 157 failed: runway control label is missing or still uses the mirrored orientation.");
                passed = false;
            }

            if (!moon || !moonDisc)
            {
                Debug.LogError(
                    "Step 157 failed: visible moon object/renderer is missing.");
                passed = false;
            }

            if (systems.Length == 0 ||
                systemsWithSixVisuals != systems.Length)
            {
                Debug.LogError(
                    "Step 157 failed: one or more P-51 armament systems are not fully equipped/bound. Systems=" +
                    systems.Length +
                    ", fully ready=" + systemsWithSixVisuals +
                    ", equipped stations=" + equippedStations + ".");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Step 157 passed. Street lights=" + streetLights +
                    ", building lights=" + buildingLights +
                    ", airport/apron lights=" + airportLights +
                    ", runway lenses=" + runwayLenses.Length +
                    ", runway real lights=" + runwayRealtime.Length +
                    ", moon visual=ready, runway label orientation=ready, P-51 armament systems=" +
                    systems.Length +
                    ", fully equipped stations=" + equippedStations + ".");
            }
        }

        static int UpgradeTownAndFacilityLights()
        {
            H51NightLight[] lights =
                UnityEngine.Object.FindObjectsByType<H51NightLight>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            int upgraded = 0;

            for (int i=0;i<lights.Length;i++)
            {
                H51NightLight light = lights[i];
                if (!light) continue;

                bool apron =
                    light.name.IndexOf(
                        "Apron",
                        StringComparison.OrdinalIgnoreCase) >= 0;
                bool street =
                    light.name.IndexOf(
                        "Street",
                        StringComparison.OrdinalIgnoreCase) >= 0;

                float distance = apron ? 950f : street ? 520f : 300f;
                float emission = apron ? 13f : street ? 11f : 10f;
                float range = apron ? 42f : street ? 28f : 16f;
                float intensity = apron ? 4.5f : street ? 3.2f : 1.8f;

                light.ConfigureFlightVisibility(
                    distance,
                    emission,
                    range,
                    intensity);

                Renderer[] renderers =
                    light.GetComponentsInChildren<Renderer>(true);
                for (int r=0;r<renderers.Length;r++)
                {
                    Renderer renderer = renderers[r];
                    if (!renderer) continue;

                    Material material = renderer.sharedMaterial;
                    if (material &&
                        material.HasProperty("_EmissionColor"))
                    {
                        material.EnableKeyword("_EMISSION");
                        EditorUtility.SetDirty(material);
                    }

                    string n = renderer.name;
                    if (n.IndexOf(
                            "Lens",
                            StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        float target =
                            apron ? .58f : street ? .44f : .34f;
                        renderer.transform.localScale =
                            Vector3.one * target;
                        EditorUtility.SetDirty(renderer.transform);
                    }
                }

                EditorUtility.SetDirty(light);
                upgraded++;
            }

            return upgraded;
        }

        static void UpgradeRunwayLights(
            GameObject runway,
            H51UtilityGridController grid,
            H51WorldClock clock,
            out int bulbCount,
            out int realtimeCount)
        {
            Renderer[] all =
                runway.GetComponentsInChildren<Renderer>(true);

            Renderer[] fixtures = all
                .Where(r =>
                    r &&
                    (r.name.IndexOf(
                        "Runway Light Lens",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                     r.name.IndexOf(
                        "Runway Light Base",
                        StringComparison.OrdinalIgnoreCase) >= 0))
                .ToArray();

            Renderer[] lenses = all
                .Where(r =>
                    r &&
                    r.name.IndexOf(
                        "Runway Light Lens",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();

            List<Light> realtime = new List<Light>();

            for (int i=0;i<lenses.Length;i++)
            {
                Renderer lensRenderer = lenses[i];
                Transform lens = lensRenderer.transform;

                lens.localScale = Vector3.one * .28f;
                EditorUtility.SetDirty(lens);

                Material material = lensRenderer.sharedMaterial;
                if (material &&
                    material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    EditorUtility.SetDirty(material);
                }

                Transform glow =
                    DirectChild(lens,"H51 Flight Visible Runway Glow");
                if (!glow)
                {
                    Transform legacy =
                        DirectChild(lens,"H51 Runtime Runway Glow");
                    if (legacy)
                    {
                        legacy.name = "H51 Flight Visible Runway Glow";
                        glow = legacy;
                    }
                }

                if (!glow)
                {
                    GameObject glowObject =
                        new GameObject("H51 Flight Visible Runway Glow");
                    Undo.RegisterCreatedObjectUndo(
                        glowObject,
                        "Create flight-visible runway light");
                    glow = glowObject.transform;
                    glow.SetParent(lens,false);
                    glow.localPosition = Vector3.zero;
                    glow.localRotation = Quaternion.identity;
                }

                Light real = glow.GetComponent<Light>();
                if (!real)
                    real = Undo.AddComponent<Light>(glow.gameObject);

                real.type = LightType.Point;
                real.range = 34f;
                real.intensity = 4.6f;
                real.color = new Color(.72f,.88f,1f,1f);
                real.shadows = LightShadows.None;
                real.enabled = false;
                EditorUtility.SetDirty(real);
                realtime.Add(real);
            }

            H51RunwayLightingController controller =
                runway.GetComponent<H51RunwayLightingController>();
            if (!controller)
                controller =
                    Undo.AddComponent<H51RunwayLightingController>(runway);

            controller.Configure(
                fixtures,
                realtime.ToArray(),
                grid,
                clock);
            controller.ConfigureFlightVisibility(
                1800f,
                18f,
                34f,
                4.6f);
            EditorUtility.SetDirty(controller);

            bulbCount = lenses.Length;
            realtimeCount = realtime.Count;
        }

        static bool RepairRunwayControlLabel()
        {
            H51RunwayLightSwitch runwaySwitch =
                UnityEngine.Object.FindFirstObjectByType<H51RunwayLightSwitch>(
                    FindObjectsInactive.Include);
            if (!runwaySwitch) return false;

            TextMesh label =
                runwaySwitch.GetComponentInChildren<TextMesh>(true);
            if (!label) return false;

            Undo.RecordObject(
                label.transform,
                "Correct runway control label orientation");
            label.transform.localRotation = Quaternion.identity;
            label.transform.localPosition =
                new Vector3(0f,1.85f,-.34f);
            label.anchor = TextAnchor.UpperCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = .09f;
            EditorUtility.SetDirty(label);
            EditorUtility.SetDirty(label.transform);

            return true;
        }

        static bool BuildOrRepairVisibleMoon(
            Transform world,
            H51DayNightCycle cycle)
        {
            Transform lightingRoot =
                FindDescendant(world,LightingRootName);
            if (!lightingRoot || !cycle) return false;

            Transform moonRoot =
                DirectChild(lightingRoot,"H51 Visible Moon");
            if (!moonRoot)
            {
                GameObject rootObject =
                    new GameObject("H51 Visible Moon");
                Undo.RegisterCreatedObjectUndo(
                    rootObject,
                    "Create visible moon root");
                moonRoot = rootObject.transform;
                moonRoot.SetParent(lightingRoot,false);
            }

            Transform disc =
                DirectChild(moonRoot,"Moon Disc");
            if (!disc)
            {
                GameObject sphere =
                    GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Undo.RegisterCreatedObjectUndo(
                    sphere,
                    "Create visible moon disc");
                sphere.name = "Moon Disc";
                disc = sphere.transform;
                disc.SetParent(moonRoot,false);
                disc.localPosition = Vector3.zero;
                disc.localRotation = Quaternion.identity;
                disc.localScale = Vector3.one;

                Collider collider = sphere.GetComponent<Collider>();
                if (collider)
                    Undo.DestroyObjectImmediate(collider);
            }

            Material moonMaterial = CreateMoonMaterial();
            Renderer renderer = disc.GetComponent<Renderer>();
            if (renderer)
            {
                renderer.sharedMaterial = moonMaterial;
                EditorUtility.SetDirty(renderer);
            }

            moonRoot.localScale = Vector3.one;
            cycle.ConfigureMoonVisual(moonRoot,1800f,95f);
            EditorUtility.SetDirty(cycle);
            return renderer && moonMaterial;
        }

        static Material CreateMoonMaterial()
        {
            EnsureFolder(GeneratedFolder);

            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(
                    MoonMaterialPath);
            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader)
                shader = Shader.Find("Unlit/Color");
            if (!shader)
                shader = Shader.Find("Standard");

            if (!material)
            {
                material = new Material(shader);
                material.name = "H51MoonDisc";
                AssetDatabase.CreateAsset(
                    material,
                    MoonMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            Color color =
                new Color(.90f,.93f,1f,1f);
            material.color = color;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor",color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color",color);
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor(
                    "_EmissionColor",
                    color * 3f);
                material.EnableKeyword("_EMISSION");
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        static void RepairWingArmament(
            out int systemsRepaired,
            out int visualBindings,
            out int equippedStations)
        {
            systemsRepaired = 0;
            visualBindings = 0;
            equippedStations = 0;

            P51WingArmamentSystem[] systems =
                UnityEngine.Object.FindObjectsByType<P51WingArmamentSystem>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                    .Where(s =>
                        s &&
                        s.transform.Find(ArmamentRootName))
                    .ToArray();

            for (int s=0;s<systems.Length;s++)
            {
                P51WingArmamentSystem system = systems[s];

                visualBindings +=
                    system.RepairVisualBindingsFromHierarchy(true);

                Transform armamentRoot =
                    system.transform.Find(ArmamentRootName);
                if (armamentRoot)
                {
                    GameObject[] bayRoots =
                        new GameObject[2];
                    bayRoots[0] =
                        FindDescendant(
                            armamentRoot,
                            "Left Wing Armament Bay Interior")
                            ?.gameObject;
                    bayRoots[1] =
                        FindDescendant(
                            armamentRoot,
                            "Right Wing Armament Bay Interior")
                            ?.gameObject;

                    P51WingArmamentBayPersistence persistence =
                        system.GetComponent<P51WingArmamentBayPersistence>();
                    if (!persistence)
                        persistence =
                            Undo.AddComponent<P51WingArmamentBayPersistence>(
                                system.gameObject);
                    persistence.Configure(bayRoots);
                    EditorUtility.SetDirty(persistence);
                }

                for (int i=0;i<6;i++)
                {
                    if (system.IsGunInstalled(i) &&
                        system.IsAmmoInstalled(i) &&
                        system.GetAmmoRemaining(i) > 0)
                        equippedStations++;
                }

                EditorUtility.SetDirty(system);
                systemsRepaired++;
            }
        }

        static int CountNamed(
            Transform root,
            string objectName)
        {
            if (!root) return 0;

            int count = 0;
            Transform[] all =
                root.GetComponentsInChildren<Transform>(true);
            for (int i=0;i<all.Length;i++)
                if (all[i] && all[i].name == objectName)
                    count++;
            return count;
        }

        static Transform FindDescendant(
            Transform root,
            string objectName)
        {
            if (!root) return null;

            Transform[] all =
                root.GetComponentsInChildren<Transform>(true);
            for (int i=0;i<all.Length;i++)
                if (all[i] && all[i].name == objectName)
                    return all[i];
            return null;
        }

        static Transform DirectChild(
            Transform root,
            string objectName)
        {
            if (!root) return null;

            for (int i=0;i<root.childCount;i++)
            {
                Transform child = root.GetChild(i);
                if (child && child.name == objectName)
                    return child;
            }
            return null;
        }

        static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) ||
                folderPath == "Assets" ||
                AssetDatabase.IsValidFolder(folderPath))
                return;

            string parent =
                Path.GetDirectoryName(folderPath)?.Replace("\\","/");
            string folder =
                Path.GetFileName(folderPath);

            if (string.IsNullOrWhiteSpace(parent) ||
                string.IsNullOrWhiteSpace(folder))
                return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent,folder);
        }
    }
}
