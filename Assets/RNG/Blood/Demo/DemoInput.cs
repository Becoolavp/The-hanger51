using UnityEngine;
#if BLOOD_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Rng.Blood.Demo
{
    public static class DemoInput
    {
        public static bool Digit(int digit)
        {
#if BLOOD_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current[Key.Digit1 + (digit - 1)].wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Alpha0 + digit);
#else
            return false;
#endif
        }

        public static bool Letter(char letter)
        {
#if BLOOD_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current[Key.A + (letter - 'a')].wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.A + (letter - 'a'));
#else
            return false;
#endif
        }

        public static Vector2 Move()
        {
            Vector2 move = Vector2.zero;
#if BLOOD_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            Keyboard keys = Keyboard.current;
            if (keys == null) return move;
            if (keys.wKey.isPressed || keys.upArrowKey.isPressed) move.y += 1f;
            if (keys.sKey.isPressed || keys.downArrowKey.isPressed) move.y -= 1f;
            if (keys.dKey.isPressed || keys.rightArrowKey.isPressed) move.x += 1f;
            if (keys.aKey.isPressed || keys.leftArrowKey.isPressed) move.x -= 1f;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move.y += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move.y -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move.x += 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) move.x -= 1f;
#endif
            return move.sqrMagnitude > 1f ? move.normalized : move;
        }

        public static bool Space()
        {
#if BLOOD_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Space);
#else
            return false;
#endif
        }

        public static bool Tab()
        {
#if BLOOD_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Tab);
#else
            return false;
#endif
        }

        public static bool Click(int button)
        {
#if BLOOD_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            if (Mouse.current == null) return false;

            return button == 0 ? Mouse.current.leftButton.wasPressedThisFrame : Mouse.current.rightButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(button);
#else
            return false;
#endif
        }

        public static bool Held(int button)
        {
#if BLOOD_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            if (Mouse.current == null) return false;

            return button == 0 ? Mouse.current.leftButton.isPressed : Mouse.current.rightButton.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(button);
#else
            return false;
#endif
        }

        public static float Scroll()
        {
#if BLOOD_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.scroll.ReadValue().y / 120f : 0f;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.mouseScrollDelta.y;
#else
            return 0f;
#endif
        }

        public static Vector2 Pointer()
        {
#if BLOOD_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return Vector2.zero;
#endif
        }
    }
}
