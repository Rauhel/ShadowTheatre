#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(NPCData))]
public class NPCDataEditor : Editor
{
    // 共享数据
    private NPCData npcData;
    private Dictionary<string, bool> pathFoldouts = new Dictionary<string, bool>();
    private Dictionary<string, bool> eventFoldouts = new Dictionary<string, bool>();
    private Vector2 scrollPosition;
    
    // 子编辑器实例
    private NPCPathConfigEditor pathEditor;
    private NPCPathEventEditor eventEditor;
    private NPCDialogueEditor dialogueEditor;
    private NPCActionEditor actionEditor;

    private void OnEnable()
    {
        npcData = (NPCData)target;
        
        // 初始化子编辑器
        pathEditor = new NPCPathConfigEditor(this);
        eventEditor = new NPCPathEventEditor(this);
        dialogueEditor = new NPCDialogueEditor(this);
        actionEditor = new NPCActionEditor(this);
    }

    public void ClearCache()
    {
        pathFoldouts.Clear();
        eventFoldouts.Clear();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 基本信息
        DrawNPCBasicInfo();
        
        // 路径配置信息
        EditorGUILayout.Space(5); // 减少空间
        EditorGUILayout.LabelField("路径配置", EditorStyles.boldLabel);
        
        // 更合理的滚动视图高度计算
        float screenHeight = Screen.height;
        float scrollHeight = Mathf.Min(screenHeight * 0.6f, 600); // 最多占屏幕60%，最大600
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(scrollHeight));
        
        for (int i = 0; i < npcData.paths.Count; i++)
        {
            pathEditor.DrawPathConfig(npcData.paths[i], i);
        }
        
        EditorGUILayout.EndScrollView();
        
        // 添加新路径
        if (GUILayout.Button("添加新路径"))
        {
            pathEditor.AddNewPath();
        }
        
        if (GUI.changed)
        {
            EditorUtility.SetDirty(npcData);
        }
        
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawNPCBasicInfo()
    {
        EditorGUILayout.LabelField("NPC基本信息", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        npcData.npcID = EditorGUILayout.TextField("NPC ID", npcData.npcID);
        npcData.npcName = EditorGUILayout.TextField("NPC 名称", npcData.npcName);
        npcData.currentScore = EditorGUILayout.FloatField("当前分数", npcData.currentScore);
        EditorGUI.indentLevel--;
    }

    // 访问器，供子编辑器使用
    public NPCData Data => npcData;
    public Dictionary<string, bool> PathFoldouts => pathFoldouts;
    public Dictionary<string, bool> EventFoldouts => eventFoldouts;
    
    // 获取其他编辑器的引用
    public NPCPathConfigEditor PathEditor => pathEditor;
    public NPCPathEventEditor EventEditor => eventEditor;
    public NPCDialogueEditor DialogueEditor => dialogueEditor;
    public NPCActionEditor ActionEditor => actionEditor;
}
#endif