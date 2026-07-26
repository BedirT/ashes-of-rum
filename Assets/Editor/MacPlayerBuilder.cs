using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AshesOfRum.Editor
{
    public static class MacPlayerBuilder
    {
        public static void PerformBuild()
        {
            var output = GetArgumentValue("-buildOutput")
                ?? Path.GetFullPath("Builds/macOS/Ashes of Rum.app");
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            UnityEditor.OSXStandalone.UserBuildSettings.architecture = OSArchitecture.ARM64;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Bootstrap.unity" },
                locationPathName = output,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.CleanBuildCache | BuildOptions.Development
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"Build failed: {report.summary.result}");
            }

            Debug.Log($"BUILD_COMPLETE:{output}:{report.summary.totalSize}:Development:ARM64");
        }

        private static string GetArgumentValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
