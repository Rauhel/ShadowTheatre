using UnityEngine;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;

/// <summary>
/// 手势接收器：负责从网络接收手势数据并传递给InputManager
/// </summary>
public class GestureReceiver : MonoBehaviour
{
    [Header("Network Settings - Position Data")]
    [SerializeField] private string positionHostIP = "127.0.0.1";
    [SerializeField] private int positionPort = 5000;

    [Header("Network Settings - Gesture Type Data")]
    [SerializeField] private string gestureHostIP = "127.0.0.1";
    [SerializeField] private int gesturePort = 8000;

    [Header("General Settings")]
    [SerializeField] private bool autoConnect = true;
    [SerializeField] private bool logMessages = true;
    [SerializeField] private string messageDelimiter = "|";

    // 位置数据连接状态
    private bool isPositionConnected = false;
    private bool isPositionRunning = false;
    private UdpClient positionUdpClient;
    private IPEndPoint positionEndPoint;
    private Thread positionReceiveThread;

    // 手势类型数据连接状态
    private bool isGestureConnected = false;
    private bool isGestureRunning = false;
    private UdpClient gestureUdpClient;
    private IPEndPoint gestureEndPoint;
    private Thread gestureReceiveThread;

    // 输入管理器引用
    private InputManager inputManager;

    // 线程安全队列，用于存储接收到的消息
    private ConcurrentQueue<string> positionMessageQueue = new ConcurrentQueue<string>();
    private ConcurrentQueue<string> gestureMessageQueue = new ConcurrentQueue<string>();

    private void Start()
    {
        // 尝试获取InputManager引用
        inputManager = InputManager.Instance;

        if (inputManager == null)
        {
            Debug.LogError("无法获取InputManager实例! 请确保场景中有一个InputManager对象。");

            // 尝试找到场景中的InputManager
            inputManager = FindObjectOfType<InputManager>();

            if (inputManager == null)
            {
                Debug.LogError("场景中没有找到InputManager组件!");
            }
        }

        if (autoConnect)
        {
            ConnectAll();
        }
    }

    private void Update()
    {
        // 处理位置数据消息队列
        while (positionMessageQueue.TryDequeue(out string message))
        {
            ProcessPositionMessageInMainThread(message);
        }

        // 处理手势类型消息队列
        while (gestureMessageQueue.TryDequeue(out string message))
        {
            ProcessGestureMessageInMainThread(message);
        }
    }

    public void ConnectAll()
    {
        ConnectPositionReceiver();
        ConnectGestureReceiver();
    }

