using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NPCEventManager : MonoBehaviour
{
    [Header("NPC配置")]
    public NPCData npcData;

    [Header("引用")]
    public Animator animator;

    // 内部状态
    private NavMeshAgent agent;
    private Transform currentPath;
    private int currentPathPoint = 0;
    private bool isProcessingEvent = false;
    private NPCEvent currentEvent = null;
    private Dictionary<string, float> gestureHoldTimes = new Dictionary<string, float>();

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (npcData == null)
        {
            Debug.LogError($"[{gameObject.name}] 没有设置NPCData!");
        }
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
        if (isProcessingEvent || npcData == null)
            return;

        // 检查事件触发
        CheckEventTriggers();

        // 检查路径分歧点
        CheckPathDecisions();

        // 路径移动
        FollowCurrentPath();
    }

    private void CheckEventTriggers()
    {
        foreach (var npcEvent in npcData.events)
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
        agent.isStopped = true;

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
        npcData.currentScore += branch.scoreValue;
        Debug.Log($"[{gameObject.name}] 分数更新: {npcData.currentScore} (+{branch.scoreValue})");

        // 播放动画
        if (!string.IsNullOrEmpty(branch.animationName) && animator != null)
        {
            animator.Play(branch.animationName);
            // 等待动画完成（假设动画长度约为1秒）
            yield return new WaitForSeconds(1f);
        }

        // 显示对话（这里假设有DialogueManager）
        if (!string.IsNullOrEmpty(branch.dialogueText))
        {
            Debug.Log($"[{gameObject.name}] 对话: {branch.dialogueText}");
            // 如果有对话系统，这里调用显示对话
            // DialogueManager.Instance.ShowDialogue(branch.dialogueText, npcData.npcName);
            yield return new WaitForSeconds(2f); // 给玩家时间阅读对话
        }

        // 处理覆盖路径
        if (branch.overridePath != null)
        {
            // 临时更改路径
            Transform originalPath = currentPath;
            int originalPathPoint = currentPathPoint;

            // 切换到覆盖路径
            SwitchToPath(branch.overridePath);

            // 等待路径完成
            while (currentPathPoint < branch.overridePath.childCount)
            {
                yield return null;
            }

            // 恢复原路径
            currentPath = originalPath;
            currentPathPoint = originalPathPoint;
        }

        // 分支完成延迟
        yield return new WaitForSeconds(branch.completionDelay);

        // 事件处理完成
        isProcessingEvent = false;
        currentEvent = null;

        // 恢复路径跟随
        agent.isStopped = false;
    }

    private void CheckPathDecisions()
    {
        if (currentPath == null || npcData.pathDecisions.Count == 0)
            return;

        foreach (var decision in npcData.pathDecisions)
        {
            if (decision.decisionLocation == null)
                continue;

            float distance = Vector3.Distance(transform.position, decision.decisionLocation.position);

            if (distance <= decision.decisionRadius)
            {
                // 到达决策点，根据分数决定路径
                Transform newPath = decision.SelectPathBasedOnScore(npcData.currentScore);

                if (newPath != null)
                {
                    // 找到匹配的路径选项用于日志输出
                    string pathDescription = "未知路径";
                    foreach (var option in decision.pathOptions)
                    {
                        if (option.path == newPath)
                        {
                            pathDescription = $"{option.optionName} (阈值:{option.scoreThreshold})";
                            break;
                        }
                    }

                    Debug.Log($"[{gameObject.name}] 在决策点 {decision.decisionPointID} 选择路径: {pathDescription}");
                    SwitchToPath(newPath);
                }

                break;
            }
        }
    }

    private void FollowCurrentPath()
    {
        if (currentPath == null || currentPathPoint >= currentPath.childCount)
            return;

        // 获取当前路径点
        Transform targetPoint = currentPath.GetChild(currentPathPoint);

        // 设置目标
        agent.SetDestination(targetPoint.position);

        // 检查是否到达当前点
        float distanceToTarget = Vector3.Distance(transform.position, targetPoint.position);
        if (distanceToTarget < 0.5f)
        {
            // 前往下一个点
            currentPathPoint++;

            // 检查是否完成整个路径
            if (currentPathPoint >= currentPath.childCount)
            {
                Debug.Log($"[{gameObject.name}] 完成路径: {currentPath.name}");

                // 检查是否有路径连接信息
                if (npcData != null)
                {
                    PathConnection connection = FindPathConnection(currentPath);
                    if (connection != null)
                    {
                        if (connection.isEndPoint)
                        {
                            // 路径终点，停止移动
                            Debug.Log($"[{gameObject.name}] 到达路径终点");
                            currentPath = null;
                        }
                        else if (connection.needDecision)
                        {
                            // 需要决策
                            PathDecision decision = FindDecisionByID(connection.decisionPointID);
                            if (decision != null)
                            {
                                Transform nextPath = decision.SelectPathBasedOnScore(npcData.currentScore);
                                if (nextPath != null)
                                {
                                    SwitchToPath(nextPath);
                                }
                            }
                            else
                            {
                                Debug.LogWarning($"[{gameObject.name}] 找不到决策点ID: {connection.decisionPointID}");

                                // 没有找到决策点但有默认下一路径
                                if (connection.nextPath != null)
                                {
                                    SwitchToPath(connection.nextPath);
                                }
                            }
                        }
                        else if (connection.nextPath != null)
                        {
                            // 直接切换到下一路径
                            SwitchToPath(connection.nextPath);
                        }
                    }
                }
            }
        }
    }

    // 根据路径查找路径连接信息
    private PathConnection FindPathConnection(Transform path)
    {
        if (npcData == null || path == null)
            return null;

        foreach (var connection in npcData.pathConnections)
        {
            if (connection.path == path)
                return connection;
        }

        return null;
    }

    // 根据ID查找决策点
    private PathDecision FindDecisionByID(string decisionID)
    {
        if (npcData == null || string.IsNullOrEmpty(decisionID))
            return null;

        foreach (var decision in npcData.pathDecisions)
        {
            if (decision.decisionPointID == decisionID)
                return decision;
        }

        return null;
    }

    // 切换到新路径
    public void SwitchToPath(Transform newPath)
    {
        if (newPath == null)
            return;

        currentPath = newPath;
        currentPathPoint = 0;

        Debug.Log($"[{gameObject.name}] 切换到路径: {newPath.name}");

        // 如果是路径生成器创建的路径点
        if (newPath.name.Contains("PathPoints") && newPath.childCount > 0)
        {
            // 使用第一个路径点作为目标
            agent.SetDestination(newPath.GetChild(0).position);
        }
        // 支持旧方式
        else if (newPath.childCount > 0)
        {
            agent.SetDestination(newPath.GetChild(0).position);
        }
    }

    // 公共API：设置初始路径
    public void SetInitialPath(Transform path)
    {
        SwitchToPath(path);
    }

    // 调试用：获取当前NPC分数
    public float GetCurrentScore()
    {
        return npcData.currentScore;
    }
}