#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

/// <summary>
/// 高级NPC数据导入导出系统
/// 支持多表格结构，完整导出路径衔接、时间控制等所有配置信息
/// </summary>
public class NPCDataAdvancedImportExport : Editor
{
    #region 表格定义
    
    // 基本信息表头
    private static readonly string[] BasicInfoHeader = new string[] {
        "配置项", "值", "备注"
    };
    
    // 路径基本信息表头
    private static readonly string[] PathBasicHeader = new string[] {
        "路径ID", "路径名称", "分数下限", "分数上限", "开始时间", "结束时间", "备注"
    };
    
    // 路径分支表头（核心：路径衔接信息）
    private static readonly string[] PathBranchHeader = new string[] {
        "当前路径ID", "当前路径名称", "分支序号", "下一路径ID", "下一路径名称", 
        "分数下限", "分数上限", "分支描述", "有效性检查"
    };
    
    // 时间控制表头
    private static readonly string[] TimeControlHeader = new string[] {
        "路径ID", "路径名称", "路径点索引", "到达时间", "停留时间", "描述", "是否虚拟点"
    };
    
    // 路径动作表头
    private static readonly string[] PathActionHeader = new string[] {
        "路径ID", "路径名称", "路径点索引", "对话内容", "动画名称", "循环次数", 
        "显示时间", "延迟时间", "停留时间", "是否激活"
    };
    
    // 事件配置表头
    private static readonly string[] EventConfigHeader = new string[] {
        "路径ID", "路径名称", "事件ID", "起始点", "结束点", "交互半径", 
        "手势保持时间", "最大识别距离", "第一幕启用", "第二幕启用", "第三幕启用"
    };
    
    // 手势响应表头
    private static readonly string[] GestureResponseHeader = new string[] {
        "路径ID", "事件ID", "响应类型", "手势类型", "分数影响", "路径点索引", 
        "对话内容", "动画名称", "循环次数", "显示时间", "延迟时间", "停留时间", "是否激活"
    };
    
    #endregion
    
    #region 主导出功能
    
    /// <summary>
    /// 导出完整的NPC数据到多个CSV文件
    /// </summary>
    public static void ExportToAdvancedCSV(NPCData data, string baseFilePath)
    {
        if (data == null)
        {
            EditorUtility.DisplayDialog("导出错误", "未选择NPC数据", "确定");
            return;
        }
        
        try
        {
            string baseName = Path.GetFileNameWithoutExtension(baseFilePath);
            string directory = Path.GetDirectoryName(baseFilePath);
            
            // 导出基本信息
            ExportBasicInfo(data, Path.Combine(directory, $"{baseName}_01_基本信息.csv"));
            
            // 导出路径基本信息
            ExportPathBasicInfo(data, Path.Combine(directory, $"{baseName}_02_路径基本信息.csv"));
            
            // 导出路径分支（核心：路径衔接）
            ExportPathBranches(data, Path.Combine(directory, $"{baseName}_03_路径分支.csv"));
            
            // 导出时间控制
            ExportTimeControl(data, Path.Combine(directory, $"{baseName}_04_时间控制.csv"));
            
            // 导出路径动作
            ExportPathActions(data, Path.Combine(directory, $"{baseName}_05_路径动作.csv"));
            
            // 导出事件配置
            ExportEventConfig(data, Path.Combine(directory, $"{baseName}_06_事件配置.csv"));
            
            // 导出手势响应
            ExportGestureResponses(data, Path.Combine(directory, $"{baseName}_07_手势响应.csv"));
            
            // 生成说明文档
            GenerateDocumentation(data, Path.Combine(directory, $"{baseName}_00_说明文档.txt"));
            
            EditorUtility.DisplayDialog("导出成功", 
                $"NPC数据已成功导出到:\n{directory}\n\n包含以下文件：\n" +
                "- 00_说明文档.txt\n" +
                "- 01_基本信息.csv\n" +
                "- 02_路径基本信息.csv\n" +
                "- 03_路径分支.csv（核心：路径衔接）\n" +
                "- 04_时间控制.csv\n" +
                "- 05_路径动作.csv\n" +
                "- 06_事件配置.csv\n" +
                "- 07_手势响应.csv", "确定");
            
            // 更新导入导出路径
            data.importExportPath = baseFilePath;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("导出错误", $"导出失败: {e.Message}", "确定");
            Debug.LogException(e);
        }
    }
    
    #endregion
    
    #region 具体导出方法
    
