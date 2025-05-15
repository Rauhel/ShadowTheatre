// 文件: NPCController.cs
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NPCController : MonoBehaviour
{
    [Header("NPC配置")]
    public NPCData npcData;

    [Header("引用")]
    public Animator animator;

    // 保护字段
    protected NavMeshAgent agent;

    // 公共属性
    public bool IsMoving => agent != null && !agent.isStopped;
    public NPCData Data => npcData;

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (npcData == null)
        {
            Debug.LogError($"[{gameObject.name}] 没有设置NPCData!");
        }

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
    }
}