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
    public static class Hanger51LowPolyWeaponsCombatSetup
    {
        const string InventoryFolder = "Assets/_Project/Inventory/Combat";
        const string DefinitionFolder = "Assets/_Project/Weapons/Definitions";

        const string RiflePrefab = "Assets/LowPolyWeapons/Prefabs/AssaultRifle/AssaultRifle1_01.prefab";
        const string PistolPrefab = "Assets/LowPolyWeapons/Prefabs/Pistols/Pistol1_01.prefab";
        const string PrecisionPrefab = "Assets/LowPolyWeapons/Prefabs/SniperRifle/SniperRifle1_01.prefab";
        const string RedDotPrefab = "Assets/LowPolyWeapons/Prefabs/Optic/Optic_01.prefab";
        const string ScopePrefab = "Assets/LowPolyWeapons/Prefabs/Optic/Optic_05.prefab";
        const string SuppressorPrefab = "Assets/LowPolyWeapons/Prefabs/Attachments/Muffler_01_1.prefab";
        const string GripPrefab = "Assets/LowPolyWeapons/Prefabs/Attachments/Mod_01.prefab";
        const string GrenadePrefab = "Assets/LowPolyWeapons/Prefabs/Grenade/Grenade_01.prefab";
        const string Ammo556Prefab = "Assets/LowPolyWeapons/Prefabs/Loot/AmoBox_01.prefab";
        const string Ammo9Prefab = "Assets/LowPolyWeapons/Prefabs/Loot/AmoBox_02.prefab";
        const string Ammo762Prefab = "Assets/LowPolyWeapons/Prefabs/Loot/AmoBox_03.prefab";
        const string MedicalPrefab = "Assets/LowPolyWeapons/Prefabs/Loot/Healthpack_01.prefab";

        sealed class CombatAssets
        {
            public InventoryItemDefinition rifle;
            public InventoryItemDefinition pistol;
            public InventoryItemDefinition precision;
            public InventoryItemDefinition ammo556;
            public InventoryItemDefinition ammo9;
            public InventoryItemDefinition ammo762;
            public InventoryItemDefinition redDot;
            public InventoryItemDefinition scope;
            public InventoryItemDefinition suppressor;
            public InventoryItemDefinition grip;
            public InventoryItemDefinition grenade;
            public InventoryItemDefinition traumaKit;
            public List<H51CombatItemDefinition> definitions =
                new List<H51CombatItemDefinition>();
        }

        [MenuItem("Hanger 51/World/Current/170 - Install Low Poly Weapons Combat System")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError(
                    "Step 170 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            FirstPersonController player =
                UnityEngine.Object.FindFirstObjectByType<FirstPersonController>(
                    FindObjectsInactive.Include);
            PlayerInventory inventory =
                player ? player.GetComponent<PlayerInventory>() : null;
            EquippedItemView equippedView =
                player ? player.GetComponentInChildren<EquippedItemView>(true) : null;
            HangarShopTerminal terminal =
                UnityEngine.Object.FindFirstObjectByType<HangarShopTerminal>(
                    FindObjectsInactive.Include);

            if (!player || !inventory || !equippedView || !terminal)
            {
                Debug.LogError(
                    "Step 170 failed. Player, PlayerInventory, EquippedItemView, or the existing Hanger 51 parts computer is missing. Keep the current shop/inventory system installed before adding weapons.");
                return;
            }

            string[] requiredPrefabs =
            {
                RiflePrefab,
                PistolPrefab,
                PrecisionPrefab,
                RedDotPrefab,
                ScopePrefab,
                SuppressorPrefab,
                GripPrefab,
                GrenadePrefab,
                Ammo556Prefab,
                Ammo9Prefab,
                Ammo762Prefab,
                MedicalPrefab
            };

            List<string> missing =
                requiredPrefabs
                    .Where(path => !AssetDatabase.LoadAssetAtPath<GameObject>(path))
                    .ToList();

            if (missing.Count > 0)
            {
                Debug.LogError(
                    "Step 170 failed. The LowPolyWeapons import is incomplete. Missing: " +
                    string.Join(", ",missing));
                return;
            }

            EnsureFolder(InventoryFolder);
            EnsureFolder(DefinitionFolder);

            CombatAssets assets =
                BuildAssets();

            inventory.EnsureMinimumSlotCount(20);

            H51CombatItemRegistry registry =
                player.GetComponent<H51CombatItemRegistry>();
            if (!registry)
                registry =
                    Undo.AddComponent<H51CombatItemRegistry>(player.gameObject);
            registry.Configure(assets.definitions);

            H51MedicalTreatmentUI medical =
                player.GetComponent<H51MedicalTreatmentUI>();
            if (!medical)
                medical =
                    Undo.AddComponent<H51MedicalTreatmentUI>(player.gameObject);

            H51PlayerCombatController combat =
                player.GetComponent<H51PlayerCombatController>();
            if (!combat)
                combat =
                    Undo.AddComponent<H51PlayerCombatController>(player.gameObject);

            if (!player.GetComponent<H51AnatomicalHealth>())
                Undo.AddComponent<H51AnatomicalHealth>(player.gameObject);

            H51CitizenIdentity[] citizens =
                UnityEngine.Object.FindObjectsByType<H51CitizenIdentity>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            int addedNpcHealth = 0;
            for (int i=0;i<citizens.Length;i++)
            {
                H51CitizenIdentity citizen = citizens[i];
                if (!citizen || !citizen.gameObject.scene.IsValid())
                    continue;

                if (!citizen.GetComponent<H51AnatomicalHealth>())
                {
                    Undo.AddComponent<H51AnatomicalHealth>(citizen.gameObject);
                    addedNpcHealth++;
                }

                if (!citizen.GetComponent<H51AnatomicalHitboxRig>())
                    Undo.AddComponent<H51AnatomicalHitboxRig>(citizen.gameObject);
            }

            List<ShopCatalogEntry> catalog =
                terminal.Catalog
                    .Where(entry =>
                        entry != null &&
                        !entry.ProductId.StartsWith(
                            "combat-",
                            StringComparison.Ordinal))
                    .ToList();

            AddCombatCatalog(catalog,assets);
            terminal.ReplaceCatalog(catalog);

            EditorUtility.SetDirty(inventory);
            EditorUtility.SetDirty(registry);
            EditorUtility.SetDirty(combat);
            EditorUtility.SetDirty(medical);
            EditorUtility.SetDirty(terminal);

            EditorSceneManager.MarkSceneDirty(
                SceneManager.GetActiveScene());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.SaveOpenScenes();

            Hanger51BuildTools.PrepareCurrentSceneForBuild(false);

            Debug.Log(
                "Step 170 complete. LowPolyWeapons first playable set installed: 5.56 service rifle, 9mm pistol, 7.62 precision rifle, three ammunition types, red-dot optic, magnified optic, suppressor, underbarrel grip, fragmentation grenade, and trauma kit. " +
                "The existing parts computer now sells all combat products through the same shipment/unboxing system; inventory expanded to " +
                inventory.SlotCount +
                " slots; NPC anatomy added where missing=" +
                addedNpcHealth +
                ". Controls: LMB fire/throw, RMB aim, R reload, C customize, F use trauma kit on aimed patient. Run Step 171.");
        }

        [MenuItem("Hanger 51/World/Current/171 - Validate Low Poly Weapons Combat System")]
        public static void Validate()
        {
            FirstPersonController player =
                UnityEngine.Object.FindFirstObjectByType<FirstPersonController>(
                    FindObjectsInactive.Include);
            PlayerInventory inventory =
                player ? player.GetComponent<PlayerInventory>() : null;
            H51CombatItemRegistry registry =
                player ? player.GetComponent<H51CombatItemRegistry>() : null;
            H51PlayerCombatController combat =
                player ? player.GetComponent<H51PlayerCombatController>() : null;
            H51MedicalTreatmentUI medical =
                player ? player.GetComponent<H51MedicalTreatmentUI>() : null;
            H51AnatomicalHealth playerHealth =
                player ? player.GetComponent<H51AnatomicalHealth>() : null;
            HangarShopTerminal terminal =
                UnityEngine.Object.FindFirstObjectByType<HangarShopTerminal>(
                    FindObjectsInactive.Include);
            H51EmergencyDispatchController dispatch =
                UnityEngine.Object.FindFirstObjectByType<H51EmergencyDispatchController>(
                    FindObjectsInactive.Include);

            int combatProducts =
                terminal != null
                    ? terminal.Catalog.Count(entry =>
                        entry != null &&
                        entry.ProductId.StartsWith(
                            "combat-",
                            StringComparison.Ordinal))
                    : 0;

            int definitions =
                registry != null && registry.Definitions != null
                    ? registry.Definitions.Count
                    : 0;

            int badDefinitions = 0;
            int firearmCount = 0;
            int ammoCount = 0;
            int attachmentCount = 0;
            int grenadeCount = 0;
            int medicalCount = 0;

            if (registry != null)
            {
                for (int i=0;i<registry.Definitions.Count;i++)
                {
                    H51CombatItemDefinition definition =
                        registry.Definitions[i];

                    if (!definition ||
                        !definition.InventoryItem ||
                        !definition.InventoryItem.WorldPrefab)
                    {
                        badDefinitions++;
                        continue;
                    }

                    if (definition.Kind == H51CombatItemKind.Firearm)
                        firearmCount++;
                    else if (definition.Kind == H51CombatItemKind.Ammunition)
                        ammoCount++;
                    else if (definition.Kind == H51CombatItemKind.Optic ||
                             definition.Kind == H51CombatItemKind.MuzzleAttachment ||
                             definition.Kind == H51CombatItemKind.UnderbarrelAttachment)
                        attachmentCount++;
                    else if (definition.Kind == H51CombatItemKind.Grenade)
                        grenadeCount++;
                    else if (definition.Kind == H51CombatItemKind.MedicalKit)
                        medicalCount++;
                }
            }

            H51CitizenIdentity[] citizens =
                UnityEngine.Object.FindObjectsByType<H51CitizenIdentity>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            int missingNpcHealth =
                citizens.Count(c => c && !c.GetComponent<H51AnatomicalHealth>());
            int missingNpcHitboxes =
                citizens.Count(c => c && !c.GetComponent<H51AnatomicalHitboxRig>());
            int missingNpcCapsules =
                citizens.Count(c => c && !c.GetComponent<CapsuleCollider>());

            bool ok =
                player &&
                inventory &&
                inventory.SlotCount >= 20 &&
                registry &&
                combat &&
                medical &&
                playerHealth &&
                terminal &&
                dispatch &&
                combatProducts >= 12 &&
                definitions >= 12 &&
                badDefinitions == 0 &&
                firearmCount >= 3 &&
                ammoCount >= 3 &&
                attachmentCount >= 4 &&
                grenadeCount >= 1 &&
                medicalCount >= 1 &&
                missingNpcHealth == 0 &&
                missingNpcHitboxes == 0 &&
                missingNpcCapsules == 0;

            if (ok)
            {
                Debug.Log(
                    "Step 171 passed. Combat shop products=" +
                    combatProducts +
                    ", definitions=" + definitions +
                    " (firearms=" + firearmCount +
                    ", ammo=" + ammoCount +
                    ", attachments=" + attachmentCount +
                    ", grenades=" + grenadeCount +
                    ", medical=" + medicalCount +
                    "), inventory slots=" + inventory.SlotCount +
                    ", NPC anatomical-health coverage=" +
                    (citizens.Length-missingNpcHealth) +
                    "/" + citizens.Length +
                    ", anatomical hitboxes=" +
                    (citizens.Length-missingNpcHitboxes) +
                    "/" + citizens.Length +
                    ", emergency dispatch connected. Build a Development Build and test purchase -> unbox -> pickup -> equip -> reload/fire/customize/treat.");
            }
            else
            {
                Debug.LogError(
                    "Step 171 failed. player=" + (player ? "yes" : "MISSING") +
                    ", inventory=" + (inventory ? inventory.SlotCount.ToString() : "MISSING") +
                    ", registry=" + (registry ? "yes" : "MISSING") +
                    ", combat controller=" + (combat ? "yes" : "MISSING") +
                    ", medical UI=" + (medical ? "yes" : "MISSING") +
                    ", player health=" + (playerHealth ? "yes" : "MISSING") +
                    ", terminal=" + (terminal ? "yes" : "MISSING") +
                    ", emergency dispatch=" + (dispatch ? "yes" : "MISSING") +
                    ", combat products=" + combatProducts + "/12" +
                    ", definitions=" + definitions + "/12" +
                    ", bad definitions=" + badDefinitions +
                    ", firearms=" + firearmCount + "/3" +
                    ", ammo=" + ammoCount + "/3" +
                    ", attachments=" + attachmentCount + "/4" +
                    ", grenades=" + grenadeCount + "/1" +
                    ", medical=" + medicalCount + "/1" +
                    ", NPCs missing anatomy=" + missingNpcHealth +
                    ", NPCs missing anatomical hitbox rigs=" + missingNpcHitboxes +
                    ", NPCs missing root hit capsules=" + missingNpcCapsules +
                    ".");
            }
        }

        static CombatAssets BuildAssets()
        {
            CombatAssets a = new CombatAssets();

            GameObject riflePrefab = LoadPrefab(RiflePrefab);
            GameObject pistolPrefab = LoadPrefab(PistolPrefab);
            GameObject precisionPrefab = LoadPrefab(PrecisionPrefab);
            GameObject ammo556Prefab = LoadPrefab(Ammo556Prefab);
            GameObject ammo9Prefab = LoadPrefab(Ammo9Prefab);
            GameObject ammo762Prefab = LoadPrefab(Ammo762Prefab);
            GameObject redDotPrefab = LoadPrefab(RedDotPrefab);
            GameObject scopePrefab = LoadPrefab(ScopePrefab);
            GameObject suppressorPrefab = LoadPrefab(SuppressorPrefab);
            GameObject gripPrefab = LoadPrefab(GripPrefab);
            GameObject grenadePrefab = LoadPrefab(GrenadePrefab);
            GameObject medicalPrefab = LoadPrefab(MedicalPrefab);

            a.rifle = Item(
                "ServiceRifle556",
                "h51-service-rifle-556",
                "5.56 Service Rifle",
                "Low-poly modular service rifle. Uses 5.56×45 ammunition and supports optics, muzzle devices, and under-barrel accessories.",
                1,
                true,
                riflePrefab,
                new Color(.16f,.17f,.17f),
                Vector3.one);

            a.pistol = Item(
                "ServicePistol9mm",
                "h51-service-pistol-9mm",
                "9mm Service Pistol",
                "Compact semiautomatic sidearm using 9×19 ammunition.",
                1,
                true,
                pistolPrefab,
                new Color(.14f,.14f,.14f),
                Vector3.one);

            a.precision = Item(
                "PrecisionRifle762",
                "h51-precision-rifle-762",
                "7.62 Precision Rifle",
                "Full-power precision rifle using 7.62×51 ammunition. Compatible with the modular optics system.",
                1,
                true,
                precisionPrefab,
                new Color(.13f,.13f,.13f),
                Vector3.one);

            a.ammo556 = Item(
                "Ammo556",
                "h51-ammo-556",
                "5.56×45 Ammunition",
                "Boxed 5.56×45 cartridges for the service rifle.",
                300,
                false,
                ammo556Prefab,
                new Color(.55f,.44f,.22f),
                Vector3.one);

            a.ammo9 = Item(
                "Ammo9mm",
                "h51-ammo-9mm",
                "9×19 Ammunition",
                "Boxed 9×19 cartridges for the service pistol.",
                300,
                false,
                ammo9Prefab,
                new Color(.55f,.44f,.22f),
                Vector3.one);

            a.ammo762 = Item(
                "Ammo762",
                "h51-ammo-762",
                "7.62×51 Ammunition",
                "Boxed 7.62×51 cartridges for the precision rifle.",
                240,
                false,
                ammo762Prefab,
                new Color(.55f,.44f,.22f),
                Vector3.one);

            a.redDot = Item(
                "RedDotOptic",
                "h51-optic-red-dot",
                "Compact Red-Dot Optic",
                "Close-range optic for faster aimed fire.",
                2,
                false,
                redDotPrefab,
                new Color(.20f,.20f,.20f),
                Vector3.one);

            a.scope = Item(
                "MagnifiedOptic",
                "h51-optic-magnified",
                "Magnified Rifle Optic",
                "Magnified optic with a narrower field of view for precision shooting.",
                2,
                false,
                scopePrefab,
                new Color(.18f,.18f,.18f),
                Vector3.one);

            a.suppressor = Item(
                "RifleSuppressor",
                "h51-muzzle-suppressor",
                "Rifle Suppressor",
                "Muzzle-mounted suppressor from the LowPolyWeapons attachment set.",
                2,
                false,
                suppressorPrefab,
                new Color(.15f,.15f,.15f),
                Vector3.one);

            a.grip = Item(
                "VerticalGrip",
                "h51-underbarrel-grip",
                "Vertical Foregrip",
                "Under-barrel foregrip that improves controllability.",
                2,
                false,
                gripPrefab,
                new Color(.16f,.16f,.16f),
                Vector3.one);

            a.grenade = Item(
                "FragmentationGrenade",
                "h51-frag-grenade",
                "Fragmentation Grenade",
                "Throwable fragmentation grenade for combat-system testing.",
                6,
                true,
                grenadePrefab,
                new Color(.25f,.32f,.20f),
                Vector3.one);

            a.traumaKit = Item(
                "TraumaKit",
                "h51-trauma-kit",
                "Trauma Medical Kit",
                "Field trauma supplies used through the anatomical treatment panel.",
                6,
                true,
                medicalPrefab,
                new Color(.72f,.16f,.14f),
                Vector3.one);

            H51CombatItemDefinition rifle = Definition("ServiceRifle556");
            rifle.ConfigureFirearm(
                a.rifle,
                H51Cartridge.FiveFiveSixByFortyFive,
                4.0f,
                900f,
                5.56f,
                .25f,
                1.0f,
                30,
                750f,
                true,
                48f,
                new Vector3(.18f,-.26f,.68f),
                new Vector3(0f,-90f,0f),
                1.28f,
                Vector3.zero,
                Vector3.zero,
                Vector3.zero);
            Dirty(rifle,a.definitions);

            H51CombatItemDefinition pistol = Definition("ServicePistol9mm");
            pistol.ConfigureFirearm(
                a.pistol,
                H51Cartridge.NineByNineteen,
                8.0f,
                360f,
                9f,
                .30f,
                1.05f,
                17,
                420f,
                false,
                51f,
                new Vector3(.16f,-.29f,.55f),
                new Vector3(0f,-90f,0f),
                1.05f,
                Vector3.zero,
                Vector3.zero,
                Vector3.zero);
            Dirty(pistol,a.definitions);

            H51CombatItemDefinition precision = Definition("PrecisionRifle762");
            precision.ConfigureFirearm(
                a.precision,
                H51Cartridge.SevenSixTwoByFiftyOne,
                9.5f,
                800f,
                7.62f,
                .23f,
                1.05f,
                10,
                90f,
                false,
                42f,
                new Vector3(.17f,-.24f,.72f),
                new Vector3(0f,-90f,0f),
                1.35f,
                Vector3.zero,
                Vector3.zero,
                Vector3.zero);
            Dirty(precision,a.definitions);

            H51CombatItemDefinition ammo556 = Definition("Ammo556");
            ammo556.ConfigureAmmo(a.ammo556,H51Cartridge.FiveFiveSixByFortyFive);
            Dirty(ammo556,a.definitions);

            H51CombatItemDefinition ammo9 = Definition("Ammo9mm");
            ammo9.ConfigureAmmo(a.ammo9,H51Cartridge.NineByNineteen);
            Dirty(ammo9,a.definitions);

            H51CombatItemDefinition ammo762 = Definition("Ammo762");
            ammo762.ConfigureAmmo(a.ammo762,H51Cartridge.SevenSixTwoByFiftyOne);
            Dirty(ammo762,a.definitions);

            H51CombatItemDefinition redDot = Definition("RedDotOptic");
            redDot.ConfigureAttachment(
                a.redDot,
                H51AttachmentSlot.Optic,
                redDotPrefab,
                40f,
                Vector3.zero,
                Vector3.one);
            Dirty(redDot,a.definitions);

            H51CombatItemDefinition scope = Definition("MagnifiedOptic");
            scope.ConfigureAttachment(
                a.scope,
                H51AttachmentSlot.Optic,
                scopePrefab,
                18f,
                Vector3.zero,
                Vector3.one);
            Dirty(scope,a.definitions);

            H51CombatItemDefinition suppressor = Definition("RifleSuppressor");
            suppressor.ConfigureAttachment(
                a.suppressor,
                H51AttachmentSlot.Muzzle,
                suppressorPrefab,
                45f,
                Vector3.zero,
                Vector3.one,
                .98f,
                .94f);
            Dirty(suppressor,a.definitions);

            H51CombatItemDefinition grip = Definition("VerticalGrip");
            grip.ConfigureAttachment(
                a.grip,
                H51AttachmentSlot.Underbarrel,
                gripPrefab,
                45f,
                Vector3.zero,
                Vector3.one,
                1f,
                .82f);
            Dirty(grip,a.definitions);

            H51CombatItemDefinition grenade = Definition("FragmentationGrenade");
            grenade.ConfigureGrenade(
                a.grenade,
                3.5f,
                7f,
                78f);
            Dirty(grenade,a.definitions);

            H51CombatItemDefinition medical = Definition("TraumaKit");
            medical.ConfigureMedicalKit(a.traumaKit);
            Dirty(medical,a.definitions);

            return a;
        }

        static void AddCombatCatalog(
            List<ShopCatalogEntry> catalog,
            CombatAssets a)
        {
            catalog.Add(Product(
                "combat-rifle-556",
                "Weapons",
                "5.56 Service Rifle",
                "Modular service rifle delivered in a shipment crate. Includes one loaded test magazine; additional reloads consume purchased 5.56 ammunition.",
                1450,
                a.rifle,
                1));

            catalog.Add(Product(
                "combat-pistol-9mm",
                "Weapons",
                "9mm Service Pistol",
                "Semiautomatic 9mm sidearm delivered through the normal Hanger 51 shipment system.",
                725,
                a.pistol,
                1));

            catalog.Add(Product(
                "combat-precision-762",
                "Weapons",
                "7.62 Precision Rifle",
                "Full-power precision rifle intended for testing long-range ballistics and magnified optics.",
                2250,
                a.precision,
                1));

            catalog.Add(Product(
                "combat-ammo-556",
                "Ammunition",
                "5.56×45 Ammo Case — 120 rounds",
                "Case of service-rifle ammunition.",
                84,
                a.ammo556,
                120));

            catalog.Add(Product(
                "combat-ammo-9mm",
                "Ammunition",
                "9×19 Ammo Case — 100 rounds",
                "Case of pistol ammunition.",
                48,
                a.ammo9,
                100));

            catalog.Add(Product(
                "combat-ammo-762",
                "Ammunition",
                "7.62×51 Ammo Case — 60 rounds",
                "Case of full-power rifle ammunition.",
                96,
                a.ammo762,
                60));

            catalog.Add(Product(
                "combat-optic-red-dot",
                "Weapon Accessories",
                "Compact Red-Dot Optic",
                "Install from the C-key weapon customization screen.",
                320,
                a.redDot,
                1));

            catalog.Add(Product(
                "combat-optic-magnified",
                "Weapon Accessories",
                "Magnified Rifle Optic",
                "High-magnification optic. Aiming uses a narrower FOV and scope overlay.",
                760,
                a.scope,
                1));

            catalog.Add(Product(
                "combat-suppressor",
                "Weapon Accessories",
                "Rifle Suppressor",
                "Muzzle attachment installed through weapon customization.",
                610,
                a.suppressor,
                1));

            catalog.Add(Product(
                "combat-grip",
                "Weapon Accessories",
                "Vertical Foregrip",
                "Under-barrel attachment installed through weapon customization.",
                145,
                a.grip,
                1));

            catalog.Add(Product(
                "combat-frag",
                "Tactical",
                "Fragmentation Grenade",
                "Throwable grenade using blast-distance anatomical trauma.",
                92,
                a.grenade,
                1));

            catalog.Add(Product(
                "combat-trauma-kit",
                "Medical",
                "Trauma Medical Kit",
                "Opens an injury-specific treatment panel for the aimed patient or the player.",
                135,
                a.traumaKit,
                1));
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
            ShopCatalogEntry entry = new ShopCatalogEntry();
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

        static InventoryItemDefinition Item(
            string assetName,
            string id,
            string display,
            string description,
            int maxStack,
            bool canEquip,
            GameObject prefab,
            Color color,
            Vector3 scale)
        {
            string path =
                InventoryFolder + "/" + assetName + ".asset";

            InventoryItemDefinition item =
                AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(path);

            if (!item)
            {
                item =
                    ScriptableObject.CreateInstance<InventoryItemDefinition>();
                item.name = assetName;
                AssetDatabase.CreateAsset(item,path);
            }

            item.Configure(
                id,
                display,
                description,
                maxStack,
                color,
                canEquip,
                prefab,
                scale);

            EditorUtility.SetDirty(item);
            return item;
        }

        static H51CombatItemDefinition Definition(string assetName)
        {
            string path =
                DefinitionFolder + "/" + assetName + ".asset";

            H51CombatItemDefinition definition =
                AssetDatabase.LoadAssetAtPath<H51CombatItemDefinition>(path);

            if (!definition)
            {
                definition =
                    ScriptableObject.CreateInstance<H51CombatItemDefinition>();
                definition.name = assetName;
                AssetDatabase.CreateAsset(definition,path);
            }

            return definition;
        }

        static void Dirty(
            H51CombatItemDefinition definition,
            List<H51CombatItemDefinition> list)
        {
            EditorUtility.SetDirty(definition);
            list.Add(definition);
        }

        static GameObject LoadPrefab(string path)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
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
