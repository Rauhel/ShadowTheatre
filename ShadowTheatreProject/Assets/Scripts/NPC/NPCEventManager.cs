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
            InputManager.Instance.RegisterGestureListener(OnGestureUpdated);
        }
        else
        {
            Debug.LogError("找不到InputManager实例，手势检测将不可用");
        }
    }

    void OnDestroy()
    {
        // 取消监听
        if (InputManager.Instance != null)
        {
            InputManager.Instance.UnregisterGestureListener(OnGestureUpdated);
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
            return;

        foreach (var pathEvent in currentPathEvents)
        {
            if (pathEvent.pathPointIndex < 0 || pathEvent.pathPointIndex >= currentPathPointsParent.childCount)
                continue;

            // 获取事件触发点
            Transform triggerPoint = currentPathPointsParent.GetChild(pathEvent.pathPointIndex);

            // 检查是否在触发区域内
            float distance = Vector3.Distance(transform.position, triggerPoint.position);
            bool isInTriggerArea = distance <= pathEvent.triggerRadius;

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

        // 停止当前路径跟随
        pathManager.PausePathProcessing(true);

        // 开始手势检测倒计时
        StartCoroutine(GestureDetectionCoroutine(pathEvent));
    }

    private IEnumerator GestureDetectionCoroutine(PathEvent pathEvent)
    {
        Debug.Log($"[{gameObject.name}] 事件 {pathEvent.eventID} 触发，等待手势输入...");

        // 清空手势保持时间记录
        gestureHoldTimes.Clear();

        // 限时等待手势
        float timeElapsed = 0;
        bool gestureSuccess = false;
        GestureResponse successResponse = null;

        while (timeElapsed < pathEvent.gestureTimeLimit && !gestureSuccess)
        {
            timeElapsed += Time.deltaTime;

            // 检查各手势的保持时间
            foreach (var response in pathEvent.gestureResponses)
            {
                if (gestureHoldTimes.TryGetValue(response.gestureType, out float holdTime) &&
                    holdTime >= pathEvent.gestureHoldTime)
                {
                    gestureSuccess = true;
                    successResponse = response;
                    break;
                }
            }

            yield return null;
        }

        // 根据检测结果执行反应
        if (gestureSuccess && successResponse != null)
        {
            Debug.Log($"[{gameObject.name}] 检测到手势: {successResponse.gestureType}，执行对应反应");
            ExecuteResponse(successResponse);
        }
        else
        {
            Debug.Log($"[{gameObject.name}] 未检测到有效手势，执行默认反应");
            ExecuteResponse(pathEvent.defaultResponse);
        }
    }

    private void OnGestureUpdated(InputManager.GestureData gestureData)
    {
        // 如果不在事件处理中，忽略手势
        if (!isProcessingEvent || currentEvent == null)
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
            }
        }
    }

    private void ExecuteResponse(GestureResponse response)
    {
        StartCoroutine(ExecuteResponseCoroutine(response));
    }

    private IEnumerator ExecuteResponseCoroutine(GestureResponse response)
    {
        // 更新分数
        controller.UpdateScore(response.scoreEffect);

        // 播放动画
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

        // 恢复路径跟随
        pathManager.ResumePathProcessing();
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