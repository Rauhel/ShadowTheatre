using UnityEngine;

public class TriggerTest : MonoBehaviour
{
    private void Awake()
    {
        Debug.Log("TriggerTest 已初始化");
        // 检查组件配置
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Debug.Log($"找到碰撞体: {col.GetType().Name}, IsTrigger: {col.isTrigger}");
        }
        else
        {
            Debug.LogError("该对象没有碰撞体组件!");
        }
        
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            Debug.Log($"找到刚体组件, IsKinematic: {rb.isKinematic}");
        }
        else
        {
            Debug.Log("该对象没有刚体组件，建议添加");
        }
    }

    private void Start()
    {
        Debug.Log("TriggerTest 已启动");
        // 输出当前对象的层和标签
        Debug.Log($"对象信息 - Layer: {gameObject.layer} ({LayerMask.LayerToName(gameObject.layer)}), Tag: {gameObject.tag}");
    }

    private void Update()
    {
        if (Time.frameCount % 100 == 0) // 每100帧输出一次
        {
            Debug.Log("TriggerTest Update 被调用");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"TriggerTest 检测到对象: {other.name}，标签: {other.tag}");
    }
    
    // 添加这个方法检测碰撞
    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"检测到普通碰撞: {collision.gameObject.name}，标签: {collision.gameObject.tag}");
    }
}