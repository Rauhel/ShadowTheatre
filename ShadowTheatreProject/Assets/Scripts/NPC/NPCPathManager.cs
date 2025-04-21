// 文件: NPCPathManager.cs
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NPCController))]
public class NPCPathManager : MonoBehaviour
{
    // 内部状态
    private NPCController controller;
    private Transform currentPath;
    private int currentPathPoint = 0;
    private bool pathProcessingPaused = false;

    // 公共属性
    public Transform CurrentPath => currentPath;
    public bool IsFollowingPath => currentPath != null && !pathProcessingPaused;

    void Awake()
    {
        controller = GetComponent<NPCController>();
    }

    void Start()
    {
        // 初始化路径 - 注意现在只从NPCData获取数据
        InitializeStartingPath();
    }

    void Update()
    {
        if (pathProcessingPaused || controller.Data == null)
            return;

        // 检查路径分歧点
        CheckPathDecisions();

        // 路径移动
        FollowCurrentPath();
    }

    // 路径初始化 - 只从NPCData获取数据
    private void InitializeStartingPath()
    {
        if (controller.Data == null)
        {
            Debug.LogError($"[{gameObject.name}] 没有设置NPCData，无法初始化路径");
            return;
        }

        // 从NPCData获取初始路径配置
        if (controller.Data.pathConnections != null && controller.Data.pathConnections.Count > 0)
        {
            // 使用第一个有效路径作为起始路径
            foreach (var connection in controller.Data.pathConnections)
            {
                if (connection.path != null)
                {
                    Debug.Log($"[{gameObject.name}] 使用NPCData中的路径初始化: {connection.path.name}");
                    SwitchToPath(connection.path);
                    return;
                }
            }
        }

        Debug.LogWarning($"[{gameObject.name}] NPCData中没有找到有效的初始路径，NPC将不会移动");
    }

    // 路径跟随逻辑
    private void FollowCurrentPath()
    {
        if (currentPath == null || currentPathPoint >= currentPath.childCount)
            return;

        // 获取当前路径点
        Transform targetPoint = currentPath.GetChild(currentPathPoint);
        NavMeshAgent agent = controller.GetComponent<NavMeshAgent>();

        // 设置目标
        agent.SetDestination(targetPoint.position);

        // 检查是否到达当前点
        float distanceToTarget = Vector3.Distance(transform.position, targetPoint.position);
        if (distanceToTarget < 0.5f)
        {
            // 前往下一个点
            currentPathPoint++;

            // 检查是否完成整个路径
            if (currentPathPoint >= currentPath.childCount)
            {
                Debug.Log($"[{gameObject.name}] 完成路径: {currentPath.name}");
                ProcessPathCompletion();
            }
        }
    }

