using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace AshesOfRum
{
    public sealed class FogOfWarSystem : MonoBehaviour
    {
        public const float MinX = -21f;
        public const float MaxX = 21f;
        public const float MinZ = -12f;
        public const float MaxZ = 28f;
        private const float CellSize = 1f;
        private const float RefreshSeconds = 0.1f;

        private readonly List<Transform> friendlySources = new();
        private readonly Dictionary<GameObject, FogVisibilityTarget> hostileTargets = new();
        private FogOfWarMap map;
        private float sightRadius;
        private float refreshRemaining;
        private Texture2D fogTexture;
        private Texture2D minimapTexture;
        private RawImage minimapImage;
        private RtsCameraController cameraController;

        public FogOfWarMap Map => map;
        public RawImage MinimapImage => minimapImage;
        public event Action<GameObject> HostileFirstRevealed;

        public void Initialize(float sharedSightRadius, RtsCameraController targetCamera, Transform hudParent)
        {
            sightRadius = sharedSightRadius;
            cameraController = targetCamera;
            map = new FogOfWarMap(MinX, MaxX, MinZ, MaxZ, CellSize);
            CreateBattlefieldOverlay();
            CreateMinimap(hudParent);
        }

        public void RegisterFriendly(Transform source)
        {
            if (source != null && !friendlySources.Contains(source)) friendlySources.Add(source);
        }

        public void RegisterHostileMobile(GameObject target) => RegisterHostile(target, false);

        public void RegisterHostileStatic(GameObject target) => RegisterHostile(target, true);

        public void UnregisterHostile(GameObject target)
        {
            if (target == null || !hostileTargets.Remove(target)) return;
            UpdateMinimapTexture();
        }

        public bool IsCurrentlyVisible(Component target) => target != null &&
            hostileTargets.TryGetValue(target.gameObject, out var visibility) &&
            visibility.State == FogState.Visible;

        public FogState StateAt(Vector3 position) => map?.StateAt(position) ?? FogState.Visible;

        public Color MinimapColorAt(Vector3 position)
        {
            if (map == null || minimapTexture == null) return Color.clear;
            var uv = map.WorldToUv(position);
            var x = Mathf.Clamp(Mathf.RoundToInt(uv.x * (map.Columns - 1)), 0, map.Columns - 1);
            var y = Mathf.Clamp(Mathf.RoundToInt(uv.y * (map.Rows - 1)), 0, map.Rows - 1);
            return minimapTexture.GetPixel(x, y);
        }

        public void RefreshNow()
        {
            if (map == null) return;
            friendlySources.RemoveAll(source => source == null);
            var positions = friendlySources.Where(IsLivingSource).Select(source => source.position).ToArray();
            map.UpdateVisibility(positions, sightRadius);
            foreach (var pair in hostileTargets.ToArray())
            {
                if (pair.Key == null || pair.Value == null || !IsLivingHostile(pair.Key))
                {
                    hostileTargets.Remove(pair.Key);
                    continue;
                }
                if (pair.Value.Apply(map.StateAt(pair.Key.transform.position)))
                    HostileFirstRevealed?.Invoke(pair.Key);
            }
            UpdateFogTexture();
            UpdateMinimapTexture();
        }

        private void Update()
        {
            refreshRemaining -= Time.unscaledDeltaTime;
            if (refreshRemaining > 0f) return;
            refreshRemaining = RefreshSeconds;
            RefreshNow();
        }

        private void RegisterHostile(GameObject target, bool remembersWhenExplored)
        {
            if (target == null || hostileTargets.ContainsKey(target)) return;
            var visibility = target.GetComponent<FogVisibilityTarget>() ?? target.AddComponent<FogVisibilityTarget>();
            visibility.Initialize(remembersWhenExplored);
            hostileTargets.Add(target, visibility);
            if (visibility.Apply(map.StateAt(target.transform.position))) HostileFirstRevealed?.Invoke(target);
        }

        private static bool IsLivingSource(Transform source)
        {
            if (source == null) return false;
            var formation = source.GetComponent<FormationAgent>();
            if (formation != null) return formation.MemberCount > 0;
            var building = source.GetComponent<ConstructibleBuilding>();
            return building == null || !building.IsDestroyed;
        }

        private static bool IsLivingHostile(GameObject target)
        {
            var formation = target.GetComponent<FormationAgent>();
            return formation == null || formation.MemberCount > 0;
        }

        private void CreateBattlefieldOverlay()
        {
            fogTexture = new Texture2D(map.Columns, map.Rows, TextureFormat.RGBA32, false)
            {
                name = "Battlefield Fog Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var overlay = new GameObject("Battlefield Fog", typeof(MeshFilter), typeof(MeshRenderer));
            overlay.transform.SetParent(transform, false);
            var mesh = new Mesh { name = "Battlefield Fog Mesh" };
            mesh.vertices = new[]
            {
                new Vector3(MinX, 0.08f, MinZ), new Vector3(MaxX, 0.08f, MinZ),
                new Vector3(MinX, 0.08f, MaxZ), new Vector3(MaxX, 0.08f, MaxZ)
            };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            overlay.GetComponent<MeshFilter>().sharedMesh = mesh;
            var shader = Shader.Find("UI/Default");
            if (shader == null) throw new InvalidOperationException("Required fog shader is unavailable.");
            var material = new Material(shader) { name = "Battlefield Fog Material", renderQueue = (int)RenderQueue.Transparent };
            material.SetTexture("_MainTex", fogTexture);
            material.SetColor("_Color", Color.white);
            overlay.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private void CreateMinimap(Transform hudParent)
        {
            var frame = new GameObject("Minimap Frame", typeof(RectTransform), typeof(Image));
            frame.transform.SetParent(hudParent, false);
            var frameRect = frame.GetComponent<RectTransform>();
            frameRect.anchorMin = new Vector2(0.80f, 0.17f);
            frameRect.anchorMax = new Vector2(0.98f, 0.43f);
            frameRect.offsetMin = frameRect.offsetMax = Vector2.zero;
            frame.GetComponent<Image>().color = new Color(0.02f, 0.035f, 0.05f, 0.96f);

            var imageObject = new GameObject("Fog Minimap", typeof(RectTransform), typeof(RawImage),
                typeof(MinimapClickHandler));
            imageObject.transform.SetParent(frame.transform, false);
            var rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.035f, 0.035f);
            rect.anchorMax = new Vector2(0.965f, 0.965f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            minimapTexture = new Texture2D(map.Columns, map.Rows, TextureFormat.RGBA32, false)
            {
                name = "Fog Minimap Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            minimapImage = imageObject.GetComponent<RawImage>();
            minimapImage.texture = minimapTexture;
            imageObject.GetComponent<MinimapClickHandler>().Initialize(map, cameraController, rect);
        }

        private void UpdateFogTexture()
        {
            var colors = new Color[map.Columns * map.Rows];
            for (var row = 0; row < map.Rows; row++)
            for (var column = 0; column < map.Columns; column++)
                colors[row * map.Columns + column] = map.StateAt(column, row) switch
                {
                    FogState.Visible => new Color(0f, 0f, 0f, 0f),
                    FogState.Explored => new Color(0.025f, 0.035f, 0.05f, 0.58f),
                    _ => new Color(0.005f, 0.008f, 0.012f, 0.94f)
                };
            fogTexture.SetPixels(colors);
            fogTexture.Apply(false);
        }

        private void UpdateMinimapTexture()
        {
            var colors = new Color[map.Columns * map.Rows];
            for (var row = 0; row < map.Rows; row++)
            for (var column = 0; column < map.Columns; column++)
                colors[row * map.Columns + column] = map.StateAt(column, row) switch
                {
                    FogState.Visible => new Color(0.38f, 0.32f, 0.24f),
                    FogState.Explored => new Color(0.12f, 0.12f, 0.12f),
                    _ => new Color(0.01f, 0.015f, 0.02f)
                };
            foreach (var source in friendlySources.Where(source => source != null && IsLivingSource(source)))
                DrawMarker(colors, source.position, new Color(0.08f, 0.42f, 1f));
            foreach (var pair in hostileTargets)
                if (pair.Key != null && pair.Value != null && pair.Value.State == FogState.Visible)
                    DrawMarker(colors, pair.Key.transform.position, new Color(0.95f, 0.16f, 0.06f));
            minimapTexture.SetPixels(colors);
            minimapTexture.Apply(false);
        }

        private void DrawMarker(Color[] colors, Vector3 position, Color color)
        {
            var uv = map.WorldToUv(position);
            var centerX = Mathf.Clamp(Mathf.RoundToInt(uv.x * (map.Columns - 1)), 0, map.Columns - 1);
            var centerY = Mathf.Clamp(Mathf.RoundToInt(uv.y * (map.Rows - 1)), 0, map.Rows - 1);
            for (var y = centerY - 1; y <= centerY + 1; y++)
            for (var x = centerX - 1; x <= centerX + 1; x++)
                if (x >= 0 && x < map.Columns && y >= 0 && y < map.Rows)
                    colors[y * map.Columns + x] = color;
        }
    }

    public sealed class FogVisibilityTarget : MonoBehaviour
    {
        private Renderer[] renderers;
        private Collider[] colliders;
        private Color[] originalColors;
        private bool remembersWhenExplored;
        private bool hasEverBeenVisible;

        public FogState State { get; private set; } = FogState.Unexplored;

        public void Initialize(bool rememberStaticTarget)
        {
            remembersWhenExplored = rememberStaticTarget;
            hasEverBeenVisible = false;
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
            originalColors = renderers.Select(itemRenderer => itemRenderer.material.color).ToArray();
        }

        public bool Apply(FogState state)
        {
            var firstReveal = state == FogState.Visible && !hasEverBeenVisible;
            if (state == FogState.Visible) hasEverBeenVisible = true;
            State = state;
            var show = state == FogState.Visible ||
                       remembersWhenExplored && hasEverBeenVisible && state == FogState.Explored;
            for (var index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] == null) continue;
                renderers[index].enabled = show;
                if (show)
                    renderers[index].material.color = state == FogState.Explored
                        ? Color.Lerp(originalColors[index], Color.black, 0.65f)
                        : originalColors[index];
            }
            foreach (var itemCollider in colliders)
                if (itemCollider != null) itemCollider.enabled = state == FogState.Visible;
            return firstReveal;
        }
    }

    public sealed class MinimapClickHandler : MonoBehaviour, IPointerClickHandler
    {
        private FogOfWarMap map;
        private RtsCameraController cameraController;
        private RectTransform rectTransform;

        public void Initialize(FogOfWarMap fogMap, RtsCameraController targetCamera, RectTransform targetRect)
        {
            map = fogMap;
            cameraController = targetCamera;
            rectTransform = targetRect;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position,
                    eventData.pressEventCamera, out var local)) return;
            var rect = rectTransform.rect;
            var uv = new Vector2(Mathf.InverseLerp(rect.xMin, rect.xMax, local.x),
                Mathf.InverseLerp(rect.yMin, rect.yMax, local.y));
            var world = map.UvToWorld(uv);
            if (map.StateAt(world) == FogState.Unexplored) return;
            cameraController.CenterOn(world);
        }
    }
}
