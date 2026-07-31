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
        [SerializeField] private float groundedVelocity = -2f;
        [SerializeField, Min(1f)] private float terminalVelocity = 50f;

        [Header("Mouse Look")]
        [SerializeField, Min(0.01f)] private float mouseSensitivity = 0.12f;
        [SerializeField, Range(1f, 89f)] private float verticalLookLimit = 85f;
        [SerializeField] private bool lockCursorOnStart = true;

        private CharacterController characterController;
        private float verticalVelocity;
        private float cameraPitch;
        private bool jumpWasHeld;

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
                    $"{nameof(FirstPersonController)} on '{name}' needs a child Camera.",
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
            verticalVelocity = 0f;
            jumpWasHeld = false;
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

            bool isGrounded = characterController.isGrounded;

            if (isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = groundedVelocity;
            }

            bool jumpHeld = controlsAreActive
                && keyboard != null
                && keyboard.spaceKey.isPressed;

            bool jumpPressed = jumpHeld && !jumpWasHeld;
            jumpWasHeld = jumpHeld;

            if (isGrounded && jumpPressed)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            verticalVelocity += gravity * Time.deltaTime;
            verticalVelocity = Mathf.Max(verticalVelocity, -terminalVelocity);

            Vector3 finalMovement = horizontalMovement;
            finalMovement.y = verticalVelocity;

            CollisionFlags collisionFlags = characterController.Move(
                finalMovement * Time.deltaTime);

            if ((collisionFlags & CollisionFlags.Below) != 0 && verticalVelocity < 0f)
            {
                verticalVelocity = groundedVelocity;
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
                    "The FirstPersonController reads input in Update(). Open "
                    + "Edit > Project Settings > Input System Package and set "
                    + "Update Mode to 'Process Events In Dynamic Update'. A Fixed Update "
                    + "input mode can cause stuttering and missed jump presses.",
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
            groundedVelocity = Mathf.Min(groundedVelocity, -0.01f);
            terminalVelocity = Mathf.Max(terminalVelocity, 1f);
        }
    }
}
