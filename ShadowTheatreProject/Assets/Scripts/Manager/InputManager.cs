using UnityEngine;
using System;
using System.Collections.Generic;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    [Header("Input Settings")]
    [SerializeField] private bool useMouseInput = true;
    [SerializeField] private bool useGestureInput = false;

    [Header("手势坐标映射配置")]
    [SerializeField] private bool invertXAxis = false;   // 是否反转X轴
    [SerializeField] private bool invertYAxis = true;    // 是否反转Y轴 (很可能需要)
    [SerializeField] private Vector2 positionOffset = Vector2.zero;  // 坐标偏移
    [SerializeField] private Vector2 positionScale = Vector2.one;    // 坐标缩放

    // 鼠标位置转换相关
    private Plane groundPlane;
    private Camera mainCamera;

    // 输入状态
    private Vector3 currentPointerPosition;
    private bool isPointerActive = false;

    // 手势数据
    [System.Serializable]
    public class GestureData
    {
        public string type;       // 手势类型（指向、抓取、放开等）
        public Vector2 position;  // 归一化的屏幕坐标 (0-1, 0-1)
        public float confidence;  // 置信度 (0-1)
        public Dictionary<string, float> additionalData = new Dictionary<string, float>(); // 其他数据
    }

    private GestureData currentGesture = new GestureData();
    private List<Action<GestureData>> gestureListeners = new List<Action<GestureData>>();

    // 常量定义
    public static class GestureTypes
    {
        public const string POINT = "point";
        public const string GRAB = "grab";
        public const string RELEASE = "release";
        public const string SWIPE_LEFT = "swipe_left";
        public const string SWIPE_RIGHT = "swipe_right";
        public const string SWIPE_UP = "swipe_up";
        public const string SWIPE_DOWN = "swipe_down";
        // 可以根据需要添加更多手势类型
    }

    private void Awake()
    {
        // 单例设置
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 初始化
        mainCamera = Camera.main;
        groundPlane = new Plane(Vector3.up, Vector3.zero);

        // 初始化手势数据
        currentGesture.type = "";
        currentGesture.position = Vector2.zero;
        currentGesture.confidence = 0f;
    }

    private void Start()
    {
        // 检查相机引用
        if (mainCamera == null)
        {
            Debug.LogError("主相机引用为空，尝试获取...");
            mainCamera = Camera.main;

            if (mainCamera == null)
            {
                Debug.LogError("严重错误：找不到主相机!");
            }
            else
            {
                Debug.Log($"找到主相机: {mainCamera.name}");
            }
        }

        // 检查地平面配置
        Debug.Log($"地平面配置: 法线={groundPlane.normal}, 距离={groundPlane.distance}");

        // 输出初始状态信息
        Debug.Log($"输入管理器初始状态: 鼠标输入={useMouseInput}, 手势输入={useGestureInput}");
    }

    private void Update()
    {
        if (useMouseInput)
        {
            UpdateMouseInput();
        }

        // 手势模式下的调试支持
        if (useGestureInput && !isPointerActive)
        {
            Debug.LogWarning("手势模式已启用但未收到数据");
            // 可以添加临时的数据模拟或回退到鼠标输入
        }


    }

    private void UpdateMouseInput()
    {
        isPointerActive = Input.mousePresent;

        if (isPointerActive)
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (groundPlane.Raycast(ray, out float distance))
            {
                currentPointerPosition = ray.GetPoint(distance);
            }
        }
    }

    /// <summary>
    /// 将MediaPipe坐标映射到Unity屏幕坐标系
    /// </summary>
    private Vector2 MapGesturePositionToScreen(Vector2 gesturePos)
    {
        // 应用缩放
        Vector2 scaledPos = new Vector2(
            gesturePos.x * positionScale.x,
            gesturePos.y * positionScale.y
        );

        // 应用反转
        if (invertXAxis) scaledPos.x = 1 - scaledPos.x;
        if (invertYAxis) scaledPos.y = 1 - scaledPos.y;

        // 应用偏移 (归一化值的偏移)
        Vector2 mappedPos = scaledPos + positionOffset;

        // 将归一化坐标(0-1)转换为屏幕像素坐标
        return new Vector2(
            mappedPos.x * Screen.width,
            mappedPos.y * Screen.height
        );
    }

    // 公共API: 获取当前指针在世界空间的位置
    public Vector3 GetPointerWorldPosition()
    {
        return currentPointerPosition;
    }

    // 公共API: 指针是否激活
    public bool IsPointerActive()
    {
        return isPointerActive;
    }

    // 公共API: 获取鼠标点击 (可以根据手势类型映射)
    public bool IsPointerDown()
    {
        if (useMouseInput && Input.GetMouseButton(0))
            return true;

        if (useGestureInput && currentGesture.type == GestureTypes.GRAB)
            return true;

        return false;
    }

    // 公共API: 获取鼠标释放 (可以根据手势类型映射)
    public bool IsPointerUp()
    {
        if (useMouseInput && Input.GetMouseButtonUp(0))
            return true;

        if (useGestureInput && currentGesture.type == GestureTypes.RELEASE)
            return true;

        return false;
    }

    // 手势输入接口: 用于从外部更新手势数据
    public void UpdateGestureData(string type, Vector2 rawPosition, float confidence = 1.0f, Dictionary<string, float> additionalData = null)
    {
        useGestureInput = true;
        isPointerActive = true;

        // 保存原始数据
        currentGesture.type = type;
        currentGesture.position = rawPosition; // 保存原始位置
        currentGesture.confidence = confidence;

        if (additionalData != null)
        {
            currentGesture.additionalData = additionalData;
        }

        // 映射坐标到Unity屏幕坐标系
        Vector2 mappedScreenPosition = MapGesturePositionToScreen(rawPosition);

        // 详细的调试信息
        Debug.Log($"手势数据: 类型={type}, 原始位置=({rawPosition.x}, {rawPosition.y}), 映射后=({mappedScreenPosition.x}, {mappedScreenPosition.y})");

        // 确保摄像机引用正确
        if (mainCamera == null)
        {
            RefreshMainCamera();
            if (mainCamera == null)
            {
                Debug.LogError("找不到主摄像机，无法进行射线检测!");
                return;
            }
        }

        // 使用映射后的屏幕坐标创建射线
        Ray ray = mainCamera.ScreenPointToRay(new Vector3(mappedScreenPosition.x, mappedScreenPosition.y, 0));
        Debug.Log($"射线: 原点={ray.origin}, 方向={ray.direction}, 平面法线={groundPlane.normal}");

        // 确保地平面配置正确
        if (groundPlane.distance != 0 || groundPlane.normal != Vector3.up)
        {
            Debug.Log("重置地平面配置");
            groundPlane = new Plane(Vector3.up, Vector3.zero);
        }

        // 尝试进行射线检测
        bool raycastSuccess = groundPlane.Raycast(ray, out float distance);
        Debug.Log($"射线检测 {(raycastSuccess ? "成功" : "失败")}, 距离: {distance}");

        if (raycastSuccess)
        {
            Vector3 hitPoint = ray.GetPoint(distance);
            Debug.Log($"命中点: {hitPoint}");

            // 更新指针位置
            currentPointerPosition = hitPoint;

            // 在场景中绘制射线，便于调试
            Debug.DrawRay(ray.origin, ray.direction * distance, Color.green, 0.1f);
        }
        else
        {
            Debug.LogWarning("射线未击中地平面!");
            // 在场景中绘制未命中的射线，便于调试
            Debug.DrawRay(ray.origin, ray.direction * 100, Color.red, 0.1f);

            // 可以尝试使用一个备用方法来计算位置
            TryAlternativePositionCalculation(ray);
        }

        // 通知监听器
        NotifyGestureListeners();
    }

    // 备用的位置计算方法
    private void TryAlternativePositionCalculation(Ray ray)
    {
        // 方法1: 使用一个固定的y值
        float fixedY = 0;
        float t = (fixedY - ray.origin.y) / ray.direction.y;

        if (t > 0)
        {
            Vector3 hitPoint = ray.origin + ray.direction * t;
            Debug.Log($"备用方法计算的位置: {hitPoint}");
            currentPointerPosition = hitPoint;
        }
        else
        {
            Debug.LogError("备用方法也无法计算有效位置!");
        }
    }

    // 注册手势监听器
    public void RegisterGestureListener(Action<GestureData> listener)
    {
        if (!gestureListeners.Contains(listener))
        {
            gestureListeners.Add(listener);
        }
    }

    // 注销手势监听器
    public void UnregisterGestureListener(Action<GestureData> listener)
    {
        if (gestureListeners.Contains(listener))
        {
            gestureListeners.Remove(listener);
        }
    }

    // 通知所有手势监听器
    private void NotifyGestureListeners()
    {
        foreach (var listener in gestureListeners)
        {
            listener.Invoke(currentGesture);
        }
    }

    // 更新地面平面
    public void UpdateGroundPlane(float height)
    {
        groundPlane = new Plane(Vector3.up, new Vector3(0, height, 0));
    }

    // 强制刷新MainCamera引用
    public void RefreshMainCamera()
    {
        mainCamera = Camera.main;
    }

    // 设置输入方法
    public void SetInputMethod(bool mouse, bool gesture)
    {
        useMouseInput = mouse;
        useGestureInput = gesture;

        // 如果都关闭了，默认启用鼠标输入作为后备
        if (!useMouseInput && !useGestureInput)
        {
            useMouseInput = true;
        }

        // 重置状态
        if (!useGestureInput)
        {
            currentGesture.type = "";
            currentGesture.position = Vector2.zero;
            currentGesture.confidence = 0f;
            currentGesture.additionalData.Clear();
        }

        // 每次切换输入方式时，强制刷新相关引用
        RefreshMainCamera();

        // 更新指针活跃状态
        if (useMouseInput)
        {
            isPointerActive = Input.mousePresent;
        }
        else
        {
            // 当禁用鼠标输入且没有收到手势数据时，临时设置为非活跃状态
            // 直到 UpdateGestureData 被调用
            isPointerActive = false;
        }

        // 如果刚启用了手势输入，记录日志
        if (useGestureInput)
        {
            Debug.Log("手势输入已启用，但需要外部系统（如MediaPipe）提供数据");
        }
    }

    // 检查是否正在使用鼠标输入
    public bool IsUsingMouseInput()
    {
        return useMouseInput;
    }

    // 检查是否正在使用手势输入
    public bool IsUsingGestureInput()
    {
        return useGestureInput;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 使用测试数据进行映射测试
    /// </summary>
    public void TestMapping(Vector2 testPosition)
    {
        Vector2 mappedPos = MapGesturePositionToScreen(testPosition);
        Debug.Log($"测试映射: 输入=({testPosition.x}, {testPosition.y}), 映射后=({mappedPos.x}, {mappedPos.y})");
        
        // 可选：临时更新手势数据
        UpdateGestureData("test", testPosition, 1.0f);
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !useGestureInput) return;
        
        // 绘制当前手势位置的可视化
        if (isPointerActive && mainCamera != null)
        {
            // 绘制原始手势位置到屏幕映射的点
            Vector2 mappedScreenPos = MapGesturePositionToScreen(currentGesture.position);
            Gizmos.color = Color.yellow;
            Vector3 screenPosWorld = mainCamera.ScreenToWorldPoint(
                new Vector3(mappedScreenPos.x, mappedScreenPos.y, 10));
            Gizmos.DrawSphere(screenPosWorld, 0.1f);
            
            // 绘制实际指针位置
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(currentPointerPosition, 0.15f);
        }
    }
#endif
    public GestureData CurrentGesture
{
    get { return currentGesture; }
}

    public bool IsGestureValid(Vector2 gesturePosition)
    {
        // 示例逻辑：检查手势位置是否在屏幕范围内
        return gesturePosition.x >= 0 && gesturePosition.x <= 1 &&
               gesturePosition.y >= 0 && gesturePosition.y <= 1;
    }
}