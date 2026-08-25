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
            if (clips == null || clips.Length == 0 || Time.time < nextAllowedImpactTime || impactsPlayed >= 3)
            {
                return;
            }

            float speed = collision.relativeVelocity.magnitude;
            if (speed < 1.1f)
            {
                return;
            }

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null)
            {
                return;
            }

            EnsureSource();
            source.pitch = Random.Range(0.93f, 1.08f);
            source.PlayOneShot(clip, Mathf.Clamp(speed / 12f, 0.08f, 0.34f));
            impactsPlayed++;
            nextAllowedImpactTime = Time.time + 0.08f;
        }

        private void EnsureSource()
        {
            if (source != null) return;
            source = GetComponent<AudioSource>();
            if (source == null) source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0.15f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 0.5f;
            source.maxDistance = 18f;
        }
    }
}
