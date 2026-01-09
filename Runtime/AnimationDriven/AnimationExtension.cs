using System;
using System.Collections;
using UnityEngine;

namespace AnimatedKit
{
    public static class AnimationExtension
    {
        public static void PlayWithCallBack(this Animator animator, string stateName,int layerIndex, Action callBack,MonoBehaviour target)
        {
            // var isPlayingCurrentState = animator.GetCurrentAnimatorStateInfo(layerIndex).IsName(stateName);
            target.StopAllCoroutines();
            animator.Play(stateName, layerIndex, 0);
            IEnumerator PlayingAnimation()
            {
                while (!animator.GetCurrentAnimatorStateInfo(layerIndex).IsName(stateName))
                {
                    yield return null;
                }
                float lastFramePercent = -1;
                
                while (true)
                {
                    var isPlaying = animator.GetCurrentAnimatorStateInfo(layerIndex).IsName(stateName);
                    if (!isPlaying)
                    {
                        Debug.LogWarning($"循环等待动画{stateName}结束回调失败,有新的动画调用");
                        callBack.Invoke();
                        yield break;
                    }
                    var normalizedTime = animator.GetCurrentAnimatorStateInfo(layerIndex).normalizedTime;
                    float fractionInCurrentCycle = normalizedTime % 1.0f;
                    // Debug.LogWarning($"normalizedTime:{normalizedTime},fractionInCurrentCycle:{fractionInCurrentCycle}");
                    var isPlayEnd = fractionInCurrentCycle >= 0.95f; //|| (lastFramePercent > 0.8 && fractionInCurrentCycle < 0.2);
                    // lastFramePercent = fractionInCurrentCycle;
                    if (isPlayEnd)
                    {
                        callBack.Invoke();
                        yield break;
                    }
                    if (lastFramePercent > 0.5f && lastFramePercent > fractionInCurrentCycle)
                    {
                        Debug.LogWarning($"播放动画:{stateName},上一帧播放到：{lastFramePercent}，这一帧:{fractionInCurrentCycle}，已经播放结束了");
                        callBack.Invoke();
                        yield break;
                    }

                    lastFramePercent = fractionInCurrentCycle;
                    yield return null;
                }
            }
            target.StartCoroutine(PlayingAnimation());
        }
    }
}