using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AshesOfRum
{
    public sealed partial class StartingEconomyController : MonoBehaviour
    {

        private sealed class ControlGroup
        {
            public ControlGroup(IEnumerable<WorkerAgent> workers, IEnumerable<FormationAgent> formations)
            {
                Workers = workers.ToList();
                Formations = formations.ToList();
            }

            public List<WorkerAgent> Workers { get; }
            public List<FormationAgent> Formations { get; }
        }

        private readonly struct PointerButtonTransition
        {
            public PointerButtonTransition(bool pressed, Vector2 position, bool modify)
            {
                Pressed = pressed;
                Position = position;
                Modify = modify;
            }

            public bool Pressed { get; }
            public Vector2 Position { get; }
            public bool Modify { get; }
        }

        private enum InputCommand
        {
            LeftPressed,
            LeftReleased,
            RightPressed,
            KeyPressed,
            ControlGroupPressed
        }

        private readonly struct QueuedInput
        {
            private QueuedInput(InputCommand command, Vector2 position, bool modify, Key key, int number,
                bool assigning)
            {
                Command = command;
                Position = position;
                Modify = modify;
                Key = key;
                Number = number;
                Assigning = assigning;
            }

            public InputCommand Command { get; }
            public Vector2 Position { get; }
            public bool Modify { get; }
            public Key Key { get; }
            public int Number { get; }
            public bool Assigning { get; }

            public static QueuedInput Pointer(InputCommand command, Vector2 position, bool modify = false) =>
                new(command, position, modify, Key.None, 0, false);

            public static QueuedInput KeyPress(Key key) =>
                new(InputCommand.KeyPressed, default, false, key, 0, false);

            public static QueuedInput ControlGroup(int number, bool assigning) =>
                new(InputCommand.ControlGroupPressed, default, false, Key.None, number, assigning);
        }

        private void TintPreview(Color color)
        {
            foreach (var itemRenderer in placementPreview.GetComponentsInChildren<Renderer>())
                itemRenderer.material.color = color;
        }

        private void SetOrderFeedback(string message)
        {
            if (orderText != null) orderText.text = message.ToUpperInvariant();
        }

        private void NotifyEconomyState(string message)
        {
            LastEconomyNotification = message;
            SetOrderFeedback(message);
        }

        private static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent,
            Vector3 localPosition, Vector3 scale, Color color)
        {
            var result = GameObject.CreatePrimitive(type);
            result.name = name;
            if (parent != null) result.transform.SetParent(parent, false);
            result.transform.localPosition = localPosition;
            result.transform.localScale = scale;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { color = color };
            result.GetComponent<Renderer>().sharedMaterial = material;
            return result;
        }

        private static void CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.025f, 0.04f, 0.065f, 0.9f);
        }

        private static Text CreateText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            int fontSize, TextAnchor alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = new Color(0.92f, 0.9f, 0.82f);
            text.alignment = alignment;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            string label, UnityEngine.Events.UnityAction onClick)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            buttonObject.GetComponent<Image>().color = new Color(0.08f, 0.22f, 0.38f, 0.95f);
            var button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            CreateText(buttonObject.transform, "Label", Vector2.zero, Vector2.one, 16, TextAnchor.MiddleCenter).text = label;
            return button;
        }

        private static Rect ScreenRect(Vector2 start, Vector2 end)
        {
            return Rect.MinMaxRect(Mathf.Min(start.x, end.x), Mathf.Min(start.y, end.y),
                Mathf.Max(start.x, end.x), Mathf.Max(start.y, end.y));
        }

        private void UpdateSelectionBox(Vector2 start, Vector2 end)
        {
            var rect = ScreenRect(start, end);
            selectionBoxTransform.position = rect.center;
            selectionBoxTransform.sizeDelta = rect.size;
        }

        private static Vector3 FormationOffset(int index, int count)
        {
            var columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            return new Vector3((index % columns - (columns - 1) * 0.5f) * 1.1f, 0f, index / columns * 1.1f);
        }

        private static void CreateOrderMarker(Vector3 position, Color color)
        {
            var marker = CreatePrimitive(PrimitiveType.Cylinder, "Order Marker", null,
                position + Vector3.up * 0.05f, new Vector3(0.65f, 0.025f, 0.65f), color);
            Object.Destroy(marker.GetComponent<Collider>());
            Object.Destroy(marker, 0.8f);
        }
    }
}
