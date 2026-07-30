using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AshesOfRum.Editor
{
    public static class CharacterAssetImportSetup
    {
        private const string RoleArgument = "-characterRole";
        private const string LoopArgument = "-loopMotions";

        public static void Configure()
        {
            var arguments = Environment.GetCommandLineArgs();
            var role = ReadArgument(arguments, RoleArgument);
            var loops = ReadArgument(arguments, LoopArgument)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .ToHashSet(StringComparer.Ordinal);

            var root = $"Assets/Art/Characters/{role}";
            var modelPath = $"{root}/Model/{role}.fbx";
            var animationFolder = $"{root}/Animations";

            ConfigureModel(modelPath);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().SingleOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                throw new InvalidOperationException($"{modelPath} did not produce a valid Humanoid Avatar.");
            }

            var importedMotions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { animationFolder }))
            {
                ConfigureAnimation(AssetDatabase.GUIDToAssetPath(guid), role, avatar, loops, importedMotions);
            }

            var unknownLoops = loops.Except(importedMotions).OrderBy(value => value).ToArray();
            if (unknownLoops.Length > 0)
            {
                throw new InvalidOperationException($"Loop motions were not found for {role}: {string.Join(", ", unknownLoops)}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Configured {role} with one Humanoid Avatar and {importedMotions.Count} motion importers.");
        }

        private static void ConfigureModel(string path)
        {
            var importer = RequireModelImporter(path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.SaveAndReimport();
        }

        private static void ConfigureAnimation(
            string path,
            string role,
            Avatar avatar,
            ISet<string> loops,
            ISet<string> importedMotions)
        {
            var importer = RequireModelImporter(path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = avatar;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;

            var clips = importer.defaultClipAnimations;
            if (clips.Length != 1)
            {
                throw new InvalidOperationException($"Expected one clip in {path}, found {clips.Length}.");
            }

            var motionName = Path.GetFileNameWithoutExtension(path);
            var prefix = $"{role}_";
            if (!motionName.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Animation filename must start with {prefix}: {path}");
            }

            motionName = motionName[prefix.Length..];
            var clip = clips[0];
            clip.name = motionName;
            clip.loopTime = loops.Contains(motionName);
            clip.loopPose = clip.loopTime;
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = false;
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = false;
            clip.lockRootPositionXZ = true;
            clip.keepOriginalPositionXZ = false;
            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();
            importedMotions.Add(motionName);
        }

        private static string ReadArgument(IReadOnlyList<string> arguments, string name)
        {
            for (var index = 0; index < arguments.Count - 1; index++)
            {
                if (arguments[index] == name && !string.IsNullOrWhiteSpace(arguments[index + 1]))
                {
                    return arguments[index + 1];
                }
            }

            throw new ArgumentException($"Required argument missing: {name}");
        }

        private static ModelImporter RequireModelImporter(string path)
        {
            return AssetImporter.GetAtPath(path) as ModelImporter
                ?? throw new InvalidOperationException($"No ModelImporter found for {path}.");
        }
    }
}
