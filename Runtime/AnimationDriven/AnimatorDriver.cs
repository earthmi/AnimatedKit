using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnimatedKit
{
    public class AnimatorDriver : MonoBehaviour,IAnimationExecutor
    {
        public Animator Controller { get; private set; }
        public event Action<int> AnimatorIKEvent;
        private Dictionary<int, Action> _animaEventRegistrant = new();

        private void Awake()
        {
            Controller = GetComponent<Animator>();
        }

        public void OnAnimatorIK(int layerIndex)
        {
            AnimatorIKEvent?.Invoke(layerIndex);
        }

        public void OnAnimationEvent(string n)
        {
            int hash = n.GetHashCode();
            if (_animaEventRegistrant.TryGetValue(hash,out var e))
            {
                e?.Invoke();
            }
        }

        public void SetSpeed(float s)
        {
            Controller.speed = s;
        }

        public void Play(string state)
        {
            Controller.Play(state,0,0);
        }

        public void Play(string state, Action callback)
        {
            if (callback!=null)
            {
                Controller.PlayWithCallBack(state,0,callback,this);
            }
            else
            {
                Play(state);
            }
        }

        public float GetLength(string state)
        {
            return Controller.GetCurrentAnimatorStateInfo(0).length;
        }

        public void RegisterEvent(string n,Action c)
        {
            int hash = n.GetHashCode();
            var exist = _animaEventRegistrant.TryGetValue(hash, out var e);
            if (!exist)
            {
                _animaEventRegistrant.Add(hash,c);
            }
            else
            {
                e += c;
                _animaEventRegistrant[hash] = e;
            }
        }
        

        public void UnRegisterEvent(string eventKey,Action action)
        {
            int hash = eventKey.GetHashCode();
            var exist = _animaEventRegistrant.TryGetValue(hash, out var e);
            if (!exist)
            {
                return;
            }
            e -= action;
            if (e == null)
            {
                _animaEventRegistrant.Remove(hash);
            }
            else
            {
                _animaEventRegistrant[hash] = e;
            }
        }
        
    }
}
