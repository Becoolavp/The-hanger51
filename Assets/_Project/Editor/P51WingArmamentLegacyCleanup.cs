using Hanger51.Aircraft;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51WingArmamentLegacyCleanup
    {
        [MenuItem("Hanger 51/Build/93 - Remove Legacy Armament Player Components")]
        public static void RemoveLegacyComponents()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("Legacy armament cleanup failed. Exit Play mode and wait for compilation to finish.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
            {
                Debug.LogError("Legacy armament cleanup failed. Open the saved game scene first.");
                return;
            }

            if (!EditorSceneManager.SaveOpenScenes())
            {
                Debug.LogError("Legacy armament cleanup could not save the open scene(s).");
                return;
            }

            int removedInteractors = 0;
            int removedGuards = 0;

            P51WingArmamentPlayerInteractor[] interactors = Object.FindObjectsByType<P51WingArmamentPlayerInteractor>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < interactors.Length; index++)
            {
                P51WingArmamentPlayerInteractor interactor = interactors[index];
                if (interactor == null || interactor.gameObject.scene != scene) continue;
                Undo.DestroyObjectImmediate(interactor);
                removedInteractors++;
            }

            P51WingArmamentRuntimePerformanceGuard[] guards = Object.FindObjectsByType<P51WingArmamentRuntimePerformanceGuard>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < guards.Length; index++)
            {
                P51WingArmamentRuntimePerformanceGuard guard = guards[index];
                if (guard == null || guard.gameObject.scene != scene) continue;
                Undo.DestroyObjectImmediate(guard);
                removedGuards++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError("Legacy armament cleanup changed the scene but Unity could not save it.");
                return;
            }

            Debug.Log(
                $"Legacy armament cleanup complete for '{scene.path}'. "
                + $"Removed legacy interactors={removedInteractors}, removed legacy guards={removedGuards}. "
                + "The standalone-safe P51WingArmamentServicePointInteractor remains in place.");
        }

        [MenuItem("Hanger 51/Build/94 - Validate Legacy Armament Components Removed")]
        public static void ValidateLegacyRemoved()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("Legacy armament validation failed: no valid active scene.");
                return;
            }

            int legacyInteractors = CountInScene<P51WingArmamentPlayerInteractor>(scene);
            int legacyGuards = CountInScene<P51WingArmamentRuntimePerformanceGuard>(scene);
            int safeInteractors = CountInScene<P51WingArmamentServicePointInteractor>(scene);
            int safePoints = CountInScene<P51WingArmamentServicePoint>(scene);

            if (legacyInteractors == 0 && legacyGuards == 0 && safeInteractors > 0 && safePoints == 14)
            {
                Debug.Log(
                    $"Legacy armament validation PASSED for '{scene.path}'. "
                    + $"LegacyInteractors=0, LegacyGuards=0, SafeInteractors={safeInteractors}, SafePoints={safePoints}.");
            }
            else
            {
                Debug.LogError(
                    $"Legacy armament validation FAILED for '{scene.path}'. "
                    + $"LegacyInteractors={legacyInteractors}, LegacyGuards={legacyGuards}, "
                    + $"SafeInteractors={safeInteractors}, SafePoints={safePoints}.");
            }
        }

        private static int CountInScene<T>(Scene scene) where T : Component
        {
            int count = 0;
            T[] components = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int index = 0; index < components.Length; index++)
            {
                T component = components[index];
                if (component != null && component.gameObject.scene == scene) count++;
            }
            return count;
        }
    }
}
