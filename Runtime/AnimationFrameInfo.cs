using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace AnimatedKit
{
    [Serializable]
    public class GPUAnimatedEvent
    {
        public string eventName;
        [Range(0f, 1f)] public float triggerTime;
        [HideInInspector] public bool hasTriggered;
        [HideInInspector] public float lastTriggerTime=-1;
        //
        // public bool isPreviewing;
        // public Action<GPUAnimatedEvent> onPreviewClick;
    }

    [Serializable]
    public class AnimationFrameInfo
    {
        public string Name;
        public int StartFrame;
        public int EndFrame;
        public int FrameCount;
        public float Seconds;
        public bool isEditorPreviewing;
        public bool IsLoop;
        public List<GPUAnimatedEvent> animatedEvents;
        public Action<AnimationFrameInfo> OnEditorPreviewClick;
        public AnimationFrameInfo(string name, int startFrame, int endFrame, int frameCount,float seconds,bool isLooping,AnimationEvent[] events)
        {
            Name = name;
            StartFrame = startFrame;
            EndFrame = endFrame;
            FrameCount = frameCount;
            Seconds = seconds;
            IsLoop = isLooping;
            if (events is {Length:>0})
            {
                animatedEvents = new List<GPUAnimatedEvent>();
                for (int j = 0; j < events.Length; j++)
                {
                    var evt = events[j];
                    if (string.IsNullOrEmpty(evt.stringParameter))
                    {
                        continue;
                    }
                    animatedEvents.Add(new GPUAnimatedEvent()
                    {
                        eventName = evt.stringParameter,
                        triggerTime = evt.time / Seconds,
                    });
                }
            }
        }
    }
}

