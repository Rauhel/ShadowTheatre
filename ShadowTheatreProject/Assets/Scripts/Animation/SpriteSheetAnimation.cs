using UnityEngine;

/// <summary>
/// 表示一个精灵表单 (SpriteSheet) 动画
/// </summary>
[System.Serializable]
public class SpriteSheetAnimation
{
    public string animationName;       // 动画名称
    public Texture2D spriteSheet;      // 包含所有帧的贴图 (修改为 Texture2D)
    public int frameCount = 4;         // 帧数量
    public float frameRate = 10f;      // 帧率 (fps)

    [Tooltip("如果为 true，将自动计算 UV 布局")]
    public bool autoCalculate = true;

    [Header("手动设置 (autoCalculate = false)")]
    public bool horizontal = true;     // 帧排列方向：水平 = true，垂直 = false
}