using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(SpriteSheetAnimator))]
public class NPCActionExecutor : MonoBehaviour
{
    [Header("对话 UI 设置")]
    public GameObject dialoguePrefab; // 对话气泡预制体
    public Vector3 dialogueOffset = new Vector3(0, 2.0f, 0); // 对话气泡相对于 NPC 的偏移
    public float fadeInTime = 0.2f;
    public float fadeOutTime = 0.2f;

    [Header("引用")]
    private NPCController controller;
    private NPCPathManager pathManager;
    private NPCEventManager eventManager;
    private SpriteSheetAnimator animator;
    private AudioSource audioSource;

    [Header("Action执行状态")]
    private Queue<ActionData> actionQueue = new Queue<ActionData>(); // 全局action队列
    private bool isExecutingAction = false;                          // 是否正在执行action
    private float lastActionEndTime = 0f;                           // 上一个action结束时间
    private float totalStopTime = 0f;                               // 累积停留时间
    private bool isMovementStopped = false;                         // 移动是否被停止

    [Header("对话系统")]
    private GameObject currentDialogueBubble;
    private TextMeshProUGUI dialogueText;
    private Coroutine displayCoroutine;

    private void Awake()
    {
        controller = GetComponent<NPCController>();
        pathManager = GetComponent<NPCPathManager>();
        eventManager = GetComponent<NPCEventManager>();
        animator = GetComponent<SpriteSheetAnimator>();
        audioSource = GetComponent<AudioSource>();

        // 如果没有AudioSource，添加一个
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // 初始化对话气泡
        if (dialoguePrefab != null)
        {
            currentDialogueBubble = Instantiate(dialoguePrefab, transform);
            currentDialogueBubble.transform.localPosition = dialogueOffset;
            dialogueText = currentDialogueBubble.GetComponentInChildren<TextMeshProUGUI>();
            currentDialogueBubble.SetActive(false);
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] 对话气泡预制体未分配！请分配一个带有 TextMeshProUGUI 组件的预制体。");
        }
    }

    private void OnEnable()
    {
        // 订阅路径点到达事件
        if (pathManager != null)
        {
            pathManager.OnPathPointReached += OnPathPointReached;
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
            pathManager.OnPathPointReached -= OnPathPointReached;
        }

        if (eventManager != null)
        {
            eventManager.OnEventCompleted -= OnEventCompleted;
        }

        // 停止所有协程
        StopAllCoroutines();
    }

    #region 事件处理

    // 路径点到达时添加action到队列
    private void OnPathPointReached(int pathPointIndex)
    {
        Debug.Log($"[{gameObject.name}] 到达路径点 {pathPointIndex}");

        List<ActionData> newActions = CollectActionsForPoint(pathPointIndex);
        foreach (var action in newActions)
        {
            actionQueue.Enqueue(action);
            Debug.Log($"[{gameObject.name}] 添加action到队列: 路径点{action.pathPointIndex}");
        }

        // 如果当前没有执行action，开始执行队列
        if (!isExecutingAction && actionQueue.Count > 0)
        {
            StartCoroutine(ProcessActionQueue());
        }
    }

    // 事件完成回调
    private void OnEventCompleted(string pathId, PathEvent completedEvent, string gestureType)
    {
        Debug.Log($"[{gameObject.name}] 事件完成回调：事件={completedEvent.eventID}，手势={gestureType}");

        // 收集事件相关的action
        List<ActionData> eventActions = CollectEventActions(completedEvent, gestureType);
        foreach (var action in eventActions)
        {
            actionQueue.Enqueue(action);
            Debug.Log($"[{gameObject.name}] 添加事件action到队列: 路径点{action.pathPointIndex}");
        }

        // 如果当前没有执行action，开始执行队列
        if (!isExecutingAction && actionQueue.Count > 0)
        {
            StartCoroutine(ProcessActionQueue());
        }
    }

    #endregion

    #region Action收集

    // 收集指定路径点的所有action
    private List<ActionData> CollectActionsForPoint(int pathPointIndex)
    {
        List<ActionData> actions = new List<ActionData>();

        // 检查当前幕是否可用
        if (!IsPointAvailableInCurrentAct(pathPointIndex))
        {
            return actions;
        }

        // 收集默认路径action（总是执行）
        PathConfig currentPath = pathManager.GetCurrentPathConfig();
        if (currentPath != null)
        {
            foreach (var action in currentPath.pathActions)
            {
                if (action.pathPointIndex == pathPointIndex &&
                    action.isActionActive)
                {
                    actions.Add(action);
                }
            }
        }

        return actions;
    }

    // 收集事件相关的action
    private List<ActionData> CollectEventActions(PathEvent completedEvent, string gestureType)
    {
        List<ActionData> actions = new List<ActionData>();

        if (completedEvent == null)
            return actions;

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

        // 收集响应中的action
        if (response != null && response.actions != null)
        {
            foreach (var action in response.actions)
            {
                if (action.isActionActive)
                {
                    actions.Add(action);
                }
            }
        }

        return actions;
    }

    // 检查路径点在当前幕是否可用
    private bool IsPointAvailableInCurrentAct(int pathPointIndex)
    {
        GameState.State currentState = GameState.Instance.GetCurrentState();

        // 这里可以根据需要添加更复杂的逻辑
        // 目前简单返回true，表示所有路径点在所有幕都可用
        return true;
    }

    #endregion

    #region Action执行

    // 处理action队列
    private IEnumerator ProcessActionQueue()
    {
        isExecutingAction = true;
        totalStopTime = 0f;
        
        Debug.Log($"[{gameObject.name}] 开始处理action队列，共{actionQueue.Count}个action");

        while (actionQueue.Count > 0)
        {
            ActionData action = actionQueue.Dequeue();
            
            Debug.Log($"[{gameObject.name}] 执行action: 路径点{action.pathPointIndex}, 延迟{action.delay}s, 停留{action.stopTime}s");

            // 等待延迟时间
            if (action.delay > 0)
            {
                float waitTime = action.delay - (Time.time - lastActionEndTime);
                if (waitTime > 0)
                {
                    Debug.Log($"[{gameObject.name}] 等待延迟时间: {waitTime}s");
                    yield return new WaitForSeconds(waitTime);
                }
            }

            // 累加停留时间并停止移动
            if (action.stopTime > 0)
            {
                totalStopTime += action.stopTime;
                if (!isMovementStopped && pathManager != null)
                {
                    pathManager.StopMovement(); // 停止移动
                    isMovementStopped = true;
                    Debug.Log($"[{gameObject.name}] 停止移动，累积停留时间: {totalStopTime}s");
                }
            }

            // 执行action内容
            yield return StartCoroutine(ExecuteSingleAction(action));

            // 更新上次action结束时间
            lastActionEndTime = Time.time;
        }

        // 所有action执行完毕，处理停留时间
        if (totalStopTime > 0 && isMovementStopped && pathManager != null)
        {
            Debug.Log($"[{gameObject.name}] 等待停留时间结束: {totalStopTime}s");
            yield return new WaitForSeconds(totalStopTime);
            pathManager.ResumeMovement(); // 恢复移动
            isMovementStopped = false;
            Debug.Log($"[{gameObject.name}] 恢复移动");
        }

        isExecutingAction = false;
        Debug.Log($"[{gameObject.name}] Action队列执行完毕");
    }

    // 执行单个action
    private IEnumerator ExecuteSingleAction(ActionData action)
    {
        Debug.Log($"[{gameObject.name}] 开始执行action内容");

        float maxDuration = 0f;
        List<Coroutine> runningCoroutines = new List<Coroutine>();

        // 播放一次性音效
        if (action.oneShotSFX != null)
        {
            audioSource.PlayOneShot(action.oneShotSFX);
            maxDuration = Mathf.Max(maxDuration, action.oneShotSFX.length);
            Debug.Log($"[{gameObject.name}] 播放音效，时长: {action.oneShotSFX.length}s");
        }

        // 播放动画
        if (!string.IsNullOrEmpty(action.animationName) && action.animationLoopCount > 0)
        {
            float animDuration = PlayAnimationWithLoops(action.animationName, action.animationLoopCount);
            maxDuration = Mathf.Max(maxDuration, animDuration);
            Debug.Log($"[{gameObject.name}] 播放动画: {action.animationName}, 循环{action.animationLoopCount}次, 总时长: {animDuration}s");
        }

        // 显示对话
        if (!string.IsNullOrEmpty(action.dialogueText))
        {
            Coroutine dialogueCoroutine = StartCoroutine(DisplayDialogueCoroutine(action.dialogueText, action.displayDuration, action.voiceClip));
            runningCoroutines.Add(dialogueCoroutine);
            maxDuration = Mathf.Max(maxDuration, action.displayDuration);
            Debug.Log($"[{gameObject.name}] 显示对话: {action.dialogueText.Substring(0, Mathf.Min(20, action.dialogueText.Length))}..., 时长: {action.displayDuration}s");
        }

        // 等待最长元素完成
        Debug.Log($"[{gameObject.name}] 等待action完成，最长时间: {maxDuration}s");
        yield return new WaitForSeconds(maxDuration);

        // 停止所有运行中的协程
        foreach (var coroutine in runningCoroutines)
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
            }
        }

        Debug.Log($"[{gameObject.name}] Action执行完成");
    }

    #endregion

    #region 动画系统

    // 播放指定循环次数的动画
    private float PlayAnimationWithLoops(string animName, int loopCount)
    {
        if (animator == null || string.IsNullOrEmpty(animName) || loopCount <= 0)
        {
            return 0f;
        }

        // 获取单次动画时长（这需要SpriteSheetAnimator提供接口）
        float singleAnimDuration = GetAnimationDuration(animName);
        float totalDuration = singleAnimDuration * loopCount;

        // 播放动画
        animator.Play(animName, loopCount > 1);

        return totalDuration;
    }

    // 获取动画时长（需要SpriteSheetAnimator支持）
    private float GetAnimationDuration(string animName)
    {
        if (animator == null || string.IsNullOrEmpty(animName))
        {
            return 1.0f; // 默认1秒
        }

        // 从SpriteSheetAnimator的动画列表中查找对应动画
        foreach (var animation in animator.animations)
        {
            if (animation.animationName == animName)
            {
                // 计算单次动画时长：总帧数 / 帧率
                float singleAnimDuration = animation.frameCount / animation.frameRate;
                return singleAnimDuration;
            }
        }

        // 如果没找到动画，返回默认值
        Debug.LogWarning($"[{gameObject.name}] 未找到动画 '{animName}'，使用默认时长1秒");
        return 1.0f;
    }

    // 播放指定的动画
    public void PlayAnimation(string animName, bool loop = false)
    {
        if (string.IsNullOrEmpty(animName) || animator == null)
        {
            return;
        }

        animator.Play(animName, loop);
    }

    #endregion

    #region 对话系统

    // 显示对话的协程
    private IEnumerator DisplayDialogueCoroutine(string text, float duration, AudioClip voiceClip)
    {
        if (dialogueText == null || currentDialogueBubble == null)
        {
            Debug.LogError($"[{gameObject.name}] 对话组件未正确初始化");
            yield break;
        }

        // 播放语音
        if (voiceClip != null)
        {
            PlayDialogueVoice(voiceClip);
        }

        // 设置文本
        dialogueText.text = text;

        // 淡入
        currentDialogueBubble.SetActive(true);
        CanvasGroup canvasGroup = currentDialogueBubble.GetComponent<CanvasGroup>();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0;
            float startTime = Time.time;

            while (Time.time < startTime + fadeInTime)
            {
                canvasGroup.alpha = (Time.time - startTime) / fadeInTime;
                yield return null;
            }

            canvasGroup.alpha = 1;
        }

        // 等待显示时间
        yield return new WaitForSeconds(duration);

        // 淡出
        if (canvasGroup != null)
        {
            float startTime = Time.time;

            while (Time.time < startTime + fadeOutTime)
            {
                canvasGroup.alpha = 1 - (Time.time - startTime) / fadeOutTime;
                yield return null;
            }

            canvasGroup.alpha = 0;
        }

        currentDialogueBubble.SetActive(false);
    }

    // 播放对话声音
    private void PlayDialogueVoice(AudioClip voiceClip)
    {
        if (voiceClip != null && SoundManager.Instance != null)
        {
            // 检查声音是否已经添加到SoundManager
            int sfxIndex = SoundManager.Instance.MySFXList.IndexOf(voiceClip);

            if (sfxIndex >= 0)
            {
                // 如果已经存在，直接播放
                SoundManager.Instance.PlayOneShotSFX(sfxIndex, gameObject, 1.0f);
            }
            else
            {
                // 如果不存在，添加到列表并播放
                SoundManager.Instance.MySFXList.Add(voiceClip);
                sfxIndex = SoundManager.Instance.MySFXList.Count - 1;
                SoundManager.Instance.PlayOneShotSFX(sfxIndex, gameObject, 1.0f);
            }
        }
        else if (voiceClip != null)
        {
            // 如果没有SoundManager，直接用AudioSource播放
            audioSource.PlayOneShot(voiceClip);
        }
    }

    #endregion

    #region 公共接口

    // 获取可用动画列表
    public List<string> GetAvailableAnimations()
    {
        if (animator != null)
        {
            return animator.GetAvailableAnimations();
        }

        return new List<string>();
    }

    // 手动添加action到队列
    public void AddActionToQueue(ActionData action)
    {
        actionQueue.Enqueue(action);

        // 如果当前没有执行action，开始执行队列
        if (!isExecutingAction && actionQueue.Count > 0)
        {
            StartCoroutine(ProcessActionQueue());
        }
    }

    // 清空action队列
    public void ClearActionQueue()
    {
        actionQueue.Clear();
        StopAllCoroutines();
        
        if (isMovementStopped && pathManager != null)
        {
            pathManager.ResumeMovement();
            isMovementStopped = false;
        }
        
        isExecutingAction = false;
    }

    // 获取当前队列中的action数量
    public int GetQueueCount()
    {
        return actionQueue.Count;
    }

    #endregion
} 