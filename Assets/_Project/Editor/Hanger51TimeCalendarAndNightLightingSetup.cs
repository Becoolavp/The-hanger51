using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hanger51.NPCs;
using Hanger51.Player;
using Hanger51.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51TimeCalendarAndNightLightingSetup
    {
        const string WorldName = "Hanger 51 Surrounding Countryside";
        const string CitizenRootName = "Hanger 51 Citizen System";
        const string LightingRootName = "H51 Time Calendar And Night Lighting";
        const string RunwayRootName = "P-51 Flight Test Runway";
        const string AircraftRootName = "P-51D Mustang Test Aircraft";
        const string AirportRootName = "Hanger 51 Airport Complex";
        const string HangarRootName = "Hanger 51 Test Hangar";
        const string MaterialFolder =
            "Assets/_Project/Environment/Generated/TimeAndLighting";
        const string PoleMaterialPath = MaterialFolder + "/NightLampMetal.mat";
        const string WarmLightMaterialPath = MaterialFolder + "/WarmNightLamp.mat";
        const string CoolLightMaterialPath = MaterialFolder + "/CoolAirportLamp.mat";
        const string MoonMaterialPath = MaterialFolder + "/H51MoonDisc.mat";

        [MenuItem("Hanger 51/World/Current/152 - Add Calendar Day-Night Cycle And Night Lighting")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError(
                    "Step 152 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            GameObject world = GameObject.Find(WorldName);
            if (!world)
            {
                Debug.LogError(
                    "Step 152 failed. The current surrounding countryside is missing.");
                return;
            }

            Transform citizenRoot = FindChild(world.transform,CitizenRootName);
            if (!citizenRoot)
            {
                Debug.LogError(
                    "Step 152 failed. The citizen system is missing. Build the citizen system first.");
                return;
            }

            H51WorldClock clock = citizenRoot.GetComponent<H51WorldClock>();
            if (!clock)
                clock = Undo.AddComponent<H51WorldClock>(citizenRoot.gameObject);

            // The date is intentionally configurable. This gives the current world a concrete
            // calendar immediately without baking historical assumptions into citizen logic.
            clock.ConfigureCalendar(2026,6,1,6.5f,1.5f);

            Transform previous = DirectChild(world.transform,LightingRootName);
            if (previous)
                Undo.DestroyObjectImmediate(previous.gameObject);

            Transform root = New(LightingRootName,world.transform);

            Light sun = ResolveSun();
            Light moon = BuildMoon(root);
            Transform moonVisual = BuildMoonVisual(root);
            H51DayNightCycle cycle =
                Undo.AddComponent<H51DayNightCycle>(root.gameObject);
            cycle.Configure(clock,sun,moon);
            cycle.ConfigureMoonVisual(moonVisual,1800f,95f);

            H51UtilityGridController grid =
                UnityEngine.Object.FindFirstObjectByType<H51UtilityGridController>(
                    FindObjectsInactive.Include);
            H51PowerPlantController plant =
                UnityEngine.Object.FindFirstObjectByType<H51PowerPlantController>(
                    FindObjectsInactive.Include);

            EnsureFolder(MaterialFolder);
            Material poleMaterial = CreateMaterial(
                PoleMaterialPath,
                new Color(.12f,.14f,.16f,1f),
                .55f,
                .42f,
                Color.black);
            Material warmLamp = CreateMaterial(
                WarmLightMaterialPath,
                new Color(.22f,.17f,.09f,1f),
                .08f,
                .72f,
                new Color(3.2f,2.15f,.78f,1f));
            Material coolLamp = CreateMaterial(
                CoolLightMaterialPath,
                new Color(.12f,.18f,.22f,1f),
                .10f,
                .75f,
                new Color(2.3f,3.1f,4.2f,1f));

            Terrain terrain =
                UnityEngine.Object.FindFirstObjectByType<Terrain>(
                    FindObjectsInactive.Include);
            H51TrafficNetwork traffic =
                world.GetComponentInChildren<H51TrafficNetwork>(true);
            H51CitizenAnchor[] anchors =
                world.GetComponentsInChildren<H51CitizenAnchor>(true);

            Transform townLights = New("Town Street And Building Lights",root);
            int streetLights = BuildStreetLights(
                townLights,
                terrain,
                traffic,
                anchors,
                grid,
                poleMaterial,
                warmLamp);
            int buildingLights = BuildBuildingLights(
                townLights,
                terrain,
                anchors,
                grid,
                poleMaterial,
                warmLamp);

            GameObject runway = GameObject.Find(RunwayRootName);
            H51RunwayLightingController runwayController = null;
            H51RunwayLightSwitch runwaySwitch = null;
            int runwayFixtures = 0;
            int runwayRealtimeLights = 0;
            int facilityLights = 0;

            if (runway)
            {
                Transform airportLights = New("Hanger 51 Airport Night Lighting",root);
                facilityLights = BuildAirportApronLights(
                    airportLights,
                    terrain,
                    runway.transform,
                    grid,
                    poleMaterial,
                    coolLamp);

                runwayController = ConfigureRunwayLights(
                    runway,
                    grid,
                    clock,
                    out runwayFixtures,
                    out runwayRealtimeLights);

                runwaySwitch = BuildRunwaySwitch(
                    airportLights,
                    runway.transform,
                    runwayController,
                    poleMaterial,
                    coolLamp);
            }
            else
            {
                Debug.LogWarning(
                    "Step 152 could not find the P-51 runway. The world day/night and town lighting were built, but runway lighting was skipped.");
            }

            FirstPersonController player =
                UnityEngine.Object.FindFirstObjectByType<FirstPersonController>(
                    FindObjectsInactive.Include);
            if (player)
            {
                H51WorldTimeHUD hud = player.GetComponent<H51WorldTimeHUD>();
                if (!hud) hud = Undo.AddComponent<H51WorldTimeHUD>(player.gameObject);
                hud.Configure(clock);

                H51FacilityLightingPlayerInteractor interactor =
                    player.GetComponent<H51FacilityLightingPlayerInteractor>();
                if (!interactor)
                    interactor =
                        Undo.AddComponent<H51FacilityLightingPlayerInteractor>(
                            player.gameObject);

                EditorUtility.SetDirty(hud);
                EditorUtility.SetDirty(interactor);
            }

            EditorUtility.SetDirty(clock);
            EditorUtility.SetDirty(cycle);
            if (runwayController) EditorUtility.SetDirty(runwayController);
            if (runwaySwitch) EditorUtility.SetDirty(runwaySwitch);
            if (grid) EditorUtility.SetDirty(grid);
            if (plant) EditorUtility.SetDirty(plant);

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.SaveOpenScenes();
            Hanger51BuildTools.PrepareCurrentSceneForBuild(false);

            Selection.activeGameObject = root.gameObject;
            Debug.Log(
                "Step 152 complete. Installed the calendar-aware world clock, seasonal sun/moon day-night cycle, " +
                "player clock/calendar HUD [K], weekday/weekend and essential-shift scheduling foundation, " +
                "street/building lights=" + (streetLights + buildingLights) +
                " (street=" + streetLights + ", building=" + buildingLights + "), airport apron lights=" +
                facilityLights + ", runway fixtures=" + runwayFixtures +
                ", nearby real runway lights=" + runwayRealtimeLights +
                ". Night lighting now requests grid power from the regional generating station. " +
                "Run Step 153 to validate.",
                root.gameObject);
        }

        [MenuItem("Hanger 51/World/Current/153 - Validate Calendar Day-Night And Night Lighting")]
        public static void Validate()
        {
            bool passed = true;
            GameObject world = GameObject.Find(WorldName);
            Transform root = world ? DirectChild(world.transform,LightingRootName) : null;
            H51WorldClock clock =
                UnityEngine.Object.FindFirstObjectByType<H51WorldClock>(
                    FindObjectsInactive.Include);
            H51DayNightCycle cycle =
                UnityEngine.Object.FindFirstObjectByType<H51DayNightCycle>(
                    FindObjectsInactive.Include);
            H51UtilityGridController grid =
                UnityEngine.Object.FindFirstObjectByType<H51UtilityGridController>(
                    FindObjectsInactive.Include);
            H51PowerPlantController plant =
                UnityEngine.Object.FindFirstObjectByType<H51PowerPlantController>(
                    FindObjectsInactive.Include);
            H51NightLight[] nightLights =
                UnityEngine.Object.FindObjectsByType<H51NightLight>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            H51RunwayLightingController runway =
                UnityEngine.Object.FindFirstObjectByType<H51RunwayLightingController>(
                    FindObjectsInactive.Include);
            H51RunwayLightSwitch runwaySwitch =
                UnityEngine.Object.FindFirstObjectByType<H51RunwayLightSwitch>(
                    FindObjectsInactive.Include);
            H51WorldTimeHUD hud =
                UnityEngine.Object.FindFirstObjectByType<H51WorldTimeHUD>(
                    FindObjectsInactive.Include);
            H51FacilityLightingPlayerInteractor interactor =
                UnityEngine.Object.FindFirstObjectByType<H51FacilityLightingPlayerInteractor>(
                    FindObjectsInactive.Include);

            if (!world || !root)
            {
                Debug.LogError("Step 153 failed: the time/night-lighting world root is missing.");
                passed = false;
            }
            if (!clock)
            {
                Debug.LogError("Step 153 failed: H51WorldClock is missing.");
                passed = false;
            }
            if (!cycle)
            {
                Debug.LogError("Step 153 failed: H51DayNightCycle is missing.");
                passed = false;
            }
            if (!grid || !plant)
            {
                Debug.LogError(
                    "Step 153 failed: the utility grid or regional power plant is missing.");
                passed = false;
            }
            if (nightLights.Length < 12)
            {
                Debug.LogError(
                    "Step 153 failed: expected at least 12 grid-powered night lights; found " +
                    nightLights.Length + ".");
                passed = false;
            }
            if (!runway || !runwaySwitch || !interactor)
            {
                Debug.LogError(
                    "Step 153 failed: runway controller, runway switch, or player lighting interactor is missing.");
                passed = false;
            }
            if (!hud)
            {
                Debug.LogError(
                    "Step 153 failed: the player clock/calendar HUD is missing.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Step 153 passed. Calendar=" +
                    clock.DateText + " " + clock.TimeText +
                    ", night lights=" + nightLights.Length +
                    ", runway mode=" + runway.Mode +
                    ". World time now drives sun/moon lighting, citizen calendar schedules, " +
                    "grid-powered night demand, and airport runway lighting.");
            }
        }

        static int BuildStreetLights(
            Transform parent,
            Terrain terrain,
            H51TrafficNetwork traffic,
            H51CitizenAnchor[] anchors,
            H51UtilityGridController grid,
            Material poleMaterial,
            Material lampMaterial)
        {
            if (!traffic || traffic.NodeCount <= 0) return 0;

            IReadOnlyList<Vector3> nodes = traffic.Nodes;
            List<Vector3> selected = new List<Vector3>();
            int count = 0;
            int stride = Mathf.Max(1,nodes.Count / 260);

            for (int i=0;i<nodes.Count && count<150;i+=stride)
            {
                Vector3 node = nodes[i];
                if (!NearUsefulAnchor(node,anchors,150f)) continue;
                if (TooClose(node,selected,30f)) continue;

                Vector3 tangent = NearestNodeDirection(nodes,i);
                if (tangent.sqrMagnitude < .01f) tangent = Vector3.forward;
                Vector3 side = Vector3.Cross(Vector3.up,tangent).normalized;
                float direction = (count & 1) == 0 ? 1f : -1f;
                Vector3 position = node + side * 5.8f * direction;
                position.y = GroundY(terrain,position) + .08f;

                BuildLamp(
                    parent,
                    "Town Street Lamp " + (count + 1),
                    position,
                    5.4f,
                    28f,
                    3.2f,
                    new Color(1f,.78f,.43f),
                    grid,
                    .0018f,
                    520f,
                    poleMaterial,
                    lampMaterial);
                selected.Add(node);
                count++;
            }

            return count;
        }

        static int BuildBuildingLights(
            Transform parent,
            Terrain terrain,
            H51CitizenAnchor[] anchors,
            H51UtilityGridController grid,
            Material poleMaterial,
            Material lampMaterial)
        {
            if (anchors == null) return 0;

            List<Vector3> selected = new List<Vector3>();
            int count = 0;
            for (int i=0;i<anchors.Length && count<120;i++)
            {
                H51CitizenAnchor anchor = anchors[i];
                if (!anchor) continue;

                bool useful =
                    anchor.Kind == H51CitizenAnchorKind.Home ||
                    anchor.Kind == H51CitizenAnchorKind.Work ||
                    anchor.Kind == H51CitizenAnchorKind.Shop ||
                    anchor.Kind == H51CitizenAnchorKind.Medical ||
                    anchor.Kind == H51CitizenAnchorKind.Civic ||
                    anchor.Kind == H51CitizenAnchorKind.Service;
                if (!useful) continue;

                Vector3 source = anchor.transform.position;
                if (TooClose(source,selected,13f)) continue;

                Vector3 forward = Vector3.ProjectOnPlane(
                    anchor.transform.forward,
                    Vector3.up).normalized;
                if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
                Vector3 position = source - forward * 1.1f;
                position.y = GroundY(terrain,position) + 2.45f;

                BuildWallLight(
                    parent,
                    "Building Night Light " + (count + 1),
                    position,
                    grid,
                    anchor.Kind == H51CitizenAnchorKind.Home ? .0009f : .0014f,
                    lampMaterial);
                selected.Add(source);
                count++;
            }
            return count;
        }

        static int BuildAirportApronLights(
            Transform parent,
            Terrain terrain,
            Transform runway,
            H51UtilityGridController grid,
            Material poleMaterial,
            Material lampMaterial)
        {
            Vector3[] local =
            {
                new Vector3(-31f,0f,-500f),
                new Vector3(31f,0f,-500f),
                new Vector3(-40f,0f,-450f),
                new Vector3(40f,0f,-450f),
                new Vector3(-34f,0f,-395f),
                new Vector3(34f,0f,-395f)
            };

            for (int i=0;i<local.Length;i++)
            {
                Vector3 position = runway.TransformPoint(local[i]);
                position.y = GroundY(terrain,position) + .08f;
                BuildLamp(
                    parent,
                    "Hanger 51 Apron Mast " + (i + 1),
                    position,
                    8.5f,
                    42f,
                    4.5f,
                    new Color(.76f,.88f,1f),
                    grid,
                    .006f,
                    950f,
                    poleMaterial,
                    lampMaterial);
            }
            return local.Length;
        }

        static H51RunwayLightingController ConfigureRunwayLights(
            GameObject runway,
            H51UtilityGridController grid,
            H51WorldClock clock,
            out int fixtureCount,
            out int realtimeCount)
        {
            Renderer[] all = runway.GetComponentsInChildren<Renderer>(true);
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
                .Where(r => r && r.name.IndexOf(
                    "Runway Light Lens",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();

            List<Light> realtime = new List<Light>();
            for (int i=0;i<lenses.Length;i++)
            {
                Transform lens = lenses[i].transform;
                lens.localScale = Vector3.one * .28f;
                Transform existing = lens.Find("H51 Runtime Runway Glow");
                Light light;
                if (existing)
                {
                    light = existing.GetComponent<Light>();
                }
                else
                {
                    Transform glow = New("H51 Runtime Runway Glow",lens);
                    glow.localPosition = Vector3.zero;
                    light = Undo.AddComponent<Light>(glow.gameObject);
                }

                light.type = LightType.Point;
                light.range = 34f;
                light.intensity = 4.6f;
                light.color = new Color(.72f,.88f,1f);
                light.shadows = LightShadows.None;
                light.enabled = false;
                realtime.Add(light);
            }

            H51RunwayLightingController controller =
                runway.GetComponent<H51RunwayLightingController>();
            if (!controller)
                controller = Undo.AddComponent<H51RunwayLightingController>(runway);
            controller.Configure(fixtures,realtime.ToArray(),grid,clock);
            controller.ConfigureFlightVisibility(1800f,18f,34f,4.6f);
            controller.SetMode(H51RunwayLightMode.Auto);

            fixtureCount = fixtures.Length;
            realtimeCount = realtime.Count;
            return controller;
        }

        static H51RunwayLightSwitch BuildRunwaySwitch(
            Transform parent,
            Transform runway,
            H51RunwayLightingController controller,
            Material metal,
            Material lamp)
        {
            Transform root = New("Hanger 51 Runway Lighting Control",parent);
            PositionRunwayControlInsideAirport(root,runway);

            GameObject cabinet = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(cabinet,"Create runway light control cabinet");
            cabinet.name = "Runway Lighting Control Cabinet";
            cabinet.transform.SetParent(root,false);
            cabinet.transform.localPosition = new Vector3(0f,1.15f,0f);
            cabinet.transform.localScale = new Vector3(1.0f,2.3f,.55f);
            Renderer cabinetRenderer = cabinet.GetComponent<Renderer>();
            if (cabinetRenderer) cabinetRenderer.sharedMaterial = metal;

            GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Undo.RegisterCreatedObjectUndo(indicator,"Create runway light control indicator");
            indicator.name = "Runway Lighting Control Indicator";
            indicator.transform.SetParent(root,false);
            indicator.transform.localPosition = new Vector3(0f,1.45f,-.31f);
            indicator.transform.localScale = Vector3.one * .17f;
            Renderer indicatorRenderer = indicator.GetComponent<Renderer>();
            if (indicatorRenderer) indicatorRenderer.sharedMaterial = lamp;
            Collider indicatorCollider = indicator.GetComponent<Collider>();
            if (indicatorCollider) Undo.DestroyObjectImmediate(indicatorCollider);

            Transform textRoot = New("Runway Lighting Status",root);
            textRoot.localPosition = new Vector3(0f,1.85f,-.34f);
            textRoot.localRotation = Quaternion.identity;
            TextMesh text = Undo.AddComponent<TextMesh>(textRoot.gameObject);
            text.anchor = TextAnchor.UpperCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = .11f;
            text.fontSize = 36;
            text.color = Color.white;

            // H51RunwayLightSwitch requires a Collider. Add the collider first so
            // Unity can satisfy the RequireComponent contract before the switch is created.
            BoxCollider interaction = root.GetComponent<BoxCollider>();
            if (!interaction)
                interaction = Undo.AddComponent<BoxCollider>(root.gameObject);
            interaction.center = new Vector3(0f,1.15f,0f);
            interaction.size = new Vector3(1.5f,2.7f,1.0f);
            interaction.isTrigger = false;

            H51RunwayLightSwitch control =
                root.GetComponent<H51RunwayLightSwitch>();
            if (!control)
                control = Undo.AddComponent<H51RunwayLightSwitch>(root.gameObject);

            if (!control)
            {
                Debug.LogError(
                    "Step 152 could not create the runway-light switch after adding its required BoxCollider.",
                    root.gameObject);
                return null;
            }

            control.Configure(controller,text);
            return control;
        }

        static void PositionRunwayControlInsideAirport(
            Transform control,
            Transform runway)
        {
            if (!control || !runway) return;

            Vector3 fallback =
                runway.TransformPoint(new Vector3(-23.5f,0f,-505f));
            control.position = fallback;
            control.rotation = runway.rotation;

            GameObject airport = GameObject.Find(AirportRootName);
            if (!airport) return;

            if (!TryFindInteriorWallPlacement(
                    airport,
                    out Renderer wall,
                    out Vector3 innerSurface,
                    out Vector3 inward))
                return;

            float y = Mathf.Clamp(
                wall.bounds.min.y + 1.35f,
                wall.bounds.min.y + .75f,
                wall.bounds.max.y - .45f);

            control.position =
                new Vector3(innerSurface.x,y,innerSurface.z) +
                inward * .62f;

            // The cabinet indicator/text are authored on local -Z.
            // Point local +Z outward so the readable face points into the airport.
            control.rotation =
                Quaternion.LookRotation(-inward,Vector3.up);
        }

        static bool TryFindInteriorWallPlacement(
            GameObject airport,
            out Renderer wall,
            out Vector3 innerSurface,
            out Vector3 inward)
        {
            wall = null;
            innerSurface = Vector3.zero;
            inward = Vector3.zero;
            if (!airport) return false;

            GameObject hangar = GameObject.Find(HangarRootName);
            Vector3 interiorReference = hangar
                ? hangar.transform.position
                : airport.transform.position;

            Renderer[] renderers =
                airport.GetComponentsInChildren<Renderer>(true);

            float bestSqr = float.PositiveInfinity;
            for (int i=0;i<renderers.Length;i++)
            {
                Renderer candidate = renderers[i];
                if (!candidate) continue;

                string n = candidate.name;
                bool isWall =
                    n.IndexOf("Perimeter Wall",StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Airport Wall",StringComparison.OrdinalIgnoreCase) >= 0;
                if (!isWall) continue;

                Vector3 closest =
                    candidate.bounds.ClosestPoint(interiorReference);
                Vector3 planar =
                    Vector3.ProjectOnPlane(
                        closest - interiorReference,
                        Vector3.up);
                float sqr = planar.sqrMagnitude;

                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                wall = candidate;
                innerSurface = closest;
            }

            if (!wall) return false;

            inward =
                Vector3.ProjectOnPlane(
                    interiorReference - innerSurface,
                    Vector3.up);
            if (inward.sqrMagnitude < .01f)
            {
                inward =
                    Vector3.ProjectOnPlane(
                        interiorReference - wall.bounds.center,
                        Vector3.up);
            }
            if (inward.sqrMagnitude < .01f)
                return false;

            inward.Normalize();

            Vector3 surfaceProbe =
                new Vector3(
                    interiorReference.x,
                    wall.bounds.center.y,
                    interiorReference.z);
            Vector3 closestSurface =
                wall.bounds.ClosestPoint(surfaceProbe);

            innerSurface =
                new Vector3(
                    closestSurface.x,
                    wall.bounds.center.y,
                    closestSurface.z);
            return true;
        }

        static void BuildLamp(
            Transform parent,
            string name,
            Vector3 position,
            float height,
            float range,
            float intensity,
            Color color,
            H51UtilityGridController grid,
            float demand,
            float realtimeDistance,
            Material poleMaterial,
            Material lampMaterial)
        {
            Transform root = New(name,parent);
            root.position = position;

            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Undo.RegisterCreatedObjectUndo(pole,"Create night-light pole");
            pole.name = "Lamp Pole";
            pole.transform.SetParent(root,false);
            pole.transform.localPosition = Vector3.up * height * .5f;
            pole.transform.localScale = new Vector3(.10f,height * .5f,.10f);
            Renderer poleRenderer = pole.GetComponent<Renderer>();
            if (poleRenderer) poleRenderer.sharedMaterial = poleMaterial;
            Collider poleCollider = pole.GetComponent<Collider>();
            if (poleCollider) Undo.DestroyObjectImmediate(poleCollider);

            GameObject lens = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Undo.RegisterCreatedObjectUndo(lens,"Create night-light lens");
            lens.name = "Lamp Lens";
            lens.transform.SetParent(root,false);
            lens.transform.localPosition = Vector3.up * height;
            lens.transform.localScale =
                Vector3.one * (height >= 7f ? .58f : .44f);
            Renderer lensRenderer = lens.GetComponent<Renderer>();
            if (lensRenderer) lensRenderer.sharedMaterial = lampMaterial;
            Collider lensCollider = lens.GetComponent<Collider>();
            if (lensCollider) Undo.DestroyObjectImmediate(lensCollider);

            Light light = Undo.AddComponent<Light>(lens);
            light.type = LightType.Point;
            light.range = range;
            light.intensity = intensity;
            light.color = color;
            light.shadows = LightShadows.None;
            light.enabled = false;

            H51NightLight schedule = Undo.AddComponent<H51NightLight>(root.gameObject);
            schedule.Configure(
                light,
                lensRenderer ? new[] { lensRenderer } : Array.Empty<Renderer>(),
                grid,
                demand,
                realtimeDistance,
                color);
            schedule.ConfigureFlightVisibility(
                realtimeDistance,
                height >= 7f ? 13f : 11f,
                range,
                intensity);
        }

        static void BuildWallLight(
            Transform parent,
            string name,
            Vector3 position,
            H51UtilityGridController grid,
            float demand,
            Material lampMaterial)
        {
            Transform root = New(name,parent);
            root.position = position;

            GameObject lens = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Undo.RegisterCreatedObjectUndo(lens,"Create building night light");
            lens.name = "Exterior Lamp Lens";
            lens.transform.SetParent(root,false);
            lens.transform.localScale = Vector3.one * .34f;
            Renderer renderer = lens.GetComponent<Renderer>();
            if (renderer) renderer.sharedMaterial = lampMaterial;
            Collider collider = lens.GetComponent<Collider>();
            if (collider) Undo.DestroyObjectImmediate(collider);

            Light light = Undo.AddComponent<Light>(lens);
            light.type = LightType.Point;
            light.range = 16f;
            light.intensity = 1.8f;
            light.color = new Color(1f,.76f,.40f);
            light.shadows = LightShadows.None;
            light.enabled = false;

            H51NightLight schedule = Undo.AddComponent<H51NightLight>(root.gameObject);
            schedule.Configure(
                light,
                renderer ? new[] { renderer } : Array.Empty<Renderer>(),
                grid,
                demand,
                300f,
                light.color);
            schedule.ConfigureFlightVisibility(
                300f,
                10f,
                16f,
                1.8f);
        }

        static Transform BuildMoonVisual(Transform parent)
        {
            Transform root = New("H51 Visible Moon",parent);

            GameObject sphere =
                GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Undo.RegisterCreatedObjectUndo(
                sphere,
                "Create visible Hanger 51 moon");
            sphere.name = "Moon Disc";
            sphere.transform.SetParent(root,false);
            sphere.transform.localPosition = Vector3.zero;
            sphere.transform.localRotation = Quaternion.identity;
            sphere.transform.localScale = Vector3.one;

            Collider collider = sphere.GetComponent<Collider>();
            if (collider)
                Undo.DestroyObjectImmediate(collider);

            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader)
                shader = Shader.Find("Unlit/Color");
            if (!shader)
                shader = Shader.Find("Standard");

            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(MoonMaterialPath);
            if (!material)
            {
                material = new Material(shader)
                {
                    name = "H51MoonDisc"
                };
                AssetDatabase.CreateAsset(material,MoonMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            Color moonColor = new Color(.88f,.92f,1f,1f);
            material.color = moonColor;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor",moonColor);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color",moonColor);
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor",moonColor * 3f);
                material.EnableKeyword("_EMISSION");
            }
            EditorUtility.SetDirty(material);

            Renderer renderer = sphere.GetComponent<Renderer>();
            if (renderer)
                renderer.sharedMaterial = material;

            root.gameObject.SetActive(false);
            return root;
        }

        static Light BuildMoon(Transform parent)
        {
            Transform root = New("H51 Moon Directional Light",parent);
            Light moon = Undo.AddComponent<Light>(root.gameObject);
            moon.type = LightType.Directional;
            moon.intensity = .10f;
            moon.color = new Color(.55f,.68f,1f);
            moon.shadows = LightShadows.None;
            moon.enabled = false;
            return moon;
        }

        static Light ResolveSun()
        {
            if (RenderSettings.sun) return RenderSettings.sun;

            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Light best = null;
            float intensity = -1f;
            for (int i=0;i<lights.Length;i++)
            {
                Light candidate = lights[i];
                if (!candidate || candidate.type != LightType.Directional) continue;
                if (candidate.intensity <= intensity) continue;
                intensity = candidate.intensity;
                best = candidate;
            }
            if (best) RenderSettings.sun = best;
            return best;
        }

        static bool NearUsefulAnchor(
            Vector3 position,
            H51CitizenAnchor[] anchors,
            float distance)
        {
            if (anchors == null || anchors.Length == 0) return true;
            float sqr = distance * distance;
            for (int i=0;i<anchors.Length;i++)
            {
                H51CitizenAnchor anchor = anchors[i];
                if (!anchor) continue;
                Vector3 delta = anchor.transform.position - position;
                delta.y = 0f;
                if (delta.sqrMagnitude <= sqr) return true;
            }
            return false;
        }

        static bool TooClose(Vector3 position,List<Vector3> selected,float distance)
        {
            float sqr = distance * distance;
            for (int i=0;i<selected.Count;i++)
            {
                Vector3 delta = selected[i] - position;
                delta.y = 0f;
                if (delta.sqrMagnitude < sqr) return true;
            }
            return false;
        }

        static Vector3 NearestNodeDirection(IReadOnlyList<Vector3> nodes,int index)
        {
            if (nodes == null || index < 0 || index >= nodes.Count)
                return Vector3.forward;

            Vector3 origin = nodes[index];
            Vector3 best = Vector3.zero;
            float bestSqr = float.PositiveInfinity;

            for (int i=0;i<nodes.Count;i++)
            {
                if (i == index) continue;
                Vector3 delta = nodes[i] - origin;
                delta.y = 0f;
                float sqr = delta.sqrMagnitude;
                if (sqr < 9f || sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = delta.normalized;
            }
            return best;
        }

        static float GroundY(Terrain terrain,Vector3 worldPosition)
        {
            if (!terrain) return worldPosition.y;
            return terrain.SampleHeight(worldPosition) + terrain.transform.position.y;
        }

        static Transform New(string name,Transform parent)
        {
            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go,"Create " + name);
            go.transform.SetParent(parent,false);
            return go.transform;
        }

        static Transform FindChild(Transform root,string name)
        {
            if (!root) return null;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i=0;i<all.Length;i++)
                if (all[i] && all[i].name == name) return all[i];
            return null;
        }

        static Transform DirectChild(Transform root,string name)
        {
            if (!root) return null;
            for (int i=0;i<root.childCount;i++)
            {
                Transform child = root.GetChild(i);
                if (child && child.name == name) return child;
            }
            return null;
        }

        static Material CreateMaterial(
            string path,
            Color baseColor,
            float metallic,
            float smoothness,
            Color emission)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) shader = Shader.Find("Standard");

            if (!material)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material,path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.color = baseColor;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor",baseColor);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color",baseColor);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic",metallic);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness",smoothness);
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness",smoothness);
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor",emission);
                if (emission.maxColorComponent > .001f)
                    material.EnableKeyword("_EMISSION");
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) ||
                folderPath == "Assets" ||
                AssetDatabase.IsValidFolder(folderPath))
                return;

            string parent =
                Path.GetDirectoryName(folderPath)?.Replace("\\","/");
            string folder = Path.GetFileName(folderPath);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(folder))
                return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent,folder);
        }
    }
}
