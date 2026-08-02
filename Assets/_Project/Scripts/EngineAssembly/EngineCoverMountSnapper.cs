using UnityEngine;

namespace Hanger51.EngineAssembly
{
    [DefaultExecutionOrder(-1000)]
    public sealed class EngineCoverMountSnapper : MonoBehaviour
    {
        [Header("Engine References")]
        [SerializeField] private Transform installedEngineRoot;
        [SerializeField] private Transform leftBank;
        [SerializeField] private Transform rightBank;
        [SerializeField] private Transform leftCover;
        [SerializeField] private Transform rightCover;

        [Header("Mount Pose")]
        [Tooltip("Position of the cover root in each cylinder bank's local space. The head-deck top is approximately Y 0.52.")]
        [SerializeField] private Vector3 bankLocalMountOffset = new Vector3(0f, 0.535f, 0f);

        public Transform InstalledEngineRoot => installedEngineRoot;
        public Transform LeftBank => leftBank;
        public Transform RightBank => rightBank;
        public Transform LeftCover => leftCover;
        public Transform RightCover => rightCover;
        public Vector3 BankLocalMountOffset => bankLocalMountOffset;
        public bool IsConfigured => installedEngineRoot != null
            && leftBank != null
            && rightBank != null
            && leftCover != null
            && rightCover != null;

        private void Awake()
        {
            SnapCoversToBanks();
        }

        public void Configure(
            Transform configuredEngineRoot,
            Transform configuredLeftBank,
            Transform configuredRightBank,
            Transform configuredLeftCover,
            Transform configuredRightCover,
            Vector3 configuredBankLocalMountOffset)
        {
            installedEngineRoot = configuredEngineRoot;
            leftBank = configuredLeftBank;
            rightBank = configuredRightBank;
            leftCover = configuredLeftCover;
            rightCover = configuredRightCover;
            bankLocalMountOffset = configuredBankLocalMountOffset;

            SnapCoversToBanks();
        }

        public void SnapCoversToBanks()
        {
            if (!IsConfigured)
            {
                return;
            }

            SnapCover(leftCover, leftBank);
            SnapCover(rightCover, rightBank);
        }

        public Vector3 GetExpectedWorldPosition(bool leftSide)
        {
            Transform bank = leftSide ? leftBank : rightBank;
            return bank != null
                ? bank.TransformPoint(bankLocalMountOffset)
                : transform.position;
        }

        public Quaternion GetExpectedWorldRotation(bool leftSide)
        {
            Transform bank = leftSide ? leftBank : rightBank;
            return bank != null ? bank.rotation : transform.rotation;
        }

        public float GetPositionError(bool leftSide)
        {
            Transform cover = leftSide ? leftCover : rightCover;
            if (cover == null)
            {
                return float.PositiveInfinity;
            }

            return Vector3.Distance(
                cover.position,
                GetExpectedWorldPosition(leftSide));
        }

        public float GetRotationError(bool leftSide)
        {
            Transform cover = leftSide ? leftCover : rightCover;
            if (cover == null)
            {
                return float.PositiveInfinity;
            }

            return Quaternion.Angle(
                cover.rotation,
                GetExpectedWorldRotation(leftSide));
        }

        private void SnapCover(Transform cover, Transform bank)
        {
            if (cover == null || bank == null)
            {
                return;
            }

            cover.position = bank.TransformPoint(bankLocalMountOffset);
            cover.rotation = bank.rotation;
            cover.localScale = Vector3.one;
        }

        private void OnValidate()
        {
            bankLocalMountOffset.y = Mathf.Max(0f, bankLocalMountOffset.y);
        }
    }
}
