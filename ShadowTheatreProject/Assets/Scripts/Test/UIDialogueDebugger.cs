using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI对话调试器
/// 用于诊断和修复对话文字显示问题
/// </summary>
public class UIDialogueDebugger : MonoBehaviour
{
    [Header("调试设置")]
    public bool enableDebugMode = true;
    public bool autoFix = true;
    
    [Header("检查结果")]
    [SerializeField] private string debugInfo = "";
    
    void Start()
    {
        if (enableDebugMode)
        {
            StartCoroutine(DebugUIAfterFrame());
        }
    }
    
    System.Collections.IEnumerator DebugUIAfterFrame()
    {
        // 等待一帧确保所有UI初始化完成
        yield return new WaitForEndOfFrame();
        
        DebugUIHierarchy();
    }
    
    [ContextMenu("调试UI层次结构")]
    public void DebugUIHierarchy()
    {
        debugInfo = "=== UI 对话调试报告 ===\n";
        
        // 1. 检查Canvas
        CheckCanvasSettings();
        
        // 2. 检查TextMeshPro组件
        CheckTextMeshProComponents();
        
        // 3. 检查当前对象的UI设置
        CheckCurrentObjectUI();
        
        // 4. 检查相机设置
        CheckCameraSettings();
        
        Debug.Log(debugInfo);
        
        if (autoFix)
        {
            AttemptAutoFix();
        }
    }
    
