using Hanger51.Health;
using Hanger51.Inventory;
using Hanger51.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Weapons
{
    [DisallowMultipleComponent]
    public sealed class H51MedicalTreatmentUI : MonoBehaviour
    {
        H51AnatomicalHealth target;
        PlayerInventory inventory;
        FirstPersonController controller;
        H51BodyInspectorUI bodyInspector;
        H51CombatItemRegistry registry;
        InventoryItemDefinition treatmentKit;
        bool traumaKitOpened;
        bool medicalKitOpened;
        bool open;
        string status = string.Empty;

        public bool IsOpen => open;

        void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            controller = GetComponent<FirstPersonController>();
            bodyInspector = GetComponent<H51BodyInspectorUI>();
            registry = GetComponent<H51CombatItemRegistry>();
        }

        void Update()
        {
            if (!open)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                keyboard.escapeKey.wasPressedThisFrame)
                Close();
        }

        public bool TryOpen(
            H51AnatomicalHealth newTarget,
            InventoryItemDefinition medicalKit,
            out string reason)
        {
            treatmentKit = medicalKit;
            return OpenAssessment(newTarget,out reason);
        }

        public bool TryInspect(
            H51AnatomicalHealth newTarget,
            out string reason)
        {
            treatmentKit = null;
            return OpenAssessment(newTarget,out reason);
        }

        bool OpenAssessment(
            H51AnatomicalHealth newTarget,
            out string reason)
        {
            reason = string.Empty;

            if (!newTarget)
            {
                reason = "No patient is available.";
                return false;
            }

            if (!inventory)
                inventory = GetComponent<PlayerInventory>();
            if (!registry)
                registry = GetComponent<H51CombatItemRegistry>();

            target = newTarget;
            open = true;
            traumaKitOpened = false;
            medicalKitOpened = false;

            bool hasTrauma =
                FindAvailableTreatmentKit(
                    H51MedicalKitRole.Trauma) != null;
            bool hasMedical =
                FindAvailableTreatmentKit(
                    H51MedicalKitRole.Medical) != null;

            status = newTarget.IsDead
                ? "Assessment opened. The patient is deceased in the current simulation; injuries can still be inspected."
                : "Assessment opened. Trauma kit: " +
                  (hasTrauma ? "available" : "not available") +
                  " | Medical kit: " +
                  (hasMedical ? "available" : "not available") +
                  ". Trauma kits control major bleeding/chest trauma; medical kits provide airway and shock support.";

            if (!bodyInspector)
                bodyInspector = GetComponent<H51BodyInspectorUI>();
            if (!bodyInspector)
                bodyInspector = gameObject.AddComponent<H51BodyInspectorUI>();

            bodyInspector.OpenExternal(newTarget);

            return true;
        }

        InventoryItemDefinition FindAvailableTreatmentKit(
            H51MedicalKitRole role)
        {
            if (!registry)
                registry = GetComponent<H51CombatItemRegistry>();
            if (!inventory || !registry)
                return null;

            for (int i=0;i<registry.Definitions.Count;i++)
            {
                H51CombatItemDefinition definition =
                    registry.Definitions[i];

                if (definition &&
                    definition.Kind == H51CombatItemKind.MedicalKit &&
                    definition.MedicalKitRole == role &&
                    definition.InventoryItem &&
                    inventory.CountItem(definition.InventoryItem) > 0)
                {
                    return definition.InventoryItem;
                }
            }

            return null;
        }

        bool EnsureTreatmentKit(
            H51MedicalKitRole role)
        {
            if (!target || target.IsDead)
            {
                status = "No field treatment can be applied to a deceased patient.";
                return false;
            }

            if (role == H51MedicalKitRole.Trauma &&
                traumaKitOpened)
                return true;

            if (role == H51MedicalKitRole.Medical &&
                medicalKitOpened)
                return true;

            if (!inventory)
                inventory = GetComponent<PlayerInventory>();

            InventoryItemDefinition kit =
                FindAvailableTreatmentKit(role);

            if (!kit ||
                !inventory ||
                inventory.RemoveItemQuantity(kit,1) != 1)
            {
                status =
                    role == H51MedicalKitRole.Trauma
                        ? "A TRAUMA KIT is required for hemorrhage control, tourniquets, and chest-trauma treatment."
                        : "A MEDICAL KIT is required for airway/breathing and shock-support treatment.";
                return false;
            }

            if (role == H51MedicalKitRole.Trauma)
                traumaKitOpened = true;
            else
                medicalKitOpened = true;

            treatmentKit = kit;
            status =
                (role == H51MedicalKitRole.Trauma
                    ? "Trauma kit"
                    : "Medical kit") +
                " opened.";

            return true;
        }

        public void Close()
        {
            open = false;

            if (!bodyInspector)
                bodyInspector = GetComponent<H51BodyInspectorUI>();

            if (bodyInspector && bodyInspector.ExternalSession)
                bodyInspector.CloseExternal();
            else
            {
                if (!controller)
                    controller = GetComponent<FirstPersonController>();
                controller?.ResumeGameplayInputAfterUiClose();
            }

            target = null;
            treatmentKit = null;
            traumaKitOpened = false;
            medicalKitOpened = false;
            status = string.Empty;
        }

        void OnDisable()
        {
            if (open)
                Close();
        }

        void OnGUI()
        {
            if (!open || !target)
                return;

            float inspectorWidth =
                Mathf.Min(
                    1180f,
                    Screen.width-60f);

            float panelWidth =
                Mathf.Min(
                    860f,
                    inspectorWidth-280f);

            float panelHeight = 132f;

            Rect panel =
                new Rect(
                    (Screen.width-panelWidth)*.5f + 135f,
                    Screen.height-panelHeight-34f,
                    panelWidth,
                    panelHeight);

            Color old =
                GUI.color;

            GUI.color =
                new Color(.025f,.03f,.035f,.98f);

            GUI.DrawTexture(
                panel,
                Texture2D.whiteTexture);

            GUI.color = old;

            GUIStyle label =
                new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    wordWrap = true
                };
            label.normal.textColor =
                new Color(.90f,.92f,.94f);

            GUIStyle warning =
                new GUIStyle(label)
                {
                    fontStyle = FontStyle.Bold
                };
            warning.normal.textColor =
                new Color(1f,.55f,.42f);

            GUIStyle button =
                new GUIStyle(GUI.skin.button)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold
                };

            float x = panel.x+12f;
            float y = panel.y+8f;
            float innerW = panel.width-24f;

            GUI.Label(
                new Rect(x,y,innerW,28f),
                target.IsDead
                    ? "CASUALTY IS DECEASED — anatomy remains inspectable; treatment is disabled."
                    : "TRAUMA KIT: bleeding/tourniquet/chest trauma.    MEDICAL KIT: airway/breathing/shock support.",
                target.IsDead ? warning : label);

            y += 30f;

            float gap = 6f;
            float buttonW =
                (innerW-gap*4f)/5f;
            float buttonH = 34f;

            GUI.enabled = !target.IsDead;

            if (GUI.Button(
                    new Rect(x,y,buttonW,buttonH),
                    "TRAUMA
DRESSING",
                    button))
            {
                if (EnsureTreatmentKit(H51MedicalKitRole.Trauma))
                {
                    target.ApplyTraumaDressing();
                    status =
                        "Trauma dressing applied. External bleeding reduced.";
                }
            }

            x += buttonW+gap;

            GUI.enabled =
                !target.IsDead &&
                target.WorstLimbBleedMlPerMinute > 20f;

            if (GUI.Button(
                    new Rect(x,y,buttonW,buttonH),
                    "TOURNIQUET
WORST LIMB",
                    button))
            {
                if (EnsureTreatmentKit(H51MedicalKitRole.Trauma))
                {
                    H51BodyRegion limb =
                        target.FindWorstBleedingLimb();

                    target.ApplyTourniquetToWorstLimb();

                    status =
                        "Tourniquet applied to " +
                        Friendly(limb.ToString()) +
                        ".";
                }
            }

            x += buttonW+gap;

            H51BodyRegionState chest =
                target.GetRegion(H51BodyRegion.Chest);

            H51OrganState leftLung =
                target.GetOrgan(H51OrganType.LeftLung);

            H51OrganState rightLung =
                target.GetOrgan(H51OrganType.RightLung);

            bool chestNeed =
                (chest != null &&
                 chest.externalBleedMlPerMinute > 10f) ||
                (leftLung != null &&
                 leftLung.condition < 85f) ||
                (rightLung != null &&
                 rightLung.condition < 85f);

            GUI.enabled =
                !target.IsDead &&
                chestNeed;

            if (GUI.Button(
                    new Rect(x,y,buttonW,buttonH),
                    "CHEST
TRAUMA",
                    button))
            {
                if (EnsureTreatmentKit(H51MedicalKitRole.Trauma))
                {
                    target.ApplyChestSealAndBreathingSupport();

                    status =
                        "Chest-wound stabilization and breathing support applied.";
                }
            }

            x += buttonW+gap;

            GUI.enabled =
                !target.IsDead &&
                (target.OxygenSaturation < .94f ||
                 target.Consciousness < 80f);

            if (GUI.Button(
                    new Rect(x,y,buttonW,buttonH),
                    "AIRWAY /
BREATHING",
                    button))
            {
                if (EnsureTreatmentKit(H51MedicalKitRole.Medical))
                {
                    target.ProvideAirwaySupport();

                    status =
                        "Airway and breathing support provided.";
                }
            }

            x += buttonW+gap;

            GUI.enabled =
                !target.IsDead &&
                target.Shock > 18f;

            if (GUI.Button(
                    new Rect(x,y,buttonW,buttonH),
                    "SHOCK /
TRANSPORT",
                    button))
            {
                if (EnsureTreatmentKit(H51MedicalKitRole.Medical))
                {
                    target.ProvideShockSupport();

                    status =
                        "Shock-support measures applied. Continue monitoring and arrange transport.";
                }
            }

            GUI.enabled = true;

            GUI.Label(
                new Rect(
                    panel.x+12f,
                    panel.y+76f,
                    panel.width-24f,
                    48f),
                string.IsNullOrWhiteSpace(status)
                    ? BuildPriorityText(target)
                    : status,
                label);
        }

        static string BuildPriorityText(H51AnatomicalHealth health)
        {
            if (health.IsDead)
                return "Patient has no survivable circulation in the current simulation. The body remains in-world for investigation/testing.";

            bool majorBleed =
                health.ExternalBleedMlPerMinute >= 180f ||
                health.InternalBleedMlPerMinute >= 180f;
            bool lowBlood =
                health.BloodFraction < .72f;
            bool lowOxygen =
                health.OxygenSaturation < .90f;
            bool criticalOrgan = false;

            for (int i=0;i<health.Organs.Count;i++)
            {
                H51OrganState organ = health.Organs[i];
                if (organ != null && organ.condition < 35f)
                {
                    criticalOrgan = true;
                    break;
                }
            }

            if (majorBleed || lowBlood || lowOxygen || criticalOrgan)
            {
                return "URGENT: This patient needs rapid stabilization and hospital-level care. A field trauma kit can control some external threats, but it cannot replace major blood loss or repair destroyed internal organs.";
            }

            if (health.ExternalBleedMlPerMinute > 1f ||
                health.Shock > 25f)
            {
                return "Priorities: control bleeding, support breathing/circulation, monitor for worsening shock, and arrange medical evaluation.";
            }

            return "No immediately life-threatening field problem is detected by the current simulation.";
        }

        static string Friendly(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            System.Text.StringBuilder builder =
                new System.Text.StringBuilder();

            for (int i=0;i<value.Length;i++)
            {
                char c = value[i];
                if (i > 0 && char.IsUpper(c))
                    builder.Append(' ');
                builder.Append(c);
            }

            return builder.ToString();
        }
    }
}
