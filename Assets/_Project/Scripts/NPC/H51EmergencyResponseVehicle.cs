using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hanger51.NPCs
{
    public enum H51EmergencyServiceType
    {
        Ambulance,
        Police,
        Fire
    }

    public enum H51EmergencyVehicleState
    {
        Idle,
        Responding,
        AtScene,
        Transporting,
        Returning
    }

    [DisallowMultipleComponent]
    public sealed class H51EmergencyResponseVehicle : MonoBehaviour
    {
        [SerializeField] H51EmergencyServiceType serviceType;
        [SerializeField] H51TrafficNetwork trafficNetwork;
        [SerializeField, Min(2f)] float speedMetersPerSecond = 13f;
        [SerializeField, Min(.5f)] float waypointTolerance = 1.4f;
        [SerializeField] Vector3 homePosition;
        [SerializeField] Quaternion homeRotation = Quaternion.identity;
        [SerializeField] GameObject[] emergencyLights = new GameObject[0];

        readonly List<Vector3> route = new List<Vector3>();
        int routeIndex;
        bool returningHome;
        Action<H51EmergencyResponseVehicle> arrivalCallback;

        public H51EmergencyServiceType ServiceType => serviceType;
        public H51EmergencyVehicleState State { get; private set; } = H51EmergencyVehicleState.Idle;
        public bool IsAvailable => State == H51EmergencyVehicleState.Idle;

        public void Configure(
            H51EmergencyServiceType configuredType,
            H51TrafficNetwork network,
            Vector3 configuredHomePosition,
            Quaternion configuredHomeRotation,
            float configuredSpeed,
            GameObject[] lights)
        {
            serviceType = configuredType;
            trafficNetwork = network;
            homePosition = configuredHomePosition;
            homeRotation = configuredHomeRotation;
            speedMetersPerSecond = Mathf.Max(2f, configuredSpeed);
            emergencyLights = lights ?? new GameObject[0];
            transform.position = homePosition;
            transform.rotation = homeRotation;
            State = H51EmergencyVehicleState.Idle;
            SetEmergencyLights(false);
        }

        public bool DispatchTo(
            Vector3 destination,
            Action<H51EmergencyResponseVehicle> onArrival,
            bool transporting = false)
        {
            if (!trafficNetwork)
                trafficNetwork = FindFirstObjectByType<H51TrafficNetwork>();
            if (!trafficNetwork) return false;

            if (!BuildRoute(destination))
                return false;

            arrivalCallback = onArrival;
            returningHome = false;
            State = transporting
                ? H51EmergencyVehicleState.Transporting
                : H51EmergencyVehicleState.Responding;
            SetEmergencyLights(true);
            return true;
        }

        public bool ReturnHome()
        {
            if (!trafficNetwork)
                trafficNetwork = FindFirstObjectByType<H51TrafficNetwork>();
            if (!trafficNetwork)
            {
                SnapHome();
                return false;
            }

            if (!BuildRoute(homePosition))
            {
                SnapHome();
                return false;
            }

            arrivalCallback = null;
            returningHome = true;
            State = H51EmergencyVehicleState.Returning;
            SetEmergencyLights(false);
            return true;
        }

        bool BuildRoute(Vector3 destination)
        {
            route.Clear();
            routeIndex = 0;

            if (!trafficNetwork.TryBuildPath(transform.position, destination, route) ||
                route.Count == 0)
                return false;

            return true;
        }

        void Update()
        {
            if (State == H51EmergencyVehicleState.Idle ||
                State == H51EmergencyVehicleState.AtScene)
                return;

            if (routeIndex >= route.Count)
            {
                CompleteRoute();
                return;
            }

            Vector3 target = route[routeIndex];
            target.y += .18f;
            Vector3 delta = target - transform.position;
            Vector3 planar = new Vector3(delta.x, 0f, delta.z);

            if (planar.magnitude <= waypointTolerance)
            {
                routeIndex++;
                if (routeIndex >= route.Count)
                    CompleteRoute();
                return;
            }

            Vector3 direction = planar.normalized;
            Quaternion wanted = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                wanted,
                180f * Time.deltaTime);

            transform.position = Vector3.MoveTowards(
                transform.position,
                target,
                speedMetersPerSecond * Time.deltaTime);

            if (State == H51EmergencyVehicleState.Responding ||
                State == H51EmergencyVehicleState.Transporting)
                AnimateEmergencyLights();
        }

        void CompleteRoute()
        {
            route.Clear();
            routeIndex = 0;

            if (returningHome)
            {
                SnapHome();
                return;
            }

            State = H51EmergencyVehicleState.AtScene;
            SetEmergencyLights(true);
            Action<H51EmergencyResponseVehicle> callback = arrivalCallback;
            arrivalCallback = null;
            callback?.Invoke(this);
        }

        void SnapHome()
        {
            returningHome = false;
            route.Clear();
            routeIndex = 0;
            arrivalCallback = null;
            transform.position = homePosition;
            transform.rotation = homeRotation;
            State = H51EmergencyVehicleState.Idle;
            SetEmergencyLights(false);
        }

        void AnimateEmergencyLights()
        {
            if (emergencyLights == null || emergencyLights.Length == 0) return;
            int phase = Mathf.FloorToInt(Time.unscaledTime * 8f);
            for (int i = 0; i < emergencyLights.Length; i++)
                if (emergencyLights[i])
                    emergencyLights[i].SetActive(((phase + i) & 1) == 0);
        }

        void SetEmergencyLights(bool active)
        {
            if (emergencyLights == null) return;
            for (int i = 0; i < emergencyLights.Length; i++)
                if (emergencyLights[i])
                    emergencyLights[i].SetActive(active);
        }

        void OnValidate()
        {
            speedMetersPerSecond = Mathf.Max(2f, speedMetersPerSecond);
            waypointTolerance = Mathf.Max(.5f, waypointTolerance);
        }
    }
}
