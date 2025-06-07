using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "New NPC Data", menuName = "Shadow Theatre/NPC Data")]
public class NPCData : ScriptableObject
{
    [Header("基本信息")]
    public string npcID;
    public string npcName;
    public float currentScore = 0f;  // NPC当前分数
    public float moveSpeed = 1.0f;   // NPC移动速度

    [Header("路径配置")]
    public List<PathConfig> paths = new List<PathConfig>();

    [HideInInspector]
    public List<PathConnection> pathConnections = new List<PathConnection>();

    [HideInInspector]
    public List<PathDecision> pathDecisions = new List<PathDecision>();

    // 用于导入导出的路径
#if UNITY_EDITOR
    [HideInInspector]
    public string importExportPath = "";
#endif

    // 获取路径点相对位置信息
    public string GetPathPointRelativePosition(Transform pathPoint)
    {
        if (pathPoint == null) return "未知";

        Transform parent = pathPoint.parent;
        if (parent != null && parent.name.Contains("PathPoints"))
        {
            // 找到路径创建器
            Transform rootPath = parent.parent;
            MultiPointPathCreator pathCreator = rootPath.GetComponent<MultiPointPathCreator>();

            if (pathCreator != null)
            {
                int childCount = parent.childCount;
                int childIndex = pathPoint.GetSiblingIndex();
                float relativePos = (childCount > 1) ? (float)childIndex / (childCount - 1) : 0;

                return $"{rootPath.name} 的 {relativePos:P0}";
            }
        }

        return "未知";
    }

    // 根据分数查找可用路径
    public PathConfig FindPathByScore(float score)
    {
        foreach (var path in paths)
        {
            if (score >= path.minScore && score <= path.maxScore)
                return path;
        }
        return null;
    }

    // 查找路径配置
    public PathConfig FindPathById(string pathId)
    {
        return paths.Find(p => p.pathID == pathId);
    }

    // 重置NPC分数到0
    public void ResetScore()
    {
        currentScore = 0f;
        Debug.Log($"[{npcName}] 分数已重置为0");
    }
}

[Serializable]
public class PathConfig
{
    [Header("路径基本信息")]
    public string pathID;  // 对应MultiPointPathCreator的ID
    public string pathName;  // 显示名称

    [Header("分数要求")]
    [Tooltip("进入此路径的最低分数")]
    public float minScore = 0f;
    [Tooltip("进入此路径的最高分数")]
    public float maxScore = 100f;

    [Header("路径事件")]
    public List<PathEvent> events = new List<PathEvent>();

    [Header("路径分支")]
    [Tooltip("此路径结束后的下一条路径")]
    public List<PathBranch> nextPaths = new List<PathBranch>();

    [Header("路径动作")]
    public List<ActionData> pathActions = new List<ActionData>();
    
    [Header("时间控制")]
    [Tooltip("NPC应该何时到达此路径起始点的故事时间")]
    public float pathStartStoryTime = 0f;
    
    [Tooltip("NPC应该何时离开此路径的故事时间（0=自动使用下一个路径的开始时间，>0=手动指定结束时间）")]
    public float pathEndStoryTime = 0f;
    
    [Tooltip("此路径上的关键时间控制点")]
    public List<PathTimePoint> timePoints = new List<PathTimePoint>();

    // 在PathConfig中添加一个新的路径点停留配置
    [Header("路径点停留配置")]
    public List<PathPointStopConfig> pathPointStopConfigs = new List<PathPointStopConfig>();

    // 根据分数选择下一条路径
    public string SelectNextPathByScore(float score)
    {
        // 如果没有分支，返回null
        if (nextPaths.Count == 0) return null;

        // 默认使用第一个路径
        string selectedPath = nextPaths[0].nextPathID;

        // 遍历所有路径选项
        foreach (var branch in nextPaths)
        {
            // 使用分数区间判定
            if (branch.IsScoreInRange(score))
            {
                selectedPath = branch.nextPathID;
                break; // 找到第一个匹配的分支就停止
            }
        }

        return selectedPath;
    }
    
    // 查找下一个时间控制点
    public PathTimePoint GetNextTimePoint(int currentPathPoint, float currentRelativeTime)
    {
        if (timePoints == null || timePoints.Count == 0) return null;
        
        // 先按照路径点索引和时间排序
        var sortedTimePoints = timePoints
            .Where(tp => tp.pathPointIndex >= currentPathPoint && 
                        (tp.pathPointIndex > currentPathPoint || tp.requiredStoryTime > currentRelativeTime))
            .OrderBy(tp => tp.pathPointIndex)
            .ThenBy(tp => tp.requiredStoryTime)
            .ToList();
        
        return sortedTimePoints.FirstOrDefault();
    }
    
