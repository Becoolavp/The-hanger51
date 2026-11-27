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
        H51CombatItemRegistry registry;
        InventoryItemDefinition treatmentKit;
        bool kitOpened;
        bool open;
        string status = string.Empty;

        public bool IsOpen => open;

        void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            controller = GetComponent<FirstPersonController>();
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
            treatmentKit = FindAvailableTreatmentKit();
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
            kitOpened = false;
            treatmentKit = treatmentKit ? treatmentKit : FindAvailableTreatmentKit();

            status = newTarget.IsDead
                ? "Assessment opened. The patient is deceased in the current simulation; injuries can still be inspected."
                : treatmentKit && inventory && inventory.CountItem(treatmentKit) > 0
                    ? "Assessment opened. A trauma kit is available if treatment is required."
                    : "Assessment opened. You can inspect the injuries, but you need a trauma kit in inventory to perform treatment.";

            if (!controller)
                controller = GetComponent<FirstPersonController>();
            controller?.SetExternalInputBlocked(true);

            return true;
        }

        InventoryItemDefinition FindAvailableTreatmentKit()
        {
            if (!registry)
                registry = GetComponent<H51CombatItemRegistry>();
            if (!inventory || !registry)
                return null;

            for (int i=0;i<registry.Definitions.Count;i++)
            {
                H51CombatItemDefinition definition = registry.Definitions[i];
                if (definition &&
                    definition.Kind == H51CombatItemKind.MedicalKit &&
                    definition.InventoryItem &&
                    inventory.CountItem(definition.InventoryItem) > 0)
                    return definition.InventoryItem;
            }

            return null;
        }

        bool EnsureTreatmentKit()
        {
            if (!target || target.IsDead)
            {
                status = "No field treatment can be applied to a deceased patient.";
                return false;
            }

            if (kitOpened)
                return true;

            if (!inventory)
                inventory = GetComponent<PlayerInventory>();

            if (!treatmentKit ||
                !inventory ||
                inventory.CountItem(treatmentKit) <= 0)
                treatmentKit = FindAvailableTreatmentKit();

            if (!treatmentKit ||
                !inventory ||
                inventory.RemoveItemQuantity(treatmentKit,1) != 1)
            {
                status = "A trauma kit is required before treatment can begin.";
                return false;
            }

            kitOpened = true;
            status = "Trauma kit opened.";
            return true;
        }

        public void Close()
        {
            open = false;
            target = null;
            treatmentKit = null;
            kitOpened = false;
            status = string.Empty;
            controller?.SetExternalInputBlocked(false);
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

            float width = Mathf.Min(760f,Screen.width-60f);
            float height = Mathf.Min(610f,Screen.height-60f);
            Rect panel = new Rect(
                (Screen.width-width)*.5f,
                (Screen.height-height)*.5f,
                width,
                height);

            Color old = GUI.color;
            GUI.color = new Color(.025f,.03f,.035f,.97f);
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
                fontSize = 14,
                wordWrap = true
            };
            label.normal.textColor = new Color(.90f,.92f,.94f);

            GUIStyle warning = new GUIStyle(label)
            {
                fontStyle = FontStyle.Bold
            };
            warning.normal.textColor = new Color(1f,.43f,.36f);

            GUIStyle button = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };

            float x = panel.x+28f;
            float y = panel.y+24f;
            float innerW = panel.width-56f;

            GUI.Label(
                new Rect(x,y,innerW,32f),
                "TRAUMA ASSESSMENT — " + target.gameObject.name,
                title);
            y += 42f;

            GUI.Label(
                new Rect(x,y,innerW,28f),
                "Status: " + target.OverallStatus +
                "    Blood: " + target.BloodVolumeLiters.ToString("0.00") +
                "/" + target.NormalBloodVolumeLiters.ToString("0.00") +
                " L    O₂: " + (target.OxygenSaturation*100f).ToString("0") + "%",
                label);
            y += 34f;

            GUI.Label(
                new Rect(x,y,innerW,28f),
                "External bleeding: " + target.ExternalBleedMlPerMinute.ToString("0") +
                " mL/min    Internal bleeding: " + target.InternalBleedMlPerMinute.ToString("0") +
                " mL/min    Shock: " + target.Shock.ToString("0") + "%",
                label);
            y += 40f;

            string priorities = BuildPriorityText(target);
            GUI.Label(
                new Rect(x,y,innerW,70f),
                priorities,
                priorities.Contains("URGENT") ? warning : label);
            y += 82f;

            float buttonH = 42f;
            float gap = 9f;

            if (GUI.Button(
                    new Rect(x,y,innerW,buttonH),
                    "CONTROL EXTERNAL BLEEDING — trauma dressing",
                    button))
            {
                if (EnsureTreatmentKit())
                {
                    target.ApplyTraumaDressing();
                    status = "Trauma dressing applied. External bleeding has been reduced.";
                }
            }
            y += buttonH+gap;

            GUI.enabled = target.WorstLimbBleedMlPerMinute > 20f;
            if (GUI.Button(
                    new Rect(x,y,innerW,buttonH),
                    "TOURNIQUET — worst severely bleeding limb",
                    button))
            {
                if (EnsureTreatmentKit())
                {
                    H51BodyRegion limb = target.FindWorstBleedingLimb();
                    target.ApplyTourniquetToWorstLimb();
                    status = "Tourniquet applied to " + Friendly(limb.ToString()) + ".";
                }
            }
            GUI.enabled = true;
            y += buttonH+gap;

            H51BodyRegionState chest =
                target.GetRegion(H51BodyRegion.Chest);
            H51OrganState leftLung =
                target.GetOrgan(H51OrganType.LeftLung);
            H51OrganState rightLung =
                target.GetOrgan(H51OrganType.RightLung);

            bool chestNeed =
                (chest != null && chest.externalBleedMlPerMinute > 10f) ||
                (leftLung != null && leftLung.condition < 85f) ||
                (rightLung != null && rightLung.condition < 85f);

            GUI.enabled = chestNeed;
            if (GUI.Button(
                    new Rect(x,y,innerW,buttonH),
                    "CHEST WOUND / BREATHING SUPPORT",
                    button))
            {
                if (EnsureTreatmentKit())
                {
                    target.ApplyChestSealAndBreathingSupport();
                    status = "Chest wound stabilization and breathing support applied.";
                }
            }
            GUI.enabled = true;
            y += buttonH+gap;

            GUI.enabled = target.OxygenSaturation < .94f || target.Consciousness < 80f;
            if (GUI.Button(
                    new Rect(x,y,innerW,buttonH),
                    "AIRWAY / BREATHING SUPPORT",
                    button))
            {
                if (EnsureTreatmentKit())
                {
                    target.ProvideAirwaySupport();
                    status = "Airway and breathing support provided.";
                }
            }
            GUI.enabled = true;
            y += buttonH+gap;

            GUI.enabled = target.Shock > 18f;
            if (GUI.Button(
                    new Rect(x,y,innerW,buttonH),
                    "SHOCK SUPPORT / PREPARE FOR TRANSPORT",
                    button))
            {
                if (EnsureTreatmentKit())
                {
                    target.ProvideShockSupport();
                    status = "Shock-support measures applied. Continue monitoring and arrange transport if needed.";
                }
            }
            GUI.enabled = true;
            y += buttonH+12f;

            GUI.Label(
                new Rect(x,y,innerW,52f),
                status,
                label);

            GUI.Label(
                new Rect(x,panel.yMax-36f,innerW,24f),
                "ESC — close treatment panel",
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
