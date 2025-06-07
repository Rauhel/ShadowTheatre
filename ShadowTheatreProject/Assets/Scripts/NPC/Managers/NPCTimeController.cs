using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

/// <summary>
/// NPC时间控制器 - 独立负责基于故事时间的NPC移动速度调整
/// 与NPCPathManager和NPCActionExecutor解耦
/// </summary>
[RequireComponent(typeof(NPCController))]
[RequireComponent(typeof(NPCPathManager))]
[RequireComponent(typeof(NavMeshAgent))]
public class NPCTimeController : MonoBehaviour
{
    /// <summary>
    /// 调试日志级别
    /// </summary>
    public enum DebugLogLevel
    {
        Silent = 0,      // 静默 - 只输出错误和警告
        Monitor = 1,     // 监控 - 输出重要状态变化
        Detailed = 2     // 详细 - 输出所有调试信息
    }
    
    [Header("时间控制设置")]
    [SerializeField] private bool timeControlEnabled = true;
    [SerializeField] private float speedMultiplierMin = 0.001f; // 最小速度倍数
    [SerializeField] private float speedMultiplierMax = 100f;  // 最大速度倍数
    [SerializeField] private float teleportThreshold = 60f;  // 超过这个速度就瞬移
    
    [Header("调试设置")]
    [SerializeField] private DebugLogLevel debugLogLevel = DebugLogLevel.Monitor;
    
    // 组件引用
    private NPCController controller;
    private NPCPathManager pathManager;
    private NavMeshAgent agent;
    private float originalMoveSpeed;
    
    // 时间控制状态
    private PathTimePoint currentTargetTimePoint;
    private bool hasInitialized = false;
    private float lastSetSpeed = -1f; // 记录上次设置的速度，避免重复日志
    private float lastUpdateTime = 0f; // 记录上次Update的时间
    private float lastPathSwitchTime = 0f; // 记录上次路径切换的时间
    
    // 私有委托引用，用于正确的事件订阅和取消订阅
    private System.Action storyTimeUpdatedHandler;
    private System.Action actTimeStartedHandler;
    
    #region 调试日志方法
    
    /// <summary>
    /// 输出详细级别日志
    /// </summary>
    private void LogDetailed(string message)
    {
        // 添加null检查，防止已销毁对象访问
        if (this == null || gameObject == null) return;
        
        if (debugLogLevel >= DebugLogLevel.Detailed)
        {
            Debug.Log($"[{gameObject.name} TimeController] {message}");
        }
    }
    
    /// <summary>
    /// 输出监控级别日志
    /// </summary>
    private void LogMonitor(string message)
    {
        // 添加null检查，防止已销毁对象访问
        if (this == null || gameObject == null) return;
        
        if (debugLogLevel >= DebugLogLevel.Monitor)
        {
            Debug.Log($"[{gameObject.name} TimeController] {message}");
        }
    }
    
    /// <summary>
    /// 输出警告日志（所有级别都显示）
    /// </summary>
    private void LogWarning(string message)
    {
        // 添加null检查，防止已销毁对象访问
        if (this == null || gameObject == null) return;
        
        if (debugLogLevel >= DebugLogLevel.Silent)
        {
            Debug.LogWarning($"[{gameObject.name} TimeController] {message}");
        }
    }
    
    /// <summary>
    /// 输出错误日志（所有级别都显示）
    /// </summary>
    private void LogError(string message)
    {
        // 添加null检查，防止已销毁对象访问
        if (this == null || gameObject == null) return;
        
        Debug.LogError($"[{gameObject.name} TimeController] {message}");
    }
    
    #endregion
    
    void Awake()
    {
        controller = GetComponent<NPCController>();
        pathManager = GetComponent<NPCPathManager>();
        agent = GetComponent<NavMeshAgent>();
        
        originalMoveSpeed = agent.speed;
        LogMonitor($"NPCTimeController初始化，原始速度: {originalMoveSpeed}");
    }
    
    void Start()
    {
        // 创建委托实例
        storyTimeUpdatedHandler = OnStoryTimeUpdated;
        actTimeStartedHandler = OnActTimeStarted;
        
        // 订阅故事时间更新事件
        if (EventCenter.Instance != null)
        {
            EventCenter.Instance.Subscribe(GameState.EventNames.STORY_TIME_UPDATED, storyTimeUpdatedHandler);
            EventCenter.Instance.Subscribe(GameState.EventNames.ACT_TIME_STARTED, actTimeStartedHandler);
        }
        
        // 订阅路径点到达事件
        if (pathManager != null)
        {
            pathManager.OnPathPointReached += OnPathPointReached;
        }
        
        InitializeTimeControl();
        
        // 延迟执行初始瞬移（只在游戏开始时执行一次）
        StartCoroutine(PerformInitialTeleportAfterDelay());
        
        // 延迟强制触发初始速度计算（确保所有系统初始化完成后）
        StartCoroutine(ForceInitialSpeedCalculation());
    }
    
    void Update()
    {
        // 持续监控时间控制，确保NPC能够正确调整速度
        if (timeControlEnabled && hasInitialized)
        {
            // 修复：只要时间控制启用，就持续检查是否需要更新目标和速度
            if (Time.time - lastUpdateTime >= 0.5f)
            {
                // 关键修复：定期重新查找目标，以防错过路径终点目标
                if (currentTargetTimePoint == null)
                {
                    LogDetailed("Update中检测到无目标，重新查找目标时间点");
                    UpdateTargetTimePoint();
                }
                
                // 如果有目标，更新速度
                if (currentTargetTimePoint != null)
                {
                    UpdateSpeedBasedOnTime();
                }
                
                lastUpdateTime = Time.time;
            }
        }
        
        // 额外的速度验证：确保速度不会被意外重置为0或异常值
        if (timeControlEnabled && hasInitialized && agent != null)
        {
            if (agent.speed <= 0.001f && currentTargetTimePoint != null)
            {
                LogWarning($"检测到异常速度{agent.speed:F3}，强制重新计算");
                UpdateSpeedBasedOnTime();
            }
        }
    }
    
    void OnDisable()
    {
        // 取消事件订阅
        if (EventCenter.Instance != null && storyTimeUpdatedHandler != null && actTimeStartedHandler != null)
        {
            EventCenter.Instance.Unsubscribe(GameState.EventNames.STORY_TIME_UPDATED, storyTimeUpdatedHandler);
            EventCenter.Instance.Unsubscribe(GameState.EventNames.ACT_TIME_STARTED, actTimeStartedHandler);
        }
        
        if (pathManager != null)
        {
            pathManager.OnPathPointReached -= OnPathPointReached;
        }
    }
    
    void OnDestroy()
    {
        // 确保事件订阅被彻底清理（防止OnDisable未被调用的情况）
        if (EventCenter.Instance != null)
        {
            if (storyTimeUpdatedHandler != null)
            {
                EventCenter.Instance.Unsubscribe(GameState.EventNames.STORY_TIME_UPDATED, storyTimeUpdatedHandler);
                storyTimeUpdatedHandler = null;
            }
            if (actTimeStartedHandler != null)
            {
                EventCenter.Instance.Unsubscribe(GameState.EventNames.ACT_TIME_STARTED, actTimeStartedHandler);
                actTimeStartedHandler = null;
            }
        }
        
        if (pathManager != null)
        {
            pathManager.OnPathPointReached -= OnPathPointReached;
        }
    }
    
    private void OnStoryTimeUpdated()
    {
        // 添加null检查，防止已销毁对象访问
        if (this == null || gameObject == null) return;
        
        if (timeControlEnabled && hasInitialized)
        {
            UpdateSpeedBasedOnTime();
        }
    }
    
    private void OnActTimeStarted()
    {
        // 添加null检查，防止已销毁对象访问
        if (this == null || gameObject == null) return;
        
        Debug.Log($"[{gameObject.name}] 收到幕开始事件，准备重新初始化时间控制");
        
        // 使用协程延迟执行，避免在状态切换过程中执行瞬移
        StartCoroutine(HandleActTimeStartedAfterDelay());
    }
    
    /// <summary>
    /// 延迟处理幕开始事件
    /// </summary>
    private System.Collections.IEnumerator HandleActTimeStartedAfterDelay()
    {
        // 等待一小段时间，确保状态切换完成
        yield return new WaitForSeconds(0.05f);
        
        // 重新初始化时间控制
        InitializeTimeControl();
        
        // 注意：不在这里执行瞬移，因为PerformInitialTeleportAfterDelay已经处理了
        Debug.Log($"[{gameObject.name}] 幕开始事件处理完成，时间控制系统已重新初始化");
    }
    
