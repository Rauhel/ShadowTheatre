// 文件：NPCEventManager.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(NPCController))]
[RequireComponent(typeof(NPCPathManager))]
public class NPCEventManager : MonoBehaviour
{
    // 内部状态
    private NPCController controller;
    private NPCPathManager pathManager;
    private bool isProcessingEvent = false;
    private PathEvent currentEvent = null;

    // 事件持久性管理
    private HashSet<string> triggeredEventIDs = new HashSet<string>();

    // 当前路径的事件
    private List<PathEvent> currentPathEvents = new List<PathEvent>();
    private List<PathEvent> filteredPathEvents = new List<PathEvent>(); // 按幕数和已触发状态过滤后的事件
    private Transform currentPathPointsParent;

    private GestureRecognitionManager gestureRecognitionManager;

    [Header("事件控制")]
    [Tooltip("当前游戏幕数 (1-3)")]
    [Range(1, 3)]
    [SerializeField] private int currentAct = 1;

    [Tooltip("事件是否在触发后在当前游戏会话中保持禁用")]
    public bool disableEventsAfterTrigger = true;

    [HideInInspector]
    public bool isEventDetectable = false; // 当前是否处于事件可检测阶段
    [HideInInspector]
    public PathEvent currentPathEvent = null; // 当前可检测的事件

    // 添加以下公共属性以访问私有变量
    /// <summary>
    /// 当前路径点的父对象
    /// </summary>
    [HideInInspector]
    public Transform CurrentPathPointsParent => currentPathPointsParent;

    /// <summary>
    /// 当前路径上的所有事件
    /// </summary>
    [HideInInspector]
    public List<PathEvent> CurrentPathEvents => currentPathEvents;

    // 获取当前幕数
    public int CurrentAct => currentAct;

    // 重置已触发事件
    public void ResetTriggeredEvents()
    {
        triggeredEventIDs.Clear();
        RefreshFilteredEvents();
    }

    // 设置当前游戏幕数
    private void SetCurrentAct(int act)
    {
        if (act >= 1 && act <= 3 && act != currentAct)
        {
            currentAct = act;
            Debug.Log($"[{gameObject.name}] 切换至第 {act} 幕");
            RefreshFilteredEvents();
        }
    }

    void Awake()
    {
        controller = GetComponent<NPCController>();
        pathManager = GetComponent<NPCPathManager>();
    }

    void Start()
    {
        // 获取手势识别管理器
        gestureRecognitionManager = FindObjectOfType<GestureRecognitionManager>();
        if (gestureRecognitionManager != null)
        {
            // 修改为匹配正确的委托签名
            gestureRecognitionManager.OnGestureRecognized += OnGestureRecognized;
            gestureRecognitionManager.OnGestureTimedOut += OnGestureTimedOut;
        }
        else
        {
            Debug.LogWarning("找不到GestureRecognitionManager实例，手势检测将不可用");
        }

        // 订阅游戏状态变化事件
        SubscribeToGameStateEvents();
    }

    // 订阅游戏状态变化事件
    private void SubscribeToGameStateEvents()
    {
        if (EventCenter.Instance != null)
        {
            // 注册各幕开始的事件
            EventCenter.Instance.Subscribe(GameState.EventNames.STATE_ENTERED + GameState.State.Act1.ToString(), () => SetCurrentAct(1));
            EventCenter.Instance.Subscribe(GameState.EventNames.STATE_ENTERED + GameState.State.Act2.ToString(), () => SetCurrentAct(2));
            EventCenter.Instance.Subscribe(GameState.EventNames.STATE_ENTERED + GameState.State.Act3.ToString(), () => SetCurrentAct(3));

            // 注册游戏重置事件
            EventCenter.Instance.Subscribe("Game_Reset", ResetTriggeredEvents);

            // 检查当前游戏状态并初始化
            InitializeCurrentActFromGameState();
        }
        else
        {
            Debug.LogWarning("找不到EventCenter实例，无法订阅游戏状态事件");
        }
    }

    // 初始化当前游戏幕数
    private void InitializeCurrentActFromGameState()
    {
        if (GameState.Instance != null)
        {
            // 根据当前游戏状态设置幕数
            var state = GameState.Instance.GetCurrentState();
            if (state == GameState.State.Act1)
                SetCurrentAct(1);
            else if (state == GameState.State.Act2)
                SetCurrentAct(2);
            else if (state == GameState.State.Act3)
                SetCurrentAct(3);
        }
    }

    void OnDestroy()
    {
        // 取消手势识别回调
        if (gestureRecognitionManager != null)
        {
            // 修改为匹配正确的委托签名
            gestureRecognitionManager.OnGestureRecognized -= OnGestureRecognized;
            gestureRecognitionManager.OnGestureTimedOut -= OnGestureTimedOut;
        }

        // 取消事件订阅
        if (EventCenter.Instance != null)
        {
            EventCenter.Instance.Unsubscribe(GameState.EventNames.STATE_ENTERED + GameState.State.Act1.ToString(), () => SetCurrentAct(1));
            EventCenter.Instance.Unsubscribe(GameState.EventNames.STATE_ENTERED + GameState.State.Act2.ToString(), () => SetCurrentAct(2));
            EventCenter.Instance.Unsubscribe(GameState.EventNames.STATE_ENTERED + GameState.State.Act3.ToString(), () => SetCurrentAct(3));
            EventCenter.Instance.Unsubscribe("Game_Reset", ResetTriggeredEvents);
        }
    }

