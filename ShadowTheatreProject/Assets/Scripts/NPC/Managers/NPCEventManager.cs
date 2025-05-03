using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(NPCController))]
[RequireComponent(typeof(NPCPathManager))]
public class NPCEventManager : MonoBehaviour
{
    [Header("组件引用")]
    private NPCController controller;
    private NPCPathManager pathManager;
    private GestureEventHandler gestureHandler;

    [Header("当前事件状态")]
    private PathEvent currentEvent = null;
    private bool isProcessingEvent = false;
    private bool isDetectingGesture = false;
    private string currentPathID = "";
    private Transform currentPathPointsParent = null;

    // 添加一个字段存储已完成的事件ID
    private HashSet<string> completedEventIDs = new HashSet<string>();

    // 事件相关委托
    public delegate void EventCompletedHandler(string pathId, PathEvent completedEvent, string gestureType);
    public event EventCompletedHandler OnEventCompleted;

    // 重要公共属性
    public bool IsEventDetectable => isProcessingEvent && !isDetectingGesture;
    public PathEvent CurrentPathEvent => currentEvent;
    public Transform CurrentPathPointsParent => currentPathPointsParent;
    public List<PathEvent> CurrentPathEvents { get; private set; } = new List<PathEvent>();

    // 当前活跃事件（根据当前幕数过滤）
    private List<PathEvent> activePathEvents = new List<PathEvent>();

    // 游戏当前幕数
    [Header("事件控制")]
    [Range(1, 3)]
    [SerializeField] private int currentAct = 1;

