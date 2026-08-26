using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Aircraft
{
    [DisallowMultipleComponent]
    public sealed class P51FuelPlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField, Min(1f)] private float interactionDistance = 3.2f;
        [SerializeField] private LayerMask interactionLayers = ~0;
        [SerializeField] private Vector3 carriedCanLocalPosition = new Vector3(0.42f, -0.34f, 0.78f);
        [SerializeField] private Vector3 carriedCanLocalEuler = new Vector3(8f, -15f, 5f);

        private P51FuelCan carriedCan;
        private string interactionPrompt = string.Empty;
        private string statusMessage = string.Empty;
        private float statusUntil;
        private GUIStyle promptStyle;
        private GUIStyle statusStyle;

        public P51FuelCan CarriedCan => carriedCan;

        private void Awake()
        {
            ResolveCamera();
        }

        private void OnEnable()
        {
            ResolveCamera();
        }

        private void Update()
        {
            ResolveCamera();
            interactionPrompt = string.Empty;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || playerCamera == null)
            {
                return;
            }

            if (carriedCan != null && keyboard.fKey.wasPressedThisFrame)
            {
                DropCan();
                SetStatus("Set the fuel can down.");
                return;
            }

            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    interactionDistance,
                    interactionLayers,
                    QueryTriggerInteraction.Collide))
            {
                if (carriedCan != null)
                {
                    interactionPrompt = $"Fuel can {carriedCan.GallonsRemaining:F1}/{carriedCan.CapacityGallons:F0} gal | F: set down";
                }
                return;
            }

            P51FuelCap cap = hit.collider.GetComponentInParent<P51FuelCap>();
            if (cap != null)
            {
                string tankName = cap.FuelSystem != null
                    ? cap.FuelSystem.GetTankDisplayName(cap.TankStation)
                    : "fuel tank";
                interactionPrompt = cap.IsRemoved
                    ? $"E: reinstall {tankName} cap"
                    : $"E: remove {tankName} cap";
                AppendCarriedCanPrompt();

                if (keyboard.eKey.wasPressedThisFrame)
                {
                    cap.TryToggle(out string result);
                    SetStatus(result);
                }
                return;
            }

            P51FuelFiller filler = hit.collider.GetComponentInParent<P51FuelFiller>();
            if (filler != null)
            {
                P51FuelSystem system = filler.FuelSystem;
                string tankName = system != null
                    ? system.GetTankDisplayName(filler.TankStation)
                    : "fuel tank";

                if (!filler.IsOpen)
                {
                    interactionPrompt = $"{tankName}: remove fuel cap first";
                }
                else if (carriedCan == null)
                {
                    interactionPrompt = $"{tankName}: pick up a fuel can";
                }
                else if (!carriedCan.HasFuel)
                {
                    interactionPrompt = $"Fuel can empty | F: set down";
                }
                else
                {
                    float current = system != null ? system.GetTankGallons(filler.TankStation) : 0f;
                    float capacity = system != null ? system.GetTankCapacityGallons(filler.TankStation) : 0f;
                    interactionPrompt = $"Hold E: pour fuel into {tankName} ({current:F1}/{capacity:F0} gal) | Can {carriedCan.GallonsRemaining:F1} gal | F: set down";
                }

                if (keyboard.eKey.isPressed && carriedCan != null)
                {
                    if (filler.TryPourFromCan(carriedCan, Time.deltaTime, out string result))
                    {
                        statusMessage = result;
                        statusUntil = Time.unscaledTime + 0.35f;
                    }
                    else if (keyboard.eKey.wasPressedThisFrame)
                    {
                        SetStatus(result);
                    }
                }
                return;
            }

            P51FuelCan can = hit.collider.GetComponentInParent<P51FuelCan>();
            if (can != null && can != carriedCan)
            {
                interactionPrompt = carriedCan == null
                    ? $"E: pick up 5-gal fuel can ({can.GallonsRemaining:F1} gal remaining)"
                    : $"Already carrying a fuel can | F: set current can down";
                if (keyboard.eKey.wasPressedThisFrame && carriedCan == null)
                {
                    PickupCan(can);
                    SetStatus($"Picked up fuel can: {can.GallonsRemaining:F1}/{can.CapacityGallons:F0} gal.");
                }
                return;
            }

            if (carriedCan != null)
            {
                interactionPrompt = $"Fuel can {carriedCan.GallonsRemaining:F1}/{carriedCan.CapacityGallons:F0} gal | F: set down";
            }
        }

        private void PickupCan(P51FuelCan can)
        {
            if (can == null || playerCamera == null)
            {
                return;
            }

            carriedCan = can;
            Rigidbody body = can.GetComponent<Rigidbody>();
            Collider[] colliders = can.GetComponentsInChildren<Collider>(true);
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                body.useGravity = false;
            }
            for (int index = 0; index < colliders.Length; index++)
            {
                colliders[index].enabled = false;
            }

            can.transform.SetParent(playerCamera.transform, false);
            can.transform.localPosition = carriedCanLocalPosition;
            can.transform.localRotation = Quaternion.Euler(carriedCanLocalEuler);
        }

        private void DropCan()
        {
            if (carriedCan == null)
            {
                return;
            }

            P51FuelCan can = carriedCan;
            carriedCan = null;
            Transform cameraTransform = playerCamera != null ? playerCamera.transform : transform;
            can.transform.SetParent(null, true);
            can.transform.position = cameraTransform.position
                + cameraTransform.forward * 0.85f
                - Vector3.up * 0.35f;
            can.transform.rotation = Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f);

            Collider[] colliders = can.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                colliders[index].enabled = true;
            }

            Rigidbody body = can.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = false;
                body.useGravity = true;
                body.WakeUp();
            }
        }

        private void AppendCarriedCanPrompt()
        {
            if (carriedCan != null)
            {
                interactionPrompt += $" | Can {carriedCan.GallonsRemaining:F1} gal | F: set down";
            }
        }

        private void SetStatus(string message)
        {
            statusMessage = message ?? string.Empty;
            statusUntil = Time.unscaledTime + 2.4f;
        }

        private void ResolveCamera()
        {
            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>(true);
            }
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (!string.IsNullOrWhiteSpace(interactionPrompt))
            {
                GUI.Box(
                    new Rect(Screen.width * 0.5f - 300f, Screen.height - 150f, 600f, 42f),
                    interactionPrompt,
                    promptStyle);
            }

            if (!string.IsNullOrWhiteSpace(statusMessage) && Time.unscaledTime <= statusUntil)
            {
                GUI.Box(
                    new Rect(Screen.width * 0.5f - 300f, Screen.height - 102f, 600f, 42f),
                    statusMessage,
                    statusStyle);
            }
        }

        private void EnsureStyles()
        {
            if (promptStyle == null)
            {
                promptStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 15,
                    wordWrap = true,
                    normal = { textColor = Color.white }
                };
            }
            if (statusStyle == null)
            {
                statusStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 15,
                    wordWrap = true,
                    normal = { textColor = Color.white }
                };
            }
        }
    }
}
