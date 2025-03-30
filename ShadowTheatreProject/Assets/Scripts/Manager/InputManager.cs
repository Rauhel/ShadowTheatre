using UnityEngine;
using System;
using System.Collections.Generic;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }
    
    [Header("Input Settings")]
    [SerializeField] private bool useMouseInput = true;
    [SerializeField] private bool useGestureInput = false;
    
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
    
    private void Update()
    {
        // 如果使用鼠标输入
        if (useMouseInput)
        {
            UpdateMouseInput();
        }
        
        // 其他输入源的更新会通过外部接口调用
        // 例如: UpdateGestureInput() 会被通信接口调用
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
    public void UpdateGestureData(string type, Vector2 position, float confidence = 1.0f, Dictionary<string, float> additionalData = null)
    {
        useGestureInput = true;
        isPointerActive = true;
        
        currentGesture.type = type;
        currentGesture.position = position;
        currentGesture.confidence = confidence;
        
        if (additionalData != null)
        {
            currentGesture.additionalData = additionalData;
        }
        
        // 将屏幕坐标转换为世界坐标
        Ray ray = mainCamera.ScreenPointToRay(new Vector3(
            position.x * Screen.width,
            position.y * Screen.height, 
            0
        ));
        
        if (groundPlane.Raycast(ray, out float distance))
        {
            currentPointerPosition = ray.GetPoint(distance);
        }
        
        // 通知所有监听器
        NotifyGestureListeners();
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
        
        // 更新指针活跃状态
        if (useMouseInput)
        {
            isPointerActive = Input.mousePresent;
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
}