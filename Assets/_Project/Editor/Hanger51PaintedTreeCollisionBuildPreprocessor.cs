using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hanger51.EditorTools
{
    public sealed class Hanger51PaintedTreeCollisionBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!Hanger51PaintedTreeCollisionProxySetup.SyncForBuild(false))
            {
                throw new BuildFailedException(
                    "Hanger 51 build stopped because painted-tree collision hitboxes could not be synchronized. Run Environment Step 11 and fix the reported tree collider error first.");
            }

            if (!EditorSceneManager.SaveOpenScenes())
            {
                throw new BuildFailedException(
                    "Hanger 51 build stopped because Unity could not save the scene after synchronizing painted-tree hitboxes.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Hanger 51 build preprocessor synchronized explicit painted-tree trunk hitboxes before building.");
        }
    }
}
