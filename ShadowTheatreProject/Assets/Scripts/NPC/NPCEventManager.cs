using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(NPCController))]
[RequireComponent(typeof(NPCPathManager))]
public class NPCEventManager : MonoBehaviour
{
    // 核心组件引用
    private NPCController controller;
    private NPCPathManager pathManager;
    private GestureEventHandler gestureHandler;

    // 当前状态
    private bool isProcessingEvent = false;
    private PathEvent currentEvent = null;
    private Transform currentPathPointsParent;

    // 当前路径上的事件
    private List<PathEvent> currentPathEvents = new List<PathEvent>();
    private List<PathEvent> activePathEvents = new List<PathEvent>(); // 当前幕中可用的事件

    // 事件检测状态
    private bool isEventDetectable = false;
    private PathEvent currentPathEvent = null;

    // 交互范围指示器
    private GameObject interactionRangeVisual;

    // 游戏当前幕数
    [Header("事件控制")]
    [Range(1, 3)]
    [SerializeField] private int currentAct = 1;

    // 公共属性
    public bool IsEventActive => isProcessingEvent;
    public PathEvent CurrentEvent => currentEvent;
    public List<PathEvent> CurrentPathEvents => currentPathEvents;
    public int CurrentAct => currentAct;
    public Transform CurrentPathPointsParent => currentPathPointsParent;
    public bool IsEventDetectable => isEventDetectable;
    public PathEvent CurrentPathEvent => currentPathEvent;

    private List<GameObject> pathSegmentMarkers = new List<GameObject>();

    void Awake()
    {
        controller = GetComponent<NPCController>();
        pathManager = GetComponent<NPCPathManager>();

        // 获取或添加手势处理器
        gestureHandler = GetComponent<GestureEventHandler>();
        if (gestureHandler == null)
        {
            gestureHandler = gameObject.AddComponent<GestureEventHandler>();
        }
    }

    void Start()
    {
        // 创建交互范围指示器
        CreateInteractionRangeIndicator();

        // 订阅游戏状态事件
        SubscribeToGameEvents();

        // 订阅手势处理器事件
        gestureHandler.OnGestureSuccess += OnGestureSuccessCallback;
        gestureHandler.OnGestureFailure += OnGestureFailureCallback;
    }

    void OnDestroy()
    {
        // 清理交互范围指示器
        if (interactionRangeVisual != null)
        {
            Destroy(interactionRangeVisual);
        }

        // 取消订阅事件
        UnsubscribeFromGameEvents();

        // 取消订阅手势处理器事件
        if (gestureHandler != null)
        {
            gestureHandler.OnGestureSuccess -= OnGestureSuccessCallback;
            gestureHandler.OnGestureFailure -= OnGestureFailureCallback;
        }
    }

    void Update()
    {
        if (controller == null || controller.Data == null || currentPathPointsParent == null)
            return;

        // 恢复事件标志，以便可以重新检查
        isEventDetectable = false;
        currentPathEvent = null;

        // 检查是否有事件在范围内
        CheckEventsInRange();
    }

    // 修改 CheckEventsInRange 方法
    private void CheckEventsInRange()
    {
        if (activePathEvents.Count == 0 || currentPathPointsParent == null)
            return;

        // 找到NPC当前在路径上的位置（最近的路径点索引）
        int currentPathIndex = GetClosestPathPointIndex();

        foreach (var pathEvent in activePathEvents)
        {
            // 检查事件路径段是否有效
            if (pathEvent.startPointIndex < 0 || pathEvent.startPointIndex >= currentPathPointsParent.childCount ||
                pathEvent.endPointIndex < 0 || pathEvent.endPointIndex >= currentPathPointsParent.childCount)
                continue;

            // 事件起始点和结束点
            Transform startPoint = currentPathPointsParent.GetChild(pathEvent.startPointIndex);
            Transform endPoint = currentPathPointsParent.GetChild(pathEvent.endPointIndex);

            // 检查NPC是否在事件路径段上
            bool isInEventSegment = IsIndexInRange(currentPathIndex, pathEvent.startPointIndex, pathEvent.endPointIndex);

            // 检查NPC是否到达事件结束点
            bool reachedEndPoint = false;
            if (pathEvent == currentEvent)
            {
                float distanceToEnd = Vector3.Distance(transform.position, endPoint.position);
                reachedEndPoint = distanceToEnd < 0.5f; // 使用适当的阈值
            }

            // 显示路径段（如果启用）
            if (pathEvent.showInteractionRange)
            {
                if ((pathEvent == currentEvent) || (isInEventSegment && !isProcessingEvent))
                {
                    ShowPathSegment(pathEvent, pathEvent == currentEvent);
                }
            }

            // 如果NPC到达事件结束点，结束事件
            if (reachedEndPoint && isProcessingEvent && pathEvent == currentEvent)
            {
                if (gestureHandler != null)
                {
                    gestureHandler.CancelGestureRecognition();
                }
                CompleteCurrentEvent();
                return;
            }

            // 如果NPC在事件段内且事件未处理，则触发事件
            if (isInEventSegment && !isProcessingEvent)
            {
                TriggerEvent(pathEvent);
                break; // 只触发一个事件
            }
        }
    }

