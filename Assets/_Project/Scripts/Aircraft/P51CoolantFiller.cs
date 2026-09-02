using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class P51CoolantFiller : MonoBehaviour
    {
        [SerializeField] private P51RadiatorCoolingSystem coolingSystem;
        [SerializeField] private P51CoolantCap coolantCap;
        [SerializeField, Min(0.1f)] private float pourRateLitersPerSecond = 2.2f;

        public P51RadiatorCoolingSystem CoolingSystem => coolingSystem;
        public P51CoolantCap CoolantCap => coolantCap;
        public bool IsOpen => coolantCap != null && coolantCap.IsRemoved;

        public void Configure(
            P51RadiatorCoolingSystem configuredCoolingSystem,
            P51CoolantCap configuredCoolantCap,
            float configuredPourRateLitersPerSecond = 2.2f)
        {
            coolingSystem = configuredCoolingSystem;
            coolantCap = configuredCoolantCap;
            pourRateLitersPerSecond = Mathf.Max(0.1f, configuredPourRateLitersPerSecond);
        }

        public bool TryPourFromJug(P51CoolantJug jug, float deltaTime, out string resultMessage)
        {
            resultMessage = string.Empty;
            if (coolingSystem == null || coolantCap == null)
            {
                resultMessage = "This filler is not connected to the radiator cooling system.";
                return false;
            }
            if (coolingSystem.EngineRunning)
            {
                resultMessage = "Stop the Merlin before adding coolant.";
                return false;
            }
            if (coolingSystem.CoolantTemperatureC > 80f)
            {
                resultMessage = $"Coolant is too hot to service ({coolingSystem.CoolantTemperatureC:F0} C).";
                return false;
            }
            if (!coolantCap.IsRemoved)
            {
                resultMessage = "Remove the radiator coolant cap first.";
                return false;
            }
            if (jug == null)
            {
                resultMessage = "Pick up a coolant jug first.";
                return false;
            }
            if (!jug.HasCoolant)
            {
                resultMessage = "The coolant jug is empty.";
                return false;
            }

            float freeSpace = Mathf.Max(
                0f,
                coolingSystem.CoolantCapacityLiters - coolingSystem.CoolantLiters);
            if (freeSpace <= 0.001f)
            {
                resultMessage = "The radiator coolant circuit is full.";
                return false;
            }

            float requested = Mathf.Min(
                pourRateLitersPerSecond * Mathf.Max(0f, deltaTime),
                freeSpace);
            float drawn = jug.DrawCoolant(requested);
            float accepted = coolingSystem.AddCoolant(drawn);
            if (accepted + 0.0001f < drawn)
            {
                jug.Configure(jug.CapacityLiters, jug.LitersRemaining + (drawn - accepted));
            }

            resultMessage = $"Coolant: {coolingSystem.CoolantLiters:F1}/{coolingSystem.CoolantCapacityLiters:F0} L | "
                + $"Jug: {jug.LitersRemaining:F1}/{jug.CapacityLiters:F0} L";
            return accepted > 0f;
        }

        private void OnValidate()
        {
            pourRateLitersPerSecond = Mathf.Max(0.1f, pourRateLitersPerSecond);
        }
    }
}
