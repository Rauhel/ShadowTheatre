using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages the overall state of the game.
/// </summary>
public class GameState : MonoBehaviour
{
    #region Singleton
    private static GameState instance;
    public static GameState Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<GameState>();
                if (instance == null)
                {
                    GameObject singleton = new GameObject(typeof(GameState).ToString());
                    instance = singleton.AddComponent<GameState>();
                }
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

            // 时间缩放功能已禁用 - 统一使用正常时间缩放以解决音频问题
            Time.timeScale = 1f;
            Debug.Log($"GameState Awake: 时间缩放设置为1（音频修复）");
            
            /*
            // 原始时间缩放逻辑（已禁用）
            if (currentState == State.MainMenu || currentState == State.GameStart)
            {
                // 使用时间缩放为很小的值而不是0，以便UI动画仍能播放
                Time.timeScale = 0.00001f;
            }
            else
            {
                // 其他状态（包括Act1）使用正常时间缩放
                Time.timeScale = 1f;
            }
            */
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }
    #endregion

    // Define all possible game states
    public enum State
    {
        MainMenu,       // 游戏主菜单
        GameStart,      // 游戏开始
        GamePaused,     // 游戏暂停
        Act1,           // 第一幕
        Act2,           // 第二幕
        Act3,           // 第三幕
        Curtain,        // 谢幕/结束场景
        GameOver        // 游戏结束
    }

    // Current state of the game
    [SerializeField] private State currentState = State.Act1; // 直接开始第一幕，跳过主菜单

    // Previous state (useful for returning from pause)
    private State previousState;

    // Timestamp tracking
    private float stateStartTime;

    // === 时间管理系统 ===
    [Header("时间管理")]
    [SerializeField] private float gameStartTime; // 游戏开始的时间戳
    [SerializeField] private float currentActStartTime; // 当前幕开始的游戏时间
    [SerializeField] private float currentStoryTime; // 当前故事时间
    
    // 每一幕的故事时间设置（单位：秒）
    [Serializable]
    public class ActStoryTimeSettings
    {
        public State actState;
        [Header("故事时间设置")]
        [Tooltip("该幕开始时的故事时间（秒）")]
        public float storyStartTime;
        [Tooltip("该幕结束时的故事时间（秒）")]
        public float storyEndTime;
        [Tooltip("该幕的故事时间描述")]
        public string storyTimeDescription;
        [Header("自动切换设置")]
        [Tooltip("是否在到达结束时间时自动切换到下一幕")]
        public bool autoSwitchToNextAct = true;
    }
    
    [Header("各幕故事时间设置")]
    [SerializeField] public List<ActStoryTimeSettings> actStoryTimeSettings = new List<ActStoryTimeSettings>
    {
        new ActStoryTimeSettings 
        { 
            actState = State.Act1, 
            storyStartTime = 0f, 
            storyEndTime = 300f, // 5分钟
            storyTimeDescription = "第一幕：早晨 - 故事开始",
            autoSwitchToNextAct = true
        },
        new ActStoryTimeSettings 
        { 
            actState = State.Act2, 
            storyStartTime = 60f, 
            storyEndTime = 360f, // 6分钟故事时间
            storyTimeDescription = "第二幕：下午 - 时间跳跃",
            autoSwitchToNextAct = true
        },
        new ActStoryTimeSettings 
        { 
            actState = State.Act3, 
            storyStartTime = 30f, 
            storyEndTime = 330f, // 5.5分钟故事时间
            storyTimeDescription = "第三幕：夜晚 - 回忆场景",
            autoSwitchToNextAct = true
        }
    };
    
    private Dictionary<State, ActStoryTimeSettings> actToStoryTimeSettings = new Dictionary<State, ActStoryTimeSettings>();

    // Scene associations (which scene to load for each state)
    [Serializable]
    public class StateSceneMapping
    {
        public State state;
        public string sceneName;
    }

    [SerializeField] private List<StateSceneMapping> stateSceneMap = new List<StateSceneMapping>();

    private Dictionary<State, string> stateToSceneMap = new Dictionary<State, string>();

    // Constants for event names
    public static class EventNames
    {
        public const string STATE_CHANGED = "GameState_StateChanged";
        public const string STATE_ENTERED = "GameState_StateEntered_";
        public const string STATE_EXITED = "GameState_StateExited_";
        // 时间相关事件
        public const string STORY_TIME_UPDATED = "GameState_StoryTimeUpdated";
        public const string ACT_TIME_STARTED = "GameState_ActTimeStarted";
    }

    void Start()
    {
        // Initialize the state-to-scene mapping dictionary
        foreach (var mapping in stateSceneMap)
        {
            stateToSceneMap[mapping.state] = mapping.sceneName;
        }
        
        // Initialize the act story time settings dictionary
        foreach (var settings in actStoryTimeSettings)
        {
            actToStoryTimeSettings[settings.actState] = settings;
        }

        // 初始化时间系统
        gameStartTime = Time.time;
        stateStartTime = Time.time;
        
        // 在游戏管理器的GameOver方法中调用
        NPCController.ResetAllNPCScores();
        
        // 添加状态调试信息
        // Debug.Log($"GameState.Start() - 当前状态: {currentState}, 时间缩放: {Time.timeScale}");
        
        // 如果当前状态是Act状态，初始化幕的时间系统
        if (IsActState(currentState))
        {
            Debug.Log($"直接启动幕: {currentState}"); // 保留关键信息
            InitializeActTime(currentState);
        }
    }
    
    void Update()
    {
        // 更新故事时间（只在幕进行时更新）
        if (IsActState(currentState))
        {
            UpdateStoryTime();
            CheckActEndTime();
        }
    }

    // Method to change the game state
    public void ChangeState(State newState)
    {
        if (newState == currentState) return;

        // Store state information
        State oldState = currentState;
        previousState = currentState;

        // Update current state
        currentState = newState;
        stateStartTime = Time.time;

        // 如果切换到新的幕，初始化该幕的时间系统
        if (IsActState(newState))
        {
            InitializeActTime(newState);
        }

        Debug.Log($"游戏状态改变: {oldState} -> {currentState}");

        // Broadcast events using EventCenter
        EventCenter.Instance.Publish(EventNames.STATE_EXITED + oldState.ToString());
        EventCenter.Instance.Publish(EventNames.STATE_ENTERED + currentState.ToString());
        EventCenter.Instance.Publish(EventNames.STATE_CHANGED);

        // Handle time scale for different states - 暂时禁用时间缩放功能以解决音频问题
        // 所有状态都保持正常时间缩放
        Time.timeScale = 1f;
        Debug.Log($"状态 {currentState}，时间缩放保持为1（音频修复）");
        
        /*
        // 原始时间缩放逻辑（已禁用）
        if (currentState == State.GamePaused)
        {
            Time.timeScale = 0f;
            Debug.Log("游戏暂停，时间缩放设置为0");
        }
        else if (currentState == State.MainMenu || currentState == State.GameStart)
        {
            // 主菜单或游戏开始状态下，保持时间几乎暂停（但不影响UI动画）
            Time.timeScale = 0.00001f;
            Debug.Log("主菜单/游戏开始状态，时间缩放设置为0.00001");
        }
        else if (currentState == State.Act1 || currentState == State.Act2 ||
                 currentState == State.Act3 || currentState == State.Curtain)
        {
            // 游戏进行中的状态
            Time.timeScale = 1f;
            Debug.Log("游戏进行状态，时间缩放设置为1");
        }
        */

        // Load the appropriate scene if mapped
        if (stateToSceneMap.ContainsKey(newState) && !string.IsNullOrEmpty(stateToSceneMap[newState]))
        {
            string sceneName = stateToSceneMap[newState];
            if (SceneManager.GetActiveScene().name != sceneName)
            {
                Debug.Log($"加载场景: {sceneName}");
                SceneManager.LoadScene(sceneName);
            }
        }
    }
    
    #region 时间管理方法
    
    /// <summary>
    /// 检查给定状态是否为幕状态
    /// </summary>
    private bool IsActState(State state)
    {
        return state == State.Act1 || state == State.Act2 || state == State.Act3;
    }
    
    /// <summary>
    /// 初始化当前幕的时间系统
    /// </summary>
    private void InitializeActTime(State actState)
    {
        currentActStartTime = Time.time;
        
        // 设置该幕的初始故事时间
        if (actToStoryTimeSettings.ContainsKey(actState))
        {
            currentStoryTime = actToStoryTimeSettings[actState].storyStartTime;
            Debug.Log($"进入{actState}，故事时间重置为{FormatTime(currentStoryTime)}");
        }
        else
        {
            currentStoryTime = 0f; // 默认从0开始
            Debug.Log($"进入{actState}，故事时间重置为{FormatTime(currentStoryTime)}（默认值）");
        }
        
        Debug.Log($"幕 {actState} 开始 - 初始故事时间: {FormatTime(currentStoryTime)}");
        
        // 广播幕时间开始事件
        EventCenter.Instance.Publish(EventNames.ACT_TIME_STARTED);
    }
    
    /// <summary>
    /// 更新故事时间
    /// </summary>
    private void UpdateStoryTime()
    {
        if (IsActState(currentState))
        {
            // 获取该幕的初始故事时间偏移
            float initialOffset = actToStoryTimeSettings.ContainsKey(currentState) ? 
                                  actToStoryTimeSettings[currentState].storyStartTime : 0f;
            
            // 故事时间 = 初始偏移 + 该幕已进行的时间
            float actElapsedTime = Time.time - currentActStartTime;
            float newStoryTime = initialOffset + actElapsedTime;
            
            // 如果故事时间发生了显著变化，广播更新事件
            if (Mathf.Abs(newStoryTime - currentStoryTime) > 0.1f)
            {
                currentStoryTime = newStoryTime;
                EventCenter.Instance.Publish(EventNames.STORY_TIME_UPDATED);
                
                // 每10秒输出一次故事时间更新
                if (Mathf.FloorToInt(currentStoryTime) % 10 == 0 && Mathf.FloorToInt(currentStoryTime) != Mathf.FloorToInt(currentStoryTime - 0.1f))
                {
                    Debug.Log($"故事时间更新: {FormatTime(currentStoryTime)} ({currentState})");
                }
            }
            else
            {
                currentStoryTime = newStoryTime;
            }
        }
    }
    
    /// <summary>
    /// 获取游戏时间（从游戏开始的总时间）
    /// </summary>
    public float GetGameTime()
    {
        return Time.time - gameStartTime;
    }
    
    /// <summary>
    /// 获取当前故事时间
    /// </summary>
    public float GetStoryTime()
    {
        return currentStoryTime;
    }
    
    /// <summary>
    /// 获取当前故事时间（别名方法，用于兼容性）
    /// </summary>
    public float GetCurrentStoryTime()
    {
        return GetStoryTime();
    }
    
    /// <summary>
    /// 获取当前幕已进行的时间
    /// </summary>
    public float GetCurrentActElapsedTime()
    {
        if (IsActState(currentState))
        {
            return Time.time - currentActStartTime;
        }
        return 0f;
    }
    
    /// <summary>
    /// 检查当前幕是否到达结束时间，并自动切换到下一幕
    /// </summary>
    private void CheckActEndTime()
    {
        if (IsActState(currentState) && actToStoryTimeSettings.ContainsKey(currentState))
        {
            ActStoryTimeSettings currentActSettings = actToStoryTimeSettings[currentState];
            
            // 检查是否启用自动切换并且到达结束时间
            if (currentActSettings.autoSwitchToNextAct && currentStoryTime >= currentActSettings.storyEndTime)
            {
                Debug.Log($"幕 {currentState} 到达结束时间 {FormatTime(currentActSettings.storyEndTime)}，自动切换到下一幕");
                
                // 根据当前幕切换到下一幕
                switch (currentState)
                {
                    case State.Act1:
                        ChangeState(State.Act2);
                        break;
                    case State.Act2:
                        ChangeState(State.Act3);
                        break;
                    case State.Act3:
                        ChangeState(State.Curtain);
                        break;
                }
            }
        }
    }
    
    /// <summary>
    /// 获取指定幕的故事时间设置
    /// </summary>
    public ActStoryTimeSettings GetActStoryTimeSettings(State actState)
    {
        return actToStoryTimeSettings.ContainsKey(actState) ? actToStoryTimeSettings[actState] : null;
    }
    
    /// <summary>
    /// 设置指定幕的故事开始时间
    /// </summary>
    public void SetActStoryStartTime(State actState, float startTime)
    {
        if (IsActState(actState) && actToStoryTimeSettings.ContainsKey(actState))
        {
            actToStoryTimeSettings[actState].storyStartTime = startTime;
            
            // 如果当前就在这一幕，重新初始化时间
            if (currentState == actState)
            {
                InitializeActTime(actState);
            }
        }
    }
    
    /// <summary>
    /// 设置指定幕的故事结束时间
    /// </summary>
    public void SetActStoryEndTime(State actState, float endTime)
    {
        if (IsActState(actState) && actToStoryTimeSettings.ContainsKey(actState))
        {
            actToStoryTimeSettings[actState].storyEndTime = endTime;
        }
    }
    
    /// <summary>
    /// 获取指定幕的故事开始时间
    /// </summary>
    public float GetActStoryStartTime(State actState)
    {
        return actToStoryTimeSettings.ContainsKey(actState) ? actToStoryTimeSettings[actState].storyStartTime : 0f;
    }
    
    /// <summary>
    /// 获取指定幕的故事结束时间
    /// </summary>
    public float GetActStoryEndTime(State actState)
    {
        return actToStoryTimeSettings.ContainsKey(actState) ? actToStoryTimeSettings[actState].storyEndTime : 0f;
    }
    
    /// <summary>
    /// 设置指定幕是否自动切换到下一幕
    /// </summary>
    public void SetActAutoSwitch(State actState, bool autoSwitch)
    {
        if (IsActState(actState) && actToStoryTimeSettings.ContainsKey(actState))
        {
            actToStoryTimeSettings[actState].autoSwitchToNextAct = autoSwitch;
        }
    }
    
    /// <summary>
    /// 获取当前幕的剩余故事时间
    /// </summary>
    public float GetCurrentActRemainingTime()
    {
        if (IsActState(currentState) && actToStoryTimeSettings.ContainsKey(currentState))
        {
            float endTime = actToStoryTimeSettings[currentState].storyEndTime;
            return Mathf.Max(0f, endTime - currentStoryTime);
        }
        return 0f;
    }
    
    /// <summary>
    /// 获取当前幕的进度百分比（0-1）
    /// </summary>
    public float GetCurrentActProgress()
    {
        if (IsActState(currentState) && actToStoryTimeSettings.ContainsKey(currentState))
        {
            ActStoryTimeSettings settings = actToStoryTimeSettings[currentState];
            float totalDuration = settings.storyEndTime - settings.storyStartTime;
            float currentProgress = currentStoryTime - settings.storyStartTime;
            return Mathf.Clamp01(currentProgress / totalDuration);
        }
        return 0f;
    }
    
    /// <summary>
    /// 格式化时间显示（分:秒）
    /// </summary>
    public string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
        return $"{minutes:00}:{seconds:00}";
    }
    
    /// <summary>
    /// 获取时间信息的详细字符串
    /// </summary>
    public string GetTimeInfoString()
    {
        string baseInfo = $"游戏时间: {FormatTime(GetGameTime())} | 故事时间: {FormatTime(GetStoryTime())}";
        
        if (IsActState(currentState))
        {
            float remainingTime = GetCurrentActRemainingTime();
            float progress = GetCurrentActProgress();
            baseInfo += $" | 当前幕进度: {(progress * 100f):F1}% | 剩余时间: {FormatTime(remainingTime)}";
        }
        
        return baseInfo;
    }
    
    #endregion

    // Pause the game and save the current state
    public void PauseGame()
    {
        if (currentState != State.GamePaused)
        {
            ChangeState(State.GamePaused);
        }
    }

    // Resume the game to the state before pausing
    public void ResumeGame()
    {
        if (currentState == State.GamePaused && previousState != State.GamePaused)
        {
            ChangeState(previousState);
        }
    }

    // Proceed to the next act
    public void NextAct()
    {
        switch (currentState)
        {
            case State.GameStart:
                ChangeState(State.Act1);
                break;
            case State.Act1:
                ChangeState(State.Act2);
                break;
            case State.Act2:
                ChangeState(State.Act3);
                break;
            case State.Act3:
                ChangeState(State.Curtain);
                break;
            case State.Curtain:
                ChangeState(State.GameOver);
                break;
        }
    }

    // Public method to get the current state
    public State GetCurrentState()
    {
        return currentState;
    }

    // Public method to get the previous state
    public State GetPreviousState()
    {
        return previousState;
    }

    // Public method to get the time spent in the current state
    public float GetTimeInCurrentState()
    {
        return Time.time - stateStartTime;
    }

    // 在 GameState 类中添加
    public void ResetGame()
    {
        // 广播游戏重置事件
        EventCenter.Instance.Publish("Game_Reset");

        // 重置时间系统
        gameStartTime = Time.time;
        currentStoryTime = 0f;
        currentActStartTime = Time.time;

        // 重置为初始状态
        ChangeState(State.GameStart);
    }
}