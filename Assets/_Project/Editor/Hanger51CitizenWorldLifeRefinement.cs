using System;
using System.Collections.Generic;
using System.Linq;
using Hanger51.NPCs;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51CitizenWorldLifeRefinement
    {
        const string WorldName = "Hanger 51 Surrounding Countryside";
        const string CitizenRootName = "Hanger 51 Citizen System";
        const string Generated = "Assets/_Project/Characters/H51Citizens/Generated/WorldLifeRefinement";
        const string TextShaderPath = "Assets/_Project/Shaders/Hanger51DepthWorldText.shader";

        [MenuItem("Hanger 51/World/Current/140 - Refine Citizen Workplaces World Details And Relationships")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("Step 140 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            Hanger51CitizenCleanupFinalizer.Build();

            GameObject world = Find(WorldName);
            Terrain terrain = FindTerrain();
            Transform citizenRoot = world ? FindChild(world.transform, CitizenRootName) : null;
            if (!world || !terrain || !citizenRoot)
            {
                Debug.LogError("Step 140 failed. The countryside/citizen system was not found.");
                return;
            }

            try
            {
                Ensure(Generated + "/Materials");

                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Fixing world-space text depth", .08f);
                int text = FixAllWorldTextDepth();

                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Clearing garage approaches and home frontage", .20f);
                int fences = RepairGarageFences(world.transform);

                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Moving bus stops safely off the road", .32f);
                int busStops = RepairBusStops(world.transform, terrain);

                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Aligning job stations with real furniture and floors", .48f);
                int activitySpots = RepairActivitySpots(world.transform);

                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Adding farmyard work routines", .62f);
                int farmTasks = AddFarmyardActivities(world.transform, terrain);

                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Grounding workers and citizen cars", .76f);
                int citizens = RepairCitizenAgentOffsets(citizenRoot);

                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Saving citizen-life refinement", .92f);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveOpenScenes();

                Debug.Log(
                    $"Step 140 complete. depth-corrected world text={text}, garage-side fences repaired={fences}, bus stops repaired={busStops}, work activity spots aligned={activitySpots}, new farmyard work activities={farmTasks}, citizen agent offsets checked={citizens}. Work routines now walk to their station, face it, and perform repeated job interactions. Relationship actions are available in conversation. Run Step 141.",
                    world);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Hanger 51/World/Current/141 - Validate Citizen Workplaces World Details And Relationships")]
        public static void Validate()
        {
            GameObject world = Find(WorldName);
            Terrain terrain = FindTerrain();
            Transform citizenRoot = world ? FindChild(world.transform, CitizenRootName) : null;
            if (!world || !terrain || !citizenRoot)
            {
                Debug.LogError("Step 141 failed. Run Step 140 first.");
                return;
            }

            Shader expected = AssetDatabase.LoadAssetAtPath<Shader>(TextShaderPath);
            int badText = 0;
            TextMesh[] texts = UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i=0;i<texts.Length;i++)
            {
                TextMesh tm = texts[i];
                if (!tm || !tm.gameObject.scene.IsValid()) continue;
                Renderer r = tm.GetComponent<Renderer>();
                if (!r || !r.sharedMaterial || r.sharedMaterial.shader != expected) badText++;
            }

            int badBusStops = 0;
            foreach (Transform t in world.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Roadside Bus Stop", StringComparison.Ordinal)) continue;
                Transform marker = FindChild(t, "H51 Bus Stop Life Polish");
                Transform sign = FindChild(t, "Route Sign");
                if (!marker || !sign || sign.localPosition.z < 1.45f) badBusStops++;
            }

            int blockedGarages = 0;
            foreach (Transform garage in world.GetComponentsInChildren<Transform>(true))
            {
                if (garage.name != "Detached Garage") continue;
                Transform detail = garage.parent;
                if (!detail) continue;
                Transform fence = garage.localPosition.x >= 0f
                    ? DirectChild(detail, "Right Fence")
                    : DirectChild(detail, "Left Fence");
                if (!fence) continue;
                if (Mathf.Abs(fence.localPosition.x - garage.localPosition.x) < 3.7f)
                    blockedGarages++;
            }

            int badActivityFloor = 0;
            H51CitizenActivitySpot[] spots = world.GetComponentsInChildren<H51CitizenActivitySpot>(true);
            H51EnterableBuilding[] buildings = world.GetComponentsInChildren<H51EnterableBuilding>(true);
            for (int i=0;i<spots.Length;i++)
            {
                H51EnterableBuilding building = FindNearestBuilding(buildings, spots[i].transform.position, 45f);
                if (!building) continue;
                Transform floor = FindChild(building.transform, "Interior Floor");
                Renderer fr = floor ? floor.GetComponent<Renderer>() : null;
                if (fr && Mathf.Abs(spots[i].transform.position.y - (fr.bounds.max.y + .03f)) > .16f)
                    badActivityFloor++;
            }

            int farmOutdoor = 0;
            foreach (H51CitizenActivitySpot spot in spots)
                if (spot && spot.name.StartsWith("Outdoor Farm Activity", StringComparison.Ordinal))
                    farmOutdoor++;

            H51CitizenVehicle[] cars = citizenRoot.GetComponentsInChildren<H51CitizenVehicle>(true);
            int staticCarParts = 0;
            for (int i=0;i<cars.Length;i++)
            {
                Transform[] carParts = cars[i].GetComponentsInChildren<Transform>(true);
                for (int p=0;p<carParts.Length;p++)
                    if (carParts[p] && carParts[p] != cars[i].transform && carParts[p].gameObject.isStatic)
                        staticCarParts++;
            }

            H51CitizenDialogue[] dialogues = citizenRoot.GetComponentsInChildren<H51CitizenDialogue>(true);
            bool ok =
                badText == 0 &&
                badBusStops == 0 &&
                blockedGarages == 0 &&
                badActivityFloor == 0 &&
                farmOutdoor >= 3 &&
                cars.Length > 0 &&
                staticCarParts == 0 &&
                dialogues.Length > 0;

            if (ok)
            {
                Debug.Log(
                    $"Step 141 passed. through-wall text failures=0, bus-stop placement/sign failures=0, garage fence conflicts=0, work-floor alignment failures=0, farmyard activities={farmOutdoor}, citizen cars={cars.Length}, static car visual parts=0, interactive citizens={dialogues.Length}.",
                    world);
            }
            else
            {
                Debug.LogError(
                    $"Step 141 failed. bad world text={badText}, bad bus stops={badBusStops}, garage fence conflicts={blockedGarages}, bad work-floor alignment={badActivityFloor}, farmyard activities={farmOutdoor}, citizen cars={cars.Length}, static car parts={staticCarParts}, dialogues={dialogues.Length}.",
                    world);
            }
        }

        static int FixAllWorldTextDepth()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(TextShaderPath);
            if (!shader) shader = Shader.Find("Hanger51/Depth World Text");
            if (!shader) return 0;

            Dictionary<Font,Material> materials = new Dictionary<Font,Material>();
            int fixedCount = 0;
            TextMesh[] texts = UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i=0;i<texts.Length;i++)
            {
                TextMesh tm = texts[i];
                if (!tm || !tm.gameObject.scene.IsValid()) continue;
                Renderer renderer = tm.GetComponent<Renderer>();
                if (!renderer) continue;

                Font font = tm.font ? tm.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (!materials.TryGetValue(font, out Material mat) || !mat)
                {
                    string safe = Safe(font ? font.name : "Legacy");
                    string path = Generated + "/Materials/DepthText_" + safe + ".mat";
                    mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (!mat)
                    {
                        mat = new Material(shader) { name = "Depth Text " + safe };
                        AssetDatabase.CreateAsset(mat,path);
                    }
                    else if (mat.shader != shader) mat.shader = shader;

                    if (font && font.material && font.material.mainTexture)
                        mat.SetTexture("_MainTex",font.material.mainTexture);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color",Color.white);
                    if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff",.02f);
                    mat.renderQueue = 2450;
                    EditorUtility.SetDirty(mat);
                    materials[font] = mat;
                }

                renderer.sharedMaterial = mat;
                renderer.sortingOrder = 0;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                EditorUtility.SetDirty(renderer);
                fixedCount++;
            }

            return fixedCount;
        }

        static int RepairGarageFences(Transform world)
        {
            int fixedCount = 0;
            Transform[] all = world.GetComponentsInChildren<Transform>(true);
            for (int i=0;i<all.Length;i++)
            {
                Transform garage = all[i];
                if (!garage || garage.name != "Detached Garage" || !garage.parent) continue;

                Transform detail = garage.parent;
                bool rightSide = garage.localPosition.x >= 0f;
                Transform fence = DirectChild(detail, rightSide ? "Right Fence" : "Left Fence");
                if (!fence) continue;

                float sign = rightSide ? 1f : -1f;
                float desiredX = garage.localPosition.x + sign * 4.35f;
                Vector3 p = fence.localPosition;
                if (Mathf.Abs(p.x - desiredX) > .02f)
                {
                    p.x = desiredX;
                    fence.localPosition = p;
                    fixedCount++;
                    EditorUtility.SetDirty(fence);
                }
            }
            return fixedCount;
        }

        static int RepairBusStops(Transform world, Terrain terrain)
        {
            int fixedCount = 0;
            Transform[] all = world.GetComponentsInChildren<Transform>(true);
            for (int i=0;i<all.Length;i++)
            {
                Transform stop = all[i];
                if (!stop || !stop.name.StartsWith("Roadside Bus Stop", StringComparison.Ordinal)) continue;

                Transform marker = DirectChild(stop,"H51 Bus Stop Life Polish");
                if (!marker)
                {
                    stop.position -= stop.forward * 5.5f;
                    Vector3 wp = stop.position;
                    wp.y = terrain.SampleHeight(wp) + terrain.transform.position.y;
                    stop.position = wp;
                    marker = New("H51 Bus Stop Life Polish",stop);
                }

                Transform pole = FindChild(stop,"Route Sign Pole");
                Transform sign = FindChild(stop,"Route Sign");
                if (pole) pole.localPosition = new Vector3(3.35f,1.45f,1.75f);
                if (sign)
                {
                    sign.localPosition = new Vector3(3.35f,2.48f,1.75f);
                    sign.localScale = new Vector3(1.15f,1.0f,.10f);
                }

                TextMesh[] labels = stop.GetComponentsInChildren<TextMesh>(true);
                for (int l=0;l<labels.Length;l++)
                {
                    if (!labels[l] || labels[l].text != "BUS") continue;
                    labels[l].transform.localPosition = new Vector3(3.35f,2.48f,1.82f);
                }

                fixedCount++;
                EditorUtility.SetDirty(stop);
            }
            return fixedCount;
        }

        static int RepairActivitySpots(Transform world)
        {
            H51EnterableBuilding[] buildings = world.GetComponentsInChildren<H51EnterableBuilding>(true);
            H51CitizenActivitySpot[] spots = world.GetComponentsInChildren<H51CitizenActivitySpot>(true);
            int repaired = 0;

            for (int i=0;i<spots.Length;i++)
            {
                H51CitizenActivitySpot spot = spots[i];
                if (!spot) continue;

                H51EnterableBuilding building = FindNearestBuilding(buildings,spot.transform.position,45f);
                if (!building) continue;

                Transform floor = FindChild(building.transform,"Interior Floor");
                Renderer floorRenderer = floor ? floor.GetComponent<Renderer>() : null;
                float floorY = floorRenderer ? floorRenderer.bounds.max.y + .03f : spot.transform.position.y;

                Transform target = FindBestWorkTarget(building,spot.ActivityLabel,spot.transform.position);
                Vector3 position = spot.transform.position;
                Vector3 facing = building.transform.forward;

                if (target)
                {
                    Renderer r = target.GetComponent<Renderer>();
                    Bounds bounds = r ? r.bounds : new Bounds(target.position,Vector3.one);
                    Vector3 targetPos = bounds.center;
                    Vector3 direction;

                    string label = spot.ActivityLabel.ToLowerInvariant();
                    bool counterTask =
                        label.Contains("counter") ||
                        label.Contains("reception") ||
                        label.Contains("order");

                    if (counterTask && building.Entrance)
                        direction = targetPos - building.Entrance.position;
                    else
                    {
                        Vector3 center = building.InteriorCenter
                            ? building.InteriorCenter.position
                            : building.transform.position;
                        direction = center - targetPos;
                    }

                    direction.y = 0f;
                    if (direction.sqrMagnitude < .01f) direction = -building.transform.forward;
                    direction.Normalize();

                    float clearance = Mathf.Max(bounds.extents.x,bounds.extents.z) + .72f;
                    position = targetPos + direction * clearance;
                    position.y = floorY;

                    Vector3 look = targetPos - position;
                    look.y = 0f;
                    if (look.sqrMagnitude > .01f) facing = look.normalized;
                }
                else
                {
                    position.y = floorY;
                }

                if (NavMesh.SamplePosition(position,out NavMeshHit hit,3.0f,NavMesh.AllAreas))
                    position = new Vector3(hit.position.x, floorY, hit.position.z);

                spot.transform.position = position;
                if (facing.sqrMagnitude > .01f)
                    spot.transform.rotation = Quaternion.LookRotation(facing,Vector3.up);

                EditorUtility.SetDirty(spot);
                repaired++;
            }

            return repaired;
        }

        static Transform FindBestWorkTarget(H51EnterableBuilding building, string label, Vector3 from)
        {
            if (!building) return null;
            string lower = (label ?? "").ToLowerInvariant();
            string[] keys;

            if (lower.Contains("counter") || lower.Contains("order") || lower.Contains("reception"))
                keys = new[]{"Counter","Register","Reception"};
            else if (lower.Contains("shelf") || lower.Contains("inventory") || lower.Contains("suppl"))
                keys = new[]{"Shelf","Storage","Cabinet","Parts Bin"};
            else if (lower.Contains("tool") || lower.Contains("equipment") || lower.Contains("service") || lower.Contains("mechanic"))
                keys = new[]{"Workbench","Service Mat","Tool","Equipment","Parts"};
            else if (lower.Contains("record") || lower.Contains("paper") || lower.Contains("mail"))
                keys = new[]{"Desk","Records","Public Counter","Mail"};
            else if (lower.Contains("farm") || lower.Contains("feed") || lower.Contains("chore"))
                keys = new[]{"Farm","Feed","Tool","Workbench","Parts Bin"};
            else if (lower.Contains("exam") || lower.Contains("medical"))
                keys = new[]{"Exam","Medical","Reception"};
            else if (lower.Contains("table") || lower.Contains("clean"))
                keys = new[]{"Table","Cafe Counter"};
            else
                keys = new[]{"Workbench","Counter","Desk","Shelf"};

            Transform best = null;
            float bestD = float.PositiveInfinity;
            Transform[] all = building.GetComponentsInChildren<Transform>(true);
            for (int i=0;i<all.Length;i++)
            {
                Transform t = all[i];
                if (!t || !t.GetComponent<Renderer>()) continue;

                bool matches = false;
                for (int k=0;k<keys.Length;k++)
                    if (t.name.IndexOf(keys[k],StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matches = true;
                        break;
                    }
                if (!matches) continue;

                float d = Planar(t.position,from);
                if (d >= bestD) continue;
                bestD = d;
                best = t;
            }
            return best;
        }

        static int AddFarmyardActivities(Transform world, Terrain terrain)
        {
            H51CitizenAnchor[] anchors = world.GetComponentsInChildren<H51CitizenAnchor>(true);
            H51EnterableBuilding[] buildings = world.GetComponentsInChildren<H51EnterableBuilding>(true);
            int made = 0;

            for (int i=0;i<anchors.Length;i++)
            {
                H51CitizenAnchor anchor = anchors[i];
                if (!anchor) continue;

                string occupation = anchor.OccupationHint ?? "";
                bool farm = anchor.Kind == H51CitizenAnchorKind.Farm ||
                            occupation.IndexOf("farm",StringComparison.OrdinalIgnoreCase) >= 0;
                if (!farm) continue;

                H51EnterableBuilding building = FindNearestFarm(buildings,anchor.transform.position,90f);
                if (!building) continue;

                for (int c=anchor.transform.childCount-1;c>=0;c--)
                {
                    Transform child = anchor.transform.GetChild(c);
                    if (child.name.StartsWith("Outdoor Farm Activity",StringComparison.Ordinal))
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                }

                Vector3 front = building.Entrance ? building.Entrance.position : building.transform.position;
                Vector3 right = building.transform.right;
                Vector3 forward = building.transform.forward;

                Vector3[] positions =
                {
                    front + right*7.5f + forward*5.0f,
                    front - right*8.5f + forward*3.0f,
                    front + right*4.0f - forward*8.0f
                };
                string[] labels =
                {
                    "checking the field rows",
                    "inspecting farm equipment",
                    "loading and checking farm supplies"
                };

                for (int p=0;p<positions.Length;p++)
                {
                    Vector3 pos = positions[p];
                    pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y + .03f;
                    if (NavMesh.SamplePosition(pos,out NavMeshHit hit,8f,NavMesh.AllAreas))
                        pos = hit.position + Vector3.up*.03f;

                    Transform spot = New("Outdoor Farm Activity - " + labels[p],anchor.transform);
                    spot.position = pos;
                    Vector3 look = building.transform.position - pos;
                    look.y = 0f;
                    if (look.sqrMagnitude > .01f) spot.rotation = Quaternion.LookRotation(look.normalized,Vector3.up);

                    H51CitizenActivitySpot activity = spot.gameObject.AddComponent<H51CitizenActivitySpot>();
                    activity.Configure(labels[p],"Interact",7f,16f);
                    made++;
                }
            }

            return made;
        }

        static int RepairCitizenAgentOffsets(Transform citizenRoot)
        {
            NavMeshAgent[] agents = citizenRoot.GetComponentsInChildren<NavMeshAgent>(true);
            int repaired = 0;
            for (int i=0;i<agents.Length;i++)
            {
                NavMeshAgent agent = agents[i];
                if (!agent) continue;
                if (Mathf.Abs(agent.baseOffset) > .02f)
                {
                    agent.baseOffset = 0f;
                    repaired++;
                    EditorUtility.SetDirty(agent);
                }
            }
            return agents.Length;
        }

        static H51EnterableBuilding FindNearestBuilding(H51EnterableBuilding[] buildings, Vector3 p, float max)
        {
            H51EnterableBuilding best = null;
            float bestD = max;
            for (int i=0;i<buildings.Length;i++)
            {
                H51EnterableBuilding b = buildings[i];
                if (!b) continue;
                Vector3 q = b.InteriorCenter ? b.InteriorCenter.position : b.transform.position;
                float d = Planar(p,q);
                if (d >= bestD) continue;
                bestD = d;
                best = b;
            }
            return best;
        }

        static H51EnterableBuilding FindNearestFarm(H51EnterableBuilding[] buildings, Vector3 p, float max)
        {
            H51EnterableBuilding best = null;
            float bestD = max;
            for (int i=0;i<buildings.Length;i++)
            {
                H51EnterableBuilding b = buildings[i];
                if (!b) continue;
                bool farm = b.Kind == H51EnterableBuildingKind.Farm ||
                            b.DisplayName.IndexOf("farm",StringComparison.OrdinalIgnoreCase) >= 0;
                if (!farm) continue;
                float d = Planar(p,b.transform.position);
                if (d >= bestD) continue;
                bestD = d;
                best = b;
            }
            return best;
        }

        static Terrain FindTerrain()
        {
            Terrain[] terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            for (int i=0;i<terrains.Length;i++)
                if (terrains[i] && terrains[i].gameObject.scene.IsValid()) return terrains[i];
            return null;
        }

        static GameObject Find(string name)
        {
            GameObject direct = GameObject.Find(name);
            if (direct) return direct;

            Transform[] all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            for (int i=0;i<all.Length;i++)
                if (all[i] && all[i].gameObject.scene.IsValid() && all[i].name == name)
                    return all[i].gameObject;
            return null;
        }

        static Transform DirectChild(Transform root, string name)
        {
            if (!root) return null;
            for (int i=0;i<root.childCount;i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == name) return child;
            }
            return null;
        }

        static Transform FindChild(Transform root, string name)
        {
            if (!root) return null;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i=0;i<all.Length;i++)
                if (all[i] && all[i].name == name) return all[i];
            return null;
        }

        static Transform New(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            if (parent) go.transform.SetParent(parent,false);
            return go.transform;
        }

        static float Planar(Vector3 a, Vector3 b)
        {
            float x = a.x-b.x;
            float z = a.z-b.z;
            return Mathf.Sqrt(x*x+z*z);
        }

        static string Safe(string value)
        {
            if (string.IsNullOrEmpty(value)) return "Unnamed";
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                value = value.Replace(c,'_');
            return value.Replace(" ","_");
        }

        static void Ensure(string path)
        {
            string[] pieces = path.Split('/');
            string current = pieces[0];
            for (int i=1;i<pieces.Length;i++)
            {
                string next = current + "/" + pieces[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current,pieces[i]);
                current = next;
            }
        }
    }
}
