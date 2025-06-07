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
    private float reachDistance = 1.1f; // 到达点的距离阈值
    private NavMeshAgent agent;
    private bool isWaitingAtPoint = false; // 新增：标记是否正在等待

    // 调试设置
    public bool ShowDebugLogs { get; set; } = false; // 默认关闭调试日志

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
        
        // 默认开启调试日志以便诊断问题
        ShowDebugLogs = false; // 关闭详细调试日志
        // Debug.Log($"[{gameObject.name}] NPCPathManager初始化完成，调试日志已开启，原始速度: {agent.speed}");
    }

    void Start()
    {
        // 初始化路径
        InitializeStartingPath();
        
        // 添加游戏状态调试信息
        // Debug.Log($"[{gameObject.name}] NPCPathManager.Start() - 当前游戏状态: {GameState.Instance.GetCurrentState()}");
        // Debug.Log($"[{gameObject.name}] NPCPathManager.Start() - 当前时间缩放: {Time.timeScale}");
        
        // 订阅游戏状态变化事件
        if (EventCenter.Instance != null)
        {
            EventCenter.Instance.Subscribe(GameState.EventNames.STATE_CHANGED, OnGameStateChanged);
        }
    }

    void Update()
    {
        if (pathProcessingPaused || controller.Data == null)
            return;
            
        // 检查时间缩放，如果几乎为0则暂停路径跟随
        if (Time.timeScale < 0.001f)
        {
            // if (ShowDebugLogs && Time.frameCount % 120 == 0) // 每2秒输出一次
            //     Debug.Log($"[{gameObject.name}] 时间缩放过小 ({Time.timeScale})，暂停路径跟随");
            return;
        }

        // 路径移动
        FollowCurrentPath();
    }

    void OnDisable()
    {
        // NPCPathManager只负责路径管理，不需要订阅故事时间事件
        // 时间控制由独立的NPCTimeController负责
        
        // 取消订阅事件
        if (EventCenter.Instance != null)
        {
            EventCenter.Instance.Unsubscribe(GameState.EventNames.STATE_CHANGED, OnGameStateChanged);
        }
    }

    // 路径跟随逻辑
    private void FollowCurrentPath()
    {
        if (currentPathCreator == null || agent == null || isWaitingAtPoint)
        {
            // if (currentPathCreator == null && ShowDebugLogs)
            //     Debug.LogWarning($"[{gameObject.name}] 路径跟随失败：currentPathCreator为空");
            // if (agent == null && ShowDebugLogs)
            //     Debug.LogWarning($"[{gameObject.name}] 路径跟随失败：agent为空");
            // if (isWaitingAtPoint && ShowDebugLogs)
            //     Debug.Log($"[{gameObject.name}] 正在等待点，暂停路径跟随");
            return;
        }

        if (currentPathPointIndex >= currentPathCreator.GetPathPointCount())
        {
            // if (ShowDebugLogs)
            //     Debug.LogWarning($"[{gameObject.name}] 路径点索引超出范围: {currentPathPointIndex}/{currentPathCreator.GetPathPointCount()}");
            return;
        }

        // 获取当前目标点位置
        Vector3 targetPosition = currentPathCreator.GetPathPointPosition(currentPathPointIndex);

        // 确保代理未停止
        if (agent.isStopped)
        {
            // if (ShowDebugLogs)
            //     Debug.Log($"[{gameObject.name}] Agent已停止，重启移动");
            agent.isStopped = false;
        }

        // 检查当前目标是否有效
        if (agent.pathPending)
        {
            // if (ShowDebugLogs)
            //     Debug.Log($"[{gameObject.name}] 路径计算中，等待...");
            return; // 路径计算中，等待
        }

        // 使用容差比较避免浮点精度问题导致的不断重新设置目标
        float destinationDistanceThreshold = 0.5f; // 目标位置比较的容差
        bool needSetDestination = Vector3.Distance(agent.destination, targetPosition) > destinationDistanceThreshold;
        
        // 设置目标（如果需要）
        if (needSetDestination)
        {
            // if (ShowDebugLogs)
            //     Debug.Log($"[{gameObject.name}] 设置新目标点: 点{currentPathPointIndex}, 位置: {targetPosition}, 当前destination距离: {Vector3.Distance(agent.destination, targetPosition):F2}");
            
            // 检查agent是否在NavMesh上
            if (!agent.isOnNavMesh)
            {
                // Debug.LogWarning($"[{gameObject.name}] Agent不在NavMesh上，尝试修复位置");
                NavMeshHit hit;
                if (NavMesh.SamplePosition(transform.position, out hit, 5f, NavMesh.AllAreas))
                {
                    transform.position = hit.position;
                    // Debug.Log($"[{gameObject.name}] 已将Agent移动到NavMesh上: {hit.position}");
                }
                else
                {
                    // Debug.LogError($"[{gameObject.name}] 无法找到附近的NavMesh位置");
                    return;
                }
            }
            
            // 检查目标位置是否在NavMesh上
            NavMeshHit targetHit;
            Vector3 validTarget = targetPosition;
            if (!NavMesh.SamplePosition(targetPosition, out targetHit, 5f, NavMesh.AllAreas))
            {
                // Debug.LogWarning($"[{gameObject.name}] 目标位置不在NavMesh上，寻找最近的有效位置");
                if (NavMesh.SamplePosition(targetPosition, out targetHit, 10f, NavMesh.AllAreas))
                {
                    validTarget = targetHit.position;
                    // Debug.Log($"[{gameObject.name}] 使用修正的目标位置: {validTarget}");
                }
                else
                {
                    // Debug.LogError($"[{gameObject.name}] 无法找到有效的目标位置");
                    return;
                }
            }
            
            bool setResult = agent.SetDestination(validTarget);
            // if (ShowDebugLogs)
            //     Debug.Log($"[{gameObject.name}] SetDestination结果: {setResult}, pathStatus: {agent.pathStatus}");
            
            // 如果设置失败，再次尝试
            if (!setResult || agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                // Debug.LogWarning($"[{gameObject.name}] 目标设置失败，尝试重新计算路径");
                agent.SetDestination(validTarget);
                // Debug.Log($"[{gameObject.name}] 重新设置后 - pathStatus: {agent.pathStatus}");
            }
        }

        // 检查是否到达当前点
        float distanceToTarget = Vector3.Distance(transform.position, targetPosition);

        if (distanceToTarget < reachDistance)
        {
            // 记录已达到的点
            int reachedPointIndex = currentPathPointIndex;
            Debug.Log($"[{gameObject.name}] 到达路径点 {reachedPointIndex}"); // 保留关键信息

            // 更新最后到达的路径点索引
            lastReachedPointIndex = reachedPointIndex;

            // 触发事件
            // Debug.Log($"[{gameObject.name}] 触发路径点到达事件: 点{reachedPointIndex}");
            OnPathPointReached?.Invoke(reachedPointIndex);

            // 处理等待时间
            float waitTime = GetWaitTimeForPathPoint(reachedPointIndex);
            // Debug.Log($"[{gameObject.name}] 路径点{reachedPointIndex}的等待时间: {waitTime}秒");
            
            if (waitTime > 0)
            {
                // Debug.Log($"[{gameObject.name}] 开始等待协程，等待时间: {waitTime}秒");
                StartCoroutine(WaitAndProceedToNextPoint(waitTime));
                return;
            }

            // 更新到下一个点 - 移除对ActionExecutor队列的检查
            // NPCActionExecutor会通过自己的移动控制来处理Action执行期间的停止
            // Debug.Log($"[{gameObject.name}] 无路径等待时间，立即前进到下一个点");
            AdvanceToNextPathPoint();
        }
        else
        {
            // 添加移动状态调试信息
            // if (ShowDebugLogs && Time.frameCount % 60 == 0) // 每秒输出一次
            // {
            //     Debug.Log($"[{gameObject.name}] 移动中 - 目标点{currentPathPointIndex}, 距离: {distanceToTarget:F2}, 速度: {agent.velocity.magnitude:F2}, remainingDistance: {agent.remainingDistance:F2}");
            //     Debug.Log($"[{gameObject.name}] Agent状态 - hasPath: {agent.hasPath}, pathStatus: {agent.pathStatus}, destination: {agent.destination}");
            // }
        }
    }

    // 获取指定路径点的waitTime（优先事件动作，其次pathActions）
    // 只取第一个匹配的 ActionData 的 waitTime，其它同路径点的 waitTime 会被忽略
    private float GetWaitTimeForPathPoint(int pathPointIndex)
    {
        // 检查路径配置中的停留时间设置
        PathConfig currentPath = GetCurrentPathConfig();
        if (currentPath != null)
        {
            // 使用路径配置中的停留时间
            float pathStopTime = currentPath.GetPathPointStopTime(pathPointIndex);
            if (pathStopTime > 0)
            {
                Debug.Log($"[{gameObject.name}] 路径点{pathPointIndex}配置停留时间: {pathStopTime}秒");
                return pathStopTime;
            }
        }
        
        // 移除时间控制等待逻辑！
        // 时间控制点的作用是确保NPC准时到达，而不是等待到指定时间
        // 如果NPC提前到达时间控制点，应该立即寻找下一个目标
        
        return 0f; // 路径本身不需要等待时间
    }

    // 等待并前进到下一个点
    private IEnumerator WaitAndProceedToNextPoint(float waitTime)
    {
        isWaitingAtPoint = true;
        Debug.Log($"[{gameObject.name}] 开始在路径点停留 {waitTime} 秒");
        
        // 记录停留前的速度，以便停留结束后恢复
        float speedBeforeStop = agent != null ? agent.speed : 0f;
        
        // 直接停止Agent，不通过controller以避免与ActionExecutor冲突
        if (agent != null)
        {
            agent.isStopped = true;
        }
        
        yield return new WaitForSeconds(waitTime);
        
        // 恢复Agent移动
        if (agent != null)
        {
            agent.isStopped = false;
            
            // 关键修复：恢复停留前的速度，保持时间控制器的设置
            if (speedBeforeStop > 0f)
            {
                agent.speed = speedBeforeStop;
                Debug.Log($"[{gameObject.name}] 停留结束，恢复移动速度为: {speedBeforeStop:F1}m/s");
            }
        }
        
        Debug.Log($"[{gameObject.name}] 停留结束，继续移动");
        isWaitingAtPoint = false;
        AdvanceToNextPathPoint();
    }

    // 前进到下一个路径点
    private void AdvanceToNextPathPoint()
    {
        int reachedPointIndex = currentPathPointIndex;
        int oldIndex = currentPathPointIndex;
        currentPathPointIndex++;

        Debug.Log($"[{gameObject.name}] 前进到下一个路径点: {oldIndex} -> {currentPathPointIndex}");

        // 处理路径循环
        if (currentPathCreator != null && currentPathPointIndex >= currentPathCreator.GetPathPointCount() && currentPathCreator.closedPath)
        {
            Debug.Log($"[{gameObject.name}] 路径循环，重置到第一个点");
            currentPathPointIndex = 0;
        }

        // 🔄 路径切换现在完全由TimeController的时间控制处理，不再依赖物理位置检测
        // 如果到达了路径的最后一个物理点，继续等待时间控制系统的指令
        if (currentPathCreator != null && currentPathPointIndex >= currentPathCreator.GetPathPointCount() && !currentPathCreator.closedPath)
        {
            Debug.Log($"[{gameObject.name}] 🎯 到达路径物理终点，等待时间控制系统处理路径切换");
            
            // 停止Agent移动，等待TimeController的时间控制触发路径切换
            if (agent != null)
            {
                agent.isStopped = true;
            }
            
            // 不再主动处理路径完成，完全交给TimeController处理
            Debug.Log($"[{gameObject.name}] ⏰ 路径切换现在由时间控制系统负责");
        }
        else if (currentPathCreator != null)
        {
            // 确保继续移动到下一个点
            Debug.Log($"[{gameObject.name}] 准备移动到下一个路径点: {currentPathPointIndex}");
            if (agent.isStopped)
            {
                Debug.Log($"[{gameObject.name}] Agent被停止，重新启动");
                agent.isStopped = false;
            }
        }
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
        }

        if (string.IsNullOrEmpty(initialPath))
        {
            if (ShowDebugLogs)
                Debug.LogWarning($"[{gameObject.name}] 未设置初始路径");
            return;
        }

        // 尝试获取路径
        MultiPointPathCreator pathCreator = PathRegistry.GetPathCreatorByID(initialPath);
        if (pathCreator != null)
        {
            // 确保路径点数据已生成
            if (pathCreator.pathPointPositions.Count == 0 && pathCreator.controlPoints.Count >= 2)
            {
                pathCreator.GeneratePathPoints();
            }

            // 切换到该路径
            SwitchToPath(pathCreator);
        }
        else
        {
            if (ShowDebugLogs)
                Debug.LogError($"[{gameObject.name}] 找不到初始路径: {initialPath}");
        }
    }

    // 处理路径完成
    private void ProcessPathCompletion()
    {
        Debug.Log($"[{gameObject.name}] 开始处理路径完成");
        
        if (currentPathCreator == null || controller.Data == null)
        {
            Debug.LogWarning($"[{gameObject.name}] 路径数据不完整，无法确定下一步 - PathCreator: {currentPathCreator != null}, Data: {controller.Data != null}");
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

        Debug.Log($"[{gameObject.name}] 当前路径配置: {currentPathConfig.pathName} (ID: {currentPathConfig.pathID}), 下一路径数量: {currentPathConfig.nextPaths.Count}");

        // 检查是否有下一条路径
        if (currentPathConfig.nextPaths.Count == 0)
        {
            Debug.Log($"[{gameObject.name}] 没有下一条路径，停止移动");
            controller.StopMovement(true);
            return;
        }

        // 根据分数选择下一条路径 - 添加详细的调试信息
        float currentScore = controller.Data.currentScore;
        Debug.Log($"[{gameObject.name}] 当前分数: {currentScore}，开始选择下一条路径");
        
        // 详细显示所有可选路径分支
        Debug.Log($"[{gameObject.name}] 可选路径分支:");
        for (int i = 0; i < currentPathConfig.nextPaths.Count; i++)
        {
            var branch = currentPathConfig.nextPaths[i];
            bool scoreMatch = branch.IsScoreInRange(currentScore);
            Debug.Log($"[{gameObject.name}]   分支{i}: 路径ID={branch.nextPathID}, 分数区间=[{branch.minScore}, {branch.maxScore}), 当前分数匹配={scoreMatch}");
        }
        
        string nextPathID = currentPathConfig.SelectNextPathByScore(currentScore);
        
        if (string.IsNullOrEmpty(nextPathID))
        {
            Debug.LogError($"[{gameObject.name}] ❌ 路径选择失败：SelectNextPathByScore返回空路径ID！");
            Debug.LogError($"[{gameObject.name}] 📋 这通常是因为路径分支配置错误，请检查路径配置中的nextPathID设置");
            Debug.LogError($"[{gameObject.name}] 🔧 建议：检查当前路径({currentPathConfig.pathName})的分支配置，确保所有分支都设置了有效的nextPathID");
            controller.StopMovement(true);
            return;
        }

        Debug.Log($"[{gameObject.name}] 选择的下一条路径ID: {nextPathID}");
        
        // 🔍 关键检查：防止选择到自己
        if (nextPathID == currentPathCreator.pathID)
        {
            Debug.LogError($"[{gameObject.name}] ❌ 检测到循环路径选择！下一路径ID({nextPathID})与当前路径ID({currentPathCreator.pathID})相同，这会导致无限循环！");
            Debug.LogError($"[{gameObject.name}] 📋 路径分支配置错误，请检查路径配置中的分支设置");
            controller.StopMovement(true);
            return;
        }
        
        // 🔍 检查路径ID是否为空字符串（这是常见的配置错误）
        if (nextPathID.Trim() == "")
        {
            Debug.LogError($"[{gameObject.name}] ❌ 检测到空路径ID！选中的分支路径ID为空字符串");
            Debug.LogError($"[{gameObject.name}] 📋 这是路径分支配置错误，请在Inspector中设置正确的nextPathID");
            controller.StopMovement(true);
            return;
        }

        // 切换到下一条路径
        MultiPointPathCreator nextPathCreator = PathRegistry.GetPathCreatorByID(nextPathID);
        if (nextPathCreator != null)
        {
            Debug.Log($"[{gameObject.name}] 成功找到下一条路径，开始切换: {nextPathCreator.name}");
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
            if (ShowDebugLogs)
                Debug.LogError($"[{gameObject.name}] 尝试切换到空路径");
            return;
        }

        // 修改这里：使用 GetPathPointCount() 而不是 pathPointsParent.childCount
        if (newPathCreator.GetPathPointCount() == 0)
        {
            if (ShowDebugLogs)
                Debug.LogError($"[{gameObject.name}] 路径 {newPathCreator.name} 没有路径点");
            return;
        }

        Debug.Log($"[{gameObject.name}] 切换到新路径: {newPathCreator.pathID}");

        // 设置新路径
        currentPathCreator = newPathCreator;
        currentPathPoints = newPathCreator.pathPointsParent;
        currentPathPointIndex = 0;
        lastReachedPointIndex = -1;
        stuckTime = 0;

        // 关键修复：更新当前路径配置
        CurrentPathConfig = controller.Data?.FindPathById(newPathCreator.pathID);
        if (CurrentPathConfig != null)
        {
            Debug.Log($"[{gameObject.name}] 路径配置已更新: {CurrentPathConfig.pathName}, 起始时间: {CurrentPathConfig.pathStartStoryTime}s");
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] 无法找到路径配置: {newPathCreator.pathID}");
        }

        // 通知事件管理器
        eventManager.SetCurrentPath(newPathCreator.pathID, currentPathPoints);

        // 关键修复：通知时间控制器路径已切换，需要重新初始化
        NPCTimeController timeController = GetComponent<NPCTimeController>();
        if (timeController != null)
        {
            Debug.Log($"[{gameObject.name}] 通知时间控制器路径切换，重新初始化时间控制");
            // 使用协程延迟通知，确保路径切换完全完成后再初始化时间控制
            StartCoroutine(NotifyTimeControllerPathSwitched());
        }

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
            if (ShowDebugLogs)
                Debug.LogError($"[{gameObject.name}] 无法获取 NavMeshAgent 组件，导航失败");
        }
    }

    /// <summary>
    /// 延迟通知时间控制器路径已切换
    /// </summary>
    private System.Collections.IEnumerator NotifyTimeControllerPathSwitched()
    {
        // 等待一小段时间，确保路径切换完全完成
        yield return new WaitForSeconds(0.1f);
        
        NPCTimeController timeController = GetComponent<NPCTimeController>();
        if (timeController != null)
        {
            Debug.Log($"[{gameObject.name}] 执行时间控制器路径切换初始化");
            
            // 重新初始化时间控制系统
            timeController.OnPathSwitched();
        }
    }
    
    /// <summary>
    /// [已废弃] 检查路径结束条件 - 现在由TimeController负责时间控制
    /// </summary>
    [System.Obsolete("路径切换现在完全由TimeController的时间控制系统处理")]
    private bool CheckPathEndConditions()
    {
        // 保留方法以避免编译错误，但标记为废弃
        Debug.LogWarning($"[{gameObject.name}] ⚠️ CheckPathEndConditions已废弃，路径切换由TimeController处理");
        return true;
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
            if (ShowDebugLogs)
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

    /// <summary>
    /// 设置当前路径点索引（用于时间控制器瞬移）
    /// </summary>
    public void SetCurrentPathPointIndex(int pointIndex)
    {
        lastReachedPointIndex = pointIndex;
        currentPathPointIndex = pointIndex + 1; // 下一个要移动到的点
        Debug.Log($"[{gameObject.name}] 时间控制器设置路径点索引: 当前点={lastReachedPointIndex}, 下一目标点={currentPathPointIndex}");
    }

    /// <summary>
    /// 由时间控制器触发的路径完成（跳过物理位置检查）
    /// </summary>
    public void ForceCompletePathFromTimeController()
    {
        Debug.Log($"[{gameObject.name}] 🔄 时间控制器触发路径完成，跳过物理位置检查");
        
        // 停止当前移动
        if (agent != null)
        {
            agent.isStopped = true;
        }
        
        // 直接处理路径完成
        ProcessPathCompletion();
    }
    
    /// <summary>
    /// 停止NPC移动 (用于Action执行期间)
    /// </summary>
    public void StopMovement()
    {
        Debug.Log($"[{gameObject.name}] StopMovement() 被调用");
        if (agent != null)
        {
            agent.isStopped = true;
            Debug.Log($"[{gameObject.name}] Agent已停止移动");
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] StopMovement() 调用失败：Agent为空");
        }
    }

    /// <summary>
    /// 恢复NPC移动 (Action执行完毕后)
    /// </summary>
    public void ResumeMovement()
    {
        Debug.Log($"[{gameObject.name}] ResumeMovement() 被调用");
        if (agent != null && !pathProcessingPaused)
        {
            agent.isStopped = false;
            Debug.Log($"[{gameObject.name}] Agent已恢复移动");
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] ResumeMovement() 调用失败：Agent={agent != null}, pathProcessingPaused={pathProcessingPaused}");
        }
    }

    // 游戏状态变化处理
    private void OnGameStateChanged()
    {
        GameState.State currentState = GameState.Instance.GetCurrentState();
        Debug.Log($"[{gameObject.name}] 游戏状态变化: {currentState}, 时间缩放: {Time.timeScale}");
        
        // 如果切换到游戏状态，确保agent正常工作
        if (currentState == GameState.State.Act1 || 
            currentState == GameState.State.Act2 || 
            currentState == GameState.State.Act3)
        {
            Debug.Log($"[{gameObject.name}] 进入游戏状态，检查移动组件");
            
            // 确保 NavMeshAgent 启用
            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;
                Debug.Log($"[{gameObject.name}] 重新启用NavMeshAgent");
            }
            
            // 如果有路径但被停止了，重新启动
            if (agent != null && agent.isStopped && currentPathCreator != null)
            {
                agent.isStopped = false;
                Debug.Log($"[{gameObject.name}] 重新启动移动");
            }
        }
    }
    
    #region 调试和测试方法
    
    /// <summary>
    /// [已废弃] 手动检查路径结束条件 - 现在由TimeController负责
    /// </summary>
    [ContextMenu("检查路径结束条件 [已废弃]")]
    public void TestCheckPathEndConditions()
    {
        Debug.LogWarning($"[{gameObject.name}] ⚠️ 路径结束条件检查已废弃，现在由TimeController的时间控制系统处理路径切换");
        Debug.LogWarning($"[{gameObject.name}] 💡 请使用TimeController的相关调试菜单来查看时间控制状态");
    }
    
    /// <summary>
    /// 强制结束当前路径（测试用）
    /// </summary>
    [ContextMenu("强制结束当前路径")]
    public void TestForceEndCurrentPath()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning($"[{gameObject.name}] 强制结束路径只能在游戏运行时使用");
            return;
        }
        
        Debug.Log($"[{gameObject.name}] 手动强制结束当前路径");
        ProcessPathCompletion();
    }
    
    /// <summary>
    /// 显示路径时间连续性信息（调试用）
    /// </summary>
    [ContextMenu("显示路径时间连续性")]
    public void ShowPathTimeContinuity()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning($"[{gameObject.name}] 路径时间连续性查看只能在游戏运行时使用");
            return;
        }
        
        PathConfig currentPathConfig = GetCurrentPathConfig();
        if (currentPathConfig == null)
        {
            Debug.Log($"[{gameObject.name}] 当前路径配置为空");
            return;
        }
        
        NPCData npcData = controller?.Data;
        float currentScore = npcData?.currentScore ?? 0f;
        
        Debug.Log($"[{gameObject.name}] === 路径时间连续性信息 ===");
        Debug.Log($"当前路径: {currentPathConfig.pathName}");
        Debug.Log($"路径开始时间: {currentPathConfig.pathStartStoryTime:F1}s");
        Debug.Log($"手动设置结束时间: {(currentPathConfig.pathEndStoryTime > 0 ? currentPathConfig.pathEndStoryTime.ToString("F1") + "s" : "未设置(0)")}");
        
        float actualEndTime = currentPathConfig.GetActualEndTime(npcData, currentScore);
        Debug.Log($"实际结束时间: {(actualEndTime > 0 ? actualEndTime.ToString("F1") + "s" : "无限制")}");
        
        // 显示下一个路径信息
        string nextPathID = currentPathConfig.SelectNextPathByScore(currentScore);
        if (!string.IsNullOrEmpty(nextPathID) && npcData != null)
        {
            PathConfig nextPath = npcData.FindPathById(nextPathID);
            if (nextPath != null)
            {
                Debug.Log($"下一个路径: {nextPath.pathName}");
                Debug.Log($"下一个路径开始时间: {nextPath.pathStartStoryTime:F1}s");
                
                if (actualEndTime > 0 && nextPath.pathStartStoryTime > 0)
                {
                    float gap = nextPath.pathStartStoryTime - actualEndTime;
                    if (Mathf.Abs(gap) < 0.1f)
                    {
                        Debug.Log($"✅ 时间连续性良好（间隔: {gap:F1}s）");
                    }
                    else if (gap > 0)
                    {
                        Debug.Log($"⚠️ 路径间有时间间隔: {gap:F1}s");
                    }
                    else
                    {
                        Debug.Log($"❌ 路径时间重叠: {-gap:F1}s");
                    }
                }
            }
        }
        else
        {
            Debug.Log($"下一个路径: 无（路径结束）");
        }
    }
    
    /// <summary>
    /// 诊断当前路径选择问题（调试用）
    /// </summary>
    [ContextMenu("诊断路径选择问题")]
    public void DiagnosePathSelection()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning($"[{gameObject.name}] 路径选择诊断只能在游戏运行时使用");
            return;
        }
        
        if (currentPathCreator == null || controller.Data == null)
        {
            Debug.LogError($"[{gameObject.name}] 缺少关键组件: PathCreator={currentPathCreator != null}, Data={controller.Data != null}");
            return;
        }
        
        PathConfig currentPathConfig = controller.Data.FindPathById(currentPathCreator.pathID);
        if (currentPathConfig == null)
        {
            Debug.LogError($"[{gameObject.name}] 找不到当前路径配置: {currentPathCreator.pathID}");
            return;
        }
        
        float currentScore = controller.Data.currentScore;
        
        Debug.Log($"[{gameObject.name}] === 路径选择诊断 ===");
        Debug.Log($"当前路径: {currentPathConfig.pathName} (ID: {currentPathConfig.pathID})");
        Debug.Log($"当前分数: {currentScore}");
        Debug.Log($"可选分支数量: {currentPathConfig.nextPaths.Count}");
        
        if (currentPathConfig.nextPaths.Count == 0)
        {
            Debug.Log($"✅ 这是终点路径，无下一路径");
            return;
        }
        
        // 详细分析每个分支
        for (int i = 0; i < currentPathConfig.nextPaths.Count; i++)
        {
            var branch = currentPathConfig.nextPaths[i];
            bool scoreMatch = branch.IsScoreInRange(currentScore);
            Debug.Log($"分支 {i}: 路径ID={branch.nextPathID}, 分数区间=[{branch.minScore}, {branch.maxScore}), 匹配={scoreMatch}");
            
            // 检查是否会导致循环
            if (branch.nextPathID == currentPathCreator.pathID)
            {
                Debug.LogError($"⚠️ 分支 {i} 会导致循环路径选择！");
            }
            
            // 检查路径是否存在
            MultiPointPathCreator targetPath = PathRegistry.GetPathCreatorByID(branch.nextPathID);
            if (targetPath == null)
            {
                Debug.LogError($"⚠️ 分支 {i} 的目标路径不存在: {branch.nextPathID}");
            }
            else
            {
                Debug.Log($"   目标路径: {targetPath.name}");
            }
        }
        
        // 显示选择结果
        string selectedPathID = currentPathConfig.SelectNextPathByScore(currentScore);
        Debug.Log($"选择结果: {selectedPathID}");
        
        if (selectedPathID == currentPathCreator.pathID)
        {
            Debug.LogError($"❌ 路径选择结果导致循环！这是问题的根源！");
        }
    }
    
    #endregion
}