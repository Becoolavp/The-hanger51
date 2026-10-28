using UnityEngine;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(430)]
    [DisallowMultipleComponent]
    public sealed class P51MerlinAudioPresenceBoost : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float volumeMultiplier = 2.15f;
        [SerializeField, Min(1f)] private float minimumFullVolumeDistance = 16f;
        [SerializeField, Min(25f)] private float maximumAudibleDistance = 700f;
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 0.96f;

        private float nextRefreshTime;
        private AudioSource[] merlinSources = new AudioSource[0];

        public void Configure(float gain, float minDistance, float maxDistance)
        {
            volumeMultiplier = Mathf.Max(1f, gain);
            minimumFullVolumeDistance = Mathf.Max(1f, minDistance);
            maximumAudibleDistance = Mathf.Max(minimumFullVolumeDistance + 1f, maxDistance);
            RefreshSources();
        }

        private void OnEnable()
        {
            nextRefreshTime = 0f;
            RefreshSources();
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= nextRefreshTime)
            {
                nextRefreshTime = Time.unscaledTime + 0.50f;
                RefreshSources();
            }

            for (int index = 0; index < merlinSources.Length; index++)
            {
                AudioSource source = merlinSources[index];
                if (source == null) continue;

                source.minDistance = minimumFullVolumeDistance;
                source.maxDistance = maximumAudibleDistance;
                source.spatialBlend = spatialBlend;

                float sourceGain = source.gameObject.name.Contains("Starter")
                    ? 1.35f
                    : source.gameObject.name.Contains("Rough")
                        ? 1.65f
                        : volumeMultiplier;
                source.volume = Mathf.Clamp01(source.volume * sourceGain);
            }
        }

        private void RefreshSources()
        {
            AudioSource[] all = GetComponentsInChildren<AudioSource>(true);
            int count = 0;
            for (int index = 0; index < all.Length; index++)
            {
                AudioSource source = all[index];
                if (source != null && source.gameObject.name.StartsWith("Merlin ")) count++;
            }

            merlinSources = new AudioSource[count];
            int write = 0;
            for (int index = 0; index < all.Length; index++)
            {
                AudioSource source = all[index];
                if (source == null || !source.gameObject.name.StartsWith("Merlin ")) continue;
                merlinSources[write++] = source;
            }
        }

        private void OnValidate()
        {
            volumeMultiplier = Mathf.Max(1f, volumeMultiplier);
            minimumFullVolumeDistance = Mathf.Max(1f, minimumFullVolumeDistance);
            maximumAudibleDistance = Mathf.Max(minimumFullVolumeDistance + 1f, maximumAudibleDistance);
        }
    }
}
