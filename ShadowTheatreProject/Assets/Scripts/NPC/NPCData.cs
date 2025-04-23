using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New NPC Data", menuName = "Shadow Theatre/NPC Data")]
public class NPCData : ScriptableObject
{
    [Header("基本信息")]
    public string npcID;
    public string npcName;
    public float currentScore = 0f;  // NPC当前分数

    [Header("路径配置")]
    public List<PathConfig> paths = new List<PathConfig>();

    [HideInInspector]
    public List<PathConnection> pathConnections = new List<PathConnection>();

    [HideInInspector]
    public List<PathDecision> pathDecisions = new List<PathDecision>();

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
}

[Serializable]
public class ActionData
{
    [Header("基础内容")]
    public string dialogueText = "";        // 对话文本
    public float displayDuration = 1.5f;    // 显示时间
    public AudioClip voiceClip;             // 语音片段
    public string animationName = "";       // 动画名称
    public float delay = 0f;                // 执行延迟
    public bool overridePrevious = true;    // 是否覆盖前一个动作

    [Header("执行条件")]
    public int pathPointIndex = 0;          // 路径点索引
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