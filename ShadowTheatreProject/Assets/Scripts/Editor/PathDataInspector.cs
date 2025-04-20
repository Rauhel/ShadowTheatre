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
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("下一路径");
        
        // 使用 GameObject 字段
        EditorGUI.BeginChangeCheck();
        connection.nextPathObject = EditorGUILayout.ObjectField(
            connection.nextPathObject,
            typeof(GameObject), 
            true
        ) as GameObject;
        
        if (EditorGUI.EndChangeCheck())
        {
            if (connection.nextPathObject != null)
            {
                // 验证对象上是否有 MultiPointPathCreator 组件
                MultiPointPathCreator pathComp = connection.nextPathObject.GetComponent<MultiPointPathCreator>();
                if (pathComp == null)
                {
                    Debug.LogWarning($"选择的对象 {connection.nextPathObject.name} 不包含 MultiPointPathCreator 组件");
                    // 可以选择清除引用或保留，取决于您的需求
                    // connection.nextPathObject = null;
                }
            }
            
            EditorUtility.SetDirty(data);
        }
        
        EditorGUILayout.EndHorizontal();
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
            
            EditorGUI.BeginChangeCheck();
            connection.scoreOptions[i].pathObject = EditorGUILayout.ObjectField(
                "目标路径", 
                connection.scoreOptions[i].pathObject,
                typeof(GameObject), 
                true
            ) as GameObject;
            
            if (EditorGUI.EndChangeCheck())
            {
                if (connection.scoreOptions[i].pathObject != null)
                {
                    // 验证对象上是否有 MultiPointPathCreator 组件
                    MultiPointPathCreator pathComp = connection.scoreOptions[i].pathObject.GetComponent<MultiPointPathCreator>();
                    if (pathComp == null)
                    {
                        Debug.LogWarning($"选择的对象 {connection.scoreOptions[i].pathObject.name} 不包含 MultiPointPathCreator 组件");
                        // 可以选择清除引用或保留
                        // connection.scoreOptions[i].pathObject = null;
                    }
                }
                
                EditorUtility.SetDirty(data);
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