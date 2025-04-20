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
        
        // 使用 ObjectField 时确保类型为 Transform
        npcEvent.triggerLocation = EditorGUILayout.ObjectField(
            npcEvent.triggerLocation, 
            typeof(Transform), 
            true  // 允许场景对象
        ) as Transform;
        
        if (EditorGUI.EndChangeCheck() && npcEvent.triggerLocation != null)
        {
            // 确保 NPCData 被标记为已修改
            EditorUtility.SetDirty(data);
        }
        
        // 添加一个定位按钮
        if (npcEvent.triggerLocation != null && GUILayout.Button("定位", GUILayout.Width(50)))
        {
            Selection.activeGameObject = npcEvent.triggerLocation.gameObject;
            SceneView.FrameLastActiveSceneView();
        }
        EditorGUILayout.EndHorizontal();
        
        // 显示触发点在路径上的相对位置
        if (npcEvent.triggerLocation != null)
        {
            string posInfo = data.GetPathPointRelativePosition(npcEvent.triggerLocation);
            if (posInfo != "未知")
            {
                EditorGUILayout.LabelField($"路径位置: {posInfo}", EditorStyles.boldLabel);
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