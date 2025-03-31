using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("UI 面板")]
    public GameObject mainMenuPanel;      // 主菜单面板
    public GameObject startGamePanel;     // 开始游戏面板
    public GameObject pauseMenuPanel;     // 暂停面板
    public GameObject hudPanel;           // 游戏中的HUD面板

    [Header("主菜单按钮")]
    public Button mouseControlButton;     // 鼠标控制按钮（同时作为开始游戏按钮）
    public Button gestureControlButton;   // 手势控制按钮（同时作为开始游戏按钮）
    public Button quitButton;             // 退出按钮

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

    // 内部状态
    private bool isGameActive = false;    // 游戏是否活跃
    private bool isPaused = false;        // 游戏是否暂停
    private float gameTimer = 0f;         // 游戏计时器

    // 输入管理器引用
    private InputManager inputManager;

    private void Awake()
    {
        // 确保所有UI面板初始状态正确
        if (mainMenuPanel) mainMenuPanel.SetActive(true);
        if (startGamePanel) startGamePanel.SetActive(false);
        if (pauseMenuPanel) pauseMenuPanel.SetActive(false);
        if (hudPanel) hudPanel.SetActive(false);

        // 查找InputManager
        inputManager = InputManager.Instance;
    }

    private void Start()
    {
        // 设置按钮监听器
        if (mouseControlButton) mouseControlButton.onClick.AddListener(OnMouseControlClicked);
        if (gestureControlButton) gestureControlButton.onClick.AddListener(OnGestureControlClicked);
        if (continueButton) continueButton.onClick.AddListener(OnContinueClicked);
        if (quitButton) quitButton.onClick.AddListener(OnQuitClicked);
        if (pauseQuitButton) pauseQuitButton.onClick.AddListener(OnQuitClicked); // 暂停界面的退出按钮

        // 订阅全局事件
        EventCenter.Instance.Subscribe(GameState.EventNames.STATE_ENTERED + GameState.State.Act1, OnGameFirstActStarted);
        EventCenter.Instance.Subscribe(GameState.EventNames.STATE_ENTERED + GameState.State.GamePaused, OnGamePaused);
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

    // 点击鼠标控制按钮 - 设置鼠标控制并开始游戏
    private void OnMouseControlClicked()
    {
        // 启用鼠标控制
        EnableMouseControl();

        // 开始游戏
        StartGame();
    }

    // 点击手势控制按钮 - 设置手势控制并开始游戏
    private void OnGestureControlClicked()
    {
        // 启用手势控制
        EnableGestureControl();

        // 开始游戏
        StartGame();
    }

    // 开始游戏流程
    private void StartGame()
    {
        if (mainMenuPanel) mainMenuPanel.SetActive(false);
        if (startGamePanel) startGamePanel.SetActive(true);
        if (hudPanel) hudPanel.SetActive(true);

        // 开始游戏淡入淡出效果
        StartCoroutine(StartGameSequence());
    }

    // 开始游戏序列
    private IEnumerator StartGameSequence()
    {
        if (startGamePanel != null)
        {
            // 获取开始游戏面板上的文本组件
            TextMeshProUGUI startText = startGamePanel.GetComponentInChildren<TextMeshProUGUI>();
            if (startText != null)
            {
                // 淡入效果
                Color textColor = startText.color;
                for (float t = 0; t < 1; t += Time.deltaTime)
                {
                    textColor.a = Mathf.Lerp(0, 1, t);
                    startText.color = textColor;
                    yield return null;
                }

                // 显示一段时间
                yield return new WaitForSeconds(1f);

                // 淡出效果
                for (float t = 0; t < 1; t += Time.deltaTime / startGameFadeDuration)
                {
                    textColor.a = Mathf.Lerp(1, 0, t);
                    startText.color = textColor;
                    yield return null;
                }
            }

            startGamePanel.SetActive(false);
        }

        // 通知游戏状态管理器开始游戏
        GameState.Instance.ChangeState(GameState.State.Act1);

        // 发布游戏开始事件
        EventCenter.Instance.Publish("GameStarted");

        // 开始计时
        isGameActive = true;
        gameTimer = 0f;
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

            // 暂停时显示当前控制方式
            if (inputManager != null && inputMethodText != null)
            {
                string controlMethod = "当前控制: 未知";

                if (inputManager.IsUsingMouseInput())
                    controlMethod = "当前控制: 鼠标";
                else if (inputManager.IsUsingGestureInput())
                    controlMethod = "当前控制: 手势";

                // 尝试在暂停菜单中找到文本组件显示控制方式
                TextMeshProUGUI pauseInputText = pauseMenuPanel.GetComponentInChildren<TextMeshProUGUI>(true);
                if (pauseInputText)
                    pauseInputText.text = controlMethod;
            }
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

    // 点击退出按钮 - 适用于主菜单和暂停菜单
    private void OnQuitClicked()
    {
        // 如果游戏已经开始并且是从暂停菜单退出，可以先回到主菜单
        if (isGameActive && isPaused)
        {
            // 可选：添加确认对话框
            ReturnToMainMenu();
        }
        else
        {
            // 直接退出游戏
            QuitGame();
        }
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

    // 返回主菜单
    private void ReturnToMainMenu()
    {
        // 恢复时间缩放
        Time.timeScale = 1f;

        // 重置状态
        isGameActive = false;
        isPaused = false;

        // 隐藏游戏UI
        if (pauseMenuPanel) pauseMenuPanel.SetActive(false);
        if (hudPanel) hudPanel.SetActive(false);

        // 显示主菜单
        if (mainMenuPanel) mainMenuPanel.SetActive(true);

        // 重置游戏状态
        GameState.Instance.ChangeState(GameState.State.MainMenu);

        // 通知游戏回到主菜单
        EventCenter.Instance.Publish("ReturnToMainMenu");
    }

    // 启用鼠标控制
    private void EnableMouseControl()
    {
        if (inputManager != null)
        {
            inputManager.SetInputMethod(true, false);

            // 更新HUD上的输入方式显示
            if (inputMethodText)
                inputMethodText.text = "控制方式: 鼠标";

            Debug.Log("已切换到鼠标控制模式");
        }
    }

    // 启用手势控制
    private void EnableGestureControl()
    {
        if (inputManager != null)
        {
            inputManager.SetInputMethod(false, true);

            // 更新HUD上的输入方式显示
            if (inputMethodText)
                inputMethodText.text = "控制方式: 手势识别";

            Debug.Log("已切换到手势控制模式，等待外部系统提供手势数据");
        }
    }

    // 游戏第一幕开始的事件处理
    private void OnGameFirstActStarted()
    {
        Debug.Log("第一幕开始，UI管理器响应！");
        isGameActive = true;
    }

    // 游戏暂停的事件处理
    private void OnGamePaused()
    {
        Debug.Log("游戏暂停");
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
}