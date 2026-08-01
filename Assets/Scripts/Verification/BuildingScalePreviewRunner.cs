using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AshesOfRum
{
    public sealed class BuildingScalePreviewRunner : MonoBehaviour
    {
        private const string HouseResource = "Presentation/WorldScale/House_Complete";
        private const string StorehouseResource = "Presentation/WorldScale/Storehouse_Complete";
        private const string WatchtowerResource = "Presentation/WorldScale/Watchtower_Complete";
        private const string HisarResource = "Presentation/Hisar/Hisar_Complete";

        [Serializable]
        private sealed class PreviewResult
        {
            public bool passed;
            public int archers;
            public Vector3 houseSize;
            public Vector3 storehouseSize;
            public Vector3 watchtowerSize;
            public Vector3 hisarVisualSize;
            public Vector3 hisarFootprintSize;
            public string screenshotPath;
            public string error;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartWhenRequested()
        {
            if (!HasArgument("--building-scale-preview")) return;
            var runner = new GameObject("Building Scale Preview Runner");
            DontDestroyOnLoad(runner);
            var preview = runner.AddComponent<BuildingScalePreviewRunner>();
            preview.StartCoroutine(preview.Run());
        }

        private IEnumerator Run()
        {
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            yield return null;
            yield return new WaitForEndOfFrame();

            var result = new PreviewResult();
            FormationAgent archers;
            GameObject house;
            GameObject storehouse;
            GameObject watchtower;
            GameObject hisar;
            try
            {
                var economy = FindAnyObjectByType<StartingEconomyController>()
                              ?? throw new InvalidOperationException("Starting economy was not found.");
                economy.SetOpponentEnabledForAutomation(false);

                archers = economy.DeployFriendlyForAutomation(FormationType.Archers,
                    new Vector3(-5f, 0f, 0.2f));
                archers.transform.rotation = Quaternion.Euler(0f, 8f, 0f);
                house = InstantiateRequired(HouseResource, new Vector3(1.7f, 0f, 1.2f));
                storehouse = InstantiateRequired(StorehouseResource, new Vector3(6.2f, 0f, 1.2f));
                watchtower = InstantiateRequired(WatchtowerResource, new Vector3(8.5f, 0f, 5.8f));
                hisar = InstantiateRequired(HisarResource, new Vector3(-0.5f, 0f, 6.6f));

                foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    canvas.gameObject.SetActive(false);
                HideUnrelatedRenderers(archers.transform, house.transform, storehouse.transform,
                    watchtower.transform, hisar.transform);
                HideFormationMarkers(archers);

                var camera = Camera.main ?? throw new InvalidOperationException("Main camera was not found.");
                var controller = camera.GetComponent<RtsCameraController>();
                if (controller != null) controller.enabled = false;
                var target = new Vector3(1.6f, 1.3f, 3.6f);
                camera.transform.position = target + new Vector3(0.2f, 7.5f, -17.5f);
                camera.transform.LookAt(target);
                camera.fieldOfView = 42f;

                result.archers = archers.MemberCount;
                result.houseSize = CalculateBounds(house).size;
                result.storehouseSize = CalculateBounds(storehouse).size;
                result.watchtowerSize = CalculateBounds(watchtower).size;
                result.hisarVisualSize = CalculateBounds(hisar).size;
                result.hisarFootprintSize = HisarPresentation.FootprintSize;
                CreateLabel(camera, archers.gameObject, "8 ARCHERS");
                CreateLabel(camera, house, "HOUSE - 3.0 m");
                CreateLabel(camera, storehouse, "STOREHOUSE - 3.0 m");
                CreateLabel(camera, watchtower, "WATCHTOWER - 4.0 m");
                CreateLabel(camera, hisar, "HISAR - 3.25 m");
                if (result.archers != 8) result.error = "The scale review requires one complete Archer group.";
                else if (Mathf.Abs(result.houseSize.y - 3f) > 0.05f)
                    result.error = $"House height was {result.houseSize.y:0.00} m instead of 3.00 m.";
                else if (Mathf.Abs(result.storehouseSize.y - 3f) > 0.05f)
                    result.error = $"Storehouse height was {result.storehouseSize.y:0.00} m instead of 3.00 m.";
                else if (Mathf.Abs(result.watchtowerSize.y - 4f) > 0.05f)
                    result.error = $"Watchtower height was {result.watchtowerSize.y:0.00} m instead of 4.00 m.";
                else if (Mathf.Abs(result.hisarVisualSize.y - 3.25f) > 0.05f)
                    result.error = $"Hisar visual height was {result.hisarVisualSize.y:0.00} m instead of 3.25 m.";
                else if (!Approximately(result.hisarFootprintSize, HisarPresentation.FootprintSize, 0.01f))
                    result.error = $"Hisar footprint was {result.hisarFootprintSize} instead of {HisarPresentation.FootprintSize}.";

            }
            catch (Exception exception)
            {
                result.error = exception.Message;
                Finish(result);
                yield break;
            }

            for (var frame = 0; frame < 18; frame++) yield return new WaitForEndOfFrame();

            result.screenshotPath = GetArgumentValue("--building-scale-preview-screenshot")
                                    ?? Path.Combine(Application.persistentDataPath, "building-scale-preview.png");
            var directory = Path.GetDirectoryName(result.screenshotPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            ScreenCapture.CaptureScreenshot(result.screenshotPath);
            var deadline = Time.realtimeSinceStartup + 15f;
            while ((!File.Exists(result.screenshotPath) || new FileInfo(result.screenshotPath).Length == 0) &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            if (!File.Exists(result.screenshotPath) || new FileInfo(result.screenshotPath).Length == 0)
                result.error = "The building scale screenshot was not created.";

            result.passed = string.IsNullOrEmpty(result.error);
            Finish(result);
        }

        private static GameObject InstantiateRequired(string resource, Vector3 position)
        {
            var prefab = Resources.Load<GameObject>(resource)
                         ?? throw new InvalidOperationException($"Scale-review prefab was not found: {resource}");
            return UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
        }

        private static void HideUnrelatedRenderers(params Transform[] keepRoots)
        {
            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer.name == "Bootstrap Ground" || keepRoots.Any(root => renderer.transform.IsChildOf(root)))
                    continue;
                renderer.gameObject.SetActive(false);
            }
        }

        private static void HideFormationMarkers(FormationAgent formation)
        {
            foreach (var itemRenderer in formation.GetComponentsInChildren<Renderer>(true))
                if (itemRenderer.GetComponent<FormationSelectionRing>() != null ||
                    itemRenderer.GetComponent<FormationFrontIndicator>() != null ||
                    itemRenderer.name == "Black Falcon Diamond")
                    itemRenderer.gameObject.SetActive(false);
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.gameObject.activeInHierarchy).ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException($"{root.name} has no visible renderers.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static void CreateLabel(Camera camera, GameObject target, string text)
        {
            var bounds = CalculateBounds(target);
            var label = new GameObject($"{text} Label");
            label.transform.position = new Vector3(bounds.center.x, bounds.max.y + 0.45f, bounds.center.z);
            label.transform.rotation = Quaternion.LookRotation(label.transform.position - camera.transform.position);
            var textMesh = label.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.characterSize = 0.04f;
            textMesh.fontSize = 32;
            textMesh.color = Color.white;
        }

        private static bool Approximately(Vector3 actual, Vector3 expected, float tolerance) =>
            Mathf.Abs(actual.x - expected.x) <= tolerance && Mathf.Abs(actual.y - expected.y) <= tolerance &&
            Mathf.Abs(actual.z - expected.z) <= tolerance;

        private static void Finish(PreviewResult result)
        {
            var outputPath = GetArgumentValue("--building-scale-preview-output")
                             ?? Path.Combine(Application.persistentDataPath, "building-scale-preview.json");
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(outputPath, JsonUtility.ToJson(result, true));
            Debug.Log($"BUILDING_SCALE_PREVIEW:{(result.passed ? "PASS" : "FAIL")}:{outputPath}");
            Application.Quit(result.passed ? 0 : 1);
        }

        private static bool HasArgument(string name) =>
            Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        private static string GetArgumentValue(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(arguments, name);
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
        }
    }
}
