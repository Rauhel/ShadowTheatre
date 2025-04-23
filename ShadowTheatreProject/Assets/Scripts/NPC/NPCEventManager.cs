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

    // 检查周围是否有事件 - 只检查当前路径上的事件
    private void CheckEventsInRange()
    {
        if (activePathEvents.Count == 0 || currentPathPointsParent == null)
            return;

        foreach (var pathEvent in activePathEvents)
        {
            if (pathEvent.pathPointIndex < 0 || pathEvent.pathPointIndex >= currentPathPointsParent.childCount)
                continue;

            // 获取事件位置
            Transform triggerPoint = currentPathPointsParent.GetChild(pathEvent.pathPointIndex);

            // 检查NPC是否在触发范围内
            float distance = Vector3.Distance(transform.position, triggerPoint.position);
            bool inRange = distance <= pathEvent.triggerRadius;

            // 如果这个事件有显示范围，更新它的状态
            if (pathEvent.showInteractionRange)
            {
                // 只对第一个在范围内的事件显示交互范围
                if (inRange && !isEventDetectable)
                {
                    isEventDetectable = true;
                    currentPathEvent = pathEvent;

                    // 显示该事件的交互范围为绿色（活跃）
                    ShowInteractionRange(pathEvent, true);
                }
                // 对于不在范围内的事件，如果它是当前显示的事件，改为红色
                else if (pathEvent == currentEvent)
                {
                    // 显示为红色（不活跃）
                    ShowInteractionRange(pathEvent, false);
                }
            }

            // 如果在范围内且尚未处理事件，触发事件
            if (inRange && !isProcessingEvent)
            {
                TriggerEvent(pathEvent);
                break; // 只触发一个事件
            }
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

    // 完成当前事件
    private void CompleteCurrentEvent()
    {
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

    // 显示/隐藏交互范围
    private void ShowInteractionRange(PathEvent pathEvent, bool isActive)
    {
        if (interactionRangeVisual == null || pathEvent == null)
            return;

        if (pathEvent.showInteractionRange)
        {
            // 设置大小
            interactionRangeVisual.transform.localScale = new Vector3(
                pathEvent.playerInteractionRadius * 2,
                pathEvent.playerInteractionRadius * 2,
                pathEvent.playerInteractionRadius * 2
            );

            // 设置颜色：活跃=绿色(NPC在范围内)，不活跃=红色(NPC不在范围内)
            Renderer renderer = interactionRangeVisual.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (isActive)
                {
                    // 活跃状态 - 绿色
                    renderer.material.color = new Color(0.2f, 0.8f, 0.2f, 0.3f);
                }
                else
                {
                    // 不活跃状态 - 红色
                    renderer.material.color = new Color(0.8f, 0.2f, 0.2f, 0.3f);
                }
            }

            interactionRangeVisual.SetActive(true);
        }
        else
        {
            interactionRangeVisual.SetActive(false);
        }
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
}