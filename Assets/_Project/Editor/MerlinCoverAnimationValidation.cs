using Hanger51.EngineAssembly;
using UnityEditor;
using UnityEngine;

namespace Hanger51.EditorTools
{
    public static class MerlinCoverAnimationValidation
    {
        [MenuItem("Hanger 51/Merlin Assembly/8 - Validate Cover Animation Path")]
        public static void ValidateCoverAnimationPath()
        {
            EngineAssemblyStation station =
                Object.FindFirstObjectByType<EngineAssemblyStation>();

            if (station == null)
            {
                Debug.LogError(
                    "Merlin Step 8 failed: the V-1650 engine stand is missing. Run Merlin Step 1 first.");
                return;
            }

            EngineCoverMountSnapper snapper =
                station.GetComponent<EngineCoverMountSnapper>();

            if (snapper == null || !snapper.IsConfigured)
            {
                Debug.LogError(
                    "Merlin Step 8 failed: the cover mount snapper is missing. Run Merlin Step 6 first.",
                    station);
                return;
            }

            EngineAssemblyInteractionTarget[] targets =
                station.GetComponentsInChildren<EngineAssemblyInteractionTarget>(true);

            bool passed = true;
            int coverTargetCount = 0;

            for (int index = 0; index < targets.Length; index++)
            {
                EngineAssemblyInteractionTarget target = targets[index];
                if (target.InteractionKind
                    != EngineAssemblyInteractionKind.CoverPlacement)
                {
                    continue;
                }

                coverTargetCount++;
                bool leftSide = target.GroupIndex == 0;
                Transform cover = leftSide ? snapper.LeftCover : snapper.RightCover;
                string sideName = leftSide ? "left" : "right";

                float finalPositionError = Vector3.Distance(
                    target.FinalWorldPosition,
                    snapper.GetExpectedWorldPosition(leftSide));
                float finalRotationError = Quaternion.Angle(
                    target.FinalWorldRotation,
                    snapper.GetExpectedWorldRotation(leftSide));

                Vector3 lift =
                    target.RaisedWorldPosition - target.FinalWorldPosition;
                float verticalLift = Vector3.Dot(lift, Vector3.up);
                float outwardLift = lift.magnitude;

                if (finalPositionError > 0.01f
                    || finalRotationError > 0.25f)
                {
                    Debug.LogError(
                        $"Merlin Step 8 failed: the {sideName} animation target does not match its cylinder-bank mount. "
                        + $"Position error {finalPositionError:F3}, rotation error {finalRotationError:F2} degrees.",
                        target);
                    passed = false;
                }

                if (cover == null
                    || Vector3.Distance(cover.position, target.FinalWorldPosition) > 0.01f)
                {
                    Debug.LogError(
                        $"Merlin Step 8 failed: the {sideName} cover visual and its animation destination do not match.",
                        target);
                    passed = false;
                }

                if (outwardLift < 0.25f || verticalLift < 0.15f)
                {
                    Debug.LogError(
                        $"Merlin Step 8 failed: the {sideName} cover animation begins below or too close to the engine. "
                        + $"Lift distance {outwardLift:F3}, vertical lift {verticalLift:F3}.",
                        target);
                    passed = false;
                }
            }

            if (coverTargetCount != 2)
            {
                Debug.LogError(
                    $"Merlin Step 8 failed: expected two cover animation targets but found {coverTargetCount}.",
                    station);
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "Merlin Step 8 passed. Both cover animations use world-space bank mounts and begin above/outward from the engine before lowering to the cylinder-head decks.",
                    station);
            }
        }
    }
}
