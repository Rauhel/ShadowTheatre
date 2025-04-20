#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

// 负责路径可视化和图形绘制
public class PathGraphRenderer
{
    // 用于绘制的参数
    private float nodeSize = 30f;
    private float horizontalSpacing = 120f;
    private float verticalSpacing = 80f;
    private Dictionary<Transform, Rect> pathNodePositions = new Dictionary<Transform, Rect>();
    private Dictionary<Transform, List<Transform>> childPaths = new Dictionary<Transform, List<Transform>>();

    // 拖拽状态
    private bool isDragging = false;
    private Transform selectedPath = null;
    private Vector2 dragStartPosition;
    private Vector2 canvasOffset = Vector2.zero;
    private float zoomFactor = 1f;

    // 引用
    private NPCData npcData;

    // 属性
    public Transform SelectedPath => selectedPath;
    public bool IsDragging => isDragging;
    public float ZoomFactor 
    {
        get => zoomFactor;
        set => zoomFactor = value;
    }

    // 初始化
    public void SetNPCData(NPCData data)
    {
        npcData = data;
        ClearCache();
    }

    // 清除缓存数据
    public void ClearCache()
    {
        pathNodePositions.Clear();
        childPaths.Clear();
    }

    // 拖拽处理
    public void StartDrag(Vector2 mousePosition)
    {
        isDragging = true;
        dragStartPosition = mousePosition;
    }

    public void EndDrag()
    {
        isDragging = false;
    }

    public void UpdateDrag(Vector2 mousePosition)
    {
        if (selectedPath == null)
        {
            canvasOffset += (mousePosition - dragStartPosition) / zoomFactor;
            dragStartPosition = mousePosition;
        }
    }

    // 重置视图
    public void RecenterView()
    {
        if (pathNodePositions.Count > 0)
        {
            // 计算所有节点的中心
            Vector2 center = Vector2.zero;
            foreach (var rect in pathNodePositions.Values)
            {
                center += rect.center;
            }
            center /= pathNodePositions.Count;
            
            // 设置偏移量，使节点中心位于视图中心
            canvasOffset = Vector2.zero;
        }
    }

    // 主要绘制方法
    public void DrawPathVisualization(Rect canvasRect, NPCData data)
    {
        if (data == null)
        {
            EditorGUI.LabelField(canvasRect, "选择一个NPC数据资源以查看路径");
            return;
        }

        // 开始绘制区域
        GUI.Box(canvasRect, "");
        
        // 应用缩放和偏移
        GUI.BeginClip(canvasRect);
        
        Matrix4x4 oldMatrix = GUI.matrix;
        Matrix4x4 translation = Matrix4x4.TRS(canvasOffset, Quaternion.identity, Vector3.one * zoomFactor);
        GUI.matrix = translation;

        // 构建路径树
        if (pathNodePositions.Count == 0)
        {
            LayoutPaths();
        }

        // 绘制连接线
        DrawPathConnections();

        // 绘制路径节点
        DrawPathNodes(canvasRect);
        
        // 恢复矩阵
        GUI.matrix = oldMatrix;
        GUI.EndClip();
    }

    // 计算树宽度修复方法
    private float CalculateTreeWidth(Transform path)
    {
        if (path == null)
            return 0;
        
        float minWidth = 100f;  // 最小宽度
        
        if (childPaths.ContainsKey(path) && childPaths[path].Count > 0)
        {
            float totalWidth = 0;
            foreach (var childPath in childPaths[path])
            {
                totalWidth += CalculateTreeWidth(childPath);
            }
            return Mathf.Max(minWidth, totalWidth);
        }
        
        return minWidth;  // 确保所有路径都返回值
    }

    // 查找路径连接修复方法
    private PathConnection FindPathConnection(Transform path)
    {
        if (npcData == null || path == null)
            return null;

        foreach (var connection in npcData.pathConnections)
        {
            if (connection.path == path || 
                (connection.pathCreator != null && connection.pathCreator.transform == path))
                return connection;
        }

        return null;  // 确保所有路径都返回值
    }

