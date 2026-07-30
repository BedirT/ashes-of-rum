using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AshesOfRum;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AshesOfRum.Editor
{
    public static class ArcherRuntimeAssetSetup
    {
        public const string MemberPrefabPath = "Assets/Resources/Presentation/ArcherMember.prefab";
        public const string ProjectilePrefabPath = "Assets/Resources/Presentation/ArcherArrowProjectile.prefab";
        public const string ControllerPath = "Assets/Resources/Presentation/Archer.controller";
        public const string BodyMaterialPath = "Assets/Resources/Presentation/ArcherBody.mat";
        public const string BodyMeshPath = "Assets/Resources/Presentation/ArcherBodyMesh.asset";
        public const string BowMaterialPath = "Assets/Resources/Presentation/ArcherBow.mat";
        public const string ArrowMaterialPath = "Assets/Resources/Presentation/ArcherArrow.mat";

        private const string ArcherRoot = "Assets/Art/Characters/Archer";
        private const string OutputFolder = "Assets/Resources/Presentation";
        private const string ModelPath = ArcherRoot + "/Model/Archer.fbx";
        private const string BowPath = ArcherRoot + "/Equipment/Bow/Archer_Bow.fbx";
        private const string ArrowPath = ArcherRoot + "/Equipment/Arrow/Archer_Arrow.fbx";

        public static void Configure()
        {
            Directory.CreateDirectory(OutputFolder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var bodyMaterial = CreateMaterial(BodyMaterialPath, ArcherRoot + "/Model/Textures/Archer", true);
            var bowMaterial = CreateMaterial(BowMaterialPath, ArcherRoot + "/Equipment/Bow/Archer_Bow", true);
            var arrowMaterial = CreateMaterial(ArrowMaterialPath, ArcherRoot + "/Equipment/Arrow/Archer_Arrow", false);
            var controller = CreateController();
            CreateMemberPrefab(bodyMaterial, bowMaterial, controller);
            CreateProjectilePrefab(arrowMaterial);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("Configured runtime Archer member, controller, materials, and projectile prefabs.");
        }

        private static Material CreateMaterial(string outputPath, string textureStem, bool useNormalMap)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? throw new InvalidOperationException("Universal Render Pipeline/Lit shader was not found.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(outputPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, outputPath);
            }
            material.shader = shader;
            material.name = Path.GetFileNameWithoutExtension(outputPath);
            material.color = Color.white;
            material.SetTexture("_BaseMap", RequireAsset<Texture2D>(textureStem + "_BaseColor.png"));
            material.SetFloat("_Smoothness", 0.25f);
            material.SetFloat("_Metallic", 0.1f);
            if (useNormalMap)
            {
                material.SetTexture("_BumpMap", RequireAsset<Texture2D>(textureStem + "_Normal.png"));
                material.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static AnimatorController CreateController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.name = "Archer";
            var stateMachine = controller.layers[0].stateMachine;
            var expectedStates = new[]
            {
                ArcherMemberPresentation.IdleState,
                ArcherMemberPresentation.MoveState,
                ArcherMemberPresentation.AttackState,
                ArcherMemberPresentation.HitState,
                ArcherMemberPresentation.DeathState
            };
            foreach (var state in stateMachine.states.Where(child => !expectedStates.Contains(child.state.name)))
                stateMachine.RemoveState(state.state);

            ConfigureState(stateMachine, ArcherMemberPresentation.IdleState, "Idle");
            ConfigureState(stateMachine, ArcherMemberPresentation.MoveState, "WalkForward");
            ConfigureState(stateMachine, ArcherMemberPresentation.AttackState, "AimRecoil");
            ConfigureState(stateMachine, ArcherMemberPresentation.HitState, "HitFront");
            ConfigureState(stateMachine, ArcherMemberPresentation.DeathState, "DeathBackward");
            stateMachine.defaultState = stateMachine.states
                .Select(child => child.state)
                .Single(state => state.name == ArcherMemberPresentation.IdleState);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void ConfigureState(AnimatorStateMachine stateMachine, string stateName, string clipName)
        {
            var state = stateMachine.states.Select(child => child.state)
                .SingleOrDefault(candidate => candidate.name == stateName)
                ?? stateMachine.AddState(stateName);
            state.motion = LoadClip(clipName);
            state.writeDefaultValues = true;
            EditorUtility.SetDirty(state);
        }

        private static AnimationClip LoadClip(string clipName)
        {
            var path = $"{ArcherRoot}/Animations/Archer_{clipName}.fbx";
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .SingleOrDefault(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"Animation clip was not found: {path}");
        }

        private static void CreateMemberPrefab(Material bodyMaterial, Material bowMaterial,
            RuntimeAnimatorController controller)
        {
            var prefabRoot = new GameObject("ArcherMember");
            try
            {
                var model = InstantiateAsset(ModelPath, prefabRoot.transform, "Archer Model");
                model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                model.transform.localScale = Vector3.one;
                var animator = model.GetComponent<Animator>()
                    ?? throw new InvalidOperationException("Archer model did not instantiate with an Animator.");
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;

                var bodyRenderers = model.GetComponentsInChildren<Renderer>(true);
                if (bodyRenderers.Length == 0)
                    throw new InvalidOperationException("Archer model did not instantiate with renderers.");
                var bodyRenderer = bodyRenderers.OfType<SkinnedMeshRenderer>().Single();
                bodyRenderer.sharedMesh = CreateCorrectedBodyMesh(bodyRenderer);
                NormalizeHeightAndGround(model.transform, bodyRenderers, 1.8f);
                AssignMaterial(bodyRenderers, bodyMaterial);

                var leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand)
                    ?? throw new InvalidOperationException("Archer Avatar has no mapped left hand.");
                var bow = InstantiateAsset(BowPath, leftHand, "Archer Bow");
                bow.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                bow.transform.localScale = Vector3.one;
                var bowRenderers = bow.GetComponentsInChildren<Renderer>(true);
                if (bowRenderers.Length == 0)
                    throw new InvalidOperationException("Archer bow did not instantiate with renderers.");
                AssignMaterial(bowRenderers, bowMaterial);

                var presentation = prefabRoot.AddComponent<ArcherMemberPresentation>();
                presentation.Configure(animator, bodyRenderers, bodyRenderers.Concat(bowRenderers).ToArray(),
                    bow.transform);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, MemberPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefabRoot);
            }
        }

        private static void CreateProjectilePrefab(Material arrowMaterial)
        {
            var prefabRoot = new GameObject("ArcherArrowProjectile");
            try
            {
                prefabRoot.AddComponent<AuthoredArrowProjectile>();
                var arrow = InstantiateAsset(ArrowPath, prefabRoot.transform, "Archer Arrow");
                arrow.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                arrow.transform.localScale = Vector3.one;
                var renderers = arrow.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                    throw new InvalidOperationException("Archer arrow did not instantiate with renderers.");
                NormalizeProjectile(arrow.transform, renderers, 0.75f);
                foreach (var itemRenderer in renderers) itemRenderer.gameObject.name = "Arrow";
                AssignMaterial(renderers, arrowMaterial);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, ProjectilePrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefabRoot);
            }
        }

        private static GameObject InstantiateAsset(string path, Transform parent, string name)
        {
            var source = RequireAsset<GameObject>(path);
            var instance = PrefabUtility.InstantiatePrefab(source, parent) as GameObject
                ?? throw new InvalidOperationException($"Could not instantiate asset: {path}");
            instance.name = name;
            return instance;
        }

        private static void AssignMaterial(Renderer[] renderers, Material material)
        {
            foreach (var itemRenderer in renderers)
                itemRenderer.sharedMaterials = Enumerable.Repeat(material, itemRenderer.sharedMaterials.Length).ToArray();
        }

        private static Mesh CreateCorrectedBodyMesh(SkinnedMeshRenderer renderer)
        {
            var source = renderer.sharedMesh;
            var corrected = UnityEngine.Object.Instantiate(source);
            corrected.name = "ArcherBodyMesh";

            var spineIndex = Array.FindIndex(renderer.bones,
                bone => bone != null && bone.name.EndsWith("Spine2", StringComparison.Ordinal));
            var headIndex = Array.FindIndex(renderer.bones,
                bone => bone != null && bone.name.EndsWith("Head", StringComparison.Ordinal));
            if (spineIndex < 0 || headIndex < 0)
                throw new InvalidOperationException("Archer mesh requires mapped Spine2 and Head bones.");

            var vertices = corrected.vertices;
            var parents = Enumerable.Range(0, vertices.Length).ToArray();
            var weldedPositions = new Dictionary<Vector3Int, int>();
            int Find(int index)
            {
                while (parents[index] != index)
                {
                    parents[index] = parents[parents[index]];
                    index = parents[index];
                }
                return index;
            }
            void Union(int left, int right)
            {
                var leftRoot = Find(left);
                var rightRoot = Find(right);
                if (leftRoot != rightRoot) parents[rightRoot] = leftRoot;
            }

            for (var index = 0; index < vertices.Length; index++)
            {
                var vertex = vertices[index];
                var key = new Vector3Int(
                    Mathf.RoundToInt(vertex.x * 100000f),
                    Mathf.RoundToInt(vertex.y * 100000f),
                    Mathf.RoundToInt(vertex.z * 100000f));
                if (weldedPositions.TryGetValue(key, out var matchingIndex)) Union(index, matchingIndex);
                else weldedPositions.Add(key, index);
            }
            var triangles = corrected.triangles;
            for (var index = 0; index < triangles.Length; index += 3)
            {
                Union(triangles[index], triangles[index + 1]);
                Union(triangles[index], triangles[index + 2]);
            }

            var weights = corrected.boneWeights;
            var arrowComponents = new HashSet<int>();
            var headBounds = new Bounds();
            var hasHeadBounds = false;
            for (var index = 0; index < vertices.Length; index++)
            {
                var vertex = vertices[index];
                var weight = weights[index];
                var headWeight = 0f;
                if (weight.boneIndex0 == headIndex) headWeight += weight.weight0;
                if (weight.boneIndex1 == headIndex) headWeight += weight.weight1;
                if (weight.boneIndex2 == headIndex) headWeight += weight.weight2;
                if (weight.boneIndex3 == headIndex) headWeight += weight.weight3;
                if (headWeight > 0.4f)
                {
                    if (!hasHeadBounds) headBounds = new Bounds(vertex, Vector3.zero);
                    else headBounds.Encapsulate(vertex);
                    hasHeadBounds = true;
                }
                if (vertex.x < -0.0018f && vertex.y > 0.0109f && vertex.z < -0.0009f && headWeight > 0.4f)
                    arrowComponents.Add(Find(index));
            }

            var correctedVertices = 0;
            for (var index = 0; index < vertices.Length; index++)
            {
                if (!arrowComponents.Contains(Find(index))) continue;
                weights[index] = new BoneWeight { boneIndex0 = spineIndex, weight0 = 1f };
                correctedVertices++;
            }
            if (correctedVertices < 500 || correctedVertices > 1500)
                throw new InvalidOperationException(
                    $"Unexpected Archer arrow vertex count: {correctedVertices}; " +
                    $"head min={headBounds.min.ToString("F6")}, max={headBounds.max.ToString("F6")}.");
            corrected.boneWeights = weights;

            var asset = AssetDatabase.LoadAssetAtPath<Mesh>(BodyMeshPath);
            if (asset == null)
            {
                AssetDatabase.CreateAsset(corrected, BodyMeshPath);
                asset = corrected;
            }
            else
            {
                EditorUtility.CopySerialized(corrected, asset);
                UnityEngine.Object.DestroyImmediate(corrected);
            }
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void NormalizeHeightAndGround(Transform model, Renderer[] renderers, float targetHeight)
        {
            var bounds = renderers[0].bounds;
            foreach (var itemRenderer in renderers.Skip(1)) bounds.Encapsulate(itemRenderer.bounds);
            if (bounds.size.y <= 0.001f)
                throw new InvalidOperationException("Archer model has no measurable renderer height.");

            model.localScale = Vector3.one * (targetHeight / bounds.size.y);
            bounds = renderers[0].bounds;
            foreach (var itemRenderer in renderers.Skip(1)) bounds.Encapsulate(itemRenderer.bounds);
            model.localPosition += Vector3.up * -bounds.min.y;
        }

        private static void NormalizeProjectile(Transform projectile, Renderer[] renderers, float targetLength)
        {
            var bounds = CombinedBounds(renderers);
            var size = bounds.size;
            var sourceAxis = size.x >= size.y && size.x >= size.z
                ? Vector3.right
                : size.y >= size.z ? Vector3.up : Vector3.forward;
            projectile.localRotation = Quaternion.FromToRotation(sourceAxis, Vector3.forward);
            bounds = CombinedBounds(renderers);
            var length = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (length <= 0.001f)
                throw new InvalidOperationException("Archer arrow has no measurable renderer length.");
            projectile.localScale = Vector3.one * (targetLength / length);
            bounds = CombinedBounds(renderers);
            projectile.localPosition -= bounds.center;
        }

        private static Bounds CombinedBounds(Renderer[] renderers)
        {
            var bounds = renderers[0].bounds;
            foreach (var itemRenderer in renderers.Skip(1)) bounds.Encapsulate(itemRenderer.bounds);
            return bounds;
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException($"Required asset was not found: {path}");
    }
}
