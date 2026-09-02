using System.Collections.Generic;
using System.Reflection;

namespace Hanger51.EngineAssembly
{
    public static class EngineAssemblyStationDeliveryExtensions
    {
        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly FieldInfo EngineBlockInstalledField =
            typeof(EngineAssemblyStation).GetField("engineBlockInstalled", PrivateInstance);
        private static readonly FieldInfo CoverPlacedField =
            typeof(EngineAssemblyStation).GetField("coverPlaced", PrivateInstance);
        private static readonly FieldInfo CoverBoltsTightenedField =
            typeof(EngineAssemblyStation).GetField("coverBoltsTightened", PrivateInstance);
        private static readonly FieldInfo SparkPlugInstalledField =
            typeof(EngineAssemblyStation).GetField("sparkPlugInstalled", PrivateInstance);
        private static readonly MethodInfo EnsureStateListsMethod =
            typeof(EngineAssemblyStation).GetMethod("EnsureStateLists", PrivateInstance);
        private static readonly MethodInfo RefreshVisualsMethod =
            typeof(EngineAssemblyStation).GetMethod("RefreshVisuals", PrivateInstance);

        public static bool SetAssemblyComplete(this EngineAssemblyStation station)
        {
            if (station == null
                || EngineBlockInstalledField == null
                || CoverPlacedField == null
                || CoverBoltsTightenedField == null
                || SparkPlugInstalledField == null
                || EnsureStateListsMethod == null
                || RefreshVisualsMethod == null)
            {
                return false;
            }

            EnsureStateListsMethod.Invoke(station, null);
            EngineBlockInstalledField.SetValue(station, true);
            SetAllTrue(CoverPlacedField.GetValue(station) as List<bool>);
            SetAllTrue(CoverBoltsTightenedField.GetValue(station) as List<bool>);
            SetAllTrue(SparkPlugInstalledField.GetValue(station) as List<bool>);
            RefreshVisualsMethod.Invoke(station, null);

            EngineAssemblyTransportController transport =
                station.GetComponent<EngineAssemblyTransportController>();
            transport?.RefreshMaintenanceTargets();
            return true;
        }

        private static void SetAllTrue(List<bool> values)
        {
            if (values == null)
            {
                return;
            }

            for (int index = 0; index < values.Count; index++)
            {
                values[index] = true;
            }
        }
    }
}