    // 路径布局方法
    private void LayoutPaths()
    {
        pathNodePositions.Clear();
        childPaths.Clear();
        
        // 寻找没有前驱的路径（起始路径）
        List<Transform> rootPaths = new List<Transform>();
        
        if (npcData != null && npcData.pathConnections != null)
        {
            // 首先记录所有作为下一路径的路径
            HashSet<Transform> childPathSet = new HashSet<Transform>();
            
            foreach (var connection in npcData.pathConnections)
            {
                // 处理直接连接
                if (connection.path != null && connection.branchType == PathBranchType.Direct && connection.nextPathCreator != null)
                {
                    Transform nextPath = connection.nextPathCreator.transform;
                    childPathSet.Add(nextPath);
                    
                    if (!childPaths.ContainsKey(connection.path))
                    {
                        childPaths[connection.path] = new List<Transform>();
                    }
                    
                    childPaths[connection.path].Add(nextPath);
                }
                
                // 处理基于分数的分支
                if (connection.path != null && connection.branchType == PathBranchType.ScoreBased && connection.scoreOptions != null)
                {
                    foreach (var option in connection.scoreOptions)
                    {
                        if (option.pathCreator != null)
                        {
                            Transform optionPath = option.pathCreator.transform;
                            childPathSet.Add(optionPath);
                            
                            if (!childPaths.ContainsKey(connection.path))
                            {
                                childPaths[connection.path] = new List<Transform>();
                            }
                            
                            childPaths[connection.path].Add(optionPath);
                        }
                    }
                }
            }
            
            // 然后找出所有没有前驱的路径作为根路径
            foreach (var connection in npcData.pathConnections)
            {
                if (connection.path != null && !childPathSet.Contains(connection.path))
                {
                    rootPaths.Add(connection.path);
                }
            }
            
            // 如果没有找到根路径，尝试使用所有路径
            if (rootPaths.Count == 0)
            {
                foreach (var connection in npcData.pathConnections)
                {
                    if (connection.path != null && !rootPaths.Contains(connection.path))
                    {
                        rootPaths.Add(connection.path);
                    }
                }
            }
        }
        
        // 排列所有根路径
        float x = 0;
        float y = 0;
        
        foreach (var rootPath in rootPaths)
        {
            float treeWidth = CalculateTreeWidth(rootPath);
            LayoutPathTree(rootPath, x + treeWidth / 2, y, 0);
            x += treeWidth + horizontalSpacing;  // 使用 horizontalSpacing
        }
    }

    // 布局单个路径树
    private void LayoutPathTree(Transform path, float x, float y, int depth)
    {
        if (path == null) return;
        
        // 存储节点位置
        Rect nodeRect = new Rect(x - nodeSize / 2, y - nodeSize / 2, nodeSize, nodeSize);  // 使用 nodeSize
        pathNodePositions[path] = nodeRect;
        
        // 处理子路径
        if (childPaths.ContainsKey(path) && childPaths[path].Count > 0)
        {
            float totalWidth = 0;
            foreach (var childPath in childPaths[path])
            {
                totalWidth += CalculateTreeWidth(childPath);
            }
            
            float currentX = x - totalWidth / 2;
            
            foreach (var childPath in childPaths[path])
            {
                float childWidth = CalculateTreeWidth(childPath);
                float childX = currentX + childWidth / 2;
                
                LayoutPathTree(childPath, childX, y + verticalSpacing, depth + 1);  // 使用 verticalSpacing
                
                currentX += childWidth;
            }
        }
    }

