using System;
using System.Collections.Generic;
using Hanger51.EngineAssembly;
using UnityEngine;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(260)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(P51FlightController))]
    [RequireComponent(typeof(P51MerlinLifecycleController))]
    public sealed class P51MerlinAudioAndExhaustFxController : MonoBehaviour
    {
        private const int SampleRate = 48000;

        [Header("Engine Mix")]
        [SerializeField, Range(0f, 1f)] private float rumbleVolume = 0.72f;
        [SerializeField, Range(0f, 1f)] private float combustionVolume = 0.58f;
        [SerializeField, Range(0f, 1f)] private float roughRunningVolume = 0.68f;
        [SerializeField, Range(0f, 1f)] private float starterVolume = 0.52f;

        [Header("3D Range")]
        [SerializeField, Min(0.5f)] private float minDistance = 4.0f;
        [SerializeField, Min(25f)] private float maxDistance = 360f;

        private P51FlightController flightController;
        private P51MerlinLifecycleController lifecycle;
        private P51EngineConditionPowerBridge conditionBridge;

        private AudioSource rumbleSource;
        private AudioSource combustionSource;
        private AudioSource roughSource;
        private AudioSource starterSource;
        private AudioClip rumbleClip;
        private AudioClip combustionClip;
        private AudioClip roughClip;
        private AudioClip starterClip;
        private AudioClip coughClip;
        private AudioClip hardMisfireClip;

        private readonly List<ParticleSystem> exhaustSystems = new List<ParticleSystem>();
        private readonly List<Transform> exhaustAnchors = new List<Transform>();
        private Material exhaustParticleMaterial;
        private float nextStartupBurstTime;
        private float nextStartupCoughTime;
        private float nextMisfireTime;
        private bool wasStarting;

        public int ExhaustOutletCount => exhaustAnchors.Count;

        private void Awake()
        {
            ResolveReferences();
            CreateAudio();
            CreateExhaustEffects();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureAudioPlaying();
            nextStartupBurstTime = 0f;
            nextStartupCoughTime = 0f;
            nextMisfireTime = 0f;
        }

        private void Update()
        {
            ResolveReferences();
            if (flightController == null || lifecycle == null)
            {
                SilenceAll();
                return;
            }

            EnsureAudioPlaying();
            UpdateEngineMix();
            UpdateStartupExhaust();
            UpdateDamageMisfires();
            wasStarting = lifecycle.IsStarting;
        }

        private void UpdateEngineMix()
        {
            float throttle = Mathf.Clamp01(flightController.Throttle);
            float roughness = conditionBridge != null && conditionBridge.ActiveCondition != null
                ? conditionBridge.ActiveCondition.RoughRunningSeverity
                : 0f;
            float power = conditionBridge != null
                ? Mathf.Clamp01(conditionBridge.AvailablePowerMultiplier)
                : 1f;

            float activity;
            float rpmPitch;
            if (lifecycle.IsStarting)
            {
                float p = lifecycle.TransitionNormalized;
                activity = Mathf.Lerp(0.22f, 0.78f, p);
                rpmPitch = Mathf.Lerp(0.48f, 0.82f, p);
                starterSource.volume = starterVolume * Mathf.Lerp(1f, 0.28f, p);
                starterSource.pitch = Mathf.Lerp(0.78f, 1.12f, p);
            }
            else if (lifecycle.IsRunning && flightController.EngineRunning)
            {
                activity = 1f;
                rpmPitch = Mathf.Lerp(0.82f, 1.62f, throttle);
                starterSource.volume = 0f;
            }
            else if (lifecycle.IsStopping)
            {
                float remaining = 1f - lifecycle.TransitionNormalized;
                activity = remaining;
                rpmPitch = Mathf.Lerp(0.42f, 0.88f, remaining);
                starterSource.volume = 0f;
            }
            else
            {
                SilenceAll();
                return;
            }

            float healthyTone = Mathf.Lerp(0.82f, 1f, power);
            rumbleSource.pitch = rpmPitch * Mathf.Lerp(0.94f, 1.02f, healthyTone);
            rumbleSource.volume = rumbleVolume
                * activity
                * Mathf.Lerp(0.58f, 1f, throttle)
                * Mathf.Lerp(0.92f, 1f, healthyTone);

            combustionSource.pitch = rpmPitch * Mathf.Lerp(0.97f, 1.06f, throttle);
            combustionSource.volume = combustionVolume
                * activity
                * Mathf.Lerp(0.36f, 1f, throttle)
                * Mathf.Lerp(0.76f, 1f, power);

            roughSource.pitch = Mathf.Lerp(0.74f, 1.18f, throttle);
            roughSource.volume = roughRunningVolume
                * activity
                * Mathf.Clamp01(roughness * 1.18f);
        }

        private void UpdateStartupExhaust()
        {
            if (!lifecycle.IsStarting)
            {
                return;
            }

            float progress = lifecycle.TransitionNormalized;
            if (!wasStarting)
            {
                nextStartupBurstTime = Time.time;
                nextStartupCoughTime = Time.time + 0.35f;
            }

            if (Time.time >= nextStartupBurstTime)
            {
                nextStartupBurstTime = Time.time + UnityEngine.Random.Range(0.075f, 0.14f);
                EmitStartupBurst(progress);
            }

            if (progress > 0.28f && Time.time >= nextStartupCoughTime)
            {
                nextStartupCoughTime = Time.time + UnityEngine.Random.Range(0.32f, 0.62f);
                PlayAt(
                    AverageExhaustPosition(),
                    coughClip,
                    Mathf.Lerp(0.16f, 0.34f, progress),
                    UnityEngine.Random.Range(0.90f, 1.08f),
                    3f,
                    120f);
            }
        }

        private void EmitStartupBurst(float progress)
        {
            if (exhaustSystems.Count == 0)
            {
                return;
            }

            for (int index = 0; index < exhaustSystems.Count; index++)
            {
                ParticleSystem system = exhaustSystems[index];
                Transform anchor = index < exhaustAnchors.Count ? exhaustAnchors[index] : null;
                if (system == null || anchor == null)
                {
                    continue;
                }

                float smokeChance = Mathf.Lerp(0.92f, 0.28f, progress);
                if (UnityEngine.Random.value < smokeChance)
                {
                    ParticleSystem.EmitParams smoke = new ParticleSystem.EmitParams
                    {
                        position = anchor.position,
                        velocity = anchor.forward * UnityEngine.Random.Range(0.45f, 1.25f)
                            + Vector3.up * UnityEngine.Random.Range(0.08f, 0.28f),
                        startLifetime = UnityEngine.Random.Range(0.65f, 1.25f),
                        startSize = UnityEngine.Random.Range(0.075f, 0.14f),
                        startColor = new Color(0.16f, 0.17f, 0.18f, UnityEngine.Random.Range(0.34f, 0.58f))
                    };
                    system.Emit(smoke, 1);
                }

                if (progress > 0.22f && UnityEngine.Random.value < Mathf.Lerp(0.18f, 0.66f, progress))
                {
                    ParticleSystem.EmitParams flame = new ParticleSystem.EmitParams
                    {
                        position = anchor.position,
                        velocity = anchor.forward * UnityEngine.Random.Range(1.6f, 3.1f),
                        startLifetime = UnityEngine.Random.Range(0.055f, 0.13f),
                        startSize = UnityEngine.Random.Range(0.045f, 0.085f),
                        startColor = Color.Lerp(
                            new Color(1f, 0.18f, 0.015f, 1f),
                            new Color(1f, 0.82f, 0.20f, 1f),
                            UnityEngine.Random.value)
                    };
                    system.Emit(flame, 1);
                }
            }
        }

        private void UpdateDamageMisfires()
        {
            if (!lifecycle.IsRunning || !flightController.EngineRunning)
            {
                return;
            }

            EngineConditionController condition = conditionBridge != null
                ? conditionBridge.ActiveCondition
                : null;
            float roughness = condition != null ? condition.RoughRunningSeverity : 0f;
            if (roughness < 0.34f || Time.time < nextMisfireTime)
            {
                return;
            }

            nextMisfireTime = Time.time + Mathf.Lerp(2.25f, 0.48f, roughness)
                * UnityEngine.Random.Range(0.72f, 1.28f);
            PlayAt(
                AverageExhaustPosition(),
                hardMisfireClip,
                Mathf.Lerp(0.12f, 0.48f, roughness),
                UnityEngine.Random.Range(0.82f, 1.08f),
                3f,
                180f);

            if (roughness > 0.58f)
            {
                EmitDamagePuff(roughness);
            }
        }

        private void EmitDamagePuff(float roughness)
        {
            if (exhaustSystems.Count == 0)
            {
                return;
            }

            int first = UnityEngine.Random.Range(0, exhaustSystems.Count);
            int count = roughness > 0.82f ? 4 : 2;
            for (int offset = 0; offset < count; offset++)
            {
                int index = (first + offset) % exhaustSystems.Count;
                ParticleSystem system = exhaustSystems[index];
                Transform anchor = exhaustAnchors[index];
                if (system == null || anchor == null) continue;

                ParticleSystem.EmitParams smoke = new ParticleSystem.EmitParams
                {
                    position = anchor.position,
                    velocity = anchor.forward * UnityEngine.Random.Range(0.7f, 1.6f) + Vector3.up * 0.22f,
                    startLifetime = UnityEngine.Random.Range(0.55f, 1.0f),
                    startSize = UnityEngine.Random.Range(0.07f, 0.13f),
                    startColor = new Color(0.08f, 0.08f, 0.075f, 0.62f)
                };
                system.Emit(smoke, 1);
            }
        }

        private void CreateAudio()
        {
            if (rumbleClip != null)
            {
                return;
            }

            rumbleClip = CreateRumbleClip();
            combustionClip = CreateCombustionClip();
            roughClip = CreateRoughClip();
            starterClip = CreateStarterClip();
            coughClip = CreateCoughClip("Merlin Startup Cough", 7701, 0.22f, 0.72f);
            hardMisfireClip = CreateCoughClip("Merlin Damaged Misfire", 7702, 0.30f, 0.92f);

            rumbleSource = CreateLoopSource("Merlin Deep Rumble", rumbleClip);
            combustionSource = CreateLoopSource("Merlin Combustion", combustionClip);
            roughSource = CreateLoopSource("Merlin Rough Running", roughClip);
            starterSource = CreateLoopSource("Merlin Starter", starterClip);
            EnsureAudioPlaying();
        }

        private AudioSource CreateLoopSource(string sourceName, AudioClip clip)
        {
            GameObject sourceObject = new GameObject(sourceName);
            sourceObject.transform.SetParent(transform, false);
            sourceObject.transform.localPosition = new Vector3(0f, 1.50f, 2.70f);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.volume = 0f;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0.28f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            return source;
        }

        private void EnsureAudioPlaying()
        {
            AudioSource[] sources = { rumbleSource, combustionSource, roughSource, starterSource };
            for (int index = 0; index < sources.Length; index++)
            {
                if (sources[index] != null && !sources[index].isPlaying)
                {
                    sources[index].Play();
                }
            }
        }

        private void CreateExhaustEffects()
        {
            if (exhaustAnchors.Count > 0)
            {
                return;
            }

            Transform[] all = GetComponentsInChildren<Transform>(true);
            List<Transform> stacks = new List<Transform>();
            for (int index = 0; index < all.Length; index++)
            {
                Transform candidate = all[index];
                if (candidate != null && candidate.name.Contains("Exhaust Stack", StringComparison.Ordinal))
                {
                    stacks.Add(candidate);
                }
            }
            stacks.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            exhaustParticleMaterial = CreateParticleMaterial();
            for (int index = 0; index < stacks.Count; index++)
            {
                Transform stack = stacks[index];
                GameObject anchorObject = new GameObject($"Startup Exhaust FX {stack.name}");
                Transform anchor = anchorObject.transform;
                anchor.SetParent(transform, true);
                anchor.position = stack.TransformPoint(Vector3.up * 1.03f);
                anchor.rotation = Quaternion.LookRotation(stack.up, transform.up);
                exhaustAnchors.Add(anchor);

                ParticleSystem particle = anchorObject.AddComponent<ParticleSystem>();
                ParticleSystem.MainModule main = particle.main;
                main.loop = false;
                main.duration = 0.20f;
                main.startLifetime = 0.4f;
                main.startSpeed = 0f;
                main.startSize = 0.08f;
                main.maxParticles = 96;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = -0.03f;

                ParticleSystem.EmissionModule emission = particle.emission;
                emission.enabled = false;

                ParticleSystem.ShapeModule shape = particle.shape;
                shape.enabled = false;

                ParticleSystemRenderer renderer = particle.GetComponent<ParticleSystemRenderer>();
                if (renderer != null && exhaustParticleMaterial != null)
                {
                    renderer.sharedMaterial = exhaustParticleMaterial;
                }
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                exhaustSystems.Add(particle);
            }
        }

        private Material CreateParticleMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;

            Material material = new Material(shader)
            {
                name = "P-51 Runtime Exhaust Flame Smoke Material",
                color = Color.white
            };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            return material;
        }

        private Vector3 AverageExhaustPosition()
        {
            if (exhaustAnchors.Count == 0)
            {
                return transform.position + transform.forward * 2.4f + transform.up * 1.5f;
            }

            Vector3 total = Vector3.zero;
            int count = 0;
            for (int index = 0; index < exhaustAnchors.Count; index++)
            {
                if (exhaustAnchors[index] == null) continue;
                total += exhaustAnchors[index].position;
                count++;
            }
            return count > 0 ? total / count : transform.position;
        }

        private static AudioClip CreateRumbleClip()
        {
            float[] data = NewBuffer(1f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float value = Mathf.Sin(2f * Mathf.PI * 32f * t) * 0.62f
                    + Mathf.Sin(2f * Mathf.PI * 64f * t) * 0.31f
                    + Mathf.Sin(2f * Mathf.PI * 96f * t) * 0.17f
                    + Mathf.Sin(2f * Mathf.PI * 160f * t) * 0.08f;
                data[i] = Saturate(value * 1.35f) * 0.82f;
            }
            return CreateClip("Merlin V12 Deep Rumble Loop", data);
        }

        private static AudioClip CreateCombustionClip()
        {
            float[] data = NewBuffer(1f);
            System.Random random = new System.Random(7710);
            float filteredNoise = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                filteredNoise = Mathf.Lerp(filteredNoise, RandomBipolar(random), 0.045f);
                float pulse = Mathf.Sin(2f * Mathf.PI * 48f * t)
                    + 0.42f * Mathf.Sin(2f * Mathf.PI * 96f * t)
                    + 0.20f * Mathf.Sin(2f * Mathf.PI * 240f * t);
                data[i] = Saturate(pulse * 0.48f + filteredNoise * 0.17f) * 0.72f;
            }
            return CreateClip("Merlin V12 Combustion Loop", data);
        }

        private static AudioClip CreateRoughClip()
        {
            float[] data = NewBuffer(1f);
            System.Random random = new System.Random(7711);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float uneven = 0.48f + 0.52f * Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 5f * t));
                float knock = Mathf.Sin(2f * Mathf.PI * 74f * t) * 0.55f
                    + Mathf.Sin(2f * Mathf.PI * 133f * t) * 0.24f;
                float rattle = RandomBipolar(random) * 0.16f;
                data[i] = Saturate((knock + rattle) * uneven) * 0.70f;
            }
            return CreateClip("Merlin Rough Damaged Loop", data);
        }

        private static AudioClip CreateStarterClip()
        {
            float[] data = NewBuffer(1f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float whir = Mathf.Sin(2f * Mathf.PI * 22f * t) * 0.52f
                    + Mathf.Sin(2f * Mathf.PI * 66f * t) * 0.26f
                    + Mathf.Sin(2f * Mathf.PI * 154f * t) * 0.12f;
                float modulation = 0.72f + 0.28f * Mathf.Sin(2f * Mathf.PI * 4f * t);
                data[i] = Saturate(whir * modulation) * 0.72f;
            }
            return CreateClip("Merlin Starter Cranking Loop", data);
        }

        private static AudioClip CreateCoughClip(string name, int seed, float duration, float strength)
        {
            float[] data = NewBuffer(duration);
            System.Random random = new System.Random(seed);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = Mathf.Exp(-t / 0.055f);
                float low = Mathf.Sin(2f * Mathf.PI * 68f * t) * 0.62f;
                float bark = Mathf.Sin(2f * Mathf.PI * 176f * t) * 0.28f;
                float noise = RandomBipolar(random) * 0.32f;
                data[i] = Saturate((low + bark + noise) * envelope * strength);
            }
            return CreateClip(name, data);
        }

        private static void PlayAt(
            Vector3 position,
            AudioClip clip,
            float volume,
            float pitch,
            float sourceMinDistance,
            float sourceMaxDistance)
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
            source.dopplerLevel = 0.16f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = sourceMinDistance;
            source.maxDistance = sourceMaxDistance;
            source.Play();
            UnityEngine.Object.Destroy(audioObject, clip.length / Mathf.Max(0.5f, source.pitch) + 0.25f);
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
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void SilenceAll()
        {
            if (rumbleSource != null) rumbleSource.volume = 0f;
            if (combustionSource != null) combustionSource.volume = 0f;
            if (roughSource != null) roughSource.volume = 0f;
            if (starterSource != null) starterSource.volume = 0f;
        }

        private void ResolveReferences()
        {
            if (flightController == null) flightController = GetComponent<P51FlightController>();
            if (lifecycle == null) lifecycle = GetComponent<P51MerlinLifecycleController>();
            if (conditionBridge == null) conditionBridge = GetComponent<P51EngineConditionPowerBridge>();
        }

        private void OnDestroy()
        {
            DestroyClip(rumbleClip);
            DestroyClip(combustionClip);
            DestroyClip(roughClip);
            DestroyClip(starterClip);
            DestroyClip(coughClip);
            DestroyClip(hardMisfireClip);
            if (exhaustParticleMaterial != null) Destroy(exhaustParticleMaterial);
        }

        private static void DestroyClip(AudioClip clip)
        {
            if (clip != null) UnityEngine.Object.Destroy(clip);
        }
    }
}
