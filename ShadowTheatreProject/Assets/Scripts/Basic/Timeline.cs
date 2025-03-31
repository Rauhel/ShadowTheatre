using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Timeline : MonoBehaviour
{
    [Serializable]
    public class TimeEvent
    {
        public float timePoint;         // 事件触发的时间点（秒）
        public string eventName;        // 事件名称
        public string description;      // 事件描述（用于编辑器中查看）
        public UnityEvent onEventTrigger; // 事件触发时调用的Unity事件
        public bool hasTriggered;       // 标记该事件是否已触发
    }

    [Serializable]
    public class ActTimeline
    {
        public GameState.State actState; // 对应的游戏Act状态
        public List<TimeEvent> events = new List<TimeEvent>(); // Act中包含的事件列表
    }

    [SerializeField] private List<ActTimeline> actTimelines = new List<ActTimeline>();
    
    private Dictionary<GameState.State, List<TimeEvent>> timelineEvents = new Dictionary<GameState.State, List<TimeEvent>>();
    private GameState gameState;
    private float actStartTime;
    private bool isTimelinePaused = false;

    private void Awake()
    {
        gameState = GameState.Instance;
        
        // 初始化事件字典
        foreach (var actTimeline in actTimelines)
        {
            if (!timelineEvents.ContainsKey(actTimeline.actState))
            {
                timelineEvents[actTimeline.actState] = new List<TimeEvent>();
            }
            
            foreach (var timeEvent in actTimeline.events)
            {
                timeEvent.hasTriggered = false;
                timelineEvents[actTimeline.actState].Add(timeEvent);
            }
        }
        
        // 订阅游戏状态变化事件
        EventCenter.Instance.Subscribe(GameState.EventNames.STATE_ENTERED + GameState.State.Act1, OnAct1Started);
        EventCenter.Instance.Subscribe(GameState.EventNames.STATE_ENTERED + GameState.State.Act2, OnAct2Started);
        EventCenter.Instance.Subscribe(GameState.EventNames.STATE_ENTERED + GameState.State.Act3, OnAct3Started);
        EventCenter.Instance.Subscribe(GameState.EventNames.STATE_ENTERED + GameState.State.GamePaused, OnGamePaused);
        EventCenter.Instance.Subscribe(GameState.EventNames.STATE_EXITED + GameState.State.GamePaused, OnGameResumed);
    }

    private void OnDestroy()
    {
        // 取消订阅
        EventCenter.Instance.Unsubscribe(GameState.EventNames.STATE_ENTERED + GameState.State.Act1, OnAct1Started);
        EventCenter.Instance.Unsubscribe(GameState.EventNames.STATE_ENTERED + GameState.State.Act2, OnAct2Started);
        EventCenter.Instance.Unsubscribe(GameState.EventNames.STATE_ENTERED + GameState.State.Act3, OnAct3Started);
        EventCenter.Instance.Unsubscribe(GameState.EventNames.STATE_ENTERED + GameState.State.GamePaused, OnGamePaused);
        EventCenter.Instance.Unsubscribe(GameState.EventNames.STATE_EXITED + GameState.State.GamePaused, OnGameResumed);
    }

    private void Update()
    {
        if (isTimelinePaused) return;
        
        // 获取当前Act状态
        GameState.State currentState = gameState.GetCurrentState();
        
        // 检查该状态是否有时间线事件
        if (!timelineEvents.ContainsKey(currentState)) return;
        
        // 计算当前Act中已经过去的时间
        float currentActTime = Time.time - actStartTime;
        
        // 检查并触发事件
        foreach (var timeEvent in timelineEvents[currentState])
        {
            if (!timeEvent.hasTriggered && currentActTime >= timeEvent.timePoint)
            {
                // 触发事件
                timeEvent.onEventTrigger.Invoke();
                
                // 广播事件
                EventCenter.Instance.Publish(timeEvent.eventName);
                
                // 标记为已触发
                timeEvent.hasTriggered = true;
                
                Debug.Log($"Timeline event triggered: {timeEvent.description} at time {currentActTime}");
            }
        }
    }

    // Act事件处理程序
    private void OnAct1Started()
    {
        ResetTimelineForState(GameState.State.Act1);
    }
    
    private void OnAct2Started()
    {
        ResetTimelineForState(GameState.State.Act2);
    }
    
    private void OnAct3Started()
    {
        ResetTimelineForState(GameState.State.Act3);
    }
    
    private void OnGamePaused()
    {
        isTimelinePaused = true;
    }
    
    private void OnGameResumed()
    {
        isTimelinePaused = false;
    }
    
    // 重置特定状态的时间线
    private void ResetTimelineForState(GameState.State state)
    {
        actStartTime = Time.time;
        isTimelinePaused = false;
        
        if (timelineEvents.ContainsKey(state))
        {
            foreach (var timeEvent in timelineEvents[state])
            {
                timeEvent.hasTriggered = false;
            }
        }
    }
    
    // 手动触发指定事件（通过事件名）
    public void TriggerEvent(string eventName)
    {
        GameState.State currentState = gameState.GetCurrentState();
        
        if (!timelineEvents.ContainsKey(currentState)) return;
        
        foreach (var timeEvent in timelineEvents[currentState])
        {
            if (timeEvent.eventName == eventName && !timeEvent.hasTriggered)
            {
                timeEvent.onEventTrigger.Invoke();
                EventCenter.Instance.Publish(timeEvent.eventName);
                timeEvent.hasTriggered = true;
                
                Debug.Log($"Timeline event manually triggered: {timeEvent.description}");
                return;
            }
        }
    }
    
    // 跳转到时间线上的特定时间点
    public void JumpToTime(float targetTime)
    {
        GameState.State currentState = gameState.GetCurrentState();
        
        if (!timelineEvents.ContainsKey(currentState)) return;
        
        actStartTime = Time.time - targetTime;
        
        // 重置所有事件状态
        foreach (var timeEvent in timelineEvents[currentState])
        {
            timeEvent.hasTriggered = (timeEvent.timePoint < targetTime);
            
            // 立即触发应该已经触发的事件
            if (timeEvent.timePoint < targetTime && !timeEvent.hasTriggered)
            {
                timeEvent.onEventTrigger.Invoke();
                EventCenter.Instance.Publish(timeEvent.eventName);
                timeEvent.hasTriggered = true;
            }
        }
        
        Debug.Log($"Timeline jumped to time: {targetTime}");
    }
}
