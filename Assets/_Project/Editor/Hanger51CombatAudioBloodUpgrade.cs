using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Hanger51.Commerce;
using Hanger51.Health;
using Hanger51.Inventory;
using Hanger51.NPCs;
using Hanger51.Player;
using Hanger51.Weapons;
using Rng.Blood;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51CombatAudioBloodUpgrade
    {
        const string AutoInventoryFolder =
            "Assets/_Project/Inventory/Combat/Auto";
        const string AutoDefinitionFolder =
            "Assets/_Project/Weapons/Definitions/Auto";
        const string SoundRoot =
            "Assets/RealisticGunSounds_v1.2";

        sealed class FamilySpec
        {
            public string prefabFolder;
            public H51WeaponClass weaponClass;
            public string category;
            public string displayClass;
            public string soundFolder;
            public string soundPrefix;
            public int shotBanks;
            public int reloadBanks;
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
            public int projectilesPerShot;
            public float projectileSpread;
            public int basePrice;
        }

        [MenuItem("Hanger 51/World/Current/174 - Integrate Weapon Audio Blood And Full Categories")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError(
                    "Step 174 failed. Exit Play mode and let Unity finish compiling first.");
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
                    "Step 174 failed. Run the combat/shop setup first so the player combat registry and procurement terminal exist.");
                return;
            }

            EnsureFolder(AutoInventoryFolder);
            EnsureFolder(AutoDefinitionFolder);

            List<H51CombatItemDefinition> definitions =
                registry.Definitions
                    .Where(d => d)
                    .Distinct()
                    .ToList();

            Dictionary<GameObject,H51CombatItemDefinition> byPrefab =
                new Dictionary<GameObject,H51CombatItemDefinition>();

            for (int i=0;i<definitions.Count;i++)
            {
                H51CombatItemDefinition definition =
                    definitions[i];

                GameObject prefab =
                    definition.InventoryItem
                        ? definition.InventoryItem.WorldPrefab
                        : null;

                if (prefab && !byPrefab.ContainsKey(prefab))
                    byPrefab.Add(prefab,definition);
            }

            List<ShopCatalogEntry> rebuiltCatalog =
                terminal.Catalog
                    .Where(entry =>
                        entry != null &&
                        !entry.ProductId.StartsWith(
                            "combat-",
                            StringComparison.Ordinal))
                    .ToList();

            List<FamilySpec> families =
                BuildFamilies();

            int firearmCount = 0;
            int audioMapped = 0;
            int generatedDefinitions = 0;

            foreach (FamilySpec family in families)
            {
                List<GameObject> prefabs =
                    FindPrefabs(family.prefabFolder);

                for (int i=0;i<prefabs.Count;i++)
                {
                    GameObject prefab =
                        prefabs[i];

                    H51CombatItemDefinition definition;

                    if (!byPrefab.TryGetValue(prefab,out definition))
                    {
                        definition =
                            CreateFirearmDefinition(
                                family,
                                prefab,
                                i);

                        definitions.Add(definition);
                        byPrefab[prefab] = definition;
                        generatedDefinitions++;
                    }

                    definition.ConfigureWeaponClass(
                        family.weaponClass,
                        family.projectilesPerShot,
                        family.projectileSpread);

                    int familyNumber =
                        Mathf.Max(
                            1,
                            ExtractFirstNumber(prefab.name));

                    AudioClip[] shots =
                        LoadAudio(
                            SoundRoot + "/" +
                            family.soundFolder +
                            "/Gunshots/" +
                            family.soundPrefix +
                            WrapBank(
                                familyNumber,
                                family.shotBanks));

                    AudioClip[] reloads =
                        LoadAudio(
                            SoundRoot + "/" +
                            family.soundFolder +
                            "/Reloads/" +
                            family.soundPrefix +
                            WrapBank(
                                familyNumber,
                                family.reloadBanks));

                    AudioClip[] tails =
                        LoadAudio(
                            SoundRoot + "/" +
                            family.soundFolder +
                            "/Tails");

                    AudioClip[] aim =
                        LoadAudio(
                            SoundRoot + "/" +
                            family.soundFolder +
                            "/Foley/Aim");

                    AudioClip[] equip =
                        LoadAudio(
                            SoundRoot + "/" +
                            family.soundFolder +
                            "/Foley/Equip");

                    AudioClip[] shells =
                        LoadAudio(
                            SoundRoot + "/" +
                            family.soundFolder +
                            "/Foley/ShellsBrass");

                    if (shells.Length == 0)
                    {
                        shells =
                            LoadAudio(
                                SoundRoot +
                                "/AssaultRifle/Foley/ShellsBrass");
                    }

                    definition.ConfigureAudio(
                        shots,
                        tails,
                        reloads,
                        aim,
                        equip,
                        shells);

                    EditorUtility.SetDirty(definition);

                    if (shots.Length > 0)
                        audioMapped++;

                    rebuiltCatalog.Add(
                        Product(
                            "combat-auto-" +
                            Slug(family.displayClass) +
                            "-" +
                            Slug(prefab.name),
                            family.category,
                            FriendlyFirearmName(
                                family.displayClass,
                                prefab.name),
                            "LowPolyWeapons " +
                            family.displayClass +
                            " variant. Uses Hanger 51 anatomical ballistics, class-matched RealisticGunSounds audio, attachment handling and emergency response.",
                            family.basePrice +
                            familyNumber*35 +
                            (i%6)*12,
                            definition.InventoryItem,
                            1));

                    firearmCount++;
                }
            }

            H51CombatItemDefinition ammo12 =
                EnsureTwelveGaugeAmmo(definitions);

            RebuildSupportProducts(
                rebuiltCatalog,
                definitions,
                ammo12);

            definitions =
                definitions
                    .Where(d => d)
                    .Distinct()
                    .OrderBy(d => d.name)
                    .ToList();

            registry.Configure(definitions);
            terminal.ReplaceCatalog(rebuiltCatalog);

            EditorUtility.SetDirty(registry);
            EditorUtility.SetDirty(terminal);

            int bloodCitizens = 0;
            H51CitizenIdentity[] citizens =
                UnityEngine.Object.FindObjectsByType<H51CitizenIdentity>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            for (int i=0;i<citizens.Length;i++)
            {
                H51CitizenIdentity citizen =
                    citizens[i];

                if (!citizen ||
                    !citizen.gameObject.scene.IsValid())
                    continue;

                H51AnatomicalHealth health =
                    citizen.GetComponent<H51AnatomicalHealth>();

                if (!health)
                    health =
                        Undo.AddComponent<H51AnatomicalHealth>(
                            citizen.gameObject);

                H51AnatomicalHitboxRig rig =
                    citizen.GetComponent<H51AnatomicalHitboxRig>();

                if (!rig)
                    rig =
                        Undo.AddComponent<H51AnatomicalHitboxRig>(
                            citizen.gameObject);

                rig.EnsureBuilt();

                if (!citizen.GetComponent<H51BloodVisualBridge>())
                    Undo.AddComponent<H51BloodVisualBridge>(
                        citizen.gameObject);

                BloodWounds wounds =
                    citizen.GetComponent<BloodWounds>();
                if (!wounds)
                    wounds =
                        Undo.AddComponent<BloodWounds>(
                            citizen.gameObject);

                BloodSupply supply =
                    citizen.GetComponent<BloodSupply>();
                if (!supply)
                    supply =
                        Undo.AddComponent<BloodSupply>(
                            citizen.gameObject);

                supply.Capacity =
                    health.NormalBloodVolumeLiters;

                EditorUtility.SetDirty(citizen.gameObject);
                EditorUtility.SetDirty(supply);
                bloodCitizens++;
            }

            int remainingBloodIssues;
            int bloodSetupFixes =
                RunBloodSetupFixAll(
                    out remainingBloodIssues);

            EditorSceneManager.MarkSceneDirty(
                SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.SaveOpenScenes();

            Hanger51BuildTools.PrepareCurrentSceneForBuild(false);

            Debug.Log(
                "Step 174 complete. Firearm prefabs cataloged=" +
                firearmCount +
                ", new firearm definitions generated=" +
                generatedDefinitions +
                ", definitions with mapped gunshot banks=" +
                audioMapped +
                ", citizens with anatomy/RNG Blood integration=" +
                bloodCitizens +
                ", RNG Blood project fixes applied=" +
                bloodSetupFixes +
                ", remaining RNG Blood setup issues=" +
                remainingBloodIssues +
                ". Weapons are now subdivided by handgun, assault rifle, sniper rifle, SMG, machine gun, shotgun, explosives, ammunition and gear. Run Step 175.");
        }

        [MenuItem("Hanger 51/World/Current/175 - Validate Weapon Audio Blood And Full Categories")]
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

            int firearmCount = 0;
            int missingGunshots = 0;
            int missingReloads = 0;
            int shotgunCount = 0;
            int smgCount = 0;
            int machineGunCount = 0;
            int handgunCount = 0;
            int assaultCount = 0;
            int sniperCount = 0;

            if (registry)
            {
                for (int i=0;i<registry.Definitions.Count;i++)
                {
                    H51CombatItemDefinition definition =
                        registry.Definitions[i];

                    if (!definition ||
                        definition.Kind != H51CombatItemKind.Firearm)
                        continue;

                    firearmCount++;

                    if (definition.GunshotClips == null ||
                        definition.GunshotClips.Length == 0)
                        missingGunshots++;

                    if (definition.ReloadClips == null ||
                        definition.ReloadClips.Length == 0)
                        missingReloads++;

                    switch (definition.WeaponClass)
                    {
                        case H51WeaponClass.Handgun:
                            handgunCount++;
                            break;
                        case H51WeaponClass.AssaultRifle:
                            assaultCount++;
                            break;
                        case H51WeaponClass.SniperRifle:
                            sniperCount++;
                            break;
                        case H51WeaponClass.Smg:
                            smgCount++;
                            break;
                        case H51WeaponClass.MachineGun:
                            machineGunCount++;
                            break;
                        case H51WeaponClass.Shotgun:
                            shotgunCount++;
                            break;
                    }
                }
            }

            H51CitizenIdentity[] citizens =
                UnityEngine.Object.FindObjectsByType<H51CitizenIdentity>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            int missingBloodBridge =
                citizens.Count(
                    c => c &&
                         !c.GetComponent<H51BloodVisualBridge>());

            int missingWounds =
                citizens.Count(
                    c => c &&
                         !c.GetComponent<BloodWounds>());

            int missingSupply =
                citizens.Count(
                    c => c &&
                         !c.GetComponent<BloodSupply>());

            int categorizedProducts = 0;
            int uncategorizedCombat = 0;

            if (terminal)
            {
                for (int i=0;i<terminal.Catalog.Count;i++)
                {
                    ShopCatalogEntry entry =
                        terminal.Catalog[i];

                    if (entry == null ||
                        !entry.ProductId.StartsWith(
                            "combat-",
                            StringComparison.Ordinal))
                        continue;

                    if (entry.Category.StartsWith(
                            "Weapons /",
                            StringComparison.Ordinal))
                        categorizedProducts++;
                    else
                        uncategorizedCombat++;
                }
            }

            int bloodIssueCount =
                GetBloodSetupIssueCount();

            int expectedFirearms =
                BuildFamilies()
                    .Sum(family =>
                        FindPrefabs(family.prefabFolder).Count);

            bool ok =
                player &&
                registry &&
                terminal &&
                firearmCount >= expectedFirearms &&
                missingGunshots == 0 &&
                handgunCount > 0 &&
                assaultCount > 0 &&
                sniperCount > 0 &&
                smgCount > 0 &&
                machineGunCount > 0 &&
                shotgunCount > 0 &&
                missingBloodBridge == 0 &&
                missingWounds == 0 &&
                missingSupply == 0 &&
                categorizedProducts > firearmCount &&
                uncategorizedCombat == 0 &&
                bloodIssueCount == 0;

            if (ok)
            {
                Debug.Log(
                    "Step 175 passed. Firearms=" +
                    firearmCount +
                    " (handguns=" + handgunCount +
                    ", assault rifles=" + assaultCount +
                    ", sniper rifles=" + sniperCount +
                    ", SMGs=" + smgCount +
                    ", machine guns=" + machineGunCount +
                    ", shotguns=" + shotgunCount +
                    "), all have gunshot audio, categorized combat products=" +
                    categorizedProducts +
                    ", citizens with RNG Blood=" +
                    citizens.Length +
                    "/" + citizens.Length +
                    ", expected full-roster firearms=" +
                    expectedFirearms +
                    ", RNG Blood setup issues=0.");
            }
            else
            {
                Debug.LogError(
                    "Step 175 failed. player=" +
                    (player ? "yes" : "MISSING") +
                    ", registry=" +
                    (registry ? "yes" : "MISSING") +
                    ", terminal=" +
                    (terminal ? "yes" : "MISSING") +
                    ", firearms=" + firearmCount +
                    ", missing gunshot audio=" + missingGunshots +
                    ", missing reload audio=" + missingReloads +
                    ", handguns=" + handgunCount +
                    ", assault=" + assaultCount +
                    ", sniper=" + sniperCount +
                    ", SMG=" + smgCount +
                    ", machine guns=" + machineGunCount +
                    ", shotguns=" + shotgunCount +
                    ", missing blood bridges=" + missingBloodBridge +
                    ", missing BloodWounds=" + missingWounds +
                    ", missing BloodSupply=" + missingSupply +
                    ", categorized combat products=" + categorizedProducts +
                    ", uncategorized combat products=" + uncategorizedCombat +
                    ", expected full-roster firearms=" + expectedFirearms +
                    ", RNG Blood setup issues=" + bloodIssueCount +
                    ".");
            }
        }

        static int RunBloodSetupFixAll(
            out int remainingIssues)
        {
            remainingIssues = -1;

            Type setupType =
                Type.GetType(
                    "Rng.Blood.EditorTools.BloodSetup, Rng.Blood.Editor");

            if (setupType == null)
            {
                Debug.LogWarning(
                    "Step 174 could not load the RNG Blood editor setup assembly. Runtime blood integration was still installed. Open Tools -> RNG Blood -> Setup manually if blood stains do not appear.");
                return 0;
            }

            System.Reflection.MethodInfo fixAll =
                setupType.GetMethod(
                    "FixAll",
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Static);

            int fixedCount = 0;

            if (fixAll != null)
            {
                object result =
                    fixAll.Invoke(
                        null,
                        null);

                if (result is int count)
                    fixedCount = count;
            }

            remainingIssues =
                GetBloodSetupIssueCount();

            return fixedCount;
        }

        static int GetBloodSetupIssueCount()
        {
            Type setupType =
                Type.GetType(
                    "Rng.Blood.EditorTools.BloodSetup, Rng.Blood.Editor");

            if (setupType == null)
                return -1;

            System.Reflection.MethodInfo check =
                setupType.GetMethod(
                    "Check",
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Static);

            if (check == null)
                return -1;

            object result =
                check.Invoke(
                    null,
                    null);

            if (result is System.Collections.ICollection collection)
                return collection.Count;

            System.Collections.IEnumerable enumerable =
                result as System.Collections.IEnumerable;

            if (enumerable == null)
                return -1;

            int count = 0;
            foreach (object _ in enumerable)
                count++;

            return count;
        }

        static List<FamilySpec> BuildFamilies()
        {
            return new List<FamilySpec>
            {
                new FamilySpec
                {
                    prefabFolder = "Assets/LowPolyWeapons/Prefabs/Pistols",
                    weaponClass = H51WeaponClass.Handgun,
                    category = "Weapons / Handguns",
                    displayClass = "Handgun",
                    soundFolder = "Handgun",
                    soundPrefix = "Handgun",
                    shotBanks = 5,
                    reloadBanks = 5,
                    cartridge = H51Cartridge.NineByNineteen,
                    mass = 8f,
                    velocity = 360f,
                    diameter = 9f,
                    drag = .30f,
                    damageScale = 1.05f,
                    capacity = 17,
                    rpm = 420f,
                    automatic = false,
                    adsFov = 51f,
                    viewPosition = new Vector3(.16f,-.29f,.34f),
                    viewScale = 1.05f,
                    projectilesPerShot = 1,
                    projectileSpread = .08f,
                    basePrice = 650
                },
                new FamilySpec
                {
                    prefabFolder = "Assets/LowPolyWeapons/Prefabs/AssaultRifle",
                    weaponClass = H51WeaponClass.AssaultRifle,
                    category = "Weapons / Assault Rifles",
                    displayClass = "Assault Rifle",
                    soundFolder = "AssaultRifle",
                    soundPrefix = "AssaultRifle",
                    shotBanks = 12,
                    reloadBanks = 6,
                    cartridge = H51Cartridge.FiveFiveSixByFortyFive,
                    mass = 4f,
                    velocity = 900f,
                    diameter = 5.56f,
                    drag = .25f,
                    damageScale = 1f,
                    capacity = 30,
                    rpm = 750f,
                    automatic = true,
                    adsFov = 48f,
                    viewPosition = new Vector3(.18f,-.26f,.40f),
                    viewScale = 1.28f,
                    projectilesPerShot = 1,
                    projectileSpread = .06f,
                    basePrice = 1350
                },
                new FamilySpec
                {
                    prefabFolder = "Assets/LowPolyWeapons/Prefabs/SniperRifle",
                    weaponClass = H51WeaponClass.SniperRifle,
                    category = "Weapons / Sniper Rifles",
                    displayClass = "Sniper Rifle",
                    soundFolder = "SniperRifle",
                    soundPrefix = "SniperRifle",
                    shotBanks = 5,
                    reloadBanks = 5,
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
                    viewPosition = new Vector3(.17f,-.24f,.42f),
                    viewScale = 1.35f,
                    projectilesPerShot = 1,
                    projectileSpread = .035f,
                    basePrice = 2150
                },
                new FamilySpec
                {
                    prefabFolder = "Assets/LowPolyWeapons/Prefabs/SMG",
                    weaponClass = H51WeaponClass.Smg,
                    category = "Weapons / SMGs",
                    displayClass = "SMG",
                    soundFolder = "Smg",
                    soundPrefix = "Smg",
                    shotBanks = 5,
                    reloadBanks = 5,
                    cartridge = H51Cartridge.NineByNineteen,
                    mass = 8f,
                    velocity = 390f,
                    diameter = 9f,
                    drag = .30f,
                    damageScale = .96f,
                    capacity = 30,
                    rpm = 860f,
                    automatic = true,
                    adsFov = 49f,
                    viewPosition = new Vector3(.17f,-.27f,.37f),
                    viewScale = 1.15f,
                    projectilesPerShot = 1,
                    projectileSpread = .10f,
                    basePrice = 1050
                },
                new FamilySpec
                {
                    prefabFolder = "Assets/LowPolyWeapons/Prefabs/LMG",
                    weaponClass = H51WeaponClass.MachineGun,
                    category = "Weapons / Machine Guns",
                    displayClass = "Machine Gun",
                    soundFolder = "MachineGun",
                    soundPrefix = "MachineGun",
                    shotBanks = 5,
                    reloadBanks = 3,
                    cartridge = H51Cartridge.FiveFiveSixByFortyFive,
                    mass = 4f,
                    velocity = 875f,
                    diameter = 5.56f,
                    drag = .25f,
                    damageScale = 1.02f,
                    capacity = 100,
                    rpm = 720f,
                    automatic = true,
                    adsFov = 46f,
                    viewPosition = new Vector3(.18f,-.28f,.42f),
                    viewScale = 1.30f,
                    projectilesPerShot = 1,
                    projectileSpread = .11f,
                    basePrice = 2350
                },
                new FamilySpec
                {
                    prefabFolder = "Assets/LowPolyWeapons/Prefabs/Shotguns",
                    weaponClass = H51WeaponClass.Shotgun,
                    category = "Weapons / Shotguns",
                    displayClass = "Shotgun",
                    soundFolder = "Shotgun",
                    soundPrefix = "Shotgun",
                    shotBanks = 4,
                    reloadBanks = 4,
                    cartridge = H51Cartridge.TwelveGauge,
                    mass = 3.5f,
                    velocity = 400f,
                    diameter = 8.4f,
                    drag = .34f,
                    damageScale = 1f,
                    capacity = 8,
                    rpm = 85f,
                    automatic = false,
                    adsFov = 50f,
                    viewPosition = new Vector3(.18f,-.27f,.40f),
                    viewScale = 1.24f,
                    projectilesPerShot = 8,
                    projectileSpread = 2.7f,
                    basePrice = 950
                }
            };
        }

        static H51CombatItemDefinition CreateFirearmDefinition(
            FamilySpec family,
            GameObject prefab,
            int index)
        {
            string baseName =
                "Auto_" +
                family.weaponClass +
                "_" +
                prefab.name;

            string itemPath =
                AutoInventoryFolder +
                "/" +
                baseName +
                ".asset";

            InventoryItemDefinition item =
                AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(
                    itemPath);

            if (!item)
            {
                item =
                    ScriptableObject.CreateInstance<InventoryItemDefinition>();
                item.name = baseName;
                AssetDatabase.CreateAsset(item,itemPath);
            }

            item.Configure(
                "h51-auto-" +
                Slug(family.displayClass) +
                "-" +
                Slug(prefab.name),
                FriendlyFirearmName(
                    family.displayClass,
                    prefab.name),
                "Automatically integrated LowPolyWeapons firearm variant.",
                1,
                new Color(.15f,.15f,.15f,1f),
                true,
                prefab,
                Vector3.one);

            EditorUtility.SetDirty(item);

            string definitionPath =
                AutoDefinitionFolder +
                "/" +
                baseName +
                ".asset";

            H51CombatItemDefinition definition =
                AssetDatabase.LoadAssetAtPath<H51CombatItemDefinition>(
                    definitionPath);

            if (!definition)
            {
                definition =
                    ScriptableObject.CreateInstance<H51CombatItemDefinition>();
                definition.name = baseName;
                AssetDatabase.CreateAsset(
                    definition,
                    definitionPath);
            }

            definition.ConfigureFirearm(
                item,
                family.cartridge,
                family.mass,
                family.velocity,
                family.diameter,
                family.drag,
                family.damageScale,
                family.capacity,
                family.rpm,
                family.automatic,
                family.adsFov,
                family.viewPosition,
                new Vector3(0f,90f,0f),
                family.viewScale,
                Vector3.zero,
                Vector3.zero,
                Vector3.zero);

            EditorUtility.SetDirty(definition);
            return definition;
        }

        static H51CombatItemDefinition EnsureTwelveGaugeAmmo(
            List<H51CombatItemDefinition> definitions)
        {
            H51CombatItemDefinition existing =
                definitions.FirstOrDefault(
                    d => d &&
                         d.Kind == H51CombatItemKind.Ammunition &&
                         d.Cartridge == H51Cartridge.TwelveGauge);

            if (existing)
                return existing;

            const string itemPath =
                "Assets/_Project/Inventory/Combat/Ammo12Gauge.asset";
            const string definitionPath =
                "Assets/_Project/Weapons/Definitions/Ammo12Gauge.asset";

            InventoryItemDefinition item =
                AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(
                    itemPath);

            if (!item)
            {
                item =
                    ScriptableObject.CreateInstance<InventoryItemDefinition>();
                item.name = "Ammo12Gauge";
                AssetDatabase.CreateAsset(item,itemPath);
            }

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/LowPolyWeapons/Prefabs/Loot/AmoBox_04.prefab");

            item.Configure(
                "h51-ammo-12g",
                "12 Gauge Ammunition",
                "Boxed 12 gauge shotgun ammunition.",
                200,
                new Color(.55f,.35f,.18f,1f),
                false,
                prefab,
                Vector3.one);

            EditorUtility.SetDirty(item);

            H51CombatItemDefinition definition =
                AssetDatabase.LoadAssetAtPath<H51CombatItemDefinition>(
                    definitionPath);

            if (!definition)
            {
                definition =
                    ScriptableObject.CreateInstance<H51CombatItemDefinition>();
                definition.name = "Ammo12Gauge";
                AssetDatabase.CreateAsset(
                    definition,
                    definitionPath);
            }

            definition.ConfigureAmmo(
                item,
                H51Cartridge.TwelveGauge);

            EditorUtility.SetDirty(definition);
            definitions.Add(definition);

            return definition;
        }

        static void RebuildSupportProducts(
            List<ShopCatalogEntry> catalog,
            List<H51CombatItemDefinition> definitions,
            H51CombatItemDefinition ammo12)
        {
            for (int i=0;i<definitions.Count;i++)
            {
                H51CombatItemDefinition definition =
                    definitions[i];

                if (!definition ||
                    !definition.InventoryItem ||
                    definition.Kind == H51CombatItemKind.Firearm)
                    continue;

                string category;
                int quantity = 1;
                int price;

                switch (definition.Kind)
                {
                    case H51CombatItemKind.Ammunition:
                        category = "Weapons / Ammunition";
                        quantity =
                            definition.Cartridge == H51Cartridge.TwelveGauge
                                ? 50
                                : definition.Cartridge == H51Cartridge.SevenSixTwoByFiftyOne
                                    ? 60
                                    : 100;
                        price =
                            definition.Cartridge == H51Cartridge.TwelveGauge
                                ? 72
                                : definition.Cartridge == H51Cartridge.SevenSixTwoByFiftyOne
                                    ? 96
                                    : definition.Cartridge == H51Cartridge.FiveFiveSixByFortyFive
                                        ? 84
                                        : 48;
                        break;

                    case H51CombatItemKind.Grenade:
                        category = "Weapons / Explosives";
                        price = 92;
                        break;

                    case H51CombatItemKind.MedicalKit:
                        category = "Weapons / Medical";
                        price = 135;
                        break;

                    default:
                        category = "Weapons / Accessories";
                        price =
                            definition.Kind == H51CombatItemKind.Optic
                                ? 450
                                : 285;
                        break;
                }

                catalog.Add(
                    Product(
                        "combat-support-" +
                        Slug(definition.InventoryItem.ItemId),
                        category,
                        definition.InventoryItem.DisplayName,
                        definition.InventoryItem.Description,
                        price,
                        definition.InventoryItem,
                        quantity));
            }
        }

        static ShopCatalogEntry Product(
            string id,
            string category,
            string name,
            string description,
            int price,
            InventoryItemDefinition item,
            int quantity)
        {
            ShopCatalogEntry entry =
                new ShopCatalogEntry();

            entry.Configure(
                id,
                category,
                name,
                description,
                price,
                ShopProductKind.InventoryItem,
                item,
                quantity,
                null);

            return entry;
        }

        static List<GameObject> FindPrefabs(
            string folder)
        {
            string[] guids =
                AssetDatabase.FindAssets(
                    "t:GameObject",
                    new[] { folder });

            List<GameObject> prefabs =
                new List<GameObject>();

            for (int i=0;i<guids.Length;i++)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(
                        guids[i]);

                if (!path.EndsWith(
                        ".prefab",
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        path);

                if (prefab)
                    prefabs.Add(prefab);
            }

            prefabs.Sort(
                (a,b) =>
                    NaturalCompare(
                        a.name,
                        b.name));

            return prefabs;
        }

        static AudioClip[] LoadAudio(
            string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
                return Array.Empty<AudioClip>();

            string[] guids =
                AssetDatabase.FindAssets(
                    "t:AudioClip",
                    new[] { folder });

            List<AudioClip> clips =
                new List<AudioClip>();

            for (int i=0;i<guids.Length;i++)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(
                        guids[i]);

                AudioClip clip =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(
                        path);

                if (clip)
                    clips.Add(clip);
            }

            clips.Sort(
                (a,b) =>
                    NaturalCompare(
                        a.name,
                        b.name));

            return clips.ToArray();
        }

        static int ExtractFirstNumber(
            string value)
        {
            Match match =
                Regex.Match(
                    value ?? string.Empty,
                    @"\d+");

            return match.Success &&
                   int.TryParse(
                       match.Value,
                       out int number)
                ? number
                : 1;
        }

        static int WrapBank(
            int value,
            int count)
        {
            count = Mathf.Max(1,count);
            return ((Mathf.Max(1,value)-1)%count)+1;
        }

        static string FriendlyFirearmName(
            string className,
            string prefabName)
        {
            Match match =
                Regex.Match(
                    prefabName ?? string.Empty,
                    @"(\d+)_0?(\d+)$");

            if (!match.Success)
                return className + " — " + prefabName;

            return
                className +
                " " +
                match.Groups[1].Value +
                " — Variant " +
                match.Groups[2].Value;
        }

        static string Slug(
            string value)
        {
            string slug =
                Regex.Replace(
                    (value ?? string.Empty).ToLowerInvariant(),
                    @"[^a-z0-9]+",
                    "-")
                .Trim('-');

            return string.IsNullOrWhiteSpace(slug)
                ? "item"
                : slug;
        }

        static int NaturalCompare(
            string left,
            string right)
        {
            return string.Compare(
                left,
                right,
                StringComparison.OrdinalIgnoreCase);
        }

        static void EnsureFolder(
            string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) ||
                folderPath == "Assets" ||
                AssetDatabase.IsValidFolder(folderPath))
                return;

            string parent =
                Path.GetDirectoryName(folderPath)?
                    .Replace("\\","/");
            string name =
                Path.GetFileName(folderPath);

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent,name);
        }
    }
}
