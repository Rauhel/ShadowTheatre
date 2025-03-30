using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SceneTrigger : MonoBehaviour
{
    [Header("场景设置")]
    [Tooltip("要加载的场景名称")]
    [SerializeField] private string targetSceneName;

    [Header("交互设置")]
    [Tooltip("使用自定义设置（否则使用SceneLoadManager中的默认设置）")]
    [SerializeField] private bool useCustomSettings = false;

    [Tooltip("是否在进入触发器时自动加载")]
    [SerializeField] private bool loadOnTriggerEnter = true;

    [Tooltip("是否需要按键激活")]
    [SerializeField] private bool requireKeyPress = false;

    [Tooltip("激活按键")]
    [SerializeField] private KeyCode activationKey = KeyCode.E;

    [Tooltip("交互提示文本")]
    [SerializeField] private string promptText = "按 E 键进入下一个场景";

    private bool playerInTrigger = false;

    private void Awake()
    {
        // 确保碰撞体是触发器
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        Debug.Log($"SceneTrigger初始化 - 目标场景: {targetSceneName}, 自动加载: {loadOnTriggerEnter}, 需要按键: {requireKeyPress}");
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"触发器检测到对象: {other.name}, 标签: {other.tag}");

        if (other.CompareTag("Player"))
        {
            Debug.Log("检测到玩家进入触发器");
            playerInTrigger = true;

            if (SceneLoadManager.Instance == null)
            {
                Debug.LogError("SceneLoadManager实例未找到!");
                return;
            }

            Debug.Log($"调用Manager.HandleTriggerEnter - 场景: {targetSceneName}, 自定义设置: {useCustomSettings}, 自动加载: {loadOnTriggerEnter}, 需要按键: {requireKeyPress}");

            // 使用管理器的方法处理触发器进入事件
            SceneLoadManager.Instance.HandleTriggerEnter(
                targetSceneName,
                useCustomSettings,
                loadOnTriggerEnter,
                requireKeyPress,
                promptText
            );
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("玩家离开触发器");
            playerInTrigger = false;

            if (SceneLoadManager.Instance == null)
            {
                Debug.LogError("SceneLoadManager实例未找到!");
                return;
            }

            // 使用管理器处理离开触发器
            SceneLoadManager.Instance.HandleTriggerExit();
        }
    }

    private void Update()
    {
        // 检查玩家按键
        if (playerInTrigger && requireKeyPress && useCustomSettings)
        {
            if (Input.GetKeyDown(activationKey))
            {
                Debug.Log($"检测到玩家按下 {activationKey} 键");

                if (SceneLoadManager.Instance != null)
                {
                    Debug.Log($"通过按键调用加载场景: {targetSceneName}");
                    SceneLoadManager.Instance.LoadScene(targetSceneName);
                }
                else
                {
                    Debug.LogError("按键时未找到SceneLoadManager实例!");
                }
            }
        }
    }

    /// <summary>
    /// 设置目标场景（可通过其他脚本动态设置）
    /// </summary>
    public void SetTargetScene(string sceneName)
    {
        Debug.Log($"更改目标场景: {targetSceneName} -> {sceneName}");
        targetSceneName = sceneName;
    }

    // 可视化触发区域
    private void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            // 设置半透明绿色
            Gizmos.color = new Color(0, 1, 0.5f, 0.3f);

            // 根据碰撞体类型绘制不同形状
            if (col is BoxCollider boxCol)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(boxCol.center, boxCol.size);
            }
            else if (col is SphereCollider sphereCol)
            {
                Gizmos.DrawSphere(transform.position + sphereCol.center, sphereCol.radius);
            }

#if UNITY_EDITOR
            // 在编辑器中显示目标场景名称和使用的交互设置
            string settingsInfo = useCustomSettings ? "自定义设置" : "默认设置";
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, 
                $"→ {(string.IsNullOrEmpty(targetSceneName) ? "未设置场景" : targetSceneName)} ({settingsInfo})");
#endif
        }
    }
}