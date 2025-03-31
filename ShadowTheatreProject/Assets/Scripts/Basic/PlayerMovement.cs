using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float smoothTime = 0.1f;
    [Tooltip("是否让角色立即面向指针方向")]
    [SerializeField] private bool facePointerDirection = true;
    [Tooltip("选择移动方式：0=即时移动到指针位置，1=平滑移动到指针位置，2=朝指针方向移动")]
    [SerializeField] private int movementType = 1;
    
    // 简化内部变量
    private Vector3 currentVelocity = Vector3.zero;
    private Vector3 targetPosition;
    private InputManager inputManager;

    private void Start()
    {
        // 获取InputManager实例
        inputManager = InputManager.Instance;
        
        // 更新地面平面高度
        if (inputManager != null)
        {
            inputManager.UpdateGroundPlane(transform.position.y);
        }
    }

    private void Update()
    {
        if (inputManager == null)
        {
            inputManager = InputManager.Instance;
            if (inputManager == null) return;
        }
        
        // 仅当指针活跃时才处理移动
        if (!inputManager.IsPointerActive())
            return;
            
        // 获取指针在世界空间中的位置
        Vector3 pointerWorldPosition = inputManager.GetPointerWorldPosition();
        
        // 如果获取到的位置无效，返回
        if (pointerWorldPosition == Vector3.zero)
            return;
            
        // 根据设置的移动类型移动玩家
        switch (movementType)
        {
            case 0: // 即时移动到指针位置
                transform.position = new Vector3(pointerWorldPosition.x, transform.position.y, pointerWorldPosition.z);
                break;
                
            case 1: // 平滑移动到指针位置
                targetPosition = new Vector3(pointerWorldPosition.x, transform.position.y, pointerWorldPosition.z);
                transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, smoothTime, moveSpeed);
                break;
                
            case 2: // 朝指针方向移动
                Vector3 direction = pointerWorldPosition - transform.position;
                direction.y = 0; // 确保在水平面上移动
                
                if (direction.magnitude > 0.1f) // 防止微小移动
                {
                    transform.position += direction.normalized * moveSpeed * Time.deltaTime;
                }
                break;
        }
        
        // 如果需要面向指针方向
        if (facePointerDirection)
        {
            Vector3 lookDirection = pointerWorldPosition - transform.position;
            lookDirection.y = 0; // 防止在Y轴上旋转
            
            if (lookDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection);
            }
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