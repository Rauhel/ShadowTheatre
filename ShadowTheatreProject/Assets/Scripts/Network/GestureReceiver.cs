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
        // 新增：自动启动gesture_app.exe
        StartGestureApp();

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

    // 新增：启动StreamingAssets下的gesture_app.exe
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

                // 更新手势数据
                if (inputManager != null)
                {
                    inputManager.UpdateGestureData("position", new Vector2(x, y), additionalData);
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
            string gestureType = (messageType == "gesture" && parts.Length > 1) ? parts[1] : messageType;

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

            // 标记为手势类型消息
            additionalData["is_gesture_type"] = 1.0f;

            // 更新手势数据
            if (inputManager != null)
            {
                inputManager.UpdateGestureData(gestureType, Vector2.zero, additionalData);
            }
        }
        catch (Exception) { /* 忽略解析错误 */ }
    }

    // 清理资源
    private void OnDestroy()
    {
        // 新增：关闭手势识别进程
        if (gestureAppProcess != null && !gestureAppProcess.HasExited)
        {
            try { gestureAppProcess.Kill(); }
            catch { /* 忽略异常 */ }
            gestureAppProcess = null;
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
}