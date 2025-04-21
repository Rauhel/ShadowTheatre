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

    // 根据路径点获取路径根对象
    public Transform GetPathRoot(Transform pathPoint)
    {
        if (pathPoint == null) return null;

        // 检查自身是否是路径根对象
        if (pathPoint.GetComponent<MultiPointPathCreator>() != null)
            return pathPoint;

        // 向上查找路径根对象
        Transform current = pathPoint;
        while (current.parent != null) // 修复：删除多余的右括号
        {
            if (current.parent.name.Contains("PathPoints"))
            {
                return current.parent.parent;
            }
            current = current.parent;
        }

        // 检查最终的对象
        return current.GetComponent<MultiPointPathCreator>()?.transform;
    }

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
    [Tooltip("路径ID")]
    public string pathID;

    [Header("路径分支类型")]
    [Tooltip("路径分支类型")]
    public PathBranchType branchType = PathBranchType.Direct;

    [Header("直接连接")]
    [Tooltip("下一条路径ID（如果是直接连接）")]
    public string nextPathID;

    [Header("基于分数的分支")]
    [Tooltip("分支选项列表（如果是分数分支）")]
    public List<PathScoreOption> scoreOptions = new List<PathScoreOption>();

    [Header("终点设置")]
    [Tooltip("该路径是否是终点")]
    public bool isEndPoint = false;

    // 智能获取 MultiPointPathCreator 组件
    public MultiPointPathCreator pathCreator
    {
        get
        {
            return PathRegistry.GetPathCreatorByID(pathID);
        }
    }

    public MultiPointPathCreator nextPathCreator
    {
        get
        {
            return PathRegistry.GetPathCreatorByID(nextPathID);
        }
    }

    // 兼容旧代码的属性
    [System.NonSerialized]
    private Transform _pathTransform;
    public Transform path
    {
        get
        {
            var creator = pathCreator;
            if (creator != null)
                return creator.transform;
            return _pathTransform;
        }
        set
        {
            _pathTransform = value;
            if (value != null)
            {
                var creator = value.GetComponent<MultiPointPathCreator>();
                if (creator != null)
                {
                    pathID = creator.pathID;
                }
                else
                {
                    // 尝试查找父级的路径创建器
                    Transform current = value;
                    while (current.parent != null)
                    {
                        current = current.parent;
                        creator = current.GetComponent<MultiPointPathCreator>();
                        if (creator != null)
                        {
                            pathID = creator.pathID;
                            break;
                        }
                    }
                }
            }
        }
    }

    [System.NonSerialized]
    private Transform _nextPathTransform;
    public Transform nextPath
    {
        get
        {
            var creator = nextPathCreator;
            if (creator != null)
                return creator.transform;
            return _nextPathTransform;
        }
        set
        {
            _nextPathTransform = value;
            if (value != null)
            {
                var creator = value.GetComponent<MultiPointPathCreator>();
                if (creator != null)
                {
                    nextPathID = creator.pathID;
                }
                else
                {
                    // 尝试查找父级的路径创建器
                    Transform current = value;
                    while (current.parent != null)
                    {
                        current = current.parent;
                        creator = current.GetComponent<MultiPointPathCreator>();
                        if (creator != null)
                        {
                            nextPathID = creator.pathID;
                            break;
                        }
                    }
                }
            }
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

    // 根据分数选择下一个路径
    public Transform SelectNextPathBasedOnScore(float currentScore)
    {
        if (branchType == PathBranchType.Direct || scoreOptions == null || scoreOptions.Count == 0)
        {
            return nextPathCreator?.transform;
        }

        // 默认使用第一个选项
        Transform selectedPath = null;

        if (scoreOptions.Count > 0)
        {
            MultiPointPathCreator firstCreator = scoreOptions[0].pathCreator;
            if (firstCreator != null)
                selectedPath = firstCreator.transform;
        }

        // 找到最高满足条件的分支
        foreach (var option in scoreOptions)
        {
            MultiPointPathCreator optionCreator = option.pathCreator;
            if (optionCreator == null) continue;

            if (currentScore >= option.scoreThreshold)
            {
                selectedPath = optionCreator.transform;
            }
            else
            {
                break; // 停止在第一个超过分数的选项
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
    public string pathID;
    [TextArea(1, 3)]
    public string description;

    // 通过 ID 获取路径创建器
    public MultiPointPathCreator pathCreator
    {
        get
        {
            return PathRegistry.GetPathCreatorByID(pathID);
        }
    }

    // 兼容旧代码
    [System.NonSerialized]
    private Transform _pathTransform;
    public Transform path
    {
        get
        {
            var creator = pathCreator;
            if (creator != null)
                return creator.transform;
            return _pathTransform;
        }
        set
        {
            _pathTransform = value;
            if (value != null)
            {
                var creator = value.GetComponent<MultiPointPathCreator>();
                if (creator != null)
                {
                    pathID = creator.pathID;
                }
                else
                {
                    // 尝试查找父级的路径创建器
                    Transform current = value;
                    while (current.parent != null)
                    {
                        current = current.parent;
                        creator = current.GetComponent<MultiPointPathCreator>();
                        if (creator != null)
                        {
                            pathID = creator.pathID;
                            break;
                        }
                    }
                }
            }
        }
    }
}