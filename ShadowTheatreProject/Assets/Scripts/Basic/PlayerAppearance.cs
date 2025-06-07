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
    [SerializeField] private bool enableDetailedDebug = true;

    // 当前激活的手势类型
    private PlayerManager.ShadowType currentShadowType = PlayerManager.ShadowType.None;

    private void Awake()
    {
        Debug.Log($"[PlayerAppearance] === Awake开始 === GameObject: {gameObject.name}");
        
        // 获取SpriteSheetAnimator组件
        if (spriteAnimator == null)
        {
            Debug.Log($"[PlayerAppearance] spriteAnimator为空，尝试从组件中获取...");
            spriteAnimator = GetComponent<SpriteSheetAnimator>();
            
            if (spriteAnimator != null)
            {
                Debug.Log($"[PlayerAppearance] ✓ 成功获取到SpriteSheetAnimator组件");
            }
            else
            {
                Debug.LogError($"[PlayerAppearance] ✗ 从组件中也获取不到SpriteSheetAnimator！");
            }
        }
        else
        {
            Debug.Log($"[PlayerAppearance] ✓ spriteAnimator已在Inspector中设置");
        }
        
        animatorFound = (spriteAnimator != null);
        
        if (!animatorFound)
        {
            Debug.LogError($"[PlayerAppearance] ✗ 在 {gameObject.name} 上找不到SpriteSheetAnimator组件！");
            
            // 尝试查找所有可能的组件
            var allRenderers = GetComponents<Renderer>();
            var allMonoBehaviours = GetComponents<MonoBehaviour>();
            Debug.Log($"[PlayerAppearance] 调试信息 - 找到的Renderer组件数量: {allRenderers.Length}");
            Debug.Log($"[PlayerAppearance] 调试信息 - 找到的MonoBehaviour组件数量: {allMonoBehaviours.Length}");
            
            foreach (var mb in allMonoBehaviours)
            {
                Debug.Log($"[PlayerAppearance] MonoBehaviour组件: {mb.GetType().Name}");
            }
        }
        else
        {
            Debug.Log($"[PlayerAppearance] ✓ SpriteSheetAnimator组件已找到并设置");
        }
        
        Debug.Log($"[PlayerAppearance] === Awake结束 ===");
    }

    private void Start()
    {
        Debug.Log($"[PlayerAppearance] === Start开始 ===");
        
        // 显示动画器状态
        if (spriteAnimator != null)
        {
            var availableAnimations = spriteAnimator.GetAvailableAnimations();
            Debug.Log($"[PlayerAppearance] 可用动画数量: {availableAnimations.Count}");
            
            if (availableAnimations.Count > 0)
            {
                Debug.Log($"[PlayerAppearance] 可用动画列表: {string.Join(", ", availableAnimations)}");
            }
            else
            {
                Debug.LogWarning($"[PlayerAppearance] ⚠️ 没有找到任何可用动画！请检查SpriteSheetAnimator的配置");
            }
        }
        
        // 初始显示默认外观
        Debug.Log($"[PlayerAppearance] 尝试播放默认动画...");
        PlayDefaultAnimation();
        
        Debug.Log($"[PlayerAppearance] === Start结束 ===");
    }

    private void OnEnable()
    {
        Debug.Log($"[PlayerAppearance] OnEnable - 组件已启用");
    }

    /// <summary>
    /// 根据阴影类型更改玩家外观
    /// </summary>
    public void ChangeShadowType(PlayerManager.ShadowType shadowType)
    {
        Debug.Log($"[PlayerAppearance] === ChangeShadowType调用 === 输入类型: {shadowType}");
        
        // 如果类型相同，不需要更新
        if (currentShadowType == shadowType)
        {
            Debug.Log($"[PlayerAppearance] 阴影类型相同 ({shadowType})，跳过更新");
            return;
        }

        Debug.Log($"[PlayerAppearance] 阴影类型变更: {currentShadowType} -> {shadowType}");

        // 更新当前阴影类型
        currentShadowType = shadowType;

        // 获取对应的动画名称
        string animationName = GetAnimationNameFromShadowType(shadowType);
        currentGestureName = animationName;
        
        Debug.Log($"[PlayerAppearance] 映射结果: {shadowType} -> '{animationName}'");

        // 播放对应的动画
        PlayGestureAnimation(animationName);

        Debug.Log($"[PlayerAppearance] === ChangeShadowType完成 === 最终手势: {animationName}");
    }

    /// <summary>
    /// 将阴影类型转换为动画名称
    /// </summary>
    private string GetAnimationNameFromShadowType(PlayerManager.ShadowType shadowType)
    {
        Debug.Log($"[PlayerAppearance] 映射阴影类型到动画名称: {shadowType}");
        
        string result;
        switch (shadowType)
        {
            case PlayerManager.ShadowType.Bird:
                result = "Bird";
                break;
            case PlayerManager.ShadowType.Wolf:
                result = "Wolf";
                break;
            case PlayerManager.ShadowType.Fist:
                result = "Fist";
                break;
            case PlayerManager.ShadowType.Goose:
                result = "Goose";
                break;
            case PlayerManager.ShadowType.Frog:
                result = "Frog";
                break;
            case PlayerManager.ShadowType.Owl:
                result = "Owl";
                break;
            case PlayerManager.ShadowType.None:
            default:
                result = defaultAnimationName;
                break;
        }
        
        Debug.Log($"[PlayerAppearance] 映射结果: {shadowType} -> '{result}'");
        return result;
    }

    /// <summary>
    /// 播放手势对应的动画
    /// </summary>
    private void PlayGestureAnimation(string animationName)
    {
        Debug.Log($"[PlayerAppearance] === PlayGestureAnimation开始 === 动画名称: '{animationName}'");
        
        if (spriteAnimator == null)
        {
            Debug.LogError($"[PlayerAppearance] ✗ SpriteSheetAnimator组件为空，无法播放动画: {animationName}");
            return;
        }

        Debug.Log($"[PlayerAppearance] ✓ SpriteSheetAnimator组件存在，继续处理...");

        // 检查动画是否存在
        var availableAnimations = spriteAnimator.GetAvailableAnimations();
        Debug.Log($"[PlayerAppearance] 当前可用动画数量: {availableAnimations.Count}");
        Debug.Log($"[PlayerAppearance] 可用动画: [{string.Join(", ", availableAnimations)}]");
        
        bool animationExists = availableAnimations.Contains(animationName);
        Debug.Log($"[PlayerAppearance] 查找动画 '{animationName}': {(animationExists ? "✓ 找到" : "✗ 未找到")}");

        if (animationExists)
        {
            Debug.Log($"[PlayerAppearance] 尝试播放动画: '{animationName}' (单帧，不循环)");
            
            // 播放指定动画（单帧，不循环）
            spriteAnimator.Play(animationName, false);
            
            // 检查动画是否成功开始播放
            if (spriteAnimator.IsPlaying())
            {
                Debug.Log($"[PlayerAppearance] ✓ 动画 '{animationName}' 成功开始播放");
            }
            else
            {
                Debug.LogWarning($"[PlayerAppearance] ⚠️ 动画 '{animationName}' 可能没有成功播放");
            }
            
            // 检查当前播放的动画名称
            string currentPlayingAnim = spriteAnimator.GetCurrentAnimationName();
            Debug.Log($"[PlayerAppearance] 当前播放的动画: '{currentPlayingAnim}'");
        }
        else
        {
            // 找不到对应动画，播放默认动画
            Debug.LogWarning($"[PlayerAppearance] ⚠️ 找不到动画 '{animationName}'，播放默认动画 '{defaultAnimationName}'");
            PlayDefaultAnimation();
        }
        
        Debug.Log($"[PlayerAppearance] === PlayGestureAnimation结束 ===");
    }

    /// <summary>
    /// 播放默认动画
    /// </summary>
    private void PlayDefaultAnimation()
    {
        Debug.Log($"[PlayerAppearance] === PlayDefaultAnimation开始 ===");
        
        if (spriteAnimator == null)
        {
            Debug.LogError($"[PlayerAppearance] ✗ spriteAnimator为空，无法播放默认动画");
            return;
        }

        var availableAnimations = spriteAnimator.GetAvailableAnimations();
        Debug.Log($"[PlayerAppearance] 检查默认动画 '{defaultAnimationName}' 是否存在...");
        Debug.Log($"[PlayerAppearance] 可用动画: [{string.Join(", ", availableAnimations)}]");
        
        if (availableAnimations.Contains(defaultAnimationName))
        {
            Debug.Log($"[PlayerAppearance] ✓ 找到默认动画 '{defaultAnimationName}'，开始播放");
            
            spriteAnimator.Play(defaultAnimationName, false);
            currentGestureName = defaultAnimationName;
            
            if (spriteAnimator.IsPlaying())
            {
                Debug.Log($"[PlayerAppearance] ✓ 默认动画 '{defaultAnimationName}' 成功播放");
            }
            else
            {
                Debug.LogWarning($"[PlayerAppearance] ⚠️ 默认动画 '{defaultAnimationName}' 可能没有成功播放");
            }
        }
        else
        {
            Debug.LogError($"[PlayerAppearance] ✗ 找不到默认动画 '{defaultAnimationName}'！");
            Debug.LogError($"[PlayerAppearance] 可用动画: {string.Join(", ", availableAnimations)}");
            
            // 如果连默认动画都没有，尝试播放第一个可用动画
            if (availableAnimations.Count > 0)
            {
                string firstAnim = availableAnimations[0];
                Debug.Log($"[PlayerAppearance] 尝试播放第一个可用动画: '{firstAnim}'");
                spriteAnimator.Play(firstAnim, false);
                currentGestureName = firstAnim;
            }
            else
            {
                Debug.LogError($"[PlayerAppearance] ✗ 没有任何可用动画！请检查SpriteSheetAnimator配置");
            }
        }
        
        Debug.Log($"[PlayerAppearance] === PlayDefaultAnimation结束 ===");
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
        Debug.Log($"[PlayerAppearance] === 动画配置详情 ===");
        
        if (spriteAnimator != null)
        {
            var animations = spriteAnimator.GetAvailableAnimations();
            Debug.Log($"[PlayerAppearance] 动画总数: {animations.Count}");
            
            for (int i = 0; i < animations.Count; i++)
            {
                Debug.Log($"[PlayerAppearance] 动画 {i + 1}: '{animations[i]}'");
            }
        }
        else
        {
            Debug.LogError($"[PlayerAppearance] SpriteAnimator为空，无法获取动画列表");
        }
        
        Debug.Log($"[PlayerAppearance] 当前状态:");
        Debug.Log($"  - 当前手势: {currentGestureName}");
        Debug.Log($"  - 当前阴影类型: {currentShadowType}");
        Debug.Log($"  - 动画器找到: {animatorFound}");
        Debug.Log($"  - 默认动画名称: {defaultAnimationName}");
        Debug.Log($"[PlayerAppearance] ==================");
    }

    // 编辑器调试方法
    [ContextMenu("播放默认动画")]
    private void DebugPlayDefault()
    {
        Debug.Log($"[PlayerAppearance] 手动触发播放默认动画");
        PlayDefaultAnimation();
    }

    [ContextMenu("显示可用动画")]
    private void DebugShowAnimations()
    {
        LogAvailableAnimations();
    }

    [ContextMenu("测试Bird手势")]
    private void DebugTestBird()
    {
        Debug.Log($"[PlayerAppearance] 手动测试Bird手势");
        ChangeShadowType(PlayerManager.ShadowType.Bird);
    }

    [ContextMenu("测试Wolf手势")]
    private void DebugTestWolf()
    {
        Debug.Log($"[PlayerAppearance] 手动测试Wolf手势");
        ChangeShadowType(PlayerManager.ShadowType.Wolf);
    }

    [ContextMenu("显示组件状态")]
    private void DebugShowComponentStatus()
    {
        Debug.Log($"[PlayerAppearance] === 组件状态检查 ===");
        Debug.Log($"GameObject: {gameObject.name}");
        Debug.Log($"组件已启用: {enabled}");
        Debug.Log($"GameObject活跃: {gameObject.activeInHierarchy}");
        Debug.Log($"SpriteAnimator引用: {(spriteAnimator != null ? "✓ 存在" : "✗ 为空")}");
        
        if (spriteAnimator != null)
        {
            Debug.Log($"SpriteAnimator已启用: {spriteAnimator.enabled}");
            Debug.Log($"SpriteAnimator GameObject活跃: {spriteAnimator.gameObject.activeInHierarchy}");
        }
        
        Debug.Log($"[PlayerAppearance] ==================");
    }
}