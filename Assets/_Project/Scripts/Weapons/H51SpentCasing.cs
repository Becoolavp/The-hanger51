using UnityEngine;

namespace Hanger51.Weapons
{
    [DisallowMultipleComponent]
    public sealed class H51SpentCasing : MonoBehaviour
    {
        AudioClip[] impactClips = System.Array.Empty<AudioClip>();
        AudioSource source;
        float nextImpactSound;
        bool initialized;

        public void Initialize(AudioClip[] clips)
        {
            impactClips = clips ?? System.Array.Empty<AudioClip>();

            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = .35f;
            source.maxDistance = 16f;
            source.dopplerLevel = .15f;
            source.volume = .34f;

            initialized = true;
            Destroy(gameObject,12f);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!initialized ||
                Time.time < nextImpactSound ||
                impactClips == null ||
                impactClips.Length == 0)
                return;

            float speed =
                collision.relativeVelocity.magnitude;

            if (speed < .45f)
                return;

            AudioClip clip =
                impactClips[
                    Random.Range(
                        0,
                        impactClips.Length)];

            if (!clip)
                return;

            nextImpactSound =
                Time.time +
                Random.Range(.06f,.12f);

            source.pitch =
                Random.Range(.92f,1.08f);

            source.PlayOneShot(
                clip,
                Mathf.Clamp01(
                    Mathf.InverseLerp(
                        .4f,
                        4.5f,
                        speed)));
        }
    }
}
