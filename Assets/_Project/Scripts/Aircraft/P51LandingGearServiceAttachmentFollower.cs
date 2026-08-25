using UnityEngine;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(175)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(P51LandingGearMaintenanceController))]
    public sealed class P51LandingGearServiceAttachmentFollower : MonoBehaviour
    {
        private const string GearSystemRootName = "P-51 Serviceable Retractable Landing Gear";
        private static readonly string[] Labels = { "Left Main", "Right Main", "Tailwheel" };

        private P51LandingGearMaintenanceController maintenance;
        private float nextRepairTime;

        public int CorrectlyAttachedTargetCount => CountCorrectAttachments();

        private void Awake()
        {
            maintenance = GetComponent<P51LandingGearMaintenanceController>();
            RepairHierarchy();
        }

        private void OnEnable()
        {
            if (maintenance == null)
            {
                maintenance = GetComponent<P51LandingGearMaintenanceController>();
            }
            RepairHierarchy();
        }

        private void LateUpdate()
        {
            // The hierarchy normally needs repairing only once. Keep a slow safety
            // check so dynamically rebuilt/spawned landing gear is corrected too.
            if (Time.unscaledTime < nextRepairTime)
            {
                return;
            }

            nextRepairTime = Time.unscaledTime + 1.0f;
            RepairHierarchy();
        }

        public int RepairHierarchy()
        {
            Transform systemRoot = transform.Find(GearSystemRootName);
            if (systemRoot == null)
            {
                return 0;
            }

            int repairedOrCorrect = 0;
            for (int wheelIndex = 0; wheelIndex < Labels.Length; wheelIndex++)
            {
                string label = Labels[wheelIndex];
                Transform movingGear = FindDescendant(systemRoot, $"{label} Serviceable Gear Visual");
                if (movingGear == null)
                {
                    continue;
                }

                Transform mountTarget = FindDescendant(systemRoot, $"{label} Large Mount Bolt Service Target");
                Transform tireTarget = FindDescendant(systemRoot, $"{label} Tire and Valve Service Target");

                if (AttachPreservingWorldPose(mountTarget, movingGear))
                {
                    repairedOrCorrect++;
                }
                if (AttachPreservingWorldPose(tireTarget, movingGear))
                {
                    repairedOrCorrect++;
                }
            }

            return repairedOrCorrect;
        }

        private int CountCorrectAttachments()
        {
            Transform systemRoot = transform.Find(GearSystemRootName);
            if (systemRoot == null)
            {
                return 0;
            }

            int count = 0;
            for (int wheelIndex = 0; wheelIndex < Labels.Length; wheelIndex++)
            {
                string label = Labels[wheelIndex];
                Transform movingGear = FindDescendant(systemRoot, $"{label} Serviceable Gear Visual");
                if (movingGear == null)
                {
                    continue;
                }

                Transform mountTarget = FindDescendant(systemRoot, $"{label} Large Mount Bolt Service Target");
                Transform tireTarget = FindDescendant(systemRoot, $"{label} Tire and Valve Service Target");
                if (mountTarget != null && mountTarget.parent == movingGear) count++;
                if (tireTarget != null && tireTarget.parent == movingGear) count++;
            }
            return count;
        }

        private static bool AttachPreservingWorldPose(Transform target, Transform movingGear)
        {
            if (target == null || movingGear == null)
            {
                return false;
            }

            if (target.parent != movingGear)
            {
                target.SetParent(movingGear, true);
            }
            return target.parent == movingGear;
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < all.Length; index++)
            {
                Transform candidate = all[index];
                if (candidate != null && candidate.name == objectName)
                {
                    return candidate;
                }
            }
            return null;
        }
    }
}
