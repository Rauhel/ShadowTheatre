using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New NPC Data", menuName = "Shadow Theatre/NPC Data")]
public class NPCData : ScriptableObject
{
    [Header("基本信息")]
    public string npcID;
    public string npcName;

    [Header("分数信息")]
    public float currentScore = 0f;

    [Header("路径分歧点")]
    public List<PathDecision> pathDecisions = new List<PathDecision>();

    [Header("事件列表")]
    public List<NPCEvent> events = new List<NPCEvent>();

    [Header("路径连接")]
    public List<PathConnection> pathConnections = new List<PathConnection>();

    public string GetPathPointRelativePosition(Transform pathPoint)
    {
        if (pathPoint == null)
            return "未知";

        // 查找该点所属的路径
        Transform parentPath = pathPoint.parent;
        if (parentPath == null || parentPath.parent == null)
            return "未知";

        // 获取路径创建器
        MultiPointPathCreator pathCreator = parentPath.parent.GetComponent<MultiPointPathCreator>();
        if (pathCreator == null || pathCreator.pathPointsParent != parentPath)
            return "未知";

        // 查找点的索引
        int pointCount = parentPath.childCount;
        int pointIndex = -1;

        for (int i = 0; i < pointCount; i++)
        {
            if (parentPath.GetChild(i) == pathPoint)
            {
                pointIndex = i;
                break;
            }
        }

        if (pointIndex < 0)
            return "未知";

        // 计算相对位置
        float relativePos = (float)pointIndex / (pointCount - 1);
        return $"{relativePos:P0}";
    }

    public Transform GetPathPointByRelativePosition(MultiPointPathCreator pathCreator, float relativePosition)
    {
        if (pathCreator == null || pathCreator.pathPointsParent == null ||
            pathCreator.pathPointsParent.childCount == 0)
            return null;

        int pointCount = pathCreator.pathPointsParent.childCount;
        // 将相对位置（0-1）转换为索引
        int index = Mathf.Clamp(Mathf.RoundToInt(relativePosition * (pointCount - 1)), 0, pointCount - 1);

        return pathCreator.pathPointsParent.GetChild(index);
    }
}

[Serializable]
public class PathDecision
{
    public string decisionPointID;
    public Transform decisionLocation;
    public float decisionRadius = 1f;

    [Header("路径选项")]
    public List<PathOption> pathOptions = new List<PathOption>();

    // 辅助方法：根据分数选择合适的路径
    public Transform SelectPathBasedOnScore(float currentScore)
    {
        // 如果没有选项，返回null
        if (pathOptions.Count == 0) return null;

        // 默认使用第一个路径
        Transform selectedPath = pathOptions[0].path;

        // 遍历所有路径选项
        foreach (var option in pathOptions)
        {
            // 如果当前分数大于等于该选项的分数阈值，选择该路径
            if (currentScore >= option.scoreThreshold)
            {
                selectedPath = option.path;
            }
            else
            {
                // 一旦遇到分数不满足的选项，停止查找（假设选项已按阈值从低到高排序）
                break;
            }
        }

        return selectedPath;
    }
}

[Serializable]
public class PathOption
{
    public string optionName;
    public float scoreThreshold;
    public Transform path;
    [TextArea(1, 3)]
    public string description; // 可选的描述信息，方便调试
}

[Serializable]
public class NPCEvent
{
    public string eventID;

    [Header("触发条件")]
    public Transform triggerLocation;
    public float triggerRadius = 2f;
    [Tooltip("在EventCenter中的事件名称（可选）")]
    public string globalEventName;

    [Header("手势检测设置")]
    public float gestureHoldTime = 2.0f;
    public float gestureTimeLimit = 5.0f;

    [Header("事件分支")]
    public EventBranch defaultBranch;
    public List<GestureBranch> gestureBranches = new List<GestureBranch>();
}

[Serializable]
public class EventBranch
{
    [Header("分支行为")]
    [Tooltip("动画名称")]
    public string animationName;
    [Tooltip("对话内容")]
    [TextArea(3, 5)]
    public string dialogueText;
    [Tooltip("覆盖路径（可选）")]
    public Transform overridePath;
    [Tooltip("行为结束后延迟（秒）")]
    public float completionDelay = 1f;

