#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq; // 添加LINQ命名空间

public class NPCPathEventEditor
{
    private NPCDataEditor mainEditor;
    
    public NPCPathEventEditor(NPCDataEditor editor)
    {
        mainEditor = editor;
    }
    
    public void DrawPathEvent(PathConfig config, PathEvent pathEvent, int index)
    {
        string key = string.IsNullOrEmpty(pathEvent.eventID) ? $"event_{index}" : pathEvent.eventID;
        
        if (!mainEditor.EventFoldouts.ContainsKey(key))
        {
            mainEditor.EventFoldouts[key] = false;
        }
        
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        EditorGUILayout.BeginHorizontal();
        mainEditor.EventFoldouts[key] = EditorGUILayout.Foldout(mainEditor.EventFoldouts[key], $"事件 {index+1}: {pathEvent.eventID}", true);
        
        if (GUILayout.Button("删除", GUILayout.Width(60)))
        {
            if (EditorUtility.DisplayDialog("确认删除", "确定要删除此事件?", "删除", "取消"))
            {
                config.events.RemoveAt(index);
                EditorUtility.SetDirty(mainEditor.Data);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
        }
        EditorGUILayout.EndHorizontal();
        
        if (mainEditor.EventFoldouts[key])
        {
            EditorGUI.indentLevel++;
            
            DrawEventBasicInfo(pathEvent);
            DrawEventAvailability(pathEvent);
            DrawEventInteractionSettings(pathEvent, config);
            DrawEventPathSegment(pathEvent, config);
            DrawGestureSettings(pathEvent);
            DrawDefaultResponse(pathEvent);
            DrawGestureResponses(pathEvent);
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawEventBasicInfo(PathEvent pathEvent)
    {
        pathEvent.eventID = EditorGUILayout.TextField("事件ID", pathEvent.eventID);
    }
    
    private void DrawEventAvailability(PathEvent pathEvent)
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("事件可用性控制", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical(GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.4f));
        pathEvent.enabledInAct1 = EditorGUILayout.Toggle("第一幕可用", pathEvent.enabledInAct1);
        pathEvent.enabledInAct3 = EditorGUILayout.Toggle("第三幕可用", pathEvent.enabledInAct3);
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.BeginVertical();
        pathEvent.enabledInAct2 = EditorGUILayout.Toggle("第二幕可用", pathEvent.enabledInAct2);
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawEventInteractionSettings(PathEvent pathEvent, PathConfig config)
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("玩家交互设置", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("玩家交互范围");
        
        float newValue = EditorGUILayout.Slider(pathEvent.playerInteractionRadius, 1f, 10f);
        
        GUILayout.Space(10);
        
        newValue = EditorGUILayout.FloatField(newValue, GUILayout.Width(50));
        
        if (newValue != pathEvent.playerInteractionRadius)
        {
            pathEvent.playerInteractionRadius = newValue;
            EditorUtility.SetDirty(mainEditor.Data);
        }
        
        EditorGUILayout.EndHorizontal();
        
        pathEvent.showInteractionRange = EditorGUILayout.Toggle("显示交互范围", pathEvent.showInteractionRange);
    }
    
    private void DrawEventPathSegment(PathEvent pathEvent, PathConfig config)
    {
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
            
            pathEvent.startPointIndex = Mathf.Clamp(pathEvent.startPointIndex, 0, pointCount - 1);
            pathEvent.endPointIndex = Mathf.Clamp(pathEvent.endPointIndex, 0, pointCount - 1);
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("路径段设置", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("起始点");
            int newStartIndex = EditorGUILayout.Popup(pathEvent.startPointIndex, pointOptions, GUILayout.Width(150));
            pathEvent.startPointIndex = EditorGUILayout.IntField(pathEvent.startPointIndex, GUILayout.Width(40));
            if (newStartIndex != pathEvent.startPointIndex)
            {
                pathEvent.startPointIndex = newStartIndex;
                EditorUtility.SetDirty(mainEditor.Data);
            }
            
            if (GUILayout.Button("定位", GUILayout.Width(50)))
            {
                Transform point = pathCreator.pathPointsParent.GetChild(pathEvent.startPointIndex);
                Selection.activeGameObject = point.gameObject;
                SceneView.FrameLastActiveSceneView();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("结束点");
            int newEndIndex = EditorGUILayout.Popup(pathEvent.endPointIndex, pointOptions, GUILayout.Width(150));
            pathEvent.endPointIndex = EditorGUILayout.IntField(pathEvent.endPointIndex, GUILayout.Width(40));
            if (newEndIndex != pathEvent.endPointIndex)
            {
                pathEvent.endPointIndex = newEndIndex;
                EditorUtility.SetDirty(mainEditor.Data);
            }
            
            if (GUILayout.Button("定位", GUILayout.Width(50)))
            {
                Transform point = pathCreator.pathPointsParent.GetChild(pathEvent.endPointIndex);
                Selection.activeGameObject = point.gameObject;
                SceneView.FrameLastActiveSceneView();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("设置结束点为起始点的下一个点"))
            {
                pathEvent.endPointIndex = Mathf.Min(pathEvent.startPointIndex + 1, pointCount - 1);
                EditorUtility.SetDirty(mainEditor.Data);
            }
            
            if (GUILayout.Button("交换起始点和结束点"))
            {
                int temp = pathEvent.startPointIndex;
                pathEvent.startPointIndex = pathEvent.endPointIndex;
                pathEvent.endPointIndex = temp;
                EditorUtility.SetDirty(mainEditor.Data);
            }
            EditorGUILayout.EndHorizontal();
            
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
                EditorUtility.SetDirty(mainEditor.Data);
            }
            EditorGUILayout.EndHorizontal();
            
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
            pathEvent.startPointIndex = EditorGUILayout.IntField("起始点索引", pathEvent.startPointIndex);
            pathEvent.endPointIndex = EditorGUILayout.IntField("结束点索引", pathEvent.endPointIndex);
        }
    }
    
    private void DrawGestureSettings(PathEvent pathEvent)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("手势检测设置", EditorStyles.boldLabel);
        
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
    }
    
    private void DrawDefaultResponse(PathEvent pathEvent)
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("默认反应(无手势)", EditorStyles.boldLabel);
        DrawGestureResponse(pathEvent.defaultResponse, true);
        
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("路径动作", EditorStyles.boldLabel); // 改为"路径动作"，完全保持一致
        mainEditor.ActionEditor.DrawEventActions(pathEvent.defaultResponse);
    }
    
    private void DrawGestureResponses(PathEvent pathEvent)
    {
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
                    EditorUtility.SetDirty(mainEditor.Data);
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
    }
    
    public void DrawGestureResponse(GestureResponse response, bool isDefault)
    {
        EditorGUI.indentLevel++;
        
        if (!isDefault)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("手影类型");
            
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
                EditorUtility.SetDirty(mainEditor.Data);
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.LabelField("响应设置", EditorStyles.boldLabel);
        
        EditorGUILayout.HelpBox("分数影响可以是正值(增加)或负值(减少)", MessageType.Info);
        response.scoreEffect = EditorGUILayout.FloatField("分数影响", response.scoreEffect);
        
        if (!isDefault)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("路径动作", EditorStyles.boldLabel);
            mainEditor.ActionEditor.DrawEventActions(response);
        }
        
        EditorGUI.indentLevel--;
    }
    
