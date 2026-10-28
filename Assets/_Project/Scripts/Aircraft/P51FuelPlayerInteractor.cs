using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Aircraft
{
    [DefaultExecutionOrder(250)]
    [DisallowMultipleComponent]
    public sealed class P51FuelPlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField, Min(1f)] private float interactionDistance = 3.2f;
        [SerializeField, Range(0.01f, 0.20f)] private float interactionSphereRadius = 0.075f;
        [SerializeField] private LayerMask interactionLayers = ~0;
        [SerializeField] private Vector3 carriedCanLocalPosition = new Vector3(0.42f, -0.34f, 0.78f);
        [SerializeField] private Vector3 carriedCanLocalEuler = new Vector3(8f, -15f, 5f);

        private readonly RaycastHit[] interactionHits = new RaycastHit[24];

        private P51FuelCan carriedCan;
        private RigidbodyInterpolation carriedCanOriginalInterpolation;
        private string interactionPrompt = string.Empty;
        private string statusMessage = string.Empty;
        private float statusUntil;
        private GUIStyle promptStyle;
        private GUIStyle statusStyle;

        public P51FuelCan CarriedCan => carriedCan;
        public bool HasActiveFuelInteraction { get; private set; }

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
            HasActiveFuelInteraction = false;

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
            if (!TryFindNearestFuelInteraction(ray, out RaycastHit hit))
            {
                if (carriedCan != null)
                {
                    interactionPrompt = $"Fuel can {carriedCan.GallonsRemaining:F1}/{carriedCan.CapacityGallons:F0} gal | F: set down";
                }
                return;
            }

            // Fuel servicing owns the interaction key for this frame. The cockpit interactor
            // runs later and uses this flag to suppress both its E action and its prompt.
            HasActiveFuelInteraction = true;

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
                HandleFillerInteraction(filler, keyboard);
                return;
            }

            P51FuelCan can = hit.collider.GetComponentInParent<P51FuelCan>();
            if (can != null && can != carriedCan)
            {
                interactionPrompt = carriedCan == null
                    ? $"E: pick up 5-gal fuel can ({can.GallonsRemaining:F1} gal remaining)"
                    : "Already carrying a fuel can | F: set current can down";
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

        private void LateUpdate()
        {
            if (carriedCan == null || playerCamera == null)
            {
                return;
            }

            // A Rigidbody parented under the camera can visibly lag because the physics
            // interpolation step keeps trying to present an older world pose. While carried,
            // make the can a pure camera-relative object and hard-apply the pose every frame.
            Transform canTransform = carriedCan.transform;
            if (canTransform.parent != playerCamera.transform)
            {
                canTransform.SetParent(playerCamera.transform, false);
            }

            canTransform.localPosition = carriedCanLocalPosition;
            canTransform.localRotation = Quaternion.Euler(carriedCanLocalEuler);

            Rigidbody body = carriedCan.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        private void HandleFillerInteraction(P51FuelFiller filler, Keyboard keyboard)
        {
            P51FuelSystem system = filler.FuelSystem;
            P51FuelCap cap = filler.FuelCap;
            string tankName = system != null
                ? system.GetTankDisplayName(filler.TankStation)
                : "fuel tank";

            // The filler collider can sit slightly above/around the cap and become the first
            // thing the camera ray sees. Route E through to the cap so the player can always
            // remove it even when the filler collider wins the hit test.
            if (!filler.IsOpen)
            {
                interactionPrompt = $"E: remove {tankName} cap";
                AppendCarriedCanPrompt();
                if (keyboard.eKey.wasPressedThisFrame && cap != null)
                {
                    cap.TryToggle(out string result);
                    SetStatus(result);
                }
                return;
            }

            if (carriedCan == null)
            {
                interactionPrompt = $"E: reinstall {tankName} cap | Pick up a fuel can to refuel";
                if (keyboard.eKey.wasPressedThisFrame && cap != null)
                {
                    cap.TryToggle(out string result);
                    SetStatus(result);
                }
                return;
            }

            if (!carriedCan.HasFuel)
            {
                interactionPrompt = $"Fuel can empty | E: reinstall {tankName} cap | F: set down";
                if (keyboard.eKey.wasPressedThisFrame && cap != null)
                {
                    cap.TryToggle(out string result);
                    SetStatus(result);
                }
                return;
            }

            float current = system != null ? system.GetTankGallons(filler.TankStation) : 0f;
            float capacity = system != null ? system.GetTankCapacityGallons(filler.TankStation) : 0f;
            interactionPrompt = $"Hold E: pour fuel into {tankName} ({current:F1}/{capacity:F0} gal) | Can {carriedCan.GallonsRemaining:F1} gal | F: set down";

            if (keyboard.eKey.isPressed)
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
        }

        private bool TryFindNearestFuelInteraction(Ray ray, out RaycastHit bestHit)
        {
            bestHit = default;
            int hitCount = Physics.SphereCastNonAlloc(
                ray,
                interactionSphereRadius,
                interactionHits,
                interactionDistance,
                interactionLayers,
                QueryTriggerInteraction.Collide);

            float nearestDistance = float.PositiveInfinity;
            bool found = false;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit candidate = interactionHits[index];
                Collider collider = candidate.collider;
                if (collider == null)
                {
                    continue;
                }

                bool isFuelInteraction = collider.GetComponentInParent<P51FuelCap>() != null
                    || collider.GetComponentInParent<P51FuelFiller>() != null
                    || collider.GetComponentInParent<P51FuelCan>() != null;
                if (!isFuelInteraction || candidate.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = candidate.distance;
                bestHit = candidate;
                found = true;
            }

            return found;
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
                carriedCanOriginalInterpolation = body.interpolation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.interpolation = RigidbodyInterpolation.None;
                body.detectCollisions = false;
                body.isKinematic = true;
                body.useGravity = false;
                body.Sleep();
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
            Vector3 dropPosition = cameraTransform.position
                + cameraTransform.forward * 0.85f
                - Vector3.up * 0.35f;
            Quaternion dropRotation = Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f);

            can.transform.SetParent(null, true);
            can.transform.SetPositionAndRotation(dropPosition, dropRotation);

            Collider[] colliders = can.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                colliders[index].enabled = true;
            }

            Rigidbody body = can.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.position = dropPosition;
                body.rotation = dropRotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.detectCollisions = true;
                body.isKinematic = false;
                body.useGravity = true;
                body.interpolation = carriedCanOriginalInterpolation;
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

        private void OnValidate()
        {
            interactionDistance = Mathf.Max(1f, interactionDistance);
            interactionSphereRadius = Mathf.Clamp(interactionSphereRadius, 0.01f, 0.20f);
        }
    }
}
