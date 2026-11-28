using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(240)]
    [DisallowMultipleComponent]
    public sealed class P51CoolantPlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private P51PilotPlayerInteractor pilotInteractor;
        [SerializeField, Min(1f)] private float interactionDistance = 3.4f;
        [SerializeField, Range(0.01f, 0.20f)] private float interactionSphereRadius = 0.08f;
        [SerializeField] private LayerMask interactionLayers = ~0;
        [SerializeField] private Vector3 carriedJugLocalPosition = new Vector3(0.42f, -0.34f, 0.78f);
        [SerializeField] private Vector3 carriedJugLocalEuler = new Vector3(8f, -15f, 5f);

        private readonly RaycastHit[] interactionHits = new RaycastHit[24];
        private P51CoolantJug carriedJug;
        private RigidbodyInterpolation carriedJugOriginalInterpolation;
        private string interactionPrompt = string.Empty;
        private string statusMessage = string.Empty;
        private float statusUntil;
        private GUIStyle promptStyle;
        private GUIStyle statusStyle;
        private bool suppressedPilotInteractor;

        public P51CoolantJug CarriedJug => carriedJug;
        public bool HasActiveCoolantInteraction { get; private set; }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        private void Update()
        {
            ResolveReferences();
            interactionPrompt = string.Empty;
            HasActiveCoolantInteraction = false;

            if (pilotInteractor != null && pilotInteractor.enabled && pilotInteractor.IsPiloting)
            {
                RestorePilotInteractorIfSuppressed();
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || playerCamera == null)
            {
                RestorePilotInteractorIfSuppressed();
                return;
            }

            if (carriedJug != null && keyboard.fKey.wasPressedThisFrame)
            {
                DropJug();
                SetStatus("Set the coolant jug down.");
                return;
            }

            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!TryFindNearestCoolantInteraction(ray, out RaycastHit hit))
            {
                RestorePilotInteractorIfSuppressed();
                if (carriedJug != null)
                {
                    interactionPrompt = $"Coolant jug {carriedJug.LitersRemaining:F1}/{carriedJug.CapacityLiters:F0} L | F: set down";
                }
                return;
            }

            HasActiveCoolantInteraction = true;
            SuppressPilotInteractor();

            P51CoolantCap cap = hit.collider.GetComponentInParent<P51CoolantCap>();
            if (cap != null)
            {
                P51RadiatorCoolingSystem system = cap.CoolingSystem;
                interactionPrompt = cap.IsRemoved
                    ? "E: reinstall radiator coolant cap"
                    : "E: remove radiator coolant cap";
                if (system != null)
                {
                    interactionPrompt += $" | {system.CoolantLiters:F1}/{system.CoolantCapacityLiters:F0} L | {system.CoolantTemperatureC:F0} C";
                }
                AppendCarriedJugPrompt();

                if (keyboard.eKey.wasPressedThisFrame)
                {
                    cap.TryToggle(out string result);
                    SetStatus(result);
                }
                return;
            }

            P51CoolantFiller filler = hit.collider.GetComponentInParent<P51CoolantFiller>();
            if (filler != null)
            {
                HandleFillerInteraction(filler, keyboard);
                return;
            }

            P51CoolantJug jug = hit.collider.GetComponentInParent<P51CoolantJug>();
            if (jug != null && jug != carriedJug)
            {
                interactionPrompt = carriedJug == null
                    ? $"E: pick up 10 L coolant jug ({jug.LitersRemaining:F1} L remaining)"
                    : "Already carrying coolant | F: set current jug down";
                if (keyboard.eKey.wasPressedThisFrame && carriedJug == null)
                {
                    PickupJug(jug);
                    SetStatus($"Picked up coolant jug: {jug.LitersRemaining:F1}/{jug.CapacityLiters:F0} L.");
                }
            }
        }

        private void LateUpdate()
        {
            if (carriedJug == null || playerCamera == null)
            {
                return;
            }

            Transform jugTransform = carriedJug.transform;
            if (jugTransform.parent != playerCamera.transform)
            {
                jugTransform.SetParent(playerCamera.transform, false);
            }
            jugTransform.localPosition = carriedJugLocalPosition;
            jugTransform.localRotation = Quaternion.Euler(carriedJugLocalEuler);

            Rigidbody body = carriedJug.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        private void HandleFillerInteraction(P51CoolantFiller filler, Keyboard keyboard)
        {
            P51RadiatorCoolingSystem system = filler.CoolingSystem;
            P51CoolantCap cap = filler.CoolantCap;
            if (system == null)
            {
                interactionPrompt = "Radiator coolant system not connected.";
                return;
            }

            if (!filler.IsOpen)
            {
                interactionPrompt = $"E: remove radiator coolant cap | {system.GetServiceReading()}";
                AppendCarriedJugPrompt();
                if (keyboard.eKey.wasPressedThisFrame && cap != null)
                {
                    cap.TryToggle(out string result);
                    SetStatus(result);
                }
                return;
            }

            if (carriedJug == null)
            {
                interactionPrompt = $"E: reinstall coolant cap | {system.GetServiceReading()} | Pick up coolant jug to fill";
                if (keyboard.eKey.wasPressedThisFrame && cap != null)
                {
                    cap.TryToggle(out string result);
                    SetStatus(result);
                }
                return;
            }

            interactionPrompt = $"Hold E: add coolant ({system.CoolantLiters:F1}/{system.CoolantCapacityLiters:F0} L) | "
                + $"Jug {carriedJug.LitersRemaining:F1} L | F: set down";
            if (keyboard.eKey.isPressed)
            {
                if (filler.TryPourFromJug(carriedJug, Time.deltaTime, out string result))
                {
                    statusMessage = result;
                    statusUntil = Time.unscaledTime + 0.35f;
                }
                else if (keyboard.eKey.wasPressedThisFrame)
                {
                    SetStatus(result);
                }
            }
        }

        private bool TryFindNearestCoolantInteraction(Ray ray, out RaycastHit bestHit)
        {
            bestHit = default;
            int hitCount = Physics.SphereCastNonAlloc(
                ray,
                interactionSphereRadius,
                interactionHits,
                interactionDistance,
                interactionLayers,
                QueryTriggerInteraction.Collide);

            float nearest = float.PositiveInfinity;
            bool found = false;
            for (int index = 0; index < hitCount; index++)
            {
                Collider collider = interactionHits[index].collider;
                if (collider == null)
                {
                    continue;
                }

                bool coolantTarget = collider.GetComponentInParent<P51CoolantCap>() != null
                    || collider.GetComponentInParent<P51CoolantFiller>() != null
                    || collider.GetComponentInParent<P51CoolantJug>() != null;
                if (!coolantTarget || interactionHits[index].distance >= nearest)
                {
                    continue;
                }

                nearest = interactionHits[index].distance;
                bestHit = interactionHits[index];
                found = true;
            }
            return found;
        }

        private void PickupJug(P51CoolantJug jug)
        {
            if (jug == null || playerCamera == null)
            {
                return;
            }

            carriedJug = jug;
            Rigidbody body = jug.GetComponent<Rigidbody>();
            if (body != null)
            {
                carriedJugOriginalInterpolation = body.interpolation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.interpolation = RigidbodyInterpolation.None;
                body.detectCollisions = false;
                body.isKinematic = true;
                body.useGravity = false;
                body.Sleep();
            }

            Collider[] colliders = jug.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                colliders[index].enabled = false;
            }

            jug.transform.SetParent(playerCamera.transform, false);
            jug.transform.localPosition = carriedJugLocalPosition;
            jug.transform.localRotation = Quaternion.Euler(carriedJugLocalEuler);
        }

        private void DropJug()
        {
            if (carriedJug == null)
            {
                return;
            }

            P51CoolantJug jug = carriedJug;
            carriedJug = null;
            Transform cameraTransform = playerCamera != null ? playerCamera.transform : transform;
            Vector3 position = cameraTransform.position + cameraTransform.forward * 0.85f - Vector3.up * 0.35f;
            Quaternion rotation = Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f);
            jug.transform.SetParent(null, true);
            jug.transform.SetPositionAndRotation(position, rotation);

            Collider[] colliders = jug.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                colliders[index].enabled = true;
            }

            Rigidbody body = jug.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.position = position;
                body.rotation = rotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.detectCollisions = true;
                body.isKinematic = false;
                body.useGravity = true;
                body.interpolation = carriedJugOriginalInterpolation;
                body.WakeUp();
            }
        }

        private void AppendCarriedJugPrompt()
        {
            if (carriedJug != null)
            {
                interactionPrompt += $" | Jug {carriedJug.LitersRemaining:F1} L | F: set down";
            }
        }

        private void SuppressPilotInteractor()
        {
            if (pilotInteractor == null || !pilotInteractor.enabled || pilotInteractor.IsPiloting)
            {
                return;
            }

            pilotInteractor.enabled = false;
            suppressedPilotInteractor = true;
        }

        private void RestorePilotInteractorIfSuppressed()
        {
            if (!suppressedPilotInteractor)
            {
                return;
            }

            suppressedPilotInteractor = false;
            if (pilotInteractor != null)
            {
                pilotInteractor.enabled = true;
            }
        }

        private void SetStatus(string message)
        {
            statusMessage = message ?? string.Empty;
            statusUntil = Time.unscaledTime + 2.5f;
        }

        private void ResolveReferences()
        {
            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>(true);
            }
            if (pilotInteractor == null)
            {
                pilotInteractor = GetComponent<P51PilotPlayerInteractor>();
            }
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (!string.IsNullOrWhiteSpace(interactionPrompt))
            {
                GUI.Box(
                    new Rect(Screen.width * 0.5f - 320f, Screen.height - 198f, 640f, 42f),
                    interactionPrompt,
                    promptStyle);
            }
            if (!string.IsNullOrWhiteSpace(statusMessage) && Time.unscaledTime <= statusUntil)
            {
                GUI.Box(
                    new Rect(Screen.width * 0.5f - 320f, Screen.height - 102f, 640f, 42f),
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

        private void OnDisable()
        {
            RestorePilotInteractorIfSuppressed();
        }

        private void OnValidate()
        {
            interactionDistance = Mathf.Max(1f, interactionDistance);
            interactionSphereRadius = Mathf.Clamp(interactionSphereRadius, 0.01f, 0.20f);
        }
    }
}
