#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(NPCEventManager))]
public class NPCEventManagerEditor : Editor
{
    private bool showEvents = true;
    private bool showPathDecisions = true;
    
    public override void OnInspectorGUI()
    {
        NPCEventManager manager = (NPCEventManager)target;
        
        // 绘制默认检查器
        DrawDefaultInspector();
        
        // 额外功能
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("调试工具", EditorStyles.boldLabel);
        
        if (GUILayout.Button("刷新 NPC 数据引用"))
        {
            SerializedProperty npcDataProp = serializedObject.FindProperty("npcData");
            if (npcDataProp.objectReferenceValue != null)
            {
                EditorUtility.SetDirty(npcDataProp.objectReferenceValue);
            }
        }
        
        if (Application.isPlaying && manager.npcData != null)
        {
            EditorGUILayout.LabelField($"当前分数: {manager.GetCurrentScore()}", EditorStyles.boldLabel);
            
            if (GUILayout.Button("重置 NPC 分数"))
            {
                manager.npcData.currentScore = 0;
            }
        }
        
        // 场景视图可视化控制
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("场景视图可视化", EditorStyles.boldLabel);
        
        showEvents = EditorGUILayout.Foldout(showEvents, "显示事件触发区域");
        showPathDecisions = EditorGUILayout.Foldout(showPathDecisions, "显示路径决策点");
        
        if (GUI.changed)
        {
            EditorUtility.SetDirty(target);
            SceneView.RepaintAll();
        }
    }
    
    // 在场景视图中绘制可视化帮助
    private void OnSceneGUI()
    {
        NPCEventManager manager = (NPCEventManager)target;
        
        if (manager.npcData == null)
            return;
            
        // 绘制事件触发区域
        if (showEvents)
        {
            foreach (var npcEvent in manager.npcData.events)
            {
                if (npcEvent.triggerLocation != null)
                {
                    // 绘制触发区域
                    Handles.color = new Color(1f, 0.5f, 0f, 0.2f);
                    Handles.DrawSolidDisc(npcEvent.triggerLocation.position, Vector3.up, npcEvent.triggerRadius);
                    
                    // 绘制边框
                    Handles.color = new Color(1f, 0.5f, 0f, 0.8f);
                    Handles.DrawWireDisc(npcEvent.triggerLocation.position, Vector3.up, npcEvent.triggerRadius);
                    
                    // 绘制标签
                    Handles.Label(npcEvent.triggerLocation.position + Vector3.up * 2f, 
                                $"Event: {npcEvent.eventID}\nTime: {npcEvent.triggerTimeRange.x}-{npcEvent.triggerTimeRange.y}");
                }
            }
        }
        
        // 绘制路径决策点
        if (showPathDecisions)
        {
            foreach (var decision in manager.npcData.pathDecisions)
            {
                if (decision.decisionLocation != null)
                {
                    // 绘制决策区域
                    Handles.color = new Color(0f, 0.5f, 1f, 0.2f);
                    Handles.DrawSolidDisc(decision.decisionLocation.position, Vector3.up, decision.decisionRadius);
                    
                    // 绘制边框
                    Handles.color = new Color(0f, 0.5f, 1f, 0.8f);
                    Handles.DrawWireDisc(decision.decisionLocation.position, Vector3.up, decision.decisionRadius);
                    
                    // 绘制标签
                    Handles.Label(decision.decisionLocation.position + Vector3.up * 2f,
                                $"Decision: {decision.decisionPointID}\n路径选项: {decision.pathOptions.Count}");
                    
                    // 绘制连接线到各路径起点
                    // 使用彩虹色渐变显示不同的路径选项
                    for (int i = 0; i < decision.pathOptions.Count; i++)
                    {
                        var option = decision.pathOptions[i];
                        if (option.path != null)
                        {
                            // 根据选项索引计算颜色
                            Color pathColor = Color.HSVToRGB(
                                (float)i / Mathf.Max(1, decision.pathOptions.Count),
                                0.7f,
                                1.0f
                            );
                            
                            DrawPathConnection(decision.decisionLocation, option.path, pathColor);
                            
                            // 在路径起点附近添加标签
                            if (option.path.childCount > 0)
                            {
                                Vector3 firstPointPos = option.path.GetChild(0).position;
                                Handles.Label(firstPointPos + Vector3.up * 0.5f,
                                            $"{option.optionName}\n阈值: {option.scoreThreshold}");
                            }
                        }
                    }
                }
            }
        }
    }
    
    private void DrawPathConnection(Transform from, Transform path, Color color)
    {
        if (from == null || path == null || path.childCount == 0)
            return;
            
        Transform firstPoint = path.GetChild(0);
        
        // 绘制从决策点到路径起点的线
        Handles.color = color;
        Handles.DrawDottedLine(from.position, firstPoint.position, 2f);
        
        // 绘制路径点之间的连接
        for (int i = 0; i < path.childCount - 1; i++)
        {
            Handles.color = new Color(color.r, color.g, color.b, 0.3f);
            Handles.DrawLine(path.GetChild(i).position, path.GetChild(i + 1).position);
        }
    }
}
#endif