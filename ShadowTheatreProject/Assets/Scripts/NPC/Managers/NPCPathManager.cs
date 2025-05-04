using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NPCController))]
[RequireComponent(typeof(NavMeshAgent))]
public class NPCPathManager : MonoBehaviour
{
    // 内部状态
    private NPCController controller;
    private NPCEventManager eventManager;
    private MultiPointPathCreator currentPathCreator;
    private Transform currentPathPoints;
    private int currentPathPointIndex = 0;
    private bool pathProcessingPaused = false;
    private float stuckTime = 0;
    private int lastReachedPointIndex = -1;
    private float reachDistance = 0.5f; // 到达点的距离阈值
    private NavMeshAgent agent;

    // 公共属性
    public Transform CurrentPath => currentPathPoints;
    public bool IsFollowingPath => currentPathPoints != null && !pathProcessingPaused;
    public string CurrentPathID => currentPathCreator ? currentPathCreator.pathID : string.Empty;
    public PathConfig CurrentPathConfig { get; private set; }
    public bool IsMoving => agent != null && !agent.isStopped && agent.velocity.magnitude > 0.1f;
    public int CurrentPathPointIndex => lastReachedPointIndex;

    // 添加路径点到达委托
    public delegate void PathPointReachedHandler(int pathPointIndex);
    public event PathPointReachedHandler OnPathPointReached;

    void Awake()
    {
        controller = GetComponent<NPCController>();
        eventManager = GetComponent<NPCEventManager>();
        agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        // 初始化路径
        InitializeStartingPath();
    }

    void Update()
    {
        if (pathProcessingPaused || controller.Data == null)
            return;

        // 路径移动
        FollowCurrentPath();

        // 检查是否到达了路径点
        if (currentPathPoints != null)
        {
            for (int i = 0; i < currentPathPoints.childCount; i++)
            {
                float distanceToPoint = Vector3.Distance(transform.position, currentPathPoints.GetChild(i).position);

                if (distanceToPoint < reachDistance && lastReachedPointIndex != i)
                {
                    lastReachedPointIndex = i;

                    // 触发路径点到达事件，让其他管理器处理各自职责
                    OnPathPointReached?.Invoke(i);

                    // 只处理等待时间逻辑
                    HandlePathPointWaitTime(i);
                    break;
                }
            }
        }
    }

    // 处理路径点等待时间
    private void HandlePathPointWaitTime(int pathPointIndex)
    {
        // 获取当前路径配置
        NPCData npcData = controller.Data;
        if (npcData == null)
            return;

        PathConfig currentPath = GetCurrentPathConfig();
        if (currentPath == null)
            return;

        float waitTime = 0f;

        // 检查是否有与此路径点关联的已完成事件的动作
        if (eventManager != null)
        {
            PathEvent completedEvent = eventManager.FindCompletedEventForPathPoint(pathPointIndex);
            if (completedEvent != null)
            {
                // 寻找路径点动作中的等待时间
                foreach (var action in completedEvent.defaultResponse.actions)
                {
                    if (action.pathPointIndex == pathPointIndex && action.isActionActive && action.waitTime > 0)
                    {
                        waitTime = action.waitTime;
                        break;
                    }
                }
            }
        }

        // 如果没有已完成的事件的等待时间，检查默认路径动作
        if (waitTime <= 0)
        {
            foreach (var action in currentPath.pathActions)
            {
                if (action.pathPointIndex == pathPointIndex && action.isActionActive && action.waitTime > 0)
                {
                    waitTime = action.waitTime;
                    break;
                }
            }
        }

        // 处理等待时间
        if (waitTime > 0)
        {
            StartCoroutine(WaitAtPathPoint(waitTime));
        }
    }

    // 添加一个新的协程来处理等待
    private IEnumerator WaitAtPathPoint(float waitTime)
    {
        Debug.Log($"[{gameObject.name}] 在路径点停留 {waitTime} 秒");

        // 停止NPC移动
        controller.StopMovement(true);

        // 等待指定时间
        yield return new WaitForSeconds(waitTime);

        // 继续移动
        controller.StopMovement(false);
        Debug.Log($"[{gameObject.name}] 停留结束，继续移动");
    }

