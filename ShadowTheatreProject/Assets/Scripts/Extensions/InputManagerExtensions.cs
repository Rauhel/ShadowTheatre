using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// InputManager的扩展功能，用于支持NPC事件系统
/// </summary>
public static class InputManagerExtensions
{
    // 当前正在保持的手势类型和时长
    private static Dictionary<string, float> gestureHoldTimes = new Dictionary<string, float>();

    // 上一帧检测到的手势类型
    private static string lastGestureType = "";

    // 手势保持时长阈值（秒）
    private static float gestureHoldThreshold = 2.0f;

    // 手势保持事件委托
    public delegate void GestureHoldHandler(string gestureType, float holdTime);

    // 当手势保持达到阈值时触发
    public static event GestureHoldHandler OnGestureHoldComplete;

    // 当手势保持进行中触发
    public static event GestureHoldHandler OnGestureHolding;

    // 每帧调用此方法来更新手势保持时间
    public static void UpdateGestureHolding(InputManager inputManager)
    {
        if (inputManager == null) return;

        // 获取当前手势数据
        InputManager.GestureData currentGesture = inputManager.GetCurrentGesture();

        // 如果当前手势类型有效
        if (!string.IsNullOrEmpty(currentGesture.type) && currentGesture.confidence > 0.5f)
        {
            // 如果是新手势，初始化保持时间
            if (lastGestureType != currentGesture.type)
            {
                // 重置之前的手势
                if (!string.IsNullOrEmpty(lastGestureType) && gestureHoldTimes.ContainsKey(lastGestureType))
                {
                    gestureHoldTimes[lastGestureType] = 0;
                }

                // 设置新手势
                if (!gestureHoldTimes.ContainsKey(currentGesture.type))
                {
                    gestureHoldTimes[currentGesture.type] = 0;
                }
                else
                {
                    gestureHoldTimes[currentGesture.type] = 0;
                }

                lastGestureType = currentGesture.type;
            }

            // 增加保持时间
            gestureHoldTimes[currentGesture.type] += Time.deltaTime;

            // 触发正在保持事件
            OnGestureHolding?.Invoke(currentGesture.type, gestureHoldTimes[currentGesture.type]);

            // 检查是否达到阈值
            if (gestureHoldTimes[currentGesture.type] >= gestureHoldThreshold)
            {
                OnGestureHoldComplete?.Invoke(currentGesture.type, gestureHoldTimes[currentGesture.type]);

                // 重置计时器，避免重复触发
                gestureHoldTimes[currentGesture.type] = 0;
            }
        }
        else
        {
            // 没有检测到手势，重置上一个手势状态
            if (!string.IsNullOrEmpty(lastGestureType))
            {
                if (gestureHoldTimes.ContainsKey(lastGestureType))
                {
                    gestureHoldTimes[lastGestureType] = 0;
                }
                lastGestureType = "";
            }
        }
    }

    // 设置手势保持阈值（秒）
    public static void SetGestureHoldThreshold(float seconds)
    {
        gestureHoldThreshold = Mathf.Max(0.1f, seconds);
    }

    // 获取当前手势保持时间
    public static float GetCurrentGestureHoldTime(string gestureType)
    {
        if (gestureHoldTimes.TryGetValue(gestureType, out float time))
        {
            return time;
        }
        return 0f;
    }

    // 重置所有手势保持时间
    public static void ResetAllGestureHoldTimes()
    {
        gestureHoldTimes.Clear();
        lastGestureType = "";
    }
}