    [Header("分支评分")]
    public float scoreValue = 0f;
}

[Serializable]
public class GestureBranch : EventBranch
{
    [Header("手势识别")]
    [Tooltip("触发此分支的手势类型")]
    public string gestureType;
    [Tooltip("最低置信度")]
    [Range(0f, 1f)]
    public float minConfidence = 0.7f;
}

[Serializable]
public class PathConnection
{
    [SerializeField, HideInInspector]
    private Transform _pathTransform;

    [SerializeField, HideInInspector]
    private Transform _nextPathTransform;

    // Unity 编辑器可视属性
    [Tooltip("路径对象")]
    public GameObject pathObject;

    [Header("路径分支类型")]
    [Tooltip("路径分支类型")]
    public PathBranchType branchType = PathBranchType.Direct;

    [Header("直接连接")]
    [Tooltip("下一条路径（如果是直接连接）")]
    public GameObject nextPathObject;

    [Header("基于分数的分支")]
    [Tooltip("分支选项列表（如果是分数分支）")]
    public List<PathScoreOption> scoreOptions = new List<PathScoreOption>();

    [Header("终点设置")]
    [Tooltip("该路径是否是终点")]
    public bool isEndPoint = false;

    // 获取缓存的 MultiPointPathCreator 组件
    public MultiPointPathCreator pathCreator
    {
        get
        {
            if (pathObject != null)
                return pathObject.GetComponent<MultiPointPathCreator>();
            return null;
        }
    }

    public MultiPointPathCreator nextPathCreator
    {
        get
        {
            if (nextPathObject != null)
                return nextPathObject.GetComponent<MultiPointPathCreator>();
            return null;
        }
    }

    // 获取路径的起点和终点
    public Transform GetPathStart()
    {
        return pathCreator?.startPoint;
    }

    public Transform GetPathEnd()
    {
        return pathCreator?.endPoint;
    }

    // 保持与旧代码的兼容性
    public Transform path
    {
        get
        {
            if (pathObject != null)
                return pathObject.transform;
            return _pathTransform;
        }
        set
        {
            _pathTransform = value;
            if (value != null)
            {
                pathObject = value.gameObject;
            }
            else
            {
                pathObject = null;
            }
        }
    }

    public Transform nextPath
    {
        get
        {
            if (nextPathObject != null)
                return nextPathObject.transform;
            return _nextPathTransform;
        }
        set
        {
            _nextPathTransform = value;
            if (value != null)
            {
                nextPathObject = value.gameObject;
            }
            else
            {
                nextPathObject = null;
            }
        }
    }

    // 根据分数选择下一个路径
    public Transform SelectNextPathBasedOnScore(float currentScore)
    {
        if (branchType == PathBranchType.Direct || scoreOptions == null || scoreOptions.Count == 0)
        {
            return nextPath;
        }

        // 默认使用第一个路径选项
        PathScoreOption firstOption = scoreOptions[0];
        Transform selectedPath = firstOption != null && firstOption.pathObject != null ?
                                firstOption.pathObject.transform : null;

        // 遍历所有路径选项
        foreach (var option in scoreOptions)
        {
            if (option == null || option.pathObject == null)
                continue;

            // 如果当前分数大于等于该选项的分数阈值，选择该路径
            if (currentScore >= option.scoreThreshold)
            {
                selectedPath = option.pathObject.transform;
            }
            else
            {
                // 一旦遇到分数不满足的选项，停止查找（假设选项已按阈值从低到高排序）
                break;
            }
        }

        return selectedPath;
    }
}

// 添加路径分支类型枚举
public enum PathBranchType
{
    Direct,     // 直接连接到下一个路径
    ScoreBased  // 基于分数的分支
}

// 修改 PathScoreOption 类
[Serializable]
public class PathScoreOption
{
    public string optionName;
    public float scoreThreshold;
    public GameObject pathObject;
    [TextArea(1, 3)]
    public string description;

    // 获取 MultiPointPathCreator 组件
    public MultiPointPathCreator pathCreator
    {
        get
        {
            if (pathObject != null)
                return pathObject.GetComponent<MultiPointPathCreator>();
            return null;
        }
    }
}