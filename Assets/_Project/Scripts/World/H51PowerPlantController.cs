using Hanger51.NPCs;
using UnityEngine;

namespace Hanger51.World
{
    [DisallowMultipleComponent]
    public sealed class H51PowerPlantController : MonoBehaviour
    {
        [Header("Generation")]
        [SerializeField, Min(1f)] float nominalOutputMegawatts = 900f;
        [SerializeField, Range(0f,1f)] float fuelFraction = .82f;
        [SerializeField, Range(0f,1f)] float generatorHealth = 1f;
        [SerializeField, Range(0f,1f)] float coolantIntegrity = 1f;
        [SerializeField] bool operatorShutdown;
        [SerializeField, Min(1f)] float fuelCycleGameDays = 24f;
        [SerializeField, Min(1f)] float maintenanceCycleGameDays = 10f;

        [Header("Presentation")]
        [SerializeField] TextMesh statusDisplay;
        [SerializeField] ParticleSystem[] vaporSystems = new ParticleSystem[0];

        double lastGameMinute = -1d;
        float gridLoadMegawatts;
        float nextVisualRefresh;

        public bool IsOnline =>
            !operatorShutdown &&
            fuelFraction > .015f &&
            generatorHealth > .20f &&
            coolantIntegrity > .25f;

        public float OutputMegawatts
        {
            get
            {
                if (!IsOnline) return 0f;
                float fuelFactor = Mathf.Lerp(.68f,1f,Mathf.InverseLerp(.015f,.18f,fuelFraction));
                return nominalOutputMegawatts *
                       fuelFactor *
                       Mathf.Lerp(.35f,1f,generatorHealth) *
                       Mathf.Lerp(.45f,1f,coolantIntegrity);
            }
        }

        public float NominalOutputMegawatts => nominalOutputMegawatts;
        public float FuelFraction => fuelFraction;
        public float GeneratorHealth => generatorHealth;
        public float CoolantIntegrity => coolantIntegrity;
        public bool NeedsFuelService => fuelFraction <= .22f;
        public bool NeedsMaintenance => generatorHealth <= .76f || coolantIntegrity <= .82f;
        public bool WaterSystemAvailable => IsOnline && OutputMegawatts >= nominalOutputMegawatts * .12f;
        public string StatusSummary =>
            (IsOnline ? "ONLINE" : "OFFLINE") +
            " | " + OutputMegawatts.ToString("F0") + " MW" +
            " | Fuel " + Mathf.RoundToInt(fuelFraction * 100f) + "%" +
            " | Generator " + Mathf.RoundToInt(generatorHealth * 100f) + "%";

        public void Configure(TextMesh display, ParticleSystem[] vapors, float nominalMegawatts = 900f)
        {
            statusDisplay = display;
            vaporSystems = vapors ?? new ParticleSystem[0];
            NormalizeVaporVelocityCurves();
            nominalOutputMegawatts = Mathf.Max(1f, nominalMegawatts);
            fuelFraction = Mathf.Clamp01(fuelFraction <= 0f ? .82f : fuelFraction);
            generatorHealth = Mathf.Clamp01(generatorHealth <= 0f ? 1f : generatorHealth);
            coolantIntegrity = Mathf.Clamp01(coolantIntegrity <= 0f ? 1f : coolantIntegrity);
            RefreshPresentation(true);
        }

        public void RestoreHealthyOperationalState()
        {
            fuelFraction = Mathf.Max(fuelFraction,.82f);
            generatorHealth = Mathf.Max(generatorHealth,.96f);
            coolantIntegrity = Mathf.Max(coolantIntegrity,.96f);
            operatorShutdown = false;
            lastGameMinute = -1d;
            RefreshPresentation(true);
        }

        public void SetGridLoad(float megawatts)
        {
            gridLoadMegawatts = Mathf.Max(0f, megawatts);
        }

        public void SetOperatorShutdown(bool shutdown)
        {
            operatorShutdown = shutdown;
            RefreshPresentation(true);
        }

        public bool PerformScheduledFuelService()
        {
            if (!NeedsFuelService) return false;

            // Gameplay abstraction: the detailed real-world nuclear refueling procedure is
            // intentionally not simulated. A qualified plant worker completing the scheduled
            // fuel-service activity restores the plant's abstract fuel inventory.
            fuelFraction = 1f;
            operatorShutdown = false;
            generatorHealth = Mathf.Clamp01(generatorHealth + .04f);
            RefreshPresentation(true);
            return true;
        }

