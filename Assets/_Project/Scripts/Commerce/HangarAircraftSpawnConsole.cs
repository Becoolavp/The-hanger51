using System;
using System.Collections.Generic;
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

        [Header("Live Master Sources")]
        [SerializeField] private GameObject masterAircraftSource;
        [SerializeField] private EngineAssemblyTransportController masterEngineSource;

        [Header("Fallback Full-Service Templates")]
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
        public GameObject MasterAircraftSource => masterAircraftSource;
        public EngineAssemblyTransportController MasterEngineSource => ResolveEngineSource();
        public bool UsesLiveMasterSources => masterAircraftSource != null
            && ResolveEngineSource() != null;
        public bool IsConfigured => ResolveAircraftSource() != null
            && ResolveEngineSource() != null
            && ResolveEngineSource().TransportRoot != null
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

        public void ConfigureLiveMasterSources(
            GameObject configuredMasterAircraft,
            EngineAssemblyTransportController configuredMasterEngine)
        {
            masterAircraftSource = configuredMasterAircraft;
            masterEngineSource = configuredMasterEngine;
        }

        public bool TrySpawn(out string resultMessage)
        {
            resultMessage = string.Empty;
            GameObject aircraftSource = ResolveAircraftSource();
            EngineAssemblyTransportController engineSource = ResolveEngineSource();
            if (!IsConfigured || aircraftSource == null || engineSource == null)
            {
                resultMessage = "The full-aircraft spawn console is missing its master aircraft, Merlin source, or spawn point.";
                return false;
            }
            if (spawnedAircraftCount >= maximumSpawnedAircraft)
            {
                resultMessage = $"Spawn row is full ({maximumSpawnedAircraft} aircraft). Restart or remove a spawned airplane before adding another.";
                return false;
            }

            buttonPressedUntil = Time.unscaledTime + 0.18f;

            string installedEnginePath = GetRelativePath(
                aircraftSource.transform,
                engineSource.TransportRoot);
            GameObject aircraft = Instantiate(aircraftSource);
            GameObject engineStationObject = CloneEngineStation(engineSource, out string engineCloneError);
            if (aircraft == null || engineStationObject == null)
            {
                if (aircraft != null) Destroy(aircraft);
                if (engineStationObject != null) Destroy(engineStationObject);
                resultMessage = string.IsNullOrWhiteSpace(engineCloneError)
                    ? "The serviceable P-51 master hierarchy could not be cloned."
                    : engineCloneError;
                return false;
            }

            aircraft.SetActive(false);
            engineStationObject.SetActive(false);
            RemoveClonedInstalledEngineRoot(aircraft, installedEnginePath);

            int number = spawnedAircraftCount + 1;
            aircraft.name = $"Spawned Fully Serviceable P-51 #{number}";
            engineStationObject.name = $"Spawned Merlin Maintenance Controller #{number}";

            Vector3 lateralOffset = spawnPoint.right * (aircraftSpacingMeters * spawnedAircraftCount);
            aircraft.transform.SetPositionAndRotation(
                spawnPoint.position + lateralOffset,
                spawnPoint.rotation);

            // The station/controller root remains alive because all engine service
            // targets reference it. Keep the controller below the playable world;
            // the cloned portable engine root is mounted into the new airplane.
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
                || service == null
                || receiver == null)
            {
                Destroy(aircraft);
                Destroy(engineStationObject);
                resultMessage = "The cloned P-51 or Merlin maintenance hierarchy is incomplete.";
                return false;
            }

            // A live master may currently be partly serviced or damaged. Every spawned
            // airplane is intentionally restored to a complete, healthy baseline while
            // retaining every current component/script/hierarchy feature from the master.
            if (!engineStation.SetAssemblyComplete())
            {
                Destroy(aircraft);
                Destroy(engineStationObject);
                resultMessage = "The cloned Merlin could not be restored to a complete serviceable assembly.";
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
            ResetFlightState(aircraft);
            EnsureLandingGearAttachments(aircraft);

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

            P51LandingGearServiceAttachmentFollower gearFollower =
                aircraft.GetComponent<P51LandingGearServiceAttachmentFollower>();
            gearFollower?.RepairHierarchy();

            spawnedAircraftCount++;
            resultMessage = $"Spawned fully serviceable P-51 #{number}: current master-aircraft features copied live, independent complete healthy Merlin, cowling/mount hardware, landing gear/tires, six guns and six full ammo boxes.";
            return true;
        }

        private GameObject ResolveAircraftSource()
        {
            return masterAircraftSource != null ? masterAircraftSource : aircraftTemplate;
        }

        private EngineAssemblyTransportController ResolveEngineSource()
        {
            if (masterAircraftSource != null)
            {
                AircraftEngineMountReceiver receiver =
                    masterAircraftSource.GetComponent<AircraftEngineMountReceiver>();
                if (receiver != null
                    && receiver.InstalledTransport != null
                    && receiver.InstalledTransport.TransportRoot != null)
                {
                    return receiver.InstalledTransport;
                }
            }

            if (masterEngineSource != null && masterEngineSource.TransportRoot != null)
            {
                return masterEngineSource;
            }

            return engineStationTemplate;
        }

        private static GameObject CloneEngineStation(
            EngineAssemblyTransportController source,
            out string error)
        {
            error = string.Empty;
            if (source == null || source.TransportRoot == null)
            {
                error = "No complete Merlin source is available for the spawn console.";
                return null;
            }

            Transform transportRoot = source.TransportRoot;
            if (transportRoot.IsChildOf(source.transform))
            {
                return Instantiate(source.gameObject);
            }

            Transform originalParent = transportRoot.parent;
            int originalSibling = transportRoot.GetSiblingIndex();
            Vector3 originalWorldPosition = transportRoot.position;
            Quaternion originalWorldRotation = transportRoot.rotation;
            Vector3 originalLocalScale = transportRoot.localScale;

            try
            {
                // Temporarily put the installed Merlin back underneath its maintenance
                // controller while cloning. This happens synchronously between frames,
                // so Unity remaps every current engine-service reference into the clone.
                transportRoot.SetParent(source.transform, true);
                return Instantiate(source.gameObject);
            }
            catch (Exception exception)
            {
                error = $"The live Merlin hierarchy could not be cloned: {exception.Message}";
                return null;
            }
            finally
            {
                transportRoot.SetParent(originalParent, true);
                transportRoot.SetPositionAndRotation(originalWorldPosition, originalWorldRotation);
                transportRoot.localScale = originalLocalScale;
                if (originalParent != null)
                {
                    transportRoot.SetSiblingIndex(
                        Mathf.Clamp(originalSibling, 0, Mathf.Max(0, originalParent.childCount - 1)));
                }
            }
        }

        private static void RemoveClonedInstalledEngineRoot(
            GameObject aircraft,
            string installedEnginePath)
        {
            if (aircraft == null || string.IsNullOrWhiteSpace(installedEnginePath))
            {
                return;
            }

            Transform clonedRoot = aircraft.transform.Find(installedEnginePath);
            if (clonedRoot == null)
            {
                return;
            }

            clonedRoot.gameObject.SetActive(false);
            clonedRoot.SetParent(null, true);
            Destroy(clonedRoot.gameObject);
        }

        private static string GetRelativePath(Transform root, Transform child)
        {
            if (root == null || child == null || child == root || !child.IsChildOf(root))
            {
                return string.Empty;
            }

            List<string> segments = new List<string>();
            Transform current = child;
            while (current != null && current != root)
            {
                segments.Add(current.name);
                current = current.parent;
            }
            if (current != root)
            {
                return string.Empty;
            }

            segments.Reverse();
            return string.Join("/", segments);
        }

        private static void EnsureLandingGearAttachments(GameObject aircraft)
        {
            if (aircraft == null || aircraft.GetComponent<P51LandingGearMaintenanceController>() == null)
            {
                return;
            }

            P51LandingGearServiceAttachmentFollower follower =
                aircraft.GetComponent<P51LandingGearServiceAttachmentFollower>();
            if (follower == null)
            {
                follower = aircraft.AddComponent<P51LandingGearServiceAttachmentFollower>();
            }
            follower.RepairHierarchy();
        }

        private static void ResetFlightState(GameObject aircraft)
        {
            P51FlightController flight = aircraft != null
                ? aircraft.GetComponent<P51FlightController>()
                : null;
            if (flight != null)
            {
                flight.SetPilotPresent(false);
            }
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
