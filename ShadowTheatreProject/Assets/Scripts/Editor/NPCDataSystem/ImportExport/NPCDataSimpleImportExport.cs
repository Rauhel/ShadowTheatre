#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

public class NPCDataSimpleImportExport : Editor
{
    // 简化版表头 - 只包含关键7列
    private static readonly string[] SimpleHeaderRow = new string[] {
        "路径名称", "事件ID", "动作类型", "路径点索引", "对话内容", "动画名称", "手势类型"
    };

    public static readonly string[] ExcelFileTypes = new string[] { "csv", "CSV" };

    // 简化版到完整版的字段映射关系
    private static readonly int[] SimpleToFullMapping = new int[] {
        1,  // 路径名称 -> 路径名称(1) 
        4,  // 事件ID -> 事件ID(4)
        5,  // 动作类型 -> 动作类型(5)
        6,  // 路径点索引 -> 路径点索引(6)
        7,  // 对话内容 -> 对话内容(7)
        8,  // 动画名称 -> 动画名称(8)
        13  // 手势类型 -> 手势类型(13)
    };

    // 导出NPC数据到简化CSV
    public static void ExportToSimpleCSV(NPCData data, string filePath)
    {
        if (data == null)
        {
            EditorUtility.DisplayDialog("导出错误", "未选择NPC数据", "确定");
            return;
        }

        StringBuilder csv = new StringBuilder();
        
        // 添加简化版表头
        csv.AppendLine(string.Join(",", SimpleHeaderRow));
        
        // 导出路径和动作
        foreach (var path in data.paths)
        {
            // 导出路径动作
            foreach (var action in path.pathActions)
            {
                string[] row = new string[SimpleHeaderRow.Length];
                row[0] = path.pathName;
                row[1] = "PATH_ACTION";
                row[2] = "PATH_ACTION";
                row[3] = action.pathPointIndex.ToString();
                row[4] = EscapeCSVField(action.dialogueText);
                row[5] = action.animationName;
                row[6] = "";  // 路径动作没有手势类型
                
                csv.AppendLine(string.Join(",", row));
            }
            
            // 导出事件及其响应
            foreach (var pathEvent in path.events)
            {
                // 导出默认响应
                foreach (var action in pathEvent.defaultResponse.actions)
                {
                    string[] row = new string[SimpleHeaderRow.Length];
                    row[0] = path.pathName;
                    row[1] = pathEvent.eventID;
                    row[2] = "DEFAULT";
                    row[3] = action.pathPointIndex.ToString();
                    row[4] = EscapeCSVField(action.dialogueText);
                    row[5] = action.animationName;
                    row[6] = "";  // 默认响应没有手势类型
                    
                    csv.AppendLine(string.Join(",", row));
                }
                
                // 导出手势响应
                foreach (var gestureResponse in pathEvent.gestureResponses)
                {
                    foreach (var action in gestureResponse.actions)
                    {
                        string[] row = new string[SimpleHeaderRow.Length];
                        row[0] = path.pathName;
                        row[1] = pathEvent.eventID;
                        row[2] = "GESTURE";
                        row[3] = action.pathPointIndex.ToString();
                        row[4] = EscapeCSVField(action.dialogueText);
                        row[5] = action.animationName;
                        row[6] = gestureResponse.gestureType;
                        
                        csv.AppendLine(string.Join(",", row));
                    }
                }
            }
        }
        
        try
        {
            File.WriteAllText(filePath, csv.ToString(), System.Text.Encoding.UTF8);
            EditorUtility.DisplayDialog("导出成功", $"简化版NPC数据已成功导出到:\n{filePath}", "确定");
            
            // 更新导入导出路径
            data.importExportPath = filePath;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("导出错误", $"导出失败: {e.Message}", "确定");
            Debug.LogException(e);
        }
    }
    