    private void Awake()
    {
        controller = GetComponent<NPCController>();
        pathManager = GetComponent<NPCPathManager>();

        // 获取或添加手势处理器
        gestureHandler = GetComponent<GestureEventHandler>();
        if (gestureHandler == null)
        {
            gestureHandler = gameObject.AddComponent<GestureEventHandler>();
        }

        if (gestureHandler != null)
        {
            gestureHandler.OnGestureSuccess += OnGestureSuccess;
            gestureHandler.OnGestureFailure += OnGestureFailure;
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] 缺少 GestureEventHandler 组件，无法处理手势事件");
        }
    }

    private void OnEnable()
    {
        // 订阅状态改变事件，当幕数变化时重新加载事件
        SubscribeToGameEvents();
    }

    private void OnDisable()
    {
        // 取消订阅
        UnsubscribeFromGameEvents();
    }

    private void OnDestroy()
    {
        if (gestureHandler != null)
        {
            gestureHandler.OnGestureSuccess -= OnGestureSuccess;
            gestureHandler.OnGestureFailure -= OnGestureFailure;
        }
    }

    private void Update()
    {
        // 只有当NPC有路径且正在移动时才需要检测
        if (pathManager == null || !pathManager.IsMoving)
            return;

        // 检查是否在处理事件，且是否应该结束
        if (isProcessingEvent && currentEvent != null)
        {
            // 已经到达结束点，结束事件
            if (HasReachedEndPoint())
            {
                CompleteCurrentEvent();
            }
        }
        else
        {
            // 检查是否有新事件需要触发
            CheckForEvents();
        }
    }

    public void SetCurrentPath(string pathId, Transform pathPointsParent)
    {
        currentPathID = pathId;
        currentPathPointsParent = pathPointsParent;

        // 重新加载此路径的所有事件
        LoadPathEvents();
    }

    private void LoadPathEvents()
    {
        CurrentPathEvents.Clear();
        activePathEvents.Clear();

        if (controller == null || controller.Data == null || string.IsNullOrEmpty(currentPathID))
            return;

        // 找到当前路径配置
        PathConfig pathConfig = controller.Data.paths.Find(p => p.pathID == currentPathID);

        if (pathConfig != null && pathConfig.events != null)
        {
            // 加载所有事件
            CurrentPathEvents = new List<PathEvent>(pathConfig.events);

            // 筛选当前幕中可用的事件
            RefreshActiveEvents();

            Debug.Log($"[{gameObject.name}] 设置路径: {currentPathID}, 有 {CurrentPathEvents.Count} 个事件, 当前幕活跃: {activePathEvents.Count}");
        }
    }

    // 筛选当前幕中可用的事件
    private void RefreshActiveEvents()
    {
        activePathEvents.Clear();

        foreach (var evt in CurrentPathEvents)
        {
            if (IsEventEnabledInCurrentAct(evt))
            {
                activePathEvents.Add(evt);
            }
        }
    }

    // 检查事件在当前幕是否启用
    private bool IsEventEnabledInCurrentAct(PathEvent pathEvent)
    {
        switch (currentAct)
        {
            case 1: return pathEvent.enabledInAct1;
            case 2: return pathEvent.enabledInAct2;
            case 3: return pathEvent.enabledInAct3;
            default: return false;
        }
    }

    // 设置当前幕数
    public void SetCurrentAct(int act)
    {
        if (act >= 1 && act <= 3 && act != currentAct)
        {
            currentAct = act;
            RefreshActiveEvents();
        }
    }

    private void OnGameStateChanged()
    {
        // 当游戏状态改变时，重新加载路径事件
        if (!string.IsNullOrEmpty(currentPathID))
        {
            // 获取当前游戏状态
            GameState.State currentGameState = GameState.Instance.GetCurrentState();

            // 根据当前游戏状态确定幕数
            switch (currentGameState)
            {
                case GameState.State.Act1:
                    SetCurrentAct(1);
                    break;
                case GameState.State.Act2:
                    SetCurrentAct(2);
                    break;
                case GameState.State.Act3:
                    SetCurrentAct(3);
                    break;
            }
        }
    }

    private void CheckForEvents()
    {
        if (activePathEvents.Count == 0 || currentPathPointsParent == null)
            return;

        // 获取当前最近的路径点索引
        int currentPointIndex = GetCurrentPathPointIndex();

        // 检查所有活跃事件
        foreach (var pathEvent in activePathEvents)
        {
            // 只有未激活的事件才需要检查
            if (pathEvent == currentEvent)
                continue;

            // 检查事件是否已完成
            if (completedEventIDs.Contains(pathEvent.eventID))
                continue;

            // 检查是否到达事件起始点
            if (IsPathPointInEventRange(currentPointIndex, pathEvent))
            {
                // 找到事件，激活它
                TriggerEvent(pathEvent);

                break;
            }
        }
    }

    // 判断当前点是否在事件范围内
    public bool IsPathPointInEventRange(int pointIndex, PathEvent pathEvent)
    {
        int startIndex = pathEvent.startPointIndex;
        int endIndex = pathEvent.endPointIndex;

        // 处理可能的方向问题（起始点在后，结束点在前）
        int minIndex = Mathf.Min(startIndex, endIndex);
        int maxIndex = Mathf.Max(startIndex, endIndex);

        // 判断当前点是否在范围内
        return pointIndex >= minIndex && pointIndex <= maxIndex;
    }

    // 获取NPC在当前路径上的点索引
    public int GetCurrentPathPointIndex()
    {
        if (currentPathPointsParent == null || currentPathPointsParent.childCount == 0)
            return -1;

        int closestIndex = 0;
        float closestDistance = float.MaxValue;

        // 找到最近的路径点
        for (int i = 0; i < currentPathPointsParent.childCount; i++)
        {
            Transform pathPoint = currentPathPointsParent.GetChild(i);
            float distance = Vector3.Distance(transform.position, pathPoint.position);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        return closestIndex;
    }

    // 触发事件
    private void TriggerEvent(PathEvent pathEvent)
    {
        currentEvent = pathEvent;
        isProcessingEvent = true;
        isDetectingGesture = true;

        // 记录事件开始位置
        RecordEventStartPosition();

        // 告诉手势处理器开始检测
        if (gestureHandler != null)
        {
            gestureHandler.StartGestureRecognition(pathEvent);
        }

        Debug.Log($"[事件触发] NPC: {gameObject.name} | 事件: {pathEvent.eventID}");
    }

    // 获取事件结束点
    private Transform GetEventEndPoint()
    {
        if (currentEvent == null || currentPathPointsParent == null)
            return null;

        if (currentEvent.endPointIndex < 0 || currentEvent.endPointIndex >= currentPathPointsParent.childCount)
            return null;

        return currentPathPointsParent.GetChild(currentEvent.endPointIndex);
    }

    // 检查是否到达事件结束点
    private bool HasReachedEndPoint()
    {
        Transform endPoint = GetEventEndPoint();
        if (endPoint == null)
            return false;

        float distanceToEnd = Vector3.Distance(transform.position, endPoint.position);
        return distanceToEnd <= 0.5f; // 使用合适的阈值
    }

    // 手势成功回调
    private void OnGestureSuccess(string gestureType)
    {
        if (currentEvent == null || !isProcessingEvent || !isDetectingGesture)
            return;

        // 找到对应的手势响应
        GestureResponse matchedResponse = null;

        foreach (var response in currentEvent.gestureResponses)
        {
            if (response.gestureType == gestureType)
            {
                matchedResponse = response;
                break;
            }
        }

        // 如果没有匹配的响应，使用默认响应
        if (matchedResponse == null)
        {
            matchedResponse = currentEvent.defaultResponse;
        }

        // 应用分数效果
        if (controller != null && matchedResponse != null)
        {
            controller.AdjustScore(matchedResponse.scoreEffect);
        }

        // 播放动作序列
        NPCDialogueManager dialogueManager = GetComponent<NPCDialogueManager>();
        if (dialogueManager != null && matchedResponse != null && matchedResponse.actions != null && matchedResponse.actions.Count > 0)
        {
            // 创建一个新的 List<ActionData> 来存储转换后的动作
            List<ActionData> actionDataList = new List<ActionData>();

            // 将所有 ActionData 添加到新列表中
            foreach (var action in matchedResponse.actions)
            {
                actionDataList.Add(action);
            }

            // 直接使用动作序列中的对话
            StartCoroutine(PlayDialogueActionSequence(actionDataList));
        }

        // 完成当前事件
        CompleteCurrentEvent(gestureType);
    }

    // 添加播放对话序列的协程
    private IEnumerator PlayDialogueActionSequence(List<ActionData> actions)
    {
        NPCDialogueManager dialogueManager = GetComponent<NPCDialogueManager>();
        if (dialogueManager == null)
            yield break;

        foreach (var action in actions)
        {
            // 等待延迟
            if (action.delay > 0)
            {
                yield return new WaitForSeconds(action.delay);
            }

            // 显示对话
            if (!string.IsNullOrEmpty(action.dialogueText))
            {
                dialogueManager.DisplayDialogue(
                    action.dialogueText,
                    action.displayDuration,
                    action.voiceClip,
                    action.overridePrevious
                );
            }

            // 等待此动作完成
            yield return new WaitForSeconds(action.displayDuration);
        }
    }

    // 手势失败回调
    private void OnGestureFailure()
    {
        if (currentEvent == null || !isProcessingEvent)
            return;

        // 使用默认响应
        if (controller != null && currentEvent.defaultResponse != null)
        {
            controller.AdjustScore(currentEvent.defaultResponse.scoreEffect);
        }

        // 完成当前事件，无手势类型
        CompleteCurrentEvent();
    }

    // 完成当前事件
    private void CompleteCurrentEvent(string gestureType = "")
    {
        if (currentEvent != null)
        {
            // 记录事件已完成
            completedEventIDs.Add(currentEvent.eventID);

            // 触发事件完成事件
            OnEventCompleted?.Invoke(currentPathID, currentEvent, gestureType);

            Debug.Log($"[事件完成] NPC: {gameObject.name} | 事件: {currentEvent.eventID} | 手势: {(string.IsNullOrEmpty(gestureType) ? "无" : gestureType)}");

            // 重置状态
            currentEvent = null;
            isProcessingEvent = false;
            isDetectingGesture = false;

            // 通知手势处理器
            if (gestureHandler != null)
            {
                gestureHandler.CancelGestureRecognition();
            }
        }
    }

    // 添加检查事件是否完成的方法
    public bool IsEventCompleted(string eventID)
    {
        return !string.IsNullOrEmpty(eventID) && completedEventIDs.Contains(eventID);
    }

    // 添加根据路径点查找相关事件的方法
    public PathEvent FindCompletedEventForPathPoint(int pathPointIndex)
    {
        if (CurrentPathEvents == null || CurrentPathEvents.Count == 0)
            return null;

        foreach (var evt in CurrentPathEvents)
        {
            // 检查事件是否已完成，以及路径点是否在事件范围内
            if (completedEventIDs.Contains(evt.eventID) &&
                IsPathPointInEventRange(pathPointIndex, evt))
            {
                return evt;
            }
        }

        return null;
    }

    // 用于从编辑器测试触发事件
    public void TestTriggerEvent(string pathId, PathEvent pathEvent)
    {
        if (pathEvent == null)
            return;

        Debug.Log($"[事件测试] 触发事件: {pathEvent.eventID}");

        // 临时设置路径ID
        currentPathID = pathId;

        // 模拟触发事件
        TriggerEvent(pathEvent);
    }

    // 获取NPC数据
    public NPCData GetNPCData()
    {
        if (controller != null)
        {
            return controller.Data;
        }
        return null;
    }

    // 计算从事件开始以来NPC移动的距离
    private Vector3 eventStartPosition;
    private bool hasRecordedStartPosition = false;

    public void RecordEventStartPosition()
    {
        eventStartPosition = transform.position;
        hasRecordedStartPosition = true;
    }

    public float GetDistanceTraveledSinceEventStart()
    {
        // 如果未记录起始位置，返回0
        if (!hasRecordedStartPosition)
            return 0f;

        return Vector3.Distance(transform.position, eventStartPosition);
    }

    // 添加通过ID触发事件的方法
    public void TriggerEventByID(string eventID)
    {
        if (string.IsNullOrEmpty(eventID) || isProcessingEvent)
            return;

        // 在当前活跃事件中寻找对应ID的事件
        PathEvent targetEvent = activePathEvents.Find(e => e.eventID == eventID);

        // 如果在活跃事件中找到了
        if (targetEvent != null)
        {
            Debug.Log($"[{gameObject.name}] 通过ID触发事件: {eventID}");
            TriggerEvent(targetEvent);
        }
        else
        {
            // 尝试在所有当前路径事件中查找
            targetEvent = CurrentPathEvents.Find(e => e.eventID == eventID);

            if (targetEvent != null)
            {
                // 找到了，但该事件在当前幕不可用
                Debug.LogWarning($"[{gameObject.name}] 事件 {eventID} 存在但在当前幕 {currentAct} 中不可用");
            }
            else
            {
                // 完全没找到对应事件，遍历所有路径查找
                if (controller?.Data != null)
                {
                    foreach (var path in controller.Data.paths)
                    {
                        // 遍历该路径的所有事件
                        foreach (var pathEvent in path.events)
                        {
                            if (pathEvent.eventID == eventID)
                            {
                                // 找到匹配的事件ID，触发它
                                TestTriggerEvent(path.pathID, pathEvent);
                                return;
                            }
                        }
                    }
                }

                // 完全没找到对应事件
                Debug.LogWarning($"[{gameObject.name}] 找不到ID为 {eventID} 的事件");
            }
        }
    }

    // 订阅游戏状态事件
    private void SubscribeToGameEvents()
    {
        if (EventCenter.Instance != null)
        {
            // 订阅状态改变事件
            EventCenter.Instance.Subscribe(GameState.EventNames.STATE_CHANGED, OnGameStateChanged);

            // 根据当前状态获取幕数 - 这是全局状态监听
            EventCenter.Instance.Subscribe(GameState.EventNames.STATE_ENTERED + GameState.State.Act1.ToString(), () => SetCurrentAct(1));
            EventCenter.Instance.Subscribe(GameState.EventNames.STATE_ENTERED + GameState.State.Act2.ToString(), () => SetCurrentAct(2));
            EventCenter.Instance.Subscribe(GameState.EventNames.STATE_ENTERED + GameState.State.Act3.ToString(), () => SetCurrentAct(3));
        }
    }

    // 取消订阅游戏状态事件
    private void UnsubscribeFromGameEvents()
    {
        if (EventCenter.Instance != null)
        {
            EventCenter.Instance.Unsubscribe(GameState.EventNames.STATE_CHANGED, OnGameStateChanged);

            EventCenter.Instance.Unsubscribe(GameState.EventNames.STATE_ENTERED + GameState.State.Act1.ToString(), () => SetCurrentAct(1));
            EventCenter.Instance.Unsubscribe(GameState.EventNames.STATE_ENTERED + GameState.State.Act2.ToString(), () => SetCurrentAct(2));
            EventCenter.Instance.Unsubscribe(GameState.EventNames.STATE_ENTERED + GameState.State.Act3.ToString(), () => SetCurrentAct(3));
        }
    }
}