    private void AddGestureToEvent(PathEvent pathEvent)
    {
        // 创建新手势响应
        GestureResponse newResponse = new GestureResponse();
        newResponse.gestureType = "Bird";
        newResponse.scoreEffect = 10;
        
        // 初始化动作列表
        newResponse.actions = new List<ActionData>();
        
        // 创建动作
        ActionData gestureAction = new ActionData();
        gestureAction.dialogueText = "手势反应文本";
        gestureAction.displayDuration = 2.0f;
        gestureAction.pathPointIndex = 0;
        
        // 添加到动作列表
        newResponse.actions.Add(gestureAction);
        
        // 添加响应到事件
        pathEvent.gestureResponses.Add(newResponse);
        
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
        
        // 创建单个动作
        ActionData defaultAction = new ActionData();
        defaultAction.dialogueText = "默认反应文本";
        defaultAction.displayDuration = 2.0f;
        defaultAction.pathPointIndex = 0;
        
        // 添加到动作列表
        defaultResponse.actions.Add(defaultAction);
        
        // 设置默认响应
        newEvent.defaultResponse = defaultResponse;
        
        // 添加事件到配置
        config.events.Add(newEvent);
        
        // 展开新事件
        mainEditor.EventFoldouts[eventId] = true;
        
        EditorUtility.SetDirty(mainEditor.Data);
    }
}
#endif