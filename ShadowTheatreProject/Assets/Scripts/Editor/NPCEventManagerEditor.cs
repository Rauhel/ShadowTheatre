#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(NPCEventManager))]
public class NPCEventManagerEditor : Editor
{
    private NPCEventManager eventManager;
    private NPCData npcData;
    private Dictionary<string, bool> eventFoldouts = new Dictionary<string, bool>();
    private bool showEvents = true;

    private void OnEnable()
    {
        eventManager = (NPCEventManager)target;
        
        // 通过 NPCMain 获取 NPCData
        NPCMain npcMain = eventManager.GetComponent<NPCMain>();
        if (npcMain != null)
        {
            npcData = npcMain.GetNPCData();
        }
        
        // 如果无法通过 NPCMain 获取，尝试直接从 NPCController 获取
        if (npcData == null)
        {
            NPCController controller = eventManager.GetComponent<NPCController>();
            if (controller != null)
            {
                npcData = controller.Data;
            }
        }
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (!eventManager || !npcData)
            return;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("事件信息", EditorStyles.boldLabel);

        // 查找所有可用的事件
        List<PathEvent> allEvents = new List<PathEvent>();

        // 从路径配置中获取事件
        if (npcData.paths != null)
        {
            foreach (var path in npcData.paths)
            {
                if (path.events != null && path.events.Count > 0)
                {
                    allEvents.AddRange(path.events);
                }
            }
        }

        if (allEvents.Count == 0)
        {
            EditorGUILayout.HelpBox("未找到任何事件。请在NPC数据中添加事件。", MessageType.Info);
            return;
        }

        showEvents = EditorGUILayout.Foldout(showEvents, $"可用事件 ({allEvents.Count})", true);

        if (showEvents)
        {
            EditorGUI.indentLevel++;

            for (int i = 0; i < allEvents.Count; i++)
            {
                PathEvent npcEvent = allEvents[i];
                string eventId = npcEvent.eventID;

                if (!eventFoldouts.ContainsKey(eventId))
                {
                    eventFoldouts[eventId] = false;
                }

                EditorGUILayout.BeginHorizontal();

                eventFoldouts[eventId] = EditorGUILayout.Foldout(
                    eventFoldouts[eventId],
                    $"事件 {i + 1}: {eventId}",
                    true
                );

                // 添加测试按钮
                if (GUILayout.Button("测试触发", GUILayout.Width(80)))
                {
                    // 获取事件所在的路径
                    string pathId = FindPathIdForEvent(npcEvent);
                    if (!string.IsNullOrEmpty(pathId))
                    {
                        TestTriggerEvent(eventManager, pathId, npcEvent);
                    }
                }

                EditorGUILayout.EndHorizontal();

                if (eventFoldouts[eventId])
                {
                    EditorGUI.indentLevel++;

                    // 显示事件详情
                    DisplayEventDetails(npcEvent);

                    EditorGUI.indentLevel--;
                }
            }

            EditorGUI.indentLevel--;
        }
    }

    private void DisplayEventDetails(PathEvent npcEvent)
    {
        // 基本信息
        EditorGUILayout.LabelField("事件ID:", npcEvent.eventID);
        EditorGUILayout.LabelField("起始点:", npcEvent.startPointIndex.ToString());
        EditorGUILayout.LabelField("结束点:", npcEvent.endPointIndex.ToString());

        // 可用性信息
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("可用性:", GUILayout.Width(100));
        GUI.enabled = false;
        EditorGUILayout.Toggle("第一幕", npcEvent.enabledInAct1);
        EditorGUILayout.Toggle("第二幕", npcEvent.enabledInAct2);
        EditorGUILayout.Toggle("第三幕", npcEvent.enabledInAct3);
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        // 手势信息
        EditorGUILayout.LabelField("手势保持时间:", npcEvent.gestureHoldTime.ToString() + "秒");
        EditorGUILayout.LabelField("最大识别距离:", npcEvent.maxRecognitionDistance.ToString() + "米");

        // 手势响应
        if (npcEvent.gestureResponses != null && npcEvent.gestureResponses.Count > 0)
        {
            EditorGUILayout.LabelField("手势响应:", EditorStyles.boldLabel);

            for (int i = 0; i < npcEvent.gestureResponses.Count; i++)
            {
                GestureResponse response = npcEvent.gestureResponses[i];
                EditorGUILayout.LabelField($"- {response.gestureType}: 分数影响 {(response.scoreEffect >= 0 ? "+" : "")}{response.scoreEffect}");
            }
        }
        else
        {
            EditorGUILayout.LabelField("手势响应: 无");
        }
    }

    // 查找事件所在的路径ID
    private string FindPathIdForEvent(PathEvent targetEvent)
    {
        foreach (var path in npcData.paths)
        {
            if (path.events != null)
            {
                foreach (var pathEvent in path.events)
                {
                    if (pathEvent == targetEvent)
                    {
                        return path.pathID;
                    }
                }
            }
        }
        return null;
    }

    // 添加一个扩展方法以在编辑器中模拟触发事件
    public void TestTriggerEvent(NPCEventManager manager, string pathId, PathEvent pathEvent)
    {
        // 记录对象状态以便撤销
        Undo.RegisterFullObjectHierarchyUndo(manager.gameObject, "Test Trigger Event");

        // 调用事件管理器的方法来测试触发事件
        EditorUtility.DisplayDialog(
            "测试事件触发",
            $"正在测试触发事件: {pathEvent.eventID}\n路径: {pathId}",
            "确定"
        );

        // 如果 NPCEventManager 有公共方法进行测试，直接调用
        // 例如: manager.SimulateEventTrigger(pathId, pathEvent);

        // 或者，我们可以直接在这里模拟触发逻辑
        Debug.Log($"[编辑器] 测试触发事件: {pathEvent.eventID} (路径: {pathId})");
    }
}
#endif