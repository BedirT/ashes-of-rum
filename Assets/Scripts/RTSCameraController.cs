using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace AshesOfRum
{
    [RequireComponent(typeof(Camera))]
    public sealed class RTSCameraController : MonoBehaviour
    {
        [SerializeField] private float keyboardSpeed = 30f;
        [SerializeField] private float edgeSpeed = 24f;
        [SerializeField] private float dragSpeed = 0.08f;
        [SerializeField] private float zoomSpeed = 0.035f;
        [SerializeField] private float edgeThickness = 12f;
        [SerializeField] private Vector2 xBounds = new(-48f, 48f);
        [SerializeField] private Vector2 zBounds = new(-30f, 30f);
        [SerializeField] private Vector2 zoomBounds = new(16f, 40f);

        private Camera controlledCamera;
        private Vector2 previousMousePosition;

        public Vector2 XBounds => xBounds;
        public Vector2 ZBounds => zBounds;
        public Vector2 ZoomBounds => zoomBounds;

        private void Awake()
        {
            controlledCamera = GetComponent<Camera>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            var motion = Vector2.zero;

            if (keyboard != null)
            {
                motion.x += ReadAxis(keyboard.aKey, keyboard.leftArrowKey, keyboard.dKey, keyboard.rightArrowKey);
                motion.y += ReadAxis(keyboard.sKey, keyboard.downArrowKey, keyboard.wKey, keyboard.upArrowKey);

                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    QuitGame();
                }
            }

            if (mouse != null)
            {
                var position = mouse.position.ReadValue();
                if (position.x <= edgeThickness) motion.x -= 1f;
                if (position.x >= Screen.width - edgeThickness) motion.x += 1f;
                if (position.y <= edgeThickness) motion.y -= 1f;
                if (position.y >= Screen.height - edgeThickness) motion.y += 1f;

                if (mouse.middleButton.wasPressedThisFrame)
                {
                    previousMousePosition = position;
                }

                if (mouse.middleButton.isPressed)
                {
                    var delta = position - previousMousePosition;
                    transform.position += new Vector3(-delta.x * dragSpeed, 0f, -delta.y * dragSpeed);
                    previousMousePosition = position;
                }

                controlledCamera.orthographicSize = Mathf.Clamp(
                    controlledCamera.orthographicSize - mouse.scroll.ReadValue().y * zoomSpeed,
                    zoomBounds.x,
                    zoomBounds.y);
            }

            var speed = IsAtScreenEdge(mouse) ? edgeSpeed : keyboardSpeed;
            transform.position += new Vector3(motion.x, 0f, motion.y).normalized * speed * Time.unscaledDeltaTime;
            transform.position = ClampPosition(transform.position, xBounds, zBounds);
        }

        public static Vector3 ClampPosition(Vector3 position, Vector2 horizontalBounds, Vector2 depthBounds)
        {
            position.x = Mathf.Clamp(position.x, horizontalBounds.x, horizontalBounds.y);
            position.z = Mathf.Clamp(position.z, depthBounds.x, depthBounds.y);
            return position;
        }

        public void QuitGame()
        {
            Application.Quit();
        }

        private static float ReadAxis(KeyControl negativeA, KeyControl negativeB, KeyControl positiveA, KeyControl positiveB)
        {
            return (positiveA.isPressed || positiveB.isPressed ? 1f : 0f)
                 - (negativeA.isPressed || negativeB.isPressed ? 1f : 0f);
        }

        private bool IsAtScreenEdge(Mouse mouse)
        {
            if (mouse == null) return false;
            var position = mouse.position.ReadValue();
            return position.x <= edgeThickness || position.x >= Screen.width - edgeThickness
                || position.y <= edgeThickness || position.y >= Screen.height - edgeThickness;
        }
    }
}
