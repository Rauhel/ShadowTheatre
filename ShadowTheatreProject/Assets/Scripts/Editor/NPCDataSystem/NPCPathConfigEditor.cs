#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq; // 添加LINQ命名空间

public class NPCPathConfigEditor
{
    private NPCDataEditor mainEditor;
    
    public NPCPathConfigEditor(NPCDataEditor editor)
    {
        mainEditor = editor;
    }
    
    public void DrawPathConfig(PathConfig config, int index)
    {
        // 使用路径ID或索引作为折叠面板的键
        string key = string.IsNullOrEmpty(config.pathID) ? $"path_{index}" : config.pathID;
        
        if (!mainEditor.PathFoldouts.ContainsKey(key))
        {
            mainEditor.PathFoldouts[key] = false;
        }
        
        // 使用更紧凑的布局
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        // 路径标题行 - 更紧凑
        EditorGUILayout.BeginHorizontal();
        mainEditor.PathFoldouts[key] = EditorGUILayout.Foldout(
            mainEditor.PathFoldouts[key], 
            $"路径 {index+1}: {config.pathName}", 
            true, 
            EditorStyles.foldoutHeader // 使用标准的折叠标题样式
        );
        
        // 删除按钮 - 更小
        if (GUILayout.Button("×", GUILayout.Width(20)))
        {
            if (EditorUtility.DisplayDialog("确认删除", $"确定要删除路径 '{config.pathName}'?", "删除", "取消"))
            {
                mainEditor.Data.paths.RemoveAt(index);
                EditorUtility.SetDirty(mainEditor.Data);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
        }
        EditorGUILayout.EndHorizontal();
        
        if (mainEditor.PathFoldouts[key])
        {
            EditorGUI.indentLevel++;
            
            // 分成紧凑的部分
            EditorGUILayout.Space(2); // 减少空间
            DrawPathBasicInfo(config);
            
            EditorGUILayout.Space(2);
            DrawPathScoreRequirements(config);
            
            // ===== 新增：时间控制设置 =====
            mainEditor.ActionEditor.DrawPathTimeSettings(config);
            
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("3. 路径动作", EditorStyles.boldLabel);
            mainEditor.ActionEditor.DrawPathActions(config);
            
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("4. 路径事件", EditorStyles.boldLabel);
            DrawPathEvents(config);
            
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("5. 下一路径分支", EditorStyles.boldLabel);
            DrawPathBranches(config);
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawPathBasicInfo(PathConfig config)
    {
        EditorGUILayout.LabelField("1. 路径基本信息", EditorStyles.boldLabel);
        
        // 路径 ID 和路径创建器
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("路径ID");
        
        // 显示当前ID和路径选择器
        EditorGUI.BeginChangeCheck();
        
        // 获取所有可用路径
        List<string> pathIDs = new List<string>();
        List<string> pathNames = new List<string>();
        MultiPointPathCreator[] allPaths = Object.FindObjectsOfType<MultiPointPathCreator>();
        
        int selectedIndex = 0;
        for (int i = 0; i < allPaths.Length; i++)
        {
            if (!string.IsNullOrEmpty(allPaths[i].pathID))
            {
                pathIDs.Add(allPaths[i].pathID);
                pathNames.Add($"{allPaths[i].name} ({allPaths[i].pathID})");
                
                if (allPaths[i].pathID == config.pathID)
                {
                    selectedIndex = pathIDs.Count - 1;
                }
            }
        }
        
        // 添加一个"无"选项
        if (pathIDs.Count == 0 || string.IsNullOrEmpty(config.pathID))
        {
            pathIDs.Insert(0, "");
            pathNames.Insert(0, "选择路径...");
            selectedIndex = 0;
        }
        
        int newIndex = EditorGUILayout.Popup(selectedIndex, pathNames.ToArray());
        
        if (EditorGUI.EndChangeCheck() && newIndex < pathIDs.Count)
        {
            config.pathID = pathIDs[newIndex];
            if (newIndex > 0) // 如果不是"无"选项
            {
                // 获取路径名
                MultiPointPathCreator selectedPath = PathRegistry.GetPathCreatorByID(config.pathID);
                if (selectedPath != null && string.IsNullOrEmpty(config.pathName))
                {
                    config.pathName = selectedPath.name;
                }
            }
            EditorUtility.SetDirty(mainEditor.Data);
        }
        
        // 添加定位按钮
        if (!string.IsNullOrEmpty(config.pathID))
        {
            MultiPointPathCreator pathCreator = PathRegistry.GetPathCreatorByID(config.pathID);
            if (pathCreator != null && GUILayout.Button("定位", GUILayout.Width(50)))
            {
                Selection.activeGameObject = pathCreator.gameObject;
                SceneView.FrameLastActiveSceneView();
            }
        }
        
        EditorGUILayout.EndHorizontal();
        
        config.pathName = EditorGUILayout.TextField("路径名称", config.pathName);
    }
    
    private void DrawPathScoreRequirements(PathConfig config)
    {
        EditorGUILayout.LabelField("2. 分数要求", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("分数范围");
        EditorGUILayout.MinMaxSlider(ref config.minScore, ref config.maxScore, 0f, 100f);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        config.minScore = EditorGUILayout.FloatField("最低分数", config.minScore);
        config.maxScore = EditorGUILayout.FloatField("最高分数", config.maxScore);
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawPathEvents(PathConfig config)
    {
        if (config.events.Count == 0)
        {
            EditorGUILayout.HelpBox("此路径没有事件。点击下方按钮添加事件。", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < config.events.Count; i++)
            {
                mainEditor.EventEditor.DrawPathEvent(config, config.events[i], i);
            }
        }
        
        if (GUILayout.Button("添加事件"))
        {
            AddEventToPath(config);
        }
    }
    
    private void DrawPathBranches(PathConfig config)
    {
        if (config.nextPaths.Count == 0)
        {
            EditorGUILayout.HelpBox("此路径没有下一条路径。这将作为终点路径。", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < config.nextPaths.Count; i++)
            {
                DrawPathBranch(config.nextPaths[i], i);
            }
        }
        
        if (GUILayout.Button("添加路径分支"))
        {
            AddBranchToPath(config);
        }
    }
    
    private void DrawPathBranch(PathBranch branch, int index)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"分支 {index+1}", EditorStyles.boldLabel);
        
        if (GUILayout.Button("删除", GUILayout.Width(60)))
        {
            if (EditorUtility.DisplayDialog("确认删除", "确定要删除此分支?", "删除", "取消"))
            {
                PathConfig config = mainEditor.Data.paths.Find(p => p.nextPaths.Contains(branch));
                if (config != null)
                {
                    config.nextPaths.Remove(branch);
                    EditorUtility.SetDirty(mainEditor.Data);
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
        }
        EditorGUILayout.EndHorizontal();
        
        // 分数区间设置
        EditorGUILayout.LabelField("分数区间", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("分数范围");
        EditorGUILayout.MinMaxSlider(ref branch.minScore, ref branch.maxScore, 0f, 100f);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        branch.minScore = EditorGUILayout.FloatField("最低分数 (含)", branch.minScore);
        branch.maxScore = EditorGUILayout.FloatField("最高分数 (不含)", branch.maxScore);
        EditorGUILayout.EndHorizontal();
        
        // 向后兼容，设置 requiredScore
        branch.requiredScore = branch.minScore;
        
        // 下一路径选择
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("下一路径", EditorStyles.boldLabel);
        
        // 获取所有可用路径
        MultiPointPathCreator[] allPathCreators = Object.FindObjectsOfType<MultiPointPathCreator>();
        List<string> pathIDs = new List<string>();
        List<string> pathNames = new List<string>();
        
        // 添加"无"选项
        pathIDs.Add("");
        pathNames.Add("选择路径...");
        
        int selectedIndex = 0;
        
        // 添加所有路径选项
        for (int i = 0; i < allPathCreators.Length; i++)
        {
            if (!string.IsNullOrEmpty(allPathCreators[i].pathID))
            {
                pathIDs.Add(allPathCreators[i].pathID);
                pathNames.Add($"{allPathCreators[i].name} ({allPathCreators[i].pathID})");
                
                if (allPathCreators[i].pathID == branch.nextPathID)
                {
                    selectedIndex = pathIDs.Count - 1;
                }
            }
        }
        
        EditorGUI.BeginChangeCheck();
        int newIndex = EditorGUILayout.Popup("路径选择", selectedIndex, pathNames.ToArray());
        
        if (EditorGUI.EndChangeCheck())
        {
            branch.nextPathID = (newIndex > 0) ? pathIDs[newIndex] : "";
            EditorUtility.SetDirty(mainEditor.Data);
        }
        
        // 显示选中路径信息
        if (!string.IsNullOrEmpty(branch.nextPathID))
        {
            MultiPointPathCreator selectedCreator = PathRegistry.GetPathCreatorByID(branch.nextPathID);
            if (selectedCreator != null)
            {
                EditorGUILayout.LabelField("路径名称:", selectedCreator.name);
                
                if (GUILayout.Button("在场景中定位"))
                {
                    Selection.activeObject = selectedCreator.gameObject;
                    SceneView.FrameLastActiveSceneView();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("找不到指定的路径ID", MessageType.Warning);
            }
        }
        
        branch.description = EditorGUILayout.TextField("描述", branch.description);
        
        EditorGUILayout.EndVertical();
    }
    
    public void AddNewPath()
    {
        // 明确初始化每个属性的正确类型
        PathConfig newPath = new PathConfig();
        newPath.pathName = "新路径";
        newPath.minScore = 0;
        newPath.maxScore = 100;
        newPath.events = new List<PathEvent>();
        newPath.nextPaths = new List<PathBranch>();
        // 确保使用 ActionData 类型初始化路径动作
        newPath.pathActions = new List<ActionData>();
        
        mainEditor.Data.paths.Add(newPath);
        
        // 展开新路径
        string key = string.IsNullOrEmpty(newPath.pathID) ? $"path_{mainEditor.Data.paths.Count - 1}" : newPath.pathID;
        mainEditor.PathFoldouts[key] = true;
        
        EditorUtility.SetDirty(mainEditor.Data);
    }
    
    private void AddEventToPath(PathConfig config)
    {
        string eventId = $"Event_{config.pathName}_{config.events.Count + 1}";
        
        // 创建新事件
        PathEvent newEvent = new PathEvent();
        newEvent.eventID = eventId;
        newEvent.startPointIndex = 0;
        newEvent.endPointIndex = 0;
        newEvent.maxRecognitionDistance = 8.0f;
        newEvent.gestureHoldTime = 1f;
        newEvent.playerInteractionRadius = 3.0f;
        newEvent.showInteractionRange = true;
        
        // 初始化手势响应列表
        newEvent.gestureResponses = new List<GestureResponse>();
        
        // 创建默认响应
        GestureResponse defaultResponse = new GestureResponse();
        defaultResponse.scoreEffect = 0;
        
        // 初始化动作列表
        defaultResponse.actions = new List<ActionData>();
        
        // 创建ActionData
        ActionData defaultAction = new ActionData();
        defaultAction.dialogueText = "默认反应文本";
        defaultAction.displayDuration = 2.0f;
        defaultAction.pathPointIndex = 0;
        
        // 添加到ActionData列表
        defaultResponse.actions.Add(defaultAction);
        
        // 设置默认响应
        newEvent.defaultResponse = defaultResponse;
        
        // 添加事件到配置
        config.events.Add(newEvent);
        
        // 展开新事件
        mainEditor.EventFoldouts[eventId] = true;
        
        EditorUtility.SetDirty(mainEditor.Data);
    }
    
    private void AddBranchToPath(PathConfig config)
    {
        float lastMax = 0;
        if (config.nextPaths.Count > 0)
        {
            lastMax = config.nextPaths[config.nextPaths.Count - 1].maxScore;
        }
        
        PathBranch newBranch = new PathBranch
        {
            minScore = lastMax,
            maxScore = lastMax + 20,
            requiredScore = lastMax,
            description = $"分支 {config.nextPaths.Count + 1}"
        };
        
        config.nextPaths.Add(newBranch);
        EditorUtility.SetDirty(mainEditor.Data);
    }
}
#endif