using UnityEngine;

namespace Hanger51.World
{
    [DisallowMultipleComponent]
    public sealed class H51UtilityGridController : MonoBehaviour
    {
        [SerializeField] H51PowerPlantController powerPlant;
        [SerializeField, Min(.2f)] float refreshSeconds = 1f;

        H51UtilityConsumer[] consumers = new H51UtilityConsumer[0];
        H51NightLight[] nightLights = new H51NightLight[0];
        H51RunwayLightingController[] runwayLighting =
            new H51RunwayLightingController[0];
        float nextRefresh;

        public bool GridOnline { get; private set; }
        public bool WaterNetworkOnline { get; private set; }
        public int ConnectedConsumers => consumers != null ? consumers.Length : 0;
        public float CurrentLoadMegawatts { get; private set; }
        public float NightLightingLoadMegawatts { get; private set; }

        public void Configure(H51PowerPlantController plant)
        {
            powerPlant = plant;
            RefreshConsumers();
            RefreshGrid();
        }

        void Start()
        {
            RefreshConsumers();
            RefreshGrid();
        }

        void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + refreshSeconds;

            if (!powerPlant)
                powerPlant = FindFirstObjectByType<H51PowerPlantController>();

            RefreshConsumers();
            RefreshGrid();
        }

        void RefreshConsumers()
        {
            consumers = FindObjectsByType<H51UtilityConsumer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            nightLights = FindObjectsByType<H51NightLight>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            runwayLighting = FindObjectsByType<H51RunwayLightingController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        }

        void RefreshGrid()
        {
            int critical = 0;
            for (int i=0;i<consumers.Length;i++)
            {
                H51UtilityConsumer consumer = consumers[i];
                if (consumer && consumer.CriticalFacility) critical++;
            }

            NightLightingLoadMegawatts = 0f;
            for (int i=0;i<nightLights.Length;i++)
                if (nightLights[i])
                    NightLightingLoadMegawatts += nightLights[i].DemandMegawatts;

            for (int i=0;i<runwayLighting.Length;i++)
                if (runwayLighting[i])
                    NightLightingLoadMegawatts += runwayLighting[i].DemandMegawatts;

            float baseLoad =
                consumers.Length * .045f +
                critical * .18f;

            CurrentLoadMegawatts =
                Mathf.Max(0f,baseLoad + NightLightingLoadMegawatts);

            bool plantOnline =
                powerPlant &&
                powerPlant.IsOnline &&
                powerPlant.OutputMegawatts > .1f;

            GridOnline =
                plantOnline &&
                powerPlant.OutputMegawatts >= CurrentLoadMegawatts;

            WaterNetworkOnline =
                GridOnline &&
                powerPlant &&
                powerPlant.WaterSystemAvailable;

            for (int i=0;i<consumers.Length;i++)
            {
                H51UtilityConsumer consumer = consumers[i];
                if (!consumer) continue;
                consumer.ApplyUtilityState(GridOnline,WaterNetworkOnline);
            }

            if (powerPlant)
                powerPlant.SetGridLoad(CurrentLoadMegawatts);
        }

        void OnValidate()
        {
            refreshSeconds = Mathf.Max(.2f,refreshSeconds);
        }
    }
}
