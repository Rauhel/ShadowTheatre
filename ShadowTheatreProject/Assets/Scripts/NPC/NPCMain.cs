// 文件: NPCMain.cs
using UnityEngine;

[RequireComponent(typeof(NPCController))]
[RequireComponent(typeof(NPCPathManager))]
[RequireComponent(typeof(NPCEventManager))]
public class NPCMain : MonoBehaviour
{
    // 组件引用
    private NPCController controller;
    private NPCPathManager pathManager;
    private NPCEventManager eventManager;

    void Awake()
    {
        // 获取所有组件
        controller = GetComponent<NPCController>();
        pathManager = GetComponent<NPCPathManager>();
        eventManager = GetComponent<NPCEventManager>();
    }

    // 提供公共接口用于其他系统交互
    public void TriggerEvent(string eventID)
    {
        eventManager.TriggerEventByID(eventID);
    }

    public void SwitchToPath(string pathID)
    {
        pathManager.SwitchToPathByID(pathID);
    }

    public void UpdateScore(float amount)
    {
        controller.UpdateScore(amount);
    }

    public void StopNPC(bool stop)
    {
        pathManager.PausePathProcessing(stop);
    }

    public float GetCurrentScore()
    {
        return controller.Data?.currentScore ?? 0;
    }

    // 根据路径索引切换路径
    public void SwitchToPathByIndex(int pathIndex)
    {
        if (controller == null || controller.Data == null ||
            controller.Data.pathConnections == null ||
            pathIndex < 0 || pathIndex >= controller.Data.pathConnections.Count)
            return;

        var connection = controller.Data.pathConnections[pathIndex];
        if (connection.path != null)
        {
            MultiPointPathCreator pathCreator = connection.path.GetComponent<MultiPointPathCreator>();
            if (pathCreator != null)
            {
                pathManager.SwitchToPath(pathCreator);
            }
        }
    }

    // 调试工具
    public void DebugNPCStatus()
    {
        controller.DebugStatus();
        Debug.Log($"[{gameObject.name}] 当前路径: {pathManager.CurrentPathID}");
        Debug.Log($"[{gameObject.name}] 路径状态: {(pathManager.IsFollowingPath ? "正在跟随" : "已暂停或无路径")}");

        // 获取当前路径配置
        var currentPathConfig = pathManager.GetCurrentPathConfig();
        if (currentPathConfig != null)
        {
            Debug.Log($"[{gameObject.name}] 当前路径: {currentPathConfig.pathName} (ID: {currentPathConfig.pathID})");
            Debug.Log($"[{gameObject.name}] 分数范围: {currentPathConfig.minScore} - {currentPathConfig.maxScore}");
            Debug.Log($"[{gameObject.name}] 事件数量: {currentPathConfig.events.Count}");
            Debug.Log($"[{gameObject.name}] 下一路径分支数: {currentPathConfig.nextPaths.Count}");
        }
    }

    public NPCData GetNPCData()
    {
        // 从 Controller 获取 NPCData
        if (controller != null)
        {
            return controller.Data;
        }

        // 如果 controller 未初始化，尝试获取
        if (controller == null)
        {
            controller = GetComponent<NPCController>();
            if (controller != null)
            {
                return controller.Data;
            }
        }

        Debug.LogWarning($"[{gameObject.name}] 无法获取 NPCData，NPCController 可能未设置或未初始化");
        return null;
    }
}