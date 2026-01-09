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
        public Material shadingMaterial;
        public GPUAnimaTextureColorMode currentUsingTexture;
        private void OnEnable()
        {
            Debug.Log($"启用GPUSkinnedAnimationData：{name}");
        }
        
        public void SetupTexture(GPUAnimaTextureColorMode targetFormat)
        {
            var texInfo = textures.Find((info => info.format == targetFormat));
            if (texInfo ==null)
            {
                return;
            }
            shadingMaterial.SetTexture("_AnimTex",texInfo.animatedTexture);
            shadingMaterial.SetFloat("_Format",(float)texInfo.format);
            shadingMaterial.SetInt("_PixelCountPerFrame",texInfo.pixelCountPerFrame);

            foreach (var info in textures)
            {
                var formatKeyword = $"_FORMAT{info.format}";
                if (info.format == texInfo.format)
                {
                    shadingMaterial.EnableKeyword(formatKeyword);
                }
                else
                {
                    shadingMaterial.DisableKeyword(formatKeyword);
                }
            }
            currentUsingTexture = targetFormat;
        }
    }
}