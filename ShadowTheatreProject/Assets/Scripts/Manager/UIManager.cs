using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("UI 面板")]
    public GameObject startGamePanel;     // 开始游戏面板
    public GameObject pauseMenuPanel;     // 暂停面板
    public GameObject hudPanel;           // 游戏中的HUD面板

    [Header("暂停菜单按钮")]
    public Button continueButton;         // 继续按钮
    public Button pauseQuitButton;        // 暂停界面的退出按钮

    [Header("文本")]
    public TextMeshProUGUI gameTimerText; // 游戏时间文本
    public TextMeshProUGUI inputMethodText; // 显示当前选择的输入方式（HUD上）

    [Header("设置")]
    public float startGameFadeDuration = 3f; // 开始游戏提示的淡出时间

    [Header("提示UI")]
    public GameObject promptPanel;        // 提示面板
    public TextMeshProUGUI promptText;    // 提示文本

    [Header("NPC状态UI")]
    public NPCStatusUIManager npcStatusUIManager; // NPC状态UI管理器

    // 内部状态
    private bool isGameActive = false;    // 游戏是否活跃
    private bool isPaused = false;        // 游戏是否暂停
    private float gameTimer = 0f;         // 游戏计时器

    // 输入管理器引用
    private InputManager inputManager;

    private void Awake()
    {
        // 确保所有UI面板初始状态正确
        if (startGamePanel) startGamePanel.SetActive(false);
        if (pauseMenuPanel) pauseMenuPanel.SetActive(false);
        if (hudPanel) hudPanel.SetActive(true); // 直接显示HUD

        // 查找InputManager
        inputManager = InputManager.Instance;
        
        // 自动查找NPC状态UI管理器
        if (npcStatusUIManager == null)
        {
            npcStatusUIManager = FindObjectOfType<NPCStatusUIManager>();
        }
    }

    private void Start()
    {
        // 设置按钮监听器 - 删除主菜单相关按钮
        if (continueButton) continueButton.onClick.AddListener(OnContinueClicked);
        if (pauseQuitButton) pauseQuitButton.onClick.AddListener(OnQuitClicked);

        // 订阅全局事件
        EventCenter.Instance.Subscribe(GameState.EventNames.STATE_ENTERED + GameState.State.Act1, OnGameFirstActStarted);
        EventCenter.Instance.Subscribe(GameState.EventNames.STATE_ENTERED + GameState.State.GamePaused, OnGamePaused);
        
        // 游戏直接开始，设置为活跃状态
        isGameActive = true;
        gameTimer = 0f;
    }

    private void Update()
    {
        // 只有当游戏处于活跃状态时才更新游戏时间
        if (isGameActive)
        {
            gameTimer += Time.deltaTime;
            UpdateGameTimerDisplay();

            // 检测ESC键
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TogglePauseMenu();
            }
        }
    }

    // 更新游戏时间显示
    private void UpdateGameTimerDisplay()
    {
        if (gameTimerText)
        {
            int minutes = Mathf.FloorToInt(gameTimer / 60);
            int seconds = Mathf.FloorToInt(gameTimer % 60);
            gameTimerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }

    // 切换暂停菜单
    private void TogglePauseMenu()
    {
        isPaused = !isPaused;

        if (isPaused)
        {
            // 显示暂停菜单
            if (pauseMenuPanel) pauseMenuPanel.SetActive(true);

            // 游戏暂停逻辑
            Time.timeScale = 0f;
        }
        else
        {
            // 隐藏暂停菜单
            if (pauseMenuPanel) pauseMenuPanel.SetActive(false);

            // 游戏恢复逻辑
            Time.timeScale = 1f;
        }
    }

    // 点击继续按钮
    private void OnContinueClicked()
    {
        TogglePauseMenu();
    }

    // 点击退出按钮
    private void OnQuitClicked()
    {
        QuitGame();
    }

    // 退出游戏
    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // 游戏第一幕开始的事件处理
    private void OnGameFirstActStarted()
    {
        // Debug.Log("第一幕开始，UI管理器响应！");
        isGameActive = true;
    }

    // 游戏暂停的事件处理
    private void OnGamePaused()
    {
        // Debug.Log("游戏暂停");
        // 如果需要额外的暂停处理逻辑，可以在这里添加
    }

    // 当场景销毁时取消订阅事件
    private void OnDestroy()
    {
        EventCenter.Instance.Unsubscribe(GameState.EventNames.STATE_ENTERED + GameState.State.Act1, OnGameFirstActStarted);
        EventCenter.Instance.Unsubscribe(GameState.EventNames.STATE_ENTERED + GameState.State.GamePaused, OnGamePaused);

        // 确保退出时恢复时间缩放
        Time.timeScale = 1f;
    }

    // 公共方法：重置游戏时间
    public void ResetGameTimer()
    {
        gameTimer = 0f;
    }

    // 公共方法：获取游戏运行时间
    public float GetGameTime()
    {
        return gameTimer;
    }

    // 公共方法：直接显示暂停菜单
    public void ShowPauseMenu()
    {
        if (!isPaused)
        {
            TogglePauseMenu();
        }
    }

    // 公共方法：隐藏暂停菜单
    public void HidePauseMenu()
    {
        if (isPaused)
        {
            TogglePauseMenu();
        }
    }

    /// <summary>
    /// 显示交互提示
    /// </summary>
    /// <param name="message">提示文本</param>
    public void ShowPrompt(string message)
    {
        if (promptPanel != null)
        {
            promptPanel.SetActive(true);

            if (promptText != null)
            {
                promptText.text = message;
            }
        }
        else
        {
            Debug.LogWarning("未设置提示面板 (promptPanel)，无法显示提示");
        }
    }

    /// <summary>
    /// 隐藏交互提示
    /// </summary>
    public void HidePrompt()
    {
        if (promptPanel != null)
        {
            promptPanel.SetActive(false);
        }
    }
    
    /// <summary>
    /// 设置NPC状态图标的自定义颜色（供外部调用的接口）
    /// </summary>
    /// <param name="npcId">NPC ID</param>
    /// <param name="normalColor">正常状态颜色</param>
    /// <param name="processingColor">正在处理事件时的颜色</param>
    public void SetNPCStatusColors(string npcId, Color normalColor, Color processingColor)
    {
        if (npcStatusUIManager != null)
        {
            npcStatusUIManager.SetCustomColorScheme(npcId, normalColor, processingColor);
        }
    }
    
    /// <summary>
    /// 获取NPC状态UI管理器（供外部访问）
    /// </summary>
    public NPCStatusUIManager GetNPCStatusUIManager()
    {
        return npcStatusUIManager;
    }
}