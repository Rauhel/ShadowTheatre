#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(MultiPointPathCreator))]
public class MultiPointPathCreatorEditor : Editor
{
    private MultiPointPathCreator pathCreator;
    
    private void OnEnable()
    {
        pathCreator = (MultiPointPathCreator)target;
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        // 绘制默认属性
        DrawDefaultInspector();
        
        EditorGUILayout.Space(10);
        
        // 控制点操作按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("添加控制点"))
        {
            Undo.RecordObject(pathCreator, "Add Control Point");
            pathCreator.AddControlPoint();
            EditorUtility.SetDirty(pathCreator);
        }
        
        if (GUILayout.Button("移除最后一点"))
        {
            Undo.RecordObject(pathCreator, "Remove Last Control Point");
            pathCreator.RemoveLastControlPoint();
            EditorUtility.SetDirty(pathCreator);
        }
        EditorGUILayout.EndHorizontal();
        
        if (GUILayout.Button("均匀分布控制点"))
        {
            EvenlyDistributePoints();
        }
        
        EditorGUILayout.Space(5);
        
        if (GUILayout.Button("生成路径点"))
        {
            Undo.RecordObject(pathCreator, "Generate Path Points");
            pathCreator.GeneratePathPoints();
            EditorUtility.SetDirty(pathCreator);
        }
        
        EditorGUILayout.Space(10);
        
        // 如果设置了前一路径且有控制点，添加一个按钮来对齐到前一路径的末端
        if (pathCreator.previousPath != null && pathCreator.controlPoints.Count > 0)
        {
            if (GUILayout.Button("对齐到前一路径终点"))
            {
                AlignToEndOfPreviousPath();
            }
        }
        
        if (GUILayout.Button("更新起点和终点引用"))
        {
            Undo.RecordObject(pathCreator, "Update Path Endpoints");
            pathCreator.UpdateStartAndEndPoints();
            EditorUtility.SetDirty(pathCreator);
        }
        
        // 显示警告信息
        if (!HasNavMesh())
        {
            EditorGUILayout.HelpBox("场景中没有NavMesh！请先烘焙NavMesh，否则无法计算正确的路径。", MessageType.Warning);
            
            if (GUILayout.Button("打开Navigation窗口"))
            {
                EditorApplication.ExecuteMenuItem("Window/AI/Navigation");
            }
        }
        
        if (GUI.changed)
        {
            EditorUtility.SetDirty(pathCreator);
        }
    }
    
    private void EvenlyDistributePoints()
    {
        if (pathCreator.controlPoints.Count < 2)
            return;
            
        Undo.RecordObject(pathCreator, "Evenly Distribute Points");
        
        List<Transform> validPoints = new List<Transform>();
        foreach (var point in pathCreator.controlPoints)
        {
            if (point != null) validPoints.Add(point);
        }
        
        if (validPoints.Count < 2)
            return;
            
        // 计算总路径长度
        float totalLength = 0;
        for (int i = 0; i < validPoints.Count - 1; i++)
        {
            totalLength += Vector3.Distance(validPoints[i].position, validPoints[i + 1].position);
        }
        
        // 平均每段长度
        float segmentLength = totalLength / (validPoints.Count - 1);
        
        // 重新分布点
        Vector3 startPos = validPoints[0].position;
        for (int i = 1; i < validPoints.Count; i++)
        {
            Vector3 dir = (validPoints[i].position - startPos).normalized;
            validPoints[i].position = startPos + dir * segmentLength;
            startPos = validPoints[i].position;
        }
        
        pathCreator.UpdateStartAndEndPoints();
        EditorUtility.SetDirty(pathCreator);
    }
    
    private void AlignToEndOfPreviousPath()
    {
        if (pathCreator.previousPath == null)
            return;
            
        MultiPointPathCreator prevPathCreator = pathCreator.previousPath.GetComponent<MultiPointPathCreator>();
        if (prevPathCreator == null || prevPathCreator.controlPoints.Count == 0)
            return;
        
        // 获取前一路径的终点
        Transform prevEndPoint = prevPathCreator.endPoint;
        if (prevEndPoint == null && prevPathCreator.controlPoints.Count > 0)
        {
            prevEndPoint = prevPathCreator.controlPoints[prevPathCreator.controlPoints.Count - 1];
        }
        
        if (prevEndPoint != null && pathCreator.controlPoints.Count > 0)
        {
            Undo.RecordObject(pathCreator.controlPoints[0], "Align Control Point");
            pathCreator.controlPoints[0].position = prevEndPoint.position;
            EditorUtility.SetDirty(pathCreator.controlPoints[0]);
        }
        
        EditorUtility.SetDirty(pathCreator);
    }
    
    private bool HasNavMesh()
    {
        if (!Application.isPlaying)
        {
            // 检查场景中是否有NavMesh数据
            // 这只是一个简单的检查，可能不总是准确的
            UnityEngine.AI.NavMeshTriangulation triangulation = UnityEngine.AI.NavMesh.CalculateTriangulation();
            return triangulation.vertices.Length > 0;
        }
        return true;
    }
    
