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
    public static class Hanger51LowPolyWeaponsVisualStoreUpgrade
    {
        const string InventoryFolder = "Assets/_Project/Inventory/Combat";
        const string DefinitionFolder = "Assets/_Project/Weapons/Definitions";
        const string MainMaterialPath = "Assets/LowPolyWeapons/Materials/LowPolyWeapons_MAIN.mat";
        const string BackgroundMaterialPath = "Assets/LowPolyWeapons/Materials/LowPolyWeapons_BG.mat";
        const string MainTexturePath = "Assets/LowPolyWeapons/Textures/Texture_01.png";

        struct VariantSpec
        {
            public string assetName;
            public string productId;
            public string displayName;
            public string category;
            public string prefabPath;
            public int price;
            public H51Cartridge cartridge;
            public float mass;
            public float velocity;
            public float diameter;
            public float drag;
            public float damageScale;
            public int capacity;
            public float rpm;
            public bool automatic;
            public float adsFov;
            public Vector3 viewPosition;
            public float viewScale;
        }

        [MenuItem("Hanger 51/World/Current/172 - Repair Weapon Visuals And Upgrade Store")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("Step 172 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            HangarShopTerminal terminal =
                UnityEngine.Object.FindFirstObjectByType<HangarShopTerminal>(
                    FindObjectsInactive.Include);

            FirstPersonController player =
                UnityEngine.Object.FindFirstObjectByType<FirstPersonController>(
                    FindObjectsInactive.Include);

            PlayerInventory inventory =
                player ? player.GetComponent<PlayerInventory>() : null;

            H51CombatItemRegistry registry =
                player ? player.GetComponent<H51CombatItemRegistry>() : null;

            if (!terminal || !player || !inventory || !registry)
            {
                Debug.LogError(
                    "Step 172 failed. Run Steps 170 and 171 first so the shop, inventory, and combat registry exist.");
                return;
            }

            EnsureFolder(InventoryFolder);
            EnsureFolder(DefinitionFolder);

            int repairedMaterials = RepairLowPolyWeaponMaterials();

            List<VariantSpec> specs = BuildVariantSpecs();
            List<string> missingPrefabs =
                specs
                    .Where(spec => !AssetDatabase.LoadAssetAtPath<GameObject>(spec.prefabPath))
                    .Select(spec => spec.prefabPath)
                    .ToList();

            if (missingPrefabs.Count > 0)
            {
                Debug.LogError(
                    "Step 172 failed. Missing LowPolyWeapons variant prefabs: " +
                    string.Join(", ",missingPrefabs));
                return;
            }

            // The first pass used -90 Y. In the actual pack that makes the
            // first-person model point toward the player. +90 is exactly a
            // 180-degree correction while leaving world/drop orientation alone.
            int repairedViewDefinitions = 0;
            string[] existingFirearmAssets =
            {
                "ServiceRifle556",
                "ServicePistol9mm",
                "PrecisionRifle762"
            };

            for (int i=0;i<existingFirearmAssets.Length;i++)
            {
                H51CombatItemDefinition definition =
                    AssetDatabase.LoadAssetAtPath<H51CombatItemDefinition>(
                        DefinitionFolder + "/" +
                        existingFirearmAssets[i] +
                        ".asset");

                if (!definition)
                    continue;

                definition.SetViewPresentation(
                    definition.ViewLocalPosition,
                    new Vector3(0f,90f,0f),
                    definition.ViewScaleMultiplier);

                EditorUtility.SetDirty(definition);
                repairedViewDefinitions++;
            }

            int createdVariants = 0;
            foreach (VariantSpec spec in specs)
            {
                InventoryItemDefinition item =
                    CreateOrRefreshVariantItem(spec);

                H51CombatItemDefinition definition =
                    CreateOrRefreshVariantDefinition(spec,item);

                if (item && definition)
                    createdVariants++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            List<H51CombatItemDefinition> allDefinitions =
                LoadAllCombatDefinitions();

            registry.Configure(allDefinitions);
            EditorUtility.SetDirty(registry);

            List<ShopCatalogEntry> catalog =
                terminal.Catalog
                    .Where(entry =>
                        entry != null &&
                        !entry.ProductId.StartsWith(
                            "combat-variant-",
                            StringComparison.Ordinal))
                    .ToList();

            foreach (VariantSpec spec in specs)
            {
                InventoryItemDefinition item =
                    AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(
                        InventoryFolder + "/" + spec.assetName + ".asset");

                if (!item)
                    continue;

                ShopCatalogEntry entry = new ShopCatalogEntry();
                entry.Configure(
                    spec.productId,
                    spec.category,
                    spec.displayName,
                    "A distinct LowPolyWeapons firearm body using the same Hanger 51 ballistic, inventory, attachment, and anatomical-damage pipeline. Inspect the live 3D preview before purchase.",
                    spec.price,
                    ShopProductKind.InventoryItem,
                    item,
                    1,
                    null);

                catalog.Add(entry);
            }

            terminal.ReplaceCatalog(catalog);
            EditorUtility.SetDirty(terminal);

            inventory.EnsureMinimumSlotCount(24);
            EditorUtility.SetDirty(inventory);

            Scene activeScene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.SaveOpenScenes();

            Hanger51BuildTools.PrepareCurrentSceneForBuild(false);

            Debug.Log(
                "Step 172 complete. Repaired " +
                repairedMaterials +
                " LowPolyWeapons shared materials for URP, flipped " +
                repairedViewDefinitions +
                " existing firearm first-person presentations by 180 degrees, generated/refreshed " +
                createdVariants +
                " additional firearm variants, expanded the combat registry, and rebuilt the weapons catalog. " +
                "The runtime shop UI now separates Aircraft Parts and Weapons and renders a live 3D product preview. Run Step 173.");
        }

        [MenuItem("Hanger 51/World/Current/173 - Validate Weapon Visuals And Store Upgrade")]
        public static void Validate()
        {
            bool passed = true;

            Material mainMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(MainMaterialPath);
            Texture2D mainTexture =
                AssetDatabase.LoadAssetAtPath<Texture2D>(MainTexturePath);

            if (!mainMaterial ||
                !mainMaterial.shader ||
                mainMaterial.shader.name != "Universal Render Pipeline/Lit")
            {
                Debug.LogError(
                    "Step 173 failed: LowPolyWeapons_MAIN is not using Universal Render Pipeline/Lit.");
                passed = false;
            }

            if (!mainTexture)
            {
                Debug.LogError(
                    "Step 173 failed: Texture_01.png is missing.");
                passed = false;
            }
            else if (mainMaterial)
            {
                Texture materialTexture =
                    mainMaterial.HasProperty("_BaseMap")
                        ? mainMaterial.GetTexture("_BaseMap")
                        : mainMaterial.mainTexture;

                if (materialTexture != mainTexture)
                {
                    Debug.LogError(
                        "Step 173 failed: LowPolyWeapons_MAIN is not using the pack's Texture_01 color atlas.");
                    passed = false;
                }
            }

            FirstPersonController player =
                UnityEngine.Object.FindFirstObjectByType<FirstPersonController>(
                    FindObjectsInactive.Include);

            H51CombatItemRegistry registry =
                player ? player.GetComponent<H51CombatItemRegistry>() : null;

            HangarShopTerminal terminal =
                UnityEngine.Object.FindFirstObjectByType<HangarShopTerminal>(
                    FindObjectsInactive.Include);

            HangarShopUI shopUI =
                UnityEngine.Object.FindFirstObjectByType<HangarShopUI>(
                    FindObjectsInactive.Include);

            string[] firearmNames =
            {
                "ServiceRifle556",
                "ServicePistol9mm",
                "PrecisionRifle762"
            };

            for (int i=0;i<firearmNames.Length;i++)
            {
                H51CombatItemDefinition definition =
                    AssetDatabase.LoadAssetAtPath<H51CombatItemDefinition>(
                        DefinitionFolder + "/" +
                        firearmNames[i] +
                        ".asset");

                if (!definition ||
                    Mathf.Abs(
                        Mathf.DeltaAngle(
                            definition.ViewLocalEuler.y,
                            90f)) > .1f)
                {
                    Debug.LogError(
                        "Step 173 failed: first-person orientation is not repaired for " +
                        firearmNames[i] +
                        ".");
                    passed = false;
                }
            }

            List<VariantSpec> specs = BuildVariantSpecs();
            int foundVariantProducts = 0;
            int foundVariantDefinitions = 0;

            foreach (VariantSpec spec in specs)
            {
                if (AssetDatabase.LoadAssetAtPath<H51CombatItemDefinition>(
                        DefinitionFolder + "/" + spec.assetName + ".asset"))
                {
                    foundVariantDefinitions++;
                }

                if (terminal != null &&
                    terminal.Catalog.Any(
                        entry =>
                            entry != null &&
                            entry.ProductId == spec.productId))
                {
                    foundVariantProducts++;
                }
            }

            if (foundVariantDefinitions != specs.Count)
            {
                Debug.LogError(
                    "Step 173 failed: expected " +
                    specs.Count +
                    " additional firearm definitions, found " +
                    foundVariantDefinitions +
                    ".");
                passed = false;
            }

            if (foundVariantProducts != specs.Count)
            {
                Debug.LogError(
                    "Step 173 failed: expected " +
                    specs.Count +
                    " additional weapon-store products, found " +
                    foundVariantProducts +
                    ".");
                passed = false;
            }

            if (!registry || registry.Definitions.Count < 27)
            {
                int count = registry ? registry.Definitions.Count : 0;
                Debug.LogError(
                    "Step 173 failed: expected at least 27 combat definitions after the variant expansion; found " +
                    count +
                    ".");
                passed = false;
            }

            if (!terminal || !shopUI)
            {
                Debug.LogError(
                    "Step 173 failed: the shop terminal or HangarShopUI is missing.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Step 173 passed. Shared LowPolyWeapons material is URP/Lit with Texture_01 assigned, first-person firearm orientation is repaired, " +
                    specs.Count +
                    " additional firearm variants are present in the Weapons department, the combat registry contains " +
                    registry.Definitions.Count +
                    " definitions, and the upgraded shop UI component is present. Make a Development Build and verify held-gun direction, texture colors, department tabs, and 3D preview.");
            }
        }

        static int RepairLowPolyWeaponMaterials()
        {
            Shader urp =
                Shader.Find("Universal Render Pipeline/Lit");

            if (!urp)
                throw new InvalidOperationException(
                    "Universal Render Pipeline/Lit shader could not be found.");

            Texture2D atlas =
                AssetDatabase.LoadAssetAtPath<Texture2D>(MainTexturePath);

            int repaired = 0;

            Material main =
                AssetDatabase.LoadAssetAtPath<Material>(MainMaterialPath);

            if (main)
            {
                main.shader = urp;
                main.color = Color.white;

                if (main.HasProperty("_BaseColor"))
                    main.SetColor("_BaseColor",Color.white);
                if (main.HasProperty("_BaseMap"))
                    main.SetTexture("_BaseMap",atlas);
                if (main.HasProperty("_MainTex"))
                    main.SetTexture("_MainTex",atlas);
                if (main.HasProperty("_Metallic"))
                    main.SetFloat("_Metallic",.05f);
                if (main.HasProperty("_Smoothness"))
                    main.SetFloat("_Smoothness",.24f);

                EditorUtility.SetDirty(main);
                repaired++;
            }

            Material background =
                AssetDatabase.LoadAssetAtPath<Material>(BackgroundMaterialPath);

            if (background)
            {
                Color bgColor = background.color;
                background.shader = urp;
                background.color = bgColor;

                if (background.HasProperty("_BaseColor"))
                    background.SetColor("_BaseColor",bgColor);
                if (background.HasProperty("_Metallic"))
                    background.SetFloat("_Metallic",0f);
                if (background.HasProperty("_Smoothness"))
                    background.SetFloat("_Smoothness",.12f);

                EditorUtility.SetDirty(background);
                repaired++;
            }

            return repaired;
        }

        static List<VariantSpec> BuildVariantSpecs()
        {
            List<VariantSpec> specs = new List<VariantSpec>();

            for (int model=2;model<=6;model++)
            {
                specs.Add(
                    new VariantSpec
                    {
                        assetName = "ServiceRifle556_V" + model,
                        productId = "combat-variant-rifle-556-v" + model,
                        displayName = "5.56 Service Rifle — Variant " + model,
                        category = "Weapons / Rifles",
                        prefabPath =
                            "Assets/LowPolyWeapons/Prefabs/AssaultRifle/AssaultRifle" +
                            model +
                            "_01.prefab",
                        price = 1325 + model*85,
                        cartridge = H51Cartridge.FiveFiveSixByFortyFive,
                        mass = 4.0f,
                        velocity = 900f,
                        diameter = 5.56f,
                        drag = .25f,
                        damageScale = 1f,
                        capacity = 30,
                        rpm = 720f + model*8f,
                        automatic = true,
                        adsFov = 48f,
                        viewPosition = new Vector3(.18f,-.26f,.68f),
                        viewScale = 1.28f
                    });
            }

            for (int model=2;model<=6;model++)
            {
                specs.Add(
                    new VariantSpec
                    {
                        assetName = "ServicePistol9mm_V" + model,
                        productId = "combat-variant-pistol-9mm-v" + model,
                        displayName = "9mm Service Pistol — Variant " + model,
                        category = "Weapons / Pistols",
                        prefabPath =
                            "Assets/LowPolyWeapons/Prefabs/Pistols/Pistol" +
                            model +
                            "_01.prefab",
                        price = 625 + model*55,
                        cartridge = H51Cartridge.NineByNineteen,
                        mass = 8.0f,
                        velocity = 360f,
                        diameter = 9f,
                        drag = .30f,
                        damageScale = 1.05f,
                        capacity = 17,
                        rpm = 420f,
                        automatic = false,
                        adsFov = 51f,
                        viewPosition = new Vector3(.16f,-.29f,.55f),
                        viewScale = 1.05f
                    });
            }

            for (int model=2;model<=6;model++)
            {
                specs.Add(
                    new VariantSpec
                    {
                        assetName = "PrecisionRifle762_V" + model,
                        productId = "combat-variant-precision-762-v" + model,
                        displayName = "7.62 Precision Rifle — Variant " + model,
                        category = "Weapons / Precision Rifles",
                        prefabPath =
                            "Assets/LowPolyWeapons/Prefabs/SniperRifle/SniperRifle" +
                            model +
                            "_01.prefab",
                        price = 2050 + model*140,
                        cartridge = H51Cartridge.SevenSixTwoByFiftyOne,
                        mass = 9.5f,
                        velocity = 800f,
                        diameter = 7.62f,
                        drag = .23f,
                        damageScale = 1.05f,
                        capacity = 10,
                        rpm = 90f,
                        automatic = false,
                        adsFov = 42f,
                        viewPosition = new Vector3(.17f,-.24f,.72f),
                        viewScale = 1.35f
                    });
            }

            return specs;
        }

        static InventoryItemDefinition CreateOrRefreshVariantItem(
            VariantSpec spec)
        {
            string path =
                InventoryFolder + "/" + spec.assetName + ".asset";

            InventoryItemDefinition item =
                AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(path);

            if (!item)
            {
                item =
                    ScriptableObject.CreateInstance<InventoryItemDefinition>();
                item.name = spec.assetName;
                AssetDatabase.CreateAsset(item,path);
            }

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(spec.prefabPath);

            item.Configure(
                spec.productId.Replace("combat-variant-","h51-"),
                spec.displayName,
                "LowPolyWeapons firearm variant integrated with Hanger 51 ballistics, anatomy, attachments, inventory, and emergency-response systems.",
                1,
                new Color(.14f,.14f,.14f,1f),
                true,
                prefab,
                Vector3.one);

            EditorUtility.SetDirty(item);
            return item;
        }

        static H51CombatItemDefinition CreateOrRefreshVariantDefinition(
            VariantSpec spec,
            InventoryItemDefinition item)
        {
            string path =
                DefinitionFolder + "/" + spec.assetName + ".asset";

            H51CombatItemDefinition definition =
                AssetDatabase.LoadAssetAtPath<H51CombatItemDefinition>(path);

            if (!definition)
            {
                definition =
                    ScriptableObject.CreateInstance<H51CombatItemDefinition>();
                definition.name = spec.assetName;
                AssetDatabase.CreateAsset(definition,path);
            }

            definition.ConfigureFirearm(
                item,
                spec.cartridge,
                spec.mass,
                spec.velocity,
                spec.diameter,
                spec.drag,
                spec.damageScale,
                spec.capacity,
                spec.rpm,
                spec.automatic,
                spec.adsFov,
                spec.viewPosition,
                new Vector3(0f,90f,0f),
                spec.viewScale,
                Vector3.zero,
                Vector3.zero,
                Vector3.zero);

            EditorUtility.SetDirty(definition);
            return definition;
        }

        static List<H51CombatItemDefinition> LoadAllCombatDefinitions()
        {
            string[] guids =
                AssetDatabase.FindAssets(
                    "t:H51CombatItemDefinition",
                    new[] { DefinitionFolder });

            List<H51CombatItemDefinition> definitions =
                new List<H51CombatItemDefinition>();

            for (int i=0;i<guids.Length;i++)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(guids[i]);

                H51CombatItemDefinition definition =
                    AssetDatabase.LoadAssetAtPath<H51CombatItemDefinition>(path);

                if (definition && !definitions.Contains(definition))
                    definitions.Add(definition);
            }

            definitions.Sort(
                (a,b) =>
                    string.Compare(
                        a.name,
                        b.name,
                        StringComparison.Ordinal));

            return definitions;
        }

        static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) ||
                folderPath == "Assets" ||
                AssetDatabase.IsValidFolder(folderPath))
                return;

            string parent =
                Path.GetDirectoryName(folderPath)?.Replace("\\","/");
            string name =
                Path.GetFileName(folderPath);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent,name);
        }
    }
}
