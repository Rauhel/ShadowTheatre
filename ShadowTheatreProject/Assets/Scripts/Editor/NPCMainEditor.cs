// 文件: NPCMainEditor.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(NPCMain))]
public class NPCMainEditor : Editor
{
    private NPCMain npcMain;
    private NPCController controller;
    private int selectedPathIndex = 0;
    private List<string> pathNames = new List<string>();
    
    private void OnEnable()
    {
        npcMain = (NPCMain)target;
        controller = npcMain.GetComponent<NPCController>();
        
        // 预填充路径名称
        UpdatePathNames();
    }
    
    private void UpdatePathNames()
    {
        pathNames.Clear();
        
        if (controller != null && controller.Data != null && 
            controller.Data.pathConnections != null)
        {
            for (int i = 0; i < controller.Data.pathConnections.Count; i++)
            {
                var connection = controller.Data.pathConnections[i];
                string name = connection.path != null ? 
                    connection.path.name : "未命名路径";
                pathNames.Add($"{i}: {name}");
            }
        }
        
        if (pathNames.Count == 0)
        {
            pathNames.Add("无可用路径");
        }
    }
    
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        // 检查NPCData变化并更新
        if (controller != null && controller.Data != null)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("NPC调试工具", EditorStyles.boldLabel);
            
            if (Application.isPlaying)
            {
                if (GUILayout.Button("显示NPC状态"))
                {
                    npcMain.DebugNPCStatus();
                }
                
                // 运动控制
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("停止NPC"))
                {
                    npcMain.StopNPC(true);
                }
                
                if (GUILayout.Button("继续NPC"))
                {
                    npcMain.StopNPC(false);
                }
                EditorGUILayout.EndHorizontal();
                
                // 分数控制
                EditorGUILayout.LabelField($"当前分数: {npcMain.GetCurrentScore()}", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("加分 (+10)"))
                {
                    npcMain.UpdateScore(10);
                }
                
                if (GUILayout.Button("减分 (-10)"))
                {
                    npcMain.UpdateScore(-10);
                }
                
                EditorGUILayout.EndHorizontal();
                
                // 路径选择
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("路径控制", EditorStyles.boldLabel);
                
                // 更新路径列表
                UpdatePathNames();
                
                // 路径下拉菜单
                selectedPathIndex = EditorGUILayout.Popup("选择路径", 
                                                        Mathf.Min(selectedPathIndex, pathNames.Count - 1), 
                                                        pathNames.ToArray());
                
                if (pathNames.Count > 0 && pathNames[0] != "无可用路径" && GUILayout.Button("切换到所选路径"))
                {
                    npcMain.SwitchToPathByIndex(selectedPathIndex);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("在运行时可使用调试工具", MessageType.Info);
            }
            
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(npcMain);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("请为NPC添加NPCController组件并设置NPCData", MessageType.Warning);
        }
    }
}
#endif