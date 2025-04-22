using UnityEngine;
using System.Collections;

/// <summary>
/// 玩家移动组件：负责处理玩家移动逻辑和手势位置转换
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float smoothTime = 0.1f;
    [Tooltip("是否让角色立即面向指针方向")]
    [SerializeField] private bool facePointerDirection = true;
    [Tooltip("选择移动方式：0=即时移动到指针位置，1=平滑移动到指针位置，2=朝指针方向移动")]
    [SerializeField] private int movementType = 1;

    [Header("Pointer Settings")]
    [SerializeField] private float pointerGroundHeight = 0f;
    [SerializeField] private bool showDebugPointer = true;

    // 引用和内部变量
    private Vector3 currentVelocity = Vector3.zero;
    private Vector3 targetPosition;
    private Vector3 currentPointerPosition;
    private bool isPointerActive = false;

    private InputManager inputManager;
    private Camera mainCamera;
    private Plane groundPlane;
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
            // 初始化地面平面
            groundPlane = new Plane(Vector3.up, new Vector3(0, pointerGroundHeight, 0));

            // 订阅手势位置事件
            inputManager.OnHandPositionUpdated += HandleHandPositionUpdated;
            inputManager.OnHandDetectionChanged += HandleHandDetectionChanged;

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
        debugPointer.name = "HandPointer_Debug";
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
    /// 处理手部位置更新
    /// </summary>
    // 在PlayerMovement.cs的HandleHandPositionUpdated方法中
    private void HandleHandPositionUpdated(Vector2 normalizedPosition)
    {
        // normalizedPosition现在已经在0.2-0.8范围内

        // 转换为屏幕坐标
        Vector2 screenPosition = new Vector2(
            normalizedPosition.x * Screen.width,
            normalizedPosition.y * Screen.height
        );

        // 转换为世界坐标
        UpdatePointerWorldPosition(screenPosition);

        // 设置指针状态为活跃
        isPointerActive = true;

        // 可视化帮助调试
        //Debug.Log($"手部位置: 归一化={normalizedPosition}, 屏幕={screenPosition}, 世界={currentPointerPosition}");
    }

    /// <summary>
    /// 响应手部检测状态变化
    /// </summary>
    private void HandleHandDetectionChanged(bool detected)
    {
        Debug.Log($"PlayerMovement: 手部检测状态变更为 {(detected ? "检测到" : "未检测到")}");
        isPointerActive = detected;

        // 如果没有检测到手，隐藏调试指针
        if (!detected && debugPointer != null)
        {
            debugPointer.SetActive(false);
        }
    }

    /// <summary>
    /// 更新指针的世界坐标位置
    /// </summary>
    private void UpdatePointerWorldPosition(Vector2 screenPosition)
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        // 创建从屏幕点到世界的射线
        Ray ray = mainCamera.ScreenPointToRay(new Vector3(screenPosition.x, screenPosition.y, 0));

        // 尝试与地面平面相交
        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 hitPoint = ray.GetPoint(distance);
            currentPointerPosition = hitPoint;

            // 更新调试指针位置
            if (debugPointer != null && isPointerActive)
            {
                debugPointer.transform.position = hitPoint;
                debugPointer.SetActive(true);
            }

            //Debug.Log($"PlayerMovement: 指针位置更新 - 屏幕={screenPosition}, 世界={hitPoint}");
        }
        else
        {
            // 备用方案：使用固定Y值
            float t = (pointerGroundHeight - ray.origin.y) / ray.direction.y;
            if (t > 0)
            {
                Vector3 hitPoint = ray.origin + ray.direction * t;
                currentPointerPosition = hitPoint;

                // 更新调试指针位置
                if (debugPointer != null && isPointerActive)
                {
                    debugPointer.transform.position = hitPoint;
                    debugPointer.SetActive(true);
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

        // 仅当指针活跃时处理移动
        if (!isPointerActive) // 使用自己的 isPointerActive 标志，而不是调用 InputManager.IsPointerActive()
        {
            return;
        }

        // 处理移动
        HandleMovement(currentPointerPosition);

        // 处理朝向
        if (facePointerDirection)
        {
            HandleRotation(currentPointerPosition);
        }
    }

    private void OnDestroy()
    {
        // 取消事件订阅
        if (inputManager != null)
        {
            inputManager.OnHandPositionUpdated -= HandleHandPositionUpdated;
            inputManager.OnHandDetectionChanged -= HandleHandDetectionChanged;
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
        Vector3 lookDirection = targetWorldPosition - transform.position;
        lookDirection.y = 0; // 防止在Y轴上旋转

        if (lookDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(lookDirection);
        }
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
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(currentPointerPosition, 0.3f);
        }
    }

    // 公共方法：更新地面平面高度
    public void UpdateGroundPlaneHeight(float height)
    {
        pointerGroundHeight = height;
        groundPlane = new Plane(Vector3.up, new Vector3(0, height, 0));
    }
}