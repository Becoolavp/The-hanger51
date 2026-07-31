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
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float groundedGravity = -2f;
        [SerializeField, Min(1f)] private float terminalVelocity = 50f;

        [Header("Mouse Look")]
        [SerializeField, Min(0.01f)] private float mouseSensitivity = 0.12f;
        [SerializeField, Range(1f, 89f)] private float verticalLookLimit = 85f;
        [SerializeField] private bool lockCursorOnStart = true;

        private CharacterController characterController;
        private float verticalVelocity;
        private float cameraPitch;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();

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

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            HandleLook();
            HandleMovement();
        }

        private void OnDisable()
        {
            verticalVelocity = 0f;
        }

        private void HandleMovement()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            Vector2 input = Vector2.zero;

            if (keyboard.wKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;

            input = Vector2.ClampMagnitude(input, 1f);

            bool isSprinting = keyboard.leftShiftKey.isPressed;
            float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

            Vector3 horizontalMovement = transform.right * input.x + transform.forward * input.y;
            horizontalMovement *= currentSpeed;

            bool wasGrounded = characterController.isGrounded;

            if (wasGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = groundedGravity;
            }

            if (wasGrounded && keyboard.spaceKey.wasPressedThisFrame)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
                verticalVelocity = Mathf.Max(verticalVelocity, -terminalVelocity);
            }

            Vector3 finalMovement = horizontalMovement;
            finalMovement.y = verticalVelocity;

            CollisionFlags collisionFlags = characterController.Move(finalMovement * Time.deltaTime);

            bool hitGround = (collisionFlags & CollisionFlags.Below) != 0;
            bool hitCeiling = (collisionFlags & CollisionFlags.Above) != 0;

            if (hitGround && verticalVelocity < 0f)
            {
                verticalVelocity = groundedGravity;
            }

            if (hitCeiling && verticalVelocity > 0f)
            {
                verticalVelocity = 0f;
            }
        }

        private void HandleLook()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            Vector2 mouseDelta = mouse.delta.ReadValue() * mouseSensitivity;

            cameraPitch -= mouseDelta.y;
            cameraPitch = Mathf.Clamp(cameraPitch, -verticalLookLimit, verticalLookLimit);

            playerCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
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

            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
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
            groundedGravity = Mathf.Min(groundedGravity, -0.01f);
            terminalVelocity = Mathf.Max(terminalVelocity, 1f);
        }
    }
}
