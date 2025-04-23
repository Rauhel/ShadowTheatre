#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(NPCData))]
public class NPCDataEditor : Editor
{
    private NPCData npcData;
    private Dictionary<string, bool> pathFoldouts = new Dictionary<string, bool>();
    private Dictionary<string, bool> eventFoldouts = new Dictionary<string, bool>();
    private Vector2 scrollPosition;

    private void OnEnable()
    {
        npcData = (NPCData)target;
    }

    public void ClearCache()
    {
        pathFoldouts.Clear();
        eventFoldouts.Clear();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 基本信息
        EditorGUILayout.LabelField("NPC基本信息", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        npcData.npcID = EditorGUILayout.TextField("NPC ID", npcData.npcID);
        npcData.npcName = EditorGUILayout.TextField("NPC 名称", npcData.npcName);
        npcData.currentScore = EditorGUILayout.FloatField("当前分数", npcData.currentScore);
        EditorGUI.indentLevel--;
        
        EditorGUILayout.Space(10);
        
        // 路径配置点信息
        EditorGUILayout.LabelField("路径配置", EditorStyles.boldLabel);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(400));
        
        for (int i = 0; i < npcData.paths.Count; i++)
        {
            DrawPathConfig(npcData.paths[i], i);
        }
        
        EditorGUILayout.EndScrollView();
        
        // 添加新路径
        if (GUILayout.Button("添加新路径"))
        {   
            AddNewPath();
        }   
        
        if (GUI.changed)
        {       
            EditorUtility.SetDirty(npcData);
        }
        
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(npcData);
    }

