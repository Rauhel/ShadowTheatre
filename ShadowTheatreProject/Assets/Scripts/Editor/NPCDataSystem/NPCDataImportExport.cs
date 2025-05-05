#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

public class NPCDataImportExport : Editor
{
    // 表头定义 - 包含所有需要导入导出的字段
    private static readonly string[] HeaderRow = new string[] {
        "路径ID", "路径名称", "分数下限", "分数上限", "事件ID", "动作类型", "路径点索引", 
        "对话内容", "动画名称", "循环动画", "显示时间", "延迟时间", "停留时间", 
        "手势类型", "分数影响", "覆盖前一动作", "是否活跃", "事件起始点", "事件结束点"
    };

    // Excel文件类型
    private static readonly string[] ExcelFileTypes = new string[] { "csv", "CSV" };

    // 导出NPC数据到CSV
    public static void ExportToCSV(NPCData data, string filePath)
    {
        if (data == null)
        {
            EditorUtility.DisplayDialog("导出错误", "未选择NPC数据", "确定");
            return;
        }

        StringBuilder csv = new StringBuilder();
        
        // 添加表头
        csv.AppendLine(string.Join(",", HeaderRow));
        
        // 导出基本信息作为注释
        csv.AppendLine($"# NPC_ID,{data.npcID}");
        csv.AppendLine($"# NPC_NAME,{data.npcName}");
        csv.AppendLine($"# CURRENT_SCORE,{data.currentScore}");
        
        // 导出路径和动作
        foreach (var path in data.paths)
        {
            // 路径描述作为注释
            csv.AppendLine($"# PATH,{path.pathID},{path.pathName}");
            
            // 导出路径动作
            foreach (var action in path.pathActions)
            {
                string[] row = new string[HeaderRow.Length];
                row[0] = path.pathID;
                row[1] = path.pathName;
                row[2] = path.minScore.ToString();
                row[3] = path.maxScore.ToString();
                row[4] = "PATH_ACTION";  // 标记为路径动作
                row[5] = "PATH_ACTION";
                row[6] = action.pathPointIndex.ToString();
                row[7] = EscapeCSVField(action.dialogueText);
                row[8] = action.animationName;
                row[9] = action.loopAnimation.ToString();
                row[10] = action.displayDuration.ToString();
                row[11] = action.delay.ToString();
                row[12] = action.waitTime.ToString();
                row[13] = "";  // 路径动作没有手势类型
                row[14] = "";  // 路径动作没有分数影响
                row[15] = action.overridePrevious.ToString();
                row[16] = action.isActionActive.ToString();
                row[17] = "";  // 路径动作没有起始点
                row[18] = "";  // 路径动作没有结束点
                
                csv.AppendLine(string.Join(",", row));
            }
            
            // 导出事件及其响应
            foreach (var pathEvent in path.events)
            {
                // 事件描述作为注释
                csv.AppendLine($"# EVENT,{path.pathID},{pathEvent.eventID}");
                
                // 导出默认响应
                foreach (var action in pathEvent.defaultResponse.actions)
                {
                    string[] row = new string[HeaderRow.Length];
                    row[0] = path.pathID;
                    row[1] = path.pathName;
                    row[2] = path.minScore.ToString();
                    row[3] = path.maxScore.ToString();
                    row[4] = pathEvent.eventID;
                    row[5] = "DEFAULT";
                    row[6] = action.pathPointIndex.ToString();
                    row[7] = EscapeCSVField(action.dialogueText);
                    row[8] = action.animationName;
                    row[9] = action.loopAnimation.ToString();
                    row[10] = action.displayDuration.ToString();
                    row[11] = action.delay.ToString();
                    row[12] = action.waitTime.ToString();
                    row[13] = "";  // 默认响应没有手势类型
                    row[14] = pathEvent.defaultResponse.scoreEffect.ToString();
                    row[15] = action.overridePrevious.ToString();
                    row[16] = action.isActionActive.ToString();
                    row[17] = pathEvent.startPointIndex.ToString();
                    row[18] = pathEvent.endPointIndex.ToString();
                    
                    csv.AppendLine(string.Join(",", row));
                }
                
                // 导出手势响应
                foreach (var gestureResponse in pathEvent.gestureResponses)
                {
                    foreach (var action in gestureResponse.actions)
                    {
                        string[] row = new string[HeaderRow.Length];
                        row[0] = path.pathID;
                        row[1] = path.pathName;
                        row[2] = path.minScore.ToString();
                        row[3] = path.maxScore.ToString();
                        row[4] = pathEvent.eventID;
                        row[5] = "GESTURE";
                        row[6] = action.pathPointIndex.ToString();
                        row[7] = EscapeCSVField(action.dialogueText);
                        row[8] = action.animationName;
                        row[9] = action.loopAnimation.ToString();
                        row[10] = action.displayDuration.ToString();
                        row[11] = action.delay.ToString();
                        row[12] = action.waitTime.ToString();
                        row[13] = gestureResponse.gestureType;
                        row[14] = gestureResponse.scoreEffect.ToString();
                        row[15] = action.overridePrevious.ToString();
                        row[16] = action.isActionActive.ToString();
                        row[17] = pathEvent.startPointIndex.ToString();
                        row[18] = pathEvent.endPointIndex.ToString();
                        
                        csv.AppendLine(string.Join(",", row));
                    }
                }
            }
            
            // 导出路径分支作为注释
            if (path.nextPaths.Count > 0)
            {
                csv.AppendLine($"# BRANCHES,{path.pathID}");
                foreach (var branch in path.nextPaths)
                {
                    csv.AppendLine($"# BRANCH,{path.pathID},{branch.nextPathID},{branch.minScore},{branch.maxScore},{EscapeCSVField(branch.description)}");
                }
            }
        }
        
        try
        {
            File.WriteAllText(filePath, csv.ToString(), System.Text.Encoding.UTF8);
            EditorUtility.DisplayDialog("导出成功", $"NPC数据已成功导出到:\n{filePath}", "确定");
            
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
    
    // 从CSV导入NPC数据
    public static void ImportFromCSV(NPCData data, string filePath)
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
            string[] lines = File.ReadAllLines(filePath, System.Text.Encoding.UTF8);
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
                
                // 清空当前数据前先解析基本信息
                Dictionary<string, string> metadata = new Dictionary<string, string>();
                Dictionary<string, List<PathBranch>> pathBranches = new Dictionary<string, List<PathBranch>>();
                
                // 第一遍：解析元数据和分支信息
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (line.StartsWith("#"))
                    {
                        // 更新进度
                        EditorUtility.DisplayProgressBar("导入NPC数据", "解析元数据...", (float)i / lines.Length * 0.2f);
                        
                        string content = line.Substring(1).Trim();
                        string[] parts = SplitCSVLine(content);
                        
                        if (parts.Length >= 2)
                        {
                            string type = parts[0].Trim();
                            
                            // 基本元数据
                            if (type == "NPC_ID" || type == "NPC_NAME" || type == "CURRENT_SCORE")
                            {
                                metadata[type] = parts[1].Trim();
                            }
                            // 分支信息
                            else if (type == "BRANCH" && parts.Length >= 5)
                            {
                                string branchPathID = parts[1].Trim();
                                
                                if (!pathBranches.ContainsKey(branchPathID))
                                {
                                    pathBranches[branchPathID] = new List<PathBranch>();
                                }
                                
                                PathBranch branch = new PathBranch
                                {
                                    nextPathID = parts[2].Trim()
                                };
                                
                                float minScore, maxScore;
                                if (float.TryParse(parts[3], out minScore))
                                    branch.minScore = minScore;
                                
                                if (float.TryParse(parts[4], out maxScore))
                                    branch.maxScore = maxScore;
                                
                                if (parts.Length >= 6)
                                    branch.description = parts[5];
                                
                                pathBranches[branchPathID].Add(branch);
                            }
                        }
                    }
                }
                
                // 设置基本信息
                if (metadata.ContainsKey("NPC_ID")) data.npcID = metadata["NPC_ID"];
                if (metadata.ContainsKey("NPC_NAME")) data.npcName = metadata["NPC_NAME"];
                if (metadata.ContainsKey("CURRENT_SCORE"))
                {
                    float score;
                    if (float.TryParse(metadata["CURRENT_SCORE"], out score))
                    {
                        data.currentScore = score;
                    }
                }

                // 清空当前路径
                data.paths.Clear();

                // 解析CSV数据
                Dictionary<string, PathConfig> pathConfigs = new Dictionary<string, PathConfig>();
                Dictionary<string, Dictionary<string, PathEvent>> pathEvents = 
                    new Dictionary<string, Dictionary<string, PathEvent>>();

                // 第二遍：解析主数据
                int totalLines = lines.Length;
                for (int i = 1; i < totalLines; i++) // 跳过表头
                {
                    // 更新进度
                    float progress = 0.2f + ((float)i / totalLines) * 0.7f;
                    EditorUtility.DisplayProgressBar("导入NPC数据", 
                        $"处理第 {i} 行，共 {totalLines} 行...", progress);
                    
                    string line = lines[i];
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                    string[] fields = ParseCSVLine(line);
                    if (fields.Length < HeaderRow.Length) continue;

                    string pathID = fields[0];
                    string pathName = fields[1];
                    string eventID = fields[4];
                    string actionType = fields[5];
                    
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
                        
                        // 设置分数范围
                        float minScore, maxScore;
                        if (float.TryParse(fields[2], out minScore))
                            newPath.minScore = minScore;
                        
                        if (float.TryParse(fields[3], out maxScore))
                            newPath.maxScore = maxScore;
                        
                        // 添加分支
                        if (pathBranches.ContainsKey(pathID))
                        {
                            newPath.nextPaths = pathBranches[pathID];
                        }
                        
                        pathConfigs[pathID] = newPath;
                        pathEvents[pathID] = new Dictionary<string, PathEvent>();
                    }
                    
                    // 创建动作数据
                    ActionData action = new ActionData();
                    try
                    {
                        action.pathPointIndex = int.Parse(fields[6]);
                    }
                    catch { action.pathPointIndex = 0; }
                    
                    action.dialogueText = fields[7];
                    action.animationName = fields[8];
                    
                    bool boolValue;
                    action.loopAnimation = bool.TryParse(fields[9], out boolValue) ? boolValue : false;
                    
                    float floatVal;
                    action.displayDuration = float.TryParse(fields[10], out floatVal) ? floatVal : 2.0f;
                    action.delay = float.TryParse(fields[11], out floatVal) ? floatVal : 0.5f;
                    action.waitTime = float.TryParse(fields[12], out floatVal) ? floatVal : 0f;
                    
                    // 额外属性
                    if (fields.Length > 15)
                        action.overridePrevious = bool.TryParse(fields[15], out boolValue) ? boolValue : true;
                    
                    if (fields.Length > 16)
                        action.isActionActive = bool.TryParse(fields[16], out boolValue) ? boolValue : true;
                    
                    // 处理不同类型的动作
                    if (actionType == "PATH_ACTION")
                    {
                        pathConfigs[pathID].pathActions.Add(action);
                    }
                    else if (actionType == "DEFAULT" || actionType == "GESTURE")
                    {
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
                            
                            // 设置事件起始点和结束点
                            if (fields.Length > 17)
                            {
                                int pointIndex;
                                if (int.TryParse(fields[17], out pointIndex))
                                    newEvent.startPointIndex = pointIndex;
                            }
                            
                            if (fields.Length > 18)
                            {
                                int pointIndex;
                                if (int.TryParse(fields[18], out pointIndex))
                                    newEvent.endPointIndex = pointIndex;
                            }
                            
                            pathEvents[pathID][eventID] = newEvent;
                            pathConfigs[pathID].events.Add(newEvent);
                        }
                        
                        PathEvent currentEvent = pathEvents[pathID][eventID];
                        
                        // 默认响应
                        if (actionType == "DEFAULT")
                        {
                            currentEvent.defaultResponse.actions.Add(action);
                            float score;
                            if (float.TryParse(fields[14], out score))
                            {
                                currentEvent.defaultResponse.scoreEffect = score;
                            }
                        }
                        // 手势响应
                        else if (actionType == "GESTURE")
                        {
                            string gestureType = fields[13];
                            
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
                            float score;
                            if (float.TryParse(fields[14], out score))
                            {
                                gestureResponse.scoreEffect = score;
                            }
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
                EditorUtility.DisplayDialog("导入成功", "成功从CSV导入NPC数据", "确定");
                
                // 更新导入导出路径
                data.importExportPath = filePath;
            }
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("导入错误", $"导入失败: {e.Message}", "确定");
            Debug.LogException(e);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }
    
