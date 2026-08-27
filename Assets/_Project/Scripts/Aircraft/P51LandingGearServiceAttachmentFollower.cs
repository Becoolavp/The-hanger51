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
        private static readonly float[] MountHeights = { 1.05f, 1.05f, 0.58f };

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

                Transform tireVisual = FindDescendant(movingGear, $"{label} Tire Visual");
                Transform mountTarget = FindDescendant(systemRoot, $"{label} Large Mount Bolt Service Target");
                Transform tireTarget = FindDescendant(systemRoot, $"{label} Tire and Valve Service Target");

                // The main-gear mount bolts represent the upper gear-to-airframe attachment,
                // so they remain at the top of each strut. On the tailwheel, the visible bolt
                // is part of the little wheel/hub assembly the player services. Keep that bolt
                // with the grounded tire so it cannot float above the rim when the raycast
                // suspension moves the tailwheel below the retracting gear root.
                if (wheelIndex == 2 && tireVisual != null)
                {
                    if (AttachAtLocalPose(mountTarget, tireVisual, Vector3.zero))
                    {
                        repairedOrCorrect++;
                    }
                }
                else if (AttachAtLocalPose(
                             mountTarget,
                             movingGear,
                             new Vector3(0f, MountHeights[wheelIndex], 0f)))
                {
                    repairedOrCorrect++;
                }

                // Tire/valve interaction belongs to the wheel, not to the unsprung gear root.
                // Parenting it to the visual tire also keeps its trigger and valve stem exactly
                // aligned with suspension travel on all three stations.
                Transform tireParent = tireVisual != null ? tireVisual : movingGear;
                if (AttachAtLocalPose(tireTarget, tireParent, Vector3.zero))
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

                Transform tireVisual = FindDescendant(movingGear, $"{label} Tire Visual");
                Transform mountTarget = FindDescendant(systemRoot, $"{label} Large Mount Bolt Service Target");
                Transform tireTarget = FindDescendant(systemRoot, $"{label} Tire and Valve Service Target");

                if (wheelIndex == 2 && tireVisual != null)
                {
                    if (IsAtLocalPose(mountTarget, tireVisual, Vector3.zero))
                    {
                        count++;
                    }
                }
                else if (IsAtLocalPose(
                             mountTarget,
                             movingGear,
                             new Vector3(0f, MountHeights[wheelIndex], 0f)))
                {
                    count++;
                }

                Transform tireParent = tireVisual != null ? tireVisual : movingGear;
                if (IsAtLocalPose(tireTarget, tireParent, Vector3.zero))
                {
                    count++;
                }
            }
            return count;
        }

        private static bool AttachAtLocalPose(
            Transform target,
            Transform movingGear,
            Vector3 localPosition)
        {
            if (target == null || movingGear == null)
            {
                return false;
            }

            if (target.parent != movingGear)
            {
                target.SetParent(movingGear, false);
            }
            target.localPosition = localPosition;
            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one;
            return IsAtLocalPose(target, movingGear, localPosition);
        }

        private static bool IsAtLocalPose(
            Transform target,
            Transform movingGear,
            Vector3 localPosition)
        {
            return target != null
                && movingGear != null
                && target.parent == movingGear
                && Vector3.SqrMagnitude(target.localPosition - localPosition) < 0.000001f;
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