    // 获取当前NPC在路径上最接近的点索引
    private int GetClosestPathPointIndex()
    {
        if (currentPathPointsParent == null || currentPathPointsParent.childCount == 0)
            return -1;

        int closestIndex = 0;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < currentPathPointsParent.childCount; i++)
        {
            Transform point = currentPathPointsParent.GetChild(i);
            float distance = Vector3.Distance(transform.position, point.position);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        return closestIndex;
    }

    // 检查索引是否在范围内（考虑路径方向）
    private bool IsIndexInRange(int currentIndex, int startIndex, int endIndex)
    {
        // 处理正向路径段
        if (startIndex <= endIndex)
        {
            return currentIndex >= startIndex && currentIndex <= endIndex;
        }
        // 处理反向路径段（如果有循环路径）
        else
        {
            return currentIndex >= startIndex || currentIndex <= endIndex;
        }
    }

    // 设置当前路径
    public void SetCurrentPath(string pathID, Transform pathPointsParent)
    {
        // 设置新的路径点父物体
        currentPathPointsParent = pathPointsParent;

        // 清空当前事件列表
        currentPathEvents.Clear();
        activePathEvents.Clear();

        // 隐藏交互范围指示器
        if (interactionRangeVisual != null)
        {
            interactionRangeVisual.SetActive(false);
        }

        // 查找并加载新路径的事件
        PathConfig pathConfig = controller.Data.FindPathById(pathID);
        if (pathConfig != null)
        {
            // 加载路径上的所有事件
            currentPathEvents = new List<PathEvent>(pathConfig.events);

            // 筛选当前幕中可用的事件
            RefreshActiveEvents();

            Debug.Log($"[{gameObject.name}] 设置路径: {pathID}, 有 {currentPathEvents.Count} 个事件, 当前幕活跃: {activePathEvents.Count}");
        }
    }

