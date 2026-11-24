using UnityEngine;

namespace Hanger51.World
{
    [DisallowMultipleComponent]
    public sealed class H51UtilityGridController : MonoBehaviour
    {
        [SerializeField] H51PowerPlantController powerPlant;
        [SerializeField, Min(.2f)] float refreshSeconds = 1f;

        H51UtilityConsumer[] consumers = new H51UtilityConsumer[0];
        float nextRefresh;

        public bool GridOnline { get; private set; }
        public bool WaterNetworkOnline { get; private set; }
        public int ConnectedConsumers => consumers != null ? consumers.Length : 0;

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
        }

        void RefreshGrid()
        {
            GridOnline =
                powerPlant &&
                powerPlant.IsOnline &&
                powerPlant.OutputMegawatts > .1f;

            WaterNetworkOnline =
                GridOnline &&
                powerPlant &&
                powerPlant.WaterSystemAvailable;

            int critical = 0;
            for (int i = 0; i < consumers.Length; i++)
            {
                H51UtilityConsumer consumer = consumers[i];
                if (!consumer) continue;
                if (consumer.CriticalFacility) critical++;
                consumer.ApplyUtilityState(GridOnline, WaterNetworkOnline);
            }

            if (powerPlant)
            {
                float estimatedLoad =
                    consumers.Length * .045f +
                    critical * .18f;
                powerPlant.SetGridLoad(estimatedLoad);
            }
        }

        void OnValidate()
        {
            refreshSeconds = Mathf.Max(.2f, refreshSeconds);
        }
    }
}
