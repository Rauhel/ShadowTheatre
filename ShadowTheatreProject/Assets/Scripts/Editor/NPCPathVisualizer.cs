#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class NPCPathVisualizer : EditorWindow
{
    [SerializeField] private NPCData selectedNPCData;
    [SerializeField] private NPCEventManager selectedNPC;
    private Vector2 scrollPosition;
    private bool showHelp = false;

    // 用于绘制的参数
    private float nodeSize = 30f;
    private float horizontalSpacing = 120f;
    private float verticalSpacing = 80f;
    private Dictionary<Transform, Rect> pathNodePositions = new Dictionary<Transform, Rect>();
    private Dictionary<Transform, List<Transform>> childPaths = new Dictionary<Transform, List<Transform>>();
    private Dictionary<string, Rect> decisionNodePositions = new Dictionary<string, Rect>();

    // 拖拽状态
    private bool isDragging = false;
    private Transform selectedPath = null;
    private Vector2 dragStartPosition;
    private Vector2 canvasOffset = Vector2.zero;
    private float zoomFactor = 1f;

    [MenuItem("Shadow Theatre/NPC Path Visualizer")]
    public static void ShowWindow()
    {
        var window = GetWindow<NPCPathVisualizer>();
        window.titleContent = new GUIContent("NPC路径可视化");
        window.Show();
    }

    private void OnEnable()
    {
        // 恢复状态（如果有）
        pathNodePositions.Clear();
    }

    private void OnGUI()
    {
        // 工具栏
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.FlexibleSpace();
        
        if (GUILayout.Button("居中视图", EditorStyles.toolbarButton))
        {
            RecenterView();
        }
        
        if (GUILayout.Button("刷新", EditorStyles.toolbarButton))
        {
            pathNodePositions.Clear();
            childPaths.Clear();
            decisionNodePositions.Clear();
            Repaint();
        }
        
        showHelp = GUILayout.Toggle(showHelp, "帮助", EditorStyles.toolbarButton);
        EditorGUILayout.EndHorizontal();

        // NPC数据选择区域
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUI.BeginChangeCheck();
        selectedNPCData = EditorGUILayout.ObjectField("NPC数据", selectedNPCData, typeof(NPCData), false) as NPCData;
        selectedNPC = EditorGUILayout.ObjectField("NPC实例", selectedNPC, typeof(NPCEventManager), true) as NPCEventManager;

        if (EditorGUI.EndChangeCheck())
        {
            pathNodePositions.Clear();
            childPaths.Clear();
            decisionNodePositions.Clear();
        }

        if (selectedNPC != null && selectedNPCData == null)
        {
            selectedNPCData = selectedNPC.npcData;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("缩放");
        zoomFactor = EditorGUILayout.Slider(zoomFactor, 0.5f, 2f);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        // 帮助信息
        if (showHelp)
        {
            EditorGUILayout.HelpBox(
                "路径可视化说明：\n" +
                "- 蓝色节点：普通路径\n" +
                "- 红色节点：路径终点\n" +
                "- 橙色节点：决策点\n" +
                "- 拖拽：移动视图\n" +
                "- 点击节点：选择路径\n" +
                "- Ctrl+点击：选择和聚焦路径",
                MessageType.Info
            );
        }

        // 路径可视化区域
        Rect canvasRect = GUILayoutUtility.GetRect(position.width, position.height - EditorGUIUtility.singleLineHeight * 8);
        
        if (Event.current.type == EventType.MouseDown && canvasRect.Contains(Event.current.mousePosition))
        {
            isDragging = true;
            dragStartPosition = Event.current.mousePosition;
        }
        else if (Event.current.type == EventType.MouseUp)
        {
            isDragging = false;
            selectedPath = null;
        }
        else if (Event.current.type == EventType.MouseDrag && isDragging)
        {
            if (selectedPath == null)
            {
                canvasOffset += (Event.current.mousePosition - dragStartPosition) / zoomFactor;
                dragStartPosition = Event.current.mousePosition;
                Repaint();
            }
        }

        DrawPathVisualization(canvasRect);

        // 显示所选路径的属性
        if (selectedPath != null)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("选中路径", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("名称", selectedPath.name);
            
            PathConnection connection = selectedNPCData != null ? FindPathConnection(selectedPath) : null;
            
            if (connection != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("终点");
                connection.isEndPoint = EditorGUILayout.Toggle(connection.isEndPoint);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("需要决策");
                connection.needDecision = EditorGUILayout.Toggle(connection.needDecision);
                EditorGUILayout.EndHorizontal();
                
                if (connection.needDecision)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel("决策点ID");
                    connection.decisionPointID = EditorGUILayout.TextField(connection.decisionPointID);
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel("下一路径");
                    connection.nextPath = EditorGUILayout.ObjectField(connection.nextPath, typeof(Transform), true) as Transform;
                    EditorGUILayout.EndHorizontal();
                }
                
                // 标记为修改
                if (GUI.changed && selectedNPCData != null)
                {
                    EditorUtility.SetDirty(selectedNPCData);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("找不到路径连接信息。点击下方按钮添加路径连接。", MessageType.Warning);
                
                if (GUILayout.Button("添加路径连接") && selectedNPCData != null)
                {
                    if (selectedNPCData.pathConnections == null)
                        selectedNPCData.pathConnections = new List<PathConnection>();
                    
                    PathConnection newConnection = new PathConnection
                    {
                        path = selectedPath,
                        isEndPoint = false,
                        needDecision = false
                    };
                    
                    selectedNPCData.pathConnections.Add(newConnection);
                    EditorUtility.SetDirty(selectedNPCData);
                }
            }
            
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawPathVisualization(Rect canvasRect)
    {
        if (selectedNPCData == null)
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
        DrawPathNodes();
        
        // 恢复矩阵
        GUI.matrix = oldMatrix;
        GUI.EndClip();
    }

    private PathConnection FindPathConnection(Transform path)
    {
        if (selectedNPCData == null || path == null)
            return null;

        foreach (var connection in selectedNPCData.pathConnections)
        {
            if (connection.path == path)
                return connection;
        }

        return null;
    }

    private PathDecision FindDecisionByID(string decisionID)
    {
        if (selectedNPCData == null || string.IsNullOrEmpty(decisionID))
            return null;

        foreach (var decision in selectedNPCData.pathDecisions)
        {
            if (decision.decisionPointID == decisionID)
                return decision;
        }

        return null;
    }

    private void LayoutPaths()
    {
        pathNodePositions.Clear();
        childPaths.Clear();
        decisionNodePositions.Clear();

        // 构建路径之间的父子关系
        foreach (var connection in selectedNPCData.pathConnections)
        {
            if (connection.path == null)
                continue;

            if (!connection.isEndPoint && !connection.needDecision && connection.nextPath != null)
            {
                if (!childPaths.ContainsKey(connection.path))
                {
                    childPaths[connection.path] = new List<Transform>();
                }
                childPaths[connection.path].Add(connection.nextPath);
            }
            else if (connection.needDecision && !string.IsNullOrEmpty(connection.decisionPointID))
            {
                PathDecision decision = FindDecisionByID(connection.decisionPointID);
                if (decision != null)
                {
                    if (!childPaths.ContainsKey(connection.path))
                    {
                        childPaths[connection.path] = new List<Transform>();
                    }

                    foreach (var option in decision.pathOptions)
                    {
                        if (option.path != null)
                        {
                            childPaths[connection.path].Add(option.path);
                        }
                    }
                }
            }
        }

        // 找出所有的根路径（没有父路径的路径）
        List<Transform> rootPaths = new List<Transform>();
        HashSet<Transform> allChildPaths = new HashSet<Transform>();
        
        foreach (var kvp in childPaths)
        {
            foreach (var childPath in kvp.Value)
            {
                allChildPaths.Add(childPath);
            }
        }

        foreach (var connection in selectedNPCData.pathConnections)
        {
            if (connection.path != null && !allChildPaths.Contains(connection.path))
            {
                rootPaths.Add(connection.path);
            }
        }

        // 如果没有找到根路径，使用连接列表中的第一个路径作为根
        if (rootPaths.Count == 0 && selectedNPCData.pathConnections.Count > 0 && 
            selectedNPCData.pathConnections[0].path != null)
        {
            rootPaths.Add(selectedNPCData.pathConnections[0].path);
        }

        // 布局决策点
        float startX = 150;
        float startY = 100;
        float x = startX;

        foreach (var decision in selectedNPCData.pathDecisions)
        {
            decisionNodePositions[decision.decisionPointID] = new Rect(x, startY, nodeSize, nodeSize);
            x += horizontalSpacing;
        }

        // 递归布局所有路径
        float maxTreeWidth = 0;
        float xPos = startX;
        
        foreach (var rootPath in rootPaths)
        {
            float treeWidth = CalculateTreeWidth(rootPath);
            LayoutPathTree(rootPath, xPos, startY + verticalSpacing * 2, 0);
            xPos += treeWidth * horizontalSpacing;
            maxTreeWidth = Mathf.Max(maxTreeWidth, treeWidth);
        }
    }

    private float CalculateTreeWidth(Transform path)
    {
        if (!childPaths.ContainsKey(path) || childPaths[path].Count == 0)
            return 1;

        float width = 0;
        foreach (var childPath in childPaths[path])
        {
            width += CalculateTreeWidth(childPath);
        }
        return width;
    }

    private void LayoutPathTree(Transform path, float x, float y, int depth)
    {
        if (path == null) return;

        // 保存节点位置
        pathNodePositions[path] = new Rect(x, y, nodeSize, nodeSize);

        // 布局子节点
        if (childPaths.ContainsKey(path) && childPaths[path].Count > 0)
        {
            float childX = x - (childPaths[path].Count - 1) * horizontalSpacing / 2f;
            
            foreach (var childPath in childPaths[path])
            {
                LayoutPathTree(childPath, childX, y + verticalSpacing, depth + 1);
                childX += horizontalSpacing;
            }
        }
    }

    private void DrawPathConnections()
    {
        // 绘制连接线
        foreach (var connection in selectedNPCData.pathConnections)
        {
            if (connection.path == null)
                continue;

            if (pathNodePositions.TryGetValue(connection.path, out Rect pathRect))
            {
                Vector2 start = new Vector2(pathRect.center.x, pathRect.yMax);

                if (!connection.isEndPoint && !connection.needDecision && connection.nextPath != null)
                {
                    if (pathNodePositions.TryGetValue(connection.nextPath, out Rect nextRect))
                    {
                        Vector2 end = new Vector2(nextRect.center.x, nextRect.yMin);
                        Handles.color = Color.gray;
                        DrawNodeConnection(start, end);
                    }
                }
                else if (connection.needDecision && !string.IsNullOrEmpty(connection.decisionPointID))
                {
                    PathDecision decision = FindDecisionByID(connection.decisionPointID);
                    if (decision != null)
                    {
                        if (decisionNodePositions.TryGetValue(decision.decisionPointID, out Rect decisionRect))
                        {
                            // 连接到决策点
                            Vector2 decisionPoint = new Vector2(decisionRect.center.x, decisionRect.center.y);
                            Handles.color = new Color(1f, 0.6f, 0.1f);
                            DrawNodeConnection(start, decisionPoint);

                            // 连接决策点到目标路径
                            foreach (var option in decision.pathOptions)
                            {
                                if (option.path != null && pathNodePositions.TryGetValue(option.path, out Rect optionRect))
                                {
                                    Vector2 optionStart = decisionPoint;
                                    Vector2 optionEnd = new Vector2(optionRect.center.x, optionRect.yMin);
                                    
                                    // 根据分数阈值选择颜色
                                    Color optionColor = Color.HSVToRGB(
                                        Mathf.Clamp01(option.scoreThreshold / 100f), 0.7f, 0.9f);
                                    Handles.color = optionColor;
                                    
                                    DrawNodeConnection(optionStart, optionEnd);
                                    
                                    // 绘制分数标签
                                    Vector2 labelPos = Vector2.Lerp(optionStart, optionEnd, 0.5f);
                                    GUI.color = optionColor;
                                    GUI.Label(
                                        new Rect(labelPos.x - 15, labelPos.y - 10, 40, 20),
                                        option.scoreThreshold.ToString("F0"),
                                        EditorStyles.boldLabel
                                    );
                                    GUI.color = Color.white;
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private void DrawNodeConnection(Vector2 start, Vector2 end)
    {
        // 使用贝塞尔曲线连接点
        Vector2 startTangent = start + Vector2.down * 30;
        Vector2 endTangent = end + Vector2.up * 30;
        Handles.DrawBezier(start, end, startTangent, endTangent, Handles.color, null, 2f);
        
        // 绘制箭头
        Vector2 direction = (end - start).normalized;
        Vector2 arrowPos = Vector2.Lerp(start, end, 0.7f);
        Vector2 arrowSize = new Vector2(10, 5);
        Vector2 right = new Vector2(-direction.y, direction.x) * arrowSize.y;
        Vector2 arrowTip = arrowPos + direction * arrowSize.x;
        Vector2 arrowLeft = arrowPos - right;
        Vector2 arrowRight = arrowPos + right;
        
        Handles.DrawPolyLine(arrowLeft, arrowTip, arrowRight);
    }

    private void DrawPathNodes()
    {
        // 绘制路径节点
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
                else if (connection.needDecision)
                {
                    nodeColor = new Color(1f, 0.6f, 0.1f); // 橙色
                }
            }
            
            // 选中状态
            if (path == selectedPath)
            {
                GUI.color = Color.yellow;
                GUI.Box(new Rect(nodeRect.x - 2, nodeRect.y - 2, nodeRect.width + 4, nodeRect.height + 4), "");
                GUI.color = Color.white;
            }
            
            // 绘制节点
            GUI.color = nodeColor;
            GUI.Box(nodeRect, "");
            GUI.color = Color.white;
            
            // 节点标签
            string nodeName = path.name;
            if (nodeName.Length > 12)
            {
                nodeName = nodeName.Substring(0, 10) + "...";
            }
            
            GUI.Label(
                new Rect(nodeRect.x - 20, nodeRect.yMax + 5, nodeRect.width + 40, 20),
                nodeName,
                EditorStyles.miniLabel
            );
            
            // 处理点击选择
            if (Event.current.type == EventType.MouseDown && nodeRect.Contains(Event.current.mousePosition / zoomFactor - canvasOffset))
            {
                bool ctrlPressed = Event.current.control || Event.current.command;
                selectedPath = path;
                
                if (ctrlPressed)
                {
                    // 聚焦到选中节点
                    Vector2 center = new Vector2(position.width / 2, position.height / 2);
                    canvasOffset = center / zoomFactor - nodeRect.center;
                    
                    // 在场景中选择该物体
                    Selection.activeGameObject = path.gameObject;
                    SceneView.FrameLastActiveSceneView();
                }
                
                Event.current.Use();
                Repaint();
            }
        }
        
        // 绘制决策点
        foreach (var kvp in decisionNodePositions)
        {
            string decisionID = kvp.Key;
            Rect nodeRect = kvp.Value;
            
            GUI.color = new Color(1f, 0.6f, 0.1f);
            GUI.Box(nodeRect, "D");
            GUI.color = Color.white;
            
            // 决策点标签
            GUI.Label(
                new Rect(nodeRect.x - 20, nodeRect.yMax + 5, nodeRect.width + 40, 20),
                decisionID,
                EditorStyles.miniLabel
            );
        }
    }

    private void RecenterView()
    {
        if (pathNodePositions.Count == 0)
            return;
            
        // 找出所有节点的边界
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        
        foreach (var rect in pathNodePositions.Values)
        {
            minX = Mathf.Min(minX, rect.x);
            minY = Mathf.Min(minY, rect.y);
            maxX = Mathf.Max(maxX, rect.x + rect.width);
            maxY = Mathf.Max(maxY, rect.y + rect.height);
        }
        
        foreach (var rect in decisionNodePositions.Values)
        {
            minX = Mathf.Min(minX, rect.x);
            minY = Mathf.Min(minY, rect.y);
            maxX = Mathf.Max(maxX, rect.x + rect.width);
            maxY = Mathf.Max(maxY, rect.y + rect.height);
        }
        
        // 计算中心点
        Vector2 center = new Vector2((minX + maxX) / 2, (minY + maxY) / 2);
        
        // 将视图中心设置为树的中心
        Vector2 viewCenter = new Vector2(position.width / 2, position.height / 2);
        canvasOffset = viewCenter / zoomFactor - center;
        
        Repaint();
    }
}
#endif