    /// <summary>
    /// 导出基本信息
    /// </summary>
    private static void ExportBasicInfo(NPCData data, string filePath)
    {
        StringBuilder csv = new StringBuilder();
        csv.AppendLine(string.Join(",", BasicInfoHeader));
        
        csv.AppendLine($"NPC ID,{EscapeCSVField(data.npcID)},NPC的唯一标识符");
        csv.AppendLine($"NPC 名称,{EscapeCSVField(data.npcName)},NPC的显示名称");
        csv.AppendLine($"当前分数,{data.currentScore},NPC的当前分数值");
        csv.AppendLine($"移动速度,{data.moveSpeed},NPC的基础移动速度");
        csv.AppendLine($"路径总数,{data.paths.Count},配置的路径总数量");
        
        File.WriteAllText(filePath, csv.ToString(), System.Text.Encoding.UTF8);
        Debug.Log($"[AdvancedExport] 基本信息导出完成: {filePath}");
    }
    
    /// <summary>
    /// 导出路径基本信息
    /// </summary>
    private static void ExportPathBasicInfo(NPCData data, string filePath)
    {
        StringBuilder csv = new StringBuilder();
        csv.AppendLine(string.Join(",", PathBasicHeader));
        
        foreach (var path in data.paths)
        {
            string[] row = new string[] {
                EscapeCSVField(path.pathID),
                EscapeCSVField(path.pathName),
                path.minScore.ToString(),
                path.maxScore.ToString(),
                path.pathStartStoryTime.ToString(),
                path.pathEndStoryTime.ToString(),
                EscapeCSVField($"时间点数量:{path.timePoints?.Count ?? 0}, 动作数量:{path.pathActions?.Count ?? 0}, 事件数量:{path.events?.Count ?? 0}")
            };
            csv.AppendLine(string.Join(",", row));
        }
        
        File.WriteAllText(filePath, csv.ToString(), System.Text.Encoding.UTF8);
        Debug.Log($"[AdvancedExport] 路径基本信息导出完成: {filePath}");
    }
    
    /// <summary>
    /// 导出路径分支（核心：路径衔接信息）
    /// </summary>
    private static void ExportPathBranches(NPCData data, string filePath)
    {
        StringBuilder csv = new StringBuilder();
        csv.AppendLine(string.Join(",", PathBranchHeader));
        
        foreach (var path in data.paths)
        {
            if (path.nextPaths == null || path.nextPaths.Count == 0)
            {
                // 终点路径
                string[] terminalRow = new string[] {
                    EscapeCSVField(path.pathID),
                    EscapeCSVField(path.pathName),
                    "终点",
                    "",
                    "无下一路径",
                    "",
                    "",
                    "这是终点路径",
                    "正常"
                };
                csv.AppendLine(string.Join(",", terminalRow));
            }
            else
            {
                for (int i = 0; i < path.nextPaths.Count; i++)
                {
                    var branch = path.nextPaths[i];
                    
                    // 查找下一路径名称
                    string nextPathName = "未找到路径";
                    string validityCheck = "正常";
                    
                    if (string.IsNullOrEmpty(branch.nextPathID))
                    {
                        nextPathName = "空路径ID";
                        validityCheck = "错误：路径ID为空";
                    }
                    else
                    {
                        var nextPath = data.paths.FirstOrDefault(p => p.pathID == branch.nextPathID);
                        if (nextPath != null)
                        {
                            nextPathName = nextPath.pathName;
                            
                            // 检查循环引用
                            if (nextPath.pathID == path.pathID)
                            {
                                validityCheck = "警告：循环引用自己";
                            }
                        }
                        else
                        {
                            validityCheck = "错误：目标路径不存在";
                        }
                    }
                    
                    string[] row = new string[] {
                        EscapeCSVField(path.pathID),
                        EscapeCSVField(path.pathName),
                        i.ToString(),
                        EscapeCSVField(branch.nextPathID),
                        EscapeCSVField(nextPathName),
                        branch.minScore.ToString(),
                        branch.maxScore.ToString(),
                        EscapeCSVField(branch.description),
                        validityCheck
                    };
                    csv.AppendLine(string.Join(",", row));
                }
            }
        }
        
        File.WriteAllText(filePath, csv.ToString(), System.Text.Encoding.UTF8);
        Debug.Log($"[AdvancedExport] 路径分支导出完成: {filePath}");
    }
    
    /// <summary>
    /// 导出时间控制信息
    /// </summary>
    private static void ExportTimeControl(NPCData data, string filePath)
    {
        StringBuilder csv = new StringBuilder();
        csv.AppendLine(string.Join(",", TimeControlHeader));
        
        foreach (var path in data.paths)
        {
            if (path.timePoints != null && path.timePoints.Count > 0)
            {
                foreach (var timePoint in path.timePoints.OrderBy(tp => tp.requiredStoryTime))
                {
                    string[] row = new string[] {
                        EscapeCSVField(path.pathID),
                        EscapeCSVField(path.pathName),
                        timePoint.pathPointIndex.ToString(),
                        timePoint.requiredStoryTime.ToString(),
                        timePoint.stopTime.ToString(),
                        EscapeCSVField(timePoint.description),
                        timePoint.isVirtualEndTarget ? "是" : "否"
                    };
                    csv.AppendLine(string.Join(",", row));
                }
            }
        }
        
        File.WriteAllText(filePath, csv.ToString(), System.Text.Encoding.UTF8);
        Debug.Log($"[AdvancedExport] 时间控制导出完成: {filePath}");
    }
    
