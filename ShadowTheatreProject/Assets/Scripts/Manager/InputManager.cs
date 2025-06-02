using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 输入管理器：纯粹负责处理手势数据并分发给其他系统
/// </summary>
public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    [Header("手势坐标归一化配置")]
    [SerializeField] private bool invertXAxis = false;
    [SerializeField] private Vector2 positionOffset = Vector2.zero;
    [SerializeField] private Vector2 positionScale = Vector2.one;

    // 手部检测状态
    private bool handDetected = false;

    // 分离手势类型和位置数据
    private string currentGestureType = "Unknown";
    private Vector2 currentHandPosition = Vector2.zero;

    // 手势数据类
    [System.Serializable]
    public class GestureData
    {
        public string type;       // 手势类型
        public Vector2 position;  // 归一化的屏幕坐标 (0-1, 0-1)
        public float confidence;  // 置信度 (0-1)
        public Dictionary<string, float> additionalData = new Dictionary<string, float>();
    }

    // 事件系统
    public event Action<GestureData> OnGestureUpdated;
    public event Action<string, float> OnGestureTypeReceived;
    public event Action<bool> OnHandDetectionChanged;
    public event Action<Vector2> OnHandPositionUpdated; // 新增：手部位置更新事件

    // 当前手势数据
    private GestureData currentGesture = new GestureData();

    private void Awake()
    {
        // 单例设置
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        ResetGestureData();
    }

    /// <summary>
    /// 重置手势数据
    /// </summary>
    private void ResetGestureData()
    {
        currentGestureType = "Unknown";
        currentHandPosition = Vector2.zero;
        currentGesture.type = currentGestureType;
        currentGesture.position = currentHandPosition;
        currentGesture.confidence = 0f;
        currentGesture.additionalData.Clear();
    }

    /// <summary>
    /// 更新手势类型数据
    /// </summary>
    public void UpdateGestureType(string gestureType, Dictionary<string, float> additionalData = null)
    {
        // 更新手势类型
        currentGestureType = gestureType;
        
        // 更新合并的手势数据
        currentGesture.type = currentGestureType;
        currentGesture.confidence = 1.0f;

        if (additionalData != null)
        {
            currentGesture.additionalData.Clear();
            foreach (var kvp in additionalData)
            {
                currentGesture.additionalData[kvp.Key] = kvp.Value;
            }
        }

        // 触发手势类型事件
        OnGestureTypeReceived?.Invoke(gestureType, 1.0f);
        
        // 通知所有监听器
        NotifyGestureListeners();
        
        Debug.Log($"[InputManager] 手势类型更新: {gestureType}");
    }

    /// <summary>
    /// 更新手部位置数据
    /// </summary>
    public void UpdateHandPosition(Vector2 rawPosition, Dictionary<string, float> additionalData = null)
    {
        // 标准化位置数据
        currentHandPosition = NormalizeGesturePosition(rawPosition);
        
        // 更新合并的手势数据
        currentGesture.position = currentHandPosition;

        if (additionalData != null)
        {
            // 只更新位置相关的附加数据，不覆盖手势类型相关数据
            foreach (var kvp in additionalData)
            {
                if (kvp.Key.Contains("hand") || kvp.Key.Contains("depth") || kvp.Key.Contains("position"))
                {
                    currentGesture.additionalData[kvp.Key] = kvp.Value;
                }
            }
        }

        // 触发手部位置更新事件
        OnHandPositionUpdated?.Invoke(currentHandPosition);
        
        // 通知所有监听器
        NotifyGestureListeners();
    }

    /// <summary>
    /// 处理手势输入数据：标准化并分发（兼容旧接口）
    /// </summary>
    public void UpdateGestureData(string type, Vector2 rawPosition, Dictionary<string, float> additionalData = null)
    {
        // 检查是否是手部检测状态消息
        if (type == "HandDetectionStatus")
        {
            // 解析状态值
            bool newHandDetectedState = false;
            if (additionalData != null && additionalData.ContainsKey("detected"))
            {
                newHandDetectedState = additionalData["detected"] > 0.5f;
            }

            // 仅当状态变化时触发事件
            if (newHandDetectedState != handDetected)
            {
                handDetected = newHandDetectedState;
                Debug.Log($"[InputManager] 手部检测状态变更: {(handDetected ? "检测到手" : "未检测到手")}");
                OnHandDetectionChanged?.Invoke(handDetected);
            }
            return;
        }

        // 检查是否是位置数据
        if (type == "position" || type == "HandPosition")
        {
            UpdateHandPosition(rawPosition, additionalData);
            return;
        }

        // 检查是否是手势类型数据
        if (additionalData != null && additionalData.ContainsKey("is_gesture_type") && additionalData["is_gesture_type"] > 0.5f)
        {
            UpdateGestureType(type, additionalData);
            return;
        }

        // 默认处理为手势类型
        UpdateGestureType(type, additionalData);
    }

    /// <summary>
    /// 标准化手势位置数据，映射到屏幕的0.2-0.8范围内
    /// </summary>
    private Vector2 NormalizeGesturePosition(Vector2 gesturePos)
    {
        // 步骤1: 应用缩放
        Vector2 scaledPos = new Vector2(
            gesturePos.x * positionScale.x,
            gesturePos.y * positionScale.y
        );

        // 步骤2: 应用反转
        if (invertXAxis) scaledPos.x = 1 - scaledPos.x;
        scaledPos.y = 1 - scaledPos.y;

        // 步骤3: 应用缩放到0.2-0.8范围
        // 将0-1范围映射到0.2-0.8范围（缩放0.6倍然后加上0.2的偏移）
        Vector2 centralizedPos = new Vector2(
            scaledPos.x * 0.6f + 0.2f,
            scaledPos.y * 0.6f + 0.2f
        );

        // 步骤4: 应用额外偏移（通过Inspector设置）
        return centralizedPos + positionOffset;
    }

    // 获取当前手势数据
    public GestureData GetCurrentGesture()
    {
        return currentGesture;
    }

    // 获取当前手势类型
    public string GetCurrentGestureType()
    {
        return currentGestureType;
    }

    // 获取当前手部检测状态
    public bool IsHandDetected()
    {
        return handDetected;
    }

    // 获取当前标准化手部位置
    public Vector2 GetNormalizedHandPosition()
    {
        return currentHandPosition;
    }

    // 通知手势监听器
    private void NotifyGestureListeners()
    {
        OnGestureUpdated?.Invoke(currentGesture);
    }

    // 注册/取消注册手势监听器
    public void RegisterGestureListener(Action<GestureData> listener)
    {
        if (listener != null)
        {
            OnGestureUpdated += listener;
        }
    }

    public void UnregisterGestureListener(Action<GestureData> listener)
    {
        if (listener != null)
        {
            OnGestureUpdated -= listener;
        }
    }
}