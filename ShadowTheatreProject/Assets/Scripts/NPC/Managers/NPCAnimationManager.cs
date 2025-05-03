using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 将新的 SpriteSheetAnimator 组件替代 Animator
[RequireComponent(typeof(SpriteSheetAnimator))]
public class NPCAnimationManager : MonoBehaviour
{
    [Header("引用")]
    private NPCController controller;
    private NPCPathManager pathManager;
    private NPCEventManager eventManager;
    private SpriteSheetAnimator animator; // 替代原来的 Animator

    [Header("动画设置")]
    public string defaultAnimationName = "Idle"; // 默认动画名称
    private string currentAnimationName;         // 当前播放的动画
    private Coroutine currentAnimationCoroutine; // 当前动画协程

    private void Awake()
    {
        controller = GetComponent<NPCController>();
        pathManager = GetComponent<NPCPathManager>();
        eventManager = GetComponent<NPCEventManager>();
        animator = GetComponent<SpriteSheetAnimator>();

        // 默认播放空闲动画
        currentAnimationName = defaultAnimationName;
        PlayAnimation(defaultAnimationName, true);
    }

    private void OnEnable()
    {
        // 订阅路径点到达事件
        if (pathManager != null)
        {
            pathManager.OnPathPointReached += CheckForPathAnimation;
        }

        // 订阅事件完成事件
        if (eventManager != null)
        {
            eventManager.OnEventCompleted += OnEventCompleted;
        }
    }

    private void OnDisable()
    {
        // 取消订阅
        if (pathManager != null)
        {
            pathManager.OnPathPointReached -= CheckForPathAnimation;
        }

        if (eventManager != null)
        {
            eventManager.OnEventCompleted -= OnEventCompleted;
        }

        // 停止所有协程
        if (currentAnimationCoroutine != null)
        {
            StopCoroutine(currentAnimationCoroutine);
            currentAnimationCoroutine = null;
        }
    }

    // 检查路径点是否有对应的动画
    private void CheckForPathAnimation(int pathPointIndex)
    {
        // 如果正在播放事件相关的动画，不处理路径动画
        if (currentAnimationCoroutine != null)
            return;

        // 获取当前路径配置
        NPCData npcData = controller.Data;
        if (npcData == null)
            return;

        PathConfig currentPath = pathManager.GetCurrentPathConfig();
        if (currentPath == null)
            return;

        // 检查是否有与此路径点关联的已完成事件
        if (eventManager != null)
        {
            PathEvent completedEvent = eventManager.FindCompletedEventForPathPoint(pathPointIndex);

            // 查找是否有未完成的事件与此路径点关联
            bool hasUncompletedEventForPathPoint = false;
            foreach (var evt in eventManager.CurrentPathEvents)
            {
                if (!eventManager.IsEventCompleted(evt.eventID) &&
                    eventManager.IsPathPointInEventRange(pathPointIndex, evt))
                {
                    hasUncompletedEventForPathPoint = true;
                    break;
                }
            }

            // 情况1: 有已完成的事件关联到此路径点
            if (completedEvent != null)
            {
                Debug.Log($"[{gameObject.name}] 动画管理器 - 路径点 {pathPointIndex} 有已完成的事件: {completedEvent.eventID}");

                // 寻找此事件响应中的路径点动作
                foreach (var action in completedEvent.defaultResponse.actions)
                {
                    if (action.pathPointIndex == pathPointIndex && !string.IsNullOrEmpty(action.animationName))
                    {
                        // 播放动画
                        PlayAnimation(action.animationName, false, action.displayDuration);

                        // 移除声音播放，由DialogueManager处理
                        return;
                    }
                }
            }
            // 情况2: 有未完成的事件关联到此路径点 - 使用默认响应
            else if (hasUncompletedEventForPathPoint)
            {
                // 查找未完成事件的默认响应
                foreach (var evt in eventManager.CurrentPathEvents)
                {
                    if (!eventManager.IsEventCompleted(evt.eventID) &&
                        eventManager.IsPathPointInEventRange(pathPointIndex, evt))
                    {
                        // 查找默认响应中的动作
                        foreach (var action in evt.defaultResponse.actions)
                        {
                            if (action.pathPointIndex == pathPointIndex && !string.IsNullOrEmpty(action.animationName))
                            {
                                // 播放动画
                                PlayAnimation(action.animationName, false, action.displayDuration);

                                // 移除声音播放，由DialogueManager处理
                                return;
                            }
                        }
                    }
                }

                Debug.Log($"[{gameObject.name}] 动画管理器 - 路径点 {pathPointIndex} 有未完成的事件，使用默认响应");
                return;
            }
        }

        // 情况3: 没有事件关联到此路径点，执行默认路径动作
        Debug.Log($"[{gameObject.name}] 动画管理器 - 检查路径点 {pathPointIndex} 的默认动画");

        // 查找默认路径动作
        foreach (var action in currentPath.pathActions)
        {
            if (action.pathPointIndex == pathPointIndex && !string.IsNullOrEmpty(action.animationName))
            {
                // 播放动画
                PlayAnimation(action.animationName, false, action.displayDuration);

                // 移除声音播放，由DialogueManager处理
                return;
            }
        }
    }

