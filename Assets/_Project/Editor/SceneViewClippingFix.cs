using UnityEditor;
using UnityEngine;

namespace Hanger51.EditorTools
{
    public static class SceneViewClippingFix
    {
        [MenuItem("Hanger 51/Environment/3 - Fix Scene View Camera Clipping")]
        public static void FixSceneViewCameraClipping()
        {
            SceneView[] sceneViews = Resources.FindObjectsOfTypeAll<SceneView>();
            if (sceneViews == null || sceneViews.Length == 0)
            {
                Debug.LogWarning("No open Scene view was found. Open a Scene tab, then run the clipping fix again.");
                return;
            }

            int updatedCount = 0;
            for (int index = 0; index < sceneViews.Length; index++)
            {
                SceneView sceneView = sceneViews[index];
                if (sceneView == null)
                {
                    continue;
                }

                SceneView.CameraSettings settings = sceneView.cameraSettings;
                settings.dynamicClip = false;
                settings.nearClip = 0.01f;
                settings.farClip = 100000f;
                sceneView.cameraSettings = settings;
                sceneView.Repaint();
                updatedCount++;
            }

            Debug.Log(
                $"Scene view clipping fix complete. Updated {updatedCount} Scene view camera(s): "
                + "Dynamic Clipping off, Near 0.01 m, Far 100,000 m. Gameplay cameras were not changed.");
        }
    }
}
