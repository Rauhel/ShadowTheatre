// 文件: NPCMain.cs
using System.Collections.Generic;
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

    public void SetPath(Transform path)
    {
        pathManager.SwitchToPath(path);
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

    // 调试用方法
    // 重写存在问题的代码块
    public void DebugNPCStatus()
    {
        controller.DebugStatus();
        Debug.Log($"[{gameObject.name}] 当前路径: {(pathManager.CurrentPath ? pathManager.CurrentPath.name : "无")}");
        Debug.Log($"[{gameObject.name}] 路径状态: {(pathManager.IsFollowingPath ? "正在跟随" : "已暂停或无路径")}");

        // 显示全部路径信息
        if (controller.Data != null && controller.Data.pathConnections != null)
        {
            Debug.Log($"[{gameObject.name}] NPCData路径连接数量: {controller.Data.pathConnections.Count}");
            for (int i = 0; i < controller.Data.pathConnections.Count; i++)
            {
                var connection = controller.Data.pathConnections[i];
                string nextPathInfo = connection.branchType == PathBranchType.Direct ?
                    (connection.nextPath ? connection.nextPath.name : "空") : "分数分支";
                string pathInfo = connection.path ? connection.path.name : "空";
                Debug.Log($"  路径 {i}: {pathInfo} -> {nextPathInfo}");
            }
        }
    }

    // 强制使用特定路径
    public void SwitchToPathByIndex(int index)
    {
        pathManager.ForceSwitchToPathByIndex(index);
    }
}