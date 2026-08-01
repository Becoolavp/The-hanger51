using UnityEngine;

namespace Hanger51.Systems
{
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class FramePacingController : MonoBehaviour
    {
        [SerializeField] private bool enableVSync = true;
        [SerializeField, Min(30)] private int fallbackTargetFrameRate = 120;

        private void Awake()
        {
            ApplyFramePacing();
        }

        private void ApplyFramePacing()
        {
            if (enableVSync)
            {
                QualitySettings.vSyncCount = 1;
                Application.targetFrameRate = -1;
                return;
            }

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = fallbackTargetFrameRate;
        }

        private void OnValidate()
        {
            fallbackTargetFrameRate = Mathf.Max(fallbackTargetFrameRate, 30);
        }
    }
}