    // 导出所有NPC数据
    public static void ExportAllNPCData()
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
                    if (EditorUtility.DisplayCancelableProgressBar("批量导出NPC数据", 
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
                            
                        fileName = SanitizeFileName(fileName) + "_data.csv";
                        string exportPath = Path.Combine(folder, fileName);
                        
                        ExportToCSV(npcData, exportPath);
                        successCount++;
                    }
                    catch
                    {
                        failCount++;
                    }
                }
            }
            
            string message = $"导出完成: 成功 {successCount} 个, 失败 {failCount} 个";
            if (failCount > 0)
                message += "\n\n请检查控制台获取错误详情";
                
            EditorUtility.DisplayDialog("批量导出结果", message, "确定");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }
    
    // 生成空白模板
    public static void GenerateTemplate(string filePath)
    {
        StringBuilder template = new StringBuilder();
        
        // 添加表头
        template.AppendLine(string.Join(",", HeaderRow));
        
        // 添加基本信息示例
        template.AppendLine("# NPC_ID,YOUR_NPC_ID");
        template.AppendLine("# NPC_NAME,YOUR_NPC_NAME");
        template.AppendLine("# CURRENT_SCORE,50");
        
        // 添加示例路径和分支
        template.AppendLine("# PATH,path_1,第一条路径");
        template.AppendLine("# BRANCH,path_1,path_2,0,50,\"分数低于50时选择此路径\"");
        template.AppendLine("# BRANCH,path_1,path_3,50,100,\"分数大于等于50时选择此路径\"");
        
        // 添加示例行
        template.AppendLine("path_1,第一条路径,0,100,PATH_ACTION,PATH_ACTION,0,\"这是一句示例对话\",Idle,FALSE,2.0,0.5,0.0,,,TRUE,TRUE,,");
        template.AppendLine("path_1,第一条路径,0,100,event_1,DEFAULT,0,\"默认响应的对话\",Talk,FALSE,2.0,0.5,1.0,,10,TRUE,TRUE,5,7");
        template.AppendLine("path_1,第一条路径,0,100,event_1,GESTURE,0,\"对鸟手势的回应\",Happy,TRUE,2.0,0.5,0.0,Bird,20,TRUE,TRUE,5,7");
        
        try
        {
            File.WriteAllText(filePath, template.ToString(), System.Text.Encoding.UTF8);
            EditorUtility.DisplayDialog("模板生成成功", $"空白模板已生成到:\n{filePath}", "确定");
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("模板生成错误", $"生成失败: {e.Message}", "确定");
        }
    }

    // 添加菜单项
    [MenuItem("工具/NPC数据/批量导出所有NPC数据")]
    public static void MenuExportAllNPCData()
    {
        ExportAllNPCData();
    }
    
    [MenuItem("工具/NPC数据/生成模板")]
    public static void MenuGenerateTemplate()
    {
        string path = EditorUtility.SaveFilePanel("保存CSV模板", Application.dataPath, 
            "npc_data_template.csv", "csv");
        if(!string.IsNullOrEmpty(path))
        {
            GenerateTemplate(path);
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
    private static string[] SplitCSVLine(string line)
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
    
    // 解析CSV行
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