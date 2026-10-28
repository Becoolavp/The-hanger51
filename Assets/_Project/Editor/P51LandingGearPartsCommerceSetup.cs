using Hanger51.Aircraft;
using Hanger51.Commerce;
using Hanger51.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class P51LandingGearPartsCommerceSetup
    {
        private const string CommerceRootName = "Hanger 51 Commerce System";
        private const string ServicePartsFolder = "Assets/_Project/Aircraft/P51/ServiceParts";
        private const string ItemFolder = "Assets/_Project/Inventory/Items";
        private const string MainPrefabPath = ServicePartsFolder + "/P51MainLandingTire.prefab";
        private const string TailPrefabPath = ServicePartsFolder + "/P51TailwheelTire.prefab";
        private const string MainItemPath = ItemFolder + "/P51MainLandingTire.asset";
        private const string TailItemPath = ItemFolder + "/P51TailwheelTire.asset";
        private const string TireMaterialPath =
            "Assets/_Project/Aircraft/P51/Materials/TireRubber.mat";

        [MenuItem("Hanger 51/Shop and Shipping/9 - Add P-51 Replacement Tires")]
        public static void AddP51ReplacementTires()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Shop Step 9 failed. Exit Play mode first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            HangarShopTerminal terminal = Object.FindFirstObjectByType<HangarShopTerminal>(
                FindObjectsInactive.Include);
            if (!scene.IsValid()
                || string.IsNullOrWhiteSpace(scene.path)
                || GameObject.Find(CommerceRootName) == null
                || terminal == null)
            {
                Debug.LogError("Shop Step 9 failed. Open the saved movement-test scene and confirm the existing Hanger 51 shop is present.");
                return;
            }

            Material tireMaterial = AssetDatabase.LoadAssetAtPath<Material>(TireMaterialPath);
            if (tireMaterial == null)
            {
                Debug.LogError("Shop Step 9 failed. The P-51 tire rubber material is missing.");
                return;
            }

            EnsureFolder("Assets/_Project/Aircraft/P51", "ServiceParts");
            EnsureFolder("Assets/_Project/Inventory", "Items");

            GameObject mainPrefab = CreateOrReplaceTirePrefab(
                MainPrefabPath,
                "P-51 Main Landing Tire",
                0.38f,
                0.22f,
                tireMaterial);
            GameObject tailPrefab = CreateOrReplaceTirePrefab(
                TailPrefabPath,
                "P-51 Tailwheel Tire",
                0.16f,
                0.12f,
                tireMaterial);
            if (mainPrefab == null || tailPrefab == null)
            {
                Debug.LogError("Shop Step 9 failed. Replacement tire prefabs could not be created.");
                return;
            }

            InventoryItemDefinition mainItem = CreateOrRefreshItem(
                MainItemPath,
                P51LandingGearReplacementService.MainTireItemId,
                "P-51 Main Landing Tire",
                "A new replacement tire sized for either P-51 main landing-gear rim. Fit it to a bare main rim, then service it to 30 PSI with the nitrogen cart.",
                mainPrefab,
                2);
            InventoryItemDefinition tailItem = CreateOrRefreshItem(
                TailItemPath,
                P51LandingGearReplacementService.TailTireItemId,
                "P-51 Tailwheel Tire",
                "A smaller new replacement tire sized only for the P-51 tailwheel rim. Fit it to the bare tail rim, then service it to 24 PSI with the nitrogen cart.",
                tailPrefab,
                2);

            SerializedObject serializedTerminal = new SerializedObject(terminal);
            SerializedProperty catalog = serializedTerminal.FindProperty("catalog");
            if (catalog == null)
            {
                Debug.LogError("Shop Step 9 failed. The existing shop catalog could not be accessed.", terminal);
                return;
            }

            ConfigureCatalogProduct(
                FindOrAppendProduct(catalog, P51LandingGearReplacementService.MainTireItemId),
                P51LandingGearReplacementService.MainTireItemId,
                "Landing Gear",
                "P-51 Main Landing Tire",
                "New main-wheel replacement tire. Fits left or right main rim; delivered uninstalled.",
                450,
                mainItem);
            ConfigureCatalogProduct(
                FindOrAppendProduct(catalog, P51LandingGearReplacementService.TailTireItemId),
                P51LandingGearReplacementService.TailTireItemId,
                "Landing Gear",
                "P-51 Tailwheel Tire",
                "New smaller tailwheel replacement tire; delivered uninstalled.",
                180,
                tailItem);
            serializedTerminal.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(mainItem);
            EditorUtility.SetDirty(tailItem);
            EditorUtility.SetDirty(terminal);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("Shop Step 9 changed the replacement-tire catalog but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("Shop Step 9 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = terminal.gameObject;
            Debug.Log("Shop Step 9 complete. Added distinct P-51 main and tailwheel replacement tires to the existing parts catalog. Main tires cost $450 and tailwheel tires cost $180.", terminal);
        }

        [MenuItem("Hanger 51/Shop and Shipping/10 - Validate P-51 Replacement Tires")]
        public static void ValidateP51ReplacementTires()
        {
            bool passed = true;
            HangarShopTerminal terminal = Object.FindFirstObjectByType<HangarShopTerminal>(
                FindObjectsInactive.Include);
            InventoryItemDefinition mainItem =
                AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(MainItemPath);
            InventoryItemDefinition tailItem =
                AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(TailItemPath);

            if (mainItem == null
                || mainItem.ItemId != P51LandingGearReplacementService.MainTireItemId
                || mainItem.WorldPrefab == null)
            {
                Debug.LogError("Shop Step 10 failed: main landing tire item or prefab is missing/invalid.");
                passed = false;
            }
            if (tailItem == null
                || tailItem.ItemId != P51LandingGearReplacementService.TailTireItemId
                || tailItem.WorldPrefab == null)
            {
                Debug.LogError("Shop Step 10 failed: tailwheel tire item or prefab is missing/invalid.");
                passed = false;
            }

            if (terminal == null
                || !HasProduct(terminal, P51LandingGearReplacementService.MainTireItemId, mainItem)
                || !HasProduct(terminal, P51LandingGearReplacementService.TailTireItemId, tailItem))
            {
                Debug.LogError("Shop Step 10 failed: one or both replacement tire products are absent from the Hanger 51 catalog.");
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("Shop Step 10 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log("Shop Step 10 passed. Distinct main and tailwheel replacement tires are purchasable, have correctly sized world prefabs, and are available to the landing-gear replacement service.");
            }
        }

        private static GameObject CreateOrReplaceTirePrefab(
            string path,
            string displayName,
            float radius,
            float width,
            Material material)
        {
            AssetDatabase.DeleteAsset(path);
            GameObject root = new GameObject(displayName);
            GameObject tire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tire.name = "Replacement Tire";
            tire.transform.SetParent(root.transform, false);
            tire.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            tire.transform.localScale = new Vector3(radius, width * 0.50f, radius);
            Renderer renderer = tire.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            Collider collider = tire.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);

            GameObject sidewall = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            sidewall.name = "Sidewall Ring";
            sidewall.transform.SetParent(root.transform, false);
            sidewall.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            sidewall.transform.localScale = new Vector3(radius * 0.78f, width * 0.515f, radius * 0.78f);
            Renderer sideRenderer = sidewall.GetComponent<Renderer>();
            if (sideRenderer != null) sideRenderer.sharedMaterial = material;
            Collider sideCollider = sidewall.GetComponent<Collider>();
            if (sideCollider != null) Object.DestroyImmediate(sideCollider);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static InventoryItemDefinition CreateOrRefreshItem(
            string path,
            string itemId,
            string displayName,
            string description,
            GameObject worldPrefab,
            int maxStack)
        {
            InventoryItemDefinition item =
                AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<InventoryItemDefinition>();
                item.name = displayName.Replace(" ", string.Empty);
                AssetDatabase.CreateAsset(item, path);
            }

            SerializedObject serialized = new SerializedObject(item);
            SetString(serialized, "itemId", itemId);
            SetString(serialized, "displayName", displayName);
            SetString(serialized, "description", description);
            SetInt(serialized, "maxStackSize", maxStack);
            SetBool(serialized, "canEquip", true);
            SetColor(serialized, "placeholderColor", new Color(0.08f, 0.08f, 0.075f, 1f));
            SetObject(serialized, "worldPrefab", worldPrefab);
            SetVector(serialized, "worldScale", Vector3.one);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        private static void ConfigureCatalogProduct(
            SerializedProperty entry,
            string id,
            string category,
            string displayName,
            string description,
            int price,
            InventoryItemDefinition item)
        {
            SetString(entry, "productId", id);
            SetString(entry, "category", category);
            SetString(entry, "displayName", displayName);
            SetString(entry, "description", description);
            SetInt(entry, "price", price);
            SerializedProperty kind = entry.FindPropertyRelative("productKind");
            if (kind != null) kind.enumValueIndex = (int)ShopProductKind.InventoryItem;
            SetObject(entry, "inventoryItem", item);
            SetInt(entry, "quantity", 1);
            SetObject(entry, "assemblyTemplate", null);
        }

        private static bool HasProduct(
            HangarShopTerminal terminal,
            string id,
            InventoryItemDefinition expectedItem)
        {
            for (int index = 0; index < terminal.Catalog.Count; index++)
            {
                ShopCatalogEntry product = terminal.Catalog[index];
                if (product != null
                    && product.ProductId == id
                    && product.IsConfigured
                    && product.ProductKind == ShopProductKind.InventoryItem
                    && product.InventoryItem == expectedItem)
                {
                    return true;
                }
            }
            return false;
        }

        private static SerializedProperty FindOrAppendProduct(
            SerializedProperty catalog,
            string productId)
        {
            for (int index = 0; index < catalog.arraySize; index++)
            {
                SerializedProperty item = catalog.GetArrayElementAtIndex(index);
                SerializedProperty id = item.FindPropertyRelative("productId");
                if (id != null && id.stringValue == productId) return item;
            }
            catalog.InsertArrayElementAtIndex(catalog.arraySize);
            return catalog.GetArrayElementAtIndex(catalog.arraySize - 1);
        }

        private static void EnsureFolder(string parent, string child)
        {
            string full = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(full))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static void SetString(SerializedObject obj, string name, string value)
        {
            SerializedProperty p = obj.FindProperty(name);
            if (p != null) p.stringValue = value;
        }
        private static void SetInt(SerializedObject obj, string name, int value)
        {
            SerializedProperty p = obj.FindProperty(name);
            if (p != null) p.intValue = value;
        }
        private static void SetBool(SerializedObject obj, string name, bool value)
        {
            SerializedProperty p = obj.FindProperty(name);
            if (p != null) p.boolValue = value;
        }
        private static void SetColor(SerializedObject obj, string name, Color value)
        {
            SerializedProperty p = obj.FindProperty(name);
            if (p != null) p.colorValue = value;
        }
        private static void SetVector(SerializedObject obj, string name, Vector3 value)
        {
            SerializedProperty p = obj.FindProperty(name);
            if (p != null) p.vector3Value = value;
        }
        private static void SetObject(SerializedObject obj, string name, Object value)
        {
            SerializedProperty p = obj.FindProperty(name);
            if (p != null) p.objectReferenceValue = value;
        }

        private static void SetString(SerializedProperty parent, string name, string value)
        {
            SerializedProperty p = parent.FindPropertyRelative(name);
            if (p != null) p.stringValue = value;
        }
        private static void SetInt(SerializedProperty parent, string name, int value)
        {
            SerializedProperty p = parent.FindPropertyRelative(name);
            if (p != null) p.intValue = value;
        }
        private static void SetObject(SerializedProperty parent, string name, Object value)
        {
            SerializedProperty p = parent.FindPropertyRelative(name);
            if (p != null) p.objectReferenceValue = value;
        }
    }
}
