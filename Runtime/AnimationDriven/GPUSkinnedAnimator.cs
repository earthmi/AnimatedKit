using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace AnimatedKit
{
    public class GPUSkinnedAnimator : MonoBehaviour,IAnimationExecutor
    {
        // [SerializeField]
        // public List<AnimationFrameInfo> FrameInformations;
        [SerializeField]
        MaterialPropertyBlockController PropertyBlockController;

        [SerializeField] private GPUSkinnedAnimationData SkinnedAnimaInfo;
        [SerializeField] private List<Transform> exposedBones;
        [SerializeField] private Transform exposedBoneRoot;
        public Material[] SharedMaterials => SkinnedAnimaInfo.materials;
        public List<AnimationTextureInfo> TextureInfos => SkinnedAnimaInfo.textures;
        public List<AnimationFrameInfo> Clips=> SkinnedAnimaInfo.clipsInfo;
        private Dictionary<int, Action> _clipEvent = new();
        public float Speed { get; set; } = 1;

        public bool IsPlaying { get; private set; }

        public void Setup(GPUSkinnedAnimationData data, MaterialPropertyBlockController propertyBlockController)
        {
            SkinnedAnimaInfo = data;
            PropertyBlockController = propertyBlockController;
            if (data.exposedBones is  {Count:<=0})
            {
                return;
            }
            exposedBoneRoot = new GameObject("ExposedBones").transform;
            exposedBoneRoot.SetParent(transform);
            exposedBoneRoot.localPosition = Vector3.zero;
            exposedBoneRoot.localRotation = Quaternion.identity;
            exposedBoneRoot.localScale = Vector3.one;
            exposedBones = new(data.exposedBones.Count);
            for (int i = 0; i < data.exposedBones.Count; i++)
            {
                var b = new GameObject($"Index:{data.exposedBones[i].Index}").transform;
                b.SetParent(exposedBoneRoot);
                exposedBones.Add(b);
            }
        }
        
        public void SetSpeed(float s)
        {
            Speed = s;
        }

        public void Play(string state)
        {
            Play(state,null);
        }

        // public void Play(string state, Action callback)
        // {
        //     Play(state,callback);
        // }

        public float GetLength(string state)
        {
            var t = SkinnedAnimaInfo.clipsInfo.Find(x => x.Name == state);
            return t!=null ? t.Seconds : 0f;
        }

        public void RegisterEvent(string eventKey,Action action)
        {
            int hash = eventKey.GetHashCode();
            var exist = _clipEvent.TryGetValue(hash, out var e);
            if (!exist)
            {
                _clipEvent.Add(hash,action);
            }
            else
            {
                e += action;
                _clipEvent[hash] = e;
            }
        }
        

        public void UnRegisterEvent(string eventKey,Action action)
        {
            int hash = eventKey.GetHashCode();
            var exist = _clipEvent.TryGetValue(hash, out var e);
            if (!exist)
            {
                return;
            }
            e -= action;
            if (e == null)
            {
                _clipEvent.Remove(hash);
            }
            else
            {
                _clipEvent[hash] = e;
            }
        }

        private int _playingIndex=-1;
        public Action OnComplete;
        public void Play(string animationName,Action onComplete = null)
        {
            var i = SkinnedAnimaInfo.clipsInfo.FindIndex(x => x.Name == animationName);
            if (i==-1)
            {
                Debug.LogError($"无法找到动画名为：{animationName} 的动画");
                return;
            }
            
            OnComplete?.Invoke();
            OnComplete = onComplete; 
            _playingIndex = i;
            time = 0f;
            var frameInformation = SkinnedAnimaInfo.clipsInfo[i];
            if (frameInformation.animatedEvents!=null)
            {
                foreach (var gpuAnimatedEvent in frameInformation.animatedEvents)
                {
                    gpuAnimatedEvent.hasTriggered = false;
                    gpuAnimatedEvent.lastTriggerTime = -1;
                }
            }

            // PropertyBlockController.SetFloat("_OffsetSeconds", offsetSeconds);
            PropertyBlockController.SetFloat("_StartFrame", frameInformation.StartFrame);
            PropertyBlockController.SetFloat("_EndFrame", frameInformation.EndFrame);
            PropertyBlockController.SetFloat("_FrameCount", frameInformation.FrameCount);
            PropertyBlockController.Apply();

            IsPlaying = true;
        }

        public void PlayNext(float offsetSeconds)
        {
            var i = _playingIndex;
            if (i==-1)
            {
                i = 0;
            }
            else
            {
                i += 1;
                if (i>=SkinnedAnimaInfo.clipsInfo.Count)
                {
                    i = 0;
                }
            }
            Play(SkinnedAnimaInfo.clipsInfo[i].Name);
        }

        public void Stop()
        {
            PropertyBlockController.SetFloat("_StartFrame", 0);
            PropertyBlockController.SetFloat("_EndFrame", 0);
            PropertyBlockController.SetFloat("_FrameCount", 1);
            PropertyBlockController.Apply();

            IsPlaying = false;
        }
        
        public void SetAllActionPlay()
        {
            var totalFrames = SkinnedAnimaInfo.clipsInfo[^1].EndFrame;
            Debug.Log($"开始播放，从0到：{totalFrames}");
            PropertyBlockController.SetFloat("_StartFrame", 0);
            PropertyBlockController.SetFloat("_EndFrame", totalFrames);
            PropertyBlockController.SetFloat("_FrameCount", totalFrames);
            PropertyBlockController.Apply();

            IsPlaying = true;
        }
        public float NormalizeTime { get; private set; }
        public float time { get; private set; }
        private void Update()
        {
            Tick();
        }


        void UpdateExposedBones(AnimationFrameInfo clip)
        {

            if (SkinnedAnimaInfo.exposedBones is {Count:<=0})
            {
                return;
            }

            if (exposedBones is {Count:<=0})
            {
                return;
            }

            var texIndex = SkinnedAnimaInfo.currentTextureIndex;
            if (texIndex<0)
            {
                return;
            }

            if (SkinnedAnimaInfo.currentUsingTexture == GPUAnimaTextureColorMode._RGBM)
            {
                return;
            }
            var fFrame = time * 30;
            int frameFloor = Mathf.FloorToInt(fFrame);
            int frameCeil = Mathf.CeilToInt(fFrame);
            int currentFrame = clip.StartFrame + Mathf.Clamp(frameFloor, 0, clip.FrameCount - 1);
            int nextFrame = clip.StartFrame + Mathf.Clamp(frameCeil, 0, clip.FrameCount - 1);
            int pixelBeginIndexCurrentFrame = currentFrame * SkinnedAnimaInfo.textures[texIndex].pixelCountPerFrame;//这一帧的骨骼矩阵像素数据的开始的像素索引 
            int pixelBeginIndexNextFrame = nextFrame * SkinnedAnimaInfo.textures[texIndex].pixelCountPerFrame;//这一帧的骨骼矩阵像素数据的开始的像素索引
            var texture = SkinnedAnimaInfo.textures[SkinnedAnimaInfo.currentTextureIndex].animatedTexture as Texture2D;
            for (int i = 0; i < SkinnedAnimaInfo.exposedBones.Count; i++)
            {
                var boneTrans = exposedBones[i];
                var boneInfo = SkinnedAnimaInfo.exposedBones[i];
                var (localPosCur,localRotationCur) = GetAnimationBoneTransform(boneInfo,pixelBeginIndexCurrentFrame,texture);

                if (SkinnedAnimaInfo.isEnableInterpolation)
                {
                    float percent = fFrame - frameFloor;

                    var (localPosNext,localRotationNext) = GetAnimationBoneTransform(boneInfo,pixelBeginIndexNextFrame,texture);
                    boneTrans.localPosition = Vector3.Lerp(localPosCur,localPosNext,percent);
                    boneTrans.localRotation = Quaternion.Lerp(localRotationCur,localRotationNext,percent);
                }
                else
                {
                    boneTrans.localPosition = localPosCur;
                    boneTrans.localRotation = localRotationCur;
                }
                
                // Matrix4x4 recordMatrix = new Matrix4x4();
                //
                // switch (SkinnedAnimaInfo.currentUsingTexture)
                // {
                //     case GPUAnimaTextureColorMode._RGBAHALF:
                //         int matrixBeginIndex = pixelBeginIndex + boneIndex * 3;
                //         var (row0U,row0V) = GetMatrixTextureCoordinate(matrixBeginIndex,texture);
                //         var (row1U,row1V) = GetMatrixTextureCoordinate(matrixBeginIndex+1,texture);
                //         var (row2U,row2V) = GetMatrixTextureCoordinate(matrixBeginIndex+2,texture);
                //         recordMatrix.SetRow(0,texture.GetPixel(row0U,row0V));
                //         recordMatrix.SetRow(1,texture.GetPixel(row1U,row1V));
                //         recordMatrix.SetRow(2,texture.GetPixel(row2U,row2V));
                //         recordMatrix.SetRow(3, new Vector4(0, 0, 0, 1));
                //         break;
                //     case GPUAnimaTextureColorMode._DUAL16FP:
                //         break;
                //     default:
                //         continue;
                //         break;
                // }
                // boneTrans.localPosition = recordMatrix.MultiplyPoint(boneInfo.Position);
                // boneTrans.localRotation = Quaternion.LookRotation(recordMatrix.MultiplyVector(boneInfo.Direction));
            }       
        }

        (Vector3,Quaternion) GetAnimationBoneTransform(ExposedBone bone,int pixelBeginIndex,Texture2D texture)
        {
            if (SkinnedAnimaInfo.currentUsingTexture == GPUAnimaTextureColorMode._RGBM)
            {
                return (default);
            }
            Matrix4x4 recordMatrix = new Matrix4x4();
            int matrixBeginIndex = 0;
            switch (SkinnedAnimaInfo.currentUsingTexture)
            {
                case GPUAnimaTextureColorMode._DUAL16FP:
                    matrixBeginIndex = pixelBeginIndex + bone.Index * 6;
                    var (color0U,color0V) = GetMatrixTextureCoordinate(matrixBeginIndex,texture);
                    var (color1U,color1V) = GetMatrixTextureCoordinate(matrixBeginIndex+1,texture);
                    var (color2U,color2V) = GetMatrixTextureCoordinate(matrixBeginIndex+2,texture);
                    var (color3U,color3V) = GetMatrixTextureCoordinate(matrixBeginIndex+3,texture);
                    var (color4U,color4V) = GetMatrixTextureCoordinate(matrixBeginIndex+4,texture);
                    var (color5U,color5V) = GetMatrixTextureCoordinate(matrixBeginIndex+5,texture);

                    MatrixTextureEncoder.DecodeTwoFloats(texture.GetPixel(color0U,color0V),out var m01,out var m02);
                    MatrixTextureEncoder.DecodeTwoFloats(texture.GetPixel(color1U,color1V),out var m03,out var m04);
                    MatrixTextureEncoder.DecodeTwoFloats(texture.GetPixel(color2U,color2V),out var m11,out var m12);
                    MatrixTextureEncoder.DecodeTwoFloats(texture.GetPixel(color3U,color3V),out var m13,out var m14);
                    MatrixTextureEncoder.DecodeTwoFloats(texture.GetPixel(color4U,color4V),out var m21,out var m22);
                    MatrixTextureEncoder.DecodeTwoFloats(texture.GetPixel(color5U,color5V),out var m23,out var m24);
                    recordMatrix.SetRow(0,new Vector4(m01,m02,m03,m04));
                    recordMatrix.SetRow(1,new Vector4(m11,m12,m13,m14));
                    recordMatrix.SetRow(2,new Vector4(m21,m22,m23,m24));
                    break;
                case GPUAnimaTextureColorMode._RGBAHALF:
                    matrixBeginIndex = pixelBeginIndex + bone.Index * 3;
                    var (row0U,row0V) = GetMatrixTextureCoordinate(matrixBeginIndex,texture);
                    var (row1U,row1V) = GetMatrixTextureCoordinate(matrixBeginIndex+1,texture);
                    var (row2U,row2V) = GetMatrixTextureCoordinate(matrixBeginIndex+2,texture);
                    recordMatrix.SetRow(0,texture.GetPixel(row0U,row0V));
                    recordMatrix.SetRow(1,texture.GetPixel(row1U,row1V));
                    recordMatrix.SetRow(2,texture.GetPixel(row2U,row2V));
                    break;
            }
            recordMatrix.SetRow(3, new Vector4(0, 0, 0, 1));

            var localPos = recordMatrix.MultiplyPoint(bone.Position);
            var localRotation = Quaternion.LookRotation(recordMatrix.MultiplyVector(bone.Direction));
            return (localPos, localRotation);
        }

        (int, int) GetMatrixTextureCoordinate(int pixelIndex,Texture texture)
        {
            int width =texture.width;
            // int height =texture.height;
            int row = Mathf.FloorToInt(pixelIndex / (float)width);
            int column =  pixelIndex % width;
            return (column, row);
        }
        

        void UpdateEvents(AnimationFrameInfo clip)
        {
            if (clip.animatedEvents == null)
            {
                return;
            }
            foreach (var eventTrigger in clip.animatedEvents)
            {
                if (string.IsNullOrEmpty(eventTrigger.eventName))
                {
                    continue;
                }
                if (eventTrigger.hasTriggered && eventTrigger.lastTriggerTime> NormalizeTime)
                {
                    eventTrigger.hasTriggered = false;
                }
                if (!eventTrigger.hasTriggered && NormalizeTime >= eventTrigger.triggerTime)
                {
                    var hash = eventTrigger.eventName.GetHashCode();
                    if (_clipEvent.TryGetValue(hash,out var e))
                    {
                        e?.Invoke();
                    }
                    eventTrigger.hasTriggered = true;
                    eventTrigger.lastTriggerTime = NormalizeTime;
                }
            }
        }

        public void Tick()
        {
            if (_playingIndex<0)
            {
                return;
            }

            var clip = SkinnedAnimaInfo.clipsInfo[_playingIndex];
            if (time>= clip.Seconds)
            {
                OnComplete?.Invoke();
                OnComplete = null;
                // Debug.Log($"播放{clip.Name}结束了");
                if (clip.IsLoop)
                {
                    time -= clip.Seconds;
                }
                else
                {
                    // var percent = Mathf.InverseLerp(0,clip.FrameCount,clip.FrameCount - 1) ;
                    // time = Mathf.Lerp(0, clip.Seconds, percent);// clip.Seconds;
                    time =clip.Seconds;
                }
            }
            else
            {
                time += Time.deltaTime * Speed;
            }
            PropertyBlockController.SetFloat("_KeepingTime",time);
            NormalizeTime = Mathf.InverseLerp(0, clip.Seconds, time);
            PropertyBlockController.Apply();
            UpdateExposedBones(clip);
            UpdateEvents(clip);
        }

        public void SetNormalizeTime(float t)
        {
            if (_playingIndex<0)
            {
                return;
            }
            var clamp = Mathf.Clamp01(t);
            var clip = SkinnedAnimaInfo.clipsInfo[_playingIndex];
            time = Mathf.Lerp(0, clip.Seconds, clamp);
            PropertyBlockController.SetFloat("_KeepingTime",time);
            PropertyBlockController.Apply();
        }
    }
}
