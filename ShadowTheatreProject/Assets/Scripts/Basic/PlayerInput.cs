using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(CharacterController))]
public class PlayerInput : MonoBehaviour
{
    [Header("移动参数")]
    public float moveSpeed = 5f;          // 移动速度
    public float acceleration = 8f;        // 加速度
    public float deceleration = 10f;       // 减速度
    public float rotationSpeed = 10f;      // 旋转速度

    [Header("输入控制")]
    public bool movementEnabled = true;    // 移动是否启用

    [Header("点击事件")]
    public bool clickEnabled = true;       // 点击事件是否启用
    public UnityEvent onMouseClick;        // 鼠标点击事件

    // 组件引用
    private CharacterController controller;
    private Camera mainCamera;

    // 运动状态
    private Vector3 currentVelocity = Vector3.zero;
    private Vector3 targetVelocity = Vector3.zero;

    private void Start()
    {
        // 获取组件引用
        controller = GetComponent<CharacterController>();
        mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogWarning("找不到主摄像机，将使用世界坐标系作为移动参考");
        }

        // 初始化事件
        if (onMouseClick == null)
        {
            onMouseClick = new UnityEvent();
        }
    }

    private void Update()
    {
        // 处理移动输入
        HandleMovementInput();

        // 处理点击输入
        HandleClickInput();
    }

    private void HandleMovementInput()
    {
        if (!movementEnabled)
        {
            // 如果移动被禁用，应用减速直到停止
            targetVelocity = Vector3.zero;
        }
        else
        {
            // 获取输入
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            // 根据相机方向确定移动方向
            Vector3 moveDirection = new Vector3(horizontal, 0, vertical);

            if (mainCamera != null)
            {
                // 获取相机前方向（忽略Y轴）
                Vector3 forward = mainCamera.transform.forward;
                forward.y = 0;
                forward.Normalize();

                // 获取相机右方向（忽略Y轴）
                Vector3 right = mainCamera.transform.right;
                right.y = 0;
                right.Normalize();

                // 根据相机方向转换输入
                moveDirection = right * horizontal + forward * vertical;
            }

            // 规范化输入方向（如果有输入）
            if (moveDirection.magnitude > 0.1f)
            {
                moveDirection.Normalize();

                // 计算目标速度
                targetVelocity = moveDirection * moveSpeed;

                // 根据移动方向旋转角色
                if (moveDirection != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }
            }
            else
            {
                // 无输入时，目标速度为零
                targetVelocity = Vector3.zero;
            }
        }

        // 应用加速度或减速度
        if (targetVelocity.magnitude > 0.1f)
        {
            // 加速
            currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, acceleration * Time.deltaTime);
        }
        else
        {
            // 减速
            currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, deceleration * Time.deltaTime);
        }

        // 移动角色
        if (currentVelocity.magnitude > 0.01f)
        {
            controller.Move(currentVelocity * Time.deltaTime);
        }
    }

    private void HandleClickInput()
    {
        if (clickEnabled && Input.GetMouseButtonDown(0))
        {
            onMouseClick.Invoke();
            Debug.Log("点击事件触发");
        }
    }

    // 公共方法 - 启用/禁用移动
    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
        if (!enabled)
        {
            // 立即停止移动
            currentVelocity = Vector3.zero;
            targetVelocity = Vector3.zero;
        }
    }

    // 公共方法 - 启用/禁用点击
    public void SetClickEnabled(bool enabled)
    {
        clickEnabled = enabled;
    }

    // 新增方法 - 启用/禁用键盘输入（实际上就是移动输入）
    public void SetKeyboardInputEnabled(bool enabled)
    {
        // 在这个简易实现中，键盘输入就等同于移动控制
        SetMovementEnabled(enabled);
    }

    // 新增方法 - 启用/禁用点击输入
    public void SetClickInputEnabled(bool enabled)
    {
        // 直接调用已有的方法
        SetClickEnabled(enabled);
    }

    // 公共方法 - 传送到指定位置
    public void TeleportTo(Vector3 position)
    {
        controller.enabled = false;  // 暂时禁用CharacterController
        transform.position = position;
        controller.enabled = true;   // 重新启用CharacterController
    }

    // 检查角色是否正在移动
    public bool IsMoving()
    {
        return currentVelocity.magnitude > 0.01f;
    }

    // 立即停止角色移动
    public void StopMovement()
    {
        currentVelocity = Vector3.zero;
        targetVelocity = Vector3.zero;
    }

    // 获取当前角色速度
    public Vector3 GetVelocity()
    {
        return currentVelocity;
    }

    // 施加外部移动力（例如被推动）
    public void ApplyExternalMovement(Vector3 movement)
    {
        controller.Move(movement * Time.deltaTime);
    }
}