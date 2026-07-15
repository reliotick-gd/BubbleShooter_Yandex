using UnityEngine;
using UnityEngine.InputSystem;

namespace CozyAnimalTown
{
    /// <summary>
    /// Обёртка над новой Input System (в проекте activeInputHandler=1).
    /// Pointer объединяет мышь / тач / перо — то, что нужно для веба и моба.
    /// </summary>
    public static class InputService
    {
        public static bool HasPointer => Pointer.current != null;

        public static bool PressedThisFrame =>
            Pointer.current != null && Pointer.current.press.wasPressedThisFrame;

        public static bool ReleasedThisFrame =>
            Pointer.current != null && Pointer.current.press.wasReleasedThisFrame;

        public static bool IsPressed =>
            Pointer.current != null && Pointer.current.press.isPressed;

        public static Vector3 WorldPos(Camera cam)
        {
            if (cam == null || Pointer.current == null) return Vector3.zero;
            Vector3 s = Pointer.current.position.ReadValue();
            s.z = -cam.transform.position.z; // расстояние до плоскости z=0
            return cam.ScreenToWorldPoint(s);
        }
    }
}
