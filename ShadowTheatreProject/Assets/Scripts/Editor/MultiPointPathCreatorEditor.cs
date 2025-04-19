#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using System.Collections.Generic;

[CustomEditor(typeof(MultiPointPathCreator))]
public class MultiPointPathCreatorEditor : Editor
{
    private MultiPointPathCreator pathCreator;
    
    private void OnEnable()
    {
        pathCreator = (MultiPointPathCreator)target;
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        EditorGUILayout.PropertyField(serializedObject.FindProperty("controlPoints"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pointSpacing"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pathColor"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("closedPath"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pathPointsParent"));
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("控制点工具", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("添加控制点"))
        {
            Undo.RecordObject(pathCreator, "Add Control Point");
            pathCreator.AddControlPoint();
            EditorUtility.SetDirty(pathCreator);
        }
        
        if (GUILayout.Button("移除最后一点"))
        {
            Undo.RecordObject(pathCreator, "Remove Last Control Point");
            pathCreator.RemoveLastControlPoint();
            EditorUtility.SetDirty(pathCreator);
        }
        EditorGUILayout.EndHorizontal();
        
        if (GUILayout.Button("均匀分布控制点"))
        {
            EvenlyDistributePoints();
        }
        
        EditorGUILayout.Space(5);
        
        if (GUILayout.Button("生成路径点"))
        {
            Undo.RecordObject(pathCreator, "Generate Path Points");
            pathCreator.GeneratePathPoints();
            EditorUtility.SetDirty(pathCreator);
        }
        
        serializedObject.ApplyModifiedProperties();
        
        // 检查NavMesh状态
        if (!HasNavMesh())
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox("场景中没有烘焙NavMesh! 请先烘焙NavMesh才能生成正确的路径。", MessageType.Warning);
            
            if (GUILayout.Button("打开Navigation窗口"))
            {
                EditorApplication.ExecuteMenuItem("Window/AI/Navigation");
            }
        }
    }
    
    private void EvenlyDistributePoints()
    {
        if (pathCreator.controlPoints.Count < 3) return;
        
        Undo.RecordObject(pathCreator, "Evenly Distribute Points");
        
        // 收集有效点
        List<Transform> validPoints = new List<Transform>();
        foreach (var point in pathCreator.controlPoints)
        {
            if (point != null) validPoints.Add(point);
        }
        
        if (validPoints.Count < 3) return;
        
        // 保留第一点和最后一点
        Vector3 startPos = validPoints[0].position;
        Vector3 endPos = validPoints[validPoints.Count - 1].position;
        
        // 计算总路径长度（简单直线估计）
        float totalDistance = 0;
        for (int i = 0; i < validPoints.Count - 1; i++)
        {
            totalDistance += Vector3.Distance(validPoints[i].position, validPoints[i + 1].position);
        }
        
        // 均匀分布中间点
        for (int i = 1; i < validPoints.Count - 1; i++)
        {
            float t = i / (float)(validPoints.Count - 1);
            Vector3 newPos = Vector3.Lerp(startPos, endPos, t);
            
            Undo.RecordObject(validPoints[i], "Move Control Point");
            validPoints[i].position = newPos;
            EditorUtility.SetDirty(validPoints[i]);
        }
        
        SceneView.RepaintAll();
    }
    
    private bool HasNavMesh()
    {
        // 尝试检测场景中是否有NavMesh
        Vector3 testPos = SceneView.lastActiveSceneView != null ? 
            SceneView.lastActiveSceneView.pivot : Vector3.zero;
        return UnityEngine.AI.NavMesh.SamplePosition(testPos, out _, 1000f, UnityEngine.AI.NavMesh.AllAreas);
    }
    
    private void OnSceneGUI()
    {
        // 允许用户拖动控制点
        for (int i = 0; i < pathCreator.controlPoints.Count; i++)
        {
            if (pathCreator.controlPoints[i] != null)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.PositionHandle(pathCreator.controlPoints[i].position, Quaternion.identity);
                
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(pathCreator.controlPoints[i], "Move Control Point");
                    pathCreator.controlPoints[i].position = newPos;
                    EditorUtility.SetDirty(pathCreator.controlPoints[i]);
                    SceneView.RepaintAll();
                }
                
                // 显示序号
                Handles.Label(pathCreator.controlPoints[i].position + Vector3.up * 0.5f, $"点 {i+1}");
            }
        }
    }
}
#endif