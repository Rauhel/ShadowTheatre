using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

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
    public bool closedPath = false;

    [Header("路径点生成")]
    public Transform pathPointsParent;

    [Header("路径连接")]
    [Tooltip("起始点引用（默认为第一个控制点）")]
    public Transform startPoint;
    [Tooltip("终点引用（默认为最后一个控制点）")]
    public Transform endPoint;
    [Tooltip("前一条路径（如果有）")]
    public Transform previousPath;
    [Tooltip("过渡点间隔，越小过渡越平滑")]
    [Range(0.1f, 2f)]
    public float transitionSpacing = 0.5f;

    [Header("ID")]
    [Tooltip("路径的唯一标识符 (自动生成)")]
    public string pathID;

    // 临时路径存储
    private NavMeshPath tempPath;

    private void Awake()
    {
        tempPath = new NavMeshPath();
        UpdateStartAndEndPoints();

        // 确保路径ID不为空
        if (string.IsNullOrEmpty(pathID))
        {
            pathID = System.Guid.NewGuid().ToString().Substring(0, 8);
        }

        // 注册到路径注册表
        PathRegistry.RegisterPath(pathID, this);
    }

    // 更新起点和终点引用
    public void UpdateStartAndEndPoints()
    {
        if (controlPoints.Count > 0)
        {
            // 起点默认为第一个控制点
            startPoint = controlPoints[0];

            // 终点默认为最后一个控制点
            endPoint = controlPoints[controlPoints.Count - 1];
        }
        else
        {
            startPoint = null;
            endPoint = null;
        }
    }

    // 添加新控制点
    public void AddControlPoint()
    {
        GameObject newPoint = new GameObject($"ControlPoint_{controlPoints.Count}");
        newPoint.transform.SetParent(transform);

        // 如果是第一个点，且设置了前一路径，则将其放在前一路径的终点位置
        if (controlPoints.Count == 0 && previousPath != null)
        {
            MultiPointPathCreator prevPathCreator = previousPath.GetComponent<MultiPointPathCreator>();
            if (prevPathCreator != null && prevPathCreator.controlPoints.Count > 0)
            {
                // 获取前一路径的最后一个控制点
                Transform lastPoint = prevPathCreator.controlPoints[prevPathCreator.controlPoints.Count - 1];
                if (lastPoint != null)
                {
                    newPoint.transform.position = lastPoint.position;
                }
                else
                {
                    newPoint.transform.position = transform.position;
                }
            }
            else
            {
                newPoint.transform.position = transform.position;
            }
        }
        // 如果已有点，放在最后一个点之后
        else if (controlPoints.Count > 0)
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
        UpdateStartAndEndPoints();
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

            UpdateStartAndEndPoints();
        }
    }

    // 生成路径点
    public void GeneratePathPoints()
    {
        if (controlPoints.Count < 2)
        {
            Debug.LogWarning("需要至少两个控制点来生成路径");
            return;
        }

        // 清除现有的路径点
        if (pathPointsParent == null)
        {
            GameObject pathPointsObj = new GameObject("PathPoints");
            pathPointsObj.transform.SetParent(transform);
            pathPointsParent = pathPointsObj.transform;
        }
        else
        {
            // 清除现有的路径点
            while (pathPointsParent.childCount > 0)
            {
                DestroyImmediate(pathPointsParent.GetChild(0).gameObject);
            }
        }

        // 初始化临时路径
        if (tempPath == null)
            tempPath = new NavMeshPath();

        // 逐段处理控制点
        for (int i = 0; i < controlPoints.Count - 1; i++)
        {
            if (controlPoints[i] == null || controlPoints[i + 1] == null)
                continue;

            // 计算NavMesh路径
            if (NavMesh.CalculatePath(controlPoints[i].position, controlPoints[i + 1].position, NavMesh.AllAreas, tempPath))
            {
                // 如果找到了路径
                if (tempPath.status == NavMeshPathStatus.PathComplete)
                {
                    // 使用NavMesh计算出的路径点
                    for (int j = 0; j < tempPath.corners.Length; j++)
                    {
                        // 第一个点是起点，如果不是第一段，则跳过起点
                        if (j == 0 && i > 0) continue;

                        // 创建路径点
                        GameObject pointObj = new GameObject($"PathPoint_{pathPointsParent.childCount}");
                        pointObj.transform.SetParent(pathPointsParent);
                        pointObj.transform.position = tempPath.corners[j];
                    }
                }
                else
                {
                    // 如果NavMesh路径不完整，使用直线路径
                    Debug.LogWarning($"控制点 {i} 和 {i + 1} 之间无法找到完整的NavMesh路径，使用直线");

                    // 计算直线上的点
                    float distance = Vector3.Distance(controlPoints[i].position, controlPoints[i + 1].position);
                    int pointCount = Mathf.Max(2, Mathf.CeilToInt(distance / pointSpacing));

                    for (int j = 0; j < pointCount; j++)
                    {
                        // 如果不是第一段的第一个点，则跳过
                        if (j == 0 && i > 0) continue;

                        float t = j / (float)(pointCount - 1);
                        Vector3 position = Vector3.Lerp(controlPoints[i].position, controlPoints[i + 1].position, t);

                        GameObject pointObj = new GameObject($"PathPoint_{pathPointsParent.childCount}");
                        pointObj.transform.SetParent(pathPointsParent);
                        pointObj.transform.position = position;
                    }
                }
            }
        }

        // 如果是闭合路径，添加从最后一个点到第一个点的连接
        if (closedPath && controlPoints.Count > 2)
        {
            Transform lastPoint = controlPoints[controlPoints.Count - 1];
            Transform firstPoint = controlPoints[0];

            if (lastPoint != null && firstPoint != null)
            {
                if (NavMesh.CalculatePath(lastPoint.position, firstPoint.position, NavMesh.AllAreas, tempPath))
                {
                    if (tempPath.status == NavMeshPathStatus.PathComplete)
                    {
                        for (int j = 1; j < tempPath.corners.Length; j++) // 跳过第一个点
                        {
                            GameObject pointObj = new GameObject($"PathPoint_{pathPointsParent.childCount}");
                            pointObj.transform.SetParent(pathPointsParent);
                            pointObj.transform.position = tempPath.corners[j];
                        }
                    }
                }
            }
        }

        // 更新起点和终点
        UpdateStartAndEndPoints();
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

        // 如果是闭合路径，绘制从最后一个点到第一个点的连接
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

        // 绘制起点和终点的特殊标记
        if (startPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(startPoint.position, 0.4f);
        }

        if (endPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(endPoint.position, 0.4f);
        }

        // 显示前一路径的连接
        if (previousPath != null)
        {
            MultiPointPathCreator prevPathCreator = previousPath.GetComponent<MultiPointPathCreator>();
            if (prevPathCreator != null && prevPathCreator.endPoint != null && startPoint != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(prevPathCreator.endPoint.position, startPoint.position);
            }
        }
    }
}