    // 筛选当前幕中可用的事件
    private void RefreshActiveEvents()
    {
        activePathEvents.Clear();

        foreach (var evt in currentPathEvents)
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
            default: return true;
        }
    }

    // 设置当前幕数
    public void SetCurrentAct(int act)
    {
        if (act >= 1 && act <= 3 && act != currentAct)
        {
            currentAct = act;
            RefreshActiveEvents();

            // 隐藏交互范围指示器，因为新幕可能有不同的活跃事件
            if (interactionRangeVisual != null)
            {
                interactionRangeVisual.SetActive(false);
            }
        }
    }

    // 触发事件
    private void TriggerEvent(PathEvent pathEvent)
    {
        if (isProcessingEvent)
            return;

        isProcessingEvent = true;
        currentEvent = pathEvent;

        // 启动手势识别
        if (pathEvent.gestureResponses.Count > 0 && gestureHandler != null)
        {
            gestureHandler.StartGestureRecognition(pathEvent);
        }
        else
        {
            // 没有手势响应，使用默认响应
            ExecuteResponse(pathEvent.defaultResponse);
        }
    }

    // 通过ID触发特定事件
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
            targetEvent = currentPathEvents.Find(e => e.eventID == eventID);

            if (targetEvent != null)
            {
                // 找到了，但该事件在当前幕不可用
                Debug.LogWarning($"[{gameObject.name}] 事件 {eventID} 存在但在当前幕 {currentAct} 中不可用");
            }
            else
            {
                // 完全没找到对应事件
                Debug.LogWarning($"[{gameObject.name}] 找不到ID为 {eventID} 的事件");
            }
        }
    }

    // 修改事件完成方法
    private void CompleteCurrentEvent()
    {
        // 隐藏路径段显示
        HidePathSegment();

        isProcessingEvent = false;
        currentEvent = null;
    }

    // 创建交互范围指示器
    private void CreateInteractionRangeIndicator()
    {
        interactionRangeVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        interactionRangeVisual.name = "InteractionRange";
        interactionRangeVisual.transform.SetParent(transform);
        interactionRangeVisual.transform.localPosition = Vector3.zero;

        // 设置半透明材质
        Renderer renderer = interactionRangeVisual.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Transparent/Diffuse"));
            mat.color = new Color(0.2f, 0.8f, 0.2f, 0.3f); // 半透明绿色
            renderer.material = mat;
        }

        // 禁用碰撞
        Collider collider = interactionRangeVisual.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        // 默认隐藏
        interactionRangeVisual.SetActive(false);
    }

    // 显示路径段
    private void ShowPathSegment(PathEvent pathEvent, bool isActive)
    {
        // 清除先前的路径段标记
        HidePathSegment();

        if (currentPathPointsParent == null)
            return;

        // 确保索引有效
        if (pathEvent.startPointIndex < 0 || pathEvent.startPointIndex >= currentPathPointsParent.childCount ||
            pathEvent.endPointIndex < 0 || pathEvent.endPointIndex >= currentPathPointsParent.childCount)
            return;

        // 确定起始和结束索引
        int startIdx = Mathf.Min(pathEvent.startPointIndex, pathEvent.endPointIndex);
        int endIdx = Mathf.Max(pathEvent.startPointIndex, pathEvent.endPointIndex);

        // 设置颜色：绿色表示激活，红色表示未激活
        Color segmentColor = isActive ?
            new Color(0.2f, 0.8f, 0.2f, 0.3f) : // 绿色半透明
            new Color(0.8f, 0.2f, 0.2f, 0.3f);  // 红色半透明

        // 为路径段的每个点创建标记
        for (int i = startIdx; i <= endIdx; i++)
        {
            Transform pathPoint = currentPathPointsParent.GetChild(i);

            // 创建标记球体
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = $"PathSegmentMarker_{i}";
            marker.transform.position = pathPoint.position;
            marker.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

            // 设置材质
            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Transparent/Diffuse"));
                mat.color = segmentColor;
                renderer.material = mat;
            }

            // 禁用碰撞
            Collider collider = marker.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            pathSegmentMarkers.Add(marker);
        }

        // 连接点之间的线段
        for (int i = startIdx; i < endIdx; i++)
        {
            Transform start = currentPathPointsParent.GetChild(i);
            Transform end = currentPathPointsParent.GetChild(i + 1);

            // 创建线段
            GameObject line = new GameObject($"PathSegmentLine_{i}");
            LineRenderer lineRenderer = line.AddComponent<LineRenderer>();

            lineRenderer.startWidth = 0.2f;
            lineRenderer.endWidth = 0.2f;
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, start.position);
            lineRenderer.SetPosition(1, end.position);

            // 设置材质
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = segmentColor;
            lineRenderer.endColor = segmentColor;

            pathSegmentMarkers.Add(line);
        }
    }

    private void HidePathSegment()
    {
        foreach (var marker in pathSegmentMarkers)
        {
            if (marker != null)
            {
                Destroy(marker);
            }
        }
        pathSegmentMarkers.Clear();
    }

    // 手势识别成功回调
    private void OnGestureSuccessCallback(string gestureType)
    {
        if (currentEvent == null || !isProcessingEvent)
            return;

        // 查找匹配的手势响应
        GestureResponse matchedResponse = currentEvent.gestureResponses.Find(r => r.gestureType == gestureType);

        if (matchedResponse != null)
        {
            // 执行匹配的手势响应
            ExecuteResponse(matchedResponse);
        }
        else
        {
            // 没有匹配的响应，使用默认响应
            ExecuteResponse(currentEvent.defaultResponse);
        }
    }

    // 手势识别失败回调
    private void OnGestureFailureCallback()
    {
        if (currentEvent == null || !isProcessingEvent)
            return;

        // 超时使用默认响应
        ExecuteResponse(currentEvent.defaultResponse);
    }

    // 执行手势响应
    private void ExecuteResponse(GestureResponse response)
    {
        if (currentEvent == null || !isProcessingEvent)
            return;

        // 更新NPC分数
        if (response.scoreEffect != 0)
        {
            controller.UpdateScore(response.scoreEffect);
        }

        // 延迟完成事件
        if (response.completionDelay > 0)
        {
            StartCoroutine(DelayedCompletion(response.completionDelay));
        }
        else
        {
            CompleteCurrentEvent();
        }
    }

    // 延迟完成事件
    private IEnumerator DelayedCompletion(float delay)
    {
        yield return new WaitForSeconds(delay);
        CompleteCurrentEvent();
    }

    // 订阅游戏状态事件
    private void SubscribeToGameEvents()
    {
        if (EventCenter.Instance != null)
        {
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
            EventCenter.Instance.Unsubscribe(GameState.EventNames.STATE_ENTERED + GameState.State.Act1.ToString(), () => SetCurrentAct(1));
            EventCenter.Instance.Unsubscribe(GameState.EventNames.STATE_ENTERED + GameState.State.Act2.ToString(), () => SetCurrentAct(2));
            EventCenter.Instance.Unsubscribe(GameState.EventNames.STATE_ENTERED + GameState.State.Act3.ToString(), () => SetCurrentAct(3));
        }
    }

    // 获取当前 NPC 的数据
    public NPCData GetNPCData()
    {
        NPCMain npcMain = GetComponent<NPCMain>();
        if (npcMain != null)
        {
            // 根据 NPCMain 类的实际实现调整
            return npcMain.GetNPCData();
        }
        return null;
    }

    // 添加一个用于测试触发事件的方法
    public void TestTriggerEvent(string pathId, PathEvent pathEvent)
    {
        if (pathEvent == null)
            return;

        Debug.Log($"[事件测试] 触发事件: {pathEvent.eventID}");

        // 设置当前路径和事件
        // 这些代码需要根据 NPCEventManager 的实际实现进行调整
        // SetCurrentPath(pathId);
        // TriggerEvent(pathEvent);
    }
}