    // 查找指定时间之前的最后一个时间点
    public PathTimePoint GetLastPassedTimePoint(float currentRelativeTime)
    {
        if (timePoints == null || timePoints.Count == 0) return null;
        
        return timePoints
            .Where(tp => tp.requiredStoryTime <= currentRelativeTime)
            .OrderByDescending(tp => tp.requiredStoryTime)
            .FirstOrDefault();
    }
    
    // 获取指定路径点的停留时间
    public float GetPathPointStopTime(int pathPointIndex)
    {
        // 首先检查PathTimePoint中的停留时间
        if (timePoints != null)
        {
            var timePoint = timePoints.FirstOrDefault(tp => tp.pathPointIndex == pathPointIndex);
            if (timePoint != null && timePoint.stopTime > 0)
            {
                return timePoint.stopTime;
            }
        }
        
        // 然后检查路径点停留配置
        if (pathPointStopConfigs != null)
        {
            var stopConfig = pathPointStopConfigs.FirstOrDefault(config => config.pathPointIndex == pathPointIndex);
            if (stopConfig != null)
            {
                return stopConfig.stopTime;
            }
        }
        
        return 0f; // 默认不停留
    }
    
    /// <summary>
    /// 检查路径是否可以结束（考虑时间因素）
    /// </summary>
    /// <param name="currentRelativeTime">当前相对于路径开始的时间</param>
    /// <param name="npcData">NPC数据，用于查找下一个路径的开始时间</param>
    /// <param name="currentScore">当前分数，用于确定下一个路径</param>
    /// <returns>true表示可以结束路径</returns>
    public bool CanEndPath(float currentRelativeTime, NPCData npcData = null, float currentScore = 0f)
    {
        // 获取实际的路径结束时间（这是绝对时间）
        float actualEndTime = GetActualEndTime(npcData, currentScore);
        
        // 如果没有有效的结束时间，表示无限制，可以立即结束
        if (actualEndTime <= 0f)
        {
            return true;
        }
        
        // 修复：需要转换为相对时间进行比较
        // actualEndTime是绝对时间，需要转换为相对时间
        float actualEndRelativeTime = actualEndTime - pathStartStoryTime;
        
        // 如果当前时间已经达到或超过路径结束时间，可以结束
        return currentRelativeTime >= actualEndRelativeTime;
    }
    
    /// <summary>
    /// 获取路径剩余的等待时间
    /// </summary>
    /// <param name="currentRelativeTime">当前相对于路径开始的时间</param>
    /// <param name="npcData">NPC数据，用于查找下一个路径的开始时间</param>
    /// <param name="currentScore">当前分数，用于确定下一个路径</param>
    /// <returns>需要等待的时间（秒），0表示无需等待</returns>
    public float GetRemainingWaitTime(float currentRelativeTime, NPCData npcData = null, float currentScore = 0f)
    {
        // 获取实际的路径结束时间（这是绝对时间）
        float actualEndTime = GetActualEndTime(npcData, currentScore);
        
        // 如果没有有效的结束时间，无需等待
        if (actualEndTime <= 0f)
        {
            return 0f;
        }
        
        // 修复：需要转换为相对时间进行比较
        // actualEndTime是绝对时间，需要转换为相对时间
        float actualEndRelativeTime = actualEndTime - pathStartStoryTime;
        
        // 计算还需要等待的时间
        float remainingTime = actualEndRelativeTime - currentRelativeTime;
        return Mathf.Max(0f, remainingTime);
    }
    
    /// <summary>
    /// 获取实际的路径结束时间
    /// 优先使用手动设置的pathEndStoryTime，如果为0则使用下一个路径的开始时间
    /// </summary>
    /// <param name="npcData">NPC数据，用于查找下一个路径</param>
    /// <param name="currentScore">当前分数，用于确定下一个路径</param>
    /// <returns>实际的路径结束时间</returns>
    public float GetActualEndTime(NPCData npcData = null, float currentScore = 0f)
    {
        // 如果手动设置了结束时间且大于0，优先使用手动设置的时间
        if (pathEndStoryTime > 0f)
        {
            return pathEndStoryTime;
        }
        
        // 否则尝试获取下一个路径的开始时间
        if (npcData != null)
        {
            string nextPathID = SelectNextPathByScore(currentScore);
            if (!string.IsNullOrEmpty(nextPathID))
            {
                PathConfig nextPath = npcData.FindPathById(nextPathID);
                if (nextPath != null)
                {
                    // 返回下一个路径的开始时间作为当前路径的结束时间
                    return nextPath.pathStartStoryTime;
                }
            }
        }
        
        // 如果都没有找到，返回0表示无限制
        return 0f;
    }
}

