using UnityEngine;
using UnityEngine.AI;

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
    
    void Awake()
    {
        controller = GetComponent<NPCController>();
        pathManager = GetComponent<NPCPathManager>();
        agent = GetComponent<NavMeshAgent>();
        
        originalMoveSpeed = agent.speed;
        // Debug.Log($"[{gameObject.name}] NPCTimeController初始化，原始速度: {originalMoveSpeed}");
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
        // Debug.Log($"[{gameObject.name}] 幕开始，重新初始化时间控制");
        InitializeTimeControl();
    }
    
    private void OnPathPointReached(int pathPointIndex)
    {
        // 路径点到达时，更新目标时间点
        UpdateTargetTimePoint();
    }
    
    /// <summary>
    /// 初始化时间控制系统
    /// </summary>
    private void InitializeTimeControl()
    {
        if (!timeControlEnabled) 
        {
            agent.speed = originalMoveSpeed;
            return;
        }
        
        UpdateTargetTimePoint();
        hasInitialized = true;
        // Debug.Log($"[{gameObject.name}] 时间控制系统已初始化");
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
            return;
        }
        
        int currentPathPoint = pathManager.CurrentPathPointIndex;
        float currentStoryTime = GameState.Instance != null ? GameState.Instance.GetStoryTime() : 0f;
        float currentRelativeTime = currentStoryTime - currentPath.pathStartStoryTime;
        
        // 查找下一个需要到达的时间控制点
        foreach (var timePoint in currentPath.timePoints)
        {
            if (timePoint.pathPointIndex > currentPathPoint || 
                (timePoint.pathPointIndex == currentPathPoint && timePoint.requiredStoryTime > currentRelativeTime))
            {
                if (currentTargetTimePoint == null || 
                    timePoint.pathPointIndex < currentTargetTimePoint.pathPointIndex ||
                    (timePoint.pathPointIndex == currentTargetTimePoint.pathPointIndex && 
                     timePoint.requiredStoryTime < currentTargetTimePoint.requiredStoryTime))
                {
                    currentTargetTimePoint = timePoint;
                }
            }
        }
        
        // if (currentTargetTimePoint != null)
        // {
        //     Debug.Log($"[{gameObject.name}] 时间控制目标：路径点{currentTargetTimePoint.pathPointIndex}，时间{currentTargetTimePoint.requiredStoryTime:F1}s");
        // }
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
                // Debug.Log($"[{gameObject.name}] 无时间控制点，恢复原始速度: {originalMoveSpeed}");
            }
            return;
        }
        
        float requiredSpeed = CalculateRequiredSpeed();
        
        if (requiredSpeed > teleportThreshold)
        {
            // 需要瞬移
            Debug.Log($"[{gameObject.name}] 瞬移到路径点{currentTargetTimePoint.pathPointIndex}"); // 保留关键信息
            TeleportToTimePoint();
        }
        else
        {
            // 调整速度
            float clampedSpeed = Mathf.Clamp(requiredSpeed, 
                                           originalMoveSpeed * speedMultiplierMin, 
                                           originalMoveSpeed * speedMultiplierMax);
            
            if (Mathf.Abs(agent.speed - clampedSpeed) > 0.1f)
            {
                agent.speed = clampedSpeed;
                float currentStoryTime = GameState.Instance.GetStoryTime();
                Debug.Log($"[{gameObject.name}] 在故事时间{FormatTime(currentStoryTime)}到达点{currentTargetTimePoint.pathPointIndex}，调整速度为{clampedSpeed:F1}m/s"); // 保留关键信息
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
        
        // 计算剩余时间
        PathConfig currentPath = pathManager.GetCurrentPathConfig();
        float currentStoryTime = GameState.Instance.GetStoryTime();
        float currentRelativeTime = currentStoryTime - currentPath.pathStartStoryTime;
        float remainingTime = currentTargetTimePoint.requiredStoryTime - currentRelativeTime;
        
        if (remainingTime <= 0)
        {
            return originalMoveSpeed * speedMultiplierMax; // 时间已过，需要快速移动
        }
        
        return distance / remainingTime;
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
        
        // Debug.Log($"[{gameObject.name}] 瞬移到路径点{currentTargetTimePoint.pathPointIndex}，位置：{targetPos}");
        
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
        // Debug.Log($"[{gameObject.name}] 时间控制{(enabled ? "已启用" : "已禁用")}");
        
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
} 