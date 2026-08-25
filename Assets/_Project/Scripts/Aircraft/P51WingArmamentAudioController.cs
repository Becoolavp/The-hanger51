using System;
using System.Collections;
using UnityEngine;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(360)]
    [DisallowMultipleComponent]
    public sealed class P51WingArmamentAudioController : MonoBehaviour
    {
        private const int GunCount = 6;
        private const int WingCount = 2;
        private const int SampleRate = 48000;

        [SerializeField] private P51WingArmamentSystem system;
        [SerializeField] private Transform[] muzzles = new Transform[GunCount];
        [SerializeField] private Transform[] ejectionPorts = new Transform[GunCount];
        [SerializeField] private Transform[] panelPivots = new Transform[WingCount];
        [SerializeField] private Transform[] gunServicePoints = new Transform[GunCount];
        [SerializeField] private Transform[] ammoServicePoints = new Transform[GunCount];

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] private float gunReportVolume = 0.24f;
        [SerializeField, Range(0f, 1f)] private float gunMechanicalVolume = 0.075f;
        [SerializeField, Range(0f, 1f)] private float serviceVolume = 0.34f;
        [SerializeField, Range(0f, 1f)] private float panelVolume = 0.32f;

        private readonly int[] previousAmmo = new int[GunCount];
        private readonly bool[] previousGunInstalled = new bool[GunCount];
        private readonly bool[] previousAmmoInstalled = new bool[GunCount];
        private readonly bool[] previousPanelOpen = new bool[WingCount];

        private AudioClip gunReportClip;
        private AudioClip gunMechanicalClip;
        private AudioClip[] casingClips;
        private AudioClip panelHingeClip;
        private AudioClip panelLatchClip;
        private AudioClip boltRatchetClip;
        private AudioClip gunInstallThudClip;
        private AudioClip ammoInstallThudClip;
        private float nextCasingScanTime;
        private bool initialized;

        public bool IsConfigured => system != null
            && muzzles != null && muzzles.Length == GunCount
            && panelPivots != null && panelPivots.Length == WingCount;

        public void Configure(
            P51WingArmamentSystem configuredSystem,
            Transform[] configuredMuzzles,
            Transform[] configuredEjectionPorts,
            Transform[] configuredPanelPivots,
            Transform[] configuredGunServicePoints,
            Transform[] configuredAmmoServicePoints)
        {
            system = configuredSystem;
            muzzles = Copy(configuredMuzzles, GunCount);
            ejectionPorts = Copy(configuredEjectionPorts, GunCount);
            panelPivots = Copy(configuredPanelPivots, WingCount);
            gunServicePoints = Copy(configuredGunServicePoints, GunCount);
            ammoServicePoints = Copy(configuredAmmoServicePoints, GunCount);
            EnsureClips();
            CaptureState();
        }

        private void Awake()
        {
            ResolveReferences();
            EnsureClips();
            CaptureState();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureClips();
            CaptureState();
            nextCasingScanTime = 0f;
        }

        private void Update()
        {
            ResolveReferences();
            EnsureClips();
            if (system == null) return;
            if (!initialized) CaptureState();

            HandleGunfireAudio();
            HandlePanelAudio();
            HandleServiceAudio();
            AttachCasingImpactAudio();
        }

        private void HandleGunfireAudio()
        {
            for (int stationIndex = 0; stationIndex < GunCount; stationIndex++)
            {
                int currentAmmo = system.GetAmmoRemaining(stationIndex);
                int fired = Mathf.Max(0, previousAmmo[stationIndex] - currentAmmo);
                if (fired > 0)
                {
                    Transform muzzle = stationIndex < muzzles.Length ? muzzles[stationIndex] : null;
                    Vector3 position = muzzle != null ? muzzle.position : transform.position;
                    float stationPitch = 0.965f + stationIndex * 0.012f + UnityEngine.Random.Range(-0.012f, 0.012f);

                    PlayAt(
                        position,
                        gunReportClip,
                        gunReportVolume * Mathf.Clamp(fired, 1, 2),
                        stationPitch,
                        3.5f,
                        420f,
                        0.30f);
                    PlayAt(
                        position,
                        gunMechanicalClip,
                        gunMechanicalVolume,
                        UnityEngine.Random.Range(0.96f, 1.05f),
                        1.5f,
                        65f,
                        0.05f);
                }

                previousAmmo[stationIndex] = currentAmmo;
            }
        }

        private void HandlePanelAudio()
        {
            for (int wingIndex = 0; wingIndex < WingCount; wingIndex++)
            {
                bool currentOpen = system.IsPanelOpen(wingIndex);
                if (currentOpen != previousPanelOpen[wingIndex])
                {
                    Transform pivot = wingIndex < panelPivots.Length ? panelPivots[wingIndex] : null;
                    Vector3 position = pivot != null ? pivot.position : transform.position;
                    PlayAt(
                        position,
                        panelHingeClip,
                        panelVolume,
                        currentOpen ? UnityEngine.Random.Range(0.96f, 1.02f) : UnityEngine.Random.Range(0.90f, 0.97f),
                        1.0f,
                        28f,
                        0f);
                    StartCoroutine(PlayPanelLatchAfter(position, 0.20f, currentOpen));
                }
                previousPanelOpen[wingIndex] = currentOpen;
            }
        }

        private void HandleServiceAudio()
        {
            for (int stationIndex = 0; stationIndex < GunCount; stationIndex++)
            {
                bool gunInstalled = system.IsGunInstalled(stationIndex);
                if (gunInstalled != previousGunInstalled[stationIndex])
                {
                    Transform point = stationIndex < gunServicePoints.Length ? gunServicePoints[stationIndex] : null;
                    StartCoroutine(PlayServiceSequence(
                        point != null ? point.position : transform.position,
                        gunInstallThudClip,
                        gunInstalled ? 1.0f : 0.91f));
                }
                previousGunInstalled[stationIndex] = gunInstalled;

                bool ammoInstalled = system.IsAmmoInstalled(stationIndex);
                if (ammoInstalled != previousAmmoInstalled[stationIndex])
                {
                    Transform point = stationIndex < ammoServicePoints.Length ? ammoServicePoints[stationIndex] : null;
                    StartCoroutine(PlayServiceSequence(
                        point != null ? point.position : transform.position,
                        ammoInstallThudClip,
                        ammoInstalled ? 1.05f : 0.95f));
                }
                previousAmmoInstalled[stationIndex] = ammoInstalled;
            }
        }

        private IEnumerator PlayPanelLatchAfter(Vector3 position, float delay, bool opening)
        {
            yield return new WaitForSeconds(delay);
            PlayAt(
                position,
                panelLatchClip,
                panelVolume * 0.82f,
                opening ? UnityEngine.Random.Range(1.00f, 1.08f) : UnityEngine.Random.Range(0.92f, 1.00f),
                0.8f,
                22f,
                0f);
        }

        private IEnumerator PlayServiceSequence(Vector3 position, AudioClip thudClip, float pitch)
        {
            PlayAt(
                position,
                boltRatchetClip,
                serviceVolume * 0.72f,
                UnityEngine.Random.Range(0.95f, 1.06f),
                0.8f,
                20f,
                0f);
            yield return new WaitForSeconds(0.12f);
            PlayAt(
                position,
                thudClip,
                serviceVolume,
                pitch + UnityEngine.Random.Range(-0.025f, 0.025f),
                1.0f,
                30f,
                0f);
        }

        private void AttachCasingImpactAudio()
        {
            if (Time.time < nextCasingScanTime) return;
            nextCasingScanTime = Time.time + 0.10f;

            Rigidbody[] bodies = FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
            for (int index = 0; index < bodies.Length; index++)
            {
                Rigidbody body = bodies[index];
                if (body == null || body.gameObject.name != "Spent Wing Gun Casing") continue;
                if (body.GetComponent<P51CasingImpactAudio>() != null) continue;

                P51CasingImpactAudio impact = body.gameObject.AddComponent<P51CasingImpactAudio>();
                impact.Configure(casingClips);
            }
        }

        private void CaptureState()
        {
            if (system == null) return;
            for (int stationIndex = 0; stationIndex < GunCount; stationIndex++)
            {
                previousAmmo[stationIndex] = system.GetAmmoRemaining(stationIndex);
                previousGunInstalled[stationIndex] = system.IsGunInstalled(stationIndex);
                previousAmmoInstalled[stationIndex] = system.IsAmmoInstalled(stationIndex);
            }
            for (int wingIndex = 0; wingIndex < WingCount; wingIndex++)
            {
                previousPanelOpen[wingIndex] = system.IsPanelOpen(wingIndex);
            }
            initialized = true;
        }

        private void ResolveReferences()
        {
            if (system == null) system = GetComponent<P51WingArmamentSystem>();
        }

        private void EnsureClips()
        {
            if (gunReportClip != null) return;

            gunReportClip = CreateGunReport();
            gunMechanicalClip = CreateMechanicalAction();
            casingClips = new[]
            {
                CreateCasingClink("P51 Casing Clink A", 5101, 2200f, 4100f),
                CreateCasingClink("P51 Casing Clink B", 5102, 1800f, 3600f),
                CreateCasingClink("P51 Casing Clink C", 5103, 2500f, 4700f)
            };
            panelHingeClip = CreatePanelHinge();
            panelLatchClip = CreatePanelLatch();
            boltRatchetClip = CreateBoltRatchet();
            gunInstallThudClip = CreateInstallThud("P51 Gun Install Thud", 5110, 82f, 255f, 0.32f);
            ammoInstallThudClip = CreateInstallThud("P51 Ammo Install Thud", 5111, 112f, 510f, 0.25f);
        }

        private static AudioClip CreateGunReport()
        {
            const float duration = 0.20f;
            int count = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[count];
            System.Random random = new System.Random(5150);

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float noise = ((float)random.NextDouble() * 2f - 1f) * Mathf.Exp(-t / 0.018f);
                float boom = Mathf.Sin(2f * Mathf.PI * 82f * t) * Mathf.Exp(-t / 0.060f);
                float bark = Mathf.Sin(2f * Mathf.PI * 215f * t) * Mathf.Exp(-t / 0.032f);
                float crack = t < 0.0035f ? (1f - t / 0.0035f) * 0.95f : 0f;
                data[i] = Mathf.Tanh(noise * 0.56f + boom * 0.46f + bark * 0.22f + crack);
            }

            return CreateClip("P51 Six Gun Heavy Report", data);
        }

        private static AudioClip CreateMechanicalAction()
        {
            const float duration = 0.12f;
            int count = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[count];
            System.Random random = new System.Random(5151);
            AddMetalImpulse(data, 0.000f, 1450f, 1.00f, random);
            AddMetalImpulse(data, 0.032f, 930f, 0.65f, random);
            AddMetalImpulse(data, 0.064f, 1650f, 0.34f, random);
            for (int i = 0; i < data.Length; i++) data[i] = Mathf.Tanh(data[i] * 0.82f);
            return CreateClip("P51 M2 Mechanical Action", data);
        }

        private static void AddMetalImpulse(float[] data, float delay, float frequency, float amplitude, System.Random random)
        {
            int start = Mathf.RoundToInt(delay * SampleRate);
            for (int i = start; i < data.Length; i++)
            {
                float t = (i - start) / (float)SampleRate;
                float envelope = Mathf.Exp(-t / 0.012f);
                float noise = ((float)random.NextDouble() * 2f - 1f) * 0.28f;
                data[i] += amplitude * envelope *
                    (Mathf.Sin(2f * Mathf.PI * frequency * t)
                    + 0.45f * Mathf.Sin(2f * Mathf.PI * frequency * 1.72f * t)
                    + noise);
            }
        }

        private static AudioClip CreateCasingClink(string name, int seed, float firstFrequency, float secondFrequency)
        {
            const float duration = 0.18f;
            int count = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[count];
            System.Random random = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = Mathf.Exp(-t / 0.035f);
                float noise = ((float)random.NextDouble() * 2f - 1f) * 0.11f * Mathf.Exp(-t / 0.018f);
                data[i] = Mathf.Tanh(
                    (Mathf.Sin(2f * Mathf.PI * firstFrequency * t)
                    + 0.42f * Mathf.Sin(2f * Mathf.PI * secondFrequency * t))
                    * envelope * 0.58f + noise);
            }
            return CreateClip(name, data);
        }

        private static AudioClip CreatePanelHinge()
        {
            const float duration = 0.62f;
            int count = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[count];
            System.Random random = new System.Random(5160);
            float smoothedNoise = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float phase = Mathf.Clamp01(t / duration);
                float envelope = Mathf.Pow(Mathf.Sin(Mathf.PI * phase), 1.35f);
                float rawNoise = (float)random.NextDouble() * 2f - 1f;
                smoothedNoise = Mathf.Lerp(smoothedNoise, rawNoise, 0.09f);
                float squeakFrequency = 510f + 95f * Mathf.Sin(2f * Mathf.PI * 2.1f * t);
                float squeak = Mathf.Sin(2f * Mathf.PI * squeakFrequency * t);
                float groan = Mathf.Sin(2f * Mathf.PI * 122f * t);
                data[i] = (smoothedNoise * 0.19f + squeak * 0.11f + groan * 0.055f) * envelope;
            }
            return CreateClip("P51 Wing Panel Hinge", data);
        }

        private static AudioClip CreatePanelLatch()
        {
            const float duration = 0.16f;
            int count = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[count];
            System.Random random = new System.Random(5161);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float high = Mathf.Sin(2f * Mathf.PI * 1160f * t) * Mathf.Exp(-t / 0.016f) * 0.68f;
                float low = Mathf.Sin(2f * Mathf.PI * 285f * t) * Mathf.Exp(-t / 0.050f) * 0.24f;
                float noise = ((float)random.NextDouble() * 2f - 1f) * Mathf.Exp(-t / 0.009f) * 0.18f;
                data[i] = Mathf.Tanh(high + low + noise);
            }
            return CreateClip("P51 Wing Panel Latch", data);
        }

        private static AudioClip CreateBoltRatchet()
        {
            const float duration = 0.42f;
            int count = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[count];
            System.Random random = new System.Random(5170);
            for (float delay = 0f; delay < 0.40f; delay += 0.055f)
            {
                int start = Mathf.RoundToInt(delay * SampleRate);
                for (int i = start; i < count; i++)
                {
                    float t = (i - start) / (float)SampleRate;
                    float envelope = Mathf.Exp(-t / 0.009f);
                    float noise = ((float)random.NextDouble() * 2f - 1f) * 0.25f;
                    data[i] += (Mathf.Sin(2f * Mathf.PI * 1500f * t) + noise) * envelope * 0.42f;
                }
            }
            for (int i = 0; i < data.Length; i++) data[i] = Mathf.Tanh(data[i]);
            return CreateClip("P51 Armament Bolt Ratchet", data);
        }

        private static AudioClip CreateInstallThud(string name, int seed, float lowFrequency, float highFrequency, float duration)
        {
            int count = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[count];
            System.Random random = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float low = Mathf.Sin(2f * Mathf.PI * lowFrequency * t) * Mathf.Exp(-t / 0.070f) * 0.68f;
                float high = Mathf.Sin(2f * Mathf.PI * highFrequency * t) * Mathf.Exp(-t / 0.040f) * 0.24f;
                float noise = ((float)random.NextDouble() * 2f - 1f) * Mathf.Exp(-t / 0.022f) * 0.15f;
                data[i] = Mathf.Tanh(low + high + noise);
            }
            return CreateClip(name, data);
        }

        private static AudioClip CreateClip(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static Transform[] Copy(Transform[] source, int length)
        {
            Transform[] result = new Transform[length];
            if (source != null)
            {
                Array.Copy(source, result, Mathf.Min(source.Length, length));
            }
            return result;
        }

        private static void PlayAt(
            Vector3 position,
            AudioClip clip,
            float volume,
            float pitch,
            float minDistance,
            float maxDistance,
            float doppler)
        {
            if (clip == null || volume <= 0f) return;

            GameObject audioObject = new GameObject($"P-51 Audio - {clip.name}");
            audioObject.transform.position = position;
            AudioSource source = audioObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = false;
            source.loop = false;
            source.volume = Mathf.Clamp01(volume);
            source.pitch = Mathf.Clamp(pitch, 0.5f, 1.5f);
            source.spatialBlend = 1f;
            source.dopplerLevel = Mathf.Max(0f, doppler);
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = Mathf.Max(0.1f, minDistance);
            source.maxDistance = Mathf.Max(source.minDistance + 0.1f, maxDistance);
            source.Play();
            Destroy(audioObject, clip.length / Mathf.Max(0.5f, Mathf.Abs(source.pitch)) + 0.25f);
        }

        private void OnDestroy()
        {
            DestroyClip(gunReportClip);
            DestroyClip(gunMechanicalClip);
            if (casingClips != null)
            {
                for (int i = 0; i < casingClips.Length; i++) DestroyClip(casingClips[i]);
            }
            DestroyClip(panelHingeClip);
            DestroyClip(panelLatchClip);
            DestroyClip(boltRatchetClip);
            DestroyClip(gunInstallThudClip);
            DestroyClip(ammoInstallThudClip);
        }

        private static void DestroyClip(AudioClip clip)
        {
            if (clip != null) Destroy(clip);
        }
    }
}
