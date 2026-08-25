using System;
using System.Reflection;
using Hanger51.Aircraft;
using Hanger51.EngineAssembly;
using UnityEngine;

namespace Hanger51.Commerce
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class HangarAircraftSpawnConsole : MonoBehaviour
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [Header("Full-Service Templates")]
        [SerializeField] private GameObject aircraftTemplate;
        [SerializeField] private EngineAssemblyTransportController engineStationTemplate;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform buttonPlunger;

        [Header("Spawn Row")]
        [SerializeField, Min(8f)] private float aircraftSpacingMeters = 13.5f;
        [SerializeField, Min(1)] private int maximumSpawnedAircraft = 8;

        private int spawnedAircraftCount;
        private Vector3 buttonRestLocalPosition;
        private float buttonPressedUntil;

        public string InteractionText => "E: spawn fully built serviceable P-51";
        public bool IsConfigured => aircraftTemplate != null
            && engineStationTemplate != null
            && engineStationTemplate.TransportRoot != null
            && spawnPoint != null;

        private void Awake()
        {
            if (buttonPlunger != null)
            {
                buttonRestLocalPosition = buttonPlunger.localPosition;
            }
        }

        private void Update()
        {
            if (buttonPlunger == null)
            {
                return;
            }

            Vector3 pressed = buttonRestLocalPosition + Vector3.down * 0.035f;
            Vector3 target = Time.unscaledTime < buttonPressedUntil
                ? pressed
                : buttonRestLocalPosition;
            buttonPlunger.localPosition = Vector3.Lerp(
                buttonPlunger.localPosition,
                target,
                1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
        }

        public void Configure(
            GameObject configuredAircraftTemplate,
            EngineAssemblyTransportController configuredEngineStationTemplate,
            Transform configuredSpawnPoint,
            Transform configuredButtonPlunger,
            float configuredSpacingMeters = 13.5f,
            int configuredMaximumAircraft = 8)
        {
            aircraftTemplate = configuredAircraftTemplate;
            engineStationTemplate = configuredEngineStationTemplate;
            spawnPoint = configuredSpawnPoint;
            buttonPlunger = configuredButtonPlunger;
            aircraftSpacingMeters = Mathf.Max(8f, configuredSpacingMeters);
            maximumSpawnedAircraft = Mathf.Max(1, configuredMaximumAircraft);
            if (buttonPlunger != null)
            {
                buttonRestLocalPosition = buttonPlunger.localPosition;
            }
        }

        public bool TrySpawn(out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!IsConfigured)
            {
                resultMessage = "The full-aircraft spawn console is missing its service templates or spawn point.";
                return false;
            }
            if (spawnedAircraftCount >= maximumSpawnedAircraft)
            {
                resultMessage = $"Spawn row is full ({maximumSpawnedAircraft} aircraft). Restart or remove a spawned airplane before adding another.";
                return false;
            }

            buttonPressedUntil = Time.unscaledTime + 0.18f;

            GameObject aircraft = Instantiate(aircraftTemplate);
            GameObject engineStationObject = Instantiate(engineStationTemplate.gameObject);
            if (aircraft == null || engineStationObject == null)
            {
                if (aircraft != null) Destroy(aircraft);
                if (engineStationObject != null) Destroy(engineStationObject);
                resultMessage = "The serviceable P-51 templates could not be cloned.";
                return false;
            }

            aircraft.SetActive(false);
            engineStationObject.SetActive(false);

            int number = spawnedAircraftCount + 1;
            aircraft.name = $"Spawned Fully Serviceable P-51 #{number}";
            engineStationObject.name = $"Spawned Merlin Maintenance Controller #{number}";

            Vector3 lateralOffset = spawnPoint.right * (aircraftSpacingMeters * spawnedAircraftCount);
            aircraft.transform.SetPositionAndRotation(
                spawnPoint.position + lateralOffset,
                spawnPoint.rotation);

            // The station/controller root must remain alive because all engine service
            // targets reference it. Keep the controller far below the playable world;
            // the actual portable engine root is reparented into the airplane below.
            engineStationObject.transform.position = new Vector3(
                spawnPoint.position.x,
                -900f - number * 15f,
                spawnPoint.position.z);

            EngineAssemblyTransportController engineTransport =
                engineStationObject.GetComponent<EngineAssemblyTransportController>();
            EngineAssemblyStation engineStation =
                engineStationObject.GetComponent<EngineAssemblyStation>();
            EngineConditionController condition =
                engineStationObject.GetComponent<EngineConditionController>();
            P51AircraftServiceController service =
                aircraft.GetComponent<P51AircraftServiceController>();
            AircraftEngineMountReceiver receiver =
                aircraft.GetComponent<AircraftEngineMountReceiver>();

            if (engineTransport == null
                || engineTransport.TransportRoot == null
                || engineStation == null
                || !engineStation.IsComplete
                || service == null
                || receiver == null)
            {
                Destroy(aircraft);
                Destroy(engineStationObject);
                resultMessage = "The cloned P-51 or Merlin maintenance hierarchy is incomplete.";
                return false;
            }

            service.ResetAircraftService();
            condition?.InitializeNewEngineCondition();

            receiver.CompleteEnginePlacement(engineTransport);
            for (int bolt = 0; bolt < receiver.MountBoltCount; bolt++)
            {
                receiver.TryInstallMountBolt(bolt, out _);
            }
            service.RefreshTargetsAndVisuals();

            ForceFullyLoadedArmament(aircraft);
            ForceCompleteLandingGear(aircraft);

            Rigidbody body = aircraft.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            // Activate the hidden maintenance controller first, then the aircraft.
            // The service targets live under the engine root now mounted in the airplane,
            // while their station/condition controllers remain independent below the map.
            engineStationObject.SetActive(true);
            aircraft.SetActive(true);

            spawnedAircraftCount++;
            resultMessage = $"Spawned fully serviceable P-51 #{number}: complete healthy Merlin, cowling and mount hardware, landing gear/tires, six guns, six full ammo boxes, and all maintenance interactions are independent.";
            return true;
        }

        private static void ForceFullyLoadedArmament(GameObject aircraft)
        {
            P51WingArmamentSystem system = aircraft != null
                ? aircraft.GetComponent<P51WingArmamentSystem>()
                : null;
            if (system == null)
            {
                return;
            }

            SetPrivateField(system, "panelOpen", new[] { false, false });
            SetPrivateField(system, "gunInstalled", FilledBoolArray(6, true));
            SetPrivateField(system, "ammoBoxInstalled", FilledBoolArray(6, true));

            int rounds = 200;
            FieldInfo roundsField = typeof(P51WingArmamentSystem).GetField("gameRoundsPerAmmoBox", PrivateInstance);
            if (roundsField != null && roundsField.GetValue(system) is int configuredRounds)
            {
                rounds = Mathf.Max(1, configuredRounds);
            }
            int[] ammo = new int[6];
            for (int index = 0; index < ammo.Length; index++) ammo[index] = rounds;
            SetPrivateField(system, "ammoRemaining", ammo);
        }

        private static void ForceCompleteLandingGear(GameObject aircraft)
        {
            P51LandingGearMaintenanceController gear = aircraft != null
                ? aircraft.GetComponent<P51LandingGearMaintenanceController>()
                : null;
            if (gear == null)
            {
                return;
            }

            SetPrivateField(gear, "gearInstalled", FilledBoolArray(3, true));
            SetPrivateField(gear, "tireInstalled", FilledBoolArray(3, true));
            SetPrivateField(gear, "tireBurst", FilledBoolArray(3, false));
            SetPrivateField(gear, "tireHealth", new[] { 100f, 100f, 100f });
            SetPrivateField(gear, "tirePressurePsi", new[] { 30f, 30f, 24f });
            SetPrivateField(gear, "gearCommandDown", true);
            SetPrivateField(gear, "deploymentFraction", 1f);
        }

        private static bool[] FilledBoolArray(int count, bool value)
        {
            bool[] result = new bool[count];
            for (int index = 0; index < result.Length; index++) result[index] = value;
            return result;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null) return;
            FieldInfo field = target.GetType().GetField(fieldName, PrivateInstance);
            field?.SetValue(target, value);
        }

        private void OnValidate()
        {
            aircraftSpacingMeters = Mathf.Max(8f, aircraftSpacingMeters);
            maximumSpawnedAircraft = Mathf.Max(1, maximumSpawnedAircraft);
        }
    }
}
