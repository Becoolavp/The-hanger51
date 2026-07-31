using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera playerCamera;

        [Header("Movement Speed")]
        [SerializeField, Min(0f)] private float walkSpeed = 5f;
        [SerializeField, Min(0f)] private float sprintSpeed = 8f;

        [Header("Movement Smoothing")]
        [SerializeField, Min(0f)] private float groundAcceleration = 30f;
        [SerializeField, Min(0f)] private float groundDeceleration = 40f;
        [SerializeField, Min(0f)] private float airAcceleration = 10f;
        [SerializeField, Min(0f)] private float airDeceleration = 4f;

        [Header("Jump and Gravity")]
        [SerializeField, Min(0f)] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -24f;
        [SerializeField] private float groundStickVelocity = -1.5f;
        [SerializeField, Min(1f)] private float terminalVelocity = 50f;

        [Header("Ground Probe")]
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField, Min(0.01f)] private float groundProbeDistance = 0.12f;
        [SerializeField, Min(0f)] private float groundProbeStartOffset = 0.05f;
        [SerializeField, Min(0f)] private float groundProbeRadiusInset = 0.03f;

        [Header("Mouse Look")]
        [SerializeField, Min(0.01f)] private float mouseSensitivity = 0.12f;
        [SerializeField, Range(1f, 89f)] private float verticalLookLimit = 85f;
        [SerializeField] private bool lockCursorOnStart = true;

        private readonly RaycastHit[] groundHits = new RaycastHit[8];

        private CharacterController characterController;
        private Vector3 horizontalVelocity;
        private float verticalVelocity;
        private float cameraPitch;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            characterController.minMoveDistance = 0f;

            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>();
            }

            if (playerCamera == null)
            {
                Debug.LogError($"{nameof(FirstPersonController)} on '{name}' needs a child Camera.", this);
                enabled = false;
            }
        }

        private void Start()
        {
            if (lockCursorOnStart)
            {
                SetCursorLocked(true);
            }
        }

        private void Update()
        {
            HandleCursorState();

            bool controlsAreActive = Cursor.lockState == CursorLockMode.Locked;

            if (controlsAreActive)
            {
                HandleLookInput();
            }

            HandleMovement(controlsAreActive);
        }

        private void LateUpdate()
        {
            if (playerCamera != null)
            {
                playerCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
            }
        }

        private void OnDisable()
        {
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
        }

        private void HandleMovement(bool controlsAreActive)
        {
            float deltaTime = Time.deltaTime;
            Keyboard keyboard = Keyboard.current;

            Vector2 movementInput = controlsAreActive && keyboard != null
                ? ReadMovementInput(keyboard)
                : Vector2.zero;

            bool isGrounded = IsGrounded();
            bool isSprinting = controlsAreActive && keyboard != null && keyboard.leftShiftKey.isPressed;
            float targetSpeed = isSprinting ? sprintSpeed : walkSpeed;

            Vector3 desiredDirection = transform.right * movementInput.x + transform.forward * movementInput.y;
            Vector3 desiredHorizontalVelocity = desiredDirection * targetSpeed;

            float smoothingRate = SelectHorizontalSmoothingRate(
                movementInput,
                desiredHorizontalVelocity,
                isGrounded);

            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                desiredHorizontalVelocity,
                smoothingRate * deltaTime);

            bool jumpPressed = controlsAreActive
                && keyboard != null
                && keyboard.spaceKey.wasPressedThisFrame;

            if (isGrounded && verticalVelocity <= 0f)
            {
                verticalVelocity = groundStickVelocity;
            }

            if (isGrounded && jumpPressed)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                isGrounded = false;
            }
            else if (!isGrounded)
            {
                verticalVelocity += gravity * deltaTime;
                verticalVelocity = Mathf.Max(verticalVelocity, -terminalVelocity);
            }

            Vector3 finalVelocity = horizontalVelocity + Vector3.up * verticalVelocity;
            CollisionFlags collisionFlags = characterController.Move(finalVelocity * deltaTime);

            if ((collisionFlags & CollisionFlags.Below) != 0 && verticalVelocity < 0f)
            {
                verticalVelocity = groundStickVelocity;
            }

            if ((collisionFlags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            {
                verticalVelocity = 0f;
            }
        }

        private static Vector2 ReadMovementInput(Keyboard keyboard)
        {
            Vector2 input = Vector2.zero;

            if (keyboard.wKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;

            return Vector2.ClampMagnitude(input, 1f);
        }

        private float SelectHorizontalSmoothingRate(
            Vector2 movementInput,
            Vector3 desiredHorizontalVelocity,
            bool isGrounded)
        {
            bool hasMovementInput = movementInput.sqrMagnitude > 0.001f;
            bool isAccelerating = hasMovementInput
                && desiredHorizontalVelocity.sqrMagnitude > horizontalVelocity.sqrMagnitude;

            if (isGrounded)
            {
                return isAccelerating ? groundAcceleration : groundDeceleration;
            }

            return isAccelerating ? airAcceleration : airDeceleration;
        }

        private bool IsGrounded()
        {
            float horizontalScale = Mathf.Max(
                Mathf.Abs(transform.lossyScale.x),
                Mathf.Abs(transform.lossyScale.z));

            float verticalScale = Mathf.Abs(transform.lossyScale.y);
            float radius = characterController.radius * horizontalScale;
            float height = Mathf.Max(characterController.height * verticalScale, radius * 2f);
            float probeRadius = Mathf.Max(0.01f, radius - groundProbeRadiusInset);

            Vector3 worldCenter = transform.TransformPoint(characterController.center);
            float lowerSphereCenterY = worldCenter.y - (height * 0.5f) + radius;

            Vector3 probeOrigin = new Vector3(
                worldCenter.x,
                lowerSphereCenterY + groundProbeStartOffset,
                worldCenter.z);

            float castDistance = groundProbeStartOffset + groundProbeDistance;

            int hitCount = Physics.SphereCastNonAlloc(
                probeOrigin,
                probeRadius,
                Vector3.down,
                groundHits,
                castDistance,
                groundLayers,
                QueryTriggerInteraction.Ignore);

            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = groundHits[index];

                if (hit.collider == null || hit.collider == characterController)
                {
                    continue;
                }

                if (hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                float groundAngle = Vector3.Angle(hit.normal, Vector3.up);
                if (groundAngle <= characterController.slopeLimit + 0.1f)
                {
                    return true;
                }
            }

            return false;
        }

        private void HandleLookInput()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            Vector2 mouseDelta = mouse.delta.ReadValue() * mouseSensitivity;

            cameraPitch -= mouseDelta.y;
            cameraPitch = Mathf.Clamp(cameraPitch, -verticalLookLimit, verticalLookLimit);

            transform.Rotate(Vector3.up * mouseDelta.x);
        }

        private void HandleCursorState()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                SetCursorLocked(false);
            }

            if (mouse != null
                && mouse.leftButton.wasPressedThisFrame
                && Cursor.lockState != CursorLockMode.Locked)
            {
                SetCursorLocked(true);
            }
        }

        private static void SetCursorLocked(bool isLocked)
        {
            Cursor.lockState = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !isLocked;
        }

        private void OnValidate()
        {
            sprintSpeed = Mathf.Max(sprintSpeed, walkSpeed);
            gravity = Mathf.Min(gravity, -0.01f);
            groundStickVelocity = Mathf.Min(groundStickVelocity, -0.01f);
            terminalVelocity = Mathf.Max(terminalVelocity, 1f);

            groundProbeDistance = Mathf.Max(groundProbeDistance, 0.01f);
            groundProbeStartOffset = Mathf.Max(groundProbeStartOffset, 0f);
            groundProbeRadiusInset = Mathf.Max(groundProbeRadiusInset, 0f);
        }
    }
}