    private void OnSceneGUI()
    {
        if (pathCreator.controlPoints.Count < 1)
            return;
            
        // 绘制控制点手柄
        for (int i = 0; i < pathCreator.controlPoints.Count; i++)
        {
            if (pathCreator.controlPoints[i] == null)
                continue;
                
            // 可拖动的控制点
            EditorGUI.BeginChangeCheck();
            Vector3 newPos = Handles.PositionHandle(pathCreator.controlPoints[i].position, Quaternion.identity);
            
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(pathCreator.controlPoints[i], "Move Control Point");
                pathCreator.controlPoints[i].position = newPos;
                EditorUtility.SetDirty(pathCreator.controlPoints[i]);
            }
            
            // 标签
            Handles.Label(pathCreator.controlPoints[i].position + Vector3.up * 0.5f, $"Point {i}");
        }
        
        // 绘制与前一路径的连接
        if (pathCreator.previousPath != null && pathCreator.controlPoints.Count > 0)
        {
            MultiPointPathCreator prevPathCreator = pathCreator.previousPath.GetComponent<MultiPointPathCreator>();
            if (prevPathCreator != null && prevPathCreator.controlPoints.Count > 0)
            {
                Transform prevLastPoint = prevPathCreator.endPoint;
                if (prevLastPoint == null)
                {
                    prevLastPoint = prevPathCreator.controlPoints[prevPathCreator.controlPoints.Count - 1];
                }
                
                Transform currentFirstPoint = pathCreator.startPoint;
                if (currentFirstPoint == null)
                {
                    currentFirstPoint = pathCreator.controlPoints[0];
                }
                
                if (prevLastPoint != null && currentFirstPoint != null)
                {
                    Handles.color = Color.magenta;
                    Handles.DrawDottedLine(prevLastPoint.position, currentFirstPoint.position, 4f);
                    
                    // 添加标签
                    Handles.Label(
                        Vector3.Lerp(prevLastPoint.position, currentFirstPoint.position, 0.5f),
                        "路径连接",
                        EditorStyles.boldLabel
                    );
                }
            }
        }
        
