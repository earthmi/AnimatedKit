using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// namespace MiniGame.MiniGame_T5GZ117U.Scripts.AnimatedMesh
namespace AnimatedKit
{
    public enum GPUAnimaTextureColorMode
    {
        //8bit RGBM编码
        _RGBM = 0,
        //16bit
        _RGBAHALF = 1,

    }

    public enum SkinnedQuality
    {
        BONE1 = 0,
        BONE2=1,
        BONE4 =2,
    }
    [Serializable]
    public class ExposedBone
    {
        public int Index;
        public Vector3 Position;
        public Vector3 Direction;
    }
    [Serializable]
    public class AnimationTextureInfo
    {
        public GPUAnimaTextureColorMode format;
        public Texture animatedTexture;
        public int pixelCountPerFrame;
    }
    
    public class GPUSkinnedAnimationData : ScriptableObject
    {
        public List<AnimationFrameInfo> clipsInfo;
        public List<AnimationTextureInfo> textures;
        public Mesh animatedMesh;
        public Material[] materials;
        // public Material shadingMaterial;


        public GPUAnimaTextureColorMode currentUsingTexture;
        public SkinnedQuality currentSkinnedQuality;
        public bool isEnableInterpolation;
        public List<ExposedBone> exposedBones;
        [HideInInspector] public int currentTextureIndex = -1; 
        private GPUAnimaTextureColorMode _currentUsingTexture;
        private SkinnedQuality _currentSkinnedQuality;
        private bool _isEnableInterpolation;
        private void OnEnable()
        {
            Debug.Log($"启用GPUSkinnedAnimationData：{name}");
            _currentUsingTexture = currentUsingTexture;
            _currentSkinnedQuality = currentSkinnedQuality;
            _isEnableInterpolation = isEnableInterpolation;
        }

        private void OnValidate()
        {
            if (_currentUsingTexture!= currentUsingTexture)
            {
                SetupTexture(currentUsingTexture);
            }

            if (_currentSkinnedQuality!= currentSkinnedQuality)
            {
                SetupSkinnedQuality(currentSkinnedQuality);
            }

            if (_isEnableInterpolation!= isEnableInterpolation)
            {
                SetupInterpolation();
            }
            
        }

        public void SetupInterpolation()
        {
            if (materials is not {Length:>0})
            {
                Debug.LogError($"无法找到材质，无法开启或关闭线性插值");
                return;
            }

            var keyword = "_INTERPOLATION";
            foreach (var VARIABLE in materials)
            {
                VARIABLE.SetInt("_Interpolation",isEnableInterpolation?1:0);
                if (isEnableInterpolation)
                {
                    VARIABLE.EnableKeyword(keyword);
                }
                else
                {
                    VARIABLE.DisableKeyword(keyword);
                }
            }
            _isEnableInterpolation = isEnableInterpolation;
            Debug.Log($"成功切换线性插值模式：{isEnableInterpolation}");

        }
        
        public void SetupSkinnedQuality(SkinnedQuality quality)
        {
            if (materials is not {Length:>0})
            {
                Debug.LogError($"无法找到材质，无法进行蒙皮骨骼数量切换");
                return;
            }
            foreach (var VARIABLE in materials)
            {
                VARIABLE.SetFloat("_Skin",(float)quality);
                foreach (SkinnedQuality skinnedBones in Enum.GetValues(typeof(SkinnedQuality)))
                {
                    var formatKeyword = $"_SKIN_{skinnedBones}";
                    if (skinnedBones == quality)
                    {
                        VARIABLE.EnableKeyword(formatKeyword);
                    }
                    else
                    {
                        VARIABLE.DisableKeyword(formatKeyword);
                    }
                }
            }
            _currentSkinnedQuality = currentSkinnedQuality;
            Debug.Log($"成功切换到蒙皮骨骼数量：{currentSkinnedQuality}");

        }

        public void SetupTexture(GPUAnimaTextureColorMode targetFormat)
        {
            
            var texIndex = textures.FindIndex((info => info.format == targetFormat));
            if (texIndex <0)
            {
                return;
            }
            var texInfo = textures[texIndex];
            if (materials is not {Length:>0})
            {
                Debug.LogError($"无法找到材质，无法进行贴图格式切换");
                return;
            }
            foreach (var VARIABLE in materials)
            {
                VARIABLE.SetTexture("_AnimTex",texInfo.animatedTexture);
                VARIABLE.SetFloat("_Format",(float)texInfo.format);
                VARIABLE.SetInt("_PixelCountPerFrame",texInfo.pixelCountPerFrame);
                foreach (var info in textures)
                {
                    var formatKeyword = $"_FORMAT{info.format}";
                    if (info.format == texInfo.format)
                    {
                        VARIABLE.EnableKeyword(formatKeyword);
                    }
                    else
                    {
                        VARIABLE.DisableKeyword(formatKeyword);
                    }
                }
            }
            _currentUsingTexture = currentUsingTexture;
            currentTextureIndex = texIndex;
            Debug.Log($"成功切换到贴图格式：{currentUsingTexture}");
        }
    }
}