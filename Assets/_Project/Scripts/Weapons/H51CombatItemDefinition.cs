using Hanger51.Inventory;
using UnityEngine;

namespace Hanger51.Weapons
{
    public enum H51CombatItemKind
    {
        Firearm,
        Ammunition,
        Optic,
        MuzzleAttachment,
        UnderbarrelAttachment,
        Grenade,
        MedicalKit
    }

    public enum H51Cartridge
    {
        None,
        NineByNineteen,
        FiveFiveSixByFortyFive,
        SevenSixTwoByFiftyOne,
        TwelveGauge
    }

    public enum H51WeaponClass
    {
        Unknown,
        Handgun,
        AssaultRifle,
        SniperRifle,
        Smg,
        MachineGun,
        Shotgun,
        HeavyWeapon
    }

    public enum H51AttachmentSlot
    {
        None,
        Optic,
        Muzzle,
        Underbarrel
    }

    [CreateAssetMenu(
        fileName = "CombatItem",
        menuName = "Hanger 51/Weapons/Combat Item Definition")]
    public sealed class H51CombatItemDefinition : ScriptableObject
    {
        [Header("Inventory")]
        [SerializeField] InventoryItemDefinition inventoryItem;
        [SerializeField] H51CombatItemKind kind;

        [Header("Firearm")]
        [SerializeField] H51WeaponClass weaponClass;
        [SerializeField] H51Cartridge cartridge;
        [SerializeField, Min(.1f)] float projectileMassGrams = 4f;
        [SerializeField, Min(10f)] float muzzleVelocityMps = 850f;
        [SerializeField, Min(.1f)] float projectileDiameterMm = 5.56f;
        [SerializeField, Range(.05f,1f)] float ballisticDrag = .26f;
        [SerializeField, Range(.1f,2f)] float damageScale = 1f;
        [SerializeField, Min(1)] int magazineCapacity = 30;
        [SerializeField, Min(30f)] float roundsPerMinute = 700f;
        [SerializeField] bool automatic;
        [SerializeField, Range(10f,75f)] float ironSightFov = 48f;
        [SerializeField] Vector3 viewLocalPosition = new Vector3(.20f,-.24f,.62f);
        [SerializeField] Vector3 viewLocalEuler = new Vector3(2f,180f,0f);
        [SerializeField, Range(.25f,3f)] float viewScaleMultiplier = 1f;
        [SerializeField, Min(1)] int projectilesPerShot = 1;
        [SerializeField, Range(0f,12f)] float projectileSpreadDegrees = .08f;

        [Header("Audio")]
        [SerializeField] AudioClip[] gunshotClips = System.Array.Empty<AudioClip>();
        [SerializeField] AudioClip[] tailClips = System.Array.Empty<AudioClip>();
        [SerializeField] AudioClip[] reloadClips = System.Array.Empty<AudioClip>();
        [SerializeField] AudioClip[] aimClips = System.Array.Empty<AudioClip>();
        [SerializeField] AudioClip[] equipClips = System.Array.Empty<AudioClip>();
        [SerializeField] AudioClip[] shellClips = System.Array.Empty<AudioClip>();

        [Header("Attachment mounts")]
        [SerializeField] Vector3 opticMount = new Vector3(0f,.04f,0f);
        [SerializeField] Vector3 muzzleMount = new Vector3(0f,0f,.45f);
        [SerializeField] Vector3 underbarrelMount = new Vector3(0f,-.06f,.08f);

        [Header("Attachment")]
        [SerializeField] H51AttachmentSlot attachmentSlot;
        [SerializeField] GameObject attachmentPrefab;
        [SerializeField] Vector3 attachmentEuler;
        [SerializeField] Vector3 attachmentScale = Vector3.one;
        [SerializeField, Range(8f,70f)] float opticFov = 40f;
        [SerializeField, Range(.5f,1.1f)] float muzzleVelocityMultiplier = 1f;
        [SerializeField, Range(.5f,1.2f)] float recoilMultiplier = 1f;

        [Header("Grenade")]
        [SerializeField, Min(.2f)] float grenadeFuseSeconds = 3.5f;
        [SerializeField, Min(.5f)] float grenadeBlastRadius = 7f;
        [SerializeField, Min(1f)] float grenadeTrauma = 75f;

        public InventoryItemDefinition InventoryItem => inventoryItem;
        public H51CombatItemKind Kind => kind;
        public H51WeaponClass WeaponClass => weaponClass;
        public H51Cartridge Cartridge => cartridge;
        public float ProjectileMassGrams => projectileMassGrams;
        public float MuzzleVelocityMps => muzzleVelocityMps;
        public float ProjectileDiameterMm => projectileDiameterMm;
        public float BallisticDrag => ballisticDrag;
        public float DamageScale => damageScale;
        public int MagazineCapacity => magazineCapacity;
        public float RoundsPerMinute => roundsPerMinute;
        public bool Automatic => automatic;
        public float IronSightFov => ironSightFov;
        public Vector3 ViewLocalPosition => viewLocalPosition;
        public Vector3 ViewLocalEuler => viewLocalEuler;
        public float ViewScaleMultiplier => viewScaleMultiplier;
        public int ProjectilesPerShot => Mathf.Max(1,projectilesPerShot);
        public float ProjectileSpreadDegrees => Mathf.Max(0f,projectileSpreadDegrees);
        public AudioClip[] GunshotClips => gunshotClips;
        public AudioClip[] TailClips => tailClips;
        public AudioClip[] ReloadClips => reloadClips;
        public AudioClip[] AimClips => aimClips;
        public AudioClip[] EquipClips => equipClips;
        public AudioClip[] ShellClips => shellClips;
        public Vector3 OpticMount => opticMount;
        public Vector3 MuzzleMount => muzzleMount;
        public Vector3 UnderbarrelMount => underbarrelMount;
        public H51AttachmentSlot AttachmentSlot => attachmentSlot;
        public GameObject AttachmentPrefab => attachmentPrefab;
        public Vector3 AttachmentEuler => attachmentEuler;
        public Vector3 AttachmentScale => attachmentScale;
        public float OpticFov => opticFov;
        public float MuzzleVelocityMultiplier => muzzleVelocityMultiplier;
        public float RecoilMultiplier => recoilMultiplier;
        public float GrenadeFuseSeconds => grenadeFuseSeconds;
        public float GrenadeBlastRadius => grenadeBlastRadius;
        public float GrenadeTrauma => grenadeTrauma;

