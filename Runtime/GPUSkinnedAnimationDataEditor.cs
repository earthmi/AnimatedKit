using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;


// namespace MiniGame.MiniGame_T5GZ117U.Scripts.AnimatedMesh
namespace AnimatedKit
{
#if UNITY_EDITOR
    [CustomEditor(typeof(GPUSkinnedAnimationData))]
    public class GPUSkinnedAnimationDataEditor:Editor
    {
        private GPUSkinnedAnimationData _animationData;
        private PreviewRenderUtility _mPreview;
        private Vector3 _CameraDirection;
        private float _CameraDistance = 8f;
        private GPUSkinnedAnimator _previewAnimator;
        Vector2 m_RotateDelta;
        private float _PreviewNormalizeTime;
        private bool _isEnableSampleProgress;
        private bool _isPreviewAnimaPlaying;
        private void OnEnable()
        {
            _animationData = target as GPUSkinnedAnimationData;
            if (!HasPreviewGUI())
            {
                return;
            }
            Debug.Log($"打开GPU动画预览:{_animationData.name}");
            _CameraDirection = Vector3.Normalize(new Vector3(0,3f,15f));
            _mPreview = new PreviewRenderUtility();
            _mPreview.camera.fieldOfView = 30.0f;
            _mPreview.camera.nearClipPlane = 0.3f;
            _mPreview.camera.farClipPlane = 1000;
            _mPreview.camera.transform.position = _CameraDirection * _CameraDistance;
            _previewAnimator = GenerateMeshRendererObject();
            BindingAction(true);
            _mPreview.AddSingleGO(_previewAnimator.gameObject);
            EditorApplication.update += OnUpdate;
        }
        
        void BindingAction(bool isEnable)
        {
            for (int i = 0; i < _animationData.clipsInfo.Count; i++)
            {
                _animationData.clipsInfo[i].isEditorPreviewing = false;
                _animationData.clipsInfo[i].OnEditorPreviewClick = isEnable ? OnPlayClick : null;
            }

            _isPreviewAnimaPlaying = false;
        }

        private void OnPlayClick(AnimationFrameInfo obj)
        {
            var isPreview = obj.isEditorPreviewing;
            _isPreviewAnimaPlaying = isPreview;
            for (int i = 0; i < _animationData.clipsInfo.Count; i++)
            {
                var clip = _animationData.clipsInfo[i];
                var isTarget = obj == clip;
                if (!isTarget)
                {
                    clip.isEditorPreviewing = false;
                }
            }
            if (_previewAnimator)
            {
                if (isPreview)
                {
                    _previewAnimator.Play(obj.Name);
                }
                else
                {
                    _previewAnimator.Stop();
                }
            }
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            // var changeFormat = _animationData.currentUsingTexture == GPUAnimaTextureColorMode._RGBM
            //     ? GPUAnimaTextureColorMode._RGBAHALF
            //     : GPUAnimaTextureColorMode._RGBM;
            // if (GUILayout.Button($"ChangeFormat({changeFormat})"))
            // {
            //     _animationData.SetupTexture(changeFormat);
            // }
        }

        private GPUSkinnedAnimator GenerateMeshRendererObject()
        {
            var go = new GameObject();
            go.name = "previewGO";

            var mf = go.AddComponent<MeshFilter>();
            mf.mesh = _animationData.animatedMesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = _animationData.materials;
            mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mr.lightProbeUsage = LightProbeUsage.Off;

            var animtedMeshRenderer = go.AddComponent<GPUSkinnedAnimator>();
            var properyBlockController = go.AddComponent<MaterialPropertyBlockController>();

            animtedMeshRenderer.Setup(_animationData, properyBlockController);

            return animtedMeshRenderer;
        }

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            OnPreviewInputCheck();
            if (_mPreview == null) return;
            _mPreview.BeginPreview(r, background);
            // InternalEditorUtility.SetCustomLighting(_mPreview.lights, new Color(0.6f, 0.6f, 0.6f, 1f));
            _mPreview.camera.Render();
            _mPreview.EndAndDrawPreview(r);
            // InternalEditorUtility.RemoveCustomLighting();
            if (!_isPreviewAnimaPlaying)
            {
                return;
            }
            GUILayout.BeginHorizontal();
            _isEnableSampleProgress = GUILayout.Toggle(_isEnableSampleProgress, "自由进度预览");
            GUILayout.EndHorizontal();
            EditorGUILayout.Space();
            if (_isEnableSampleProgress)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("动画进度:");
                _PreviewNormalizeTime = GUILayout.HorizontalSlider(_PreviewNormalizeTime, 0f, 1f);
                GUILayout.Label($"NormalizeTime:{_PreviewNormalizeTime.ToString("f2")}");
                GUILayout.Label($"Time:{_previewAnimator.time.ToString("f2")}");

                GUILayout.EndHorizontal();
                EditorGUILayout.Space();
            }

        }


        public override void OnPreviewSettings()
        {
            base.OnPreviewSettings();
        }


        public override bool HasPreviewGUI()
        {
            if (!_animationData)
            {
                return false;
            }

            if (_animationData.clipsInfo is not {Count:>0})
            {
                return false;
            }

            if (_animationData.animatedMesh ==null)
            {
                return false;
            }

            if (_animationData.textures is not {Count:>0})
            {
                return false;
            }
            if (_animationData.materials is not {Length:>0})
            {
                return false;
            }
            return true;
        }

        private void OnUpdate()
        {
            if (!HasPreviewGUI())
            {
                return;
            }
            // Debug.Log($"预览中");
            _mPreview.camera.transform.position = _CameraDirection * _CameraDistance;
            _mPreview.camera.transform.LookAt(_previewAnimator.transform);
            _mPreview.lights[0].transform.position = _mPreview.camera.transform.position;
            _mPreview.lights[0].transform.rotation = _mPreview.camera.transform.rotation;
            _previewAnimator.transform.rotation = Quaternion.Euler(m_RotateDelta.y, m_RotateDelta.x, 0f);

            if (!_isEnableSampleProgress)
            {
                _previewAnimator.Tick();
            }
            else
            {
                _previewAnimator.SetNormalizeTime(_PreviewNormalizeTime);
            }

            Repaint();
        }
        
        void OnPreviewInputCheck()
        {
            if (Event.current == null) return;
            if(Event.current.type == EventType.MouseDrag && !_isEnableSampleProgress)
            {
                m_RotateDelta += Event.current.delta;
            }

            if(Event.current.type == EventType.ScrollWheel)
            {
                _CameraDistance = Mathf.Clamp(_CameraDistance + Event.current.delta.y * .2f, 0, 20f);
            }
        }

        private void OnDisable()
        {
            Debug.Log($"关闭GPU动画预览:{_animationData.name}");
            BindingAction(false);
            _animationData = null;
            _mPreview?.Cleanup();
            _mPreview = null;
            if (_previewAnimator != null)
            {
                DestroyImmediate(_previewAnimator.gameObject);
            }
            _previewAnimator = null;
            EditorApplication.update += OnUpdate;
        }
    }
#endif

}