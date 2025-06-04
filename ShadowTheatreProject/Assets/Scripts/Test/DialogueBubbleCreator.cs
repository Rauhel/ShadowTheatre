using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 对话气泡创建器
/// 用于创建正确配置的对话气泡预制体
/// </summary>
public class DialogueBubbleCreator : MonoBehaviour
{
    [ContextMenu("创建对话气泡预制体")]
    public void CreateDialogueBubblePrefab()
    {
        // 1. 创建根Canvas对象
        GameObject dialogueBubble = new GameObject("DialogueBubble");
        
        // 2. 添加Canvas组件
        Canvas canvas = dialogueBubble.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100; // 确保在其他UI之上
        
        // 3. 添加CanvasScaler
        CanvasScaler scaler = dialogueBubble.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        
        // 4. 添加GraphicRaycaster
        GraphicRaycaster raycaster = dialogueBubble.AddComponent<GraphicRaycaster>();
        
        // 5. 添加CanvasGroup用于透明度控制
        CanvasGroup canvasGroup = dialogueBubble.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        
        // 6. 设置Canvas的Transform
        RectTransform canvasRect = dialogueBubble.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(300, 100);
        
        // 设置合适的缩放（世界空间Canvas需要较小的缩放）
        dialogueBubble.transform.localScale = Vector3.one * 0.01f;
        
        // 7. 创建背景
        GameObject background = new GameObject("Background");
        background.transform.SetParent(dialogueBubble.transform, false);
        
        Image bgImage = background.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.8f); // 半透明黑色背景
        bgImage.raycastTarget = false;
        
        RectTransform bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgRect.anchoredPosition = Vector2.zero;
        
        // 8. 创建文字对象
        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(background.transform, false);
        
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = "对话文本";
        text.fontSize = 24;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Normal;
        text.autoSizeTextContainer = true;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        
        // 设置文字的RectTransform
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = new Vector2(-20, -20); // 留出边距
        textRect.anchoredPosition = Vector2.zero;
        
        Debug.Log("对话气泡预制体创建完成！");
        Debug.Log("使用方法：");
        Debug.Log("1. 将这个对话气泡拖拽到Project窗口创建预制体");
        Debug.Log("2. 在NPCActionExecutor的dialoguePrefab字段中引用这个预制体");
        Debug.Log("3. 确保主相机的Culling Mask包含UI层");
        
        // 显示配置建议
        ShowConfigurationTips();
    }
    
    private void ShowConfigurationTips()
    {
        string tips = @"
对话气泡配置建议：

1. Canvas设置：
   - Render Mode: World Space
   - Sort Order: 100+
   - World Camera: 设置为主相机

2. 缩放建议：
   - Transform Scale: (0.01, 0.01, 0.01)
   - 这样可以让300x100的UI在世界空间中显示为合适大小

3. 字体设置：
   - 使用TextMeshPro
   - 字体大小: 24-36
   - 确保字体资源正确

4. 相机设置：
   - 确保主相机的Culling Mask包含UI层
   - 如果有多个相机，确保UI相机设置正确

5. 层级设置：
   - 对话气泡建议放在UI层
   - Sort Order要高于其他UI

6. 透明度控制：
   - 使用CanvasGroup控制整体透明度
   - NPCActionExecutor会自动控制淡入淡出
        ";
        
        Debug.Log(tips);
    }
    
    [ContextMenu("修复现有对话气泡")]
    public void FixExistingDialogueBubbles()
    {
        // 查找场景中所有可能的对话气泡
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        
        foreach (var canvas in canvases)
        {
            if (canvas.name.Contains("Dialogue") || canvas.name.Contains("Dialog"))
            {
                Debug.Log($"修复对话Canvas: {canvas.name}");
                
                // 确保渲染模式正确
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 100;
                
                // 确保有世界相机引用
                if (canvas.worldCamera == null)
                {
                    canvas.worldCamera = Camera.main;
                }
                
                // 确保缩放合适
                if (canvas.transform.localScale.magnitude > 0.1f)
                {
                    canvas.transform.localScale = Vector3.one * 0.01f;
                }
                
                // 确保有CanvasGroup
                CanvasGroup canvasGroup = canvas.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
                }
                canvasGroup.alpha = 1f;
                
                // 修复TextMeshPro设置
                TextMeshProUGUI[] texts = canvas.GetComponentsInChildren<TextMeshProUGUI>();
                foreach (var text in texts)
                {
                    text.color = Color.white;
                    text.fontSize = Mathf.Max(text.fontSize, 24f);
                    text.alignment = TextAlignmentOptions.Center;
                }
            }
        }
        
        Debug.Log("对话气泡修复完成");
    }
} 