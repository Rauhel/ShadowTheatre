#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
    private NPCActionEditor actionEditor;

    private void OnEnable()
    {
        npcData = (NPCData)target;
        
        // 初始化子编辑器
        pathEditor = new NPCPathConfigEditor(this);
        eventEditor = new NPCPathEventEditor(this);
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
        npcData.moveSpeed = EditorGUILayout.FloatField("移动速度", npcData.moveSpeed);
        EditorGUI.indentLevel--;
    }
    
    private void DrawImportExportSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("数据导入导出", EditorStyles.boldLabel);

        // 完整版导出
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("导出完整版CSV"))
        {
            string path = EditorUtility.SaveFilePanel("导出CSV", 
                !string.IsNullOrEmpty(npcData.importExportPath) ? Path.GetDirectoryName(npcData.importExportPath) : Application.dataPath,
                SanitizeFileName(npcData.npcName) + "_data.csv", "csv");
            if (!string.IsNullOrEmpty(path))
            {
                NPCDataImportExport.ExportToCSV(npcData, path);
            }
        }

        // 简化版导出按钮
        if (GUILayout.Button("导出简化版CSV"))
        {
            string path = EditorUtility.SaveFilePanel("导出简化版CSV", 
                !string.IsNullOrEmpty(npcData.importExportPath) ? Path.GetDirectoryName(npcData.importExportPath) : Application.dataPath,
                SanitizeFileName(npcData.npcName) + "_data_simple.csv", "csv");
            if (!string.IsNullOrEmpty(path))
            {
                NPCDataSimpleImportExport.ExportToSimpleCSV(npcData, path);
            }
        }
        GUILayout.EndHorizontal();

        // 导入按钮
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("导入完整版CSV"))
        {
            string path = EditorUtility.OpenFilePanelWithFilters("导入CSV", 
                !string.IsNullOrEmpty(npcData.importExportPath) ? Path.GetDirectoryName(npcData.importExportPath) : Application.dataPath,
                NPCDataImportExport.ExcelFileTypes);
            if (!string.IsNullOrEmpty(path))
            {
                NPCDataImportExport.ImportFromCSV(npcData, path);
            }
        }

        // 简化版导入按钮
        if (GUILayout.Button("导入简化版CSV"))
        {
            string path = EditorUtility.OpenFilePanelWithFilters("导入简化版CSV", 
                !string.IsNullOrEmpty(npcData.importExportPath) ? Path.GetDirectoryName(npcData.importExportPath) : Application.dataPath,
                NPCDataSimpleImportExport.ExcelFileTypes);  // 使用NPCDataSimpleImportExport中定义的ExcelFileTypes
            if (!string.IsNullOrEmpty(path))
            {
                NPCDataSimpleImportExport.ImportFromSimpleCSV(npcData, path);
            }
        }
        GUILayout.EndHorizontal();
        
        // 显示当前路径（如果已有这部分代码）
        if (!string.IsNullOrEmpty(npcData.importExportPath))
        {
            EditorGUILayout.LabelField("当前导入/导出路径:", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(npcData.importExportPath, EditorStyles.textField, 
                GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }
    }

    private string SanitizeFileName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "unnamed";
            
        char[] invalidChars = Path.GetInvalidFileNameChars();
        string sanitized = new string(name.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
        return sanitized;
    }

    // 访问器，供子编辑器使用
    public NPCData Data => npcData;
    public Dictionary<string, bool> PathFoldouts => pathFoldouts;
    public Dictionary<string, bool> EventFoldouts => eventFoldouts;
    
    // 获取其他编辑器的引用
    public NPCPathConfigEditor PathEditor => pathEditor;
    public NPCPathEventEditor EventEditor => eventEditor;
    public NPCActionEditor ActionEditor => actionEditor;
}
#endif