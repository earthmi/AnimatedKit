using System;

namespace AnimatedKit
{
    public interface IAnimationExecutor
    {
        void SetSpeed(float s);
        void Play(string state);
        void Play(string state, Action callback);
        float GetLength(string state);
        void RegisterEvent(string eventName,Action callback);
        void UnRegisterEvent(string eventName,Action callback);
    }
}