    /// <summary>
    /// 导出路径动作
    /// </summary>
    private static void ExportPathActions(NPCData data, string filePath)
    {
        StringBuilder csv = new StringBuilder();
        csv.AppendLine(string.Join(",", PathActionHeader));
        
        foreach (var path in data.paths)
        {
            if (path.pathActions != null && path.pathActions.Count > 0)
            {
                foreach (var action in path.pathActions)
                {
                    string[] row = new string[] {
                        EscapeCSVField(path.pathID),
                        EscapeCSVField(path.pathName),
                        action.pathPointIndex.ToString(),
                        EscapeCSVField(action.dialogueText),
                        EscapeCSVField(action.animationName),
                        action.animationLoopCount.ToString(),
                        action.displayDuration.ToString(),
                        action.delay.ToString(),
                        action.stopTime.ToString(),
                        action.isActionActive ? "是" : "否"
                    };
                    csv.AppendLine(string.Join(",", row));
                }
            }
        }
        
        File.WriteAllText(filePath, csv.ToString(), System.Text.Encoding.UTF8);
        Debug.Log($"[AdvancedExport] 路径动作导出完成: {filePath}");
    }
    
    /// <summary>
    /// 导出事件配置
    /// </summary>
    private static void ExportEventConfig(NPCData data, string filePath)
    {
        StringBuilder csv = new StringBuilder();
        csv.AppendLine(string.Join(",", EventConfigHeader));
        
        foreach (var path in data.paths)
        {
            if (path.events != null && path.events.Count > 0)
            {
                foreach (var pathEvent in path.events)
                {
                    string[] row = new string[] {
                        EscapeCSVField(path.pathID),
                        EscapeCSVField(path.pathName),
                        EscapeCSVField(pathEvent.eventID),
                        pathEvent.startPointIndex.ToString(),
                        pathEvent.endPointIndex.ToString(),
                        pathEvent.playerInteractionRadius.ToString(),
                        pathEvent.gestureHoldTime.ToString(),
                        pathEvent.maxRecognitionDistance.ToString(),
                        pathEvent.enabledInAct1 ? "是" : "否",
                        pathEvent.enabledInAct2 ? "是" : "否",
                        pathEvent.enabledInAct3 ? "是" : "否"
                    };
                    csv.AppendLine(string.Join(",", row));
                }
            }
        }
        
        File.WriteAllText(filePath, csv.ToString(), System.Text.Encoding.UTF8);
        Debug.Log($"[AdvancedExport] 事件配置导出完成: {filePath}");
    }
    
    /// <summary>
    /// 导出手势响应
    /// </summary>
    private static void ExportGestureResponses(NPCData data, string filePath)
    {
        StringBuilder csv = new StringBuilder();
        csv.AppendLine(string.Join(",", GestureResponseHeader));
        
        foreach (var path in data.paths)
        {
            if (path.events != null && path.events.Count > 0)
            {
                foreach (var pathEvent in path.events)
                {
                    // 导出默认响应
                    if (pathEvent.defaultResponse?.actions != null)
                    {
                        foreach (var action in pathEvent.defaultResponse.actions)
                        {
                            string[] row = new string[] {
                                EscapeCSVField(path.pathID),
                                EscapeCSVField(pathEvent.eventID),
                                "默认响应",
                                "",
                                pathEvent.defaultResponse.scoreEffect.ToString(),
                                action.pathPointIndex.ToString(),
                                EscapeCSVField(action.dialogueText),
                                EscapeCSVField(action.animationName),
                                action.animationLoopCount.ToString(),
                                action.displayDuration.ToString(),
                                action.delay.ToString(),
                                action.stopTime.ToString(),
                                action.isActionActive ? "是" : "否"
                            };
                            csv.AppendLine(string.Join(",", row));
                        }
                    }
                    
                    // 导出手势响应
                    if (pathEvent.gestureResponses != null)
                    {
                        foreach (var gestureResponse in pathEvent.gestureResponses)
                        {
                            if (gestureResponse.actions != null)
                            {
                                foreach (var action in gestureResponse.actions)
                                {
                                    string[] row = new string[] {
                                        EscapeCSVField(path.pathID),
                                        EscapeCSVField(pathEvent.eventID),
                                        "手势响应",
                                        EscapeCSVField(gestureResponse.gestureType),
                                        gestureResponse.scoreEffect.ToString(),
                                        action.pathPointIndex.ToString(),
                                        EscapeCSVField(action.dialogueText),
                                        EscapeCSVField(action.animationName),
                                        action.animationLoopCount.ToString(),
                                        action.displayDuration.ToString(),
                                        action.delay.ToString(),
                                        action.stopTime.ToString(),
                                        action.isActionActive ? "是" : "否"
                                    };
                                    csv.AppendLine(string.Join(",", row));
                                }
                            }
                        }
                    }
                }
            }
        }
        
        File.WriteAllText(filePath, csv.ToString(), System.Text.Encoding.UTF8);
        Debug.Log($"[AdvancedExport] 手势响应导出完成: {filePath}");
    }
    
