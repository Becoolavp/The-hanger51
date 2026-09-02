using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(510)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(P51AircraftServiceController))]
    public sealed class P51CowlingEngineServiceLock : MonoBehaviour
    {
        [SerializeField] private P51AircraftServiceController aircraftService;
        [SerializeField, Min(0.05f)] private float rescanIntervalSeconds = 0.25f;

        private readonly List<Collider> trackedColliders = new List<Collider>();
        private readonly List<bool> originalEnabledStates = new List<bool>();
        private bool lockActive;
        private float nextScanTime;

        public bool IsLocked => lockActive;
        public int TrackedServiceColliderCount => trackedColliders.Count;

        private void Awake()
        {
            ResolveReferences();
            RefreshLockNow();
        }

        private void OnEnable()
        {
            ResolveReferences();
            RefreshLockNow();
        }

        private void LateUpdate()
        {
            ResolveReferences();
            bool shouldLock = aircraftService != null && aircraftService.IsTopCowlingInstalled;

            if (shouldLock != lockActive)
            {
                if (shouldLock)
                {
                    BeginLock();
                }
                else
                {
                    EndLock();
                }
            }

            if (!lockActive)
            {
                return;
            }

            if (Time.unscaledTime >= nextScanTime)
            {
                CaptureNewServiceColliders();
                nextScanTime = Time.unscaledTime + Mathf.Max(0.05f, rescanIntervalSeconds);
            }

            // Some service targets refresh their own colliders. Cowling state remains the
            // final authority, so keep every captured internal service collider disabled.
            for (int index = 0; index < trackedColliders.Count; index++)
            {
                Collider collider = trackedColliders[index];
                if (collider != null && collider.enabled)
                {
                    collider.enabled = false;
                }
            }
        }

        public void RefreshLockNow()
        {
            ResolveReferences();
            bool shouldLock = aircraftService != null && aircraftService.IsTopCowlingInstalled;
            if (shouldLock)
            {
                if (!lockActive)
                {
                    BeginLock();
                }
                else
                {
                    CaptureNewServiceColliders();
                }
            }
            else if (lockActive)
            {
                EndLock();
            }
        }

        private void BeginLock()
        {
            lockActive = true;
            trackedColliders.Clear();
            originalEnabledStates.Clear();
            CaptureNewServiceColliders();
            nextScanTime = Time.unscaledTime + Mathf.Max(0.05f, rescanIntervalSeconds);
        }

        private void CaptureNewServiceColliders()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                if (collider == null
                    || trackedColliders.Contains(collider)
                    || !BelongsToInternalEngineServiceTarget(collider.transform))
                {
                    continue;
                }

                trackedColliders.Add(collider);
                originalEnabledStates.Add(collider.enabled);
                collider.enabled = false;
            }
        }

        private void EndLock()
        {
            for (int index = 0; index < trackedColliders.Count; index++)
            {
                Collider collider = trackedColliders[index];
                if (collider != null)
                {
                    bool restoreEnabled = index < originalEnabledStates.Count
                        && originalEnabledStates[index];
                    collider.enabled = restoreEnabled;
                }
            }

            trackedColliders.Clear();
            originalEnabledStates.Clear();
            lockActive = false;
        }

        private bool BelongsToInternalEngineServiceTarget(Transform candidate)
        {
            Transform cursor = candidate;
            while (cursor != null && cursor != transform)
            {
                Component[] components = cursor.GetComponents<Component>();
                for (int index = 0; index < components.Length; index++)
                {
                    Component component = components[index];
                    if (component == null)
                    {
                        continue;
                    }

                    AircraftServiceInteractionTarget aircraftTarget =
                        component as AircraftServiceInteractionTarget;
                    if (aircraftTarget != null
                        && aircraftTarget.InteractionKind == AircraftServiceInteractionKind.EngineMountBolt)
                    {
                        return true;
                    }

                    Type type = component.GetType();
                    string typeNamespace = type.Namespace ?? string.Empty;
                    if (!typeNamespace.StartsWith("Hanger51.EngineAssembly", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string typeName = type.Name;
                    if (typeName.IndexOf("InteractionTarget", StringComparison.Ordinal) >= 0
                        || typeName.IndexOf("InspectionTarget", StringComparison.Ordinal) >= 0
                        || typeName.IndexOf("ServiceTarget", StringComparison.Ordinal) >= 0
                        || typeName.IndexOf("Dipstick", StringComparison.Ordinal) >= 0
                        || typeName.IndexOf("OilCap", StringComparison.Ordinal) >= 0
                        || typeName.IndexOf("OilFiller", StringComparison.Ordinal) >= 0)
                    {
                        return true;
                    }
                }

                cursor = cursor.parent;
            }

            return false;
        }

        private void ResolveReferences()
        {
            if (aircraftService == null)
            {
                aircraftService = GetComponent<P51AircraftServiceController>();
            }
        }

        private void OnDisable()
        {
            if (lockActive)
            {
                EndLock();
            }
        }

        private void OnValidate()
        {
            rescanIntervalSeconds = Mathf.Max(0.05f, rescanIntervalSeconds);
        }
    }
}
