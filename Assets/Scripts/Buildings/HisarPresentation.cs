using System;
using UnityEngine;

namespace AshesOfRum
{
    public enum HisarBuildState
    {
        Foundation,
        RaisedFrame,
        CanvasInstallation,
        Complete
    }

    public static class HisarPresentation
    {
        private const string ResourceRoot = "Presentation/Hisar";
        public static readonly Vector3 FootprintSize = new(10.6f, 3.25f, 6.6f);

        public static GameObject Create(Transform parent, HisarBuildState state, bool friendly = true)
        {
            var suffix = !friendly && state == HisarBuildState.Complete ? "_Hostile" : string.Empty;
            var resourcePath = $"{ResourceRoot}/Hisar_{state}{suffix}";
            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
                throw new InvalidOperationException($"Hisar presentation is missing: {resourcePath}");

            var instance = UnityEngine.Object.Instantiate(prefab, parent, false);
            instance.name = state == HisarBuildState.Complete
                ? (friendly ? "Authored Hisar" : "Authored Hostile Hisar")
                : $"Hisar {state}";
            return instance;
        }
    }
}
