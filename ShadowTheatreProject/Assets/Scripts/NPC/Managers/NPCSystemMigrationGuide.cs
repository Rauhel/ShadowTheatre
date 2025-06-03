using UnityEngine;

/// <summary>
/// NPC系统重构迁移指南
/// 
/// 【重要更改】
/// 旧系统的 NPCAnimationManager 和 NPCDialogueManager 已被统一的 NPCActionExecutor 替代
/// 
/// 【迁移步骤】
/// 1. 移除所有NPC对象上的 NPCAnimationManager 和 NPCDialogueManager 组件
/// 2. 添加新的 NPCActionExecutor 组件
/// 3. 设置对话气泡预制体 (dialoguePrefab)
/// 4. 确保NPC有 SpriteSheetAnimator 和 AudioSource 组件
/// 
/// 【ActionData字段更改】
/// - loopAnimation (bool) → animationLoopCount (int) // 0=不播放, 1+=循环次数
/// - waitTime → stopTime // 停止移动时间
/// - delay // 现在表示距离上一个行动结束的延迟时间
/// - 新增: oneShotSFX (AudioClip) // 一次性音效
/// - 删除: overridePrevious // 不再需要覆盖机制
/// 
/// 【新特性】
/// - 统一的Action队列系统
/// - 自动移动控制 (stopTime)
/// - 支持动画循环次数设置
/// - 支持一次性音效播放
/// - 更精确的时间控制
/// 
/// 【API变更】
/// 旧方法 → 新方法
/// - NPCAnimationManager.PlayAnimation() → NPCActionExecutor.PlayAnimation()
/// - NPCDialogueManager.DisplayDialogue() → 通过ActionData配置
/// 
/// 【注意事项】
/// - 所有action现在通过队列系统串行执行
/// - 同一action内的对话、动画、音效会同时开始
/// - NPC移动由stopTime字段自动控制
/// - 事件action和默认action都会正常执行
/// </summary>
[System.Obsolete("这是一个迁移指南脚本，不应该添加到游戏对象上")]
public class NPCSystemMigrationGuide : MonoBehaviour
{
    [Header("⚠️ 这是迁移指南脚本")]
    [TextArea(5, 10)]
    public string migrationInstructions = 
        "请查看脚本注释了解详细的迁移步骤。\n" +
        "完成迁移后请移除此组件。";

    private void Awake()
    {
        Debug.LogError($"[{gameObject.name}] NPCSystemMigrationGuide 不应该在运行时存在！请移除此组件并完成迁移。");
        Destroy(this);
    }
} 