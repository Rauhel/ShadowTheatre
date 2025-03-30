using UnityEngine;

public class PalyerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float smoothTime = 0.1f;
    [Tooltip("是否让角色立即面向鼠标方向")]
    [SerializeField] private bool faceMouseDirection = true;
    [Tooltip("选择移动方式：0=即时移动到鼠标位置，1=平滑移动到鼠标位置，2=朝鼠标方向移动")]
    [SerializeField] private int movementType = 1;
    [Tooltip("如果使用相机平面投影，请设置为true")]
    [SerializeField] private bool useScreenPlane = true;
    
    // 内部变量
    private Camera mainCamera;
    private Vector3 currentVelocity = Vector3.zero;
    private Vector3 targetPosition;
    private Plane groundPlane;

    private void Start()
    {
        mainCamera = Camera.main;
        // 创建一个与Y轴对齐的平面(地面平面)
        groundPlane = new Plane(Vector3.up, Vector3.zero);
    }

    private void Update()
    {
        // 获取鼠标在世界空间中的位置
        Vector3 mouseWorldPosition = GetMouseWorldPosition();
        
        // 如果获取到的位置无效，返回
        if (mouseWorldPosition == Vector3.zero)
            return;
            
        // 根据设置的移动类型移动玩家
        switch (movementType)
        {
            case 0: // 即时移动到鼠标位置
                transform.position = new Vector3(mouseWorldPosition.x, transform.position.y, mouseWorldPosition.z);
                break;
                
            case 1: // 平滑移动到鼠标位置
                targetPosition = new Vector3(mouseWorldPosition.x, transform.position.y, mouseWorldPosition.z);
                transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, smoothTime, moveSpeed);
                break;
                
            case 2: // 朝鼠标方向移动
                Vector3 direction = mouseWorldPosition - transform.position;
                direction.y = 0; // 确保在水平面上移动
                
                if (direction.magnitude > 0.1f) // 防止微小移动
                {
                    transform.position += direction.normalized * moveSpeed * Time.deltaTime;
                }
                break;
        }
        
        // 如果需要面向鼠标方向
        if (faceMouseDirection)
        {
            Vector3 lookDirection = mouseWorldPosition - transform.position;
            lookDirection.y = 0; // 防止在Y轴上旋转
            
            if (lookDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection);
            }
        }
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePosition = Input.mousePosition;
        
        if (useScreenPlane)
        {
            // 从屏幕射线投影到地面平面
            Ray ray = mainCamera.ScreenPointToRay(mousePosition);
            if (groundPlane.Raycast(ray, out float distance))
            {
                return ray.GetPoint(distance);
            }
        }
        else
        {
            // 使用固定的Y坐标
            float depth = transform.position.y - mainCamera.transform.position.y;
            mousePosition.z = depth;
            return mainCamera.ScreenToWorldPoint(mousePosition);
        }
        
        return Vector3.zero;
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