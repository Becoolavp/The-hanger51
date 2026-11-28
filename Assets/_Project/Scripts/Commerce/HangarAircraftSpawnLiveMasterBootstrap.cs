using Hanger51.Aircraft;
using Hanger51.EngineAssembly;
using UnityEngine;

namespace Hanger51.Commerce
{
    public static class HangarAircraftSpawnLiveMasterBootstrap
    {
        private const string MasterAircraftName = "P-51D Mustang Test Aircraft";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BindLiveMasterSources()
        {
            HangarAircraftSpawnConsole[] consoles =
                Object.FindObjectsByType<HangarAircraftSpawnConsole>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            if (consoles == null || consoles.Length == 0)
            {
                return;
            }

            GameObject masterAircraft = GameObject.Find(MasterAircraftName);
            if (masterAircraft == null)
            {
                P51FlightController[] aircraft =
                    Object.FindObjectsByType<P51FlightController>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None);
                for (int index = 0; index < aircraft.Length; index++)
                {
                    P51FlightController candidate = aircraft[index];
                    if (candidate == null
                        || candidate.gameObject.name.StartsWith("Spawned Fully Serviceable P-51"))
                    {
                        continue;
                    }
                    masterAircraft = candidate.gameObject;
                    break;
                }
            }

            if (masterAircraft == null)
            {
                return;
            }

            AircraftEngineMountReceiver receiver =
                masterAircraft.GetComponent<AircraftEngineMountReceiver>();
            EngineAssemblyTransportController masterEngine = receiver != null
                ? receiver.InstalledTransport
                : null;

            for (int index = 0; index < consoles.Length; index++)
            {
                HangarAircraftSpawnConsole console = consoles[index];
                if (console != null)
                {
                    console.ConfigureLiveMasterSources(masterAircraft, masterEngine);
                }
            }
        }
    }
}
