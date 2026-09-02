using UnityEngine;

namespace Hanger51.Aircraft
{
    /// <summary>
    /// Removes the obsolete armament interactor if the legacy runtime installer in
    /// P51WingArmamentSystem.cs adds it after scene load. The standalone-safe
    /// P51WingArmamentServicePointInteractor is the only supported interaction path.
    /// </summary>
    public static class P51WingArmamentLegacyRuntimeBlocker
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RemoveLegacyInteractor()
        {
            P51WingArmamentPlayerInteractor[] legacy = Object.FindObjectsByType<P51WingArmamentPlayerInteractor>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int index = 0; index < legacy.Length; index++)
            {
                P51WingArmamentPlayerInteractor interactor = legacy[index];
                if (interactor == null) continue;
                Object.Destroy(interactor);
            }
        }
    }
}
