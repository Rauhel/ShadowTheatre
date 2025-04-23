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

    // 修改 DrawEventTriggers 方法
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
            // 验证起始和结束索引
            if (pathEvent.startPointIndex < 0 || pathEvent.startPointIndex >= manager.CurrentPathPointsParent.childCount ||
                pathEvent.endPointIndex < 0 || pathEvent.endPointIndex >= manager.CurrentPathPointsParent.childCount)
                continue;

            // 获取事件起始和结束点
            Transform startPoint = manager.CurrentPathPointsParent.GetChild(pathEvent.startPointIndex);
            Transform endPoint = manager.CurrentPathPointsParent.GetChild(pathEvent.endPointIndex);
            
            // 确定颜色
            Color segmentColor;
            if (!Application.isPlaying)
            {
                // 编辑模式下显示为蓝色
                segmentColor = new Color(0.2f, 0.5f, 0.8f, 0.3f);
            }
            else
            {
                // 运行时根据状态变化颜色
                segmentColor = (manager.IsEventDetectable && manager.CurrentPathEvent == pathEvent) 
                    ? new Color(0.2f, 0.8f, 0.2f, 0.3f)  // 绿色
                    : new Color(0.8f, 0.2f, 0.2f, 0.3f); // 红色
            }

            // 绘制起始点和结束点（结束点更大一些）
            Handles.color = segmentColor;
            Handles.DrawSolidDisc(startPoint.position, Vector3.up, 0.4f);
            Handles.DrawSolidDisc(endPoint.position, Vector3.up, 0.6f);
            
            // 绘制整个路径段内所有点
            if (pathEvent.startPointIndex != pathEvent.endPointIndex)
            {
                int start = Mathf.Min(pathEvent.startPointIndex, pathEvent.endPointIndex);
                int end = Mathf.Max(pathEvent.startPointIndex, pathEvent.endPointIndex);
                
                for (int i = start + 1; i < end; i++)
                {
                    Transform pointInSegment = manager.CurrentPathPointsParent.GetChild(i);
                    Handles.DrawSolidDisc(pointInSegment.position, Vector3.up, 0.25f);
                }
            }
            
            // 绘制连线 - 更粗壮的线
            if (pathEvent.startPointIndex != pathEvent.endPointIndex)
            {
                // 连接所有点，形成完整路径
                int start = Mathf.Min(pathEvent.startPointIndex, pathEvent.endPointIndex);
                int end = Mathf.Max(pathEvent.startPointIndex, pathEvent.endPointIndex);
                
                for (int i = start; i < end; i++)
                {
                    Transform current = manager.CurrentPathPointsParent.GetChild(i);
                    Transform next = manager.CurrentPathPointsParent.GetChild(i + 1);
                    
                    // 使用更粗的线
                    Handles.DrawAAPolyLine(5f, current.position + Vector3.up * 0.05f, next.position + Vector3.up * 0.05f);
                }
            }
            
            // 显示事件ID和方向标记
            GUIStyle labelStyle = new GUIStyle();
            labelStyle.normal.textColor = Color.white;
            labelStyle.fontStyle = FontStyle.Bold;
            labelStyle.alignment = TextAnchor.MiddleCenter;
            
            // 计算路径段中点位置用于显示标签
            Vector3 midPoint;
            if (pathEvent.startPointIndex != pathEvent.endPointIndex)
            {
                // 计算所有点的中心位置
                int start = Mathf.Min(pathEvent.startPointIndex, pathEvent.endPointIndex);
                int end = Mathf.Max(pathEvent.startPointIndex, pathEvent.endPointIndex);
                int midIndex = start + (end - start) / 2;
                midPoint = manager.CurrentPathPointsParent.GetChild(midIndex).position;
            }
            else
            {
                // 如果起始和结束点相同，就用那个点
                midPoint = startPoint.position;
            }
            
            // 在中点显示事件ID
            Handles.Label(midPoint + Vector3.up * 0.7f, pathEvent.eventID, labelStyle);
            
            // 在游戏运行时显示更多信息
            if (Application.isPlaying && manager.IsEventDetectable && manager.CurrentPathEvent == pathEvent)
            {
                // 显示手势要求和距离参数
                string gestureInfo = "";
                if (pathEvent.gestureResponses.Count > 0)
                {
                    gestureInfo = "需要手势: ";
                    foreach (var response in pathEvent.gestureResponses)
                    {
                        gestureInfo += $"{response.gestureType} ";
                    }
                    gestureInfo += $"\n保持时间: {pathEvent.gestureHoldTime}秒, 最大距离: {pathEvent.maxRecognitionDistance:F1}米";
                }
                else
                {
                    gestureInfo = "无手势要求";
                }
                
                Handles.Label(
                    midPoint + Vector3.up * 1.2f, 
                    gestureInfo, 
                    labelStyle
                );
                
                // 显示交互范围
                if (pathEvent.showInteractionRange)
                {
                    Handles.color = new Color(0.2f, 0.8f, 0.2f, 0.2f);
                    Handles.DrawWireDisc(manager.transform.position, Vector3.up, pathEvent.playerInteractionRadius);
                }
            }
        }
    }
}
#endif