    /// <summary>
    /// 生成说明文档
    /// </summary>
    private static void GenerateDocumentation(NPCData data, string filePath)
    {
        StringBuilder doc = new StringBuilder();
        
        doc.AppendLine("==========================================");
        doc.AppendLine($"NPC数据导出说明文档");
        doc.AppendLine($"NPC名称: {data.npcName}");
        doc.AppendLine($"导出时间: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        doc.AppendLine("==========================================");
        doc.AppendLine();
        
        doc.AppendLine("📁 文件说明：");
        doc.AppendLine("01_基本信息.csv      - NPC的基础属性（ID、名称、分数等）");
        doc.AppendLine("02_路径基本信息.csv  - 每条路径的基本信息（ID、名称、分数范围、时间等）");
        doc.AppendLine("03_路径分支.csv      - ⭐核心文件⭐ 路径间的衔接关系和分数条件");
        doc.AppendLine("04_时间控制.csv      - 路径上的时间控制点配置");
        doc.AppendLine("05_路径动作.csv      - 路径上的动作和对话");
        doc.AppendLine("06_事件配置.csv      - 交互事件的配置");
        doc.AppendLine("07_手势响应.csv      - 事件的手势响应配置");
        doc.AppendLine();
        
        doc.AppendLine("🔗 路径衔接关系图：");
        var pathGraph = BuildPathGraph(data);
        foreach (var line in pathGraph)
        {
            doc.AppendLine(line);
        }
        doc.AppendLine();
        
        doc.AppendLine("⚠️ 配置检查结果：");
        var validationResults = ValidatePathConfiguration(data);
        foreach (var result in validationResults)
        {
            doc.AppendLine(result);
        }
        doc.AppendLine();
        
        doc.AppendLine("📊 统计信息：");
        doc.AppendLine($"- 总路径数量: {data.paths.Count}");
        doc.AppendLine($"- 终点路径数量: {data.paths.Count(p => p.nextPaths == null || p.nextPaths.Count == 0)}");
        doc.AppendLine($"- 分支路径数量: {data.paths.Count(p => p.nextPaths != null && p.nextPaths.Count > 1)}");
        doc.AppendLine($"- 总时间控制点: {data.paths.Sum(p => p.timePoints?.Count ?? 0)}");
        doc.AppendLine($"- 总路径动作: {data.paths.Sum(p => p.pathActions?.Count ?? 0)}");
        doc.AppendLine($"- 总交互事件: {data.paths.Sum(p => p.events?.Count ?? 0)}");
        
        File.WriteAllText(filePath, doc.ToString(), System.Text.Encoding.UTF8);
        Debug.Log($"[AdvancedExport] 说明文档生成完成: {filePath}");
    }
    
    #endregion
    
    #region 分析和验证功能
    
    /// <summary>
    /// 构建路径关系图
    /// </summary>
    private static List<string> BuildPathGraph(NPCData data)
    {
        var graph = new List<string>();
        
        foreach (var path in data.paths)
        {
            if (path.nextPaths == null || path.nextPaths.Count == 0)
            {
                graph.Add($"{path.pathName} → [终点]");
            }
            else if (path.nextPaths.Count == 1)
            {
                var nextPath = data.paths.FirstOrDefault(p => p.pathID == path.nextPaths[0].nextPathID);
                string nextName = nextPath?.pathName ?? "未找到路径";
                graph.Add($"{path.pathName} → {nextName} (分数: {path.nextPaths[0].minScore}-{path.nextPaths[0].maxScore})");
            }
            else
            {
                graph.Add($"{path.pathName} → [多分支]");
                for (int i = 0; i < path.nextPaths.Count; i++)
                {
                    var branch = path.nextPaths[i];
                    var nextPath = data.paths.FirstOrDefault(p => p.pathID == branch.nextPathID);
                    string nextName = nextPath?.pathName ?? "未找到路径";
                    graph.Add($"  └─ 分支{i}: {nextName} (分数: {branch.minScore}-{branch.maxScore})");
                }
            }
        }
        
        return graph;
    }
    
    /// <summary>
    /// 验证路径配置
    /// </summary>
    private static List<string> ValidatePathConfiguration(NPCData data)
    {
        var results = new List<string>();
        
        // 检查路径ID重复
        var duplicateIds = data.paths.GroupBy(p => p.pathID)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);
        
        foreach (var id in duplicateIds)
        {
            results.Add($"❌ 重复的路径ID: {id}");
        }
        
        // 检查路径分支
        foreach (var path in data.paths)
        {
            if (path.nextPaths != null)
            {
                foreach (var branch in path.nextPaths)
                {
                    if (string.IsNullOrEmpty(branch.nextPathID))
                    {
                        results.Add($"❌ 路径 {path.pathName} 存在空的分支路径ID");
                    }
                    else if (!data.paths.Any(p => p.pathID == branch.nextPathID))
                    {
                        results.Add($"❌ 路径 {path.pathName} 的分支指向不存在的路径: {branch.nextPathID}");
                    }
                    else if (branch.nextPathID == path.pathID)
                    {
                        results.Add($"⚠️ 路径 {path.pathName} 存在循环引用自己");
                    }
                }
            }
        }
        
        if (results.Count == 0)
        {
            results.Add("✅ 路径配置检查通过，未发现问题");
        }
        
        return results;
    }
    
