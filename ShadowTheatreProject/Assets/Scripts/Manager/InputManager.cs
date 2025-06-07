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
    [Tooltip("是否限制手势范围到屏幕中央（0.2-0.8），false=使用全屏范围（0-1）")]
    [SerializeField] private bool useRestrictedRange = false;
    
    [Header("世界坐标转换配置")]
    [SerializeField] private float pointerGroundHeight = 0f;
    [SerializeField] private float raycastZOffset = -5f;

    [Header("输入模式管理")]
    [SerializeField] private bool enableDebugLogs = false;
    [Tooltip("手部检测超时时间（秒），超过此时间未收到位置数据则认为手部丢失")]
    [SerializeField] private float handDetectionTimeout = 0.5f;

    // 输入模式枚举
    public enum InputMode
    {
        Mouse,      // 鼠标输入模式
        Gesture     // 手势输入模式
    }

    // 当前输入模式
    private InputMode currentInputMode = InputMode.Mouse;
    private float lastHandPositionTime = 0f;

    // 手部检测状态
    private bool handDetected = false;

    // 分离手势类型和位置数据
    private string currentGestureType = "Unknown";
    private Vector2 currentHandPosition = Vector2.zero;
    private Vector3 currentWorldPosition = Vector3.zero; // 新增：世界坐标位置

    // 手势数据类
    [System.Serializable]
    public class GestureData
    {
        public string type;       // 手势类型
        public Vector2 position;  // 归一化的屏幕坐标 (0-1, 0-1)
        public Vector3 worldPosition; // 世界坐标位置
        public float confidence;  // 置信度 (0-1)
        public Dictionary<string, float> additionalData = new Dictionary<string, float>();
    }

    // 事件系统
    public event Action<GestureData> OnGestureUpdated;
    public event Action<string, float> OnGestureTypeReceived;
    public event Action<bool> OnHandDetectionChanged;
    public event Action<Vector2> OnHandPositionUpdated; // 归一化位置事件（保持兼容性）
    public event Action<Vector3> OnWorldPositionUpdated; // 新增：世界位置更新事件
    public event Action<InputMode> OnInputModeChanged; // 新增：输入模式变更事件

    // 当前手势数据
    private GestureData currentGesture = new GestureData();
    
    // 相机和地面平面引用
    private Camera mainCamera;
    private Plane groundPlane;

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
        InitializeCamera();
    }

    private void Update()
    {
        // 检查手部检测超时
        CheckHandDetectionTimeout();
    }

    /// <summary>
    /// 检查手部检测超时
    /// </summary>
    private void CheckHandDetectionTimeout()
    {
        if (handDetected && Time.time - lastHandPositionTime > handDetectionTimeout)
        {
            // 手部检测超时，切换到鼠标模式
            SetHandDetected(false);
            
            if (enableDebugLogs)
            {
                Debug.Log("[InputManager] 手部检测超时，自动切换到鼠标模式");
            }
        }
    }

    /// <summary>
    /// 设置手部检测状态并管理输入模式切换
    /// </summary>
    private void SetHandDetected(bool detected)
    {
        if (handDetected != detected)
        {
            handDetected = detected;
            
            // 根据手部检测状态切换输入模式
            InputMode newMode = detected ? InputMode.Gesture : InputMode.Mouse;
            if (currentInputMode != newMode)
            {
                currentInputMode = newMode;
                OnInputModeChanged?.Invoke(currentInputMode);
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[InputManager] 输入模式切换为: {currentInputMode}");
                }
            }
            
            OnHandDetectionChanged?.Invoke(handDetected);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[InputManager] 手部检测状态变更: {(handDetected ? "检测到手" : "未检测到手")}");
            }
        }
    }

    /// <summary>
    /// 初始化相机和地面平面
    /// </summary>
    private void InitializeCamera()
    {
        mainCamera = Camera.main;
        if (mainCamera != null)
        {
            groundPlane = new Plane(Vector3.up, new Vector3(0, pointerGroundHeight, 0));
            if (enableDebugLogs)
            {
                Debug.Log("[InputManager] 相机和地面平面初始化成功");
            }
        }
        else
        {
            Debug.LogWarning("[InputManager] 找不到主相机，世界坐标转换可能无法正常工作");
        }
    }

    /// <summary>
    /// 重置手势数据
    /// </summary>
    private void ResetGestureData()
    {
        currentGestureType = "Unknown";
        currentHandPosition = Vector2.zero;
        currentWorldPosition = Vector3.zero;
        currentGesture.type = currentGestureType;
        currentGesture.position = currentHandPosition;
        currentGesture.worldPosition = currentWorldPosition;
        currentGesture.confidence = 0f;
        currentGesture.additionalData.Clear();
    }

    /// <summary>
    /// 更新手势类型数据
    /// </summary>
    public void UpdateGestureType(string gestureType, Dictionary<string, float> additionalData = null)
    {
        Debug.Log($"[InputManager] === UpdateGestureType调用 === 手势类型: '{gestureType}'");
        
        // 更新手势类型
        currentGestureType = gestureType;
        Debug.Log($"[InputManager] 当前手势类型已更新为: '{currentGestureType}'");
        
        // 更新合并的手势数据
        currentGesture.type = currentGestureType;
        currentGesture.confidence = 1.0f;

        if (additionalData != null)
        {
            Debug.Log($"[InputManager] 处理附加数据，数量: {additionalData.Count}");
            currentGesture.additionalData.Clear();
            foreach (var kvp in additionalData)
            {
                currentGesture.additionalData[kvp.Key] = kvp.Value;
                Debug.Log($"[InputManager] 附加数据: {kvp.Key} = {kvp.Value}");
            }
        }

        // 触发手势类型事件
        Debug.Log($"[InputManager] 准备触发OnGestureTypeReceived事件...");
        Debug.Log($"[InputManager] OnGestureTypeReceived订阅者数量: {GetGestureTypeSubscriberCount()}");
        
        if (OnGestureTypeReceived != null)
        {
            Debug.Log($"[InputManager] ✓ 触发OnGestureTypeReceived事件: '{gestureType}', 置信度: 1.0");
            OnGestureTypeReceived?.Invoke(gestureType, 1.0f);
            Debug.Log($"[InputManager] ✓ OnGestureTypeReceived事件已触发");
        }
        else
        {
            Debug.LogWarning($"[InputManager] ⚠️ OnGestureTypeReceived事件为空，没有订阅者！");
        }
        
        // 通知所有监听器
        Debug.Log($"[InputManager] 通知手势监听器...");
        NotifyGestureListeners();
        
        if (enableDebugLogs)
        {
            Debug.Log($"[InputManager] 手势类型更新完成: {gestureType}");
        }
        
        Debug.Log($"[InputManager] === UpdateGestureType结束 ===");
    }

    /// <summary>
    /// 更新手部位置数据并转换为世界坐标
    /// </summary>
    public void UpdateHandPosition(Vector2 rawPosition, Dictionary<string, float> additionalData = null)
    {
        // 记录接收到位置数据的时间
        lastHandPositionTime = Time.time;
        
        // 如果之前没有检测到手，现在检测到了
        if (!handDetected)
        {
            SetHandDetected(true);
        }
        
        // 标准化位置数据
        currentHandPosition = NormalizeGesturePosition(rawPosition);
        
        // 转换为世界坐标
        currentWorldPosition = ConvertToWorldPosition(currentHandPosition);
        
        // 更新合并的手势数据
        currentGesture.position = currentHandPosition;
        currentGesture.worldPosition = currentWorldPosition;

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

        // 只有在手势输入模式下才触发位置事件
        if (currentInputMode == InputMode.Gesture)
        {
            OnHandPositionUpdated?.Invoke(currentHandPosition); // 保持兼容性
            OnWorldPositionUpdated?.Invoke(currentWorldPosition); // 新的世界坐标事件
            
            if (enableDebugLogs)
            {
                Debug.Log($"[InputManager] 手势位置更新: 归一化={currentHandPosition}, 世界={currentWorldPosition}");
            }
        }
        
        // 通知所有监听器
        NotifyGestureListeners();
    }

    /// <summary>
    /// 将归一化坐标转换为世界坐标
    /// </summary>
    private Vector3 ConvertToWorldPosition(Vector2 normalizedPosition)
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogWarning("[InputManager] 无法获取主相机，返回默认世界坐标");
                return Vector3.zero;
            }
        }

        // 转换为屏幕坐标
        Vector2 screenPosition = new Vector2(
            normalizedPosition.x * Screen.width,
            normalizedPosition.y * Screen.height
        );

        // 创建从屏幕点到世界的射线
        Ray ray = mainCamera.ScreenPointToRay(new Vector3(screenPosition.x, screenPosition.y, 0));

        // 与地面平面求交
        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 hitPoint = ray.GetPoint(distance);
            // 应用Z轴偏移量
            hitPoint.z += raycastZOffset;
            return hitPoint;
        }
        else
        {
            // 备用方案：使用固定Y值
            float t = (pointerGroundHeight - ray.origin.y) / ray.direction.y;
            if (t > 0)
            {
                Vector3 hitPoint = ray.origin + ray.direction * t;
                hitPoint.z += raycastZOffset;
                return hitPoint;
            }
        }

        return Vector3.zero;
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

            // 设置手部检测状态
            SetHandDetected(newHandDetectedState);
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
    /// 标准化手势位置数据，可选择映射到全屏或限制范围
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

        // 步骤3: 根据设置决定是否限制范围
        Vector2 rangedPos;
        if (useRestrictedRange)
        {
            // 应用缩放到0.2-0.8范围（原有逻辑）
            // 将0-1范围映射到0.2-0.8范围（缩放0.6倍然后加上0.2的偏移）
            rangedPos = new Vector2(
                scaledPos.x * 0.6f + 0.2f,
                scaledPos.y * 0.6f + 0.2f
            );
        }
        else
        {
            // 使用全屏范围（0-1），与鼠标输入一致
            rangedPos = scaledPos;
        }

        // 步骤4: 应用额外偏移（通过Inspector设置）
        return rangedPos + positionOffset;
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

    // 获取当前输入模式
    public InputMode GetCurrentInputMode()
    {
        return currentInputMode;
    }

    // 获取当前标准化手部位置
    public Vector2 GetNormalizedHandPosition()
    {
        return currentHandPosition;
    }

    // 新增：获取当前世界坐标位置
    public Vector3 GetCurrentWorldPosition()
    {
        return currentWorldPosition;
    }

    // 新增：更新地面平面高度
    public void UpdateGroundPlaneHeight(float height)
    {
        pointerGroundHeight = height;
        groundPlane = new Plane(Vector3.up, new Vector3(0, height, 0));
    }

    // 新增：获取地面平面高度
    public float GetGroundPlaneHeight()
    {
        return pointerGroundHeight;
    }

    // 新增：获取Z轴偏移量
    public float GetRaycastZOffset()
    {
        return raycastZOffset;
    }

    // 新增：强制切换输入模式（调试用）
    public void ForceInputMode(InputMode mode)
    {
        if (currentInputMode != mode)
        {
            currentInputMode = mode;
            OnInputModeChanged?.Invoke(currentInputMode);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[InputManager] 强制切换输入模式为: {currentInputMode}");
            }
        }
    }

    // 新增：调试控制
    public void SetDebugLogsEnabled(bool enabled)
    {
        enableDebugLogs = enabled;
        Debug.Log($"[InputManager] 调试日志已{(enabled ? "启用" : "禁用")}");
    }

    // 新增：显示当前映射设置信息
    [ContextMenu("显示映射设置")]
    public void ShowMappingSettings()
    {
        Debug.Log("=== InputManager 映射设置 ===");
        Debug.Log($"使用限制范围: {useRestrictedRange} {(useRestrictedRange ? "(0.2-0.8)" : "(0-1全屏)")}");
        Debug.Log($"X轴反转: {invertXAxis}");
        Debug.Log($"位置缩放: {positionScale}");
        Debug.Log($"位置偏移: {positionOffset}");
        Debug.Log($"地面平面高度: {pointerGroundHeight}");
        Debug.Log($"Z轴偏移: {raycastZOffset}");
        Debug.Log($"手部检测超时: {handDetectionTimeout}秒");
        Debug.Log("==========================");
    }

    // 新增：设置是否使用限制范围
    public void SetUseRestrictedRange(bool restricted)
    {
        useRestrictedRange = restricted;
        Debug.Log($"[InputManager] 手势范围设置为: {(restricted ? "限制范围(0.2-0.8)" : "全屏范围(0-1)")}");
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

    // 获取订阅者数量的安全方法
    public int GetGestureTypeSubscriberCount()
    {
        return OnGestureTypeReceived?.GetInvocationList().Length ?? 0;
    }
}