    private void CheckCanvasSettings()
    {
        debugInfo += "\n--- Canvas 检查 ---\n";
        
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        debugInfo += $"场景中Canvas数量: {canvases.Length}\n";
        
        foreach (var canvas in canvases)
        {
            debugInfo += $"Canvas: {canvas.name}\n";
            debugInfo += $"  - Render Mode: {canvas.renderMode}\n";
            debugInfo += $"  - Sort Order: {canvas.sortingOrder}\n";
            debugInfo += $"  - World Camera: {(canvas.worldCamera != null ? canvas.worldCamera.name : "null")}\n";
            
            if (canvas.renderMode == RenderMode.WorldSpace)
            {
                debugInfo += $"  - World Space Position: {canvas.transform.position}\n";
                debugInfo += $"  - World Space Scale: {canvas.transform.localScale}\n";
            }
            
            // 检查Canvas的CanvasGroup
            CanvasGroup canvasGroup = canvas.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                debugInfo += $"  - CanvasGroup Alpha: {canvasGroup.alpha}\n";
                debugInfo += $"  - CanvasGroup Interactable: {canvasGroup.interactable}\n";
                debugInfo += $"  - CanvasGroup BlocksRaycasts: {canvasGroup.blocksRaycasts}\n";
            }
        }
    }
    
    private void CheckTextMeshProComponents()
    {
        debugInfo += "\n--- TextMeshPro 检查 ---\n";
        
        TextMeshProUGUI[] textComponents = FindObjectsOfType<TextMeshProUGUI>();
        debugInfo += $"场景中TextMeshProUGUI数量: {textComponents.Length}\n";
        
        foreach (var text in textComponents)
        {
            debugInfo += $"TextMeshPro: {text.name}\n";
            debugInfo += $"  - Text: '{text.text}'\n";
            debugInfo += $"  - Active: {text.gameObject.activeInHierarchy}\n";
            debugInfo += $"  - Alpha: {text.alpha}\n";
            debugInfo += $"  - Color: {text.color}\n";
            debugInfo += $"  - Font: {(text.font != null ? text.font.name : "null")}\n";
            debugInfo += $"  - Font Size: {text.fontSize}\n";
            debugInfo += $"  - Position: {text.transform.position}\n";
            debugInfo += $"  - Local Position: {text.transform.localPosition}\n";
            debugInfo += $"  - Parent: {(text.transform.parent != null ? text.transform.parent.name : "null")}\n";
            
            // 检查Canvas
            Canvas parentCanvas = text.GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                debugInfo += $"  - Parent Canvas: {parentCanvas.name}\n";
            }
            
            // 检查CanvasGroup
            CanvasGroup textCanvasGroup = text.GetComponent<CanvasGroup>();
            if (textCanvasGroup != null)
            {
                debugInfo += $"  - CanvasGroup Alpha: {textCanvasGroup.alpha}\n";
            }
            
            // 检查RectTransform
            RectTransform rectTransform = text.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                debugInfo += $"  - RectTransform Size: {rectTransform.sizeDelta}\n";
                debugInfo += $"  - RectTransform Anchors: {rectTransform.anchorMin} - {rectTransform.anchorMax}\n";
            }
        }
    }
    
    private void CheckCurrentObjectUI()
    {
        debugInfo += "\n--- 当前对象UI检查 ---\n";
        
        // 检查当前对象及其子对象中的UI组件
        TextMeshProUGUI currentText = GetComponentInChildren<TextMeshProUGUI>();
        if (currentText != null)
        {
            debugInfo += $"当前对象TextMeshPro: {currentText.name}\n";
            debugInfo += $"  - 文字内容: '{currentText.text}'\n";
            debugInfo += $"  - 激活状态: {currentText.gameObject.activeInHierarchy}\n";
            debugInfo += $"  - 世界位置: {currentText.transform.position}\n";
            
            // 检查是否在相机视野内
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                Vector3 screenPoint = mainCamera.WorldToScreenPoint(currentText.transform.position);
                bool inScreen = screenPoint.x >= 0 && screenPoint.x <= Screen.width && 
                               screenPoint.y >= 0 && screenPoint.y <= Screen.height && 
                               screenPoint.z > 0;
                debugInfo += $"  - 在屏幕内: {inScreen} (屏幕坐标: {screenPoint})\n";
            }
        }
        
        // 检查NPCActionExecutor设置
        NPCActionExecutor actionExecutor = GetComponent<NPCActionExecutor>();
        if (actionExecutor != null)
        {
            debugInfo += "NPCActionExecutor配置:\n";
            // 通过反射获取私有字段（仅用于调试）
            var dialoguePrefabField = typeof(NPCActionExecutor).GetField("dialoguePrefab");
            if (dialoguePrefabField != null)
            {
                GameObject dialoguePrefab = dialoguePrefabField.GetValue(actionExecutor) as GameObject;
                debugInfo += $"  - Dialogue Prefab: {(dialoguePrefab != null ? dialoguePrefab.name : "null")}\n";
            }
        }
    }
    
    private void CheckCameraSettings()
    {
        debugInfo += "\n--- 相机检查 ---\n";
        
        Camera[] cameras = FindObjectsOfType<Camera>();
        debugInfo += $"场景中相机数量: {cameras.Length}\n";
        
        foreach (var camera in cameras)
        {
            debugInfo += $"Camera: {camera.name}\n";
            debugInfo += $"  - Culling Mask: {camera.cullingMask}\n";
            debugInfo += $"  - Clear Flags: {camera.clearFlags}\n";
            debugInfo += $"  - Depth: {camera.depth}\n";
            debugInfo += $"  - Field of View: {camera.fieldOfView}\n";
            
            if (camera.name.Contains("UI") || camera == Camera.main)
            {
                debugInfo += $"  - 是否为主相机或UI相机: True\n";
            }
        }
    }
    
    private void AttemptAutoFix()
    {
        debugInfo += "\n--- 自动修复尝试 ---\n";
        
        // 1. 修复Canvas设置
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        foreach (var canvas in canvases)
        {
            if (canvas.renderMode == RenderMode.WorldSpace)
            {
                // 确保世界空间Canvas有正确的相机引用
                if (canvas.worldCamera == null)
                {
                    Camera mainCamera = Camera.main;
                    if (mainCamera != null)
                    {
                        canvas.worldCamera = mainCamera;
                        debugInfo += $"为Canvas {canvas.name} 设置了主相机\n";
                    }
                }
                
                // 检查Canvas的缩放
                if (canvas.transform.localScale.magnitude < 0.001f)
                {
                    canvas.transform.localScale = Vector3.one * 0.01f;
                    debugInfo += $"修复了Canvas {canvas.name} 的缩放\n";
                }
            }
        }
        
        // 2. 修复TextMeshPro设置
        TextMeshProUGUI[] textComponents = FindObjectsOfType<TextMeshProUGUI>();
        foreach (var text in textComponents)
        {
            // 确保文字可见
            if (text.alpha < 0.1f)
            {
                text.alpha = 1f;
                debugInfo += $"修复了 {text.name} 的透明度\n";
            }
            
            // 确保颜色不是透明的
            if (text.color.a < 0.1f)
            {
                Color newColor = text.color;
                newColor.a = 1f;
                text.color = newColor;
                debugInfo += $"修复了 {text.name} 的颜色透明度\n";
            }
            
            // 确保字体大小合适
            if (text.fontSize < 1f)
            {
                text.fontSize = 36f;
                debugInfo += $"修复了 {text.name} 的字体大小\n";
            }
        }
        
        // 3. 修复CanvasGroup设置
        CanvasGroup[] canvasGroups = FindObjectsOfType<CanvasGroup>();
        foreach (var group in canvasGroups)
        {
            if (group.alpha < 0.1f)
            {
                group.alpha = 1f;
                debugInfo += $"修复了CanvasGroup {group.name} 的透明度\n";
            }
        }
        
        debugInfo += "自动修复完成\n";
    }
    
    [ContextMenu("创建测试对话")]
    public void CreateTestDialogue()
    {
        // 创建一个简单的测试对话来验证显示
        GameObject testCanvas = new GameObject("TestCanvas");
        Canvas canvas = testCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        testCanvas.transform.position = transform.position + Vector3.up * 2f;
        testCanvas.transform.localScale = Vector3.one * 0.01f;
        
        // 添加CanvasScaler
        CanvasScaler scaler = testCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        
        // 创建文字
        GameObject textObject = new GameObject("TestText");
        textObject.transform.SetParent(testCanvas.transform);
        
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = "测试文字显示";
        text.fontSize = 36;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        
        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(200, 50);
        rectTransform.anchoredPosition = Vector2.zero;
        
        Debug.Log("创建了测试对话，检查是否能看到文字");
    }
} 