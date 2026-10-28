using System;
using System.Reflection;
using Hanger51.Inventory;
using UnityEngine;

namespace Hanger51.Aircraft
{
    /// <summary>
    /// Keeps the wing-armament service targets lightweight in standalone builds.
    ///
    /// P51WingArmamentServiceTarget originally refreshed its highlight from LateUpdate on every
    /// target. The armament state accessors defensively resize/copy their backing arrays, so twelve
    /// service targets doing several state checks every rendered frame can create a large stream of
    /// short-lived allocations. That is especially unfriendly to a freshly launched standalone
    /// Player while the scene and shaders are also warming up.
    ///
    /// The service-target MonoBehaviours do not need Unity Update callbacks to remain interactable:
    /// the Player interactor raycasts their colliders and calls their public methods directly. We
    /// therefore disable only their automatic callbacks and refresh installation highlights here at
    /// a modest rate. Hold interactions, bolt animation, inspection and installation/removal remain
    /// owned by the original target components.
    /// </summary>
    [DefaultExecutionOrder(340)]
    [DisallowMultipleComponent]
    public sealed class P51WingArmamentRuntimePerformanceGuard : MonoBehaviour
    {
        private const float HighlightRefreshSeconds = 0.12f;
        private const float TargetRefreshSeconds = 1.0f;
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [SerializeField] private P51WingArmamentSystem system;

        private PlayerInventory inventory;
        private P51WingArmamentServiceTarget[] targets = Array.Empty<P51WingArmamentServiceTarget>();
        private MethodInfo updateHighlightMethod;
        private float nextHighlightRefresh;
        private float nextTargetRefresh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallRuntimePerformanceGuard()
        {
            P51WingArmamentSystem[] systems = FindObjectsByType<P51WingArmamentSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int index = 0; index < systems.Length; index++)
            {
                P51WingArmamentSystem candidate = systems[index];
                if (candidate == null
                    || candidate.GetComponent<P51WingArmamentRuntimePerformanceGuard>() != null)
                {
                    continue;
                }

                P51WingArmamentRuntimePerformanceGuard guard =
                    candidate.gameObject.AddComponent<P51WingArmamentRuntimePerformanceGuard>();
                guard.system = candidate;
            }
        }

        private void Awake()
        {
            ResolveReferences();
            RefreshTargets();
        }

        private void OnEnable()
        {
            ResolveReferences();
            RefreshTargets();
        }

        private void Update()
        {
            ResolveReferences();

            if (Time.unscaledTime >= nextTargetRefresh)
            {
                RefreshTargets();
            }

            if (Time.unscaledTime < nextHighlightRefresh)
            {
                return;
            }

            nextHighlightRefresh = Time.unscaledTime + HighlightRefreshSeconds;
            RefreshHighlights();
        }

        private void ResolveReferences()
        {
            if (system == null)
            {
                system = GetComponent<P51WingArmamentSystem>();
            }

            if (inventory == null)
            {
                inventory = FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            }

            if (updateHighlightMethod == null)
            {
                updateHighlightMethod = typeof(P51WingArmamentServiceTarget)
                    .GetMethod("UpdateHighlight", PrivateInstance);
            }
        }

        private void RefreshTargets()
        {
            if (system == null)
            {
                targets = Array.Empty<P51WingArmamentServiceTarget>();
                nextTargetRefresh = Time.unscaledTime + TargetRefreshSeconds;
                return;
            }

            targets = system.GetComponentsInChildren<P51WingArmamentServiceTarget>(true);
            for (int index = 0; index < targets.Length; index++)
            {
                P51WingArmamentServiceTarget target = targets[index];
                if (target == null)
                {
                    continue;
                }

                // Disabled MonoBehaviours are still returned by GetComponentInParent and their
                // public methods can still be invoked by P51WingArmamentPlayerInteractor. This only
                // removes the per-target LateUpdate loop.
                if (target.enabled)
                {
                    target.enabled = false;
                }
            }

            nextTargetRefresh = Time.unscaledTime + TargetRefreshSeconds;
            RefreshHighlights();
        }

        private void RefreshHighlights()
        {
            if (updateHighlightMethod == null || targets == null)
            {
                return;
            }

            for (int index = 0; index < targets.Length; index++)
            {
                P51WingArmamentServiceTarget target = targets[index];
                if (target == null
                    || target.ServiceKind == P51WingArmamentServiceKind.WingPanel)
                {
                    continue;
                }

                try
                {
                    updateHighlightMethod.Invoke(target, new object[] { inventory });
                }
                catch (TargetInvocationException exception)
                {
                    Debug.LogException(exception.InnerException ?? exception, target);
                    updateHighlightMethod = null;
                    return;
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, target);
                    updateHighlightMethod = null;
                    return;
                }
            }
        }
    }
}
