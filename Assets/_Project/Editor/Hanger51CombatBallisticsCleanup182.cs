using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hanger51.Commerce;
using Hanger51.Health;
using Hanger51.Inventory;
using Hanger51.NPCs;
using Hanger51.Player;
using Hanger51.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51CombatBallisticsCleanup182
    {
        const string PackLoot =
            "Assets/LowPolyWeapons/Prefabs/Loot";

        const string ItemFolder =
            "Assets/_Project/Inventory/Combat/AmmoCasesV2";

        const string DefinitionFolder =
            "Assets/_Project/Weapons/Definitions/AmmoCasesV2";

        sealed class AmmoCaseSpec
        {
            public string key;
            public string display;
            public string prefabName;
            public H51Cartridge cartridge;
            public int rounds;
            public int price;
        }

        [MenuItem("Hanger 51/World/Current/182 - Clean Ammo Catalog Refresh Anatomy Ragdoll ADS And Casings")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying ||
                EditorApplication.isCompiling)
            {
                Debug.LogError(
                    "Step 182 failed. Exit Play mode and let Unity finish compiling first.");
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

            if (!player ||
                !registry ||
                !terminal)
            {
                Debug.LogError(
                    "Step 182 failed. The player combat registry or procurement terminal is missing.");
                return;
            }

            EnsureFolder(ItemFolder);
            EnsureFolder(DefinitionFolder);

            List<H51CombatItemDefinition> definitions =
                registry.Definitions
                    .Where(d => d)
                    .Distinct()
                    .ToList();

            // Remove only the prior V2 definitions before recreating them.
            definitions.RemoveAll(
                d => d &&
                     d.InventoryItem &&
                     d.InventoryItem.ItemId.StartsWith(
                         "h51-ammo-case-v2-",
                         StringComparison.Ordinal));

            // Rebuild the ammunition shelf from scratch. This intentionally
            // removes loose/single-bullet listings and the earlier ambiguous
            // reused ammo-box products while leaving every other department intact.
            List<ShopCatalogEntry> catalog =
                terminal.Catalog
                    .Where(e =>
                        e != null &&
                        !string.Equals(
                            e.Category,
                            "Weapons / Ammunition",
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();

            int addedProducts = 0;

            foreach (AmmoCaseSpec spec in Specs())
            {
                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        PackLoot +
                        "/" +
                        spec.prefabName +
                        ".prefab");

                if (!prefab)
                {
                    Debug.LogWarning(
                        "Step 182 could not find ammo-box prefab " +
                        spec.prefabName +
                        ".");
                    continue;
                }

                InventoryItemDefinition item =
                    EnsureUniqueItem(
                        spec);

                H51CombatItemDefinition ammo =
                    EnsureDefinition(
                        spec.key);

                ammo.ConfigureAmmo(
                    item,
                    spec.cartridge);

                EditorUtility.SetDirty(ammo);

                if (!definitions.Contains(ammo))
                    definitions.Add(ammo);

                ShopCatalogEntry product =
                    new ShopCatalogEntry();

                product.Configure(
                    "combat-ammo-case-v2-" +
                    spec.key,
                    "Weapons / Ammunition",
                    spec.display,
                    "Boxed " +
                    FriendlyCartridge(spec.cartridge) +
                    " ammunition. Purchase includes " +
                    spec.rounds +
                    " reloadable rounds; no loose single-round product is sold.",
                    spec.price,
                    ShopProductKind.InventoryItem,
                    item,
                    spec.rounds,
                    null);

                catalog.Add(product);
                addedProducts++;
            }

            registry.Configure(
                definitions
                    .Where(d => d)
                    .Distinct()
                    .OrderBy(d => d.name)
                    .ToList());

            terminal.ReplaceCatalog(catalog);

            int anatomyRefreshed = 0;
            int ragdollsPresent = 0;

            H51AnatomicalHealth[] bodies =
                UnityEngine.Object.FindObjectsByType<H51AnatomicalHealth>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            for (int i=0;i<bodies.Length;i++)
            {
                H51AnatomicalHealth body =
                    bodies[i];

                if (!body ||
                    !body.gameObject.scene.IsValid())
                    continue;

                H51AnatomicalHitboxRig rig =
                    body.GetComponent<H51AnatomicalHitboxRig>();

                if (!rig)
                    rig =
                        Undo.AddComponent<H51AnatomicalHitboxRig>(
                            body.gameObject);

                rig.EnsureBuilt();
                anatomyRefreshed++;

                if (body.GetComponent<H51CitizenIdentity>())
                {
                    H51CitizenRagdoll ragdoll =
                        body.GetComponent<H51CitizenRagdoll>();

                    if (!ragdoll)
                        ragdoll =
                            Undo.AddComponent<H51CitizenRagdoll>(
                                body.gameObject);

                    if (ragdoll)
                        ragdollsPresent++;
                }

                EditorUtility.SetDirty(
                    body.gameObject);
            }

            EditorUtility.SetDirty(registry);
            EditorUtility.SetDirty(terminal);

            EditorSceneManager.MarkSceneDirty(
                SceneManager.GetActiveScene());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.SaveOpenScenes();
            Hanger51BuildTools.PrepareCurrentSceneForBuild(false);

            Debug.Log(
                "Step 182 complete. Clean boxed-ammunition products=" +
                addedProducts +
                ", anatomical bodies refreshed=" +
                anatomyRefreshed +
                ", citizen ragdolls present=" +
                ragdollsPresent +
                ". Runtime code also locks firearm presentation to camera pitch/yaw, traces wound channels through internal organs, rebinds ragdolls to BoZo's live Animator, and ejects dedicated spent-case geometry. Run Step 183.");
        }

        [MenuItem("Hanger 51/World/Current/183 - Validate Ammo Catalog Anatomy Ragdoll ADS And Casings")]
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

            int ammoProducts = 0;
            int looseAmmoProducts = 0;
            int wrongProductLinks = 0;

            if (terminal)
            {
                foreach (ShopCatalogEntry product in terminal.Catalog)
                {
                    if (product == null ||
                        !string.Equals(
                            product.Category,
                            "Weapons / Ammunition",
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    ammoProducts++;

                    if (!product.ProductId.StartsWith(
                            "combat-ammo-case-v2-",
                            StringComparison.Ordinal))
                    {
                        looseAmmoProducts++;
                    }

                    if (!product.InventoryItem ||
                        !product.InventoryItem.ItemId.StartsWith(
                            "h51-ammo-case-v2-",
                            StringComparison.Ordinal))
                    {
                        wrongProductLinks++;
                    }
                }
            }

            int ammoDefinitions = 0;
            int[] caliberCoverage = new int[4];

            if (registry)
            {
                foreach (H51CombatItemDefinition d in registry.Definitions)
                {
                    if (!d ||
                        d.Kind != H51CombatItemKind.Ammunition ||
                        !d.InventoryItem ||
                        !d.InventoryItem.ItemId.StartsWith(
                            "h51-ammo-case-v2-",
                            StringComparison.Ordinal))
                        continue;

                    ammoDefinitions++;

                    switch (d.Cartridge)
                    {
                        case H51Cartridge.NineByNineteen:
                            caliberCoverage[0]++;
                            break;
                        case H51Cartridge.FiveFiveSixByFortyFive:
                            caliberCoverage[1]++;
                            break;
                        case H51Cartridge.SevenSixTwoByFiftyOne:
                            caliberCoverage[2]++;
                            break;
                        case H51Cartridge.TwelveGauge:
                            caliberCoverage[3]++;
                            break;
                    }
                }
            }

            int citizens = 0;
            int missingRig = 0;
            int missingOrgans = 0;
            int missingRagdoll = 0;

            H51CitizenIdentity[] citizenList =
                UnityEngine.Object.FindObjectsByType<H51CitizenIdentity>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            for (int i=0;i<citizenList.Length;i++)
            {
                H51CitizenIdentity citizen =
                    citizenList[i];

                if (!citizen ||
                    !citizen.gameObject.scene.IsValid())
                    continue;

                citizens++;

                H51AnatomicalHitboxRig rig =
                    citizen.GetComponent<H51AnatomicalHitboxRig>();

                if (!rig)
                {
                    missingRig++;
                    continue;
                }

                rig.EnsureBuilt();

                H51OrganHitbox[] organs =
                    citizen.GetComponentsInChildren<H51OrganHitbox>(true);

                bool[] present =
                    new bool[
                        Enum.GetValues(
                            typeof(H51OrganType)).Length];

                for (int o=0;o<organs.Length;o++)
                {
                    if (!organs[o])
                        continue;

                    int index =
                        (int)organs[o].Organ;

                    if (index >= 0 &&
                        index < present.Length)
                        present[index] = true;
                }

                if (present.Any(v => !v))
                    missingOrgans++;

                if (!citizen.GetComponent<H51CitizenRagdoll>())
                    missingRagdoll++;
            }

            bool ok =
                player &&
                registry &&
                terminal &&
                ammoProducts == 8 &&
                looseAmmoProducts == 0 &&
                wrongProductLinks == 0 &&
                ammoDefinitions == 8 &&
                caliberCoverage.All(v => v >= 2) &&
                citizens > 0 &&
                missingRig == 0 &&
                missingOrgans == 0 &&
                missingRagdoll == 0;

            if (ok)
            {
                Debug.Log(
                    "Step 183 passed. Ammunition shelf contains exactly 8 boxed products with unique inventory assets (2 each for 9 mm, 5.56, 7.62 and 12 gauge); no loose-round store products remain. All " +
                    citizens +
                    " citizens have complete internal organ rigs and ragdoll components.");
            }
            else
            {
                Debug.LogError(
                    "Step 183 failed. ammo products=" +
                    ammoProducts +
                    "/8, loose products=" +
                    looseAmmoProducts +
                    ", wrong product links=" +
                    wrongProductLinks +
                    ", V2 ammo definitions=" +
                    ammoDefinitions +
                    "/8, caliber coverage=" +
                    string.Join(",",caliberCoverage) +
                    ", citizens=" +
                    citizens +
                    ", missing anatomical rig=" +
                    missingRig +
                    ", missing complete organs=" +
                    missingOrgans +
                    ", missing ragdoll=" +
                    missingRagdoll +
                    ".");
            }
        }

        static InventoryItemDefinition EnsureUniqueItem(
            AmmoCaseSpec spec)
        {
            string path =
                ItemFolder +
                "/" +
                spec.key +
                ".asset";

            InventoryItemDefinition item =
                AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(
                    path);

            if (!item)
            {
                item =
                    ScriptableObject.CreateInstance<InventoryItemDefinition>();

                item.name =
                    "AmmoCaseV2-" +
                    spec.key;

                AssetDatabase.CreateAsset(
                    item,
                    path);
            }

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    PackLoot +
                    "/" +
                    spec.prefabName +
                    ".prefab");

            item.Configure(
                "h51-ammo-case-v2-" +
                spec.key,
                spec.display,
                "Boxed " +
                FriendlyCartridge(spec.cartridge) +
                " ammunition — " +
                spec.rounds +
                " rounds.",
                Mathf.Max(500,spec.rounds),
                new Color(.34f,.31f,.16f,1f),
                false,
                prefab,
                Vector3.one);

            EditorUtility.SetDirty(item);
            return item;
        }

        static H51CombatItemDefinition EnsureDefinition(
            string key)
        {
            string path =
                DefinitionFolder +
                "/" +
                key +
                ".asset";

            H51CombatItemDefinition d =
                AssetDatabase.LoadAssetAtPath<H51CombatItemDefinition>(
                    path);

            if (!d)
            {
                d =
                    ScriptableObject.CreateInstance<H51CombatItemDefinition>();

                d.name =
                    "AmmoCaseV2-" +
                    key;

                AssetDatabase.CreateAsset(
                    d,
                    path);
            }

            return d;
        }

        static IEnumerable<AmmoCaseSpec> Specs()
        {
            yield return Spec(
                "9mm-standard",
                "9x19 mm Ammo Box — 100 rounds",
                "AmoBox_01",
                H51Cartridge.NineByNineteen,
                100,
                48);

            yield return Spec(
                "9mm-bulk",
                "9x19 mm Bulk Ammo Case — 300 rounds",
                "BigAmoBox_01",
                H51Cartridge.NineByNineteen,
                300,
                125);

            yield return Spec(
                "556-standard",
                "5.56x45 mm Ammo Box — 120 rounds",
                "AmoBox_02",
                H51Cartridge.FiveFiveSixByFortyFive,
                120,
                84);

            yield return Spec(
                "556-bulk",
                "5.56x45 mm Bulk Ammo Case — 360 rounds",
                "BigAmoBox_02",
                H51Cartridge.FiveFiveSixByFortyFive,
                360,
                215);

            yield return Spec(
                "762-standard",
                "7.62x51 mm Ammo Box — 80 rounds",
                "AmoBox_03",
                H51Cartridge.SevenSixTwoByFiftyOne,
                80,
                96);

            yield return Spec(
                "762-bulk",
                "7.62x51 mm Bulk Ammo Case — 240 rounds",
                "BigAmoBox_03",
                H51Cartridge.SevenSixTwoByFiftyOne,
                240,
                255);

            yield return Spec(
                "12ga-standard",
                "12 Gauge Shell Box — 40 shells",
                "AmoBox_04",
                H51Cartridge.TwelveGauge,
                40,
                72);

            yield return Spec(
                "12ga-bulk",
                "12 Gauge Bulk Shell Case — 60 shells",
                "AmoBox_08",
                H51Cartridge.TwelveGauge,
                60,
                98);
        }

        static AmmoCaseSpec Spec(
            string key,
            string display,
            string prefabName,
            H51Cartridge cartridge,
            int rounds,
            int price)
        {
            return new AmmoCaseSpec
            {
                key = key,
                display = display,
                prefabName = prefabName,
                cartridge = cartridge,
                rounds = rounds,
                price = price
            };
        }

        static string FriendlyCartridge(
            H51Cartridge cartridge)
        {
            switch (cartridge)
            {
                case H51Cartridge.NineByNineteen:
                    return "9x19 mm";
                case H51Cartridge.FiveFiveSixByFortyFive:
                    return "5.56x45 mm";
                case H51Cartridge.SevenSixTwoByFiftyOne:
                    return "7.62x51 mm";
                case H51Cartridge.TwelveGauge:
                    return "12 gauge";
                default:
                    return "compatible";
            }
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
