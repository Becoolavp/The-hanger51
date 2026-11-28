using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hanger51.Commerce;
using Hanger51.Inventory;
using Hanger51.Player;
using Hanger51.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51CombatPolish178
    {
        const string PackLoot =
            "Assets/LowPolyWeapons/Prefabs/Loot";
        const string PackItemFolder =
            "Assets/_Project/Inventory/Combat/PackComplete";
        const string DefinitionFolder =
            "Assets/_Project/Weapons/Definitions/PackComplete";

        sealed class AmmoBoxSpec
        {
            public string prefabName;
            public H51Cartridge cartridge;
            public int rounds;
            public int price;
            public string caliber;
        }

        [MenuItem("Hanger 51/World/Current/178 - Fix Ammo Boxes Medical Roles Ragdoll And Pistol Cycling")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying ||
                EditorApplication.isCompiling)
            {
                Debug.LogError(
                    "Step 178 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            FirstPersonController player =
                UnityEngine.Object.FindFirstObjectByType<FirstPersonController>(
                    FindObjectsInactive.Include);

            H51CombatItemRegistry registry =
                player
                    ? player.GetComponent<H51CombatItemRegistry>()
                    : null;

            HangarShopTerminal terminal =
                UnityEngine.Object.FindFirstObjectByType<HangarShopTerminal>(
                    FindObjectsInactive.Include);

            if (!player || !registry || !terminal)
            {
                Debug.LogError(
                    "Step 178 failed. Run the combat/store setup first so the registry and procurement terminal exist.");
                return;
            }

            EnsureFolder(PackItemFolder);
            EnsureFolder(DefinitionFolder);

            List<H51CombatItemDefinition> definitions =
                registry.Definitions
                    .Where(d => d)
                    .Distinct()
                    .ToList();

            List<ShopCatalogEntry> catalog =
                terminal.Catalog
                    .Where(e =>
                        e != null &&
                        !e.ProductId.StartsWith(
                            "combat-pack-loot-amobox",
                            StringComparison.Ordinal) &&
                        !e.ProductId.StartsWith(
                            "combat-pack-loot-bigamobox",
                            StringComparison.Ordinal) &&
                        !e.ProductId.StartsWith(
                            "combat-pack-loot-healthpack",
                            StringComparison.Ordinal) &&
                        !e.ProductId.StartsWith(
                            "combat-functional-ammo-",
                            StringComparison.Ordinal) &&
                        !e.ProductId.StartsWith(
                            "combat-functional-medical-",
                            StringComparison.Ordinal))
                    .ToList();

            int ammoBoxes = 0;

            foreach (AmmoBoxSpec spec in AmmoSpecs())
            {
                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        PackLoot + "/" +
                        spec.prefabName +
                        ".prefab");

                if (!prefab)
                    continue;

                InventoryItemDefinition item =
                    FindItemForPrefab(prefab) ??
                    EnsureItemAsset(
                        "functional-" +
                        spec.prefabName);

                item.Configure(
                    "h51-ammo-box-" +
                    spec.prefabName.ToLowerInvariant(),
                    spec.caliber +
                    " Ammo Box — " +
                    spec.rounds +
                    " rounds",
                    "Functional " +
                    spec.caliber +
                    " ammunition supply. Each inventory unit is one reloadable round; this shipment contains " +
                    spec.rounds +
                    " rounds.",
                    Mathf.Max(500,spec.rounds),
                    new Color(.32f,.31f,.18f,1f),
                    false,
                    prefab,
                    Vector3.one);

                EditorUtility.SetDirty(item);

                H51CombatItemDefinition ammo =
                    definitions.FirstOrDefault(
                        d => d &&
                             d.Kind == H51CombatItemKind.Ammunition &&
                             d.InventoryItem == item);

                if (!ammo)
                {
                    ammo =
                        EnsureDefinition(
                            "functional-ammo-" +
                            spec.prefabName);

                    if (!definitions.Contains(ammo))
                        definitions.Add(ammo);
                }

                ammo.ConfigureAmmo(
                    item,
                    spec.cartridge);

                EditorUtility.SetDirty(ammo);

                ShopCatalogEntry product =
                    new ShopCatalogEntry();

                product.Configure(
                    "combat-functional-ammo-" +
                    spec.prefabName.ToLowerInvariant(),
                    "Weapons / Ammunition",
                    item.DisplayName,
                    item.Description,
                    spec.price,
                    ShopProductKind.InventoryItem,
                    item,
                    spec.rounds,
                    null);

                catalog.Add(product);
                ammoBoxes++;
            }

            int medicalKits = 0;

            for (int i=1;i<=2;i++)
            {
                string prefabName =
                    "Healthpack_0" + i;

                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        PackLoot + "/" +
                        prefabName +
                        ".prefab");

                if (!prefab)
                    continue;

                InventoryItemDefinition item =
                    FindItemForPrefab(prefab) ??
                    EnsureItemAsset(
                        "medical-" +
                        prefabName);

                string display =
                    i == 1
                        ? "Medical Kit — Compact"
                        : "Medical Kit — Field";

                item.Configure(
                    "h51-medical-kit-" + i,
                    display,
                    "General medical/support kit. Used for airway and breathing support plus shock stabilization. It is not a substitute for the Trauma Kit used for major hemorrhage, tourniquets, or penetrating chest trauma.",
                    5,
                    new Color(.86f,.92f,.88f,1f),
                    true,
                    prefab,
                    Vector3.one);

                EditorUtility.SetDirty(item);

                H51CombatItemDefinition medical =
                    definitions.FirstOrDefault(
                        d => d &&
                             d.Kind == H51CombatItemKind.MedicalKit &&
                             d.InventoryItem == item);

                if (!medical)
                {
                    medical =
                        EnsureDefinition(
                            "medical-" +
                            prefabName);

                    if (!definitions.Contains(medical))
                        definitions.Add(medical);
                }

                medical.ConfigureMedicalKit(
                    item,
                    H51MedicalKitRole.Medical);

                EditorUtility.SetDirty(medical);

                ShopCatalogEntry product =
                    new ShopCatalogEntry();

                product.Configure(
                    "combat-functional-medical-" +
                    prefabName.ToLowerInvariant(),
                    "Weapons / Medical",
                    item.DisplayName,
                    item.Description,
                    i == 1 ? 95 : 145,
                    ShopProductKind.InventoryItem,
                    item,
                    1,
                    null);

                catalog.Add(product);
                medicalKits++;
            }

            int traumaKits = 0;

            for (int i=0;i<definitions.Count;i++)
            {
                H51CombatItemDefinition definition =
                    definitions[i];

                if (!definition ||
                    definition.Kind != H51CombatItemKind.MedicalKit ||
                    definition.MedicalKitRole != H51MedicalKitRole.Trauma ||
                    !definition.InventoryItem)
                    continue;

                InventoryItemDefinition item =
                    definition.InventoryItem;

                if (item.WorldPrefab &&
                    (item.WorldPrefab.name.StartsWith(
                        "Healthpack_",
                        StringComparison.OrdinalIgnoreCase)))
                    continue;

                string display =
                    item.DisplayName.IndexOf(
                        "Trauma",
                        StringComparison.OrdinalIgnoreCase) >= 0
                        ? item.DisplayName
                        : "Trauma Kit";

                item.Configure(
                    item.ItemId,
                    display,
                    "Major-trauma field kit. Required for hemorrhage control, trauma dressings, tourniquets, and penetrating chest-wound stabilization. Pair it with a Medical Kit for airway/breathing and shock support.",
                    Mathf.Max(1,item.MaxStackSize),
                    item.PlaceholderColor,
                    item.CanEquip,
                    item.WorldPrefab,
                    item.WorldScale);

                EditorUtility.SetDirty(item);
                traumaKits++;
            }

            registry.Configure(
                definitions
                    .Where(d => d)
                    .Distinct()
                    .OrderBy(d => d.name)
                    .ToList());

            terminal.ReplaceCatalog(catalog);

            EditorUtility.SetDirty(registry);
            EditorUtility.SetDirty(terminal);

            EditorSceneManager.MarkSceneDirty(
                SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.SaveOpenScenes();
            Hanger51BuildTools.PrepareCurrentSceneForBuild(false);

            Debug.Log(
                "Step 178 complete. Functional reloadable ammo-box products=" +
                ammoBoxes +
                ", Medical-role kits=" +
                medicalKits +
                ", Trauma-role kits clarified=" +
                traumaKits +
                ". Runtime fixes also make spent casings ignore player collision, strengthen flinch/ragdoll behavior, and animate compatible semi-auto pistol slides. Run Step 179.");
        }

        [MenuItem("Hanger 51/World/Current/179 - Validate Ammo Boxes Medical Roles Ragdoll And Pistol Cycling")]
        public static void Validate()
        {
            FirstPersonController player =
                UnityEngine.Object.FindFirstObjectByType<FirstPersonController>(
                    FindObjectsInactive.Include);

            H51CombatItemRegistry registry =
                player
                    ? player.GetComponent<H51CombatItemRegistry>()
                    : null;

            HangarShopTerminal terminal =
                UnityEngine.Object.FindFirstObjectByType<HangarShopTerminal>(
                    FindObjectsInactive.Include);

            int functionalBoxes = 0;
            int trauma = 0;
            int medical = 0;
            int[] caliberCounts = new int[4];

            if (registry)
            {
                foreach (H51CombatItemDefinition d in registry.Definitions)
                {
                    if (!d)
                        continue;

                    if (d.Kind == H51CombatItemKind.Ammunition &&
                        d.InventoryItem &&
                        d.InventoryItem.WorldPrefab &&
                        (d.InventoryItem.WorldPrefab.name.StartsWith("AmoBox_",StringComparison.OrdinalIgnoreCase) ||
                         d.InventoryItem.WorldPrefab.name.StartsWith("BigAmoBox_",StringComparison.OrdinalIgnoreCase)))
                    {
                        functionalBoxes++;

                        switch (d.Cartridge)
                        {
                            case H51Cartridge.NineByNineteen:
                                caliberCounts[0]++;
                                break;
                            case H51Cartridge.FiveFiveSixByFortyFive:
                                caliberCounts[1]++;
                                break;
                            case H51Cartridge.SevenSixTwoByFiftyOne:
                                caliberCounts[2]++;
                                break;
                            case H51Cartridge.TwelveGauge:
                                caliberCounts[3]++;
                                break;
                        }
                    }

                    if (d.Kind == H51CombatItemKind.MedicalKit)
                    {
                        if (d.MedicalKitRole == H51MedicalKitRole.Trauma)
                            trauma++;
                        else
                            medical++;
                    }
                }
            }

            int functionalProducts =
                terminal == null
                    ? 0
                    : terminal.Catalog.Count(
                        e => e != null &&
                             e.ProductId.StartsWith(
                                 "combat-functional-ammo-",
                                 StringComparison.Ordinal));

            bool ok =
                player &&
                registry &&
                terminal &&
                functionalBoxes >= 13 &&
                functionalProducts >= 13 &&
                caliberCounts.All(count => count > 0) &&
                trauma > 0 &&
                medical >= 2;

            if (ok)
            {
                Debug.Log(
                    "Step 179 passed. Reloadable ammo boxes=" +
                    functionalBoxes +
                    ", functional ammo store products=" +
                    functionalProducts +
                    ", caliber coverage=9mm:" + caliberCounts[0] +
                    " 5.56:" + caliberCounts[1] +
                    " 7.62:" + caliberCounts[2] +
                    " 12ga:" + caliberCounts[3] +
                    ", Trauma kits=" + trauma +
                    ", Medical kits=" + medical +
                    ". Build and test reloads, casing/player collision, fatal ragdoll, stronger flinch, medical roles, and pistol slide cycling.");
            }
            else
            {
                Debug.LogError(
                    "Step 179 failed. player=" + (player ? "yes" : "MISSING") +
                    ", registry=" + (registry ? "yes" : "MISSING") +
                    ", terminal=" + (terminal ? "yes" : "MISSING") +
                    ", reloadable ammo boxes=" + functionalBoxes +
                    ", ammo products=" + functionalProducts +
                    ", caliber coverage=" +
                    string.Join(",",caliberCounts) +
                    ", Trauma kits=" + trauma +
                    ", Medical kits=" + medical +
                    ".");
            }
        }

        static IEnumerable<AmmoBoxSpec> AmmoSpecs()
        {
            yield return Box("AmoBox_01",H51Cartridge.NineByNineteen,100,48,"9x19 mm");
            yield return Box("AmoBox_02",H51Cartridge.FiveFiveSixByFortyFive,120,84,"5.56x45 mm");
            yield return Box("AmoBox_03",H51Cartridge.SevenSixTwoByFiftyOne,80,96,"7.62x51 mm");
            yield return Box("AmoBox_04",H51Cartridge.TwelveGauge,40,72,"12 Gauge");
            yield return Box("AmoBox_05",H51Cartridge.NineByNineteen,150,68,"9x19 mm");
            yield return Box("AmoBox_06",H51Cartridge.FiveFiveSixByFortyFive,180,118,"5.56x45 mm");
            yield return Box("AmoBox_07",H51Cartridge.SevenSixTwoByFiftyOne,120,138,"7.62x51 mm");
            yield return Box("AmoBox_08",H51Cartridge.TwelveGauge,60,98,"12 Gauge");
            yield return Box("AmoBox_09",H51Cartridge.FiveFiveSixByFortyFive,240,154,"5.56x45 mm");
            yield return Box("AmoBox_10",H51Cartridge.SevenSixTwoByFiftyOne,160,178,"7.62x51 mm");
            yield return Box("BigAmoBox_01",H51Cartridge.NineByNineteen,300,125,"9x19 mm");
            yield return Box("BigAmoBox_02",H51Cartridge.FiveFiveSixByFortyFive,360,215,"5.56x45 mm");
            yield return Box("BigAmoBox_03",H51Cartridge.SevenSixTwoByFiftyOne,240,255,"7.62x51 mm");
        }

        static AmmoBoxSpec Box(
            string prefab,
            H51Cartridge cartridge,
            int rounds,
            int price,
            string caliber)
        {
            return new AmmoBoxSpec
            {
                prefabName = prefab,
                cartridge = cartridge,
                rounds = rounds,
                price = price,
                caliber = caliber
            };
        }

        static InventoryItemDefinition FindItemForPrefab(
            GameObject prefab)
        {
            if (!prefab)
                return null;

            string[] guids =
                AssetDatabase.FindAssets(
                    "t:InventoryItemDefinition",
                    new[]
                    {
                        "Assets/_Project/Inventory/Combat"
                    });

            for (int i=0;i<guids.Length;i++)
            {
                InventoryItemDefinition item =
                    AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(
                        AssetDatabase.GUIDToAssetPath(
                            guids[i]));

                if (item &&
                    item.WorldPrefab == prefab)
                    return item;
            }

            return null;
        }

        static InventoryItemDefinition EnsureItemAsset(
            string key)
        {
            string path =
                PackItemFolder +
                "/" +
                key.ToLowerInvariant() +
                ".asset";

            InventoryItemDefinition item =
                AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(
                    path);

            if (!item)
            {
                item =
                    ScriptableObject.CreateInstance<InventoryItemDefinition>();
                item.name = key;
                AssetDatabase.CreateAsset(
                    item,
                    path);
            }

            return item;
        }

        static H51CombatItemDefinition EnsureDefinition(
            string key)
        {
            string path =
                DefinitionFolder +
                "/" +
                key.ToLowerInvariant() +
                ".asset";

            H51CombatItemDefinition definition =
                AssetDatabase.LoadAssetAtPath<H51CombatItemDefinition>(
                    path);

            if (!definition)
            {
                definition =
                    ScriptableObject.CreateInstance<H51CombatItemDefinition>();
                definition.name = key;
                AssetDatabase.CreateAsset(
                    definition,
                    path);
            }

            return definition;
        }

        static void EnsureFolder(
            string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) ||
                folder == "Assets" ||
                AssetDatabase.IsValidFolder(folder))
                return;

            string parent =
                Path.GetDirectoryName(folder)?
                    .Replace("\\","/");

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(
                parent,
                Path.GetFileName(folder));
        }
    }
}