    private void DrawPathConfig(PathConfig config, int index)
    {
        // 使用路径ID或索引作为折叠面板的键
        string key = string.IsNullOrEmpty(config.pathID) ? $"path_{index}" : config.pathID;
        
        if (!pathFoldouts.ContainsKey(key))
        {
            pathFoldouts[key] = false;
        }
        
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        // 路径标题
        EditorGUILayout.BeginHorizontal();
        pathFoldouts[key] = EditorGUILayout.Foldout(pathFoldouts[key], $"路径 {index+1}: {config.pathName}", true);
        
        if (GUILayout.Button("删除", GUILayout.Width(60)))
        {
            if (EditorUtility.DisplayDialog("确认删除", $"确定要删除路径 '{config.pathName}'?", "删除", "取消"))
            {
                npcData.paths.RemoveAt(index);
                EditorUtility.SetDirty(npcData);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
        }
        EditorGUILayout.EndHorizontal();
        
        if (pathFoldouts[key])
        {
            EditorGUI.indentLevel++;
            
            // 路径基本信息
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
                EditorUtility.SetDirty(npcData);
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
            
            // 分数范围
            EditorGUILayout.LabelField("2. 分数要求", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("分数范围");
            EditorGUILayout.MinMaxSlider(ref config.minScore, ref config.maxScore, 0f, 100f);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            config.minScore = EditorGUILayout.FloatField("最低分数", config.minScore);
            config.maxScore = EditorGUILayout.FloatField("最高分数", config.maxScore);
            EditorGUILayout.EndHorizontal();
            
            // 事件列表
            EditorGUILayout.LabelField("3. 路径事件", EditorStyles.boldLabel);
            
            if (config.events.Count == 0)
            {
                EditorGUILayout.HelpBox("此路径没有事件。点击下方按钮添加事件。", MessageType.Info);
            }
            else
            {
                for (int i = 0; i < config.events.Count; i++)
                {
                    DrawPathEvent(config, config.events[i], i);
                }
            }
            
            if (GUILayout.Button("添加事件"))
            {
                AddEventToPath(config);
            }
            
            // 下一路径分支
            EditorGUILayout.LabelField("4. 下一路径分支", EditorStyles.boldLabel);
            
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
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawPathEvent(PathConfig config, PathEvent pathEvent, int index)
    {
        string key = string.IsNullOrEmpty(pathEvent.eventID) ? $"event_{index}" : pathEvent.eventID;
        
        if (!eventFoldouts.ContainsKey(key))
        {
            eventFoldouts[key] = false;
        }
        
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        EditorGUILayout.BeginHorizontal();
        eventFoldouts[key] = EditorGUILayout.Foldout(eventFoldouts[key], $"事件 {index+1}: {pathEvent.eventID}", true);
        
        if (GUILayout.Button("删除", GUILayout.Width(60)))
        {
            if (EditorUtility.DisplayDialog("确认删除", "确定要删除此事件?", "删除", "取消"))
            {
                config.events.RemoveAt(index);
                EditorUtility.SetDirty(npcData);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
        }
        EditorGUILayout.EndHorizontal();
        
        if (eventFoldouts[key])
        {
            EditorGUI.indentLevel++;
            
            pathEvent.eventID = EditorGUILayout.TextField("事件ID", pathEvent.eventID);
            
            // 改进幕数控制选项布局
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("事件可用性控制", EditorStyles.boldLabel);
            
            // 使用2列布局，更紧凑地显示可用性选项
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.4f));
            pathEvent.enabledInAct1 = EditorGUILayout.Toggle("第一幕可用", pathEvent.enabledInAct1);
            pathEvent.enabledInAct3 = EditorGUILayout.Toggle("第三幕可用", pathEvent.enabledInAct3);
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.BeginVertical();
            pathEvent.enabledInAct2 = EditorGUILayout.Toggle("第二幕可用", pathEvent.enabledInAct2);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            
            // 添加玩家交互范围设置
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("玩家交互设置", EditorStyles.boldLabel);
            
            // 显示精确数值
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("玩家交互范围");
            pathEvent.playerInteractionRadius = EditorGUILayout.Slider(pathEvent.playerInteractionRadius, 1f, 10f);
            pathEvent.playerInteractionRadius = EditorGUILayout.FloatField(pathEvent.playerInteractionRadius, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();
            
            pathEvent.showInteractionRange = EditorGUILayout.Toggle("显示交互范围", pathEvent.showInteractionRange);
            
            // 显示路径点选择
            MultiPointPathCreator pathCreator = PathRegistry.GetPathCreatorByID(config.pathID);
            if (pathCreator != null && pathCreator.pathPointsParent != null)
            {
                int pointCount = pathCreator.pathPointsParent.childCount;
                string[] pointOptions = new string[pointCount];
                
                for (int i = 0; i < pointCount; i++)
                {
                    float relativePos = (float)i / (pointCount - 1);
                    pointOptions[i] = $"点 {i} ({relativePos:P0})";
                }
                
                // 确保索引在范围内
                pathEvent.startPointIndex = Mathf.Clamp(pathEvent.startPointIndex, 0, pointCount - 1);
                pathEvent.endPointIndex = Mathf.Clamp(pathEvent.endPointIndex, 0, pointCount - 1);
                
                // 起始点选择 - 更简洁的界面
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("路径段设置", EditorStyles.boldLabel);
                
                // 添加起始点和结束点索引的精确值显示
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("起始点");
                int newStartIndex = EditorGUILayout.Popup(pathEvent.startPointIndex, pointOptions, GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.5f - 80));
                pathEvent.startPointIndex = EditorGUILayout.IntField(pathEvent.startPointIndex, GUILayout.Width(40));
                if (newStartIndex != pathEvent.startPointIndex)
                {
                    pathEvent.startPointIndex = newStartIndex;
                    EditorUtility.SetDirty(npcData);
                }
                
                // 添加定位按钮
                if (GUILayout.Button("定位", GUILayout.Width(50)))
                {
                    Transform point = pathCreator.pathPointsParent.GetChild(pathEvent.startPointIndex);
                    Selection.activeGameObject = point.gameObject;
                    SceneView.FrameLastActiveSceneView();
                }
                EditorGUILayout.EndHorizontal();
                
                // 结束点选择
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("结束点");
                int newEndIndex = EditorGUILayout.Popup(pathEvent.endPointIndex, pointOptions, GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.5f - 80));
                pathEvent.endPointIndex = EditorGUILayout.IntField(pathEvent.endPointIndex, GUILayout.Width(40));
                if (newEndIndex != pathEvent.endPointIndex)
                {
                    pathEvent.endPointIndex = newEndIndex;
                    EditorUtility.SetDirty(npcData);
                }
                
                // 添加定位按钮
                if (GUILayout.Button("定位", GUILayout.Width(50)))
                {
                    Transform point = pathCreator.pathPointsParent.GetChild(pathEvent.endPointIndex);
                    Selection.activeGameObject = point.gameObject;
                    SceneView.FrameLastActiveSceneView();
                }
                EditorGUILayout.EndHorizontal();
                
                // 添加一些辅助按钮，帮助快速设置点位置
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("设置结束点为起始点的下一个点"))
                {
                    pathEvent.endPointIndex = Mathf.Min(pathEvent.startPointIndex + 1, pointCount - 1);
                    EditorUtility.SetDirty(npcData);
                }
                
                if (GUILayout.Button("交换起始点和结束点"))
                {
                    int temp = pathEvent.startPointIndex;
                    pathEvent.startPointIndex = pathEvent.endPointIndex;
                    pathEvent.endPointIndex = temp;
                    EditorUtility.SetDirty(npcData);
                }
                EditorGUILayout.EndHorizontal();
                
                // 添加一个按钮，方便设置路径段长度
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("路径段长度");
                int segmentLength = Mathf.Abs(pathEvent.endPointIndex - pathEvent.startPointIndex);
                int newSegmentLength = EditorGUILayout.IntSlider(segmentLength, 1, pointCount - 1 - Mathf.Min(pathEvent.startPointIndex, pathEvent.endPointIndex));
                if (newSegmentLength != segmentLength)
                {
                    if (pathEvent.endPointIndex > pathEvent.startPointIndex)
                    {
                        pathEvent.endPointIndex = pathEvent.startPointIndex + newSegmentLength;
                    }
                    else
                    {
                        pathEvent.startPointIndex = pathEvent.endPointIndex + newSegmentLength;
                    }
                    EditorUtility.SetDirty(npcData);
                }
                EditorGUILayout.EndHorizontal();
                
                // 路径段可视化预览
                if (pathEvent.startPointIndex != pathEvent.endPointIndex)
                {
                    string segmentInfo = "";
                    int start = Mathf.Min(pathEvent.startPointIndex, pathEvent.endPointIndex);
                    int end = Mathf.Max(pathEvent.startPointIndex, pathEvent.endPointIndex);
                    
                    for (int i = start; i <= end; i++)
                    {
                        float relativePos = (float)i / (pointCount - 1);
                        segmentInfo += $"{relativePos:P0}";
                        if (i < end)
                            segmentInfo += " → ";
                    }
                    
                    string direction = pathEvent.startPointIndex < pathEvent.endPointIndex ? "从前向后" : "从后向前";
                    EditorGUILayout.HelpBox($"路径段: {segmentInfo}\n方向: {direction}", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("警告：起始点和结束点相同，这将导致不明确的路径段", MessageType.Warning);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("找不到路径点，请确保选择了有效的路径ID且路径点已生成。", MessageType.Warning);
                // 直接输入索引
                pathEvent.startPointIndex = EditorGUILayout.IntField("起始点索引", pathEvent.startPointIndex);
                pathEvent.endPointIndex = EditorGUILayout.IntField("结束点索引", pathEvent.endPointIndex);
            }
            
            // 手势检测设置 - 公开具体值
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("手势检测设置", EditorStyles.boldLabel);
            
            // 使用滑块和字段一起显示，更清晰地展示数值
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("手势保持时间(秒)");
            float newGestureHoldTime = EditorGUILayout.Slider(pathEvent.gestureHoldTime, 0.5f, 5f);
            pathEvent.gestureHoldTime = EditorGUILayout.FloatField(newGestureHoldTime, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("最大识别距离(米)");
            float newMaxDistance = EditorGUILayout.Slider(pathEvent.maxRecognitionDistance, 1f, 15f);
            pathEvent.maxRecognitionDistance = EditorGUILayout.FloatField(newMaxDistance, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();
            
            // 默认反应
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("默认反应(无手势)", EditorStyles.boldLabel);
            DrawGestureResponse(pathEvent.defaultResponse, true);
            
            // 手势列表
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("手势反应列表", EditorStyles.boldLabel);
            
            if (pathEvent.gestureResponses.Count == 0)
            {
                EditorGUILayout.HelpBox("没有手势反应。点击下方按钮添加手势反应。", MessageType.Info);
            }
            else
            {
                for (int i = 0; i < pathEvent.gestureResponses.Count; i++)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"手势 {i+1}: {pathEvent.gestureResponses[i].gestureType}");
                    
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.Space(EditorGUI.indentLevel * 15);
                    if (GUILayout.Button("删除手势", GUILayout.Width(80)))
                    {
                        pathEvent.gestureResponses.RemoveAt(i);
                        EditorUtility.SetDirty(npcData);
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                    
                    DrawGestureResponse(pathEvent.gestureResponses[i], false);
                    EditorGUILayout.EndVertical();
                }
            }
            
            if (GUILayout.Button("添加手势反应"))
            {
                AddGestureToEvent(pathEvent);
            }
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawGestureResponse(GestureResponse response, bool isDefault)
    {
        EditorGUI.indentLevel++;
        
        if (!isDefault)
        {
            // 手势类型 - 改为使用PlayerManager中定义的手影类型
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("手影类型");
            
            // 使用ShadowType枚举中的值
            string[] shadowTypes = new string[] { "Bird", "Wolf", "Deer", "Sheep", "Goose" };
            int selectedIndex = 0;
            
            for (int i = 0; i < shadowTypes.Length; i++)
            {
                if (shadowTypes[i] == response.gestureType)
                {
                    selectedIndex = i;
                    break;
                }
            }
            
            int newIndex = EditorGUILayout.Popup(selectedIndex, shadowTypes);
            if (newIndex != selectedIndex)
            {
                response.gestureType = shadowTypes[newIndex];
                EditorUtility.SetDirty(npcData);
            }
            
            EditorGUILayout.EndHorizontal();
            
            // 删除置信度设置，这在您的系统中不需要
            // response.minConfidence = EditorGUILayout.Slider("最低置信度", response.minConfidence, 0f, 1f);
        }
        
        // 基本响应配置
        EditorGUILayout.LabelField("响应设置", EditorStyles.boldLabel);
        
        // 添加分数影响的说明文本
        EditorGUILayout.HelpBox("分数影响可以是正值(增加)或负值(减少)", MessageType.Info);
        response.scoreEffect = EditorGUILayout.FloatField("分数影响", response.scoreEffect);
        
        response.animationName = EditorGUILayout.TextField("动画名称", response.animationName);
        response.dialogueText = EditorGUILayout.TextArea(response.dialogueText, GUILayout.Height(40));
        response.completionDelay = EditorGUILayout.FloatField("完成延迟(秒)", response.completionDelay);
        
        EditorGUI.indentLevel--;
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
                PathConfig config = npcData.paths.Find(p => p.nextPaths.Contains(branch));
                if (config != null)
                {
                    config.nextPaths.Remove(branch);
                    EditorUtility.SetDirty(npcData);
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
            EditorUtility.SetDirty(npcData);
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
    
    private void AddNewPath()
    {
        PathConfig newPath = new PathConfig
        {
            pathName = "新路径",
            minScore = 0,
            maxScore = 100,
            events = new List<PathEvent>(),
            nextPaths = new List<PathBranch>()
        };
        
        npcData.paths.Add(newPath);
        
        // 展开新路径
        string key = string.IsNullOrEmpty(newPath.pathID) ? $"path_{npcData.paths.Count - 1}" : newPath.pathID;
        pathFoldouts[key] = true;
        
        EditorUtility.SetDirty(npcData);
    }
    
    private void AddEventToPath(PathConfig config)
    {
        string eventId = $"Event_{config.pathName}_{config.events.Count + 1}";
        
        PathEvent newEvent = new PathEvent
        {
            eventID = eventId,
            startPointIndex = 0, // 设置默认值
            endPointIndex = 0,   // 设置默认值
            maxRecognitionDistance = 8.0f, // 设置默认值
            gestureHoldTime = 1f,
            gestureResponses = new List<GestureResponse>(),
            defaultResponse = new GestureResponse { scoreEffect = 0 }
        };
        
        config.events.Add(newEvent);
        
        // 展开新事件
        eventFoldouts[eventId] = true;
        
        EditorUtility.SetDirty(npcData);
    }
    
    private void AddGestureToEvent(PathEvent pathEvent)
    {
        GestureResponse newResponse = new GestureResponse
        {
            gestureType = "Bird", // 默认值
            scoreEffect = 10,     // 默认加10分
            //minConfidence = 0.7f
        };
        
        pathEvent.gestureResponses.Add(newResponse);
        EditorUtility.SetDirty(npcData);
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
        EditorUtility.SetDirty(npcData);
    }
}
#endif