    private void OnPathPointReached(int pathPointIndex)
    {
        Debug.Log($"[{gameObject.name}] 时间控制器收到路径点到达事件：点{pathPointIndex}");
        
        // 检查当前路径点是否是时间控制点
        PathConfig currentPath = pathManager.GetCurrentPathConfig();
        bool isTimeControlPoint = false;
        
        if (currentPath != null && currentPath.timePoints != null)
        {
            foreach (var timePoint in currentPath.timePoints)
            {
                if (timePoint.pathPointIndex == pathPointIndex)
                {
                    isTimeControlPoint = true;
                    break;
                }
            }
        }
        
        // 只在时间控制点才进行速度计算
        if (!isTimeControlPoint)
        {
            Debug.Log($"[{gameObject.name}] 路径点{pathPointIndex}不是时间控制点，跳过速度计算");
            return;
        }
        
        // 检查是否到达了当前的目标时间点
        if (currentTargetTimePoint != null && pathPointIndex == currentTargetTimePoint.pathPointIndex)
        {
            Debug.Log($"[{gameObject.name}] 到达目标时间控制点{pathPointIndex}，标记完成并查找下一个目标");
            
            // 关键修复：立即清空当前目标，避免重复选择
            currentTargetTimePoint = null;
        }
        
        // 路径点到达时，查找下一个目标时间点
        UpdateTargetTimePoint();
        
        // 关键修复：无论是否找到目标，都需要更新速度
        // UpdateTargetTimePoint()可能创建了虚拟的路径终点目标，或者设置为null
        if (timeControlEnabled && hasInitialized)
        {
            if (currentTargetTimePoint != null)
            {
                Debug.Log($"[{gameObject.name}] 找到新目标（路径点{currentTargetTimePoint.pathPointIndex}），计算速度");
                UpdateSpeedBasedOnTime();
            }
            else
            {
                // 真正没有任何目标了，恢复原始速度
                if (Mathf.Abs(agent.speed - originalMoveSpeed) > 0.1f)
                {
                    agent.speed = originalMoveSpeed;
                    Debug.Log($"[{gameObject.name}] 所有目标已完成，恢复原始速度: {originalMoveSpeed}");
                }
            }
        }
    }
    
    /// <summary>
    /// 初始化时间控制系统
    /// </summary>
    private void InitializeTimeControl()
    {
        if (!timeControlEnabled) 
        {
            agent.speed = originalMoveSpeed;
            Debug.Log($"[{gameObject.name}] 时间控制已禁用，使用原始速度: {originalMoveSpeed}");
            return;
        }
        
        PathConfig currentPath = pathManager.GetCurrentPathConfig();
        if (currentPath != null)
        {
            Debug.Log($"[{gameObject.name}] 当前路径: {currentPath.pathName}, 起始故事时间: {currentPath.pathStartStoryTime}s, 时间点数量: {(currentPath.timePoints?.Count ?? 0)}");
            
            if (currentPath.timePoints != null && currentPath.timePoints.Count > 0)
            {
                foreach (var tp in currentPath.timePoints)
                {
                    Debug.Log($"[{gameObject.name}] 时间点配置 - 路径点{tp.pathPointIndex}: {tp.requiredStoryTime}s, 停留{tp.stopTime}s");
                }
            }
        }
        else
        {
            Debug.Log($"[{gameObject.name}] 当前路径配置为空");
        }
        
        UpdateTargetTimePoint();
        hasInitialized = true;
        Debug.Log($"[{gameObject.name}] 时间控制系统已初始化");
        
        // 初始化后立即计算速度
        if (currentTargetTimePoint != null)
        {
            Debug.Log($"[{gameObject.name}] 初始化时立即计算速度到目标时间点：路径点{currentTargetTimePoint.pathPointIndex}");
            UpdateSpeedBasedOnTime();
        }
        else
        {
            Debug.Log($"[{gameObject.name}] 初始化时无目标时间点，使用原始速度");
        }
    }
    
    /// <summary>
    /// 新幕开始时瞬移到正确位置
    /// </summary>
    private void TeleportToCorrectPositionForNewAct()
    {
        if (!timeControlEnabled) return;
        
        PathConfig currentPath = pathManager.GetCurrentPathConfig();
        if (currentPath == null || GameState.Instance == null) return;
        
        float currentStoryTime = GameState.Instance.GetStoryTime();
        float currentRelativeTime = currentStoryTime - currentPath.pathStartStoryTime;
        
        Debug.Log($"[{gameObject.name}] 新幕瞬移检查 - 当前故事时间: {FormatTime(currentStoryTime)}, 路径起始时间: {FormatTime(currentPath.pathStartStoryTime)}, 相对时间: {FormatTime(currentRelativeTime)}");
        
        // 如果当前故事时间还没到路径开始时间，瞬移到起始点
        if (currentRelativeTime < 0)
        {
            var pathCreator = PathRegistry.GetPathCreatorByID(pathManager.CurrentPathID);
            if (pathCreator != null && pathCreator.GetPathPointCount() > 0)
            {
                Vector3 startPos = pathCreator.GetPathPointPosition(0);
                transform.position = startPos;
                pathManager.SetCurrentPathPointIndex(-1); // 设置为路径开始前
                Debug.Log($"[{gameObject.name}] 路径尚未开始，瞬移到起始点: {startPos}");
            }
            return;
        }
        
        // 计算应该在的位置
        CalculateAndTeleportToCorrectPosition(currentPath, currentRelativeTime);
    }
    
    /// <summary>
    /// 计算并瞬移到正确位置
    /// </summary>
    private void CalculateAndTeleportToCorrectPosition(PathConfig path, float currentRelativeTime)
    {
        var pathCreator = PathRegistry.GetPathCreatorByID(pathManager.CurrentPathID);
        if (pathCreator == null || pathCreator.GetPathPointCount() == 0)
        {
            Debug.LogWarning($"[{gameObject.name}] 无法获取路径创建器或路径点为空");
            return;
        }
        
        // 情况1：有时间控制点的情况
        if (path.timePoints != null && path.timePoints.Count > 0)
        {
            CalculatePositionWithTimePoints(path, currentRelativeTime, pathCreator);
        }
        // 情况2：没有时间控制点，按平均速度计算
        else
        {
            CalculatePositionWithoutTimePoints(path, currentRelativeTime, pathCreator);
        }
    }
    
    /// <summary>
    /// 基于时间控制点计算位置
    /// </summary>
    private void CalculatePositionWithTimePoints(PathConfig path, float currentRelativeTime, MultiPointPathCreator pathCreator)
    {
        // 找到当前时间之前的最后一个时间点
        PathTimePoint lastPassedTimePoint = path.GetLastPassedTimePoint(currentRelativeTime);
        PathTimePoint nextTimePoint = null;
        
        // 找到下一个时间点
        foreach (var tp in path.timePoints)
        {
            if (tp.requiredStoryTime > currentRelativeTime)
            {
                nextTimePoint = tp;
                break;
            }
        }
        
        if (lastPassedTimePoint != null)
        {
            // 如果在两个时间点之间，进行插值计算
            if (nextTimePoint != null && nextTimePoint.pathPointIndex > lastPassedTimePoint.pathPointIndex)
            {
                CalculateInterpolatedPosition(lastPassedTimePoint, nextTimePoint, currentRelativeTime, pathCreator);
            }
            else
            {
                // 瞬移到最后已过的时间点，但要保守处理避免提前到达感
                Vector3 targetPos = pathCreator.GetPathPointPosition(lastPassedTimePoint.pathPointIndex);
                transform.position = targetPos;
                
                // 修复：如果时间刚好超过该时间点不久，设置路径点索引时要保守一些
                float timePassedSincePoint = currentRelativeTime - lastPassedTimePoint.requiredStoryTime;
                int pointIndexToSet;
                
                if (timePassedSincePoint < 1f) // 刚过1秒内，设置为该点
                {
                    pointIndexToSet = lastPassedTimePoint.pathPointIndex;
                }
                else
                {
                    // 时间过了较久，可以设置为该点（表示已经到达并可能离开）
                    pointIndexToSet = lastPassedTimePoint.pathPointIndex;
                }
                
                pathManager.SetCurrentPathPointIndex(pointIndexToSet);
                LogMonitor($"瞬移到已过时间点{lastPassedTimePoint.pathPointIndex}，位置: {targetPos}, 时间: {FormatTime(lastPassedTimePoint.requiredStoryTime)}");
                LogDetailed($"时间差: {timePassedSincePoint:F1}s，设置路径点: {pointIndexToSet}");
            }
        }
        else
        {
            // 还没到达第一个时间点，根据进度插值到第一个时间点
            if (nextTimePoint != null && nextTimePoint.pathPointIndex > 0)
            {
                float progress = currentRelativeTime / nextTimePoint.requiredStoryTime;
                progress = Mathf.Clamp01(progress);
                
                Vector3 startPos = pathCreator.GetPathPointPosition(0);
                Vector3 targetPos = pathCreator.GetPathPointPosition(nextTimePoint.pathPointIndex);
                Vector3 interpolatedPos = CalculatePathInterpolation(0, nextTimePoint.pathPointIndex, progress, pathCreator);
                
                transform.position = interpolatedPos;
                
                // 修复：更精确的路径点索引计算，避免提前几秒的问题
                // 如果进度小于一定阈值，设置为路径开始前；否则根据实际进度计算
                int estimatedPointIndex;
                if (progress < 0.1f) // 进度小于10%，认为还在起始阶段
                {
                    estimatedPointIndex = -1; // 路径开始前
                }
                else
                {
                    // 计算实际应该经过的路径点数量，但不要减1（避免提前）
                    estimatedPointIndex = Mathf.FloorToInt(progress * nextTimePoint.pathPointIndex);
                    // 确保不超过实际应该到达的位置
                    estimatedPointIndex = Mathf.Min(estimatedPointIndex, nextTimePoint.pathPointIndex - 1);
                }
                pathManager.SetCurrentPathPointIndex(estimatedPointIndex);
                
                LogMonitor($"在第一个时间点前插值，进度{progress:P1}，位置: {interpolatedPos}，预估路径点: {estimatedPointIndex}");
                LogDetailed($"瞬移计算详情 - 相对时间: {currentRelativeTime:F1}s, 目标时间: {nextTimePoint.requiredStoryTime:F1}s, 目标路径点: {nextTimePoint.pathPointIndex}");
            }
            else
            {
                // 瞬移到起始点
                Vector3 startPos = pathCreator.GetPathPointPosition(0);
                transform.position = startPos;
                pathManager.SetCurrentPathPointIndex(-1);
                LogMonitor($"瞬移到路径起始点: {startPos}");
            }
        }
    }
    
