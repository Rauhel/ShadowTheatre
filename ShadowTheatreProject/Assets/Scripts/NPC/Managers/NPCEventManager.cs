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

    // 添加一个字典来跟踪事件完成的手势类型
    private Dictionary<string, string> eventCompletionGestures = new Dictionary<string, string>();

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

            //Debug.Log($"[{gameObject.name}] 设置路径: {currentPathID}, 有 {CurrentPathEvents.Count} 个事件, 当前幕活跃: {activePathEvents.Count}");
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

    // 新增：设置动作是否可用
    public void SetActionActive(string eventID, string gestureType, int actionIndex, bool active)
    {
        foreach (var pathEvent in CurrentPathEvents)
        {
            if (pathEvent.eventID == eventID)
            {
                // 找到对应的事件
                if (string.IsNullOrEmpty(gestureType) && pathEvent.defaultResponse != null)
                {
                    // 设置默认响应的动作
                    if (actionIndex >= 0 && actionIndex < pathEvent.defaultResponse.actions.Count)
                    {
                        pathEvent.defaultResponse.actions[actionIndex].isActionActive = active;
                        Debug.Log($"[{gameObject.name}] 设置事件 {eventID} 默认响应的动作 {actionIndex} 为 {(active ? "可用" : "不可用")}");
                        return;
                    }
                }
                else
                {
                    // 查找对应手势类型的响应
                    foreach (var response in pathEvent.gestureResponses)
                    {
                        if (response.gestureType == gestureType)
                        {
                            // 设置该手势响应的动作
                            if (actionIndex >= 0 && actionIndex < response.actions.Count)
                            {
                                response.actions[actionIndex].isActionActive = active;
                                Debug.Log($"[{gameObject.name}] 设置事件 {eventID} 手势 {gestureType} 响应的动作 {actionIndex} 为 {(active ? "可用" : "不可用")}");
                                return;
                            }
                        }
                    }
                }
            }
        }

        Debug.LogWarning($"[{gameObject.name}] 找不到事件 {eventID} 或手势 {gestureType} 或动作索引 {actionIndex}");
    }

    // 新增：批量设置特定事件所有动作的可用状态
    public void SetAllActionsForEvent(string eventID, bool active)
    {
        foreach (var pathEvent in CurrentPathEvents)
        {
            if (pathEvent.eventID == eventID)
            {
                // 设置默认响应的所有动作
                if (pathEvent.defaultResponse != null && pathEvent.defaultResponse.actions != null)
                {
                    foreach (var action in pathEvent.defaultResponse.actions)
                    {
                        action.isActionActive = active;
                    }
                }

                // 设置所有手势响应的所有动作
                foreach (var response in pathEvent.gestureResponses)
                {
                    if (response.actions != null)
                    {
                        foreach (var action in response.actions)
                        {
                            action.isActionActive = active;
                        }
                    }
                }

                Debug.Log($"[{gameObject.name}] 已将事件 {eventID} 的所有动作设置为 {(active ? "可用" : "不可用")}");
                return;
            }
        }

        Debug.LogWarning($"[{gameObject.name}] 找不到事件 {eventID}");
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
        if (pathManager != null)
        {
            return pathManager.CurrentPathPointIndex;
        }
        return -1;
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

        // 完成当前事件
        CompleteCurrentEvent(gestureType);
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
            string eventID = currentEvent.eventID;

            // 记录事件已完成
            completedEventIDs.Add(eventID);

            // 记录事件完成时使用的手势类型
            eventCompletionGestures[eventID] = gestureType;

            // 触发事件完成事件
            OnEventCompleted?.Invoke(currentPathID, currentEvent, gestureType);

            Debug.Log($"[事件完成] NPC: {gameObject.name} | 事件: {eventID} | 手势: {(string.IsNullOrEmpty(gestureType) ? "无" : gestureType)}");

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

    // 添加一个方法来检查事件是否使用特定手势完成
    public bool WasEventCompletedWithGesture(string eventID, string gestureType)
    {
        if (string.IsNullOrEmpty(eventID) || !completedEventIDs.Contains(eventID))
            return false;

        // 检查事件是否使用指定手势完成
        if (eventCompletionGestures.TryGetValue(eventID, out string usedGesture))
        {
            return usedGesture == gestureType;
        }

        return false;
    }

    // 获取事件完成时使用的手势
    public string GetEventCompletionGesture(string eventID)
    {
        if (string.IsNullOrEmpty(eventID) || !completedEventIDs.Contains(eventID))
            return "";

        if (eventCompletionGestures.TryGetValue(eventID, out string gesture))
        {
            return gesture;
        }

        return "";
    }

    // 添加方法检查动作是否应该激活
    public bool ShouldActionBeActive(ActionData action, string eventID, string responseGestureType)
    {
        // 如果动作不是激活状态，直接返回false
        if (!action.isActionActive)
            return false;

        // 如果事件未完成，不应该激活
        if (!IsEventCompleted(eventID))
            return false;

        // 获取事件完成时使用的手势
        string completionGesture = GetEventCompletionGesture(eventID);

        // 1. 无手势响应的动作 - 只要事件完成就激活
        if (string.IsNullOrEmpty(responseGestureType))
            return true;

        // 2. 特定手势响应的动作 - 必须事件是用该手势完成的
        return responseGestureType == completionGesture;
    }

    // 获取所有已完成的事件
    public List<PathEvent> GetAllCompletedEvents()
    {
        List<PathEvent> result = new List<PathEvent>();

        foreach (var pathEvent in CurrentPathEvents)
        {
            if (completedEventIDs.Contains(pathEvent.eventID))
            {
                result.Add(pathEvent);
            }
        }

        return result;
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