        // 绘制和选择路径点
        if (pathCreator.pathPointsParent != null)
        {
            for (int i = 0; i < pathCreator.GetPathPointCount(); i++)
            {
                Vector3 pointPosition = pathCreator.GetPathPointPosition(i);
                float size = HandleUtility.GetHandleSize(pointPosition) * 0.15f;
                
                // 检查是否被用作事件触发点
                bool isUsedAsEventTrigger = IsPointUsedAsEventTrigger(i);
                
                // 设置颜色
                Handles.color = isUsedAsEventTrigger ? Color.yellow : Color.cyan;
                
                // 绘制一个可以点击的小球
                if (Handles.Button(pointPosition, Quaternion.identity, size, size, Handles.SphereHandleCap))
                {
                    // 创建临时对象进行选择
                    GameObject tempObject = new GameObject($"PathPoint_{i}");
                    tempObject.transform.position = pointPosition;
                    Selection.activeGameObject = tempObject;
                    
                    // 显示右键菜单以便设置为触发点
                    if (Event.current.button == 1)
                    {
                        ShowPathPointContextMenu(i, pathCreator.transform);
                    }
                    
                    // 延迟销毁临时对象
                    EditorApplication.delayCall += () => {
                        if(tempObject != null)
                            GameObject.DestroyImmediate(tempObject);
                    };
                }
                
                // 显示点的相对位置信息和事件ID
                float relativePos = (float)i / (pathCreator.GetPathPointCount() - 1);
                Handles.Label(pointPosition + Vector3.up * 0.3f, $"{relativePos:P0}");
                
                if (isUsedAsEventTrigger)
                {
                    string eventID = GetEventIDForPoint(i);
                    if (!string.IsNullOrEmpty(eventID))
                    {
                        Handles.Label(pointPosition + Vector3.up * 0.6f, $"事件: {eventID}", EditorStyles.boldLabel);
                    }
                }
            }
        }
    }
    
    // 修改 IsPointUsedAsEventTrigger 方法
    private bool IsPointUsedAsEventTrigger(int pointIndex)
    {
        // 查找所有 NPCData 资源
        string[] guids = AssetDatabase.FindAssets("t:NPCData");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            NPCData data = AssetDatabase.LoadAssetAtPath<NPCData>(path);
            
            if (data != null && data.paths != null)
            {
                foreach (var pathData in data.paths)
                {
                    if (pathData.events != null)
                    {
                        foreach (var npcEvent in pathData.events)
                        {
                            if (npcEvent.startPointIndex == pointIndex || npcEvent.endPointIndex == pointIndex)
                                return true;
                        }
                    }
                }
            }
        }
        return false;
    }
    
    // 修改 GetEventIDForPoint 方法
    private string GetEventIDForPoint(int pointIndex)
    {
        // 查找所有 NPCData 资源
        string[] guids = AssetDatabase.FindAssets("t:NPCData");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            NPCData data = AssetDatabase.LoadAssetAtPath<NPCData>(path);
            
            if (data != null && data.paths != null)
            {
                foreach (var pathData in data.paths)
                {
                    // 检查路径ID是否匹配
                    if (pathData.pathID == pathCreator.pathID && pathData.events != null)
                    {
                        foreach (var npcEvent in pathData.events)
                        {
                            // 检查点索引是否匹配事件的起始点或结束点
                            if (npcEvent.startPointIndex == pointIndex || npcEvent.endPointIndex == pointIndex)
                                return npcEvent.eventID;
                        }
                    }
                }
            }
        }
        return string.Empty;
    }
    
    // 修改 ShowPathPointContextMenu 方法
    private void ShowPathPointContextMenu(int pointIndex, Transform pathRoot)
    {
        GenericMenu menu = new GenericMenu();
        
        menu.AddItem(new GUIContent("复制路径点引用"), false, () => {
            EditorGUIUtility.systemCopyBuffer = $"{pathRoot.name}/PathPoint_{pointIndex}";
            Debug.Log($"已复制路径点引用: {pathRoot.name}/PathPoint_{pointIndex}");
        });
        
        if (pointIndex < 0)
        {
            menu.AddDisabledItem(new GUIContent("无法确定点索引"));
            menu.ShowAsContext();
            return;
        }
        
        // 添加到所有NPC数据资源中的所有事件
        string[] guids = AssetDatabase.FindAssets("t:NPCData");
        bool hasNpcData = false;
        
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            NPCData data = AssetDatabase.LoadAssetAtPath<NPCData>(assetPath);
            
            if (data == null || data.paths == null)
                continue;
                
            hasNpcData = true;
            string dataName = data.name;
            
            menu.AddSeparator($"NPC数据/{dataName}/");
            
            foreach (var pathData in data.paths)
            {
                // 只处理与当前路径匹配的路径配置
                if (pathData.pathID != pathCreator.pathID || pathData.events == null)
                    continue;
                    
                // 添加"创建新事件"选项
                menu.AddItem(
                    new GUIContent($"NPC数据/{dataName}/在此点创建新事件"),
                    false,
                    () => {
                        Undo.RecordObject(data, "创建新事件");
                        
                        // 创建新事件
                        PathEvent newEvent = new PathEvent
                        {
                            eventID = $"Event_{pathData.pathName}_{pathData.events.Count + 1}",
                            startPointIndex = pointIndex,
                            endPointIndex = Mathf.Min(pointIndex + 1, pathCreator.GetPathPointCount() - 1),
                            gestureHoldTime = 2.0f,
                            maxRecognitionDistance = 8.0f,
                            playerInteractionRadius = 3.0f,
                            enabledInAct1 = true,
                            enabledInAct2 = true,
                            enabledInAct3 = true,
                            gestureResponses = new List<GestureResponse>(),
                            defaultResponse = new GestureResponse { scoreEffect = 0 }
                        };
                        
                        pathData.events.Add(newEvent);
                        EditorUtility.SetDirty(data);
                        AssetDatabase.SaveAssets();
                        Debug.Log($"已在 {dataName} 的路径 {pathData.pathName} 上创建新事件，起始点: {pointIndex}");
                    }
                );
                
                // 添加为现有事件设置起始点/结束点的选项
                foreach (var npcEvent in pathData.events)
                {
                    string eventName = npcEvent.eventID;
                    bool isStartPoint = (npcEvent.startPointIndex == pointIndex);
                    bool isEndPoint = (npcEvent.endPointIndex == pointIndex);
                    
                    menu.AddItem(
                        new GUIContent($"NPC数据/{dataName}/设为事件 \"{eventName}\" 的起始点"),
                        isStartPoint,
                        () => {
                            Undo.RecordObject(data, "设置事件起始点");
                            npcEvent.startPointIndex = pointIndex;
                            EditorUtility.SetDirty(data);
                            AssetDatabase.SaveAssets();
                            Debug.Log($"已将点 {pointIndex} 设置为 {dataName} 中事件 {eventName} 的起始点");
                        }
                    );
                    
                    menu.AddItem(
                        new GUIContent($"NPC数据/{dataName}/设为事件 \"{eventName}\" 的结束点"),
                        isEndPoint,
                        () => {
                            Undo.RecordObject(data, "设置事件结束点");
                            npcEvent.endPointIndex = pointIndex;
                            EditorUtility.SetDirty(data);
                            AssetDatabase.SaveAssets();
                            Debug.Log($"已将点 {pointIndex} 设置为 {dataName} 中事件 {eventName} 的结束点");
                        }
                    );
                }
            }
        }
        
        if (!hasNpcData)
        {
            menu.AddDisabledItem(new GUIContent("没有找到 NPC 数据资源"));
        }
        
        menu.ShowAsContext();
    }
}
#endif