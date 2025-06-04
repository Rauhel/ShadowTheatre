#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class NPCActionEditor
{
    private NPCDataEditor mainEditor;
    private List<string> cachedAnimations = new List<string>();
    private string lastNpcName = "";
    
    public NPCActionEditor(NPCDataEditor editor)
    {
        mainEditor = editor;
        // 初始化动画列表
        RefreshAnimationList();
    }
    
    // 刷新动画列表方法
    private void RefreshAnimationList()
    {
        cachedAnimations.Clear();
        
        // 添加空选项
        cachedAnimations.Add("(无动画)");
        
        // 获取当前NPC名称
        string npcName = "";
        if (mainEditor != null && mainEditor.Data != null)
        {
            npcName = mainEditor.Data.npcName;
        }
        
        // 如果NPC名称为空，不执行筛选
        if (string.IsNullOrEmpty(npcName))
        {
            return;
        }
        
        // 记录当前NPC名称
        lastNpcName = npcName;
        
        // 查找项目中所有动画剪辑
        string[] animClipGuids = AssetDatabase.FindAssets("t:AnimationClip");
        
        foreach (string guid in animClipGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            
            if (clip != null)
            {
                // 只添加与NPC名称匹配的动画
                if (clip.name.Contains(npcName) || path.Contains(npcName))
                {
                    if (!cachedAnimations.Contains(clip.name))
                    {
                        cachedAnimations.Add(clip.name);
                    }
                }
            }
        }
        
        // 同时查找所有Animator控制器中的动画
        string[] animatorGuids = AssetDatabase.FindAssets("t:AnimatorController");
        foreach (string guid in animatorGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            
            // 检查路径是否与NPC名称相关
            if (path.Contains(npcName))
            {
                RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
                if (controller != null)
                {
                    foreach (var clip in controller.animationClips)
                    {
                        if (!cachedAnimations.Contains(clip.name))
                        {
                            cachedAnimations.Add(clip.name);
                        }
                    }
                }
            }
        }
        
        // 如果没有找到任何动画，添加一些常见的预设动画名称
        if (cachedAnimations.Count <= 1)
        {
            cachedAnimations.Add("Idle");
            cachedAnimations.Add("Walk");
            cachedAnimations.Add("Run");
            cachedAnimations.Add("Talk");
        }
        
        // 按字母顺序排序（保持"(无动画)"在第一位）
        string noneOption = cachedAnimations[0];
        cachedAnimations.RemoveAt(0);
        cachedAnimations.Sort();
        cachedAnimations.Insert(0, noneOption);
    }
    
    // ===== 路径动作编辑器 =====
    public void DrawPathActions(PathConfig config)
    {
        // 检查NPC名称是否变更，如果变更则刷新动画列表
        if (mainEditor != null && mainEditor.Data != null && 
            mainEditor.Data.npcName != lastNpcName)
        {
            RefreshAnimationList();
        }
        
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        // 标题和表头
        EditorGUILayout.LabelField("路径动作", EditorStyles.boldLabel);
        
        // 确保列表已初始化
        if(config.pathActions == null)
        {
            config.pathActions = new List<ActionData>();
        }
        
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUILayout.LabelField("路径点", GUILayout.Width(80));
        EditorGUILayout.LabelField("对话内容", GUILayout.Width(200));
        EditorGUILayout.LabelField("动画", GUILayout.Width(100));
        EditorGUILayout.LabelField("操作", GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();
        
        // 绘制每个动作
        for(int i = 0; i < config.pathActions.Count; i++)
        {
            ActionData action = config.pathActions[i];
            
            // 第一行：基本信息
            EditorGUILayout.BeginHorizontal();
            
            // 路径点选择器
            MultiPointPathCreator pathCreator = PathRegistry.GetPathCreatorByID(config.pathID);
            if(pathCreator != null)
            {
                // 修改这里：使用 GetPathPointCount
                int pointCount = pathCreator.GetPathPointCount();
                string[] options = new string[pointCount];
                for(int j = 0; j < pointCount; j++)
                {
                    options[j] = $"点 {j}";
                }
                action.pathPointIndex = EditorGUILayout.Popup(action.pathPointIndex, options, GUILayout.Width(80));
            }
            else
            {
                action.pathPointIndex = EditorGUILayout.IntField(action.pathPointIndex, GUILayout.Width(80));
            }
            
            // 对话内容
            action.dialogueText = EditorGUILayout.TextField(action.dialogueText, GUILayout.Width(200));
            
            // 动画下拉选择器
            int currentAnimIndex = 0;
            if (!string.IsNullOrEmpty(action.animationName))
            {
                currentAnimIndex = cachedAnimations.IndexOf(action.animationName);
                if (currentAnimIndex < 0) currentAnimIndex = 0;
            }

            int newAnimIndex = EditorGUILayout.Popup(currentAnimIndex, cachedAnimations.ToArray(), GUILayout.Width(100));
            action.animationName = (newAnimIndex > 0) ? cachedAnimations[newAnimIndex] : "";
            
            // 删除按钮
            if(GUILayout.Button("删除", GUILayout.Width(60)))
            {
                config.pathActions.RemoveAt(i);
                i--;
                EditorUtility.SetDirty(mainEditor.Data);
                continue;
            }
            
            EditorGUILayout.EndHorizontal();
            
            // 第二行：扩展设置
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.Space(10);
            
            // 声音设置
            EditorGUILayout.BeginHorizontal(GUILayout.Width(200));
            EditorGUILayout.LabelField("声音:", GUILayout.Width(40));
            action.voiceClip = (AudioClip)EditorGUILayout.ObjectField(action.voiceClip, typeof(AudioClip), false);
            EditorGUILayout.EndHorizontal();
            
            // 一次性音效设置(新增)
            EditorGUILayout.BeginHorizontal(GUILayout.Width(200));
            EditorGUILayout.LabelField("音效:", GUILayout.Width(40));
            action.oneShotSFX = (AudioClip)EditorGUILayout.ObjectField(action.oneShotSFX, typeof(AudioClip), false);
            EditorGUILayout.EndHorizontal();
            
            // 动画循环次数设置(修改)
            EditorGUILayout.BeginHorizontal(GUILayout.Width(100));
            EditorGUILayout.LabelField("动画循环:", GUILayout.Width(60));
            action.animationLoopCount = EditorGUILayout.IntField(action.animationLoopCount, GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();
            
            // 时间设置 - 对话相关设置保持不变
            EditorGUILayout.BeginHorizontal(GUILayout.Width(260));
            EditorGUILayout.LabelField("对话显示:", GUILayout.Width(60));
            action.displayDuration = EditorGUILayout.FloatField(action.displayDuration, GUILayout.Width(80));
            EditorGUILayout.LabelField("延迟:", GUILayout.Width(40));
            action.delay = EditorGUILayout.FloatField(action.delay, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndHorizontal();
            
            // 第三行：停止时间(重命名)
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("停止:", GUILayout.Width(40));
            action.stopTime = EditorGUILayout.FloatField(action.stopTime, GUILayout.Width(80));
            EditorGUILayout.LabelField("(已弃用，请使用路径点停留设置)", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            
            // 分隔线
            EditorGUILayout.Space(5);
            NPCEditorUtility.DrawSeparator();
            EditorGUILayout.Space(2);
        }
        
        // 添加按钮
        if(GUILayout.Button("添加路径动作"))
        {
            // 创建新的路径动作
            ActionData newAction = new ActionData();
            newAction.pathPointIndex = 0;
            newAction.delay = 0.5f;
            newAction.displayDuration = 2.0f; // 默认对话显示时间
            newAction.stopTime = 0f;  // 默认不停留
            
            // 直接添加到路径动作列表
            config.pathActions.Add(newAction);
            EditorUtility.SetDirty(mainEditor.Data);
        }
        
        // 在编辑器底部添加刷新动画列表按钮
        EditorGUILayout.Space(5);
        if (GUILayout.Button("刷新动画列表"))
        {
            RefreshAnimationList();
        }
        
        EditorGUILayout.EndVertical();
    }
    
    // ===== 事件动作编辑器 =====
    public void DrawEventActions(GestureResponse response)
    {
        // 检查NPC名称是否变更，如果变更则刷新动画列表
        if (mainEditor != null && mainEditor.Data != null && 
            mainEditor.Data.npcName != lastNpcName)
        {
            RefreshAnimationList();
        }
        
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        // 标题和表头
        EditorGUILayout.LabelField("事件动作", EditorStyles.boldLabel);
        
        // 确保列表已初始化
        if(response.actions == null)
        {
            response.actions = new List<ActionData>();
        }
        
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUILayout.LabelField("路径点", GUILayout.Width(80));
        EditorGUILayout.LabelField("对话内容", GUILayout.Width(200));
        EditorGUILayout.LabelField("动画", GUILayout.Width(100));
        EditorGUILayout.LabelField("操作", GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();
        
        // 绘制每个动作
        for(int i = 0; i < response.actions.Count; i++)
        {
            ActionData action = response.actions[i];
            
            // 第一行：基本信息
            EditorGUILayout.BeginHorizontal();
            
            // 找出当前事件所属的路径
            PathConfig currentPath = null;
            PathEvent currentEvent = null;
            
            foreach(var path in mainEditor.Data.paths)
            {
                foreach(var evt in path.events)
                {
                    // 检查默认响应
                    if(evt.defaultResponse == response)
                    {
                        currentEvent = evt;
                        currentPath = path;
                        break;
                    }
                    
                    // 检查手势响应
                    foreach(var gestureResponse in evt.gestureResponses)
                    {
                        if(gestureResponse == response)
                        {
                            currentEvent = evt;
                            currentPath = path;
                            break;
                        }
                    }
                    
                    if(currentEvent != null) break;
                }
                if(currentPath != null) break;
            }
            
            // 显示路径点选择器
            if(currentPath != null)
            {
                MultiPointPathCreator pathCreator = PathRegistry.GetPathCreatorByID(currentPath.pathID);
                if(pathCreator != null)
                {
                    int pointCount = pathCreator.GetPathPointCount();
                    string[] options = new string[pointCount];
                    for(int j = 0; j < pointCount; j++)
                    {
                        options[j] = $"点 {j}";
                    }
                    action.pathPointIndex = EditorGUILayout.Popup(action.pathPointIndex, options, GUILayout.Width(80));
                }
                else
                {
                    action.pathPointIndex = EditorGUILayout.IntField(action.pathPointIndex, GUILayout.Width(80));
                }
            }
            else
            {
                action.pathPointIndex = EditorGUILayout.IntField(action.pathPointIndex, GUILayout.Width(80));
            }
            
            // 对话内容
            action.dialogueText = EditorGUILayout.TextField(action.dialogueText, GUILayout.Width(200));
            
            // 动画下拉选择器
            int currentAnimIndex = 0;
            if (!string.IsNullOrEmpty(action.animationName))
            {
                currentAnimIndex = cachedAnimations.IndexOf(action.animationName);
                if (currentAnimIndex < 0) currentAnimIndex = 0;
            }

            int newAnimIndex = EditorGUILayout.Popup(currentAnimIndex, cachedAnimations.ToArray(), GUILayout.Width(100));
            action.animationName = (newAnimIndex > 0) ? cachedAnimations[newAnimIndex] : "";
            
            // 删除按钮
            if(GUILayout.Button("删除", GUILayout.Width(60)))
            {
                response.actions.RemoveAt(i);
                i--;
                EditorUtility.SetDirty(mainEditor.Data);
                continue;
            }
            
            EditorGUILayout.EndHorizontal();
            
            // 第二行：扩展设置
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.Space(10);
            
            // 声音设置
            EditorGUILayout.BeginHorizontal(GUILayout.Width(200));
            EditorGUILayout.LabelField("声音:", GUILayout.Width(40));
            action.voiceClip = (AudioClip)EditorGUILayout.ObjectField(action.voiceClip, typeof(AudioClip), false);
            EditorGUILayout.EndHorizontal();
            
            // 一次性音效设置(新增)
            EditorGUILayout.BeginHorizontal(GUILayout.Width(200));
            EditorGUILayout.LabelField("音效:", GUILayout.Width(40));
            action.oneShotSFX = (AudioClip)EditorGUILayout.ObjectField(action.oneShotSFX, typeof(AudioClip), false);
            EditorGUILayout.EndHorizontal();
            
            // 动画循环次数设置(修改)
            EditorGUILayout.BeginHorizontal(GUILayout.Width(100));
            EditorGUILayout.LabelField("动画循环:", GUILayout.Width(60));
            action.animationLoopCount = EditorGUILayout.IntField(action.animationLoopCount, GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();
            
            // 时间设置 - 修改描述以明确这是对话时间而非动画时间
            EditorGUILayout.BeginHorizontal(GUILayout.Width(360));
            EditorGUILayout.LabelField("对话显示:", GUILayout.Width(60));
            action.displayDuration = EditorGUILayout.FloatField(action.displayDuration, GUILayout.Width(80));
            EditorGUILayout.LabelField("延迟:", GUILayout.Width(40));
            action.delay = EditorGUILayout.FloatField(action.delay, GUILayout.Width(80));
            EditorGUILayout.LabelField("停留:", GUILayout.Width(40));
            action.stopTime = EditorGUILayout.FloatField(action.stopTime, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndHorizontal();
            
            // 分隔线
            EditorGUILayout.Space(5);
            NPCEditorUtility.DrawSeparator();
            EditorGUILayout.Space(2);
        }
        
        // 添加按钮
        if(GUILayout.Button("添加事件动作"))
        {
            // 创建新的事件动作
            ActionData newAction = new ActionData();
            newAction.pathPointIndex = 0;
            newAction.delay = 0.5f;
            newAction.displayDuration = 2.0f; // 默认对话显示时间
            newAction.stopTime = 0f;  // 默认不停留
            
            // 添加到事件动作列表
            response.actions.Add(newAction);
            EditorUtility.SetDirty(mainEditor.Data);
        }
        
        // 在编辑器底部添加刷新动画列表按钮
        EditorGUILayout.Space(5);
        if (GUILayout.Button("刷新动画列表"))
        {
            RefreshAnimationList();
        }
        
        EditorGUILayout.EndVertical();
    }
    
    // 提供相同的接口名称，保持原有调用不变
    public void DrawNonEventActions(GestureResponse response)
    {
        DrawEventActions(response);
    }
    
    // ===== 合并自NPCDialogueEditor - 详细的单个动作编辑器 =====
    public void DrawPathAction(PathConfig config, ActionData action, int index)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"动作 {index+1}", EditorStyles.boldLabel);
        
        if (GUILayout.Button("删除", GUILayout.Width(60)))
        {
            if (EditorUtility.DisplayDialog("确认删除", "确定要删除此动作?", "删除", "取消"))
            {
                config.pathActions.RemoveAt(index);
                EditorUtility.SetDirty(mainEditor.Data);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
        }
        EditorGUILayout.EndHorizontal();
        
        // 路径点索引
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("路径点索引");
        
        // 查找路径创建器获取点数量
        MultiPointPathCreator dialogPathCreator = PathRegistry.GetPathCreatorByID(config.pathID);
        if (dialogPathCreator != null)
        {
            int pointCount = dialogPathCreator.GetPathPointCount();
            string[] pointOptions = new string[pointCount];
            
            for (int i = 0; i < pointCount; i++)
            {
                float relativePos = (float)i / (pointCount - 1);
                pointOptions[i] = $"点 {i} ({relativePos:P0})";
            }
            
            // 确保索引在范围内
            action.pathPointIndex = Mathf.Clamp(action.pathPointIndex, 0, pointCount - 1);
            
            // 下拉菜单选择点
            int newPointIndex = EditorGUILayout.Popup(action.pathPointIndex, pointOptions);
            if (newPointIndex != action.pathPointIndex)
            {
                action.pathPointIndex = newPointIndex;
                EditorUtility.SetDirty(mainEditor.Data);
            }
            
            // 定位按钮
            if (GUILayout.Button("定位", GUILayout.Width(50)))
            {
                // 获取位置
                Vector3 position = dialogPathCreator.GetPathPointPosition(action.pathPointIndex);
                
                // 创建临时定位标记
                GameObject tempMarker = new GameObject("TempPathPointMarker");
                tempMarker.transform.position = position;
                Selection.activeGameObject = tempMarker;
                SceneView.FrameLastActiveSceneView();
                
                // 延迟销毁临时标记
                EditorApplication.delayCall += () => {
                    if(tempMarker != null)
                        GameObject.DestroyImmediate(tempMarker);
                };
            }
        }
        else
        {
            action.pathPointIndex = EditorGUILayout.IntField(action.pathPointIndex);
        }
        EditorGUILayout.EndHorizontal();
        
        // 对话文本
        EditorGUILayout.LabelField("对话内容");
        action.dialogueText = EditorGUILayout.TextArea(action.dialogueText, GUILayout.Height(60));
        
        // 显示时间
        action.displayDuration = EditorGUILayout.FloatField("显示时间(秒)", action.displayDuration);
        
        // 停止时间和延迟时间设置
        action.stopTime = EditorGUILayout.FloatField("停止时间(秒)", action.stopTime);
        action.delay = EditorGUILayout.FloatField("延迟时间(秒)", action.delay);
        
        // 语音片段 (可选)
        action.voiceClip = (AudioClip)EditorGUILayout.ObjectField("语音片段", action.voiceClip, typeof(AudioClip), false);
        
        // 一次性音效 (新增)
        action.oneShotSFX = (AudioClip)EditorGUILayout.ObjectField("一次性音效", action.oneShotSFX, typeof(AudioClip), false);
        
        // 动画名称
        action.animationName = EditorGUILayout.TextField("动画名称", action.animationName);
        
        // 动画循环次数 (新增)
        action.animationLoopCount = EditorGUILayout.IntField("动画循环次数 (0=不播放)", action.animationLoopCount);
        
        EditorGUILayout.EndVertical();
    }
    
    // ===== 新增：时间控制编辑器 =====
    public void DrawPathTimeSettings(PathConfig config)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        // 标题
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("⏰ 时间控制设置", EditorStyles.boldLabel);
        
        // 帮助按钮
        if (GUILayout.Button("?", GUILayout.Width(20)))
        {
            EditorUtility.DisplayDialog("时间控制说明", 
                "• 路径起始故事时间：NPC应该何时到达此路径起始点\n" +
                "• 时间控制点：路径上的关键时间节点，用于控制NPC移动速度\n" +
                "• 时间基于当前幕的故事时间计算\n" +
                "• 系统会自动计算NPC在各时间点间的移动速度", "明白了");
        }
        EditorGUILayout.EndHorizontal();
        
        // 路径起始时间
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        float newPathStartTime = EditorGUILayout.FloatField("路径起始故事时间(秒)", config.pathStartStoryTime);
        if (EditorGUI.EndChangeCheck())
        {
            config.pathStartStoryTime = newPathStartTime;
            EditorUtility.SetDirty(mainEditor.Data);
        }
        EditorGUILayout.LabelField(FormatTimeDisplay(config.pathStartStoryTime), GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        
        // 时间控制点列表
        EditorGUILayout.LabelField("时间控制点:", EditorStyles.boldLabel);
        
        if (config.timePoints == null)
            config.timePoints = new List<PathTimePoint>();
        
        // 绘制现有时间点
        for (int i = 0; i < config.timePoints.Count; i++)
        {
            DrawTimePointField(config, config.timePoints[i], i);
        }
        
        // 添加时间点按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ 添加时间控制点"))
        {
            config.timePoints.Add(new PathTimePoint 
            { 
                pathPointIndex = 0, 
                requiredStoryTime = 0f,
                description = ""
            });
            EditorUtility.SetDirty(mainEditor.Data);
        }
        
        // 排序按钮
        if (config.timePoints.Count > 1 && GUILayout.Button("按时间排序", GUILayout.Width(100)))
        {
            config.timePoints.Sort((a, b) => a.requiredStoryTime.CompareTo(b.requiredStoryTime));
            EditorUtility.SetDirty(mainEditor.Data);
        }
        
        // 验证按钮
        if (config.timePoints.Count > 0 && GUILayout.Button("验证时间配置", GUILayout.Width(100)))
        {
            ValidateTimeConfiguration(config);
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawTimePointField(PathConfig config, PathTimePoint timePoint, int index)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        // 获取路径创建器（只获取一次）
        MultiPointPathCreator pathCreator = PathRegistry.GetPathCreatorByID(config.pathID);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"时间点 {index + 1}", EditorStyles.boldLabel, GUILayout.Width(80));
        
        // 显示当前选择的路径点信息
        string currentPointInfo = $"当前: 点{timePoint.pathPointIndex}";
        if (pathCreator != null && timePoint.pathPointIndex < pathCreator.GetPathPointCount())
        {
            float relativePos = pathCreator.GetPathPointCount() > 1 ? 
                               (float)timePoint.pathPointIndex / (pathCreator.GetPathPointCount() - 1) : 0f;
            currentPointInfo += $" ({relativePos:P0})";
        }
        EditorGUILayout.LabelField(currentPointInfo, EditorStyles.miniLabel, GUILayout.Width(100));
        
        // 删除按钮
        if (GUILayout.Button("删除", GUILayout.Width(50)))
        {
            if (EditorUtility.DisplayDialog("确认删除", "确定要删除此时间控制点?", "删除", "取消"))
            {
                config.timePoints.RemoveAt(index);
                EditorUtility.SetDirty(mainEditor.Data);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
        }
        EditorGUILayout.EndHorizontal();
        
        // 路径点选择 - 与路径动作编辑保持一致的写法
        if (pathCreator != null)
        {
            int pointCount = pathCreator.GetPathPointCount();
            string[] pointOptions = new string[pointCount];
            
            for (int i = 0; i < pointCount; i++)
            {
                // 增强显示信息，显示相对位置百分比
                float relativePos = pointCount > 1 ? (float)i / (pointCount - 1) : 0f;
                pointOptions[i] = $"点 {i} ({relativePos:P0})";
            }
            
            EditorGUILayout.BeginHorizontal();
            // 直接赋值，与路径动作编辑保持一致
            timePoint.pathPointIndex = EditorGUILayout.Popup("路径点", 
                Mathf.Clamp(timePoint.pathPointIndex, 0, pointCount - 1), 
                pointOptions);
            
            // 添加定位按钮
            if (GUILayout.Button("定位", GUILayout.Width(50)))
            {
                // 获取位置
                Vector3 position = pathCreator.GetPathPointPosition(timePoint.pathPointIndex);
                
                // 创建临时定位标记
                GameObject tempMarker = new GameObject("TempTimePointMarker");
                tempMarker.transform.position = position;
                Selection.activeGameObject = tempMarker;
                SceneView.FrameLastActiveSceneView();
                
                // 延迟销毁临时标记
                EditorApplication.delayCall += () => {
                    if(tempMarker != null)
                        GameObject.DestroyImmediate(tempMarker);
                };
            }
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            // 直接赋值，与路径动作编辑保持一致
            timePoint.pathPointIndex = EditorGUILayout.IntField("路径点", timePoint.pathPointIndex);
            EditorGUILayout.LabelField("(路径未找到)", GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();
        }
        
        // 时间设置 - 与路径动作编辑保持一致的写法
        EditorGUILayout.BeginHorizontal();
        timePoint.requiredStoryTime = EditorGUILayout.FloatField("相对时间(秒)", timePoint.requiredStoryTime);
        
        // 时间显示
        EditorGUILayout.LabelField(FormatTimeDisplay(timePoint.requiredStoryTime), GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();
        
        // 停留时间设置
        EditorGUILayout.BeginHorizontal();
        timePoint.stopTime = EditorGUILayout.FloatField("停留时间(秒)", timePoint.stopTime);
        
        // 时间显示
        EditorGUILayout.LabelField(FormatTimeDisplay(timePoint.stopTime), GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();
        
        // 添加时间预设按钮
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("快速设置:", GUILayout.Width(60));
        if (GUILayout.Button("10秒", GUILayout.Width(40)))
        {
            timePoint.requiredStoryTime = 10f;
            EditorUtility.SetDirty(mainEditor.Data);
        }
        if (GUILayout.Button("30秒", GUILayout.Width(40)))
        {
            timePoint.requiredStoryTime = 30f;
            EditorUtility.SetDirty(mainEditor.Data);
        }
        if (GUILayout.Button("1分", GUILayout.Width(35)))
        {
            timePoint.requiredStoryTime = 60f;
            EditorUtility.SetDirty(mainEditor.Data);
        }
        if (GUILayout.Button("2分", GUILayout.Width(35)))
        {
            timePoint.requiredStoryTime = 120f;
            EditorUtility.SetDirty(mainEditor.Data);
        }
        EditorGUILayout.EndHorizontal();
        
        // 描述 - 与路径动作编辑保持一致的写法
        timePoint.description = EditorGUILayout.TextField("描述", timePoint.description);
        
        // 计算绝对时间并显示
        float absoluteTime = config.pathStartStoryTime + timePoint.requiredStoryTime;
        EditorGUILayout.LabelField($"绝对故事时间: {FormatTimeDisplay(absoluteTime)}", EditorStyles.helpBox);
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
        
        // 确保数据被标记为脏数据
            EditorUtility.SetDirty(mainEditor.Data);
    }
    
    private string FormatTimeDisplay(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
        return $"{minutes:00}:{seconds:00}";
    }
    
    private void ValidateTimeConfiguration(PathConfig config)
    {
        List<string> warnings = new List<string>();
        List<string> errors = new List<string>();
        
        // 检查时间点顺序
        for (int i = 0; i < config.timePoints.Count - 1; i++)
        {
            if (config.timePoints[i].requiredStoryTime >= config.timePoints[i + 1].requiredStoryTime)
            {
                errors.Add($"时间点 {i + 1} 和 {i + 2} 的时间顺序不正确");
            }
        }
        
        // 检查路径点顺序
        for (int i = 0; i < config.timePoints.Count - 1; i++)
        {
            if (config.timePoints[i].pathPointIndex >= config.timePoints[i + 1].pathPointIndex)
            {
                warnings.Add($"时间点 {i + 1} 和 {i + 2} 的路径点顺序可能不正确");
            }
        }
        
        // 检查速度合理性
        MultiPointPathCreator pathCreator = PathRegistry.GetPathCreatorByID(config.pathID);
        if (pathCreator != null)
        {
            for (int i = 0; i < config.timePoints.Count - 1; i++)
            {
                var current = config.timePoints[i];
                var next = config.timePoints[i + 1];
                
                // 计算距离
                float distance = CalculateDistanceBetweenPoints(pathCreator, current.pathPointIndex, next.pathPointIndex);
                float timeInterval = next.requiredStoryTime - current.requiredStoryTime;
                
                if (timeInterval > 0)
                {
                    float requiredSpeed = distance / timeInterval;
                    
                    if (requiredSpeed > 60f) // 超过瞬移阈值
                    {
                        errors.Add($"时间点 {i + 1} 到 {i + 2} 需要速度 {requiredSpeed:F1}m/s，将触发瞬移");
                    }
                    else if (requiredSpeed > 10f) // 超过最大追赶速度
                    {
                        warnings.Add($"时间点 {i + 1} 到 {i + 2} 需要速度 {requiredSpeed:F1}m/s，较快");
                    }
                }
            }
        }
        
        // 显示验证结果
        string message = "时间配置验证完成\n\n";
        
        if (errors.Count > 0)
        {
            message += "❌ 错误:\n" + string.Join("\n", errors) + "\n\n";
        }
        
        if (warnings.Count > 0)
        {
            message += "⚠️ 警告:\n" + string.Join("\n", warnings) + "\n\n";
        }
        
        if (errors.Count == 0 && warnings.Count == 0)
        {
            message += "✅ 配置正确，没有发现问题";
        }
        
        EditorUtility.DisplayDialog("时间配置验证", message, "确定");
    }
    
    private float CalculateDistanceBetweenPoints(MultiPointPathCreator pathCreator, int fromIndex, int toIndex)
    {
        float totalDistance = 0f;
        
        for (int i = fromIndex; i < toIndex; i++)
        {
            Vector3 currentPos = pathCreator.GetPathPointPosition(i);
            Vector3 nextPos = pathCreator.GetPathPointPosition(i + 1);
            totalDistance += Vector3.Distance(currentPos, nextPos);
        }
        
        return totalDistance;
    }
    
    // ===== 新增：路径点停留设置编辑器 =====
    public void DrawPathPointStopSettings(PathConfig config)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        // 标题
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("⏸️ 路径点停留设置", EditorStyles.boldLabel);
        
        // 帮助按钮
        if (GUILayout.Button("?", GUILayout.Width(20)))
        {
            EditorUtility.DisplayDialog("停留设置说明", 
                "• 路径点停留设置：NPC到达指定路径点时的停留时间\n" +
                "• 与Action系统独立：停留时间由路径管理，Action专注表演内容\n" +
                "• 优先级：时间控制点停留时间 > 路径点停留设置\n" +
                "• 适用场景：需要NPC在某个位置等待一段时间的情况", "明白了");
        }
        EditorGUILayout.EndHorizontal();
        
        if (config.pathPointStopConfigs == null)
            config.pathPointStopConfigs = new List<PathPointStopConfig>();
        
        // 绘制现有停留配置
        for (int i = 0; i < config.pathPointStopConfigs.Count; i++)
        {
            DrawPathPointStopField(config, config.pathPointStopConfigs[i], i);
        }
        
        // 添加停留配置按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ 添加路径点停留"))
        {
            config.pathPointStopConfigs.Add(new PathPointStopConfig 
            { 
                pathPointIndex = 0, 
                stopTime = 3f,
                description = ""
            });
            EditorUtility.SetDirty(mainEditor.Data);
        }
        
        // 清理重复按钮
        if (config.pathPointStopConfigs.Count > 1 && GUILayout.Button("清理重复", GUILayout.Width(80)))
        {
            // 按路径点索引分组，保留每个索引的第一个配置
            var uniqueConfigs = config.pathPointStopConfigs
                .GroupBy(c => c.pathPointIndex)
                .Select(g => g.First())
                .OrderBy(c => c.pathPointIndex)
                .ToList();
            
            if (uniqueConfigs.Count != config.pathPointStopConfigs.Count)
            {
                config.pathPointStopConfigs = uniqueConfigs;
                EditorUtility.SetDirty(mainEditor.Data);
                Debug.Log($"已清理重复的路径点停留配置，剩余{uniqueConfigs.Count}个");
            }
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawPathPointStopField(PathConfig config, PathPointStopConfig stopConfig, int index)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        // 获取路径创建器（只获取一次）
        MultiPointPathCreator pathCreator = PathRegistry.GetPathCreatorByID(config.pathID);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"停留配置 {index + 1}", EditorStyles.boldLabel, GUILayout.Width(100));
        
        // 显示当前选择的路径点信息
        string currentPointInfo = $"点{stopConfig.pathPointIndex}";
        if (pathCreator != null && stopConfig.pathPointIndex < pathCreator.GetPathPointCount())
        {
            float relativePos = pathCreator.GetPathPointCount() > 1 ? 
                               (float)stopConfig.pathPointIndex / (pathCreator.GetPathPointCount() - 1) : 0f;
            currentPointInfo += $" ({relativePos:P0})";
        }
        EditorGUILayout.LabelField(currentPointInfo, EditorStyles.miniLabel, GUILayout.Width(100));
        
        // 删除按钮
        if (GUILayout.Button("删除", GUILayout.Width(50)))
        {
            if (EditorUtility.DisplayDialog("确认删除", "确定要删除此停留配置?", "删除", "取消"))
            {
                config.pathPointStopConfigs.RemoveAt(index);
                EditorUtility.SetDirty(mainEditor.Data);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
        }
        EditorGUILayout.EndHorizontal();
        
        // 路径点选择
        if (pathCreator != null)
        {
            int pointCount = pathCreator.GetPathPointCount();
            string[] pointOptions = new string[pointCount];
            
            for (int i = 0; i < pointCount; i++)
            {
                float relativePos = pointCount > 1 ? (float)i / (pointCount - 1) : 0f;
                pointOptions[i] = $"点 {i} ({relativePos:P0})";
            }
            
            stopConfig.pathPointIndex = EditorGUILayout.Popup("路径点", 
                Mathf.Clamp(stopConfig.pathPointIndex, 0, pointCount - 1), 
                pointOptions);
        }
        else
        {
            stopConfig.pathPointIndex = EditorGUILayout.IntField("路径点", stopConfig.pathPointIndex);
        }
        
        // 停留时间设置
        EditorGUILayout.BeginHorizontal();
        stopConfig.stopTime = EditorGUILayout.FloatField("停留时间(秒)", stopConfig.stopTime);
        
        // 时间显示
        EditorGUILayout.LabelField(FormatTimeDisplay(stopConfig.stopTime), GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();
        
        // 添加时间预设按钮
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("快速设置:", GUILayout.Width(60));
        if (GUILayout.Button("1秒", GUILayout.Width(35)))
        {
            stopConfig.stopTime = 1f;
            EditorUtility.SetDirty(mainEditor.Data);
        }
        if (GUILayout.Button("3秒", GUILayout.Width(35)))
        {
            stopConfig.stopTime = 3f;
            EditorUtility.SetDirty(mainEditor.Data);
        }
        if (GUILayout.Button("5秒", GUILayout.Width(35)))
        {
            stopConfig.stopTime = 5f;
            EditorUtility.SetDirty(mainEditor.Data);
        }
        if (GUILayout.Button("10秒", GUILayout.Width(40)))
        {
            stopConfig.stopTime = 10f;
            EditorUtility.SetDirty(mainEditor.Data);
        }
        EditorGUILayout.EndHorizontal();
        
        // 描述
        stopConfig.description = EditorGUILayout.TextField("描述", stopConfig.description);
        
        // 检查是否与时间控制点冲突
        if (config.timePoints != null)
        {
            var conflictTimePoint = config.timePoints.FirstOrDefault(tp => tp.pathPointIndex == stopConfig.pathPointIndex && tp.stopTime > 0);
            if (conflictTimePoint != null)
            {
                EditorGUILayout.HelpBox($"注意：此路径点在时间控制点中也设置了停留时间({conflictTimePoint.stopTime}s)，时间控制点优先级更高", MessageType.Warning);
            }
        }
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
        
        EditorUtility.SetDirty(mainEditor.Data);
    }
}
#endif