    // 绘制路径连接线
    private void DrawPathConnections()
    {
        if (npcData == null) return;
        
        foreach (var path in pathNodePositions.Keys)
        {
            PathConnection connection = FindPathConnection(path);
            if (connection == null) continue;
            
            if (connection.branchType == PathBranchType.ScoreBased && connection.scoreOptions != null && connection.scoreOptions.Count > 0)
            {
                // 将决策点放在路径下方的中间
                float pathX = pathNodePositions[path].center.x;
                float pathY = pathNodePositions[path].center.y;
                float decisionY = pathY + verticalSpacing / 2;  // 使用 verticalSpacing
                
                // 绘制一个虚拟的决策点
                Vector2 decisionPos = new Vector2(pathX, decisionY);
                Rect decisionRect = new Rect(decisionPos.x - nodeSize/3, decisionPos.y - nodeSize/3, nodeSize*2/3, nodeSize*2/3);  // 使用 nodeSize
                
                // 路径到决策点的连接
                Vector2 start = new Vector2(pathNodePositions[path].center.x, pathNodePositions[path].yMax);
                Vector2 end = new Vector2(decisionRect.center.x, decisionRect.yMin);
                DrawNodeConnection(start, end);
                
                // 绘制决策点
                Handles.BeginGUI();
                Handles.color = new Color(1f, 0.6f, 0.1f);
                
                // 用 Vector3 定义菱形多边形的点
                Vector3[] diamond = new Vector3[5] {
                    new Vector3(decisionRect.center.x, decisionRect.yMin, 0),
                    new Vector3(decisionRect.xMax, decisionRect.center.y, 0),
                    new Vector3(decisionRect.center.x, decisionRect.yMax, 0),
                    new Vector3(decisionRect.xMin, decisionRect.center.y, 0),
                    new Vector3(decisionRect.center.x, decisionRect.yMin, 0) // 闭合多边形
                };
                
                // 使用修正后的绘制方法
                Handles.DrawAAPolyLine(2f, diamond);
                Handles.EndGUI();
                
                // 决策点标签
                GUI.color = Color.white;
                GUI.Label(
                    new Rect(decisionRect.x - 30, decisionRect.yMax + 5, 100, 20),
                    "分数分支",
                    new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter }
                );
                
                // 决策点到各选项的连接
                foreach (var option in connection.scoreOptions)
                {
                    if (option.pathObject != null && pathNodePositions.ContainsKey(option.pathObject.transform))
                    {
                        Vector2 decisionEnd = new Vector2(decisionRect.center.x, decisionRect.yMax);
                        Vector2 optionStart = new Vector2(pathNodePositions[option.pathObject.transform].center.x, pathNodePositions[option.pathObject.transform].yMin);
                        
                        float scoreRatio = Mathf.Clamp01(option.scoreThreshold / 100f);
                        Color lineColor = Color.Lerp(Color.green, Color.red, scoreRatio);
                        
                        DrawNodeConnection(decisionEnd, optionStart, lineColor);
                        
                        // 在连线中点显示阈值
                        Vector2 labelPos = Vector2.Lerp(decisionEnd, optionStart, 0.5f);
                        GUI.Label(
                            new Rect(labelPos.x - 30, labelPos.y - 10, 60, 20),
                            $">= {option.scoreThreshold}"
                        );
                    }
                }
            }
            else if (!connection.isEndPoint && connection.branchType == PathBranchType.Direct && 
                     connection.nextPathObject != null && pathNodePositions.ContainsKey(connection.nextPathObject.transform))
            {
                // 直接到下一路径的连接
                Vector2 start = new Vector2(pathNodePositions[path].center.x, pathNodePositions[path].yMax);
                Vector2 end = new Vector2(pathNodePositions[connection.nextPathObject.transform].center.x, pathNodePositions[connection.nextPathObject.transform].yMin);
                DrawNodeConnection(start, end);
            }
        }
    }

    // 绘制两点之间的连接线
    private void DrawNodeConnection(Vector2 start, Vector2 end, Color? color = null)
    {
        Color lineColor = color ?? Color.white;
        float strengthFactor = zoomFactor < 1 ? 1 / zoomFactor : 1;
        
        Vector2 direction = (end - start).normalized;
        float distance = Vector2.Distance(start, end);
        
        // 曲线控制点
        Vector2 startTangent = start + new Vector2(0, distance * 0.3f);
        Vector2 endTangent = end - new Vector2(0, distance * 0.3f);
        
        Handles.BeginGUI();
        Handles.color = lineColor;
        Handles.DrawBezier(
            start, end,
            startTangent, endTangent,
            lineColor, null, 2f * strengthFactor
        );
        
        // 箭头
        Vector2 arrowPos = Vector2.Lerp(start, end, 0.7f);
        Vector2 arrowDir = (end - arrowPos).normalized;
        Vector2 arrowLeft = arrowPos + new Vector2(arrowDir.y, -arrowDir.x) * 5f * strengthFactor - arrowDir * 5f * strengthFactor;
        Vector2 arrowRight = arrowPos + new Vector2(-arrowDir.y, arrowDir.x) * 5f * strengthFactor - arrowDir * 5f * strengthFactor;
        
        Handles.DrawAAPolyLine(3f * strengthFactor, 
            new Vector3(arrowPos.x, arrowPos.y, 0), 
            new Vector3(arrowLeft.x, arrowLeft.y, 0));
        Handles.DrawAAPolyLine(3f * strengthFactor, 
            new Vector3(arrowPos.x, arrowPos.y, 0), 
            new Vector3(arrowRight.x, arrowRight.y, 0));
        
        Handles.EndGUI();
    }

    // 绘制路径节点
    private void DrawPathNodes(Rect canvasRect)
    {
        // 绘制所有路径节点
        foreach (var kvp in pathNodePositions)
        {
            Transform path = kvp.Key;
            Rect nodeRect = kvp.Value;
            
            PathConnection connection = FindPathConnection(path);
            Color nodeColor = Color.blue;
            
            if (connection != null)
            {
                if (connection.isEndPoint)
                {
                    nodeColor = Color.red;
                }
                else if (connection.branchType == PathBranchType.ScoreBased)
                {
                    nodeColor = new Color(1f, 0.6f, 0.1f);  // 橙色
                }
            }
            
            // 选中状态
            if (path == selectedPath)
            {
                GUI.color = Color.white;
                GUI.Box(new Rect(nodeRect.x - 5, nodeRect.y - 5, nodeRect.width + 10, nodeRect.height + 10), "");
            }
            
            // 绘制节点
            GUI.color = nodeColor;
            GUI.Box(nodeRect, "");
            
            // 节点标签
            GUI.color = Color.white;
            GUI.Label(
                new Rect(nodeRect.x, nodeRect.yMax + 5, 100, 20),
                path.name,
                new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter }
            );
            
            // 检测点击
            if (Event.current.type == EventType.MouseDown && nodeRect.Contains(Event.current.mousePosition))
            {
                selectedPath = path;
                
                if (Event.current.control)
                {
                    // Ctrl+点击: 在Scene窗口中选择路径
                    Selection.activeGameObject = path.gameObject;
                    SceneView.FrameLastActiveSceneView();
                }
                
                Event.current.Use();
            }
        }
        
        GUI.color = Color.white;
    }
}
#endif