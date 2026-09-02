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
                Transform rimVisual = FindDescendant(movingGear, $"{label} Rim Visual");
                Transform mountTarget = FindDescendant(systemRoot, $"{label} Large Mount Bolt Service Target");
                Transform tireTarget = FindDescendant(systemRoot, $"{label} Tire and Valve Service Target");

                // Main-gear mount bolts stay at the upper strut attachment. The tailwheel's
                // visible bolt is part of the little rim/hub assembly, so keep it on the rim.
                // Using the rim rather than the rubber tire also keeps the bolt available for
                // service after the tire itself has been removed.
                if (wheelIndex == 2 && rimVisual != null)
                {
                    if (AttachAtLocalPose(mountTarget, rimVisual, Vector3.zero))
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

                // Tire/valve interaction belongs to the tire and follows suspension travel.
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
                Transform rimVisual = FindDescendant(movingGear, $"{label} Rim Visual");
                Transform mountTarget = FindDescendant(systemRoot, $"{label} Large Mount Bolt Service Target");
                Transform tireTarget = FindDescendant(systemRoot, $"{label} Tire and Valve Service Target");

                if (wheelIndex == 2 && rimVisual != null)
                {
                    if (IsAtLocalPose(mountTarget, rimVisual, Vector3.zero))
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
