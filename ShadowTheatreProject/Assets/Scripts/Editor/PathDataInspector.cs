#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

// 处理路径数据的检查和编辑
public class PathDataInspector
{
    private Vector2 scrollPosition;
    private NPCData npcData;

    public void SetNPCData(NPCData data)
    {
        npcData = data;
    }

    public void ClearCache()
    {
        // 清除任何缓存数据
    }

    // 显示所选路径的详细信息
    public void ShowSelectedPathInfo(Transform selectedPath, NPCData data)
    {
        if (selectedPath == null || data == null) return;

        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField("选中路径", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("名称", selectedPath.name);
        
        PathConnection connection = FindPathConnection(selectedPath, data);
        
        // 显示路径点信息
        MultiPointPathCreator selectedPathCreator = selectedPath.GetComponent<MultiPointPathCreator>();
        if (selectedPathCreator != null)
        {
            ShowPathPointInfo(selectedPathCreator);
        }
        
        if (connection != null)
        {
            ShowPathConnectionInfo(connection, data);
        }
        else
        {
            EditorGUILayout.HelpBox("找不到路径连接信息。点击下方按钮添加路径连接。", MessageType.Warning);
            
            if (GUILayout.Button("添加路径连接") && data != null)
            {
                if (data.pathConnections == null)
                    data.pathConnections = new List<PathConnection>();
                
                PathConnection newConnection = new PathConnection
                {
                    path = selectedPath,
                    isEndPoint = false,
                    branchType = PathBranchType.Direct
                };
                
                data.pathConnections.Add(newConnection);
                EditorUtility.SetDirty(data);
            }
        }
        
        EditorGUILayout.EndVertical();
    }

    // 显示路径连接信息
    private void ShowPathConnectionInfo(PathConnection connection, NPCData data)
    {
        // 路径设置标题
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("路径设置", EditorStyles.boldLabel);
        
        // 终点设置
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("终点");
        connection.isEndPoint = EditorGUILayout.Toggle(connection.isEndPoint);
        EditorGUILayout.EndHorizontal();
        
        // 分支类型
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("分支类型");
        connection.branchType = (PathBranchType)EditorGUILayout.EnumPopup(connection.branchType);
        EditorGUILayout.EndHorizontal();
        
        // 根据分支类型显示不同的选项
        if (connection.branchType == PathBranchType.Direct)
        {
            ShowDirectConnectionOptions(connection, data);
        }
        else if (connection.branchType == PathBranchType.ScoreBased)
        {
            ShowScoreBasedConnectionOptions(connection, data);
        }
        
        // 标记为修改
        if (GUI.changed && data != null)
        {
            EditorUtility.SetDirty(data);
        }
    }

    // 修改后的 ShowDirectConnectionOptions 方法
    private void ShowDirectConnectionOptions(PathConnection connection, NPCData data)
    {
        string currentPathID = connection.nextPathID;
        string[] availablePathIDs = GetAvailablePathIDs();
        string[] displayOptions = GetPathDisplayNames();
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("下一路径");
        
        // 查找当前路径ID在数组中的索引
        int currentIndex = 0;
        for (int i = 0; i < availablePathIDs.Length; i++)
        {
            if (availablePathIDs[i] == currentPathID)
            {
                currentIndex = i;
                break;
            }
        }
        
        // 使用下拉菜单选择路径ID
        int newIndex = EditorGUILayout.Popup(currentIndex, displayOptions);
        
        // 如果选择发生变化，更新路径ID
        if (newIndex != currentIndex && newIndex < availablePathIDs.Length)
        {
            connection.nextPathID = availablePathIDs[newIndex];
            EditorUtility.SetDirty(data);
        }
        
        EditorGUILayout.EndHorizontal();
        
        // 显示实际路径信息
        var pathCreator = connection.nextPathCreator;
        if (pathCreator != null)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("路径名称:", pathCreator.name);
            EditorGUILayout.LabelField("路径ID:", connection.nextPathID);
            
            // 添加定位按钮
            if (GUILayout.Button("在场景中定位"))
            {
                Selection.activeGameObject = pathCreator.gameObject;
                SceneView.FrameLastActiveSceneView();
            }
            EditorGUI.indentLevel--;
        }
    }

    // 添加辅助方法获取所有可用路径
    private string[] GetAvailablePathIDs()
    {
        // 获取所有路径创建器
        MultiPointPathCreator[] allPaths = Object.FindObjectsOfType<MultiPointPathCreator>();
        
        // 添加一个"无"选项
        List<string> pathIDs = new List<string> { "" };
        
        foreach (var path in allPaths)
        {
            if (!string.IsNullOrEmpty(path.pathID))
            {
                pathIDs.Add(path.pathID);
            }
        }
        
        return pathIDs.ToArray();
    }

    // 获取路径的显示名称
    private string[] GetPathDisplayNames()
    {
        MultiPointPathCreator[] allPaths = Object.FindObjectsOfType<MultiPointPathCreator>();
        
        // 添加一个"无"选项
        List<string> displayNames = new List<string> { "无" };
        
        foreach (var path in allPaths)
        {
            string displayName = string.IsNullOrEmpty(path.pathID) ? 
                $"{path.name} (无ID)" : 
                $"{path.name} ({path.pathID})";
            displayNames.Add(displayName);
        }
        
        return displayNames.ToArray();
    }

