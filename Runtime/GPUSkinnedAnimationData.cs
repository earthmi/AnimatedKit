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
        private GPUAnimaTextureColorMode _currentUsingTexture;
        public SkinnedQuality currentSkinnedQuality;
        private SkinnedQuality _currentSkinnedQuality;

        public bool isEnableInterpolation;
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
                _currentUsingTexture = currentUsingTexture;
            }

            if (_currentSkinnedQuality!= currentSkinnedQuality)
            {
                SetupSkinnedQuality(currentSkinnedQuality);
                _currentSkinnedQuality = currentSkinnedQuality;
            }

            if (_isEnableInterpolation!= isEnableInterpolation)
            {
                SetupInterpolation();
                _isEnableInterpolation = isEnableInterpolation;
            }
            
        }

        public void SetupInterpolation()
        {
            foreach (var VARIABLE in materials)
            {
                VARIABLE.SetInt("_Interpolation",isEnableInterpolation?1:0);
                if (isEnableInterpolation)
                {
                    VARIABLE.EnableKeyword("_INTERPOLATION");
                }
                else
                {
                    VARIABLE.DisableKeyword("_INTERPOLATION");
                }
            }
        }
        
        public void SetupSkinnedQuality(SkinnedQuality quality)
        {
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
        }
        
        public void SetupTexture(GPUAnimaTextureColorMode targetFormat)
        {
            var texInfo = textures.Find((info => info.format == targetFormat));
            if (texInfo ==null)
            {
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
            currentUsingTexture = targetFormat;
        }
    }
}