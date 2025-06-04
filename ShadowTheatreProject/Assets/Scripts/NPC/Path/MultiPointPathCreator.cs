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
    [Tooltip("路径点总数，设置后将均匀分布于整个路径上，设为0时使用点间隔方式生成")]
    public int totalPathPoints = 0;

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

    // 添加用于存储路径点数据的列表
    [HideInInspector]
    public List<Vector3> pathPointPositions = new List<Vector3>();
    [HideInInspector]
    public List<string> pathPointNames = new List<string>();

    private void Awake()
    {
        tempPath = new NavMeshPath();
        UpdateStartAndEndPoints();

        // 确保路径ID不为空
        if (string.IsNullOrEmpty(pathID))
        {
            pathID = System.Guid.NewGuid().ToString().Substring(0, 8);
        }

        // 确保路径点已生成
        if (pathPointPositions.Count == 0 && controlPoints.Count >= 2)
        {
            GeneratePathPoints();
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

        // 清空路径点数据
        pathPointPositions.Clear();
        pathPointNames.Clear();

        // 保留父物体用于组织结构，但不实际创建子物体
        if (pathPointsParent == null)
        {
            GameObject pathPointsObj = new GameObject("PathPoints");
            pathPointsObj.transform.SetParent(transform);
            pathPointsParent = pathPointsObj.transform;
        }
        else
        {
            // 清除现有的路径点物体
            while (pathPointsParent.childCount > 0)
            {
                DestroyImmediate(pathPointsParent.GetChild(0).gameObject);
            }
        }

        // 初始化临时路径
        if (tempPath == null)
            tempPath = new NavMeshPath();

        // 如果指定了总点数，则使用均匀分布的方式生成
        if (totalPathPoints > 1)
        {
            GeneratePathPointsEvenly();
        }
        else
        {
            // 否则使用原有的基于间距的方式
            GeneratePathPointsBySpacing();
        }

        // 更新起点和终点
        UpdateStartAndEndPoints();
    }

    // 根据间距生成路径点（严格插值于控制点之间，不再用NavMesh寻路）
    private void GeneratePathPointsBySpacing()
    {
        for (int i = 0; i < controlPoints.Count - 1; i++)
        {
            if (controlPoints[i] == null || controlPoints[i + 1] == null)
                continue;

            Vector3 start = controlPoints[i].position;
            Vector3 end = controlPoints[i + 1].position;
            float distance = Vector3.Distance(start, end);
            int pointCount = Mathf.Max(2, Mathf.CeilToInt(distance / pointSpacing));

            for (int j = 0; j < pointCount; j++)
            {
                // 如果不是第一段的第一个点，则跳过
                if (j == 0 && i > 0) continue;

                float t = j / (float)(pointCount - 1);
                Vector3 position = Vector3.Lerp(start, end, t);

                string pointName = $"PathPoint_{pathPointPositions.Count}";
                pathPointNames.Add(pointName);
                pathPointPositions.Add(position);
            }
        }

        // 如果是闭合路径，添加从最后一个点到第一个点的连接
        if (closedPath && controlPoints.Count > 2)
        {
            Vector3 start = controlPoints[controlPoints.Count - 1].position;
            Vector3 end = controlPoints[0].position;
            float distance = Vector3.Distance(start, end);
            int pointCount = Mathf.Max(2, Mathf.CeilToInt(distance / pointSpacing));
            for (int j = 1; j < pointCount; j++) // 跳过第一个点
            {
                float t = j / (float)(pointCount - 1);
                Vector3 position = Vector3.Lerp(start, end, t);

                string pointName = $"PathPoint_{pathPointPositions.Count}";
                pathPointNames.Add(pointName);
                pathPointPositions.Add(position);
            }
        }
    }

    // 根据总数均匀分布路径点（严格插值于所有控制点路径段）
    private void GeneratePathPointsEvenly()
    {
        // 先收集所有段的长度
        List<Vector3> allPoints = new List<Vector3>();
        List<float> segmentLengths = new List<float>();
        float totalLength = 0f;

        for (int i = 0; i < controlPoints.Count - 1; i++)
        {
            Vector3 a = controlPoints[i].position;
            Vector3 b = controlPoints[i + 1].position;
            float len = Vector3.Distance(a, b);
            allPoints.Add(a);
            segmentLengths.Add(len);
            totalLength += len;
        }
        // 闭合路径
        if (closedPath && controlPoints.Count > 2)
        {
            Vector3 a = controlPoints[controlPoints.Count - 1].position;
            Vector3 b = controlPoints[0].position;
            float len = Vector3.Distance(a, b);
            allPoints.Add(a);
            segmentLengths.Add(len);
            totalLength += len;
        }
        // 添加最后一个点
        if (!closedPath && controlPoints.Count > 1)
            allPoints.Add(controlPoints[controlPoints.Count - 1].position);

        if (allPoints.Count < 2 || segmentLengths.Count == 0)
            return;

        // 均匀分布totalPathPoints个点
        for (int i = 0; i < totalPathPoints; i++)
        {
            float t = i / (float)(totalPathPoints - 1);
            float targetDist = t * totalLength;

            // 找到在哪一段
            float accum = 0f;
            int seg = 0;
            while (seg < segmentLengths.Count && accum + segmentLengths[seg] < targetDist)
            {
                accum += segmentLengths[seg];
                seg++;
            }
            // 计算在该段内的插值
            float segT = segmentLengths[seg] > 0 ? (targetDist - accum) / segmentLengths[seg] : 0f;
            Vector3 p0 = allPoints[seg];
            Vector3 p1 = (seg + 1 < allPoints.Count) ? allPoints[seg + 1] : allPoints[0];
            Vector3 pos = Vector3.Lerp(p0, p1, segT);

            string pointName = $"PathPoint_{i}";
            pathPointNames.Add(pointName);
            pathPointPositions.Add(pos);
        }
    }

    // 添加获取路径点位置的方法
    public Vector3 GetPathPointPosition(int index)
    {
        if (index >= 0 && index < pathPointPositions.Count)
            return pathPointPositions[index];

        Debug.LogWarning($"路径点索引 {index} 超出范围");
        return Vector3.zero;
    }

    // 添加获取下一个路径点位置的方法
    public Vector3 GetNextPathPointPosition(int currentIndex)
    {
        int nextIndex = currentIndex + 1;

        // 如果是闭合路径且已到末尾，则返回第一个点
        if (closedPath && nextIndex >= pathPointPositions.Count)
            nextIndex = 0;

        if (nextIndex >= 0 && nextIndex < pathPointPositions.Count)
            return pathPointPositions[nextIndex];

        // 如果没有下一个点，返回当前点位置
        if (currentIndex >= 0 && currentIndex < pathPointPositions.Count)
            return pathPointPositions[currentIndex];

        Debug.LogWarning($"路径点索引 {currentIndex} 超出范围");
        return Vector3.zero;
    }

    // 获取路径点总数
    public int GetPathPointCount()
    {
        return pathPointPositions.Count;
    }

    // 根据名称获取路径点索引
    public int GetPathPointIndex(string pointName)
    {
        return pathPointNames.IndexOf(pointName);
    }

    /// <summary>
    /// 计算两个路径点之间的实际路径距离
    /// 基于路径点均匀分布的特性，使用平均路径长度 × 路径点间隔
    /// </summary>
    public float GetDistanceBetweenPathPoints(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || toIndex < 0 || fromIndex >= pathPointPositions.Count || toIndex >= pathPointPositions.Count)
        {
            Debug.LogWarning($"路径点索引超出范围: from={fromIndex}, to={toIndex}, total={pathPointPositions.Count}");
            return 0f;
        }
        
        if (fromIndex == toIndex)
            return 0f;
            
        // 确保 fromIndex < toIndex
        if (fromIndex > toIndex)
        {
            int temp = fromIndex;
            fromIndex = toIndex;
            toIndex = temp;
        }
        
        // 计算总路径长度
        float totalPathLength = CalculateTotalPathLength();
        
        if (totalPathLength <= 0f || pathPointPositions.Count <= 1)
            return 0f;
        
        // 计算平均每个路径点间隔的距离
        float averageSegmentLength = totalPathLength / (pathPointPositions.Count - 1);
        
        // 路径点间隔数 × 平均段长度
        int segmentCount = toIndex - fromIndex;
        float pathDistance = segmentCount * averageSegmentLength;
        
        return pathDistance;
    }
    
    /// <summary>
    /// 计算整个路径的总长度
    /// </summary>
    public float CalculateTotalPathLength()
    {
        if (controlPoints.Count < 2)
            return 0f;
        
        float totalLength = 0f;
        
        // 计算所有控制点之间的直线距离总和
        for (int i = 0; i < controlPoints.Count - 1; i++)
        {
            if (controlPoints[i] != null && controlPoints[i + 1] != null)
            {
                float segmentLength = Vector3.Distance(controlPoints[i].position, controlPoints[i + 1].position);
                totalLength += segmentLength;
            }
        }
        
        // 如果是闭合路径，添加最后一个点到第一个点的距离
        if (closedPath && controlPoints.Count > 2)
        {
            if (controlPoints[controlPoints.Count - 1] != null && controlPoints[0] != null)
            {
                float closingLength = Vector3.Distance(controlPoints[controlPoints.Count - 1].position, controlPoints[0].position);
                totalLength += closingLength;
            }
        }
        
        return totalLength;
    }
    
    /// <summary>
    /// 从当前位置到指定路径点的距离（包含当前位置到下一个路径点的部分距离）
    /// </summary>
    public float GetDistanceFromCurrentPosition(Vector3 currentPosition, int currentPathPointIndex, int targetPathPointIndex)
    {
        if (targetPathPointIndex <= currentPathPointIndex)
            return 0f;
            
        float totalDistance = 0f;
        
        // 1. 当前位置到下一个路径点的距离
        if (currentPathPointIndex >= -1 && currentPathPointIndex + 1 < pathPointPositions.Count)
        {
            Vector3 nextPointPos = pathPointPositions[currentPathPointIndex + 1];
            totalDistance += Vector3.Distance(currentPosition, nextPointPos);
        }
        
        // 2. 中间路径点之间的距离
        if (currentPathPointIndex + 1 < targetPathPointIndex)
        {
            totalDistance += GetDistanceBetweenPathPoints(currentPathPointIndex + 1, targetPathPointIndex);
        }
        
        return totalDistance;
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

        // 添加绘制存储的路径点位置
        if (pathPointPositions.Count > 0)
        {
            Gizmos.color = new Color(0f, 0.8f, 0.2f, 0.8f);
            foreach (Vector3 pos in pathPointPositions)
            {
                Gizmos.DrawSphere(pos, 0.15f);
            }
        }
    }
}