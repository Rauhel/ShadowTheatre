using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// NPC状态UI管理器：管理指定NPC的状态图标显示
/// </summary>
public class NPCStatusUIManager : MonoBehaviour
{
    [Header("指定的NPC列表")]
    [SerializeField] private List<NPCController> targetNPCs = new List<NPCController>(); // 手动指定的5个NPC
    
    [Header("对应的状态图标")]
    [SerializeField] private List<NPCStatusIcon> statusIcons = new List<NPCStatusIcon>(); // 对应的5个状态图标
    
    [Header("动画设置")]
    [SerializeField] private float colorTransitionDuration = 0.3f;  // 颜色过渡时间
    [SerializeField] private float scaleOnProcessingEvent = 1.2f;   // 正在处理事件时的缩放
    
    [Header("调试")]
    [SerializeField] private bool showDebugInfo = false;
    
    // 内部数据
    private Dictionary<string, NPCStatusIcon> npcIconMap = new Dictionary<string, NPCStatusIcon>();
    
    private void Awake()
    {
        // 订阅NPC状态变化事件
        NPCController.OnNPCStatusChanged += OnNPCStatusChanged;
    }
    
    private void Start()
    {
        // 建立NPC和图标的映射关系
        SetupNPCIconMapping();
    }
    
    private void OnDestroy()
    {
        // 取消订阅事件
        NPCController.OnNPCStatusChanged -= OnNPCStatusChanged;
    }
    
    /// <summary>
    /// 建立NPC和图标的映射关系
    /// </summary>
    private void SetupNPCIconMapping()
    {
        npcIconMap.Clear();
        
        int count = Mathf.Min(targetNPCs.Count, statusIcons.Count);
        
        for (int i = 0; i < count; i++)
        {
            NPCController npc = targetNPCs[i];
            NPCStatusIcon icon = statusIcons[i];
            
            if (npc != null && icon != null && npc.Data != null)
            {
                string npcId = npc.Data.npcID;
                npcIconMap[npcId] = icon;
                
                // 初始化图标
                NPCStatusData statusData = npc.GetStatusData();
                icon.Initialize(statusData, colorTransitionDuration, scaleOnProcessingEvent);
                
                if (showDebugInfo)
                {
                    Debug.Log($"[NPCStatusUI] 映射NPC: {npcId} 到图标 {i}");
                }
            }
            else
            {
                Debug.LogWarning($"[NPCStatusUI] 索引 {i} 的NPC或图标为空，跳过映射");
            }
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[NPCStatusUI] 完成映射，共 {npcIconMap.Count} 个NPC图标");
        }
    }
    
    /// <summary>
    /// 处理NPC状态变化事件
    /// </summary>
    private void OnNPCStatusChanged(string npcId, NPCStatusData statusData)
    {
        if (npcIconMap.ContainsKey(npcId))
        {
            Debug.Log($"[NPCStatusUI] 收到状态更新: {npcId}, 处理事件: {statusData.isProcessingEvent}, 图标存在: true");
            npcIconMap[npcId].UpdateStatus(statusData);
            
            if (showDebugInfo)
            {
                Debug.Log($"[NPCStatusUI] 更新NPC状态: {npcId}, 处理事件: {statusData.isProcessingEvent}");
            }
        }
        else
        {
            Debug.LogWarning($"[NPCStatusUI] 收到状态更新但找不到对应图标: {npcId}, 处理事件: {statusData.isProcessingEvent}");
        }
    }
    
    /// <summary>
    /// 手动刷新所有NPC状态
    /// </summary>
    [ContextMenu("刷新所有NPC状态")]
    public void RefreshAllNPCStatus()
    {
        foreach (NPCController npc in targetNPCs)
        {
            if (npc != null)
            {
                npc.RefreshStatus();
            }
        }
    }
    
    /// <summary>
    /// 设置特定NPC的自定义颜色
    /// </summary>
    public void SetCustomColorScheme(string npcId, Color normalColor, Color processingColor)
    {
        if (npcIconMap.ContainsKey(npcId))
        {
            npcIconMap[npcId].SetCustomColors(normalColor, processingColor);
        }
    }
    
    /// <summary>
    /// 获取映射的NPC数量
    /// </summary>
    public int GetMappedNPCCount()
    {
        return npcIconMap.Count;
    }
    
    /// <summary>
    /// 手动添加NPC和图标的映射（运行时使用）
    /// </summary>
    public void AddNPCIconMapping(NPCController npc, NPCStatusIcon icon)
    {
        if (npc != null && icon != null && npc.Data != null)
        {
            string npcId = npc.Data.npcID;
            npcIconMap[npcId] = icon;
            
            // 初始化图标
            NPCStatusData statusData = npc.GetStatusData();
            icon.Initialize(statusData, colorTransitionDuration, scaleOnProcessingEvent);
            
            if (showDebugInfo)
            {
                Debug.Log($"[NPCStatusUI] 运行时添加映射: {npcId}");
            }
        }
    }
    
    /// <summary>
    /// 移除NPC图标映射
    /// </summary>
    public void RemoveNPCIconMapping(string npcId)
    {
        if (npcIconMap.ContainsKey(npcId))
        {
            npcIconMap.Remove(npcId);
            
            if (showDebugInfo)
            {
                Debug.Log($"[NPCStatusUI] 移除映射: {npcId}");
            }
        }
    }
} 