    // 修改后的 ShowScoreBasedConnectionOptions 方法
    private void ShowScoreBasedConnectionOptions(PathConnection connection, NPCData data)
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("分数分支选项", EditorStyles.boldLabel);
        
        if (connection.scoreOptions == null)
            connection.scoreOptions = new List<PathScoreOption>();
        
        for (int i = 0; i < connection.scoreOptions.Count; i++)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel($"选项 {i+1}");
            
            if (GUILayout.Button("删除", GUILayout.Width(60)))
            {
                connection.scoreOptions.RemoveAt(i);
                EditorUtility.SetDirty(data);
                break;
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUI.indentLevel++;
            
            connection.scoreOptions[i].optionName = EditorGUILayout.TextField("名称", connection.scoreOptions[i].optionName);
            connection.scoreOptions[i].scoreThreshold = EditorGUILayout.FloatField("分数阈值", connection.scoreOptions[i].scoreThreshold);
            
            // 使用同样的ID选择器
            string currentPathID = connection.scoreOptions[i].pathID;
            string[] availablePathIDs = GetAvailablePathIDs();
            string[] displayOptions = GetPathDisplayNames();
            
            int currentIndex = 0;
            for (int j = 0; j < availablePathIDs.Length; j++)
            {
                if (availablePathIDs[j] == currentPathID)
                {
                    currentIndex = j;
                    break;
                }
            }
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("目标路径");
            int newIndex = EditorGUILayout.Popup(currentIndex, displayOptions);
            EditorGUILayout.EndHorizontal();
            
            if (newIndex != currentIndex && newIndex < availablePathIDs.Length)
            {
                connection.scoreOptions[i].pathID = availablePathIDs[newIndex];
                EditorUtility.SetDirty(data);
            }
            
            // 显示实际路径信息
            var pathCreator = connection.scoreOptions[i].pathCreator;
            if (pathCreator != null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("路径名称:", pathCreator.name);
                EditorGUILayout.LabelField("路径ID:", connection.scoreOptions[i].pathID);
                
                // 添加定位按钮
                if (GUILayout.Button("在场景中定位"))
                {
                    Selection.activeGameObject = pathCreator.gameObject;
                    SceneView.FrameLastActiveSceneView();
                }
                EditorGUI.indentLevel--;
            }
            
            connection.scoreOptions[i].description = EditorGUILayout.TextField("描述", connection.scoreOptions[i].description);
            
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }
        
        if (GUILayout.Button("添加分支选项"))
        {
            connection.scoreOptions.Add(new PathScoreOption
            {
                optionName = $"选项 {connection.scoreOptions.Count + 1}",
                scoreThreshold = connection.scoreOptions.Count > 0 ? 
                    connection.scoreOptions[connection.scoreOptions.Count - 1].scoreThreshold + 10 : 0
            });
            EditorUtility.SetDirty(data);
        }
    }

    // 显示路径点信息
    private void ShowPathPointInfo(MultiPointPathCreator creator)
    {
        if (creator == null || creator.pathPointsParent == null || 
            creator.pathPointsParent.childCount < 2)
        {
            EditorGUILayout.HelpBox("此路径没有生成路径点。请先生成路径点。", MessageType.Warning);
            return;
        }
        
        EditorGUILayout.LabelField("路径点信息", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("提示：可将这些路径点拖入NPC事件的触发位置，以设置事件触发点。", MessageType.Info);
        EditorGUILayout.BeginVertical(GUI.skin.box);
        
        int pathPointCount = creator.pathPointsParent.childCount;
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
        
        for (int i = 0; i < pathPointCount; i++)
        {
            Transform point = creator.pathPointsParent.GetChild(i);
            // 计算相对位置（0-1之间的值）
            float relativePos = (float)i / (pathPointCount - 1);
            // 百分比显示
            string posLabel = $"{relativePos:P0}";
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.ObjectField(point, typeof(Transform), true);
            EditorGUILayout.LabelField(posLabel, GUILayout.Width(50));
            
            // 添加一个定位按钮
            if (GUILayout.Button("定位", GUILayout.Width(50)))
            {
                Selection.activeGameObject = point.gameObject;
                SceneView.FrameLastActiveSceneView();
            }
            
            // 检查该点是否被用作事件触发点
            string eventID = GetEventIDForPoint(point);
            if (!string.IsNullOrEmpty(eventID))
            {
                EditorGUILayout.LabelField($"事件: {eventID}", EditorStyles.miniLabel, GUILayout.Width(80));
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // 获取路径点上的事件ID
    private string GetEventIDForPoint(Transform pathPoint)
    {
        if (npcData == null || pathPoint == null)
            return string.Empty;
            
        foreach (var npcEvent in npcData.events)
        {
            if (npcEvent.triggerLocation == pathPoint)
                return npcEvent.eventID;
        }
        
        return string.Empty;
    }

    // 根据路径查找路径连接信息
    private PathConnection FindPathConnection(Transform path, NPCData data)
    {
        if (data == null || path == null)
            return null;

        foreach (var connection in data.pathConnections)
        {
            if (connection.path == path || 
                (connection.pathCreator != null && connection.pathCreator.transform == path))
                return connection;
        }

        return null;
    }
}
#endif