    // 从简化CSV导入NPC数据
    public static void ImportFromSimpleCSV(NPCData data, string filePath)
    {
        if (data == null)
        {
            EditorUtility.DisplayDialog("导入错误", "未选择NPC数据", "确定");
            return;
        }

        if (!File.Exists(filePath))
        {
            EditorUtility.DisplayDialog("导入错误", $"文件不存在: {filePath}", "确定");
            return;
        }

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
                    linesList.Add(line);
                }
                lines = linesList.ToArray();
            }
            
            if (lines.Length <= 1) // 只有表头或空文件
            {
                EditorUtility.DisplayDialog("导入错误", "文件为空或只包含表头", "确定");
                return;
            }

            // 备份当前数据
            if (EditorUtility.DisplayDialog("确认导入", 
                "导入将覆盖当前NPC数据。是否继续？\n建议在导入前备份当前数据。", 
                "继续导入", "取消"))
            {
                // 显示进度条
                EditorUtility.DisplayProgressBar("导入NPC数据", "准备导入...", 0f);
                
                // 清空当前路径
                data.paths.Clear();

                // 解析CSV数据
                Dictionary<string, PathConfig> pathConfigs = new Dictionary<string, PathConfig>();
                Dictionary<string, Dictionary<string, PathEvent>> pathEvents = 
                    new Dictionary<string, Dictionary<string, PathEvent>>();

                // 用于记录上下文信息的变量
                string lastPathName = "";
                string lastEventID = "";
                string lastActionType = "";
                int nextPathPointIndex = 0;
                int pathCounter = 1; // 用于自动生成路径ID
                
                for (int i = 1; i < lines.Length; i++) // 跳过表头
                {
                    // 更新进度
                    float progress = (float)i / lines.Length;
                    EditorUtility.DisplayProgressBar("导入简化版NPC数据", 
                        $"处理第 {i} 行，共 {lines.Length} 行...", progress);
                    
                    string line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    string[] fields = ParseCSVLine(line);
                    if (fields.Length == 0) continue; // 跳过空行
                    
                    // 补全字段数组到简化版表头长度
                    if (fields.Length < SimpleHeaderRow.Length)
                    {
                        string[] extendedFields = new string[SimpleHeaderRow.Length];
                        for (int f = 0; f < SimpleHeaderRow.Length; f++)
                        {
                            if (f < fields.Length)
                                extendedFields[f] = fields[f];
                            else
                                extendedFields[f] = ""; // 填充空值
                        }
                        fields = extendedFields;
                    }

                    // 智能补全字段
                    // 1. 路径名称
                    if (string.IsNullOrWhiteSpace(fields[0]))
                        fields[0] = lastPathName;
                    else
                        lastPathName = fields[0];
                    
                    // 2. 事件ID
                    if (string.IsNullOrWhiteSpace(fields[1]))
                        fields[1] = lastEventID;
                    else
                        lastEventID = fields[1];
                    
                    // 3. 动作类型
                    if (string.IsNullOrWhiteSpace(fields[2]))
                        fields[2] = lastActionType;
                    else
                        lastActionType = fields[2];
                    
                    // 4. 路径点索引 - 自动递增
                    if (string.IsNullOrWhiteSpace(fields[3]) || !int.TryParse(fields[3], out _))
                        fields[3] = (nextPathPointIndex++).ToString();
                    else
                        nextPathPointIndex = int.Parse(fields[3]) + 1;

                    // 获取关键数据
                    string pathName = fields[0];
                    string pathID = pathName;  // 简化版中将路径名作为路径ID
                    
                    // 如果关键字段为数字，生成描述性ID
                    if (int.TryParse(pathID, out _))
                    {
                        pathID = "path_" + pathID;
                    }
                    
                    string eventID = fields[1];
                    string actionType = fields[2];
                    
                    // 确保路径存在
                    if (!pathConfigs.ContainsKey(pathID))
                    {
                        PathConfig newPath = new PathConfig
                        {
                            pathID = pathID,
                            pathName = pathName,
                            pathActions = new List<ActionData>(),
                            events = new List<PathEvent>(),
                            nextPaths = new List<PathBranch>()
                        };
                        
                        // 设置默认分数范围
                        newPath.minScore = 0f;
                        newPath.maxScore = 100f;
                        
                        pathConfigs[pathID] = newPath;
                        pathEvents[pathID] = new Dictionary<string, PathEvent>();
                        pathCounter++;
                    }
                    
                    // 创建动作数据
                    ActionData action = new ActionData();
                    
                    // 路径点索引
                    if (int.TryParse(fields[3], out int pointIndex))
                        action.pathPointIndex = pointIndex;
                    else
                        action.pathPointIndex = 0;
                    
                    // 对话文本
                    action.dialogueText = fields[4];
                    
                    // 动画名称
                    action.animationName = fields[5];
                    
                    // 设置默认值
                    action.loopAnimation = false;
                    action.displayDuration = 2.0f;
                    action.delay = 0.5f;
                    action.waitTime = 0f;
                    action.overridePrevious = true;
                    action.isActionActive = true;
                    
                    // 处理不同类型的动作
                    if (actionType == "PATH_ACTION")
                    {
                        pathConfigs[pathID].pathActions.Add(action);
                    }
                    else if (actionType == "DEFAULT" || actionType == "GESTURE")
                    {
                        // 自动生成事件ID如果为空
                        if (string.IsNullOrWhiteSpace(eventID))
                        {
                            eventID = "event_" + pointIndex;
                        }
                        
                        // 确保事件存在
                        if (!pathEvents[pathID].ContainsKey(eventID))
                        {
                            PathEvent newEvent = new PathEvent
                            {
                                eventID = eventID,
                                defaultResponse = new GestureResponse
                                {
                                    actions = new List<ActionData>()
                                },
                                gestureResponses = new List<GestureResponse>()
                            };
                            
                            // 设置默认事件起始点和结束点
                            newEvent.startPointIndex = action.pathPointIndex;
                            newEvent.endPointIndex = action.pathPointIndex + 2;
                            
                            pathEvents[pathID][eventID] = newEvent;
                            pathConfigs[pathID].events.Add(newEvent);
                        }
                        
                        PathEvent currentEvent = pathEvents[pathID][eventID];
                        
                        // 默认响应
                        if (actionType == "DEFAULT")
                        {
                            currentEvent.defaultResponse.actions.Add(action);
                            currentEvent.defaultResponse.scoreEffect = 0f;  // 默认分数影响
                        }
                        // 手势响应
                        else if (actionType == "GESTURE")
                        {
                            string gestureType = fields[6];
                            if (string.IsNullOrWhiteSpace(gestureType))
                                gestureType = "未指定";
                            
                            // 查找或创建手势响应
                            GestureResponse gestureResponse = currentEvent.gestureResponses
                                .FirstOrDefault(r => r.gestureType == gestureType);
                                    
                            if (gestureResponse == null)
                            {
                                gestureResponse = new GestureResponse
                                {
                                    gestureType = gestureType,
                                    actions = new List<ActionData>()
                                };
                                currentEvent.gestureResponses.Add(gestureResponse);
                            }
                            
                            gestureResponse.actions.Add(action);
                            gestureResponse.scoreEffect = 0f;  // 默认分数影响
                        }
                    }
                }

                // 添加所有路径到NPC数据
                foreach (var pathConfig in pathConfigs.Values)
                {
                    data.paths.Add(pathConfig);
                }

                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("导入成功", "成功从简化CSV导入NPC数据", "确定");
                
                // 更新导入导出路径
                data.importExportPath = filePath;
            }
        }
        catch (System.Exception e)
        {
            // 更详细的错误信息
            string errorDetails = e.GetType().Name + ": " + e.Message;
            if (e.InnerException != null)
                errorDetails += "\n引发自: " + e.InnerException.Message;
                
            EditorUtility.DisplayDialog("导入错误", $"导入失败: {errorDetails}", "确定");
            Debug.LogException(e);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }
    
    // 导出所有NPC数据(简化版)
    public static void ExportAllNPCDataSimple()
    {
        string folder = EditorUtility.OpenFolderPanel("选择导出文件夹", Application.dataPath, "");
        if (string.IsNullOrEmpty(folder)) return;
        
        // 查找所有NPCData
        string[] guids = AssetDatabase.FindAssets("t:NPCData");
        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("导出错误", "未找到任何NPC数据资源", "确定");
            return;
        }
        
        int successCount = 0;
        int failCount = 0;
        
        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                NPCData npcData = AssetDatabase.LoadAssetAtPath<NPCData>(path);
                
                if (npcData != null)
                {
                    // 更新进度条
                    float progress = (float)i / guids.Length;
                    if (EditorUtility.DisplayCancelableProgressBar("批量导出简化版NPC数据", 
                        $"正在导出: {npcData.npcName} ({i+1}/{guids.Length})", progress))
                    {
                        break; // 用户取消操作
                    }
                    
                    try
                    {
                        // 根据npcID或npcName生成文件名
                        string fileName = string.IsNullOrEmpty(npcData.npcID) ? 
                            (string.IsNullOrEmpty(npcData.npcName) ? Path.GetFileNameWithoutExtension(path) : npcData.npcName) : 
                            npcData.npcID;
                            
                        fileName = SanitizeFileName(fileName) + "_data_simple.csv";
                        string exportPath = Path.Combine(folder, fileName);
                        
                        ExportToSimpleCSV(npcData, exportPath);
                        successCount++;
                    }
                    catch
                    {
                        failCount++;
                    }
                }
            }
            
            string message = $"简化版导出完成: 成功 {successCount} 个, 失败 {failCount} 个";
            if (failCount > 0)
                message += "\n\n请检查控制台获取错误详情";
                
            EditorUtility.DisplayDialog("批量导出结果", message, "确定");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }
    
    // 生成简化版空白模板
    public static void GenerateSimpleTemplate(string filePath)
    {
        StringBuilder template = new StringBuilder();
        
        // 添加简化版表头
        template.AppendLine(string.Join(",", SimpleHeaderRow));
        
        // 添加示例行
        template.AppendLine("路径1,PATH_ACTION,PATH_ACTION,1,\"这是路径点1的对话\",Idle,");
        template.AppendLine(",,,,\"这是路径点2的对话\",,");
        template.AppendLine(",事件A,DEFAULT,3,\"默认响应对话\",,");
        template.AppendLine(",,GESTURE,4,\"对手势的响应\",Happy,Bird");
        template.AppendLine("路径2,PATH_ACTION,PATH_ACTION,1,\"第二条路径的对话\",,");
        
        try
        {
            File.WriteAllText(filePath, template.ToString(), System.Text.Encoding.UTF8);
            EditorUtility.DisplayDialog("简化模板生成成功", $"简化版空白模板已生成到:\n{filePath}", "确定");
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("模板生成错误", $"生成失败: {e.Message}", "确定");
        }
    }

    // 添加菜单项
    [MenuItem("工具/NPC数据/简化版/批量导出所有NPC数据")]
    public static void MenuExportAllNPCDataSimple()
    {
        ExportAllNPCDataSimple();
    }
    
    [MenuItem("工具/NPC数据/简化版/生成模板")]
    public static void MenuGenerateSimpleTemplate()
    {
        string path = EditorUtility.SaveFilePanel("保存简化CSV模板", Application.dataPath, 
            "npc_data_simple_template.csv", "csv");
        if(!string.IsNullOrEmpty(path))
        {
            GenerateSimpleTemplate(path);
        }
    }

    // CSV字段转义
    private static string EscapeCSVField(string field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        
        bool needsQuotes = field.Contains(",") || field.Contains("\"") || 
                         field.Contains("\n") || field.Contains("\r");
                         
        if (!needsQuotes) return field;
        
        return "\"" + field.Replace("\"", "\"\"") + "\"";
    }
    
    // 分割CSV行（处理带引号的字段）
    private static string[] ParseCSVLine(string line)
    {
        List<string> fields = new List<string>();
        
        bool inQuotes = false;
        StringBuilder currentField = new StringBuilder();
        
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // 处理双引号转义
                    currentField.Append('"');
                    i++; // 跳过下一个引号
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(currentField.ToString());
                currentField.Clear();
            }
            else
            {
                currentField.Append(c);
            }
        }
        
        fields.Add(currentField.ToString());
        return fields.ToArray();
    }
    
    // 处理文件名，确保合法
    private static string SanitizeFileName(string name)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        string sanitized = new string(name.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
        return sanitized;
    }
}
#endif