    /// <summary>
    /// 在两个时间点之间进行插值计算位置
    /// </summary>
    private void CalculateInterpolatedPosition(PathTimePoint fromPoint, PathTimePoint toPoint, float currentRelativeTime, MultiPointPathCreator pathCreator)
    {
        float fromTime = fromPoint.requiredStoryTime;
        float toTime = toPoint.requiredStoryTime;
        float timeProgress = (currentRelativeTime - fromTime) / (toTime - fromTime);
        timeProgress = Mathf.Clamp01(timeProgress);
        
        Vector3 interpolatedPos = CalculatePathInterpolation(fromPoint.pathPointIndex, toPoint.pathPointIndex, timeProgress, pathCreator);
        transform.position = interpolatedPos;
        
        // 修复：更保守的路径点索引计算，避免提前判定到达
        int pathPointRange = toPoint.pathPointIndex - fromPoint.pathPointIndex;
        int estimatedPointIndex;
        
        if (timeProgress < 0.05f) // 时间进度小于5%，保持在起始点
        {
            estimatedPointIndex = fromPoint.pathPointIndex;
        }
        else if (timeProgress > 0.95f) // 时间进度大于95%，接近终点但还未到达
        {
            estimatedPointIndex = toPoint.pathPointIndex - 1; // 保持在终点前一个位置
        }
        else
        {
            // 中间阶段，正常计算但保守一些
            int progressPoints = Mathf.FloorToInt(timeProgress * pathPointRange);
            estimatedPointIndex = fromPoint.pathPointIndex + progressPoints;
            // 确保不超过终点前一个位置
            estimatedPointIndex = Mathf.Min(estimatedPointIndex, toPoint.pathPointIndex - 1);
        }
        
        pathManager.SetCurrentPathPointIndex(estimatedPointIndex);
        
        LogMonitor($"在时间点{fromPoint.pathPointIndex}-{toPoint.pathPointIndex}间插值，时间进度{timeProgress:P1}，位置: {interpolatedPos}，路径点: {estimatedPointIndex}");
        LogDetailed($"插值详情 - 从时间{fromTime:F1}s到{toTime:F1}s，当前{currentRelativeTime:F1}s，路径点范围{pathPointRange}");
    }
    
    /// <summary>
    /// 基于路径点插值计算位置
    /// </summary>
    private Vector3 CalculatePathInterpolation(int fromPointIndex, int toPointIndex, float progress, MultiPointPathCreator pathCreator)
    {
        if (fromPointIndex == toPointIndex)
        {
            return pathCreator.GetPathPointPosition(fromPointIndex);
        }
        
        // 计算路径上的总距离
        float totalDistance = 0f;
        List<Vector3> pathSegments = new List<Vector3>();
        List<float> segmentDistances = new List<float>();
        
        for (int i = fromPointIndex; i < toPointIndex; i++)
        {
            Vector3 from = pathCreator.GetPathPointPosition(i);
            Vector3 to = pathCreator.GetPathPointPosition(i + 1);
            float segmentDist = Vector3.Distance(from, to);
            
            pathSegments.Add(from);
            segmentDistances.Add(segmentDist);
            totalDistance += segmentDist;
        }
        pathSegments.Add(pathCreator.GetPathPointPosition(toPointIndex));
        
        // 根据进度计算目标距离
        float targetDistance = progress * totalDistance;
        
        // 找到目标距离对应的路径段
        float accumulatedDistance = 0f;
        for (int i = 0; i < segmentDistances.Count; i++)
        {
            if (accumulatedDistance + segmentDistances[i] >= targetDistance)
            {
                // 在这个段内插值
                float segmentProgress = (targetDistance - accumulatedDistance) / segmentDistances[i];
                return Vector3.Lerp(pathSegments[i], pathSegments[i + 1], segmentProgress);
            }
            accumulatedDistance += segmentDistances[i];
        }
        
        // 如果超出范围，返回终点
        return pathSegments[pathSegments.Count - 1];
    }
    
    /// <summary>
    /// 没有时间控制点时基于平均速度计算位置
    /// </summary>
    private void CalculatePositionWithoutTimePoints(PathConfig path, float currentRelativeTime, MultiPointPathCreator pathCreator)
    {
        // 计算路径总长度
        float totalPathLength = 0f;
        for (int i = 0; i < pathCreator.GetPathPointCount() - 1; i++)
        {
            Vector3 from = pathCreator.GetPathPointPosition(i);
            Vector3 to = pathCreator.GetPathPointPosition(i + 1);
            totalPathLength += Vector3.Distance(from, to);
        }
        
        // 假设以原始速度移动，计算应该移动的距离
        float expectedDistance = currentRelativeTime * originalMoveSpeed;
        float pathProgress = Mathf.Clamp01(expectedDistance / totalPathLength);
        
        // 根据进度计算位置
        Vector3 interpolatedPos = CalculatePathInterpolation(0, pathCreator.GetPathPointCount() - 1, pathProgress, pathCreator);
        transform.position = interpolatedPos;
        
        // 修复：更保守的路径点索引估算，避免提前到达
        int estimatedPointIndex;
        if (pathProgress < 0.05f) // 进度很小，设置为路径开始前
        {
            estimatedPointIndex = -1;
        }
        else
        {
            // 计算路径点索引，但不要减1（避免提前）
            int totalPoints = pathCreator.GetPathPointCount() - 1;
            estimatedPointIndex = Mathf.FloorToInt(pathProgress * totalPoints);
            // 确保不超过实际路径范围
            estimatedPointIndex = Mathf.Clamp(estimatedPointIndex, 0, totalPoints - 1);
        }
        pathManager.SetCurrentPathPointIndex(estimatedPointIndex);
        
        LogMonitor($"无时间控制点，按平均速度计算位置，路径进度{pathProgress:P1}，位置: {interpolatedPos}，估算路径点: {estimatedPointIndex}");
        LogDetailed($"平均速度计算详情 - 相对时间: {currentRelativeTime:F1}s, 预期距离: {expectedDistance:F1}m, 总长度: {totalPathLength:F1}m");
    }
    