    #endregion
    
    #region 菜单入口
    
    [MenuItem("工具/NPC数据/高级版/导出当前NPC数据")]
    public static void MenuExportAdvanced()
    {
        var activeObject = Selection.activeObject;
        if (activeObject is NPCData npcData)
        {
            string defaultPath = !string.IsNullOrEmpty(npcData.importExportPath) 
                ? npcData.importExportPath 
                : $"Assets/NPCData_{npcData.npcName}_Advanced.csv";
                
            string path = EditorUtility.SaveFilePanel(
                "导出NPC数据（高级版）",
                Path.GetDirectoryName(defaultPath),
                Path.GetFileNameWithoutExtension(defaultPath),
                "csv"
            );
            
            if (!string.IsNullOrEmpty(path))
            {
                ExportToAdvancedCSV(npcData, path);
            }
        }
        else
        {
            EditorUtility.DisplayDialog("错误", "请先选择一个NPCData资源", "确定");
        }
    }
    
    [MenuItem("工具/NPC数据/高级版/批量导出所有NPC数据")]
    public static void MenuExportAllAdvanced()
    {
        string folderPath = EditorUtility.SaveFolderPanel("选择导出文件夹", "Assets", "");
        if (string.IsNullOrEmpty(folderPath))
            return;
            
        // 查找所有NPCData资源
        string[] guids = AssetDatabase.FindAssets("t:NPCData");
        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", "未找到任何NPCData资源", "确定");
            return;
        }
        
        int exportedCount = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            NPCData npcData = AssetDatabase.LoadAssetAtPath<NPCData>(assetPath);
            
