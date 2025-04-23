using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 管理手势提示指示器的显示
/// </summary>
public class GestureIndicatorManager : MonoBehaviour
{
    [Header("UI设置")]
    [Tooltip("手势指示器的父对象")]
    public RectTransform indicatorContainer;

    [Tooltip("手势指示器预制体")]
    public GameObject gestureIndicatorPrefab;

    [Tooltip("指示器大小")]
    public float indicatorSize = 80f;

    [Tooltip("指示器之间的间距")]
    public float indicatorSpacing = 10f;

    [Header("位置设置")]
    [Tooltip("指示器相对NPC的Y轴偏移")]
    public float verticalOffset = 2.0f;

    [Tooltip("指示器与NPC的最小水平距离")]
    public float minHorizontalDistance = 1.0f;

    [Header("动画设置")]
    [Tooltip("指示器淡入时间")]
    public float fadeInTime = 0.3f;

    [Tooltip("指示器淡出时间")]
    public float fadeOutTime = 0.5f;

    [Header("手势图标")]
    [Tooltip("手势类型到图标的映射")]
    public List<GestureIconMapping> gestureIcons = new List<GestureIconMapping>();

    // 当前活跃的指示器
    private List<GameObject> activeIndicators = new List<GameObject>();
    private Transform currentNPC;
    private Transform playerTransform;
    private Camera mainCamera;

    // 单例实例
    private static GestureIndicatorManager _instance;
    public static GestureIndicatorManager Instance => _instance;

    private void Awake()
    {
        // 单例模式
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        mainCamera = Camera.main;
        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;

        // 创建指示器容器（如果不存在）
        if (indicatorContainer == null)
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("GestureIndicatorCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            GameObject containerObj = new GameObject("GestureIndicatorContainer");
            containerObj.transform.SetParent(canvas.transform, false);
            indicatorContainer = containerObj.AddComponent<RectTransform>();
            indicatorContainer.anchorMin = Vector2.zero;
            indicatorContainer.anchorMax = Vector2.one;
            indicatorContainer.offsetMin = Vector2.zero;
            indicatorContainer.offsetMax = Vector2.zero;
        }

        // 创建指示器预制体（如果不存在）
        if (gestureIndicatorPrefab == null)
        {
            CreateDefaultIndicatorPrefab();
        }
    }

