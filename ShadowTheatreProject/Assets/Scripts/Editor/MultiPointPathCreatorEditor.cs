#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
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
        
        // 绘制默认属性
        DrawDefaultInspector();
        
        EditorGUILayout.Space(10);
        
        // 控制点操作按钮
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
        
        EditorGUILayout.Space(10);
        
        // 如果设置了前一路径且有控制点，添加一个按钮来对齐到前一路径的末端
        if (pathCreator.previousPath != null && pathCreator.controlPoints.Count > 0)
        {
            if (GUILayout.Button("对齐到前一路径终点"))
            {
                AlignToEndOfPreviousPath();
            }
        }
        
        if (GUILayout.Button("更新起点和终点引用"))
        {
            Undo.RecordObject(pathCreator, "Update Path Endpoints");
            pathCreator.UpdateStartAndEndPoints();
            EditorUtility.SetDirty(pathCreator);
        }
        
        // 显示警告信息
        if (!HasNavMesh())
        {
            EditorGUILayout.HelpBox("场景中没有NavMesh！请先烘焙NavMesh，否则无法计算正确的路径。", MessageType.Warning);
            
            if (GUILayout.Button("打开Navigation窗口"))
            {
                EditorApplication.ExecuteMenuItem("Window/AI/Navigation");
            }
        }
        
        if (GUI.changed)
        {
            EditorUtility.SetDirty(pathCreator);
        }
    }
    
    private void EvenlyDistributePoints()
    {
        if (pathCreator.controlPoints.Count < 2)
            return;
            
        Undo.RecordObject(pathCreator, "Evenly Distribute Points");
        
        List<Transform> validPoints = new List<Transform>();
        foreach (var point in pathCreator.controlPoints)
        {
            if (point != null) validPoints.Add(point);
        }
        
        if (validPoints.Count < 2)
            return;
            
        // 计算总路径长度
        float totalLength = 0;
        for (int i = 0; i < validPoints.Count - 1; i++)
        {
            totalLength += Vector3.Distance(validPoints[i].position, validPoints[i + 1].position);
        }
        
        // 平均每段长度
        float segmentLength = totalLength / (validPoints.Count - 1);
        
        // 重新分布点
        Vector3 startPos = validPoints[0].position;
        for (int i = 1; i < validPoints.Count; i++)
        {
            Vector3 dir = (validPoints[i].position - startPos).normalized;
            validPoints[i].position = startPos + dir * segmentLength;
            startPos = validPoints[i].position;
        }
        
        pathCreator.UpdateStartAndEndPoints();
        EditorUtility.SetDirty(pathCreator);
    }
    
    private void AlignToEndOfPreviousPath()
    {
        if (pathCreator.previousPath == null)
            return;
            
        MultiPointPathCreator prevPathCreator = pathCreator.previousPath.GetComponent<MultiPointPathCreator>();
        if (prevPathCreator == null || prevPathCreator.controlPoints.Count == 0)
            return;
        
        // 获取前一路径的终点
        Transform prevEndPoint = prevPathCreator.endPoint;
        if (prevEndPoint == null && prevPathCreator.controlPoints.Count > 0)
        {
            prevEndPoint = prevPathCreator.controlPoints[prevPathCreator.controlPoints.Count - 1];
        }
        
        if (prevEndPoint != null && pathCreator.controlPoints.Count > 0)
        {
            Undo.RecordObject(pathCreator.controlPoints[0], "Align Control Point");
            pathCreator.controlPoints[0].position = prevEndPoint.position;
            EditorUtility.SetDirty(pathCreator.controlPoints[0]);
        }
        
        EditorUtility.SetDirty(pathCreator);
    }
    
    private bool HasNavMesh()
    {
        if (!Application.isPlaying)
        {
            // 检查场景中是否有NavMesh数据
            // 这只是一个简单的检查，可能不总是准确的
            UnityEngine.AI.NavMeshTriangulation triangulation = UnityEngine.AI.NavMesh.CalculateTriangulation();
            return triangulation.vertices.Length > 0;
        }
        return true;
    }
    
    private void OnSceneGUI()
    {
        if (pathCreator.controlPoints.Count < 1)
            return;
            
        // 绘制控制点手柄
        for (int i = 0; i < pathCreator.controlPoints.Count; i++)
        {
            if (pathCreator.controlPoints[i] == null)
                continue;
                
            // 可拖动的控制点
            EditorGUI.BeginChangeCheck();
            Vector3 newPos = Handles.PositionHandle(pathCreator.controlPoints[i].position, Quaternion.identity);
            
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(pathCreator.controlPoints[i], "Move Control Point");
                pathCreator.controlPoints[i].position = newPos;
                EditorUtility.SetDirty(pathCreator.controlPoints[i]);
            }
            
            // 标签
            Handles.Label(pathCreator.controlPoints[i].position + Vector3.up * 0.5f, $"Point {i}");
        }
        
        // 绘制与前一路径的连接
        if (pathCreator.previousPath != null && pathCreator.controlPoints.Count > 0)
        {
            MultiPointPathCreator prevPathCreator = pathCreator.previousPath.GetComponent<MultiPointPathCreator>();
            if (prevPathCreator != null && prevPathCreator.controlPoints.Count > 0)
            {
                Transform prevLastPoint = prevPathCreator.endPoint;
                if (prevLastPoint == null)
                {
                    prevLastPoint = prevPathCreator.controlPoints[prevPathCreator.controlPoints.Count - 1];
                }
                
                Transform currentFirstPoint = pathCreator.startPoint;
                if (currentFirstPoint == null)
                {
                    currentFirstPoint = pathCreator.controlPoints[0];
                }
                
                if (prevLastPoint != null && currentFirstPoint != null)
                {
                    Handles.color = Color.magenta;
                    Handles.DrawDottedLine(prevLastPoint.position, currentFirstPoint.position, 4f);
                    
                    // 添加标签
                    Handles.Label(
                        Vector3.Lerp(prevLastPoint.position, currentFirstPoint.position, 0.5f),
                        "路径连接",
                        EditorStyles.boldLabel
                    );
                }
            }
        }
        
        // 绘制和选择路径点
        if (pathCreator.pathPointsParent != null && pathCreator.pathPointsParent.childCount > 0)
        {
            Handles.color = Color.cyan;
            
            for (int i = 0; i < pathCreator.pathPointsParent.childCount; i++)
            {
                Transform point = pathCreator.pathPointsParent.GetChild(i);
                float size = HandleUtility.GetHandleSize(point.position) * 0.1f;
                
                // 绘制一个可以点击的小球
                if (Handles.Button(point.position, Quaternion.identity, size, size, Handles.SphereHandleCap))
                {
                    // 选中该路径点
                    Selection.activeObject = point.gameObject;
                }
                
                // 显示点的相对位置信息
                float relativePos = (float)i / (pathCreator.pathPointsParent.childCount - 1);
                Handles.Label(point.position + Vector3.up * 0.2f, $"{relativePos:P0}");
            }
        }
    }
}
#endif