        public void PerformOperatorMaintenance()
        {
            generatorHealth = Mathf.Clamp01(generatorHealth + .065f);
            coolantIntegrity = Mathf.Clamp01(coolantIntegrity + .035f);
            RefreshPresentation(true);
        }

        void Awake()
        {
            fuelCycleGameDays = Mathf.Max(1f, fuelCycleGameDays);
            maintenanceCycleGameDays = Mathf.Max(1f, maintenanceCycleGameDays);
            NormalizeVaporVelocityCurves();
            RefreshPresentation(true);
        }

        void Update()
        {
            double now = H51WorldClock.Instance
                ? H51WorldClock.Instance.TotalGameMinutes
                : Time.timeAsDouble * 1.5d;

            if (lastGameMinute < 0d)
                lastGameMinute = now;

            double elapsedGameMinutes = System.Math.Max(0d, now - lastGameMinute);
            lastGameMinute = now;

            if (IsOnline && elapsedGameMinutes > 0d)
            {
                fuelFraction = Mathf.Max(
                    0f,
                    fuelFraction - (float)(elapsedGameMinutes / (fuelCycleGameDays * 1440d)));

                float maintenanceWear =
                    (float)(elapsedGameMinutes / (maintenanceCycleGameDays * 1440d));
                generatorHealth = Mathf.Max(0f, generatorHealth - maintenanceWear * .11f);
                coolantIntegrity = Mathf.Max(0f, coolantIntegrity - maintenanceWear * .035f);
            }

            if (fuelFraction <= .015f || generatorHealth <= .20f || coolantIntegrity <= .25f)
                operatorShutdown = true;

            if (Time.unscaledTime >= nextVisualRefresh)
            {
                nextVisualRefresh = Time.unscaledTime + .5f;
                RefreshPresentation(false);
            }
        }

        void RefreshPresentation(bool forceParticles)
        {
            if (statusDisplay)
            {
                float reserve = Mathf.Max(0f, OutputMegawatts - gridLoadMegawatts);
                statusDisplay.text =
                    "REGIONAL GENERATING STATION\n" +
                    (IsOnline ? "STATUS: ONLINE" : "STATUS: SHUTDOWN") + "\n" +
                    "OUTPUT: " + OutputMegawatts.ToString("F0") + " MW / " +
                    nominalOutputMegawatts.ToString("F0") + " MW\n" +
                    "GRID LOAD: " + gridLoadMegawatts.ToString("F1") + " MW\n" +
                    "RESERVE: " + reserve.ToString("F1") + " MW\n" +
                    "FUEL: " + Mathf.RoundToInt(fuelFraction * 100f) + "%   " +
                    "GEN: " + Mathf.RoundToInt(generatorHealth * 100f) + "%";
            }

            float outputFraction = nominalOutputMegawatts > 0f
                ? Mathf.Clamp01(OutputMegawatts / nominalOutputMegawatts)
                : 0f;

            for (int i = 0; i < vaporSystems.Length; i++)
            {
                ParticleSystem ps = vaporSystems[i];
                if (!ps) continue;

                ParticleSystem.EmissionModule emission = ps.emission;
                emission.rateOverTime = IsOnline
                    ? Mathf.Lerp(2f, 14f, outputFraction)
                    : 0f;

                if (forceParticles && !ps.isPlaying)
                    ps.Play();
            }
        }

        void NormalizeVaporVelocityCurves()
        {
            if (vaporSystems == null) return;

            for (int i = 0; i < vaporSystems.Length; i++)
            {
                ParticleSystem ps = vaporSystems[i];
                if (!ps) continue;

                // The cone shape plus start speed already supplies the upward plume.
                // Disable Velocity over Lifetime entirely so Unity never has to reconcile
                // mixed serialized MinMaxCurve modes from older generated plant particles.
                ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
                velocity.enabled = false;
            }
        }

        void OnValidate()
        {
            nominalOutputMegawatts = Mathf.Max(1f, nominalOutputMegawatts);
            fuelCycleGameDays = Mathf.Max(1f, fuelCycleGameDays);
            maintenanceCycleGameDays = Mathf.Max(1f, maintenanceCycleGameDays);
            fuelFraction = Mathf.Clamp01(fuelFraction);
            generatorHealth = Mathf.Clamp01(generatorHealth);
            coolantIntegrity = Mathf.Clamp01(coolantIntegrity);
        }
    }
}
