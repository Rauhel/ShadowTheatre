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

    [Header("按钮")]
    public Button startGameButton;        // 开始游戏按钮
    public Button continueButton;         // 继续按钮
    public Button quitButton;             // 退出按钮

    [Header("文本")]
    public TextMeshProUGUI gameTimerText; // 游戏时间文本

    [Header("设置")]
    public float startGameFadeDuration = 3f; // 开始游戏提示的淡出时间

    // 内部状态
    private bool isGameActive = false;    // 游戏是否活跃
    private bool isPaused = false;        // 游戏是否暂停（仅暂停玩家控制，不暂停时间）
    private float gameTimer = 0f;         // 游戏计时器

    // 玩家输入组件引用
    private PlayerInput playerInput;

    private void Awake()
    {
        // 确保所有UI面板初始状态正确
        if (mainMenuPanel) mainMenuPanel.SetActive(true);
        if (startGamePanel) startGamePanel.SetActive(false);
        if (pauseMenuPanel) pauseMenuPanel.SetActive(false);
        if (hudPanel) hudPanel.SetActive(false);

        // 查找场景中的玩家输入组件
        playerInput = FindObjectOfType<PlayerInput>();

        // 初始时禁用玩家常规输入，但允许鼠标点击
        if (playerInput != null)
        {
            playerInput.SetKeyboardInputEnabled(false);
            playerInput.SetClickInputEnabled(true);
        }
    }

    private void Start()
    {
        // 设置按钮监听器
        if (startGameButton) startGameButton.onClick.AddListener(OnStartGameClicked);
        if (continueButton) continueButton.onClick.AddListener(OnContinueClicked);
        if (quitButton) quitButton.onClick.AddListener(OnQuitClicked);

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

    // 点击开始游戏按钮
    private void OnStartGameClicked()
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

            // 暂停玩家控制
            if (playerInput != null)
            {
                playerInput.enabled = false;
            }
        }
        else
        {
            // 隐藏暂停菜单
            if (pauseMenuPanel) pauseMenuPanel.SetActive(false);

            // 恢复玩家控制
            if (playerInput != null)
            {
                playerInput.enabled = true;
            }
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
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // 游戏第一幕开始的事件处理
    private void OnGameFirstActStarted()
    {
        Debug.Log("第一幕开始，UI管理器响应！");
        isGameActive = true;

        // 启用玩家输入
        if (playerInput != null)
        {
            playerInput.SetKeyboardInputEnabled(true);
            // 保留鼠标点击功能
            playerInput.SetClickInputEnabled(true);
        }
    }

    // 游戏暂停的事件处理
    private void OnGamePaused()
    {
        // 注意：此处我们可能不需要特殊处理，因为我们设计的暂停系统不暂停游戏时间
        Debug.Log("游戏暂停，但时间继续流动");
    }

    // 当场景销毁时取消订阅事件
    private void OnDestroy()
    {
        EventCenter.Instance.Unsubscribe(GameState.EventNames.STATE_ENTERED + GameState.State.Act1, OnGameFirstActStarted);
        EventCenter.Instance.Unsubscribe(GameState.EventNames.STATE_ENTERED + GameState.State.GamePaused, OnGamePaused);
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
}