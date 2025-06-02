using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 手势识别管理器：监控手势识别过程并提供反馈
/// </summary>
public class GestureRecognitionManager : MonoBehaviour
{
    [Header("识别设置")]
    [Tooltip("手势完全识别所需时间(秒)")]
    public float fullRecognitionTime = 2f;

    [Tooltip("手势识别总时间限制(秒)")]
    public float timeLimit = 3f;

    [Tooltip("正确手势进度增加速度")]
    [Range(0.1f, 3f)]
    public float progressIncreaseRate = 1f;

    [Tooltip("错误手势进度减少速度")]
    [Range(0.1f, 3f)]
    public float progressDecreaseRate = 0.8f;

    [Header("UI引用")]
    [Tooltip("手势识别进度条")]
    public Slider gestureProgressBar;

    [Tooltip("倒计时文本")]
    public Text countdownText;

    [Tooltip("提示文本")]
    public Text promptText;

    [Header("调试设置")]
    public bool debugMode = false;

    [Tooltip("实时手势进度文本")]
    public Text debugProgressText;

    [Tooltip("是否在UI上显示手势识别详细信息")]
    public bool showDebugInfo = true;

    [Header("高级设置")]
    [Tooltip("当玩家不在交互范围内时是否暂停进度")]
    public bool pauseWhenOutOfRange = true;

    // 事件
    public event Action<string> OnGestureRecognized;
    public event Action OnGestureTimedOut;
    public event Action<float> OnProgressChanged;

    // 内部状态
    private bool isRecognizing = false;
    private string targetGesture = "";
    private string currentGesture = "";
    private float recognitionProgress = 0f;
    private float remainingTime = 0f;
    private Coroutine recognitionCoroutine;
    public string CurrentGesture => currentGesture;

    // 调试信息
    private string currentDebugText = "";
    private Dictionary<string, float> gestureProgressMap = new Dictionary<string, float>();
    private Dictionary<string, string> debugInfoMap = new Dictionary<string, string>();

    // 引用
    private InputManager inputManager;

    // 新增控制暂停/恢复的变量
    private bool isRecognitionPaused = false;

    private void Awake()
    {
        inputManager = FindObjectOfType<InputManager>();
        if (inputManager == null)
        {
            Debug.LogError("找不到InputManager，手势识别将无法工作");
        }

        // 初始UI状态
        if (gestureProgressBar != null)
        {
            gestureProgressBar.value = 0;
            gestureProgressBar.gameObject.SetActive(false);
        }

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }

        if (promptText != null)
        {
            promptText.gameObject.SetActive(false);
        }

        if (debugProgressText != null && !showDebugInfo)
        {
            debugProgressText.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (inputManager != null)
        {
            inputManager.OnGestureTypeReceived += HandleGestureUpdated;
        }
    }

    private void OnDisable()
    {
        if (inputManager != null)
        {
            inputManager.OnGestureTypeReceived -= HandleGestureUpdated;
        }
    }

    /// <summary>
    /// 开始识别特定手势
    /// </summary>
    /// <param name="gestureType">目标手势类型</param>
    public void StartGestureRecognition(string gestureType)
    {
        // 如果已经在识别中，先停止
        if (isRecognizing)
        {
            StopGestureRecognition();
        }

        // 清空手势进度字典
        gestureProgressMap.Clear();

        targetGesture = gestureType;
        isRecognizing = true;
        recognitionProgress = 0f;
        remainingTime = timeLimit;

        // 显示UI
        if (gestureProgressBar != null)
        {
            gestureProgressBar.value = 0;
            gestureProgressBar.gameObject.SetActive(true);
        }

        if (countdownText != null)
        {
            countdownText.text = timeLimit.ToString("F1") + "s";
            countdownText.gameObject.SetActive(true);
        }

        if (promptText != null)
        {
            // 使用格式化方法避免转义符出现问题
            promptText.text = string.Format("请做出\"{0}\"手势", gestureType);
            promptText.gameObject.SetActive(true);
        }

        // 显示调试UI
        if (debugProgressText != null && showDebugInfo)
        {
            debugProgressText.gameObject.SetActive(true);
            debugProgressText.text = "等待手势数据...";
        }

        // 开始识别协程
        recognitionCoroutine = StartCoroutine(GestureRecognitionProcess());

        Debug.Log($"开始识别手势: {gestureType}");
    }

