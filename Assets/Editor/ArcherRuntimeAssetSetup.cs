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
        public const string BowMeshPath = "Assets/Resources/Presentation/ArcherBowMesh.asset";
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
            var authoredPoses = CaptureAuthoredEquipmentPoses();
            CreateMemberPrefab(bodyMaterial, bowMaterial, arrowMaterial, controller, authoredPoses);
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
                ArcherMemberPresentation.DeathState,
                ArcherMemberPresentation.TurnLeftState,
                ArcherMemberPresentation.TurnRightState,
                ArcherMemberPresentation.PreviewWalkForwardState,
                ArcherMemberPresentation.PreviewAimWalkForwardState,
                ArcherMemberPresentation.PreviewWalkLeftState,
                ArcherMemberPresentation.PreviewWalkRightState,
                ArcherMemberPresentation.PreviewWalkBackwardState
            };
            foreach (var state in stateMachine.states.Where(child => !expectedStates.Contains(child.state.name)))
                stateMachine.RemoveState(state.state);

            ConfigureState(stateMachine, ArcherMemberPresentation.IdleState, "Idle");
            ConfigureState(stateMachine, ArcherMemberPresentation.MoveState, "RunForward", 0.78f);
            ConfigureState(stateMachine, ArcherMemberPresentation.AttackState, "AimRecoil");
            ConfigureState(stateMachine, ArcherMemberPresentation.HitState, "HitFront");
            ConfigureState(stateMachine, ArcherMemberPresentation.DeathState, "DeathBackward");
            ConfigureState(stateMachine, ArcherMemberPresentation.TurnLeftState, "TurnLeft90", 2.5f);
            ConfigureState(stateMachine, ArcherMemberPresentation.TurnRightState, "TurnRight90", 2.4f);
            ConfigureState(stateMachine, ArcherMemberPresentation.PreviewWalkForwardState, "WalkForward");
            ConfigureState(stateMachine, ArcherMemberPresentation.PreviewAimWalkForwardState, "AimWalkForward");
            ConfigureState(stateMachine, ArcherMemberPresentation.PreviewWalkLeftState, "WalkLeft");
            ConfigureState(stateMachine, ArcherMemberPresentation.PreviewWalkRightState, "WalkRight");
            ConfigureState(stateMachine, ArcherMemberPresentation.PreviewWalkBackwardState, "WalkBackward");
            stateMachine.defaultState = stateMachine.states
                .Select(child => child.state)
                .Single(state => state.name == ArcherMemberPresentation.IdleState);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void ConfigureState(AnimatorStateMachine stateMachine, string stateName, string clipName,
            float speed = 1f)
        {
            var state = stateMachine.states.Select(child => child.state)
                .SingleOrDefault(candidate => candidate.name == stateName)
                ?? stateMachine.AddState(stateName);
            state.motion = LoadClip(clipName);
            state.speed = speed;
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

        private static void CreateMemberPrefab(Material bodyMaterial, Material bowMaterial, Material arrowMaterial,
            RuntimeAnimatorController controller, IReadOnlyDictionary<string, AuthoredLocalPose> authoredPoses)
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
                var bowSocket = new GameObject("Bow Grip Socket");
                bowSocket.transform.SetParent(leftHand, false);
                var bowAttachment = bowSocket.AddComponent<AuthoredEquipmentAttachment>();
                bowAttachment.Configure("Bow", HumanBodyBones.LeftHand);

                var bow = InstantiateAsset(BowPath, bowSocket.transform, "Archer Bow");
                bow.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                bow.transform.localScale = Vector3.one;
                var bowRenderers = bow.GetComponentsInChildren<Renderer>(true);
                if (bowRenderers.Length == 0)
                    throw new InvalidOperationException("Archer bow did not instantiate with renderers.");
                var bowMeshFilter = bow.GetComponentInChildren<MeshFilter>()
                    ?? throw new InvalidOperationException("Archer bow requires a mesh.");
                bowMeshFilter.sharedMesh = CreateBowMeshWithoutRigidString(bowMeshFilter.sharedMesh);
                NormalizeLength(bow.transform, bowRenderers, 1.45f);
                AssignMaterial(bowRenderers, bowMaterial);
                PlaceBowGripAtSocket(bow.transform, bowSocket.transform);
                if (authoredPoses.TryGetValue("Bow", out var authoredPose))
                    authoredPose.ApplyTo(bowSocket.transform);

                var stringAnchors = CreateBowStringAnchors(bow.transform, bowMeshFilter);
                var stringObject = new GameObject("Bow String");
                stringObject.transform.SetParent(prefabRoot.transform, false);
                var bowString = stringObject.AddComponent<LineRenderer>();
                bowString.useWorldSpace = true;
                bowString.widthMultiplier = 0.008f;
                bowString.positionCount = 3;
                bowString.sharedMaterial = bowMaterial;
                bowString.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                bowString.receiveShadows = false;

                var nockedArrow = InstantiateAsset(ArrowPath, prefabRoot.transform, "Nocked Arrow");
                nockedArrow.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                nockedArrow.transform.localScale = Vector3.one;
                var nockedArrowRenderers = nockedArrow.GetComponentsInChildren<Renderer>(true);
                NormalizeProjectile(nockedArrow.transform, nockedArrowRenderers, 0.75f);
                AssignMaterial(nockedArrowRenderers, arrowMaterial);
                nockedArrow.SetActive(false);

                var presentation = prefabRoot.AddComponent<ArcherMemberPresentation>();
                presentation.Configure(animator, bodyRenderers, bodyRenderers.Concat(bowRenderers).ToArray(),
                    bow.transform, nockedArrow.transform, bowString, stringAnchors.upper, stringAnchors.lower);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, MemberPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefabRoot);
            }
        }

        private static IReadOnlyDictionary<string, AuthoredLocalPose> CaptureAuthoredEquipmentPoses()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MemberPrefabPath);
            if (prefab == null) return new Dictionary<string, AuthoredLocalPose>();

            return prefab.GetComponentsInChildren<AuthoredEquipmentAttachment>(true)
                .Where(attachment => !string.IsNullOrWhiteSpace(attachment.AttachmentId) &&
                                     attachment.name.EndsWith("Socket", StringComparison.Ordinal))
                .ToDictionary(attachment => attachment.AttachmentId,
                    attachment => new AuthoredLocalPose(attachment.transform));
        }

        private static void PlaceBowGripAtSocket(Transform bow, Transform socket)
        {
            var meshFilter = bow.GetComponentInChildren<MeshFilter>()
                ?? throw new InvalidOperationException("Archer bow requires a mesh for grip placement.");
            var bounds = meshFilter.sharedMesh.bounds;
            var axes = new[]
            {
                (axis: Vector3.right, size: bounds.size.x),
                (axis: Vector3.up, size: bounds.size.y),
                (axis: Vector3.forward, size: bounds.size.z)
            };
            var longAxis = axes.OrderByDescending(candidate => candidate.size).First().axis;
            var thinAxis = axes.OrderBy(candidate => candidate.size).First().axis;
            var breadthAxis = Vector3.one - longAxis - thinAxis;

            var longDirection = meshFilter.transform.TransformDirection(longAxis);
            bow.rotation = Quaternion.FromToRotation(longDirection, socket.up) * bow.rotation;
            var normal = Vector3.ProjectOnPlane(meshFilter.transform.TransformDirection(thinAxis), socket.up).normalized;
            var facing = Vector3.ProjectOnPlane(socket.forward, socket.up).normalized;
            bow.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(normal, facing, socket.up), socket.up) * bow.rotation;

            var localGrip = bounds.center + Vector3.Scale(bounds.extents, breadthAxis) * 0.9f;
            bow.position += socket.position - meshFilter.transform.TransformPoint(localGrip);
        }

        private static (Transform upper, Transform lower) CreateBowStringAnchors(Transform bow,
            MeshFilter meshFilter)
        {
            var bounds = meshFilter.sharedMesh.bounds;
            var axes = new[]
            {
                (axis: Vector3.right, size: bounds.size.x),
                (axis: Vector3.up, size: bounds.size.y),
                (axis: Vector3.forward, size: bounds.size.z)
            };
            var longAxis = axes.OrderByDescending(candidate => candidate.size).First().axis;
            var longExtent = Vector3.Scale(bounds.extents, longAxis) * 0.96f;
            var firstPosition = meshFilter.transform.TransformPoint(bounds.center + longExtent);
            var secondPosition = meshFilter.transform.TransformPoint(bounds.center - longExtent);
            var upperPosition = firstPosition.y >= secondPosition.y ? firstPosition : secondPosition;
            var lowerPosition = firstPosition.y >= secondPosition.y ? secondPosition : firstPosition;

            var upper = new GameObject("Bow Upper String Anchor").transform;
            upper.SetParent(bow, false);
            upper.position = upperPosition;
            var lower = new GameObject("Bow Lower String Anchor").transform;
            lower.SetParent(bow, false);
            lower.position = lowerPosition;
            return (upper, lower);
        }

        private readonly struct AuthoredLocalPose
        {
            private readonly Vector3 position;
            private readonly Quaternion rotation;
            private readonly Vector3 scale;

            public AuthoredLocalPose(Transform transform)
            {
                position = transform.localPosition;
                rotation = transform.localRotation;
                scale = transform.localScale;
            }

            public void ApplyTo(Transform transform)
            {
                transform.SetLocalPositionAndRotation(position, rotation);
                transform.localScale = scale;
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

        private static Mesh CreateBowMeshWithoutRigidString(Mesh source)
        {
            if (source.subMeshCount != 1)
                throw new InvalidOperationException("Archer bow string removal expects one mesh submesh.");
            var vertices = source.vertices;
            var triangles = source.triangles;
            var parents = Enumerable.Range(0, vertices.Length).ToArray();
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

            var weldedPositions = new Dictionary<Vector3Int, int>();
            for (var index = 0; index < vertices.Length; index++)
            {
                var vertex = vertices[index];
                var key = new Vector3Int(Mathf.RoundToInt(vertex.x * 10000f),
                    Mathf.RoundToInt(vertex.y * 10000f), Mathf.RoundToInt(vertex.z * 10000f));
                if (weldedPositions.TryGetValue(key, out var matchingIndex)) Union(index, matchingIndex);
                else weldedPositions.Add(key, index);
            }
            for (var index = 0; index < triangles.Length; index += 3)
            {
                Union(triangles[index], triangles[index + 1]);
                Union(triangles[index], triangles[index + 2]);
            }

            var components = new Dictionary<int, Bounds>();
            for (var index = 0; index < vertices.Length; index++)
            {
                var root = Find(index);
                if (!components.TryGetValue(root, out var bounds)) bounds = new Bounds(vertices[index], Vector3.zero);
                else bounds.Encapsulate(vertices[index]);
                components[root] = bounds;
            }
            var sourceBounds = source.bounds;
            var sourceAxes = new[] { sourceBounds.size.x, sourceBounds.size.y, sourceBounds.size.z };
            var longAxis = Array.IndexOf(sourceAxes, sourceAxes.Max());
            var longSize = sourceAxes[longAxis];
            var crossSize = sourceAxes.Where((_, axis) => axis != longAxis).Max();
            var stringRoots = components.Where(component =>
            {
                var sizes = new[] { component.Value.size.x, component.Value.size.y, component.Value.size.z };
                return sizes[longAxis] > longSize * 0.7f &&
                       sizes.Where((_, axis) => axis != longAxis).Max() < crossSize * 0.1f;
            }).Select(component => component.Key).ToArray();
            if (stringRoots.Length != 1)
                throw new InvalidOperationException(
                    $"Expected one isolated rigid bow string, found {stringRoots.Length}.");
            var stringRoot = stringRoots[0];
            var retainedTriangles = new List<int>(triangles.Length);
            for (var index = 0; index < triangles.Length; index += 3)
            {
                if (Find(triangles[index]) == stringRoot) continue;
                retainedTriangles.Add(triangles[index]);
                retainedTriangles.Add(triangles[index + 1]);
                retainedTriangles.Add(triangles[index + 2]);
            }
            if (retainedTriangles.Count >= triangles.Length)
                throw new InvalidOperationException("Archer rigid bow string triangles were not removed.");

            var corrected = UnityEngine.Object.Instantiate(source);
            corrected.name = "ArcherBowMesh";
            corrected.triangles = retainedTriangles.ToArray();
            corrected.RecalculateBounds();
            var asset = AssetDatabase.LoadAssetAtPath<Mesh>(BowMeshPath);
            if (asset == null)
            {
                AssetDatabase.CreateAsset(corrected, BowMeshPath);
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

        private static void NormalizeLength(Transform target, Renderer[] renderers, float targetLength)
        {
            var bounds = CombinedBounds(renderers);
            var length = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (length <= 0.001f)
                throw new InvalidOperationException($"{target.name} has no measurable renderer length.");
            target.localScale *= targetLength / length;
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
