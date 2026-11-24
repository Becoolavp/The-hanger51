using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class P51WheelInstallDiagnostics : MonoBehaviour
    {
        private const string TracePrefix = "[H51-WHEEL-TRACE]";
        private const string TraceFileName = "Hanger51_WheelInstallTrace.txt";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private P51LandingGearMaintenanceController maintenance;
        private P51LandingGearInventoryBridge bridge;
        private P51RaycastLandingGear physicsGear;
        private int activeWheelIndex = -1;
        private int installSequence;

        public static string TraceFilePath =>
            Path.Combine(Application.persistentDataPath, TraceFileName);

        public static P51WheelInstallDiagnostics Ensure(
            P51LandingGearMaintenanceController configuredMaintenance)
        {
            if (configuredMaintenance == null)
            {
                return null;
            }

            P51WheelInstallDiagnostics diagnostics =
                configuredMaintenance.GetComponent<P51WheelInstallDiagnostics>();
            if (diagnostics == null)
            {
                diagnostics =
                    configuredMaintenance.gameObject.AddComponent<P51WheelInstallDiagnostics>();
            }
            diagnostics.ResolveReferences();
            return diagnostics;
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        public void BeginInstall(
            int wheelIndex,
            P51LooseWheelAssembly wheelAssembly)
        {
            ResolveReferences();
            StopAllCoroutines();
            activeWheelIndex = wheelIndex;
            installSequence++;

            string header =
                $"{TracePrefix} NEW INSTALL TRACE #{installSequence}\n"
                + $"UTC={DateTime.UtcNow:O}\n"
                + $"Realtime={Time.realtimeSinceStartup:F3}\n"
                + $"SceneObject={name}\n"
                + $"TraceFile={TraceFilePath}\n";
            WriteFresh(header);

            Write(
                $"BEGIN wheel={WheelLabel(wheelIndex)} index={wheelIndex} "
                + $"assembly={(wheelAssembly != null ? wheelAssembly.name : "<null>")} "
                + $"isCarried={(wheelAssembly != null && wheelAssembly.IsCarried)} "
                + $"isComplete={(wheelAssembly != null && wheelAssembly.IsComplete)} "
                + $"canInstall={(wheelAssembly != null && wheelAssembly.CanInstallOn(wheelIndex))} "
                + $"origin={(wheelAssembly != null ? wheelAssembly.OriginWheelIndex : -99)} "
                + $"pressure={(wheelAssembly != null ? wheelAssembly.TirePressurePsi : -1f):F1}");

            Snapshot("BEFORE_INSTALL", wheelIndex, wheelAssembly);
        }

        public void Checkpoint(
            string label,
            int wheelIndex,
            P51LooseWheelAssembly wheelAssembly = null)
        {
            Snapshot(label, wheelIndex, wheelAssembly);
        }

        public void CompleteInstall(int wheelIndex)
        {
            activeWheelIndex = wheelIndex;
            Snapshot(
                "IMMEDIATE_AFTER_COMPLETE_CALL",
                wheelIndex,
                P51LooseWheelAssembly.CurrentCarried);
            StartCoroutine(VerifyAfterInstall(wheelIndex));
        }

        private IEnumerator VerifyAfterInstall(int wheelIndex)
        {
            yield return null;
            Snapshot("NEXT_FRAME", wheelIndex, P51LooseWheelAssembly.CurrentCarried);

            yield return new WaitForEndOfFrame();
            Snapshot("END_OF_FRAME", wheelIndex, P51LooseWheelAssembly.CurrentCarried);

            yield return new WaitForSecondsRealtime(0.10f);
            Snapshot("AFTER_0_10_SECONDS", wheelIndex, P51LooseWheelAssembly.CurrentCarried);

            yield return new WaitForSecondsRealtime(0.40f);
            Snapshot("AFTER_0_50_SECONDS", wheelIndex, P51LooseWheelAssembly.CurrentCarried);

            yield return new WaitForSecondsRealtime(0.50f);
            Snapshot("AFTER_1_00_SECONDS", wheelIndex, P51LooseWheelAssembly.CurrentCarried);

            Write(
                $"TRACE_COMPLETE wheel={WheelLabel(wheelIndex)} index={wheelIndex} "
                + $"file=\"{TraceFilePath}\"");
        }

        public void RecordReconcilerChange(
            int wheelIndex,
            bool rimBefore,
            bool tireBefore,
            bool rimAfter,
            bool tireAfter)
        {
            Write(
                $"RECONCILER_CHANGE wheel={WheelLabel(wheelIndex)} index={wheelIndex} "
                + $"rim={rimBefore}->{rimAfter} tire={tireBefore}->{tireAfter}");
            Snapshot("AFTER_RECONCILER_CHANGE", wheelIndex, P51LooseWheelAssembly.CurrentCarried);
        }

        private void Snapshot(
            string stage,
            int wheelIndex,
            P51LooseWheelAssembly wheelAssembly)
        {
            ResolveReferences();

            bool validIndex = wheelIndex >= 0 && wheelIndex < 3;
            bool gearInstalled = validIndex && maintenance != null
                && maintenance.IsGearInstalled(wheelIndex);
            bool tireInstalled = validIndex && maintenance != null
                && maintenance.IsTireInstalled(wheelIndex);
            bool rimInstalled = validIndex && bridge != null
                && bridge.IsRimInstalled(wheelIndex);

            Transform systemRoot = transform.Find("P-51 Serviceable Retractable Landing Gear");
            string label = WheelHierarchyLabel(wheelIndex);
            Transform gear = FindDescendant(
                systemRoot,
                $"{label} Serviceable Gear Visual");
            Transform tire = FindDescendant(
                systemRoot,
                $"{label} Tire Visual");
            Transform rim = FindDescendant(
                systemRoot,
                $"{label} Rim Visual");
            Transform valve = FindDescendant(
                systemRoot,
                $"{label} Tire and Valve Service Target");
            Transform proxy = GetPhysicsProxy(wheelIndex);

            int looseWheelCount = UnityEngine.Object.FindObjectsByType<P51LooseWheelAssembly>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None).Length;

            Write(
                $"SNAPSHOT stage={stage} frame={Time.frameCount} realtime={Time.realtimeSinceStartup:F3} "
                + $"wheel={WheelLabel(wheelIndex)} index={wheelIndex} "
                + $"state[gear={gearInstalled},tire={tireInstalled},rim={rimInstalled}] "
                + $"currentCarried={(P51LooseWheelAssembly.CurrentCarried != null ? P51LooseWheelAssembly.CurrentCarried.name : "<null>")} "
                + $"passedAssembly={(wheelAssembly != null ? wheelAssembly.name : "<null>")} "
                + $"looseWheelObjects={looseWheelCount}");

            WriteTransform("GEAR", gear, valve);
            WriteTransform("TIRE", tire, proxy != null ? proxy : valve);
            WriteTransform("RIM", rim, proxy != null ? proxy : valve);
            WriteTransform("VALVE_TARGET", valve, null);
            WriteTransform("PHYSICS_PROXY", proxy, null);

            if (tire != null && rim != null)
            {
                Write(
                    $"WHEEL_RELATION tireToRimDistance={Vector3.Distance(tire.position, rim.position):F4} "
                    + $"sameParent={tire.parent == rim.parent} "
                    + $"tireParent={PathOf(tire.parent)} rimParent={PathOf(rim.parent)}");
            }
        }

        private void WriteTransform(
            string role,
            Transform target,
            Transform reference)
        {
            if (target == null)
            {
                Write($"{role} missing=true");
                return;
            }

            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            int enabledRendererCount = 0;
            int activeRendererCount = 0;
            Bounds bounds = default;
            bool hasBounds = false;

            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                if (renderer.enabled)
                {
                    enabledRendererCount++;
                }
                if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                {
                    activeRendererCount++;
                }

                if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                {
                    if (!hasBounds)
                    {
                        bounds = renderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }
            }

            string referenceText = reference != null
                ? $" reference={PathOf(reference)} distanceToReference={Vector3.Distance(target.position, reference.position):F4}"
                : string.Empty;
            string boundsText = hasBounds
                ? $" boundsCenter={Format(bounds.center)} boundsSize={Format(bounds.size)}"
                : " bounds=<none>";

            Write(
                $"{role} path={PathOf(target)} "
                + $"instanceId={target.GetInstanceID()} "
                + $"activeSelf={target.gameObject.activeSelf} "
                + $"activeInHierarchy={target.gameObject.activeInHierarchy} "
                + $"pos={Format(target.position)} localPos={Format(target.localPosition)} "
                + $"rot={Format(target.eulerAngles)} scale={Format(target.lossyScale)} "
                + $"renderers={renderers.Length} enabledRenderers={enabledRendererCount} "
                + $"activeRenderers={activeRendererCount}"
                + referenceText
                + boundsText);
        }

        private Transform GetPhysicsProxy(int wheelIndex)
        {
            if (physicsGear == null || wheelIndex < 0 || wheelIndex > 2)
            {
                return null;
            }

            string fieldName = wheelIndex == 0
                ? "leftMainVisual"
                : wheelIndex == 1
                    ? "rightMainVisual"
                    : "tailwheelVisual";
            FieldInfo field = typeof(P51RaycastLandingGear).GetField(
                fieldName,
                PrivateInstance);
            return field != null ? field.GetValue(physicsGear) as Transform : null;
        }

        private void ResolveReferences()
        {
            if (maintenance == null)
            {
                maintenance = GetComponent<P51LandingGearMaintenanceController>();
            }
            if (bridge == null)
            {
                bridge = GetComponent<P51LandingGearInventoryBridge>();
            }
            if (physicsGear == null)
            {
                physicsGear = GetComponent<P51RaycastLandingGear>();
            }
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < all.Length; index++)
            {
                Transform candidate = all[index];
                if (candidate != null && candidate.name == objectName)
                {
                    return candidate;
                }
            }
            return null;
        }

        private static string WheelHierarchyLabel(int wheelIndex)
        {
            return wheelIndex == 0
                ? "Left Main"
                : wheelIndex == 1
                    ? "Right Main"
                    : "Tailwheel";
        }

        private static string WheelLabel(int wheelIndex)
        {
            return wheelIndex == 0
                ? "left-main"
                : wheelIndex == 1
                    ? "right-main"
                    : "tail";
        }

        private static string Format(Vector3 value)
        {
            return $"({value.x:F4},{value.y:F4},{value.z:F4})";
        }

        private static string PathOf(Transform target)
        {
            if (target == null)
            {
                return "<null>";
            }

            string path = target.name;
            Transform current = target.parent;
            int safety = 0;
            while (current != null && safety++ < 64)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }

        private static void WriteFresh(string text)
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(TraceFilePath, text);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"{TracePrefix} Could not create trace file: {exception.Message}");
            }

            Debug.Log(text);
        }

        private static void Write(string message)
        {
            string line = $"{TracePrefix} {message}";
            Debug.Log(line);

            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                File.AppendAllText(
                    TraceFilePath,
                    line + Environment.NewLine);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"{TracePrefix} Could not append trace file: {exception.Message}");
            }
        }

        private void OnDisable()
        {
            if (activeWheelIndex >= 0)
            {
                Write(
                    $"DIAGNOSTICS_DISABLED activeWheel={WheelLabel(activeWheelIndex)} "
                    + $"frame={Time.frameCount}");
            }
        }
    }
}
