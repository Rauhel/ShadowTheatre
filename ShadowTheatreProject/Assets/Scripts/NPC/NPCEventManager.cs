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
    private Dictionary<string, float> gestureHoldTimes = new Dictionary<string, float>();

    // 当前路径的事件
    private List<PathEvent> currentPathEvents = new List<PathEvent>();
    private Transform currentPathPointsParent;

    private GestureRecognitionManager gestureRecognitionManager;

    // 添加一个选项来选择使用哪个系统
    [Header("手势识别设置")]
    [Tooltip("是否使用新的手势识别系统")]
    public bool useNewGestureSystem = true;

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

    void Awake()
    {
        controller = GetComponent<NPCController>();
        pathManager = GetComponent<NPCPathManager>();
    }

    void Start()
    {
        // 注册手势监听
        if (InputManager.Instance != null)
        {
            // 只在使用旧系统时注册
            if (!useNewGestureSystem)
            {
                InputManager.Instance.RegisterGestureListener(OnGestureUpdated);
            }
        }
        else
        {
            Debug.LogError("找不到InputManager实例，手势检测将不可用");
        }

        // 获取手势识别管理器
        gestureRecognitionManager = FindObjectOfType<GestureRecognitionManager>();
        if (gestureRecognitionManager != null && useNewGestureSystem)
        {
            gestureRecognitionManager.OnGestureRecognized += HandleGestureRecognized;
            gestureRecognitionManager.OnGestureTimedOut += HandleGestureTimedOut;
        }
    }

    void OnDestroy()
    {
        // 取消监听
        if (InputManager.Instance != null && !useNewGestureSystem)
        {
            InputManager.Instance.UnregisterGestureListener(OnGestureUpdated);
        }

        if (gestureRecognitionManager != null && useNewGestureSystem)
        {
            gestureRecognitionManager.OnGestureRecognized -= HandleGestureRecognized;
            gestureRecognitionManager.OnGestureTimedOut -= HandleGestureTimedOut;
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

        // 查找对应路径配置
        PathConfig pathConfig = controller.Data.FindPathById(pathID);
        if (pathConfig != null)
        {
            currentPathEvents = pathConfig.events;
            Debug.Log($"[{gameObject.name}] 设置当前路径: {pathID}, 包含 {currentPathEvents.Count} 个事件");
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] 未找到路径配置: {pathID}");
        }
    }

    private void CheckEventTriggers()
    {
        if (currentPathEvents.Count == 0 || currentPathPointsParent == null)
        {
            isEventDetectable = false;
            currentPathEvent = null;
            return;
        }

        isEventDetectable = false;
        currentPathEvent = null;

        foreach (var pathEvent in currentPathEvents)
        {
            if (pathEvent.pathPointIndex < 0 || pathEvent.pathPointIndex >= currentPathPointsParent.childCount)
                continue;

            // 获取事件触发点
            Transform triggerPoint = currentPathPointsParent.GetChild(pathEvent.pathPointIndex);

            // 检查是否在触发区域内
            float distance = Vector3.Distance(transform.position, triggerPoint.position);
            bool isInTriggerArea = distance <= pathEvent.triggerRadius;

            // 当NPC接近事件触发区域时标记为可检测（距离在触发半径的1.5倍内）
            if (distance <= pathEvent.triggerRadius * 1f)
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

        // 根据选择的系统进行不同的处理
        if (useNewGestureSystem)
        {
            // 使用新系统处理手势识别
            if (pathEvent.gestureResponses.Count > 0)
            {
                StartGestureRecognitionForEvent(pathEvent, pathEvent.gestureResponses[0].gestureType);
            }
            else
            {
                // 没有手势响应，使用默认响应
                ExecuteGestureResponse(pathEvent.defaultResponse);
            }
        }
        else
        {
            // 使用旧系统处理 - 这里不需要做特别处理，旧系统会通过 OnGestureUpdated 回调自动处理
            // 清空手势保持时间
            gestureHoldTimes.Clear();
            Debug.Log($"[{gameObject.name}] 开始事件: {pathEvent.eventID}，等待手势...");
        }
    }

    // 当事件进入手势识别阶段时调用
    private void StartGestureRecognitionForEvent(PathEvent pathEvent, string targetGestureType)
    {
        // 配置手势识别管理器
        if (gestureRecognitionManager != null)
        {
            // 使用事件中配置的时间参数
            gestureRecognitionManager.fullRecognitionTime = pathEvent.gestureHoldTime;
            gestureRecognitionManager.timeLimit = pathEvent.gestureTimeLimit;

            // 开始识别
            gestureRecognitionManager.StartGestureRecognition(targetGestureType);
        }
    }

    // 处理手势识别成功
    private void HandleGestureRecognized(string recognizedGesture)
    {
        // 查找当前事件中匹配的手势响应
        if (currentEvent != null)
        {
            GestureResponse matchedResponse = currentEvent.gestureResponses.Find(r => r.gestureType == recognizedGesture);

            if (matchedResponse != null)
            {
                // 执行手势响应
                ExecuteGestureResponse(matchedResponse);
            }
        }
    }

    // 处理手势识别超时
    private void HandleGestureTimedOut()
    {
        // 执行默认响应
        if (currentEvent != null && currentEvent.defaultResponse != null)
        {
            ExecuteGestureResponse(currentEvent.defaultResponse);
        }
    }

    // 执行手势响应
    private void ExecuteGestureResponse(GestureResponse response)
    {
        // 应用分数影响
        if (controller != null && controller.Data != null)
        {
            controller.Data.currentScore += response.scoreEffect;
            Debug.Log($"手势反应: 分数 {(response.scoreEffect >= 0 ? "+" : "")}{response.scoreEffect}，当前分数: {controller.Data.currentScore}");
        }

        // 播放动画
        if (!string.IsNullOrEmpty(response.animationName))
        {
            controller.PlayAnimation(response.animationName);
        }

        // 显示对话
        if (!string.IsNullOrEmpty(response.dialogueText))
        {
            Debug.Log($"[{gameObject.name}] 对话: {response.dialogueText}");
            // 如果有对话系统，这里调用显示对话
            // DialogueManager.Instance.ShowDialogue(response.dialogueText, controller.Data.npcName);
        }

        // 延迟完成
        StartCoroutine(DelayedCompletion(response.completionDelay));
    }

    private IEnumerator DelayedCompletion(float delay)
    {
        yield return new WaitForSeconds(delay);

        // 完成当前事件
        CompleteCurrentEvent();
    }

    private void CompleteCurrentEvent()
    {
        // 事件处理完成
        isProcessingEvent = false;
        currentEvent = null;

        // 不再需要恢复路径跟踪，因为我们没有暂停它
        // pathManager.ResumePathProcessing();
    }

    private void OnGestureUpdated(InputManager.GestureData gestureData)
    {
        // 如果使用新系统或不在事件处理中，忽略手势
        if (useNewGestureSystem || !isProcessingEvent || currentEvent == null)
            return;

        // 检查这个手势是否匹配任何响应
        foreach (var response in currentEvent.gestureResponses)
        {
            if (gestureData.type == response.gestureType &&
                gestureData.confidence >= response.minConfidence)
            {
                // 累计保持时间
                if (!gestureHoldTimes.ContainsKey(gestureData.type))
                {
                    gestureHoldTimes[gestureData.type] = 0;
                }

                gestureHoldTimes[gestureData.type] += Time.deltaTime;

                // 调试信息
                Debug.Log($"[{gameObject.name}] 检测到手势 {gestureData.type}，" +
                          $"置信度: {gestureData.confidence:F2}，" +
                          $"保持时间: {gestureHoldTimes[gestureData.type]:F2}/{currentEvent.gestureHoldTime}");

                // 检查是否达到所需保持时间
                if (gestureHoldTimes[gestureData.type] >= currentEvent.gestureHoldTime)
                {
                    // 手势识别成功，执行响应
                    Debug.Log($"[{gameObject.name}] 手势 {gestureData.type} 保持时间达到要求，执行响应");
                    ExecuteGestureResponse(response);
                    return;
                }
            }
            else
            {
                // 如果手势不匹配，则重置该手势的保持时间
                gestureHoldTimes[response.gestureType] = 0;
            }
        }

        // 检查是否超时
        // (这里可以添加超时逻辑，但为了简化起见暂时略过)
    }

    private void ExecuteResponse(GestureResponse response)
    {
        StartCoroutine(ExecuteResponseCoroutine(response));
    }

    private IEnumerator ExecuteResponseCoroutine(GestureResponse response)
    {
        // 更新分数
        controller.UpdateScore(response.scoreEffect);

        // 播放动画 - NPC会继续移动，同时播放动画
        controller.PlayAnimation(response.animationName);

        // 如果有动画，等待动画完成
        if (!string.IsNullOrEmpty(response.animationName))
        {
            yield return new WaitForSeconds(1f); // 假设动画长度约为1秒
        }

        // 显示对话
        if (!string.IsNullOrEmpty(response.dialogueText))
        {
            Debug.Log($"[{gameObject.name}] 对话: {response.dialogueText}");
            // 如果有对话系统，这里调用显示对话
            // DialogueManager.Instance.ShowDialogue(response.dialogueText, controller.Data.npcName);
            yield return new WaitForSeconds(2f); // 给玩家时间阅读对话
        }

        // 分支完成延迟
        yield return new WaitForSeconds(response.completionDelay);

        // 事件处理完成
        isProcessingEvent = false;
        currentEvent = null;

        // 不再需要恢复路径跟踪，因为我们没有暂停它
        // pathManager.ResumePathProcessing();
    }

    // 直接触发事件的公共方法（例如从其他系统触发）
    public void TriggerEventByID(string eventID)
    {
        if (isProcessingEvent)
            return;

        PathEvent targetEvent = currentPathEvents.Find(e => e.eventID == eventID);
        if (targetEvent != null)
        {
            TriggerEvent(targetEvent);
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] 找不到ID为 {eventID} 的事件");
        }
    }
}