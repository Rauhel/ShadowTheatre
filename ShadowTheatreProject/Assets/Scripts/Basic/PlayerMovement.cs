using UnityEngine;
using System.Collections;

/// <summary>
/// 玩家移动组件：负责处理玩家移动逻辑，支持手势和鼠标输入
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float smoothTime = 0.1f;
    [Tooltip("是否让角色立即面向指针方向")]
    [SerializeField] private bool facePointerDirection = true;
    [Tooltip("是否锁定Y轴旋转，使角色始终朝向正前方（适用于纸片人）")]
    [SerializeField] private bool lockYRotation = true;
    [Tooltip("锁定旋转时的固定朝向角度")]
    [SerializeField] private float fixedYRotation = 0f;
    [Tooltip("选择移动方式：0=即时移动到指针位置，1=平滑移动到指针位置，2=朝指针方向移动")]
    [SerializeField] private int movementType = 2;

    [Header("Input Settings")]
    [SerializeField] private bool enableMouseInput = true;
    [SerializeField] private bool enableGestureInput = true;
    [SerializeField] private bool showDebugPointer = true;

    [Header("Hover Detection")]
    [SerializeField] private bool enableHoverStop = true;
    [SerializeField] private Vector2 hoverMinThreshold = new Vector2(0.4f, 0.4f);
    [SerializeField] private Vector2 hoverMaxThreshold = new Vector2(0.6f, 0.6f);
    [SerializeField] private Color hoverDebugColor = new Color(1f, 0.5f, 0f, 0.5f); // 橙色

    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogs = false;

    // 引用和内部变量
    private Vector3 currentVelocity = Vector3.zero;
    private Vector3 targetPosition;
    private Vector3 currentPointerPosition;
    private bool isPointerActive = false;
    private bool isHovering = false;

    private InputManager inputManager;
    private Camera mainCamera;
    private bool isInitialized = false;

    // 可视化调试
    private GameObject debugPointer;

    private void Start()
    {
        InitializeComponents();
        StartCoroutine(DelayedInitialize());

        // 创建调试指针
        if (showDebugPointer)
        {
            CreateDebugPointer();
        }
    }

    private IEnumerator DelayedInitialize()
    {
        yield return new WaitForSeconds(0.5f);
        if (!isInitialized)
        {
            InitializeComponents();
        }
    }

    /// <summary>
    /// 初始化组件引用
    /// </summary>
    private void InitializeComponents()
    {
        inputManager = InputManager.Instance;
        mainCamera = Camera.main;

        if (inputManager != null && mainCamera != null)
        {
            // 订阅输入模式变化事件
            inputManager.OnInputModeChanged += HandleInputModeChanged;
            
            // 订阅手势世界位置事件
            if (enableGestureInput)
            {
                inputManager.OnWorldPositionUpdated += HandleWorldPositionUpdated;
                inputManager.OnHandPositionUpdated += HandleHandPositionUpdated; // 用于悬停检测
                inputManager.OnHandDetectionChanged += HandleHandDetectionChanged;
            }

            isInitialized = true;
            Debug.Log("PlayerMovement: 组件初始化成功");
        }
        else
        {
            if (inputManager == null) Debug.LogWarning("PlayerMovement: 找不到InputManager");
            if (mainCamera == null) Debug.LogWarning("PlayerMovement: 找不到主相机");
        }
    }

    /// <summary>
    /// 创建调试指针对象
    /// </summary>
    private void CreateDebugPointer()
    {
        debugPointer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        debugPointer.name = "Pointer_Debug";
        debugPointer.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

        // 设置材质颜色
        Renderer renderer = debugPointer.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = Color.green;
        }

        // 禁用碰撞
        Collider collider = debugPointer.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        debugPointer.SetActive(false);
    }

    /// <summary>
    /// 处理输入模式变化
    /// </summary>
    private void HandleInputModeChanged(InputManager.InputMode newMode)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerMovement] 输入模式变更为: {newMode}");
        }

        // 根据输入模式重置状态
        if (newMode == InputManager.InputMode.Mouse)
        {
            // 切换到鼠标模式时，重置手势相关状态
            isHovering = false;
            
            // 如果当前没有鼠标输入，停用指针
            if (!enableMouseInput)
            {
                isPointerActive = false;
                if (debugPointer != null)
                {
                    debugPointer.SetActive(false);
                }
            }
        }
        else if (newMode == InputManager.InputMode.Gesture)
        {
            // 切换到手势模式时，停用鼠标控制的指针
            // 手势指针会由HandleWorldPositionUpdated处理
        }
    }

    /// <summary>
    /// 处理世界坐标位置更新（来自手势输入）
    /// </summary>
    private void HandleWorldPositionUpdated(Vector3 worldPosition)
    {
        if (!enableGestureInput) return;

        // 只有在手势输入模式下才处理
        if (inputManager != null && inputManager.GetCurrentInputMode() != InputManager.InputMode.Gesture)
        {
            return;
        }

        // 直接使用InputManager转换好的世界坐标
        currentPointerPosition = worldPosition;
        isPointerActive = true;

        // 更新调试指针位置
        if (debugPointer != null)
        {
            debugPointer.transform.position = worldPosition;
            debugPointer.SetActive(true);
            
            // 更新颜色以反映悬停状态
            Renderer renderer = debugPointer.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = isHovering ? hoverDebugColor : Color.green;
            }
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerMovement] 手势位置更新: 世界坐标={worldPosition}, 指针激活={isPointerActive}");
        }
    }

    /// <summary>
    /// 处理归一化位置更新（用于悬停检测）
    /// </summary>
    private void HandleHandPositionUpdated(Vector2 normalizedPosition)
    {
        if (!enableGestureInput) return;

        // 检查是否在悬停区域内
        bool newHoverState = IsPositionInHoverArea(normalizedPosition);
        if (newHoverState != isHovering)
        {
            isHovering = newHoverState;
            //Debug.Log($"PlayerMovement: 悬停状态变更为 {(isHovering ? "悬停中" : "移动中")}");
            
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerMovement] 悬停状态变更为 {(isHovering ? "悬停中" : "移动中")}");
            }
        }
    }

    /// <summary>
    /// 处理鼠标输入（转换为世界坐标）
    /// </summary>
    private void HandleMouseInput()
    {
        if (!enableMouseInput) return;

        // 确保当前是鼠标输入模式
        if (inputManager != null && inputManager.GetCurrentInputMode() != InputManager.InputMode.Mouse)
        {
            return;
        }

        // 检测鼠标是否在屏幕内
        Vector3 mouseScreenPos = Input.mousePosition;
        if (mouseScreenPos.x >= 0 && mouseScreenPos.x <= Screen.width &&
            mouseScreenPos.y >= 0 && mouseScreenPos.y <= Screen.height)
        {
            Vector3 worldPos = GetWorldPositionFromMouse(mouseScreenPos);
            
            if (worldPos != Vector3.zero)
            {
                currentPointerPosition = worldPos;
                isPointerActive = true;

                // 更新调试指针
                if (debugPointer != null)
                {
                    debugPointer.transform.position = worldPos;
                    debugPointer.SetActive(true);
                    
                    // 鼠标输入时使用蓝色
                    Renderer renderer = debugPointer.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material.color = Color.blue;
                    }
                }
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[PlayerMovement] 鼠标位置更新: 屏幕={mouseScreenPos}, 世界={worldPos}");
                }
            }
        }
        else
        {
            // 鼠标离开屏幕时停用指针
            isPointerActive = false;
            if (debugPointer != null)
            {
                debugPointer.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 将鼠标屏幕坐标转换为世界坐标
    /// </summary>
    private Vector3 GetWorldPositionFromMouse(Vector3 mouseScreenPos)
    {
        if (mainCamera == null) return Vector3.zero;

        Ray ray = mainCamera.ScreenPointToRay(mouseScreenPos);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        
        if (groundPlane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }
        
        return Vector3.zero;
    }

    /// <summary>
    /// 响应手部检测状态变化
    /// </summary>
    private void HandleHandDetectionChanged(bool detected)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerMovement] 手部检测状态变更为 {(detected ? "检测到" : "未检测到")}");
        }
        
        if (!detected)
        {
            // 手势输入不可用时，重置悬停状态
            isHovering = false;
            
            // 如果没有鼠标输入，隐藏调试指针
            if (!enableMouseInput || !Input.GetMouseButton(0))
            {
                isPointerActive = false;
                if (debugPointer != null)
                {
                    debugPointer.SetActive(false);
                }
            }
        }
    }

    private void Update()
    {
        // 如果未初始化，尝试初始化
        if (!isInitialized)
        {
            InitializeComponents();
            if (!isInitialized) return;
        }

        // 根据InputManager的当前输入模式处理输入
        if (inputManager != null)
        {
            InputManager.InputMode currentMode = inputManager.GetCurrentInputMode();
            
            if (currentMode == InputManager.InputMode.Mouse && enableMouseInput)
            {
                // 鼠标输入模式
                HandleMouseInput();
            }
            else if (currentMode == InputManager.InputMode.Gesture && enableGestureInput)
            {
                // 手势输入模式 - 位置更新由HandleWorldPositionUpdated处理
                // 这里不需要额外处理
            }
        }

        // 仅当指针活跃时且不在悬停区域时处理移动
        if (!isPointerActive || (enableHoverStop && isHovering && enableGestureInput))
        {
            return;
        }

        // 处理移动
        HandleMovement(currentPointerPosition);

        // 处理朝向
        if (lockYRotation)
        {
            // 直接设置固定朝向
            transform.rotation = Quaternion.Euler(0, fixedYRotation, 0);
        }
        else if (facePointerDirection)
        {
            HandleRotation(currentPointerPosition);
        }
        
        if (enableDebugLogs && Time.frameCount % 60 == 0) // 每秒输出一次，避免刷屏
        {
            string currentMode = inputManager != null ? inputManager.GetCurrentInputMode().ToString() : "Unknown";
            Debug.Log($"[PlayerMovement] 状态: 输入模式={currentMode}, 指针激活={isPointerActive}, 悬停={isHovering}, 位置={currentPointerPosition}");
        }
    }

    private void OnDestroy()
    {
        // 取消事件订阅
        if (inputManager != null)
        {
            inputManager.OnInputModeChanged -= HandleInputModeChanged;
            
            if (enableGestureInput)
            {
                inputManager.OnWorldPositionUpdated -= HandleWorldPositionUpdated;
                inputManager.OnHandPositionUpdated -= HandleHandPositionUpdated;
                inputManager.OnHandDetectionChanged -= HandleHandDetectionChanged;
            }
        }

        // 清理调试对象
        if (debugPointer != null)
        {
            Destroy(debugPointer);
        }
    }

    /// <summary>
    /// 处理不同类型的移动
    /// </summary>
    private void HandleMovement(Vector3 targetWorldPosition)
    {
        // 如果目标位置无效，返回
        if (targetWorldPosition == Vector3.zero)
            return;

        // 根据设置的移动类型移动玩家
        switch (movementType)
        {
            case 0: // 即时移动到指针位置
                transform.position = new Vector3(
                    targetWorldPosition.x,
                    transform.position.y,
                    targetWorldPosition.z
                );
                break;

            case 1: // 平滑移动到指针位置
                targetPosition = new Vector3(
                    targetWorldPosition.x,
                    transform.position.y,
                    targetWorldPosition.z
                );
                transform.position = Vector3.SmoothDamp(
                    transform.position,
                    targetPosition,
                    ref currentVelocity,
                    smoothTime,
                    moveSpeed
                );
                break;

            case 2: // 朝指针方向移动
                Vector3 direction = targetWorldPosition - transform.position;
                direction.y = 0; // 确保在水平面上移动

                if (direction.magnitude > 0.1f) // 防止微小移动
                {
                    transform.position += direction.normalized * moveSpeed * Time.deltaTime;
                }
                break;
        }
    }

    /// <summary>
    /// 处理角色朝向
    /// </summary>
    private void HandleRotation(Vector3 targetWorldPosition)
    {
        if (lockYRotation)
        {
            // 锁定Y轴旋转，使用固定朝向
            transform.rotation = Quaternion.Euler(0, fixedYRotation, 0);
        }
        else
        {
            // 原有的面向指针逻辑
            Vector3 lookDirection = targetWorldPosition - transform.position;
            lookDirection.y = 0; // 防止在Y轴上旋转

            if (lookDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection);
            }
        }
    }

    /// <summary>
    /// 检查位置是否在悬停区域内
    /// </summary>
    private bool IsPositionInHoverArea(Vector2 normalizedPosition)
    {
        return normalizedPosition.x >= hoverMinThreshold.x &&
               normalizedPosition.x <= hoverMaxThreshold.x &&
               normalizedPosition.y >= hoverMinThreshold.y &&
               normalizedPosition.y <= hoverMaxThreshold.y;
    }

    // 在Unity编辑器中显示目标位置的可视化
    private void OnDrawGizmos()
    {
        if (Application.isPlaying && targetPosition != Vector3.zero)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(targetPosition, 0.5f);
        }

        if (Application.isPlaying && currentPointerPosition != Vector3.zero && isPointerActive)
        {
            Gizmos.color = isHovering ? hoverDebugColor : Color.yellow;
            Gizmos.DrawWireSphere(currentPointerPosition, 0.3f);
        }

        // 可视化悬停区域
        if (enableHoverStop && mainCamera != null && enableGestureInput)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // 半透明橙色

            // 计算悬停区域的四个角
            Vector2 screenMin = new Vector2(
                hoverMinThreshold.x * Screen.width,
                hoverMinThreshold.y * Screen.height
            );
            Vector2 screenMax = new Vector2(
                hoverMaxThreshold.x * Screen.width,
                hoverMaxThreshold.y * Screen.height
            );

            // 将四个角转换为世界坐标
            Vector3 worldBL = GetWorldPositionFromScreenPoint(screenMin);
            Vector3 worldTL = GetWorldPositionFromScreenPoint(new Vector2(screenMin.x, screenMax.y));
            Vector3 worldTR = GetWorldPositionFromScreenPoint(screenMax);
            Vector3 worldBR = GetWorldPositionFromScreenPoint(new Vector2(screenMax.x, screenMin.y));

            // 绘制悬停区域
            Gizmos.DrawLine(worldBL, worldTL);
            Gizmos.DrawLine(worldTL, worldTR);
            Gizmos.DrawLine(worldTR, worldBR);
            Gizmos.DrawLine(worldBR, worldBL);
        }
    }

    /// <summary>
    /// 辅助方法：将屏幕坐标转换为世界坐标（用于Gizmos）
    /// </summary>
    private Vector3 GetWorldPositionFromScreenPoint(Vector2 screenPoint)
    {
        if (mainCamera == null) return Vector3.zero;

        Ray ray = mainCamera.ScreenPointToRay(new Vector3(screenPoint.x, screenPoint.y, 0));
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        
        if (groundPlane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }

        return Vector3.zero;
    }

    // 公共方法：获取当前悬停状态
    public bool IsHovering()
    {
        return isHovering && enableHoverStop && enableGestureInput;
    }

    // 公共方法：启用/禁用输入类型
    public void SetMouseInputEnabled(bool enabled)
    {
        enableMouseInput = enabled;
    }

    public void SetGestureInputEnabled(bool enabled)
    {
        enableGestureInput = enabled;
        
        if (!enabled)
        {
            isHovering = false;
            if (inputManager != null)
            {
                inputManager.OnWorldPositionUpdated -= HandleWorldPositionUpdated;
                inputManager.OnHandPositionUpdated -= HandleHandPositionUpdated;
                inputManager.OnHandDetectionChanged -= HandleHandDetectionChanged;
            }
        }
        else if (inputManager != null)
        {
            inputManager.OnWorldPositionUpdated += HandleWorldPositionUpdated;
            inputManager.OnHandPositionUpdated += HandleHandPositionUpdated;
            inputManager.OnHandDetectionChanged += HandleHandDetectionChanged;
        }
    }

    // 公共方法：调试控制
    public void SetDebugLogsEnabled(bool enabled)
    {
        enableDebugLogs = enabled;
        Debug.Log($"[PlayerMovement] 调试日志已{(enabled ? "启用" : "禁用")}");
    }

    // 公共方法：获取当前状态信息
    public string GetCurrentStatusInfo()
    {
        string currentMode = inputManager != null ? inputManager.GetCurrentInputMode().ToString() : "Unknown";
        return $"输入模式={currentMode}, 指针激活={isPointerActive}, 悬停={isHovering}, " +
               $"手势检测={inputManager?.IsHandDetected()}, 鼠标输入={enableMouseInput}, 手势输入={enableGestureInput}, 位置={currentPointerPosition}";
    }

    // 公共方法：一键启用所有调试信息
    [ContextMenu("启用所有调试")]
    public void EnableAllDebugging()
    {
        enableDebugLogs = true;
        if (inputManager != null)
        {
            inputManager.SetDebugLogsEnabled(true);
        }
        
        Debug.Log("[PlayerMovement] 已启用所有调试信息");
        Debug.Log($"[PlayerMovement] 当前状态: {GetCurrentStatusInfo()}");
    }

    [ContextMenu("禁用所有调试")]
    public void DisableAllDebugging()
    {
        enableDebugLogs = false;
        if (inputManager != null)
        {
            inputManager.SetDebugLogsEnabled(false);
        }
        
        Debug.Log("[PlayerMovement] 已禁用所有调试信息");
    }
}