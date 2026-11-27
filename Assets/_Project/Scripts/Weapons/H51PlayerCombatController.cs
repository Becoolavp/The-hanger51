using System.Collections.Generic;
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
        bool customizationOpen;
        string statusMessage = string.Empty;
        float statusUntil;
        int presentationSignature;

        void Awake()
        {
            ResolveReferences();
            if (playerCamera)
                baseCameraFov = playerCamera.fieldOfView;
        }

        void OnDisable()
        {
            CloseCustomization();
            if (playerCamera)
                playerCamera.fieldOfView = baseCameraFov;
        }

        void Update()
        {
            ResolveReferences();
            RefreshActiveDefinition();

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
                activeState = GetState(activeDefinition);

            if (!activeDefinition ||
                activeDefinition.Kind != H51CombatItemKind.Firearm)
            {
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
                !activeState ||
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

            float spreadDegrees =
                Mathf.Lerp(.32f,.055f,aimBlend);

            Vector2 random =
                Random.insideUnitCircle *
                Mathf.Tan(spreadDegrees*Mathf.Deg2Rad);

            Vector3 direction =
                (playerCamera.transform.forward +
                 playerCamera.transform.right*random.x +
                 playerCamera.transform.up*random.y)
                .normalized;

            GameObject projectileObject =
                new GameObject("H51 Ballistic Projectile");
            H51BallisticProjectile projectile =
                projectileObject.AddComponent<H51BallisticProjectile>();

            float velocityMultiplier =
                activeState.muzzle
                    ? activeState.muzzle.MuzzleVelocityMultiplier
                    : 1f;

            projectile.Initialize(
                activeDefinition,
                gameObject,
                playerCamera.transform.position +
                direction*.45f,
                direction,
                velocityMultiplier);

            presentationSignature = 0;
        }

        void Reload()
        {
            if (!activeDefinition ||
                !activeState ||
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
                    Time.unscaledDeltaTime*7f);

            float desiredFov =
                baseCameraFov;

            if (activeDefinition &&
                activeDefinition.Kind == H51CombatItemKind.Firearm)
            {
                desiredFov =
                    activeState != null &&
                    activeState.optic
                        ? activeState.optic.OpticFov
                        : activeDefinition.IronSightFov;

                desiredFov =
                    Mathf.Lerp(
                        baseCameraFov,
                        desiredFov,
                        aimBlend);
            }

            playerCamera.fieldOfView =
                Mathf.Lerp(
                    playerCamera.fieldOfView,
                    desiredFov,
                    1f-Mathf.Exp(-Time.unscaledDeltaTime*10f));

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

            Vector3 hip =
                activeDefinition.ViewLocalPosition;
            Vector3 ads =
                new Vector3(
                    0f,
                    -.18f,
                    Mathf.Max(.32f,hip.z-.12f));

            model.transform.localPosition =
                Vector3.Lerp(
                    hip,
                    ads,
                    aimBlend);

            model.transform.localRotation =
                Quaternion.Euler(activeDefinition.ViewLocalEuler);

            model.transform.localScale =
                modelBaseScale *
                activeDefinition.ViewScaleMultiplier;

            int signature =
                AttachmentSignature(activeState);

            if (signature != presentationSignature)
            {
                presentationSignature = signature;
                RebuildAttachmentVisuals(model);
            }
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
                root.transform,
                activeState.optic,
                activeDefinition.OpticMount);

            SpawnAttachment(
                root.transform,
                activeState.muzzle,
                activeDefinition.MuzzleMount);

            SpawnAttachment(
                root.transform,
                activeState.underbarrel,
                activeDefinition.UnderbarrelMount);
        }

        void SpawnAttachment(
            Transform parent,
            H51CombatItemDefinition attachment,
            Vector3 mount)
        {
            if (!attachment || !attachment.AttachmentPrefab)
                return;

            GameObject visual =
                Instantiate(
                    attachment.AttachmentPrefab,
                    parent);

            visual.name =
                attachment.InventoryItem
                    ? attachment.InventoryItem.DisplayName
                    : attachment.name;

            visual.transform.localPosition = mount;
            visual.transform.localRotation =
                Quaternion.Euler(attachment.AttachmentEuler);
            visual.transform.localScale =
                attachment.AttachmentScale;

            Collider[] colliders =
                visual.GetComponentsInChildren<Collider>(true);
            for (int i=0;i<colliders.Length;i++)
                colliders[i].enabled = false;

            InventoryPickup[] pickups =
                visual.GetComponentsInChildren<InventoryPickup>(true);
            for (int i=0;i<pickups.Length;i++)
                pickups[i].enabled = false;
        }

        void SetAttachment(
            H51AttachmentSlot slot,
            H51CombatItemDefinition selected)
        {
            if (!activeState || !inventory)
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
            if (!activeDefinition)
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

                if (aimBlend > .72f)
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
                    Mathf.Min(Screen.width,Screen.height)*.62f;
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
