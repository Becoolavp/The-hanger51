using UnityEngine;
using UnityEngine.InputSystem;

namespace Hanger51.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera playerCamera;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float walkSpeed = 5f;
        [SerializeField, Min(0f)] private float sprintSpeed = 8f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -24f;
        [SerializeField, Min(1f)] private float terminalVelocity = 50f;

        [Header("Ground Detection")]
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField, Min(0.01f)] private float groundProbeDistance = 0.12f;
        [SerializeField, Min(0f)] private float groundProbeStartOffset = 0.05f;
        [SerializeField, Min(0f)] private float groundProbeRadiusInset = 0.04f;

        [Header("Mouse Look")]
        [SerializeField, Min(0.01f)] private float mouseSensitivity = 0.12f;
        [SerializeField, Range(1f, 89f)] private float verticalLookLimit = 85f;
        [SerializeField] private bool lockCursorOnStart = true;

        private readonly RaycastHit[] groundHits = new RaycastHit[8];

        private CharacterController characterController;
        private float verticalVelocity;
        private float cameraPitch;
        private bool jumpWasHeld;
        private bool externalInputBlocked;

        public float CameraPitch => cameraPitch;

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
                Debug.LogError(
                    $"{nameof(FirstPersonController)} on '{name}' needs a Camera reference.",
                    this);
                enabled = false;
                return;
            }

            ValidateInputSystemUpdateMode();
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

            bool controlsAreActive = !externalInputBlocked
                && Cursor.lockState == CursorLockMode.Locked;

            if (controlsAreActive)
            {
                HandleLookInput();
            }

            HandleMovement(controlsAreActive);
        }

        private void OnDisable()
        {
            verticalVelocity = 0f;
            jumpWasHeld = false;
            externalInputBlocked = false;
        }

        public void SetExternalInputBlocked(bool isBlocked)
        {
            externalInputBlocked = isBlocked;
            jumpWasHeld = false;
            SetCursorLocked(!isBlocked);
        }

        private void HandleMovement(bool controlsAreActive)
        {
            Keyboard keyboard = Keyboard.current;
            Vector2 movementInput = controlsAreActive && keyboard != null
                ? ReadMovementInput(keyboard)
                : Vector2.zero;

            bool isSprinting = controlsAreActive
                && keyboard != null
                && keyboard.leftShiftKey.isPressed;

            float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

            Vector3 horizontalMovement = transform.right * movementInput.x
                + transform.forward * movementInput.y;
            horizontalMovement *= currentSpeed;

            bool jumpHeld = controlsAreActive
                && keyboard != null
                && keyboard.spaceKey.isPressed;

            bool jumpPressed = jumpHeld && !jumpWasHeld;
            jumpWasHeld = jumpHeld;

            bool isRising = verticalVelocity > 0.01f;
            bool isGrounded = !isRising && IsStandingOnGround();

            if (isGrounded)
            {
                verticalVelocity = 0f;

                if (jumpPressed)
                {
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                    isGrounded = false;
                }
            }

            if (!isGrounded)
            {
                verticalVelocity += gravity * Time.deltaTime;
                verticalVelocity = Mathf.Max(verticalVelocity, -terminalVelocity);
            }

            Vector3 finalMovement = horizontalMovement;
            finalMovement.y = verticalVelocity;

            CollisionFlags collisionFlags = characterController.Move(
                finalMovement * Time.deltaTime);

            if ((collisionFlags & CollisionFlags.Below) != 0 && verticalVelocity < 0f)
            {
                verticalVelocity = 0f;
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

        private bool IsStandingOnGround()
        {
            if (characterController.isGrounded)
            {
                return true;
            }

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
            if (externalInputBlocked)
            {
                return;
            }

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

        private void ValidateInputSystemUpdateMode()
        {
            InputSettings settings = InputSystem.settings;
            if (settings == null)
            {
                return;
            }

            if (settings.updateMode != InputSettings.UpdateMode.ProcessEventsInDynamicUpdate)
            {
                Debug.LogWarning(
                    "The FirstPersonController reads input in Update(). Run "
                    + "Hanger 51 > Setup > 3 - Configure Input and Frame Pacing.",
                    this);
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
            terminalVelocity = Mathf.Max(terminalVelocity, 1f);
            groundProbeDistance = Mathf.Max(groundProbeDistance, 0.01f);
            groundProbeStartOffset = Mathf.Max(groundProbeStartOffset, 0f);
            groundProbeRadiusInset = Mathf.Max(groundProbeRadiusInset, 0f);
        }
    }
}
