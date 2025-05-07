#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

public class TMPCharacterExtractor : EditorWindow
{
    private List<string> selectedFilePaths = new List<string>();
    private HashSet<char> uniqueCharacters = new HashSet<char>();
    private string outputPath = "";
    private Vector2 scrollPosition;
    private bool includeComments = true;
    private bool processSubfolders = true;

    // 添加菜单项
    [MenuItem("工具/TMP字体工具/字符提取器")]
    public static void ShowWindow()
    {
        GetWindow<TMPCharacterExtractor>("TMP字符提取器");
    }
    
    private void OnGUI()
    {
        EditorGUILayout.LabelField("TMP字符提取工具", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("此工具可提取CSV文件中的所有独特字符，用于创建TMP字体包", MessageType.Info);
        EditorGUILayout.Space();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("选择单个文件", GUILayout.Width(120)))
        {
            string path = EditorUtility.OpenFilePanel(
                "选择CSV文件",
                Application.dataPath,
                "csv,txt");
                
            if (!string.IsNullOrEmpty(path) && !selectedFilePaths.Contains(path))
                selectedFilePaths.Add(path);
        }
        
        if (GUILayout.Button("选择文件夹", GUILayout.Width(120)))
        {
            string folderPath = EditorUtility.OpenFolderPanel("选择包含CSV文件的文件夹", Application.dataPath, "");
            if (!string.IsNullOrEmpty(folderPath))
            {
                SearchOption option = processSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                string[] csvFiles = Directory.GetFiles(folderPath, "*.csv", option);
                string[] txtFiles = Directory.GetFiles(folderPath, "*.txt", option);
                string[] allFiles = csvFiles.Concat(txtFiles).ToArray();
                
                if (allFiles.Length > 0)
                {
                    foreach (string file in allFiles)
                    {
                        if (!selectedFilePaths.Contains(file))
                            selectedFilePaths.Add(file);
                    }
                    Debug.Log($"已添加 {allFiles.Length} 个文件用于提取字符");
                }
                else
                {
                    EditorUtility.DisplayDialog("提示", "所选文件夹中没有找到CSV或TXT文件", "确定");
                }
            }
        }
        
        if (GUILayout.Button("清除列表", GUILayout.Width(100)))
        {
            selectedFilePaths.Clear();
            uniqueCharacters.Clear();
        }
        EditorGUILayout.EndHorizontal();
        
        // 输出路径
        EditorGUILayout.BeginHorizontal();
        outputPath = EditorGUILayout.TextField("输出文件:", outputPath);
        if (GUILayout.Button("保存位置", GUILayout.Width(100)))
        {
            string path = EditorUtility.SaveFilePanel(
                "保存字符列表",
                Application.dataPath,
                "tmp_characters",
                "txt");
                
            if (!string.IsNullOrEmpty(path))
            {
                outputPath = path;
            }
        }
        EditorGUILayout.EndHorizontal();
        
        // 选项
        includeComments = EditorGUILayout.Toggle("包含注释行", includeComments);
        processSubfolders = EditorGUILayout.Toggle("处理子文件夹", processSubfolders);
        
        EditorGUILayout.Space();
        
        // 显示所选文件列表
        EditorGUILayout.LabelField($"已选择 {selectedFilePaths.Count} 个文件:");
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(150));
        foreach (var path in selectedFilePaths)
        {
            EditorGUILayout.LabelField(Path.GetFileName(path));
        }
        EditorGUILayout.EndScrollView();
        
        EditorGUILayout.Space();
        
        EditorGUI.BeginDisabledGroup(selectedFilePaths.Count == 0);
        if (GUILayout.Button("提取唯一字符"))
        {
            ExtractCharacters();
        }
        EditorGUI.EndDisabledGroup();
        
        if (uniqueCharacters.Count > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"已提取 {uniqueCharacters.Count} 个唯一字符", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("保存字符列表"))
            {
                if (string.IsNullOrEmpty(outputPath))
                {
                    outputPath = EditorUtility.SaveFilePanel(
                        "保存字符列表",
                        Application.dataPath,
                        "tmp_characters",
                        "txt");
                }
                
                if (!string.IsNullOrEmpty(outputPath))
                {
                    SaveCharactersToFile();
                }
            }
            
            if (GUILayout.Button("复制到剪贴板"))
            {
                CopyToClipboard();
            }
            EditorGUILayout.EndHorizontal();
            
            // 显示字符预览
            EditorGUILayout.LabelField("字符预览 (前100个):");
            int previewCount = Mathf.Min(uniqueCharacters.Count, 100);
            string preview = new string(uniqueCharacters.Take(previewCount).ToArray());
            EditorGUILayout.SelectableLabel(preview, EditorStyles.textArea, GUILayout.Height(60));
        }
    }
    
    private void ExtractCharacters()
    {
        uniqueCharacters.Clear();
        int processedFiles = 0;
        
        try
        {
            // 显示进度条
            EditorUtility.DisplayProgressBar("提取字符", "准备处理文件...", 0);
            
            foreach (var filePath in selectedFilePaths)
            {
                if (!File.Exists(filePath)) continue;
                
                float progress = (float)processedFiles / selectedFilePaths.Count;
                EditorUtility.DisplayProgressBar("提取字符", 
                    $"处理文件 ({processedFiles+1}/{selectedFilePaths.Count}): {Path.GetFileName(filePath)}", 
                    progress);
                
                ProcessCSVFile(filePath);
                processedFiles++;
            }
            
            // 排序字符
            var sortedChars = uniqueCharacters.ToList();
            sortedChars.Sort();
            uniqueCharacters = new HashSet<char>(sortedChars);
            
            Debug.Log($"成功提取 {uniqueCharacters.Count} 个唯一字符。");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"提取字符时出错: {e.Message}\n{e.StackTrace}");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }
    
    private void ProcessCSVFile(string filePath)
    {
        try
        {
            string[] lines;
            
            // 使用FileShare.ReadWrite允许其他进程读取该文件
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader reader = new StreamReader(fs, System.Text.Encoding.UTF8))
            {
                // 读取所有行
                List<string> linesList = new List<string>();
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    // 根据设置决定是否包含注释行
                    if (!includeComments && line.StartsWith("#"))
                        continue;
                        
                    linesList.Add(line);
                }
                lines = linesList.ToArray();
            }
            
            // 处理所有行
            foreach (string line in lines)
            {
                foreach (char c in line)
                {
                    uniqueCharacters.Add(c);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"处理文件 {filePath} 时出错: {e.Message}");
        }
    }
    
    private void SaveCharactersToFile()
    {
        try
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in uniqueCharacters)
            {
                sb.Append(c);
            }
            
            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
            
            Debug.Log($"成功将 {uniqueCharacters.Count} 个字符保存到: {outputPath}");
            EditorUtility.RevealInFinder(outputPath);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"保存字符列表时出错: {e.Message}");
        }
    }
    
    private void CopyToClipboard()
    {
        StringBuilder sb = new StringBuilder();
        foreach (char c in uniqueCharacters)
        {
            sb.Append(c);
        }
        
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log($"已复制 {uniqueCharacters.Count} 个字符到剪贴板");
    }
}
#endif