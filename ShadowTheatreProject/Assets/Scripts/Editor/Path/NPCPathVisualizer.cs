#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class NPCPathVisualizer : EditorWindow
{
    [SerializeField] private NPCData selectedNPCData;
    private Vector2 scrollPosition;
    private bool showHelp = false;
    private bool showPathPoints = false;
    private bool showEvents = false;

    // 子系统引用
    private PathGraphRenderer graphRenderer;
    private PathDataInspector pathInspector;

    [MenuItem("Shadow Theatre/NPC Path Visualizer")]
    public static void ShowWindow()
    {
        var window = GetWindow<NPCPathVisualizer>();
        window.titleContent = new GUIContent("NPC路径可视化");
        window.Show();
    }

    private void OnEnable()
    {
        // 初始化子系统
        graphRenderer = new PathGraphRenderer();
        pathInspector = new PathDataInspector();
    }

    private void OnGUI()
    {
        // 工具栏
        DrawToolbar();

        // NPC数据选择区域
        DrawDataSelector();

        // 帮助信息
        if (showHelp)
        {
            EditorGUILayout.HelpBox(
                "路径可视化说明：\n" +
                "- 蓝色节点：普通路径\n" +
                "- 红色节点：路径终点\n" +
                "- 橙色节点：决策点\n" +
                "- 拖拽：移动视图\n" +
                "- 点击节点：选择路径\n" +
                "- Ctrl+点击：选择和聚焦路径",
                MessageType.Info
            );
        }

        // 路径可视化区域
        Rect canvasRect = GUILayoutUtility.GetRect(position.width, position.height - EditorGUIUtility.singleLineHeight * 8);
        HandleCanvasInteraction(canvasRect);
        graphRenderer.DrawPathVisualization(canvasRect, selectedNPCData);

        // 在下方添加标签页功能
        DrawTabs();

        // 显示所选路径的属性或事件信息
        if (showPathPoints && graphRenderer.SelectedPath != null)
        {
            pathInspector.ShowSelectedPathInfo(graphRenderer.SelectedPath, selectedNPCData);
        }
        else if (showEvents && selectedNPCData != null)
        {
            ShowPathEventsInfo(selectedNPCData);
        }
    }

    private void ShowPathEventsInfo(NPCData data)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("路径事件信息", EditorStyles.boldLabel);

        if (data.paths != null && data.paths.Count > 0)
        {
            int totalEvents = 0;

            // 计算总事件数
            foreach (var path in data.paths)
            {
                if (path.events != null)
                {
                    totalEvents += path.events.Count;
                }
            }

            if (totalEvents == 0)
            {
                EditorGUILayout.HelpBox("NPC没有配置任何事件。", MessageType.Info);
            }
            else
            {
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));

                foreach (var path in data.paths)
                {
                    if (path.events == null || path.events.Count == 0)
                        continue;

                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField($"路径: {path.pathName} ({path.pathID})", EditorStyles.boldLabel);

                    for (int i = 0; i < path.events.Count; i++)
                    {
                        PathEvent pathEvent = path.events[i];
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                        // 事件基本信息
                        EditorGUILayout.LabelField($"事件 {i + 1}: {pathEvent.eventID}", EditorStyles.boldLabel);
                        EditorGUILayout.Space(2);

                        // 显示事件位置
                        EditorGUILayout.LabelField($"起始点: {pathEvent.startPointIndex}, 结束点: {pathEvent.endPointIndex}");

                        // 显示启用状态
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("启用状态:", GUILayout.Width(80));
                        EditorGUILayout.LabelField($"幕1: {(pathEvent.enabledInAct1 ? "√" : "×")}", GUILayout.Width(60));
                        EditorGUILayout.LabelField($"幕2: {(pathEvent.enabledInAct2 ? "√" : "×")}", GUILayout.Width(60));
                        EditorGUILayout.LabelField($"幕3: {(pathEvent.enabledInAct3 ? "√" : "×")}", GUILayout.Width(60));
                        EditorGUILayout.EndHorizontal();

                        // 显示手势信息
                        EditorGUILayout.Space(2);
                        EditorGUILayout.LabelField($"手势检测:", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField($"保持时间: {pathEvent.gestureHoldTime}秒, 最远距离: {pathEvent.maxRecognitionDistance}米");

                        if (pathEvent.gestureResponses != null && pathEvent.gestureResponses.Count > 0)
                        {
                            EditorGUILayout.LabelField($"可接受手势:", EditorStyles.boldLabel);
                            foreach (var gesture in pathEvent.gestureResponses)
                            {
                                EditorGUILayout.LabelField($"- {gesture.gestureType}: 分数影响 {(gesture.scoreEffect >= 0 ? "+" : "")}{gesture.scoreEffect}");
                            }
                        }
                        else
                        {
                            EditorGUILayout.LabelField("没有设置手势响应");
                        }

                        // 定位按钮
                        MultiPointPathCreator pathCreator = PathRegistry.GetPathCreatorByID(path.pathID);
                        if (pathCreator != null && pathCreator.pathPointsParent != null)
                        {
                            if (pathEvent.startPointIndex >= 0 && pathEvent.startPointIndex < pathCreator.pathPointsParent.childCount &&
                                GUILayout.Button("在场景中定位"))
                            {
                                Transform point = pathCreator.pathPointsParent.GetChild(pathEvent.startPointIndex);
                                Selection.activeGameObject = point.gameObject;
                                SceneView.FrameLastActiveSceneView();
                                EditorGUIUtility.PingObject(point);
                            }
                        }

                        EditorGUILayout.EndVertical();
                        EditorGUILayout.Space(5);
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("NPC没有配置任何路径。", MessageType.Info);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("居中视图", EditorStyles.toolbarButton))
        {
            graphRenderer.RecenterView();
        }

        if (GUILayout.Button("刷新", EditorStyles.toolbarButton))
        {
            graphRenderer.ClearCache();
            Repaint();
        }

        showHelp = GUILayout.Toggle(showHelp, "帮助", EditorStyles.toolbarButton);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawDataSelector()
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUI.BeginChangeCheck();
        selectedNPCData = EditorGUILayout.ObjectField("NPC数据", selectedNPCData, typeof(NPCData), false) as NPCData;

        if (EditorGUI.EndChangeCheck())
        {
            graphRenderer.ClearCache();
            pathInspector.ClearCache();

            // 将数据传递给子系统
            graphRenderer.SetNPCData(selectedNPCData);
            pathInspector.SetNPCData(selectedNPCData);
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("缩放");
        graphRenderer.ZoomFactor = EditorGUILayout.Slider(graphRenderer.ZoomFactor, 0.5f, 2f);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void HandleCanvasInteraction(Rect canvasRect)
    {
        if (Event.current.type == EventType.MouseDown && canvasRect.Contains(Event.current.mousePosition))
        {
            graphRenderer.StartDrag(Event.current.mousePosition);
        }
        else if (Event.current.type == EventType.MouseUp)
        {
            graphRenderer.EndDrag();
        }
        else if (Event.current.type == EventType.MouseDrag && graphRenderer.IsDragging)
        {
            graphRenderer.UpdateDrag(Event.current.mousePosition);
            Repaint();
        }
    }

    private void DrawTabs()
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.BeginHorizontal();
        bool oldShowPath = showPathPoints;
        bool oldShowEvents = showEvents;

        if (graphRenderer.SelectedPath != null)
        {
            if (GUILayout.Toggle(showPathPoints && !showEvents, "路径点信息", EditorStyles.toolbarButton))
            {
                showPathPoints = true;
                showEvents = false;
            }
        }

        if (selectedNPCData != null)
        {
            if (GUILayout.Toggle(showEvents && !showPathPoints, "事件信息", EditorStyles.toolbarButton))
            {
                showEvents = true;
                showPathPoints = false;
            }
        }

        if (oldShowPath != showPathPoints || oldShowEvents != showEvents)
        {
            Repaint();
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }
}
#endif