#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public static class NPCEditorUtility
{
    // 绘制分隔线
    public static void DrawSeparator()
    {
        EditorGUILayout.Space(5);
        Rect rect = EditorGUILayout.GetControlRect(false, 1);
        rect.height = 1;
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
        EditorGUILayout.Space(5);
    }
    
    // 绘制标题
    public static void DrawHeader(string title)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }
    
    // 绘制数组
    public static void DrawArray<T>(List<T> list, string title, System.Action<T, int> drawElement, System.Func<T> createNew)
    {
        DrawHeader(title);
        
        if (list.Count == 0)
        {
            EditorGUILayout.HelpBox($"没有{title}。点击下方按钮添加。", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < list.Count; i++)
            {
                drawElement(list[i], i);
            }
        }
        
        if (GUILayout.Button($"添加{title}"))
        {
            list.Add(createNew());
        }
    }
    
    // 获取点数据
    public static string[] GetPathPointOptions(MultiPointPathCreator pathCreator)
    {
        if (pathCreator == null || pathCreator.pathPointsParent == null)
            return new string[0];
            
        int pointCount = pathCreator.pathPointsParent.childCount;
        string[] options = new string[pointCount];
        
        for (int i = 0; i < pointCount; i++)
        {
            float relativePos = (float)i / (pointCount - 1);
            options[i] = $"点 {i} ({relativePos:P0})";
        }
        
        return options;
    }

    // 修改 NPCEditorUtility.cs 中的 AnimationDropdown 方法
    public static string AnimationDropdown(string currentAnimation, string npcName, string label, Object targetObject, float width = 200)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(40));
        
        string result = currentAnimation;
        // 使用固定宽度显示当前动画名称
        EditorGUILayout.LabelField(
            string.IsNullOrEmpty(result) ? "未选择" : result, 
            EditorStyles.textField, 
            GUILayout.Width(width - 85)
        );
        
        // 添加紧凑的按钮
        if (GUILayout.Button("选择", GUILayout.Width(40)))
        {
            // 显示动画选择菜单...（保持原来的实现逻辑）
            GenericMenu menu = new GenericMenu();
            
            // 获取所有动画资源
            string[] guids = AssetDatabase.FindAssets("t:AnimationClip");
            List<string> filteredAnims = new List<string>();
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                
                // 筛选包含NPC名称的动画
                if (clip != null && (string.IsNullOrEmpty(npcName) || clip.name.Contains(npcName)))
                {
                    filteredAnims.Add(clip.name);
                }
            }
            
            // 添加菜单项
            if (filteredAnims.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("没有找到相关动画"));
            }
            else
            {
                // 排序并分组显示
                filteredAnims.Sort();
                
                foreach (string anim in filteredAnims)
                {
                    string animName = anim;
                    menu.AddItem(new GUIContent(animName), animName == currentAnimation, () => {
                        result = animName;
                        EditorUtility.SetDirty(targetObject);
                    });
                }
            }
            
            menu.ShowAsContext();
        }
        
        // 添加清除按钮
        if (GUILayout.Button("清除", GUILayout.Width(40)))
        {
            result = "";
            EditorUtility.SetDirty(targetObject);
        }
        
        EditorGUILayout.EndHorizontal();
        
        return result;
    }
}
#endif