[Serializable]
public class ActionData
{
    [Header("基础内容")]
    public string dialogueText = "";        // 对话文本
    public float displayDuration = 2f;      // 对话显示时间
    public AudioClip voiceClip;             // 语音片段
    public string animationName = "";       // 动画名称
    public int animationLoopCount = 1;      // 动画循环次数 (0=不播放)
    public AudioClip oneShotSFX;           // 一次性音效
    public float delay = 0f;                // 距离上一个行动结束的延迟时间
    public float stopTime = 0f;             // 停止移动时间
    
    [Header("执行条件")]
    public int pathPointIndex = 0;          // 路径点索引
    public bool isActionActive = true;      // 动作是否可用
    
    [Header("时间触发设置")]
    [Tooltip("绝对触发时间（故事时间秒数，-1表示立即触发，0表示路径开始时触发）")]
    public float triggerTime = -1f;         // 绝对触发时间
    [Tooltip("对话优先级（数值越高优先级越高，0为普通优先级）")]
    public int priority = 0;                // 对话优先级
    
    /// <summary>
    /// 是否是立即触发的对话（传统模式）
    /// </summary>
    public bool IsImmediateTrigger => triggerTime < 0f;
    
    /// <summary>
    /// 是否是时间触发的对话
    /// </summary>
    public bool IsTimeTrigger => triggerTime >= 0f;
    
    /// <summary>
    /// 获取显示用的触发时间文本
    /// </summary>
    public string GetTriggerTimeText()
    {
        if (IsImmediateTrigger)
            return "立即触发";
        
        return $"故事时间 {triggerTime:F1}秒";
    }
    
    /// <summary>
    /// 获取优先级显示文本
    /// </summary>
    public string GetPriorityText()
    {
        if (priority == 0)
            return "普通";
        else if (priority > 0)
            return $"高优先级({priority})";
        else
            return $"低优先级({priority})";
    }
}

[Serializable]
public class PathEvent
{
    public string eventID;
    public int startPointIndex;
    public int endPointIndex;
    public float playerInteractionRadius = 3.0f;
    public bool showInteractionRange = true;

    // 手势设置
    public float gestureHoldTime = 1.0f;
    public float maxRecognitionDistance = 8.0f;

    // 事件可用性控制
    public bool enabledInAct1 = true;
    public bool enabledInAct2 = true;
    public bool enabledInAct3 = true;

    // 手势响应
    public List<GestureResponse> gestureResponses = new List<GestureResponse>();
    public GestureResponse defaultResponse = new GestureResponse();
}

[Serializable]
public class GestureResponse
{
    public string gestureType;              // 手势类型
    public float scoreEffect;               // 分数影响
    public List<ActionData> actions = new List<ActionData>(); // 统一使用 ActionData
}

[Serializable]
public class PathBranch
{
    [Tooltip("下一条路径ID")]
    public string nextPathID;

    [Header("分数区间")]
    [Tooltip("最低分数 (含)")]
    public float minScore = 0f;

    [Tooltip("最高分数 (不含)")]
    public float maxScore = 100f;

    // 向后兼容的字段
    [HideInInspector]
    public float requiredScore = 0f;

    [Tooltip("描述")]
    public string description;

    // 引用的路径创建器 (用于编辑器)
    public MultiPointPathCreator pathCreator => PathRegistry.GetPathCreatorByID(nextPathID);

    // 检查分数是否在区间内
    public bool IsScoreInRange(float score)
    {
        return score >= minScore && score < maxScore;
    }
}

[Serializable]
public class PathTimePoint
{
    [SerializeField]
    [Tooltip("路径点索引")]
    public int pathPointIndex = 0;
    
    [SerializeField]
    [Tooltip("从路径开始计算的相对时间（秒）")]
    public float requiredStoryTime = 0f;
    
    [SerializeField]
    [Tooltip("在此路径点的停留时间（秒）")]
    public float stopTime = 0f;
    
    [SerializeField]
    [Tooltip("时间点描述")]
    public string description = "";
    
    /// <summary>
    /// 标记这是否是一个虚拟的路径终点目标（非配置文件中的真实时间控制点）
    /// </summary>
    [NonSerialized]
    public bool isVirtualEndTarget = false;
    
    // 编辑器显示用
    public string GetDisplayText()
    {
        return $"点{pathPointIndex} - {requiredStoryTime:F1}s" + 
               (stopTime > 0 ? $" (停留{stopTime:F1}s)" : "") +
               (string.IsNullOrEmpty(description) ? "" : $" ({description})") +
               (isVirtualEndTarget ? " [虚拟终点]" : "");
    }
}

// 在PathConfig中添加一个新的路径点停留配置
[Serializable]
public class PathPointStopConfig
{
    [Tooltip("路径点索引")]
    public int pathPointIndex = 0;
    
    [Tooltip("在此路径点的停留时间（秒）")]
    public float stopTime = 0f;
    
    [Tooltip("停留原因描述")]
    public string description = "";
}