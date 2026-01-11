using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AnimatedKit
{
    public class GPUSkinnedAnimator : MonoBehaviour,IAnimationExecutor
    {
        // [SerializeField]
        // public List<AnimationFrameInfo> FrameInformations;
        [SerializeField]
        MaterialPropertyBlockController PropertyBlockController;

        [SerializeField] private GPUSkinnedAnimationData SkinnedAnimaInfo;

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
        }
        
        public void SetSpeed(float s)
        {
            Speed = s;
        }

        public void Play(string state)
        {
            Play(state, 0);
        }

        public void Play(string state, Action callback)
        {
            Play(state, 0,callback);
        }

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
        public bool Play(string animationName, float offsetSeconds=0f,Action onComplete = null)
        {
            var i = SkinnedAnimaInfo.clipsInfo.FindIndex(x => x.Name == animationName);
            if (i==-1)
            {
                Debug.LogError($"无法找到动画名为：{animationName} 的动画");
                return false;
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

            PropertyBlockController.SetFloat("_OffsetSeconds", offsetSeconds);
            PropertyBlockController.SetFloat("_StartFrame", frameInformation.StartFrame);
            PropertyBlockController.SetFloat("_EndFrame", frameInformation.EndFrame);
            PropertyBlockController.SetFloat("_FrameCount", frameInformation.FrameCount);
            PropertyBlockController.Apply();

            IsPlaying = true;
            return true;
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
            Play(SkinnedAnimaInfo.clipsInfo[i].Name,offsetSeconds);
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
