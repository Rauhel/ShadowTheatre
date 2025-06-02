using UnityEngine;

/// <summary>
/// 手势事件指示器 - 在NPC身上显示事件提示特效
/// 创建独立的子对象，不影响NPC自身的动画
/// </summary>
public class GestureEventIndicator : MonoBehaviour
{
    [Header("事件指示器动画")]
    [SerializeField] private SpriteSheetAnimation eventIndicatorAnimation;
    
    [Header("指示器设置")]
    [SerializeField] private string eventIndicatorAnimationName = "EventIndicator";
    [SerializeField] private Vector3 offsetFromNPC = new Vector3(0, 1.5f, 0);
    [SerializeField] private float indicatorSize = 0.5f;
    [SerializeField] private bool autoSetupOnAwake = true;
    
    [Header("调试设置")]
    [SerializeField] private bool enableFrameDebug = true; // 启用帧级别调试
    
    // 独立的指示器对象和组件
    private GameObject indicatorObject;
    private SpriteSheetAnimator indicatorAnimator;
    private MeshRenderer indicatorRenderer;
    private bool isVisible = false;
    
    private void Awake()
    {
        if (autoSetupOnAwake)
        {
            SetupIndicator();
        }
    }
    
    private void Update()
    {
        // 帧级别调试
        if (enableFrameDebug && isVisible && indicatorAnimator != null)
        {
            DebugAnimationFrame();
        }
    }
    
    /// <summary>
    /// 调试动画帧信息
    /// </summary>
    private void DebugAnimationFrame()
    {
        if (indicatorAnimator.IsPlaying())
        {
            // 使用反射或直接访问序列化字段来获取帧信息
            // 由于这些字段是 [SerializeField] private，我们需要用不同的方法来获取调试信息
            string currentAnim = indicatorAnimator.GetCurrentAnimationName();
            
            // 每秒输出一次播放状态（简化版）
            if (Time.frameCount % 60 == 0)
            {
                Debug.Log($"[{gameObject.name}] 🔄 动画播放状态: 播放中, 当前动画: '{currentAnim}'");
                
                // 检查材质和贴图
                if (indicatorRenderer != null && indicatorRenderer.material != null)
                {
                    var material = indicatorRenderer.material;
                    Debug.Log($"[{gameObject.name}] 🖼️ 材质贴图: {material.mainTexture?.name}, UV偏移: {material.mainTextureOffset}, UV缩放: {material.mainTextureScale}");
                }
                
                // 检查动画配置
                if (eventIndicatorAnimation != null)
                {
                    Debug.Log($"[{gameObject.name}] ⚙️ 动画配置: 总帧数={eventIndicatorAnimation.frameCount}, 帧率={eventIndicatorAnimation.frameRate}, 贴图={eventIndicatorAnimation.spriteSheet?.name}");
                }
            }
        }
        else if (Time.frameCount % 120 == 0) // 每2秒检查一次非播放状态
        {
            Debug.Log($"[{gameObject.name}] ⏸️ 动画未播放 - 动画器状态检查:");
            Debug.Log($"[{gameObject.name}] - 动画器存在: {indicatorAnimator != null}");
            Debug.Log($"[{gameObject.name}] - 指示器可见: {isVisible}");
            Debug.Log($"[{gameObject.name}] - 动画数量: {(indicatorAnimator != null ? indicatorAnimator.animations.Count : 0)}");
            if (indicatorAnimator != null && indicatorAnimator.animations.Count > 0)
            {
                Debug.Log($"[{gameObject.name}] - 第一个动画: {indicatorAnimator.animations[0]?.animationName}");
            }
        }
    }
    
    /// <summary>
    /// 设置独立的事件指示器对象
    /// </summary>
    [ContextMenu("设置事件指示器")]
    public void SetupIndicator()
    {
        Debug.Log($"[{gameObject.name}] 🔧 开始设置事件指示器");
        
        // 如果已经存在，先清理
        if (indicatorObject != null)
        {
            DestroyImmediate(indicatorObject);
        }
        
        // 创建独立的指示器对象
        indicatorObject = new GameObject("EventIndicator");
        indicatorObject.transform.SetParent(transform);
        indicatorObject.transform.localPosition = offsetFromNPC;
        indicatorObject.transform.localScale = Vector3.one * indicatorSize;
        
        // 添加MeshFilter和MeshRenderer
        MeshFilter meshFilter = indicatorObject.AddComponent<MeshFilter>();
        indicatorRenderer = indicatorObject.AddComponent<MeshRenderer>();
        
        // 创建简单的四边形网格
        meshFilter.mesh = CreateQuadMesh();
        
        // 添加独立的SpriteSheetAnimator
        indicatorAnimator = indicatorObject.AddComponent<SpriteSheetAnimator>();
        indicatorAnimator.targetRenderer = indicatorRenderer;
        
        // 设置默认材质
        SetupDefaultMaterial();
        
        // 如果有事件指示器动画，添加到动画器中
        if (eventIndicatorAnimation != null)
        {
            AddEventIndicatorAnimation();
        }
        
        // 初始状态隐藏
        indicatorObject.SetActive(false);
        
        Debug.Log($"[{gameObject.name}] ✅ 事件指示器设置完成");
    }
    
