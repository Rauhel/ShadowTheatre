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
    private EventDataInspector eventInspector;

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
        eventInspector = new EventDataInspector();
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
            eventInspector.ShowEventsInfo(selectedNPCData);
        }
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
            eventInspector.ClearCache();

            // 将数据传递给子系统
            graphRenderer.SetNPCData(selectedNPCData);
            pathInspector.SetNPCData(selectedNPCData);
            eventInspector.SetNPCData(selectedNPCData);
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