#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

[CustomEditor(typeof(NPCData))]
public class NPCDataEditor : Editor
{
    // 共享数据
    private NPCData npcData;
    private Dictionary<string, bool> pathFoldouts = new Dictionary<string, bool>();
    private Dictionary<string, bool> eventFoldouts = new Dictionary<string, bool>();
    private Vector2 scrollPosition;
    private bool showImportExport = false;
    
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
        
        // 导入导出功能
        DrawImportExportSection();
        
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
    
    private void DrawImportExportSection()
    {
        EditorGUILayout.Space(5);
        
        // 显示/隐藏导入导出选项的折叠面板
        showImportExport = EditorGUILayout.Foldout(showImportExport, "导入/导出工具", true, EditorStyles.foldoutHeader);
        
        if(showImportExport)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // 文件路径选择
            EditorGUILayout.BeginHorizontal();
            npcData.importExportPath = EditorGUILayout.TextField("文件路径", npcData.importExportPath);
            if(GUILayout.Button("浏览...", GUILayout.Width(60)))
            {
                string initialDir = string.IsNullOrEmpty(npcData.importExportPath) ? 
                    Application.dataPath : Path.GetDirectoryName(npcData.importExportPath);
                string path = EditorUtility.SaveFilePanel("选择CSV文件", initialDir, 
                    $"{npcData.npcName}_data.csv", "csv");
                    
                if(!string.IsNullOrEmpty(path))
                {
                    npcData.importExportPath = path;
                    EditorUtility.SetDirty(npcData);
                }
            }
            EditorGUILayout.EndHorizontal();
            
            // 导入导出按钮
            EditorGUILayout.BeginHorizontal();
            
            // 导出按钮
            if(GUILayout.Button("导出为CSV", GUILayout.Height(30)))
            {
                string path = npcData.importExportPath;
                if(string.IsNullOrEmpty(path))
                {
                    path = EditorUtility.SaveFilePanel("导出CSV", Application.dataPath, 
                        $"{npcData.npcName}_data.csv", "csv");
                }
                
                if(!string.IsNullOrEmpty(path))
                {
                    NPCDataImportExport.ExportToCSV(npcData, path);
                }
            }
            
            // 导入按钮
            if(GUILayout.Button("从CSV导入", GUILayout.Height(30)))
            {
                string path = npcData.importExportPath;
                if(string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    path = EditorUtility.OpenFilePanel("导入CSV", Application.dataPath, "csv");
                }
                
                if(!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    NPCDataImportExport.ImportFromCSV(npcData, path);
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            // 生成模板按钮
            if(GUILayout.Button("生成CSV模板"))
            {
                string path = EditorUtility.SaveFilePanel("保存CSV模板", Application.dataPath, 
                    "npc_data_template.csv", "csv");
                if(!string.IsNullOrEmpty(path))
                {
                    NPCDataImportExport.GenerateTemplate(path);
                }
            }
            
            // 导入导出说明
            EditorGUILayout.HelpBox(
                "CSV格式说明:\n" + 
                "1. 第一行为表头，请勿修改\n" +
                "2. # 开头的行为注释，用于基本信息\n" + 
                "3. 动作类型: PATH_ACTION, DEFAULT, GESTURE", 
                MessageType.Info);
            
            EditorGUILayout.EndVertical();
        }
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