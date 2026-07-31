using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AshesOfRum.Tests
{
    public sealed partial class StartingEconomyPlayModeTests
    {
        private static IEnumerator LoadEconomy(float simulationSpeed = 20f)
        {
            yield return SceneManager.LoadSceneAsync(HarnessContract.SceneName, LoadSceneMode.Single);
            yield return null;
            Time.timeScale = simulationSpeed;
            Object.FindAnyObjectByType<StartingEconomyController>()?.SetOpponentEnabledForAutomation(false);
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition)
        {
            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, $"Condition did not become true within {TimeoutSeconds} seconds.");
        }

        private static int PlacementPreviewCount() =>
            GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Count(item => item.name.EndsWith("Placement Preview"));

        private static void SetPrivateField<T>(object target, string fieldName, T value) =>
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);

        private static T GetPrivateField<T>(object target, string fieldName) =>
            (T)target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target);

        private static void InvokePrivateMethod(object target, string methodName, params object[] arguments) =>
            target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, arguments);

        private static void PressControlGroupHotkey(StartingEconomyController economy, Keyboard keyboard,
            params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.Update();
            InvokePrivateMethod(economy, "HandleControlGroupInput");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            InvokePrivateMethod(economy, "HandleControlGroupInput");
        }

        private static void QueueCoalescedKeyboardChord(Keyboard keyboard, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
        }

        private static IEnumerator PressMouseButton(StartingEconomyController economy, Mouse mouse, Vector2 position,
            MouseButton button, string handler)
        {
            var buttonControl = button == MouseButton.Left ? mouse.leftButton : mouse.rightButton;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(button));
            InputSystem.Update();
            Assert.That(Mouse.current, Is.SameAs(mouse));
            Assert.That(buttonControl.isPressed, Is.True);
            InvokePrivateMethod(economy, handler);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            InputSystem.Update();
            Assert.That(buttonControl.isPressed, Is.False);
            InvokePrivateMethod(economy, handler);
            yield break;
        }

        private static void QueueCoalescedClick(Mouse mouse, Vector2 position, MouseButton button)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(button));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            InputSystem.Update();
            Assert.That(Mouse.current, Is.SameAs(mouse));
            Assert.That(button == MouseButton.Left ? mouse.leftButton.isPressed : mouse.rightButton.isPressed,
                Is.False);
        }

        private static IEnumerator DragBattlefieldSelection(StartingEconomyController economy, Mouse mouse,
            Vector2 start, Vector2 end)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = start }.WithButton(MouseButton.Left));
            InputSystem.Update();
            Assert.That(Mouse.current, Is.SameAs(mouse));
            Assert.That(mouse.leftButton.isPressed, Is.True);
            InvokePrivateMethod(economy, "HandleSelectionInput");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = end }.WithButton(MouseButton.Left));
            InputSystem.Update();
            InvokePrivateMethod(economy, "HandleSelectionInput");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = end });
            InputSystem.Update();
            Assert.That(mouse.leftButton.isPressed, Is.False);
            InvokePrivateMethod(economy, "HandleSelectionInput");
            yield break;
        }

        private static FormationAgent CreateFormationForTest(string name, FormationType type, bool friendly,
            EconomyTuning tuning, System.Func<IEnumerable<FormationAgent>> availableHostiles = null,
            System.Action<int> onCasualty = null, System.Action<Vector3> onAttack = null)
        {
            var root = new GameObject(name);
            root.transform.position = new Vector3(100f, 0f, 100f);
            var formation = root.AddComponent<FormationAgent>();
            formation.Initialize(type, friendly, tuning, onCasualty, availableHostiles: availableHostiles,
                onAttack: onAttack);
            return formation;
        }

        private static GameObject CreateRouteBlocker(string name, Vector3 position, Vector3 scale)
        {
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = name;
            blocker.transform.position = position;
            blocker.transform.localScale = scale;
            var obstacle = blocker.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
            return blocker;
        }

        private static void AssertSweptSegmentOutsideBounds(Vector3 start, Vector3 end, Bounds bounds)
        {
            var distance = Vector3.Distance(start, end);
            var samples = Mathf.Max(1, Mathf.CeilToInt(distance / 0.02f));
            for (var sample = 0; sample <= samples; sample++)
            {
                var position = Vector3.Lerp(start, end, sample / (float)samples);
                var footprintPosition = new Vector3(position.x, bounds.center.y, position.z);
                Assert.That(bounds.Contains(footprintPosition), Is.False,
                    $"The swept member step must not cross the carved obstacle at {footprintPosition}.");
            }
        }
    }
}
