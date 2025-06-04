using UnityEngine;

/// <summary>
/// 测试NPC停留配置的工具脚本
/// 用于在运行时为测试NPC添加路径点停留时间
/// </summary>
public class TestNPCStopConfiguration : MonoBehaviour
{
    [Header("测试配置")]
    public NPCController targetNPC;
    public bool applyTestConfiguration = false;
    
    [Header("路径点停留配置")]
    public int pathPointIndex = 1;
    public float stopTime = 3f;
    public string description = "测试停留";
    
    void Start()
    {
        if (applyTestConfiguration && targetNPC != null)
        {
            ApplyTestStopConfiguration();
        }
    }
    
    [ContextMenu("应用测试停留配置")]
    public void ApplyTestStopConfiguration()
    {
        if (targetNPC == null || targetNPC.Data == null)
        {
            Debug.LogError("测试配置失败：目标NPC或数据为空");
            return;
        }
        
        // 获取当前路径配置
        var pathManager = targetNPC.GetComponent<NPCPathManager>();
        if (pathManager == null)
        {
            Debug.LogError("测试配置失败：找不到NPCPathManager");
            return;
        }
        
        string currentPathID = pathManager.CurrentPathID;
        if (string.IsNullOrEmpty(currentPathID))
        {
            Debug.LogError("测试配置失败：当前路径ID为空");
            return;
        }
        
        // 查找当前路径配置
        PathConfig currentPath = targetNPC.Data.FindPathById(currentPathID);
        if (currentPath == null)
        {
            Debug.LogError($"测试配置失败：找不到路径配置 {currentPathID}");
            return;
        }
        
        // 初始化停留配置列表
        if (currentPath.pathPointStopConfigs == null)
        {
            currentPath.pathPointStopConfigs = new System.Collections.Generic.List<PathPointStopConfig>();
        }
        
        // 检查是否已经存在该路径点的配置
        var existingConfig = currentPath.pathPointStopConfigs.Find(config => config.pathPointIndex == pathPointIndex);
        if (existingConfig != null)
        {
            // 更新现有配置
            existingConfig.stopTime = stopTime;
            existingConfig.description = description;
            Debug.Log($"更新路径点{pathPointIndex}的停留配置：{stopTime}秒 - {description}");
        }
        else
        {
            // 添加新配置
            var newConfig = new PathPointStopConfig
            {
                pathPointIndex = pathPointIndex,
                stopTime = stopTime,
                description = description
            };
            currentPath.pathPointStopConfigs.Add(newConfig);
            Debug.Log($"为路径点{pathPointIndex}添加停留配置：{stopTime}秒 - {description}");
        }
        
        // 标记数据为脏，在编辑器模式下保存
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(targetNPC.Data);
#endif
    }
    
    [ContextMenu("清除测试停留配置")]
    public void ClearTestStopConfiguration()
    {
        if (targetNPC == null || targetNPC.Data == null)
        {
            Debug.LogError("清除配置失败：目标NPC或数据为空");
            return;
        }
        
        var pathManager = targetNPC.GetComponent<NPCPathManager>();
        if (pathManager == null) return;
        
        string currentPathID = pathManager.CurrentPathID;
        if (string.IsNullOrEmpty(currentPathID)) return;
        
        PathConfig currentPath = targetNPC.Data.FindPathById(currentPathID);
        if (currentPath == null) return;
        
        if (currentPath.pathPointStopConfigs != null)
        {
            currentPath.pathPointStopConfigs.RemoveAll(config => config.pathPointIndex == pathPointIndex);
            Debug.Log($"已清除路径点{pathPointIndex}的停留配置");
            
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(targetNPC.Data);
#endif
        }
    }
    
    void OnValidate()
    {
        // 确保停留时间不为负数
        if (stopTime < 0) stopTime = 0;
        if (pathPointIndex < 0) pathPointIndex = 0;
    }
} 