    /// <summary>
    /// 创建四边形网格
    /// </summary>
    private Mesh CreateQuadMesh()
    {
        Mesh mesh = new Mesh();
        
        Vector3[] vertices = new Vector3[4]
        {
            new Vector3(-0.5f, -0.5f, 0),
            new Vector3(0.5f, -0.5f, 0),
            new Vector3(-0.5f, 0.5f, 0),
            new Vector3(0.5f, 0.5f, 0)
        };
        
        Vector2[] uv = new Vector2[4]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1)
        };
        
        int[] triangles = new int[6]
        {
            0, 2, 1,
            2, 3, 1
        };
        
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        
        return mesh;
    }
    
    /// <summary>
    /// 设置默认材质 - 关键修复：确保SpriteSheetAnimator能正常工作
    /// </summary>
    private void SetupDefaultMaterial()
    {
        if (indicatorRenderer == null) return;
        
        Debug.Log($"[{gameObject.name}] 🎨 设置默认材质");
        
        // 创建材质，确保与SpriteSheetAnimator兼容
        Material mat = new Material(Shader.Find("Sprites/Default"));
        
        // 设置材质属性，确保支持精灵表动画
        mat.color = Color.white;
        
        // 如果有事件指示器动画，直接设置贴图
        if (eventIndicatorAnimation != null && eventIndicatorAnimation.spriteSheet != null)
        {
            mat.mainTexture = eventIndicatorAnimation.spriteSheet;
            Debug.Log($"[{gameObject.name}] 🖼️ 设置材质贴图: {eventIndicatorAnimation.spriteSheet.name} ({eventIndicatorAnimation.spriteSheet.width}x{eventIndicatorAnimation.spriteSheet.height})");
        }
        
        indicatorRenderer.material = mat;
        
        // 确保渲染器设置正确
        indicatorRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        indicatorRenderer.receiveShadows = false;
        
        Debug.Log($"[{gameObject.name}] ✅ 材质设置完成，Shader: {mat.shader.name}");
    }
    
    /// <summary>
    /// 添加事件指示器动画到独立的动画器中
    /// </summary>
    [ContextMenu("添加EventIndicator动画")]
    public void AddEventIndicatorAnimation()
    {
        if (indicatorAnimator == null || eventIndicatorAnimation == null) return;
        
        Debug.Log($"[{gameObject.name}] 🎭 添加事件指示器动画");
        
        // 设置动画名称
        eventIndicatorAnimation.animationName = eventIndicatorAnimationName;
        
        // 清空原有动画列表，只保留EventIndicator动画
        indicatorAnimator.animations.Clear();
        indicatorAnimator.animations.Add(eventIndicatorAnimation);
        
        // 更新材质贴图
        if (indicatorRenderer != null && eventIndicatorAnimation.spriteSheet != null)
        {
            indicatorRenderer.material.mainTexture = eventIndicatorAnimation.spriteSheet;
        }
        
        Debug.Log($"[{gameObject.name}] ✅ 动画添加完成: '{eventIndicatorAnimationName}', 帧数: {eventIndicatorAnimation.frameCount}, 帧率: {eventIndicatorAnimation.frameRate}");
    }
    
    /// <summary>
    /// 显示事件指示器
    /// </summary>
    public void ShowIndicator()
    {
        Debug.Log($"[{gameObject.name}] 🟢 显示事件指示器");
        
        if (isVisible) return;
        
        if (indicatorObject == null)
        {
            SetupIndicator();
        }
        
        if (indicatorObject == null) return;
        
        isVisible = true;
        indicatorObject.SetActive(true);
        
        Debug.Log($"[{gameObject.name}] 📍 指示器对象已激活, 位置: {indicatorObject.transform.position}");
        
        // 播放事件指示器动画（循环播放）
        if (indicatorAnimator != null && HasAnimation(eventIndicatorAnimationName))
        {
            Debug.Log($"[{gameObject.name}] ▶️ 开始播放动画: '{eventIndicatorAnimationName}' (循环模式)");
            indicatorAnimator.Play(eventIndicatorAnimationName, true);
            
            // 立即检查播放状态
            Debug.Log($"[{gameObject.name}] 🔍 播放后状态检查:");
            Debug.Log($"[{gameObject.name}] - IsPlaying: {indicatorAnimator.IsPlaying()}");
            Debug.Log($"[{gameObject.name}] - 当前动画: '{indicatorAnimator.GetCurrentAnimationName()}'");
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] ❌ 无法播放动画: animator={indicatorAnimator != null}, hasAnim={HasAnimation(eventIndicatorAnimationName)}");
            if (indicatorAnimator != null)
            {
                var availableAnims = indicatorAnimator.GetAvailableAnimations();
                Debug.LogWarning($"[{gameObject.name}] 可用动画: {string.Join(", ", availableAnims)}");
            }
        }
    }
    
    /// <summary>
    /// 隐藏事件指示器
    /// </summary>
    public void HideIndicator()
    {
        Debug.Log($"[{gameObject.name}] 🔴 隐藏事件指示器");
        
        if (!isVisible) return;
        
        isVisible = false;
        
        if (indicatorAnimator != null)
        {
            indicatorAnimator.Stop();
            Debug.Log($"[{gameObject.name}] ⏹️ 动画已停止");
        }
        
        if (indicatorObject != null)
        {
            indicatorObject.SetActive(false);
            Debug.Log($"[{gameObject.name}] 📍 指示器对象已停用");
        }
    }
    
    /// <summary>
    /// 检查是否有指定名称的动画
    /// </summary>
    private bool HasAnimation(string animName)
    {
        if (indicatorAnimator == null || string.IsNullOrEmpty(animName)) return false;
        return indicatorAnimator.GetAvailableAnimations().Contains(animName);
    }
    
    /// <summary>
    /// 检查是否正在显示
    /// </summary>
    public bool IsVisible 
    { 
        get { return isVisible; }
    }
    
    /// <summary>
    /// 设置事件指示器动画
    /// </summary>
    public void SetEventIndicatorAnimation(SpriteSheetAnimation animation)
    {
        eventIndicatorAnimation = animation;
        if (animation != null)
        {
            animation.animationName = eventIndicatorAnimationName;
            
            // 如果指示器已经设置，立即添加动画
            if (indicatorAnimator != null)
            {
                AddEventIndicatorAnimation();
            }
        }
    }
    
    /// <summary>
    /// 设置指示器位置偏移
    /// </summary>
    public void SetOffset(Vector3 offset)
    {
        offsetFromNPC = offset;
        if (indicatorObject != null)
        {
            indicatorObject.transform.localPosition = offset;
        }
    }
    
    /// <summary>
    /// 设置指示器大小
    /// </summary>
    public void SetSize(float size)
    {
        indicatorSize = size;
        if (indicatorObject != null)
        {
            indicatorObject.transform.localScale = Vector3.one * size;
        }
    }
    
    /// <summary>
    /// 强制检查动画状态（调试用）
    /// </summary>
    [ContextMenu("检查动画状态")]
    public void CheckAnimationStatus()
    {
        Debug.Log($"[{gameObject.name}] 📋 动画状态检查:");
        Debug.Log($"[{gameObject.name}] - 指示器可见: {isVisible}");
        Debug.Log($"[{gameObject.name}] - 动画器存在: {indicatorAnimator != null}");
        
        if (indicatorAnimator != null)
        {
            Debug.Log($"[{gameObject.name}] - 动画数量: {indicatorAnimator.animations.Count}");
            Debug.Log($"[{gameObject.name}] - 正在播放: {indicatorAnimator.IsPlaying()}");
            Debug.Log($"[{gameObject.name}] - 当前动画: '{indicatorAnimator.GetCurrentAnimationName()}'");
            
            var availableAnims = indicatorAnimator.GetAvailableAnimations();
            Debug.Log($"[{gameObject.name}] - 可用动画: [{string.Join(", ", availableAnims)}]");
        }
        
        if (indicatorRenderer != null && indicatorRenderer.material != null)
        {
            var mat = indicatorRenderer.material;
            Debug.Log($"[{gameObject.name}] - 材质: {mat.name}");
            Debug.Log($"[{gameObject.name}] - Shader: {mat.shader.name}");
            Debug.Log($"[{gameObject.name}] - 主贴图: {mat.mainTexture?.name}");
            Debug.Log($"[{gameObject.name}] - UV偏移: {mat.mainTextureOffset}");
            Debug.Log($"[{gameObject.name}] - UV缩放: {mat.mainTextureScale}");
        }
        
        if (eventIndicatorAnimation != null)
        {
            Debug.Log($"[{gameObject.name}] - 配置动画: '{eventIndicatorAnimation.animationName}'");
            Debug.Log($"[{gameObject.name}] - 配置帧数: {eventIndicatorAnimation.frameCount}");
            Debug.Log($"[{gameObject.name}] - 配置帧率: {eventIndicatorAnimation.frameRate}");
            Debug.Log($"[{gameObject.name}] - 配置贴图: {eventIndicatorAnimation.spriteSheet?.name}");
        }
    }
} 