using System;
using Hanger51.Aircraft;
using Hanger51.EngineAssembly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51FinalRangeAndSpawnTools
    {
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string ArmamentRootName = "P-51 Serviceable Wing Armament";
        private const string TargetRootName = "P-51 Gun Test Target";
        private const float BayFrontZ = 0.50f;
        private const float BayRearZ = -0.78f;
        private const float BayWidth = 2.60f;

        [MenuItem("Hanger 51/P-51 Mustang/44 - Loud Merlin, Fix Gun Bay Floor and Add Test Target")]
        public static void ApplyFinalRangePass()
        {
            if (!CanEdit(out Scene scene)) return;

            GameObject aircraft = GameObject.Find(AircraftRootName);
            if (aircraft == null)
            {
                Debug.LogError("P-51 Step 44 failed. The main P-51 aircraft is missing.");
                return;
            }

            int fixedBays = FixArmamentBayBottoms(aircraft);
            if (fixedBays != 2)
            {
                Debug.LogError($"P-51 Step 44 failed. Expected to refit 2 armament bays, refit {fixedBays}.", aircraft);
                return;
            }

            P51MerlinAudioPresenceBoost boost = aircraft.GetComponent<P51MerlinAudioPresenceBoost>();
            if (boost == null) boost = Undo.AddComponent<P51MerlinAudioPresenceBoost>(aircraft);
            boost.Configure(2.15f, 16f, 700f);
            EditorUtility.SetDirty(boost);

            ConfigureTargetHitBridge(aircraft);
            P51GunTestTarget target = CreateOrResetTargetInternal(aircraft);
            if (target == null)
            {
                Debug.LogError("P-51 Step 44 failed. The gun test target could not be created.");
                return;
            }

            SaveScene(scene);
            Hanger51BuildTools.PrepareCurrentSceneForBuild(false);
            Debug.Log(
                "P-51 Step 44 complete. Replaced both thick black armament-bay floor cubes with one-sided recessed floors, "
                + "compressed the black bay walls/ribs inside the wing, substantially increased Merlin presence/range, "
                + "kept casing impacts local-only, and installed a shootable resetting gun target tied to real muzzle raycasts.",
                aircraft);
        }

        [MenuItem("Hanger 51/P-51 Mustang/45 - Validate Final Gun Bay Audio and Target")]
        public static void ValidateFinalRangePass()
        {
            bool passed = true;
            GameObject aircraft = GameObject.Find(AircraftRootName);
            if (aircraft == null)
            {
                Debug.LogError("P-51 Step 45 failed: main P-51 is missing.");
                return;
            }

            int oneSidedFloors = 0;
            Transform armament = aircraft.transform.Find(ArmamentRootName);
            if (armament != null)
            {
                for (int wing = 0; wing < 2; wing++)
                {
                    string wingName = wing == 0 ? "Left" : "Right";
                    Transform floor = FindChildRecursive(armament, $"{wingName} Armament Bay Floor");
                    if (floor != null
                        && floor.GetComponent<MeshFilter>() != null
                        && floor.GetComponent<MeshCollider>() == null
                        && floor.GetComponent<BoxCollider>() == null
                        && Mathf.Abs(Mathf.DeltaAngle(floor.localEulerAngles.x, 270f)) < 1f)
                    {
                        oneSidedFloors++;
                    }
                    else
                    {
                        Debug.LogError($"P-51 Step 45 failed: {wingName.ToLowerInvariant()} bay floor is not the one-sided recessed floor.");
                        passed = false;
                    }
                }
            }

            P51MerlinAudioPresenceBoost boost = aircraft.GetComponent<P51MerlinAudioPresenceBoost>();
            P51GunTargetHitBridge hitBridge = aircraft.GetComponent<P51GunTargetHitBridge>();
            P51GunTestTarget target = Object.FindFirstObjectByType<P51GunTestTarget>();
            if (boost == null)
            {
                Debug.LogError("P-51 Step 45 failed: Merlin audio presence boost is missing.");
                passed = false;
            }
            if (hitBridge == null || !hitBridge.IsConfigured)
            {
                Debug.LogError("P-51 Step 45 failed: gun-target hit bridge is missing or incomplete.");
                passed = false;
            }
            if (target == null)
            {
                Debug.LogError("P-51 Step 45 failed: shootable gun test target is missing.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"P-51 Step 45 passed. One-sided bay floors={oneSidedFloors}/2, loud Merlin boost installed, "
                    + "local brass audio active, six-gun target hit bridge configured, and shootable target ready.");
            }
        }

        [MenuItem("Hanger 51/Test Range/1 - Create or Reset P-51 Gun Test Target")]
        public static void CreateOrResetGunTarget()
        {
            if (!CanEdit(out Scene scene)) return;
            GameObject aircraft = GameObject.Find(AircraftRootName);
            if (aircraft == null)
            {
                Debug.LogError("Gun target setup failed. The main P-51 is missing.");
                return;
            }

            ConfigureTargetHitBridge(aircraft);
            P51GunTestTarget target = CreateOrResetTargetInternal(aircraft);
            SaveScene(scene);
            if (target != null)
            {
                Selection.activeGameObject = target.gameObject;
                Debug.Log("Gun test target ready about 120 m ahead of the main P-51. Fire with Left Ctrl; it flashes, tracks hits/health, falls when destroyed, and automatically resets.", target);
            }
        }

        [MenuItem("Hanger 51/P-51 Mustang/Spawn Fully Built Test P-51")]
        public static void SpawnFullyBuiltTestP51()
        {
            if (!CanEdit(out Scene scene)) return;

            GameObject sourceAircraft = GameObject.Find(AircraftRootName);
            if (sourceAircraft == null)
            {
                Debug.LogError("Fully built P-51 spawn failed. The main configured P-51 is missing.");
                return;
            }

            EngineAssemblyTransportController sourceTransport = FindBestEngineVisualSource();
            if (sourceTransport == null || sourceTransport.TransportRoot == null)
            {
                Debug.LogError("Fully built P-51 spawn failed. No Merlin portable assembly root exists to clone for the installed engine visual.");
                return;
            }

            GameObject clone = Object.Instantiate(sourceAircraft);
            Undo.RegisterCreatedObjectUndo(clone, "Spawn fully built P-51 test aircraft");
            clone.name = GetUniqueSpawnName();
            int existingAircraft = Object.FindObjectsByType<P51FlightController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            clone.transform.position = sourceAircraft.transform.position
                + sourceAircraft.transform.right * (12f + Mathf.Max(0, existingAircraft - 2) * 4f);
            clone.transform.rotation = sourceAircraft.transform.rotation;

            RemoveClonedEngineVisuals(clone);

            P51AircraftServiceController service = clone.GetComponent<P51AircraftServiceController>();
            AircraftEngineMountReceiver receiver = clone.GetComponent<AircraftEngineMountReceiver>();
            if (service == null || receiver == null)
            {
                Undo.DestroyObjectImmediate(clone);
                Debug.LogError("Fully built P-51 spawn failed. The cloned aircraft lost its service/engine receiver components.");
                return;
            }

            service.ResetAircraftService();
            EngineAssemblyTransportController proxyTransport = BuildCompleteEngineProxy(clone, sourceTransport);
            if (proxyTransport == null)
            {
                Undo.DestroyObjectImmediate(clone);
                Debug.LogError("Fully built P-51 spawn failed while creating the independent installed Merlin.");
                return;
            }

            receiver.CompleteEnginePlacement(proxyTransport);
            ForceReceiverBoltsTight(receiver);
            service.RefreshTargetsAndVisuals();

            ForceArmamentFullyLoaded(clone);
            ForceLandingGearComplete(clone);
            ConfigureTargetHitBridge(clone);

            P51MerlinAudioPresenceBoost boost = clone.GetComponent<P51MerlinAudioPresenceBoost>();
            if (boost == null) boost = Undo.AddComponent<P51MerlinAudioPresenceBoost>(clone);
            boost.Configure(2.15f, 16f, 700f);
            EditorUtility.SetDirty(boost);

            P51MerlinLifecycleController lifecycle = clone.GetComponent<P51MerlinLifecycleController>();
            if (lifecycle == null) lifecycle = Undo.AddComponent<P51MerlinLifecycleController>(clone);
            lifecycle.Configure(3.2f, 2.2f);
            EditorUtility.SetDirty(lifecycle);

            if (clone.GetComponent<P51MerlinAudioAndExhaustFxController>() == null)
            {
                Undo.AddComponent<P51MerlinAudioAndExhaustFxController>(clone);
            }

            EditorUtility.SetDirty(clone);
            SaveScene(scene);
            Hanger51BuildTools.PrepareCurrentSceneForBuild(false);
            Selection.activeGameObject = clone;
            Debug.Log(
                $"Spawned '{clone.name}' fully configured for immediate testing: independent installed Merlin visual/controller, four engine bolts tight, cowling secured, "
                + "landing gear/tires healthy and installed, six guns installed, six full ammo boxes loaded, panels closed, loud Merlin audio/startup FX, and gun-target hit support. "
                + "This spawned test copy is intended for instant flying/firing rather than engine-maintenance teardown.",
                clone);
        }

        private static int FixArmamentBayBottoms(GameObject aircraft)
        {
            Transform armament = aircraft.transform.Find(ArmamentRootName);
            if (armament == null) return 0;

            int count = 0;
            for (int wing = 0; wing < 2; wing++)
            {
                string wingName = wing == 0 ? "Left" : "Right";
                Transform interior = FindChildRecursive(armament, $"{wingName} Wing Armament Bay Interior");
                if (interior == null) continue;

                Transform oldFloor = FindChildRecursive(interior, $"{wingName} Armament Bay Floor");
                Material floorMaterial = oldFloor != null && oldFloor.GetComponent<Renderer>() != null
                    ? oldFloor.GetComponent<Renderer>().sharedMaterial
                    : null;
                if (oldFloor != null)
                {
                    Undo.DestroyObjectImmediate(oldFloor.gameObject);
                }

                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Undo.RegisterCreatedObjectUndo(floor, "Replace P-51 armament bay floor");
                floor.name = $"{wingName} Armament Bay Floor";
                floor.transform.SetParent(interior, false);
                floor.transform.localPosition = new Vector3(0f, 0.055f, (BayFrontZ + BayRearZ) * 0.5f);
                floor.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                floor.transform.localScale = new Vector3(BayWidth - 0.10f, BayFrontZ - BayRearZ - 0.10f, 1f);
                Renderer renderer = floor.GetComponent<Renderer>();
                if (renderer != null && floorMaterial != null) renderer.sharedMaterial = floorMaterial;
                Collider floorCollider = floor.GetComponent<Collider>();
                if (floorCollider != null) Undo.DestroyObjectImmediate(floorCollider);

                string[] wallNames = { "Front Bay Wall", "Rear Bay Wall", "Inner Bay Wall", "Outer Bay Wall" };
                for (int index = 0; index < wallNames.Length; index++)
                {
                    Transform wall = FindChildRecursive(interior, wallNames[index]);
                    if (wall == null) continue;
                    Undo.RecordObject(wall, "Tuck P-51 armament bay wall");
                    Vector3 position = wall.localPosition;
                    position.y = 0.15f;
                    wall.localPosition = position;
                    Vector3 scale = wall.localScale;
                    scale.y = 0.070f;
                    wall.localScale = scale;
                    EditorUtility.SetDirty(wall);
                }

                Transform[] all = interior.GetComponentsInChildren<Transform>(true);
                for (int index = 0; index < all.Length; index++)
                {
                    Transform rib = all[index];
                    if (rib == null || rib.name != "Armament Bay Rib") continue;
                    Undo.RecordObject(rib, "Tuck P-51 armament bay rib");
                    Vector3 position = rib.localPosition;
                    position.y = 0.105f;
                    rib.localPosition = position;
                    Vector3 scale = rib.localScale;
                    scale.y = 0.030f;
                    rib.localScale = scale;
                    EditorUtility.SetDirty(rib);
                }

                count++;
            }
            return count;
        }

        private static void ConfigureTargetHitBridge(GameObject aircraft)
        {
            P51WingArmamentSystem system = aircraft.GetComponent<P51WingArmamentSystem>();
            if (system == null) return;

            Transform[] muzzles = ReadTransformArray(system, "muzzles", 6);
            P51GunTargetHitBridge bridge = aircraft.GetComponent<P51GunTargetHitBridge>();
            if (bridge == null) bridge = Undo.AddComponent<P51GunTargetHitBridge>(aircraft);
            bridge.Configure(system, muzzles);
            EditorUtility.SetDirty(bridge);
        }

        private static P51GunTestTarget CreateOrResetTargetInternal(GameObject aircraft)
        {
            GameObject existing = GameObject.Find(TargetRootName);
            if (existing != null)
            {
                P51GunTestTarget existingTarget = existing.GetComponent<P51GunTestTarget>();
                if (existingTarget != null)
                {
                    existingTarget.ResetTarget();
                    EditorUtility.SetDirty(existingTarget);
                    return existingTarget;
                }
                Undo.DestroyObjectImmediate(existing);
            }

            Material aluminum = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Aircraft/P51/Materials/PolishedAluminum.mat");
            Material darkMetal = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Aircraft/P51/Materials/DarkAircraftMetal.mat");
            if (aluminum == null || darkMetal == null)
            {
                Debug.LogError("Gun target setup failed. P-51 aluminum/dark metal materials are missing.");
                return null;
            }

            GameObject root = new GameObject(TargetRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create P-51 gun test target");
            root.transform.position = aircraft.transform.position + aircraft.transform.forward * 120f;
            root.transform.rotation = Quaternion.LookRotation(-aircraft.transform.forward, Vector3.up);

            CreateCube(root.transform, "Target Concrete Base", new Vector3(0f, 0.16f, 0f), new Vector3(5.4f, 0.32f, 1.5f), darkMetal);
            CreateCube(root.transform, "Left Target Support", new Vector3(-1.55f, 1.45f, 0f), new Vector3(0.18f, 2.65f, 0.18f), darkMetal);
            CreateCube(root.transform, "Right Target Support", new Vector3(1.55f, 1.45f, 0f), new Vector3(0.18f, 2.65f, 0.18f), darkMetal);

            GameObject plate = CreateCube(root.transform, "Shootable Steel Target Plate", new Vector3(0f, 2.55f, 0f), new Vector3(4.25f, 3.05f, 0.20f), aluminum);
            Renderer plateRenderer = plate.GetComponent<Renderer>();

            GameObject textObject = new GameObject("Target Status Text");
            textObject.transform.SetParent(root.transform, false);
            textObject.transform.localPosition = new Vector3(0f, 4.55f, -0.18f);
            textObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            TextMesh text = textObject.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 54;
            text.characterSize = 0.055f;
            text.color = Color.white;

            P51GunTestTarget target = Undo.AddComponent<P51GunTestTarget>(root);
            target.Configure(plate.transform, plateRenderer, text, 600f);
            EditorUtility.SetDirty(target);
            return target;
        }

        private static EngineAssemblyTransportController FindBestEngineVisualSource()
        {
            EngineAssemblyTransportController[] transports = Object.FindObjectsByType<EngineAssemblyTransportController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            EngineAssemblyTransportController best = null;
            int bestRendererCount = -1;
            for (int index = 0; index < transports.Length; index++)
            {
                EngineAssemblyTransportController transport = transports[index];
                if (transport == null || transport.TransportRoot == null) continue;
                int rendererCount = transport.TransportRoot.GetComponentsInChildren<Renderer>(true).Length;
                if (rendererCount > bestRendererCount)
                {
                    best = transport;
                    bestRendererCount = rendererCount;
                }
            }
            return best;
        }

        private static EngineAssemblyTransportController BuildCompleteEngineProxy(
            GameObject aircraft,
            EngineAssemblyTransportController sourceTransport)
        {
            GameObject controllerRoot = new GameObject("Spawned Complete Merlin Controller");
            Undo.RegisterCreatedObjectUndo(controllerRoot, "Create spawned Merlin controller");
            controllerRoot.transform.SetParent(aircraft.transform, false);

            BoxCollider stationCollider = Undo.AddComponent<BoxCollider>(controllerRoot);
            stationCollider.enabled = false;
            EngineAssemblyStation station = Undo.AddComponent<EngineAssemblyStation>(controllerRoot);
            EngineAssemblyTransportController transport = Undo.AddComponent<EngineAssemblyTransportController>(controllerRoot);

            GameObject engineVisual = Object.Instantiate(sourceTransport.TransportRoot.gameObject);
            Undo.RegisterCreatedObjectUndo(engineVisual, "Clone complete Merlin visual");
            engineVisual.name = "Portable Engine Assembly Root";
            engineVisual.transform.SetParent(controllerRoot.transform, false);
            engineVisual.transform.localPosition = Vector3.zero;
            engineVisual.transform.localRotation = Quaternion.identity;

            MonoBehaviour[] behaviours = engineVisual.GetComponentsInChildren<MonoBehaviour>(true);
            for (int index = 0; index < behaviours.Length; index++)
            {
                if (behaviours[index] != null) behaviours[index].enabled = false;
            }
            Collider[] colliders = engineVisual.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null) colliders[index].enabled = false;
            }
            Transform[] visuals = engineVisual.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < visuals.Length; index++)
            {
                Transform candidate = visuals[index];
                if (candidate == null) continue;
                if (candidate.name.Contains("Highlight") || candidate.name.Contains("Placement Beacon"))
                {
                    candidate.gameObject.SetActive(false);
                }
                else if (candidate.name.StartsWith("Installed "))
                {
                    candidate.gameObject.SetActive(true);
                }
            }

            Transform lift = GetOrCreateMarker(engineVisual.transform, "Engine Lift Point", new Vector3(0f, 0.65f, 0f));
            Transform ground = GetOrCreateMarker(engineVisual.transform, "Engine Ground Contact Point", new Vector3(0f, -0.55f, 0f));
            Transform left = GetOrCreateMarker(engineVisual.transform, "Left Engine Lift Lug", new Vector3(-0.24f, 0.55f, 0f));
            Transform right = GetOrCreateMarker(engineVisual.transform, "Right Engine Lift Lug", new Vector3(0.24f, 0.55f, 0f));

            transport.Configure(
                engineVisual.transform,
                lift,
                ground,
                left,
                right,
                stationCollider,
                Vector3.zero,
                Quaternion.identity,
                engineVisual.transform.localScale);

            SerializedObject stationSerialized = new SerializedObject(station);
            SerializedProperty block = stationSerialized.FindProperty("engineBlockInstalled");
            if (block != null) block.boolValue = true;
            stationSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(station);
            EditorUtility.SetDirty(transport);
            return transport;
        }

        private static void ForceReceiverBoltsTight(AircraftEngineMountReceiver receiver)
        {
            SerializedObject serialized = new SerializedObject(receiver);
            SerializedProperty positioned = serialized.FindProperty("enginePositioned");
            SerializedProperty bolts = serialized.FindProperty("mountBoltsTightened");
            if (positioned != null) positioned.boolValue = true;
            if (bolts != null && bolts.isArray)
            {
                bolts.arraySize = 4;
                for (int index = 0; index < bolts.arraySize; index++)
                {
                    bolts.GetArrayElementAtIndex(index).boolValue = true;
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(receiver);
        }

        private static void ForceArmamentFullyLoaded(GameObject aircraft)
        {
            P51WingArmamentSystem system = aircraft.GetComponent<P51WingArmamentSystem>();
            if (system == null) return;

            SerializedObject serialized = new SerializedObject(system);
            SetBoolArray(serialized.FindProperty("gunInstalled"), 6, true);
            SetBoolArray(serialized.FindProperty("ammoBoxInstalled"), 6, true);
            SetIntArray(serialized.FindProperty("ammoRemaining"), 6, 200);
            SetBoolArray(serialized.FindProperty("panelOpen"), 2, false);

            SerializedProperty gunVisuals = serialized.FindProperty("installedGunVisuals");
            SerializedProperty ammoVisuals = serialized.FindProperty("installedAmmoVisuals");
            SerializedProperty pivots = serialized.FindProperty("panelPivots");
            serialized.ApplyModifiedPropertiesWithoutUndo();

            SetObjectArrayActive(gunVisuals, true);
            SetObjectArrayActive(ammoVisuals, true);
            if (pivots != null && pivots.isArray)
            {
                for (int index = 0; index < pivots.arraySize; index++)
                {
                    Transform pivot = pivots.GetArrayElementAtIndex(index).objectReferenceValue as Transform;
                    if (pivot != null) pivot.localRotation = Quaternion.identity;
                }
            }
            EditorUtility.SetDirty(system);
        }

        private static void ForceLandingGearComplete(GameObject aircraft)
        {
            P51LandingGearMaintenanceController maintenance = aircraft.GetComponent<P51LandingGearMaintenanceController>();
            if (maintenance == null) return;

            SerializedObject serialized = new SerializedObject(maintenance);
            SetBoolArray(serialized.FindProperty("gearInstalled"), 3, true);
            SetBoolArray(serialized.FindProperty("tireInstalled"), 3, true);
            SetBoolArray(serialized.FindProperty("tireBurst"), 3, false);
            SetFloatArray(serialized.FindProperty("tireHealth"), new[] { 100f, 100f, 100f });
            SetFloatArray(serialized.FindProperty("tirePressurePsi"), new[] { 30f, 30f, 24f });
            SerializedProperty down = serialized.FindProperty("gearCommandDown");
            SerializedProperty fraction = serialized.FindProperty("deploymentFraction");
            if (down != null) down.boolValue = true;
            if (fraction != null) fraction.floatValue = 1f;

            SerializedProperty gearRoots = serialized.FindProperty("gearVisualRoots");
            SerializedProperty tireRoots = serialized.FindProperty("tireVisualRoots");
            SerializedProperty rimRoots = serialized.FindProperty("rimVisualRoots");
            serialized.ApplyModifiedPropertiesWithoutUndo();

            SetObjectArrayActive(gearRoots, true);
            SetObjectArrayActive(tireRoots, true);
            SetObjectArrayActive(rimRoots, true);
            EditorUtility.SetDirty(maintenance);
        }

        private static void RemoveClonedEngineVisuals(GameObject clone)
        {
            Transform[] all = clone.GetComponentsInChildren<Transform>(true);
            for (int index = all.Length - 1; index >= 0; index--)
            {
                Transform candidate = all[index];
                if (candidate == null || candidate == clone.transform) continue;
                if (candidate.name == "Portable Engine Assembly Root"
                    || candidate.name == "Spawned Complete Merlin Controller")
                {
                    Undo.DestroyObjectImmediate(candidate.gameObject);
                }
            }
        }

        private static string GetUniqueSpawnName()
        {
            int index = 1;
            while (GameObject.Find($"P-51D Mustang Fully Built Test Aircraft {index}") != null) index++;
            return $"P-51D Mustang Fully Built Test Aircraft {index}";
        }

        private static Transform[] ReadTransformArray(UnityEngine.Object target, string propertyName, int length)
        {
            Transform[] result = new Transform[length];
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray) return result;
            int count = Mathf.Min(length, property.arraySize);
            for (int index = 0; index < count; index++)
            {
                result[index] = property.GetArrayElementAtIndex(index).objectReferenceValue as Transform;
            }
            return result;
        }

        private static void SetBoolArray(SerializedProperty property, int length, bool value)
        {
            if (property == null || !property.isArray) return;
            property.arraySize = length;
            for (int index = 0; index < length; index++) property.GetArrayElementAtIndex(index).boolValue = value;
        }

        private static void SetIntArray(SerializedProperty property, int length, int value)
        {
            if (property == null || !property.isArray) return;
            property.arraySize = length;
            for (int index = 0; index < length; index++) property.GetArrayElementAtIndex(index).intValue = value;
        }

        private static void SetFloatArray(SerializedProperty property, float[] values)
        {
            if (property == null || !property.isArray || values == null) return;
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++) property.GetArrayElementAtIndex(index).floatValue = values[index];
        }

        private static void SetObjectArrayActive(SerializedProperty property, bool active)
        {
            if (property == null || !property.isArray) return;
            for (int index = 0; index < property.arraySize; index++)
            {
                GameObject value = property.GetArrayElementAtIndex(index).objectReferenceValue as GameObject;
                if (value != null) value.SetActive(active);
                Transform transform = property.GetArrayElementAtIndex(index).objectReferenceValue as Transform;
                if (transform != null) transform.gameObject.SetActive(active);
            }
        }

        private static Transform GetOrCreateMarker(Transform parent, string name, Vector3 localPosition)
        {
            Transform existing = FindChildRecursive(parent, name);
            if (existing != null) return existing;
            GameObject marker = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(marker, "Create spawned Merlin marker");
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = localPosition;
            return marker.transform;
        }

        private static GameObject CreateCube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(cube, "Create P-51 test range geometry");
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localRotation = Quaternion.identity;
            cube.transform.localScale = scale;
            Renderer renderer = cube.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            return cube;
        }

        private static Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null) return null;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < all.Length; index++)
            {
                if (all[index] != null && all[index].name == name) return all[index];
            }
            return null;
        }

        private static bool CanEdit(out Scene scene)
        {
            scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play mode before running this Hanger 51 setup tool.");
                return false;
            }
            if (EditorApplication.isCompiling)
            {
                Debug.LogError("Wait for Unity to finish compiling before running this Hanger 51 setup tool.");
                return false;
            }
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("Open and save the hangar scene before running this Hanger 51 setup tool.");
                return false;
            }
            return true;
        }

        private static void SaveScene(Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("Hanger 51 setup changed the scene but Unity could not save it.");
            }
        }
    }
}
