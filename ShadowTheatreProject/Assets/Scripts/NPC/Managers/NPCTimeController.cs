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
    [Header("时间控制设置")]
    [SerializeField] private bool timeControlEnabled = true;
    [SerializeField] private float speedMultiplierMin = 0.1f; // 最小速度倍数
    [SerializeField] private float speedMultiplierMax = 10f;  // 最大速度倍数
    [SerializeField] private float teleportThreshold = 60f;  // 超过这个速度就瞬移
    
    // 组件引用
    private NPCController controller;
    private NPCPathManager pathManager;
    private NavMeshAgent agent;
    private float originalMoveSpeed;
    
    // 时间控制状态
    private PathTimePoint currentTargetTimePoint;
    private bool hasInitialized = false;
    private float lastSetSpeed = -1f; // 记录上次设置的速度，避免重复日志
    
    void Awake()
    {
        controller = GetComponent<NPCController>();
        pathManager = GetComponent<NPCPathManager>();
        agent = GetComponent<NavMeshAgent>();
        
        originalMoveSpeed = agent.speed;
        Debug.Log($"[{gameObject.name}] NPCTimeController初始化，原始速度: {originalMoveSpeed}");
    }
    
    void Start()
    {
        // 订阅故事时间更新事件
        if (EventCenter.Instance != null)
        {
            EventCenter.Instance.Subscribe(GameState.EventNames.STORY_TIME_UPDATED, () => OnStoryTimeUpdated());
            EventCenter.Instance.Subscribe(GameState.EventNames.ACT_TIME_STARTED, () => OnActTimeStarted());
        }
        
        // 订阅路径点到达事件
        if (pathManager != null)
        {
            pathManager.OnPathPointReached += OnPathPointReached;
        }
        
        InitializeTimeControl();
        
        // 在游戏开始时执行初始瞬移
        // 使用协程延迟执行，确保所有组件都已初始化
        StartCoroutine(PerformInitialTeleportAfterDelay());
    }
    
    void Update()
    {
        // 修复：禁用持续监控，避免重复计算
        // 时间控制只在路径点到达瞬间进行一次性计算
        
        // 注释掉原来的重复计算逻辑
        /*
        // 持续监控时间控制，确保NPC准时到达时间点
        if (timeControlEnabled && hasInitialized && currentTargetTimePoint != null)
        {
            // 检查是否已经到达或超过目标时间点，避免重复计算
            PathConfig currentPath = pathManager.GetCurrentPathConfig();
            if (currentPath != null && GameState.Instance != null)
            {
                float currentStoryTime = GameState.Instance.GetStoryTime();
                float currentRelativeTime = currentStoryTime - currentPath.pathStartStoryTime;
                int currentPathPoint = pathManager.CurrentPathPointIndex;
                
                // 如果已经到达或超过目标时间点，停止计算
                if (currentPathPoint >= currentTargetTimePoint.pathPointIndex && 
                    currentRelativeTime >= currentTargetTimePoint.requiredStoryTime)
                {
                    // Debug.Log($"[{gameObject.name}] 已到达目标时间点{currentTargetTimePoint.pathPointIndex}，停止速度计算");
                    return;
                }
            }
            
            // 每0.5秒更新一次速度（避免过于频繁）
            if (Time.time % 0.5f < 0.1f)
            {
                UpdateSpeedBasedOnTime();
            }
        }
        */
    }
    
    void OnDisable()
    {
        // 取消事件订阅
        if (EventCenter.Instance != null)
        {
            EventCenter.Instance.Unsubscribe(GameState.EventNames.STORY_TIME_UPDATED, () => OnStoryTimeUpdated());
            EventCenter.Instance.Unsubscribe(GameState.EventNames.ACT_TIME_STARTED, () => OnActTimeStarted());
        }
        
        if (pathManager != null)
        {
            pathManager.OnPathPointReached -= OnPathPointReached;
        }
    }
    
    private void OnStoryTimeUpdated()
    {
        if (timeControlEnabled && hasInitialized)
        {
            UpdateSpeedBasedOnTime();
        }
    }
    
    private void OnActTimeStarted()
    {
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
        
        // 新幕开始时，计算NPC应该在的位置并瞬移
        if (timeControlEnabled && hasInitialized)
        {
            Debug.Log($"[{gameObject.name}] 幕开始，执行位置校正瞬移");
            TeleportToCorrectPositionForNewAct();
        }
    }
    
    private void OnPathPointReached(int pathPointIndex)
    {
        Debug.Log($"[{gameObject.name}] 时间控制器收到路径点到达事件：点{pathPointIndex}");
        
        // 检查是否到达了当前的目标时间点
        if (currentTargetTimePoint != null && pathPointIndex == currentTargetTimePoint.pathPointIndex)
        {
            Debug.Log($"[{gameObject.name}] 到达目标时间控制点{pathPointIndex}，标记完成并查找下一个目标");
            
            // 关键修复：立即清空当前目标，避免重复选择
            currentTargetTimePoint = null;
        }
        
        // 路径点到达时，查找下一个目标时间点
        UpdateTargetTimePoint();
        
        // 只在有新的目标时间点时才计算速度
        if (timeControlEnabled && hasInitialized && currentTargetTimePoint != null)
        {
            Debug.Log($"[{gameObject.name}] 计算到下一个时间控制点的速度");
            UpdateSpeedBasedOnTime();
        }
        else if (currentTargetTimePoint == null)
        {
            // 没有更多时间控制点，恢复原始速度
            if (Mathf.Abs(agent.speed - originalMoveSpeed) > 0.1f)
            {
                agent.speed = originalMoveSpeed;
                Debug.Log($"[{gameObject.name}] 所有时间控制点已完成，恢复原始速度: {originalMoveSpeed}");
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
            UpdateSpeedBasedOnTime();
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
                // 瞬移到最后已过的时间点
                Vector3 targetPos = pathCreator.GetPathPointPosition(lastPassedTimePoint.pathPointIndex);
                transform.position = targetPos;
                pathManager.SetCurrentPathPointIndex(lastPassedTimePoint.pathPointIndex);
                Debug.Log($"[{gameObject.name}] 瞬移到已过时间点{lastPassedTimePoint.pathPointIndex}，位置: {targetPos}, 时间: {FormatTime(lastPassedTimePoint.requiredStoryTime)}");
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
                
                // 根据进度设置当前路径点索引
                int estimatedPointIndex = Mathf.FloorToInt(progress * nextTimePoint.pathPointIndex) - 1;
                pathManager.SetCurrentPathPointIndex(estimatedPointIndex);
                
                Debug.Log($"[{gameObject.name}] 在第一个时间点前插值，进度{progress:P1}，位置: {interpolatedPos}，预估路径点: {estimatedPointIndex}");
            }
            else
            {
                // 瞬移到起始点
                Vector3 startPos = pathCreator.GetPathPointPosition(0);
                transform.position = startPos;
                pathManager.SetCurrentPathPointIndex(-1);
                Debug.Log($"[{gameObject.name}] 瞬移到路径起始点: {startPos}");
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
        
        // 根据时间进度计算当前路径点索引
        int pathPointRange = toPoint.pathPointIndex - fromPoint.pathPointIndex;
        int estimatedPointIndex = fromPoint.pathPointIndex + Mathf.FloorToInt(timeProgress * pathPointRange);
        pathManager.SetCurrentPathPointIndex(estimatedPointIndex);
        
        Debug.Log($"[{gameObject.name}] 在时间点{fromPoint.pathPointIndex}-{toPoint.pathPointIndex}间插值，时间进度{timeProgress:P1}，位置: {interpolatedPos}，路径点: {estimatedPointIndex}");
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
        
        // 估算当前路径点索引
        int estimatedPointIndex = Mathf.FloorToInt(pathProgress * (pathCreator.GetPathPointCount() - 1)) - 1;
        pathManager.SetCurrentPathPointIndex(estimatedPointIndex);
        
        Debug.Log($"[{gameObject.name}] 无时间控制点，按平均速度计算位置，路径进度{pathProgress:P1}，位置: {interpolatedPos}，估算路径点: {estimatedPointIndex}");
    }
    
    /// <summary>
    /// 更新目标时间控制点
    /// </summary>
    private void UpdateTargetTimePoint()
    {
        currentTargetTimePoint = null;
        
        PathConfig currentPath = pathManager.GetCurrentPathConfig();
        if (currentPath == null || currentPath.timePoints == null || currentPath.timePoints.Count == 0)
        {
            Debug.Log($"[{gameObject.name}] 无时间控制点配置");
            return;
        }
        
        int currentPathPoint = pathManager.CurrentPathPointIndex;
        float currentStoryTime = GameState.Instance != null ? GameState.Instance.GetStoryTime() : 0f;
        float currentRelativeTime = currentStoryTime - currentPath.pathStartStoryTime;
        
        Debug.Log($"[{gameObject.name}] 查找目标时间点 - 当前路径点: {currentPathPoint}, 故事时间: {FormatTime(currentStoryTime)}, 相对时间: {FormatTime(currentRelativeTime)}");
        
        // 按时间顺序排序时间控制点
        var sortedTimePoints = currentPath.timePoints.OrderBy(tp => tp.requiredStoryTime).ToList();
        
        // 查找下一个需要到达的时间控制点
        foreach (var timePoint in sortedTimePoints)
        {
            bool isValidTarget = false;
            
            if (currentPathPoint == -1)
            {
                // 路径刚开始，查找第一个时间点（任何时间点都是有效的）
                isValidTarget = true;
                Debug.Log($"[{gameObject.name}] 路径开始，找到第一个时间控制点: 点{timePoint.pathPointIndex}, 时间{timePoint.requiredStoryTime}s");
            }
            else
            {
                // 已在路径上，查找未来的时间点
                // 条件：路径点索引更大，或者相对时间还没到达
                bool isFuturePoint = timePoint.pathPointIndex > currentPathPoint;
                bool isTimeNotReached = timePoint.requiredStoryTime > currentRelativeTime + 0.1f; // 加0.1秒容差避免浮点误差
                
                isValidTarget = isFuturePoint || (timePoint.pathPointIndex >= currentPathPoint && isTimeNotReached);
                
                if (isValidTarget)
                {
                    Debug.Log($"[{gameObject.name}] 找到下一个时间控制点: 点{timePoint.pathPointIndex}, 时间{timePoint.requiredStoryTime}s (当前在点{currentPathPoint}, 时间{currentRelativeTime:F1}s)");
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
        }
        else
        {
            Debug.Log($"[{gameObject.name}] 未找到下一个时间控制目标");
        }
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
                Debug.Log($"[{gameObject.name}] 无时间控制点，恢复原始速度: {originalMoveSpeed}");
            }
            return;
        }
        
        float requiredSpeed = CalculateRequiredSpeed();
        float currentStoryTime = GameState.Instance.GetStoryTime();
        PathConfig currentPath = pathManager.GetCurrentPathConfig();
        float targetAbsoluteTime = currentPath.pathStartStoryTime + currentTargetTimePoint.requiredStoryTime;
        
        if (requiredSpeed > teleportThreshold)
        {
            // 需要瞬移
            Debug.Log($"[{gameObject.name}] 故事时间{FormatTime(currentStoryTime)}需要瞬移到路径点{currentTargetTimePoint.pathPointIndex}（目标时间{FormatTime(targetAbsoluteTime)}）- 所需速度{requiredSpeed:F1}超过阈值{teleportThreshold}");
            TeleportToTimePoint();
        }
        else
        {
            // 调整速度
            float clampedSpeed = Mathf.Clamp(requiredSpeed, 
                                           originalMoveSpeed * speedMultiplierMin, 
                                           originalMoveSpeed * speedMultiplierMax);
            
            // 只有速度变化超过阈值时才更新和输出日志
            if (Mathf.Abs(agent.speed - clampedSpeed) > 0.2f)
            {
                agent.speed = clampedSpeed;
                lastSetSpeed = clampedSpeed;
                Debug.Log($"[{gameObject.name}] 故事时间{FormatTime(currentStoryTime)}到达点{currentTargetTimePoint.pathPointIndex}（目标时间{FormatTime(targetAbsoluteTime)}），调整速度为{clampedSpeed:F1}m/s（需求速度{requiredSpeed:F1}）");
            }
        }
    }
    
    /// <summary>
    /// 计算到达目标时间点所需的速度
    /// </summary>
    private float CalculateRequiredSpeed()
    {
        if (currentTargetTimePoint == null || pathManager == null) 
            return originalMoveSpeed;
        
        // 获取当前位置到目标点的距离
        float distance = CalculateDistanceToTarget();
        
        // 如果距离为0，说明已经在目标位置，返回原始速度
        if (distance <= 0.1f)
        {
            // Debug.Log($"[{gameObject.name}] 已在目标位置附近，距离: {distance:F1}m，使用原始速度");
            return originalMoveSpeed;
        }
        
        // 计算剩余时间
        PathConfig currentPath = pathManager.GetCurrentPathConfig();
        float currentStoryTime = GameState.Instance.GetStoryTime();
        float currentRelativeTime = currentStoryTime - currentPath.pathStartStoryTime;
        float remainingTime = currentTargetTimePoint.requiredStoryTime - currentRelativeTime;
        
        // 减去路径点停留时间（如果NPC还需要在路径点停留）
        float totalStopTime = CalculateRemainingStopTime();
        float movingTime = remainingTime - totalStopTime;
        
        // 只有在需要时才输出详细的调试信息
        if (distance > 1f || movingTime < 1f)
        {
            Debug.Log($"[{gameObject.name}] 速度计算 - 距离: {distance:F1}m, 剩余时间: {remainingTime:F1}s, 停留时间: {totalStopTime:F1}s, 移动时间: {movingTime:F1}s");
        }
        
        if (movingTime <= 0)
        {
            Debug.Log($"[{gameObject.name}] 移动时间不足或已过期，需要快速移动");
            return originalMoveSpeed * speedMultiplierMax; // 时间已过，需要快速移动
        }
        
        float requiredSpeed = distance / movingTime;
        
        // 只有在需要时才输出速度计算结果
        if (distance > 1f || Mathf.Abs(requiredSpeed - agent.speed) > 0.5f)
        {
            Debug.Log($"[{gameObject.name}] 计算所需速度: {requiredSpeed:F1}m/s");
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
        if (currentPath == null || currentPath.timePoints == null || currentTargetTimePoint == null) 
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
                        float timePassedSinceArrival = currentRelativeTime - timePoint.requiredStoryTime;
                        float remainingStopTime = Mathf.Max(0, timePoint.stopTime - timePassedSinceArrival);
                        totalStopTime += remainingStopTime;
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
        
        float distance = 0f;
        int currentPoint = pathManager.CurrentPathPointIndex;
        int targetPoint = currentTargetTimePoint.pathPointIndex;
        
        // 如果目标点就是当前点，距离为0
        if (targetPoint <= currentPoint) return 0f;
        
        // 计算从当前位置到目标点的路径距离
        Vector3 currentPos = transform.position;
        
        // 处理路径刚开始的情况（currentPoint = -1）
        if (currentPoint == -1)
        {
            // 从当前位置到目标路径点的距离
            for (int i = 0; i <= targetPoint && i < pathCreator.GetPathPointCount(); i++)
            {
                Vector3 pointPos = pathCreator.GetPathPointPosition(i);
                
                if (i == 0)
                {
                    // 从当前位置到第一个路径点
                    distance += Vector3.Distance(currentPos, pointPos);
                }
                else if (i <= targetPoint)
                {
                    // 路径点之间的距离
                    Vector3 prevPointPos = pathCreator.GetPathPointPosition(i - 1);
                    distance += Vector3.Distance(prevPointPos, pointPos);
                }
            }
        }
        else
        {
            // 先计算到下一个路径点的距离
            if (currentPoint < pathCreator.GetPathPointCount() - 1)
            {
                Vector3 nextPointPos = pathCreator.GetPathPointPosition(currentPoint + 1);
                distance += Vector3.Distance(currentPos, nextPointPos);
                
                // 然后计算后续路径点之间的距离
                for (int i = currentPoint + 1; i < targetPoint && i < pathCreator.GetPathPointCount() - 1; i++)
                {
                    Vector3 pointA = pathCreator.GetPathPointPosition(i);
                    Vector3 pointB = pathCreator.GetPathPointPosition(i + 1);
                    distance += Vector3.Distance(pointA, pointB);
                }
            }
        }
        
        Debug.Log($"[{gameObject.name}] 计算距离 - 从路径点{currentPoint}到{targetPoint}: {distance:F1}m");
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
        
        Debug.Log($"[{gameObject.name}] 瞬移到路径点{currentTargetTimePoint.pathPointIndex}，位置：{targetPos}");
        
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
    
    #endregion
} 