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
            if (clips == null || clips.Length == 0 || Time.time < nextAllowedImpactTime || impactsPlayed >= 4)
            {
                return;
            }

            float speed = collision.relativeVelocity.magnitude;
            if (speed < 0.95f)
            {
                return;
            }

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null)
            {
                return;
            }

            EnsureSource();
            source.pitch = Random.Range(0.90f, 1.12f);
            float impactVolume = Mathf.Clamp(speed / 13f, 0.055f, 0.28f);
            source.PlayOneShot(clip, impactVolume);
            impactsPlayed++;
            nextAllowedImpactTime = Time.time + Random.Range(0.055f, 0.095f);
        }

        private void EnsureSource()
        {
            if (source != null) return;
            source = GetComponent<AudioSource>();
            if (source == null) source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.volume = 0.82f;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0.05f;
            source.spread = 8f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;

            // Brass impacts are intentionally very local. A casing hitting the ramp should
            // be audible beside the airplane, but disappear rapidly several metres away.
            source.minDistance = 0.30f;
            source.maxDistance = 7.0f;
        }
    }
}
