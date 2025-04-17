using UnityEngine;

/// <summary>
/// 玩家移动组件：负责处理玩家的移动逻辑
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

    // 引用和内部变量
    private Vector3 currentVelocity = Vector3.zero;
    private Vector3 targetPosition;
    private InputManager inputManager;
    private bool isInitialized = false;

    private void Start()
    {
        InitializeComponents();
        // 注册手部检测状态变化事件
        if (inputManager != null)
        {
            inputManager.OnHandDetectionChanged += OnHandDetectionChanged;
        }
    }

    /// <summary>
    /// 初始化组件引用
    /// </summary>
    private void InitializeComponents()
    {
        // 获取InputManager实例
        inputManager = InputManager.Instance;

        if (inputManager != null)
        {
            // 更新地面平面高度
            inputManager.UpdateGroundPlane(transform.position.y);
            isInitialized = true;
        }
        else
        {
            Debug.LogWarning("PlayerMovement无法获取InputManager实例，将在Update中重试");
        }
    }

    /// <summary>
    /// 响应手部检测状态变化
    /// </summary>
    private void OnHandDetectionChanged(bool detected)
    {
        Debug.Log($"PlayerMovement: 手部检测状态变更为 {(detected ? "检测到" : "未检测到")}");
        // 不需要额外处理，Update方法会根据isPointerActive状态来决定是否移动
    }

    private void Update()
    {
        // 如果未初始化，尝试初始化
        if (!isInitialized)
        {
            InitializeComponents();
            if (!isInitialized) return;
        }

        // 仅当指针活跃时才处理移动（已包含手部检测状态逻辑）
        if (!inputManager.IsPointerActive())
            return;

        // 获取指针位置
        Vector3 pointerWorldPosition = inputManager.GetPointerWorldPosition();

        // 如果获取到的位置无效，返回
        if (pointerWorldPosition == Vector3.zero)
            return;

        // 处理移动
        HandleMovement(pointerWorldPosition);

        // 处理朝向
        if (facePointerDirection)
        {
            HandleRotation(pointerWorldPosition);
        }
    }

    private void OnDestroy()
    {
        // 取消注册事件
        if (inputManager != null)
        {
            inputManager.OnHandDetectionChanged -= OnHandDetectionChanged;
        }
    }

    /// <summary>
    /// 处理不同类型的移动
    /// </summary>
    private void HandleMovement(Vector3 targetWorldPosition)
    {
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
    }
}