    void Update()
    {
        if (isProcessingEvent || controller.Data == null)
            return;

        // 检查事件触发
        CheckEventTriggers();
    }

    // 设置当前路径
    public void SetCurrentPath(string pathID, Transform pathPointsParent)
    {
        currentPathPointsParent = pathPointsParent;
        currentPathEvents.Clear();
        filteredPathEvents.Clear();

        // 查找对应路径配置
        PathConfig pathConfig = controller.Data.FindPathById(pathID);
        if (pathConfig != null)
        {
            currentPathEvents = pathConfig.events;
            Debug.Log($"[{gameObject.name}] 设置当前路径: {pathID}, 包含 {currentPathEvents.Count} 个事件");
            RefreshFilteredEvents();
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] 未找到路径配置: {pathID}");
        }
    }

    // 刷新经过筛选的事件列表
    private void RefreshFilteredEvents()
    {
        filteredPathEvents.Clear();

        foreach (var pathEvent in currentPathEvents)
        {
            // 1. 检查是否已触发且配置为不重复触发
            if (disableEventsAfterTrigger && triggeredEventIDs.Contains(pathEvent.eventID))
                continue;

            // 2. 检查该事件在当前幕是否启用
            if (!IsEventEnabledInCurrentAct(pathEvent))
                continue;

            // 通过所有过滤条件，添加到可触发列表
            filteredPathEvents.Add(pathEvent);
        }

        Debug.Log($"[{gameObject.name}] 过滤后可触发事件: {filteredPathEvents.Count}/{currentPathEvents.Count}");
    }

    // 检查事件在当前幕是否启用
    private bool IsEventEnabledInCurrentAct(PathEvent pathEvent)
    {
        switch (currentAct)
        {
            case 1:
                return pathEvent.enabledInAct1;
            case 2:
                return pathEvent.enabledInAct2;
            case 3:
                return pathEvent.enabledInAct3;
            default:
                return true; // 默认启用
        }
    }

    private void CheckEventTriggers()
    {
        if (filteredPathEvents.Count == 0 || currentPathPointsParent == null)
        {
            isEventDetectable = false;
            currentPathEvent = null;
            return;
        }

        isEventDetectable = false;
        currentPathEvent = null;

        foreach (var pathEvent in filteredPathEvents)
        {
            if (pathEvent.pathPointIndex < 0 || pathEvent.pathPointIndex >= currentPathPointsParent.childCount)
                continue;

            // 获取事件触发点
            Transform triggerPoint = currentPathPointsParent.GetChild(pathEvent.pathPointIndex);

            // 检查是否在触发区域内
            float distance = Vector3.Distance(transform.position, triggerPoint.position);
            bool isInTriggerArea = distance <= pathEvent.triggerRadius;

            // 当NPC接近事件触发区域时标记为可检测（距离在触发半径的1.5倍内）
            if (distance <= pathEvent.triggerRadius * 1.5f)
            {
                isEventDetectable = true;
                currentPathEvent = pathEvent;
            }

            if (isInTriggerArea)
            {
                TriggerEvent(pathEvent);
                break; // 只处理一个事件
            }
        }
    }

    private void TriggerEvent(PathEvent pathEvent)
    {
        if (isProcessingEvent)
            return;

        isProcessingEvent = true;
        currentEvent = pathEvent;

        // 添加到已触发事件列表
        if (disableEventsAfterTrigger)
        {
            triggeredEventIDs.Add(pathEvent.eventID);
            Debug.Log($"[{gameObject.name}] 事件 {pathEvent.eventID} 已触发，添加到已触发列表");
        }

        // 使用手势识别系统
        if (pathEvent.gestureResponses.Count > 0 && gestureRecognitionManager != null)
        {
            StartGestureRecognitionForEvent(pathEvent);
        }
        else
        {
            // 没有手势响应或手势系统不可用，使用默认响应
            ExecuteGestureResponse(pathEvent.defaultResponse);
        }
    }

    private void CompleteCurrentEvent()
    {
        // 事件处理完成
        isProcessingEvent = false;
        currentEvent = null;

        // 刷新过滤后的事件列表
        RefreshFilteredEvents();
    }

    // 直接触发事件的公共方法（例如从其他系统触发）
    public void TriggerEventByID(string eventID)
    {
        if (isProcessingEvent)
            return;

        // 在过滤后的事件中查找
        PathEvent targetEvent = filteredPathEvents.Find(e => e.eventID == eventID);
        if (targetEvent != null)
        {
            TriggerEvent(targetEvent);
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] 找不到ID为 {eventID} 的事件，或该事件在当前幕中不可用");
        }
    }

    // 启动手势识别
    private void StartGestureRecognitionForEvent(PathEvent pathEvent)
    {
        if (gestureRecognitionManager == null)
        {
            Debug.LogError("无法启动手势识别：手势识别管理器不可用");
            // 使用默认响应作为后备
            ExecuteGestureResponse(pathEvent.defaultResponse);
            return;
        }

        // 收集此事件可接受的所有手势类型
        List<string> acceptableGestures = new List<string>();
        foreach (var response in pathEvent.gestureResponses)
        {
            if (!string.IsNullOrEmpty(response.gestureType) && !acceptableGestures.Contains(response.gestureType))
            {
                acceptableGestures.Add(response.gestureType);
            }
        }

        // 检查是否有可接受的手势
        if (acceptableGestures.Count > 0)
        {
            // 设置手势识别管理器的参数
            gestureRecognitionManager.fullRecognitionTime = pathEvent.gestureHoldTime;
            gestureRecognitionManager.timeLimit = pathEvent.gestureTimeLimit;

            // 启动第一个可接受的手势的识别
            // 注意：GestureRecognitionManager目前只支持一次识别一种手势
            gestureRecognitionManager.StartGestureRecognition(acceptableGestures[0]);

            // 如果有多个手势，记录在调试信息中
            if (acceptableGestures.Count > 1)
            {
                gestureRecognitionManager.SetDebugInfo("可接受手势", string.Join(", ", acceptableGestures));
            }

            Debug.Log($"[{gameObject.name}] 开始手势识别，主要手势: {acceptableGestures[0]}, 总计: {acceptableGestures.Count} 个");
        }
        else
        {
            // 没有可接受的手势，使用默认响应
            Debug.LogWarning("没有设置可接受的手势，使用默认响应");
            ExecuteGestureResponse(pathEvent.defaultResponse);
        }
    }

    // 处理识别到的手势 - 直接使用 OnGestureRecognized 回调的签名
    private void OnGestureRecognized(string gestureType)
    {
        if (currentEvent == null || !isProcessingEvent)
            return;

        Debug.Log($"[{gameObject.name}] 识别到手势: {gestureType}");

        // 查找匹配的手势响应
        GestureResponse matchedResponse = currentEvent.gestureResponses.Find(r =>
            r.gestureType == gestureType);

        if (matchedResponse != null)
        {
            // 执行匹配的手势响应
            ExecuteGestureResponse(matchedResponse);
        }
        else
        {
            // 没有匹配，使用默认响应
            Debug.Log($"[{gameObject.name}] 没有匹配的手势响应，使用默认响应");
            ExecuteGestureResponse(currentEvent.defaultResponse);
        }
    }

    // 保持 OnGestureTimedOut 方法，直接转发给 HandleGestureTimedOut
    private void OnGestureTimedOut()
    {
        HandleGestureTimedOut();
    }

    // 处理手势超时
    private void HandleGestureTimedOut()
    {
        if (currentEvent == null || !isProcessingEvent)
            return;

        Debug.Log($"[{gameObject.name}] 手势识别超时，使用默认响应");

        // 使用默认响应
        ExecuteGestureResponse(currentEvent.defaultResponse);
    }

    // 执行手势响应
    private void ExecuteGestureResponse(GestureResponse response)
    {
        if (currentEvent == null || !isProcessingEvent)
            return;

        Debug.Log($"[{gameObject.name}] 执行手势响应: {(string.IsNullOrEmpty(response.gestureType) ? "默认" : response.gestureType)}");

        // 1. 更新NPC分数
        if (response.scoreEffect != 0)
        {
            controller.UpdateScore(response.scoreEffect);
            Debug.Log($"[{gameObject.name}] 分数更新: {(response.scoreEffect >= 0 ? "+" : "")}{response.scoreEffect}");
        }

        // 2. 播放动画 (暂时注释，等待实现)
        /*
        if (!string.IsNullOrEmpty(response.animationName))
        {
            controller.PlayAnimation(response.animationName);
            Debug.Log($"[{gameObject.name}] 播放动画: {response.animationName}");
        }
        */

        // 3. 显示对话文本 (暂时注释，等待实现)
        /*
        if (!string.IsNullOrEmpty(response.dialogueText))
        {
            // 如果有对话系统，可以在这里调用
            Debug.Log($"[{gameObject.name}] 对话: {response.dialogueText}");

            // 临时：直接显示对话
            // 可以替换为适当的对话系统调用
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.ShowDialogue(controller.Data.npcName, response.dialogueText);
            }
        }
        */

        // 4. 完成事件处理（延迟）
        if (response.completionDelay > 0)
        {
            StartCoroutine(DelayedEventCompletion(response.completionDelay));
        }
        else
        {
            CompleteCurrentEvent();
        }
    }

    // 延迟完成事件
    private IEnumerator DelayedEventCompletion(float delay)
    {
        yield return new WaitForSeconds(delay);
        CompleteCurrentEvent();
    }
}