    public void ConnectPositionReceiver()
    {
        try
        {
            // 初始化UDP客户端
            if (positionUdpClient != null)
            {
                positionUdpClient.Close();
            }
            positionUdpClient = new UdpClient(positionPort);
            positionEndPoint = new IPEndPoint(IPAddress.Any, positionPort);

            // 启动接收线程
            isPositionRunning = true;

            // 确保之前的线程已停止
            if (positionReceiveThread != null && positionReceiveThread.IsAlive)
            {
                positionReceiveThread.Abort();
            }

            positionReceiveThread = new Thread(new ThreadStart(ReceivePositionData));
            positionReceiveThread.IsBackground = true;
            positionReceiveThread.Start();

            isPositionConnected = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"GestureReceiver: 位置数据连接错误 - {e.Message}");
            isPositionConnected = false;
        }
    }

    public void ConnectGestureReceiver()
    {
        try
        {
            // 初始化UDP客户端
            if (gestureUdpClient != null)
            {
                gestureUdpClient.Close();
            }
            gestureUdpClient = new UdpClient(gesturePort);
            gestureEndPoint = new IPEndPoint(IPAddress.Any, gesturePort);

            // 启动接收线程
            isGestureRunning = true;

            // 确保之前的线程已停止
            if (gestureReceiveThread != null && gestureReceiveThread.IsAlive)
            {
                gestureReceiveThread.Abort();
            }

            gestureReceiveThread = new Thread(new ThreadStart(ReceiveGestureData));
            gestureReceiveThread.IsBackground = true;
            gestureReceiveThread.Start();

            isGestureConnected = true;
            Debug.Log($"GestureReceiver: 成功在端口 {gesturePort} 上开始监听手势类型数据");
        }
        catch (Exception e)
        {
            Debug.LogError($"GestureReceiver: 手势类型数据连接错误 - {e.Message}");
            isGestureConnected = false;
        }
    }

    private void ReceivePositionData()
    {
        while (isPositionRunning)
        {
            try
            {
                // 接收数据
                byte[] data = positionUdpClient.Receive(ref positionEndPoint);
                string message = Encoding.UTF8.GetString(data);

                // 添加消息到队列，由主线程处理
                positionMessageQueue.Enqueue(message);
            }
            catch (Exception e)
            {
                if (isPositionRunning)
                {
                    Debug.LogError($"GestureReceiver: 位置数据接收错误 - {e.Message}");
                }
            }
        }
    }

    private void ReceiveGestureData()
    {
        Debug.Log("GestureReceiver: 开始监听手势类型数据");

        while (isGestureRunning)
        {
            try
            {
                // 接收数据
                byte[] data = gestureUdpClient.Receive(ref gestureEndPoint);
                string message = Encoding.UTF8.GetString(data);

                // 记录接收到的消息
                Debug.Log($"GestureReceiver: 接收到原始手势类型消息: {message}");

                // 添加消息到队列，由主线程处理
                gestureMessageQueue.Enqueue(message);
            }
            catch (Exception e)
            {
                if (isGestureRunning)
                {
                    Debug.LogError($"GestureReceiver: 手势类型数据接收错误 - {e.Message}");
                }
            }
        }

        Debug.Log("GestureReceiver: 停止监听手势类型数据");
    }

    private void ProcessPositionMessageInMainThread(string message)
    {
        // 期望格式: "position|hand_idx|x|y|z"
        string[] parts = message.Split(messageDelimiter[0]);

        if (parts.Length < 5)
        {
            return;
        }

        try
        {
            string msgType = parts[0];

            // 确认是位置消息
            if (msgType != "position")
            {
                return;
            }

            int handIndex = int.Parse(parts[1]);
            float x = float.Parse(parts[2]);
            float y = float.Parse(parts[3]);
            float z = float.Parse(parts[4]);

            // 只有当检测到手时才处理（handIndex >= 0）
            if (handIndex >= 0)
            {
                // 配置额外数据
                Dictionary<string, float> additionalData = new Dictionary<string, float>();
                additionalData.Add("hand_index", handIndex);
                additionalData.Add("depth", z);

                // 对于5-N的部分，解析为键值对
                for (int i = 5; i < parts.Length; i++)
                {
                    string[] keyValue = parts[i].Split(':');
                    if (keyValue.Length == 2)
                    {
                        additionalData.Add(keyValue[0], float.Parse(keyValue[1]));
                    }
                }

                // 再次检查InputManager引用是否有效
                if (inputManager == null)
                {
                    Debug.LogError("InputManager引用为空!");
                    inputManager = InputManager.Instance;
                    if (inputManager == null)
                    {
                        Debug.LogError("无法获取InputManager实例!");
                        return;
                    }
                }

                // 调用InputManager更新位置数据
                inputManager.UpdateGestureData("position", new Vector2(x, y), 1.0f, additionalData);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"GestureReceiver: 解析位置消息错误 - {e.Message}\n堆栈: {e.StackTrace}");
        }
    }

    private void ProcessGestureMessageInMainThread(string message)
    {
        // 期望格式: "gesture_type|confidence|key1:value1|key2:value2" 或 "gesture|gesture_type|confidence|key1:value1"
        Debug.Log($"主线程处理手势类型消息: {message}");

        string[] parts = message.Split(messageDelimiter[0]);

        if (parts.Length < 1)
        {
            Debug.LogWarning($"GestureReceiver: 无效的手势类型消息格式: {message}");
            return;
        }

        try
        {
            // 这里处理两种可能格式:
            // 1. "gesture|Bird|0.95|..."  - 第一个部分为消息类型，第二个部分为具体手势类型
            // 2. "Bird|0.95|..."          - 第一个部分直接是手势类型

            string messageType = parts[0];
            string gestureType = (messageType == "gesture" && parts.Length > 1) ? parts[1] : messageType;
            float confidence = 1.0f;

            // 解析置信度
            if (parts.Length > ((messageType == "gesture") ? 2 : 1))
            {
                int confidenceIndex = (messageType == "gesture") ? 2 : 1;
                if (confidenceIndex < parts.Length)
                {
                    if (float.TryParse(parts[confidenceIndex], out float parsedConfidence))
                    {
                        confidence = parsedConfidence;
                    }
                }
            }

            Debug.Log($"【手势识别】: 消息类型={messageType}, 手势类型={gestureType}, 置信度={confidence:F3}");

            // 解析额外数据
            Dictionary<string, float> additionalData = new Dictionary<string, float>();
            int startIndex = (messageType == "gesture") ? 3 : 2;
            for (int i = startIndex; i < parts.Length; i++)
            {
                string[] keyValue = parts[i].Split(':');
                if (keyValue.Length == 2 && float.TryParse(keyValue[1], out float value))
                {
                    additionalData.Add(keyValue[0], value);
                }
            }

            // 检查是否是手部检测状态消息
            if (messageType == "HandDetectionStatus" || gestureType == "HandDetectionStatus")
            {
                bool detectionStatus = false;

                // 解析检测状态
                if (parts.Length > 1)
                {
                    string statusValue = (messageType == "HandDetectionStatus") ? parts[1] : parts[0].Split('|')[1];
                    detectionStatus = statusValue.ToLower() == "true";
                }

                // 添加到附加数据
                additionalData["detected"] = detectionStatus ? 1.0f : 0.0f;

                // 向InputManager传递状态
                inputManager.UpdateGestureData("HandDetectionStatus", Vector2.zero, detectionStatus ? 1.0f : 0.0f, additionalData);

                Debug.Log($"【手势识别】: 手部检测状态: {(detectionStatus ? "检测到手" : "未检测到手")}");
                return;
            }

            // 如果有额外数据，也显示出来
            if (additionalData.Count > 0)
            {
                string extraDataStr = "附加数据: ";
                foreach (var pair in additionalData)
                {
                    extraDataStr += $"{pair.Key}={pair.Value:F3}, ";
                }
                Debug.Log(extraDataStr.TrimEnd(' ', ','));
            }

            // 在额外数据中标记这是手势类型消息
            additionalData["is_gesture_type"] = 1.0f;

            // 再次检查InputManager引用是否有效
            if (inputManager == null)
            {
                Debug.LogError("InputManager引用为空!");
                inputManager = InputManager.Instance;
                if (inputManager == null)
                {
                    Debug.LogError("无法获取InputManager实例!");
                    return;
                }
            }

            Debug.Log($"将手势类型 '{gestureType}' 传递给InputManager.UpdateGestureData");

            // 调用InputManager更新手势类型数据
            inputManager.UpdateGestureData(gestureType, Vector2.zero, confidence, additionalData);

            // 记录不同手势类型的调试信息
            switch (gestureType.ToLower())
            {
                case "bird":
                    Debug.Log("【手势事件】: 检测到鸟形手势!");
                    break;
                case "deer":
                    Debug.Log("【手势事件】: 检测到鹿形手势!");
                    break;
                case "wolf":
                    Debug.Log("【手势事件】: 检测到狼形手势!");
                    break;
                case "sheep":
                    Debug.Log("【手势事件】: 检测到羊形手势!");
                    break;
                case "goose":
                    Debug.Log("【手势事件】: 检测到鹅形手势!");
                    break;
                default:
                    Debug.Log($"【手势事件】: 检测到其他手势: {gestureType}");
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"GestureReceiver: 解析手势类型消息错误 - {e.Message}\n堆栈: {e.StackTrace}");
        }
    }

    private void OnDestroy()
    {
        DisconnectAll();
    }

    private void OnApplicationQuit()
    {
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
            try
            {
                positionReceiveThread.Abort();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"关闭位置数据线程时出错: {e.Message}");
            }
            positionReceiveThread = null;
        }

        if (positionUdpClient != null)
        {
            try
            {
                positionUdpClient.Close();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"关闭位置数据UDP客户端时出错: {e.Message}");
            }
            positionUdpClient = null;
        }

        isPositionConnected = false;
    }

    public void DisconnectGestureReceiver()
    {
        isGestureRunning = false;

        if (gestureReceiveThread != null && gestureReceiveThread.IsAlive)
        {
            try
            {
                gestureReceiveThread.Abort();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"关闭手势类型数据线程时出错: {e.Message}");
            }
            gestureReceiveThread = null;
        }

        if (gestureUdpClient != null)
        {
            try
            {
                gestureUdpClient.Close();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"关闭手势类型数据UDP客户端时出错: {e.Message}");
            }
            gestureUdpClient = null;
        }

        isGestureConnected = false;
        Debug.Log("GestureReceiver: 手势类型数据接收已断开连接");
    }

    // 公共API: 检查连接状态
    public bool IsPositionConnected()
    {
        return isPositionConnected;
    }

    public bool IsGestureConnected()
    {
        return isGestureConnected;
    }

    public bool IsFullyConnected()
    {
        return isPositionConnected && isGestureConnected;
    }

    // 兼容原有API
    public bool IsConnected()
    {
        return isPositionConnected;
    }
}