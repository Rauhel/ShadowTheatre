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

    // 引用
    private InputManager inputManager;

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

        Debug.Log("手势识别已停止");
    }

    /// <summary>
    /// 处理输入系统更新的手势类型
    /// </summary>
    // 修改方法签名以匹配委托
    private void HandleGestureUpdated(string gestureType, float _)  // 使用下划线表示忽略的参数
    {
        if (!isRecognizing) return;

        currentGesture = gestureType;

        if (debugMode)
        {
            Debug.Log($"当前手势: {currentGesture}, 目标手势: {targetGesture}");
        }
    }

    /// <summary>
    /// 手势识别过程协程
    /// </summary>
    private IEnumerator GestureRecognitionProcess()
    {
        while (isRecognizing && remainingTime > 0)
        {
            // 更新剩余时间
            remainingTime -= Time.deltaTime;

            // 更新倒计时UI
            if (countdownText != null)
            {
                countdownText.text = Mathf.Max(0, remainingTime).ToString("F1") + "s";
            }

            // 检查当前手势与目标手势的匹配
            if (currentGesture == targetGesture)
            {
                // 正确手势，大幅增加进度
                // 增加系数让进度条增长更快，原来是每秒只增加 progressIncreaseRate/fullRecognitionTime
                float speedMultiplier = 5.0f; // 加速系数
                recognitionProgress += Time.deltaTime * progressIncreaseRate * speedMultiplier / fullRecognitionTime;

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