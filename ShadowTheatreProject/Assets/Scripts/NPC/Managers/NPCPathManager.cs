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
    private float reachDistance = 1f; // 到达点的距离阈值
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

    // 修改 NPCPathManager.cs 的 Update 方法
    void Update()
    {
        if (pathProcessingPaused || controller.Data == null)
            return;

        // 路径移动
        FollowCurrentPath();

        // 路径点检测
        if (currentPathCreator != null)
        {
            // 每隔几帧输出一次调试信息，避免日志过多
            if (Time.frameCount % 30 == 0)
                Debug.Log($"[{gameObject.name}] 正在检查路径点，当前位置: {transform.position}, lastReachedPointIndex={lastReachedPointIndex}");

            for (int i = 0; i < currentPathCreator.GetPathPointCount(); i++)
            {
                Vector3 pointPosition = currentPathCreator.GetPathPointPosition(i);
                float distanceToPoint = Vector3.Distance(transform.position, pointPosition);

                // 对每个接近的点输出详细信息
                if (distanceToPoint < reachDistance * 2)
                {
                    Debug.Log($"[{gameObject.name}] 接近路径点 {i}, 距离={distanceToPoint:F2}, 阈值={reachDistance}, 是否为最后到达点={i == lastReachedPointIndex}");
                }

                if (distanceToPoint < reachDistance && lastReachedPointIndex != i)
                {
                    lastReachedPointIndex = i;
                    Debug.Log($"[{gameObject.name}] 到达路径点 {i}，触发事件");

                    // 触发路径点到达事件
                    OnPathPointReached?.Invoke(i);

                    // 处理等待时间
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
        if (controller == null || controller.Data == null)
            return;

        string initialPath = "";
        if (controller.Data.paths != null && controller.Data.paths.Count > 0)
        {
            initialPath = controller.Data.paths[0].pathID;
            Debug.Log($"[{gameObject.name}] 使用第一个可用路径作为初始路径: {initialPath}");
        }

        if (string.IsNullOrEmpty(initialPath))
        {
            Debug.LogWarning($"[{gameObject.name}] 未设置初始路径");
            return;
        }

        // 尝试获取路径
        MultiPointPathCreator pathCreator = PathRegistry.GetPathCreatorByID(initialPath);
        if (pathCreator != null)
        {
            Debug.Log($"[{gameObject.name}] 使用路径初始化: {pathCreator.name}");

            // 确保路径点数据已生成
            if (pathCreator.pathPointPositions.Count == 0 && pathCreator.controlPoints.Count >= 2)
            {
                Debug.Log($"[{gameObject.name}] 正在为路径生成路径点: {pathCreator.name}");
                pathCreator.GeneratePathPoints();
            }

            // 确保验证
            Debug.Log($"[{gameObject.name}] 路径 {pathCreator.name} 包含 {pathCreator.GetPathPointCount()} 个路径点");

            // 切换到该路径
            SwitchToPath(pathCreator);
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] 找不到初始路径: {initialPath}");
        }
    }

    // 路径跟随逻辑
    private void FollowCurrentPath()
    {
        if (currentPathCreator == null || agent == null)
            return;

        if (currentPathPointIndex >= currentPathCreator.GetPathPointCount())
        {
            Debug.LogWarning($"[{gameObject.name}] 路径点索引超出范围: {currentPathPointIndex}/{currentPathCreator.GetPathPointCount()}");
            return;
        }

        // 获取当前目标点位置
        Vector3 targetPosition = currentPathCreator.GetPathPointPosition(currentPathPointIndex);

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

        // 设置目标（如果需要）
        if (agent.destination != targetPosition)
        {
            Debug.Log($"[{gameObject.name}] 设置新目标点: 点{currentPathPointIndex}, 位置: {targetPosition}");
            agent.SetDestination(targetPosition);
        }

        // 检查是否到达当前点
        float distanceToTarget = Vector3.Distance(transform.position, targetPosition);

        // 添加调试信息
        if (distanceToTarget < reachDistance * 2) // 扩大一点检测范围仅用于调试
        {
            Debug.Log($"[{gameObject.name}] 接近目标点 {currentPathPointIndex}，距离: {distanceToTarget}, 阈值: {reachDistance}");
        }

        if (distanceToTarget < reachDistance)
        {
            Debug.Log($"[{gameObject.name}] 已到达路径点 {currentPathPointIndex}");

            // 记录已达到的点
            int reachedPointIndex = currentPathPointIndex;

            // 更新到下一个点
            currentPathPointIndex++;

            // 处理路径循环
            if (currentPathPointIndex >= currentPathCreator.GetPathPointCount() && currentPathCreator.closedPath)
            {
                Debug.Log($"[{gameObject.name}] 路径循环，重置到第一个点");
                currentPathPointIndex = 0;
            }

            // 触发事件
            OnPathPointReached?.Invoke(reachedPointIndex);

            // 如果是最后一个点，停止移动
            if (currentPathPointIndex >= currentPathCreator.GetPathPointCount() && !currentPathCreator.closedPath)
            {
                Debug.Log($"[{gameObject.name}] 到达路径终点");
                agent.isStopped = true;
            }
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

        // 修改这里：使用 GetPathPointCount() 而不是 pathPointsParent.childCount
        if (newPathCreator.GetPathPointCount() == 0)
        {
            Debug.LogError($"[{gameObject.name}] 路径 {newPathCreator.name} 没有路径点");
            return;
        }

        Debug.Log($"[{gameObject.name}] 切换到路径: {newPathCreator.name} (包含 {newPathCreator.GetPathPointCount()} 个点)");

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

            // 修改这里：使用 GetPathPointPosition 方法获取第一个路径点位置
            if (currentPathCreator.GetPathPointCount() > 0)
            {
                Vector3 targetPosition = currentPathCreator.GetPathPointPosition(0);
                NavMeshHit hit;
                if (NavMesh.SamplePosition(targetPosition, out hit, 5f, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                }
                else
                {
                    agent.SetDestination(targetPosition);
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