/// <summary>
/// Editor文件夹重构日志
/// 记录Editor系统的修复和优化过程
/// 
/// 日期: 2024年
/// </summary>

/*
=== Editor引用问题修复总结 ===

🔧 修复的编译错误：
1. NPCPathManager.cs中waitTime字段引用错误
   - 将action.waitTime改为action.stopTime (4处修复)

2. NPCAnimationManagerEditor.cs引用已删除的类
   - 删除NPCAnimationManagerEditor.cs文件

🔄 字段名称统一：
1. ActionData字段重命名:
   - waitTime → stopTime (停止移动时间)
   - loopAnimation → animationLoopCount (动画循环次数)
   - 移除overridePrevious字段

2. 新增字段支持:
   - oneShotSFX (一次性音效)
   - animationLoopCount (动画循环次数，int类型)

📁 文件结构优化：
1. 合并编辑器文件:
   - NPCDialogueEditor.cs合并到NPCActionEditor.cs
   - 统一ActionData编辑界面
   - 减少代码重复，提高维护性

2. 更新文件引用:
   - 更新NPCDataEditor.cs，移除dialogueEditor引用
   - 保持ActionEditor的DrawPathAction方法

📊 导入导出系统修复：
1. NPCDataImportExport.cs:
   - 修复字段映射关系
   - 更新CSV表头结构
   - 修正animationLoopCount的数据类型处理

2. NPCDataSimpleImportExport.cs:
   - 同步字段名称变更
   - 保持简化版导入导出功能

✅ 验证结果：
- 所有编译错误已修复
- 编辑器界面功能完整
- 导入导出功能正常
- 字段引用一致性检查通过

🎯 优化效果：
- 代码结构更清晰
- 编辑器功能更统一
- 维护成本降低
- 用户体验提升

=== 新的ActionData结构 ===
public class ActionData
{
    public string dialogueText;         // 对话文本
    public float displayDuration;       // 对话显示时间
    public AudioClip voiceClip;         // 语音片段
    public string animationName;        // 动画名称
    public int animationLoopCount;      // 动画循环次数 (0=不播放)
    public AudioClip oneShotSFX;       // 一次性音效
    public float delay;                 // 延迟时间
    public float stopTime;              // 停止移动时间
    public int pathPointIndex;          // 路径点索引
    public bool isActionActive;         // 动作是否可用
}

=== 编辑器文件结构 ===
NPCDataSystem/
├── NPCDataEditor.cs           (主编辑器)
├── NPCActionEditor.cs         (动作编辑器 - 已合并对话编辑功能)
├── NPCPathConfigEditor.cs     (路径配置编辑器)
├── NPCPathEventEditor.cs      (路径事件编辑器)
├── NPCEditorUtility.cs        (编辑器工具类)
└── ImportExport/              (导入导出功能)
    ├── NPCDataImportExport.cs      (完整版)
    └── NPCDataSimpleImportExport.cs (简化版)

*/ 