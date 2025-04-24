using UnityEngine;
using TMPro;
using System.Collections;

public class WorldSpaceUIElement : MonoBehaviour
{
    [Header("位置设置")]
    public Vector3 offset = new Vector3(0, 2.0f, 0.5f); // 上方偏移

    [Header("外观设置")]
    public float fadeInTime = 0.3f;
    public float fadeOutTime = 0.3f;

    private Canvas canvas;
    private RectTransform rectTransform;
    private TextMeshProUGUI textComponent;
    private CanvasGroup canvasGroup;
    private Transform npcTransform;

    private Coroutine fadeCoroutine;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        rectTransform = GetComponent<RectTransform>();
        textComponent = GetComponentInChildren<TextMeshProUGUI>();
        canvasGroup = GetComponent<CanvasGroup>();

        // 获取父对象(NPC)的Transform
        npcTransform = transform.parent;

        // 设置Canvas
        if (canvas)
        {
            canvas.worldCamera = Camera.main;

            // 确保为World Space模式
            canvas.renderMode = RenderMode.WorldSpace;
        }

        // 设置位置
        if (rectTransform)
        {
            // 设置适当的大小
            rectTransform.sizeDelta = new Vector2(3, 1.5f); // 根据需要调整大小

            // 设置本地位置
            rectTransform.localPosition = offset;
        }

        // 初始隐藏
        if (canvasGroup)
        {
            canvasGroup.alpha = 0;
        }
    }

    private void LateUpdate()
    {
        // 始终面向摄像机
        if (Camera.main != null)
        {
            transform.rotation = Camera.main.transform.rotation;
        }
    }

    // 显示文本
    public void ShowText(string text, float duration)
    {
        if (textComponent)
        {
            textComponent.text = text;
        }

        // 取消之前的渐变
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        // 开始新的渐变
        fadeCoroutine = StartCoroutine(FadeRoutine(0, 1, fadeInTime, duration));
    }

    // 隐藏
    public void Hide()
    {
        // 取消之前的渐变
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        // 开始淡出
        fadeCoroutine = StartCoroutine(FadeRoutine(1, 0, fadeOutTime, 0));
    }

    // 淡入淡出协程
    private IEnumerator FadeRoutine(float startAlpha, float targetAlpha, float fadeTime, float holdDuration)
    {
        if (canvasGroup == null) yield break;

        // 设置初始透明度
        canvasGroup.alpha = startAlpha;

        // 渐变到目标透明度
        float elapsed = 0;
        while (elapsed < fadeTime)
        {
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 设置最终透明度
        canvasGroup.alpha = targetAlpha;

        // 如果是显示，等待指定时间后自动淡出
        if (targetAlpha > 0 && holdDuration > 0)
        {
            yield return new WaitForSeconds(holdDuration);
            fadeCoroutine = StartCoroutine(FadeRoutine(1, 0, fadeOutTime, 0));
        }
    }

    // 用于在编辑器中可视化位置
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 position = transform.position;
        Gizmos.DrawWireSphere(position, 0.1f);

        // 如果有父对象，画一条线连接到父对象
        if (transform.parent != null)
        {
            Gizmos.DrawLine(position, transform.parent.position);
        }
    }
}