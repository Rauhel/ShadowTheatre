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

            // 在游戏开始前，确保游戏逻辑时间暂停（但不影响UI动画）
            if (currentState == State.MainMenu || currentState == State.GameStart)
            {
                // 使用时间缩放为很小的值而不是0，以便UI动画仍能播放
                Time.timeScale = 0.00001f;
            }
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
    [SerializeField] private State currentState = State.MainMenu;

    // Previous state (useful for returning from pause)
    private State previousState;

    // Timestamp tracking
    private float stateStartTime;

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
    }

    void Start()
    {
        // Initialize the state-to-scene mapping dictionary
        foreach (var mapping in stateSceneMap)
        {
            stateToSceneMap[mapping.state] = mapping.sceneName;
        }

        // Start with the current state
        stateStartTime = Time.time;
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

        Debug.Log($"State changed from {oldState} to {currentState}");

        // Broadcast events using EventCenter
        EventCenter.Instance.Publish(EventNames.STATE_EXITED + oldState.ToString());
        EventCenter.Instance.Publish(EventNames.STATE_ENTERED + currentState.ToString());
        EventCenter.Instance.Publish(EventNames.STATE_CHANGED);

        // Handle time scale for different states
        if (currentState == State.GamePaused)
        {
            Time.timeScale = 0f;
        }
        else if (currentState == State.MainMenu || currentState == State.GameStart)
        {
            // 主菜单或游戏开始状态下，保持时间几乎暂停（但不影响UI动画）
            Time.timeScale = 0.00001f;
        }
        else if (currentState == State.Act1 || currentState == State.Act2 ||
                 currentState == State.Act3 || currentState == State.Curtain)
        {
            // 游戏进行中的状态
            Time.timeScale = 1f;
        }

        // Load the appropriate scene if mapped
        if (stateToSceneMap.ContainsKey(newState) && !string.IsNullOrEmpty(stateToSceneMap[newState]))
        {
            string sceneName = stateToSceneMap[newState];
            if (SceneManager.GetActiveScene().name != sceneName)
            {
                SceneManager.LoadScene(sceneName);
            }
        }
    }

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
}