    /// <summary>
    /// 更新目标时间控制点
    /// </summary>
    private void UpdateTargetTimePoint()
    {
        currentTargetTimePoint = null;
        
        PathConfig currentPath = pathManager.GetCurrentPathConfig();
        if (currentPath == null)
        {
            Debug.Log($"[{gameObject.name}] 无路径配置");
            return;
        }
        
        int currentPathPoint = pathManager.CurrentPathPointIndex;
        float currentStoryTime = GameState.Instance != null ? GameState.Instance.GetStoryTime() : 0f;
        float currentRelativeTime = currentStoryTime - currentPath.pathStartStoryTime;
        
        Debug.Log($"[{gameObject.name}] 查找目标时间点 - 当前路径点: {currentPathPoint}, 故事时间: {FormatTime(currentStoryTime)}, 相对时间: {FormatTime(currentRelativeTime)}");
        
        // 如果有时间控制点配置，优先使用时间控制点
        if (currentPath.timePoints != null && currentPath.timePoints.Count > 0)
        {
            // 按时间顺序排序时间控制点
            var sortedTimePoints = currentPath.timePoints.OrderBy(tp => tp.requiredStoryTime).ToList();
            
            // 查找下一个需要到达的时间控制点
            foreach (var timePoint in sortedTimePoints)
            {
                bool isValidTarget = false;
                
                if (currentPathPoint == -1)
                {
                    // 路径刚开始，查找第一个时间点
                    isValidTarget = true;
                    Debug.Log($"[{gameObject.name}] 路径开始，找到第一个时间控制点: 点{timePoint.pathPointIndex}, 时间{timePoint.requiredStoryTime}s");
                }
                else
                {
                    // 已在路径上，查找未来的时间点
                    bool isFuturePoint = timePoint.pathPointIndex > currentPathPoint;
                    
                    // 或者是相同路径点但时间还没到（修复：允许路径点0的时间控制）
                    bool isSamePointButTimeNotReached = (timePoint.pathPointIndex == currentPathPoint && 
                                                       timePoint.requiredStoryTime > currentRelativeTime + 0.1f);
                    
                    LogDetailed($"检查时间点{timePoint.pathPointIndex}: 是未来点={isFuturePoint}, 是同点但时间未到={isSamePointButTimeNotReached}, 时间差={(timePoint.requiredStoryTime - currentRelativeTime):F3}s");
                    
                    // 修复：包含同点时间等待逻辑，特别是对于路径点0
                    isValidTarget = isFuturePoint || isSamePointButTimeNotReached;
                    
                    if (isValidTarget)
                    {
                        if (isFuturePoint)
                        {
                            Debug.Log($"[{gameObject.name}] 找到下一个时间控制点: 点{timePoint.pathPointIndex}, 时间{timePoint.requiredStoryTime}s (当前在点{currentPathPoint}, 时间{currentRelativeTime:F1}s)");
                        }
                        else if (isSamePointButTimeNotReached)
                        {
                            Debug.Log($"[{gameObject.name}] 找到当前点{timePoint.pathPointIndex}的时间控制目标，时间{timePoint.requiredStoryTime}s (当前时间{currentRelativeTime:F1}s)");
                        }
                    }
                }
                
                if (isValidTarget)
                {
                    currentTargetTimePoint = timePoint;
                    break; // 找到第一个有效目标就停止
                }
            }
            
            if (currentTargetTimePoint != null)
            {
                float targetAbsoluteTime = currentPath.pathStartStoryTime + currentTargetTimePoint.requiredStoryTime;
                Debug.Log($"[{gameObject.name}] 设定时间控制目标：路径点{currentTargetTimePoint.pathPointIndex}，相对时间{currentTargetTimePoint.requiredStoryTime:F1}s，绝对时间{FormatTime(targetAbsoluteTime)}");
                return;
            }
        }
        
        // 关键修复：如果没有找到时间控制点，检查是否需要以路径终点作为目标
        var pathCreator = PathRegistry.GetPathCreatorByID(pathManager.CurrentPathID);
        if (pathCreator != null && pathCreator.GetPathPointCount() > 0)
        {
            int pathEndIndex = pathCreator.GetPathPointCount() - 1;
            
            LogDetailed($"检查路径终点目标 - 当前点: {currentPathPoint}, 路径终点: {pathEndIndex}");
            
            // 如果当前位置还没到达路径终点，创建一个虚拟的路径终点目标
            if (currentPathPoint < pathEndIndex)
            {
                // 计算到达路径终点的目标时间（基于路径配置的结束时间）
                float pathEndTime = currentPath.GetActualEndTime(controller?.Data, controller?.Data?.currentScore ?? 0f);
                
                LogDetailed($"路径终点计算 - 绝对结束时间: {FormatTime(pathEndTime)}, 路径起始时间: {FormatTime(currentPath.pathStartStoryTime)}");
                
                // 创建一个虚拟的路径终点目标
                currentTargetTimePoint = new PathTimePoint
                {
                    pathPointIndex = pathEndIndex,
                    requiredStoryTime = pathEndTime - currentPath.pathStartStoryTime,
                    stopTime = 0f,
                    isVirtualEndTarget = true  // 标记为虚拟终点目标
                };
                
                float targetAbsoluteTime = pathEndTime;
                
                // 🔍 检查虚拟目标时间是否合理
                if (currentTargetTimePoint.requiredStoryTime < 0)
                {
                    LogError($"❌ 虚拟目标时间异常：相对时间{currentTargetTimePoint.requiredStoryTime:F1}s为负数！");
                    LogError($"   路径结束时间: {FormatTime(pathEndTime)}");
                    LogError($"   路径开始时间: {FormatTime(currentPath.pathStartStoryTime)}");
                    LogError($"   当前故事时间: {FormatTime(currentStoryTime)}");
                    LogError($"   这可能是路径时间配置错误，将时间设置为0以避免极高速度");
                    
                    // 修复负时间，设置为当前相对时间
                    currentTargetTimePoint.requiredStoryTime = Mathf.Max(0f, currentStoryTime - currentPath.pathStartStoryTime);
                    targetAbsoluteTime = currentPath.pathStartStoryTime + currentTargetTimePoint.requiredStoryTime;
                }
                
                LogMonitor($"✅ 创建路径终点虚拟目标：路径点{currentTargetTimePoint.pathPointIndex}，相对时间{currentTargetTimePoint.requiredStoryTime:F1}s，绝对时间{FormatTime(targetAbsoluteTime)}");
                return;
            }
            else
            {
                LogDetailed($"当前已在路径终点或超过终点，检查是否应该触发路径切换");
                
                // 当前已在路径终点，检查时间条件是否满足路径切换
                float pathEndTime = currentPath.GetActualEndTime(controller?.Data, controller?.Data?.currentScore ?? 0f);
                // 复用已经定义的currentStoryTime变量，避免重复声明
                
                LogDetailed($"路径终点时间检查 - 当前故事时间: {FormatTime(currentStoryTime)}, 路径结束时间: {FormatTime(pathEndTime)}");
                
                if (pathEndTime > 0 && currentStoryTime >= pathEndTime)
                {
                    LogMonitor($"🔄 已在路径终点且时间已到({FormatTime(currentStoryTime)} >= {FormatTime(pathEndTime)})，触发路径切换");
                    
                    // 触发路径切换
                    if (pathManager != null)
                    {
                        StartCoroutine(TriggerPathSwitchAfterDelay());
                    }
                    
                    // 临时设置一个虚拟目标以避免空指针异常，直到路径切换完成
                    float relativeTime = pathEndTime - currentPath.pathStartStoryTime;
                    if (relativeTime < 0)
                    {
                        LogWarning($"⚠️ 临时虚拟目标时间异常（负数{relativeTime:F1}s），设置为0");
                        relativeTime = 0f;
                    }
                    
                    currentTargetTimePoint = new PathTimePoint
                    {
                        pathPointIndex = pathEndIndex,
                        requiredStoryTime = relativeTime,
                        stopTime = 0f,
                        isVirtualEndTarget = true
                    };
                    
                    LogDetailed($"临时创建虚拟终点目标，避免空指针异常");
                    return;
                }
                else if (pathEndTime > 0)
                {
                    LogDetailed($"已在路径终点但时间未到，等待到{FormatTime(pathEndTime)}");
                    
                    // 创建等待型虚拟终点目标
                    float waitRelativeTime = pathEndTime - currentPath.pathStartStoryTime;
                    if (waitRelativeTime < 0)
                    {
                        LogWarning($"⚠️ 等待型虚拟目标时间异常（负数{waitRelativeTime:F1}s），设置为当前相对时间");
                        waitRelativeTime = currentStoryTime - currentPath.pathStartStoryTime;
                    }
                    
                    currentTargetTimePoint = new PathTimePoint
                    {
                        pathPointIndex = pathEndIndex,
                        requiredStoryTime = waitRelativeTime,
                        stopTime = 0f,
                        isVirtualEndTarget = true
                    };
                    
                    LogMonitor($"✅ 在路径终点创建等待目标：等待到{FormatTime(pathEndTime)}");
                    return;
                }
                else
                {
                    LogDetailed($"路径无结束时间限制，立即触发路径切换");
                    
                    if (pathManager != null)
                    {
                        StartCoroutine(TriggerPathSwitchAfterDelay());
                    }
                    
                    // 创建临时目标避免空指针
                    currentTargetTimePoint = new PathTimePoint
                    {
                        pathPointIndex = pathEndIndex,
                        requiredStoryTime = 0f,
                        stopTime = 0f,
                        isVirtualEndTarget = true
                    };
                    return;
                }
            }
        }
        else
        {
            LogWarning($"无法获取路径创建器或路径为空，无法创建路径终点目标");
        }
        
        LogWarning($"[{gameObject.name}] ⚠️ 意外情况：未能创建任何目标，设置临时目标避免崩溃");
        
        // 设置一个临时目标以避免空指针异常
        currentTargetTimePoint = new PathTimePoint
        {
            pathPointIndex = 0,
            requiredStoryTime = 0f,
            stopTime = 0f,
            isVirtualEndTarget = false
        };
    }
    
