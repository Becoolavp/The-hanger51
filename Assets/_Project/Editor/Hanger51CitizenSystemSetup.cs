using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Hanger51.NPCs;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51CitizenSystemSetup
    {
        const string WorldName = "Hanger 51 Surrounding Countryside";
        const string TerrainName = "Hanger 51 Editable Terrain";
        const string CitizenRootName = "Hanger 51 Citizen System";

        const string Base = "Assets/_Project/Characters/H51Citizens";
        const string Source = Base + "/Source";
        const string PackAPath = Source + "/PackA/H51_Citizens_Pack_A.fbx";
        const string PackBPath = Source + "/PackB/H51_Citizens_Pack_B.fbx";
        const string Generated = Base + "/Generated";
        const string PrefabFolder = Generated + "/Prefabs";
        const string ControllerFolder = Generated + "/Controllers";
        const string CatalogPath = Generated + "/H51CitizenVisualCatalog.asset";

        static readonly string[] PackAOutfits =
        {
            "Worker","Suit","Adventurer","Punk","Formal","Casual","Soldier"
        };

        static readonly string[] PackBOutfits =
        {
            "Farmer","Worker","Punk","Beach","Casual2","Casual","Suit","Adventurer","Swat"
        };

        static readonly string[] FirstNames =
        {
            "Aaron","Abigail","Adam","Alicia","Andrew","Anna","Ben","Brandon","Caroline","Charles",
            "Claire","Daniel","David","Derek","Emily","Emma","Ethan","Evelyn","Grace","Hannah",
            "Henry","Jack","James","Jason","John","Julia","Kevin","Laura","Leah","Luke",
            "Maria","Matthew","Megan","Michael","Natalie","Nathan","Nicole","Olivia","Patrick","Rachel",
            "Rebecca","Robert","Ryan","Sarah","Scott","Sophia","Thomas","Victoria","William","Zoe",
            "Caleb","Molly","Eric","Jenny","Noah","Brooke","Logan","Erin","Cole","Amanda"
        };

        static readonly string[] LastNames =
        {
            "Anderson","Baker","Bennett","Brown","Campbell","Carter","Clark","Collins","Cooper","Davis",
            "Edwards","Evans","Foster","Garcia","Green","Hall","Harris","Hayes","Hill","Hughes",
            "Jackson","Johnson","Kelly","King","Larson","Lewis","Martin","Miller","Mitchell","Moore",
            "Morgan","Nelson","Parker","Peterson","Phillips","Reed","Reynolds","Roberts","Robinson","Rogers",
            "Scott","Smith","Stewart","Taylor","Thomas","Thompson","Turner","Walker","Ward","Williams",
            "Wilson","Wood","Young","Murphy","Sullivan","Olson","Jensen","Murray","Fischer","Bailey"
        };

        [MenuItem("Hanger 51/World/Current/128 - Import Free Citizen Character Packs")]
        public static void ImportCharacterPacks()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("Step 128 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            Ensure(Source + "/PackA");
            Ensure(Source + "/PackB");

            try
            {
                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Finding the two uploaded character packs", .08f);

                string archiveA = FindArchiveFor(new[] { "All together/FBX/All together.fbx", "H51_Citizens_Pack_A.fbx" });
                if (string.IsNullOrEmpty(archiveA))
                    archiveA = EditorUtility.OpenFilePanel("Select character ZIP containing All together.fbx", DefaultBrowseFolder(), "zip");
                if (string.IsNullOrEmpty(archiveA))
                {
                    Debug.LogError("Step 128 cancelled. Could not locate the first character ZIP.");
                    return;
                }

                string archiveB = ArchiveContainsAny(archiveA, new[] { "All together/FBX/Humans_Master.fbx", "H51_Citizens_Pack_B.fbx" })
                    ? archiveA
                    : FindArchiveFor(new[] { "All together/FBX/Humans_Master.fbx", "H51_Citizens_Pack_B.fbx" });

                if (string.IsNullOrEmpty(archiveB))
                    archiveB = EditorUtility.OpenFilePanel("Select character ZIP containing Humans_Master.fbx", DefaultBrowseFolder(), "zip");
                if (string.IsNullOrEmpty(archiveB))
                {
                    Debug.LogError("Step 128 cancelled. Could not locate the second character ZIP.");
                    return;
                }

                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Extracting Pack A master FBX", .28f);
                if (!ExtractEntry(archiveA, new[] { "All together/FBX/All together.fbx", "H51_Citizens_Pack_A.fbx" }, PackAPath))
                {
                    Debug.LogError("Step 128 failed. The first ZIP did not contain All together.fbx.");
                    return;
                }

                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Extracting Pack B master FBX", .46f);
                if (!ExtractEntry(archiveB, new[] { "All together/FBX/Humans_Master.fbx", "H51_Citizens_Pack_B.fbx" }, PackBPath))
                {
                    Debug.LogError("Step 128 failed. The second ZIP did not contain Humans_Master.fbx.");
                    return;
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Configuring Unity Humanoid import and animation loops", .66f);
                ConfigureModelImporter(PackAPath);
                ConfigureModelImporter(PackBPath);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                int clipsA = AnimationClips(PackAPath).Count;
                int clipsB = AnimationClips(PackBPath).Count;
                GameObject modelA = AssetDatabase.LoadAssetAtPath<GameObject>(PackAPath);
                GameObject modelB = AssetDatabase.LoadAssetAtPath<GameObject>(PackBPath);

                bool ok = modelA && modelB && clipsA >= 10 && clipsB >= 10;
                if (ok)
                {
                    Debug.Log($"Step 128 complete. Imported both free modular citizen packs as Humanoid FBXs. Pack A clips={clipsA}, Pack B clips={clipsB}. Run Step 129 next.");
                }
                else
                {
                    Debug.LogError($"Step 128 imported the files but validation was incomplete. Pack A model={(modelA ? "yes" : "NO")}, clips={clipsA}; Pack B model={(modelB ? "yes" : "NO")}, clips={clipsB}.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Debug.LogError("Step 128 failed while importing the free citizen packs: " + ex.Message);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Hanger 51/World/Current/129 - Build Citizen Visual Library")]
        public static void BuildVisualLibrary()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("Step 129 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            GameObject modelA = AssetDatabase.LoadAssetAtPath<GameObject>(PackAPath);
            GameObject modelB = AssetDatabase.LoadAssetAtPath<GameObject>(PackBPath);
            if (!modelA || !modelB)
            {
                Debug.LogError("Step 129 failed. Run Step 128 first so both character FBXs are inside the project.");
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Resetting generated citizen library", .05f);
                if (AssetDatabase.IsValidFolder(Generated)) AssetDatabase.DeleteAsset(Generated);
                Ensure(PrefabFolder);
                Ensure(ControllerFolder);
                Ensure(Generated + "/Materials");

                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Building Pack A civilian animator", .15f);
                RuntimeAnimatorController controllerA = BuildController(PackAPath, ControllerFolder + "/H51_Citizen_PackA.controller");

                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Building Pack B civilian animator", .25f);
                RuntimeAnimatorController controllerB = BuildController(PackBPath, ControllerFolder + "/H51_Citizen_PackB.controller");

                H51CitizenVisualCatalog catalog = ScriptableObject.CreateInstance<H51CitizenVisualCatalog>();
                catalog.name = "H51 Citizen Visual Catalog";

                int total = PackAOutfits.Length + PackBOutfits.Length;
                int made = 0;

                foreach (string outfit in PackAOutfits)
                {
                    EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Building Pack A " + outfit, .30f + .55f * made / Mathf.Max(1f,total));
                    GameObject prefab = BuildOutfitPrefab(modelA, "A", outfit, controllerA);
                    if (prefab)
                    {
                        catalog.entries.Add(new H51CitizenVisualCatalog.Entry
                        {
                            packId = "A",
                            archetype = outfit,
                            description = "Pack A " + outfit,
                            prefab = prefab
                        });
                    }
                    made++;
                }

                foreach (string outfit in PackBOutfits)
                {
                    EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Building Pack B " + outfit, .30f + .55f * made / Mathf.Max(1f,total));
                    GameObject prefab = BuildOutfitPrefab(modelB, "B", outfit, controllerB);
                    if (prefab)
                    {
                        catalog.entries.Add(new H51CitizenVisualCatalog.Entry
                        {
                            packId = "B",
                            archetype = outfit,
                            description = "Pack B " + outfit,
                            prefab = prefab
                        });
                    }
                    made++;
                }

                AssetDatabase.CreateAsset(catalog, CatalogPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                if (catalog.entries.Count >= 12)
                    Debug.Log($"Step 129 complete. Built {catalog.entries.Count} reusable citizen archetype prefabs with Humanoid idle/walk/run/wave/interact animation controllers. Run Step 130 next.", catalog);
                else
                    Debug.LogError($"Step 129 completed but only {catalog.entries.Count} citizen archetypes were created. Expected at least 12.", catalog);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Hanger 51/World/Current/130 - Populate Towns With Persistent Citizens")]
        public static void PopulateTowns()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("Step 130 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            GameObject world = Find(WorldName);
            Terrain terrain = FindTerrain();
            H51CitizenVisualCatalog catalog = AssetDatabase.LoadAssetAtPath<H51CitizenVisualCatalog>(CatalogPath);
            Transform settlements = world ? FindChild(world.transform, "Settlements") : null;

            if (!world || !terrain || !settlements || settlements.childCount < 1 || !catalog || catalog.entries.Count == 0)
            {
                Debug.LogError("Step 130 failed. Need the current countryside plus the citizen visual library. Run Steps 128 and 129 first.");
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Removing the previous citizen simulation", .03f);
                Transform previous = FindChild(world.transform, CitizenRootName);
                if (previous) UnityEngine.Object.DestroyImmediate(previous.gameObject);

                Transform root = New(CitizenRootName, world.transform);
                H51WorldClock clock = root.gameObject.AddComponent<H51WorldClock>();
                H51CitizenRegistry registry = root.gameObject.AddComponent<H51CitizenRegistry>();
                Transform navigationRoot = New("Citizen Navigation", root);
                Transform anchorsRoot = New("Citizen Anchors", root);
                Transform populationRoot = New("Citizen Population", root);

                List<Transform> towns = new List<Transform>();
                for (int i = 0; i < settlements.childCount; i++)
                {
                    Transform town = settlements.GetChild(i);
                    if (Houses(town).Count > 0) towns.Add(town);
                }

                List<H51CitizenAnchor> allAnchors = new List<H51CitizenAnchor>();
                Dictionary<string,List<H51CitizenAnchor>> homesByTown = new Dictionary<string,List<H51CitizenAnchor>>();
                Dictionary<string,List<H51CitizenAnchor>> worksByTown = new Dictionary<string,List<H51CitizenAnchor>>();

                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Building home, work, shopping and leisure anchors", .16f);
                for (int i = 0; i < towns.Count; i++)
                {
                    Transform town = towns[i];
                    BuildTownAnchors(world.transform, terrain, anchorsRoot, town, i, allAnchors, homesByTown, worksByTown);
                }

                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Baking local NavMeshes for the towns", .34f);
                List<NavMeshSurface> surfaces = new List<NavMeshSurface>();
                for (int i = 0; i < towns.Count; i++)
                {
                    NavMeshSurface surface = BuildTownNavMesh(navigationRoot, towns[i], i);
                    if (surface) surfaces.Add(surface);
                }

                EditorUtility.DisplayProgressBar("Hanger 51 Citizens", "Creating persistent named residents", .58f);
                int created = 0;
                HashSet<string> usedNames = new HashSet<string>();
                Dictionary<string,int> workLoads = new Dictionary<string,int>();

                for (int t = 0; t < towns.Count; t++)
                {
                    Transform town = towns[t];
                    string townName = town.name;
                    int desired = t == 0 ? 20 : 8;

                    if (!homesByTown.TryGetValue(townName, out List<H51CitizenAnchor> homes) || homes.Count == 0) continue;
                    worksByTown.TryGetValue(townName, out List<H51CitizenAnchor> works);
                    if (works == null) works = new List<H51CitizenAnchor>();

                    Transform townPopulation = New(townName + " Residents", populationRoot);
                    System.Random rng = new System.Random(51600 + t * 1009);

                    for (int i = 0; i < desired; i++)
                    {
                        H51CitizenAnchor home = homes[i % homes.Count];
                        H51CitizenAnchor work = PickLeastLoadedWork(works, workLoads, rng);
                        string occupation = work ? work.OccupationHint : "Resident";
                        H51CitizenVisualCatalog.Entry visual = PickVisualForOccupation(catalog, occupation, created, t);
                        if (visual == null || !visual.prefab) continue;

                        string displayName = UniqueName(usedNames, rng);
                        int seed = Math.Abs((displayName + townName + i).GetHashCode());
                        int age = rng.Next(19, 76);

                        GameObject citizen = PrefabUtility.InstantiatePrefab(visual.prefab) as GameObject;
                        if (!citizen) continue;
                        citizen.name = "Citizen - " + displayName + " - " + occupation;
                        citizen.transform.SetParent(townPopulation, false);

                        Vector3 spawn = home.transform.position + RandomXZ(rng, 1.2f, 3.5f);
                        if (NavMesh.SamplePosition(spawn, out NavMeshHit hit, 15f, NavMesh.AllAreas)) spawn = hit.position;
                        else spawn.y = Ground(terrain, spawn);
                        citizen.transform.position = spawn;
                        citizen.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);

                        float heightVariation = Mathf.Lerp(.94f, 1.06f, (float)rng.NextDouble());
                        citizen.transform.localScale *= heightVariation;

                        H51CitizenIdentity identity = citizen.GetComponent<H51CitizenIdentity>();
                        H51CitizenVisualVariation variation = citizen.GetComponent<H51CitizenVisualVariation>();
                        NavMeshAgent agent = citizen.GetComponent<NavMeshAgent>();

                        string citizenId = "H51-" + t.ToString("00") + "-" + i.ToString("000");
                        if (identity)
                        {
                            identity.Configure(
                                citizenId, displayName, age, townName, occupation,
                                home.AnchorId, work ? work.AnchorId : "",
                                visual.packId + "-" + visual.archetype, seed,
                                (float)rng.NextDouble(), (float)rng.NextDouble(),
                                (float)rng.NextDouble(), (float)rng.NextDouble());
                        }

                        if (variation) variation.Configure(seed, 1f);
                        if (agent)
                        {
                            agent.speed = Mathf.Lerp(1.35f, 1.85f, (float)rng.NextDouble());
                            agent.acceleration = 7f;
                            agent.angularSpeed = 320f;
                            agent.radius = .32f;
                            agent.height = 1.8f;
                            agent.stoppingDistance = .65f;
                            agent.avoidancePriority = 25 + (seed % 50);
                        }

                        created++;
                    }
                }

                registry.RefreshSceneReferences();
                EditorUtility.SetDirty(clock);
                EditorUtility.SetDirty(registry);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveOpenScenes();
                Selection.activeGameObject = root.gameObject;

                Debug.Log($"Step 130 complete. Created {created} persistent named citizens across {towns.Count} towns, anchors={allAnchors.Count}, local NavMesh surfaces={surfaces.Count}. Citizens now have homes, jobs, schedules, personalities, player relationship state and persistent memory storage. Run Step 131 to validate.", root.gameObject);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Hanger 51/World/Current/131 - Validate Citizen System")]
        public static void ValidateCitizenSystem()
        {
            GameObject world = Find(WorldName);
            Transform root = world ? FindChild(world.transform, CitizenRootName) : null;
            H51CitizenVisualCatalog catalog = AssetDatabase.LoadAssetAtPath<H51CitizenVisualCatalog>(CatalogPath);

            if (!world || !root || !catalog)
            {
                Debug.LogError("Step 131 failed. Run Steps 128-130 first.");
                return;
            }

            H51CitizenIdentity[] citizens = root.GetComponentsInChildren<H51CitizenIdentity>(true);
            H51CitizenAnchor[] anchors = root.GetComponentsInChildren<H51CitizenAnchor>(true);
            NavMeshSurface[] surfaces = root.GetComponentsInChildren<NavMeshSurface>(true);
            H51CitizenRegistry registry = root.GetComponent<H51CitizenRegistry>();
            H51WorldClock clock = root.GetComponent<H51WorldClock>();

            int missingAgent = 0, missingAnimator = 0, missingCollider = 0, missingBrain = 0, badHome = 0, badWork = 0;
            HashSet<string> ids = new HashSet<string>();
            HashSet<string> names = new HashSet<string>();
            HashSet<string> anchorIds = new HashSet<string>();

            for (int i = 0; i < anchors.Length; i++)
                if (anchors[i] && !string.IsNullOrEmpty(anchors[i].AnchorId)) anchorIds.Add(anchors[i].AnchorId);

            int duplicateIds = 0, duplicateNames = 0;
            for (int i = 0; i < citizens.Length; i++)
            {
                H51CitizenIdentity c = citizens[i];
                if (!c) continue;
                if (!ids.Add(c.CitizenId)) duplicateIds++;
                if (!names.Add(c.DisplayName)) duplicateNames++;
                if (!c.GetComponent<NavMeshAgent>()) missingAgent++;
                if (!c.GetComponentInChildren<Animator>(true)) missingAnimator++;
                if (!c.GetComponent<CapsuleCollider>()) missingCollider++;
                if (!c.GetComponent<H51CitizenBrain>()) missingBrain++;
                if (string.IsNullOrEmpty(c.HomeAnchorId) || !anchorIds.Contains(c.HomeAnchorId)) badHome++;
                if (!string.IsNullOrEmpty(c.WorkAnchorId) && !anchorIds.Contains(c.WorkAnchorId)) badWork++;
            }

            int surfacesWithData = 0;
            for (int i = 0; i < surfaces.Length; i++)
                if (surfaces[i] && surfaces[i].navMeshData) surfacesWithData++;

            int clipA = AnimationClips(PackAPath).Count;
            int clipB = AnimationClips(PackBPath).Count;

            bool ok =
                catalog.entries.Count >= 12 &&
                citizens.Length >= 35 &&
                anchors.Length >= 45 &&
                surfaces.Length >= 4 &&
                surfacesWithData == surfaces.Length &&
                registry && clock &&
                duplicateIds == 0 &&
                duplicateNames == 0 &&
                missingAgent == 0 &&
                missingAnimator == 0 &&
                missingCollider == 0 &&
                missingBrain == 0 &&
                badHome == 0 &&
                badWork == 0 &&
                clipA >= 10 && clipB >= 10;

            if (ok)
            {
                Debug.Log($"Step 131 passed. visual archetypes={catalog.entries.Count}, citizens={citizens.Length}, unique names={names.Count}, anchors={anchors.Length}, NavMesh surfaces={surfacesWithData}, Pack A clips={clipA}, Pack B clips={clipB}, missing agents/animators/colliders/brains=0, bad home/work assignments=0. Persistent identity + memory foundation is ready.", root.gameObject);
            }
            else
            {
                Debug.LogError($"Step 131 failed. visual archetypes={catalog.entries.Count}, citizens={citizens.Length}, anchors={anchors.Length}, surfaces={surfaces.Length}, surfaces with data={surfacesWithData}, duplicate IDs={duplicateIds}, duplicate names={duplicateNames}, missing agents={missingAgent}, animators={missingAnimator}, colliders={missingCollider}, brains={missingBrain}, bad homes={badHome}, bad work={badWork}, Pack A clips={clipA}, Pack B clips={clipB}.", root.gameObject);
            }
        }

        static void ConfigureModelImporter(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (!importer) return;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.importBlendShapes = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.resampleCurves = true;
            importer.optimizeGameObjects = false;
            importer.SaveAndReimport();

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips != null && clips.Length > 0)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    string n = clips[i].name.ToLowerInvariant();
                    bool loop = n.Contains("idle") || n.Contains("walk") || n.Contains("run");
                    clips[i].loopTime = loop;
                    clips[i].loopPose = loop;
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }

        static RuntimeAnimatorController BuildController(string modelPath, string controllerPath)
        {
            List<AnimationClip> clips = AnimationClips(modelPath);
            AnimationClip idle = FindClip(clips, "idle_neutral") ?? FindClip(clips, "idle");
            AnimationClip walk = FindClip(clips, "walk") ?? idle;
            AnimationClip run = FindClip(clips, "|run") ?? FindClip(clips, "run") ?? walk;
            AnimationClip wave = FindClip(clips, "wave") ?? idle;
            AnimationClip interact = FindClip(clips, "interact") ?? wave ?? idle;

            if (!idle) throw new InvalidOperationException("Could not find an Idle clip in " + modelPath);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Wave", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Interact", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState idleState = sm.AddState("Idle");
            AnimatorState walkState = sm.AddState("Walk");
            AnimatorState runState = sm.AddState("Run");
            AnimatorState waveState = sm.AddState("Wave");
            AnimatorState interactState = sm.AddState("Interact");
            sm.defaultState = idleState;

            idleState.motion = idle;
            walkState.motion = walk;
            runState.motion = run;
            waveState.motion = wave;
            interactState.motion = interact;

            AnimatorStateTransition idleWalk = idleState.AddTransition(walkState);
            ConfigureTransition(idleWalk, false, .14f);
            idleWalk.AddCondition(AnimatorConditionMode.Greater, .10f, "Speed");

            AnimatorStateTransition walkIdle = walkState.AddTransition(idleState);
            ConfigureTransition(walkIdle, false, .14f);
            walkIdle.AddCondition(AnimatorConditionMode.Less, .10f, "Speed");

            AnimatorStateTransition walkRun = walkState.AddTransition(runState);
            ConfigureTransition(walkRun, false, .12f);
            walkRun.AddCondition(AnimatorConditionMode.Greater, 2.25f, "Speed");

            AnimatorStateTransition runWalk = runState.AddTransition(walkState);
            ConfigureTransition(runWalk, false, .12f);
            runWalk.AddCondition(AnimatorConditionMode.Less, 2.25f, "Speed");

            AnimatorStateTransition anyWave = sm.AddAnyStateTransition(waveState);
            ConfigureTransition(anyWave, false, .08f);
            anyWave.canTransitionToSelf = false;
            anyWave.AddCondition(AnimatorConditionMode.If, 0f, "Wave");

            AnimatorStateTransition waveIdle = waveState.AddTransition(idleState);
            ConfigureTransition(waveIdle, true, .12f);
            waveIdle.exitTime = .92f;

            AnimatorStateTransition anyInteract = sm.AddAnyStateTransition(interactState);
            ConfigureTransition(anyInteract, false, .08f);
            anyInteract.canTransitionToSelf = false;
            anyInteract.AddCondition(AnimatorConditionMode.If, 0f, "Interact");

            AnimatorStateTransition interactIdle = interactState.AddTransition(idleState);
            ConfigureTransition(interactIdle, true, .12f);
            interactIdle.exitTime = .92f;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        static void ConfigureTransition(AnimatorStateTransition transition, bool exit, float duration)
        {
            transition.hasExitTime = exit;
            transition.hasFixedDuration = true;
            transition.duration = duration;
        }

        static GameObject BuildOutfitPrefab(GameObject source, string packId, string outfit, RuntimeAnimatorController controller)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (!instance) return null;

            instance.name = "H51 Citizen " + packId + " " + outfit;
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            int visible = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                bool keep = MatchesOutfit(renderers[i].gameObject.name, outfit);
                renderers[i].enabled = keep;
                if (keep) visible++;
            }

            if (visible == 0)
            {
                UnityEngine.Object.DestroyImmediate(instance);
                Debug.LogWarning("No renderer pieces matched outfit " + packId + " " + outfit);
                return null;
            }

            ConvertVisibleMaterialsToUrp(instance, packId);

            foreach (Camera camera in instance.GetComponentsInChildren<Camera>(true)) UnityEngine.Object.DestroyImmediate(camera);
            foreach (Light light in instance.GetComponentsInChildren<Light>(true)) UnityEngine.Object.DestroyImmediate(light);
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            foreach (Rigidbody body in instance.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);

            NormalizeVisibleHeight(instance.transform, 1.76f);

            Animator animator = instance.GetComponent<Animator>();
            if (!animator) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            CapsuleCollider capsule = instance.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, .90f, 0f);
            capsule.height = 1.80f;
            capsule.radius = .29f;
            capsule.direction = 1;

            Rigidbody rb = instance.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.constraints = RigidbodyConstraints.FreezeRotation;

            NavMeshAgent agent = instance.AddComponent<NavMeshAgent>();
            agent.radius = .32f;
            agent.height = 1.8f;
            agent.speed = 1.6f;
            agent.acceleration = 7f;
            agent.angularSpeed = 320f;
            agent.stoppingDistance = .65f;
            agent.autoBraking = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

            instance.AddComponent<H51CitizenIdentity>();
            instance.AddComponent<H51CitizenVisualVariation>();
            instance.AddComponent<H51CitizenBrain>();

            string path = PrefabFolder + "/H51_Citizen_" + packId + "_" + Safe(outfit) + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            UnityEngine.Object.DestroyImmediate(instance);
            return prefab;
        }

        static void ConvertVisibleMaterialsToUrp(GameObject instance, string packId)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            Dictionary<Material,Material> cache = new Dictionary<Material,Material>();

            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer renderer = renderers[r];
                if (!renderer.enabled) continue;
                Material[] sourceMaterials = renderer.sharedMaterials;
                Material[] converted = new Material[sourceMaterials.Length];

                for (int i = 0; i < sourceMaterials.Length; i++)
                {
                    Material sourceMaterial = sourceMaterials[i];
                    if (!sourceMaterial) { converted[i] = null; continue; }

                    if (!cache.TryGetValue(sourceMaterial, out Material material))
                    {
                        string cleanName = Safe(sourceMaterial.name);
                        string path = Generated + "/Materials/H51_" + packId + "_" + cleanName + ".mat";
                        material = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (!material)
                        {
                            material = new Material(shader) { name = "H51_" + packId + "_" + sourceMaterial.name };
                            Color color = sourceMaterial.HasProperty("_BaseColor") ? sourceMaterial.GetColor("_BaseColor") :
                                          sourceMaterial.HasProperty("_Color") ? sourceMaterial.color : Color.white;
                            material.color = color;
                            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
                            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .05f);
                            material.enableInstancing = true;
                            AssetDatabase.CreateAsset(material, path);
                        }
                        cache[sourceMaterial] = material;
                    }
                    converted[i] = material;
                }

                renderer.sharedMaterials = converted;
            }
        }

        static bool MatchesOutfit(string rendererName, string outfit)
        {
            if (string.IsNullOrEmpty(rendererName)) return false;
            if (rendererName.StartsWith(outfit + "_", StringComparison.OrdinalIgnoreCase)) return true;
            if (outfit.Equals("Formal", StringComparison.OrdinalIgnoreCase) &&
                rendererName.StartsWith("Formad_", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static void NormalizeVisibleHeight(Transform root, float targetHeight)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(false);
            bool set = false;
            Bounds bounds = new Bounds();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].enabled || !renderers[i].gameObject.activeInHierarchy) continue;
                if (!set) { bounds = renderers[i].bounds; set = true; }
                else bounds.Encapsulate(renderers[i].bounds);
            }
            if (!set || bounds.size.y < .05f) return;
            float factor = Mathf.Clamp(targetHeight / bounds.size.y, .05f, 20f);
            root.localScale *= factor;
        }

        static void BuildTownAnchors(
            Transform world, Terrain terrain, Transform anchorsRoot, Transform town, int townIndex,
            List<H51CitizenAnchor> all,
            Dictionary<string,List<H51CitizenAnchor>> homesByTown,
            Dictionary<string,List<H51CitizenAnchor>> worksByTown)
        {
            string townName = town.name;
            Transform townRoot = New(townName + " Anchors", anchorsRoot);
            List<H51CitizenAnchor> homes = new List<H51CitizenAnchor>();
            List<H51CitizenAnchor> works = new List<H51CitizenAnchor>();
            homesByTown[townName] = homes;
            worksByTown[townName] = works;

            List<Transform> houses = Houses(town);
            for (int i = 0; i < houses.Count; i++)
            {
                Transform h = houses[i];
                Vector3 p = h.position + h.forward * 10f;
                p.y = Ground(terrain, p) + .05f;
                H51CitizenAnchor a = Anchor(townRoot, "Home Anchor " + (i+1).ToString("00"), p, h.forward,
                    "HOME-" + townIndex.ToString("00") + "-" + i.ToString("000"), townName, H51CitizenAnchorKind.Home, "Resident", 5);
                homes.Add(a); all.Add(a);
            }

            int workIndex = 0;
            foreach (Transform tr in world.GetComponentsInChildren<Transform>(true))
            {
                if (!tr.name.StartsWith("Purpose - " + townName, StringComparison.Ordinal)) continue;
                string type = PurposeType(tr.name);
                Vector3 front = tr.position + tr.forward * 12f;
                front.y = Ground(terrain, front) + .05f;

                H51CitizenAnchor work = Anchor(townRoot, "Work - " + type, front, tr.forward,
                    "WORK-" + townIndex.ToString("00") + "-" + (workIndex++).ToString("000"), townName,
                    H51CitizenAnchorKind.Work, OccupationFor(type), type.Contains("Fire") ? 5 : 7);
                works.Add(work); all.Add(work);

                H51CitizenAnchorKind visitorKind =
                    type.Contains("Store") || type.Contains("Cafe") || type.Contains("Supply")
                    ? H51CitizenAnchorKind.Shop : H51CitizenAnchorKind.Civic;

                H51CitizenAnchor visitor = Anchor(townRoot, "Visitor - " + type, front + tr.right * 3f, tr.forward,
                    "VISIT-" + townIndex.ToString("00") + "-" + workIndex.ToString("000"), townName,
                    visitorKind, "", 12);
                all.Add(visitor);
            }

            foreach (Transform tr in world.GetComponentsInChildren<Transform>(true))
            {
                if (tr.name != "Community Park - " + townName) continue;
                Vector3 p = tr.position;
                p.y = Ground(terrain, p) + .05f;
                H51CitizenAnchor park = Anchor(townRoot, "Park Anchor", p, tr.forward,
                    "PARK-" + townIndex.ToString("00"), townName, H51CitizenAnchorKind.Park, "", 20);
                all.Add(park);
            }

            foreach (Transform tr in world.GetComponentsInChildren<Transform>(true))
            {
                if (tr.name != "Farmstead - " + townName) continue;
                Vector3 p = tr.position + tr.forward * 25f;
                p.y = Ground(terrain, p) + .05f;
                H51CitizenAnchor farm = Anchor(townRoot, "Farm Work Anchor", p, tr.forward,
                    "FARM-" + townIndex.ToString("00"), townName, H51CitizenAnchorKind.Work, "Farmer", 8);
                works.Add(farm); all.Add(farm);
            }

            if (works.Count == 0)
            {
                Vector3 c = Average(houses);
                H51CitizenAnchor fallback = Anchor(townRoot, "Community Work Anchor", c, town.forward,
                    "WORK-" + townIndex.ToString("00") + "-COMMUNITY", townName, H51CitizenAnchorKind.Work, "Community Worker", 12);
                works.Add(fallback); all.Add(fallback);
            }
        }

        static H51CitizenAnchor Anchor(
            Transform parent, string name, Vector3 position, Vector3 forward,
            string id, string town, H51CitizenAnchorKind kind, string occupation, int capacity)
        {
            Transform t = New(name, parent);
            t.position = position;
            if (forward.sqrMagnitude > .01f) t.rotation = Quaternion.LookRotation(new Vector3(forward.x,0f,forward.z).normalized, Vector3.up);
            H51CitizenAnchor a = t.gameObject.AddComponent<H51CitizenAnchor>();
            a.Configure(id, town, kind, occupation, capacity);
            return a;
        }

        static NavMeshSurface BuildTownNavMesh(Transform parent, Transform town, int index)
        {
            List<Transform> houses = Houses(town);
            if (houses.Count == 0) return null;
            Vector3 center = Average(houses);
            float radius = 180f;
            for (int i = 0; i < houses.Count; i++) radius = Mathf.Max(radius, Planar(center, houses[i].position) + 150f);
            radius = Mathf.Clamp(radius, 250f, index == 0 ? 650f : 480f);

            Transform root = New("NavMesh - " + town.name, parent);
            root.position = center;
            NavMeshSurface surface = root.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.center = new Vector3(0f, 25f, 0f);
            surface.size = new Vector3(radius * 2f, 90f, radius * 2f);
            surface.BuildNavMesh();
            return surface;
        }

        static H51CitizenVisualCatalog.Entry PickVisualForOccupation(H51CitizenVisualCatalog catalog, string occupation, int citizenIndex, int townIndex)
        {
            if (catalog == null || catalog.entries == null || catalog.entries.Count == 0) return null;

            string[] preferred;
            string lower = (occupation ?? "").ToLowerInvariant();
            if (lower.Contains("farmer") || lower.Contains("farm supply"))
                preferred = new[] { "Farmer", "Worker", "Casual" };
            else if (lower.Contains("mechanic"))
                preferred = new[] { "Worker", "Adventurer", "Casual" };
            else if (lower.Contains("fire"))
                preferred = new[] { "Worker", "Swat", "Soldier" };
            else if (lower.Contains("clinic"))
                preferred = new[] { "Formal", "Suit", "Casual2", "Casual" };
            else if (lower.Contains("municipal") || lower.Contains("postal"))
                preferred = new[] { "Suit", "Formal", "Casual2", "Casual" };
            else if (lower.Contains("store") || lower.Contains("cafe"))
                preferred = new[] { "Casual", "Casual2", "Punk", "Suit" };
            else
                preferred = new[] { "Casual", "Casual2", "Worker", "Adventurer", "Punk", "Suit", "Formal" };

            List<H51CitizenVisualCatalog.Entry> pool = new List<H51CitizenVisualCatalog.Entry>();
            for (int p = 0; p < preferred.Length; p++)
                for (int i = 0; i < catalog.entries.Count; i++)
                {
                    H51CitizenVisualCatalog.Entry e = catalog.entries[i];
                    if (e != null && e.prefab && e.archetype.Equals(preferred[p], StringComparison.OrdinalIgnoreCase))
                        pool.Add(e);
                }

            if (pool.Count == 0) pool.AddRange(catalog.entries.Where(e => e != null && e.prefab));
            if (pool.Count == 0) return null;
            return pool[Math.Abs(citizenIndex * 7 + townIndex * 11) % pool.Count];
        }

        static H51CitizenAnchor PickLeastLoadedWork(List<H51CitizenAnchor> works, Dictionary<string,int> loads, System.Random rng)
        {
            if (works == null || works.Count == 0) return null;
            int bestLoad = int.MaxValue;
            List<H51CitizenAnchor> best = new List<H51CitizenAnchor>();
            for (int i = 0; i < works.Count; i++)
            {
                H51CitizenAnchor a = works[i];
                if (!a) continue;
                loads.TryGetValue(a.AnchorId, out int load);
                if (load >= a.Capacity) continue;
                if (load < bestLoad) { bestLoad = load; best.Clear(); best.Add(a); }
                else if (load == bestLoad) best.Add(a);
            }
            if (best.Count == 0) return works[rng.Next(works.Count)];
            H51CitizenAnchor chosen = best[rng.Next(best.Count)];
            loads.TryGetValue(chosen.AnchorId, out int current);
            loads[chosen.AnchorId] = current + 1;
            return chosen;
        }

        static string UniqueName(HashSet<string> used, System.Random rng)
        {
            for (int tries = 0; tries < 500; tries++)
            {
                string name = FirstNames[rng.Next(FirstNames.Length)] + " " + LastNames[rng.Next(LastNames.Length)];
                if (used.Add(name)) return name;
            }
            string fallback = "Resident " + (used.Count + 1).ToString("000");
            used.Add(fallback);
            return fallback;
        }

        static string PurposeType(string fullName)
        {
            string[] known =
            {
                "General Store","Fire Station","Post Office","Town Hall","Repair Garage",
                "Clinic","Cafe","Farm Supply","Community Store","Community Hall"
            };
            for (int i = 0; i < known.Length; i++)
                if (fullName.EndsWith(known[i], StringComparison.Ordinal)) return known[i];
            return "Community Business";
        }

        static string OccupationFor(string type)
        {
            if (type.Contains("General Store") || type.Contains("Community Store")) return "Store Clerk";
            if (type.Contains("Fire")) return "Firefighter";
            if (type.Contains("Post")) return "Postal Worker";
            if (type.Contains("Town Hall")) return "Municipal Clerk";
            if (type.Contains("Garage")) return "Mechanic";
            if (type.Contains("Clinic")) return "Clinic Staff";
            if (type.Contains("Cafe")) return "Cafe Worker";
            if (type.Contains("Farm Supply")) return "Farm Supply Clerk";
            if (type.Contains("Community Hall")) return "Community Worker";
            return "Town Worker";
        }

        static List<AnimationClip> AnimationClips(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .Where(c => c && !c.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        static AnimationClip FindClip(List<AnimationClip> clips, string contains)
        {
            if (clips == null) return null;
            string needle = contains.ToLowerInvariant();
            for (int i = 0; i < clips.Count; i++)
                if (clips[i] && clips[i].name.ToLowerInvariant().Contains(needle))
                    return clips[i];
            return null;
        }

        static string FindArchiveFor(string[] entryCandidates)
        {
            string[] roots =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            for (int r = 0; r < roots.Length; r++)
            {
                string root = roots[r];
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                string[] zips;
                try { zips = Directory.GetFiles(root, "*.zip", SearchOption.TopDirectoryOnly); }
                catch { continue; }

                for (int i = zips.Length - 1; i >= 0; i--)
                    if (ArchiveContainsAny(zips[i], entryCandidates)) return zips[i];
            }
            return null;
        }

        static bool ArchiveContainsAny(string zipPath, string[] entryCandidates)
        {
            try
            {
                using (FileStream stream = File.OpenRead(zipPath))
                using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                        for (int i = 0; i < entryCandidates.Length; i++)
                            if (EntryMatches(entry.FullName, entryCandidates[i])) return true;
                }
            }
            catch { }
            return false;
        }

        static bool ExtractEntry(string zipPath, string[] entryCandidates, string assetDestination)
        {
            string absolute = AssetPathToAbsolute(assetDestination);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));

            using (FileStream stream = File.OpenRead(zipPath))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                ZipArchiveEntry found = null;
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    for (int i = 0; i < entryCandidates.Length; i++)
                    {
                        if (!EntryMatches(entry.FullName, entryCandidates[i])) continue;
                        found = entry;
                        break;
                    }
                    if (found != null) break;
                }

                if (found == null) return false;
                using (Stream input = found.Open())
                using (FileStream output = File.Create(absolute))
                    input.CopyTo(output);
            }
            return File.Exists(absolute) && new FileInfo(absolute).Length > 1024 * 1024;
        }

        static bool EntryMatches(string fullName, string candidate)
        {
            string a = fullName.Replace('\\','/').TrimStart('/');
            string b = candidate.Replace('\\','/').TrimStart('/');
            return a.Equals(b, StringComparison.OrdinalIgnoreCase) ||
                   a.EndsWith("/" + b, StringComparison.OrdinalIgnoreCase) ||
                   a.EndsWith(b, StringComparison.OrdinalIgnoreCase);
        }

        static string DefaultBrowseFolder()
        {
            string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            return Directory.Exists(downloads) ? downloads : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        }

        static string AssetPathToAbsolute(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        static List<Transform> Houses(Transform town)
        {
            List<Transform> houses = new List<Transform>();
            if (!town) return houses;
            for (int i = 0; i < town.childCount; i++)
            {
                Transform c = town.GetChild(i);
                if (c.name.StartsWith("Detailed House", StringComparison.Ordinal) ||
                    c.name.StartsWith("Building", StringComparison.Ordinal))
                    houses.Add(c);
            }
            return houses;
        }

        static Vector3 Average(List<Transform> items)
        {
            if (items == null || items.Count == 0) return Vector3.zero;
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < items.Count; i++) sum += items[i].position;
            return sum / items.Count;
        }

        static Vector3 RandomXZ(System.Random rng, float min, float max)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float r = Mathf.Lerp(min, max, (float)rng.NextDouble());
            return new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        }

        static float Ground(Terrain terrain, Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;

        static float Planar(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x, z = a.z - b.z;
            return Mathf.Sqrt(x*x + z*z);
        }

        static Terrain FindTerrain()
        {
            GameObject g = Find(TerrainName);
            Terrain t = g ? (g.GetComponent<Terrain>() ?? g.GetComponentInChildren<Terrain>(true)) : null;
            if (t) return t;
            Terrain[] all = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return all.Length > 0 ? all[0] : null;
        }

        static GameObject Find(string name)
        {
            GameObject direct = GameObject.Find(name);
            if (direct) return direct;
            Transform[] all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i] && all[i].gameObject.scene.IsValid() && all[i].name == name)
                    return all[i].gameObject;
            return null;
        }

        static Transform FindChild(Transform root, string name)
        {
            if (!root) return null;
            foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                if (tr.name == name) return tr;
            return null;
        }

        static Transform New(string name, Transform parent)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(parent, false);
            return g.transform;
        }

        static string Safe(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return value.Replace(' ', '_');
        }

        static void Ensure(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
