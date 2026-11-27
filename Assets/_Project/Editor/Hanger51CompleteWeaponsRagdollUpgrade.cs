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
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class Hanger51CompleteWeaponsRagdollUpgrade
    {
        const string ItemFolder = "Assets/_Project/Inventory/Combat/PackComplete";
        const string DefinitionFolder = "Assets/_Project/Weapons/Definitions/PackComplete";
        const string PackRoot = "Assets/LowPolyWeapons/Prefabs";
        const string SoundRoot = "Assets/RealisticGunSounds_v1.2";

        [MenuItem("Hanger 51/World/Current/176 - Complete Weapon Pack Ragdoll Casings And Medical Interaction")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("Step 176 failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            FirstPersonController player =
                UnityEngine.Object.FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            H51CombatItemRegistry registry =
                player ? player.GetComponent<H51CombatItemRegistry>() : null;
            HangarShopTerminal terminal =
                UnityEngine.Object.FindFirstObjectByType<HangarShopTerminal>(FindObjectsInactive.Include);

            if (!player || !registry || !terminal)
            {
                Debug.LogError("Step 176 failed. The combat registry or procurement terminal is missing. Run the earlier combat/store setup first.");
                return;
            }

            EnsureFolder(ItemFolder);
            EnsureFolder(DefinitionFolder);

            List<H51CombatItemDefinition> definitions =
                registry.Definitions.Where(d => d).Distinct().ToList();

            // If the full firearm scan has not yet been applied, run the prior
            // migration first so Step 176 remains safe on older scenes.
            int firearmCount =
                definitions.Count(d => d && d.Kind == H51CombatItemKind.Firearm);
            if (firearmCount < 198)
            {
                Hanger51CombatAudioBloodUpgrade.Apply();
                definitions =
                    registry.Definitions.Where(d => d).Distinct().ToList();
            }

            Dictionary<GameObject,H51CombatItemDefinition> byPrefab =
                new Dictionary<GameObject,H51CombatItemDefinition>();
            for (int i=0;i<definitions.Count;i++)
            {
                H51CombatItemDefinition d = definitions[i];
                GameObject prefab = d && d.InventoryItem ? d.InventoryItem.WorldPrefab : null;
                if (prefab && !byPrefab.ContainsKey(prefab))
                    byPrefab.Add(prefab,d);
            }

            List<ShopCatalogEntry> catalog =
                terminal.Catalog
                    .Where(e => e != null &&
                        !e.ProductId.StartsWith("combat-support-",StringComparison.Ordinal) &&
                        !e.ProductId.StartsWith("combat-pack-",StringComparison.Ordinal))
                    .ToList();

            GameObject pistolCase = LoadPrefab(PackRoot + "/Loot/Bullet_01.prefab");
            GameObject rifleCase = LoadPrefab(PackRoot + "/Loot/Bullet_02.prefab");
            GameObject sniperCase = LoadPrefab(PackRoot + "/Loot/Bullet_04.prefab");
            GameObject shotgunCase = LoadPrefab(PackRoot + "/Loot/Bullet_07.prefab");

            AudioClip[] brass = LoadAudio(SoundRoot + "/AssaultRifle/Foley/ShellsBrass");
            AudioClip[] plastic = LoadAudio(SoundRoot + "/Shotgun/Foley/ShellsPlastic");

            int firearmUpdated = 0;
            for (int i=0;i<definitions.Count;i++)
            {
                H51CombatItemDefinition d = definitions[i];
                if (!d || d.Kind != H51CombatItemKind.Firearm)
                    continue;

                GameObject casing =
                    d.WeaponClass == H51WeaponClass.Handgun ? pistolCase :
                    d.WeaponClass == H51WeaponClass.SniperRifle ? sniperCase :
                    d.WeaponClass == H51WeaponClass.Shotgun ? shotgunCase :
                    rifleCase;

                d.ConfigureCasing(casing);
                d.ConfigureAudio(
                    d.GunshotClips,
                    d.TailClips,
                    d.ReloadClips,
                    d.AimClips,
                    d.EquipClips,
                    d.WeaponClass == H51WeaponClass.Shotgun && plastic.Length > 0
                        ? plastic
                        : brass);
                EditorUtility.SetDirty(d);
                firearmUpdated++;
            }

            int attachmentCount = 0;
            int grenadeCount = 0;
            int equipmentCount = 0;

            // Eight dedicated optics.
            List<GameObject> optics = FindPrefabs(PackRoot + "/Optic");
            for (int i=0;i<optics.Count;i++)
            {
                GameObject prefab = optics[i];
                InventoryItemDefinition item = EnsureItem(
                    "optic-" + prefab.name,
                    "Optic " + (i+1),
                    "LowPolyWeapons optic. Install from the firearm customization screen.",
                    prefab,
                    1,
                    true);

                H51CombatItemDefinition d =
                    definitions.FirstOrDefault(x =>
                        x &&
                        x.Kind == H51CombatItemKind.Optic &&
                        x.InventoryItem &&
                        x.InventoryItem.WorldPrefab == prefab)
                    ?? EnsureDefinition("Optic_" + prefab.name);
                float fov = Mathf.Lerp(52f,18f,optics.Count <= 1 ? 0f : i/(float)(optics.Count-1));
                d.ConfigureAttachment(
                    item,
                    H51AttachmentSlot.Optic,
                    prefab,
                    fov,
                    Vector3.zero,
                    Vector3.one);
                EditorUtility.SetDirty(d);
                AddDefinition(definitions,d);
                AddProduct(catalog,"combat-pack-optic-"+Slug(prefab.name),"Weapons / Attachments",item,Mathf.RoundToInt(250f+(52f-fov)*17f));
                attachmentCount++;
            }

            // Suppressors, lights, lasers and modular rail accessories.
            List<GameObject> attachments = FindPrefabs(PackRoot + "/Attachments");
            for (int i=0;i<attachments.Count;i++)
            {
                GameObject prefab = attachments[i];
                bool muzzle = prefab.name.StartsWith("Muffler_",StringComparison.OrdinalIgnoreCase);
                InventoryItemDefinition item = EnsureItem(
                    "attachment-" + prefab.name,
                    Friendly(prefab.name),
                    muzzle
                        ? "LowPolyWeapons muzzle device/suppressor. Install from firearm customization."
                        : "LowPolyWeapons rail accessory. Install from firearm customization.",
                    prefab,
                    1,
                    true);

                H51AttachmentSlot slot =
                    muzzle ? H51AttachmentSlot.Muzzle : H51AttachmentSlot.Underbarrel;

                H51CombatItemDefinition d =
                    definitions.FirstOrDefault(x =>
                        x &&
                        x.InventoryItem &&
                        x.InventoryItem.WorldPrefab == prefab &&
                        x.AttachmentSlot == slot)
                    ?? EnsureDefinition("Attachment_" + prefab.name);
                d.ConfigureAttachment(
                    item,
                    slot,
                    prefab,
                    45f,
                    Vector3.zero,
                    Vector3.one,
                    muzzle ? .94f : 1f,
                    muzzle ? .90f : .94f);
                EditorUtility.SetDirty(d);
                AddDefinition(definitions,d);
                AddProduct(catalog,"combat-pack-attachment-"+Slug(prefab.name),"Weapons / Attachments",item,muzzle ? 420 : 240);
                attachmentCount++;
            }

            // All actual hand-grenade model variants are usable throwables.
            List<GameObject> explosives = FindPrefabs(PackRoot + "/Grenade");
            for (int i=0;i<explosives.Count;i++)
            {
                GameObject prefab = explosives[i];
                if (prefab.name.StartsWith("Particles_",StringComparison.OrdinalIgnoreCase))
                    continue;

                bool throwable =
                    prefab.name.StartsWith("Grenade_",StringComparison.OrdinalIgnoreCase);

                InventoryItemDefinition item = EnsureItem(
                    "explosive-" + prefab.name,
                    Friendly(prefab.name),
                    throwable
                        ? "Throwable grenade from the LowPolyWeapons pack."
                        : "LowPolyWeapons explosive item. Store/equip support is active; dedicated placement/ignition mechanics are a later pass.",
                    prefab,
                    throwable ? 6 : 3,
                    true);

                if (throwable)
                {
                    H51CombatItemDefinition d =
                        definitions.FirstOrDefault(x =>
                            x &&
                            x.Kind == H51CombatItemKind.Grenade &&
                            x.InventoryItem &&
                            x.InventoryItem.WorldPrefab == prefab)
                        ?? EnsureDefinition("Grenade_" + prefab.name);
                    d.ConfigureGrenade(item,3.4f,6.5f,78f);
                    EditorUtility.SetDirty(d);
                    AddDefinition(definitions,d);
                    grenadeCount++;
                }

                AddProduct(catalog,"combat-pack-explosive-"+Slug(prefab.name),"Weapons / Explosives",item,
                    prefab.name.StartsWith("C4",StringComparison.OrdinalIgnoreCase) ? 180 :
                    prefab.name.StartsWith("Mine",StringComparison.OrdinalIgnoreCase) ? 140 :
                    prefab.name.StartsWith("Molotov",StringComparison.OrdinalIgnoreCase) ? 35 : 85);
                equipmentCount++;
            }

            AddGenericFolder(catalog,PackRoot+"/HandWeapon","Weapons / Melee","melee",65,true,ref equipmentCount);
            AddGenericFolder(catalog,PackRoot+"/HeavyWeapon","Weapons / Heavy Weapons","heavy",2750,true,ref equipmentCount);

            // The remaining loot props are all represented in the store. Health
            // packs are also valid trauma kits; ammunition props remain visible
            // supply products while the caliber ammo definitions drive reloading.
            List<GameObject> loot = FindPrefabs(PackRoot + "/Loot");
            for (int i=0;i<loot.Count;i++)
            {
                GameObject prefab = loot[i];
                bool medical = prefab.name.StartsWith("Healthpack",StringComparison.OrdinalIgnoreCase);
                bool ammo =
                    prefab.name.StartsWith("Amo",StringComparison.OrdinalIgnoreCase) ||
                    prefab.name.StartsWith("BigAmo",StringComparison.OrdinalIgnoreCase) ||
                    prefab.name.StartsWith("Bullet_",StringComparison.OrdinalIgnoreCase);

                InventoryItemDefinition item = EnsureItem(
                    "loot-" + prefab.name,
                    Friendly(prefab.name),
                    medical
                        ? "Medical supply from the LowPolyWeapons pack. Can be opened for trauma treatment."
                        : ammo
                            ? "Ammunition/supply prop from the LowPolyWeapons pack."
                            : "Equipment/supply item from the LowPolyWeapons pack.",
                    prefab,
                    medical ? 5 : 20,
                    true);

                if (medical)
                {
                    H51CombatItemDefinition d = EnsureDefinition("Medical_" + prefab.name);
                    d.ConfigureMedicalKit(
                        item,
                        H51MedicalKitRole.Medical);
                    EditorUtility.SetDirty(d);
                    AddDefinition(definitions,d);
                }

                string category = medical
                    ? "Weapons / Medical"
                    : ammo
                        ? "Weapons / Ammunition"
                        : "Weapons / Equipment";
                AddProduct(catalog,"combat-pack-loot-"+Slug(prefab.name),category,item,medical ? 125 : ammo ? 45 : 80);
                equipmentCount++;
            }

            // Preserve all caliber ammo and any pre-existing medical products that
            // do not correspond to a pack prefab.
            for (int i=0;i<definitions.Count;i++)
            {
                H51CombatItemDefinition d = definitions[i];
                if (!d || !d.InventoryItem)
                    continue;

                if (d.Kind == H51CombatItemKind.Ammunition)
                {
                    AddProductIfMissing(catalog,"combat-pack-ammo-"+Slug(d.InventoryItem.ItemId),"Weapons / Ammunition",
                        d.InventoryItem,d.Cartridge == H51Cartridge.SevenSixTwoByFiftyOne ? 96 : 70,
                        d.Cartridge == H51Cartridge.SevenSixTwoByFiftyOne ? 60 : 100);
                }
                else if (d.Kind == H51CombatItemKind.MedicalKit)
                {
                    AddProductIfMissing(catalog,"combat-pack-med-"+Slug(d.InventoryItem.ItemId),"Weapons / Medical",
                        d.InventoryItem,135,1);
                }
            }

            registry.Configure(definitions.Where(d => d).Distinct().OrderBy(d => d.name).ToList());
            terminal.ReplaceCatalog(catalog);

            H51CitizenIdentity[] citizens =
                UnityEngine.Object.FindObjectsByType<H51CitizenIdentity>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            int ragdolls = 0;
            for (int i=0;i<citizens.Length;i++)
            {
                H51CitizenIdentity citizen = citizens[i];
                if (!citizen || !citizen.gameObject.scene.IsValid())
                    continue;

                if (!citizen.GetComponent<H51CitizenRagdoll>())
                    Undo.AddComponent<H51CitizenRagdoll>(citizen.gameObject);

                ragdolls++;
                EditorUtility.SetDirty(citizen.gameObject);
            }

            EditorUtility.SetDirty(registry);
            EditorUtility.SetDirty(terminal);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.SaveOpenScenes();
            Hanger51BuildTools.PrepareCurrentSceneForBuild(false);

            Debug.Log(
                "Step 176 complete. Firearms updated for physical casings=" + firearmUpdated +
                ", attachment definitions=" + attachmentCount +
                ", throwable grenade variants=" + grenadeCount +
                ", additional pack equipment products=" + equipmentCount +
                ", citizens with ragdoll component=" + ragdolls +
                ". Run Step 177.");
        }

        [MenuItem("Hanger 51/World/Current/177 - Validate Complete Weapon Pack Ragdoll Casings And Medical Interaction")]
        public static void Validate()
        {
            FirstPersonController player =
                UnityEngine.Object.FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            H51CombatItemRegistry registry = player ? player.GetComponent<H51CombatItemRegistry>() : null;
            HangarShopTerminal terminal =
                UnityEngine.Object.FindFirstObjectByType<HangarShopTerminal>(FindObjectsInactive.Include);

            int expectedFirearms =
                FindPrefabs(PackRoot+"/Pistols").Count +
                FindPrefabs(PackRoot+"/AssaultRifle").Count +
                FindPrefabs(PackRoot+"/SniperRifle").Count +
                FindPrefabs(PackRoot+"/SMG").Count +
                FindPrefabs(PackRoot+"/LMG").Count +
                FindPrefabs(PackRoot+"/Shotguns").Count;

            int expectedExtra =
                FindPrefabs(PackRoot+"/Optic").Count +
                FindPrefabs(PackRoot+"/Attachments").Count +
                FindPrefabs(PackRoot+"/Grenade").Count(p => !p.name.StartsWith("Particles_")) +
                FindPrefabs(PackRoot+"/HandWeapon").Count +
                FindPrefabs(PackRoot+"/HeavyWeapon").Count +
                FindPrefabs(PackRoot+"/Loot").Count;

            int firearms = 0;
            int firearmsMissingCasing = 0;
            int throwables = 0;
            int attachmentDefs = 0;

            if (registry)
            {
                foreach (H51CombatItemDefinition d in registry.Definitions)
                {
                    if (!d) continue;
                    if (d.Kind == H51CombatItemKind.Firearm)
                    {
                        firearms++;
                        if (!d.CasingPrefab || d.ShellClips == null || d.ShellClips.Length == 0)
                            firearmsMissingCasing++;
                    }
                    if (d.Kind == H51CombatItemKind.Grenade) throwables++;
                    if (d.Kind == H51CombatItemKind.Optic ||
                        d.Kind == H51CombatItemKind.MuzzleAttachment ||
                        d.Kind == H51CombatItemKind.UnderbarrelAttachment)
                        attachmentDefs++;
                }
            }

            int packProducts = terminal == null ? 0 :
                terminal.Catalog.Count(e => e != null &&
                    (e.ProductId.StartsWith("combat-auto-",StringComparison.Ordinal) ||
                     e.ProductId.StartsWith("combat-pack-",StringComparison.Ordinal)));

            H51CitizenIdentity[] citizens =
                UnityEngine.Object.FindObjectsByType<H51CitizenIdentity>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            int missingRagdoll =
                citizens.Count(c => c && !c.GetComponent<H51CitizenRagdoll>());

            H51CitizenPlayerInteractor interactor =
                player ? player.GetComponent<H51CitizenPlayerInteractor>() : null;
            H51MedicalTreatmentUI medical =
                player ? player.GetComponent<H51MedicalTreatmentUI>() : null;

            bool ok =
                player && registry && terminal &&
                firearms >= expectedFirearms &&
                firearmsMissingCasing == 0 &&
                throwables >= 6 &&
                attachmentDefs >= 27 &&
                packProducts >= expectedFirearms + expectedExtra &&
                missingRagdoll == 0 &&
                interactor && medical;

            if (ok)
            {
                Debug.Log(
                    "Step 177 passed. Full firearm roster=" + firearms +
                    ", casing/audio-ready firearms=" + (firearms-firearmsMissingCasing) +
                    ", throwable grenades=" + throwables +
                    ", attachment definitions=" + attachmentDefs +
                    ", pack/store products=" + packProducts +
                    ", ragdoll citizens=" + citizens.Length + "/" + citizens.Length +
                    ". Build and test.");
            }
            else
            {
                Debug.LogError(
                    "Step 177 failed. firearms=" + firearms + "/" + expectedFirearms +
                    ", missing casing/audio=" + firearmsMissingCasing +
                    ", throwable grenades=" + throwables +
                    ", attachments=" + attachmentDefs +
                    ", pack products=" + packProducts + "/" + (expectedFirearms+expectedExtra) +
                    ", missing ragdolls=" + missingRagdoll +
                    ", citizen interactor=" + (interactor ? "yes" : "MISSING") +
                    ", medical UI=" + (medical ? "yes" : "MISSING") + ".");
            }
        }

        static void AddGenericFolder(
            List<ShopCatalogEntry> catalog,
            string folder,
            string category,
            string prefix,
            int basePrice,
            bool canEquip,
            ref int count)
        {
            List<GameObject> prefabs = FindPrefabs(folder);
            for (int i=0;i<prefabs.Count;i++)
            {
                GameObject prefab = prefabs[i];
                InventoryItemDefinition item = EnsureItem(
                    prefix+"-"+prefab.name,
                    Friendly(prefab.name),
                    category.Contains("Heavy")
                        ? "Heavy weapon from the LowPolyWeapons pack. Store/equip support is active; dedicated rocket/heavy-projectile mechanics are a later combat pass."
                        : "Hand/melee weapon from the LowPolyWeapons pack. Store/equip support is active; dedicated melee strike mechanics are a later combat pass.",
                    prefab,
                    1,
                    canEquip);

                AddProduct(catalog,"combat-pack-"+prefix+"-"+Slug(prefab.name),category,item,basePrice+i*15);
                count++;
            }
        }

        static InventoryItemDefinition EnsureItem(
            string key,string display,string description,GameObject prefab,int stack,bool equip)
        {
            string path = ItemFolder + "/" + Slug(key) + ".asset";
            InventoryItemDefinition item =
                AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(path);
            if (!item)
            {
                item = ScriptableObject.CreateInstance<InventoryItemDefinition>();
                item.name = Slug(key);
                AssetDatabase.CreateAsset(item,path);
            }

            item.Configure(
                "h51-pack-"+Slug(key),
                display,
                description,
                Mathf.Max(1,stack),
                new Color(.18f,.18f,.18f,1f),
                equip,
                prefab,
                Vector3.one);
            EditorUtility.SetDirty(item);
            return item;
        }

        static H51CombatItemDefinition EnsureDefinition(string key)
        {
            string path = DefinitionFolder + "/" + Slug(key) + ".asset";
            H51CombatItemDefinition d =
                AssetDatabase.LoadAssetAtPath<H51CombatItemDefinition>(path);
            if (!d)
            {
                d = ScriptableObject.CreateInstance<H51CombatItemDefinition>();
                d.name = Slug(key);
                AssetDatabase.CreateAsset(d,path);
            }
            return d;
        }

        static void AddDefinition(List<H51CombatItemDefinition> list,H51CombatItemDefinition d)
        {
            if (d && !list.Contains(d))
                list.Add(d);
        }

        static void AddProduct(
            List<ShopCatalogEntry> catalog,string id,string category,
            InventoryItemDefinition item,int price,int quantity=1)
        {
            catalog.RemoveAll(e => e != null && e.ProductId == id);
            ShopCatalogEntry entry = new ShopCatalogEntry();
            entry.Configure(
                id,category,item.DisplayName,item.Description,Mathf.Max(0,price),
                ShopProductKind.InventoryItem,item,Mathf.Max(1,quantity),null);
            catalog.Add(entry);
        }

        static void AddProductIfMissing(
            List<ShopCatalogEntry> catalog,string id,string category,
            InventoryItemDefinition item,int price,int quantity)
        {
            if (catalog.Any(e => e != null && e.InventoryItem == item))
                return;
            AddProduct(catalog,id,category,item,price,quantity);
        }

        static List<GameObject> FindPrefabs(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
                return new List<GameObject>();

            string[] guids = AssetDatabase.FindAssets("t:GameObject",new[]{folder});
            List<GameObject> result = new List<GameObject>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase))
                    continue;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab) result.Add(prefab);
            }
            result.Sort((a,b) => string.Compare(a.name,b.name,StringComparison.OrdinalIgnoreCase));
            return result;
        }

        static AudioClip[] LoadAudio(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
                return Array.Empty<AudioClip>();
            return AssetDatabase.FindAssets("t:AudioClip",new[]{folder})
                .Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c)
                .OrderBy(c => c.name)
                .ToArray();
        }

        static GameObject LoadPrefab(string path) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(path);

        static string Friendly(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Equipment";
            string spaced = Regex.Replace(value.Replace("_"," "),@"(?<=[A-Za-z])(?=\d)"," ");
            return Regex.Replace(spaced,@"\s+"," ").Trim();
        }

        static string Slug(string value)
        {
            string slug = Regex.Replace((value ?? "").ToLowerInvariant(),@"[^a-z0-9]+","-").Trim('-');
            return string.IsNullOrEmpty(slug) ? "item" : slug;
        }

        static void EnsureFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || folder == "Assets" || AssetDatabase.IsValidFolder(folder))
                return;
            string parent = Path.GetDirectoryName(folder)?.Replace("\\","/");
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent,Path.GetFileName(folder));
        }
    }
}
