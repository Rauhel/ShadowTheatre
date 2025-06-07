using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// NPC对话调度器 - 管理基于绝对时间触发的对话系统
/// 支持立即触发和时间触发两种模式，以及优先级管理
/// </summary>
public class NPCDialogueScheduler : MonoBehaviour
{
    [Header("调试信息")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private List<ScheduledDialogue> scheduledDialogues = new List<ScheduledDialogue>();
    [SerializeField] private List<ScheduledDialogue> immediateQueue = new List<ScheduledDialogue>();
    
    // 事件
    public event Action<ActionData> OnDialogueReady;
    
    // 组件引用
    private NPCController npcController;
    
    private void Awake()
    {
        npcController = GetComponent<NPCController>();
    }
    
    private void Update()
    {
        if (GameState.Instance != null)
        {
            CheckScheduledDialogues();
        }
    }
    
    /// <summary>
    /// 添加对话到调度器
    /// </summary>
    /// <param name="action">对话动作数据</param>
    /// <param name="pathPointIndex">路径点索引</param>
    public void ScheduleDialogue(ActionData action, int pathPointIndex)
    {
        if (action == null) return;
        
        var scheduledDialogue = new ScheduledDialogue
        {
            action = action,
            pathPointIndex = pathPointIndex,
            addedTime = Time.time,
            currentStoryTime = GameState.Instance?.GetCurrentStoryTime() ?? 0f
        };
        
        if (action.IsImmediateTrigger)
        {
            // 立即触发的对话加入即时队列
            AddToImmediateQueue(scheduledDialogue);
            DebugLog($"添加立即对话到队列: 路径点{pathPointIndex}, 优先级{action.priority}");
        }
        else
        {
            // 时间触发的对话加入时间调度队列
            scheduledDialogues.Add(scheduledDialogue);
            DebugLog($"添加时间触发对话: 路径点{pathPointIndex}, 触发时间{action.triggerTime}秒, 优先级{action.priority}");
        }
    }
    
    /// <summary>
    /// 检查时间调度的对话是否到时间触发
    /// </summary>
    private void CheckScheduledDialogues()
    {
        if (scheduledDialogues.Count == 0) return;
        
        float currentStoryTime = GameState.Instance.GetCurrentStoryTime();
        
        // 查找所有到时间的对话
        var readyDialogues = scheduledDialogues
            .Where(d => currentStoryTime >= d.action.triggerTime)
            .ToList();
        
        if (readyDialogues.Count > 0)
        {
            // 从调度列表中移除
            foreach (var dialogue in readyDialogues)
            {
                scheduledDialogues.Remove(dialogue);
            }
            
            // 加入即时队列
            foreach (var dialogue in readyDialogues)
            {
                AddToImmediateQueue(dialogue);
                DebugLog($"时间触发对话就绪: 路径点{dialogue.pathPointIndex}, 当前时间{currentStoryTime:F1}秒");
            }
        }
    }
    
    /// <summary>
    /// 添加对话到即时队列并按优先级排序
    /// </summary>
    /// <param name="dialogue">对话数据</param>
    private void AddToImmediateQueue(ScheduledDialogue dialogue)
    {
        immediateQueue.Add(dialogue);
        
        // 按优先级降序排序（高优先级在前）
        immediateQueue.Sort((a, b) => b.action.priority.CompareTo(a.action.priority));
        
        // 立即触发最高优先级的对话
        ProcessNextDialogue();
    }
    
    /// <summary>
    /// 处理下一个对话
    /// </summary>
    private void ProcessNextDialogue()
    {
        if (immediateQueue.Count > 0)
        {
            var nextDialogue = immediateQueue[0];
            immediateQueue.RemoveAt(0);
            
            DebugLog($"触发对话: {nextDialogue.action.GetTriggerTimeText()}, {nextDialogue.action.GetPriorityText()}");
            
            // 通知对话执行器执行对话
            OnDialogueReady?.Invoke(nextDialogue.action);
        }
    }
    
    /// <summary>
    /// 获取当前队列状态信息
    /// </summary>
    public DialogueQueueStatus GetQueueStatus()
    {
        return new DialogueQueueStatus
        {
            immediateQueueCount = immediateQueue.Count,
            scheduledQueueCount = scheduledDialogues.Count,
            nextScheduledTime = scheduledDialogues.Count > 0 ? 
                scheduledDialogues.Min(d => d.action.triggerTime) : -1f,
            currentStoryTime = GameState.Instance?.GetCurrentStoryTime() ?? 0f
        };
    }
    
    /// <summary>
    /// 清空所有队列
    /// </summary>
    public void ClearAllQueues()
    {
        immediateQueue.Clear();
        scheduledDialogues.Clear();
        DebugLog("清空所有对话队列");
    }
    
    /// <summary>
    /// 获取指定优先级以上的对话数量
    /// </summary>
    /// <param name="minPriority">最小优先级</param>
    /// <returns>对话数量</returns>
    public int GetHighPriorityDialogueCount(int minPriority = 1)
    {
        return immediateQueue.Count(d => d.action.priority >= minPriority) +
               scheduledDialogues.Count(d => d.action.priority >= minPriority);
    }
    
    /// <summary>
    /// 调试日志
    /// </summary>
    private void DebugLog(string message)
    {
        if (showDebugInfo)
        {
            Debug.Log($"[{gameObject.name}] DialogueScheduler: {message}");
        }
    }
    
    /// <summary>
    /// 在编辑器中显示调试信息
    /// </summary>
    private void OnGUI()
    {
        if (!showDebugInfo || !Application.isPlaying) return;
        
        var status = GetQueueStatus();
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label($"NPC对话调度器 - {gameObject.name}", new GUIStyle { normal = { textColor = Color.white }, fontSize = 12, fontStyle = FontStyle.Bold });
        GUILayout.Label($"当前故事时间: {status.currentStoryTime:F1}秒");
        GUILayout.Label($"即时队列: {status.immediateQueueCount}");
        GUILayout.Label($"时间队列: {status.scheduledQueueCount}");
        
        if (status.nextScheduledTime >= 0)
        {
            GUILayout.Label($"下次触发: {status.nextScheduledTime:F1}秒");
        }
        
        if (immediateQueue.Count > 0)
        {
            GUILayout.Label("即时队列:", new GUIStyle { normal = { textColor = Color.yellow } });
            foreach (var dialogue in immediateQueue.Take(3))
            {
                GUILayout.Label($"  点{dialogue.pathPointIndex}: {dialogue.action.GetPriorityText()}");
            }
        }
        
        GUILayout.EndArea();
    }
}

/// <summary>
/// 已调度的对话数据
/// </summary>
[Serializable]
public class ScheduledDialogue
{
    public ActionData action;           // 对话动作数据
    public int pathPointIndex;          // 路径点索引
    public float addedTime;             // 添加到队列的游戏时间
    public float currentStoryTime;      // 添加时的故事时间
}

/// <summary>
/// 对话队列状态信息
/// </summary>
[Serializable]
public class DialogueQueueStatus
{
    public int immediateQueueCount;     // 即时队列数量
    public int scheduledQueueCount;     // 时间队列数量
    public float nextScheduledTime;     // 下次调度时间
    public float currentStoryTime;      // 当前故事时间
} 