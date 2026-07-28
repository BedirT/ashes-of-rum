using System;
using UnityEngine;
using UnityEngine.UI;

namespace AshesOfRum
{
    public sealed class HealthBarVisibilityState
    {
        private readonly float damagedDuration;
        private float damagedUntil = float.NegativeInfinity;

        public HealthBarVisibilityState(float damagedDisplaySeconds)
        {
            damagedDuration = Mathf.Max(0f, damagedDisplaySeconds);
        }

        public bool IsSelected { get; private set; }
        public bool IsHovered { get; private set; }

        public void SetSelected(bool selected) => IsSelected = selected;
        public void SetHovered(bool hovered) => IsHovered = hovered;
        public void RecordDamage(float now) => damagedUntil = now + damagedDuration;

        public bool ShouldShow(float now) => IsSelected || IsHovered || now < damagedUntil;
    }

    public sealed class WorldHealthBar : MonoBehaviour
    {
        public const string ObjectName = "Health Bar";
        public const float DamagedDisplaySeconds = 3f;

        private Func<int> currentHealth;
        private Func<int> maximumHealth;
        private Func<bool> isCurrentlyVisible;
        private HealthBarVisibilityState visibilityState;
        private Canvas canvas;
        private RectTransform fill;
        private Camera worldCamera;

        public bool IsVisible => canvas != null && canvas.gameObject.activeSelf;
        public float FillFraction { get; private set; } = 1f;

        public void Initialize(Func<int> health, Func<int> maxHealth, float height, Color factionColor,
            Func<bool> visibilityPredicate = null)
        {
            currentHealth = health;
            maximumHealth = maxHealth;
            isCurrentlyVisible = visibilityPredicate;
            visibilityState = new HealthBarVisibilityState(DamagedDisplaySeconds);
            worldCamera = Camera.main;

            var canvasObject = new GameObject(ObjectName, typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.transform.localPosition = Vector3.up * height;
            canvasObject.transform.localScale = Vector3.one * 0.012f;
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;
            var canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(110f, 14f);

            var background = CreateImage(canvasObject.transform, "Background", new Color(0.015f, 0.02f, 0.025f, 0.92f));
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.offsetMin = background.offsetMax = Vector2.zero;

            fill = CreateImage(canvasObject.transform, "Fill", factionColor);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = Vector2.one;
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = new Vector2(2f, 2f);
            fill.offsetMax = new Vector2(-2f, -2f);

            RefreshNow();
        }

        public void SetSelected(bool selected)
        {
            visibilityState?.SetSelected(selected);
            RefreshNow();
        }

        public void SetHovered(bool hovered)
        {
            visibilityState?.SetHovered(hovered);
            RefreshNow();
        }

        public void RecordDamage()
        {
            visibilityState?.RecordDamage(Time.unscaledTime);
            RefreshNow();
        }

        public void RefreshNow()
        {
            if (canvas == null || currentHealth == null || maximumHealth == null || visibilityState == null) return;
            FillFraction = Mathf.Clamp01((float)Mathf.Max(0, currentHealth()) / Mathf.Max(1, maximumHealth()));
            fill.anchorMax = new Vector2(FillFraction, 1f);
            var visibleInWorld = isCurrentlyVisible == null || isCurrentlyVisible();
            canvas.gameObject.SetActive(visibleInWorld && visibilityState.ShouldShow(Time.unscaledTime) &&
                                        currentHealth() > 0);
        }

        private void LateUpdate()
        {
            RefreshNow();
            if (!IsVisible) return;
            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera != null) canvas.transform.rotation = worldCamera.transform.rotation;
        }

        private static RectTransform CreateImage(Transform parent, string name, Color color)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image.rectTransform;
        }
    }
}
