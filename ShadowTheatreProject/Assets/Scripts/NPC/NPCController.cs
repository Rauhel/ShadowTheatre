// 文件: NPCController.cs
using UnityEngine;
using UnityEngine.AI;
using System;

[RequireComponent(typeof(NavMeshAgent))]
public class NPCController : MonoBehaviour
{
    [Header("NPC配置")]
    public NPCData npcData;

    [Header("引用")]
    public Animator animator;
    
    [Header("UI状态")]
    public Sprite npcStatusIcon;      // 用于状态UI的图标

    // 保护字段
    protected NavMeshAgent agent;
    protected NPCEventManager eventManager;

    // 状态事件
    public static event Action<string, NPCStatusData> OnNPCStatusChanged;

    // 公共属性
    public bool IsMoving => agent != null && !agent.isStopped;
    public NPCData Data => npcData;
    public string UniqueId => npcData != null ? npcData.npcID : gameObject.name; // 使用NPCData中的npcID

    // 缓存的状态数据
    private NPCStatusData cachedStatusData;
    private bool lastProcessingState = false;

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        eventManager = GetComponent<NPCEventManager>();

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (npcData == null)
        {
            Debug.LogError($"[{gameObject.name}] 没有设置NPCData!");
            return;
        }

        // 初始化状态数据
        InitializeStatusData();

        // 优先使用npcData中的速度
        if (npcData != null && npcData.moveSpeed > 0.01f)
        {
            agent.speed = npcData.moveSpeed;
        }
        else if (agent.speed <= 0.1f)
        {
            Debug.LogWarning($"[{gameObject.name}] NavMeshAgent速度过低: {agent.speed}，已自动设置为默认值");
            agent.speed = 1.0f; // 设置一个默认速度
        }
    }

    protected virtual void Start()
    {
        // 发送初始状态
        NotifyStatusChanged();
    }

    protected virtual void Update()
    {
        // 检查状态是否发生变化
        CheckStatusChanges();
    }

    /// <summary>
    /// 初始化状态数据
    /// </summary>
    private void InitializeStatusData()
    {
        if (npcData == null) return;
        
        cachedStatusData = new NPCStatusData(npcData.npcID, npcData.npcName);
        cachedStatusData.npcIcon = npcStatusIcon;
        lastProcessingState = IsProcessingEvent();
    }

    /// <summary>
    /// 检查状态变化并通知UI
    /// </summary>
    private void CheckStatusChanges()
    {
        bool currentProcessingState = IsProcessingEvent();
        if (currentProcessingState != lastProcessingState)
        {
            Debug.Log($"[NPCController] {gameObject.name} 状态变化: {lastProcessingState} -> {currentProcessingState}");
            lastProcessingState = currentProcessingState;
            NotifyStatusChanged();
        }
    }

    /// <summary>
    /// 获取当前状态数据
    /// </summary>
    public NPCStatusData GetStatusData()
    {
        UpdateStatusData();
        return cachedStatusData;
    }

    /// <summary>
    /// 更新状态数据
    /// </summary>
    private void UpdateStatusData()
    {
        if (cachedStatusData == null) return;

        bool isProcessing = IsProcessingEvent();
        cachedStatusData.UpdateStatus(isProcessing);
    }

    /// <summary>
    /// 检查NPC是否正在处理事件
    /// </summary>
    public bool IsProcessingEvent()
    {
        if (eventManager == null) return false;
        
        // 直接访问NPCEventManager的isProcessingEvent字段
        var field = eventManager.GetType().GetField("isProcessingEvent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            bool isProcessing = (bool)field.GetValue(eventManager);
            return isProcessing;
        }
        
        Debug.LogWarning($"[{gameObject.name}] 无法访问NPCEventManager的isProcessingEvent字段");
        return false;
    }

    /// <summary>
    /// 通知状态改变
    /// </summary>
    private void NotifyStatusChanged()
    {
        if (npcData == null) return;
        
        UpdateStatusData();
        Debug.Log($"[NPCController] {gameObject.name} 通知状态更新: {npcData.npcID}, 处理事件: {cachedStatusData.isProcessingEvent}");
        OnNPCStatusChanged?.Invoke(npcData.npcID, cachedStatusData);
    }

    /// <summary>
    /// 手动刷新状态（供外部调用）
    /// </summary>
    public void RefreshStatus()
    {
        NotifyStatusChanged();
    }

    // 提供公共接口用于动画控制
    public virtual void PlayAnimation(string animName)
    {
        if (animator != null && !string.IsNullOrEmpty(animName))
        {
            animator.Play(animName);
        }
    }

    // 修改方法名，保持向后兼容性
    public virtual void AdjustScore(float amount)
    {
        if (npcData != null)
        {
            npcData.currentScore += amount;
            Debug.Log($"[{gameObject.name}] 分数更新: {npcData.currentScore} ({(amount >= 0 ? "+" : "")}{amount})");
            
            // 分数变化不影响状态UI显示，因为不显示分数
        }
    }

    // 为了保持兼容性，添加原名称的方法
    public virtual void UpdateScore(float amount)
    {
        AdjustScore(amount); // 调用改名后的方法
    }

    // 提供公共接口用于停止/恢复移动
    public virtual void StopMovement(bool stop)
    {
        if (agent != null)
        {
            agent.isStopped = stop;
        }
    }

    // 重置当前NPC的分数
    public virtual void ResetScore()
    {
        if (npcData != null)
        {
            npcData.ResetScore();
            Debug.Log($"[{gameObject.name}] 分数已重置为0");
        }
    }

    // 重置场景中所有NPC的分数
    public static void ResetAllNPCScores()
    {
        NPCController[] allNpcs = GameObject.FindObjectsOfType<NPCController>();
        int count = 0;

        foreach (NPCController npc in allNpcs)
        {
            if (npc.npcData != null)
            {
                npc.npcData.ResetScore();
                count++;
            }
        }

        Debug.Log($"游戏结束: 已重置 {count} 个NPC的分数");
    }

    // 调试工具
    public virtual void DebugStatus()
    {
        Debug.Log($"[{gameObject.name}] 当前分数: {(npcData != null ? npcData.currentScore : 0)}");
        Debug.Log($"[{gameObject.name}] 导航状态: {(agent != null ? (agent.isStopped ? "已停止" : "移动中") : "无导航组件")}");
        Debug.Log($"[{gameObject.name}] 实际速度: {(agent != null ? agent.velocity.magnitude : 0)}");
        Debug.Log($"[{gameObject.name}] 事件处理状态: {(IsProcessingEvent() ? "正在处理事件" : "未处理事件")}");
    }

    protected virtual void OnDestroy()
    {
        // 清理事件引用
        OnNPCStatusChanged = null;
    }
}