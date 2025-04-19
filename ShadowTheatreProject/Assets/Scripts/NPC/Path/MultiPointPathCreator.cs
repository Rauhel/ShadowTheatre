using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

// 只在编辑器中导入这个命名空间
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MultiPointPathCreator : MonoBehaviour
{
    [Header("路径设置")]
    [Tooltip("路径的所有控制点，按顺序排列")]
    public List<Transform> controlPoints = new List<Transform>();
    [Range(0.1f, 5f)]
    public float pointSpacing = 1f;
    public Color pathColor = Color.cyan;
    public bool closedPath = false; // 是否闭合路径（最后一点连回第一点）

    [Header("路径点生成")]
    public Transform pathPointsParent;

    // 临时路径存储
    private NavMeshPath tempPath;

    private void Awake()
    {
        tempPath = new NavMeshPath();
    }

    private void OnDrawGizmos()
    {
        if (controlPoints.Count < 2) return;

        // 初始化临时路径
        if (tempPath == null)
            tempPath = new NavMeshPath();

        // 确保所有点都有效
        List<Transform> validPoints = new List<Transform>();
        foreach (var point in controlPoints)
        {
            if (point != null) validPoints.Add(point);
        }

        if (validPoints.Count < 2) return;

        // 绘制控制点
        Gizmos.color = Color.white;
        foreach (var point in validPoints)
        {
            Gizmos.DrawSphere(point.position, 0.3f);
        }

        // 连接所有控制点
        for (int i = 0; i < validPoints.Count - 1; i++)
        {
            // 检查NavMesh可访问性
            if (!NavMesh.SamplePosition(validPoints[i].position, out _, 0.1f, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(validPoints[i + 1].position, out _, 0.1f, NavMesh.AllAreas))
            {
                // 无法访问的点用红色标记
                Gizmos.color = Color.red;
                Gizmos.DrawLine(validPoints[i].position, validPoints[i + 1].position);
                continue;
            }

            // 计算两点间的NavMesh路径
            NavMesh.CalculatePath(
                validPoints[i].position,
                validPoints[i + 1].position,
                NavMesh.AllAreas,
                tempPath
            );

            // 绘制NavMesh路径
            Gizmos.color = pathColor;

            if (tempPath.status == NavMeshPathStatus.PathComplete && tempPath.corners.Length > 0)
            {
                for (int j = 0; j < tempPath.corners.Length - 1; j++)
                {
                    Gizmos.DrawLine(tempPath.corners[j], tempPath.corners[j + 1]);
                    Gizmos.DrawSphere(tempPath.corners[j], 0.1f);
                }
                Gizmos.DrawSphere(tempPath.corners[tempPath.corners.Length - 1], 0.1f);
            }
            else
            {
                // 路径计算失败，绘制直线
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(validPoints[i].position, validPoints[i + 1].position);
            }
        }

        // 如果是闭合路径，连接最后一点和第一点
        if (closedPath && validPoints.Count > 2)
        {
            int lastIndex = validPoints.Count - 1;

            if (NavMesh.SamplePosition(validPoints[lastIndex].position, out _, 0.1f, NavMesh.AllAreas) &&
                NavMesh.SamplePosition(validPoints[0].position, out _, 0.1f, NavMesh.AllAreas))
            {
                NavMesh.CalculatePath(
                    validPoints[lastIndex].position,
                    validPoints[0].position,
                    NavMesh.AllAreas,
                    tempPath
                );

                Gizmos.color = pathColor;

                if (tempPath.status == NavMeshPathStatus.PathComplete && tempPath.corners.Length > 0)
                {
                    for (int j = 0; j < tempPath.corners.Length - 1; j++)
                    {
                        Gizmos.DrawLine(tempPath.corners[j], tempPath.corners[j + 1]);
                        Gizmos.DrawSphere(tempPath.corners[j], 0.1f);
                    }
                    Gizmos.DrawSphere(tempPath.corners[tempPath.corners.Length - 1], 0.1f);
                }
                else
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(validPoints[lastIndex].position, validPoints[0].position);
                }
            }
            else
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(validPoints[lastIndex].position, validPoints[0].position);
            }
        }

        // 将使用 Handles 的代码放在预处理指令中
#if UNITY_EDITOR
        // 绘制序号
        for (int i = 0; i < validPoints.Count; i++)
        {
            Handles.Label(validPoints[i].position + Vector3.up * 0.5f, $"{i + 1}");
        }
#endif
    }

    public void GeneratePathPoints()
    {
        if (controlPoints.Count < 2)
        {
            Debug.LogError("需要至少两个控制点才能生成路径!");
            return;
        }

        // 确保所有点都有效
        List<Transform> validPoints = new List<Transform>();
        foreach (var point in controlPoints)
        {
            if (point != null) validPoints.Add(point);
        }

        if (validPoints.Count < 2)
        {
            Debug.LogError("有效控制点不足两个!");
            return;
        }

        // 确保路径点父物体存在
        if (pathPointsParent == null)
        {
            GameObject parent = new GameObject("PathPoints");
            parent.transform.SetParent(transform);
            pathPointsParent = parent.transform;
        }
        else
        {
            // 清除现有路径点
            while (pathPointsParent.childCount > 0)
            {
                DestroyImmediate(pathPointsParent.GetChild(0).gameObject);
            }
        }

        // 收集所有插值点
        List<Vector3> allPathPoints = new List<Vector3>();

        // 初始化临时路径
        if (tempPath == null)
            tempPath = new NavMeshPath();

        // 连接所有控制点
        for (int i = 0; i < validPoints.Count - 1; i++)
        {
            // 检查NavMesh可访问性
            if (!NavMesh.SamplePosition(validPoints[i].position, out _, 0.1f, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(validPoints[i + 1].position, out _, 0.1f, NavMesh.AllAreas))
            {
                Debug.LogWarning($"控制点 {i + 1} 到 {i + 2} 不在NavMesh上，将使用直线连接");

                // 使用直线插值
                Vector3 start = validPoints[i].position;
                Vector3 end = validPoints[i + 1].position;
                float distance = Vector3.Distance(start, end);
                int pointCount = Mathf.Max(2, Mathf.CeilToInt(distance / pointSpacing));

                for (int j = 0; j < pointCount; j++)
                {
                    float t = j / (float)(pointCount - 1);
                    Vector3 point = Vector3.Lerp(start, end, t);

                    // 如果不是第一个区段的第一个点，并且是一个区段的第一个点，则不添加
                    // 这可以避免重复点
                    if (!(j == 0 && i > 0))
                    {
                        allPathPoints.Add(point);
                    }
                }

                continue;
            }

            // 计算两点间的NavMesh路径
            NavMesh.CalculatePath(
                validPoints[i].position,
                validPoints[i + 1].position,
                NavMesh.AllAreas,
                tempPath
            );

            if (tempPath.status == NavMeshPathStatus.PathComplete && tempPath.corners.Length > 0)
            {
                // 在每两个导航点之间插值生成点
                for (int cornerIdx = 0; cornerIdx < tempPath.corners.Length - 1; cornerIdx++)
                {
                    Vector3 start = tempPath.corners[cornerIdx];
                    Vector3 end = tempPath.corners[cornerIdx + 1];
                    float distance = Vector3.Distance(start, end);
                    int pointCount = Mathf.Max(2, Mathf.CeilToInt(distance / pointSpacing));

                    for (int j = 0; j < pointCount; j++)
                    {
                        float t = j / (float)(pointCount - 1);
                        Vector3 point = Vector3.Lerp(start, end, t);

                        // 只有当这不是一个重复点时才添加
                        if (!(cornerIdx == 0 && j == 0 && i > 0 && allPathPoints.Count > 0))
                        {
                            allPathPoints.Add(point);
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning($"控制点 {i + 1} 到 {i + 2} 的路径计算失败，使用直线连接");

                // 路径计算失败，使用直线
                Vector3 start = validPoints[i].position;
                Vector3 end = validPoints[i + 1].position;
                float distance = Vector3.Distance(start, end);
                int pointCount = Mathf.Max(2, Mathf.CeilToInt(distance / pointSpacing));

                for (int j = 0; j < pointCount; j++)
                {
                    float t = j / (float)(pointCount - 1);
                    Vector3 point = Vector3.Lerp(start, end, t);

                    // 如果不是第一个区段的第一个点，并且是一个区段的第一个点，则不添加
                    if (!(j == 0 && i > 0))
                    {
                        allPathPoints.Add(point);
                    }
                }
            }
        }

        // 如果是闭合路径，连接最后一点和第一点
        if (closedPath && validPoints.Count > 2)
        {
            int lastIndex = validPoints.Count - 1;

            if (NavMesh.SamplePosition(validPoints[lastIndex].position, out _, 0.1f, NavMesh.AllAreas) &&
                NavMesh.SamplePosition(validPoints[0].position, out _, 0.1f, NavMesh.AllAreas))
            {
                NavMesh.CalculatePath(
                    validPoints[lastIndex].position,
                    validPoints[0].position,
                    NavMesh.AllAreas,
                    tempPath
                );

                if (tempPath.status == NavMeshPathStatus.PathComplete && tempPath.corners.Length > 0)
                {
                    // 在每两个导航点之间插值生成点
                    for (int cornerIdx = 0; cornerIdx < tempPath.corners.Length - 1; cornerIdx++)
                    {
                        Vector3 start = tempPath.corners[cornerIdx];
                        Vector3 end = tempPath.corners[cornerIdx + 1];
                        float distance = Vector3.Distance(start, end);
                        int pointCount = Mathf.Max(2, Mathf.CeilToInt(distance / pointSpacing));

                        // 最后一段路径不需要加入最后一个点，因为已经有第一个点了
                        int endPoint = (cornerIdx == tempPath.corners.Length - 2) ? pointCount - 1 : pointCount;

                        for (int j = 0; j < endPoint; j++)
                        {
                            float t = j / (float)(pointCount - 1);
                            Vector3 point = Vector3.Lerp(start, end, t);

                            // 避免添加重复点
                            if (!(cornerIdx == 0 && j == 0))
                            {
                                allPathPoints.Add(point);
                            }
                        }
                    }
                }
                else
                {
                    // 使用直线
                    Vector3 start = validPoints[lastIndex].position;
                    Vector3 end = validPoints[0].position;
                    float distance = Vector3.Distance(start, end);
                    int pointCount = Mathf.Max(2, Mathf.CeilToInt(distance / pointSpacing));

                    // 不要添加最后一个点，因为它就是第一个点
                    for (int j = 0; j < pointCount - 1; j++)
                    {
                        float t = j / (float)(pointCount - 1);
                        Vector3 point = Vector3.Lerp(start, end, t);
                        allPathPoints.Add(point);
                    }
                }
            }
        }

        // 创建路径点
        for (int i = 0; i < allPathPoints.Count; i++)
        {
            GameObject pointObj = new GameObject($"PathPoint_{i}");
            pointObj.transform.SetParent(pathPointsParent);
            pointObj.transform.position = allPathPoints[i];
        }

        Debug.Log($"已生成 {pathPointsParent.childCount} 个路径点");
    }

    // 添加新控制点
    public void AddControlPoint()
    {
        GameObject newPoint = new GameObject($"ControlPoint_{controlPoints.Count}");
        newPoint.transform.SetParent(transform);

        // 如果已有点，放在最后一个点之后
        if (controlPoints.Count > 0)
        {
            Transform lastPoint = controlPoints[controlPoints.Count - 1];
            if (lastPoint != null)
            {
                Vector3 direction = (controlPoints.Count > 1 && controlPoints[controlPoints.Count - 2] != null)
                    ? (lastPoint.position - controlPoints[controlPoints.Count - 2].position).normalized
                    : Vector3.forward;

                newPoint.transform.position = lastPoint.position + direction * 2f;
            }
            else
            {
                newPoint.transform.position = transform.position + Vector3.forward * (controlPoints.Count * 2f);
            }
        }
        else
        {
            newPoint.transform.position = transform.position;
        }

        controlPoints.Add(newPoint.transform);
    }

    // 移除最后一个控制点
    public void RemoveLastControlPoint()
    {
        if (controlPoints.Count > 0)
        {
            Transform lastPoint = controlPoints[controlPoints.Count - 1];
            controlPoints.RemoveAt(controlPoints.Count - 1);

            if (lastPoint != null)
                DestroyImmediate(lastPoint.gameObject);
        }
    }
}