            if (npcData != null)
            {
                EditorUtility.DisplayProgressBar("批量导出NPC数据", 
                    $"正在导出: {npcData.npcName} ({i + 1}/{guids.Length})", 
                    (float)i / guids.Length);
                
                string fileName = $"NPCData_{npcData.npcName}_Advanced.csv";
                string filePath = Path.Combine(folderPath, fileName);
                
                try
                {
                    ExportToAdvancedCSV(npcData, filePath);
                    exportedCount++;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"导出 {npcData.npcName} 失败: {e.Message}");
                }
            }
        }
        
        EditorUtility.ClearProgressBar();
        EditorUtility.DisplayDialog("批量导出完成", 
            $"成功导出 {exportedCount} 个NPC数据到:\n{folderPath}", "确定");
    }
    
    [MenuItem("工具/NPC数据/高级版/分析当前NPC路径配置")]
    public static void MenuAnalyzeCurrentNPC()
    {
        var activeObject = Selection.activeObject;
        if (activeObject is NPCData npcData)
        {
            AnalyzeNPCConfiguration(npcData);
        }
        else
        {
            EditorUtility.DisplayDialog("错误", "请先选择一个NPCData资源", "确定");
        }
    }
    
    [MenuItem("工具/NPC数据/高级版/生成路径关系图")]
    public static void MenuGeneratePathGraph()
    {
        var activeObject = Selection.activeObject;
        if (activeObject is NPCData npcData)
        {
            string defaultPath = $"Assets/PathGraph_{npcData.npcName}.txt";
            string path = EditorUtility.SaveFilePanel(
                "保存路径关系图",
                "Assets",
                $"PathGraph_{npcData.npcName}",
                "txt"
            );
            
            if (!string.IsNullOrEmpty(path))
            {
                GeneratePathGraphFile(npcData, path);
            }
        }
        else
        {
            EditorUtility.DisplayDialog("错误", "请先选择一个NPCData资源", "确定");
        }
    }
    
    [MenuItem("工具/NPC数据/高级版/生成Mermaid路径图")]
    public static void MenuGenerateMermaidGraph()
    {
        var activeObject = Selection.activeObject;
        if (activeObject is NPCData npcData)
        {
            string mermaidCode = GenerateMermaidPathGraph(npcData);
            
            string path = EditorUtility.SaveFilePanel(
                "保存Mermaid路径图",
                "Assets",
                $"PathGraph_{npcData.npcName}_Mermaid",
                "md"
            );
            
            if (!string.IsNullOrEmpty(path))
            {
                StringBuilder markdown = new StringBuilder();
                markdown.AppendLine($"# {npcData.npcName} 路径关系图");
                markdown.AppendLine();
                markdown.AppendLine("```mermaid");
                markdown.AppendLine(mermaidCode);
                markdown.AppendLine("```");
                markdown.AppendLine();
                markdown.AppendLine("## 说明");
                markdown.AppendLine("- 🟦 普通路径：蓝色矩形");
                markdown.AppendLine("- 🟨 分支路径：橙色菱形");
                markdown.AppendLine("- 🟩 终点路径：绿色圆角矩形");
                markdown.AppendLine("- ❌ 错误路径：红色虚线连接");
                
                File.WriteAllText(path, markdown.ToString(), System.Text.Encoding.UTF8);
                EditorUtility.DisplayDialog("生成成功", 
                    $"Mermaid路径图已保存到:\n{path}\n\n" +
                    "可以在支持Mermaid的编辑器中查看图表，如：\n" +
                    "- GitHub/GitLab\n" +
                    "- Typora\n" +
                    "- VS Code (with Mermaid extension)", "确定");
                
                Debug.Log($"[AdvancedExport] Mermaid路径图生成完成: {path}");
            }
        }
        else
        {
            EditorUtility.DisplayDialog("错误", "请先选择一个NPCData资源", "确定");
        }
    }
    
    #endregion
    
    #region 分析工具
    
    /// <summary>
    /// 分析NPC配置并在控制台输出结果
    /// </summary>
    private static void AnalyzeNPCConfiguration(NPCData data)
    {
        Debug.Log($"==================== {data.npcName} 配置分析 ====================");
        
        // 基本统计
        Debug.Log($"📊 基本统计:");
        Debug.Log($"  - 总路径数量: {data.paths.Count}");
        Debug.Log($"  - 终点路径: {data.paths.Count(p => p.nextPaths == null || p.nextPaths.Count == 0)}");
        Debug.Log($"  - 分支路径: {data.paths.Count(p => p.nextPaths != null && p.nextPaths.Count > 1)}");
        Debug.Log($"  - 总时间控制点: {data.paths.Sum(p => p.timePoints?.Count ?? 0)}");
        Debug.Log($"  - 总路径动作: {data.paths.Sum(p => p.pathActions?.Count ?? 0)}");
        Debug.Log($"  - 总交互事件: {data.paths.Sum(p => p.events?.Count ?? 0)}");
        
        // 路径关系图
        Debug.Log($"🔗 路径关系图:");
        var pathGraph = BuildPathGraph(data);
        foreach (var line in pathGraph)
        {
            Debug.Log($"  {line}");
        }
        
        // 配置验证
        Debug.Log($"⚠️ 配置检查:");
        var validationResults = ValidatePathConfiguration(data);
        foreach (var result in validationResults)
        {
            if (result.StartsWith("❌"))
                Debug.LogError($"  {result}");
            else if (result.StartsWith("⚠️"))
                Debug.LogWarning($"  {result}");
            else
                Debug.Log($"  {result}");
        }
        
        Debug.Log($"==================== 分析完成 ====================");
    }
    
    /// <summary>
    /// 生成路径关系图文件
    /// </summary>
    private static void GeneratePathGraphFile(NPCData data, string filePath)
    {
        StringBuilder graph = new StringBuilder();
        
        graph.AppendLine($"路径关系图 - {data.npcName}");
        graph.AppendLine($"生成时间: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        graph.AppendLine("".PadRight(50, '='));
        graph.AppendLine();
        
        var pathGraph = BuildPathGraph(data);
        foreach (var line in pathGraph)
        {
            graph.AppendLine(line);
        }
        
        graph.AppendLine();
        graph.AppendLine("配置检查结果:");
        graph.AppendLine("".PadRight(20, '-'));
        var validationResults = ValidatePathConfiguration(data);
        foreach (var result in validationResults)
        {
            graph.AppendLine(result);
        }
        
        File.WriteAllText(filePath, graph.ToString(), System.Text.Encoding.UTF8);
        EditorUtility.DisplayDialog("生成成功", $"路径关系图已保存到:\n{filePath}", "确定");
        Debug.Log($"[AdvancedExport] 路径关系图生成完成: {filePath}");
    }
    
    /// <summary>
    /// 生成Mermaid格式的路径关系图
    /// </summary>
    public static string GenerateMermaidPathGraph(NPCData data)
    {
        StringBuilder mermaid = new StringBuilder();
        
        mermaid.AppendLine("graph TD");
        mermaid.AppendLine($"    %% {data.npcName} 路径关系图");
        mermaid.AppendLine();
        
        // 定义节点样式
        mermaid.AppendLine("    %% 节点定义");
        foreach (var path in data.paths)
        {
            string nodeId = SanitizeNodeId(path.pathID);
            string nodeName = path.pathName.Replace("\"", "'");
            
            if (path.nextPaths == null || path.nextPaths.Count == 0)
            {
                // 终点路径用圆角矩形
                mermaid.AppendLine($"    {nodeId}[\"{nodeName}<br/>终点\"]");
            }
            else if (path.nextPaths.Count > 1)
            {
                // 分支路径用菱形
                mermaid.AppendLine($"    {nodeId}{{\"{nodeName}<br/>分支\"}};");
            }
            else
            {
                // 普通路径用矩形
                mermaid.AppendLine($"    {nodeId}[\"{nodeName}\"];");
            }
        }
        
        mermaid.AppendLine();
        mermaid.AppendLine("    %% 路径连接");
        
        // 定义连接关系
        foreach (var path in data.paths)
        {
            string fromNodeId = SanitizeNodeId(path.pathID);
            
            if (path.nextPaths != null && path.nextPaths.Count > 0)
            {
                foreach (var branch in path.nextPaths)
                {
                    if (!string.IsNullOrEmpty(branch.nextPathID))
                    {
                        string toNodeId = SanitizeNodeId(branch.nextPathID);
                        string label = $"分数:{branch.minScore}-{branch.maxScore}";
                        
                        // 检查目标路径是否存在
                        var targetPath = data.paths.FirstOrDefault(p => p.pathID == branch.nextPathID);
                        if (targetPath != null)
                        {
                            mermaid.AppendLine($"    {fromNodeId} -->|\"{label}\"| {toNodeId};");
                        }
                        else
                        {
                            // 不存在的路径用红色虚线
                            string errorNodeId = $"ERROR_{toNodeId}";
                            mermaid.AppendLine($"    {errorNodeId}[\"❌ 路径不存在<br/>{branch.nextPathID}\"];");
                            mermaid.AppendLine($"    {fromNodeId} -.->|\"{label}\"| {errorNodeId};");
                            mermaid.AppendLine($"    classDef errorNode fill:#ffcccc,stroke:#ff0000;");
                            mermaid.AppendLine($"    class {errorNodeId} errorNode;");
                        }
                    }
                }
            }
        }
        
        mermaid.AppendLine();
        mermaid.AppendLine("    %% 样式定义");
        mermaid.AppendLine("    classDef normalPath fill:#e1f5fe,stroke:#01579b;");
        mermaid.AppendLine("    classDef branchPath fill:#fff3e0,stroke:#e65100;");
        mermaid.AppendLine("    classDef endPath fill:#e8f5e8,stroke:#2e7d32;");
        
        // 应用样式
        foreach (var path in data.paths)
        {
            string nodeId = SanitizeNodeId(path.pathID);
            
            if (path.nextPaths == null || path.nextPaths.Count == 0)
            {
                mermaid.AppendLine($"    class {nodeId} endPath;");
            }
            else if (path.nextPaths.Count > 1)
            {
                mermaid.AppendLine($"    class {nodeId} branchPath;");
            }
            else
            {
                mermaid.AppendLine($"    class {nodeId} normalPath;");
            }
        }
        
        return mermaid.ToString();
    }
    
    /// <summary>
    /// 清理节点ID，确保符合Mermaid语法
    /// </summary>
    private static string SanitizeNodeId(string id)
    {
        if (string.IsNullOrEmpty(id))
            return "EMPTY_ID";
            
        // 替换特殊字符为下划线
        return System.Text.RegularExpressions.Regex.Replace(id, @"[^a-zA-Z0-9_]", "_");
    }
    
    #endregion
    
    #region 导入功能（基础版）
    
    [MenuItem("工具/NPC数据/高级版/从路径分支表导入")]
    public static void MenuImportFromPathBranches()
    {
        var activeObject = Selection.activeObject;
        if (activeObject is NPCData npcData)
        {
            string path = EditorUtility.OpenFilePanel(
                "选择路径分支CSV文件",
                "Assets",
                "csv"
            );
            
            if (!string.IsNullOrEmpty(path))
            {
                ImportPathBranchesFromCSV(npcData, path);
            }
        }
        else
        {
            EditorUtility.DisplayDialog("错误", "请先选择一个NPCData资源", "确定");
        }
    }
    
    /// <summary>
    /// 从路径分支CSV文件导入数据
    /// </summary>
    private static void ImportPathBranchesFromCSV(NPCData data, string filePath)
    {
        try
        {
            string[] lines = File.ReadAllLines(filePath, System.Text.Encoding.UTF8);
            if (lines.Length <= 1)
            {
                EditorUtility.DisplayDialog("导入错误", "文件为空或只包含表头", "确定");
                return;
            }
            
            if (!EditorUtility.DisplayDialog("确认导入", 
                "导入将更新路径分支配置。是否继续？\n建议在导入前备份当前数据。", 
                "继续导入", "取消"))
            {
                return;
            }
            
            int updatedPaths = 0;
            int errors = 0;
            
            for (int i = 1; i < lines.Length; i++) // 跳过表头
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                string[] fields = ParseCSVLine(line);
                if (fields.Length < 9) continue; // 确保有足够的字段
                
                string currentPathID = fields[0];
                string branchIndexStr = fields[2];
                string nextPathID = fields[3];
                string minScoreStr = fields[5];
                string maxScoreStr = fields[6];
                string description = fields[7];
                
                // 跳过终点路径行
                if (branchIndexStr == "终点") continue;
                
                // 查找当前路径
                var currentPath = data.paths.FirstOrDefault(p => p.pathID == currentPathID);
                if (currentPath == null)
                {
                    Debug.LogWarning($"未找到路径ID: {currentPathID}");
                    errors++;
                    continue;
                }
                
                // 解析分支索引
                if (!int.TryParse(branchIndexStr, out int branchIndex))
                {
                    Debug.LogWarning($"无效的分支索引: {branchIndexStr}");
                    errors++;
                    continue;
                }
                
                // 确保nextPaths列表存在且足够大
                if (currentPath.nextPaths == null)
                    currentPath.nextPaths = new List<PathBranch>();
                
                while (currentPath.nextPaths.Count <= branchIndex)
                {
                    currentPath.nextPaths.Add(new PathBranch());
                }
                
                // 更新分支信息
                var branch = currentPath.nextPaths[branchIndex];
                branch.nextPathID = nextPathID;
                branch.description = description;
                
                if (float.TryParse(minScoreStr, out float minScore))
                    branch.minScore = minScore;
                if (float.TryParse(maxScoreStr, out float maxScore))
                    branch.maxScore = maxScore;
                
                updatedPaths++;
            }
            
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            
            EditorUtility.DisplayDialog("导入完成", 
                $"成功更新 {updatedPaths} 个路径分支配置\n错误: {errors} 个", "确定");
            
            Debug.Log($"[AdvancedImport] 路径分支导入完成: 更新{updatedPaths}个，错误{errors}个");
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("导入错误", $"导入失败: {e.Message}", "确定");
            Debug.LogException(e);
        }
    }
    
    /// <summary>
    /// 解析CSV行
    /// </summary>
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
                    // 转义的引号
                    currentField.Append('"');
                    i++; // 跳过下一个引号
                }
                else
                {
                    // 切换引号状态
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                // 字段分隔符
                fields.Add(currentField.ToString());
                currentField.Clear();
            }
            else
            {
                currentField.Append(c);
            }
        }
        
        // 添加最后一个字段
        fields.Add(currentField.ToString());
        
        return fields.ToArray();
    }
    
    #endregion
    
    #region 快速修复工具
    
    [MenuItem("工具/NPC数据/高级版/快速修复空路径ID")]
    public static void MenuFixEmptyPathIDs()
    {
        var activeObject = Selection.activeObject;
        if (activeObject is NPCData npcData)
        {
            FixEmptyPathIDs(npcData);
        }
        else
        {
            EditorUtility.DisplayDialog("错误", "请先选择一个NPCData资源", "确定");
        }
    }
    
    /// <summary>
    /// 修复空的路径ID
    /// </summary>
    private static void FixEmptyPathIDs(NPCData data)
    {
        int fixedCount = 0;
        
        foreach (var path in data.paths)
        {
            if (path.nextPaths != null)
            {
                for (int i = path.nextPaths.Count - 1; i >= 0; i--)
                {
                    var branch = path.nextPaths[i];
                    if (string.IsNullOrEmpty(branch.nextPathID))
                    {
                        if (EditorUtility.DisplayDialog("发现空路径ID", 
                            $"路径 '{path.pathName}' 的分支 {i} 有空的路径ID。\n是否删除此分支？", 
                            "删除", "保留"))
                        {
                            path.nextPaths.RemoveAt(i);
                            fixedCount++;
                        }
                    }
                }
            }
        }
        
        if (fixedCount > 0)
        {
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("修复完成", $"已修复 {fixedCount} 个空路径ID", "确定");
        }
        else
        {
            EditorUtility.DisplayDialog("检查完成", "未发现空路径ID", "确定");
        }
    }
    
    #endregion
    
    #region 工具方法
    
    /// <summary>
    /// CSV字段转义
    /// </summary>
    private static string EscapeCSVField(string field)
    {
        if (string.IsNullOrEmpty(field))
            return "";
        
        // 如果包含逗号、引号或换行符，需要转义
        if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
        {
            // 将引号替换为两个引号，然后用引号包围整个字段
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }
        
        return field;
    }
    
    #endregion
}

#endif 