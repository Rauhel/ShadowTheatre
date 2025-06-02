using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 手势调试UI：用于在游戏运行时查看手势接收器状态和切换模式
/// </summary>
public class GestureDebugUI : MonoBehaviour
{
    [Header("UI引用")]
    [SerializeField] private Text statusText;
    [SerializeField] private Text gestureTypeText;
    [SerializeField] private Text handPositionText;
    [SerializeField] private Button debugModeButton;
    [SerializeField] private Button releaseModeButton;
    [SerializeField] private Button instructionsButton;
    
    [Header("设置")]
    [SerializeField] private bool showUI = true;
    [SerializeField] private float updateInterval = 0.5f;
    
    private GestureReceiver gestureReceiver;
    private InputManager inputManager;
    private float lastUpdateTime;
    
    private void Start()
    {
        // 查找组件
        gestureReceiver = FindObjectOfType<GestureReceiver>();
        inputManager = InputManager.Instance;
        
        // 设置UI可见性
        gameObject.SetActive(showUI);
        
        // 设置按钮事件
        if (debugModeButton != null)
        {
            debugModeButton.onClick.AddListener(() => {
                if (gestureReceiver != null)
                {
                    gestureReceiver.SwitchToDebugMode();
                }
            });
        }
        
        if (releaseModeButton != null)
        {
            releaseModeButton.onClick.AddListener(() => {
                if (gestureReceiver != null)
                {
                    gestureReceiver.SwitchToReleaseMode();
                }
            });
        }
        
        if (instructionsButton != null)
        {
            instructionsButton.onClick.AddListener(() => {
                if (gestureReceiver != null)
                {
                    gestureReceiver.PrintDebugInstructions();
                }
            });
        }
    }
    
    private void Update()
    {
        if (!showUI || Time.time - lastUpdateTime < updateInterval)
            return;
            
        UpdateUI();
        lastUpdateTime = Time.time;
    }
    
    private void UpdateUI()
    {
        // 更新状态信息
        if (statusText != null && gestureReceiver != null)
        {
            statusText.text = gestureReceiver.GetConnectionStatus();
        }
        
        // 更新手势类型
        if (gestureTypeText != null && inputManager != null)
        {
            string gestureType = inputManager.GetCurrentGestureType();
            gestureTypeText.text = $"当前手势: {gestureType}";
        }
        
        // 更新手部位置
        if (handPositionText != null && inputManager != null)
        {
            Vector2 position = inputManager.GetNormalizedHandPosition();
            bool handDetected = inputManager.IsHandDetected();
            handPositionText.text = $"手部位置: ({position.x:F2}, {position.y:F2})\n检测状态: {(handDetected ? "检测到" : "未检测到")}";
        }
        
        // 更新按钮状态
        if (debugModeButton != null && releaseModeButton != null && gestureReceiver != null)
        {
            bool isDebugMode = gestureReceiver.IsDebugMode();
            debugModeButton.interactable = !isDebugMode;
            releaseModeButton.interactable = isDebugMode;
        }
    }
    
    // 切换UI显示
    [ContextMenu("切换UI显示")]
    public void ToggleUI()
    {
        showUI = !showUI;
        gameObject.SetActive(showUI);
    }
    
    // 设置UI显示
    public void SetUIVisible(bool visible)
    {
        showUI = visible;
        gameObject.SetActive(showUI);
    }
} 