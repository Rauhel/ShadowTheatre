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
    [Tooltip("当前路径")]
    public Transform path;
    [Tooltip("下一条路径（如果不是决策点）")]
    public Transform nextPath;
    [Tooltip("是否是路径终点")]
    public bool isEndPoint = false;
    [Tooltip("是否在路径结束处需要决策")]
    public bool needDecision = false;
    [Tooltip("如果需要决策，关联的决策点ID")]
    public string decisionPointID;
}