#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace AnimatedKit
{
    public class AnimatedMeshToAsset
    {
        public class PoseData
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
        
            public PoseData(Transform transform)
            {
                position = transform.localPosition;
                rotation = transform.localRotation;
                scale = transform.localScale;
            }
        
            public void ApplyTo(Transform transform)
            {
                transform.localPosition = position;
                transform.localRotation = rotation;
                transform.localScale = scale;
            }
        }
        /// <summary>
        /// 设置数据保存到纹理上的精度
        /// </summary>
        private const GPUAnimaTextureColorMode TextureColorMode = GPUAnimaTextureColorMode._RGBM;
        private const int BoneMatrixRowCount = 3;
        private const int TargetFrameRate = 30;
        private static readonly string FolderName = "GPUAnimatedData";
        static Dictionary<Transform, PoseData> originalPose;

        [MenuItem("AnimatedMeshRendererGenerator/TPose")]
        static void TPose()
        {
            var targetObject = Selection.activeGameObject;
            if (targetObject == null)
            {
                EditorUtility.DisplayDialog("Warning", "Selected object type is not gameobject.", "OK");
                return;
            }
            var animator = targetObject.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                EditorUtility.DisplayDialog("Warning", "Selected object does not have Animator.", "OK");
                return;
            }

            if (!animator.avatar)
            {
                EditorUtility.DisplayDialog("Warning", "Selected object does not have Avatar.", "OK");
                return;
            }
            EnforceTPose(animator);
        }


        static void CaptureOriginPose(SkinnedMeshRenderer skinnedMeshRenderer)
        {
            originalPose = new();
            //记录原始Pose / TPose
            foreach (Transform bone in skinnedMeshRenderer.bones)
            {
                originalPose[bone] = new PoseData(bone);
            }
        }

        static void ResetOriginPose()
        {
            if (originalPose==null)
            {
                return;
            }
            foreach (var kvp in originalPose)
            {
                if (kvp.Key != null)
                {
                    kvp.Value.ApplyTo(kvp.Key);
                }
            }
        }
        [MenuItem("AnimatedMeshRendererGenerator/MeshToAsset")]
        private static void Generate()
        {
            var targetObject = Selection.activeGameObject;
            if (targetObject == null)
            {
                EditorUtility.DisplayDialog("Warning", "Selected object type is not gameobject.", "OK");
                return;
            }

            var skinnedMeshRenderers = targetObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (!skinnedMeshRenderers.Any())
            {
                EditorUtility.DisplayDialog("Warning", "Selected object does not have one skinnedMeshRenderer.", "OK");
                return;
            }

            var animator = targetObject.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                EditorUtility.DisplayDialog("Warning", "Selected object does not have Animator.", "OK");
                return;
            }
            var selectionPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(targetObject));
            var skinnedMeshRenderer = skinnedMeshRenderers.First();

            var clips = animator.runtimeAnimatorController.animationClips;

            var path = Path.Combine(selectionPath, FolderName);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            CaptureOriginPose(skinnedMeshRenderer);
            
            var filePathPre = string.Format($"{{0}}/{FolderName}/{{1}}", selectionPath,
                targetObject.name);
            var textureFormats = Enum.GetValues(typeof(GPUAnimaTextureColorMode));
            List<AnimationTextureInfo> textureInfos = new();
            foreach (GPUAnimaTextureColorMode colorMode in textureFormats)
            {
                var animationTexture = GenerateAnimationTexture(animator.gameObject, clips, skinnedMeshRenderer,colorMode);
                var animTexPath = $"{filePathPre}_AnimationTexture{colorMode}.asset";//   string.Format($"{{0}}/{FolderName}/{{1}}_AnimationTexture.asset", selectionPath,targetObject.name);
                WriteUnityFile(animTexPath, animationTexture);
                textureInfos.Add(new()
                {
                    animatedTexture = animationTexture,
                    format = colorMode,
                    pixelCountPerFrame = perFrameBoneMatrixPixels,
                });
            }
            //采样完成之后 恢复原始Pose / TPose
            ResetOriginPose();


            var mesh = GenerateUvBoneWeightedMesh(skinnedMeshRenderer);
            var meshPath = $"{filePathPre}_mesh.asset";
            WriteUnityFile(meshPath, mesh);
            // AssetDatabase.CreateAsset(mesh, $"{meshPath}_Mesh.asset");

            var materials = GenerateMaterials(skinnedMeshRenderer);
            foreach (var VARIABLE in materials)
            {
                var matName = VARIABLE.name.Replace("(Clone)", "");
                var materialPath = $"{filePathPre}_{matName}_mat.asset";
                WriteUnityFile(materialPath, VARIABLE);
            }
            // AssetDatabase.CreateAsset(material, string.Format($"{{0}}/{FolderName}/{{1}}_Material.asset", selectionPath, targetObject.name));

            var exposedBones = GetExposedBones(skinnedMeshRenderer);
            var dataCollection = GenerateSO(GetAnimaFrameInfos(clips),textureInfos,mesh,materials,exposedBones);
            var dataCollectionPath = $"{filePathPre}_GPUAnimatedSO.asset";
            WriteUnityFile(dataCollectionPath, dataCollection);
            // AssetDatabase.CreateAsset(dataCollection, string.Format($"{{0}}/{FolderName}/{{1}}_GPUAnimatedSO.asset", selectionPath, targetObject.name));

            var go = GenerateMeshRendererObject(targetObject, dataCollection);
            var prefabPath = $"{filePathPre}_GPUAnima.prefab";
            WritePrefab(prefabPath,go);
            // PrefabUtility.CreatePrefab(string.Format($"{{0}}/{FolderName}/{{1}}.prefab", selectionPath, targetObject.name), go);
            
            Object.DestroyImmediate(go);
            AssetDatabase.Refresh();
        }

        static List<ExposedBone> GetExposedBones(SkinnedMeshRenderer smr)
        {
            List<ExposedBone> bones = new();
            var rootWorldToLocal = smr.transform.worldToLocalMatrix;
            for (int i = 0; i < smr.bones.Length; i++)
            {
                var b = smr.bones[i];
                var exposedBone = b.GetComponent<GPUAnimationExposedBoneMark>();
                if (exposedBone)
                {
                    bones.Add(new ExposedBone()
                    {
                        Index = i,
                        Position = rootWorldToLocal.MultiplyPoint(b.position),
                        Direction = rootWorldToLocal.MultiplyPoint(b.forward),
                    });
                }
            }

            return bones;
        }
        

        static void WriteUnityFile(string path, Object target)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            AssetDatabase.CreateAsset(target, path);
        }
        
        static void WritePrefab(string path, GameObject target)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            PrefabUtility.CreatePrefab(path,target);
        }


        static GPUSkinnedAnimationData GenerateSO(List<AnimationFrameInfo> frameInfos,List<AnimationTextureInfo> textureInfos,Mesh mesh,Material[] mats,List<ExposedBone> exposeBones)
        {
            var so = ScriptableObject.CreateInstance<GPUSkinnedAnimationData>();
            so.clipsInfo = frameInfos;
            so.animatedMesh = mesh;
            so.textures = textureInfos;
            so.materials = mats;
            so.isEnableInterpolation = false;
            so.currentSkinnedQuality = SkinnedQuality.BONE1;
            so.currentUsingTexture = GPUAnimaTextureColorMode._RGBAHALF;
            so.exposedBones = exposeBones;
            so.SetupInterpolation();
            so.SetupTexture(so.currentUsingTexture);
            so.SetupSkinnedQuality(so.currentSkinnedQuality);
            return so;
        }

        private static Mesh GenerateUvBoneWeightedMesh(SkinnedMeshRenderer smr)
        {
            var mesh = Object.Instantiate(smr.sharedMesh);

            var boneSets = smr.sharedMesh.boneWeights;
            var boneIndexes = boneSets.Select(x => new Vector4(x.boneIndex0, x.boneIndex1, x.boneIndex2, x.boneIndex3)).ToList();
            var boneWeights = boneSets.Select(x => new Vector4(x.weight0, x.weight1, x.weight2, x.weight3)).ToList();

            mesh.SetUVs(2, boneIndexes);
            mesh.SetUVs(3, boneWeights);
            mesh.boneWeights = null;
            mesh.bindposes = null;
            mesh.bounds = new Bounds((m_BoundsMin + m_BoundsMax) / 2, m_BoundsMax - m_BoundsMin);
            return mesh;
        }

        static TextureFormat GetTextureFormat(GPUAnimaTextureColorMode codeMode)
        {
            switch (codeMode)
            {
                case GPUAnimaTextureColorMode._RGBAHALF:
                    return TextureFormat.RGBAHalf;
                case GPUAnimaTextureColorMode._RGBM:
                    return TextureFormat.RGBA32;
                case GPUAnimaTextureColorMode._DUAL16FP:
                    return TextureFormat.RGBA32;
                default:
                    return TextureFormat.RGBAHalf;
            }
        }

        private static int pixelIndex;
        static Vector3 m_BoundsMin = Vector3.zero;
        static Vector3 m_BoundsMax = Vector3.zero;
        static float minValue = float.MaxValue;
        static float maxValue = float.MinValue;
        static void WriteBoneMatrix2Color(SkinnedMeshRenderer smr,Color[] pixels,GPUAnimaTextureColorMode codeMode)
        {
            if (codeMode == GPUAnimaTextureColorMode._RGBAHALF)
            {
                foreach (var boneMatrix in smr.bones.Select((b, idx) => b.localToWorldMatrix * smr.sharedMesh.bindposes[idx]))
                {
                    pixels[pixelIndex++] = new Color(boneMatrix.m00, boneMatrix.m01, boneMatrix.m02 , boneMatrix.m03 );
                    pixels[pixelIndex++] = new Color(boneMatrix.m10, boneMatrix.m11, boneMatrix.m12, boneMatrix.m13);
                    pixels[pixelIndex++] = new Color(boneMatrix.m20, boneMatrix.m21, boneMatrix.m22, boneMatrix.m23);    
                }
            }else if (codeMode == GPUAnimaTextureColorMode._RGBM)
            {
                foreach (var boneMatrix in smr.bones.Select((b, idx) => b.localToWorldMatrix * smr.sharedMesh.bindposes[idx]))
                {
                    for (int row = 0; row < 3; row++)
                    {
                        for (int col = 0; col < 4; col++)
                        {
                            float v = boneMatrix[row, col];
                            Color c = EncodeFloatToRGBA32(v, minValue, maxValue);
                            // if (pixelIndex>= pixels.Length)
                            // {
                            //     Debug.Log($"数组已经越界了，颜色缓冲：{pixels.Length},索引：{pixelIndex}");
                            //     return;
                            // }
                            pixels[pixelIndex] = c;
                            pixelIndex++;
                        }
                    }
                }
            }else if (codeMode == GPUAnimaTextureColorMode._DUAL16FP)
            {
                foreach (var boneMatrix in smr.bones.Select((b, idx) => b.localToWorldMatrix * smr.sharedMesh.bindposes[idx]))
                {
                    var colors = MatrixTextureEncoder.EncodeMatrix4x3(boneMatrix);
                    for (int i = 0; i < colors.Length; i++)
                    {
                        var c = colors[i];
                        pixels[pixelIndex] = c;
                        pixelIndex++;
                    }
                }
            }

        }
        
        static void EnforceTPose(Animator animator) {

            if (animator.avatar)
            {
                SkeletonBone[] skeletonBones = animator.avatar.humanDescription.skeleton;

                foreach (HumanBodyBones hbb in Enum.GetValues(typeof(HumanBodyBones))) {
                    if (hbb == HumanBodyBones.LastBone) continue;

                    Transform boneTransform = animator.GetBoneTransform(hbb);
                    if (!boneTransform) continue;

                    SkeletonBone skeletonBone = skeletonBones.FirstOrDefault(sb => sb.name == boneTransform.name);
                    if (skeletonBone.name == null) continue;

                    if (hbb == HumanBodyBones.Hips) boneTransform.localPosition = skeletonBone.position;
                    boneTransform.localRotation = skeletonBone.rotation;
                }    
            }
        }
        
        /// <summary>
        /// 将一个float类型值转RGBA32
        /// </summary>
        static Color EncodeFloatToRGBA32(float value, float minValue, float maxValue)
        {
            // 归一化到0~1
            float normalized = Mathf.Clamp01((value - minValue) / (maxValue - minValue));
            uint enc = (uint)(normalized * 4294967295.0f); // 0xFFFFFFFF
            byte r = (byte)((enc >> 24) & 0xFF);
            byte g = (byte)((enc >> 16) & 0xFF);
            byte b = (byte)((enc >> 8) & 0xFF);
            byte a = (byte)(enc & 0xFF);
            return new Color32(r, g, b, a);
        }

        private static Texture GenerateAnimationTexture(GameObject targetObject, IEnumerable<AnimationClip> clips, SkinnedMeshRenderer smr,GPUAnimaTextureColorMode colorMode)
        {
            var textureBoundary = GetCalculatedTextureBoundary(clips, smr.bones.Count(),colorMode);
            
            var texture = new Texture2D((int)textureBoundary.x, (int)textureBoundary.y, GetTextureFormat(colorMode), false, true);
            
            var pixels = texture.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.black;
                
            }
            pixelIndex = 0;
            

            // 1. 统计所有矩阵元素的最大最小值
            minValue = float.MaxValue;
            maxValue = float.MinValue;
            foreach (var clip in clips)
            {
                var totalFrames = (int)(clip.length * TargetFrameRate);
                foreach (var frame in Enumerable.Range(0, totalFrames))
                {
                    clip.SampleAnimation(targetObject, (float)frame / TargetFrameRate);
                    foreach (var boneMatrix in smr.bones.Select((b, idx) => b.localToWorldMatrix * smr.sharedMesh.bindposes[idx]))
                    {
                        for (int row = 0; row < 3; row++)
                        {
                            for (int col = 0; col < 4; col++)
                            {
                                float v = boneMatrix[row, col];
                                if (v < minValue) minValue = v;
                                if (v > maxValue) maxValue = v;
                            }
                        } 
                    }

                }
            }
            
            ResetOriginPose();
            //采样第一帧为 TPose
            WriteBoneMatrix2Color(smr, pixels,colorMode);

            foreach (var clip in clips)
            {
                var totalFrames = (int)(clip.length * TargetFrameRate);
                foreach (var frame in Enumerable.Range(0, totalFrames))
                {
                    clip.SampleAnimation(targetObject, (float)frame / TargetFrameRate);
                    WriteBoneMatrix2Color(smr, pixels,colorMode);
                    //计算包围盒
                    var boundsCheckMesh = new Mesh();
                    smr.BakeMesh(boundsCheckMesh);
                    var vertices = boundsCheckMesh.vertices;
                    for (var k = 0; k < vertices.Length; k++)
                    {
                        var _src = vertices[k];
                        var _tar = smr.transform.localScale;
                        var _v = new Vector3(_src.x / _tar.x, _src.y / _tar.y, _src.z / _tar.z);
                        m_BoundsMin = Vector3.Min(m_BoundsMin, _v);
                        m_BoundsMax = Vector3.Max(m_BoundsMax, _v);
                    }
                    boundsCheckMesh.Clear();
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Point;

            return texture;
        }
        static int perFrameBoneMatrixPixels = 0;
        private static Vector2 GetCalculatedTextureBoundary(IEnumerable<AnimationClip> clips, int boneLength,GPUAnimaTextureColorMode textureFormat)
        {
            perFrameBoneMatrixPixels = 0;
            switch (textureFormat)
            {
                case GPUAnimaTextureColorMode._RGBAHALF:
                    perFrameBoneMatrixPixels = BoneMatrixRowCount * boneLength;
                    break;
                case GPUAnimaTextureColorMode._RGBM:
                    //以RGBM编码存储，则8bit精度，则1个float转换精度，保存到一个color中，矩阵matrix3x4 数据3 x float4,则需要12个color
                    perFrameBoneMatrixPixels = BoneMatrixRowCount * 4 * boneLength;
                    break;
                case GPUAnimaTextureColorMode._DUAL16FP:
                    perFrameBoneMatrixPixels = BoneMatrixRowCount * 2 * boneLength;
                    break;
                default:
                    perFrameBoneMatrixPixels = BoneMatrixRowCount * boneLength;
                    break;
            }
            var totalPixels = clips.Aggregate(perFrameBoneMatrixPixels, (pixels, currentClip) => pixels + perFrameBoneMatrixPixels * (int)(currentClip.length * TargetFrameRate));
            Debug.Log($"计算动画矩阵纹理，存储模式：{textureFormat}，所有像素：{totalPixels}");
            // var (textureWidth,textureHeight) = TextureSizeCalculator.CalculateOptimalTextureSize(totalPixels,requirePowerOfTwo:false);
            var textureWidth = 1;
            var textureHeight = 1;
            
            while (textureWidth * textureHeight < totalPixels)
            {
                if (textureWidth <= textureHeight)
                {
                    textureWidth *= 2;
                }
                else
                {
                    textureHeight *= 2;
                }
            }

            return new Vector2(textureWidth, textureHeight);
        }

        private static Material[] GenerateMaterials(SkinnedMeshRenderer smr)
        {
            Material[] mats = new Material[smr.sharedMaterials.Length];
            for (int i = 0; i < smr.sharedMaterials.Length; i++)
            {
                var material = Object.Instantiate(smr.sharedMaterials[i]);
                var shaderPath = "GPUAnimation/SkinnedSkeleton-Lit(Low)";
                material.shader = Shader.Find(shaderPath);
                material.SetVector("_BoundsRange", new Vector2(minValue,maxValue));
                material.enableInstancing = true;
                mats[i] = material;
            }
            return mats;
        }

        static List<AnimationFrameInfo> GetAnimaFrameInfos(IEnumerable<AnimationClip> clips)
        {
            var frameInformations = new List<AnimationFrameInfo>();
            var currentClipFrames = 0;
            
            foreach (var clip in clips)
            {
                var frameCount = (int)(clip.length * TargetFrameRate);
                var startFrame = currentClipFrames + 1;
                var endFrame = startFrame + frameCount - 1;
                frameInformations.Add(new AnimationFrameInfo(clip.name, startFrame, endFrame, frameCount,clip.length,clip.isLooping,clip.events));

                currentClipFrames = endFrame;
            }

            return frameInformations;
        }
        

        private static GameObject GenerateMeshRendererObject(GameObject targetObject, GPUSkinnedAnimationData data)
        {
            var go = new GameObject();
            go.name = targetObject.name;

            var mf = go.AddComponent<MeshFilter>();
            mf.mesh = data.animatedMesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = data.materials;
            // mr.sharedMaterials = new Material[data.materials.Length];
            // for (int i = 0; i < mr.sharedMaterials.Length; i++)
            // {
            //     mr.sharedMaterials[i] = data.materials[i];
            // }
            // mr.sharedMaterial = data.shadingMaterial;
            mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mr.lightProbeUsage = LightProbeUsage.Off;

            var animtedMeshRenderer = go.AddComponent<GPUSkinnedAnimator>();
            var properyBlockController = go.AddComponent<MaterialPropertyBlockController>();

            animtedMeshRenderer.Setup(data, properyBlockController);

            return go;
        }
    }
}

#endif