    /// <summary>
    /// 根据故事时间更新移动速度
    /// </summary>
    private void UpdateSpeedBasedOnTime()
    {
        if (currentTargetTimePoint == null)
        {
            // 没有时间控制点，使用原始速度
            if (Mathf.Abs(agent.speed - originalMoveSpeed) > 0.1f)
            {
                agent.speed = originalMoveSpeed;
                lastSetSpeed = originalMoveSpeed;
                LogMonitor($"无任何目标点，恢复原始速度: {originalMoveSpeed}");
            }
            return;
        }
        
        float requiredSpeed = CalculateRequiredSpeed();
        float currentStoryTime = GameState.Instance.GetStoryTime();
        PathConfig currentPath = pathManager.GetCurrentPathConfig();
        
        // 额外保护：确保currentPath不为空
        if (currentPath == null)
        {
            LogWarning($"⚠️ UpdateSpeedBasedOnTime: currentPath为空，尝试重新初始化");
            
            // 尝试重新初始化路径
            if (pathManager != null && controller != null && controller.Data != null)
            {
                // 检查是否有可用的路径配置
                if (controller.Data.paths != null && controller.Data.paths.Count > 0)
                {
                    LogMonitor($"发现{controller.Data.paths.Count}个可用路径配置，尝试重新初始化时间控制");
                    InitializeTimeControl();
                    return; // 等待下一帧重新处理
                }
                else
                {
                    LogWarning($"NPC数据中没有可用的路径配置，保持当前状态等待");
                    return; // 保持当前状态，不修改速度
                }
            }
            else
            {
                LogError($"pathManager为null，无法获取路径信息");
                return; // 保持当前状态
            }
        }
        
        // 额外保护：确保currentTargetTimePoint不为空
        if (currentTargetTimePoint == null)
        {
            LogWarning($"⚠️ UpdateSpeedBasedOnTime: currentTargetTimePoint意外为空，恢复原始速度");
            agent.speed = originalMoveSpeed;
            return;
        }
        
        float targetAbsoluteTime = currentPath.pathStartStoryTime + currentTargetTimePoint.requiredStoryTime;
        
        if (requiredSpeed > teleportThreshold)
        {
            // 需要瞬移
            LogMonitor($"故事时间{FormatTime(currentStoryTime)}需要瞬移到路径点{currentTargetTimePoint.pathPointIndex}（目标时间{FormatTime(targetAbsoluteTime)}）- 所需速度{requiredSpeed:F1}超过阈值{teleportThreshold}");
            TeleportToTimePoint();
        }
        else
        {
            // 调整速度
            float minAllowedSpeed = originalMoveSpeed * speedMultiplierMin;
            float maxAllowedSpeed = originalMoveSpeed * speedMultiplierMax;
            float clampedSpeed = Mathf.Clamp(requiredSpeed, minAllowedSpeed, maxAllowedSpeed);
            
            // 详细的速度调试信息
            bool speedChanged = Mathf.Abs(agent.speed - clampedSpeed) > 0.01f; // 降低检测阈值
            
            if (debugLogLevel >= DebugLogLevel.Detailed && (speedChanged || Time.time % 3f < 0.1f)) // 要么速度改变，要么每3秒输出一次状态
            {
                LogDetailed("=== 速度调整详情 ===");
                LogDetailed($"  原始速度: {originalMoveSpeed:F3}m/s");
                LogDetailed($"  需求速度: {requiredSpeed:F3}m/s");
                LogDetailed($"  速度范围: {minAllowedSpeed:F3} - {maxAllowedSpeed:F3}m/s");
                LogDetailed($"  限制后速度: {clampedSpeed:F3}m/s");
                LogDetailed($"  当前Agent速度: {agent.speed:F3}m/s");
                LogDetailed($"  目标时间点: 路径点{currentTargetTimePoint.pathPointIndex}（目标时间{FormatTime(targetAbsoluteTime)}）");
                
                if (requiredSpeed < minAllowedSpeed)
                {
                    LogDetailed($"  ⚠️ 需求速度{requiredSpeed:F3}被最小限制{minAllowedSpeed:F3}提升");
                }
                else if (requiredSpeed > maxAllowedSpeed)
                {
                    LogDetailed($"  ⚠️ 需求速度{requiredSpeed:F3}被最大限制{maxAllowedSpeed:F3}降低");
                }
            }
            
            // 设置速度
            if (speedChanged)
            {
                // 保存原始Agent状态
                bool wasEnabled = agent.enabled;
                
                // 尝试设置速度
                agent.speed = clampedSpeed;
                lastSetSpeed = clampedSpeed;
                
                // 如果速度设置失败，可能是NavMeshAgent的限制
                if (Mathf.Abs(agent.speed - clampedSpeed) > 0.001f)
                {
                    LogWarning($"⚠️ NavMeshAgent速度设置受限！尝试设置{clampedSpeed:F3}，实际为{agent.speed:F3}");
                    LogDetailed($"NavMeshAgent属性 - acceleration: {agent.acceleration}, angularSpeed: {agent.angularSpeed}");
                    
                    // 如果需要极慢的速度，考虑其他方案
                    if (clampedSpeed < 0.1f && agent.speed > clampedSpeed * 2f)
                    {
                        LogWarning($"需要极慢速度({clampedSpeed:F3})但Agent限制为{agent.speed:F3}，可能需要使用其他移动方式");
                    }
                }
                else
                {
                    LogMonitor($"✅ 速度已调整为{agent.speed:F3}m/s");
                }
            }
        }
    }
    
    /// <summary>
    /// 计算到达目标时间点所需的速度
    /// </summary>
    private float CalculateRequiredSpeed(int recursionDepth = 0)
    {
        // 防止无限递归
        if (recursionDepth > 3)
        {
            LogWarning($"CalculateRequiredSpeed递归深度超过限制({recursionDepth})，使用原始速度");
            return originalMoveSpeed;
        }
        if (currentTargetTimePoint == null || pathManager == null) 
            return originalMoveSpeed;
        
        // 获取当前位置到目标点的距离
        float distance = CalculateDistanceToTarget();
        
        // 计算剩余时间
        PathConfig currentPath = pathManager.GetCurrentPathConfig();
        
        // 保护：确保currentPath不为空
        if (currentPath == null)
        {
            LogWarning($"⚠️ CalculateRequiredSpeed: currentPath为空，尝试获取有效路径");
            
            // 尝试重新获取路径
            if (pathManager != null && controller != null && controller.Data != null)
            {
                var availablePaths = controller.Data.paths;
                if (availablePaths != null && availablePaths.Count > 0)
                {
                    LogMonitor($"发现{availablePaths.Count}个可用路径，当前可能正在切换中");
                    // 保持极慢速度，等待路径切换完成
                    return 0.1f;
                }
            }
            
            LogWarning($"没有可用路径配置，保持极慢速度等待");
            return 0.1f; // 不是原始速度，而是极慢速度等待
        }
        
        float currentStoryTime = GameState.Instance.GetStoryTime();
        float currentRelativeTime = currentStoryTime - currentPath.pathStartStoryTime;
        float remainingTime = currentTargetTimePoint.requiredStoryTime - currentRelativeTime;
        
        // 关键修复：如果距离很小且时间还有剩余，说明需要等待
        if (distance <= 0.1f)
        {
            if (remainingTime > 0.1f)
            {
                LogDetailed($"已在目标位置附近，距离: {distance:F3}m，剩余时间: {remainingTime:F3}s，需要等待");
                return 0.01f; // 极慢速度，实际上是等待
            }
            else
            {
                LogDetailed($"已在目标位置附近，距离: {distance:F3}m，时间已到，寻找下一个目标");
                
                // 检查是否到达了虚拟路径终点目标，如果是，应该触发路径切换
                bool isVirtualEndTarget = currentTargetTimePoint.isVirtualEndTarget;
                if (isVirtualEndTarget)
                {
                    LogDetailed($"到达虚拟路径终点目标，时间已到，通知PathManager进行路径切换");
                    
                    // 通知PathManager强制完成当前路径并切换到下一个路径
                    if (pathManager != null)
                    {
                        LogMonitor($"🔄 TimeController通知PathManager：虚拟终点已到达，强制路径切换");
                        // 使用协程延迟执行，避免在Update循环中直接修改状态
                        StartCoroutine(TriggerPathSwitchAfterDelay());
                    }
                    
                    return originalMoveSpeed;
                }
                
                // 时间已到，需要寻找下一个目标时间点
                UpdateTargetTimePoint();
                
                // 根据系统设计，这里一定会找到新的目标时间点（要么真实时间点，要么虚拟终点）
                if (currentTargetTimePoint != null)
                {
                    LogDetailed($"找到新目标时间点，重新计算速度");
                    return CalculateRequiredSpeed(recursionDepth + 1); // 递归调用，但有保护机制防止无限递归
                }
                else
                {
                    LogError($"❌ 系统错误：时间已到但无法找到下一个目标时间点！这不应该发生。");
                    return originalMoveSpeed;
                }
            }
        }
        
        // 减去路径点停留时间（如果NPC还需要在路径点停留）
        float totalStopTime = CalculateRemainingStopTime();
        float movingTime = remainingTime - totalStopTime;
        
        // 详细的速度计算调试信息（初始判断不包含requiredSpeed比较）
        bool shouldShowDetails = debugLogLevel >= DebugLogLevel.Detailed && (distance > 0.5f || movingTime < 5f || Time.time % 4f < 0.1f);
        
        if (shouldShowDetails)
        {
            LogDetailed("=== 速度计算详情 ===");
            LogDetailed($"  距离到目标: {distance:F3}m");
            LogDetailed($"  剩余时间: {remainingTime:F3}s"); 
            LogDetailed($"  停留时间: {totalStopTime:F3}s");
            LogDetailed($"  实际移动时间: {movingTime:F3}s");
            LogDetailed($"  当前故事时间: {currentRelativeTime:F3}s");
            LogDetailed($"  目标到达时间: {currentTargetTimePoint.requiredStoryTime:F3}s");
        }
        
        if (movingTime <= 0)
        {
            LogWarning($"⚠️ 移动时间不足或已过期({movingTime:F3}s)，需要快速移动");
            return originalMoveSpeed * speedMultiplierMax; // 时间已过，需要快速移动
        }
        
        float requiredSpeed = distance / movingTime;
        
        // 重新检查是否需要显示详情（现在包含速度比较）
        shouldShowDetails = shouldShowDetails || (debugLogLevel >= DebugLogLevel.Detailed && Mathf.Abs(requiredSpeed - agent.speed) > 0.1f);
        
        if (shouldShowDetails)
        {
            LogDetailed($"  计算公式: 速度 = 距离({distance:F3}) / 移动时间({movingTime:F3}) = {requiredSpeed:F3}m/s");
            LogDetailed($"  当前Agent速度: {agent.speed:F3}m/s");
            LogDetailed($"  速度差异: {Mathf.Abs(requiredSpeed - agent.speed):F3}m/s");
            
            if (requiredSpeed < 0.1f)
            {
                LogDetailed($"  📌 需求速度很小({requiredSpeed:F3}m/s)，这可能是因为距离很短或时间很充足");
            }
            else if (requiredSpeed > 10f)
            {
                LogDetailed($"  ⚡ 需求速度很大({requiredSpeed:F3}m/s)，这可能是因为距离很远或时间紧迫");
            }
        }
        
        return requiredSpeed;
    }
    
