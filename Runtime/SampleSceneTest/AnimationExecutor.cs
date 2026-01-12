using System;
using System.Collections.Generic;
using AnimatedKit;
using UnityEngine;

namespace DefaultNamespace
{
    public class AnimationExecutor : MonoBehaviour
    {
        private List<IAnimationExecutor> _animationExecutors;
        public string state;
        [Range(0,1)]
        public float speed = 1;
        public List<string> states;
        private int _playIndex;
        private bool _isPlayingSingleState;
        private void Awake()
        {
            _animationExecutors = new();
            foreach (Transform VARIABLE in transform)
            {
                IAnimationExecutor exe = VARIABLE.GetComponentInChildren<IAnimationExecutor>();
                if (exe!=null)
                {
                    _animationExecutors.Add(exe);
                }
            }
        }
        
        void Play()
        {
            _isAutoPlaying = false;
            _isPlayingSingleState = true;
            for (int i = 0; i < _animationExecutors.Count; i++)
            {
                var exe = _animationExecutors[i];
                exe.Play(state);
            }
        }

        void PlayAuto()
        {
            _isAutoPlaying = true;
            _isPlayingSingleState = false;

            if (_playIndex>=states.Count)
            {
                _playIndex = 0;
            }
            
            var s = states[_playIndex];
            _length = _animationExecutors[0].GetLength(s);
            _timer = 0;
            for (int i = 0; i < _animationExecutors.Count; i++)
            {
                var exe = _animationExecutors[i];
                exe.Play(s);
            }
        }

        private float _timer;
        private float _length;
        
        private void Update()
        {
            foreach (var VARIABLE in _animationExecutors)
            {
                VARIABLE.SetSpeed(speed);
            }

            if (_isAutoPlaying)
            {
                _timer += Time.deltaTime * speed;
                if (_timer>= _length)
                {
                    _playIndex ++;
                    PlayAuto();
                }
            }
        }

        private bool _isAutoPlaying;
        private void OnGUI()
        {
            Rect btnRect = new Rect(100, 200, 100, 100);

            if (GUI.Button(btnRect,"Play"))
            {
                Play();
            }
            Rect btn1Rect = new Rect(400, 200, 100, 100);

            if (GUI.Button(btn1Rect,"AutoPlay"))
            {
                _playIndex =0;

                PlayAuto();
            }
        }
    }
}