    // 路径初始化
    private void InitializeStartingPath()
    {
        if (controller.Data == null || controller.Data.paths.Count == 0)
        {
            Debug.LogError($"[{gameObject.name}] 没有设置NPCData或没有路径配置，无法初始化路径");
            return;
        }

        // 使用第一个有效路径作为起始路径
        foreach (var pathConfig in controller.Data.paths)
        {
            MultiPointPathCreator pathCreator = PathRegistry.GetPathCreatorByID(pathConfig.pathID);
            if (pathCreator != null)
            {
                Debug.Log($"[{gameObject.name}] 使用路径初始化: {pathConfig.pathName}");
                SwitchToPath(pathCreator);
                return;
            }
        }

        Debug.LogWarning($"[{gameObject.name}] NPCData中没有找到有效的初始路径，NPC将不会移动");
    }

    // 路径跟随逻辑
    private void FollowCurrentPath()
    {
        if (currentPathPoints == null || currentPathPointIndex >= currentPathPoints.childCount || agent == null)
            return;

        // 获取当前路径点
        Transform targetPoint = currentPathPoints.GetChild(currentPathPointIndex);

        // 确保代理未停止
        if (agent.isStopped)
        {
            agent.isStopped = false;
        }

        // 检查当前目标是否有效
        if (agent.pathPending)
        {
            return; // 路径计算中，等待
        }

        if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            Debug.LogError($"[{gameObject.name}] 无法找到到达目标点的路径，尝试跳到下一点");
            currentPathPointIndex++;
            return;
        }

        // 设置目标（如果需要）
        if (agent.destination != targetPoint.position)
        {
            agent.SetDestination(targetPoint.position);
        }

        // 检查是否到达当前点
        float distanceToTarget = Vector3.Distance(transform.position, targetPoint.position);
        float stoppingDistance = agent.stoppingDistance;

        if (distanceToTarget <= Mathf.Max(stoppingDistance, 0.5f) || agent.remainingDistance <= stoppingDistance)
        {
            // 前往下一个点
            currentPathPointIndex++;

            // 立即设置下一个目标点（如果有）
            if (currentPathPointIndex < currentPathPoints.childCount)
            {
                Transform nextPoint = currentPathPoints.GetChild(currentPathPointIndex);
                agent.SetDestination(nextPoint.position);
            }
            // 检查是否完成整个路径
            else
            {
                Debug.Log($"[{gameObject.name}] 完成路径: {currentPathCreator.name}");
                ProcessPathCompletion();
            }
        }