    /// <summary>
    /// 显示NPC的手势提示
    /// </summary>
    /// <param name="npc">NPC Transform</param>
    /// <param name="gestureTypes">可用的手势类型列表</param>
    public void ShowGestureIndicators(Transform npc, List<string> gestureTypes)
    {
        if (npc == null || gestureTypes == null || gestureTypes.Count == 0)
            return;

        // 清除现有指示器
        ClearIndicators();

        currentNPC = npc;

        // 计算指示器位置
        Vector3 basePosition = CalculateIndicatorBasePosition(npc);
        Vector3 screenPos = mainCamera.WorldToScreenPoint(basePosition);

        // 计算指示器总宽度
        float totalWidth = gestureTypes.Count * indicatorSize + (gestureTypes.Count - 1) * indicatorSpacing;
        float startX = screenPos.x - totalWidth / 2;

        // 创建每个手势的指示器
        for (int i = 0; i < gestureTypes.Count; i++)
        {
            string gestureType = gestureTypes[i];
            Sprite iconSprite = GetGestureIcon(gestureType);

            // 创建指示器
            GameObject indicator = Instantiate(gestureIndicatorPrefab, indicatorContainer);
            activeIndicators.Add(indicator);

            // 设置位置
            RectTransform rt = indicator.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(
                startX + i * (indicatorSize + indicatorSpacing) + indicatorSize / 2,
                screenPos.y);

            // 设置大小
            rt.sizeDelta = new Vector2(indicatorSize, indicatorSize);

            // 设置图标
            Image iconImage = indicator.GetComponent<Image>();
            if (iconImage != null && iconSprite != null)
            {
                iconImage.sprite = iconSprite;
                iconImage.preserveAspect = true;
            }

            // 设置标签（如果需要）
            Text labelText = indicator.GetComponentInChildren<Text>();
            if (labelText != null)
            {
                labelText.text = gestureType;
            }

            // 淡入动画
            CanvasGroup canvasGroup = indicator.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0;
                StartCoroutine(FadeCanvasGroup(canvasGroup, 0, 1, fadeInTime));
            }
        }
    }

    /// <summary>
    /// 隐藏所有手势提示
    /// </summary>
    public void HideGestureIndicators()
    {
        foreach (var indicator in activeIndicators)
        {
            CanvasGroup canvasGroup = indicator.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                StartCoroutine(FadeCanvasGroup(canvasGroup, 1, 0, fadeOutTime, () => Destroy(indicator)));
            }
            else
            {
                Destroy(indicator);
            }
        }
        activeIndicators.Clear();
        currentNPC = null;
    }

    /// <summary>
    /// 更新指示器位置
    /// </summary>
    private void Update()
    {
        if (currentNPC != null && activeIndicators.Count > 0)
        {
            // 计算新位置
            Vector3 basePosition = CalculateIndicatorBasePosition(currentNPC);
            Vector3 screenPos = mainCamera.WorldToScreenPoint(basePosition);

            // 计算指示器总宽度
            float totalWidth = activeIndicators.Count * indicatorSize + (activeIndicators.Count - 1) * indicatorSpacing;
            float startX = screenPos.x - totalWidth / 2;

            // 更新每个指示器的位置
            for (int i = 0; i < activeIndicators.Count; i++)
            {
                RectTransform rt = activeIndicators[i].GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(
                    startX + i * (indicatorSize + indicatorSpacing) + indicatorSize / 2,
                    screenPos.y);
            }
        }
    }

    /// <summary>
    /// 计算指示器基础位置
    /// </summary>
    private Vector3 CalculateIndicatorBasePosition(Transform npc)
    {
        // 基础位置 - NPC头顶上方
        Vector3 basePosition = npc.position + Vector3.up * verticalOffset;

        if (playerTransform != null)
        {
            // 计算NPC到玩家的向量（水平面上）
            Vector3 toPlayer = playerTransform.position - npc.position;
            toPlayer.y = 0;

            // 如果距离太近，沿着这个方向放置指示器
            if (toPlayer.magnitude < minHorizontalDistance && toPlayer.magnitude > 0.1f)
            {
                Vector3 direction = toPlayer.normalized;
                basePosition += direction * minHorizontalDistance;
            }
        }

        return basePosition;
    }

    /// <summary>
    /// 淡入淡出Canvas Group
    /// </summary>
    private System.Collections.IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, float from, float to, float duration, System.Action onComplete = null)
    {
        float elapsed = 0;
        while (elapsed < duration)
        {
            canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        canvasGroup.alpha = to;
        onComplete?.Invoke();
    }

    /// <summary>
    /// 清除所有当前活跃的指示器
    /// </summary>
    private void ClearIndicators()
    {
        foreach (var indicator in activeIndicators)
        {
            Destroy(indicator);
        }
        activeIndicators.Clear();
    }

    /// <summary>
    /// 获取指定手势类型的图标
    /// </summary>
    private Sprite GetGestureIcon(string gestureType)
    {
        foreach (var mapping in gestureIcons)
        {
            if (mapping.gestureType == gestureType)
            {
                return mapping.icon;
            }
        }
        return null;
    }

    /// <summary>
    /// 创建默认的指示器预制体
    /// </summary>
    private void CreateDefaultIndicatorPrefab()
    {
        gestureIndicatorPrefab = new GameObject("GestureIndicator");

        // 添加图像组件
        Image iconImage = gestureIndicatorPrefab.AddComponent<Image>();
        iconImage.color = new Color(1, 1, 1, 0.9f);

        // 添加Canvas Group用于淡入淡出
        CanvasGroup canvasGroup = gestureIndicatorPrefab.AddComponent<CanvasGroup>();

        // 添加文本标签（可选）
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(gestureIndicatorPrefab.transform, false);
        Text label = labelObj.AddComponent<Text>();
        label.alignment = TextAnchor.LowerCenter;
        label.fontSize = 14;
        label.color = Color.white;
        label.text = "手势";

        RectTransform labelRT = label.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0, 0);
        labelRT.anchorMax = new Vector2(1, 0.3f);
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;

        // 使预制体不显示在场景中
        gestureIndicatorPrefab.SetActive(false);
    }
}

/// <summary>
/// 手势类型到图标的映射
/// </summary>
[System.Serializable]
public class GestureIconMapping
{
    public string gestureType;
    public Sprite icon;
}