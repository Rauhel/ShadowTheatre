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
        "手势类型", "分数影响", "是否活跃", "事件起始点", "事件结束点"
    };

    // Excel文件类型
    public static readonly string[] ExcelFileTypes = new string[] { "csv", "CSV" };

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
            // 路径描述作为注释（增加时间信息）
            csv.AppendLine($"# PATH,{path.pathID},{path.pathName}");
            csv.AppendLine($"# PATH_START_TIME,{path.pathID},{path.pathStartStoryTime}");
            
            // 导出时间控制点作为注释
            if (path.timePoints != null && path.timePoints.Count > 0)
            {
                foreach (var timePoint in path.timePoints)
                {
                    csv.AppendLine($"# TIME_POINT,{path.pathID},{timePoint.pathPointIndex},{timePoint.requiredStoryTime},{EscapeCSVField(timePoint.description)}");
                }
            }
            
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
                row[9] = action.animationLoopCount.ToString();
                row[10] = action.displayDuration.ToString();
                row[11] = action.delay.ToString();
                row[12] = action.stopTime.ToString();
                row[13] = "";  // 路径动作没有手势类型
                row[14] = "";  // 路径动作没有分数影响
                row[15] = action.isActionActive.ToString();
                row[16] = "";  // 路径动作没有起始点
                row[17] = "";  // 路径动作没有结束点
                
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
                    row[9] = action.animationLoopCount.ToString();
                    row[10] = action.displayDuration.ToString();
                    row[11] = action.delay.ToString();
                    row[12] = action.stopTime.ToString();
                    row[13] = "";  // 默认响应没有手势类型
                    row[14] = pathEvent.defaultResponse.scoreEffect.ToString();
                    row[15] = action.isActionActive.ToString();
                    row[16] = pathEvent.startPointIndex.ToString();
                    row[17] = pathEvent.endPointIndex.ToString();
                    
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
                        row[9] = action.animationLoopCount.ToString();
                        row[10] = action.displayDuration.ToString();
                        row[11] = action.delay.ToString();
                        row[12] = action.stopTime.ToString();
                        row[13] = gestureResponse.gestureType;
                        row[14] = gestureResponse.scoreEffect.ToString();
                        row[15] = action.isActionActive.ToString();
                        row[16] = pathEvent.startPointIndex.ToString();
                        row[17] = pathEvent.endPointIndex.ToString();
                        
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
                
                // 清空当前数据前先解析基本信息
                Dictionary<string, string> metadata = new Dictionary<string, string>();
                Dictionary<string, List<PathBranch>> pathBranches = new Dictionary<string, List<PathBranch>>();
                Dictionary<string, float> pathStartTimes = new Dictionary<string, float>(); // 新增：路径起始时间
                Dictionary<string, List<PathTimePoint>> pathTimePoints = new Dictionary<string, List<PathTimePoint>>(); // 新增：时间控制点
                
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
                            // 路径起始时间
                            else if (type == "PATH_START_TIME" && parts.Length >= 3)
                            {
                                string pathID = parts[1].Trim();
                                float startTime;
                                if (float.TryParse(parts[2], out startTime))
                                {
                                    pathStartTimes[pathID] = startTime;
                                }
                            }
                            // 时间控制点
                            else if (type == "TIME_POINT" && parts.Length >= 4)
                            {
                                string pathID = parts[1].Trim();
                                int pathPointIndex;
                                float requiredTime;
                                
                                if (int.TryParse(parts[2], out pathPointIndex) && 
                                    float.TryParse(parts[3], out requiredTime))
                                {
                                    if (!pathTimePoints.ContainsKey(pathID))
                                    {
                                        pathTimePoints[pathID] = new List<PathTimePoint>();
                                    }
                                    
                                    PathTimePoint timePoint = new PathTimePoint
                                    {
                                        pathPointIndex = pathPointIndex,
                                        requiredStoryTime = requiredTime,
                                        description = parts.Length >= 5 ? parts[4] : ""
                                    };
                                    
                                    pathTimePoints[pathID].Add(timePoint);
                                }
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
                
                // 用于记录上下文信息的变量
                string lastPathID = "";
                string lastPathName = "";
                float lastMinScore = 0f;
                float lastMaxScore = 100f;
                string lastEventID = "";
                string lastActionType = "";
                int nextPathPointIndex = 0;
                
                for (int i = 1; i < totalLines; i++) // 跳过表头
                {
                    // 更新进度
                    float progress = 0.2f + ((float)i / totalLines) * 0.7f;
                    EditorUtility.DisplayProgressBar("导入NPC数据", 
                        $"处理第 {i} 行，共 {totalLines} 行...", progress);
                    
                    string line = lines[i];
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                    string[] fields = ParseCSVLine(line);
                    if (fields.Length == 0) continue; // 跳过空行
                    
                    // 补全字段数组到表头长度
                    if (fields.Length < HeaderRow.Length)
                    {
                        string[] extendedFields = new string[HeaderRow.Length];
                        for (int f = 0; f < HeaderRow.Length; f++)
                        {
                            if (f < fields.Length)
                                extendedFields[f] = fields[f];
                            else
                                extendedFields[f] = ""; // 填充空值
                        }
                        fields = extendedFields;
                    }

                    // 智能补全字段
                    // 1. 路径ID
                    if (string.IsNullOrWhiteSpace(fields[0]))
                        fields[0] = lastPathID;
                    else
                        lastPathID = fields[0];
                    
                    // 2. 路径名称
                    if (string.IsNullOrWhiteSpace(fields[1]))
                        fields[1] = lastPathName;
                    else
                        lastPathName = fields[1];
                    
                    // 3. 分数范围
                    if (string.IsNullOrWhiteSpace(fields[2]) || !float.TryParse(fields[2], out _))
                        fields[2] = lastMinScore.ToString();
                    else
                        float.TryParse(fields[2], out lastMinScore);
                    
                    if (string.IsNullOrWhiteSpace(fields[3]) || !float.TryParse(fields[3], out _))
                        fields[3] = lastMaxScore.ToString();
                    else
                        float.TryParse(fields[3], out lastMaxScore);
                    
                    // 4. 事件ID
                    if (string.IsNullOrWhiteSpace(fields[4]))
                        fields[4] = lastEventID;
                    else
                        lastEventID = fields[4];
                    
                    // 5. 动作类型
                    if (string.IsNullOrWhiteSpace(fields[5]))
                        fields[5] = lastActionType;
                    else
                        lastActionType = fields[5];
                    
                    // 6. 路径点索引 - 自动递增
                    if (string.IsNullOrWhiteSpace(fields[6]) || !int.TryParse(fields[6], out _))
                        fields[6] = (nextPathPointIndex++).ToString();
                    else
                        nextPathPointIndex = int.Parse(fields[6]) + 1;

                    string pathID = fields[0];
                    string pathName = fields[1];
                    string eventID = fields[4];
                    string actionType = fields[5];
                    
                    // 确保路径存在 (剩余逻辑与之前相同)
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
                        else
                            newPath.minScore = 0f; // 默认最小值
                        
                        if (float.TryParse(fields[3], out maxScore))
                            newPath.maxScore = maxScore;
                        else
                            newPath.maxScore = 100f; // 默认最大值
                        
                        // 添加分支
                        if (pathBranches.ContainsKey(pathID))
                        {
                            newPath.nextPaths = pathBranches[pathID];
                        }
                        
                        // 应用时间控制数据
                        if (pathStartTimes.ContainsKey(pathID))
                        {
                            newPath.pathStartStoryTime = pathStartTimes[pathID];
                        }
                        
                        if (pathTimePoints.ContainsKey(pathID))
                        {
                            newPath.timePoints = pathTimePoints[pathID];
                            // 按时间排序时间控制点
                            newPath.timePoints.Sort((a, b) => a.requiredStoryTime.CompareTo(b.requiredStoryTime));
                        }
                        else
                        {
                            newPath.timePoints = new List<PathTimePoint>();
                        }
                        
                        pathConfigs[pathID] = newPath;
                        pathEvents[pathID] = new Dictionary<string, PathEvent>();
                    }
                    
                    // 创建动作数据
                    ActionData action = new ActionData();
                    
                    // 路径点索引
                    if (int.TryParse(fields[6], out int pointIndex))
                        action.pathPointIndex = pointIndex;
                    else
                        action.pathPointIndex = 0;
                    
                    // 对话文本
                    action.dialogueText = fields[7];
                    
                    // 动画名称
                    action.animationName = fields[8];
                    
                    // 循环动画 - 改为动画循环次数
                    if (int.TryParse(fields[9], out int loopCount))
                        action.animationLoopCount = loopCount;
                    else
                        action.animationLoopCount = 1; // 默认值为1次
                    
                    // 显示时间
                    if (float.TryParse(fields[10], out float displayTime))
                        action.displayDuration = displayTime;
                    else
                        action.displayDuration = 2.0f; // 默认值
                    
                    // 延迟时间
                    if (float.TryParse(fields[11], out float delayTime))
                        action.delay = delayTime;
                    else
                        action.delay = 0.5f; // 默认值
                    
                    // 停留时间
                    if (float.TryParse(fields[12], out float waitTime))
                        action.stopTime = waitTime;
                    else
                        action.stopTime = 0f; // 默认值
                    
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
                            if (fields.Length > 13 && int.TryParse(fields[13], out int startPoint))
                                newEvent.startPointIndex = startPoint;
                            else
                                newEvent.startPointIndex = action.pathPointIndex; // 默认使用当前路径点
                            
                            if (fields.Length > 14 && int.TryParse(fields[14], out int endPoint))
                                newEvent.endPointIndex = endPoint;
                            else
                                newEvent.endPointIndex = action.pathPointIndex + 2; // 默认结束点为当前点+2
                            
                            pathEvents[pathID][eventID] = newEvent;
                            pathConfigs[pathID].events.Add(newEvent);
                        }
                        
                        PathEvent currentEvent = pathEvents[pathID][eventID];
                        
                        // 默认响应
                        if (actionType == "DEFAULT")
                        {
                            currentEvent.defaultResponse.actions.Add(action);
                            
                            // 分数影响
                            if (fields.Length > 14 && float.TryParse(fields[14], out float scoreEffect))
                                currentEvent.defaultResponse.scoreEffect = scoreEffect;
                            UnityEngine.Debug.Log($"[ImportExport] 添加DEFAULT动作到默认响应");
                        }
                        // 手势响应
                        else if (actionType == "GESTURE")
                        {
                            string originalGestureType = fields.Length > 13 ? fields[13] : "DEFAULT";
                            string gestureType = NormalizeGestureTypeForImport(originalGestureType);
                            
                            if (originalGestureType != gestureType)
                            {
                                UnityEngine.Debug.Log($"[ImportExport] 手势类型转换: '{originalGestureType}' -> '{gestureType}'");
                            }
                            
                            // 区分处理：DEFAULT类型作为默认响应，其他作为手势响应
                            if (gestureType == "DEFAULT")
                            {
                                // DEFAULT类型添加到事件的默认响应中
                                currentEvent.defaultResponse.actions.Add(action);
                                
                                // 分数影响
                                if (fields.Length > 14 && float.TryParse(fields[14], out float scoreEffect))
                                    currentEvent.defaultResponse.scoreEffect = scoreEffect;
                                UnityEngine.Debug.Log($"[ImportExport] GESTURE中的DEFAULT添加到默认响应");
                            }
                            else
                            {
                                // 其他手势类型添加到手势响应中
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
                                    UnityEngine.Debug.Log($"[ImportExport] 创建新的手势响应: {gestureType}");
                                }
                                
                                gestureResponse.actions.Add(action);
                                
                                // 分数影响
                                if (fields.Length > 14 && float.TryParse(fields[14], out float scoreEffect))
                                    gestureResponse.scoreEffect = scoreEffect;
                                UnityEngine.Debug.Log($"[ImportExport] 添加动作到手势响应: {gestureType}");
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
                
                // 统计导入结果
                int totalPaths = pathConfigs.Count;
                int totalEvents = pathConfigs.Values.Sum(p => p.events.Count);
                int totalGestureResponses = pathConfigs.Values
                    .SelectMany(p => p.events)
                    .Sum(e => e.gestureResponses.Count);
                
                string summary = $"导入完成!\n路径数: {totalPaths}\n事件数: {totalEvents}\n手势响应数: {totalGestureResponses}";
                EditorUtility.DisplayDialog("导入成功", summary, "确定");
                
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
        template.AppendLine("path_1,第一条路径,0,100,event_1,GESTURE,0,\"对鸟手势的回应\",Happy,TRUE,2.0,0.5,0.0,BIRD,20,TRUE,TRUE,5,7");
        
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

    // 标准化导入的手势类型
    private static string NormalizeGestureTypeForImport(string gestureType)
    {
        if (string.IsNullOrWhiteSpace(gestureType) || gestureType == "未指定")
            return "DEFAULT";

        // 转换为大写并处理旧的命名映射
        string normalized = gestureType.ToUpper().Trim();
        
        // 处理旧的手势类型映射
        switch (normalized)
        {
            case "BIRD":
                return "BIRD";
            case "WOLF":
                return "WOLF";
            case "FROG":
                return "FROG";
            case "GOOSE":
                return "GOOSE";
            case "OWL":
                return "OWL";
            case "DEFAULT":
                return "DEFAULT";
            // 处理旧的命名格式
            case "DEER":
                return "DEFAULT";  // 旧的DEER映射为DEFAULT
            case "SHEEP":
                return "DEFAULT"; // 旧的SHEEP映射为DEFAULT
            case "FIST":
                return "DEFAULT"; // 旧的FIST映射为DEFAULT
            case "未指定":
                return "DEFAULT";
            case "UNKNOWN":
                return "DEFAULT";
            default:
                UnityEngine.Debug.LogWarning($"[ImportExport] 未知的手势类型: {gestureType}，将使用DEFAULT");
                return "DEFAULT";
        }
    }
}
#endif