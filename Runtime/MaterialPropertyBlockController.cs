using UnityEngine;
using UnityEngine.Rendering;

namespace AnimatedKit
{
    public class MaterialPropertyBlockController : MonoBehaviour
    {
        private Renderer _renderer;
        public Renderer Renderer => _renderer ? _renderer : (_renderer = GetComponent<Renderer>());

        private MaterialPropertyBlock _materialPropertyBlock;
        private MaterialPropertyBlock MaterialPropertyBlock => _materialPropertyBlock ??= new MaterialPropertyBlock();

        public void SetColor(string propertyName, Color color)
        {
            MaterialPropertyBlock.SetColor(propertyName, color);
        }

        public void SetFloat(string propertyName, float value)
        {
            MaterialPropertyBlock.SetFloat(propertyName, value);
        }
    
        public void SetFloat(int propertyNameHash, float value)
        {
            MaterialPropertyBlock.SetFloat(propertyNameHash, value);
        }

        public void Apply()
        {
            Renderer.SetPropertyBlock(MaterialPropertyBlock);
        }

        public int GetInt(string propertyName)
        {
            return MaterialPropertyBlock.GetInt(propertyName);
        }

        public float GetFloat(string propertyName)
        {
            return MaterialPropertyBlock.GetFloat(propertyName);
        }
        public void SetMaterial(Material material)
        {
            // Renderer.material = material;
            Renderer.sharedMaterial = material;
        }
        public void EnableKeyWord(string kw)
        {
            Renderer.material.EnableKeyword(kw);
        }
        public void DisableKeyWord(string kw)
        {
            Renderer.material.DisableKeyword(kw);
        }

        public void ReceivedShadow(bool on)
        {
            Renderer.shadowCastingMode = on ? ShadowCastingMode.On : ShadowCastingMode.Off;
            Renderer.receiveShadows = on;
        }
    }

}
