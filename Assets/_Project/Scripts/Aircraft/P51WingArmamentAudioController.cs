using System;
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

        [Header("3D Audio Mix")]
        [SerializeField, Range(0f, 1f)] private float gunReportVolume = 0.44f;
        [SerializeField, Range(0f, 1f)] private float gunMechanicalVolume = 0.12f;
        [SerializeField, Range(0f, 1f)] private float serviceVolume = 0.44f;
        [SerializeField, Range(0f, 1f)] private float panelVolume = 0.48f;

        private readonly int[] previousAmmo = new int[GunCount];
        private readonly bool[] previousGunInstalled = new bool[GunCount];
        private readonly bool[] previousAmmoInstalled = new bool[GunCount];
        private readonly bool[] previousPanelOpen = new bool[WingCount];
        private readonly float[] nextGunFastenerTime = new float[GunCount];
        private readonly float[] nextAmmoFastenerTime = new float[GunCount];

        private AudioClip gunReportClip;
        private AudioClip gunMechanicalClip;
        private AudioClip[] casingClips;
        private AudioClip panelOpenClip;
        private AudioClip panelCloseClip;
        private AudioClip gunFastenerClip;
        private AudioClip ammoFastenerClip;
        private AudioClip gunInstallThudClip;
        private AudioClip ammoInstallThudClip;
        private float nextCasingScanTime;
        private bool initialized;

        public bool IsConfigured => system != null
            && muzzles != null && muzzles.Length == GunCount
            && CountAssigned(muzzles) == GunCount
            && panelPivots != null && panelPivots.Length == WingCount
            && CountAssigned(panelPivots) == WingCount;

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

            // Reapplying Step 40 intentionally updates older scene instances to the current mix.
            gunReportVolume = 0.44f;
            gunMechanicalVolume = 0.12f;
            serviceVolume = 0.44f;
            panelVolume = 0.48f;
            initialized = false;
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
            if (system == null) return;

            EnsureClips();
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
                int roundsFired = Mathf.Max(0, previousAmmo[stationIndex] - currentAmmo);

                if (roundsFired > 0)
                {
                    Transform muzzle = SafeAt(muzzles, stationIndex);
                    Vector3 position = muzzle != null ? muzzle.position : transform.position;
                    float pitch = 0.955f
                        + stationIndex * 0.012f
                        + UnityEngine.Random.Range(-0.014f, 0.014f);

                    PlayAt(
                        position,
                        gunReportClip,
                        gunReportVolume * Mathf.Clamp(roundsFired, 1, 2),
                        pitch,
                        4.5f,
                        560f,
                        0.34f);

                    PlayAt(
                        position,
                        gunMechanicalClip,
                        gunMechanicalVolume,
                        UnityEngine.Random.Range(0.94f, 1.05f),
                        1.8f,
                        80f,
                        0.08f);
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
                    Transform pivot = SafeAt(panelPivots, wingIndex);
                    Vector3 position = pivot != null ? pivot.position : transform.position;
                    AudioClip clip = currentOpen ? panelOpenClip : panelCloseClip;

                    PlayAt(
                        position,
                        clip,
                        panelVolume,
                        currentOpen
                            ? UnityEngine.Random.Range(0.97f, 1.03f)
                            : UnityEngine.Random.Range(0.94f, 1.00f),
                        1.1f,
                        34f,
                        0f);
                }

                previousPanelOpen[wingIndex] = currentOpen;
            }
        }

        private void HandleServiceAudio()
        {
            for (int stationIndex = 0; stationIndex < GunCount; stationIndex++)
            {
                Transform gunTransform = SafeAt(gunServicePoints, stationIndex);
                P51WingArmamentServicePoint gunPoint = gunTransform != null
                    ? gunTransform.GetComponent<P51WingArmamentServicePoint>()
                    : null;
                HandleFastenerDuringInteraction(
                    gunPoint,
                    gunTransform != null ? gunTransform.position : transform.position,
                    stationIndex,
                    true);

                bool gunInstalled = system.IsGunInstalled(stationIndex);
                if (gunInstalled != previousGunInstalled[stationIndex])
                {
                    PlayAt(
                        gunTransform != null ? gunTransform.position : transform.position,
                        gunInstallThudClip,
                        serviceVolume,
                        gunInstalled
                            ? UnityEngine.Random.Range(0.94f, 1.00f)
                            : UnityEngine.Random.Range(0.84f, 0.91f),
                        1.2f,
                        32f,
                        0f);
                }
                previousGunInstalled[stationIndex] = gunInstalled;

                Transform ammoTransform = SafeAt(ammoServicePoints, stationIndex);
                P51WingArmamentServicePoint ammoPoint = ammoTransform != null
                    ? ammoTransform.GetComponent<P51WingArmamentServicePoint>()
                    : null;
                HandleFastenerDuringInteraction(
                    ammoPoint,
                    ammoTransform != null ? ammoTransform.position : transform.position,
                    stationIndex,
                    false);

                bool ammoInstalled = system.IsAmmoInstalled(stationIndex);
                if (ammoInstalled != previousAmmoInstalled[stationIndex])
                {
                    PlayAt(
                        ammoTransform != null ? ammoTransform.position : transform.position,
                        ammoInstallThudClip,
                        serviceVolume * 0.86f,
                        ammoInstalled
                            ? UnityEngine.Random.Range(0.98f, 1.05f)
                            : UnityEngine.Random.Range(0.90f, 0.97f),
                        0.9f,
                        26f,
                        0f);
                }
                previousAmmoInstalled[stationIndex] = ammoInstalled;
            }
        }

        private void HandleFastenerDuringInteraction(
            P51WingArmamentServicePoint point,
            Vector3 position,
            int stationIndex,
            bool gunFastener)
        {
            if (point == null || !point.IsInteractionInProgress)
            {
                if (gunFastener)
                    nextGunFastenerTime[stationIndex] = 0f;
                else
                    nextAmmoFastenerTime[stationIndex] = 0f;
                return;
            }

            float nextTime = gunFastener
                ? nextGunFastenerTime[stationIndex]
                : nextAmmoFastenerTime[stationIndex];
            if (Time.time < nextTime) return;

            AudioClip clip = gunFastener ? gunFastenerClip : ammoFastenerClip;
            float interval = gunFastener ? 0.115f : 0.145f;
            float volume = serviceVolume * (gunFastener ? 0.88f : 0.62f);
            float basePitch = point.IsRemoving
                ? (gunFastener ? 0.88f : 0.96f)
                : (gunFastener ? 0.96f : 1.04f);

            PlayAt(
                position,
                clip,
                volume,
                basePitch + UnityEngine.Random.Range(-0.025f, 0.025f),
                0.75f,
                gunFastener ? 25f : 20f,
                0f);

            if (gunFastener)
                nextGunFastenerTime[stationIndex] = Time.time + interval;
            else
                nextAmmoFastenerTime[stationIndex] = Time.time + interval;
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
                nextGunFastenerTime[stationIndex] = 0f;
                nextAmmoFastenerTime[stationIndex] = 0f;
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
                CreateCasingClink("P51 Brass Impact A", 5201, 3100f, 5350f, 7600f),
                CreateCasingClink("P51 Brass Impact B", 5202, 2750f, 4800f, 6900f),
                CreateCasingClink("P51 Brass Impact C", 5203, 3450f, 5750f, 8200f)
            };
            panelOpenClip = CreatePanelOpen();
            panelCloseClip = CreatePanelClose();
            gunFastenerClip = CreateGunFastener();
            ammoFastenerClip = CreateAmmoFastener();
            gunInstallThudClip = CreateInstallThud(
                "P51 Gun Mount Seat", 5210, 68f, 230f, 0.34f, 0.92f);
            ammoInstallThudClip = CreateInstallThud(
                "P51 Ammo Box Seat", 5211, 105f, 460f, 0.25f, 0.70f);
        }

        private static AudioClip CreateGunReport()
        {
            const float duration = 0.26f;
            float[] data = NewBuffer(duration);
            System.Random random = new System.Random(5200);

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float blastNoise = RandomBipolar(random) * Mathf.Exp(-t / 0.022f);
                float sub = (
                    Mathf.Sin(2f * Mathf.PI * 62f * t) * 0.62f
                    + Mathf.Sin(2f * Mathf.PI * 96f * t) * 0.48f)
                    * Mathf.Exp(-t / 0.095f);
                float body = (
                    Mathf.Sin(2f * Mathf.PI * 235f * t) * 0.48f
                    + Mathf.Sin(2f * Mathf.PI * 430f * t) * 0.24f)
                    * Mathf.Exp(-t / 0.050f);
                float pressure = Mathf.Sin(2f * Mathf.PI * 880f * t)
                    * Mathf.Exp(-t / 0.017f) * 0.24f;
                float crack = t < 0.0022f
                    ? (1f - t / 0.0022f) * 1.45f
                    : 0f;
                float secondary = t >= 0.006f && t < 0.010f
                    ? (1f - (t - 0.006f) / 0.004f) * 0.48f
                    : 0f;

                data[i] = Saturate((
                    blastNoise * 0.88f
                    + sub
                    + body
                    + pressure
                    + crack
                    + secondary) * 1.18f);
            }

            return CreateClip("P51 Intense Six Gun Report", data);
        }

        private static AudioClip CreateMechanicalAction()
        {
            float[] data = NewBuffer(0.13f);
            System.Random random = new System.Random(5204);
            AddMetalImpulse(data, 0.000f, 720f, 0.78f, random);
            AddMetalImpulse(data, 0.027f, 1120f, 0.66f, random);
            AddMetalImpulse(data, 0.057f, 1680f, 0.43f, random);
            AddLowImpulse(data, 0.000f, 185f, 0.38f);

            for (int i = 0; i < data.Length; i++)
            {
                data[i] = Saturate(data[i] * 0.94f);
            }

            return CreateClip("P51 M2 Heavy Mechanical Action", data);
        }

        private static AudioClip CreateCasingClink(
            string name,
            int seed,
            float firstFrequency,
            float secondFrequency,
            float thirdFrequency)
        {
            float[] data = NewBuffer(0.23f);
            System.Random random = new System.Random(seed);

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float ring1 = Mathf.Sin(2f * Mathf.PI * firstFrequency * t)
                    * Mathf.Exp(-t / 0.060f) * 0.58f;
                float ring2 = Mathf.Sin(2f * Mathf.PI * secondFrequency * t)
                    * Mathf.Exp(-t / 0.042f) * 0.44f;
                float ring3 = Mathf.Sin(2f * Mathf.PI * thirdFrequency * t)
                    * Mathf.Exp(-t / 0.026f) * 0.26f;
                float body = Mathf.Sin(2f * Mathf.PI * 780f * t)
                    * Mathf.Exp(-t / 0.022f) * 0.18f;
                float strike = RandomBipolar(random)
                    * Mathf.Exp(-t / 0.0045f) * 0.48f;

                data[i] = Saturate((ring1 + ring2 + ring3 + body + strike) * 0.92f);
            }

            return CreateClip(name, data);
        }

        private static AudioClip CreatePanelOpen()
        {
            const float duration = 0.36f;
            float[] data = NewBuffer(duration);
            System.Random random = new System.Random(5220);
            float filteredNoise = 0f;

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float phase = Mathf.Clamp01(t / duration);
                filteredNoise = Mathf.Lerp(filteredNoise, RandomBipolar(random), 0.08f);
                float movementEnvelope = Mathf.Pow(Mathf.Sin(Mathf.PI * phase), 0.78f);
                float scrape = filteredNoise * movementEnvelope * 0.24f;
                float sheet = (
                    Mathf.Sin(2f * Mathf.PI * 168f * t) * 0.17f
                    + Mathf.Sin(2f * Mathf.PI * 335f * t) * 0.12f
                    + Mathf.Sin(2f * Mathf.PI * 710f * t) * 0.07f)
                    * movementEnvelope;
                float latch = EventRing(t, 0.000f, 920f, 0.030f, 0.62f)
                    + EventRing(t, 0.004f, 1540f, 0.018f, 0.34f);
                float hingeKnock = EventRing(t, 0.105f, 420f, 0.026f, 0.22f);

                data[i] = Saturate(scrape + sheet + latch + hingeKnock);
            }

            return CreateClip("P51 Metal Wing Panel Opening", data);
        }

        private static AudioClip CreatePanelClose()
        {
            const float duration = 0.34f;
            float[] data = NewBuffer(duration);
            System.Random random = new System.Random(5221);
            float filteredNoise = 0f;

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float phase = Mathf.Clamp01(t / duration);
                filteredNoise = Mathf.Lerp(filteredNoise, RandomBipolar(random), 0.075f);
                float movementEnvelope = Mathf.Sin(Mathf.PI * phase);
                float scrape = filteredNoise * movementEnvelope * 0.22f;
                float sheet = (
                    Mathf.Sin(2f * Mathf.PI * 150f * t) * 0.16f
                    + Mathf.Sin(2f * Mathf.PI * 305f * t) * 0.10f)
                    * movementEnvelope;
                float seat = EventRing(t, 0.215f, 110f, 0.080f, 0.72f)
                    + EventRing(t, 0.215f, 335f, 0.050f, 0.50f)
                    + EventRing(t, 0.218f, 1120f, 0.024f, 0.38f);
                float latch = EventRing(t, 0.255f, 1420f, 0.020f, 0.48f);

                data[i] = Saturate((scrape + sheet + seat + latch) * 1.05f);
            }

            return CreateClip("P51 Metal Wing Panel Closing", data);
        }

        private static AudioClip CreateGunFastener()
        {
            float[] data = NewBuffer(0.105f);
            System.Random random = new System.Random(5230);

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float low = Mathf.Sin(2f * Mathf.PI * 178f * t)
                    * Mathf.Exp(-t / 0.035f) * 0.58f;
                float torque = Mathf.Sin(2f * Mathf.PI * 470f * t)
                    * Mathf.Exp(-t / 0.022f) * 0.46f;
                float click = Mathf.Sin(2f * Mathf.PI * 1160f * t)
                    * Mathf.Exp(-t / 0.010f) * 0.34f;
                float noise = RandomBipolar(random)
                    * Mathf.Exp(-t / 0.009f) * 0.16f;
                data[i] = Saturate((low + torque + click + noise) * 1.08f);
            }

            return CreateClip("P51 Deep Gun Mount Fastener", data);
        }

        private static AudioClip CreateAmmoFastener()
        {
            float[] data = NewBuffer(0.09f);
            System.Random random = new System.Random(5231);

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float body = Mathf.Sin(2f * Mathf.PI * 420f * t)
                    * Mathf.Exp(-t / 0.024f) * 0.36f;
                float latch = Mathf.Sin(2f * Mathf.PI * 1450f * t)
                    * Mathf.Exp(-t / 0.012f) * 0.42f;
                float ring = Mathf.Sin(2f * Mathf.PI * 2600f * t)
                    * Mathf.Exp(-t / 0.016f) * 0.18f;
                float noise = RandomBipolar(random)
                    * Mathf.Exp(-t / 0.008f) * 0.12f;
                data[i] = Saturate(body + latch + ring + noise);
            }

            return CreateClip("P51 Ammo Bay Metal Fastener", data);
        }

        private static AudioClip CreateInstallThud(
            string name,
            int seed,
            float lowFrequency,
            float highFrequency,
            float duration,
            float strength)
        {
            float[] data = NewBuffer(duration);
            System.Random random = new System.Random(seed);

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float low = Mathf.Sin(2f * Mathf.PI * lowFrequency * t)
                    * Mathf.Exp(-t / 0.080f) * 0.74f;
                float high = Mathf.Sin(2f * Mathf.PI * highFrequency * t)
                    * Mathf.Exp(-t / 0.043f) * 0.30f;
                float knock = Mathf.Sin(2f * Mathf.PI * (highFrequency * 2.15f) * t)
                    * Mathf.Exp(-t / 0.018f) * 0.16f;
                float noise = RandomBipolar(random)
                    * Mathf.Exp(-t / 0.024f) * 0.15f;
                data[i] = Saturate((low + high + knock + noise) * strength);
            }

            return CreateClip(name, data);
        }

        private static void AddMetalImpulse(
            float[] data,
            float delay,
            float frequency,
            float amplitude,
            System.Random random)
        {
            int start = Mathf.RoundToInt(delay * SampleRate);
            for (int i = start; i < data.Length; i++)
            {
                float t = (i - start) / (float)SampleRate;
                float envelope = Mathf.Exp(-t / 0.013f);
                float noise = RandomBipolar(random) * 0.22f;
                data[i] += amplitude * envelope *
                    (Mathf.Sin(2f * Mathf.PI * frequency * t)
                    + 0.38f * Mathf.Sin(2f * Mathf.PI * frequency * 1.68f * t)
                    + noise);
            }
        }

        private static void AddLowImpulse(float[] data, float delay, float frequency, float amplitude)
        {
            int start = Mathf.RoundToInt(delay * SampleRate);
            for (int i = start; i < data.Length; i++)
            {
                float t = (i - start) / (float)SampleRate;
                data[i] += Mathf.Sin(2f * Mathf.PI * frequency * t)
                    * Mathf.Exp(-t / 0.045f) * amplitude;
            }
        }

        private static float EventRing(
            float time,
            float eventTime,
            float frequency,
            float decay,
            float amplitude)
        {
            float t = time - eventTime;
            if (t < 0f) return 0f;
            return Mathf.Sin(2f * Mathf.PI * frequency * t)
                * Mathf.Exp(-t / Mathf.Max(0.001f, decay))
                * amplitude;
        }

        private static float[] NewBuffer(float duration)
        {
            return new float[Mathf.Max(1, Mathf.CeilToInt(duration * SampleRate))];
        }

        private static float RandomBipolar(System.Random random)
        {
            return (float)random.NextDouble() * 2f - 1f;
        }

        private static float Saturate(float value)
        {
            return (float)Math.Tanh(value);
        }

        private static AudioClip CreateClip(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(
                name,
                data.Length,
                1,
                SampleRate,
                false);
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

        private static Transform SafeAt(Transform[] values, int index)
        {
            return values != null && index >= 0 && index < values.Length
                ? values[index]
                : null;
        }

        private static int CountAssigned(Transform[] values)
        {
            if (values == null) return 0;
            int count = 0;
            for (int index = 0; index < values.Length; index++)
            {
                if (values[index] != null) count++;
            }
            return count;
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

            float life = clip.length / Mathf.Max(0.5f, Mathf.Abs(source.pitch)) + 0.25f;
            UnityEngine.Object.Destroy(audioObject, life);
        }

        private void OnDestroy()
        {
            DestroyClip(gunReportClip);
            DestroyClip(gunMechanicalClip);
            if (casingClips != null)
            {
                for (int index = 0; index < casingClips.Length; index++)
                {
                    DestroyClip(casingClips[index]);
                }
            }
            DestroyClip(panelOpenClip);
            DestroyClip(panelCloseClip);
            DestroyClip(gunFastenerClip);
            DestroyClip(ammoFastenerClip);
            DestroyClip(gunInstallThudClip);
            DestroyClip(ammoInstallThudClip);
        }

        private static void DestroyClip(AudioClip clip)
        {
            if (clip != null)
            {
                UnityEngine.Object.Destroy(clip);
            }
        }
    }
}
