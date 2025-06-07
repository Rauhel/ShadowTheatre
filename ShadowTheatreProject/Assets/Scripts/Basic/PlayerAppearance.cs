using UnityEngine;

/// <summary>
/// 玩家外观组件：负责处理玩家的外观变化，使用动画系统切换手势对应的贴图
/// </summary>
public class PlayerAppearance : MonoBehaviour
{
    [Header("动画组件")]
    [SerializeField] private SpriteSheetAnimator spriteAnimator;
    
    [Header("默认动画设置")]
    [SerializeField] private string defaultAnimationName = "Default";
    
    [Header("调试信息")]
    [SerializeField] private string currentGestureName;
    [SerializeField] private bool animatorFound;

    // 当前激活的手势类型
    private PlayerManager.ShadowType currentShadowType = PlayerManager.ShadowType.None;

    private void Awake()
    {
        // 获取SpriteSheetAnimator组件
        if (spriteAnimator == null)
        {
            spriteAnimator = GetComponent<SpriteSheetAnimator>();
        }
        
        animatorFound = (spriteAnimator != null);
        
        if (!animatorFound)
        {
            Debug.LogError($"[PlayerAppearance] 在 {gameObject.name} 上找不到SpriteSheetAnimator组件！");
        }
    }

    private void Start()
    {
        // 初始显示默认外观
        PlayDefaultAnimation();
    }

    /// <summary>
    /// 根据阴影类型更改玩家外观
    /// </summary>
    public void ChangeShadowType(PlayerManager.ShadowType shadowType)
    {
        // 如果类型相同，不需要更新
        if (currentShadowType == shadowType) return;

        // 更新当前阴影类型
        currentShadowType = shadowType;

        // 获取对应的动画名称
        string animationName = GetAnimationNameFromShadowType(shadowType);
        currentGestureName = animationName;

        // 播放对应的动画
        PlayGestureAnimation(animationName);

        Debug.Log($"[PlayerAppearance] 玩家外观已更改为: {shadowType} -> 动画: {animationName}");
    }

    /// <summary>
    /// 将阴影类型转换为动画名称
    /// </summary>
    private string GetAnimationNameFromShadowType(PlayerManager.ShadowType shadowType)
    {
        switch (shadowType)
        {
            case PlayerManager.ShadowType.Bird:
                return "Bird";
            case PlayerManager.ShadowType.Wolf:
                return "Wolf";
            case PlayerManager.ShadowType.Fist:
                return "Fist";
            case PlayerManager.ShadowType.Goose:
                return "Goose";
            case PlayerManager.ShadowType.Frog:
                return "Frog";
            case PlayerManager.ShadowType.Owl:
                return "Owl";
            case PlayerManager.ShadowType.None:
            default:
                return defaultAnimationName;
        }
    }

    /// <summary>
    /// 播放手势对应的动画
    /// </summary>
    private void PlayGestureAnimation(string animationName)
    {
        if (spriteAnimator == null)
        {
            Debug.LogError($"[PlayerAppearance] SpriteSheetAnimator组件为空，无法播放动画: {animationName}");
            return;
        }

        // 检查动画是否存在
        var availableAnimations = spriteAnimator.GetAvailableAnimations();
        bool animationExists = availableAnimations.Contains(animationName);

        if (animationExists)
        {
            // 播放指定动画（单帧，不循环）
            spriteAnimator.Play(animationName, false);
            Debug.Log($"[PlayerAppearance] 播放动画: {animationName}");
        }
        else
        {
            // 找不到对应动画，播放默认动画
            Debug.LogWarning($"[PlayerAppearance] 找不到动画 '{animationName}'，播放默认动画 '{defaultAnimationName}'");
            PlayDefaultAnimation();
        }
    }

    /// <summary>
    /// 播放默认动画
    /// </summary>
    private void PlayDefaultAnimation()
    {
        if (spriteAnimator == null) return;

        var availableAnimations = spriteAnimator.GetAvailableAnimations();
        if (availableAnimations.Contains(defaultAnimationName))
        {
            spriteAnimator.Play(defaultAnimationName, false);
            currentGestureName = defaultAnimationName;
            Debug.Log($"[PlayerAppearance] 播放默认动画: {defaultAnimationName}");
        }
        else
        {
            Debug.LogError($"[PlayerAppearance] 找不到默认动画 '{defaultAnimationName}'！可用动画: {string.Join(", ", availableAnimations)}");
        }
    }

    /// <summary>
    /// 获取当前手势名称
    /// </summary>
    public string GetCurrentGestureName()
    {
        return currentGestureName;
    }

    /// <summary>
    /// 获取当前阴影类型
    /// </summary>
    public PlayerManager.ShadowType GetCurrentShadowType()
    {
        return currentShadowType;
    }

    /// <summary>
    /// 获取所有可用的动画列表（调试用）
    /// </summary>
    public void LogAvailableAnimations()
    {
        if (spriteAnimator != null)
        {
            var animations = spriteAnimator.GetAvailableAnimations();
            Debug.Log($"[PlayerAppearance] 可用动画: {string.Join(", ", animations)}");
        }
    }

    // 编辑器调试方法
    [ContextMenu("播放默认动画")]
    private void DebugPlayDefault()
    {
        PlayDefaultAnimation();
    }

    [ContextMenu("显示可用动画")]
    private void DebugShowAnimations()
    {
        LogAvailableAnimations();
    }
}