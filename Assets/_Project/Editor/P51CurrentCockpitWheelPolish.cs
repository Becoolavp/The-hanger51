using System;
using System.Linq;
using Hanger51.Aircraft;
using Hanger51.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hanger51.EditorTools
{
    public static class P51CurrentCockpitWheelPolish
    {
        const string FuelGaugeName = "P-51 Main Fuel Quantity Gauge";
        const string CoolantGaugeName = "P-51 Coolant Temperature Gauge";
        const string OilGaugeName = "P-51 Oil Pressure Gauge";

        const string GaugeMountName = "P-51 Live Gauge Cluster Mount";
        static readonly Vector3 FuelPosition = new Vector3(0f,0f,0f);
        static readonly Vector3 CoolantPosition = new Vector3(-.205f,0f,0f);
        static readonly Vector3 OilPosition = new Vector3(.205f,0f,0f);
        const float GaugeBaseScale = .62f;

        [MenuItem("Hanger 51/P-51 Mustang/Current/Repair Cockpit Gauges And Wheel Reinstall")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            {
                Debug.LogError("P-51 current usability repair failed. Exit Play mode and let Unity finish compiling first.");
                return;
            }

            P51FlightController[] aircraft = UnityEngine.Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            if (aircraft.Length == 0)
            {
                Debug.LogError("P-51 current usability repair failed. No P-51 aircraft were found.");
                return;
            }

            int gaugeSets = 0;
            int tireTargets = 0;
            int reconcilers = 0;
            int wheelVisualBindings = 0;

            for (int i=0;i<aircraft.Length;i++)
            {
                P51FlightController flight = aircraft[i];
                if (!flight) continue;

                P51FuelQuantityGauge fuel =
                    flight.GetComponentsInChildren<P51FuelQuantityGauge>(true)
                        .FirstOrDefault(g => g && g.gameObject.activeSelf);
                P51CoolantTemperatureGauge coolant =
                    flight.GetComponentInChildren<P51CoolantTemperatureGauge>(true);
                P51OilPressureGauge oil =
                    flight.GetComponentInChildren<P51OilPressureGauge>(true);

                if (fuel && coolant && oil && fuel.transform.parent)
                {
                    Transform panel = ResolveInstrumentPanel(fuel.transform);
                    if (panel)
                    {
                        Transform mount = EnsureGaugeMount(panel);

                        Undo.RecordObject(fuel.transform,"Polish P-51 fuel gauge");
                        Undo.RecordObject(coolant.transform,"Polish P-51 coolant gauge");
                        Undo.RecordObject(oil.transform,"Polish P-51 oil gauge");

                        fuel.transform.SetParent(mount,false);
                        coolant.transform.SetParent(mount,false);
                        oil.transform.SetParent(mount,false);

                        ApplyGaugePose(fuel.transform,FuelPosition,Vector3.one*GaugeBaseScale);
                        ApplyGaugePose(coolant.transform,CoolantPosition,Vector3.one*GaugeBaseScale);
                        ApplyGaugePose(oil.transform,OilPosition,Vector3.one*GaugeBaseScale);

                        ForceGaugeVisible(fuel.transform);
                        ForceGaugeVisible(coolant.transform);
                        ForceGaugeVisible(oil.transform);

                        fuel.RefreshGauge();
                        coolant.RefreshGauge();
                        oil.RefreshGauge();

                        EditorUtility.SetDirty(fuel);
                        EditorUtility.SetDirty(coolant);
                        EditorUtility.SetDirty(oil);
                        gaugeSets++;
                    }
                }

                P51LandingGearMaintenanceController maintenance =
                    flight.GetComponent<P51LandingGearMaintenanceController>();
                if (maintenance)
                {
                    P51LandingGearVisualRuntimeBootstrap.EnsureConfiguredFor(maintenance);
                    wheelVisualBindings += maintenance.RepairAllWheelVisualBindingsFromHierarchy();
                    EditorUtility.SetDirty(maintenance);
                }

                P51LandingGearInventoryBridge bridge =
                    flight.GetComponent<P51LandingGearInventoryBridge>();
                if (bridge && !flight.GetComponent<P51WheelInstallStateReconciler>())
                {
                    Undo.AddComponent<P51WheelInstallStateReconciler>(flight.gameObject);
                    reconcilers++;
                }

                P51LandingGearServiceTarget[] targets =
                    flight.GetComponentsInChildren<P51LandingGearServiceTarget>(true);
                for (int t=0;t<targets.Length;t++)
                {
                    P51LandingGearServiceTarget target = targets[t];
                    if (!target || target.ServiceKind != P51LandingGearServiceKind.TireAndValve)
                        continue;

                    Undo.RecordObject(target.gameObject,"Enable P-51 wheel install target");
                    target.gameObject.SetActive(true);

                    Collider collider = target.GetComponent<Collider>();
                    if (collider)
                    {
                        Undo.RecordObject(collider,"Enable P-51 wheel target collider");
                        collider.enabled = true;
                        collider.isTrigger = true;
                        EditorUtility.SetDirty(collider);
                    }
                    tireTargets++;
                }
            }

            PlayerInventory player = UnityEngine.Object.FindFirstObjectByType<PlayerInventory>(
                FindObjectsInactive.Include);
            bool feedbackAdded = false;
            if (player && !player.GetComponent<P51WheelInstallationVisualFeedback>())
            {
                Undo.AddComponent<P51WheelInstallationVisualFeedback>(player.gameObject);
                feedbackAdded = true;
            }

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveOpenScenes();

            Debug.Log(
                "P-51 current usability repair complete. Gauge sets polished=" + gaugeSets +
                ", tire/valve install targets enabled=" + tireTargets +
                ", wheel state reconcilers added=" + reconcilers +
                ", permanent wheel visual bindings repaired=" + wheelVisualBindings +
                ", persistent wheel highlight feedback added=" + feedbackAdded +
                ". Fuel/coolant/oil gauges are now round and evenly spaced, and carried complete wheels rebind the live aircraft tire/rim hierarchy before reinstall.");
        }

        [MenuItem("Hanger 51/P-51 Mustang/Current/Validate Cockpit Gauges And Wheel Reinstall")]
        public static void Validate()
        {
            P51FlightController[] aircraft = UnityEngine.Object.FindObjectsByType<P51FlightController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            bool passed = aircraft.Length > 0;
            int checkedAircraft = 0;
            int validTargets = 0;

            for (int i=0;i<aircraft.Length;i++)
            {
                P51FlightController flight = aircraft[i];
                if (!flight) continue;
                checkedAircraft++;

                P51FuelQuantityGauge fuel =
                    flight.GetComponentsInChildren<P51FuelQuantityGauge>(true)
                        .FirstOrDefault(g => g && g.gameObject.activeSelf);
                P51CoolantTemperatureGauge coolant =
                    flight.GetComponentInChildren<P51CoolantTemperatureGauge>(true);
                P51OilPressureGauge oil =
                    flight.GetComponentInChildren<P51OilPressureGauge>(true);

                if (!fuel || !coolant || !oil
                    || fuel.transform.parent != coolant.transform.parent
                    || fuel.transform.parent != oil.transform.parent
                    || !fuel.transform.parent
                    || fuel.transform.parent.name != GaugeMountName)
                {
                    Debug.LogError("P-51 gauge validation failed on '"+flight.name+"': fuel/coolant/oil gauge set is incomplete or not mounted on the live unscaled gauge cluster.",flight);
                    passed = false;
                }
                else
                {
                    if (!Approximately(fuel.transform.localPosition,FuelPosition,.015f)
                        || !Approximately(coolant.transform.localPosition,CoolantPosition,.015f)
                        || !Approximately(oil.transform.localPosition,OilPosition,.015f))
                    {
                        Debug.LogError("P-51 gauge validation failed on '"+flight.name+"': the three engine gauges are not using the corrected evenly spaced cluster positions.",flight);
                        passed = false;
                    }

                    if (!HasEnabledRenderer(fuel.transform)
                        || !HasEnabledRenderer(coolant.transform)
                        || !HasEnabledRenderer(oil.transform))
                    {
                        Debug.LogError("P-51 gauge validation failed on '"+flight.name+"': one or more live gauges have no enabled renderer component.",flight);
                        passed = false;
                    }

                    Vector3 worldScale = fuel.transform.lossyScale;
                    float roundness = Mathf.Abs(Mathf.Abs(worldScale.x)-Mathf.Abs(worldScale.y));
                    if (roundness > .035f)
                    {
                        Debug.LogError(
                            "P-51 gauge validation failed on '"+flight.name+"': fuel gauge is still visibly squashed. World scale X/Y="+
                            worldScale.x.ToString("F3")+"/"+worldScale.y.ToString("F3")+".",fuel);
                        passed = false;
                    }
                }

                P51LandingGearMaintenanceController maintenance =
                    flight.GetComponent<P51LandingGearMaintenanceController>();
                P51LandingGearVisualSuspensionFollower wheelFollower =
                    flight.GetComponent<P51LandingGearVisualSuspensionFollower>();
                P51LandingGearInventoryBridge bridge =
                    flight.GetComponent<P51LandingGearInventoryBridge>();
                P51WheelInstallStateReconciler reconciler =
                    flight.GetComponent<P51WheelInstallStateReconciler>();
                if (!maintenance || !wheelFollower || !bridge || !bridge.IsReady || !reconciler)
                {
                    Debug.LogError("P-51 wheel validation failed on '"+flight.name+"': maintenance controller, visual suspension follower, inventory bridge, or wheel-state reconciler is missing.",flight);
                    passed = false;
                }

                P51LandingGearServiceTarget[] targets =
                    flight.GetComponentsInChildren<P51LandingGearServiceTarget>(true)
                        .Where(t => t && t.ServiceKind == P51LandingGearServiceKind.TireAndValve)
                        .ToArray();
                if (targets.Length != 3)
                {
                    Debug.LogError("P-51 wheel validation failed on '"+flight.name+"': expected 3 tire/valve axle targets; found "+targets.Length+".",flight);
                    passed = false;
                }

                for (int t=0;t<targets.Length;t++)
                {
                    Collider collider = targets[t].GetComponent<Collider>();
                    if (!targets[t].gameObject.activeSelf || !collider || !collider.enabled)
                    {
                        Debug.LogError("P-51 wheel validation failed on '"+flight.name+"': an axle reinstall target is inactive or has no enabled collider.",targets[t]);
                        passed = false;
                    }
                    else
                    {
                        validTargets++;
                    }
                }
            }

            PlayerInventory player = UnityEngine.Object.FindFirstObjectByType<PlayerInventory>(
                FindObjectsInactive.Include);
            bool feedbackReady =
                player && player.GetComponent<P51WheelInstallationVisualFeedback>();
            if (!feedbackReady)
            {
                Debug.LogError("P-51 wheel validation failed: the player is missing persistent wheel-install visual feedback.");
                passed = false;
            }

            if (passed)
            {
                Debug.Log(
                    "P-51 current usability validation passed. Aircraft checked=" + checkedAircraft +
                    ", enabled axle targets=" + validTargets +
                    ". Gauges are round/evenly spaced and carried wheel assemblies have active reinstall targets plus stale-state reconciliation.");
            }
        }

        static void ApplyGaugePose(Transform gauge,Vector3 position,Vector3 scale)
        {
            gauge.localPosition = position;
            gauge.localRotation = Quaternion.identity;
            gauge.localScale = scale;
            EditorUtility.SetDirty(gauge);
        }

        static Transform ResolveInstrumentPanel(Transform gauge)
        {
            if (!gauge) return null;

            Transform current = gauge.parent;
            while (current)
            {
                if (current.name == "P-51 Cockpit Instrument Panel")
                    return current;
                current = current.parent;
            }

            P51FlightController flight =
                gauge.GetComponentInParent<P51FlightController>();
            if (!flight) return null;

            Transform[] all = flight.GetComponentsInChildren<Transform>(true);
            return all.FirstOrDefault(t => t && t.name == "P-51 Cockpit Instrument Panel");
        }

        static Transform EnsureGaugeMount(Transform panel)
        {
            Transform parent = panel.parent;
            Transform mount = parent ? parent.Find(GaugeMountName) : null;
            if (!mount)
            {
                GameObject go = new GameObject(GaugeMountName);
                Undo.RegisterCreatedObjectUndo(go,"Create P-51 live gauge cluster mount");
                mount = go.transform;
                mount.SetParent(parent,false);
            }
            else
            {
                Undo.RecordObject(mount,"Move P-51 live gauge cluster mount");
            }

            // The panel primitive itself is non-uniformly scaled. Mount the instruments as a
            // sibling in unscaled cockpit space, positioned just in front of the panel face.
            Vector3 worldCenter = panel.TransformPoint(new Vector3(0f,-.115f,-.62f));
            mount.position = worldCenter;
            mount.rotation = panel.rotation;
            mount.localScale = Vector3.one;
            EditorUtility.SetDirty(mount);
            return mount;
        }

        static void ForceGaugeVisible(Transform gauge)
        {
            if (!gauge) return;
            gauge.gameObject.SetActive(true);
            Renderer[] renderers = gauge.GetComponentsInChildren<Renderer>(true);
            for (int i=0;i<renderers.Length;i++)
            {
                if (!renderers[i]) continue;
                renderers[i].enabled = true;
                EditorUtility.SetDirty(renderers[i]);
            }
        }

        static bool HasEnabledRenderer(Transform gauge)
        {
            if (!gauge) return false;

            // Spawn/template P-51s are intentionally inactive in the scene hierarchy.
            // Their gauges are still correctly configured if their renderer components
            // are enabled; activeInHierarchy should not be treated as a validation failure.
            Renderer[] renderers = gauge.GetComponentsInChildren<Renderer>(true);
            return renderers.Any(r => r && r.enabled);
        }

        static bool Approximately(Vector3 a,Vector3 b,float tolerance)
        {
            return Mathf.Abs(a.x-b.x) <= tolerance
                && Mathf.Abs(a.y-b.y) <= tolerance
                && Mathf.Abs(a.z-b.z) <= tolerance;
        }
    }
}
