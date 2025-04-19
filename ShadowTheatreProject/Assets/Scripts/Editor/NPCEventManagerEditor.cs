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
                if (npcEvent.triggerLocation == null)
                    continue;
                
                // 绘制触发区域
                Handles.color = new Color(1f, 0.5f, 0.5f, 0.3f);
                Handles.DrawSolidDisc(npcEvent.triggerLocation.position, Vector3.up, npcEvent.triggerRadius);
                
                // 绘制事件ID标签
                Handles.Label(
                    npcEvent.triggerLocation.position + Vector3.up * 1.5f, 
                    $"事件: {npcEvent.eventID}",
                    EditorStyles.whiteBoldLabel
                );
                
                // 删除对时间范围的引用
                // var timeText = $"时间: {npcEvent.triggerTimeRange.x}-{npcEvent.triggerTimeRange.y}";
                // Handles.Label(
                //     npcEvent.triggerLocation.position + Vector3.up * 1.2f, 
                //     timeText,
                //     EditorStyles.whiteLabel
                // );
            }
        }
        
        // 绘制路径决策点
        if (showPathDecisions)
        {
            foreach (var decision in manager.npcData.pathDecisions)
            {
                if (decision.decisionLocation == null)
                    continue;
                
                // 绘制决策范围
                Handles.color = new Color(0.5f, 0.8f, 1f, 0.3f);
                Handles.DrawSolidDisc(decision.decisionLocation.position, Vector3.up, decision.decisionRadius);
                
                // 绘制决策点ID标签
                Handles.Label(
                    decision.decisionLocation.position + Vector3.up * 1.5f, 
                    $"决策点: {decision.decisionPointID}",
                    EditorStyles.whiteBoldLabel
                );
                
                // 绘制到各选项的连线
                foreach (var option in decision.pathOptions)
                {
                    if (option.path == null || option.path.childCount == 0)
                        continue;
                    
                    // 根据分数阈值颜色渐变
                    Color pathColor = Color.HSVToRGB(Mathf.Clamp01(option.scoreThreshold / 100f), 0.7f, 0.9f);
                    DrawPathConnection(decision.decisionLocation, option.path, pathColor);
                    
                    // 在连线中点显示分数阈值
                    Vector3 midPoint = Vector3.Lerp(
                        decision.decisionLocation.position, 
                        option.path.GetChild(0).position, 
                        0.5f
                    );
                    Handles.Label(
                        midPoint + Vector3.up * 0.5f, 
                        $"分数 >= {option.scoreThreshold}",
                        EditorStyles.whiteLabel
                    );
                }
            }
        }
    }
    
    private void DrawPathConnection(Transform from, Transform path, Color color)
    {
        if (from == null || path == null || path.childCount == 0)
            return;
            
        // 绘制一个有向曲线从决策点到路径起点
        Vector3 startPos = from.position;
        Vector3 endPos = path.GetChild(0).position;
        
        // 计算控制点
        Vector3 startTangent = startPos + from.forward * 2f;
        Vector3 endTangent = endPos + path.GetChild(0).forward * 2f;
        
        Handles.color = color;
        Handles.DrawBezier(startPos, endPos, startTangent, endTangent, color, null, 2f);
        
        // 绘制箭头
        Vector3 dir = (endPos - startPos).normalized;
        Vector3 arrowPos = Vector3.Lerp(startPos, endPos, 0.7f);
        
        // 确保箭头垂直于地面
        Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
        
        Vector3 arrowSize = new Vector3(0.5f, 0, 0.25f);
        Vector3 arrowTip = arrowPos + dir * arrowSize.x;
        Vector3 arrowLeft = arrowPos - right * arrowSize.z;
        Vector3 arrowRight = arrowPos + right * arrowSize.z;
        
        Handles.DrawLine(arrowLeft, arrowTip);
        Handles.DrawLine(arrowRight, arrowTip);
    }
}
#endif