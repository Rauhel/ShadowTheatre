using UnityEngine;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;

public class GestureReceiver : MonoBehaviour
{
    [Header("Network Settings")]
    [SerializeField] private string hostIP = "127.0.0.1";
    [SerializeField] private int port = 8000;
    [SerializeField] private bool autoConnect = true;
    [SerializeField] private bool logMessages = true;

    [Header("Protocol Settings")]
    [SerializeField] private string messageDelimiter = "|";

    // 连接状态
    private bool isConnected = false;
    private bool isRunning = false;
    private UdpClient udpClient;
    private IPEndPoint endPoint;
    private Thread receiveThread;

    // 输入管理器引用
    private InputManager inputManager;

    // 线程安全队列，用于存储接收到的消息
    private ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();

    private void Start()
    {
        Debug.Log("GestureReceiver: 启动中...");

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
            else
            {
                Debug.Log($"已找到InputManager: {inputManager.gameObject.name}");
            }
        }
        else
        {
            Debug.Log("成功获取InputManager单例实例");
        }

        if (autoConnect)
        {
            Connect();
        }
    }

    private void Update()
    {
        // 处理队列中的所有消息
        while (messageQueue.TryDequeue(out string message))
        {
            ProcessMessageInMainThread(message);
        }
    }

    public void Connect()
    {
        try
        {
            // 初始化UDP客户端
            if (udpClient != null)
            {
                udpClient.Close();
            }
            udpClient = new UdpClient(port);
            endPoint = new IPEndPoint(IPAddress.Any, port);

            // 启动接收线程
            isRunning = true;

            // 确保之前的线程已停止
            if (receiveThread != null && receiveThread.IsAlive)
            {
                receiveThread.Abort();
            }

            receiveThread = new Thread(new ThreadStart(ReceiveData));
            receiveThread.IsBackground = true;
            receiveThread.Start();

            isConnected = true;
            Debug.Log($"GestureReceiver: 尝试在端口 {port} 上监听UDP数据包");
        }
        catch (Exception e)
        {
            Debug.LogError("GestureReceiver: 连接错误 - " + e.Message);
            isConnected = false;
        }
    }

    private void ReceiveData()
    {
        Debug.Log("GestureReceiver: 开始监听UDP数据"); // 确认线程已启动

        while (isRunning)
        {
            try
            {
                // 接收数据
                byte[] data = udpClient.Receive(ref endPoint);
                string message = Encoding.UTF8.GetString(data);

                Debug.Log($"GestureReceiver: 接收到消息: {message}"); // 记录每条接收的消息

                // 添加消息到队列，由主线程处理
                messageQueue.Enqueue(message);
            }
            catch (Exception e)
            {
                if (isRunning)
                {
                    Debug.LogError($"GestureReceiver: 接收错误 - {e.Message}");
                }
            }
        }

        Debug.Log("GestureReceiver: 停止监听UDP数据"); // 确认线程已结束
    }

    private void ProcessMessageInMainThread(string message)
    {
        // 期望格式: "gesture_type|x|y|confidence|key1:value1|key2:value2"
        Debug.Log($"主线程处理消息: {message}");

        string[] parts = message.Split(messageDelimiter[0]);

        if (parts.Length < 4)
        {
            Debug.LogWarning("GestureReceiver: 无效消息格式: " + message);
            return;
        }

        try
        {
            string gestureType = parts[0];
            float x = float.Parse(parts[1]);
            float y = float.Parse(parts[2]);
            float confidence = float.Parse(parts[3]);

            Debug.Log($"解析数据: 类型={gestureType}, x={x}, y={y}, 置信度={confidence}");

            // 解析额外数据
            Dictionary<string, float> additionalData = new Dictionary<string, float>();
            for (int i = 4; i < parts.Length; i++)
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

            // 直接在主线程上调用
            Debug.Log($"调用InputManager.UpdateGestureData: {gestureType}, ({x}, {y})");
            inputManager.UpdateGestureData(gestureType, new Vector2(x, y), confidence, additionalData);
        }
        catch (Exception e)
        {
            Debug.LogError($"GestureReceiver: 解析消息错误 - {e.Message}\n堆栈: {e.StackTrace}");
        }
    }

    private void OnDestroy()
    {
        Disconnect();
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }

    public void Disconnect()
    {
        isRunning = false;

        if (receiveThread != null && receiveThread.IsAlive)
        {
            try
            {
                receiveThread.Abort();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"关闭线程时出错: {e.Message}");
            }
            receiveThread = null;
        }

        if (udpClient != null)
        {
            try
            {
                udpClient.Close();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"关闭UDP客户端时出错: {e.Message}");
            }
            udpClient = null;
        }

        isConnected = false;
        if (logMessages) Debug.Log("GestureReceiver: 已断开连接");
    }

    // 公共API: 检查是否已连接
    public bool IsConnected()
    {
        return isConnected;
    }
}