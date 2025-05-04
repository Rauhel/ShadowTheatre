using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteSheetAnimator))]
public class NPCAnimationManager : MonoBehaviour
{
    [Header("引用")]
    private NPCController controller;
    private NPCPathManager pathManager;
    private NPCEventManager eventManager;
    private SpriteSheetAnimator animator;

    [Header("动画设置")]
    public string defaultAnimationName = "Idle"; // 默认动画名称
    private string currentAnimationName;         // 当前播放的动画
    private int currentPathPointIndex = -1;      // 当前路径点索引

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
    }

    private void Update()
    {
        // 如果使用的是非循环动画，并且动画已结束，恢复默认动画
        if (animator != null && !string.IsNullOrEmpty(currentAnimationName) &&
            currentAnimationName != defaultAnimationName && !animator.IsPlaying())
        {
            Debug.Log($"[{gameObject.name}] 检测到非循环动画结束，恢复默认动画 {defaultAnimationName}");
            PlayAnimation(defaultAnimationName, true);
        }
    }

    // 检查路径点是否有对应的动画
    private void CheckForPathAnimation(int pathPointIndex)
    {
        // 先记录当前路径点，以便动作序列可以使用
        currentPathPointIndex = pathPointIndex;

        Debug.Log($"[{gameObject.name}] 检查路径点{pathPointIndex}的动画");

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
            // 获取所有已完成的事件，而不仅仅是与当前路径点关联的
            List<PathEvent> completedEvents = eventManager.GetAllCompletedEvents();

            // 检查每个已完成事件，查找与当前路径点匹配的动作
            foreach (var completedEvent in completedEvents)
            {
                string eventID = completedEvent.eventID;
                string completionGesture = eventManager.GetEventCompletionGesture(eventID);

                Debug.Log($"[{gameObject.name}] 检查已完成事件 {eventID} (手势={completionGesture}) 的路径点{pathPointIndex}动作");

                // 首先处理默认响应的动画 - 这总是执行
                bool defaultAnimationPlayed = false;
                if (completedEvent.defaultResponse != null)
                {
                    foreach (var action in completedEvent.defaultResponse.actions)
                    {
                        if (action.pathPointIndex == pathPointIndex &&
                            action.isActionActive &&
                            !string.IsNullOrEmpty(action.animationName))
                        {
                            PlayAnimation(action.animationName, action.loopAnimation);
                            defaultAnimationPlayed = true;
                            Debug.Log($"[{gameObject.name}] 路径点{pathPointIndex}播放已完成事件 {eventID} 的默认响应动画: {action.animationName}");
                            break; // 只播放第一个匹配的
                        }
                    }
                }

                // 如果有特定手势响应，并且使用了该手势完成事件，执行对应动画
                if (!string.IsNullOrEmpty(completionGesture))
                {
                    foreach (var response in completedEvent.gestureResponses)
                    {
                        if (response.gestureType == completionGesture)
                        {
                            foreach (var action in response.actions)
                            {
                                if (action.pathPointIndex == pathPointIndex &&
                                    action.isActionActive &&
                                    !string.IsNullOrEmpty(action.animationName))
                                {
                                    // 如果覆盖之前的动画，或者之前没有播放过动画
                                    if (action.overridePrevious || !defaultAnimationPlayed)
                                    {
                                        PlayAnimation(action.animationName, action.loopAnimation);
                                        Debug.Log($"[{gameObject.name}] 路径点{pathPointIndex}播放已完成事件 {eventID} 的手势 {completionGesture} 响应动画: {action.animationName}");
                                    }
                                    return; // 找到对应手势的动作后退出
                                }
                            }
                            break; // 已找到对应手势响应，跳出循环
                        }
                    }
                }

                // 如果已经播放了默认动画，直接返回
                if (defaultAnimationPlayed)
                    return;
            }
        }

        // 如果没有已完成事件或没有匹配的动作，查找默认路径动作的动画
        foreach (var action in currentPath.pathActions)
        {
            if (action.pathPointIndex == pathPointIndex &&
                action.isActionActive &&
                !string.IsNullOrEmpty(action.animationName))
            {
                // 播放动画，使用动作中定义的循环设置
                PlayAnimation(action.animationName, action.loopAnimation);
                Debug.Log($"[{gameObject.name}] 路径点{pathPointIndex}播放默认路径动画: {action.animationName}");
                return;
            }
        }

        // 如果没有找到任何动画，恢复默认动画（这确保非循环动画结束后有动画播放）
        if (!animator.IsPlaying())
        {
            Debug.Log($"[{gameObject.name}] 路径点{pathPointIndex}没有找到动画，恢复默认动画: {defaultAnimationName}");
            PlayAnimation(defaultAnimationName, true);
        }
    }

    // 事件完成回调
    private void OnEventCompleted(string pathId, PathEvent completedEvent, string gestureType)
    {
        Debug.Log($"[{gameObject.name}] 事件完成回调: 事件={completedEvent.eventID}, 手势={gestureType}");

        // 对于事件完成时的动作，仅处理不关联特定路径点的动作(pathPointIndex < 0)或与当前路径点匹配的动作
        // 其他路径点的动作将在NPC到达相应路径点时由CheckForPathAnimation处理

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
                    Debug.Log($"[{gameObject.name}] 找到匹配手势响应: {gestureType}");
                    break;
                }
            }
        }

        // 如果没有找到，使用默认响应
        if (response == null)
        {
            response = completedEvent.defaultResponse;
            Debug.Log($"[{gameObject.name}] 使用默认响应");
        }

        // 如果有动作序列，播放动作序列中的动画
        if (response != null && response.actions != null && response.actions.Count > 0)
        {
            // 过滤只获取当前路径点的可用动作，或没有指定路径点的动作
            int currentPathPointIndex = eventManager?.GetCurrentPathPointIndex() ?? -1;
            List<ActionData> activeActions = new List<ActionData>();

            foreach (var action in response.actions)
            {
                // 只添加当前点的动作或无特定路径点的动作
                if (action.isActionActive &&
                    (action.pathPointIndex < 0 || action.pathPointIndex == currentPathPointIndex))
                {
                    activeActions.Add(action);
                }
            }

            if (activeActions.Count > 0)
            {
                Debug.Log($"[{gameObject.name}] 开始播放动作序列: {activeActions.Count}个动作(当前路径点{currentPathPointIndex})");
                StartCoroutine(PlayActionAnimationSequence(activeActions));
            }
            else
            {
                Debug.Log($"[{gameObject.name}] 事件响应中没有当前路径点({currentPathPointIndex})可用的动作");
            }
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] 事件响应中没有定义动作");
        }
    }

    // 播放动作序列中的动画
    private IEnumerator PlayActionAnimationSequence(List<ActionData> actions)
    {
        if (actions == null || actions.Count == 0)
        {
            Debug.LogWarning($"[{gameObject.name}] 动作序列为空");
            yield break;
        }

        Debug.Log($"[{gameObject.name}] 准备播放动作序列: {actions.Count}个动作");

        // 获取当前路径上的点索引
        int currentPathPointIndex = -1;
        if (eventManager != null)
        {
            currentPathPointIndex = eventManager.GetCurrentPathPointIndex();
            Debug.Log($"[{gameObject.name}] 当前路径点索引: {currentPathPointIndex}");
        }

        // 设置一个标志，表示我们正在处理事件动作序列
        bool inEventActionSequence = true;

        int actionCounter = 0;
        foreach (var action in actions)
        {
            actionCounter++;
            Debug.Log($"[{gameObject.name}] 准备执行动作{actionCounter}: 动画={action.animationName}, 路径点={action.pathPointIndex}");

            // 等待延迟
            if (action.delay > 0)
            {
                Debug.Log($"[{gameObject.name}] 等待延迟: {action.delay}秒");
                yield return new WaitForSeconds(action.delay);
            }

            // 检查是否需要在当前点播放此动作的动画
            bool shouldPlayAnimation = false;

            // 如果没有指定路径点，或者路径点与当前点匹配，才播放动画
            if (action.pathPointIndex < 0 || action.pathPointIndex == currentPathPointIndex)
            {
                shouldPlayAnimation = true;
            }

            // 播放动画，使用动作中定义的循环设置
            if (shouldPlayAnimation && !string.IsNullOrEmpty(action.animationName))
            {
                Debug.Log($"[{gameObject.name}] 播放动作动画: {action.animationName}, 循环={action.loopAnimation}");
                PlayAnimation(action.animationName, action.loopAnimation);

                // 等待对话显示完成
                if (action.displayDuration > 0)
                {
                    Debug.Log($"[{gameObject.name}] 等待对话显示完成: {action.displayDuration}秒");

                    // 分段等待，以便可以被路径点动画打断
                    float remainingTime = action.displayDuration;
                    float waitInterval = 0.1f; // 每段等待0.1秒

                    while (remainingTime > 0 && inEventActionSequence)
                    {
                        float waitTime = Mathf.Min(waitInterval, remainingTime);
                        yield return new WaitForSeconds(waitTime);
                        remainingTime -= waitTime;

                        // 检查是否已经被新的路径点动画中断
                        if (!inEventActionSequence)
                        {
                            Debug.Log($"[{gameObject.name}] 动作序列被路径点动画中断");
                            yield break;
                        }
                    }
                }
            }
            else
            {
                Debug.Log($"[{gameObject.name}] 跳过动作，不在当前路径点(当前={currentPathPointIndex}, 动作定义={action.pathPointIndex})或没有指定动画");
            }
        }

        // 如果没有被中断，恢复默认动画
        PlayAnimation(defaultAnimationName, true);
        Debug.Log($"[{gameObject.name}] 动作序列播放完成，恢复默认动画");
        inEventActionSequence = false;
    }

    /// <summary>
    /// 播放指定的动画
    /// </summary>
    /// <param name="animName">动画名称</param>
    /// <param name="loop">是否循环播放</param>
    public void PlayAnimation(string animName, bool loop = false)
    {
        Debug.Log($"[{gameObject.name}] PlayAnimation被调用: {animName}, 循环={loop}");

        if (string.IsNullOrEmpty(animName) || animator == null)
        {
            Debug.LogWarning($"[{gameObject.name}] 无法播放动画: animName={animName}, animator={animator != null}");
            return;
        }

        // 保存当前动画名称
        currentAnimationName = animName;

        // 使用 SpriteSheetAnimator 播放动画
        animator.Play(animName, loop);
        Debug.Log($"[{gameObject.name}] 调用animator.Play: {animName}, 循环={loop}");
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