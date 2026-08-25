using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    public sealed class P51CasingImpactAudio : MonoBehaviour
    {
        private AudioClip[] clips;
        private AudioSource source;
        private int impactsPlayed;
        private float nextAllowedImpactTime;

        public void Configure(AudioClip[] configuredClips)
        {
            clips = configuredClips;
            EnsureSource();
        }

        private void Awake()
        {
            EnsureSource();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (clips == null || clips.Length == 0 || Time.time < nextAllowedImpactTime || impactsPlayed >= 5)
            {
                return;
            }

            float speed = collision.relativeVelocity.magnitude;
            if (speed < 0.75f)
            {
                return;
            }

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null)
            {
                return;
            }

            EnsureSource();
            source.pitch = Random.Range(0.88f, 1.16f);
            float volume = Mathf.Clamp(speed / 8.5f, 0.14f, 0.58f);
            source.PlayOneShot(clip, volume);
            impactsPlayed++;
            nextAllowedImpactTime = Time.time + 0.055f;
        }

        private void EnsureSource()
        {
            if (source != null) return;
            source = GetComponent<AudioSource>();
            if (source == null) source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0.08f;
            source.spread = 18f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 0.65f;
            source.maxDistance = 24f;
        }
    }
}
