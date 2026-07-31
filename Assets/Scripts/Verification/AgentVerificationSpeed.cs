using System.Globalization;
using UnityEngine;

namespace AshesOfRum
{
    public static class AgentVerificationSpeed
    {
        public const float Default = 1f;
        public const float Maximum = 100f;
        public const string Argument = "--agent-simulation-speed";

        public static bool TryRead(string[] arguments, out float speed, out string error)
        {
            speed = Default;
            error = null;
            for (var index = 0; index < arguments.Length; index++)
            {
                if (arguments[index] != Argument) continue;
                if (index + 1 >= arguments.Length ||
                    !float.TryParse(arguments[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture,
                        out speed) || !float.IsFinite(speed) || speed <= 0f || speed > Maximum)
                {
                    speed = Default;
                    error = $"{Argument} must be greater than zero and at most {Maximum}.";
                    return false;
                }

                return true;
            }

            return true;
        }

        public static void Apply(float speed)
        {
            Time.timeScale = speed;
        }
    }
}
