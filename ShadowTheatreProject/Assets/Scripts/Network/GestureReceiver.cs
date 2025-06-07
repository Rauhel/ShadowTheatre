using UnityEngine;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using System.Diagnostics;

/// <summary>
/// 手势接收器：负责从网络接收手势数据并传递给InputManager
/// </summary>
public class GestureReceiver : MonoBehaviour
{
    [Header("Network Settings")]
    [SerializeField] private int positionPort = 5000;
    [SerializeField] private int gesturePort = 8000;
    [SerializeField] private bool autoConnect = true;
    [SerializeField] private string messageDelimiter = "|";

    [Header("运行模式设置")]
    [Tooltip("true=调试模式(手动运行Python脚本), false=正式模式(自动启动exe)")]
    [SerializeField] private bool debugMode = false;
    
    [Tooltip("调试模式下显示的提示信息")]
    [SerializeField] private bool showDebugInstructions = true;

    // 连接状态和网络组件
    private bool isPositionConnected = false;
    private bool isPositionRunning = false;
    private UdpClient positionUdpClient;
    private IPEndPoint positionEndPoint;
    private Thread positionReceiveThread;

    private bool isGestureConnected = false;
    private bool isGestureRunning = false;
    private UdpClient gestureUdpClient;
    private IPEndPoint gestureEndPoint;
    private Thread gestureReceiveThread;

    // 输入管理器引用
    private InputManager inputManager;

    // 消息队列
    private ConcurrentQueue<string> positionMessageQueue = new ConcurrentQueue<string>();
    private ConcurrentQueue<string> gestureMessageQueue = new ConcurrentQueue<string>();

    // 新增：手势识别进程
    private Process gestureAppProcess;

    private void Start()
    {
        if (debugMode)
        {
            // 调试模式：不启动exe，等待手动运行Python脚本
            UnityEngine.Debug.Log("[GestureReceiver] === 调试模式 ===");
            if (showDebugInstructions)
            {
                UnityEngine.Debug.Log("[GestureReceiver] 请手动运行Python脚本:");
                UnityEngine.Debug.Log("[GestureReceiver] 1. 打开命令行，进入Gesture目录");
                UnityEngine.Debug.Log("[GestureReceiver] 2. 运行: python main.py");
                UnityEngine.Debug.Log("[GestureReceiver] 或者运行: python gesture_recognition.py");
                UnityEngine.Debug.Log("[GestureReceiver] 注意：确保Python环境已安装所需依赖");
            }
        }
        else
        {
            // 正式模式：自动启动gesture_app.exe
            UnityEngine.Debug.Log("[GestureReceiver] === 正式模式 ===");
            StartGestureApp();
        }

        // 获取InputManager引用
        inputManager = InputManager.Instance;
        if (inputManager == null)
        {
            inputManager = FindObjectOfType<InputManager>();
            if (inputManager == null)
            {
                UnityEngine.Debug.LogError("找不到InputManager!");
                return;
            }
        }

        if (autoConnect)
        {
            ConnectAll();
        }
    }