    /// <summary>
    /// 计算剩余停留时间
    /// 包括：当前位置的停留时间 + 路径中间点的停留时间  
    /// 不包括：目标时间点的停留时间（因为是到达后才停留）
    /// 公式：A→B速度 = 距离/(B时间-A时间-A点停留时间-中间点停留时间)
    /// </summary>
    private float CalculateRemainingStopTime()
    {
        PathConfig currentPath = pathManager.GetCurrentPathConfig();
        if (currentPath == null || currentTargetTimePoint == null) 
            return 0f;
        
        // 如果当前路径没有时间控制点配置，说明目标是虚拟的路径终点，无停留时间
        if (currentPath.timePoints == null || currentPath.timePoints.Count == 0)
            return 0f;
        
        int currentPathPoint = pathManager.CurrentPathPointIndex;
        float currentStoryTime = GameState.Instance.GetStoryTime();
        float currentRelativeTime = currentStoryTime - currentPath.pathStartStoryTime;
        float totalStopTime = 0f;
        
        // 计算从当前位置到目标点之间的停留时间（不包括目标点）
        foreach (var timePoint in currentPath.timePoints)
        {
            // 检查这个时间点是否在需要考虑的范围内
            bool shouldInclude = false;
            
            if (currentPathPoint == -1)
            {
                // 路径开始，考虑到目标点之前的所有时间点（不包括目标点）
                shouldInclude = timePoint.pathPointIndex < currentTargetTimePoint.pathPointIndex;
            }
            else
            {
                // 已在路径上，考虑从当前点到目标点之间的时间点
                if (timePoint.pathPointIndex == currentPathPoint)
                {
                    // 当前点：如果有停留且时间已到，计算剩余停留时间
                    if (timePoint.requiredStoryTime <= currentRelativeTime && timePoint.stopTime > 0)
                    {
                        // 修复：正确计算已经过去的停留时间
                        float timePassedSinceArrival = currentRelativeTime - timePoint.requiredStoryTime;
                        float remainingStopTime = Mathf.Max(0, timePoint.stopTime - timePassedSinceArrival);
                        totalStopTime += remainingStopTime;
                        
                        LogDetailed($"当前点{currentPathPoint}停留时间计算 - 到达时间:{timePoint.requiredStoryTime:F1}s, 当前相对时间:{currentRelativeTime:F1}s, 已过去:{timePassedSinceArrival:F1}s, 总停留:{timePoint.stopTime:F1}s, 剩余停留:{remainingStopTime:F1}s");
                    }
                }
                else if (timePoint.pathPointIndex > currentPathPoint && timePoint.pathPointIndex < currentTargetTimePoint.pathPointIndex)
                {
                    // 中间点：完整停留时间
                    shouldInclude = true;
                }
            }
            
            if (shouldInclude && timePoint.stopTime > 0)
            {
                totalStopTime += timePoint.stopTime;
            }
        }
        
        return totalStopTime;
    }
    
    /// <summary>
    /// 计算到目标时间点的距离
    /// </summary>
    private float CalculateDistanceToTarget()
    {
        var pathCreator = PathRegistry.GetPathCreatorByID(pathManager.CurrentPathID);
        if (pathCreator == null) return 0f;
        
        int currentPoint = pathManager.CurrentPathPointIndex;
        int targetPoint = currentTargetTimePoint.pathPointIndex;
        
        LogDetailed($"距离计算 - 当前路径点: {currentPoint}, 目标路径点: {targetPoint}");
        
        // 关键修复：如果目标点就是当前点或在当前点之前，距离为0
        if (targetPoint <= currentPoint)
        {
            LogDetailed($"目标点{targetPoint}在当前点{currentPoint}或之前，距离为0");
            return 0f;
        }
        
        // 使用MultiPointPathCreator的新方法计算实际路径距离
        float distance = pathCreator.GetDistanceFromCurrentPosition(transform.position, currentPoint, targetPoint);
        
        LogDetailed($"计算路径距离 - 从路径点{currentPoint}到{targetPoint}: {distance:F3}m (使用路径实际距离计算)");
        return distance;
    }
    
    /// <summary>
    /// 瞬移到目标时间点
    /// </summary>
    private void TeleportToTimePoint()
    {
        var pathCreator = PathRegistry.GetPathCreatorByID(pathManager.CurrentPathID);
        if (pathCreator == null) return;
        
        Vector3 targetPos = pathCreator.GetPathPointPosition(currentTargetTimePoint.pathPointIndex);
        transform.position = targetPos;
        
        // 更新路径管理器的当前路径点
        pathManager.SetCurrentPathPointIndex(currentTargetTimePoint.pathPointIndex);
        
        LogMonitor($"瞬移到路径点{currentTargetTimePoint.pathPointIndex}，位置：{targetPos}");
        
        // 更新目标时间点
        UpdateTargetTimePoint();
    }
    
