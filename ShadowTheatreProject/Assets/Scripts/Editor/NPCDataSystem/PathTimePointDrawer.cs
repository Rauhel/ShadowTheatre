#if UNITY_EDITOR && FALSE  // 临时禁用
using UnityEngine;
using UnityEditor;

/// <summary>
/// PathTimePoint的自定义属性绘制器
/// 提供更好的Inspector显示效果
/// 临时禁用 - 与NPCActionEditor冲突
/// </summary>
[CustomPropertyDrawer(typeof(PathTimePoint))]
public class PathTimePointDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        
        // 获取属性
        SerializedProperty pathPointIndex = property.FindPropertyRelative("pathPointIndex");
        SerializedProperty requiredStoryTime = property.FindPropertyRelative("requiredStoryTime");
        SerializedProperty description = property.FindPropertyRelative("description");
        
        // 计算布局
        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        
        Rect labelRect = new Rect(position.x, position.y, position.width, lineHeight);
        Rect pathPointRect = new Rect(position.x, position.y + lineHeight + spacing, position.width * 0.3f - 5, lineHeight);
        Rect timeRect = new Rect(position.x + position.width * 0.3f, position.y + lineHeight + spacing, position.width * 0.4f - 5, lineHeight);
        Rect timeDisplayRect = new Rect(position.x + position.width * 0.7f, position.y + lineHeight + spacing, position.width * 0.3f, lineHeight);
        Rect descriptionRect = new Rect(position.x, position.y + (lineHeight + spacing) * 2, position.width, lineHeight);
        
        // 绘制标签
        string displayLabel = $"时间点 (点{pathPointIndex.intValue} - {FormatTime(requiredStoryTime.floatValue)})";
        EditorGUI.LabelField(labelRect, displayLabel, EditorStyles.boldLabel);
        
        // 绘制路径点索引
        EditorGUI.PropertyField(pathPointRect, pathPointIndex, new GUIContent("路径点"));
        
        // 绘制时间
        EditorGUI.PropertyField(timeRect, requiredStoryTime, new GUIContent("时间(秒)"));
        
        // 显示格式化时间
        EditorGUI.LabelField(timeDisplayRect, FormatTime(requiredStoryTime.floatValue), EditorStyles.miniLabel);
        
        // 绘制描述
        EditorGUI.PropertyField(descriptionRect, description, new GUIContent("描述"));
        
        EditorGUI.EndProperty();
    }
    
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // 3行高度 + 间距
        return EditorGUIUtility.singleLineHeight * 3 + EditorGUIUtility.standardVerticalSpacing * 2;
    }
    
    private string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
        return $"{minutes:00}:{seconds:00}";
    }
}
#endif 