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
    public float fadeInTime = 0.05f;   // 非常快的淡入（50毫秒）
    public float fadeOutTime = 0.05f;  // 非常快的淡出（50毫秒）

    [Header("引用")]
    private NPCController controller;
    private NPCPathManager pathManager;
    private NPCEventManager eventManager;
    private SpriteSheetAnimator animator;
    private AudioSource audioSource;
    private NPCDialogueScheduler dialogueScheduler; // 对话调度器

    [Header("Action执行状态")]
    private Queue<ActionData> actionQueue = new Queue<ActionData>(); // 全局action队列
    private bool isExecutingAction = false;                          // 是否正在执行action
    private float lastActionEndTime = 0f;                           // 上一个action结束时间
    
    [Header("时间触发设置")]
    [SerializeField] private bool useTimeBasedDialogue = true;       // 是否启用基于时间的对话系统

    [Header("对话系统")]
    private GameObject currentDialogueBubble;
    private TextMeshProUGUI dialogueText;
    private Coroutine currentDialogueCoroutine;  // 当前正在显示的对话协程
    private bool isDisplayingDialogue = false;   // 是否正在显示对话（互斥锁）

    private void Awake()
    {
        controller = GetComponent<NPCController>();
        pathManager = GetComponent<NPCPathManager>();
        eventManager = GetComponent<NPCEventManager>();
        animator = GetComponent<SpriteSheetAnimator>();
        audioSource = GetComponent<AudioSource>();
        
        // 获取或添加对话调度器
        dialogueScheduler = GetComponent<NPCDialogueScheduler>();
        if (dialogueScheduler == null && useTimeBasedDialogue)
        {
            dialogueScheduler = gameObject.AddComponent<NPCDialogueScheduler>();
            Debug.Log($"[{gameObject.name}] 自动添加NPCDialogueScheduler组件");
        }

        // 如果没有AudioSource，添加一个
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // 初始化对话气泡
        if (dialoguePrefab != null)
        {
            // 首先检查是否已经有现成的对话气泡（用户在Scene中设置的）
            TextMeshProUGUI existingText = GetComponentInChildren<TextMeshProUGUI>();
            if (existingText != null)
            {
                // 使用Scene中现有的对话气泡
                currentDialogueBubble = existingText.transform.GetComponentInParent<Canvas>().gameObject;
                dialogueText = existingText;
                Debug.Log($"[{gameObject.name}] 使用Scene中现有的对话气泡，保持用户设置");
            }
            else
            {
                // 如果Scene中没有，再从预制体创建
                currentDialogueBubble = Instantiate(dialoguePrefab, transform);
                currentDialogueBubble.transform.localPosition = dialogueOffset;
                dialogueText = currentDialogueBubble.GetComponentInChildren<TextMeshProUGUI>();
                Debug.Log($"[{gameObject.name}] 从预制体创建新的对话气泡");
            }
            
            // 确保对话气泡有CanvasGroup组件（用于淡入淡出效果）
            if (currentDialogueBubble != null)
            {
                CanvasGroup canvasGroup = currentDialogueBubble.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = currentDialogueBubble.AddComponent<CanvasGroup>();
                    Debug.Log($"[{gameObject.name}] 自动为对话气泡添加CanvasGroup组件以支持淡入淡出效果");
                }
                canvasGroup.alpha = 0f; // 初始设为透明
            }
            
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
        
        // 订阅对话调度器事件
        if (dialogueScheduler != null)
        {
            dialogueScheduler.OnDialogueReady += OnDialogueReady;
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
        
        if (dialogueScheduler != null)
        {
            dialogueScheduler.OnDialogueReady -= OnDialogueReady;
        }

        // 停止所有协程
        StopAllCoroutines();
        
        // 清理对话状态
        isDisplayingDialogue = false;
        currentDialogueCoroutine = null;
    }

    #region 事件处理

    // 路径点到达时添加action到队列
    private void OnPathPointReached(int pathPointIndex)
    {
        Debug.Log($"[{gameObject.name}] ActionExecutor收到路径点到达事件：点{pathPointIndex}");

        List<ActionData> newActions = CollectActionsForPoint(pathPointIndex);
        Debug.Log($"[{gameObject.name}] 为路径点{pathPointIndex}收集到{newActions.Count}个action");
        
        foreach (var action in newActions)
        {
            // 检查是否启用了时间触发系统并且有对话内容
            if (useTimeBasedDialogue && dialogueScheduler != null && !string.IsNullOrEmpty(action.dialogueText))
            {
                // 使用对话调度器处理对话
                dialogueScheduler.ScheduleDialogue(action, pathPointIndex);
                Debug.Log($"[{gameObject.name}] 将对话交给调度器处理: 路径点{action.pathPointIndex}, {action.GetTriggerTimeText()}");
            }
            else
            {
                // 使用传统的即时队列处理
                actionQueue.Enqueue(action);
                Debug.Log($"[{gameObject.name}] 添加action到传统队列: 路径点{action.pathPointIndex}");
            }
        }

        Debug.Log($"[{gameObject.name}] 当前action队列长度: {actionQueue.Count}, 正在执行action: {isExecutingAction}");

        // 如果当前没有执行action，开始执行队列
        if (!isExecutingAction && actionQueue.Count > 0)
        {
            Debug.Log($"[{gameObject.name}] 开始执行action队列");
            StartCoroutine(ProcessActionQueue());
        }
        else if (isExecutingAction)
        {
            Debug.Log($"[{gameObject.name}] 已在执行action，新action已加入队列等待");
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
            // 事件触发的对话也支持时间调度
            if (useTimeBasedDialogue && dialogueScheduler != null && !string.IsNullOrEmpty(action.dialogueText))
            {
                dialogueScheduler.ScheduleDialogue(action, action.pathPointIndex);
                Debug.Log($"[{gameObject.name}] 将事件对话交给调度器处理: {action.GetTriggerTimeText()}");
            }
            else
            {
                actionQueue.Enqueue(action);
                Debug.Log($"[{gameObject.name}] 添加事件action到传统队列: 路径点{action.pathPointIndex}");
            }
        }

        // 如果当前没有执行action，开始执行队列
        if (!isExecutingAction && actionQueue.Count > 0)
        {
            StartCoroutine(ProcessActionQueue());
        }
    }
    
    // 对话调度器就绪回调
    private void OnDialogueReady(ActionData action)
    {
        Debug.Log($"[{gameObject.name}] 收到调度器对话就绪通知: {action.GetTriggerTimeText()}, {action.GetPriorityText()}");
        
        // 将就绪的对话加入即时执行队列
        actionQueue.Enqueue(action);
        
        // 如果当前没有执行action，立即开始执行
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
        
        // Debug.Log($"[{gameObject.name}] ActionExecutor开始处理action队列，共{actionQueue.Count}个action");

        while (actionQueue.Count > 0)
        {
            ActionData action = actionQueue.Dequeue();
            
            Debug.Log($"[{gameObject.name}] 执行新action: 路径点{action.pathPointIndex}, 对话:{!string.IsNullOrEmpty(action.dialogueText)}");

            // 等待延迟时间
            if (action.delay > 0)
            {
                float waitTime = action.delay - (Time.time - lastActionEndTime);
                if (waitTime > 0)
                {
                    // Debug.Log($"[{gameObject.name}] 等待延迟时间: {waitTime}s");
                    yield return new WaitForSeconds(waitTime);
                }
            }

            // 执行action内容（不再控制移动）
            yield return StartCoroutine(ExecuteSingleAction(action));

            // 更新上次action结束时间
            lastActionEndTime = Time.time;
        }

        isExecutingAction = false;
        Debug.Log($"[{gameObject.name}] ActionExecutor队列执行完毕");
    }

    // 执行单个action
    private IEnumerator ExecuteSingleAction(ActionData action)
    {
        // Debug.Log($"[{gameObject.name}] 开始执行action内容");

        float maxDuration = 0f;
        List<Coroutine> runningCoroutines = new List<Coroutine>();

        // 播放一次性音效
        if (action.oneShotSFX != null)
        {
            audioSource.PlayOneShot(action.oneShotSFX);
            maxDuration = Mathf.Max(maxDuration, action.oneShotSFX.length);
            // Debug.Log($"[{gameObject.name}] 播放音效，时长: {action.oneShotSFX.length}s");
        }

        // 播放动画
        if (!string.IsNullOrEmpty(action.animationName) && action.animationLoopCount > 0)
        {
            float animDuration = PlayAnimationWithLoops(action.animationName, action.animationLoopCount);
            maxDuration = Mathf.Max(maxDuration, animDuration);
            // Debug.Log($"[{gameObject.name}] 播放动画: {action.animationName}, 循环{action.animationLoopCount}次, 总时长: {animDuration}s");
        }

        // 显示对话 - 使用互斥机制确保同时只有一个对话显示
        if (!string.IsNullOrEmpty(action.dialogueText))
        {
            // 如果有正在显示的对话，先停止它
            if (currentDialogueCoroutine != null)
            {
                StopCoroutine(currentDialogueCoroutine);
                currentDialogueCoroutine = null;
            }
            
            // 启动新的对话
            currentDialogueCoroutine = StartCoroutine(DisplayDialogueCoroutine(action.dialogueText, action.displayDuration, action.voiceClip));
            // Debug.Log($"[{gameObject.name}] 启动对话显示: {action.dialogueText.Substring(0, Mathf.Min(20, action.dialogueText.Length))}..., 显示时长: {action.displayDuration}s");
        }

        // 等待其他元素完成（音效和动画），对话不阻塞队列处理
        if (maxDuration > 0)
        {
            yield return new WaitForSeconds(maxDuration);
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
            currentDialogueCoroutine = null;
            yield break;
        }

        isDisplayingDialogue = true;
        Debug.Log($"[{gameObject.name}] 开始显示对话: '{text}', 时长: {duration}秒");

        try
        {
            // 播放语音
            if (voiceClip != null)
            {
                PlayDialogueVoice(voiceClip);
            }

            // 设置文本
            dialogueText.text = text;
            
            Debug.Log($"[{gameObject.name}] 文字设置完成 - 内容: '{dialogueText.text}', 大小: {dialogueText.fontSize}, 颜色: {dialogueText.color}");

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
                Debug.Log($"[{gameObject.name}] 对话淡入完成");
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name}] 对话气泡缺少CanvasGroup组件，无法实现淡入淡出效果");
            }

            // 等待显示时间
            Debug.Log($"[{gameObject.name}] 对话显示中，等待 {duration} 秒");
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
                Debug.Log($"[{gameObject.name}] 对话淡出完成");
            }

            currentDialogueBubble.SetActive(false);
            Debug.Log($"[{gameObject.name}] 对话显示结束");
        }
        finally
        {
            // 确保在协程结束时清理状态
            isDisplayingDialogue = false;
            currentDialogueCoroutine = null;
        }
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
                // 如果已经存在，直接播放（移除了GameObject参数，现在只使用2个参数）
                SoundManager.Instance.PlayOneShotSFX(sfxIndex, 1.0f);
            }
            else
            {
                // 如果不存在，添加到列表并播放（移除了GameObject参数，现在只使用2个参数）
                SoundManager.Instance.MySFXList.Add(voiceClip);
                sfxIndex = SoundManager.Instance.MySFXList.Count - 1;
                SoundManager.Instance.PlayOneShotSFX(sfxIndex, 1.0f);
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
        
        isExecutingAction = false;
    }

    // 获取当前队列中的action数量
    public int GetQueueCount()
    {
        return actionQueue.Count;
    }

    // 测试对话显示（用于调试）
    [ContextMenu("测试对话显示")]
    public void TestDialogueDisplay()
    {
        if (dialogueText == null || currentDialogueBubble == null)
        {
            Debug.LogError($"[{gameObject.name}] 对话组件未初始化，无法测试");
            return;
        }
        
        // 只设置文字内容，保持用户的字体设置
        dialogueText.text = "测试对话文字显示";
        currentDialogueBubble.SetActive(true);
        
        Debug.Log($"[{gameObject.name}] 测试对话已显示：'{dialogueText.text}'");
        Debug.Log($"[{gameObject.name}] 文字位置：{dialogueText.transform.position}");
        Debug.Log($"[{gameObject.name}] Canvas位置：{currentDialogueBubble.transform.position}");
        Debug.Log($"[{gameObject.name}] 当前字体设置 - 大小: {dialogueText.fontSize}, 颜色: {dialogueText.color}");
    }
    
    // 隐藏测试对话
    [ContextMenu("隐藏测试对话")]
    public void HideTestDialogue()
    {
        if (currentDialogueBubble != null)
        {
            currentDialogueBubble.SetActive(false);
            Debug.Log($"[{gameObject.name}] 测试对话已隐藏");
        }
    }

    #endregion
} 