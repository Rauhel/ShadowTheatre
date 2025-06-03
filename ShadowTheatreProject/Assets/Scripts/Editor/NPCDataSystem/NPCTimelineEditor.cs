#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// NPC时间轴可视化编辑器
/// 提供直观的时间轴界面来编辑和查看NPC的时间控制配置
/// </summary>
public class NPCTimelineEditor : EditorWindow
{
    private NPCData selectedNPCData;
    private Vector2 scrollPosition;
    private float timelineScale = 1f;
    private float timelineOffset = 0f;
    private const float TIMELINE_HEIGHT = 60f;
    private const float PATH_ROW_HEIGHT = 80f;
    
    // 新增：选中的时间点信息
    private PathConfig selectedPath;
    private PathTimePoint selectedTimePoint;
    private int selectedTimePointIndex = -1;
    
    // 颜色配置
    private static readonly Color pathColor = new Color(0.3f, 0.6f, 1f, 0.8f);
    private static readonly Color timePointColor = new Color(1f, 0.4f, 0.4f, 0.9f);
    private static readonly Color selectedTimePointColor = new Color(1f, 0.8f, 0.2f, 1f);
    private static readonly Color currentTimeColor = new Color(0f, 1f, 0f, 0.8f);
    
    [MenuItem("Shadow Theatre/NPC时间轴编辑器")]
    public static void ShowWindow()
    {
        GetWindow<NPCTimelineEditor>("NPC时间轴").Show();
    }
    
    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("NPC 时间轴可视化编辑器", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        
        // NPC数据选择
        EditorGUI.BeginChangeCheck();
        selectedNPCData = (NPCData)EditorGUILayout.ObjectField("NPC数据", selectedNPCData, typeof(NPCData), false);
        if (EditorGUI.EndChangeCheck())
        {
            Repaint();
        }
        
        if (selectedNPCData == null)
        {
            EditorGUILayout.HelpBox("请选择一个NPC数据文件来查看时间轴", MessageType.Info);
            return;
        }
        
        EditorGUILayout.Space(10);
        
        // 时间轴控制
        DrawTimelineControls();
        
        // 使用说明
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("💡 使用提示:", EditorStyles.miniLabel, GUILayout.Width(70));
        EditorGUILayout.LabelField("点击时间轴上的红色时间点可以进行详细编辑", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(10);
        
        // 绘制时间轴
        DrawTimeline();
        
        EditorGUILayout.Space(10);
        
        // 当前故事时间显示
        DrawCurrentTimeInfo();
        
        // 新增：选中时间点的详细编辑面板
        if (selectedTimePoint != null && selectedPath != null)
        {
            EditorGUILayout.Space(10);
            DrawSelectedTimePointEditor();
        }
    }
    
    private void DrawTimelineControls()
    {
        EditorGUILayout.BeginHorizontal();
        
        EditorGUILayout.LabelField("时间轴控制:", EditorStyles.boldLabel, GUILayout.Width(80));
        
        // 缩放控制 - 修复：添加可输入的FloatField
        EditorGUILayout.LabelField("缩放:", GUILayout.Width(40));
        EditorGUILayout.BeginHorizontal();
        timelineScale = EditorGUILayout.Slider(timelineScale, 0.1f, 5f, GUILayout.Width(100));
        timelineScale = EditorGUILayout.FloatField(timelineScale, GUILayout.Width(50));
        timelineScale = Mathf.Clamp(timelineScale, 0.1f, 5f); // 确保值在有效范围内
        EditorGUILayout.EndHorizontal();
        
        // 偏移控制  
        EditorGUILayout.LabelField("偏移:", GUILayout.Width(40));
        timelineOffset = EditorGUILayout.FloatField(timelineOffset, GUILayout.Width(60));
        
        // 重置按钮
        if (GUILayout.Button("重置视图", GUILayout.Width(80)))
        {
            timelineScale = 1f;
            timelineOffset = 0f;
        }
        
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawTimeline()
    {
        if (selectedNPCData.paths == null || selectedNPCData.paths.Count == 0)
        {
            EditorGUILayout.HelpBox("此NPC没有配置路径", MessageType.Warning);
            return;
        }
        
        // 计算时间轴范围
        float maxTime = CalculateMaxTime();
        if (maxTime <= 0) maxTime = 300f; // 默认5分钟
        
        EditorGUILayout.BeginVertical();
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        // 绘制时间轴标尺
        DrawTimeRuler(maxTime);
        
        // 绘制每条路径的时间轴
        for (int i = 0; i < selectedNPCData.paths.Count; i++)
        {
            DrawPathTimeline(selectedNPCData.paths[i], i, maxTime);
        }
        
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }
    
    private void DrawTimeRuler(float maxTime)
    {
        Rect rulerRect = GUILayoutUtility.GetRect(0, 30);
        
        // 绘制背景
        EditorGUI.DrawRect(rulerRect, new Color(0.2f, 0.2f, 0.2f, 0.3f));
        
        // 绘制时间刻度
        float pixelsPerSecond = (rulerRect.width - 200) / maxTime * timelineScale;
        int interval = Mathf.Max(1, Mathf.RoundToInt(30f / pixelsPerSecond)); // 每30像素一个刻度
        
        for (int t = 0; t <= maxTime; t += interval)
        {
            float x = 200 + (t - timelineOffset) * pixelsPerSecond;
            if (x >= 200 && x <= rulerRect.width)
            {
                // 绘制刻度线
                EditorGUI.DrawRect(new Rect(x, rulerRect.y, 1, rulerRect.height), Color.white);
                
                // 绘制时间标签
                string timeLabel = FormatTime(t);
                GUI.Label(new Rect(x - 20, rulerRect.y + 5, 40, 20), timeLabel, EditorStyles.miniLabel);
            }
        }
    }
    
    private void DrawPathTimeline(PathConfig path, int pathIndex, float maxTime)
    {
        Rect pathRect = GUILayoutUtility.GetRect(0, PATH_ROW_HEIGHT);
        
        // 绘制路径背景
        Color bgColor = pathIndex % 2 == 0 ? new Color(0.1f, 0.1f, 0.1f, 0.1f) : new Color(0.2f, 0.2f, 0.2f, 0.1f);
        EditorGUI.DrawRect(pathRect, bgColor);
        
        // 路径名称
        GUI.Label(new Rect(pathRect.x + 5, pathRect.y + 5, 190, 20), $"路径 {pathIndex + 1}: {path.pathName}", EditorStyles.boldLabel);
        GUI.Label(new Rect(pathRect.x + 5, pathRect.y + 25, 190, 15), $"起始时间: {FormatTime(path.pathStartStoryTime)}", EditorStyles.miniLabel);
        
        // 时间轴区域
        Rect timelineRect = new Rect(pathRect.x + 200, pathRect.y + 10, pathRect.width - 210, TIMELINE_HEIGHT);
        EditorGUI.DrawRect(timelineRect, new Color(0.3f, 0.3f, 0.3f, 0.2f));
        
        float pixelsPerSecond = timelineRect.width / maxTime * timelineScale;
        
        // 绘制当前时间指示器（如果游戏正在运行）
        if (Application.isPlaying && GameState.Instance != null)
        {
            float currentStoryTime = GameState.Instance.GetStoryTime();
            float currentTimeX = (currentStoryTime - timelineOffset) * pixelsPerSecond;
            
            if (currentTimeX >= 0 && currentTimeX <= timelineRect.width)
            {
                Rect currentTimeRect = new Rect(timelineRect.x + currentTimeX - 1, timelineRect.y, 2, timelineRect.height);
                EditorGUI.DrawRect(currentTimeRect, currentTimeColor);
                
                // 当前时间标签
                GUI.Label(new Rect(timelineRect.x + currentTimeX - 30, timelineRect.y - 15, 60, 15), 
                         "当前时间", EditorStyles.miniLabel);
            }
        }
        
        // 绘制路径起始点
        float startX = (path.pathStartStoryTime - timelineOffset) * pixelsPerSecond;
        if (startX >= 0 && startX <= timelineRect.width)
        {
            Rect startRect = new Rect(timelineRect.x + startX - 2, timelineRect.y, 4, timelineRect.height);
            EditorGUI.DrawRect(startRect, pathColor);
            
            // 路径起始标签
            GUI.Label(new Rect(timelineRect.x + startX - 30, timelineRect.y - 15, 60, 15), 
                     "路径开始", EditorStyles.miniLabel);
        }
        
        // 绘制时间控制点
        if (path.timePoints != null)
        {
            for (int i = 0; i < path.timePoints.Count; i++)
            {
                var timePoint = path.timePoints[i];
                float absoluteTime = path.pathStartStoryTime + timePoint.requiredStoryTime;
                float pointX = (absoluteTime - timelineOffset) * pixelsPerSecond;
                
                if (pointX >= 0 && pointX <= timelineRect.width)
                {
                    // 确定时间点颜色（选中状态）
                    Color pointColor = (selectedTimePoint == timePoint && selectedPath == path) ? 
                                       selectedTimePointColor : timePointColor;
                    
                    // 时间点标记
                    Rect pointRect = new Rect(timelineRect.x + pointX - 3, timelineRect.y + 5, 6, timelineRect.height - 10);
                    EditorGUI.DrawRect(pointRect, pointColor);
                    
                    // 检测点击
                    if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                    {
                        Rect clickRect = new Rect(timelineRect.x + pointX - 8, timelineRect.y, 16, timelineRect.height);
                        if (clickRect.Contains(Event.current.mousePosition))
                        {
                            selectedPath = path;
                            selectedTimePoint = timePoint;
                            selectedTimePointIndex = i;
                            Event.current.Use();
                            Repaint();
                        }
                    }
                    
                    // 时间点信息
                    string pointInfo = $"点{timePoint.pathPointIndex}\n{FormatTime(absoluteTime)}";
                    if (!string.IsNullOrEmpty(timePoint.description))
                    {
                        pointInfo += $"\n{timePoint.description}";
                    }
                    
                    GUI.Label(new Rect(timelineRect.x + pointX - 40, timelineRect.y + timelineRect.height + 2, 80, 40), 
                             pointInfo, EditorStyles.miniLabel);
                }
            }
        }
    }
    
    private void DrawCurrentTimeInfo()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("当前游戏状态", EditorStyles.boldLabel);
        
        if (Application.isPlaying && GameState.Instance != null)
        {
            float currentStoryTime = GameState.Instance.GetStoryTime();
            GameState.State currentState = GameState.Instance.GetCurrentState();
            
            EditorGUILayout.LabelField($"当前故事时间: {FormatTime(currentStoryTime)}");
            EditorGUILayout.LabelField($"当前状态: {currentState}");
            
            // 如果是幕状态，显示更详细的信息
            if (currentState == GameState.State.Act1 || 
                currentState == GameState.State.Act2 || 
                currentState == GameState.State.Act3)
            {
                float actProgress = GameState.Instance.GetCurrentActProgress();
                EditorGUILayout.LabelField($"当前幕进度: {actProgress:P1}");
                
                float remainingTime = GameState.Instance.GetCurrentActRemainingTime();
                EditorGUILayout.LabelField($"剩余时间: {FormatTime(remainingTime)}");
            }
            
            // 绘制当前时间线
            Repaint();
        }
        else
        {
            EditorGUILayout.LabelField("游戏未运行");
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private float CalculateMaxTime()
    {
        float maxTime = 0f;
        
        foreach (var path in selectedNPCData.paths)
        {
            // 路径起始时间
            maxTime = Mathf.Max(maxTime, path.pathStartStoryTime);
            
            // 时间控制点
            if (path.timePoints != null)
            {
                foreach (var timePoint in path.timePoints)
                {
                    float absoluteTime = path.pathStartStoryTime + timePoint.requiredStoryTime;
                    maxTime = Mathf.Max(maxTime, absoluteTime);
                }
            }
        }
        
        return Mathf.Max(maxTime + 60f, 300f); // 至少显示5分钟
    }
    
    private string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
        return $"{minutes:00}:{seconds:00}";
    }
    
    private void OnInspectorUpdate()
    {
        if (Application.isPlaying)
        {
            Repaint(); // 运行时刷新显示当前时间
        }
    }
    
    // 新增：绘制选中时间点的详细编辑器
    private void DrawSelectedTimePointEditor()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("时间点详细编辑", EditorStyles.boldLabel);
        
        // 取消选择按钮
        if (GUILayout.Button("取消选择", GUILayout.Width(80)))
        {
            selectedTimePoint = null;
            selectedPath = null;
            selectedTimePointIndex = -1;
            Repaint();
        }
        EditorGUILayout.EndHorizontal();
        
        if (selectedTimePoint != null && selectedPath != null)
        {
            EditorGUILayout.Space(5);
            
            // 路径信息
            EditorGUILayout.LabelField($"所属路径: {selectedPath.pathName}");
            EditorGUILayout.LabelField($"时间点索引: {selectedTimePointIndex}");
            
            EditorGUILayout.Space(5);
            
            // 路径点选择 - 使用Popup下拉菜单
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("路径点");
            
            MultiPointPathCreator pathCreator = PathRegistry.GetPathCreatorByID(selectedPath.pathID);
            if (pathCreator != null)
            {
                int pointCount = pathCreator.GetPathPointCount();
                string[] pointOptions = new string[pointCount];
                
                for (int i = 0; i < pointCount; i++)
                {
                    pointOptions[i] = $"点 {i}";
                }
                
                EditorGUI.BeginChangeCheck();
                int newPointIndex = EditorGUILayout.Popup(
                    Mathf.Clamp(selectedTimePoint.pathPointIndex, 0, pointCount - 1), 
                    pointOptions, 
                    GUILayout.Width(100));
                
                if (EditorGUI.EndChangeCheck())
                {
                    selectedTimePoint.pathPointIndex = newPointIndex;
                    EditorUtility.SetDirty(selectedNPCData);
                }
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                int newPointIndex = EditorGUILayout.IntField(selectedTimePoint.pathPointIndex, GUILayout.Width(100));
                if (EditorGUI.EndChangeCheck())
                {
                    selectedTimePoint.pathPointIndex = newPointIndex;
                    EditorUtility.SetDirty(selectedNPCData);
                }
            }
            EditorGUILayout.EndHorizontal();
            
            // 相对时间 - 可输入和拖动
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("相对时间(秒)");
            EditorGUI.BeginChangeCheck();
            float newRequiredTime = EditorGUILayout.FloatField(selectedTimePoint.requiredStoryTime, GUILayout.Width(80));
            if (EditorGUI.EndChangeCheck())
            {
                selectedTimePoint.requiredStoryTime = newRequiredTime;
                EditorUtility.SetDirty(selectedNPCData);
            }
            
            // 时间格式显示
            EditorGUILayout.LabelField(FormatTime(selectedTimePoint.requiredStoryTime), GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();
            
            // 绝对时间显示
            float absoluteTime = selectedPath.pathStartStoryTime + selectedTimePoint.requiredStoryTime;
            EditorGUILayout.LabelField($"绝对故事时间: {FormatTime(absoluteTime)}");
            
            // 描述
            EditorGUILayout.LabelField("描述");
            EditorGUI.BeginChangeCheck();
            string newDescription = EditorGUILayout.TextArea(selectedTimePoint.description, GUILayout.Height(40));
            if (EditorGUI.EndChangeCheck())
            {
                selectedTimePoint.description = newDescription;
                EditorUtility.SetDirty(selectedNPCData);
            }
            
            EditorGUILayout.Space(10);
            
            // 操作按钮
            EditorGUILayout.BeginHorizontal();
            
            // 删除时间点按钮
            if (GUILayout.Button("删除时间点", GUILayout.Width(100)))
            {
                if (EditorUtility.DisplayDialog("确认删除", "确定要删除此时间控制点?", "删除", "取消"))
                {
                    selectedPath.timePoints.RemoveAt(selectedTimePointIndex);
                    selectedTimePoint = null;
                    selectedPath = null;
                    selectedTimePointIndex = -1;
                    EditorUtility.SetDirty(selectedNPCData);
                    Repaint();
                }
            }
            
            // 复制时间点按钮
            if (GUILayout.Button("复制时间点", GUILayout.Width(100)))
            {
                PathTimePoint newTimePoint = new PathTimePoint
                {
                    pathPointIndex = selectedTimePoint.pathPointIndex,
                    requiredStoryTime = selectedTimePoint.requiredStoryTime + 10f, // 偏移10秒
                    description = selectedTimePoint.description + " (副本)"
                };
                selectedPath.timePoints.Add(newTimePoint);
                EditorUtility.SetDirty(selectedNPCData);
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndVertical();
    }
}
#endif 