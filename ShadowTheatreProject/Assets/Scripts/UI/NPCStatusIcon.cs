using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

/// <summary>
/// NPC状态图标：单个NPC的状态显示组件
/// </summary>
public class NPCStatusIcon : MonoBehaviour
{
    [Header("UI组件")]
    [SerializeField] private Image iconImage;           // NPC图标
    [SerializeField] private Image backgroundImage;     // 背景图像
    [SerializeField] private GameObject eventIndicator; // 事件指示器
    
    [Header("动画组件")]
    [SerializeField] private Animator iconAnimator;     // 图标动画器
    
    [Header("默认设置")]
    [SerializeField] private Sprite defaultIcon;        // 默认图标
    [SerializeField] private Color defaultBackgroundColor = Color.white;
    
    // 状态数据
    private NPCStatusData currentStatusData;
    private float colorTransitionDuration = 0.3f;
    private float processingEventScale = 1.2f;
    
    // 动画相关
    private Coroutine colorTransitionCoroutine;
    private Coroutine scaleAnimationCoroutine;
    private Vector3 originalScale;
    
    private void Awake()
    {
        // 自动查找组件
        if (iconImage == null)
            iconImage = GetComponentInChildren<Image>();
            
        if (iconAnimator == null)
            iconAnimator = GetComponent<Animator>();
            
        // 记录原始缩放
        originalScale = transform.localScale;
        
        // 确保RectTransform的pivot设置为中心，避免缩放时位置偏移
        RectTransform rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }
    }
    
    /// <summary>
    /// 初始化状态图标
    /// </summary>
    public void Initialize(NPCStatusData statusData, float transitionDuration = 0.3f, float eventScale = 1.2f)
    {
        // 创建状态数据的副本，避免引用问题
        currentStatusData = new NPCStatusData(statusData);
        
        colorTransitionDuration = transitionDuration;
        processingEventScale = eventScale;
        
        // 设置基本信息
        SetIcon(statusData.npcIcon);
        UpdateVisualState(currentStatusData, false); // 初始化时不使用动画
        
        Debug.Log($"[NPCStatusIcon] 初始化完成: {currentStatusData.npcId}, 处理事件: {currentStatusData.isProcessingEvent}");
    }
    
    /// <summary>
    /// 更新状态数据
    /// </summary>
    public void UpdateStatus(NPCStatusData newStatusData)
    {
        Debug.Log($"[NPCStatusIcon] 开始更新状态: {newStatusData.npcId}, 处理事件: {newStatusData.isProcessingEvent}");
        
        if (currentStatusData == null)
        {
            Debug.Log($"[NPCStatusIcon] 首次初始化状态: {newStatusData.npcId}");
            Initialize(newStatusData);
            return;
        }
        
        // 检查是否有变化
        bool hasChanges = HasStatusChanged(currentStatusData, newStatusData);
        
        Debug.Log($"[NPCStatusIcon] 状态是否变化: {hasChanges}, 旧状态: {currentStatusData.isProcessingEvent}, 新状态: {newStatusData.isProcessingEvent}");
        
        if (hasChanges)
        {
            NPCStatusData oldStatusData = new NPCStatusData(currentStatusData);
            
            // 更新当前状态数据（创建副本）
            currentStatusData.isProcessingEvent = newStatusData.isProcessingEvent;
            currentStatusData.npcIcon = newStatusData.npcIcon;
            currentStatusData.normalColor = newStatusData.normalColor;
            currentStatusData.processingEventColor = newStatusData.processingEventColor;
            
            // 更新UI元素
            UpdateVisualState(currentStatusData, true); // 使用动画
            
            // 如果事件状态发生变化，触发特殊动画
            if (oldStatusData.isProcessingEvent != currentStatusData.isProcessingEvent)
            {
                Debug.Log($"[NPCStatusIcon] 触发事件状态动画: {currentStatusData.isProcessingEvent}");
                TriggerEventStateAnimation(currentStatusData.isProcessingEvent);
            }
        }
        else
        {
            Debug.Log($"[NPCStatusIcon] 状态无变化，跳过更新");
        }
    }
    
    /// <summary>
    /// 检查状态是否发生变化
    /// </summary>
    private bool HasStatusChanged(NPCStatusData oldData, NPCStatusData newData)
    {
        return oldData.isProcessingEvent != newData.isProcessingEvent;
    }
    
    /// <summary>
    /// 设置图标
    /// </summary>
    private void SetIcon(Sprite icon)
    {
        if (iconImage != null)
        {
            iconImage.sprite = icon != null ? icon : defaultIcon;
        }
    }
    
    /// <summary>
    /// 更新视觉状态
    /// </summary>
    private void UpdateVisualState(NPCStatusData statusData, bool useAnimation = true)
    {
        Color targetColor = statusData.GetCurrentColor();
        
        // 更新背景颜色
        if (useAnimation)
        {
            StartColorTransition(targetColor);
        }
        else
        {
            SetBackgroundColor(targetColor);
        }
        
        // 更新事件指示器
        if (eventIndicator != null)
        {
            eventIndicator.SetActive(statusData.isProcessingEvent);
        }
        
        // 如果有动画器，触发相应的动画状态
        if (iconAnimator != null)
        {
            if (statusData.isProcessingEvent)
            {
                iconAnimator.SetTrigger("ProcessingEvent");
            }
            else
            {
                iconAnimator.SetTrigger("Normal");
            }
        }
    }
    
    /// <summary>
    /// 开始颜色过渡动画
    /// </summary>
    private void StartColorTransition(Color targetColor)
    {
        if (colorTransitionCoroutine != null)
        {
            StopCoroutine(colorTransitionCoroutine);
        }
        
        colorTransitionCoroutine = StartCoroutine(ColorTransitionCoroutine(targetColor));
    }
    
    /// <summary>
    /// 颜色过渡协程
    /// </summary>
    private IEnumerator ColorTransitionCoroutine(Color targetColor)
    {
        Color startColor = GetBackgroundColor();
        float elapsedTime = 0f;
        
        while (elapsedTime < colorTransitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / colorTransitionDuration;
            
            Color currentColor = Color.Lerp(startColor, targetColor, t);
            SetBackgroundColor(currentColor);
            
            yield return null;
        }
        
        SetBackgroundColor(targetColor);
        colorTransitionCoroutine = null;
    }
    
    /// <summary>
    /// 设置背景颜色
    /// </summary>
    private void SetBackgroundColor(Color color)
    {
        if (backgroundImage != null)
        {
            backgroundImage.color = color;
        }
        else if (iconImage != null)
        {
            iconImage.color = color;
        }
    }
    
    /// <summary>
    /// 获取当前背景颜色
    /// </summary>
    private Color GetBackgroundColor()
    {
        if (backgroundImage != null)
        {
            return backgroundImage.color;
        }
        else if (iconImage != null)
        {
            return iconImage.color;
        }
        return defaultBackgroundColor;
    }
    
    /// <summary>
    /// 触发事件状态动画
    /// </summary>
    private void TriggerEventStateAnimation(bool isProcessingEvent)
    {
        if (scaleAnimationCoroutine != null)
        {
            StopCoroutine(scaleAnimationCoroutine);
        }
        
        scaleAnimationCoroutine = StartCoroutine(ScaleAnimationCoroutine(isProcessingEvent));
    }
    
    /// <summary>
    /// 缩放动画协程
    /// </summary>
    private IEnumerator ScaleAnimationCoroutine(bool scaleUp)
    {
        // 优先缩放图标本身，而不是整个容器
        Transform targetTransform = null;
        Vector3 originalTargetScale = Vector3.one;
        
        if (iconImage != null)
        {
            targetTransform = iconImage.transform;
            originalTargetScale = targetTransform.localScale;
            Debug.Log($"[NPCStatusIcon] 缩放图标对象: {iconImage.name}");
        }
        else
        {
            targetTransform = transform;
            originalTargetScale = originalScale;
            Debug.Log($"[NPCStatusIcon] 缩放容器对象: {transform.name}");
        }
        
        Vector3 targetScale = scaleUp ? originalTargetScale * processingEventScale : originalTargetScale;
        Vector3 startScale = targetTransform.localScale;
        
        Debug.Log($"[NPCStatusIcon] 缩放动画: {startScale} -> {targetScale}");
        
        float animationDuration = 0.2f;
        float elapsedTime = 0f;
        
        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / animationDuration;
            
            // 使用缓动函数让动画更自然
            float easedT = Mathf.SmoothStep(0f, 1f, t);
            targetTransform.localScale = Vector3.Lerp(startScale, targetScale, easedT);
            
            yield return null;
        }
        
        targetTransform.localScale = targetScale;
        Debug.Log($"[NPCStatusIcon] 缩放动画完成: {targetTransform.localScale}");
        scaleAnimationCoroutine = null;
    }
    
    /// <summary>
    /// 设置自定义颜色方案（供外部调用）
    /// </summary>
    public void SetCustomColors(Color normalColor, Color processingColor)
    {
        if (currentStatusData != null)
        {
            currentStatusData.normalColor = normalColor;
            currentStatusData.processingEventColor = processingColor;
            
            // 立即更新视觉状态
            UpdateVisualState(currentStatusData, true);
        }
    }
    
    /// <summary>
    /// 播放点击反馈动画
    /// </summary>
    public void PlayClickFeedback()
    {
        if (iconAnimator != null)
        {
            iconAnimator.SetTrigger("Click");
        }
        else
        {
            // 简单的缩放反馈
            StartCoroutine(ClickFeedbackCoroutine());
        }
    }
    
    /// <summary>
    /// 点击反馈协程
    /// </summary>
    private IEnumerator ClickFeedbackCoroutine()
    {
        // 优先缩放图标本身，而不是整个容器
        Transform targetTransform = iconImage != null ? iconImage.transform : transform;
        Vector3 originalScale = targetTransform.localScale;
        Vector3 scaledDown = originalScale * 0.9f;
        
        // 缩小
        float duration = 0.1f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            targetTransform.localScale = Vector3.Lerp(originalScale, scaledDown, t);
            yield return null;
        }
        
        // 恢复
        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            targetTransform.localScale = Vector3.Lerp(scaledDown, originalScale, t);
            yield return null;
        }
        
        targetTransform.localScale = originalScale;
    }
    
    /// <summary>
    /// 获取当前状态数据
    /// </summary>
    public NPCStatusData GetStatusData()
    {
        return currentStatusData;
    }
} 