    // 事件完成回调
    private void OnEventCompleted(string pathId, PathEvent completedEvent, string gestureType)
    {
        // 根据手势类型查找对应的响应
        GestureResponse response = null;

        // 查找匹配的手势响应
        if (!string.IsNullOrEmpty(gestureType))
        {
            foreach (var r in completedEvent.gestureResponses)
            {
                if (r.gestureType == gestureType)
                {
                    response = r;
                    break;
                }
            }
        }

        // 如果没有找到，使用默认响应
        if (response == null)
        {
            response = completedEvent.defaultResponse;
        }

        // 如果有动作序列，播放动作序列中的动画
        if (response != null && response.actions != null && response.actions.Count > 0)
        {
            StartCoroutine(PlayActionAnimationSequence(response.actions));
        }
    }

    // 播放动作序列中的动画
    private IEnumerator PlayActionAnimationSequence(List<ActionData> actions)
    {
        if (actions == null || actions.Count == 0)
            yield break;

        // 获取当前路径上的点索引
        int currentPathPointIndex = -1;
        if (eventManager != null)
        {
            currentPathPointIndex = eventManager.GetCurrentPathPointIndex();
        }

        foreach (var action in actions)
        {
            // 检查是否在当前路径点
            if (currentPathPointIndex >= 0 && action.pathPointIndex != currentPathPointIndex)
            {
                continue; // 跳过不匹配的路径点动作
            }

            // 等待延迟
            if (action.delay > 0)
            {
                yield return new WaitForSeconds(action.delay);
            }

            // 播放动画
            if (!string.IsNullOrEmpty(action.animationName))
            {
                PlayAnimation(action.animationName, false, action.displayDuration);
            }

            // 移除声音播放，由DialogueManager处理

            // 等待此动作完成
            yield return new WaitForSeconds(action.displayDuration);
        }
    }

    // 播放动画
    public void PlayAnimation(string animName, bool loop = false, float duration = 0)
    {
        if (string.IsNullOrEmpty(animName) || animator == null)
            return;

        // 如果当前有动画协程，停止它
        if (currentAnimationCoroutine != null)
        {
            StopCoroutine(currentAnimationCoroutine);
            currentAnimationCoroutine = null;
        }

        // 保存当前动画名称
        currentAnimationName = animName;

        // 使用 SpriteSheetAnimator 播放动画
        animator.Play(animName, loop);

        // 如果有持续时间且不循环，则设置计时器返回默认动画
        if (duration > 0 && !loop)
        {
            currentAnimationCoroutine = StartCoroutine(ReturnToDefaultAnimation(duration));
        }
    }

    // 返回到默认动画的协程
    private IEnumerator ReturnToDefaultAnimation(float delay)
    {
        yield return new WaitForSeconds(delay);

        // 播放默认动画
        animator.Play(defaultAnimationName, true);
        currentAnimationName = defaultAnimationName;
        currentAnimationCoroutine = null;
    }

    public List<string> GetAvailableAnimations()
    {
        if (animator != null)
        {
            return animator.GetAvailableAnimations();
        }

        // 如果 animator 为空，返回空列表
        return new List<string>();
    }
}