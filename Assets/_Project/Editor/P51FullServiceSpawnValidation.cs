using Hanger51.Aircraft;
using Hanger51.Commerce;
using Hanger51.EngineAssembly;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hanger51.EditorTools
{
    public static class P51FullServiceSpawnValidation
    {
        private const string AircraftRootName = "P-51D Mustang Test Aircraft";
        private const string AircraftTemplateName = "P-51 Full Service Aircraft Template";
        private const string EngineTemplateName = "Merlin Full Service Engine Template";

        [MenuItem("Hanger 51/P-51 Mustang/48 - Validate Full-Service Hangar Spawn Console")]
        public static void Validate()
        {
            bool passed = true;
            GameObject sourceAircraft = GameObject.Find(AircraftRootName);
            HangarAircraftSpawnConsole console = Object.FindFirstObjectByType<HangarAircraftSpawnConsole>();
            HangarCommercePlayerInteractor commerce = Object.FindFirstObjectByType<HangarCommercePlayerInteractor>();

            P51FlightController[] aircraftControllers = Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            GameObject aircraftTemplate = null;
            for (int index = 0; index < aircraftControllers.Length; index++)
            {
                if (aircraftControllers[index] != null
                    && aircraftControllers[index].gameObject.name == AircraftTemplateName)
                {
                    aircraftTemplate = aircraftControllers[index].gameObject;
                    break;
                }
            }

            EngineAssemblyTransportController[] transports = Object.FindObjectsByType<EngineAssemblyTransportController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            EngineAssemblyTransportController engineTemplate = null;
            for (int index = 0; index < transports.Length; index++)
            {
                if (transports[index] != null && transports[index].gameObject.name == EngineTemplateName)
                {
                    engineTemplate = transports[index];
                    break;
                }
            }

            if (console == null || !console.IsConfigured)
            {
                Debug.LogError("P-51 Step 48 failed: physical hangar spawn console is missing or not configured.");
                passed = false;
            }
            if (commerce == null)
            {
                Debug.LogError("P-51 Step 48 failed: Player commerce interactor is missing, so E cannot press the spawn button.");
                passed = false;
            }
            if (aircraftTemplate == null || aircraftTemplate.activeSelf)
            {
                Debug.LogError("P-51 Step 48 failed: inactive full-service aircraft template is missing.");
                passed = false;
            }
            if (engineTemplate == null
                || engineTemplate.gameObject.activeSelf
                || engineTemplate.TransportRoot == null
                || !engineTemplate.TransportRoot.IsChildOf(engineTemplate.transform))
            {
                Debug.LogError("P-51 Step 48 failed: inactive independent Merlin template hierarchy is missing.");
                passed = false;
            }
            else
            {
                EngineAssemblyStation station = engineTemplate.GetComponent<EngineAssemblyStation>();
                EngineConditionController condition = engineTemplate.GetComponent<EngineConditionController>();
                if (station == null || !station.IsComplete || condition == null)
                {
                    Debug.LogError("P-51 Step 48 failed: Merlin template is not a complete maintenance-capable engine with condition state.");
                    passed = false;
                }
            }

            P51GunTestTarget target = Object.FindFirstObjectByType<P51GunTestTarget>();
            float lateral = 0f;
            if (sourceAircraft == null || target == null)
            {
                Debug.LogError("P-51 Step 48 failed: main aircraft or gun target is missing.");
                passed = false;
            }
            else
            {
                Vector3 delta = target.transform.position - sourceAircraft.transform.position;
                lateral = Mathf.Abs(Vector3.Dot(delta, sourceAircraft.transform.right));
                if (lateral < 25f)
                {
                    Debug.LogError($"P-51 Step 48 failed: gun target is only {lateral:F1} m off the runway line.");
                    passed = false;
                }
            }

            if (passed)
            {
                int engineTargets = engineTemplate.TransportRoot
                    .GetComponentsInChildren<EngineAssemblyInteractionTarget>(true).Length;
                Debug.Log(
                    $"P-51 Step 48 passed. Hangar E-button is configured, aircraft template is inactive, independent complete Merlin template is inactive, "
                    + $"engine service targets={engineTargets}, and gun target is {lateral:F1} m off the runway line. Runtime button spawning is ready.");
            }
        }
    }
}