        public void ConfigureFirearm(
            InventoryItemDefinition item,
            H51Cartridge configuredCartridge,
            float massGrams,
            float velocityMps,
            float diameterMm,
            float drag,
            float configuredDamageScale,
            int capacity,
            float rpm,
            bool isAutomatic,
            float adsFov,
            Vector3 viewPosition,
            Vector3 viewEuler,
            float viewScale,
            Vector3 opticPosition,
            Vector3 muzzlePosition,
            Vector3 underbarrelPosition)
        {
            inventoryItem = item;
            kind = H51CombatItemKind.Firearm;
            cartridge = configuredCartridge;
            projectileMassGrams = Mathf.Max(.1f,massGrams);
            muzzleVelocityMps = Mathf.Max(10f,velocityMps);
            projectileDiameterMm = Mathf.Max(.1f,diameterMm);
            ballisticDrag = Mathf.Clamp(drag,.05f,1f);
            damageScale = Mathf.Clamp(configuredDamageScale,.1f,2f);
            magazineCapacity = Mathf.Max(1,capacity);
            roundsPerMinute = Mathf.Max(30f,rpm);
            automatic = isAutomatic;
            ironSightFov = Mathf.Clamp(adsFov,10f,75f);
            viewLocalPosition = viewPosition;
            viewLocalEuler = viewEuler;
            viewScaleMultiplier = Mathf.Clamp(viewScale,.25f,3f);
            opticMount = opticPosition;
            muzzleMount = muzzlePosition;
            underbarrelMount = underbarrelPosition;
        }

        public void SetViewPresentation(
            Vector3 position,
            Vector3 euler,
            float scaleMultiplier)
        {
            viewLocalPosition = position;
            viewLocalEuler = euler;
            viewScaleMultiplier = Mathf.Clamp(scaleMultiplier,.25f,3f);
        }

        public void ConfigureWeaponClass(
            H51WeaponClass configuredClass,
            int configuredProjectilesPerShot = 1,
            float configuredSpreadDegrees = .08f)
        {
            weaponClass = configuredClass;
            projectilesPerShot = Mathf.Max(1,configuredProjectilesPerShot);
            projectileSpreadDegrees = Mathf.Clamp(configuredSpreadDegrees,0f,12f);
        }

        public void ConfigureAudio(
            AudioClip[] configuredGunshots,
            AudioClip[] configuredTails,
            AudioClip[] configuredReloads,
            AudioClip[] configuredAim,
            AudioClip[] configuredEquip,
            AudioClip[] configuredShells)
        {
            gunshotClips = configuredGunshots ?? System.Array.Empty<AudioClip>();
            tailClips = configuredTails ?? System.Array.Empty<AudioClip>();
            reloadClips = configuredReloads ?? System.Array.Empty<AudioClip>();
            aimClips = configuredAim ?? System.Array.Empty<AudioClip>();
            equipClips = configuredEquip ?? System.Array.Empty<AudioClip>();
            shellClips = configuredShells ?? System.Array.Empty<AudioClip>();
        }

        public void ConfigureAmmo(
            InventoryItemDefinition item,
            H51Cartridge configuredCartridge)
        {
            inventoryItem = item;
            kind = H51CombatItemKind.Ammunition;
            cartridge = configuredCartridge;
        }

        public void ConfigureAttachment(
            InventoryItemDefinition item,
            H51AttachmentSlot slot,
            GameObject prefab,
            float configuredOpticFov,
            Vector3 euler,
            Vector3 scale,
            float velocityMultiplier = 1f,
            float configuredRecoilMultiplier = 1f)
        {
            inventoryItem = item;
            attachmentSlot = slot;
            attachmentPrefab = prefab;
            opticFov = Mathf.Clamp(configuredOpticFov,8f,70f);
            attachmentEuler = euler;
            attachmentScale = scale;
            muzzleVelocityMultiplier = Mathf.Clamp(velocityMultiplier,.5f,1.1f);
            recoilMultiplier = Mathf.Clamp(configuredRecoilMultiplier,.5f,1.2f);
            kind = slot == H51AttachmentSlot.Optic
                ? H51CombatItemKind.Optic
                : slot == H51AttachmentSlot.Muzzle
                    ? H51CombatItemKind.MuzzleAttachment
                    : H51CombatItemKind.UnderbarrelAttachment;
        }

        public void ConfigureGrenade(
            InventoryItemDefinition item,
            float fuse,
            float radius,
            float trauma)
        {
            inventoryItem = item;
            kind = H51CombatItemKind.Grenade;
            grenadeFuseSeconds = Mathf.Max(.2f,fuse);
            grenadeBlastRadius = Mathf.Max(.5f,radius);
            grenadeTrauma = Mathf.Max(1f,trauma);
        }

        public void ConfigureMedicalKit(InventoryItemDefinition item)
        {
            inventoryItem = item;
            kind = H51CombatItemKind.MedicalKit;
        }
    }
}