        // 添加额外检查 - 如果速度接近零但未到达目标，可能卡住了
        if (agent.velocity.magnitude < 0.1f && distanceToTarget > stoppingDistance + 0.5f)
        {
            stuckTime += Time.deltaTime;

            if (stuckTime > 3.0f)
            {
                Debug.LogWarning($"[{gameObject.name}] 可能卡住了，尝试跳到下一点");
                currentPathPointIndex++;
                stuckTime = 0;
            }
        }
        else
        {
            stuckTime = 0;
        }
    }

    // 处理路径完成
    private void ProcessPathCompletion()
    {
        if (currentPathCreator == null || controller.Data == null)
        {
            Debug.LogWarning($"[{gameObject.name}] 路径数据不完整，无法确定下一步");
            controller.StopMovement(true);
            return;
        }

        // 查找当前路径配置
        PathConfig currentPathConfig = controller.Data.FindPathById(currentPathCreator.pathID);
        if (currentPathConfig == null)
        {
            Debug.LogWarning($"[{gameObject.name}] 找不到当前路径的配置: {currentPathCreator.pathID}");
            controller.StopMovement(true);
            return;
        }

        // 检查是否有下一条路径
        if (currentPathConfig.nextPaths.Count == 0)
        {
            Debug.Log($"[{gameObject.name}] 路径 {currentPathConfig.pathName} 没有下一条路径，停止移动");
            controller.StopMovement(true);
            return;
        }

        // 根据分数选择下一条路径
        string nextPathID = currentPathConfig.SelectNextPathByScore(controller.Data.currentScore);
        if (string.IsNullOrEmpty(nextPathID))
        {
            Debug.LogWarning($"[{gameObject.name}] 无法确定下一条路径，停止移动");
            controller.StopMovement(true);
            return;
        }

        // 切换到下一条路径
        MultiPointPathCreator nextPathCreator = PathRegistry.GetPathCreatorByID(nextPathID);
        if (nextPathCreator != null)
        {
            SwitchToPath(nextPathCreator);
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] 找不到ID为 {nextPathID} 的路径，停止移动");
            controller.StopMovement(true);
        }
    }

    // 路径切换
    public void SwitchToPath(MultiPointPathCreator newPathCreator)
    {
        if (newPathCreator == null)
        {
            Debug.LogError($"[{gameObject.name}] 尝试切换到空路径");
            return;
        }

        if (newPathCreator.pathPointsParent == null || newPathCreator.pathPointsParent.childCount == 0)
        {
            Debug.LogError($"[{gameObject.name}] 路径 {newPathCreator.name} 没有路径点");
            return;
        }

        Debug.Log($"[{gameObject.name}] 切换到路径: {newPathCreator.name} (包含 {newPathCreator.pathPointsParent.childCount} 个点)");

        // 设置新路径
        currentPathCreator = newPathCreator;
        currentPathPoints = newPathCreator.pathPointsParent;
        currentPathPointIndex = 0;
        stuckTime = 0;

        // 通知事件管理器
        eventManager.SetCurrentPath(newPathCreator.pathID, currentPathPoints);

        // 确保 NavMeshAgent 已设置
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        // 启用并设置 NavMeshAgent
        if (agent != null)
        {
            agent.enabled = true;
            agent.isStopped = false;

            // 设置当前目标点
            if (currentPathPoints.childCount > 0)
            {
                Transform targetPoint = currentPathPoints.GetChild(0);
                NavMeshHit hit;
                if (NavMesh.SamplePosition(targetPoint.position, out hit, 5f, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                }
                else
                {
                    agent.SetDestination(targetPoint.position);
                }
            }
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] 无法获取 NavMeshAgent 组件，导航失败");
        }
    }

    // 根据路径ID切换
    public void SwitchToPathByID(string pathID)
    {
        MultiPointPathCreator pathCreator = PathRegistry.GetPathCreatorByID(pathID);
        if (pathCreator != null)
        {
            SwitchToPath(pathCreator);
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] 找不到ID为 {pathID} 的路径");
        }
    }

    // 暂停/恢复路径处理
    public void PausePathProcessing(bool pause)
    {
        pathProcessingPaused = pause;
        controller.StopMovement(pause);
    }

    // 返回路径处理
    public void ResumePathProcessing()
    {
        PausePathProcessing(false);
    }

    // 获取当前路径配置
    public PathConfig GetCurrentPathConfig()
    {
        if (currentPathCreator == null || controller.Data == null)
            return null;

        return controller.Data.FindPathById(currentPathCreator.pathID);
    }

    // 设置路径
    public void SetPath(string pathId)
    {
        MultiPointPathCreator pathCreator = PathRegistry.GetPathCreatorByID(pathId);
        if (pathCreator != null)
        {
            SwitchToPath(pathCreator);

            // 获取并保存当前路径配置
            CurrentPathConfig = null;
            if (controller != null && controller.Data != null)
            {
                foreach (var path in controller.Data.paths)
                {
                    if (path.pathID == pathId)
                    {
                        CurrentPathConfig = path;
                        break;
                    }
                }
            }

            // 重置路径点索引
            lastReachedPointIndex = -1;
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] 找不到ID为 {pathId} 的路径");
        }
    }

    // 获取当前点索引
    public int GetCurrentPathPointIndex()
    {
        return lastReachedPointIndex;
    }
}