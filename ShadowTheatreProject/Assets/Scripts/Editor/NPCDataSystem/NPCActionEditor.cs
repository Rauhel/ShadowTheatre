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
            if(pathCreator != null && pathCreator.pathPointsParent != null)
            {
                int pointCount = pathCreator.pathPointsParent.childCount;
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
            
            // 时间设置
            EditorGUILayout.BeginHorizontal(GUILayout.Width(360));
            EditorGUILayout.LabelField("显示:", GUILayout.Width(40));
            action.displayDuration = EditorGUILayout.FloatField(action.displayDuration, GUILayout.Width(80));
            EditorGUILayout.LabelField("延迟:", GUILayout.Width(40));
            action.delay = EditorGUILayout.FloatField(action.delay, GUILayout.Width(80));
            EditorGUILayout.LabelField("停留:", GUILayout.Width(40));
            action.waitTime = EditorGUILayout.FloatField(action.waitTime, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();
            
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
            newAction.displayDuration = 2.0f;
            newAction.waitTime = 0f;  // 默认不停留
            
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
                if(pathCreator != null && pathCreator.pathPointsParent != null)
                {
                    int pointCount = pathCreator.pathPointsParent.childCount;
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
            
            // 时间设置
            EditorGUILayout.BeginHorizontal(GUILayout.Width(360));
            EditorGUILayout.LabelField("显示:", GUILayout.Width(40));
            action.displayDuration = EditorGUILayout.FloatField(action.displayDuration, GUILayout.Width(80));
            EditorGUILayout.LabelField("延迟:", GUILayout.Width(40));
            action.delay = EditorGUILayout.FloatField(action.delay, GUILayout.Width(80));
            EditorGUILayout.LabelField("停留:", GUILayout.Width(40));
            action.waitTime = EditorGUILayout.FloatField(action.waitTime, GUILayout.Width(80));
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
            newAction.displayDuration = 2.0f;
            newAction.waitTime = 0f;  // 默认不停留
            
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
}
#endif