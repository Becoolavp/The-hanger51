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

        static readonly Vector3 FuelPosition = new Vector3(0f,-.115f,-.072f);
        static readonly Vector3 CoolantPosition = new Vector3(-.300f,-.115f,-.072f);
        static readonly Vector3 OilPosition = new Vector3(.300f,-.115f,-.072f);
        const float GaugeBaseScale = .64f;

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
                    Transform panel = fuel.transform.parent;
                    Vector3 scale = GaugeScaleForPanel(panel);

                    Undo.RecordObject(fuel.transform,"Polish P-51 fuel gauge");
                    Undo.RecordObject(coolant.transform,"Polish P-51 coolant gauge");
                    Undo.RecordObject(oil.transform,"Polish P-51 oil gauge");

                    ApplyGaugePose(fuel.transform,FuelPosition,scale);
                    ApplyGaugePose(coolant.transform,CoolantPosition,scale);
                    ApplyGaugePose(oil.transform,OilPosition,scale);

                    fuel.RefreshGauge();
                    coolant.RefreshGauge();
                    oil.RefreshGauge();

                    EditorUtility.SetDirty(fuel);
                    EditorUtility.SetDirty(coolant);
                    EditorUtility.SetDirty(oil);
                    gaugeSets++;
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
                ", persistent wheel highlight feedback added=" + feedbackAdded +
                ". Fuel/coolant/oil gauges are now round and evenly spaced, and carried complete wheels can self-heal stale origin-axle state before reinstall.");
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
                    || fuel.transform.parent != oil.transform.parent)
                {
                    Debug.LogError("P-51 gauge validation failed on '"+flight.name+"': fuel/coolant/oil gauge set is incomplete or split across different parents.",flight);
                    passed = false;
                }
                else
                {
                    if (!Approximately(fuel.transform.localPosition,FuelPosition,.015f)
                        || !Approximately(coolant.transform.localPosition,CoolantPosition,.015f)
                        || !Approximately(oil.transform.localPosition,OilPosition,.015f))
                    {
                        Debug.LogError("P-51 gauge validation failed on '"+flight.name+"': the three engine gauges are not using the corrected evenly spaced panel positions.",flight);
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

                P51LandingGearInventoryBridge bridge =
                    flight.GetComponent<P51LandingGearInventoryBridge>();
                P51WheelInstallStateReconciler reconciler =
                    flight.GetComponent<P51WheelInstallStateReconciler>();
                if (!bridge || !bridge.IsReady || !reconciler)
                {
                    Debug.LogError("P-51 wheel validation failed on '"+flight.name+"': inventory bridge or wheel-state reconciler is missing.",flight);
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

        static Vector3 GaugeScaleForPanel(Transform panel)
        {
            float panelX = Mathf.Max(.001f,Mathf.Abs(panel.lossyScale.x));
            float panelY = Mathf.Max(.001f,Mathf.Abs(panel.lossyScale.y));
            return new Vector3(
                GaugeBaseScale,
                GaugeBaseScale * panelX / panelY,
                GaugeBaseScale);
        }

        static bool Approximately(Vector3 a,Vector3 b,float tolerance)
        {
            return Mathf.Abs(a.x-b.x) <= tolerance
                && Mathf.Abs(a.y-b.y) <= tolerance
                && Mathf.Abs(a.z-b.z) <= tolerance;
        }
    }
}
