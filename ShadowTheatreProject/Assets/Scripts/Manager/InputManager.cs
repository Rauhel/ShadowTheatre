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
        currentGesture.type = "";
        currentGesture.position = Vector2.zero;
        currentGesture.confidence = 0f;
        currentGesture.additionalData.Clear();
    }

    /// <summary>
    /// 处理手势输入数据：标准化并分发
    /// </summary>
    public void UpdateGestureData(string type, Vector2 rawPosition, float confidence = 1.0f, Dictionary<string, float> additionalData = null)
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
            else if (confidence > 0.5f)
            {
                newHandDetectedState = true;
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

        // 保存手势数据
        currentGesture.type = type;
        currentGesture.position = rawPosition;
        currentGesture.confidence = confidence;

        if (additionalData != null)
        {
            currentGesture.additionalData.Clear();
            foreach (var kvp in additionalData)
            {
                currentGesture.additionalData[kvp.Key] = kvp.Value;
            }
        }

        // 处理手势类型消息
        if (additionalData != null && additionalData.ContainsKey("is_gesture_type") && additionalData["is_gesture_type"] > 0.5f)
        {
            // 触发手势类型事件
            OnGestureTypeReceived?.Invoke(type, confidence);
        }
        else if (type == "HandPosition" || type == "position") // 手部位置消息
        {
            // 标准化位置数据
            Vector2 normalizedPosition = NormalizeGesturePosition(rawPosition);
            // 触发手部位置更新事件
            OnHandPositionUpdated?.Invoke(normalizedPosition);
        }

        // 通知所有监听器
        NotifyGestureListeners();
    }

    /// <summary>
    /// 标准化手势位置数据
    /// </summary>
    private Vector2 NormalizeGesturePosition(Vector2 gesturePos)
    {
        // 应用缩放
        Vector2 scaledPos = new Vector2(
            gesturePos.x * positionScale.x,
            gesturePos.y * positionScale.y
        );

        // 应用反转
        if (invertXAxis) scaledPos.x = 1 - scaledPos.x;
        scaledPos.y = 1 - scaledPos.y;

        // 应用偏移 (归一化值的偏移)
        return scaledPos + positionOffset;
    }

    // 获取当前手势数据
    public GestureData GetCurrentGesture()
    {
        return currentGesture;
    }

    // 获取当前手部检测状态
    public bool IsHandDetected()
    {
        return handDetected;
    }

    // 获取当前标准化手部位置
    public Vector2 GetNormalizedHandPosition()
    {
        return NormalizeGesturePosition(currentGesture.position);
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