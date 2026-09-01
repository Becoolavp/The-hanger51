using Hanger51.Aircraft;
using Hanger51.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51AftServiceAnimationHighlightAndCrouchSetup
    {
        private const string HighlightRootName = "Aft Equipment Placement Highlight";
        private const string HighlightMaterialPath = "Assets/_Project/Aircraft/P51/Materials/AftServicePlacementHighlight.mat";
        private const string FinderRingPrefix = "Aft Fastener Finder Ring ";
        private const string InteractionAssistName = "Fastener Interaction Assist";

        [MenuItem("Hanger 51/P-51 Mustang/Current/94 - Animate Aft Servicing, Add Placement Guides and Crouch")]
        public static void ApplyAftServiceUsability()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 Step 94 requires Edit mode with Unity finished compiling.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("P-51 Step 94 requires the saved gameplay scene to be open.");
                return;
            }

            Material highlightMaterial = GetOrCreateHighlightMaterial();
            if (highlightMaterial == null)
            {
                Debug.LogError("P-51 Step 94 could not create the placement-guide material.");
                return;
            }

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft == null || aircraft.Length == 0)
            {
                Debug.LogError("P-51 Step 94 could not find any P-51 aircraft in the scene.");
                return;
            }

            int configuredAircraft = 0;
            int configuredSlots = 0;
            int configuredFasteners = 0;
            for (int aircraftIndex = 0; aircraftIndex < aircraft.Length; aircraftIndex++)
            {
                P51FlightController flight = aircraft[aircraftIndex];
                if (flight == null || !flight.gameObject.scene.IsValid())
                {
                    continue;
                }

                P51AftEquipmentBay bay = flight.GetComponentInChildren<P51AftEquipmentBay>(true);
                if (bay == null || bay.AccessPanel == null)
                {
                    Debug.LogWarning($"P-51 Step 94 skipped '{flight.name}' because its aft equipment bay is not configured.", flight);
                    continue;
                }

                P51AftEquipmentSlot[] slots = bay.GetComponentsInChildren<P51AftEquipmentSlot>(true);
                for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
                {
                    P51AftEquipmentSlot slot = slots[slotIndex];
                    if (slot == null)
                    {
                        continue;
                    }
                    BuildPlacementGuide(slot, highlightMaterial);
                    configuredSlots++;
                }

                P51AftPanelFastener[] fasteners = bay.AccessPanel.GetComponentsInChildren<P51AftPanelFastener>(true);
                RemoveOldFinderRings(bay.AccessPanel.transform);
                for (int fastenerIndex = 0; fastenerIndex < fasteners.Length; fastenerIndex++)
                {
                    P51AftPanelFastener fastener = fasteners[fastenerIndex];
                    if (fastener == null)
                    {
                        continue;
                    }
                    ImproveFastenerTarget(fastener, highlightMaterial);
                    configuredFasteners++;
                }

                configuredAircraft++;
            }

            FirstPersonController player = Object.FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            if (player == null)
            {
                Debug.LogError("P-51 Step 94 could not find the FirstPersonController.");
                return;
            }
            Undo.RecordObject(player, "Configure Hanger 51 crouch servicing");
            player.ConfigureCrouch(1.05f, 2.7f, 0.62f, 10f);
            EditorUtility.SetDirty(player);

            P51AftEquipmentPlayerInteractor aftInteractor = player.GetComponent<P51AftEquipmentPlayerInteractor>();
            if (aftInteractor == null)
            {
                Debug.LogError("P-51 Step 94 could not find the aft-equipment player interactor.", player);
                return;
            }
            Undo.RecordObject(aftInteractor, "Configure easier aft service reach");
            aftInteractor.ConfigureServiceReach(4.25f, 0.9f);
            EditorUtility.SetDirty(aftInteractor);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("P-51 Step 94 applied the service-usability changes but Unity could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("P-51 Step 94 completed the service-usability changes, but build preparation failed.");
                return;
            }

            Debug.Log(
                $"P-51 Step 94 complete. Aircraft={configuredAircraft}, placement guides={configuredSlots}, easier fasteners={configuredFasteners}. "
                + "Aft panel/equipment service motions are active, compatible empty rack positions highlight while carried, and hold C crouches for low service access.");
        }

        [MenuItem("Hanger 51/P-51 Mustang/Current/95 - Validate Aft Service Animation, Guides and Crouch")]
        public static void ValidateAftServiceUsability()
        {
            bool passed = true;
            int checkedAircraft = 0;
            int checkedSlots = 0;
            Material highlightMaterial = AssetDatabase.LoadAssetAtPath<Material>(HighlightMaterialPath);
            if (highlightMaterial == null)
            {
                Debug.LogError("P-51 Step 95 failed: the placement-guide material is missing.");
                passed = false;
            }

            P51FlightController[] aircraft = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int aircraftIndex = 0; aircraftIndex < aircraft.Length; aircraftIndex++)
            {
                P51FlightController flight = aircraft[aircraftIndex];
                if (flight == null || !flight.gameObject.scene.IsValid())
                {
                    continue;
                }

                P51AftEquipmentBay bay = flight.GetComponentInChildren<P51AftEquipmentBay>(true);
                if (bay == null || bay.AccessPanel == null)
                {
                    continue;
                }
                checkedAircraft++;

                P51AftEquipmentSlot[] slots = bay.GetComponentsInChildren<P51AftEquipmentSlot>(true);
                for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
                {
                    P51AftEquipmentSlot slot = slots[slotIndex];
                    if (slot == null)
                    {
                        continue;
                    }
                    checkedSlots++;
                    if (slot.PlacementHighlightRoot == null
                        || slot.PlacementHighlightRoot.name != HighlightRootName)
                    {
                        Debug.LogError($"P-51 Step 95 failed: '{flight.name}' slot {slot.SlotIndex} has no placement guide.", slot);
                        passed = false;
                    }
                }

                P51AftPanelFastener[] fasteners = bay.AccessPanel.GetComponentsInChildren<P51AftPanelFastener>(true);
                if (fasteners.Length != 8)
                {
                    Debug.LogError($"P-51 Step 95 failed: '{flight.name}' should have 8 aft-panel fasteners; found {fasteners.Length}.", bay.AccessPanel);
                    passed = false;
                }
                for (int fastenerIndex = 0; fastenerIndex < fasteners.Length; fastenerIndex++)
                {
                    P51AftPanelFastener fastener = fasteners[fastenerIndex];
                    Transform assist = FindDirectChild(fastener.transform, InteractionAssistName);
                    SphereCollider sphere = assist != null ? assist.GetComponent<SphereCollider>() : null;
                    if (assist == null || sphere == null || !sphere.isTrigger)
                    {
                        Debug.LogError($"P-51 Step 95 failed: fastener {fastener.FastenerIndex + 1} on '{flight.name}' is missing its easier trigger target.", fastener);
                        passed = false;
                    }
                }
            }

            FirstPersonController player = Object.FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            P51AftEquipmentPlayerInteractor aftInteractor = player != null
                ? player.GetComponent<P51AftEquipmentPlayerInteractor>()
                : null;
            FirstPersonCameraSmoother smoother = player != null
                ? player.GetComponentInChildren<FirstPersonCameraSmoother>(true)
                : null;

            if (player == null
                || player.ConfiguredCrouchHeight >= player.StandingHeight - 0.05f
                || player.CrouchEyeDrop < 0.30f)
            {
                Debug.LogError("P-51 Step 95 failed: hold-C crouch is not configured with a useful lower service height.");
                passed = false;
            }
            if (smoother == null)
            {
                Debug.LogError("P-51 Step 95 failed: the first-person camera smoother is missing, so crouch camera height cannot follow the player capsule.");
                passed = false;
            }
            if (aftInteractor == null || aftInteractor.InteractionDistance < 4.0f)
            {
                Debug.LogError("P-51 Step 95 failed: the aft service interactor is missing or its easier service reach was not applied.");
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    $"P-51 Step 95 passed. Aircraft={checkedAircraft}, guided slots={checkedSlots}. Animated aft servicing, pulsing compatible placement guides, "
                    + "larger/easier fastener targets and hold-C crouch are configured.");
            }
        }

        private static Material GetOrCreateHighlightMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(HighlightMaterialPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }
            if (shader == null)
            {
                return null;
            }

            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "Aft Service Placement Highlight"
                };
                AssetDatabase.CreateAsset(material, HighlightMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            Color color = new Color(1f, 0.62f, 0.06f, 1f);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildPlacementGuide(P51AftEquipmentSlot slot, Material material)
        {
            Transform old = FindDirectChild(slot.transform, HighlightRootName);
            if (old != null)
            {
                Undo.DestroyObjectImmediate(old.gameObject);
            }

            GameObject rootObject = new GameObject(HighlightRootName);
            Undo.RegisterCreatedObjectUndo(rootObject, "Create aft equipment placement guide");
            Transform root = rootObject.transform;
            root.SetParent(slot.transform, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;

            Vector3 dimensions = slot.AcceptedKind == P51AftEquipmentKind.Battery
                ? new Vector3(0.48f, 0.36f, 0.44f)
                : new Vector3(0.30f, 0.68f, 0.30f);
            CreateWireCage(root, dimensions, 0.018f, material);
            rootObject.SetActive(false);
            slot.ConfigurePlacementHighlight(rootObject);
            EditorUtility.SetDirty(slot);
        }

        private static void CreateWireCage(Transform parent, Vector3 size, float thickness, Material material)
        {
            float hx = size.x * 0.5f;
            float hy = size.y * 0.5f;
            float hz = size.z * 0.5f;

            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    CreateGuideBeam(parent, new Vector3(0f, y * hy, z * hz), new Vector3(size.x, thickness, thickness), material);
                }
            }
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    CreateGuideBeam(parent, new Vector3(x * hx, 0f, z * hz), new Vector3(thickness, size.y, thickness), material);
                }
            }
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    CreateGuideBeam(parent, new Vector3(x * hx, y * hy, 0f), new Vector3(thickness, thickness, size.z), material);
                }
            }
        }

        private static void CreateGuideBeam(Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(beam, "Create aft placement-guide beam");
            beam.name = "Placement Guide Beam";
            beam.transform.SetParent(parent, false);
            beam.transform.localPosition = localPosition;
            beam.transform.localRotation = Quaternion.identity;
            beam.transform.localScale = localScale;
            Renderer renderer = beam.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            Collider collider = beam.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
        }

        private static void ImproveFastenerTarget(P51AftPanelFastener fastener, Material highlightMaterial)
        {
            Transform fastenerTransform = fastener.transform;
            Undo.RecordObject(fastenerTransform, "Make aft panel fastener easier to see");
            fastenerTransform.localScale = new Vector3(0.036f, 0.010f, 0.036f);

            Transform oldAssist = FindDirectChild(fastenerTransform, InteractionAssistName);
            if (oldAssist != null)
            {
                Undo.DestroyObjectImmediate(oldAssist.gameObject);
            }
            GameObject assistObject = new GameObject(InteractionAssistName);
            Undo.RegisterCreatedObjectUndo(assistObject, "Create easier aft fastener target");
            assistObject.transform.SetParent(fastenerTransform, false);
            assistObject.transform.localPosition = Vector3.zero;
            assistObject.transform.localRotation = Quaternion.identity;
            assistObject.transform.localScale = Vector3.one;
            SphereCollider sphere = Undo.AddComponent<SphereCollider>(assistObject);
            sphere.radius = 2.0f;
            sphere.isTrigger = true;

            P51AftAccessPanel panel = fastener.GetComponentInParent<P51AftAccessPanel>();
            if (panel != null)
            {
                GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Undo.RegisterCreatedObjectUndo(ring, "Create aft fastener finder ring");
                ring.name = FinderRingPrefix + (fastener.FastenerIndex + 1);
                ring.transform.SetParent(panel.transform, false);
                ring.transform.localPosition = fastenerTransform.localPosition
                    - (fastenerTransform.localRotation * Vector3.up) * 0.010f;
                ring.transform.localRotation = fastenerTransform.localRotation;
                ring.transform.localScale = new Vector3(0.052f, 0.004f, 0.052f);
                Renderer ringRenderer = ring.GetComponent<Renderer>();
                if (ringRenderer != null)
                {
                    ringRenderer.sharedMaterial = highlightMaterial;
                    ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    ringRenderer.receiveShadows = false;
                }
                Collider ringCollider = ring.GetComponent<Collider>();
                if (ringCollider != null)
                {
                    Object.DestroyImmediate(ringCollider);
                }
            }

            EditorUtility.SetDirty(fastenerTransform);
            EditorUtility.SetDirty(fastener);
        }

        private static void RemoveOldFinderRings(Transform panel)
        {
            if (panel == null)
            {
                return;
            }
            for (int index = panel.childCount - 1; index >= 0; index--)
            {
                Transform child = panel.GetChild(index);
                if (child != null && child.name.StartsWith(FinderRingPrefix, System.StringComparison.Ordinal))
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (child != null && child.name == name)
                {
                    return child;
                }
            }
            return null;
        }
    }
}