    // 处理路径完成
    private void ProcessPathCompletion()
    {
        // 尝试获取当前路径的父对象（应该是MultiPointPathCreator）
        Transform pathRoot = currentPath.parent;

        // 检查是否有路径连接信息
        if (controller.Data != null && pathRoot != null)
        {
            PathConnection connection = FindPathConnection(pathRoot);
            if (connection != null)
            {
                if (connection.isEndPoint)
                {
                    // 路径终点，停止移动
                    Debug.Log($"[{gameObject.name}] 到达路径终点");
                    currentPath = null;
                    controller.StopMovement(true);
                }
                else if (connection.branchType == PathBranchType.ScoreBased)
                {
                    // 根据分数选择下一条路径
                    Transform nextPath = connection.SelectNextPathBasedOnScore(controller.Data.currentScore);
                    if (nextPath != null)
                    {
                        SwitchToPath(nextPath);
                    }
                    else
                    {
                        Debug.LogWarning($"[{gameObject.name}] 根据分数无法找到有效路径");
                        controller.StopMovement(true);
                    }
                }
                else if (connection.branchType == PathBranchType.Direct && connection.nextPathCreator != null)
                {
                    // 直接切换到下一路径
                    SwitchToPath(connection.nextPath);
                }
                else
                {
                    Debug.LogWarning($"[{gameObject.name}] 路径 {pathRoot.name} 没有设置下一个路径");
                    controller.StopMovement(true);
                }
            }
            else
            {
                Debug.LogWarning($"[{gameObject.name}] 无法找到路径 {pathRoot.name} 的连接信息");
                controller.StopMovement(true);
            }
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] 路径数据不完整，无法确定下一步");
            controller.StopMovement(true);
        }
    }

    // 检查路径决策点
    private void CheckPathDecisions()
    {
        if (currentPath == null || controller.Data.pathDecisions.Count == 0)
            return;

        foreach (var decision in controller.Data.pathDecisions)
        {
            if (decision.decisionLocation == null)
                continue;

            float distance = Vector3.Distance(transform.position, decision.decisionLocation.position);

            if (distance <= decision.decisionRadius)
            {
                // 到达决策点，根据分数决定路径
                Transform newPath = decision.SelectPathBasedOnScore(controller.Data.currentScore);

                if (newPath != null)
                {
                    // 找到匹配的路径选项用于日志输出
                    string pathDescription = "未知路径";
                    foreach (var option in decision.pathOptions)
                    {
                        if (option.path == newPath)
                        {
                            pathDescription = $"{option.optionName} (阈值:{option.scoreThreshold})";
                            break;
                        }
                    }

                    Debug.Log($"[{gameObject.name}] 在决策点 {decision.decisionPointID} 选择路径: {pathDescription}");
                    SwitchToPath(newPath);
                }

                break;
            }
        }
    }

    // 路径连接查询
    private PathConnection FindPathConnection(Transform path)
    {
        if (controller.Data == null || path == null)
            return null;

        foreach (var connection in controller.Data.pathConnections)
        {
            // 直接尝试匹配 Transform
            if (connection.path == path)
                return connection;

            // 如果存在路径创建器，尝试匹配其 Transform
            if (connection.pathCreator != null && connection.pathCreator.transform == path)
                return connection;

            // 查找路径点的父路径
            if (path.parent != null && path.parent.parent == connection.path)
                return connection;
        }

        return null;
    }

    // 路径切换
    public void SwitchToPath(Transform newPath)
    {
        if (newPath == null)
            return;

        MultiPointPathCreator pathCreator = newPath.GetComponent<MultiPointPathCreator>();
        if (pathCreator == null || pathCreator.pathPointsParent == null ||
            pathCreator.pathPointsParent.childCount == 0)
        {
            Debug.LogWarning($"[{gameObject.name}] 无法切换到路径: {newPath.name}，该路径没有路径点");
            return;
        }

        Debug.Log($"[{gameObject.name}] 切换到路径: {newPath.name}");

        // 查找前一条路径和当前路径的连接点
        Transform previousEndPoint = null;
        if (currentPath != null && currentPath.childCount > 0)
        {
            // 使用当前路径的最后一个点作为前一条路径的终点
            previousEndPoint = currentPath.GetChild(currentPath.childCount - 1);
        }

        // 设置新路径
        currentPath = pathCreator.pathPointsParent;

        // 如果有指定起点，使用指定起点，否则默认从第一个点开始
        currentPathPoint = 0;

        // 计算从哪个路径点开始最合适（找到距离前一条路径终点最近的点）
        if (previousEndPoint != null && currentPath.childCount > 1)
        {
            float minDistance = float.MaxValue;
            int closestPointIndex = 0;

            for (int i = 0; i < currentPath.childCount; i++)
            {
                float dist = Vector3.Distance(previousEndPoint.position, currentPath.GetChild(i).position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closestPointIndex = i;
                }
            }

            // 如果最近的点不是第一个点，且距离很近，则从该点开始
            if (closestPointIndex > 0 && minDistance < 3f)
            {
                currentPathPoint = closestPointIndex;
                Debug.Log($"[{gameObject.name}] 从路径点 {closestPointIndex} 开始跟随新路径（与前一路径终点距离: {minDistance:F2}）");
            }
        }

        // 设置当前目标点
        if (currentPath.childCount > currentPathPoint)
        {
            controller.GetComponent<NavMeshAgent>().SetDestination(currentPath.GetChild(currentPathPoint).position);
            controller.StopMovement(false); // 确保NPC开始移动
        }
    }

    // 暂停/恢复路径处理
    public void PausePathProcessing(bool pause)
    {
        pathProcessingPaused = pause;
        controller.StopMovement(pause);
    }

    // 返回路径处理
    public void ResumePathProcessing()
    {
        PausePathProcessing(false);
    }

    // 临时路径
    public IEnumerator FollowTemporaryPath(Transform tempPath)
    {
        if (tempPath == null)
            yield break;

        // 备份当前路径状态
        Transform originalPath = currentPath;
        int originalPathPoint = currentPathPoint;
        bool wasProcessingPaused = pathProcessingPaused;

        // 暂停常规路径处理
        PausePathProcessing(true);

        // 切换到临时路径
        SwitchToPath(tempPath);
        PausePathProcessing(false);  // 允许移动

        // 等待路径完成
        while (currentPath != null && currentPathPoint < currentPath.childCount)
        {
            yield return null;
        }

        // 恢复原来的路径状态
        currentPath = originalPath;
        currentPathPoint = originalPathPoint;
        PausePathProcessing(wasProcessingPaused);

        if (currentPath != null && currentPath.childCount > currentPathPoint)
        {
            controller.GetComponent<NavMeshAgent>().SetDestination(currentPath.GetChild(currentPathPoint).position);
        }
    }

    // 强制使用特定路径（用于调试或外部调用）
    public void ForceSwitchToPathByIndex(int pathIndex)
    {
        if (controller.Data == null || controller.Data.pathConnections == null ||
            pathIndex < 0 || pathIndex >= controller.Data.pathConnections.Count)
        {
            Debug.LogError($"[{gameObject.name}] 无效的路径索引: {pathIndex}");
            return;
        }

        var connection = controller.Data.pathConnections[pathIndex];
        if (connection.path != null)
        {
            Debug.Log($"[{gameObject.name}] 强制切换到路径索引 {pathIndex}: {connection.path.name}");
            SwitchToPath(connection.path);
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] 路径索引 {pathIndex} 引用了空路径");
        }
    }
}