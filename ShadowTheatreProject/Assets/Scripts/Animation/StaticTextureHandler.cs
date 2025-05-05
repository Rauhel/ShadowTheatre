using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 用于设置静态贴图的简单组件，支持编辑器中预览和自动调整比例
/// </summary>
public class StaticTextureHandler : MonoBehaviour
{
    [Header("基本设置")]
    public Renderer targetRenderer;
    public Texture2D texture;

    [Header("位置设置")]
    [Tooltip("设置后物体底部会移动到Y=0位置")]
    public bool setBottomToZero = false;

    // Shader属性ID
    private static readonly int MainTexProperty = Shader.PropertyToID("_MainTex");
    private static readonly int FrameCountProperty = Shader.PropertyToID("_FrameCount");
    private static readonly int FrameIndexProperty = Shader.PropertyToID("_FrameIndex");

    private MaterialPropertyBlock propertyBlock;
    private SpriteSheetAnimator animator;

    // 添加公共标志，表示该组件已经调整过比例
    public bool hasAdjustedScale { get; private set; } = false;

    private void Awake()
    {
        InitializeRenderer();
        propertyBlock = new MaterialPropertyBlock();
        animator = GetComponent<SpriteSheetAnimator>();
    }

    private void InitializeRenderer()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<MeshRenderer>();
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }
        }
    }

    private void Start()
    {
        // 在Start中应用贴图
        if (texture != null)
        {
            ApplyTexture();

            // 游戏开始时检查是否需要设置底部到Y=0
            if (setBottomToZero)
            {
                AdjustPivotToBottom();
            }
        }
    }

    private void OnEnable()
    {
        // 组件启用时应用贴图
        if (texture != null)
        {
            ApplyTexture();
        }
    }

    private void Update()
    {
        // 动画停止时恢复此贴图
        if (animator != null && !animator.IsPlaying())
        {
            ApplyTexture();
        }
    }

    /// <summary>
    /// 设置并应用静态贴图
    /// </summary>
    public void SetTexture(Texture2D newTexture)
    {
        texture = newTexture;
        ApplyTexture();

        if (newTexture != null)
        {
            AdjustScale();

            if (setBottomToZero)
            {
                AdjustPivotToBottom();
            }
        }
    }

    /// <summary>
    /// 应用当前设置的贴图
    /// </summary>
    public void ApplyTexture()
    {
        InitializeRenderer();

        if (targetRenderer == null)
        {
            Debug.LogError($"[{gameObject.name}] StaticTextureHandler: targetRenderer为空");
            return;
        }

        if (texture == null)
        {
            return;
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        // 获取当前材质
        Material material = targetRenderer.sharedMaterial;
        if (material != null)
        {
            // 确保材质使用了正确的渲染模式
            if (material.HasProperty("_SurfaceType") || material.HasProperty("_BlendMode"))
            {
                // 如果是URP/HDRP材质，尝试设置相关属性
                if (material.HasProperty("_SurfaceType"))
                    material.SetFloat("_SurfaceType", 1); // 1通常代表透明

                if (material.HasProperty("_BlendMode"))
                    material.SetFloat("_BlendMode", 0); // 0通常代表Alpha Blend
            }
            else
            {
                // 对于标准着色器，直接设置渲染模式
                material.SetFloat("_Mode", 3); // 3 = Transparent
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = 3000;
            }
        }

        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetTexture(MainTexProperty, texture);
        propertyBlock.SetFloat(FrameCountProperty, 1);
        propertyBlock.SetFloat(FrameIndexProperty, 0);
        targetRenderer.SetPropertyBlock(propertyBlock);

        AdjustScale();
    }

    /// <summary>
    /// 调整物体缩放以匹配贴图比例，保持最大尺寸
    /// </summary>
    public void AdjustScale()
    {
        if (texture == null) return;

        // 获取贴图的宽高比
        float textureAspect = (float)texture.width / texture.height;

        // 获取当前缩放
        Vector3 currentScale = transform.localScale;

        // 计算新的缩放，保持最大尺寸
        Vector3 newScale = currentScale;

        if (currentScale.x >= currentScale.y)
        {
            // X更大，保持X
            newScale.y = newScale.x / textureAspect;
        }
        else
        {
            // Y更大，保持Y
            newScale.x = newScale.y * textureAspect;
        }

        // 保持Z轴不变
        newScale.z = currentScale.z;

        // 应用新的缩放
        transform.localScale = newScale;

        // 设置已调整标志
        hasAdjustedScale = true;
    }

    /// <summary>
    /// 获取贴图的宽高比
    /// </summary>
    public float GetTextureAspect()
    {
        if (texture == null) return 1.0f;
        return (float)texture.width / texture.height;
    }

    /// <summary>
    /// 调整物体位置，使底部位于Y=0
    /// </summary>
    public void AdjustPivotToBottom()
    {
        if (targetRenderer == null)
        {
            InitializeRenderer();
            if (targetRenderer == null)
            {
                Debug.LogError($"[{gameObject.name}] 无法调整位置：找不到渲染器");
                return;
            }
        }

        // 获取渲染器边界
        Bounds bounds = targetRenderer.bounds;

        // 计算当前底部y坐标（在世界空间）
        float bottomY = bounds.min.y;

        // 计算需要上移的距离
        float offsetY = -bottomY;

        // 上移物体
        transform.position = new Vector3(
            transform.position.x,
            transform.position.y + offsetY,
            transform.position.z
        );
    }

#if UNITY_EDITOR
    // 编辑器相关代码，让我们可以在编辑模式下看到贴图
    private void OnValidate()
    {
        if (EditorApplication.isPlaying)
            return;

        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            
            // 应用贴图
            ApplyTexture();
            
            // 检查是否需要调整底部位置
            if (setBottomToZero)
            {
                AdjustPivotToBottom();
            }
        };
    }
    
    // 添加右键菜单选项，方便手动调整
    [ContextMenu("设置底部到Y=0")]
    private void SetBottomToZeroMenu()
    {
        AdjustPivotToBottom();
    }
    
    // 添加右键菜单选项，使用默认设置
    [ContextMenu("重新调整贴图比例")]
    private void ReAdjustScaleMenu()
    {
        AdjustScale();
    }
    
    // 在编辑器中添加自定义按钮
    [CustomEditor(typeof(StaticTextureHandler))]
    public class StaticTextureHandlerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            // 绘制默认Inspector
            DrawDefaultInspector();
            
            // 获取目标组件
            StaticTextureHandler handler = (StaticTextureHandler)target;
            
            // 添加一个空行
            EditorGUILayout.Space();
            
            // 添加按钮
            if (GUILayout.Button("设置底部到Y=0"))
            {
                handler.AdjustPivotToBottom();
            }
            
            if (GUILayout.Button("重新调整贴图比例"))
            {
                handler.AdjustScale();
            }
        }
    }
#endif
}