    /// <summary>
    /// 停止当前手势识别
    /// </summary>
    public void StopGestureRecognition()
    {
        if (!isRecognizing) return;

        isRecognizing = false;

        if (recognitionCoroutine != null)
        {
            StopCoroutine(recognitionCoroutine);
            recognitionCoroutine = null;
        }

        // 隐藏UI
        if (gestureProgressBar != null)
        {
            gestureProgressBar.gameObject.SetActive(false);
        }

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }

        if (promptText != null)
        {
            promptText.gameObject.SetActive(false);
        }

        // 清空调试文本
        if (debugProgressText != null)
        {
            debugProgressText.gameObject.SetActive(false);
        }

        // 清空手势进度映射
        gestureProgressMap.Clear();

        Debug.Log("手势识别已停止");
    }

    /// <summary>
    /// 暂停或恢复手势识别
    /// </summary>
    public void SetRecognitionPaused(bool paused)
    {
        isRecognitionPaused = paused;

        // 更新UI显示
        if (gestureProgressBar != null)
        {
            // 当暂停时，进度条变成灰色
            gestureProgressBar.gameObject.GetComponent<Image>().color =
                isRecognitionPaused ? Color.gray : Color.green;
        }

        // 更新调试信息
        if (debugMode)
        {
            SetDebugInfo("识别状态", isRecognitionPaused ? "已暂停" : "进行中");
        }
    }

    /// <summary>
    /// 处理输入系统更新的手势类型
    /// </summary>
    private void HandleGestureUpdated(string gestureType, float _)  // 使用下划线表示忽略的参数
    {
        if (!isRecognizing) return;

        currentGesture = gestureType;

        // 记录或更新这个手势的进度
        if (!gestureProgressMap.ContainsKey(gestureType))
        {
            gestureProgressMap[gestureType] = 0f;
        }

        if (debugMode)
        {
            Debug.Log($"当前手势: {currentGesture}, 目标手势: {targetGesture}");
        }
    }

    private void Update()
    {
        // 如果在识别过程中，每帧尝试获取最新手势数据
        if (isRecognizing && inputManager != null)
        {
            // 获取当前手势类型（使用新的分离方法）
            string latestGestureType = inputManager.GetCurrentGestureType();
            if (!string.IsNullOrEmpty(latestGestureType) && latestGestureType != "Unknown")
            {
                currentGesture = latestGestureType;

                // 确保手势类型在字典中
                if (!gestureProgressMap.ContainsKey(currentGesture))
                {
                    gestureProgressMap[currentGesture] = 0f;
                }

                if (debugMode)
                {
                    Debug.Log($"Update获取手势: {currentGesture}, 目标: {targetGesture}");
                }
            }
            else
            {
                // 如果没有有效手势，设置为Unknown
                currentGesture = "Unknown";
                if (debugMode)
                {
                    Debug.Log($"Update获取手势: {currentGesture}, 目标: {targetGesture}");
                }
            }

            // 更新调试文本
            UpdateDebugText();
        }
    }

    /// <summary>
    /// 设置调试信息的公共方法
    /// </summary>
    public void SetDebugInfo(string key, string value)
    {
        debugInfoMap[key] = value;
    }

    /// <summary>
    /// 更新调试文本显示各手势的识别进度
    /// </summary>
    private void UpdateDebugText()
    {
        if (!showDebugInfo || debugProgressText == null) return;

        currentDebugText = $"目标手势: {targetGesture} ({(recognitionProgress * 100):F0}%)\n";
        currentDebugText += $"当前手势: {currentGesture}\n";
        currentDebugText += $"剩余时间: {remainingTime:F1}s\n";
        currentDebugText += $"状态: {(isRecognitionPaused ? "已暂停" : "进行中")}\n";

        // 添加自定义调试信息
        foreach (var info in debugInfoMap)
        {
            currentDebugText += $"{info.Key}: {info.Value}\n";
        }

        currentDebugText += "手势进度:\n";

        foreach (var kvp in gestureProgressMap)
        {
            string gestureType = kvp.Key;
            float progress = kvp.Value;

            // 高亮显示当前和目标手势
            if (gestureType == targetGesture)
            {
                currentDebugText += $"<color=green>* {gestureType}: {(progress * 100):F0}%</color>\n";
            }
            else if (gestureType == currentGesture && gestureType != targetGesture)
            {
                currentDebugText += $"<color=yellow>* {gestureType}: {(progress * 100):F0}%</color>\n";
            }
            else
            {
                currentDebugText += $"  {gestureType}: {(progress * 100):F0}%\n";
            }
        }

        debugProgressText.text = currentDebugText;
    }

    /// <summary>
    /// 手势识别过程协程
    /// </summary>
    private IEnumerator GestureRecognitionProcess()
    {
        while (isRecognizing && remainingTime > 0)
        {
            // 更新剩余时间 - 即使暂停也会倒计时
            remainingTime -= Time.deltaTime;

            // 更新倒计时UI
            if (countdownText != null)
            {
                countdownText.text = Mathf.Max(0, remainingTime).ToString("F1") + "s";
            }

            // 只有在未暂停状态下才处理手势识别
            if (!isRecognitionPaused)
            {
                // 检查当前手势与目标手势的匹配
                if (currentGesture == targetGesture)
                {
                    // 正确手势，大幅增加进度
                    // 增加系数让进度条增长更快，原来是每秒只增加 progressIncreaseRate/fullRecognitionTime
                    float speedMultiplier = 5.0f; // 加速系数
                    recognitionProgress += Time.deltaTime * progressIncreaseRate * speedMultiplier / fullRecognitionTime;

                    // 同时更新当前手势的进度
                    if (gestureProgressMap.ContainsKey(currentGesture))
                    {
                        gestureProgressMap[currentGesture] += Time.deltaTime * progressIncreaseRate * speedMultiplier / fullRecognitionTime;
                        gestureProgressMap[currentGesture] = Mathf.Clamp01(gestureProgressMap[currentGesture]);
                    }

                    // 可视化反馈 - 进度条变绿
                    if (gestureProgressBar != null)
                    {
                        gestureProgressBar.gameObject.GetComponent<Image>().color = Color.green;
                    }
                }
                else
                {
                    // 错误手势，减少进度
                    recognitionProgress -= Time.deltaTime * progressDecreaseRate / fullRecognitionTime;

                    // 降低当前目标手势的进度
                    if (gestureProgressMap.ContainsKey(targetGesture))
                    {
                        gestureProgressMap[targetGesture] -= Time.deltaTime * progressDecreaseRate / fullRecognitionTime;
                        gestureProgressMap[targetGesture] = Mathf.Clamp01(gestureProgressMap[targetGesture]);
                    }

                    // 但增加当前手势的进度（以较低的比例）
                    if (!string.IsNullOrEmpty(currentGesture) && gestureProgressMap.ContainsKey(currentGesture))
                    {
                        gestureProgressMap[currentGesture] += Time.deltaTime * 0.1f;
                        gestureProgressMap[currentGesture] = Mathf.Clamp01(gestureProgressMap[currentGesture]);
                    }

                    // 可视化反馈 - 进度条变红
                    if (gestureProgressBar != null)
                    {
                        gestureProgressBar.gameObject.GetComponent<Image>().color = Color.red;
                    }
                }

                // 限制进度范围
                recognitionProgress = Mathf.Clamp01(recognitionProgress);

                // 更新进度条
                if (gestureProgressBar != null)
                {
                    gestureProgressBar.value = recognitionProgress;
                }

                // 触发进度更新事件
                OnProgressChanged?.Invoke(recognitionProgress);
            }

            // 更新调试文本
            UpdateDebugText();

            // 检查是否完成识别
            if (recognitionProgress >= 1.0f)
            {
                // 手势识别成功
                if (debugMode)
                {
                    Debug.Log($"成功识别手势: {targetGesture}");
                }

                // 触发成功事件
                OnGestureRecognized?.Invoke(targetGesture);

                // 停止识别
                StopGestureRecognition();
                yield break;
            }

            yield return null;
        }

        // 如果达到这里，说明超时了
        if (remainingTime <= 0)
        {
            Debug.Log("手势识别超时");
            OnGestureTimedOut?.Invoke();
        }

        // 停止识别
        StopGestureRecognition();
    }
}