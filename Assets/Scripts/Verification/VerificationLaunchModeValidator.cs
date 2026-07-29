using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshesOfRum
{
    public static class VerificationLaunchModeValidator
    {
        public const string ConflictMarker = "VERIFICATION_LAUNCH_CONFLICT";
        public const string ConflictReason = "multiple_verification_modes";

        private static bool conflictReported;

        public static bool IsValid(bool smoke, bool scriptedAgent, bool liveAgent, out string reason)
        {
            var enabledModes = new List<string>(3);
            if (smoke) enabledModes.Add("--smoke-test");
            if (scriptedAgent) enabledModes.Add("--agent-script");
            if (liveAgent) enabledModes.Add("--agent-live-dir");
            reason = enabledModes.Count > 1
                ? $"{ConflictReason}:{string.Join(",", enabledModes)}"
                : null;
            return reason == null;
        }

        public static bool AllowsCurrentProcess()
        {
            var arguments = Environment.GetCommandLineArgs();
            if (IsValid(HasArgument(arguments, "--smoke-test"), HasArgument(arguments, "--agent-script"),
                    HasArgument(arguments, "--agent-live-dir"), out var reason))
                return true;

            if (!conflictReported)
            {
                conflictReported = true;
                Debug.LogError($"{ConflictMarker}:{reason}");
                Application.Quit(2);
            }
            return false;
        }

        private static bool HasArgument(string[] arguments, string name) => Array.IndexOf(arguments, name) >= 0;
    }
}
