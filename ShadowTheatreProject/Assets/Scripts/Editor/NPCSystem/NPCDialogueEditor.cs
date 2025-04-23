#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class NPCDialogueEditor
{
    private NPCDataEditor mainEditor;
    
    public NPCDialogueEditor(NPCDataEditor editor)
    {
        mainEditor = editor;
    }
    
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
        if (dialogPathCreator != null && dialogPathCreator.pathPointsParent != null)
        {
            int pointCount = dialogPathCreator.pathPointsParent.childCount;
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
                Transform point = dialogPathCreator.pathPointsParent.GetChild(action.pathPointIndex);
                Selection.activeGameObject = point.gameObject;
                SceneView.FrameLastActiveSceneView();
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
        
        // 覆盖设置
        action.overridePrevious = EditorGUILayout.Toggle("覆盖前一对话", action.overridePrevious);
        
        // 语音片段 (可选)
        action.voiceClip = (AudioClip)EditorGUILayout.ObjectField("语音片段", action.voiceClip, typeof(AudioClip), false);
        
        // 动画名称
        action.animationName = EditorGUILayout.TextField("动画名称", action.animationName);
        
        // 延迟
        action.delay = EditorGUILayout.FloatField("延迟(秒)", action.delay);
        
        EditorGUILayout.EndVertical();
    }
}
#endif