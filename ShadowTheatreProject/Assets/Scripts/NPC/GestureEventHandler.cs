using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 负责处理手势识别和玩家输入，与NPCEventManager分离关注点
/// </summary>
public class GestureEventHandler : MonoBehaviour
{
    // 事件回调
    public event Action<string> OnGestureSuccess;
    public event Action OnGestureFailure;

    // 引用
    private GestureRecognitionManager gestureRecognitionManager;

    // 状态
    private bool isProcessingGesture = false;
    private PathEvent currentEvent = null;

    // 手势进度跟踪
    private Dictionary<string, float> gestureProgress = new Dictionary<string, float>();
    private string lastDetectedGesture = null;
    private bool wasInRange = true;
    private bool eventCompleted = false;

    [Header("玩家交互设置")]
    public Transform playerTransform;
    public bool requirePlayerInRange = true;

    [Header("调试设置")]
    public bool enableDebugLogs = true;
    private float gestureProcessTime = 0f;  // 跟踪手势识别持续时间

    void Start()
    {
        // 查找手势识别管理器
        gestureRecognitionManager = FindObjectOfType<GestureRecognitionManager>();
        if (gestureRecognitionManager != null)
        {
            gestureRecognitionManager.OnGestureRecognized += OnGestureRecognized;
            gestureRecognitionManager.OnGestureTimedOut += OnGestureTimedOut;
        }

        // 查找玩家
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }
    }

    void OnDestroy()
    {
        // 清理手势回调
        if (gestureRecognitionManager != null)
        {
            gestureRecognitionManager.OnGestureRecognized -= OnGestureRecognized;
            gestureRecognitionManager.OnGestureTimedOut -= OnGestureTimedOut;
        }
    }

    void Update()
    {
        // 如果正在处理手势，检查玩家是否在交互范围内
        if (isProcessingGesture && currentEvent != null && !eventCompleted)
        {
            // 计时
            gestureProcessTime += Time.deltaTime;

            // 检查是否超时
            if (gestureProcessTime >= currentEvent.gestureTimeLimit)
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning($"[手势识别超时] NPC: {gameObject.name} | 事件: {currentEvent.eventID} | " +
                                   $"时限: {currentEvent.gestureTimeLimit}s");
                }
                OnGestureFailure?.Invoke();
                ResetGestureState();
                return;
            }

            // 检查玩家范围
            bool playerInRange = IsPlayerInInteractionRange(currentEvent.playerInteractionRadius);

            // 检测范围状态变化
            bool rangeChanged = wasInRange != playerInRange;
            wasInRange = playerInRange;

            // 获取当前检测到的手势
            string currentGesture = gestureRecognitionManager.CurrentGesture;
            bool gestureChanged = lastDetectedGesture != currentGesture;
            lastDetectedGesture = currentGesture;

            // 只有在范围内并且手势是有效的才累积进度
            if (playerInRange && IsValidGesture(currentGesture))
            {
                // 更新进度
                if (!gestureProgress.ContainsKey(currentGesture))
                {
                    gestureProgress[currentGesture] = 0f;
                }

                // 累加进度
                gestureProgress[currentGesture] += Time.deltaTime;

                // 检查进度是否达到要求
                if (gestureProgress[currentGesture] >= currentEvent.gestureHoldTime)
                {
                    // 手势成功完成
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[手势识别成功] NPC: {gameObject.name} | 事件: {currentEvent.eventID} | " +
                                $"手势: {currentGesture} | 进度: 100% | 耗时: {gestureProcessTime:F2}s");
                    }

                    eventCompleted = true;
                    OnGestureSuccess?.Invoke(currentGesture);
                    return;
                }
            }

            // 显示详细调试信息
            if (enableDebugLogs && (Time.frameCount % 20 == 0 || rangeChanged || gestureChanged))
            {
                string gestureTypes = string.Join(", ", GetAcceptableGestures(currentEvent));
                float timeRemaining = currentEvent.gestureTimeLimit - gestureProcessTime;

                // 构建进度字符串
                string progressInfo = "";
                foreach (var pair in gestureProgress)
                {
                    float percent = Mathf.Clamp01(pair.Value / currentEvent.gestureHoldTime) * 100f;
                    progressInfo += $"{pair.Key}: {percent:F0}%, ";
                }

                if (progressInfo.Length > 0)
                {
                    progressInfo = progressInfo.Substring(0, progressInfo.Length - 2);
                }
                else
                {
                    progressInfo = "无进度";
                }

                Debug.Log($"[手势处理] NPC: {gameObject.name} | " +
                          $"事件: {currentEvent.eventID} | " +
                          $"时间: {gestureProcessTime:F1}s / {currentEvent.gestureTimeLimit}s | " +
                          $"剩余: {(timeRemaining > 0 ? timeRemaining : 0):F1}s | " +
                          $"玩家在范围内: {(playerInRange ? "是" : "否")} | " +
                          $"当前手势: {(string.IsNullOrEmpty(currentGesture) ? "无" : currentGesture)} | " +
                          $"进度: {progressInfo} | " +
                          $"可接受手势: {gestureTypes}");
            }

            // 更新调试信息
            if (gestureRecognitionManager != null && gestureRecognitionManager.debugMode)
            {
                UpdateGestureManagerDebugInfo(playerInRange, currentEvent.playerInteractionRadius);
            }
        }
    }

    // 启动手势识别
    public void StartGestureRecognition(PathEvent pathEvent)
    {
        if (isProcessingGesture || gestureRecognitionManager == null)
            return;

        currentEvent = pathEvent;
        isProcessingGesture = true;
        gestureProcessTime = 0f;  // 重置计时器
        gestureProgress.Clear();   // 清空进度
        lastDetectedGesture = null;
        wasInRange = true;
        eventCompleted = false;

        if (enableDebugLogs)
        {
            Debug.Log($"[手势识别开始] NPC: {gameObject.name} | 事件: {pathEvent.eventID} | " +
                     $"时限: {pathEvent.gestureTimeLimit}s | 保持时间: {pathEvent.gestureHoldTime}s");
        }

        // 获取此事件接受的所有手势类型
        List<string> acceptableGestures = GetAcceptableGestures(pathEvent);

        if (acceptableGestures.Count > 0)
        {
            // 设置手势识别参数
            gestureRecognitionManager.fullRecognitionTime = pathEvent.gestureHoldTime;
            gestureRecognitionManager.timeLimit = pathEvent.gestureTimeLimit;

            // 启动识别
            gestureRecognitionManager.StartGestureRecognition(acceptableGestures[0]);

            if (enableDebugLogs)
            {
                Debug.Log($"[手势识别参数] 可接受手势: {string.Join(", ", acceptableGestures)} | " +
                         $"保持时间: {pathEvent.gestureHoldTime}s | 时限: {pathEvent.gestureTimeLimit}s");
            }

            // 如果有多个手势，添加到调试信息
            if (acceptableGestures.Count > 1)
            {
                gestureRecognitionManager.SetDebugInfo("可接受手势", string.Join(", ", acceptableGestures));
            }
        }
        else
        {
            // 没有可接受的手势，通知失败
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[手势识别错误] NPC: {gameObject.name} | 事件: {pathEvent.eventID} | 原因: 没有配置可接受的手势");
            }

            OnGestureFailure?.Invoke();
            ResetGestureState();
        }
    }

    // 取消当前手势识别
    public void CancelGestureRecognition()
    {
        if (!isProcessingGesture)
            return;

        if (enableDebugLogs && currentEvent != null)
        {
            Debug.Log($"[手势识别取消] NPC: {gameObject.name} | 事件: {currentEvent.eventID} | " +
                     $"耗时: {gestureProcessTime:F2}s");
        }

        if (gestureRecognitionManager != null)
        {
            gestureRecognitionManager.StopGestureRecognition();
        }

        ResetGestureState();
    }

    // 重置手势状态
    private void ResetGestureState()
    {
        isProcessingGesture = false;
        currentEvent = null;
        gestureProgress.Clear();
        lastDetectedGesture = null;
        eventCompleted = false;
        gestureProcessTime = 0f;
    }

    // 检查手势是否有效（在当前事件中可接受）
    private bool IsValidGesture(string gesture)
    {
        if (string.IsNullOrEmpty(gesture) || currentEvent == null)
            return false;

        foreach (var response in currentEvent.gestureResponses)
        {
            if (response.gestureType == gesture)
                return true;
        }

        return false;
    }

    // 旧的回调方法 - 我们不再使用这些方法进行成功判定，而是在Update中持续跟踪进度
    private void OnGestureRecognized(string gestureType)
    {
        // 保留这个方法来接收事件，但实际处理逻辑已经移到Update中
    }

    private void OnGestureTimedOut()
    {
        // 保留这个方法来接收事件，但实际处理逻辑已经移到Update中
    }

    // 检查玩家是否在交互范围内
    private bool IsPlayerInInteractionRange(float radius)
    {
        if (!requirePlayerInRange || playerTransform == null)
        {
            return true;
        }

        float distance = Vector3.Distance(transform.position, playerTransform.position);
        return distance <= radius;
    }

    // 更新手势管理器的调试信息
    private void UpdateGestureManagerDebugInfo(bool playerInRange, float interactionRadius)
    {
        gestureRecognitionManager.SetDebugInfo("玩家在范围内", playerInRange ? "是" : "否");
        gestureRecognitionManager.SetDebugInfo("交互范围", $"{interactionRadius}米");

        if (playerTransform != null)
        {
            float distance = Vector3.Distance(transform.position, playerTransform.position);
            gestureRecognitionManager.SetDebugInfo("当前距离", $"{distance:F1}米");
        }

        // 添加进度信息
        string progressInfo = "";
        foreach (var pair in gestureProgress)
        {
            float percent = Mathf.Clamp01(pair.Value / currentEvent.gestureHoldTime) * 100f;
            progressInfo += $"{pair.Key}: {percent:F0}%, ";
        }

        if (progressInfo.Length > 0)
        {
            gestureRecognitionManager.SetDebugInfo("手势进度", progressInfo);
        }
    }

    // 获取事件可接受的手势类型
    private List<string> GetAcceptableGestures(PathEvent pathEvent)
    {
        List<string> gestures = new List<string>();
        if (pathEvent != null)
        {
            foreach (var response in pathEvent.gestureResponses)
            {
                if (!string.IsNullOrEmpty(response.gestureType) && !gestures.Contains(response.gestureType))
                {
                    gestures.Add(response.gestureType);
                }
            }
        }
        return gestures;
    }
}