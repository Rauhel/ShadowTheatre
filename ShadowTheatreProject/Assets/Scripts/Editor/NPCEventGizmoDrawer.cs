using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

#if UNITY_EDITOR
[InitializeOnLoad]
public class NPCEventGizmoDrawer
{
    // 静态构造函数，确保编辑器加载时注册
    static NPCEventGizmoDrawer()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    static void OnSceneGUI(SceneView sceneView)
    {
        // 查找场景中所有的NPCEventManager
        NPCEventManager[] managers = Object.FindObjectsOfType<NPCEventManager>();
        foreach (var manager in managers)
        {
            DrawEventTriggers(manager);
        }
    }

    static void DrawEventTriggers(NPCEventManager manager)
    {
        if (manager == null || manager.CurrentPathPointsParent == null)
            return;

        // 获取当前路径的所有事件
        List<PathEvent> events = manager.CurrentPathEvents;
        if (events == null || events.Count == 0)
            return;

        foreach (var pathEvent in events)
        {
            if (pathEvent.pathPointIndex < 0 || pathEvent.pathPointIndex >= manager.CurrentPathPointsParent.childCount)
                continue;

            // 获取事件触发点
            Transform triggerPoint = manager.CurrentPathPointsParent.GetChild(pathEvent.pathPointIndex);
            
            // 确定颜色 - 编辑模式下始终红色；运行时根据状态变化
            Color triggerColor;
            
            if (!Application.isPlaying)
            {
                // 编辑模式下始终显示为红色
                triggerColor = new Color(0.8f, 0.2f, 0.2f, 0.3f); // 红色半透明
            }
            else
            {
                // 运行时根据状态变化颜色
                triggerColor = (manager.isEventDetectable && manager.currentPathEvent == pathEvent) 
                    ? new Color(0.2f, 0.8f, 0.2f, 0.3f)  // 绿色半透明
                    : new Color(0.8f, 0.2f, 0.2f, 0.3f); // 红色半透明
            }

            // 绘制触发区域
            Handles.color = triggerColor;
            Handles.DrawSolidDisc(triggerPoint.position, Vector3.up, pathEvent.triggerRadius);
            
            // 绘制轮廓
            Handles.color = new Color(triggerColor.r, triggerColor.g, triggerColor.b, 0.8f);
            Handles.DrawWireDisc(triggerPoint.position, Vector3.up, pathEvent.triggerRadius);
            
            // 绘制事件ID
            GUIStyle labelStyle = new GUIStyle();
            labelStyle.normal.textColor = Color.white;
            labelStyle.fontStyle = FontStyle.Bold;
            labelStyle.alignment = TextAnchor.MiddleCenter;
            Handles.Label(triggerPoint.position + Vector3.up * 0.5f, pathEvent.eventID, labelStyle);
            
            // 游戏运行时显示更多信息
            if (Application.isPlaying && manager.isEventDetectable && manager.currentPathEvent == pathEvent)
            {
                // 显示手势要求和时间参数
                string gestureInfo = "";
                if (pathEvent.gestureResponses.Count > 0)
                {
                    gestureInfo = "需要手势: ";
                    foreach (var response in pathEvent.gestureResponses)
                    {
                        gestureInfo += $"{response.gestureType} ";
                    }
                    gestureInfo += $"\n保持时间: {pathEvent.gestureHoldTime}秒, 时限: {pathEvent.gestureTimeLimit}秒";
                }
                else
                {
                    gestureInfo = "无手势要求";
                }
                
                Handles.Label(
                    triggerPoint.position + Vector3.up * 1.0f, 
                    gestureInfo, 
                    labelStyle
                );
            }
            else if (!Application.isPlaying)
            {
                // 在编辑模式下显示基本事件信息
                string eventInfo = "";
                if (pathEvent.gestureResponses.Count > 0)
                {
                    eventInfo = "手势: ";
                    foreach (var response in pathEvent.gestureResponses)
                    {
                        eventInfo += $"{response.gestureType} ";
                    }
                }
                else
                {
                    eventInfo = "无手势要求";
                }
                
                Handles.Label(
                    triggerPoint.position + Vector3.up * 1.0f, 
                    eventInfo, 
                    labelStyle
                );
            }
        }
    }
}
#endif