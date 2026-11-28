using Hanger51.Aircraft;
using Hanger51.Commerce;
using Hanger51.EngineAssembly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51InHangarFullServiceSpawnSetup
    {
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string ConsoleName = "Hanger 51 Full P-51 Spawn Console";
        private const string TemplateContainerName = "Hanger 51 Full-Service Spawn Templates";
        private const string AircraftTemplateName = "P-51 Full Service Aircraft Template";
        private const string EngineTemplateName = "Merlin Full Service Engine Template";
        private const string SpawnPointName = "P-51 Full Service Spawn Point";
        private const string TargetRootName = "P-51 Gun Test Target";

        [MenuItem("Hanger 51/P-51 Mustang/46 - Install In-Hangar Full-Service P-51 Spawn Button")]
        public static void InstallInHangarSpawner()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("P-51 Step 46 failed. Exit Play mode first.");
                return;
            }
            if (EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 Step 46 failed. Wait for Unity to finish compiling.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            GameObject sourceAircraft = GameObject.Find(AircraftRootName);
            HangarShopTerminal shopTerminal = Object.FindFirstObjectByType<HangarShopTerminal>();
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path) || sourceAircraft == null || shopTerminal == null)
            {
                Debug.LogError("P-51 Step 46 failed. Open the saved hangar scene containing the configured P-51 and parts terminal first.");
                return;
            }

            EnsureRuntimeAircraftSystems(sourceAircraft);
            EngineAssemblyTransportController sourceTransport = FindCompleteEngineSource(sourceAircraft);
            EngineAssemblyStation sourceStation = sourceTransport != null
                ? sourceTransport.GetComponent<EngineAssemblyStation>()
                : null;
            if (sourceTransport == null
                || sourceTransport.TransportRoot == null
                || sourceStation == null
                || !sourceStation.IsComplete)
            {
                Debug.LogError("P-51 Step 46 failed. A complete serviceable Merlin engine assembly is required as the spawn template source.");
                return;
            }

            DestroyExisting(ConsoleName);
            DestroyExisting(TemplateContainerName);
            DestroyExisting(SpawnPointName);

            GameObject templateContainer = new GameObject(TemplateContainerName);
            Undo.RegisterCreatedObjectUndo(templateContainer, "Create full-service spawn template container");
            templateContainer.transform.position = new Vector3(0f, -700f, 0f);

            GameObject aircraftTemplate;
            EngineAssemblyTransportController engineTemplate;
            if (!CreateIndependentTemplates(
                    sourceAircraft,
                    sourceTransport,
                    templateContainer.transform,
                    out aircraftTemplate,
                    out engineTemplate))
            {
                Undo.DestroyObjectImmediate(templateContainer);
                return;
            }

            Transform spawnPoint = new GameObject(SpawnPointName).transform;
            Undo.RegisterCreatedObjectUndo(spawnPoint.gameObject, "Create full-service P-51 spawn point");
            spawnPoint.position = sourceAircraft.transform.position
                + sourceAircraft.transform.right * 15.5f
                - sourceAircraft.transform.forward * 4f;
            spawnPoint.rotation = sourceAircraft.transform.rotation;

            HangarAircraftSpawnConsole console = BuildSpawnConsole(
                shopTerminal,
                aircraftTemplate,
                engineTemplate,
                spawnPoint);
            if (console == null)
            {
                Debug.LogError("P-51 Step 46 failed while building the in-hangar spawn console.");
                return;
            }

            EnsureTargetOffRunway(sourceAircraft);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 46 completed its edits but Unity could not save the scene.");
                return;
            }

            Hanger51BuildTools.PrepareCurrentSceneForBuild(false);
            Selection.activeGameObject = console.gameObject;
            Debug.Log(
                "P-51 Step 46 complete. Added a physical in-hangar FULL P-51 SPAWN button beside the parts terminal. "
                + "Each press creates a completely independent full aircraft plus its own complete Merlin maintenance controller/engine assembly, "
                + "so cowling, engine mount bolts, plugs, cylinder covers, oil, guns, ammunition, landing gear and tires remain fully interactable. "
                + "The gun target was also moved about 35 m sideways off the runway centerline.",
                console);
        }

        [MenuItem("Hanger 51/P-51 Mustang/47 - Validate In-Hangar Full-Service P-51 Spawn Button")]
        public static void ValidateInHangarSpawner()
        {
            bool passed = true;
            GameObject sourceAircraft = GameObject.Find(AircraftRootName);
            HangarAircraftSpawnConsole console = Object.FindFirstObjectByType<HangarAircraftSpawnConsole>();
            GameObject aircraftTemplate = GameObject.Find(AircraftTemplateName);
            EngineAssemblyTransportController[] transports = Object.FindObjectsByType<EngineAssemblyTransportController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            EngineAssemblyTransportController engineTemplate = null;
            for (int index = 0; index < transports.Length; index++)
            {
                if (transports[index] != null && transports[index].gameObject.name == EngineTemplateName)
                {
                    engineTemplate = transports[index];
                    break;
                }
            }

            if (console == null || !console.IsConfigured)
            {
                Debug.LogError("P-51 Step 47 failed: in-hangar spawn console is missing or incomplete.");
                passed = false;
            }
            if (aircraftTemplate == null || aircraftTemplate.activeSelf)
            {
                Debug.LogError("P-51 Step 47 failed: inactive full-aircraft template is missing.");
                passed = false;
            }
            if (engineTemplate == null
                || engineTemplate.gameObject.activeSelf
                || engineTemplate.TransportRoot == null
                || !engineTemplate.TransportRoot.IsChildOf(engineTemplate.transform)
                || engineTemplate.GetComponent<EngineAssemblyStation>() == null
                || !engineTemplate.GetComponent<EngineAssemblyStation>().IsComplete)
            {
                Debug.LogError("P-51 Step 47 failed: independent complete Merlin maintenance template is missing or incomplete.");
                passed = false;
            }

            P51GunTestTarget target = Object.FindFirstObjectByType<P51GunTestTarget>();
            if (sourceAircraft == null || target == null)
            {
                Debug.LogError("P-51 Step 47 failed: source aircraft or gun target is missing.");
                passed = false;
            }
            else
            {
                Vector3 delta = target.transform.position - sourceAircraft.transform.position;
                float lateral = Mathf.Abs(Vector3.Dot(delta, sourceAircraft.transform.right));
                if (lateral < 25f)
                {
                    Debug.LogError($"P-51 Step 47 failed: target is only {lateral:F1} m off the runway line; expected at least 25 m.");
                    passed = false;
                }
            }

            HangarCommercePlayerInteractor commerceInteractor = Object.FindFirstObjectByType<HangarCommercePlayerInteractor>();
            if (commerceInteractor == null)
            {
                Debug.LogError("P-51 Step 47 failed: Player commerce interactor is missing, so the physical button cannot receive E input.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "P-51 Step 47 passed. Physical hangar spawn button, inactive full-aircraft template, independent complete Merlin maintenance template, "
                    + "Player interaction path, spawn point, and off-runway gun target are ready.");
            }
        }

        private static bool CreateIndependentTemplates(
            GameObject sourceAircraft,
            EngineAssemblyTransportController sourceTransport,
            Transform templateContainer,
            out GameObject aircraftTemplate,
            out EngineAssemblyTransportController engineTemplate)
        {
            aircraftTemplate = null;
            engineTemplate = null;

            Transform transportRoot = sourceTransport.TransportRoot;
            Transform originalParent = transportRoot.parent;
            int originalSibling = transportRoot.GetSiblingIndex();
            Vector3 originalWorldPosition = transportRoot.position;
            Quaternion originalWorldRotation = transportRoot.rotation;
            Vector3 originalLocalScale = transportRoot.localScale;

            try
            {
                // Bring the portable engine root back under its maintenance controller
                // only while cloning. Unity can then remap all station/target references
                // internally into the new engine template instead of retaining references
                // to the original aircraft/engine.
                transportRoot.SetParent(sourceTransport.transform, true);

                aircraftTemplate = Object.Instantiate(sourceAircraft);
                aircraftTemplate.name = AircraftTemplateName;
                aircraftTemplate.transform.SetParent(templateContainer, false);
                aircraftTemplate.transform.localPosition = Vector3.zero;
                aircraftTemplate.transform.localRotation = Quaternion.identity;

                GameObject engineTemplateObject = Object.Instantiate(sourceTransport.gameObject);
                engineTemplateObject.name = EngineTemplateName;
                engineTemplateObject.transform.SetParent(templateContainer, false);
                engineTemplateObject.transform.localPosition = new Vector3(0f, -20f, 0f);
                engineTemplateObject.transform.localRotation = Quaternion.identity;
                engineTemplate = engineTemplateObject.GetComponent<EngineAssemblyTransportController>();
            }
            finally
            {
                transportRoot.SetParent(originalParent, true);
                transportRoot.SetPositionAndRotation(originalWorldPosition, originalWorldRotation);
                transportRoot.localScale = originalLocalScale;
                transportRoot.SetSiblingIndex(Mathf.Clamp(originalSibling, 0, originalParent != null ? originalParent.childCount - 1 : 0));
            }

            if (aircraftTemplate == null
                || engineTemplate == null
                || engineTemplate.TransportRoot == null
                || !engineTemplate.TransportRoot.IsChildOf(engineTemplate.transform))
            {
                Debug.LogError("P-51 Step 46 failed: Unity did not remap the independent engine template hierarchy correctly.");
                return false;
            }

            P51AircraftServiceController service = aircraftTemplate.GetComponent<P51AircraftServiceController>();
            if (service == null)
            {
                Debug.LogError("P-51 Step 46 failed: full-aircraft template lost its service controller.");
                return false;
            }
            service.ResetAircraftService();

            EngineConditionController condition = engineTemplate.GetComponent<EngineConditionController>();
            condition?.InitializeNewEngineCondition();
            EngineAssemblyStation station = engineTemplate.GetComponent<EngineAssemblyStation>();
            if (station == null || !station.IsComplete)
            {
                Debug.LogError("P-51 Step 46 failed: cloned Merlin template is not a complete serviceable engine.");
                return false;
            }

            ForceTemplateArmamentLoaded(aircraftTemplate);
            ForceTemplateLandingGearComplete(aircraftTemplate);

            aircraftTemplate.SetActive(false);
            engineTemplate.gameObject.SetActive(false);
            EditorUtility.SetDirty(aircraftTemplate);
            EditorUtility.SetDirty(engineTemplate.gameObject);
            return true;
        }

        private static HangarAircraftSpawnConsole BuildSpawnConsole(
            HangarShopTerminal shopTerminal,
            GameObject aircraftTemplate,
            EngineAssemblyTransportController engineTemplate,
            Transform spawnPoint)
        {
            Material dark = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Aircraft/P51/Materials/DarkAircraftMetal.mat");
            Material aluminum = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Aircraft/P51/Materials/PolishedAluminum.mat");
            Material red = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Aircraft/P51/Materials/MustangRed.mat");
            if (dark == null || aluminum == null || red == null)
            {
                return null;
            }

            Collider shopCollider = shopTerminal.GetComponent<Collider>();
            float floorY = shopCollider != null ? shopCollider.bounds.min.y : shopTerminal.transform.position.y;
            GameObject root = new GameObject(ConsoleName);
            Undo.RegisterCreatedObjectUndo(root, "Create full P-51 spawn console");
            root.transform.position = new Vector3(
                shopTerminal.transform.position.x,
                floorY,
                shopTerminal.transform.position.z)
                + shopTerminal.transform.right * 1.65f;
            root.transform.rotation = shopTerminal.transform.rotation;

            BoxCollider interactionCollider = root.AddComponent<BoxCollider>();
            interactionCollider.center = new Vector3(0f, 0.98f, 0.12f);
            interactionCollider.size = new Vector3(0.72f, 0.72f, 0.44f);

            CreateCube(root.transform, "Spawn Console Pedestal", new Vector3(0f, 0.48f, 0f), new Vector3(0.72f, 0.96f, 0.54f), dark);
            CreateCube(root.transform, "Spawn Console Aluminum Face", new Vector3(0f, 0.98f, 0.20f), new Vector3(0.62f, 0.42f, 0.08f), aluminum);
            GameObject plunger = CreateCylinder(root.transform, "FULL P-51 SPAWN Red Button", new Vector3(0f, 1.00f, 0.275f), new Vector3(0.14f, 0.055f, 0.14f), new Vector3(90f, 0f, 0f), red);

            GameObject labelObject = new GameObject("Full P-51 Spawn Label");
            labelObject.transform.SetParent(root.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 1.32f, 0.285f);
            labelObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = "FULL P-51\nSPAWN";
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 48;
            label.characterSize = 0.032f;
            label.color = Color.white;

            HangarAircraftSpawnConsole console = Undo.AddComponent<HangarAircraftSpawnConsole>(root);
            console.Configure(aircraftTemplate, engineTemplate, spawnPoint, plunger.transform, 13.5f, 8);
            EditorUtility.SetDirty(console);
            return console;
        }

        private static void EnsureRuntimeAircraftSystems(GameObject aircraft)
        {
            P51MerlinAudioPresenceBoost boost = aircraft.GetComponent<P51MerlinAudioPresenceBoost>();
            if (boost == null) boost = Undo.AddComponent<P51MerlinAudioPresenceBoost>(aircraft);
            boost.Configure(2.15f, 16f, 700f);

            P51MerlinLifecycleController lifecycle = aircraft.GetComponent<P51MerlinLifecycleController>();
            if (lifecycle == null) lifecycle = Undo.AddComponent<P51MerlinLifecycleController>(aircraft);
            lifecycle.Configure(3.2f, 2.2f);

            if (aircraft.GetComponent<P51MerlinAudioAndExhaustFxController>() == null)
            {
                Undo.AddComponent<P51MerlinAudioAndExhaustFxController>(aircraft);
            }

            P51WingArmamentSystem system = aircraft.GetComponent<P51WingArmamentSystem>();
            if (system != null)
            {
                Transform[] muzzles = ReadTransformArray(system, "muzzles", 6);
                P51GunTargetHitBridge bridge = aircraft.GetComponent<P51GunTargetHitBridge>();
                if (bridge == null) bridge = Undo.AddComponent<P51GunTargetHitBridge>(aircraft);
                bridge.Configure(system, muzzles);
            }
        }

        private static EngineAssemblyTransportController FindCompleteEngineSource(GameObject sourceAircraft)
        {
            AircraftEngineMountReceiver receiver = sourceAircraft.GetComponent<AircraftEngineMountReceiver>();
            if (receiver != null
                && receiver.InstalledTransport != null
                && receiver.InstalledTransport.GetComponent<EngineAssemblyStation>() != null
                && receiver.InstalledTransport.GetComponent<EngineAssemblyStation>().IsComplete)
            {
                return receiver.InstalledTransport;
            }

            EngineAssemblyTransportController[] transports = Object.FindObjectsByType<EngineAssemblyTransportController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < transports.Length; index++)
            {
                EngineAssemblyTransportController transport = transports[index];
                EngineAssemblyStation station = transport != null
                    ? transport.GetComponent<EngineAssemblyStation>()
                    : null;
                if (transport != null && transport.TransportRoot != null && station != null && station.IsComplete)
                {
                    return transport;
                }
            }
            return null;
        }

        private static void EnsureTargetOffRunway(GameObject aircraft)
        {
            P51GunTestTarget target = Object.FindFirstObjectByType<P51GunTestTarget>();
            if (target == null)
            {
                target = CreateTarget();
            }
            if (target == null)
            {
                return;
            }

            target.transform.position = aircraft.transform.position
                + aircraft.transform.forward * 110f
                + aircraft.transform.right * 35f;
            target.transform.rotation = aircraft.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
            target.ResetTarget();
            EditorUtility.SetDirty(target.transform);
        }

        private static P51GunTestTarget CreateTarget()
        {
            Material aluminum = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Aircraft/P51/Materials/PolishedAluminum.mat");
            Material dark = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Aircraft/P51/Materials/DarkAircraftMetal.mat");
            if (aluminum == null || dark == null)
            {
                return null;
            }

            DestroyExisting(TargetRootName);
            GameObject root = new GameObject(TargetRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create off-runway P-51 gun test target");
            CreateCube(root.transform, "Target Concrete Base", new Vector3(0f, 0.16f, 0f), new Vector3(5.4f, 0.32f, 1.5f), dark);
            CreateCube(root.transform, "Left Target Support", new Vector3(-1.55f, 1.45f, 0f), new Vector3(0.18f, 2.65f, 0.18f), dark);
            CreateCube(root.transform, "Right Target Support", new Vector3(1.55f, 1.45f, 0f), new Vector3(0.18f, 2.65f, 0.18f), dark);
            GameObject plate = CreateCube(root.transform, "Shootable Steel Target Plate", new Vector3(0f, 2.55f, 0f), new Vector3(4.8f, 3.05f, 0.20f), aluminum);

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
            target.Configure(plate.transform, plate.GetComponent<Renderer>(), text, 600f);
            return target;
        }

        private static void ForceTemplateArmamentLoaded(GameObject aircraft)
        {
            P51WingArmamentSystem system = aircraft.GetComponent<P51WingArmamentSystem>();
            if (system == null) return;
            SerializedObject serialized = new SerializedObject(system);
            SetBoolArray(serialized.FindProperty("panelOpen"), 2, false);
            SetBoolArray(serialized.FindProperty("gunInstalled"), 6, true);
            SetBoolArray(serialized.FindProperty("ammoBoxInstalled"), 6, true);
            SerializedProperty ammo = serialized.FindProperty("ammoRemaining");
            SerializedProperty rounds = serialized.FindProperty("gameRoundsPerAmmoBox");
            int full = rounds != null ? Mathf.Max(1, rounds.intValue) : 200;
            if (ammo != null)
            {
                ammo.arraySize = 6;
                for (int index = 0; index < 6; index++) ammo.GetArrayElementAtIndex(index).intValue = full;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ForceTemplateLandingGearComplete(GameObject aircraft)
        {
            P51LandingGearMaintenanceController gear = aircraft.GetComponent<P51LandingGearMaintenanceController>();
            if (gear == null) return;
            SerializedObject serialized = new SerializedObject(gear);
            SetBoolArray(serialized.FindProperty("gearInstalled"), 3, true);
            SetBoolArray(serialized.FindProperty("tireInstalled"), 3, true);
            SetBoolArray(serialized.FindProperty("tireBurst"), 3, false);
            SetFloatArray(serialized.FindProperty("tireHealth"), new[] { 100f, 100f, 100f });
            SetFloatArray(serialized.FindProperty("tirePressurePsi"), new[] { 30f, 30f, 24f });
            SerializedProperty command = serialized.FindProperty("gearCommandDown");
            SerializedProperty deployment = serialized.FindProperty("deploymentFraction");
            if (command != null) command.boolValue = true;
            if (deployment != null) deployment.floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform[] ReadTransformArray(Object target, string fieldName, int size)
        {
            Transform[] result = new Transform[size];
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null || !property.isArray) return result;
            for (int index = 0; index < Mathf.Min(size, property.arraySize); index++)
            {
                result[index] = property.GetArrayElementAtIndex(index).objectReferenceValue as Transform;
            }
            return result;
        }

        private static void SetBoolArray(SerializedProperty property, int size, bool value)
        {
            if (property == null || !property.isArray) return;
            property.arraySize = size;
            for (int index = 0; index < size; index++) property.GetArrayElementAtIndex(index).boolValue = value;
        }

        private static void SetFloatArray(SerializedProperty property, float[] values)
        {
            if (property == null || !property.isArray || values == null) return;
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++) property.GetArrayElementAtIndex(index).floatValue = values[index];
        }

        private static GameObject CreateCube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localScale = localScale;
            Renderer renderer = cube.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            return cube;
        }

        private static GameObject CreateCylinder(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Vector3 localEuler, Material material)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent, false);
            cylinder.transform.localPosition = localPosition;
            cylinder.transform.localRotation = Quaternion.Euler(localEuler);
            cylinder.transform.localScale = localScale;
            Renderer renderer = cylinder.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            Collider collider = cylinder.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            return cylinder;
        }

        private static void DestroyExisting(string name)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null) Undo.DestroyObjectImmediate(existing);
        }
    }
}