    // 启动StreamingAssets下的gesture_app.exe（正式模式）
    private void StartGestureApp()
    {
        string exePath = System.IO.Path.Combine(Application.streamingAssetsPath, "gesture_app.exe");
        if (System.IO.File.Exists(exePath))
        {
            try
            {
                gestureAppProcess = new Process();
                gestureAppProcess.StartInfo.FileName = exePath;
                gestureAppProcess.StartInfo.UseShellExecute = false;
                gestureAppProcess.StartInfo.CreateNoWindow = true;
                gestureAppProcess.Start();
                UnityEngine.Debug.Log($"[GestureReceiver] 成功启动手势识别应用: {exePath}");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"启动手势识别应用失败: {e.Message}");
            }
        }
        else
        {
            UnityEngine.Debug.LogError($"未找到手势识别应用: {exePath}");
        }
    }

    // 在Inspector中切换模式时调用（仅在编辑器中有效）
    [ContextMenu("切换到调试模式")]
    public void SwitchToDebugMode()
    {
        if (debugMode)
        {
            UnityEngine.Debug.Log("[GestureReceiver] 已经处于调试模式");
            return;
        }

        // 先关闭正式模式的exe进程
        if (gestureAppProcess != null && !gestureAppProcess.HasExited)
        {
            try
            {
                gestureAppProcess.Kill();
                UnityEngine.Debug.Log("[GestureReceiver] 已关闭exe进程，切换到调试模式");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"关闭exe进程时出现错误: {e.Message}");
            }
            gestureAppProcess = null;
        }

        debugMode = true;
        UnityEngine.Debug.Log("[GestureReceiver] 已切换到调试模式");
        if (showDebugInstructions)
        {
            PrintDebugInstructions();
        }
    }

    [ContextMenu("切换到正式模式")]
    public void SwitchToReleaseMode()
    {
        if (!debugMode)
        {
            UnityEngine.Debug.Log("[GestureReceiver] 已经处于正式模式");
            return;
        }

        debugMode = false;
        UnityEngine.Debug.Log("[GestureReceiver] 已切换到正式模式，正在启动exe...");
        
        // 启动exe进程
        StartGestureApp();
    }

    [ContextMenu("显示调试说明")]
    public void PrintDebugInstructions()
    {
        UnityEngine.Debug.Log("=== 调试模式使用说明 ===");
        UnityEngine.Debug.Log("1. 确保Python环境已安装（Python 3.7+）");
        UnityEngine.Debug.Log("2. 安装必需的Python包：");
        UnityEngine.Debug.Log("   pip install opencv-python mediapipe numpy scikit-learn");
        UnityEngine.Debug.Log("3. 打开命令行，进入Gesture目录");
        UnityEngine.Debug.Log("4. 运行Python脚本：");
        UnityEngine.Debug.Log("   python main.py");
        UnityEngine.Debug.Log("   或");
        UnityEngine.Debug.Log("   python gesture_recognition.py");
        UnityEngine.Debug.Log("5. 确保摄像头可用且未被其他程序占用");
        UnityEngine.Debug.Log("6. Python脚本会自动连接到Unity（端口5000和8000）");
        UnityEngine.Debug.Log("===================");
    }

    private void Update()
    {
        // 处理位置数据消息队列
        while (positionMessageQueue.TryDequeue(out string message))
        {
            ProcessPositionMessage(message);
        }

        // 处理手势类型消息队列
        while (gestureMessageQueue.TryDequeue(out string message))
        {
            ProcessGestureMessage(message);
        }
    }

    // 网络连接方法
    public void ConnectAll()
    {
        ConnectPositionReceiver();
        ConnectGestureReceiver();
    }

    public void ConnectPositionReceiver()
    {
        try
        {
            if (positionUdpClient != null) positionUdpClient.Close();

            positionUdpClient = new UdpClient(positionPort);
            positionEndPoint = new IPEndPoint(IPAddress.Any, positionPort);

            isPositionRunning = true;

            if (positionReceiveThread != null && positionReceiveThread.IsAlive)
                positionReceiveThread.Abort();

            positionReceiveThread = new Thread(new ThreadStart(ReceivePositionData));
            positionReceiveThread.IsBackground = true;
            positionReceiveThread.Start();

            isPositionConnected = true;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"位置数据连接错误: {e.Message}");
            isPositionConnected = false;
        }
    }

    public void ConnectGestureReceiver()
    {
        try
        {
            if (gestureUdpClient != null) gestureUdpClient.Close();

            gestureUdpClient = new UdpClient(gesturePort);
            gestureEndPoint = new IPEndPoint(IPAddress.Any, gesturePort);

            isGestureRunning = true;

            if (gestureReceiveThread != null && gestureReceiveThread.IsAlive)
                gestureReceiveThread.Abort();

            gestureReceiveThread = new Thread(new ThreadStart(ReceiveGestureData));
            gestureReceiveThread.IsBackground = true;
            gestureReceiveThread.Start();

            isGestureConnected = true;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"手势数据连接错误: {e.Message}");
            isGestureConnected = false;
        }
    }

    // 接收数据线程方法
    private void ReceivePositionData()
    {
        while (isPositionRunning)
        {
            try
            {
                byte[] data = positionUdpClient.Receive(ref positionEndPoint);
                string message = Encoding.UTF8.GetString(data);
                positionMessageQueue.Enqueue(message);
            }
            catch (Exception) { /* 忽略异常 */ }
        }
    }

    private void ReceiveGestureData()
    {
        while (isGestureRunning)
        {
            try
            {
                byte[] data = gestureUdpClient.Receive(ref gestureEndPoint);
                string message = Encoding.UTF8.GetString(data);
                gestureMessageQueue.Enqueue(message);
            }
            catch (Exception) { /* 忽略异常 */ }
        }
    }

    // 消息处理方法
    private void ProcessPositionMessage(string message)
    {
        string[] parts = message.Split(messageDelimiter[0]);
        if (parts.Length < 5) return;

        try
        {
            if (parts[0] != "position") return;

            int handIndex = int.Parse(parts[1]);
            float x = float.Parse(parts[2]);
            float y = float.Parse(parts[3]);
            float z = float.Parse(parts[4]);

            if (handIndex >= 0)
            {
                Dictionary<string, float> additionalData = new Dictionary<string, float>();
                additionalData.Add("hand_index", handIndex);
                additionalData.Add("depth", z);

                // 解析额外数据
                for (int i = 5; i < parts.Length; i++)
                {
                    string[] keyValue = parts[i].Split(':');
                    if (keyValue.Length == 2 && float.TryParse(keyValue[1], out float value))
                    {
                        additionalData.Add(keyValue[0], value);
                    }
                }

                // 更新手部位置数据（使用新的分离方法）
                if (inputManager != null)
                {
                    inputManager.UpdateHandPosition(new Vector2(x, y), additionalData);
                }
            }
        }
        catch (Exception) { /* 忽略解析错误 */ }
    }

    private void ProcessGestureMessage(string message)
    {
        string[] parts = message.Split(messageDelimiter[0]);
        if (parts.Length < 1) return;

        try
        {
            string messageType = parts[0];
            string gestureType = messageType;

            // 如果消息格式是 "gesture|GestureType"，则提取手势类型
            if (messageType == "gesture" && parts.Length > 1)
            {
                gestureType = parts[1];
            }

            Dictionary<string, float> additionalData = new Dictionary<string, float>();
            int startIndex = (messageType == "gesture") ? 2 : 1;

            for (int i = startIndex; i < parts.Length; i++)
            {
                string[] keyValue = parts[i].Split(':');
                if (keyValue.Length == 2 && float.TryParse(keyValue[1], out float value))
                {
                    additionalData.Add(keyValue[0], value);
                }
            }

            // 处理手部检测状态
            if (messageType == "HandDetectionStatus" || gestureType == "HandDetectionStatus")
            {
                bool detectionStatus = false;
                if (parts.Length > 1)
                {
                    string statusValue = (messageType == "HandDetectionStatus") ? parts[1] : parts[0].Split('|')[1];
                    detectionStatus = statusValue.ToLower() == "true";
                }

                additionalData["detected"] = detectionStatus ? 1.0f : 0.0f;

                if (inputManager != null)
                {
                    inputManager.UpdateGestureData("HandDetectionStatus", Vector2.zero, additionalData);
                }
                return;
            }

            // 验证手势类型是否为有效的手势名称
            if (IsValidGestureType(gestureType))
            {
                // 标记为手势类型消息
                additionalData["is_gesture_type"] = 1.0f;

                // 将手势类型转换为Unity内部使用的大写格式
                string normalizedGestureType = NormalizeGestureType(gestureType);

                // 更新手势类型数据（使用新的分离方法）
                if (inputManager != null)
                {
                    inputManager.UpdateGestureType(normalizedGestureType, additionalData);
                }
                
                UnityEngine.Debug.Log($"[GestureReceiver] 接收到手势类型: {gestureType} -> {normalizedGestureType}");
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[GestureReceiver] 接收到未知手势类型: {gestureType}");
            }
        }
        catch (Exception e) 
        { 
            UnityEngine.Debug.LogError($"[GestureReceiver] 解析手势消息失败: {e.Message}");
        }
    }

    /// <summary>
    /// 验证手势类型是否有效
    /// </summary>
    private bool IsValidGestureType(string gestureType)
    {
        // 定义有效的手势类型（支持网络传输的小写格式和Unity内部的大写格式）
        string[] validGestures = { "bird", "wolf", "fist", "goose", "frog", "owl", "default", "BIRD", "WOLF", "FIST", "GOOSE", "FROG", "OWL", "DEFAULT", "Unknown" };
        
        foreach (string validGesture in validGestures)
        {
            if (string.Equals(gestureType, validGesture, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// 将手势类型标准化为Unity内部使用的大写格式
    /// </summary>
    private string NormalizeGestureType(string gestureType)
    {
        if (string.IsNullOrEmpty(gestureType))
            return "DEFAULT";

        // 转换为大写并处理特殊情况
        string normalized = gestureType.ToUpper();
        
        // 确保使用正确的映射
        switch (normalized)
        {
            case "BIRD":
                return "BIRD";
            case "WOLF":
                return "WOLF";
            case "FIST":
                return "FIST";
            case "GOOSE":
                return "GOOSE";
            case "FROG":
                return "FROG";
            case "OWL":
                return "OWL";
            case "DEFAULT":
                return "DEFAULT";
            case "UNKNOWN":
                return "DEFAULT";  // 未知手势映射为默认
            default:
                return "DEFAULT";
        }
    }

    // 清理资源
    private void OnDestroy()
    {
        // 关闭手势识别进程（仅在正式模式下）
        if (!debugMode && gestureAppProcess != null && !gestureAppProcess.HasExited)
        {
            try 
            { 
                gestureAppProcess.Kill();
                UnityEngine.Debug.Log("[GestureReceiver] 已关闭手势识别进程");
            }
            catch 
            { 
                UnityEngine.Debug.LogWarning("[GestureReceiver] 关闭手势识别进程时出现异常");
            }
            gestureAppProcess = null;
        }
        else if (debugMode)
        {
            UnityEngine.Debug.Log("[GestureReceiver] 调试模式下，请手动关闭Python脚本");
        }

        DisconnectAll();
    }

    public void DisconnectAll()
    {
        DisconnectPositionReceiver();
        DisconnectGestureReceiver();
    }

    public void DisconnectPositionReceiver()
    {
        isPositionRunning = false;

        if (positionReceiveThread != null && positionReceiveThread.IsAlive)
        {
            try { positionReceiveThread.Abort(); }
            catch { /* 忽略异常 */ }
            positionReceiveThread = null;
        }

        if (positionUdpClient != null)
        {
            try { positionUdpClient.Close(); }
            catch { /* 忽略异常 */ }
            positionUdpClient = null;
        }

        isPositionConnected = false;
    }

    public void DisconnectGestureReceiver()
    {
        isGestureRunning = false;

        if (gestureReceiveThread != null && gestureReceiveThread.IsAlive)
        {
            try { gestureReceiveThread.Abort(); }
            catch { /* 忽略异常 */ }
            gestureReceiveThread = null;
        }

        if (gestureUdpClient != null)
        {
            try { gestureUdpClient.Close(); }
            catch { /* 忽略异常 */ }
            gestureUdpClient = null;
        }

        isGestureConnected = false;
    }

    // 公共API
    public bool IsConnected()
    {
        return isPositionConnected && isGestureConnected;
    }

    // 公共方法：供外部脚本调用
    public void SetDebugMode(bool isDebugMode)
    {
        if (isDebugMode)
        {
            SwitchToDebugMode();
        }
        else
        {
            SwitchToReleaseMode();
        }
    }

    // 获取当前模式
    public bool IsDebugMode()
    {
        return debugMode;
    }

    // 获取连接状态信息
    public string GetConnectionStatus()
    {
        string mode = debugMode ? "调试模式" : "正式模式";
        string posStatus = isPositionConnected ? "已连接" : "未连接";
        string gestureStatus = isGestureConnected ? "已连接" : "未连接";
        string processStatus = "";
        
        if (!debugMode)
        {
            if (gestureAppProcess != null && !gestureAppProcess.HasExited)
            {
                processStatus = " (exe运行中)";
            }
            else
            {
                processStatus = " (exe未运行)";
            }
        }
        
        return $"模式: {mode}{processStatus}, 位置端口: {posStatus}, 手势端口: {gestureStatus}";
    }
}