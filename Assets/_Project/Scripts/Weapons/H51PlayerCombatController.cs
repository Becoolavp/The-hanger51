using System.Collections.Generic;
using Hanger51.Aircraft;
using Hanger51.Commerce;
using Hanger51.Health;
using Hanger51.Inventory;
using Hanger51.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Weapons
{
    [DisallowMultipleComponent]
    public sealed class H51PlayerCombatController : MonoBehaviour
    {
        sealed class WeaponState
        {
            public bool initialized;
            public int magazine;
            public H51CombatItemDefinition optic;
            public H51CombatItemDefinition muzzle;
            public H51CombatItemDefinition underbarrel;
        }

        [SerializeField] Camera playerCamera;
        [SerializeField] PlayerInventory inventory;
        [SerializeField] InventoryUI inventoryUI;
        [SerializeField] EquippedItemView equippedView;
        [SerializeField] H51CombatItemRegistry registry;
        [SerializeField] H51MedicalTreatmentUI medicalUI;
        [SerializeField] FirstPersonController firstPersonController;
        [SerializeField] P51PilotPlayerInteractor pilotInteractor;
        [SerializeField] AudioSource weaponAudioSource;
        [SerializeField, Min(1f)] float medicalInteractionDistance = 4f;

        readonly Dictionary<string,WeaponState> states =
            new Dictionary<string,WeaponState>();

        H51CombatItemDefinition activeDefinition;
        WeaponState activeState;
        GameObject lastWeaponModel;
        Vector3 modelBaseScale = Vector3.one;
        float nextShotTime;
        float aimBlend;
        float baseCameraFov = 60f;
        float recoilVisual;
        float recoilSpread;
        bool customizationOpen;
        string statusMessage = string.Empty;
        float statusUntil;
        int presentationSignature;
        int shotSequenceCounter;
        bool wasAiming;
        Texture2D scopeMaskTexture;

        void Awake()
        {
            ResolveReferences();
            EnsureAudioSource();
            if (playerCamera)
                baseCameraFov = playerCamera.fieldOfView;
        }

        void OnDisable()
        {
            CloseCustomization();
            if (playerCamera)
                playerCamera.fieldOfView = baseCameraFov;
        }

        void OnDestroy()
        {
            if (scopeMaskTexture)
                Destroy(scopeMaskTexture);
        }

        void Update()
        {
            ResolveReferences();
            RefreshActiveDefinition();

            if (pilotInteractor && pilotInteractor.IsPiloting)
            {
                customizationOpen = false;
                wasAiming = false;
                aimBlend = 0f;

                if (equippedView && equippedView.DisplayedPrefabModel)
                    equippedView.DisplayedPrefabModel.SetActive(false);

                return;
            }

            if (statusUntil > 0f &&
                Time.unscaledTime >= statusUntil)
            {
                statusMessage = string.Empty;
                statusUntil = 0f;
            }

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (customizationOpen)
            {
                if (keyboard != null &&
                    (keyboard.cKey.wasPressedThisFrame ||
                     keyboard.escapeKey.wasPressedThisFrame))
                    CloseCustomization();

                UpdatePresentation(false);
                return;
            }

            if (medicalUI && medicalUI.IsOpen)
            {
                UpdatePresentation(false);
                return;
            }

            H51BodyInspectorUI bodyInspector =
                GetComponent<H51BodyInspectorUI>();

            bool blocked =
                (inventoryUI && inventoryUI.IsOpen) ||
                HangarShopUI.IsAnyShopOpen ||
                (bodyInspector && bodyInspector.Visible);

            if (blocked || !activeDefinition)
            {
                UpdatePresentation(false);
                return;
            }

            if (activeDefinition.Kind == H51CombatItemKind.Firearm)
            {
                bool aiming =
                    mouse != null &&
                    mouse.rightButton.isPressed;

                if (aiming && !wasAiming)
                    PlayRandom(
                        activeDefinition.AimClips,
                        .34f,
                        .97f,
                        1.03f);

                wasAiming = aiming;
                UpdatePresentation(aiming);

                if (keyboard != null &&
                    keyboard.cKey.wasPressedThisFrame)
                {
                    OpenCustomization();
                    return;
                }

                if (keyboard != null &&
                    keyboard.rKey.wasPressedThisFrame)
                    Reload();

                bool wantsShot =
                    mouse != null &&
                    (activeDefinition.Automatic
                        ? mouse.leftButton.isPressed
                        : mouse.leftButton.wasPressedThisFrame);

                if (wantsShot &&
                    Time.time >= nextShotTime)
                    Fire();
            }
            else
            {
                wasAiming = false;
                UpdatePresentation(false);

                if (activeDefinition.Kind == H51CombatItemKind.Grenade &&
                    mouse != null &&
                    mouse.leftButton.wasPressedThisFrame)
                {
                    ThrowGrenade();
                }
                else if (activeDefinition.Kind == H51CombatItemKind.MedicalKit &&
                         keyboard != null &&
                         keyboard.fKey.wasPressedThisFrame)
                {
                    OpenMedicalTreatment();
                }
            }
        }

        void ResolveReferences()
        {
            if (!playerCamera)
                playerCamera = GetComponentInChildren<Camera>();
            if (!inventory)
                inventory = GetComponent<PlayerInventory>();
            if (!inventoryUI)
                inventoryUI = FindFirstObjectByType<InventoryUI>();
            if (!equippedView)
                equippedView = GetComponentInChildren<EquippedItemView>(true);
            if (!registry)
                registry = GetComponent<H51CombatItemRegistry>();
            if (!medicalUI)
                medicalUI = GetComponent<H51MedicalTreatmentUI>();
            if (!firstPersonController)
                firstPersonController = GetComponent<FirstPersonController>();
            if (!pilotInteractor)
                pilotInteractor = GetComponent<P51PilotPlayerInteractor>();
            EnsureAudioSource();
        }

        void EnsureAudioSource()
        {
            if (weaponAudioSource)
                return;

            weaponAudioSource =
                GetComponent<AudioSource>();

            if (!weaponAudioSource)
                weaponAudioSource =
                    gameObject.AddComponent<AudioSource>();

            weaponAudioSource.playOnAwake = false;
            weaponAudioSource.spatialBlend = 0f;
            weaponAudioSource.dopplerLevel = 0f;
            weaponAudioSource.volume = 1f;
        }

        void PlayFireAudio()
        {
            if (!activeDefinition)
                return;

            bool suppressed =
                activeState != null &&
                activeState.muzzle != null;

            PlayRandom(
                activeDefinition.GunshotClips,
                suppressed ? .48f : .95f,
                suppressed ? .94f : .97f,
                suppressed ? 1.00f : 1.03f);

            float tailChance =
                activeDefinition.Automatic
                    ? .28f
                    : .82f;

            if (Random.value <= tailChance)
            {
                PlayRandom(
                    activeDefinition.TailClips,
                    suppressed ? .15f : .48f,
                    .96f,
                    1.03f);
            }

        }

        void EjectSpentCasing()
        {
            if (!activeDefinition ||
                !activeDefinition.CasingPrefab ||
                !playerCamera)
                return;

            GameObject casing =
                Instantiate(activeDefinition.CasingPrefab);

            casing.name =
                "Spent Casing - " +
                activeDefinition.name;

            // The LowPolyWeapons Bullet_* prefabs contain a case plus a
            // projectile child. A fired case keeps only the root case mesh.
            for (int i=casing.transform.childCount-1;i>=0;i--)
            {
                GameObject child = casing.transform.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            Collider[] originalColliders =
                casing.GetComponentsInChildren<Collider>(true);
            for (int i=0;i<originalColliders.Length;i++)
            {
                originalColliders[i].enabled = false;
                Destroy(originalColliders[i]);
            }

            Renderer[] renderers =
                casing.GetComponentsInChildren<Renderer>(true);

            float targetLength =
                activeDefinition.WeaponClass == H51WeaponClass.Handgun
                    ? .021f
                    : activeDefinition.WeaponClass == H51WeaponClass.Shotgun
                        ? .052f
                        : .044f;

            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i=1;i<renderers.Length;i++)
                    bounds.Encapsulate(renderers[i].bounds);

                float largest =
                    Mathf.Max(
                        bounds.size.x,
                        bounds.size.y,
                        bounds.size.z);

                if (largest > .0001f)
                    casing.transform.localScale *=
                        targetLength/largest;
            }

            casing.transform.position =
                playerCamera.transform.position +
                playerCamera.transform.forward*.42f +
                playerCamera.transform.right*.18f -
                playerCamera.transform.up*.10f;

            casing.transform.rotation =
                Random.rotation;

            CapsuleCollider capsule =
                casing.AddComponent<CapsuleCollider>();
            capsule.direction = 0;
            capsule.radius = targetLength*.17f;
            capsule.height = targetLength;

            Rigidbody body =
                casing.AddComponent<Rigidbody>();
            body.mass =
                activeDefinition.WeaponClass == H51WeaponClass.Shotgun
                    ? .011f
                    : activeDefinition.WeaponClass == H51WeaponClass.Handgun
                        ? .004f
                        : .006f;
            body.collisionDetectionMode =
                CollisionDetectionMode.ContinuousDynamic;
            body.interpolation =
                RigidbodyInterpolation.Interpolate;
            body.linearVelocity =
                playerCamera.transform.right*
                    Random.Range(1.5f,2.6f) +
                playerCamera.transform.up*
                    Random.Range(.65f,1.35f) +
                playerCamera.transform.forward*
                    Random.Range(-.25f,.35f);
            body.angularVelocity =
                Random.onUnitSphere*
                Random.Range(8f,20f);

            H51SpentCasing spent =
                casing.AddComponent<H51SpentCasing>();
            spent.Initialize(
                activeDefinition.ShellClips);
        }

        void PlayRandom(
            AudioClip[] clips,
            float volume,
            float pitchMin,
            float pitchMax)
        {
            if (clips == null ||
                clips.Length == 0)
                return;

            EnsureAudioSource();

            AudioClip clip =
                clips[
                    Random.Range(
                        0,
                        clips.Length)];

            if (!clip)
                return;

            weaponAudioSource.pitch =
                Random.Range(
                    pitchMin,
                    pitchMax);

            weaponAudioSource.PlayOneShot(
                clip,
                Mathf.Clamp01(volume));
        }

        void RefreshActiveDefinition()
        {
            InventoryItemDefinition equipped =
                inventory ? inventory.EquippedItem : null;

            H51CombatItemDefinition definition =
                registry ? registry.Find(equipped) : null;

            if (definition == activeDefinition)
                return;

            activeDefinition = definition;
            activeState = null;
            lastWeaponModel = null;
            presentationSignature = 0;

            if (activeDefinition &&
                activeDefinition.Kind == H51CombatItemKind.Firearm)
            {
                activeState = GetState(activeDefinition);
                PlayRandom(
                    activeDefinition.EquipClips,
                    .42f,
                    .96f,
                    1.04f);
            }

            if (!activeDefinition ||
                activeDefinition.Kind != H51CombatItemKind.Firearm)
            {
                wasAiming = false;
                aimBlend = 0f;
                if (playerCamera)
                    playerCamera.fieldOfView = baseCameraFov;
            }
        }

        WeaponState GetState(H51CombatItemDefinition firearm)
        {
            string key =
                firearm.InventoryItem
                    ? firearm.InventoryItem.ItemId
                    : firearm.name;

            if (!states.TryGetValue(key,out WeaponState state))
            {
                state = new WeaponState
                {
                    initialized = true,
                    // Test-friendly first purchase: the firearm arrives with
                    // one magazine loaded. Subsequent reloads consume inventory ammo.
                    magazine = firearm.MagazineCapacity
                };
                states.Add(key,state);
            }

            return state;
        }

        void Fire()
        {
            if (!activeDefinition ||
                activeState == null ||
                activeState.magazine <= 0 ||
                !playerCamera)
            {
                ShowStatus("Weapon is empty — press R to reload.");
                return;
            }

            activeState.magazine--;

            float interval =
                60f /
                Mathf.Max(30f,activeDefinition.RoundsPerMinute);
            nextShotTime =
                Time.time + interval;

            float recoilMultiplier = 1f;
            if (activeState.muzzle)
                recoilMultiplier *= activeState.muzzle.RecoilMultiplier;
            if (activeState.underbarrel)
                recoilMultiplier *= activeState.underbarrel.RecoilMultiplier;

            float baseSpreadDegrees =
                Mathf.Lerp(.32f,.055f,aimBlend) *
                (1f + recoilSpread) *
                recoilMultiplier;

            int projectileCount =
                Mathf.Max(
                    1,
                    activeDefinition.ProjectilesPerShot);

            int shotSequence =
                ++shotSequenceCounter;

            float velocityMultiplier =
                activeState.muzzle
                    ? activeState.muzzle.MuzzleVelocityMultiplier
                    : 1f;

            for (int i=0;i<projectileCount;i++)
            {
                float spreadDegrees =
                    baseSpreadDegrees +
                    activeDefinition.ProjectileSpreadDegrees;

                Vector2 random =
                    Random.insideUnitCircle *
                    Mathf.Tan(
                        spreadDegrees *
                        Mathf.Deg2Rad);

                Vector3 direction =
                    (playerCamera.transform.forward +
                     playerCamera.transform.right*random.x +
                     playerCamera.transform.up*random.y)
                    .normalized;

                GameObject projectileObject =
                    new GameObject("H51 Ballistic Projectile");

                H51BallisticProjectile projectile =
                    projectileObject.AddComponent<H51BallisticProjectile>();

                projectile.Initialize(
                    activeDefinition,
                    gameObject,
                    playerCamera.transform.position +
                    direction*.45f,
                    direction,
                    velocityMultiplier,
                    shotSequence);
            }

            PlayFireAudio();
            EjectSpentCasing();

            recoilVisual =
                Mathf.Clamp(
                    recoilVisual +
                    .055f *
                    recoilMultiplier *
                    (activeDefinition.WeaponClass == H51WeaponClass.Shotgun
                        ? 1.8f
                        : 1f),
                    0f,
                    .16f);

            recoilSpread =
                Mathf.Clamp(
                    recoilSpread +
                    .18f *
                    recoilMultiplier *
                    (activeDefinition.WeaponClass == H51WeaponClass.Shotgun
                        ? 1.55f
                        : 1f),
                    0f,
                    .95f);
        }

        void Reload()
        {
            if (!activeDefinition ||
                activeState == null ||
                activeDefinition.Kind != H51CombatItemKind.Firearm ||
                !inventory ||
                !registry)
                return;

            int needed =
                activeDefinition.MagazineCapacity -
                activeState.magazine;

            if (needed <= 0)
            {
                ShowStatus("Magazine is already full.");
                return;
            }

            H51CombatItemDefinition ammo =
                registry.FindAmmo(activeDefinition.Cartridge);

            if (!ammo || !ammo.InventoryItem)
            {
                ShowStatus("No compatible ammunition definition is configured.");
                return;
            }

            int available =
                inventory.CountItem(ammo.InventoryItem);

            int requested =
                Mathf.Min(needed,available);

            if (requested <= 0)
            {
                ShowStatus(
                    "No " +
                    ammo.InventoryItem.DisplayName +
                    " in inventory.");
                return;
            }

            int removed =
                inventory.RemoveItemQuantity(
                    ammo.InventoryItem,
                    requested);

            activeState.magazine += removed;

            PlayRandom(
                activeDefinition.ReloadClips,
                .72f,
                .97f,
                1.03f);

            ShowStatus(
                "Reloaded " +
                removed +
                " round" +
                (removed == 1 ? string.Empty : "s") +
                ".");
        }

        void ThrowGrenade()
        {
            if (!activeDefinition ||
                activeDefinition.Kind != H51CombatItemKind.Grenade ||
                !inventory ||
                !playerCamera)
                return;

            if (inventory.CountItem(activeDefinition.InventoryItem) <= 0)
                return;

            GameObject root =
                new GameObject("Thrown " + activeDefinition.InventoryItem.DisplayName);
            root.transform.position =
                playerCamera.transform.position +
                playerCamera.transform.forward*.65f -
                playerCamera.transform.up*.12f;

            SphereCollider collider =
                root.AddComponent<SphereCollider>();
            collider.radius = .08f;

            Rigidbody rb =
                root.AddComponent<Rigidbody>();
            rb.mass = .42f;
            rb.collisionDetectionMode =
                CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation =
                RigidbodyInterpolation.Interpolate;
            rb.linearVelocity =
                playerCamera.transform.forward*14f +
                playerCamera.transform.up*2.0f;

            GameObject prefab =
                activeDefinition.InventoryItem.WorldPrefab;
            if (prefab)
            {
                GameObject visual =
                    Instantiate(prefab,root.transform);
                visual.name = "Grenade Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;

                Collider[] childColliders =
                    visual.GetComponentsInChildren<Collider>(true);
                for (int i=0;i<childColliders.Length;i++)
                    childColliders[i].enabled = false;

                InventoryPickup[] pickups =
                    visual.GetComponentsInChildren<InventoryPickup>(true);
                for (int i=0;i<pickups.Length;i++)
                    pickups[i].enabled = false;

                Renderer[] renderers =
                    visual.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    for (int i=1;i<renderers.Length;i++)
                        bounds.Encapsulate(renderers[i].bounds);

                    float largest =
                        Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
                    if (largest > .001f)
                        visual.transform.localScale =
                            Vector3.one * (.18f/largest);
                }
            }

            H51GrenadeProjectile grenade =
                root.AddComponent<H51GrenadeProjectile>();
            grenade.Initialize(activeDefinition,gameObject);

            inventory.RemoveItemQuantity(
                activeDefinition.InventoryItem,
                1);

            ShowStatus("Grenade thrown.");
        }

        void OpenMedicalTreatment()
        {
            if (!activeDefinition ||
                activeDefinition.Kind != H51CombatItemKind.MedicalKit ||
                !medicalUI ||
                !playerCamera)
                return;

            H51AnatomicalHealth patient = null;

            RaycastHit[] hits =
                Physics.RaycastAll(
                    playerCamera.transform.position,
                    playerCamera.transform.forward,
                    medicalInteractionDistance,
                    ~0,
                    QueryTriggerInteraction.Collide);

            System.Array.Sort(
                hits,
                (a,b) => a.distance.CompareTo(b.distance));

            for (int i=0;i<hits.Length;i++)
            {
                H51AnatomicalHealth candidate =
                    hits[i].collider
                        ? hits[i].collider.GetComponentInParent<H51AnatomicalHealth>()
                        : null;

                if (candidate && candidate.gameObject != gameObject)
                {
                    patient = candidate;
                    break;
                }
            }

            if (!patient)
                patient = GetComponent<H51AnatomicalHealth>();

            if (!medicalUI.TryOpen(
                    patient,
                    activeDefinition.InventoryItem,
                    out string reason))
                ShowStatus(reason);
        }

        void OpenCustomization()
        {
            if (!activeDefinition ||
                activeDefinition.Kind != H51CombatItemKind.Firearm)
                return;

            customizationOpen = true;
            firstPersonController?.SetExternalInputBlocked(true);
        }

        void CloseCustomization()
        {
            if (!customizationOpen)
                return;

            customizationOpen = false;
            firstPersonController?.SetExternalInputBlocked(false);
        }

        void UpdatePresentation(bool aiming)
        {
            if (!playerCamera)
                return;

            recoilVisual =
                Mathf.MoveTowards(
                    recoilVisual,
                    0f,
                    Time.unscaledDeltaTime*.36f);
            recoilSpread =
                Mathf.MoveTowards(
                    recoilSpread,
                    0f,
                    Time.unscaledDeltaTime*1.55f);

            float targetAim =
                aiming &&
                activeDefinition &&
                activeDefinition.Kind == H51CombatItemKind.Firearm
                    ? 1f
                    : 0f;

            aimBlend =
                Mathf.MoveTowards(
                    aimBlend,
                    targetAim,
                    Time.unscaledDeltaTime*5.4f);

            float smoothAim =
                aimBlend * aimBlend * (3f - 2f*aimBlend);

            float targetSightFov =
                activeDefinition &&
                activeDefinition.Kind == H51CombatItemKind.Firearm &&
                activeState != null &&
                activeState.optic
                    ? activeState.optic.OpticFov
                    : activeDefinition && activeDefinition.Kind == H51CombatItemKind.Firearm
                        ? activeDefinition.IronSightFov
                        : baseCameraFov;

            // Drive FOV from the exact same blend as the weapon pose. This
            // keeps the optic and zoom locked together during the transition.
            playerCamera.fieldOfView =
                Mathf.Lerp(
                    baseCameraFov,
                    targetSightFov,
                    smoothAim);

            if (!equippedView ||
                !activeDefinition ||
                activeDefinition.Kind != H51CombatItemKind.Firearm)
                return;

            GameObject model =
                equippedView.DisplayedPrefabModel;

            if (!model)
                return;

            if (model != lastWeaponModel)
            {
                lastWeaponModel = model;
                modelBaseScale = model.transform.localScale;
                presentationSignature = 0;
            }

            model.transform.localRotation =
                Quaternion.Euler(activeDefinition.ViewLocalEuler);

            model.transform.localScale =
                modelBaseScale *
                activeDefinition.ViewScaleMultiplier;

            int signature =
                AttachmentSignature(activeState);

            if (signature != presentationSignature ||
                !AttachmentVisualsIntact(model,activeState))
            {
                RebuildAttachmentVisuals(model);
                presentationSignature = signature;
            }

            Vector3 hip =
                activeDefinition.ViewLocalPosition;

            // The first pass placed long guns too far from the camera.
            // Clamp the authored depth so the weapon reads as shoulder-held
            // rather than floating several feet in front of the player.
            hip.z =
                Mathf.Min(
                    hip.z,
                    activeDefinition.Cartridge == H51Cartridge.NineByNineteen
                        ? .34f
                        : .42f);

            Vector3 ads =
                ResolveAdsPosition(
                    model,
                    activeState,
                    hip);

            model.transform.localPosition =
                Vector3.Lerp(
                    hip,
                    ads,
                    smoothAim) +
                Vector3.back*recoilVisual +
                Vector3.up*recoilVisual*.18f;

            UpdateMagnifiedScopeVisibility(
                model,
                smoothAim);
        }

        int AttachmentSignature(WeaponState state)
        {
            if (state == null)
                return 0;

            unchecked
            {
                int hash = 17;
                hash = hash*31 + (state.optic ? state.optic.GetInstanceID() : 0);
                hash = hash*31 + (state.muzzle ? state.muzzle.GetInstanceID() : 0);
                hash = hash*31 + (state.underbarrel ? state.underbarrel.GetInstanceID() : 0);
                return hash;
            }
        }

        void RebuildAttachmentVisuals(GameObject model)
        {
            if (!model || activeState == null || !activeDefinition)
                return;

            Transform old =
                model.transform.Find("H51 Runtime Attachments");
            if (old)
                Destroy(old.gameObject);

            GameObject root =
                new GameObject("H51 Runtime Attachments");
            root.transform.SetParent(model.transform,false);

            SpawnAttachment(
                model,
                root.transform,
                activeState.optic,
                H51AttachmentSlot.Optic,
                activeDefinition.OpticMount);

            SpawnAttachment(
                model,
                root.transform,
                activeState.muzzle,
                H51AttachmentSlot.Muzzle,
                activeDefinition.MuzzleMount);

            SpawnAttachment(
                model,
                root.transform,
                activeState.underbarrel,
                H51AttachmentSlot.Underbarrel,
                activeDefinition.UnderbarrelMount);
        }

        void SpawnAttachment(
            GameObject weaponModel,
            Transform parent,
            H51CombatItemDefinition attachment,
            H51AttachmentSlot slot,
            Vector3 mountOffset)
        {
            if (!attachment || !attachment.AttachmentPrefab)
                return;

            GameObject visual =
                Instantiate(
                    attachment.AttachmentPrefab,
                    parent);

            visual.name =
                "H51 " +
                slot +
                " - " +
                (attachment.InventoryItem
                    ? attachment.InventoryItem.DisplayName
                    : attachment.name);

            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation =
                Quaternion.Euler(attachment.AttachmentEuler);
            visual.transform.localScale =
                attachment.AttachmentScale;

            PlaceAttachmentByBounds(
                weaponModel,
                visual,
                slot,
                ResolveAttachmentMount(
                    weaponModel,
                    slot) +
                mountOffset);

            Collider[] colliders =
                visual.GetComponentsInChildren<Collider>(true);
            for (int i=0;i<colliders.Length;i++)
                colliders[i].enabled = false;

            InventoryPickup[] pickups =
                visual.GetComponentsInChildren<InventoryPickup>(true);
            for (int i=0;i<pickups.Length;i++)
                pickups[i].enabled = false;
        }

        static void PlaceAttachmentByBounds(
            GameObject weaponModel,
            GameObject attachmentVisual,
            H51AttachmentSlot slot,
            Vector3 targetMount)
        {
            if (!weaponModel || !attachmentVisual)
                return;

            Renderer[] renderers =
                attachmentVisual.GetComponentsInChildren<Renderer>(true);

            if (renderers.Length == 0)
            {
                attachmentVisual.transform.localPosition = targetMount;
                return;
            }

            Transform frame = weaponModel.transform;
            bool haveBounds = false;
            Bounds bounds = new Bounds(Vector3.zero,Vector3.zero);

            for (int i=0;i<renderers.Length;i++)
            {
                Bounds world = renderers[i].bounds;
                Vector3 min = world.min;
                Vector3 max = world.max;
                Vector3[] corners =
                {
                    new Vector3(min.x,min.y,min.z),
                    new Vector3(min.x,min.y,max.z),
                    new Vector3(min.x,max.y,min.z),
                    new Vector3(min.x,max.y,max.z),
                    new Vector3(max.x,min.y,min.z),
                    new Vector3(max.x,min.y,max.z),
                    new Vector3(max.x,max.y,min.z),
                    new Vector3(max.x,max.y,max.z)
                };

                for (int j=0;j<corners.Length;j++)
                {
                    Vector3 local = frame.InverseTransformPoint(corners[j]);
                    if (!haveBounds)
                    {
                        bounds = new Bounds(local,Vector3.zero);
                        haveBounds = true;
                    }
                    else
                        bounds.Encapsulate(local);
                }
            }

            if (!haveBounds)
            {
                attachmentVisual.transform.localPosition = targetMount;
                return;
            }

            Vector3 attachmentPoint;

            switch (slot)
            {
                case H51AttachmentSlot.Optic:
                    attachmentPoint =
                        new Vector3(
                            bounds.center.x,
                            bounds.min.y,
                            bounds.center.z);
                    break;

                case H51AttachmentSlot.Muzzle:
                    // Barrel points toward -X in the corrected weapon view;
                    // align the rear/+X end of the muzzle device to the barrel.
                    attachmentPoint =
                        new Vector3(
                            bounds.max.x,
                            bounds.center.y,
                            bounds.center.z);
                    break;

                case H51AttachmentSlot.Underbarrel:
                    attachmentPoint =
                        new Vector3(
                            bounds.center.x,
                            bounds.max.y,
                            bounds.center.z);
                    break;

                default:
                    attachmentPoint = bounds.center;
                    break;
            }

            attachmentVisual.transform.localPosition +=
                targetMount -
                attachmentPoint;
        }

        static Vector3 ResolveAttachmentMount(
            GameObject model,
            H51AttachmentSlot slot)
        {
            if (!model)
                return Vector3.zero;

            Renderer[] renderers =
                model.GetComponentsInChildren<Renderer>(true);

            bool haveBounds = false;
            Bounds localBounds = new Bounds(Vector3.zero,Vector3.zero);

            Transform attachmentRoot =
                model.transform.Find("H51 Runtime Attachments");

            for (int i=0;i<renderers.Length;i++)
            {
                Renderer renderer = renderers[i];
                if (!renderer ||
                    (attachmentRoot &&
                     renderer.transform.IsChildOf(attachmentRoot)))
                    continue;

                Bounds world = renderer.bounds;
                Vector3 min = world.min;
                Vector3 max = world.max;

                Vector3[] corners =
                {
                    new Vector3(min.x,min.y,min.z),
                    new Vector3(min.x,min.y,max.z),
                    new Vector3(min.x,max.y,min.z),
                    new Vector3(min.x,max.y,max.z),
                    new Vector3(max.x,min.y,min.z),
                    new Vector3(max.x,min.y,max.z),
                    new Vector3(max.x,max.y,min.z),
                    new Vector3(max.x,max.y,max.z)
                };

                for (int c=0;c<corners.Length;c++)
                {
                    Vector3 local =
                        model.transform.InverseTransformPoint(corners[c]);

                    if (!haveBounds)
                    {
                        localBounds = new Bounds(local,Vector3.zero);
                        haveBounds = true;
                    }
                    else
                        localBounds.Encapsulate(local);
                }
            }

            if (!haveBounds)
                return Vector3.zero;

            // LowPolyWeapons models are authored side-on with the barrel along
            // local X. Use the live bounds so long rifles and short carbines
            // receive sensible mount positions without per-model hand tuning.
            switch (slot)
            {
                case H51AttachmentSlot.Optic:
                    return new Vector3(
                        localBounds.center.x - localBounds.extents.x*.06f,
                        localBounds.max.y + localBounds.size.y*.03f,
                        localBounds.center.z);

                case H51AttachmentSlot.Muzzle:
                    // The LowPolyWeapons firearm bodies present their muzzle
                    // on the negative local-X end in our corrected first-person
                    // orientation. Using min.x keeps suppressors on the barrel
                    // rather than the stock.
                    return new Vector3(
                        localBounds.min.x,
                        localBounds.center.y,
                        localBounds.center.z);

                case H51AttachmentSlot.Underbarrel:
                    return new Vector3(
                        localBounds.center.x - localBounds.extents.x*.14f,
                        localBounds.min.y - localBounds.size.y*.02f,
                        localBounds.center.z);

                default:
                    return localBounds.center;
            }
        }

        bool AttachmentVisualsIntact(
            GameObject model,
            WeaponState state)
        {
            if (!model || state == null)
                return false;

            Transform root =
                model.transform.Find("H51 Runtime Attachments");

            int expected =
                (state.optic ? 1 : 0) +
                (state.muzzle ? 1 : 0) +
                (state.underbarrel ? 1 : 0);

            if (!root)
                return expected == 0;

            int actual = 0;
            for (int i=0;i<root.childCount;i++)
            {
                Transform child = root.GetChild(i);
                if (child && child.gameObject.activeInHierarchy)
                    actual++;
            }

            return actual == expected;
        }

        Vector3 ResolveAdsPosition(
            GameObject model,
            WeaponState state,
            Vector3 fallbackHip)
        {
            if (!model || model.transform.parent == null)
                return new Vector3(0f,-.18f,.28f);

            Vector3 sightLocal;

            if (!TryGetSightReferenceLocal(
                    model,
                    state,
                    out sightLocal))
            {
                sightLocal =
                    EstimateIronSightLocal(model);
            }

            Quaternion localRotation =
                model.transform.localRotation;
            Vector3 localScale =
                model.transform.localScale;

            Vector3 transformedSight =
                localRotation *
                Vector3.Scale(
                    sightLocal,
                    localScale);

            // Put the optic/iron-sight center directly on the camera axis.
            // The slight negative Y keeps the center just below the crosshair
            // while transitioning, then settles nearly dead-center.
            Vector3 desiredSightInParent =
                new Vector3(
                    0f,
                    -.012f,
                    state != null &&
                    state.optic &&
                    state.optic.OpticFov <= 26f
                        ? .205f
                        : .245f);

            Vector3 solved =
                desiredSightInParent -
                transformedSight;

            // Guard against unusual model bounds producing a wild pose.
            solved.z =
                Mathf.Clamp(
                    solved.z,
                    .08f,
                    .40f);
            solved.x =
                Mathf.Clamp(
                    solved.x,
                    -.18f,
                    .18f);
            solved.y =
                Mathf.Clamp(
                    solved.y,
                    -.36f,
                    .10f);

            return solved;
        }

        bool TryGetSightReferenceLocal(
            GameObject model,
            WeaponState state,
            out Vector3 sightLocal)
        {
            sightLocal = Vector3.zero;

            if (!model ||
                state == null ||
                !state.optic)
                return false;

            Transform root =
                model.transform.Find("H51 Runtime Attachments");

            if (!root)
                return false;

            Transform opticTransform = null;

            for (int i=0;i<root.childCount;i++)
            {
                Transform child = root.GetChild(i);
                if (child &&
                    child.name.StartsWith(
                        "H51 Optic",
                        System.StringComparison.Ordinal))
                {
                    opticTransform = child;
                    break;
                }
            }

            if (!opticTransform)
                return false;

            Renderer[] renderers =
                opticTransform.GetComponentsInChildren<Renderer>(true);

            if (renderers.Length == 0)
            {
                sightLocal =
                    model.transform.InverseTransformPoint(
                        opticTransform.position);
                return true;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i=1;i<renderers.Length;i++)
                bounds.Encapsulate(renderers[i].bounds);

            sightLocal =
                model.transform.InverseTransformPoint(
                    bounds.center);

            return true;
        }

        static Vector3 EstimateIronSightLocal(
            GameObject model)
        {
            Renderer[] renderers =
                model.GetComponentsInChildren<Renderer>(true);

            Transform attachmentRoot =
                model.transform.Find("H51 Runtime Attachments");

            bool haveBounds = false;
            Bounds localBounds =
                new Bounds(Vector3.zero,Vector3.zero);

            for (int i=0;i<renderers.Length;i++)
            {
                Renderer renderer = renderers[i];

                if (!renderer ||
                    (attachmentRoot &&
                     renderer.transform.IsChildOf(attachmentRoot)))
                    continue;

                Bounds world = renderer.bounds;
                Vector3 min = world.min;
                Vector3 max = world.max;

                Vector3[] corners =
                {
                    new Vector3(min.x,min.y,min.z),
                    new Vector3(min.x,min.y,max.z),
                    new Vector3(min.x,max.y,min.z),
                    new Vector3(min.x,max.y,max.z),
                    new Vector3(max.x,min.y,min.z),
                    new Vector3(max.x,min.y,max.z),
                    new Vector3(max.x,max.y,min.z),
                    new Vector3(max.x,max.y,max.z)
                };

                for (int c=0;c<corners.Length;c++)
                {
                    Vector3 local =
                        model.transform.InverseTransformPoint(
                            corners[c]);

                    if (!haveBounds)
                    {
                        localBounds =
                            new Bounds(local,Vector3.zero);
                        haveBounds = true;
                    }
                    else
                        localBounds.Encapsulate(local);
                }
            }

            if (!haveBounds)
                return Vector3.zero;

            // Muzzle is negative local X after the corrected presentation,
            // so the rear sight sits slightly toward +X on top of the receiver.
            return new Vector3(
                localBounds.center.x +
                localBounds.extents.x*.18f,
                localBounds.max.y -
                localBounds.size.y*.035f,
                localBounds.center.z);
        }

        void UpdateMagnifiedScopeVisibility(
            GameObject model,
            float smoothAim)
        {
            if (!model)
                return;

            bool magnified =
                activeState != null &&
                activeState.optic &&
                activeState.optic.OpticFov <= 26f;

            bool hideForScope =
                magnified &&
                smoothAim >= .965f;

            Renderer[] renderers =
                model.GetComponentsInChildren<Renderer>(true);

            for (int i=0;i<renderers.Length;i++)
            {
                if (renderers[i])
                    renderers[i].enabled = !hideForScope;
            }
        }

        void SetAttachment(
            H51AttachmentSlot slot,
            H51CombatItemDefinition selected)
        {
            if (activeState == null || !inventory)
                return;

            H51CombatItemDefinition current =
                slot == H51AttachmentSlot.Optic
                    ? activeState.optic
                    : slot == H51AttachmentSlot.Muzzle
                        ? activeState.muzzle
                        : activeState.underbarrel;

            if (current == selected)
            {
                if (current && current.InventoryItem)
                    inventory.AddItem(current.InventoryItem,1);
                AssignAttachment(slot,null);
                presentationSignature = 0;
                return;
            }

            if (selected)
            {
                if (!selected.InventoryItem ||
                    inventory.RemoveItemQuantity(selected.InventoryItem,1) != 1)
                {
                    ShowStatus("That attachment is not in inventory.");
                    return;
                }
            }

            if (current && current.InventoryItem)
                inventory.AddItem(current.InventoryItem,1);

            AssignAttachment(slot,selected);
            presentationSignature = 0;
        }

        void AssignAttachment(
            H51AttachmentSlot slot,
            H51CombatItemDefinition selected)
        {
            if (slot == H51AttachmentSlot.Optic)
                activeState.optic = selected;
            else if (slot == H51AttachmentSlot.Muzzle)
                activeState.muzzle = selected;
            else if (slot == H51AttachmentSlot.Underbarrel)
                activeState.underbarrel = selected;
        }

        void OnGUI()
        {
            if ((pilotInteractor && pilotInteractor.IsPiloting) ||
                !activeDefinition)
                return;

            if (activeDefinition.Kind == H51CombatItemKind.Firearm &&
                activeState != null)
            {
                H51CombatItemDefinition ammo =
                    registry
                        ? registry.FindAmmo(activeDefinition.Cartridge)
                        : null;

                int reserve =
                    ammo && ammo.InventoryItem && inventory
                        ? inventory.CountItem(ammo.InventoryItem)
                        : 0;

                GUIStyle hud =
                    new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 15,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleRight
                    };
                hud.normal.textColor = Color.white;

                GUI.Label(
                    new Rect(
                        Screen.width-390f,
                        Screen.height-92f,
                        360f,
                        58f),
                    activeDefinition.InventoryItem.DisplayName +
                    "\n" +
                    activeState.magazine + " / " + reserve +
                    "    R RELOAD   C CUSTOMIZE",
                    hud);

                if (aimBlend > .94f)
                    DrawAimOverlay();
            }
            else if (activeDefinition.Kind == H51CombatItemKind.Grenade)
            {
                DrawBottomHint("LMB — throw grenade");
            }
            else if (activeDefinition.Kind == H51CombatItemKind.MedicalKit)
            {
                DrawBottomHint("F — treat aimed patient / self");
            }

            if (!string.IsNullOrWhiteSpace(statusMessage))
            {
                GUIStyle status =
                    new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 14,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter
                    };
                status.normal.textColor = Color.white;

                GUI.Label(
                    new Rect(
                        Screen.width*.5f-260f,
                        Screen.height-145f,
                        520f,
                        32f),
                    statusMessage,
                    status);
            }

            if (customizationOpen)
                DrawCustomization();
        }

        void DrawAimOverlay()
        {
            float opticFov =
                activeState != null && activeState.optic
                    ? activeState.optic.OpticFov
                    : activeDefinition.IronSightFov;

            if (opticFov <= 26f)
            {
                float opening =
                    Mathf.Min(Screen.width,Screen.height)*.68f;
                float left =
                    (Screen.width-opening)*.5f;
                float top =
                    (Screen.height-opening)*.5f;

                Color old = GUI.color;
                GUI.color = Color.black;
                GUI.DrawTexture(
                    new Rect(0,0,Screen.width,top),
                    Texture2D.whiteTexture);
                GUI.DrawTexture(
                    new Rect(0,top+opening,Screen.width,Screen.height-(top+opening)),
                    Texture2D.whiteTexture);
                GUI.DrawTexture(
                    new Rect(0,top,left,opening),
                    Texture2D.whiteTexture);
                GUI.DrawTexture(
                    new Rect(left+opening,top,Screen.width-(left+opening),opening),
                    Texture2D.whiteTexture);
                GUI.color = Color.white;

                EnsureScopeMask();
                if (scopeMaskTexture)
                {
                    GUI.DrawTexture(
                        new Rect(left,top,opening,opening),
                        scopeMaskTexture,
                        ScaleMode.StretchToFill,
                        true);
                }

                GUI.color = old;
            }

            Color previous = GUI.color;
            GUI.color = new Color(.85f,.90f,.88f,.92f);
            GUI.DrawTexture(
                new Rect(Screen.width*.5f-.7f,Screen.height*.5f-34f,1.4f,68f),
                Texture2D.whiteTexture);
            GUI.DrawTexture(
                new Rect(Screen.width*.5f-34f,Screen.height*.5f-.7f,68f,1.4f),
                Texture2D.whiteTexture);
            GUI.color = previous;
        }

        void EnsureScopeMask()
        {
            if (scopeMaskTexture)
                return;

            const int size = 256;
            scopeMaskTexture =
                new Texture2D(
                    size,
                    size,
                    TextureFormat.RGBA32,
                    false,
                    true)
                {
                    name = "H51 Runtime Scope Mask",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };

            Color32[] pixels =
                new Color32[size*size];

            float center =
                (size-1)*.5f;
            float outer =
                size*.495f;
            float ring =
                size*.455f;

            for (int y=0;y<size;y++)
            for (int x=0;x<size;x++)
            {
                float dx = x-center;
                float dy = y-center;
                float distance =
                    Mathf.Sqrt(dx*dx+dy*dy);

                byte alpha =
                    distance >= outer
                        ? (byte)255
                        : distance >= ring
                            ? (byte)Mathf.RoundToInt(
                                Mathf.InverseLerp(
                                    ring,
                                    outer,
                                    distance)*255f)
                            : (byte)0;

                pixels[y*size+x] =
                    new Color32(0,0,0,alpha);
            }

            scopeMaskTexture.SetPixels32(pixels);
            scopeMaskTexture.Apply(false,true);
        }

        void DrawCustomization()
        {
            float width = Mathf.Min(760f,Screen.width-60f);
            float height = Mathf.Min(620f,Screen.height-60f);
            Rect panel = new Rect(
                (Screen.width-width)*.5f,
                (Screen.height-height)*.5f,
                width,
                height);

            Color old = GUI.color;
            GUI.color = new Color(.02f,.025f,.03f,.97f);
            GUI.DrawTexture(panel,Texture2D.whiteTexture);
            GUI.color = old;

            GUIStyle title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold
            };
            title.normal.textColor = Color.white;

            GUIStyle label = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14
            };
            label.normal.textColor = Color.white;

            GUIStyle button = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };

            float x = panel.x+28f;
            float y = panel.y+24f;
            float w = panel.width-56f;

            GUI.Label(
                new Rect(x,y,w,30f),
                "WEAPON CUSTOMIZATION — " +
                activeDefinition.InventoryItem.DisplayName,
                title);
            y += 42f;

            GUI.Label(
                new Rect(x,y,w,26f),
                "Installed attachments are removed from inventory and returned when uninstalled/swapped.",
                label);
            y += 38f;

            DrawAttachmentSlot(
                ref y,
                x,
                w,
                H51AttachmentSlot.Optic,
                "OPTIC",
                activeState.optic,
                button,
                label);

            DrawAttachmentSlot(
                ref y,
                x,
                w,
                H51AttachmentSlot.Muzzle,
                "MUZZLE",
                activeState.muzzle,
                button,
                label);

            DrawAttachmentSlot(
                ref y,
                x,
                w,
                H51AttachmentSlot.Underbarrel,
                "UNDERBARREL",
                activeState.underbarrel,
                button,
                label);

            GUI.Label(
                new Rect(x,panel.yMax-38f,w,25f),
                "C / ESC — close customization",
                label);
        }

        void DrawAttachmentSlot(
            ref float y,
            float x,
            float width,
            H51AttachmentSlot slot,
            string heading,
            H51CombatItemDefinition installed,
            GUIStyle button,
            GUIStyle label)
        {
            GUI.Label(
                new Rect(x,y,width,24f),
                heading + " — installed: " +
                (installed && installed.InventoryItem
                    ? installed.InventoryItem.DisplayName
                    : "None"),
                label);
            y += 28f;

            if (installed)
            {
                if (GUI.Button(
                        new Rect(x,y,180f,34f),
                        "REMOVE",
                        button))
                    SetAttachment(slot,installed);

                y += 42f;
            }

            float bx = x;
            int rowCount = 0;

            for (int i=0;i<registry.Definitions.Count;i++)
            {
                H51CombatItemDefinition candidate =
                    registry.Definitions[i];

                if (!candidate ||
                    candidate.AttachmentSlot != slot ||
                    candidate == installed ||
                    !candidate.InventoryItem)
                    continue;

                int owned =
                    inventory
                        ? inventory.CountItem(candidate.InventoryItem)
                        : 0;

                if (owned <= 0)
                    continue;

                if (rowCount > 0 && rowCount%3 == 0)
                {
                    y += 40f;
                    bx = x;
                }

                if (GUI.Button(
                        new Rect(bx,y,Mathf.Min(205f,width/3f-8f),34f),
                        candidate.InventoryItem.DisplayName +
                        " (" + owned + ")",
                        button))
                    SetAttachment(slot,candidate);

                bx += width/3f;
                rowCount++;
            }

            if (rowCount == 0)
            {
                GUI.Label(
                    new Rect(x,y,width,28f),
                    "No compatible owned attachments in inventory.",
                    label);
            }

            y += 54f;
        }

        void DrawBottomHint(string text)
        {
            GUIStyle style =
                new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleRight
                };
            style.normal.textColor = Color.white;

            GUI.Label(
                new Rect(
                    Screen.width-390f,
                    Screen.height-70f,
                    360f,
                    34f),
                text,
                style);
        }

        void ShowStatus(string message)
        {
            statusMessage = message ?? string.Empty;
            statusUntil = Time.unscaledTime+2.5f;
        }
    }
}
