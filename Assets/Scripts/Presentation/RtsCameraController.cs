using UnityEngine;
using UnityEngine.InputSystem;

namespace AshesOfRum
{
    [RequireComponent(typeof(Camera))]
    public sealed class RtsCameraController : MonoBehaviour
    {
        private const float PanSpeed = 18f;
        private const float EdgeSize = 12f;
        private const float DragScale = 0.025f;
        private Vector2 previousMousePosition;

        public Vector3 LastRequestedCenter { get; private set; }

        public void CenterOn(Vector3 worldPosition)
        {
            LastRequestedCenter = worldPosition;
            var position = transform.position;
            var forward = transform.forward;
            var groundOffset = forward.y < -0.01f
                ? forward * (position.y / -forward.y)
                : Vector3.zero;
            position.x = Mathf.Clamp(worldPosition.x - groundOffset.x, -20f, 20f);
            position.z = Mathf.Clamp(worldPosition.z - groundOffset.z, -25f, 12f);
            transform.position = position;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;

            var move = Vector3.zero;
            var dragMovement = Vector3.zero;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move += Vector3.forward;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move += Vector3.back;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move += Vector3.left;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move += Vector3.right;

            var mousePosition = mouse.position.ReadValue();
            if (mousePosition.x > 0f && mousePosition.x <= EdgeSize) move += Vector3.left;
            if (mousePosition.x < Screen.width && mousePosition.x >= Screen.width - EdgeSize) move += Vector3.right;
            if (mousePosition.y > 0f && mousePosition.y <= EdgeSize) move += Vector3.back;
            if (mousePosition.y < Screen.height && mousePosition.y >= Screen.height - EdgeSize) move += Vector3.forward;

            if (mouse.middleButton.wasPressedThisFrame) previousMousePosition = mousePosition;
            if (mouse.middleButton.isPressed)
            {
                var delta = mousePosition - previousMousePosition;
                dragMovement = new Vector3(-delta.x, 0f, -delta.y) * DragScale;
                previousMousePosition = mousePosition;
            }

            var position = transform.position + move.normalized * (PanSpeed * Time.unscaledDeltaTime) + dragMovement;
            var scroll = mouse.scroll.ReadValue().y;
            position.y = Mathf.Clamp(position.y - scroll * 0.012f, 10f, 22f);
            position.x = Mathf.Clamp(position.x, -20f, 20f);
            position.z = Mathf.Clamp(position.z, -25f, 12f);
            transform.position = position;
        }
    }
}
