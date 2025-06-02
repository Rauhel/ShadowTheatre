using UnityEngine;
using System;

/// <summary>
/// NPC状态数据：用于UI显示的NPC状态信息
/// </summary>
[Serializable]
public class NPCStatusData
{
    [Header("基本信息")]
    public string npcId;              // 使用NPCData中的npcID
    public string npcName;            // 使用NPCData中的npcName
    public Sprite npcIcon;            // NPC图标
    
    [Header("状态信息")]
    public bool isProcessingEvent;    // NPC是否正在处理事件
    
    [Header("UI状态")]
    public Color normalColor = Color.white;        // 正常状态颜色
    public Color processingEventColor = Color.yellow;  // 正在处理事件时的颜色
    
    public NPCStatusData(string id, string name)
    {
        npcId = id;
        npcName = name;
        isProcessingEvent = false;
    }
    
    /// <summary>
    /// 复制构造函数
    /// </summary>
    public NPCStatusData(NPCStatusData other)
    {
        npcId = other.npcId;
        npcName = other.npcName;
        npcIcon = other.npcIcon;
        isProcessingEvent = other.isProcessingEvent;
        normalColor = other.normalColor;
        processingEventColor = other.processingEventColor;
    }
    
    /// <summary>
    /// 获取当前应该显示的颜色
    /// </summary>
    public Color GetCurrentColor()
    {
        return isProcessingEvent ? processingEventColor : normalColor;
    }
    
    /// <summary>
    /// 更新状态数据
    /// </summary>
    public void UpdateStatus(bool processingEvent)
    {
        isProcessingEvent = processingEvent;
    }
} 