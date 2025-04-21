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
    private NPCEvent currentEvent = null;
    private Dictionary<string, float> gestureHoldTimes = new Dictionary<string, float>();

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

    private void CheckEventTriggers()
    {
        foreach (var npcEvent in controller.Data.events)
        {
            if (npcEvent.triggerLocation == null)
                continue;

            // 检查是否在触发区域内
            float distance = Vector3.Distance(transform.position, npcEvent.triggerLocation.position);

            // 只检查距离条件
            bool isInTriggerArea = distance <= npcEvent.triggerRadius;

            if (isInTriggerArea)
            {
                TriggerEvent(npcEvent);
                break; // 只处理一个事件
            }
        }
    }

    private void TriggerEvent(NPCEvent npcEvent)
    {
        if (isProcessingEvent)
            return;

        isProcessingEvent = true;
        currentEvent = npcEvent;

        // 停止当前路径跟随
        pathManager.PausePathProcessing(true);

        // 如果有全局事件，则通知EventCenter
        if (!string.IsNullOrEmpty(npcEvent.globalEventName) && EventCenter.Instance != null)
        {
            EventCenter.Instance.Publish(npcEvent.globalEventName);
        }

        // 开始手势检测倒计时
        StartCoroutine(GestureDetectionCoroutine(npcEvent));
    }

    private IEnumerator GestureDetectionCoroutine(NPCEvent npcEvent)
    {
        Debug.Log($"[{gameObject.name}] 事件 {npcEvent.eventID} 触发，等待手势输入...");

        // 清空手势保持时间记录
        gestureHoldTimes.Clear();

        // 限时等待手势
        float timeElapsed = 0;
        bool gestureSuccess = false;
        GestureBranch successBranch = null;

        while (timeElapsed < npcEvent.gestureTimeLimit && !gestureSuccess)
        {
            timeElapsed += Time.deltaTime;

            // 检查各手势的保持时间
            foreach (var gestureBranch in npcEvent.gestureBranches)
            {
                if (gestureHoldTimes.TryGetValue(gestureBranch.gestureType, out float holdTime) &&
                    holdTime >= npcEvent.gestureHoldTime)
                {
                    gestureSuccess = true;
                    successBranch = gestureBranch;
                    break;
                }
            }

            yield return null;
        }

        // 根据检测结果执行分支
        if (gestureSuccess && successBranch != null)
        {
            Debug.Log($"[{gameObject.name}] 检测到手势: {successBranch.gestureType}，执行对应分支");
            ExecuteBranch(successBranch);
        }
        else
        {
            Debug.Log($"[{gameObject.name}] 未检测到有效手势，执行默认分支");
            ExecuteBranch(npcEvent.defaultBranch);
        }
    }

    private void OnGestureUpdated(InputManager.GestureData gestureData)
    {
        // 如果不在事件处理中，忽略手势
        if (!isProcessingEvent || currentEvent == null)
            return;

        // 检查这个手势是否匹配任何分支
        foreach (var branch in currentEvent.gestureBranches)
        {
            if (gestureData.type == branch.gestureType &&
                gestureData.confidence >= branch.minConfidence)
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

    private void ExecuteBranch(EventBranch branch)
    {
        StartCoroutine(ExecuteBranchCoroutine(branch));
    }

    private IEnumerator ExecuteBranchCoroutine(EventBranch branch)
    {
        // 更新分数
        controller.UpdateScore(branch.scoreValue);

        // 播放动画
        controller.PlayAnimation(branch.animationName);

        // 如果有动画，等待动画完成
        if (!string.IsNullOrEmpty(branch.animationName))
        {
            yield return new WaitForSeconds(1f); // 假设动画长度约为1秒
        }

        // 显示对话（这里假设有DialogueManager）
        if (!string.IsNullOrEmpty(branch.dialogueText))
        {
            Debug.Log($"[{gameObject.name}] 对话: {branch.dialogueText}");
            // 如果有对话系统，这里调用显示对话
            // DialogueManager.Instance.ShowDialogue(branch.dialogueText, controller.Data.npcName);
            yield return new WaitForSeconds(2f); // 给玩家时间阅读对话
        }

        // 处理覆盖路径
        if (branch.overridePath != null)
        {
            // 使用临时路径
            yield return StartCoroutine(pathManager.FollowTemporaryPath(branch.overridePath));
        }

        // 分支完成延迟
        yield return new WaitForSeconds(branch.completionDelay);

        // 事件处理完成
        isProcessingEvent = false;
        currentEvent = null;

        // 恢复路径跟随
        pathManager.ResumePathProcessing();
    }

    // 直接触发事件的公共方法（例如从其他系统触发）
    public void TriggerEventByID(string eventID)
    {
        if (isProcessingEvent || controller.Data == null)
            return;

        NPCEvent targetEvent = controller.Data.events.Find(e => e.eventID == eventID);
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