    /// <summary>
    /// 格式化时间显示
    /// </summary>
    private string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
        return $"{minutes:00}:{seconds:00}";
    }
    
    /// <summary>
    /// 设置是否启用时间控制
    /// </summary>
    public void SetTimeControlEnabled(bool enabled)
    {
        timeControlEnabled = enabled;
        Debug.Log($"[{gameObject.name}] 时间控制{(enabled ? "已启用" : "已禁用")}");
        
        if (!enabled)
        {
            agent.speed = originalMoveSpeed;
        }
        else if (hasInitialized)
        {
            UpdateSpeedBasedOnTime();
        }
    }
    
    /// <summary>
    /// 获取当前时间控制状态
    /// </summary>
    public bool IsTimeControlEnabled => timeControlEnabled;
    
    /// <summary>
    /// 获取当前目标时间点
    /// </summary>
    public PathTimePoint GetCurrentTargetTimePoint() => currentTargetTimePoint;
    
    /// <summary>
    /// 设置调试日志级别
    /// </summary>
    public void SetDebugLogLevel(DebugLogLevel level)
    {
        debugLogLevel = level;
        LogMonitor($"调试日志级别已设置为: {level}");
    }
    
    /// <summary>
    /// 获取当前调试日志级别
    /// </summary>
    public DebugLogLevel GetDebugLogLevel() => debugLogLevel;
    
    /// <summary>
    /// 路径切换通知处理，重新初始化时间控制系统
    /// </summary>
    public void OnPathSwitched()
    {
        LogMonitor("收到路径切换通知，重新初始化时间控制系统");
        
        // 🔍 重置路径切换时间，允许正常的路径切换
        lastPathSwitchTime = 0f;
        
        // 清空当前时间控制状态
        currentTargetTimePoint = null;
        lastSetSpeed = -1f;
        
        // 重新初始化时间控制
        InitializeTimeControl();
        
        // 立即执行一次瞬移到正确位置（适应新路径）
        if (timeControlEnabled)
        {
            LogMonitor("路径切换后执行瞬移校正");
            TeleportToCorrectPositionForNewAct();
            
            // 重新查找目标时间点并计算速度
            UpdateTargetTimePoint();
            if (currentTargetTimePoint != null)
            {
                UpdateSpeedBasedOnTime();
                LogMonitor($"路径切换后重新计算速度，目标时间点：路径点{currentTargetTimePoint.pathPointIndex}，当前速度：{agent.speed:F2}");
            }
            else
            {
                // 没有时间控制点，恢复原始速度
                agent.speed = originalMoveSpeed;
                LogMonitor($"新路径无时间控制点，恢复原始速度：{originalMoveSpeed:F2}");
            }
            
            // 关键修复：使用协程持续监控和修正速度，防止被其他组件覆盖
            StartCoroutine(MonitorSpeedAfterPathSwitch());
        }
        
        LogMonitor("路径切换时间控制初始化完成");
    }
    
    /// <summary>
    /// 路径切换后持续监控速度，防止被其他组件覆盖
    /// </summary>
    private System.Collections.IEnumerator MonitorSpeedAfterPathSwitch()
    {
        float monitorDuration = 2f; // 监控2秒
        float startTime = Time.time;
        float lastCorrectSpeed = agent.speed;
        
        LogDetailed($"开始监控路径切换后的速度，持续{monitorDuration}秒");
        
        while (Time.time - startTime < monitorDuration)
        {
            // 如果时间控制启用且有目标时间点
            if (timeControlEnabled && hasInitialized && currentTargetTimePoint != null)
            {
                // 重新计算应该的速度
                float requiredSpeed = CalculateRequiredSpeed();
                float minAllowedSpeed = originalMoveSpeed * speedMultiplierMin;
                float maxAllowedSpeed = originalMoveSpeed * speedMultiplierMax;
                float expectedSpeed = Mathf.Clamp(requiredSpeed, minAllowedSpeed, maxAllowedSpeed);
                
                // 如果当前速度偏离预期速度太多，重新设置
                if (Mathf.Abs(agent.speed - expectedSpeed) > 0.1f)
                {
                    LogMonitor($"检测到速度偏离（期望{expectedSpeed:F2}，实际{agent.speed:F2}），重新设置");
                    agent.speed = expectedSpeed;
                    lastCorrectSpeed = expectedSpeed;
                }
                else if (Mathf.Abs(agent.speed - lastCorrectSpeed) > 0.05f)
                {
                    // 速度变化不大，但仍然记录
                    lastCorrectSpeed = agent.speed;
                }
            }
            else if (timeControlEnabled && currentTargetTimePoint == null)
            {
                // 没有目标时间点，确保使用原始速度
                if (Mathf.Abs(agent.speed - originalMoveSpeed) > 0.1f)
                {
                    LogMonitor($"无时间控制目标，确保使用原始速度（期望{originalMoveSpeed:F2}，实际{agent.speed:F2}）");
                    agent.speed = originalMoveSpeed;
                    lastCorrectSpeed = originalMoveSpeed;
                }
            }
            
            // 每0.2秒检查一次
            yield return new WaitForSeconds(0.2f);
        }
        
        LogDetailed($"路径切换后速度监控结束，最终速度：{agent.speed:F2}");
    }
    
    #region 调试和测试方法
    
    /// <summary>
    /// 手动触发初始瞬移（游戏开始时的瞬移，测试用）
    /// </summary>
    [ContextMenu("测试初始瞬移")]
    public void TestInitialTeleport()
    {
        if (Application.isPlaying)
        {
            Debug.Log($"[{gameObject.name}] 手动触发初始瞬移测试");
            StartCoroutine(PerformInitialTeleportAfterDelay());
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] 初始瞬移测试只能在游戏运行时使用");
        }
    }
    
    /// <summary>
    /// 手动触发新幕瞬移（测试用）
    /// </summary>
    [ContextMenu("测试新幕瞬移")]
    public void TestNewActTeleport()
    {
        if (Application.isPlaying)
        {
            Debug.Log($"[{gameObject.name}] 手动触发新幕瞬移测试");
            TeleportToCorrectPositionForNewAct();
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] 新幕瞬移测试只能在游戏运行时使用");
        }
    }
    
    /// <summary>
    /// 显示当前时间控制状态（测试用）
    /// </summary>
    [ContextMenu("显示时间控制状态")]
    public void ShowTimeControlStatus()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning($"[{gameObject.name}] 时间控制状态只能在游戏运行时查看");
            return;
        }
        
        PathConfig currentPath = pathManager.GetCurrentPathConfig();
        if (currentPath == null)
        {
            Debug.Log($"[{gameObject.name}] 当前路径配置为空");
            return;
        }
        
        float currentStoryTime = GameState.Instance != null ? GameState.Instance.GetStoryTime() : 0f;
        float currentRelativeTime = currentStoryTime - currentPath.pathStartStoryTime;
        int currentPathPoint = pathManager.CurrentPathPointIndex;
        
        Debug.Log($"[{gameObject.name}] === 时间控制状态 ===");
        Debug.Log($"当前故事时间: {FormatTime(currentStoryTime)}");
        Debug.Log($"路径起始时间: {FormatTime(currentPath.pathStartStoryTime)}");
        Debug.Log($"相对时间: {FormatTime(currentRelativeTime)}");
        Debug.Log($"当前路径点: {currentPathPoint}");
        Debug.Log($"当前速度: {agent.speed:F2}m/s (原始: {originalMoveSpeed:F2}m/s)");
        Debug.Log($"时间控制启用: {timeControlEnabled}");
        
        if (currentTargetTimePoint != null)
        {
            float targetAbsoluteTime = currentPath.pathStartStoryTime + currentTargetTimePoint.requiredStoryTime;
            Debug.Log($"目标时间点: 路径点{currentTargetTimePoint.pathPointIndex}, 时间{FormatTime(targetAbsoluteTime)}");
        }
        else
        {
            Debug.Log($"目标时间点: 无");
        }
        
        if (currentPath.timePoints != null && currentPath.timePoints.Count > 0)
        {
            Debug.Log($"路径时间点配置:");
            foreach (var tp in currentPath.timePoints)
            {
                float absoluteTime = currentPath.pathStartStoryTime + tp.requiredStoryTime;
                bool isPassed = currentRelativeTime >= tp.requiredStoryTime;
                string status = isPassed ? "[已过]" : "[未到]";
                Debug.Log($"  - 点{tp.pathPointIndex}: {FormatTime(absoluteTime)} {status}");
            }
        }
    }
    
    /// <summary>
    /// 强制重新计算速度（测试用）
    /// </summary>
    [ContextMenu("重新计算速度")]
    public void ForceRecalculateSpeed()
    {
        if (Application.isPlaying && timeControlEnabled && hasInitialized)
        {
            Debug.Log($"[{gameObject.name}] 强制重新计算速度");
            UpdateTargetTimePoint();
            UpdateSpeedBasedOnTime();
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] 无法重新计算速度 - 游戏未运行或时间控制未启用");
        }
    }
    
    /// <summary>
    /// 设置日志级别为静默
    /// </summary>
    [ContextMenu("日志级别: 静默")]
    public void SetLogLevelSilent()
    {
        SetDebugLogLevel(DebugLogLevel.Silent);
    }
    
    /// <summary>
    /// 设置日志级别为监控
    /// </summary>
    [ContextMenu("日志级别: 监控")]
    public void SetLogLevelMonitor()
    {
        SetDebugLogLevel(DebugLogLevel.Monitor);
    }
    
    /// <summary>
    /// 设置日志级别为详细
    /// </summary>
    [ContextMenu("日志级别: 详细")]
    public void SetLogLevelDetailed()
    {
        SetDebugLogLevel(DebugLogLevel.Detailed);
    }
    
    /// <summary>
    /// 手动触发路径切换时间控制重新初始化（测试用）
    /// </summary>
    [ContextMenu("测试路径切换时间控制")]
    public void TestPathSwitchTimeControl()
    {
        if (Application.isPlaying)
        {
            Debug.Log($"[{gameObject.name}] 手动触发路径切换时间控制重新初始化");
            OnPathSwitched();
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] 路径切换测试只能在游戏运行时使用");
        }
    }
    
    /// <summary>
    /// 诊断路径切换时的时间计算问题
    /// </summary>
    [ContextMenu("诊断路径切换时间")]
    public void DiagnosePathSwitchTiming()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning($"[{gameObject.name}] 路径切换诊断只能在游戏运行时使用");
            return;
        }
        
        PathConfig currentPath = pathManager.GetCurrentPathConfig();
        if (currentPath == null)
        {
            Debug.Log($"[{gameObject.name}] 当前路径配置为空，无法诊断");
            return;
        }
        
        float currentStoryTime = GameState.Instance != null ? GameState.Instance.GetStoryTime() : 0f;
        float currentRelativeTime = currentStoryTime - currentPath.pathStartStoryTime;
        
        Debug.Log($"[{gameObject.name}] === 路径切换时间诊断 ===");
        Debug.Log($"当前故事时间: {FormatTime(currentStoryTime)}");
        Debug.Log($"路径起始时间: {FormatTime(currentPath.pathStartStoryTime)}");
        Debug.Log($"相对时间: {FormatTime(currentRelativeTime)}");
        Debug.Log($"当前路径点: {pathManager.CurrentPathPointIndex}");
        Debug.Log($"当前位置: {transform.position}");
        
        // 分析时间控制点
        if (currentPath.timePoints != null && currentPath.timePoints.Count > 0)
        {
            Debug.Log($"时间控制点分析:");
            foreach (var tp in currentPath.timePoints.OrderBy(t => t.requiredStoryTime))
            {
                float absoluteTime = currentPath.pathStartStoryTime + tp.requiredStoryTime;
                bool isPassed = currentRelativeTime >= tp.requiredStoryTime;
                string status = isPassed ? "[已过]" : "[未到]";
                float timeDiff = tp.requiredStoryTime - currentRelativeTime;
                Debug.Log($"  - 点{tp.pathPointIndex}: 相对时间{tp.requiredStoryTime:F1}s, 绝对时间{FormatTime(absoluteTime)}, 时差{timeDiff:F1}s {status}");
            }
            
            // 找到当前应该的位置
            PathTimePoint lastPassed = currentPath.GetLastPassedTimePoint(currentRelativeTime);
            if (lastPassed != null)
            {
                Debug.Log($"最后经过的时间点: 点{lastPassed.pathPointIndex}, 时间{lastPassed.requiredStoryTime:F1}s");
            }
            else
            {
                Debug.Log($"还未经过任何时间点");
            }
        }
        else
        {
            Debug.Log($"无时间控制点配置");
        }
        
        // 检查瞬移计算
        Debug.Log($"=== 瞬移计算测试 ===");
        var pathCreator = PathRegistry.GetPathCreatorByID(pathManager.CurrentPathID);
        if (pathCreator != null)
        {
            Vector3 originalPos = transform.position;
            int originalPointIndex = pathManager.CurrentPathPointIndex;
            
            // 模拟瞬移计算（不实际执行）
            Debug.Log($"模拟瞬移到正确位置（当前相对时间: {currentRelativeTime:F1}s）");
            
            // 恢复原始状态（确保这只是诊断，不影响实际游戏）
            // transform.position = originalPos;
            // pathManager.SetCurrentPathPointIndex(originalPointIndex);
            
            Debug.Log($"诊断完成，位置和状态未改变");
        }
    }
    
    /// <summary>
    /// 测试Update方法的持续更新
    /// </summary>
    [ContextMenu("测试Update持续更新")]
    public void TestUpdateContinuousTracking()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning($"[{gameObject.name}] 只能在游戏运行时测试");
            return;
        }
        
        Debug.Log($"[{gameObject.name}] === 测试Update持续更新 ===");
        Debug.Log($"时间控制启用: {timeControlEnabled}");
        Debug.Log($"已初始化: {hasInitialized}");
        Debug.Log($"当前目标: {(currentTargetTimePoint != null ? $"路径点{currentTargetTimePoint.pathPointIndex}" : "无")}");
        Debug.Log($"上次更新时间: {lastUpdateTime:F2}");
        Debug.Log($"当前时间: {Time.time:F2}");
        Debug.Log($"距上次更新: {Time.time - lastUpdateTime:F2}s");
        Debug.Log($"当前Agent速度: {agent.speed:F3}m/s");
        
        // 强制清空目标并触发Update逻辑
        currentTargetTimePoint = null;
        Debug.Log($"已清空当前目标，下次Update应该重新查找目标");
        
        // 立即调用一次目标查找
        UpdateTargetTimePoint();
        if (currentTargetTimePoint != null)
        {
            Debug.Log($"立即查找到目标: 路径点{currentTargetTimePoint.pathPointIndex}");
            UpdateSpeedBasedOnTime();
            Debug.Log($"立即更新速度: {agent.speed:F3}m/s");
        }
        else
        {
            Debug.Log($"立即查找未找到目标");
        }
        
        Debug.Log($"=== 测试完成，请观察后续Update日志 ===");
    }
    
    /// <summary>
    /// 测试路径终点目标设置
    /// </summary>
    [ContextMenu("测试路径终点目标")]
    public void TestPathEndTarget()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning($"[{gameObject.name}] 只能在游戏运行时测试");
            return;
        }
        
        Debug.Log($"[{gameObject.name}] === 测试路径终点目标设置 ===");
        
        PathConfig currentPath = pathManager.GetCurrentPathConfig();
        var pathCreator = PathRegistry.GetPathCreatorByID(pathManager.CurrentPathID);
        
        if (currentPath == null || pathCreator == null)
        {
            Debug.LogWarning($"[{gameObject.name}] 路径数据不完整");
            return;
        }
        
        int currentPoint = pathManager.CurrentPathPointIndex;
        int pathEndIndex = pathCreator.GetPathPointCount() - 1;
        
        Debug.Log($"当前路径点: {currentPoint}");
        Debug.Log($"路径终点索引: {pathEndIndex}");
        Debug.Log($"时间控制点数量: {currentPath.timePoints?.Count ?? 0}");
        
        // 模拟清空时间控制点目标并重新查找
        currentTargetTimePoint = null;
        UpdateTargetTimePoint();
        
        if (currentTargetTimePoint != null)
        {
            Debug.Log($"找到目标: 路径点{currentTargetTimePoint.pathPointIndex}, 时间{currentTargetTimePoint.requiredStoryTime:F1}s, 停留{currentTargetTimePoint.stopTime}s");
            
            // 计算距离和速度
            float distance = CalculateDistanceToTarget();
            float requiredSpeed = CalculateRequiredSpeed();
            Debug.Log($"到目标距离: {distance:F1}m");
            Debug.Log($"所需速度: {requiredSpeed:F3}m/s");
        }
        else
        {
            Debug.Log($"未找到任何目标");
        }
        
        Debug.Log($"=== 测试完成 ===");
    }
    
    /// <summary>
    /// 诊断路径切换后的时间控制状态
    /// </summary>
    [ContextMenu("诊断路径切换后状态")]
    public void DiagnosePathSwitchState()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning($"[{gameObject.name}] 只能在游戏运行时诊断");
            return;
        }
        
        Debug.Log($"[{gameObject.name}] === 路径切换后状态诊断 ===");
        
        // 基本状态
        Debug.Log($"时间控制启用: {timeControlEnabled}");
        Debug.Log($"已初始化: {hasInitialized}");
        Debug.Log($"当前Agent速度: {agent.speed:F3} (原始: {originalMoveSpeed:F3})");
        
        // 路径信息
        PathConfig currentPath = pathManager.GetCurrentPathConfig();
        if (currentPath != null)
        {
            float currentStoryTime = GameState.Instance?.GetStoryTime() ?? 0f;
            float currentRelativeTime = currentStoryTime - currentPath.pathStartStoryTime;
            
            Debug.Log($"当前路径: {currentPath.pathName}");
            Debug.Log($"路径起始时间: {FormatTime(currentPath.pathStartStoryTime)}");
            Debug.Log($"当前故事时间: {FormatTime(currentStoryTime)}");
            Debug.Log($"相对时间: {FormatTime(currentRelativeTime)}");
            Debug.Log($"当前路径点: {pathManager.CurrentPathPointIndex}");
            
            // 时间控制点信息
            if (currentPath.timePoints != null && currentPath.timePoints.Count > 0)
            {
                Debug.Log($"时间控制点数量: {currentPath.timePoints.Count}");
                foreach (var tp in currentPath.timePoints.OrderBy(t => t.requiredStoryTime))
                {
                    bool isPassed = currentRelativeTime >= tp.requiredStoryTime;
                    string status = isPassed ? "[已过]" : "[未到]";
                    Debug.Log($"  - 点{tp.pathPointIndex}: {tp.requiredStoryTime:F1}s {status}");
                }
            }
            else
            {
                Debug.Log($"无时间控制点配置");
            }
            
            // 当前目标
            if (currentTargetTimePoint != null)
            {
                Debug.Log($"当前目标: 路径点{currentTargetTimePoint.pathPointIndex}, 时间{currentTargetTimePoint.requiredStoryTime:F1}s");
                
                // 计算所需速度
                float requiredSpeed = CalculateRequiredSpeed();
                float distance = CalculateDistanceToTarget();
                Debug.Log($"到目标距离: {distance:F1}m");
                Debug.Log($"所需速度: {requiredSpeed:F3}m/s");
            }
            else
            {
                Debug.Log($"当前目标: 无");
            }
        }
        else
        {
            Debug.Log($"当前路径配置: 空");
        }
        
        Debug.Log($"=== 诊断完成 ===");
    }
    
    /// <summary>
    /// 测试极端速度设置（调试用）
    /// </summary>
    [ContextMenu("测试极端速度")]
    public void TestExtremeSpeed()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning($"[{gameObject.name}] 测试极端速度只能在游戏运行时使用");
            return;
        }
        
        LogMonitor("=== 测试极端速度设置 ===");
        
        float originalSpeed = agent.speed;
        LogMonitor($"当前速度: {originalSpeed:F3}m/s");
        
        // 测试极小速度
        float testSpeedMin = originalMoveSpeed * speedMultiplierMin;
        agent.speed = testSpeedMin;
        LogMonitor($"尝试设置极小速度: {testSpeedMin:F3}m/s，实际: {agent.speed:F3}m/s");
        
        // 测试极大速度
        float testSpeedMax = originalMoveSpeed * speedMultiplierMax;
        agent.speed = testSpeedMax;
        LogMonitor($"尝试设置极大速度: {testSpeedMax:F3}m/s，实际: {agent.speed:F3}m/s");
        
        // 恢复原始速度
        agent.speed = originalSpeed;
        LogMonitor($"恢复原始速度: {agent.speed:F3}m/s");
        
        LogDetailed($"NavMeshAgent属性:");
        LogDetailed($"  speed: {agent.speed:F3}");
        LogDetailed($"  acceleration: {agent.acceleration:F3}");
        LogDetailed($"  angularSpeed: {agent.angularSpeed:F3}");
        LogDetailed($"  baseOffset: {agent.baseOffset:F3}");
        LogDetailed($"  radius: {agent.radius:F3}");
        LogDetailed($"  height: {agent.height:F3}");
    }
    
    /// <summary>
    /// 延迟执行初始瞬移（确保所有组件初始化完成）
    /// </summary>
    private System.Collections.IEnumerator PerformInitialTeleportAfterDelay()
    {
        // 等待一帧，确保所有组件都已初始化
        yield return null;
        
        // 再等待一小段时间，确保GameState也已完全初始化
        yield return new WaitForSeconds(0.1f);
        
        // 检查是否在幕状态下并执行初始瞬移
        if (GameState.Instance != null && timeControlEnabled)
        {
            GameState.State currentState = GameState.Instance.GetCurrentState();
            if (currentState == GameState.State.Act1 || currentState == GameState.State.Act2 || currentState == GameState.State.Act3)
            {
                Debug.Log($"[{gameObject.name}] 游戏开始时执行初始瞬移，当前幕: {currentState}");
                TeleportToCorrectPositionForNewAct();
            }
        }
    }
    
    /// <summary>
    /// 延迟强制触发初始速度计算
    /// </summary>
    private System.Collections.IEnumerator ForceInitialSpeedCalculation()
    {
        // 等待更长时间，确保所有系统都完全初始化
        yield return new WaitForSeconds(0.2f);
        
        // 强制触发一次速度计算
        if (timeControlEnabled && hasInitialized)
        {
            Debug.Log($"[{gameObject.name}] 延迟强制触发初始速度计算");
            UpdateTargetTimePoint();
            if (currentTargetTimePoint != null)
            {
                UpdateSpeedBasedOnTime();
                Debug.Log($"[{gameObject.name}] 强制速度计算完成，目标：路径点{currentTargetTimePoint.pathPointIndex}，当前速度：{agent.speed:F2}");
            }
            else
            {
                Debug.Log($"[{gameObject.name}] 强制速度计算完成，无目标时间点，当前速度：{agent.speed:F2}");
            }
        }
    }
    
    /// <summary>
    /// 延迟触发路径切换，避免在Update循环中直接修改状态
    /// </summary>
    private System.Collections.IEnumerator TriggerPathSwitchAfterDelay()
    {
        // 等待一帧，确保当前Update循环完成
        yield return null;
        
        LogMonitor("🔄 执行虚拟终点触发的路径切换");
        
        // 🔍 添加循环保护：记录触发时间，防止短时间内重复触发
        if (lastPathSwitchTime > 0 && Time.time - lastPathSwitchTime < 0.5f)
        {
            LogError($"❌ 路径切换过于频繁！上次切换时间: {Time.time - lastPathSwitchTime:F3}秒前，可能存在无限循环！");
            LogError($"❌ 停止路径切换以防止系统崩溃");
            yield break;
        }
        
        lastPathSwitchTime = Time.time;
        
        if (pathManager != null)
        {
            LogMonitor("✅ 调用PathManager.ForceCompletePathFromTimeController()强制路径切换");
            pathManager.ForceCompletePathFromTimeController();
        }
        else
        {
            LogError("❌ PathManager引用为空，无法触发路径切换");
        }
    }
    
    #endregion
} 