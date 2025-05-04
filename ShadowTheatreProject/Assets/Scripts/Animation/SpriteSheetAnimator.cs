using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 处理精灵表单 (SpriteSheet) 动画的组件
/// </summary>
public class SpriteSheetAnimator : MonoBehaviour
{
    [Header("渲染器设置")]
    public Renderer targetRenderer;  // 可以是MeshRenderer

    [Header("动画数据")]
    public List<SpriteSheetAnimation> animations = new List<SpriteSheetAnimation>();

    [Header("调试信息")]
    [SerializeField] private string currentAnimationName;
    [SerializeField] private bool isPlaying;
    [SerializeField] private bool isLooping;
    [SerializeField] private int currentFrame;
    [SerializeField] private float frameTimer;

    [Header("比例设置")]
    [SerializeField] private bool respectStaticHandlerScale = true;

    // 私有字段
    private SpriteSheetAnimation currentAnimation;
    private MaterialPropertyBlock propertyBlock;

    // Shader属性ID
    private static readonly int MainTexProperty = Shader.PropertyToID("_MainTex");
    private static readonly int FrameIndexProperty = Shader.PropertyToID("_FrameIndex");
    private static readonly int FrameCountProperty = Shader.PropertyToID("_FrameCount");

    private void Awake()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<MeshRenderer>();
        }

        propertyBlock = new MaterialPropertyBlock();
    }

    private void Start()
    {
        // 检查是否存在StaticTextureHandler组件，如果存在，则使用其调整的比例
        StaticTextureHandler staticHandler = GetComponent<StaticTextureHandler>();
        if (staticHandler != null && staticHandler.texture != null)
        {
            // 静态贴图处理器已经处理了初始大小和比例，不需要额外操作
            // 我们可以使用该组件计算的尺寸比例来设置动画尺寸
            Debug.Log($"[{gameObject.name}] 使用StaticTextureHandler的比例设置");
        }
    }

    private void Update()
    {
        if (!isPlaying || currentAnimation == null)
        {
            if (Time.frameCount % 300 == 0) // 每300帧检查一次
            {
                Debug.Log($"[{gameObject.name}] 动画未播放 - isPlaying:{isPlaying}, 有当前动画:{currentAnimation != null}");
            }
            return;
        }

        // 更新帧计时器
        frameTimer += Time.deltaTime;
        float frameDuration = 1f / currentAnimation.frameRate;

        // 检查是否需要更新帧
        if (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            currentFrame++;

            // 检查是否需要循环或停止
            if (currentFrame >= currentAnimation.frameCount)
            {
                if (isLooping)
                {
                    currentFrame = 0;
                    Debug.Log($"[{gameObject.name}] 动画 {currentAnimationName} 循环");
                }
                else
                {
                    currentFrame = currentAnimation.frameCount - 1;
                    isPlaying = false;
                    Debug.Log($"[{gameObject.name}] 动画 {currentAnimationName} 完成播放");
                }
            }

            // 更新渲染器显示当前帧
            UpdateFrame();

            // 每次更新帧时记录日志
            Debug.Log($"[{gameObject.name}] 更新帧: {currentFrame}/{currentAnimation.frameCount}, 动画:{currentAnimationName}");
        }
    }

    /// <summary>
    /// 播放指定名称的动画
    /// </summary>
    public void Play(string animationName, bool loop = true)
    {
        Debug.Log($"[{gameObject.name}] 尝试播放动画: {animationName}, 循环播放: {loop}");

        // 查找动画数据
        SpriteSheetAnimation anim = animations.Find(a => a.animationName == animationName);
        if (anim == null)
        {
            Debug.LogWarning($"[{gameObject.name}] 动画未找到: {animationName}, 可用动画: {string.Join(", ", GetAvailableAnimations())}");
            return;
        }

        Debug.Log($"[{gameObject.name}] 找到动画: {animationName}, 帧数: {anim.frameCount}, 帧率: {anim.frameRate}");

        // 设置当前动画
        currentAnimation = anim;
        currentAnimationName = animationName;

        // 重置帧状态
        currentFrame = 0;
        frameTimer = 0;
        isPlaying = true;
        isLooping = loop;

        // 更新材质属性
        if (targetRenderer != null)
        {
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetTexture(MainTexProperty, anim.spriteSheet);
            propertyBlock.SetFloat(FrameCountProperty, anim.frameCount);
            propertyBlock.SetFloat(FrameIndexProperty, currentFrame);
            targetRenderer.SetPropertyBlock(propertyBlock);

            // 检查是否需要尊重静态处理器的比例
            if (!respectStaticHandlerScale)
            {
                // 如果不使用静态处理器的比例，可以在这里添加自己的比例调整逻辑
                // 例如，调整为动画贴图的比例
            }

            Debug.Log($"[{gameObject.name}] 设置材质参数: 贴图={anim.spriteSheet != null}, 帧数={anim.frameCount}, 当前帧={currentFrame}");
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] targetRenderer为空，无法设置材质属性");
        }
    }

    /// <summary>
    /// 停止当前动画
    /// </summary>
    public void Stop()
    {
        isPlaying = false;
    }

    /// <summary>
    /// 暂停当前动画
    /// </summary>
    public void Pause()
    {
        isPlaying = false;
    }

    /// <summary>
    /// 恢复当前动画
    /// </summary>
    public void Resume()
    {
        isPlaying = true;
    }

    /// <summary>
    /// 更新帧索引
    /// </summary>
    private void UpdateFrame()
    {
        if (currentAnimation == null || targetRenderer == null)
        {
            Debug.LogWarning($"[{gameObject.name}] UpdateFrame失败: currentAnimation={currentAnimation != null}, targetRenderer={targetRenderer != null}");
            return;
        }

        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(FrameIndexProperty, currentFrame);
        targetRenderer.SetPropertyBlock(propertyBlock);

        Debug.Log($"[{gameObject.name}] 更新材质属性: 帧={currentFrame}");
    }

    /// <summary>
    /// 获取当前动画的名称
    /// </summary>
    public string GetCurrentAnimationName()
    {
        return currentAnimationName;
    }

    /// <summary>
    /// 获取所有可用的动画名称列表
    /// </summary>
    public List<string> GetAvailableAnimations()
    {
        List<string> animNames = new List<string>();
        foreach (var anim in animations)
        {
            animNames.Add(anim.animationName);
        }
        return animNames;
    }

    /// <summary>
    /// 该动画是否正在播放
    /// </summary>
    public bool IsPlaying()
    {
        return isPlaying;
    }

    /// <summary>
    /// RuntimeAnimatorController 属性，用于兼容原有代码
    /// </summary>
    public RuntimeAnimatorController runtimeAnimatorController
    {
        get { return null; }
    }
}