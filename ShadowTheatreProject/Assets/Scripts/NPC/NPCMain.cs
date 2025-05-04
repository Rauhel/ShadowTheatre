// 文件: NPCMain.cs
using UnityEngine;

[RequireComponent(typeof(NPCController))]
[RequireComponent(typeof(NPCPathManager))]
[RequireComponent(typeof(NPCEventManager))]
[RequireComponent(typeof(NPCDialogueManager))]
[RequireComponent(typeof(NPCAnimationManager))] // 添加动画管理器需求
[RequireComponent(typeof(WorldSpaceUIElement))] // 添加世界空间UI元素需求
[RequireComponent(typeof(GestureEventHandler))] // 添加手势事件处理器需求
public class NPCMain : MonoBehaviour
{
    // 组件引用
    private NPCController controller;
    private NPCPathManager pathManager;
    private NPCEventManager eventManager;
    private NPCDialogueManager dialogueManager;
    private NPCAnimationManager animationManager; // 添加动画管理器引用
    private WorldSpaceUIElement worldSpaceUIElement; // 添加世界空间UI元素引用
    private GestureEventHandler gestureEventHandler; // 添加手势事件处理器引用

    void Awake()
    {
        // 获取所有组件
        controller = GetComponent<NPCController>();
        pathManager = GetComponent<NPCPathManager>();
        eventManager = GetComponent<NPCEventManager>();
        dialogueManager = GetComponent<NPCDialogueManager>();
        animationManager = GetComponent<NPCAnimationManager>(); // 初始化动画管理器
        worldSpaceUIElement = GetComponent<WorldSpaceUIElement>(); // 初始化世界空间UI元素
        gestureEventHandler = GetComponent<GestureEventHandler>(); // 初始化手势事件处理器

        // 确保所有组件都存在
        if (controller == null)
        {
            Debug.LogError($"[{gameObject.name}] 缺少 NPCController 组件");
        }

        if (pathManager == null)
        {
            Debug.LogError($"[{gameObject.name}] 缺少 NPCPathManager 组件");
        }

        if (eventManager == null)
        {
            Debug.LogError($"[{gameObject.name}] 缺少 NPCEventManager 组件");
        }

        if (dialogueManager == null)
        {
            Debug.LogError($"[{gameObject.name}] 缺少 NPCDialogueManager 组件");
        }

        if (animationManager == null)
        {
            Debug.LogError($"[{gameObject.name}] 缺少 NPCAnimationManager 组件");
        }

        if (worldSpaceUIElement == null)
        {
            Debug.LogError($"[{gameObject.name}] 缺少 WorldSpaceUIElement 组件");
        }
    }

    // 提供公共接口用于其他系统交互
    public void TriggerEvent(string eventID)
    {
        if (eventManager != null)
        {
            eventManager.TriggerEventByID(eventID);
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] 无法触发事件：缺少 NPCEventManager 组件");
        }
    }

    public void SwitchToPath(string pathID)
    {
        pathManager.SwitchToPathByID(pathID);
    }

    public void UpdateScore(float amount)
    {
        controller.AdjustScore(amount); // 使用新的方法名
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

    // 添加播放动画的公共接口
    public void PlayAnimation(string animName, bool loop = false)
    {
        if (animationManager != null)
        {
            animationManager.PlayAnimation(animName, loop);
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] 无法播放动画：缺少 NPCAnimationManager 组件");
        }
    }
}