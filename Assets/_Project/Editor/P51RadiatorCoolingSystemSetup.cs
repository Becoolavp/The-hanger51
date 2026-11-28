using System;
using Hanger51.Aircraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51RadiatorCoolingSystemSetup
    {
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string RadiatorRootName = "P-51 Functional Belly Radiator";
        private const string JugRootName = "Hanger 51 Coolant Jugs";
        private const string AirportRootName = "Hanger 51 Airport Complex";
        private const float CoolantCapacityLiters = 98f;

        private const string AluminumPath =
            "Assets/_Project/Aircraft/P51/Materials/PolishedAluminum.mat";
        private const string DarkPath =
            "Assets/_Project/Aircraft/P51/Materials/DarkAircraftMetal.mat";
        private const string HardwarePath =
            "Assets/_Project/Aircraft/P51/Materials/ServiceHardware.mat";
        private const string CoolantMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/RadiatorCoolantBlue.mat";

        [MenuItem("Hanger 51/P-51 Mustang/63 - Build Functional Radiator and Coolant System")]
        public static void BuildFunctionalRadiatorAndCoolantSystem()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 Step 63 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("P-51 Step 63 failed. Open the saved Hanger 51 movement-test scene first.");
                return;
            }

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 63 failed. No P-51 flight controller exists in the scene.");
                return;
            }

            Material aluminum = AssetDatabase.LoadAssetAtPath<Material>(AluminumPath);
            Material dark = AssetDatabase.LoadAssetAtPath<Material>(DarkPath);
            Material hardware = AssetDatabase.LoadAssetAtPath<Material>(HardwarePath);
            Material coolant = GetOrCreateCoolantMaterial();
            if (aluminum == null || dark == null || hardware == null || coolant == null)
            {
                Debug.LogError("P-51 Step 63 failed. Required P-51 materials could not be loaded or created.");
                return;
            }

            int installed = 0;
            P51FlightController master = null;
            for (int index = 0; index < aircraft.Length; index++)
            {
                P51FlightController flight = aircraft[index];
                if (flight == null || !flight.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (flight.name == AircraftRootName)
                {
                    master = flight;
                }

                BuildAircraftRadiator(flight, aluminum, dark, hardware, coolant);
                installed++;
            }

            if (master == null)
            {
                master = aircraft[0];
            }

            InstallPlayerCoolantInteractor();
            CreateCoolantJugs(master, aluminum, dark, hardware, coolant);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 63 made the radiator changes but Unity could not save the scene.");
                return;
            }

            Hanger51BuildTools.PrepareCurrentSceneForBuild(false);
            Selection.activeGameObject = master != null ? master.gameObject : null;

            Debug.Log(
                $"P-51 Step 63 complete. Functional belly radiator/coolant systems installed={installed}. "
                + $"Each system carries {CoolantCapacityLiters:F0} L of coolant, automatically positions its exit door from temperature, "
                + "loses coolant when the core is damaged, overheats and damages the installed Merlin when cooling is inadequate, and has a removable service cap/filler. "
                + "Two portable 10 L coolant jugs were added near the master P-51.",
                master != null ? master.gameObject : null);
        }

        [MenuItem("Hanger 51/P-51 Mustang/64 - Validate Functional Radiator and Coolant System")]
        public static void ValidateFunctionalRadiatorAndCoolantSystem()
        {
            bool passed = true;
            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 64 failed. No P-51 aircraft were found.");
                return;
            }

            int validAircraft = 0;
            for (int index = 0; index < aircraft.Length; index++)
            {
                P51FlightController flight = aircraft[index];
                if (flight == null || !flight.gameObject.scene.IsValid()) continue;

                P51RadiatorCoolingSystem system = flight.GetComponent<P51RadiatorCoolingSystem>();
                Transform root = FindDescendant(flight.transform, RadiatorRootName);
                P51RadiatorDamageReceiver receiver =
                    root != null ? root.GetComponentInChildren<P51RadiatorDamageReceiver>(true) : null;
                P51CoolantCap cap = root != null ? root.GetComponentInChildren<P51CoolantCap>(true) : null;
                P51CoolantFiller filler = root != null ? root.GetComponentInChildren<P51CoolantFiller>(true) : null;
                Transform door = root != null ? FindDescendant(root, "Radiator Exit Door Pivot") : null;

                if (system == null
                    || root == null
                    || receiver == null
                    || cap == null
                    || filler == null
                    || door == null)
                {
                    Debug.LogError(
                        $"P-51 Step 64 failed. '{flight.name}' is missing radiator system, model root, damage core, cap, filler, or automatic exit door.",
                        flight);
                    passed = false;
                    continue;
                }

                if (Mathf.Abs(system.CoolantCapacityLiters - CoolantCapacityLiters) > 0.1f)
                {
                    Debug.LogError(
                        $"P-51 Step 64 failed. '{flight.name}' coolant capacity is {system.CoolantCapacityLiters:F1} L instead of {CoolantCapacityLiters:F0} L.",
                        system);
                    passed = false;
                    continue;
                }

                Collider damageCollider = receiver.GetComponent<Collider>();
                if (damageCollider == null || damageCollider.isTrigger)
                {
                    Debug.LogError(
                        $"P-51 Step 64 failed. '{flight.name}' radiator core needs a small solid internal damage collider.",
                        receiver);
                    passed = false;
                    continue;
                }

                validAircraft++;
            }

            P51CoolantPlayerInteractor playerInteractor =
                Object.FindFirstObjectByType<P51CoolantPlayerInteractor>(FindObjectsInactive.Include);
            P51CoolantJug[] jugs = Object.FindObjectsByType<P51CoolantJug>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (playerInteractor == null)
            {
                Debug.LogError("P-51 Step 64 failed. Player coolant-service interactor is missing.");
                passed = false;
            }
            if (jugs.Length < 2)
            {
                Debug.LogError($"P-51 Step 64 failed. Expected at least two coolant jugs; found {jugs.Length}.");
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("P-51 Step 64 failed. Standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"P-51 Step 64 passed. Functional radiators={validAircraft}, portable coolant jugs={jugs.Length}. "
                    + "Radiator cores are damageable, coolant leak/temperature logic is present, exit doors are animated by temperature, "
                    + "coolant caps/fillers are serviceable, and the system is attached to each aircraft for live-master spawn inheritance.");
            }
        }

        private static void BuildAircraftRadiator(
            P51FlightController flight,
            Material aluminum,
            Material dark,
            Material hardware,
            Material coolant)
        {
            Transform oldRoot = flight.transform.Find(RadiatorRootName);
            if (oldRoot != null)
            {
                Undo.DestroyObjectImmediate(oldRoot.gameObject);
            }

            P51RadiatorCoolingSystem system = flight.GetComponent<P51RadiatorCoolingSystem>();
            if (system == null)
            {
                system = Undo.AddComponent<P51RadiatorCoolingSystem>(flight.gameObject);
            }

            GameObject rootObject = new GameObject(RadiatorRootName);
            Undo.RegisterCreatedObjectUndo(rootObject, "Build P-51 functional belly radiator");
            rootObject.transform.SetParent(flight.transform, false);
            Transform root = rootObject.transform;

            // P-51-style ventral scoop: open intake lip, tapered-looking side walls, an
            // internal radiator face, plumbing/reservoir detail and a hinged rear exit door.
            CreatePart(root, PrimitiveType.Cube, "Radiator Scoop Top Duct",
                new Vector3(0f, 0.92f, -1.55f), new Vector3(0.88f, 0.09f, 1.66f), Vector3.zero, aluminum, false);
            CreatePart(root, PrimitiveType.Cube, "Radiator Scoop Left Wall",
                new Vector3(-0.43f, 0.72f, -1.52f), new Vector3(0.08f, 0.40f, 1.62f), new Vector3(0f, 0f, -3f), aluminum, false);
            CreatePart(root, PrimitiveType.Cube, "Radiator Scoop Right Wall",
                new Vector3(0.43f, 0.72f, -1.52f), new Vector3(0.08f, 0.40f, 1.62f), new Vector3(0f, 0f, 3f), aluminum, false);
            CreatePart(root, PrimitiveType.Cube, "Radiator Intake Lower Lip",
                new Vector3(0f, 0.56f, -0.72f), new Vector3(0.86f, 0.08f, 0.16f), new Vector3(-6f, 0f, 0f), hardware, false);
            CreatePart(root, PrimitiveType.Cube, "Radiator Intake Upper Lip",
                new Vector3(0f, 0.92f, -0.72f), new Vector3(0.86f, 0.07f, 0.16f), new Vector3(5f, 0f, 0f), hardware, false);

            GameObject coreObject = new GameObject("Radiator Core Damage Volume");
            coreObject.transform.SetParent(root, false);
            coreObject.transform.localPosition = new Vector3(0f, 0.73f, -1.38f);
            BoxCollider coreCollider = coreObject.AddComponent<BoxCollider>();
            coreCollider.size = new Vector3(0.72f, 0.30f, 0.10f);
            coreCollider.isTrigger = false;
            P51RadiatorDamageReceiver damageReceiver =
                coreObject.AddComponent<P51RadiatorDamageReceiver>();
            damageReceiver.Configure(system);

            CreatePart(coreObject.transform, PrimitiveType.Cube, "Radiator Dark Core",
                Vector3.zero, new Vector3(0.70f, 0.29f, 0.06f), Vector3.zero, dark, false);
            for (int index = -4; index <= 4; index++)
            {
                CreatePart(coreObject.transform, PrimitiveType.Cube, $"Radiator Core Vertical Fin {index + 5}",
                    new Vector3(index * 0.075f, 0f, -0.038f), new Vector3(0.012f, 0.28f, 0.012f), Vector3.zero, hardware, false);
            }
            for (int index = -2; index <= 2; index++)
            {
                CreatePart(coreObject.transform, PrimitiveType.Cube, $"Radiator Core Horizontal Fin {index + 3}",
                    new Vector3(0f, index * 0.052f, -0.040f), new Vector3(0.68f, 0.010f, 0.010f), Vector3.zero, hardware, false);
            }

            CreatePart(root, PrimitiveType.Cylinder, "Coolant Header Tank",
                new Vector3(0f, 0.85f, -1.72f), new Vector3(0.26f, 0.36f, 0.26f), new Vector3(0f, 0f, 90f), aluminum, false);
            CreatePart(root, PrimitiveType.Cylinder, "Coolant Feed Pipe Left",
                new Vector3(-0.29f, 0.82f, -1.57f), new Vector3(0.055f, 0.34f, 0.055f), new Vector3(0f, 0f, 62f), hardware, false);
            CreatePart(root, PrimitiveType.Cylinder, "Coolant Feed Pipe Right",
                new Vector3(0.29f, 0.82f, -1.57f), new Vector3(0.055f, 0.34f, 0.055f), new Vector3(0f, 0f, -62f), hardware, false);
            CreatePart(root, PrimitiveType.Cube, "Visible Coolant Sight Detail",
                new Vector3(0f, 0.85f, -1.96f), new Vector3(0.26f, 0.16f, 0.035f), Vector3.zero, coolant, false);

            GameObject doorPivotObject = new GameObject("Radiator Exit Door Pivot");
            doorPivotObject.transform.SetParent(root, false);
            doorPivotObject.transform.localPosition = new Vector3(0f, 0.56f, -2.30f);
            Transform doorPivot = doorPivotObject.transform;
            CreatePart(doorPivot, PrimitiveType.Cube, "Radiator Exit Door",
                new Vector3(0f, 0f, 0.30f), new Vector3(0.82f, 0.055f, 0.62f), Vector3.zero, aluminum, false);
            CreatePart(doorPivot, PrimitiveType.Cube, "Radiator Exit Door Reinforcement",
                new Vector3(0f, 0.032f, 0.30f), new Vector3(0.72f, 0.022f, 0.05f), Vector3.zero, hardware, false);

            ParticleSystem leakEffect = BuildLeakEffect(root, coolant);
            BuildCoolantFillerAndCap(root, system, aluminum, hardware);

            system.Configure(
                flight,
                doorPivot,
                Vector3.zero,
                new Vector3(-38f, 0f, 0f),
                leakEffect,
                CoolantCapacityLiters);
            EditorUtility.SetDirty(system);
        }

        private static void BuildCoolantFillerAndCap(
            Transform root,
            P51RadiatorCoolingSystem system,
            Material aluminum,
            Material hardware)
        {
            Vector3 fillerPosition = new Vector3(0.43f, 0.98f, -1.72f);

            GameObject neck = CreatePart(root, PrimitiveType.Cylinder, "Radiator Coolant Filler Neck",
                fillerPosition, new Vector3(0.085f, 0.055f, 0.085f), Vector3.zero, hardware, false);
            BoxCollider fillerCollider = neck.AddComponent<BoxCollider>();
            fillerCollider.isTrigger = true;
            fillerCollider.size = new Vector3(2.8f, 2.4f, 2.8f);

            GameObject capObject = CreatePart(root, PrimitiveType.Cylinder, "Radiator Coolant Cap",
                fillerPosition + Vector3.up * 0.082f,
                new Vector3(0.11f, 0.035f, 0.11f), Vector3.zero, aluminum, false);
            BoxCollider capCollider = capObject.AddComponent<BoxCollider>();
            capCollider.isTrigger = true;
            capCollider.size = new Vector3(2.6f, 2.0f, 2.6f);

            Vector3 installed = capObject.transform.localPosition;
            Vector3 removed = installed + new Vector3(0.24f, -0.02f, -0.05f);
            P51CoolantCap cap = capObject.AddComponent<P51CoolantCap>();
            cap.Configure(
                system,
                capObject.transform,
                installed,
                Vector3.zero,
                removed,
                new Vector3(72f, 18f, 10f));

            P51CoolantFiller filler = neck.AddComponent<P51CoolantFiller>();
            filler.Configure(system, cap, 2.2f);
        }

        private static ParticleSystem BuildLeakEffect(Transform root, Material coolantMaterial)
        {
            GameObject effectObject = new GameObject("Radiator Coolant Leak FX");
            effectObject.transform.SetParent(root, false);
            effectObject.transform.localPosition = new Vector3(0f, 0.55f, -1.45f);
            effectObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            ParticleSystem particles = effectObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true;
            main.startLifetime = 0.75f;
            main.startSpeed = 1.25f;
            main.startSize = 0.035f;
            main.startColor = new Color(0.20f, 0.75f, 0.95f, 0.82f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 160;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 7f;
            shape.radius = 0.04f;

            ParticleSystemRenderer renderer = effectObject.GetComponent<ParticleSystemRenderer>();
            if (renderer != null && coolantMaterial != null)
            {
                renderer.sharedMaterial = coolantMaterial;
            }
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return particles;
        }

        private static void InstallPlayerCoolantInteractor()
        {
            P51PilotPlayerInteractor pilot =
                Object.FindFirstObjectByType<P51PilotPlayerInteractor>(FindObjectsInactive.Include);
            if (pilot == null)
            {
                Debug.LogWarning("P-51 Step 63 could not find the Player pilot interactor, so coolant interaction was not installed.");
                return;
            }

            P51CoolantPlayerInteractor coolantInteractor =
                pilot.GetComponent<P51CoolantPlayerInteractor>();
            if (coolantInteractor == null)
            {
                coolantInteractor = Undo.AddComponent<P51CoolantPlayerInteractor>(pilot.gameObject);
            }
            EditorUtility.SetDirty(coolantInteractor);
        }

        private static void CreateCoolantJugs(
            P51FlightController master,
            Material aluminum,
            Material dark,
            Material hardware,
            Material coolant)
        {
            if (master == null)
            {
                return;
            }

            GameObject existing = FindSceneObjectByExactName(JugRootName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            GameObject rootObject = new GameObject(JugRootName);
            Undo.RegisterCreatedObjectUndo(rootObject, "Create Hanger 51 coolant jugs");
            GameObject airport = FindSceneObjectByExactName(AirportRootName);
            if (airport != null)
            {
                rootObject.transform.SetParent(airport.transform, true);
            }

            for (int index = 0; index < 2; index++)
            {
                Vector3 worldPosition = master.transform.position
                    + master.transform.right * (3.0f + index * 0.75f)
                    + master.transform.forward * -0.8f
                    + Vector3.up * 0.42f;

                GameObject jugObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Undo.RegisterCreatedObjectUndo(jugObject, "Create coolant jug");
                jugObject.name = $"10 L Engine Coolant Jug {index + 1}";
                jugObject.transform.SetParent(rootObject.transform, true);
                jugObject.transform.SetPositionAndRotation(
                    worldPosition,
                    Quaternion.Euler(0f, master.transform.eulerAngles.y, 0f));
                jugObject.transform.localScale = new Vector3(0.34f, 0.48f, 0.22f);
                Renderer bodyRenderer = jugObject.GetComponent<Renderer>();
                if (bodyRenderer != null) bodyRenderer.sharedMaterial = coolant;

                Rigidbody body = jugObject.AddComponent<Rigidbody>();
                body.mass = 4.5f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.Continuous;

                P51CoolantJug jug = jugObject.AddComponent<P51CoolantJug>();
                jug.Configure(10f, 10f);

                CreatePart(jugObject.transform, PrimitiveType.Cylinder, "Coolant Jug Cap",
                    new Vector3(0.25f, 0.55f, 0f), new Vector3(0.12f, 0.08f, 0.12f), Vector3.zero, hardware, false);
                CreatePart(jugObject.transform, PrimitiveType.Cube, "Coolant Jug Handle Top",
                    new Vector3(-0.12f, 0.56f, 0f), new Vector3(0.38f, 0.07f, 0.12f), Vector3.zero, dark, false);
                CreatePart(jugObject.transform, PrimitiveType.Cube, "Coolant Jug Handle Side",
                    new Vector3(-0.28f, 0.36f, 0f), new Vector3(0.07f, 0.34f, 0.12f), Vector3.zero, dark, false);
                CreatePart(jugObject.transform, PrimitiveType.Cube, "Coolant Jug Label",
                    new Vector3(0f, 0f, -0.515f), new Vector3(0.64f, 0.30f, 0.03f), Vector3.zero, aluminum, false);
            }
        }

        private static Material GetOrCreateCoolantMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(CoolantMaterialPath);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            if (shader == null)
            {
                return null;
            }

            material = new Material(shader)
            {
                name = "Radiator Coolant Blue",
                color = new Color(0.10f, 0.58f, 0.82f, 0.90f)
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", new Color(0.10f, 0.58f, 0.82f, 0.90f));
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.30f);
            }
            AssetDatabase.CreateAsset(material, CoolantMaterialPath);
            return material;
        }

        private static GameObject CreatePart(
            Transform parent,
            PrimitiveType type,
            string objectName,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEuler,
            Material material,
            bool keepCollider)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = objectName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(localEuler);
            part.transform.localScale = localScale;

            Collider collider = part.GetComponent<Collider>();
            if (!keepCollider && collider != null)
            {
                Object.DestroyImmediate(collider);
            }
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }
            return part;
        }

        private static Transform FindDescendant(Transform root, string exactName)
        {
            if (root == null) return null;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < all.Length; index++)
            {
                if (all[index] != null && all[index].name == exactName)
                {
                    return all[index];
                }
            }
            return null;
        }

        private static GameObject FindSceneObjectByExactName(string exactName)
        {
            GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Transform[] all = roots[rootIndex].GetComponentsInChildren<Transform>(true);
                for (int index = 0; index < all.Length; index++)
                {
                    if (all[index] != null && all[index].name == exactName)
                    {
                        return all[index].gameObject;
                    }
                }
            }
            return null;
        }
    }
}
