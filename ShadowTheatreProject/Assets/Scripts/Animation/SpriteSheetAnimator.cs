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

    private void Update()
    {
        if (!isPlaying || currentAnimation == null)
            return;

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
                }
                else
                {
                    currentFrame = currentAnimation.frameCount - 1;
                    isPlaying = false;
                }
            }

            // 更新渲染器显示当前帧
            UpdateFrame();
        }
    }

    /// <summary>
    /// 播放指定名称的动画
    /// </summary>
    public void Play(string animationName, bool loop = true)
    {
        // 查找动画数据
        SpriteSheetAnimation anim = animations.Find(a => a.animationName == animationName);
        if (anim == null)
        {
            Debug.LogWarning($"动画未找到: {animationName}");
            return;
        }

        // 如果已经在播放此动画且设置一致，不重复设置
        if (currentAnimation == anim && isPlaying && isLooping == loop)
            return;

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
            return;

        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(FrameIndexProperty, currentFrame);
        targetRenderer.SetPropertyBlock(propertyBlock);
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