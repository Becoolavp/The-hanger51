using System.Reflection;
using UnityEngine;

namespace Hanger51.EngineAssembly
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EngineHoistController))]
    public sealed class EngineHoistMultiEngineSelector : MonoBehaviour
    {
        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly FieldInfo EngineTransportField =
            typeof(EngineHoistController).GetField("engineTransport", PrivateInstance);

        [SerializeField, Min(0.02f)] private float refreshInterval = 0.10f;

        private EngineHoistController hoist;
        private float nextRefreshTime;

        public bool IsReady => hoist != null && EngineTransportField != null;

        private void Awake()
        {
            ResolveReferences();
            SelectNearestAvailableEngine();
        }

        private void OnEnable()
        {
            ResolveReferences();
            nextRefreshTime = 0f;
            SelectNearestAvailableEngine();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + Mathf.Max(0.02f, refreshInterval);
            SelectNearestAvailableEngine();
        }

        public bool SelectNearestAvailableEngine()
        {
            ResolveReferences();
            if (!IsReady
                || hoist.HookPoint == null
                || hoist.HasAttachedEngine
                || hoist.IsBusy)
            {
                return false;
            }

            EngineAssemblyTransportController[] transports =
                FindObjectsByType<EngineAssemblyTransportController>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);

            EngineAssemblyTransportController nearest = null;
            float nearestSqrDistance = float.PositiveInfinity;
            Vector3 hookPosition = hoist.HookPoint.position;

            for (int index = 0; index < transports.Length; index++)
            {
                EngineAssemblyTransportController candidate = transports[index];
                if (candidate == null
                    || !candidate.HasEngine
                    || candidate.IsSuspended
                    || candidate.LiftPoint == null
                    || !candidate.gameObject.activeInHierarchy)
                {
                    continue;
                }

                float sqrDistance =
                    (candidate.LiftPoint.position - hookPosition).sqrMagnitude;
                if (sqrDistance < nearestSqrDistance)
                {
                    nearest = candidate;
                    nearestSqrDistance = sqrDistance;
                }
            }

            if (nearest == null || hoist.EngineTransport == nearest)
            {
                return nearest != null;
            }

            EngineTransportField.SetValue(hoist, nearest);
            return true;
        }

        private void ResolveReferences()
        {
            if (hoist == null)
            {
                hoist = GetComponent<EngineHoistController>();
            }
        }

        private void OnValidate()
        {
            refreshInterval = Mathf.Max(0.02f, refreshInterval);
        }
    }
}
