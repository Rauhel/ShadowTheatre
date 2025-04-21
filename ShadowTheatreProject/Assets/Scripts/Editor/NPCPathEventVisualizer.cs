#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[InitializeOnLoad]
public class NPCPathEventVisualizer
{
    // 静态构造函数，注册场景GUI回调
    static NPCPathEventVisualizer()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    // 在场景视图中绘制
    private static void OnSceneGUI(SceneView sceneView)
    {
        // 检查当前选中的物体
        if (Selection.activeGameObject == null)
            return;

        // 检查是否选中了路径创建器
        MultiPointPathCreator pathCreator = Selection.activeGameObject.GetComponent<MultiPointPathCreator>();
        if (pathCreator != null)
        {
            VisualizePath(pathCreator);
        }

        // 检查是否选中了NPC控制器
        NPCController controller = Selection.activeGameObject.GetComponent<NPCController>();
        if (controller != null && controller.Data != null)
        {
            VisualizeNPCEvents(controller.Data);
        }
    }

    // 可视化路径上的事件
    private static void VisualizePath(MultiPointPathCreator pathCreator)
    {
        if (pathCreator.pathPointsParent == null || pathCreator.pathPointsParent.childCount == 0)
            return;

        // 查找所有NPC数据资源
        string[] guids = AssetDatabase.FindAssets("t:NPCData");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            NPCData data = AssetDatabase.LoadAssetAtPath<NPCData>(path);
            
            if (data == null || data.paths == null)
                continue;

            // 查找使用此路径的路径配置
            foreach (var pathConfig in data.paths)
            {
                if (pathConfig.pathID == pathCreator.pathID)
                {
                    // 绘制事件触发区域
                    DrawPathEvents(pathCreator, pathConfig, data.npcName);
                    break;
                }
            }
        }
    }

    // 绘制路径上的事件触发区域
    private static void DrawPathEvents(MultiPointPathCreator pathCreator, PathConfig pathConfig, string npcName)
    {
        if (pathConfig.events == null || pathConfig.events.Count == 0)
            return;

        Transform pathPoints = pathCreator.pathPointsParent;
        
        foreach (var pathEvent in pathConfig.events)
        {
            if (pathEvent.pathPointIndex < 0 || pathEvent.pathPointIndex >= pathPoints.childCount)
                continue;

            // 获取触发点
            Transform triggerPoint = pathPoints.GetChild(pathEvent.pathPointIndex);
            
            // 绘制触发圆
            Handles.color = new Color(1f, 0.5f, 0.5f, 0.3f);
            Handles.DrawSolidDisc(triggerPoint.position, Vector3.up, pathEvent.triggerRadius);
            
            // 绘制轮廓
            Handles.color = new Color(1f, 0.3f, 0.3f, 0.8f);
            Handles.DrawWireDisc(triggerPoint.position, Vector3.up, pathEvent.triggerRadius);
            
            // 绘制标签
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = Color.white;
            style.alignment = TextAnchor.MiddleCenter;
            
            Handles.Label(
                triggerPoint.position + Vector3.up * 1.5f, 
                $"{npcName}: {pathEvent.eventID}",
                style
            );
            
            // 绘制手势类型
            if (pathEvent.gestureResponses.Count > 0)
            {
                string gestures = "";
                for (int i = 0; i < pathEvent.gestureResponses.Count; i++)
                {
                    if (i > 0) gestures += ", ";
                    gestures += pathEvent.gestureResponses[i].gestureType;
                }
                
                Handles.Label(
                    triggerPoint.position + Vector3.up * 2f, 
                    $"手势: {gestures}",
                    EditorStyles.whiteMiniLabel
                );
            }
        }
    }

    // 可视化NPC的所有事件
    private static void VisualizeNPCEvents(NPCData data)
    {
        if (data.paths == null)
            return;

        foreach (var pathConfig in data.paths)
        {
            if (string.IsNullOrEmpty(pathConfig.pathID))
                continue;

            MultiPointPathCreator pathCreator = PathRegistry.GetPathCreatorByID(pathConfig.pathID);
            if (pathCreator != null && pathCreator.pathPointsParent != null && pathCreator.pathPointsParent.childCount > 0)
            {
                DrawPathEvents(pathCreator, pathConfig, data.npcName);
            }
        }
    }
}
#endif