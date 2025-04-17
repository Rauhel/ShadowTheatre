using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 输入管理器：负责处理所有输入数据的转换和管理
/// </summary>
public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    [Header("Input Settings")]
    [SerializeField] private bool useMouseInput = true;
    [SerializeField] private bool useGestureInput = false;

    [Header("手势坐标映射配置")]
    [SerializeField] private bool invertXAxis = false;
    [SerializeField] private bool invertYAxis = true;
    [SerializeField] private Vector2 positionOffset = Vector2.zero;
    [SerializeField] private Vector2 positionScale = Vector2.one;

    // 鼠标位置转换相关
    private Plane groundPlane;
    private Camera mainCamera;

    // 输入状态
    private Vector3 currentPointerPosition;
    private bool isPointerActive = false;

    // 手部检测状态
    private bool handDetected = true;

    // 手势数据
    [System.Serializable]
    public class GestureData
    {
        public string type;       // 手势类型
        public Vector2 position;  // 归一化的屏幕坐标 (0-1, 0-1)
        public float confidence;  // 置信度 (0-1)
        public Dictionary<string, float> additionalData = new Dictionary<string, float>();
    }

    // 手势事件
    public event Action<GestureData> OnGestureUpdated;
    public event Action<string, float> OnGestureTypeReceived;
    public event Action<bool> OnHandDetectionChanged;

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

        // 初始化
        mainCamera = Camera.main;
        groundPlane = new Plane(Vector3.up, Vector3.zero);
        currentGesture.type = "";
        currentGesture.position = Vector2.zero;
        currentGesture.confidence = 0f;
        currentGesture.additionalData = new Dictionary<string, float>();
    }

    private void Start()
    {
        // 检查相机引用
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("严重错误：找不到主相机!");
            }
        }
    }

    private void Update()
    {
        if (useMouseInput)
        {
            UpdateMouseInput();
        }
    }

    /// <summary>
    /// 处理鼠标输入
    /// </summary>
    private void UpdateMouseInput()
    {
        isPointerActive = Input.mousePresent;

        if (isPointerActive)
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (groundPlane.Raycast(ray, out float distance))
            {
                currentPointerPosition = ray.GetPoint(distance);
            }
        }
    }

    /// <summary>
    /// 将MediaPipe坐标映射到Unity屏幕坐标系
    /// </summary>
    private Vector2 MapGesturePositionToScreen(Vector2 gesturePos)
    {
        // 应用缩放
        Vector2 scaledPos = new Vector2(
            gesturePos.x * positionScale.x,
            gesturePos.y * positionScale.y
        );

        // 应用反转
        if (invertXAxis) scaledPos.x = 1 - scaledPos.x;
        if (invertYAxis) scaledPos.y = 1 - scaledPos.y;

        // 应用偏移 (归一化值的偏移)
        Vector2 mappedPos = scaledPos + positionOffset;

        // 将归一化坐标(0-1)转换为屏幕像素坐标
        return new Vector2(
            mappedPos.x * Screen.width,
            mappedPos.y * Screen.height
        );
    }

    /// <summary>
    /// 处理来自GestureReceiver的手势数据
    /// </summary>
    public void UpdateGestureData(string type, Vector2 rawPosition, float confidence = 1.0f, Dictionary<string, float> additionalData = null)
    {
        useGestureInput = true;

        // 检查是否是手部检测状态消息
        if (type == "HandDetectionStatus")
        {
            // 解析状态值
            bool newHandDetectedState = false;
            if (additionalData != null && additionalData.ContainsKey("detected"))
            {
                newHandDetectedState = additionalData["detected"] > 0.5f;
            }
            else if (confidence > 0.5f) // 使用confidence作为备用
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

            // 更新指针活跃状态
            isPointerActive = handDetected;
            return;
        }

        // 其他情况下，只有检测到手时才保持指针活跃
        isPointerActive = handDetected;

        // 保存原始数据
        currentGesture.type = type;
        currentGesture.position = rawPosition;
        currentGesture.confidence = confidence;

        if (additionalData != null)
        {
            // 清除之前的额外数据
            currentGesture.additionalData.Clear();

            // 添加新的额外数据
            foreach (var kvp in additionalData)
            {
                currentGesture.additionalData[kvp.Key] = kvp.Value;
            }
        }

        // 检查是否是手势类型消息
        bool isGestureTypeMessage = false;
        if (additionalData != null && additionalData.ContainsKey("is_gesture_type") && additionalData["is_gesture_type"] > 0.5f)
        {
            isGestureTypeMessage = true;
            // 触发手势类型事件，供PlayerManager处理
            Debug.Log($"[InputManager] 接收到手势类型: {type}, 置信度: {confidence:F3}, 触发OnGestureTypeReceived事件");
            OnGestureTypeReceived?.Invoke(type, confidence);
        }
        else // 位置数据
        {
            ProcessPositionData(rawPosition);
        }

        // 通知所有监听器
        NotifyGestureListeners();
    }

    /// <summary>
    /// 处理位置数据，转换为3D世界坐标
    /// </summary>
    private void ProcessPositionData(Vector2 rawPosition)
    {
        // 映射坐标到Unity屏幕坐标系
        Vector2 mappedScreenPosition = MapGesturePositionToScreen(rawPosition);

        // 确保摄像机引用正确
        if (mainCamera == null)
        {
            RefreshMainCamera();
            if (mainCamera == null)
            {
                Debug.LogError("找不到主摄像机，无法进行射线检测!");
                return;
            }
        }

        // 使用映射后的屏幕坐标创建射线
        Ray ray = mainCamera.ScreenPointToRay(new Vector3(mappedScreenPosition.x, mappedScreenPosition.y, 0));

        // 尝试进行射线检测
        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 hitPoint = ray.GetPoint(distance);
            currentPointerPosition = hitPoint;
            Debug.DrawRay(ray.origin, ray.direction * distance, Color.green, 0.1f);
        }
        else
        {
            Debug.DrawRay(ray.origin, ray.direction * 100, Color.red, 0.1f);
            TryAlternativePositionCalculation(ray);
        }
    }

    /// <summary>
    /// 备用的位置计算方法
    /// </summary>
    private void TryAlternativePositionCalculation(Ray ray)
    {
        // 使用固定Y值
        float fixedY = 0;
        float t = (fixedY - ray.origin.y) / ray.direction.y;

        if (t > 0)
        {
            Vector3 hitPoint = ray.origin + ray.direction * t;
            currentPointerPosition = hitPoint;
        }
    }

    // 公共API: 获取当前指针在世界空间的位置
    public Vector3 GetPointerWorldPosition()
    {
        return currentPointerPosition;
    }

    // 公共API: 指针是否激活
    public bool IsPointerActive()
    {
        return isPointerActive;
    }

    // 更新地面平面
    public void UpdateGroundPlane(float height)
    {
        groundPlane = new Plane(Vector3.up, new Vector3(0, height, 0));
    }

    // 强制刷新MainCamera引用
    public void RefreshMainCamera()
    {
        mainCamera = Camera.main;
    }

    // 设置输入方法
    public void SetInputMethod(bool mouse, bool gesture)
    {
        useMouseInput = mouse;
        useGestureInput = gesture;

        // 如果都关闭了，默认启用鼠标输入作为后备
        if (!useMouseInput && !useGestureInput)
        {
            useMouseInput = true;
        }

        // 重置状态
        if (!useGestureInput)
        {
            currentGesture.type = "";
            currentGesture.position = Vector2.zero;
            currentGesture.confidence = 0f;
            currentGesture.additionalData.Clear();
        }

        RefreshMainCamera();
        isPointerActive = useMouseInput ? Input.mousePresent : false;
    }

    // 获取当前手势数据
    public GestureData GetCurrentGesture()
    {
        return currentGesture;
    }

    // 公共API: 检查是否正在使用鼠标输入
    public bool IsUsingMouseInput()
    {
        return useMouseInput;
    }

    // 公共API: 检查是否正在使用手势输入
    public bool IsUsingGestureInput()
    {
        return useGestureInput;
    }

    // 公共API: 获取当前手部检测状态
    public bool IsHandDetected()
    {
        return handDetected;
    }

    // 添加缺失的 NotifyGestureListeners 方法
    private void NotifyGestureListeners()
    {
        // 通知所有订阅者手势数据已更新
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

#if UNITY_EDITOR
    // 用于测试的方法
    public void TestMapping(Vector2 testPosition)
    {
        Vector2 mappedPos = MapGesturePositionToScreen(testPosition);
        Debug.Log($"测试手势映射: 输入=({testPosition.x}, {testPosition.y}), 映射后=({mappedPos.x}, {mappedPos.y})");
        UpdateGestureData("test", testPosition, 1.0f);
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !isPointerActive || mainCamera == null) return;
        
        // 绘制当前指针位置
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(currentPointerPosition, 0.15f);
        
        // 如果使用手势输入，绘制原始手势位置到屏幕的映射点
        if (useGestureInput)
        {
            Vector2 mappedScreenPos = MapGesturePositionToScreen(currentGesture.position);
            Gizmos.color = Color.yellow;
            Vector3 screenPosWorld = mainCamera.ScreenToWorldPoint(
                new Vector3(mappedScreenPos.x, mappedScreenPos.y, 10));
            Gizmos.DrawSphere(screenPosWorld, 0.1f);
        }
    }
#endif
}