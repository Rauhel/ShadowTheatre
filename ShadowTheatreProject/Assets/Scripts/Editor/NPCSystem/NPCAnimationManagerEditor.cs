#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(NPCAnimationManager))]
public class NPCAnimationManagerEditor : Editor
{
    private List<string> availableAnimations = new List<string>();
    
    private void OnEnable()
    {
        NPCAnimationManager manager = (NPCAnimationManager)target;
        availableAnimations = manager.GetAvailableAnimations();
    }
    
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("可用动画列表", EditorStyles.boldLabel);
        
        if (availableAnimations.Count == 0)
        {
            EditorGUILayout.HelpBox("没有可用的动画。请确保Animator组件已设置正确的Controller。", MessageType.Info);
        }
        else
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foreach (var anim in availableAnimations)
            {
                EditorGUILayout.LabelField(anim);
            }
            EditorGUILayout.EndVertical();
        }
        
        if (GUILayout.Button("刷新动画列表"))
        {
            NPCAnimationManager manager = (NPCAnimationManager)target;
            availableAnimations = manager.GetAvailableAnimations();
        }
    }
}
#endif