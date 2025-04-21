using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PathConnection
{
    // 当前路径ID
    public string pathID;

    // 当前路径引用 (向后兼容)
    public Transform path;

    // 标记是否为终点
    public bool isEndPoint;

    // 分支类型
    public PathBranchType branchType = PathBranchType.Direct;

    // 直接连接的下一条路径ID
    public string nextPathID;

    // 直接连接的下一条路径引用（向后兼容）
    public Transform nextPath;

    // 分数分支选项（可以包含多条路径）
    public List<PathScoreOption> scoreOptions = new List<PathScoreOption>();

    // 路径创建器引用 (用于编辑器)
    public MultiPointPathCreator pathCreator => PathRegistry.GetPathCreatorByID(pathID)
                                             ?? path?.GetComponent<MultiPointPathCreator>();

    // 下一个路径创建器 (用于编辑器)
    public MultiPointPathCreator nextPathCreator => PathRegistry.GetPathCreatorByID(nextPathID)
                                                 ?? nextPath?.GetComponent<MultiPointPathCreator>();

    // 根据分数选择下一条路径ID
    public string GetNextPathIDByScore(float score)
    {
        if (branchType == PathBranchType.Direct || scoreOptions.Count == 0)
            return nextPathID;

        // 默认使用直接连接的路径ID
        string selectedPathID = nextPathID;

        // 遍历所有分数选项，找到分数范围匹配的选项
        foreach (var option in scoreOptions)
        {
            // 完整的分数区间判定：分数应该在最低分和最高分之间
            if (score >= option.minScore && score < option.maxScore)
            {
                selectedPathID = option.pathID;
                break; // 找到第一个匹配的就停止
            }
        }

        return selectedPathID;
    }

    // 根据分数选择下一条路径（向后兼容）
    public Transform GetNextPathByScore(float score)
    {
        string id = GetNextPathIDByScore(score);
        if (string.IsNullOrEmpty(id))
            return nextPath;

        MultiPointPathCreator creator = PathRegistry.GetPathCreatorByID(id);
        return creator != null ? creator.transform : nextPath;
    }

    // 添加一个新的分数选项（方便在编辑器中使用）
    public void AddScoreOption(float minScore, float maxScore, string pathID)
    {
        PathScoreOption option = new PathScoreOption
        {
            minScore = minScore,
            maxScore = maxScore,
            pathID = pathID,
            optionName = $"分数 {minScore}-{maxScore}"
        };

        // 为向后兼容设置path属性
        MultiPointPathCreator pathCreator = PathRegistry.GetPathCreatorByID(pathID);
        if (pathCreator != null)
        {
            option.path = pathCreator.transform;
        }

        scoreOptions.Add(option);
    }
}

// 分支类型
public enum PathBranchType
{
    Direct,  // 直接连接到下一路径
    ScoreBased  // 基于分数选择多条路径
}

[Serializable]
public class PathScoreOption
{
    // 选项名称
    public string optionName;

    // 分数阈值（向后兼容）
    public float scoreThreshold;

    // 完整的分数区间
    public float minScore = 0f;  // 最低分数（含）
    public float maxScore = 100f;  // 最高分数（不含）

    // 路径ID (主要引用方式)
    public string pathID;

    // 路径引用 (向后兼容)
    public Transform path;

    // 描述
    public string description;

    // 路径创建器引用 - 添加了getter和setter以便在Inspector中可选择
    public MultiPointPathCreator pathCreator
    {
        get
        {
            // 先尝试从ID获取，再从Transform获取
            return PathRegistry.GetPathCreatorByID(pathID) ?? path?.GetComponent<MultiPointPathCreator>();
        }
        set
        {
            // 当在Inspector中选择新值时，同时更新ID和Transform引用
            if (value != null)
            {
                pathID = value.pathID;
                path = value.transform;
            }
            else
            {
                pathID = string.Empty;
                path = null;
            }
        }
    }

    // 是否满足分数条件
    public bool IsScoreInRange(float score)
    {
        return score >= minScore && score < maxScore;
    }
}