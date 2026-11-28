using Hanger51.EngineAssembly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class MerlinCoverMountRepairSetup
    {
        private const string StationName = "V-1650 Engine Stand";
        private const string InstalledEngineName = "Installed Engine Core";
        private const string LeftBankName = "Left Bank";
        private const string RightBankName = "Right Bank";
        private const string LeftCoverName = "Installed Left Cylinder Cover";
        private const string RightCoverName = "Installed Right Cylinder Cover";

        private static readonly Vector3 CoverMountOffset =
            new Vector3(0f, 0.535f, 0f);

        [MenuItem("Hanger 51/Merlin Assembly/6 - Repair Cylinder Cover Mount Positions")]
        public static void RepairCylinderCoverMountPositions()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Merlin Step 6 failed. Exit Play mode before repairing the cover mounts.");
                return;
            }

            if (!TryFindRequiredTransforms(
                    out EngineAssemblyStation station,
                    out Transform installedEngine,
                    out Transform leftBank,
                    out Transform rightBank,
                    out Transform leftCover,
                    out Transform rightCover))
            {
                return;
            }

            EngineCoverMountSnapper snapper =
                station.GetComponent<EngineCoverMountSnapper>();
            if (snapper == null)
            {
                snapper = Undo.AddComponent<EngineCoverMountSnapper>(station.gameObject);
            }

            Undo.RecordObjects(
                new Object[]
                {
                    snapper,
                    leftCover,
                    rightCover
                },
                "Repair V-1650 cover mount positions");

            snapper.Configure(
                installedEngine,
                leftBank,
                rightBank,
                leftCover,
                rightCover,
                CoverMountOffset);

            EditorUtility.SetDirty(snapper);
            EditorUtility.SetDirty(leftCover);
            EditorUtility.SetDirty(rightCover);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            // Rebuild the interaction targets after moving the covers so the
            // highlights, bolt locations, and spark-plug wells inherit the
            // corrected bank-relative pose.
            MerlinFastenerInteractionSetup.AddHighlightsAndFastenerInteractions();

            snapper = station.GetComponent<EngineCoverMountSnapper>();
            if (snapper != null)
            {
                snapper.SnapCoversToBanks();
                EditorUtility.SetDirty(snapper);
            }

            Scene activeScene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            AssetDatabase.SaveAssets();

            if (string.IsNullOrWhiteSpace(activeScene.path)
                || !EditorSceneManager.SaveScene(activeScene))
            {
                Debug.LogError("Merlin Step 6 repaired the cover positions but could not save the scene.");
                return;
            }

            if (!Hanger51BuildTools.PrepareCurrentSceneForBuild(false))
            {
                Debug.LogError(
                    "Merlin Step 6 repaired the covers, but build preparation failed. Run Build Step 1.");
                return;
            }

            Selection.activeGameObject = station.gameObject;
            Debug.Log(
                "Merlin Step 6 complete. Both cylinder covers are now anchored directly to their bank head decks. "
                + "Cover highlights, bolts, and spark-plug wells were rebuilt around the corrected positions.",
                station);
        }

        [MenuItem("Hanger 51/Merlin Assembly/7 - Validate Cylinder Cover Mount Positions")]
        public static void ValidateCylinderCoverMountPositions()
        {
            if (!TryFindRequiredTransforms(
                    out EngineAssemblyStation station,
                    out Transform installedEngine,
                    out Transform leftBank,
                    out Transform rightBank,
                    out Transform leftCover,
                    out Transform rightCover))
            {
                return;
            }

            EngineCoverMountSnapper snapper =
                station.GetComponent<EngineCoverMountSnapper>();
            bool passed = true;

            if (snapper == null || !snapper.IsConfigured)
            {
                Debug.LogError(
                    "Merlin Step 7 failed: EngineCoverMountSnapper is missing or incomplete. Run Merlin Step 6.",
                    station);
                passed = false;
            }
            else
            {
                passed &= ValidateCoverPose(
                    "left",
                    leftCover,
                    snapper.GetExpectedWorldPosition(true),
                    snapper.GetExpectedWorldRotation(true));
                passed &= ValidateCoverPose(
                    "right",
                    rightCover,
                    snapper.GetExpectedWorldPosition(false),
                    snapper.GetExpectedWorldRotation(false));
            }

            EngineAssemblyInteractionTarget[] targets =
                station.GetComponentsInChildren<EngineAssemblyInteractionTarget>(true);

            for (int coverIndex = 0; coverIndex < 2; coverIndex++)
            {
                EngineAssemblyInteractionTarget placementTarget = null;
                for (int index = 0; index < targets.Length; index++)
                {
                    if (targets[index].InteractionKind
                            == EngineAssemblyInteractionKind.CoverPlacement
                        && targets[index].GroupIndex == coverIndex)
                    {
                        placementTarget = targets[index];
                        break;
                    }
                }

                Transform cover = coverIndex == 0 ? leftCover : rightCover;
                if (placementTarget == null)
                {
                    Debug.LogError(
                        $"Merlin Step 7 failed: cover {coverIndex + 1} placement target is missing.",
                        station);
                    passed = false;
                }
                else if (Vector3.Distance(
                             placementTarget.transform.position,
                             cover.position) > 0.01f)
                {
                    Debug.LogError(
                        $"Merlin Step 7 failed: cover {coverIndex + 1} highlight does not match the corrected cover position.",
                        placementTarget);
                    passed = false;
                }
            }

            if (!Hanger51BuildTools.ValidateBuildSetup(false))
            {
                Debug.LogError("Merlin Step 7 failed: standalone build setup is not ready.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Merlin Step 7 passed. Both covers are seated on their cylinder-bank head decks, "
                    + "their rotations match the 60-degree V-bank, and the interaction highlights follow the corrected poses.",
                    station);
            }
        }

        private static bool ValidateCoverPose(
            string sideName,
            Transform cover,
            Vector3 expectedPosition,
            Quaternion expectedRotation)
        {
            float positionError = Vector3.Distance(cover.position, expectedPosition);
            float rotationError = Quaternion.Angle(cover.rotation, expectedRotation);

            if (positionError <= 0.01f && rotationError <= 0.25f)
            {
                return true;
            }

            Debug.LogError(
                $"Merlin Step 7 failed: the {sideName} cover is off its bank mount. "
                + $"Position error: {positionError:F3}, rotation error: {rotationError:F2} degrees.",
                cover);
            return false;
        }

        private static bool TryFindRequiredTransforms(
            out EngineAssemblyStation station,
            out Transform installedEngine,
            out Transform leftBank,
            out Transform rightBank,
            out Transform leftCover,
            out Transform rightCover)
        {
            station = Object.FindFirstObjectByType<EngineAssemblyStation>();
            installedEngine = null;
            leftBank = null;
            rightBank = null;
            leftCover = null;
            rightCover = null;

            if (station == null || station.name != StationName)
            {
                Debug.LogError(
                    "Merlin cover repair failed: the generated V-1650 engine stand is missing. Run Merlin Step 1 first.");
                return false;
            }

            installedEngine = station.transform.Find(InstalledEngineName);
            leftCover = station.transform.Find(LeftCoverName);
            rightCover = station.transform.Find(RightCoverName);

            if (installedEngine != null)
            {
                leftBank = FindDescendant(installedEngine, LeftBankName);
                rightBank = FindDescendant(installedEngine, RightBankName);
            }

            if (installedEngine == null
                || leftBank == null
                || rightBank == null
                || leftCover == null
                || rightCover == null)
            {
                Debug.LogError(
                    "Merlin cover repair failed: the installed engine, cylinder banks, or cover visuals are missing. "
                    + "Run Merlin Steps 1 and 4 before Step 6.",
                    station);
                return false;
            }

            return true;
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < descendants.Length; index++)
            {
                if (descendants[index].name == objectName)
                {
                    return descendants[index];
                }
            }

            return null;
        }
    }
}
