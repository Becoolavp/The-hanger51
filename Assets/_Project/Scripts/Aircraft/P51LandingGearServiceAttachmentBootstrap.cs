using UnityEngine;

namespace Hanger51.Aircraft
{
    public static class P51LandingGearServiceAttachmentBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallFollowers()
        {
            P51LandingGearMaintenanceController[] controllers =
                Object.FindObjectsByType<P51LandingGearMaintenanceController>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);

            for (int index = 0; index < controllers.Length; index++)
            {
                P51LandingGearMaintenanceController maintenance = controllers[index];
                if (maintenance == null)
                {
                    continue;
                }

                P51LandingGearServiceAttachmentFollower follower =
                    maintenance.GetComponent<P51LandingGearServiceAttachmentFollower>();
                if (follower == null)
                {
                    follower = maintenance.gameObject.AddComponent<P51LandingGearServiceAttachmentFollower>();
                }
                follower.RepairHierarchy();
            }
        }
    }
}
