using UnityEngine;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class P51BatteryTester : MonoBehaviour
    {
        private Rigidbody body;
        private Collider interactionCollider;

        public string InteractionText => "E: pick up battery tester";

        private void Awake()
        {
            ResolvePhysics();
        }

        public void SetHeld(bool held)
        {
            ResolvePhysics();
            if (body != null)
            {
                body.isKinematic = held;
                body.useGravity = !held;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            if (interactionCollider != null)
            {
                interactionCollider.enabled = !held;
            }
        }

        public string ReadBattery(P51AftEquipmentItem battery)
        {
            if (battery == null || battery.EquipmentKind != P51AftEquipmentKind.Battery)
            {
                return "Battery tester: no battery connected.";
            }

            float voltage = battery.BatteryVoltage;
            string condition = voltage >= 23.5f
                ? "GOOD"
                : voltage >= 20.5f
                    ? "LOW / STARTABLE"
                    : "TOO LOW TO START";
            return $"Battery tester connected: {voltage:F1} V — {condition}.";
        }

        private void ResolvePhysics()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }
            if (interactionCollider == null)
            {
                interactionCollider = GetComponent<Collider>();
            }
        }
    }
}
