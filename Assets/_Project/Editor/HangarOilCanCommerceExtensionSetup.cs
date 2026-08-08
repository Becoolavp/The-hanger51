using Hanger51.Commerce;
using Hanger51.EngineAssembly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class HangarOilCanCommerceExtensionSetup
    {
        private const string CommerceRootName = "Hanger 51 Commerce System";
        private const string TemplateRootName = "Commerce Templates";
        private const string OilCanTemplateName = "Aircraft Oil Can Shipment Template";
        private const string ProductId = "aircraft-oil-can-20l";

        [MenuItem("Hanger 51/Shop and Shipping/7 - Add Purchasable Aircraft Oil Cans")]
        public static void AddPurchasableAircraftOilCans()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Shop Step 7 failed. Exit Play mode first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            GameObject commerceRoot = GameObject.Find(CommerceRootName);
            HangarShopTerminal terminal = Object.FindFirstObjectByType<HangarShopTerminal>(
                FindObjectsInactive.Include);
            if (!scene.IsValid()
                || string.IsNullOrWhiteSpace(scene.path)
                || commerceRoot == null
                || terminal == null)
            {
                Debug.LogError("Shop Step 7 failed. Open the saved movement-test scene and confirm the existing Hanger 51 shop is present.");
                return;
            }

            Transform templateRoot = FindDescendant(commerceRoot.transform, TemplateRootName);
            if (templateRoot == null)
            {
                Debug.LogError("Shop Step 7 failed. The existing Commerce Templates root is missing. Do not rebuild the shop; restore the current commerce setup first.");
                return;
            }

            Transform previousTemplate = templateRoot.Find(OilCanTemplateName);
            if (previousTemplate != null)
            {
                Undo.DestroyObjectImmediate(previousTemplate.gameObject);
            }

            EngineOilCanController source = FindSourceOilCan(templateRoot);
            if (source == null)
            {
                Debug.LogError("Shop Step 7 failed. No existing working aircraft oil can was found in the scene. Run the current Merlin condition setup first.");
                return;
            }

            GameObject template = Object.Instantiate(source.gameObject);
            Undo.RegisterCreatedObjectUndo(template, "Create oil can shipment template");
            template.name = OilCanTemplateName;
            template.transform.SetParent(templateRoot, false);
            template.transform.localPosition = Vector3.zero;
            template.transform.localRotation = Quaternion.identity;

            ShipmentDeliveryOccupancy[] occupancies =
                template.GetComponentsInChildren<ShipmentDeliveryOccupancy>(true);
            for (int index = 0; index < occupancies.Length; index++)
            {
                if (occupancies[index] != null)
                {
                    Undo.DestroyObjectImmediate(occupancies[index]);
                }
            }

            EngineOilCanController templateCan =
                template.GetComponentInChildren<EngineOilCanController>(true);
            if (templateCan == null)
            {
                Undo.DestroyObjectImmediate(template);
                Debug.LogError("Shop Step 7 failed. The copied service template lost its oil-can controller.");
                return;
            }
            templateCan.ResetToFullServiceState();
            template.SetActive(false);

            SerializedObject serializedTerminal = new SerializedObject(terminal);
            SerializedProperty catalog = serializedTerminal.FindProperty("catalog");
            if (catalog == null)
            {
                Undo.DestroyObjectImmediate(template);
                Debug.LogError("Shop Step 7 failed. The shop catalog field could not be found.", terminal);
                return;
            }

            SerializedProperty entry = FindOrAppendProduct(catalog, ProductId);
            SetString(entry, "productId", ProductId);
            SetString(entry, "category", "Fluids");
            SetString(entry, "displayName", "20 L Aircraft Engine Oil Can");
            SetString(
                entry,
                "description",
                "A full reusable 20 L aircraft engine oil can. Unbox it, pick it up with E, open the cap with F, and hold E at a Merlin oil filler to pour.");
            SetInt(entry, "price", 125);
            SetEnum(entry, "productKind", (int)ShopProductKind.ServiceObject);
            SetObject(entry, "inventoryItem", null);
            SetInt(entry, "quantity", 1);
            SetObject(entry, "assemblyTemplate", template);
            serializedTerminal.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(templateCan);
            EditorUtility.SetDirty(template);
            EditorUtility.SetDirty(terminal);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("Shop Step 7 changed the oil-can catalog entry but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError("Shop Step 7 completed, but build preparation failed.");
                return;
            }

            Selection.activeGameObject = terminal.gameObject;
            Debug.Log("Shop Step 7 complete. Added a $125 full 20 L aircraft engine oil can as a reusable service-equipment shipment without rebuilding or replacing the existing shop catalog.", terminal);
        }

        [MenuItem("Hanger 51/Shop and Shipping/8 - Validate Purchasable Aircraft Oil Cans")]
        public static void ValidatePurchasableAircraftOilCans()
        {
            bool passed = true;
            GameObject commerceRoot = GameObject.Find(CommerceRootName);
            HangarShopTerminal terminal = Object.FindFirstObjectByType<HangarShopTerminal>(
                FindObjectsInactive.Include);
            Transform templateRoot = commerceRoot != null
                ? FindDescendant(commerceRoot.transform, TemplateRootName)
                : null;
            Transform template = templateRoot != null
                ? templateRoot.Find(OilCanTemplateName)
                : null;
            EngineOilCanController templateCan = template != null
                ? template.GetComponentInChildren<EngineOilCanController>(true)
                : null;

            if (terminal == null)
            {
                Debug.LogError("Shop Step 8 failed: shop terminal is missing.");
                passed = false;
            }
            else
            {
                bool found = false;
                for (int index = 0; index < terminal.Catalog.Count; index++)
                {
                    ShopCatalogEntry product = terminal.Catalog[index];
                    if (product == null || product.ProductId != ProductId)
                    {
                        continue;
                    }

                    found = product.IsConfigured
                        && product.ProductKind == ShopProductKind.ServiceObject
                        && product.AssemblyTemplate == (template != null ? template.gameObject : null)
                        && product.Price == 125;
                    break;
                }

                if (!found)
                {
                    Debug.LogError("Shop Step 8 failed: the 20 L aircraft oil-can catalog product is missing or incorrectly configured.", terminal);
                    passed = false;
                }
            }

            if (template == null
                || template.gameObject.activeSelf
                || templateCan == null
                || templateCan.CapacityLiters < 19.9f)
            {
                Debug.LogError("Shop Step 8 failed: the inactive full-size aircraft oil-can shipment template is missing or invalid.");
                passed = false;
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("Shop Step 8 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log("Shop Step 8 passed. The existing shop contains a purchasable 20 L aircraft oil can delivered as reusable service equipment, and the standalone build setup is ready.");
            }
        }

        private static EngineOilCanController FindSourceOilCan(Transform templateRoot)
        {
            EngineOilCanController[] cans = Object.FindObjectsByType<EngineOilCanController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < cans.Length; index++)
            {
                EngineOilCanController can = cans[index];
                if (can == null || can.transform.IsChildOf(templateRoot))
                {
                    continue;
                }
                return can;
            }
            return null;
        }

        private static SerializedProperty FindOrAppendProduct(
            SerializedProperty catalog,
            string productId)
        {
            for (int index = 0; index < catalog.arraySize; index++)
            {
                SerializedProperty item = catalog.GetArrayElementAtIndex(index);
                SerializedProperty id = item.FindPropertyRelative("productId");
                if (id != null && id.stringValue == productId)
                {
                    return item;
                }
            }

            catalog.InsertArrayElementAtIndex(catalog.arraySize);
            return catalog.GetArrayElementAtIndex(catalog.arraySize - 1);
        }

        private static void SetString(SerializedProperty parent, string name, string value)
        {
            SerializedProperty property = parent.FindPropertyRelative(name);
            if (property != null) property.stringValue = value;
        }

        private static void SetInt(SerializedProperty parent, string name, int value)
        {
            SerializedProperty property = parent.FindPropertyRelative(name);
            if (property != null) property.intValue = value;
        }

        private static void SetEnum(SerializedProperty parent, string name, int value)
        {
            SerializedProperty property = parent.FindPropertyRelative(name);
            if (property != null) property.enumValueIndex = value;
        }

        private static void SetObject(SerializedProperty parent, string name, Object value)
        {
            SerializedProperty property = parent.FindPropertyRelative(name);
            if (property != null) property.objectReferenceValue = value;
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null) return null;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < all.Length; index++)
            {
                if (all[index] != null && all[index].name == objectName)
                {
                    return all[index];
                }
            }
            return null;
        }
    }
}
