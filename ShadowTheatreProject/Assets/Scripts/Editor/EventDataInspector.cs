#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

// 处理事件数据的检查和编辑
public class EventDataInspector
{
    private Vector2 scrollPosition;
    private Dictionary<string, bool> eventFoldouts = new Dictionary<string, bool>();
    private NPCData npcData;

    public void SetNPCData(NPCData data)
    {
        npcData = data;
    }

    public void ClearCache()
    {
        eventFoldouts.Clear();
    }

    // 显示事件信息
    public void ShowEventsInfo(NPCData data)
    {
        if (data == null) return;

        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField("NPC事件列表", EditorStyles.boldLabel);
        
        if (data.events == null || data.events.Count == 0)
        {
            EditorGUILayout.HelpBox("没有找到事件。", MessageType.Info);
        }
        else
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
            
            for (int i = 0; i < data.events.Count; i++)
            {
                ShowEventItem(data.events[i], data);
            }
            
            EditorGUILayout.EndScrollView();
            
            if (GUI.changed)
            {
                EditorUtility.SetDirty(data);
            }
        }
        
        // 添加新事件的按钮
        if (GUILayout.Button("添加新事件"))
        {
            AddNewEvent(data);
        }
        
        EditorGUILayout.EndVertical();
    }

    // 显示单个事件项目
    private void ShowEventItem(NPCEvent npcEvent, NPCData data)
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        
        // 使用事件ID作为折叠状态的键
        string eventKey = npcEvent.eventID;
        if (!eventFoldouts.ContainsKey(eventKey))
        {
            eventFoldouts[eventKey] = false;
        }
        
        string headerText = $"事件：{npcEvent.eventID}";
        bool newFoldout = EditorGUILayout.Foldout(eventFoldouts[eventKey], headerText, true);
        
        if (newFoldout != eventFoldouts[eventKey])
        {
            eventFoldouts[eventKey] = newFoldout;
        }
        
        if (eventFoldouts[eventKey])
        {
            ShowEventDetails(npcEvent, data);
        }
        
        EditorGUILayout.EndVertical();
    }

    // 显示事件详细信息
    private void ShowEventDetails(NPCEvent npcEvent, NPCData data)
    {
        // 显示触发位置信息
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("触发位置");
        EditorGUI.BeginChangeCheck();
        
        // 使用 ObjectField 接受任何 Transform
        npcEvent.triggerLocation = EditorGUILayout.ObjectField(
            npcEvent.triggerLocation, 
            typeof(Transform), 
            true
        ) as Transform;
        
        if (EditorGUI.EndChangeCheck())
        {
            // 验证所选对象是否是路径点
            bool isValidPathPoint = false;
            if (npcEvent.triggerLocation != null)
            {
                Transform parent = npcEvent.triggerLocation.parent;
                if (parent != null && parent.name.Contains("PathPoints"))
                {
                    isValidPathPoint = true;
                }
                else if (!isValidPathPoint)
                {
                    Debug.LogWarning("所选对象不是路径点，建议选择路径点作为触发位置");
                }
            }
            
            EditorUtility.SetDirty(data);
        }
        
        // 添加定位按钮
        if (npcEvent.triggerLocation != null && GUILayout.Button("定位", GUILayout.Width(50)))
        {
            Selection.activeGameObject = npcEvent.triggerLocation.gameObject;
            SceneView.FrameLastActiveSceneView();
        }
        EditorGUILayout.EndHorizontal();
        
        // 显示路径点信息
        if (npcEvent.triggerLocation != null)
        {
            // 检查是否是路径点
            Transform point = npcEvent.triggerLocation;
            Transform parent = point.parent;
            
            // 路径点通常在 PathPoints 下
            if (parent != null && parent.name.Contains("PathPoints"))
            {
                string posInfo = data.GetPathPointRelativePosition(point);
                if (posInfo != "未知")
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel("路径位置");
                    EditorGUILayout.LabelField(posInfo, EditorStyles.boldLabel);
                    
                    // 添加一个跳转到路径按钮
                    if (GUILayout.Button("查看路径", GUILayout.Width(80)))
                    {
                        // 查找路径对象 (父对象的父对象)
                        if (parent.parent != null)
                        {
                            Selection.activeGameObject = parent.parent.gameObject;
                            SceneView.FrameLastActiveSceneView();
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("当前选择的不是路径点。建议选择路径点作为触发位置，以获得更好的位置精度。", MessageType.Warning);
            }
        }
        
        // 触发半径
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("触发半径");
        npcEvent.triggerRadius = EditorGUILayout.FloatField(npcEvent.triggerRadius);
        EditorGUILayout.EndHorizontal();
        
        // 显示其他事件属性
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("事件配置", EditorStyles.boldLabel);
        
        // 全局事件名
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("全局事件名");
        npcEvent.globalEventName = EditorGUILayout.TextField(npcEvent.globalEventName);
        EditorGUILayout.EndHorizontal();
        
        // 手势设置
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("手势保持时间");
        npcEvent.gestureHoldTime = EditorGUILayout.FloatField(npcEvent.gestureHoldTime);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("手势时间限制");
        npcEvent.gestureTimeLimit = EditorGUILayout.FloatField(npcEvent.gestureTimeLimit);
        EditorGUILayout.EndHorizontal();

        // 显示事件分支信息(简略版)
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("事件分支", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"默认分支分数: {npcEvent.defaultBranch.scoreValue}");
        EditorGUILayout.LabelField($"手势分支数量: {npcEvent.gestureBranches.Count}");
    }

    // 添加新事件
    private void AddNewEvent(NPCData data)
    {
        if (data.events == null)
            data.events = new List<NPCEvent>();
            
        string newEventID = "Event_" + (data.events.Count + 1);
        
        // 确保ID唯一
        int suffix = 1;
        while (data.events.Exists(e => e.eventID == newEventID))
        {
            newEventID = $"Event_{data.events.Count + 1}_{suffix}";
            suffix++;
        }
        
        NPCEvent newEvent = new NPCEvent
        {
            eventID = newEventID,
            triggerRadius = 2f,
            gestureHoldTime = 2f,
            gestureTimeLimit = 5f,
            defaultBranch = new EventBranch(),
            gestureBranches = new List<GestureBranch>()
        };
        
        data.events.Add(newEvent);
        
        // 设置新事件为展开状态
        eventFoldouts[newEventID] = true;
        
        EditorUtility.SetDirty(data);
    }
}
#endif